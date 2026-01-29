using System.ComponentModel.DataAnnotations;

namespace NexoSferaApi.Models.Requests;

/// <summary>
/// Request for creating a customer order (ZK)
/// </summary>
public class CreateCustomerOrderRequest
{
    /// <summary>
    /// Customer ID (required if CustomerNIP not provided)
    /// </summary>
    public int? CustomerId { get; set; }

    /// <summary>
    /// Customer NIP (required if CustomerId not provided)
    /// </summary>
    [MaxLength(13)]
    public string? CustomerNIP { get; set; }

    /// <summary>
    /// Recipient ID (if different from customer)
    /// </summary>
    public int? RecipientId { get; set; }

    /// <summary>
    /// Warehouse symbol
    /// </summary>
    [MaxLength(20)]
    public string? WarehouseSymbol { get; set; }

    /// <summary>
    /// Warehouse ID
    /// </summary>
    public int? WarehouseId { get; set; }

    /// <summary>
    /// Issue date (default: now)
    /// </summary>
    public DateTime? IssueDate { get; set; }

    /// <summary>
    /// Realization deadline
    /// </summary>
    public DateTime? DeadlineDate { get; set; }

    /// <summary>
    /// External order number
    /// </summary>
    [MaxLength(50)]
    public string? ExternalNumber { get; set; }

    /// <summary>
    /// Reference number
    /// </summary>
    [MaxLength(50)]
    public string? ReferenceNumber { get; set; }

    /// <summary>
    /// Order title
    /// </summary>
    [MaxLength(200)]
    public string? Title { get; set; }

    /// <summary>
    /// Price level ID
    /// </summary>
    public int? PriceLevelId { get; set; }

    /// <summary>
    /// Currency (default: PLN)
    /// </summary>
    [MaxLength(3)]
    public string Currency { get; set; } = "PLN";

    /// <summary>
    /// Enable split payment (MPP)
    /// </summary>
    public bool? SplitPayment { get; set; }

    /// <summary>
    /// Reservation type (0=none, 1=quantity, 2=delivery)
    /// </summary>
    public byte? ReservationType { get; set; }

    /// <summary>
    /// Notes/comments
    /// </summary>
    [MaxLength(2000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Order line items
    /// </summary>
    [Required]
    [MinLength(1)]
    public List<CreateCustomerOrderItemRequest> Items { get; set; } = new();

    /// <summary>
    /// Whether to reserve document number before saving.
    /// If true: calls ZarezerwujNumer() before Zapisz() - number is reserved immediately, may create gaps if save fails.
    /// If false (default): number is assigned automatically during Zapisz() - no gaps in numbering.
    /// </summary>
    public bool ReserveNumber { get; set; } = false;
}

/// <summary>
/// Request for customer order line item
/// </summary>
public class CreateCustomerOrderItemRequest
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
    /// Item name (required if ProductId/ProductSymbol not provided)
    /// </summary>
    [MaxLength(200)]
    public string? Name { get; set; }

    /// <summary>
    /// Description
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Quantity
    /// </summary>
    [Required]
    [Range(0.0001, double.MaxValue)]
    public decimal Quantity { get; set; }

    /// <summary>
    /// Unit symbol
    /// </summary>
    [MaxLength(10)]
    public string Unit { get; set; } = "szt.";

    /// <summary>
    /// Net price (optional, uses price list if not provided)
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? PriceNet { get; set; }

    /// <summary>
    /// Gross price (alternative to PriceNet)
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? PriceGross { get; set; }

    /// <summary>
    /// Discount percent
    /// </summary>
    [Range(0, 100)]
    public decimal? DiscountPercent { get; set; }

    /// <summary>
    /// VAT rate symbol (e.g., "23%", "8%", "ZW")
    /// </summary>
    [MaxLength(5)]
    public string? VatRate { get; set; }

    /// <summary>
    /// Create reservation for this item
    /// </summary>
    public bool? CreateReservation { get; set; }
}

/// <summary>
/// Request for updating customer order
/// </summary>
public class UpdateCustomerOrderRequest
{
    /// <summary>
    /// Realization deadline
    /// </summary>
    public DateTime? DeadlineDate { get; set; }

    /// <summary>
    /// External order number
    /// </summary>
    [MaxLength(50)]
    public string? ExternalNumber { get; set; }

    /// <summary>
    /// Reference number
    /// </summary>
    [MaxLength(50)]
    public string? ReferenceNumber { get; set; }

    /// <summary>
    /// Order title
    /// </summary>
    [MaxLength(200)]
    public string? Title { get; set; }

    /// <summary>
    /// Notes/comments
    /// </summary>
    [MaxLength(2000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Status ID
    /// </summary>
    public int? StatusId { get; set; }
}

/// <summary>
/// Query parameters for customer orders
/// </summary>
public class CustomerOrderQueryRequest
{
    /// <summary>
    /// Search by order number, external number, or customer name
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Filter by customer ID
    /// </summary>
    public int? CustomerId { get; set; }

    /// <summary>
    /// Filter by customer NIP
    /// </summary>
    public string? CustomerNIP { get; set; }

    /// <summary>
    /// Filter by warehouse symbol
    /// </summary>
    public string? WarehouseSymbol { get; set; }

    /// <summary>
    /// Filter by issue date from
    /// </summary>
    public DateTime? DateFrom { get; set; }

    /// <summary>
    /// Filter by issue date to
    /// </summary>
    public DateTime? DateTo { get; set; }

    /// <summary>
    /// Filter by deadline date from
    /// </summary>
    public DateTime? DeadlineFrom { get; set; }

    /// <summary>
    /// Filter by deadline date to
    /// </summary>
    public DateTime? DeadlineTo { get; set; }

    /// <summary>
    /// Filter by status ID
    /// </summary>
    public int? StatusId { get; set; }

    /// <summary>
    /// Filter only closed orders
    /// </summary>
    public bool? IsClosed { get; set; }

    /// <summary>
    /// Filter only realized orders
    /// </summary>
    public bool? IsRealized { get; set; }

    /// <summary>
    /// Filter only overdue orders
    /// </summary>
    public bool? IsOverdue { get; set; }

    /// <summary>
    /// Page number (1-based)
    /// </summary>
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    /// <summary>
    /// Page size
    /// </summary>
    [Range(1, 1000)]
    public int PageSize { get; set; } = 50;

    /// <summary>
    /// Sort field
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// Sort descending
    /// </summary>
    public bool SortDesc { get; set; }
}

/// <summary>
/// Request for realizing customer order
/// </summary>
public class RealizeCustomerOrderRequest
{
    /// <summary>
    /// Target document type (SalesInvoice, WarehouseRelease)
    /// </summary>
    [Required]
    public string DocumentType { get; set; } = "SalesInvoice";

    /// <summary>
    /// Realize all positions (default) or only specified
    /// </summary>
    public bool RealizeAll { get; set; } = true;

    /// <summary>
    /// Position IDs to realize (if RealizeAll = false)
    /// </summary>
    public List<int>? PositionIds { get; set; }

    /// <summary>
    /// Quantities to realize (if partial realization)
    /// </summary>
    public Dictionary<int, decimal>? PositionQuantities { get; set; }

    /// <summary>
    /// Issue date for created document
    /// </summary>
    public DateTime? IssueDate { get; set; }

    /// <summary>
    /// Auto-confirm created document
    /// </summary>
    public bool AutoConfirm { get; set; }
}
