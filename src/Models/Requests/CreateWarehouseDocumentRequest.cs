using System.ComponentModel.DataAnnotations;

namespace NexoSferaApi.Models.Requests;

/// <summary>
/// Request for warehouse document creation (WZ, PZ, RW, PW, MM)
/// </summary>
public class CreateWarehouseDocumentRequest
{
    /// <summary>
    /// Document type: WZ (external release), PZ (external receipt),
    /// RW (internal consumption), PW (internal receipt), MM (inter-warehouse transfer)
    /// </summary>
    [Required]
    public WarehouseDocumentType Type { get; set; }

    /// <summary>
    /// Customer/Supplier ID (required for WZ, PZ)
    /// </summary>
    public int? ContractorId { get; set; }

    /// <summary>
    /// Customer/Supplier NIP (alternative to ContractorId)
    /// </summary>
    [MaxLength(13)]
    public string? ContractorNIP { get; set; }

    /// <summary>
    /// Customer/Supplier Symbol (alternative to ContractorId/NIP) - SDK preferred method
    /// </summary>
    [MaxLength(50)]
    public string? ContractorSymbol { get; set; }

    /// <summary>
    /// Source warehouse symbol (required)
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string WarehouseSymbol { get; set; } = string.Empty;

    /// <summary>
    /// Target warehouse symbol (required for MM - inter-warehouse transfer)
    /// </summary>
    [MaxLength(20)]
    public string? TargetWarehouseSymbol { get; set; }

    /// <summary>
    /// Issue date
    /// </summary>
    public DateTime? IssueDate { get; set; }

    /// <summary>
    /// Operation date (for stock movement)
    /// </summary>
    public DateTime? OperationDate { get; set; }

    /// <summary>
    /// Related document number (e.g., external invoice number for PZ)
    /// </summary>
    [MaxLength(50)]
    public string? RelatedDocumentNumber { get; set; }

    /// <summary>
    /// External document date (e.g., supplier invoice date for PZ with VAT).
    /// CRITICAL for PZ documents with financial aspect - this date is used for VAT settlement period.
    /// If not provided, defaults to IssueDate.
    /// </summary>
    public DateTime? ExternalDocumentDate { get; set; }

