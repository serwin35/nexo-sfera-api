using System.Globalization;
using InsERT.Moria.Asortymenty;
using InsERT.Moria.CennikiICeny;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Requests;

namespace NexoSferaApi.Helpers;

/// <summary>
/// Typed write paths of price list positions through the price list business object (<c>ICenniki.Znajdz(Cennik)</c>):
/// <c>PUT /api/dictionary/price-lists/by-id/{id}/items</c> sets the gross or net price of main positions
/// (<c>ICennik.Pozycje</c>), <c>POST .../reprice</c> runs Nexo's price change (<c>ICennik.Przecen</c>),
/// <c>POST .../items/recalculate-costs</c> refreshes the cost of positions and keeps their sales prices, and
/// <c>PUT .../items/price-lock</c> switches the price lock on documents (<c>ICennikPozycje.UstawBlokadeCen</c>).
/// </summary>
/// <remarks>
/// <para>
/// Nexo recalculates the other price and the calculation parameter (markup, margin or profit) itself; the calculation
/// method, rounding function and quantity tiers are left as they are. A later change of the base price recalculates the
/// position again, like any price set in Subiekt.
/// </para>
/// <para>
/// All-or-nothing: every position is resolved and checked before the first edit, and all edits are saved with one
/// <c>ICennik.Zapisz()</c>. Must run on the SDK thread.
/// </para>
/// </remarks>
public static class PriceListWriter
{
    public const string Updated = "updated";
    public const string Unchanged = "unchanged";
    public const string Skipped = "skipped";

    /// <summary>Maximum number of items in one request.</summary>
    public const int MaxItems = PriceListReader.MaxProductIds;

    /// <summary>Result of <see cref="SetMainPrices"/>.</summary>
    public sealed class Outcome
    {
        public bool PriceListFound { get; init; } = true;
        public List<PriceListPriceChangeDto> Changes { get; } = new();
        public List<string> Errors { get; } = new();

        /// <summary>Items whose current price differs from <c>expectedPriceGross</c> / <c>expectedPriceNet</c>.</summary>
        public List<string> Conflicts { get; } = new();
    }

    /// <summary>Result of <see cref="Reprice"/>.</summary>
    public sealed class RepriceOutcome
    {
        public bool PriceListFound { get; init; } = true;
        public List<PriceListRepriceChangeDto> Changes { get; } = new();
        public List<string> Errors { get; } = new();
    }

    /// <summary>Result of <see cref="RecalculateCosts"/>.</summary>
    public sealed class CostOutcome
    {
        public bool PriceListFound { get; init; } = true;
        public List<PriceListCostChangeDto> Changes { get; } = new();
        public List<string> Errors { get; } = new();
    }

    /// <summary>Result of <see cref="SetPriceLock"/>.</summary>
    public sealed class PriceLockOutcome
    {
        public bool PriceListFound { get; init; } = true;
        public List<PriceListPriceLockChangeDto> Changes { get; } = new();
        public List<string> Errors { get; } = new();
    }

    private static readonly IReadOnlyDictionary<string, RodzajPrzeceny> RepriceKinds =
        new Dictionary<string, RodzajPrzeceny>(StringComparer.OrdinalIgnoreCase)
        {
            ["decreasePercent"] = RodzajPrzeceny.ObnizkaProcentowa,
            ["increasePercent"] = RodzajPrzeceny.PodwyzkaProcentowa,
            ["decreaseAmount"] = RodzajPrzeceny.ObnizkaKwotowa,
            ["increaseAmount"] = RodzajPrzeceny.PodwyzkaKwotowa,
        };

    /// <summary>Request validation that needs no SDK access. One message per problem, prefixed with the JSON path.</summary>
    public static List<string> Validate(SetPriceListPricesRequest? request)
    {
        var errors = new List<string>();
        var items = request?.Items;
        if (items == null || items.Count == 0)
        {
            errors.Add("items: at least one item is required.");
            return errors;
        }

        if (items.Count > MaxItems)
        {
            errors.Add($"items: at most {MaxItems} items per request, got {items.Count}.");
        }

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item.ProductId <= 0)
            {
                errors.Add($"items[{i}].productId: must be a positive Nexo product id.");
            }

            if (item.PriceGross.HasValue == item.PriceNet.HasValue)
            {
                errors.Add($"items[{i}]: provide exactly one of priceGross or priceNet.");
            }
            else if ((item.PriceGross ?? item.PriceNet) <= 0m)
            {
                errors.Add($"items[{i}]: the price must be greater than zero.");
            }

            if (item.ExpectedPriceGross <= 0m)
            {
                errors.Add($"items[{i}].expectedPriceGross: must be greater than zero.");
            }

