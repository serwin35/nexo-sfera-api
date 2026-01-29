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

    /// <summary>
    /// Whether to reserve document number before saving.
    /// If true: calls ZarezerwujNumer() before Zapisz() - number is reserved immediately, may create gaps if save fails.
    /// If false (default): number is assigned automatically during Zapisz() - no gaps in numbering.
    /// </summary>
    public bool ReserveNumber { get; set; } = false;
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

/// <summary>
/// Request for updating an existing document
/// </summary>
public class UpdateDocumentRequest
{
    /// <summary>
    /// Person who issued/created the document (text field - appears on printouts)
    /// </summary>
    [MaxLength(100)]
    public string? Wystawil { get; set; }

    /// <summary>
    /// ID of the employee (Pracownik) who issued the document
    /// </summary>
    public int? WystawilaOsobaId { get; set; }

    /// <summary>
    /// Person who received the document (for warehouse documents)
    /// </summary>
    [MaxLength(100)]
    public string? Odebral { get; set; }

    /// <summary>
    /// Notes/remarks
    /// </summary>
    [MaxLength(2000)]
    public string? Notes { get; set; }

    /// <summary>
    /// External document number (e.g., supplier invoice number for FZ)
    /// </summary>
    [MaxLength(50)]
    public string? ExternalNumber { get; set; }

    /// <summary>
    /// Due date for payment
    /// </summary>
    public DateTime? DueDate { get; set; }

    /// <summary>
    /// Sale date
    /// </summary>
    public DateTime? SaleDate { get; set; }

    /// <summary>
    /// Status ID
    /// </summary>
    public int? StatusId { get; set; }

    /// <summary>
    /// Description/title
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }
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

/// <summary>
/// Request for realizing customer orders (ZK) to sales documents (FS, PA)
/// </summary>
public class RealizeOrderRequest
{
    /// <summary>
    /// Target document type: FS (invoice), PA (receipt), PAi (named receipt)
    /// </summary>
    [Required]
    public RealizeTargetDocumentType TargetDocumentType { get; set; }

    /// <summary>
    /// Order IDs to realize (at least one required)
    /// </summary>
    [Required]
    [MinLength(1)]
    public List<int> OrderIds { get; set; } = new();

    /// <summary>
    /// Optional: specific position IDs to realize (partial realization).
    /// If empty, all positions from orders will be realized.
    /// </summary>
    public List<int>? PositionIds { get; set; }

    /// <summary>
    /// Position consolidation method
    /// </summary>
    public PositionConsolidationMethod ConsolidationMethod { get; set; } = PositionConsolidationMethod.NoConsolidation;

    /// <summary>
    /// Whether to transfer immediate payments from orders
    /// </summary>
    public bool TransferImmediatePayments { get; set; } = true;

    /// <summary>
    /// Whether to transfer prepayments from orders
    /// </summary>
    public bool TransferPrepayments { get; set; } = true;

    /// <summary>
    /// Warehouse symbol (optional, defaults to order's warehouse)
    /// </summary>
    [MaxLength(20)]
    public string? WarehouseSymbol { get; set; }

    /// <summary>
    /// Issue date (optional, defaults to today)
    /// </summary>
    public DateTime? IssueDate { get; set; }

    /// <summary>
    /// Sale date (optional, defaults to IssueDate)
    /// </summary>
    public DateTime? SaleDate { get; set; }

    /// <summary>
    /// Additional notes
    /// </summary>
    [MaxLength(2000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Whether to reserve document number before saving
    /// </summary>
    public bool ReserveNumber { get; set; } = false;
}

/// <summary>
/// Target document type for order realization
/// </summary>
public enum RealizeTargetDocumentType
{
    /// <summary>Faktura sprzedaży - Sales Invoice</summary>
    FS = 0,
    /// <summary>Paragon - Receipt</summary>
    PA = 1,
    /// <summary>Paragon imienny - Named Receipt</summary>
    PAi = 2,
    /// <summary>Faktura zaliczkowa - Advance Invoice</summary>
    FZ_Zaliczka = 3
}

/// <summary>
/// Position consolidation method for document realization
/// </summary>
public enum PositionConsolidationMethod
{
    /// <summary>No consolidation - each position separately</summary>
    NoConsolidation = 0,
    /// <summary>Consolidate by unit of measure</summary>
    ConsolidateByUnit = 1,
    /// <summary>Consolidate regardless of unit</summary>
    ConsolidateIgnoreUnit = 2,
    /// <summary>Consolidate by unit and price</summary>
    ConsolidateByUnitAndPrice = 3
}
