using System.Globalization;
using System.Linq.Expressions;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Requests;
using TypRozrachunku = InsERT.Moria.Rozrachunki.TypRozrachunku;

namespace NexoSferaApi.Helpers;

/// <summary>
/// Typed read of <c>GET /api/documents/settlements</c>: the settlement state of many documents (paid, remaining, due
/// date, overdue instalments) as one SQL query for the page plus one count, with every filter in SQL.
/// </summary>
/// <remarks>
/// <para>
/// A trade document has at most one settlement (<c>Dokument.Rozrachunek</c>, 0..1 in the model). Its amounts are in the
/// settlement currency (<c>Rozrachunek.Waluta</c>). Payment terms are the instalments of the settlement's payment schedule
/// (<c>Rozrachunek.Pozycje</c>, each with its own <c>TerminPlatnosci</c> and <c>KwotaPozostala</c>); overdue is decided per
/// instalment against the bridge host's date. A settlement without instalments falls back to
/// <c>Rozrachunek.TerminPlatnosci</c>.
/// </para>
/// <para>Read only. Must run on the SDK thread.</para>
/// </remarks>
public static class DocumentSettlementReader
{
    public const int MaxPageSize = 1000;
    public const int MaxIds = 200;

    private static readonly string[] SortOrders = { "id", "due_date", "issue_date", "issue_date_desc" };

    /// <summary>Raw values of one document, read in SQL; mapped to the DTO in memory.</summary>
    internal sealed class Row
    {
        public int DocumentId { get; set; }
        public string? Symbol { get; set; }
        public string? Number { get; set; }
        public string? ExternalNumber { get; set; }
        public DateTime? IssueDate { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerNIP { get; set; }
        public decimal TotalGross { get; set; }
        public decimal AmountToPay { get; set; }
        public string? DocumentCurrency { get; set; }
        public bool? IsCanceled { get; set; }
        public int? SettlementId { get; set; }
        public byte? Type { get; set; }
        public decimal? Amount { get; set; }
        public decimal? Remaining { get; set; }
        public decimal? Unsettled { get; set; }
        public string? Currency { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? LastSettlementDate { get; set; }
        public DateTimeOffset? SettlementModifiedAt { get; set; }
        public int? InstallmentCount { get; set; }
        public DateTime? NextOpenDueDate { get; set; }
        public decimal? OverdueAmount { get; set; }
    }

    /// <summary>Request validation that needs no SDK access. One message per problem, prefixed with the parameter name.</summary>
    public static List<string> Validate(DocumentSettlementQueryRequest query, out List<int>? ids)
    {
        var errors = new List<string>();
        ids = null;

        if (query.Page < 1) errors.Add("page: must be at least 1.");
        if (query.PageSize < 1 || query.PageSize > MaxPageSize) errors.Add($"pageSize: must be between 1 and {MaxPageSize}.");

        if (!SortOrders.Contains((query.SortBy ?? "id").Trim().ToLowerInvariant()))
        {
            errors.Add($"sortBy: one of {string.Join(", ", SortOrders)}.");
        }

        if (query.DateFrom > query.DateTo) errors.Add("dateFrom: must not be after dateTo.");
        if (query.DueDateFrom > query.DueDateTo) errors.Add("dueDateFrom: must not be after dueDateTo.");

        if (!string.IsNullOrWhiteSpace(query.Ids))
        {
            var parsed = new List<int>();
            foreach (var token in SplitList(query.Ids))
            {
                if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
                {
                    errors.Add($"ids: invalid id '{token}'.");
                    return errors;
                }

                parsed.Add(id);
            }

            parsed = parsed.Distinct().ToList();
            if (parsed.Count > MaxIds) errors.Add($"ids: at most {MaxIds} ids, got {parsed.Count}.");
            ids = parsed.Count > 0 ? parsed : null;
        }

        return errors;
    }

    /// <summary>One page of documents with their settlement state and the number of documents matching the filters.</summary>
    public static (int TotalCount, List<DocumentSettlementStateDto> Items) Read(
        Uchwyt sfera, DocumentSettlementQueryRequest query, IReadOnlyCollection<int>? ids, DateTime today)
    {
        var filtered = Filter(sfera.Dokumenty().Dane.Wszystkie(), query, ids, today);
        var totalCount = filtered.Count();

        var rows = Project(Sort(filtered, query.SortBy).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize), today)
            .ToList();

        return (totalCount, rows.Select(row => Map(row, today)).ToList());
    }

