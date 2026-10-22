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
/// (<c>ICennik.Pozycje</c>), <c>POST .../reprice</c> runs Nexo's price change (<c>ICennik.Przecen</c>).
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

            if (main[0].WyliczCeneKompletuZeSkladnikow == true)
            {
                outcome.Errors.Add($"product {item.ProductId}: the price is calculated from kit components " +
                                   "(WyliczCeneKompletuZeSkladnikow); switch that off in Subiekt first.");
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
            if (gross == position.CenaBrutto || net == position.CenaNetto)
            {
                change.Status = Unchanged;
            }
            else
            {
                bool accepted;
                position.RozpocznijEdycje();
                try
                {
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
    private static List<string> SaveErrors(dynamic businessObject)
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