    /// <summary>
    /// Notes
    /// </summary>
    [MaxLength(2000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Document items
    /// </summary>
    [Required]
    [MinLength(1)]
    public List<CreateWarehouseDocumentItemRequest> Items { get; set; } = new();

    /// <summary>
    /// Whether to reserve document number before saving.
    /// If true: calls ZarezerwujNumer() before Zapisz() - number is reserved immediately, may create gaps if save fails.
    /// If false (default): number is assigned automatically during Zapisz() - no gaps in numbering.
    /// </summary>
    public bool ReserveNumber { get; set; } = false;
}

/// <summary>
/// Warehouse document item
/// </summary>
public class CreateWarehouseDocumentItemRequest
{
    /// <summary>
    /// Product ID
    /// </summary>
    public int? ProductId { get; set; }

    /// <summary>
    /// Product symbol (alternative to ProductId)
    /// </summary>
    [MaxLength(50)]
    public string? ProductSymbol { get; set; }

    /// <summary>
    /// Product EAN (alternative lookup)
    /// </summary>
    [MaxLength(20)]
    public string? ProductEan { get; set; }

    /// <summary>
    /// Quantity
    /// </summary>
    [Required]
    [Range(0.0001, double.MaxValue)]
    public decimal Quantity { get; set; }

    /// <summary>
    /// Unit
    /// </summary>
    [MaxLength(10)]
    public string? Unit { get; set; }

    /// <summary>
    /// Net price (for valued documents)
    /// </summary>
    public decimal? PriceNet { get; set; }

    /// <summary>
    /// Batch/Lot number
    /// </summary>
    [MaxLength(50)]
    public string? BatchNumber { get; set; }

    /// <summary>
    /// Serial number
    /// </summary>
    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    /// <summary>
    /// Expiration date (for products with shelf life)
    /// </summary>
    public DateTime? ExpirationDate { get; set; }

    /// <summary>
    /// Production date
    /// </summary>
    public DateTime? ProductionDate { get; set; }

    /// <summary>
    /// Source location in warehouse
    /// </summary>
    [MaxLength(50)]
    public string? SourceLocation { get; set; }

    /// <summary>
    /// Target location in warehouse
    /// </summary>
    [MaxLength(50)]
    public string? TargetLocation { get; set; }
}

/// <summary>
/// Warehouse document types
/// </summary>
public enum WarehouseDocumentType
{
    /// <summary>Wydanie zewnętrzne - External release</summary>
    WZ = 0,
    /// <summary>Przyjęcie zewnętrzne - External receipt</summary>
    PZ = 1,
    /// <summary>Rozchód wewnętrzny - Internal consumption</summary>
    RW = 2,
    /// <summary>Przychód wewnętrzny - Internal receipt</summary>
    PW = 3,
    /// <summary>Przesunięcie międzymagazynowe - Inter-warehouse transfer</summary>
    MM = 4
}

/// <summary>
/// Request for supplier order creation (ZD - Zamowienie do dostawcy)
/// </summary>
public class CreateSupplierOrderRequest
{
    /// <summary>
    /// Supplier ID
    /// </summary>
    public int? SupplierId { get; set; }

    /// <summary>
    /// Supplier NIP
    /// </summary>
    [MaxLength(13)]
    public string? SupplierNIP { get; set; }

    /// <summary>
    /// Target warehouse symbol
    /// </summary>
    [MaxLength(20)]
    public string? WarehouseSymbol { get; set; }

    /// <summary>
    /// Order date
    /// </summary>
    public DateTime? OrderDate { get; set; }

    /// <summary>
    /// Expected delivery date
    /// </summary>
    public DateTime? ExpectedDeliveryDate { get; set; }

    /// <summary>
    /// Currency
    /// </summary>
    [MaxLength(3)]
    public string Currency { get; set; } = "PLN";

    /// <summary>
    /// Payment method
    /// </summary>
    [MaxLength(50)]
    public string? PaymentMethod { get; set; }

    /// <summary>
    /// Notes
    /// </summary>
    [MaxLength(2000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Order items
    /// </summary>
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

/// <summary>
/// Request for commercial offer creation (Oferta handlowa)
/// </summary>
public class CreateCommercialOfferRequest
{
    /// <summary>
    /// Customer ID
    /// </summary>
    public int? CustomerId { get; set; }

    /// <summary>
    /// Customer NIP
    /// </summary>
    [MaxLength(13)]
    public string? CustomerNIP { get; set; }

    /// <summary>
    /// Offer title
    /// </summary>
    [MaxLength(200)]
    public string? Title { get; set; }

    /// <summary>
    /// Issue date
    /// </summary>
    public DateTime? IssueDate { get; set; }

    /// <summary>
    /// Validity date (offer expires after)
    /// </summary>
    public DateTime? ValidUntil { get; set; }

    /// <summary>
    /// Currency
    /// </summary>
    [MaxLength(3)]
    public string Currency { get; set; } = "PLN";

    /// <summary>
    /// Notes
    /// </summary>
    [MaxLength(4000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Offer items
    /// </summary>
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

/// <summary>
/// Request for updating an existing warehouse document
/// </summary>
public class UpdateWarehouseDocumentRequest
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
    /// Person who received the document
    /// </summary>
    [MaxLength(100)]
    public string? Odebral { get; set; }

    /// <summary>
    /// Notes/remarks
    /// </summary>
    [MaxLength(2000)]
    public string? Notes { get; set; }

    /// <summary>
    /// External document number (e.g., supplier document number for PZ)
    /// </summary>
    [MaxLength(50)]
    public string? ExternalNumber { get; set; }

    /// <summary>
    /// External document date
    /// </summary>
    public DateTime? ExternalDocumentDate { get; set; }

    /// <summary>
    /// Description
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }
}

/// <summary>
/// Query parameters for warehouse documents
/// </summary>
public class WarehouseDocumentQueryRequest
{
    public WarehouseDocumentType? Type { get; set; }
    public int? ContractorId { get; set; }
    public string? WarehouseSymbol { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
