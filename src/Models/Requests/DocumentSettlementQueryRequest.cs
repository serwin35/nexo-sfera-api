namespace NexoSferaApi.Models.Requests;

/// <summary>
/// Query of <c>GET /api/documents/settlements</c>: the settlement state (rozrachunek) of many documents. Every filter
/// runs in SQL.
/// </summary>
public class DocumentSettlementQueryRequest
{
    /// <summary>Document symbols, comma separated (e.g. <c>FS,KFS,PA</c>). Empty = every document kind.</summary>
    public string? Symbols { get; set; }

    /// <summary>Document ids, comma separated (at most 200). Documents without a settlement are returned for these too.</summary>
    public string? Ids { get; set; }

    /// <summary>Contractor of the document (Dokument.PodmiotId).</summary>
    public int? CustomerId { get; set; }

    /// <summary>Receivable (należność) or payable (zobowiązanie): Rozrachunek.Typ.</summary>
    public ReceivableType? Type { get; set; }

    /// <summary>
    /// Unsettled: nothing paid yet (remaining = amount); PartiallySettled: 0 &lt; remaining &lt; amount;
    /// Settled: nothing remains. Same meaning as in <c>GET /api/payments/receivables</c>.
    /// </summary>
    public ReceivableStatus? Status { get; set; }

    /// <summary>true: something remains to pay (remaining &gt; 0); false: nothing remains.</summary>
    public bool? Open { get; set; }

    /// <summary>true: an open instalment is past its due date (Rozrachunek.Pozycje[].TerminPlatnosci before today).</summary>
    public bool? Overdue { get; set; }

    /// <summary>Issue date range (Dokument.DataWydaniaWystawienia), inclusive.</summary>
    public DateTime? DateFrom { get; set; }

    public DateTime? DateTo { get; set; }

    /// <summary>Settlement due date range (Rozrachunek.TerminPlatnosci), inclusive.</summary>
    public DateTime? DueDateFrom { get; set; }

    public DateTime? DueDateTo { get; set; }

    /// <summary>
    /// Delta sync: only documents whose header or settlement changed at or after this time (the same rule as
    /// <c>GET /api/documents?modifiedSince=</c>). ISO 8601; without an offset the bridge host's local time is assumed.
    /// </summary>
    public DateTimeOffset? ModifiedSince { get; set; }

    /// <summary>true: only cancelled (invalidated) documents; false: only documents that are not cancelled.</summary>
    public bool? IsCanceled { get; set; }

    /// <summary>Also return documents that have no settlement (hasSettlement = false). Implied by <see cref="Ids"/>.</summary>
    public bool IncludeWithoutSettlement { get; set; }

    /// <summary>Sort order: id (default), due_date (no due date last), issue_date, issue_date_desc. Ties by id.</summary>
    public string SortBy { get; set; } = "id";

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 100;
}
