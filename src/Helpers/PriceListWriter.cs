using InsERT.Moria.Asortymenty;
using InsERT.Moria.CennikiICeny;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Requests;

namespace NexoSferaApi.Helpers;

/// <summary>
/// Typed write path of <c>PUT /api/dictionary/price-lists/by-id/{id}/items</c>: sets the gross or net price of main price
/// list positions through the price list business object (<c>ICenniki.Znajdz(Cennik)</c> → <c>ICennik.Pozycje</c>).
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
    }

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
    /// price is calculated from kit components, or when Nexo refuses to save.
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

            targets.Add((item, main[0]));
        }

        if (outcome.Errors.Count > 0) return outcome;

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
                position.RozpocznijEdycje();
                try
                {
                    if (gross.HasValue) position.CenaBrutto = gross.Value;
                    else position.CenaNetto = net!.Value;
                }
                finally
                {
                    position.ZakonczEdycje();
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

    /// <summary>
    /// Validation errors of a business object after a failed <c>Zapisz()</c>: <c>InvalidData[].Errors</c> plus the
    /// <c>PodajBledy()</c> text. <c>PodajBledy</c> is not part of the compiled <c>ICennik</c> interface, so both are read
    /// late-bound, best effort, as in the other controllers.
    /// </summary>
    internal static List<string> SaveErrors(dynamic businessObject)
    {
        var errors = new List<string>();
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