    private static IQueryable<Dokument> Filter(IQueryable<Dokument> documents, DocumentSettlementQueryRequest query, IReadOnlyCollection<int>? ids, DateTime today)
    {
        if (ids != null)
        {
            var idList = ids.ToList();
            documents = documents.Where(d => idList.Contains(d.Id));
        }
        else if (!query.IncludeWithoutSettlement)
        {
            documents = documents.Where(d => d.Rozrachunek != null);
        }

        var symbols = SplitList(query.Symbols).Select(s => s.ToUpperInvariant()).Distinct().ToList();
        if (symbols.Count > 0) documents = documents.Where(d => symbols.Contains(d.Symbol));

        if (query.CustomerId.HasValue)
        {
            var customerId = query.CustomerId.Value;
            documents = documents.Where(d => d.PodmiotId == customerId);
        }

        if (query.Type.HasValue)
        {
            var type = query.Type.Value == ReceivableType.Payable ? (byte)TypRozrachunku.Zobowiazanie : (byte)TypRozrachunku.Naleznosc;
            documents = documents.Where(d => d.Rozrachunek != null && d.Rozrachunek.Typ == type);
        }

        switch (query.Status)
        {
            case ReceivableStatus.Unsettled:
                documents = documents.Where(d => d.Rozrachunek != null && d.Rozrachunek.KwotaPozostala > 0
                                                 && d.Rozrachunek.KwotaPozostala == d.Rozrachunek.Kwota);
                break;
            case ReceivableStatus.PartiallySettled:
                documents = documents.Where(d => d.Rozrachunek != null && d.Rozrachunek.KwotaPozostala > 0
                                                 && d.Rozrachunek.KwotaPozostala < d.Rozrachunek.Kwota);
                break;
            case ReceivableStatus.Settled:
                documents = documents.Where(d => d.Rozrachunek != null && d.Rozrachunek.KwotaPozostala <= 0);
                break;
        }

        if (query.Open.HasValue)
        {
            documents = query.Open.Value
                ? documents.Where(d => d.Rozrachunek != null && d.Rozrachunek.KwotaPozostala > 0)
                : documents.Where(d => d.Rozrachunek != null && d.Rozrachunek.KwotaPozostala <= 0);
        }

        if (query.Overdue.HasValue)
        {
            var overdue = IsOverdue(today);
            documents = documents.Where(query.Overdue.Value ? overdue : Not(overdue));
        }

        if (query.DateFrom.HasValue)
        {
            var from = query.DateFrom.Value;
            documents = documents.Where(d => d.DataWydaniaWystawienia >= from);
        }

        if (query.DateTo.HasValue)
        {
            var to = query.DateTo.Value;
            documents = documents.Where(d => d.DataWydaniaWystawienia <= to);
        }

        if (query.DueDateFrom.HasValue)
        {
            var from = query.DueDateFrom.Value;
            documents = documents.Where(d => d.Rozrachunek != null && d.Rozrachunek.TerminPlatnosci >= from);
        }

        if (query.DueDateTo.HasValue)
        {
            var to = query.DueDateTo.Value;
            documents = documents.Where(d => d.Rozrachunek != null && d.Rozrachunek.TerminPlatnosci <= to);
        }

        if (query.ModifiedSince.HasValue)
        {
            // Same rule as DocumentReader.ModifiedSince: the document itself or its settlement (a payment).
            var since = query.ModifiedSince.Value;
            documents = documents.Where(d => (d.Naglowek != null && d.Naglowek.Zmieniono >= since)
                                             || (d.Rozrachunek != null && d.Rozrachunek.Naglowek != null && d.Rozrachunek.Naglowek.Zmieniono >= since));
        }

        if (query.IsCanceled.HasValue)
        {
            var canceled = query.IsCanceled.Value;
            documents = documents.Where(d => (d.StatusDokumentu != null && d.StatusDokumentu.Uniewazniony) == canceled);
        }

        return documents;
    }

    /// <summary>Something remains and an open instalment (or, without instalments, the settlement) is past its due date.</summary>
    private static Expression<Func<Dokument, bool>> IsOverdue(DateTime today) =>
        d => d.Rozrachunek != null && d.Rozrachunek.KwotaPozostala > 0
             && (d.Rozrachunek.Pozycje.Any(p => p.KwotaPozostala > 0 && p.TerminPlatnosci < today)
                 || (!d.Rozrachunek.Pozycje.Any() && d.Rozrachunek.TerminPlatnosci < today));

    private static Expression<Func<Dokument, bool>> Not(Expression<Func<Dokument, bool>> predicate) =>
        Expression.Lambda<Func<Dokument, bool>>(Expression.Not(predicate.Body), predicate.Parameters);

    private static IQueryable<Dokument> Sort(IQueryable<Dokument> documents, string? sortBy) =>
        (sortBy ?? "id").Trim().ToLowerInvariant() switch
        {
            "due_date" => documents
                .OrderBy(d => d.Rozrachunek == null || d.Rozrachunek.TerminPlatnosci == null)
                .ThenBy(d => d.Rozrachunek.TerminPlatnosci)
                .ThenBy(d => d.Id),
            "issue_date" => documents.OrderBy(d => d.DataWydaniaWystawienia).ThenBy(d => d.Id),
            "issue_date_desc" => documents.OrderByDescending(d => d.DataWydaniaWystawienia).ThenByDescending(d => d.Id),
            _ => documents.OrderBy(d => d.Id),
        };

