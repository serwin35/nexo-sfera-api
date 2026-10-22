using InsERT.Moria.CennikiICeny;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using NexoSferaApi.Models.Requests;

namespace NexoSferaApi.Helpers;

/// <summary>
/// Typed creation of additional price lists and their status changes, following the SDK sample
/// <c>PrzykladyCenniki/ObslugaCennikow.DodawanieCennikaDodatkowego</c>: <c>ICenniki.Utworz()</c>, <c>Dane.Tytul</c>,
/// <c>UstawJakoDodatkowy(main)</c>, positions, optional <c>UstawStatus</c>, one <c>Zapisz()</c>.
/// </summary>
/// <remarks>
/// Main price lists (<c>Cennik.Bazowy</c>) define a price level and are never created or have their status changed here.
/// Everything is checked before the business object is created, and nothing is saved on any error. Must run on the SDK
/// thread.
/// </remarks>
public static class PriceListCreator
{
    private static readonly IReadOnlyDictionary<string, StatusCennika> Statuses =
        new Dictionary<string, StatusCennika>(StringComparer.OrdinalIgnoreCase)
        {
            ["defined"] = StatusCennika.Definiowany,
            ["approved"] = StatusCennika.Zatwierdzony,
            ["closed"] = StatusCennika.Zamkniety,
        };

    /// <summary>Result of <see cref="Create"/> and <see cref="SetStatus"/>.</summary>
    public sealed class Outcome
    {
        public bool PriceListFound { get; init; } = true;
        public int? PriceListId { get; set; }
        public int PositionCount { get; set; }
        public bool Changed { get; set; }
        public List<string> Errors { get; } = new();
    }