            if (item.ExpectedPriceNet <= 0m)
            {
                errors.Add($"items[{i}].expectedPriceNet: must be greater than zero.");
            }
        }

        foreach (var duplicate in items.GroupBy(i => i.ProductId).Where(g => g.Count() > 1))
        {
            errors.Add($"items: product {duplicate.Key} is listed more than once.");
        }

        return errors;
    }

    /// <summary>
    /// Sets the prices of the main positions of <paramref name="items"/> in price list <paramref name="priceListId"/>.
    /// Returns errors (and saves nothing) when a product or its single main position is missing, when the position
    /// price is calculated from kit components, or when Nexo refuses to save. Returns conflicts (and saves nothing) when
    /// an expected price differs from the current one.
    /// </summary>
    public static Outcome SetMainPrices(Uchwyt sfera, int priceListId, IReadOnlyList<PriceListPositionPriceRequest> items)
    {
        var cennik = sfera.Cenniki().Dane.Wszystkie().FirstOrDefault(c => c.Id == priceListId);
        if (cennik == null) return new Outcome { PriceListFound = false };

        var outcome = new Outcome();
        var products = PriceListReader.LoadProducts(sfera, items.Select(i => i.ProductId).ToList());

        using ICennik cennikBo = sfera.Cenniki().Znajdz(cennik);
        var targets = new List<(PriceListPositionPriceRequest Item, IUproszczonaPozycjaCennika Position)>();

        foreach (var item in items)
        {
            if (!products.TryGetValue(item.ProductId, out var asortyment))
            {
                outcome.Errors.Add($"product {item.ProductId}: not found.");
                continue;
            }

            var main = (cennikBo.Pozycje.ZnajdzPozycjeCennika(asortyment) ?? Enumerable.Empty<IUproszczonaPozycjaCennika>())
                .Where(p => p.Glowna)
                .ToList();
            if (main.Count != 1)
            {
                outcome.Errors.Add($"product {item.ProductId}: expected one main position in price list {priceListId}, found {main.Count}.");
                continue;
            }

            if (main[0].WyliczCeneKompletuZeSkladnikow == true && !item.DisableKitComponentPricing)
            {
                outcome.Errors.Add($"product {item.ProductId}: the price is the sum of the kit components " +
                                   "(WyliczCeneKompletuZeSkladnikow); send disableKitComponentPricing: true to set a fixed price.");
                continue;
            }

            var conflict = ExpectedPriceConflict(item, main[0]);
            if (conflict != null)
            {
                outcome.Conflicts.Add(conflict);
                continue;
            }

            targets.Add((item, main[0]));
        }

        if (outcome.Errors.Count > 0 || outcome.Conflicts.Count > 0) return outcome;

        var edited = new List<(PriceListPriceChangeDto Change, IUproszczonaPozycjaCennika Position)>();
        foreach (var (item, position) in targets)
        {
            var precision = Convert.ToInt32(position.PrecyzjaWaluty);
            var change = new PriceListPriceChangeDto
            {
                ProductId = item.ProductId,
                ProductSymbol = SdkMember.Read(() => position.SymbolAsortymentu, null),
                OldPriceNet = position.CenaNetto,
                OldPriceGross = position.CenaBrutto,
            };

            var gross = item.PriceGross.HasValue ? Math.Round(item.PriceGross.Value, precision, MidpointRounding.AwayFromZero) : (decimal?)null;
            var net = item.PriceNet.HasValue ? Math.Round(item.PriceNet.Value, precision, MidpointRounding.AwayFromZero) : (decimal?)null;
            var switchOffKitPricing = item.DisableKitComponentPricing && position.WyliczCeneKompletuZeSkladnikow == true;
            if (!switchOffKitPricing && (gross == position.CenaBrutto || net == position.CenaNetto))
            {
                change.Status = Unchanged;
            }
            else
            {
                bool accepted;
                position.RozpocznijEdycje();
                try
                {
                    if (switchOffKitPricing)
                    {
                        position.WyliczCeneKompletuZeSkladnikow = false;
                        change.KitComponentPricingDisabled = true;
                    }

                    if (gross.HasValue) position.CenaBrutto = gross.Value;
                    else position.CenaNetto = net!.Value;
                }
                finally
                {
                    accepted = position.ZakonczEdycje();
                }

                if (!accepted)
                {
                    // Nothing is saved: disposing the business object discards the edits made so far.
                    outcome.Errors.Add($"product {item.ProductId}: Nexo rejected the price edit of the position.");
                    return outcome;
                }

                change.Status = Updated;
            }

            edited.Add((change, position));
        }

        if (edited.Any(e => e.Change.Status == Updated) && !cennikBo.Zapisz())
        {
            outcome.Errors.AddRange(SaveErrors(cennikBo));
            if (outcome.Errors.Count == 0) outcome.Errors.Add("Nexo refused to save the price list.");
            return outcome;
        }

        foreach (var (change, position) in edited)
        {
            change.PriceNet = position.CenaNetto;
            change.PriceGross = position.CenaBrutto;
            change.CalculationParameter = position.ParametrKalkulacyjny;
            outcome.Changes.Add(change);
        }

        return outcome;
    }

    /// <summary>Validation of a price change request that needs no SDK access, one message per problem.</summary>
    public static List<string> ValidateReprice(RepricePriceListRequest? request)
    {
        var errors = new List<string>();
        var productIds = request?.ProductIds;
        if (productIds == null || productIds.Count == 0)
        {
            errors.Add("productIds: at least one product id is required.");
        }
        else
        {
            if (productIds.Count > MaxItems) errors.Add($"productIds: at most {MaxItems} ids per request, got {productIds.Count}.");
            if (productIds.Any(id => id <= 0)) errors.Add("productIds: every id must be a positive Nexo product id.");
            if (productIds.Distinct().Count() != productIds.Count) errors.Add("productIds: every product must be listed once.");
        }

        var kind = request?.Kind;
        if (kind == null || !RepriceKinds.ContainsKey(kind))
        {
            errors.Add($"kind: must be one of {string.Join(", ", RepriceKinds.Keys)}.");
        }

        var value = request?.Value;
        if (value is not > 0m)
        {
            errors.Add("value: must be greater than zero.");
        }
        else if (string.Equals(kind, "decreasePercent", StringComparison.OrdinalIgnoreCase) && value >= 1m)
        {
            errors.Add("value: a percent decrease is a fraction below 1 (0.10 = 10%).");
        }
        else if (string.Equals(kind, "increasePercent", StringComparison.OrdinalIgnoreCase) && value > 1m)
        {
            errors.Add("value: a percent increase is a fraction up to 1 (0.10 = 10%).");
        }

        if (request?.RoundingFunction is { } rounding && !PriceListReader.TryGetRoundingFunctionId(rounding.Trim(), out _))
        {
            errors.Add($"roundingFunction: unknown, use one of {string.Join(", ", PriceListReader.RoundingFunctionNameList())}.");
        }

        if (request?.PriceCorrection == 0m)
        {
            errors.Add("priceCorrection: must not be zero; omit it for no correction.");
        }

        return errors;
    }

    /// <summary>
    /// Runs <c>ICennik.Przecen</c> on the positions of <c>request.ProductIds</c> in price list
    /// <paramref name="priceListId"/> (main positions and, unless <c>MainOnly</c>, quantity tiers) and saves once.
    /// Returns errors (and saves nothing) for a closed price list, a missing product or position, a kit position priced
    /// from its components, a position Nexo could not reprice, or a refused save. Call <see cref="ValidateReprice"/> first.
    /// </summary>
    public static RepriceOutcome Reprice(Uchwyt sfera, int priceListId, RepricePriceListRequest request)
    {
        var cennik = sfera.Cenniki().Dane.Wszystkie().FirstOrDefault(c => c.Id == priceListId);
        if (cennik == null) return new RepriceOutcome { PriceListFound = false };

        var outcome = new RepriceOutcome();
        if (cennik.Status == (int)StatusCennika.Zamkniety)
        {
            outcome.Errors.Add($"price list {priceListId}: the price list is closed.");
            return outcome;
        }

        var products = PriceListReader.LoadProducts(sfera, request.ProductIds);

        using ICennik cennikBo = sfera.Cenniki().Znajdz(cennik);
        var targets = new List<IUproszczonaPozycjaCennika>();

        foreach (var productId in request.ProductIds)
        {
            if (!products.TryGetValue(productId, out var asortyment))
            {
                outcome.Errors.Add($"product {productId}: not found.");
                continue;
            }

            var positions = (cennikBo.Pozycje.ZnajdzPozycjeCennika(asortyment) ?? Enumerable.Empty<IUproszczonaPozycjaCennika>())
                .Where(p => !request.MainOnly || p.Glowna)
                .ToList();
            if (positions.Count == 0)
            {
                outcome.Errors.Add($"product {productId}: no position in price list {priceListId}.");
                continue;
            }

            if (positions.Any(p => p.WyliczCeneKompletuZeSkladnikow))
            {
                outcome.Errors.Add($"product {productId}: the price is calculated from kit components " +
                                   "(WyliczCeneKompletuZeSkladnikow); switch that off in Subiekt first.");
                continue;
            }

            targets.AddRange(positions);
        }

        if (outcome.Errors.Count > 0) return outcome;

        var before = targets.Select(p => (Position: p, Net: p.CenaNetto, Gross: p.CenaBrutto)).ToList();
        Guid? rounding = request.RoundingFunction != null && PriceListReader.TryGetRoundingFunctionId(request.RoundingFunction.Trim(), out var roundingId)
            ? roundingId
            : null;
        bool? correctionSign = request.PriceCorrection.HasValue ? request.PriceCorrection.Value > 0m : null;
        decimal? correction = request.PriceCorrection.HasValue ? Math.Abs(request.PriceCorrection.Value) : null;

        var failures = cennikBo.Przecen(targets, RepriceKinds[request.Kind!], request.Value!.Value, rounding,
            correctionSign, correction, request.ResetDefaultDiscount);
        if (failures is { Count: > 0 })
        {
            foreach (var (position, message) in failures)
            {
                outcome.Errors.Add($"{Describe(position)}: {message}");
            }

            return outcome;
        }

        if (!cennikBo.Zapisz())
        {
            outcome.Errors.AddRange(SaveErrors(cennikBo));
            if (outcome.Errors.Count == 0) outcome.Errors.Add("Nexo refused to save the price list.");
            return outcome;
        }

        foreach (var (position, oldNet, oldGross) in before)
        {
            outcome.Changes.Add(new PriceListRepriceChangeDto
            {
                ProductId = position.IdAsortymentu,
                ProductSymbol = SdkMember.Read(() => position.SymbolAsortymentu, null),
                IsMain = position.Glowna,
                MinQuantity = position.IloscMinAsortymentu,
                OldPriceNet = oldNet,
                OldPriceGross = oldGross,
                PriceNet = position.CenaNetto,
                PriceGross = position.CenaBrutto,
                CalculationParameter = position.ParametrKalkulacyjny,
                Status = position.CenaNetto == oldNet && position.CenaBrutto == oldGross ? Unchanged : Updated,
            });
        }

        return outcome;
    }

    /// <summary>Validation of a cost refresh request that needs no SDK access, one message per problem.</summary>
    public static List<string> ValidateRecalculateCosts(RecalculatePriceListCostsRequest? request)
    {
        var errors = new List<string>();
        var productIds = request?.ProductIds ?? new List<int>();
        var componentIds = request?.ComponentProductIds ?? new List<int>();
        if (productIds.Count == 0 && componentIds.Count == 0)
        {
            errors.Add("productIds: at least one product id (or a componentProductIds entry) is required.");
            return errors;
        }

        if (productIds.Count > MaxItems) errors.Add($"productIds: at most {MaxItems} ids per request, got {productIds.Count}.");
        if (productIds.Any(id => id <= 0)) errors.Add("productIds: every id must be a positive Nexo product id.");
        if (productIds.Distinct().Count() != productIds.Count) errors.Add("productIds: every product must be listed once.");
        if (componentIds.Count > MaxItems) errors.Add($"componentProductIds: at most {MaxItems} ids per request, got {componentIds.Count}.");
        if (componentIds.Any(id => id <= 0)) errors.Add("componentProductIds: every id must be a positive Nexo product id.");
        if (componentIds.Distinct().Count() != componentIds.Count) errors.Add("componentProductIds: every product must be listed once.");
        if (request?.SkipIncompleteKits == true && request.RecordPriceFromComponents != true)
            errors.Add("skipIncompleteKits: only applies together with recordPriceFromComponents.");
        if (request?.Mode is { } mode && !CostModes.Contains(mode.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            errors.Add($"mode: must be one of {string.Join(", ", CostModes)}.");
        }

        return errors;
    }

    private const string CostModeFull = "full";
    private const string CostModeEstimate = "estimate";
    private const string CostModeCalculation = "calculation";

    /// <summary>Modes of <see cref="RecalculateCosts"/>; the first is the default.</summary>
    public static readonly IReadOnlyList<string> CostModes = new[] { CostModeFull, CostModeEstimate, CostModeCalculation };

    private static string NormalizeCostMode(string? mode) =>
        string.IsNullOrWhiteSpace(mode) ? CostModeFull : mode.Trim().ToLowerInvariant();

    /// <summary>
    /// Refreshes the cost of the main positions of <c>request.ProductIds</c> in price list <paramref name="priceListId"/>
    /// and keeps their sales prices; Nexo recalculates margin, markup and profit. With <c>LockPrices</c> the price is also
    /// locked on documents (<c>UstawBlokadeCen</c>). Modes: <c>full</c> recalculates the whole position from its base price
    /// (<c>AktualizujPozycjeCennika</c>; a kit's base price is the value of its components), <c>estimate</c> switches on
    /// <c>SzacujKosztKompletuZeSkladnikow</c> for kits and recalculates the estimated cost
    /// (<c>PrzeliczSzacunkowyKoszt</c>), <c>calculation</c> only recalculates the calculation price from the current base
    /// price (<c>AktualizujCeneKalkulacyjna</c>, which does not refresh the base price). Returns errors (and saves nothing) for a closed price list, a missing
    /// product or main position, a position priced from its kit components, a sales price that Nexo would change, or a
    /// refused save. With <c>DryRun</c> nothing is saved. Call <see cref="ValidateRecalculateCosts"/> first.
    /// </summary>
    public static CostOutcome RecalculateCosts(Uchwyt sfera, int priceListId, RecalculatePriceListCostsRequest request)
    {
        var cennik = sfera.Cenniki().Dane.Wszystkie().FirstOrDefault(c => c.Id == priceListId);
        if (cennik == null) return new CostOutcome { PriceListFound = false };

        var outcome = new CostOutcome();
        if (cennik.Status == (int)StatusCennika.Zamkniety)
        {
            outcome.Errors.Add($"price list {priceListId}: the price list is closed.");
            return outcome;
        }

        var productIds = ResolveCostProductIds(sfera, request, outcome.Errors);
        if (outcome.Errors.Count > 0) return outcome;

        Dictionary<int, KitRecordPrice>? recordPrices = null;
        var skipped = new List<PriceListCostChangeDto>();
        if (request.RecordPriceFromComponents)
        {
            recordPrices = KitRecordPrices(sfera, productIds);
            foreach (var kit in recordPrices.Values.Where(k => k.Reason != null))
            {
                if (!request.SkipIncompleteKits)
                {
                    outcome.Errors.Add($"product {kit.ProductId} ({kit.Symbol}): {kit.Reason}; send skipIncompleteKits to leave it out.");
                    continue;
                }

                skipped.Add(new PriceListCostChangeDto
                {
                    ProductId = kit.ProductId,
                    ProductSymbol = kit.Symbol,
                    Status = Skipped,
                    OldRecordPrice = kit.OldRecordPrice,
                    Reason = kit.Reason,
                });
                productIds.Remove(kit.ProductId);
            }

            if (outcome.Errors.Count > 0) return outcome;
            if (productIds.Count == 0)
            {
                outcome.Changes.AddRange(skipped);
                return outcome;
            }
        }

        var products = PriceListReader.LoadProducts(sfera, productIds);

        // Every position is checked before a kit card is saved, so a refused request changes nothing.
        using (ICennik check = sfera.Cenniki().Znajdz(cennik))
        {
            ResolveCostTargets(check, products, productIds, priceListId, outcome.Errors);
        }

        if (outcome.Errors.Count > 0) return outcome;

        if (recordPrices != null && !request.DryRun)
        {
            SaveKitRecordPrices(sfera, recordPrices.Values.Where(k => k.Reason == null && productIds.Contains(k.ProductId)).ToList(), outcome.Errors);
            if (outcome.Errors.Count > 0) return outcome;
        }

        // Opened after the kit cards are saved: a kit's base price is read from its registry price.
        using ICennik cennikBo = sfera.Cenniki().Znajdz(cennik);
        var targets = ResolveCostTargets(cennikBo, products, productIds, priceListId, outcome.Errors);

        if (outcome.Errors.Count > 0) return outcome;

        var before = targets.Select(p => new PriceListCostChangeDto
        {
            ProductId = p.IdAsortymentu,
            ProductSymbol = SdkMember.Read(() => p.SymbolAsortymentu, null),
            PriceNet = p.CenaNetto,
            PriceGross = p.CenaBrutto,
            OldBasePrice = p.CenaBazowa,
            OldCalculationPrice = p.CenaKalkulacyjna,
            OldEstimatedCost = p.SzacowanyKoszt,
            OldMargin = SdkMember.Read(() => (decimal?)p.Marza, null),
            OldProfit = p.Zysk,
        }).ToList();

        if (request.LockPrices)
        {
            // Blocks price changes and discounts on document lines. The sales price is kept by the check below, not by this.
            cennikBo.Pozycje.UstawBlokadeCen(true, targets);
        }

        switch (NormalizeCostMode(request.Mode))
        {
            case CostModeEstimate:
                foreach (var position in targets.Where(p => p.CzyKomplet && !p.SzacujKosztKompletuZeSkladnikow))
                {
                    position.RozpocznijEdycje();
                    try
                    {
                        position.SzacujKosztKompletuZeSkladnikow = true;
                    }
                    finally
                    {
                        if (!position.ZakonczEdycje()) outcome.Errors.Add($"{Describe(position)}: Nexo rejected the cost estimation switch.");
                    }
                }

                if (outcome.Errors.Count > 0) return outcome;
                foreach (var position in targets) position.PrzeliczSzacunkowyKoszt();
                cennikBo.Pozycje.AktualizujCeneKalkulacyjna(targets);
                break;
            case CostModeCalculation:
                cennikBo.Pozycje.AktualizujCeneKalkulacyjna(targets);
                break;
            default:
                cennikBo.Pozycje.AktualizujPozycjeCennika(targets);
                break;
        }

        for (var i = 0; i < targets.Count; i++)
        {
            if (targets[i].CenaNetto != before[i].PriceNet || targets[i].CenaBrutto != before[i].PriceGross)
            {
                // Nothing is saved: disposing the business object discards the recalculation.
                outcome.Errors.Add($"{Describe(targets[i])}: Nexo would change the sales price from {Format(before[i].PriceGross)} " +
                                   $"to {Format(targets[i].CenaBrutto)} gross; nothing was saved.");
            }
        }

        if (outcome.Errors.Count > 0) return outcome;

        if (!request.DryRun && !cennikBo.Zapisz())
        {
            outcome.Errors.AddRange(SaveErrors(cennikBo));
            if (outcome.Errors.Count == 0) outcome.Errors.Add("Nexo refused to save the price list.");
            return outcome;
        }

        for (var i = 0; i < targets.Count; i++)
        {
            var change = before[i];
            var position = targets[i];
            change.BasePrice = position.CenaBazowa;
            change.CalculationPrice = position.CenaKalkulacyjna;
            change.EstimatedCost = position.SzacowanyKoszt;
            change.Margin = SdkMember.Read(() => (decimal?)position.Marza, null);
            change.Profit = position.Zysk;
            change.PriceLocked = position.CenaSztywnaNaDokumencie;
            change.Status = change.CalculationPrice == change.OldCalculationPrice && change.BasePrice == change.OldBasePrice
                ? Unchanged
                : Updated;
            if (recordPrices != null && recordPrices.TryGetValue(change.ProductId, out var kit) && kit.RecordPrice.HasValue)
            {
                change.OldRecordPrice = kit.OldRecordPrice;
                change.RecordPrice = kit.RecordPrice;
                change.ProjectedMargin = change.PriceNet > 0
                    ? Math.Round((change.PriceNet - kit.RecordPrice.Value) / change.PriceNet, 4, MidpointRounding.AwayFromZero)
                    : null;
                if (kit.RecordPrice != kit.OldRecordPrice) change.Status = Updated;
            }

            outcome.Changes.Add(change);
        }

        outcome.Changes.AddRange(skipped);
        return outcome;
    }

    /// <summary>A kit's registry price computed from its components, or the reason it cannot be.</summary>
    private sealed record KitRecordPrice(int ProductId, string? Symbol, decimal OldRecordPrice, decimal? RecordPrice, string? Reason);

    /// <summary>
    /// <c>ProductIds</c> plus every kit containing one of <c>ComponentProductIds</c> (one SQL query), in request order.
    /// More than <see cref="MaxItems"/> products, or components no kit contains, is an error.
    /// </summary>
    private static List<int> ResolveCostProductIds(Uchwyt sfera, RecalculatePriceListCostsRequest request, List<string> errors)
    {
        var ids = new List<int>(request.ProductIds ?? new List<int>());
        var componentIds = request.ComponentProductIds ?? new List<int>();
        if (componentIds.Count > 0)
        {
            var kits = sfera.Asortymenty().Dane.Wszystkie()
                .Where(a => a.SkladnikiKompletu.Any(s => componentIds.Contains(s.Skladnik.Id)))
                .Select(a => a.Id)
                .OrderBy(id => id)
                .ToList();
            if (kits.Count == 0) errors.Add("componentProductIds: no kit contains these components.");
            ids.AddRange(kits.Where(id => !ids.Contains(id)));
        }

        if (ids.Count > MaxItems)
            errors.Add($"products: {ids.Count} to refresh (productIds and the kits of componentProductIds), at most {MaxItems} per request.");
        return ids;
    }

    /// <summary>
    /// The registry price of each kit among <paramref name="productIds"/>: the sum of quantity x registry price of its
    /// components, rounded to 2 decimals (SQL projection, no lazy loading). Products without components are not listed.
    /// A component counted in another unit than its base unit, or without a registry price, makes the kit incomplete.
    /// </summary>
    private static Dictionary<int, KitRecordPrice> KitRecordPrices(Uchwyt sfera, List<int> productIds)
    {
        var rows = sfera.Asortymenty().Dane.Wszystkie()
            .Where(a => productIds.Contains(a.Id) && a.SkladnikiKompletu.Any())
            .Select(a => new
            {
                a.Id,
                a.Symbol,
                a.CenaEwidencyjna,
                Components = a.SkladnikiKompletu.Select(s => new
                {
                    s.Ilosc,
                    Symbol = s.Skladnik.Symbol,
                    RecordPrice = s.Skladnik.CenaEwidencyjna,
                    UnitId = s.JednostkaMiaryAsortymentu == null ? (int?)null : s.JednostkaMiaryAsortymentu.Id,
                    BaseUnitId = s.Skladnik.PodstawowaJednostkaMiaryAsortymentu == null ? (int?)null : s.Skladnik.PodstawowaJednostkaMiaryAsortymentu.Id,
                }),
            })
            .ToList();

        var result = new Dictionary<int, KitRecordPrice>();
        foreach (var row in rows)
        {
            var components = row.Components.ToList();
            var otherUnit = components.FirstOrDefault(c => c.UnitId != null && c.BaseUnitId != null && c.UnitId != c.BaseUnitId);
            var unpriced = components.FirstOrDefault(c => c.RecordPrice <= 0m);
            string? reason = otherUnit != null
                ? $"component {otherUnit.Symbol} is counted in a unit other than its base unit, so its cost is not computed here"
                : unpriced != null
                    ? $"component {unpriced.Symbol} has no registry price, so the sum would understate the cost"
                    : null;
            var recordPrice = reason == null
                ? Math.Round(components.Sum(c => c.Ilosc * c.RecordPrice), 2, MidpointRounding.AwayFromZero)
                : (decimal?)null;
            result[row.Id] = new KitRecordPrice(row.Id, row.Symbol, row.CenaEwidencyjna, recordPrice, reason);
        }

        return result;
    }

    /// <summary>
    /// Saves the computed registry price on each kit card that changes (<c>IAsortyment</c>, one save per card). Stops at
    /// the first card Nexo refuses; the cards saved before it are named in the error.
    /// </summary>
    private static void SaveKitRecordPrices(Uchwyt sfera, List<KitRecordPrice> kits, List<string> errors)
    {
        var asortymenty = sfera.Asortymenty();
        var saved = new List<string>();
        foreach (var kit in kits.Where(k => k.RecordPrice.HasValue && k.RecordPrice != k.OldRecordPrice))
        {
            var entity = asortymenty.Dane.Wszystkie().First(a => a.Id == kit.ProductId);
            using var product = asortymenty.Znajdz(entity);
            product.Dane.CenaEwidencyjna = kit.RecordPrice!.Value;
            if (product.Zapisz())
            {
                saved.Add(kit.Symbol ?? kit.ProductId.ToString());
                continue;
            }

            var sdkErrors = SdkErrors.Of(product);
            errors.Add($"product {kit.ProductId} ({kit.Symbol}): Nexo refused the registry price {Format(kit.RecordPrice!.Value)} " +
                       $"({(sdkErrors.Count > 0 ? string.Join("; ", sdkErrors) : "no details")}). The price list was not changed" +
                       (saved.Count > 0 ? $"; registry prices already saved: {string.Join(", ", saved)}." : "."));
            return;
        }
    }

    /// <summary>
    /// The main position of each product in the price list, or an error for a missing product, a product without exactly
    /// one main position, or a position priced from its kit components.
    /// </summary>
    private static List<IUproszczonaPozycjaCennika> ResolveCostTargets(
        ICennik cennikBo, Dictionary<int, Asortyment> products, List<int> productIds, int priceListId, List<string> errors)
    {
        var targets = new List<IUproszczonaPozycjaCennika>();
        foreach (var position in ResolveMainPositions(cennikBo, products, productIds, priceListId, errors))
        {
            if (position.WyliczCeneKompletuZeSkladnikow)
            {
                // The sales price follows the components there, so a fixed price with a margin does not apply.
                errors.Add($"product {position.IdAsortymentu}: the price is calculated from kit components " +
                           "(WyliczCeneKompletuZeSkladnikow); set a fixed price first (disableKitComponentPricing).");
                continue;
            }

            targets.Add(position);
        }

        return targets;
    }

    /// <summary>The single main position of each product in request order; a missing product or position is an error.</summary>
    private static List<IUproszczonaPozycjaCennika> ResolveMainPositions(
        ICennik cennikBo, Dictionary<int, Asortyment> products, List<int> productIds, int priceListId, List<string> errors)
    {
        var positions = new List<IUproszczonaPozycjaCennika>();
        foreach (var productId in productIds)
        {
            if (!products.TryGetValue(productId, out var asortyment))
            {
                errors.Add($"product {productId}: not found.");
                continue;
            }

            var main = (cennikBo.Pozycje.ZnajdzPozycjeCennika(asortyment) ?? Enumerable.Empty<IUproszczonaPozycjaCennika>())
                .Where(p => p.Glowna)
                .ToList();
            if (main.Count != 1)
            {
                errors.Add($"product {productId}: expected one main position in price list {priceListId}, found {main.Count}.");
                continue;
            }

            positions.Add(main[0]);
        }

        return positions;
    }

    /// <summary>Validation of a price lock request that needs no SDK access, one message per problem.</summary>
    public static List<string> ValidatePriceLock(SetPriceListPriceLockRequest? request)
    {
        var errors = new List<string>();
        var productIds = request?.ProductIds ?? new List<int>();
        if (productIds.Count == 0) errors.Add("productIds: at least one product id is required.");
        if (productIds.Count > MaxItems) errors.Add($"productIds: at most {MaxItems} ids per request, got {productIds.Count}.");
        if (productIds.Any(id => id <= 0)) errors.Add("productIds: every id must be a positive Nexo product id.");
        if (productIds.Distinct().Count() != productIds.Count) errors.Add("productIds: every product must be listed once.");
        if (request?.Locked == null) errors.Add("locked: required, true locks the price on documents and false unlocks it.");
        return errors;
    }

    /// <summary>
    /// Switches the price lock on documents (<c>CenaSztywnaNaDokumencie</c>) of the main positions of
    /// <c>request.ProductIds</c> in price list <paramref name="priceListId"/> through <c>ICennikPozycje.UstawBlokadeCen</c>.
    /// Positions that already have the requested lock are reported as <c>unchanged</c>. Returns errors (and saves nothing)
    /// for a closed price list, a missing product or main position, a position Nexo did not switch, a sales price that
    /// would change, or a refused save. With <c>DryRun</c> nothing is saved. Call <see cref="ValidatePriceLock"/> first.
    /// </summary>
    public static PriceLockOutcome SetPriceLock(Uchwyt sfera, int priceListId, SetPriceListPriceLockRequest request)
    {
        var cennik = sfera.Cenniki().Dane.Wszystkie().FirstOrDefault(c => c.Id == priceListId);
        if (cennik == null) return new PriceLockOutcome { PriceListFound = false };

        var outcome = new PriceLockOutcome();
        if (cennik.Status == (int)StatusCennika.Zamkniety)
        {
            outcome.Errors.Add($"price list {priceListId}: the price list is closed.");
            return outcome;
        }

        var locked = request.Locked!.Value;
        var products = PriceListReader.LoadProducts(sfera, request.ProductIds);

        using ICennik cennikBo = sfera.Cenniki().Znajdz(cennik);
        var targets = ResolveMainPositions(cennikBo, products, request.ProductIds, priceListId, outcome.Errors);
        if (outcome.Errors.Count > 0) return outcome;

        var changes = targets.Select(p => new PriceListPriceLockChangeDto
        {
            ProductId = p.IdAsortymentu,
            ProductSymbol = SdkMember.Read(() => p.SymbolAsortymentu, null),
            Status = p.CenaSztywnaNaDokumencie == locked ? Unchanged : Updated,
            OldLocked = p.CenaSztywnaNaDokumencie,
            Locked = locked,
            PriceNet = p.CenaNetto,
            PriceGross = p.CenaBrutto,
        }).ToList();

        var switched = targets.Where(p => p.CenaSztywnaNaDokumencie != locked).ToList();
        if (switched.Count > 0)
        {
            cennikBo.Pozycje.UstawBlokadeCen(locked, switched);

            for (var i = 0; i < targets.Count; i++)
            {
                if (targets[i].CenaSztywnaNaDokumencie != locked)
                {
                    outcome.Errors.Add($"{Describe(targets[i])}: Nexo did not switch the price lock; nothing was saved.");
                }

                if (targets[i].CenaNetto != changes[i].PriceNet || targets[i].CenaBrutto != changes[i].PriceGross)
                {
                    // Nothing is saved: disposing the business object discards the switch.
                    outcome.Errors.Add($"{Describe(targets[i])}: Nexo would change the sales price from {Format(changes[i].PriceGross)} " +
                                       $"to {Format(targets[i].CenaBrutto)} gross; nothing was saved.");
                }
            }

            if (outcome.Errors.Count > 0) return outcome;

            if (!request.DryRun && !cennikBo.Zapisz())
            {
                outcome.Errors.AddRange(SaveErrors(cennikBo));
                if (outcome.Errors.Count == 0) outcome.Errors.Add("Nexo refused to save the price list.");
                return outcome;
            }
        }

        outcome.Changes.AddRange(changes);
        return outcome;
    }

    /// <summary>A conflict message when an expected price differs from the current price, otherwise <c>null</c>.</summary>
    private static string? ExpectedPriceConflict(PriceListPositionPriceRequest item, IUproszczonaPozycjaCennika position)
    {
        var precision = Convert.ToInt32(position.PrecyzjaWaluty);
        var mismatches = new List<string>();
        if (item.ExpectedPriceGross.HasValue
            && Math.Round(item.ExpectedPriceGross.Value, precision, MidpointRounding.AwayFromZero) != position.CenaBrutto)
        {
            mismatches.Add($"expectedPriceGross {Format(item.ExpectedPriceGross.Value)}, current {Format(position.CenaBrutto)}");
        }

        if (item.ExpectedPriceNet.HasValue
            && Math.Round(item.ExpectedPriceNet.Value, precision, MidpointRounding.AwayFromZero) != position.CenaNetto)
        {
            mismatches.Add($"expectedPriceNet {Format(item.ExpectedPriceNet.Value)}, current {Format(position.CenaNetto)}");
        }

        return mismatches.Count == 0 ? null : $"product {item.ProductId}: the price changed in Nexo ({string.Join("; ", mismatches)}).";
    }

    private static string Describe(IUproszczonaPozycjaCennika position)
    {
        var symbol = SdkMember.Read(() => position.SymbolAsortymentu, null);
        var tier = position.Glowna ? string.Empty : $", tier from {Format(position.IloscMinAsortymentu)}";
        return $"product {position.IdAsortymentu} ({symbol}{tier})";
    }

    private static string Format(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Validation errors of a business object after a failed <c>Zapisz()</c>: the typed <c>IObiektBiznesowy.Bledy</c>
    /// (<see cref="SdkErrors"/>), then <c>InvalidData[].Errors</c> plus the <c>PodajBledy()</c> text. <c>PodajBledy</c> is not part of the compiled <c>ICennik</c> interface, so both are read
    /// late-bound, best effort, as in the other controllers.
    /// </summary>
    internal static List<string> SaveErrors(dynamic businessObject)
    {
        var errors = new List<string>();
        errors.AddRange(SdkErrors.Of((InsERT.Mox.ObiektyBiznesowe.IObiektBiznesowy)businessObject));

        try
        {
            var invalidData = DynamicPropertyHelper.GetProperty(businessObject, "InvalidData");
            if (invalidData != null)
            {
                foreach (var entity in invalidData)
                {
                    var entityErrors = DynamicPropertyHelper.GetProperty(entity, "Errors");
                    if (entityErrors == null) continue;
                    foreach (var error in entityErrors)
                    {
                        string? text = error?.ToString();
                        if (!string.IsNullOrWhiteSpace(text) && !errors.Contains(text.Trim())) errors.Add(text.Trim());
                    }
                }
            }
        }
        catch
        {
            // best effort
        }

        try
        {
            string? text = businessObject.PodajBledy()?.ToString();
            foreach (var line in (text ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!errors.Contains(line)) errors.Add(line);
            }
        }
        catch
        {
            // PodajBledy not available on this object
        }

        return errors;
    }
}
