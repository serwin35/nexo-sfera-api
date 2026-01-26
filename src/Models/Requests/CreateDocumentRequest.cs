using System.ComponentModel.DataAnnotations;
using NexoSferaApi.Models.Dto;

namespace NexoSferaApi.Models.Requests;

public class CreateDocumentRequest
{
    [Required]
    public DocumentType Type { get; set; }

    public int? CustomerId { get; set; }

    [MaxLength(13)]
    public string? CustomerNIP { get; set; }

    [MaxLength(20)]
    public string? WarehouseSymbol { get; set; }

    public DateTime? IssueDate { get; set; }

    public DateTime? SaleDate { get; set; }

    public DateTime? DueDate { get; set; }

    /// <summary>
    /// Payment method symbol (e.g., GOTOWKA, PRZELEW, KARTA, PAYNOW)
    /// </summary>
    [MaxLength(50)]
    public string? PaymentMethod { get; set; }

    /// <summary>
    /// Payment method ID (alternative to PaymentMethod symbol)
    /// </summary>
    public int? PaymentMethodId { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "PLN";

    [MaxLength(2000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Document status ID. For historical documents (past IssueDate), use status without automatic document creation:
    /// - 4: "Bez rezerwacji" (no reservation) - RECOMMENDED for historical import
    /// - 11: "Odłożone wydanie towaru" (deferred goods release)
    /// - 17: "Odłożone wydanie towaru i wykonanie usług" (deferred release and services)
    /// If not specified for historical documents, defaults to status 4 to avoid auto-document creation failures.
    /// </summary>
    public int? StatusId { get; set; }

    [Required]
    [MinLength(1)]
    public List<CreateDocumentItemRequest> Items { get; set; } = new();
}

public class CreateDocumentItemRequest
{
    public int? ProductId { get; set; }

    [MaxLength(50)]
    public string? ProductSymbol { get; set; }

    [MaxLength(20)]
    public string? ProductEan { get; set; }

    [MaxLength(200)]
    public string? Name { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    [Range(0.0001, double.MaxValue)]
    public decimal Quantity { get; set; }

    [MaxLength(10)]
    public string Unit { get; set; } = "szt.";

    [Range(0, double.MaxValue)]
    public decimal? PriceNet { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? PriceGross { get; set; }

    [Range(0, 100)]
    public decimal? DiscountPercent { get; set; }

    [MaxLength(5)]
    public string? VatRate { get; set; }

    /// <summary>
    /// Warehouse symbol for this item (optional - defaults to document warehouse)
    /// </summary>
    [MaxLength(20)]
    public string? WarehouseSymbol { get; set; }
}

public class DocumentQueryRequest
{
    /// <summary>
    /// Filter by document symbol (e.g., FS, FZ, ZK, ZD, PZ, WZ, etc.)
    /// </summary>
    public string? Symbol { get; set; }

    /// <summary>
    /// Search by document number or customer name
    /// </summary>
    public string? Search { get; set; }

    public DocumentType? Type { get; set; }
    public int? CustomerId { get; set; }

    /// <summary>
    /// Filter by customer NIP
    /// </summary>
    public string? CustomerNIP { get; set; }

    /// <summary>
    /// Filter by issue date from
    /// </summary>
    public DateTime? DateFrom { get; set; }

    /// <summary>
    /// Filter by issue date to
    /// </summary>
    public DateTime? DateTo { get; set; }

    /// <summary>
    /// Filter by sale date from
    /// </summary>
    public DateTime? SaleDateFrom { get; set; }

    /// <summary>
    /// Filter by sale date to
    /// </summary>
    public DateTime? SaleDateTo { get; set; }

    /// <summary>
    /// Filter by payment due date from
    /// </summary>
    public DateTime? DueDateFrom { get; set; }

    /// <summary>
    /// Filter by payment due date to
    /// </summary>
    public DateTime? DueDateTo { get; set; }

    public string? Status { get; set; }
    public int? StatusId { get; set; }
    public string? WarehouseSymbol { get; set; }
    public int? WarehouseId { get; set; }

    /// <summary>
    /// Filter by currency symbol (e.g., PLN, EUR, USD)
    /// </summary>
    public string? Currency { get; set; }

    /// <summary>
    /// Show only paid documents
    /// </summary>
    public bool PaidOnly { get; set; }

    /// <summary>
    /// Show only unpaid documents
    /// </summary>
    public bool UnpaidOnly { get; set; }

    /// <summary>
    /// Show only overdue documents (unpaid and past due date)
    /// </summary>
    public bool OverdueOnly { get; set; }

    /// <summary>
    /// Filter by KSeF status (sent, not_sent, error)
    /// </summary>
    public string? KsefStatus { get; set; }

    /// <summary>
    /// Show only canceled documents
    /// </summary>
    public bool? IsCanceled { get; set; }

    /// <summary>
    /// Filter by minimum total gross amount
    /// </summary>
    public decimal? MinAmount { get; set; }

    /// <summary>
    /// Filter by maximum total gross amount
    /// </summary>
    public decimal? MaxAmount { get; set; }

    /// <summary>
    /// Sort order: date_desc (default), date_asc, amount_desc, amount_asc, number_desc, number_asc
    /// </summary>
    public string SortBy { get; set; } = "date_desc";

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