    /// <summary>Validation of a create request that needs no SDK access, one message per problem.</summary>
    public static List<string> ValidateCreate(CreatePriceListRequest? request)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request?.Title)) errors.Add("title: required.");
        if (request?.Subtitle != null && string.IsNullOrWhiteSpace(request.Subtitle)) errors.Add("subtitle: must not be empty; omit it instead.");
        if (request?.MainPriceListId is not > 0) errors.Add("mainPriceListId: must be the id of a main price list.");

        var productIds = request?.ProductIds ?? new List<int>();
        if (productIds.Count > PriceListWriter.MaxItems) errors.Add($"productIds: at most {PriceListWriter.MaxItems} ids, got {productIds.Count}.");
        if (productIds.Any(id => id <= 0)) errors.Add("productIds: every id must be a positive Nexo product id.");
        if (productIds.Distinct().Count() != productIds.Count) errors.Add("productIds: every product must be listed once.");

        return errors;
    }

    /// <summary>Validation of a status request that needs no SDK access.</summary>
    public static List<string> ValidateStatus(SetPriceListStatusRequest? request)
    {
        return request?.Status != null && Statuses.ContainsKey(request.Status)
            ? new List<string>()
            : new List<string> { $"status: must be one of {string.Join(", ", Statuses.Keys)}." };
    }

    /// <summary>
    /// Creates an additional price list supplementing <c>request.MainPriceListId</c>, adds the positions of
    /// <c>request.ProductIds</c> with the main price list's calculation parameters and, with <c>Approve</c>, approves it
    /// as the logged-in operator. Call <see cref="ValidateCreate"/> first.
    /// </summary>
    public static Outcome Create(Uchwyt sfera, string? operatorLogin, CreatePriceListRequest request)
    {
        var outcome = new Outcome();
        var cenniki = sfera.Cenniki();
        var main = cenniki.Dane.Wszystkie().FirstOrDefault(c => c.Id == request.MainPriceListId);
        if (main == null || !main.Bazowy)
        {
            outcome.Errors.Add($"mainPriceListId: {request.MainPriceListId} is not a main price list.");
            return outcome;
        }

        if (main.Status == (int)StatusCennika.Zamkniety)
        {
            outcome.Errors.Add($"mainPriceListId: price list {main.Id} is closed.");
            return outcome;
        }

        Uzytkownik? approver = null;
        if (request.Approve)
        {
            approver = FindOperator(sfera, operatorLogin);
            if (approver == null) outcome.Errors.Add($"approve: no Nexo user matches the API operator '{operatorLogin}'.");
        }

        var productIds = request.ProductIds ?? new List<int>();
        if (productIds.Count > 0)
        {
            var products = PriceListReader.LoadProducts(sfera, productIds);
            using ICennik mainBo = cenniki.Znajdz(main);
            foreach (var productId in productIds)
            {
                if (!products.TryGetValue(productId, out var asortyment))
                {
                    outcome.Errors.Add($"productIds: product {productId} not found.");
                }
                else if (!(mainBo.Pozycje.ZnajdzPozycjeCennika(asortyment)?.Any(p => p.Glowna) ?? false))
                {
                    outcome.Errors.Add($"productIds: product {productId} has no main position in price list {main.Id}.");
                }
            }
        }

        if (outcome.Errors.Count > 0) return outcome;

        using ICennik cennik = cenniki.Utworz();
        cennik.Dane.Tytul = request.Title!.Trim();
        if (request.Subtitle != null) cennik.Dane.Podtytul = request.Subtitle.Trim();
        if (!string.IsNullOrWhiteSpace(request.Description)) cennik.Dane.Opis = request.Description;
        cennik.UstawJakoDodatkowy(main);

        if (productIds.Count > 0)
        {
            var added = (cennik.Pozycje.DodajIPrzepiszParametryPozycjiZGlownego(productIds, (_, _, _) => false)
                         ?? Enumerable.Empty<IUproszczonaPozycjaCennika>()).ToList();
            var addedIds = added.Select(p => p.IdAsortymentu).ToHashSet();
            foreach (var productId in productIds.Where(id => !addedIds.Contains(id)))
            {
                outcome.Errors.Add($"productIds: Nexo did not add a position for product {productId}.");
            }

            if (outcome.Errors.Count > 0) return outcome;
            outcome.PositionCount = added.Count;
        }

        if (approver != null && !TrySetStatus(cennik, approver, StatusCennika.Zatwierdzony, outcome.Errors)) return outcome;

        if (!cennik.Zapisz())
        {
            outcome.Errors.AddRange(PriceListWriter.SaveErrors(cennik));
            if (outcome.Errors.Count == 0) outcome.Errors.Add("Nexo refused to save the price list.");
            return outcome;
        }

        outcome.PriceListId = cennik.Dane.Id;
        outcome.Changed = true;
        return outcome;
    }

    /// <summary>
    /// Sets the status of additional price list <paramref name="priceListId"/> as the logged-in operator. A status equal
    /// to the current one saves nothing. Call <see cref="ValidateStatus"/> first.
    /// </summary>
    public static Outcome SetStatus(Uchwyt sfera, string? operatorLogin, int priceListId, string status)
    {
        var cennik = sfera.Cenniki().Dane.Wszystkie().FirstOrDefault(c => c.Id == priceListId);
        if (cennik == null) return new Outcome { PriceListFound = false };

        var outcome = new Outcome { PriceListId = priceListId };
        if (cennik.Bazowy)
        {
            outcome.Errors.Add("status: the status of a main price list is managed in Subiekt.");
            return outcome;
        }

        var target = Statuses[status];
        if (cennik.Status == (int)target) return outcome;

        var operatorUser = FindOperator(sfera, operatorLogin);
        if (operatorUser == null)
        {
            outcome.Errors.Add($"status: no Nexo user matches the API operator '{operatorLogin}'.");
            return outcome;
        }

        using ICennik cennikBo = sfera.Cenniki().Znajdz(cennik);
        if (!TrySetStatus(cennikBo, operatorUser, target, outcome.Errors)) return outcome;
        if (!cennikBo.Zapisz())
        {
            outcome.Errors.AddRange(PriceListWriter.SaveErrors(cennikBo));
            if (outcome.Errors.Count == 0) outcome.Errors.Add("Nexo refused to save the price list.");
            return outcome;
        }

        outcome.Changed = true;
        return outcome;
    }

    /// <summary><c>UstawStatus</c>; a transition Nexo refuses by throwing becomes a 400 message instead of a 500.</summary>
    private static bool TrySetStatus(ICennik cennik, Uzytkownik user, StatusCennika status, List<string> errors)
    {
        try
        {
            cennik.UstawStatus(user, status);
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            errors.Add($"status: Nexo refused the status {status} ({ex.Message}).");
            return false;
        }
    }

    /// <summary>The Nexo user (<c>Uzytkownik</c>) the API is logged in as, recorded by Nexo as the approver.</summary>
    private static Uzytkownik? FindOperator(Uchwyt sfera, string? operatorLogin)
    {
        if (string.IsNullOrWhiteSpace(operatorLogin)) return null;

        return sfera.Uzytkownicy().Dane.Wszystkie().FirstOrDefault(u => u.Login == operatorLogin);
    }
}
