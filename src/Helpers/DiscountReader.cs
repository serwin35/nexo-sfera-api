using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using NexoSferaApi.Models.Dto;
using RodzajRabatu = InsERT.Moria.Dokumenty.Logistyka.RodzajRabatu;

namespace NexoSferaApi.Helpers;

/// <summary>
/// Typed, read-only access to the discount dictionary (<c>Rabat</c>, <c>Uchwyt.Rabaty()</c>) and the contractors a
/// discount is assigned to (<c>Rabat.Podmioty</c>).
/// </summary>
/// <remarks>
/// <para>
/// The late-bound mapper read members that <c>Rabat</c> does not have: <c>Aktywny</c>, <c>OdDaty</c>, <c>DoDaty</c>,
/// <c>Symbol</c>, <c>Opis</c>, <c>Priorytet</c>, <c>ProcentRabatu</c> and <c>KwotaRabatu</c>. The default
/// <c>activeOnly=true</c> filtered every discount out, and without the filter, mapping a discount threw
/// ("Cannot perform runtime binding on a null reference"). The real members are <c>Nazwa</c>, <c>RodzajRabatu</c>
/// (<see cref="RodzajRabatu"/>), <c>Procent</c>, <c>Kwota</c>, <c>Waluta</c> and <c>Podmioty</c>.
/// </para>
/// <para>
/// A discount has no validity period, priority or activity flag; the ones in the recycle bin are not returned by
/// <c>Dane.Wszystkie()</c>. Must run on the SDK thread. Nothing here modifies SDK objects.
/// </para>
/// </remarks>
public static class DiscountReader
{
    /// <summary>Subject type of a contractor assigned to a discount (as before).</summary>
    public const string ContractorSubject = "Contractor";

    private sealed class Row
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public int Kind { get; set; }
        public decimal? Percent { get; set; }
        public decimal? Amount { get; set; }
        public string? Currency { get; set; }
    }

    private sealed class SubjectRow
    {
        public int DiscountId { get; set; }
        public int ContractorId { get; set; }
        public string? Name { get; set; }
        public string? Symbol { get; set; }
    }

    public static IQueryable<Rabat> All(Uchwyt sfera) => sfera.Rabaty().Dane.Wszystkie();

    public static bool ContractorExists(Uchwyt sfera, int contractorId) =>
        sfera.Podmioty().Dane.Wszystkie().Any(p => p.Id == contractorId);

    /// <summary>Discounts that apply to the contractor: assigned to it, or assigned to no contractor at all (as before).</summary>
    public static IQueryable<Rabat> ForContractor(Uchwyt sfera, int contractorId) =>
        All(sfera).Where(r => !r.Podmioty.Any() || r.Podmioty.Any(p => p.Id == contractorId));

    /// <summary>One page ordered by name, then id, with the assigned contractors (two SQL queries).</summary>
    public static List<DiscountDto> Page(Uchwyt sfera, IQueryable<Rabat> discounts, int skip, int take) =>
        Map(sfera, discounts.OrderBy(r => r.Nazwa).ThenBy(r => r.Id).Skip(skip).Take(take));

    /// <summary>The discounts of the query with the assigned contractors (two SQL queries), in query order.</summary>
    public static List<DiscountDto> Map(Uchwyt sfera, IQueryable<Rabat> discounts)
    {
        var rows = discounts
            .Select(r => new Row
            {
                Id = r.Id,
                Name = r.Nazwa,
                Kind = r.RodzajRabatu,
                Percent = r.Procent,
                Amount = r.Kwota,
                Currency = r.Waluta == null ? null : r.Waluta.Symbol,
            })
            .ToList();
        if (rows.Count == 0) return new List<DiscountDto>();

        var ids = rows.Select(r => r.Id).ToList();
        var subjects = All(sfera)
            .Where(r => ids.Contains(r.Id))
            .SelectMany(r => r.Podmioty.Select(p => new SubjectRow
            {
                DiscountId = r.Id,
                ContractorId = p.Id,
                Name = p.NazwaSkrocona,
                // Sygnatura is a complex type: a null check does not translate to SQL; a missing value reads as null.
                Symbol = p.Sygnatura.PelnaSygnatura,
            }))
            .ToList();
        var byDiscount = subjects.GroupBy(s => s.DiscountId).ToDictionary(g => g.Key, g => g.OrderBy(s => s.ContractorId).ToList());

        return rows.Select(r => ToDto(r, byDiscount.TryGetValue(r.Id, out var list) ? list : new List<SubjectRow>())).ToList();
    }

    private static DiscountDto ToDto(Row row, List<SubjectRow> subjects)
    {
        var kind = (RodzajRabatu)row.Kind;
        var isPercent = kind is RodzajRabatu.Procentowy or RodzajRabatu.ProcentowyOdWartosci;
        return new DiscountDto
        {
            Id = row.Id,
            Name = row.Name,
            Type = isPercent ? DiscountType.Percentage : DiscountType.Amount,
            Kind = kind.ToString(),
            // Nexo keeps the percentage as a fraction ("Standardowy 5%" has Procent = 0.05); the API reports percent (5).
            PercentValue = isPercent && row.Percent.HasValue ? row.Percent.Value * 100m : null,
            AmountValue = isPercent ? null : row.Amount,
            CurrencySymbol = row.Currency,
            IsActive = true,
            Subjects = subjects.Select(s => new DiscountSubjectDto
            {
                Id = s.ContractorId,
                SubjectType = ContractorSubject,
                SubjectId = s.ContractorId,
                SubjectSymbol = s.Symbol,
                SubjectName = s.Name,
            }).ToList(),
        };
    }
}