    private static IQueryable<Row> Project(IQueryable<Dokument> documents, DateTime today) =>
        documents.Select(d => new Row
        {
            DocumentId = d.Id,
            Symbol = d.Symbol,
            Number = d.NumerWewnetrzny.PelnaSygnatura,
            ExternalNumber = d.NumerZewnetrzny,
            IssueDate = d.DataWydaniaWystawienia,
            CustomerId = d.PodmiotId,
            CustomerName = d.Podmiot == null ? null : d.Podmiot.NazwaSkrocona,
            CustomerNIP = d.Podmiot == null ? null : d.Podmiot.NIP,
            TotalGross = d.Wartosc.BruttoPoRabacie,
            AmountToPay = d.KwotaDoZaplaty,
            DocumentCurrency = d.Waluta == null ? null : d.Waluta.Symbol,
            IsCanceled = d.StatusDokumentu == null ? (bool?)null : d.StatusDokumentu.Uniewazniony,
            SettlementId = d.Rozrachunek == null ? (int?)null : d.Rozrachunek.Id,
            Type = d.Rozrachunek == null ? (byte?)null : d.Rozrachunek.Typ,
            Amount = d.Rozrachunek == null ? (decimal?)null : d.Rozrachunek.Kwota,
            Remaining = d.Rozrachunek == null ? (decimal?)null : d.Rozrachunek.KwotaPozostala,
            Unsettled = d.Rozrachunek == null ? (decimal?)null : d.Rozrachunek.KwotaNierozliczona,
            Currency = d.Rozrachunek == null || d.Rozrachunek.Waluta == null ? null : d.Rozrachunek.Waluta.Symbol,
            DueDate = d.Rozrachunek == null ? null : d.Rozrachunek.TerminPlatnosci,
            LastSettlementDate = d.Rozrachunek == null ? null : d.Rozrachunek.DataOstatniegoRozliczenia,
            SettlementModifiedAt = d.Rozrachunek == null || d.Rozrachunek.Naglowek == null
                ? (DateTimeOffset?)null
                : d.Rozrachunek.Naglowek.Zmieniono,
            InstallmentCount = d.Rozrachunek == null ? (int?)null : d.Rozrachunek.Pozycje.Count(),
            NextOpenDueDate = d.Rozrachunek == null
                ? null
                : d.Rozrachunek.Pozycje.Where(p => p.KwotaPozostala > 0).Min(p => p.TerminPlatnosci),
            OverdueAmount = d.Rozrachunek == null
                ? null
                : d.Rozrachunek.Pozycje.Where(p => p.KwotaPozostala > 0 && p.TerminPlatnosci < today).Sum(p => (decimal?)p.KwotaPozostala),
        });

    private static DocumentSettlementStateDto Map(Row row, DateTime today)
    {
        var dto = new DocumentSettlementStateDto
        {
            DocumentId = row.DocumentId,
            Symbol = row.Symbol ?? string.Empty,
            Number = row.Number,
            ExternalNumber = string.IsNullOrEmpty(row.ExternalNumber) ? null : row.ExternalNumber,
            IssueDate = row.IssueDate,
            CustomerId = row.CustomerId,
            CustomerName = row.CustomerName,
            CustomerNIP = string.IsNullOrEmpty(row.CustomerNIP) ? null : row.CustomerNIP,
            TotalGross = row.TotalGross,
            AmountToPay = row.AmountToPay,
            DocumentCurrency = row.DocumentCurrency,
            IsCanceled = row.IsCanceled,
            HasSettlement = row.SettlementId.HasValue,
        };

        if (!row.SettlementId.HasValue) return dto;

        var amount = row.Amount ?? 0m;
        var remaining = row.Remaining ?? 0m;
        var nextDueDate = remaining > 0 ? row.NextOpenDueDate : null;
        var overdueAmount = row.OverdueAmount ?? 0m;

        if (row.InstallmentCount == 0 && remaining > 0)
        {
            // No payment schedule: the settlement itself carries the term.
            nextDueDate = row.DueDate;
            overdueAmount = row.DueDate.HasValue && row.DueDate.Value.Date < today ? remaining : 0m;
        }

        dto.SettlementId = row.SettlementId;
        dto.Type = row.Type switch
        {
            (byte)TypRozrachunku.Naleznosc => "Receivable",
            (byte)TypRozrachunku.Zobowiazanie => "Payable",
            _ => null,
        };
        dto.Amount = amount;
        dto.RemainingAmount = remaining;
        dto.UnsettledAmount = row.Unsettled;
        dto.PaidAmount = amount - remaining;
        dto.Currency = row.Currency ?? row.DocumentCurrency;
        dto.DueDate = row.DueDate;
        dto.NextDueDate = nextDueDate;
        dto.OverdueAmount = overdueAmount;
        dto.DaysOverdue = overdueAmount > 0 && nextDueDate.HasValue && nextDueDate.Value.Date < today
            ? (int)(today - nextDueDate.Value.Date).TotalDays
            : 0;
        dto.InstallmentCount = row.InstallmentCount;
        dto.LastSettlementDate = row.LastSettlementDate;
        dto.IsSettled = remaining <= 0;
        dto.IsOverdue = overdueAmount > 0;
        dto.SettlementModifiedAt = row.SettlementModifiedAt;
        return dto;
    }

    private static IEnumerable<string> SplitList(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? Enumerable.Empty<string>()
            : raw.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
