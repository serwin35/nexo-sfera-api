using System.ComponentModel.DataAnnotations;
using NexoSferaApi.Models.Dto;

namespace NexoSferaApi.Models.Requests;

/// <summary>
/// Request model for creating document corrections
/// </summary>
public class CreateCorrectionRequest
{
    /// <summary>
    /// Original document ID to correct (required for corrections of existing documents)
    /// </summary>
    public int? OriginalDocumentId { get; set; }

    /// <summary>
    /// Original document number (alternative to ID)
    /// </summary>
    [MaxLength(50)]
    public string? OriginalDocumentNumber { get; set; }

    /// <summary>
    /// Customer ID (for corrections without original document)
    /// </summary>
    public int? CustomerId { get; set; }

    /// <summary>
    /// Customer NIP (alternative to CustomerId)
    /// </summary>
    [MaxLength(13)]
    public string? CustomerNIP { get; set; }

    /// <summary>
    /// Warehouse symbol
    /// </summary>
    [MaxLength(20)]
    public string? WarehouseSymbol { get; set; }

    /// <summary>
    /// Correction issue date
    /// </summary>
    public DateTime? IssueDate { get; set; }

    /// <summary>
    /// Reason for correction
    /// </summary>
    [Required]
    [MaxLength(2000)]
    public string CorrectionReason { get; set; } = string.Empty;

    /// <summary>
    /// Additional notes
    /// </summary>
    [MaxLength(2000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Correction items (positions to correct)
    /// </summary>
    [Required]
    [MinLength(1)]
    public List<CreateCorrectionItemRequest> Items { get; set; } = new();
}

/// <summary>
/// Correction item - defines what to correct on a position
/// </summary>
public class CreateCorrectionItemRequest
{
    /// <summary>
    /// Original position ID (for correcting existing position)
    /// </summary>
    public int? OriginalPositionId { get; set; }

    /// <summary>
    /// Product ID (for new positions or when not linking to original)
    /// </summary>
    public int? ProductId { get; set; }

    /// <summary>
    /// Product symbol (alternative to ProductId)
    /// </summary>
    [MaxLength(50)]
    public string? ProductSymbol { get; set; }

    /// <summary>
    /// Corrected quantity (negative for returns)
    /// </summary>
    [Required]
    public decimal QuantityCorrection { get; set; }

    /// <summary>
    /// Corrected net price (optional, keeps original if not specified)
    /// </summary>
    public decimal? PriceNetCorrection { get; set; }

    /// <summary>
    /// Unit
    /// </summary>
    [MaxLength(10)]
    public string? Unit { get; set; }

    /// <summary>
    /// Reason for this item correction
    /// </summary>
    [MaxLength(500)]
    public string? ItemCorrectionReason { get; set; }
}

/// <summary>
/// Request for receipt/paragon creation
/// </summary>
public class CreateReceiptRequest
{
    /// <summary>
    /// Receipt type: regular (PA), named (PAi), fiscal (PAf)
    /// </summary>
    public ReceiptType Type { get; set; } = ReceiptType.Regular;

    /// <summary>
    /// Customer ID (optional for regular receipts, required for named receipts)
    /// </summary>
    public int? CustomerId { get; set; }

    /// <summary>
    /// Customer NIP (for named receipts)
    /// </summary>
    [MaxLength(13)]
    public string? CustomerNIP { get; set; }

    /// <summary>
    /// Warehouse symbol
    /// </summary>
    [MaxLength(20)]
    public string? WarehouseSymbol { get; set; }

    /// <summary>
    /// Issue date
    /// </summary>
    public DateTime? IssueDate { get; set; }

    /// <summary>
    /// Notes
    /// </summary>
    [MaxLength(2000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Payment method (gotowka, karta, przelew)
    /// </summary>
    [MaxLength(50)]
    public string? PaymentMethod { get; set; }

    /// <summary>
    /// Receipt items
    /// </summary>
    [Required]
    [MinLength(1)]
    public List<CreateDocumentItemRequest> Items { get; set; } = new();
}

public enum ReceiptType
{
    /// <summary>Regular receipt (PA)</summary>
    Regular = 0,
    /// <summary>Named receipt with customer data (PAi)</summary>
    Named = 1,
    /// <summary>Fiscal receipt from cash register (PAf)</summary>
    Fiscal = 2
}

/// <summary>
/// Request for proforma invoice creation
/// </summary>
public class CreateProformaRequest
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
    /// Warehouse symbol
    /// </summary>
    [MaxLength(20)]
    public string? WarehouseSymbol { get; set; }

    /// <summary>
    /// Issue date
    /// </summary>
    public DateTime? IssueDate { get; set; }

    /// <summary>
    /// Due date for payment
    /// </summary>
    public DateTime? DueDate { get; set; }

    /// <summary>
    /// Validity period in days
    /// </summary>
    public int? ValidityDays { get; set; }

    /// <summary>
    /// Payment method
    /// </summary>
    [MaxLength(50)]
    public string? PaymentMethod { get; set; }

    /// <summary>
    /// Currency
    /// </summary>
    [MaxLength(3)]
    public string Currency { get; set; } = "PLN";

    /// <summary>
    /// Notes
    /// </summary>
    [MaxLength(2000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Proforma items
    /// </summary>
    [Required]
    [MinLength(1)]
    public List<CreateDocumentItemRequest> Items { get; set; } = new();
}

/// <summary>
/// Request for advance invoice creation
/// </summary>
public class CreateAdvanceInvoiceRequest
{
    /// <summary>
    /// Related order ID (ZK) if creating advance for existing order
    /// </summary>
    public int? OrderId { get; set; }

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
    /// Warehouse symbol
    /// </summary>
    [MaxLength(20)]
    public string? WarehouseSymbol { get; set; }

    /// <summary>
    /// Issue date
    /// </summary>
    public DateTime? IssueDate { get; set; }

    /// <summary>
    /// Sale date
    /// </summary>
    public DateTime? SaleDate { get; set; }

    /// <summary>
    /// Advance payment amount
    /// </summary>
    [Range(0.01, double.MaxValue)]
    public decimal? AdvanceAmount { get; set; }

    /// <summary>
    /// Advance percentage of total (alternative to AdvanceAmount)
    /// </summary>
    [Range(0.01, 100)]
    public decimal? AdvancePercentage { get; set; }

    /// <summary>
    /// Payment method
    /// </summary>
    [MaxLength(50)]
    public string? PaymentMethod { get; set; }

    /// <summary>
    /// Currency
    /// </summary>
    [MaxLength(3)]
    public string Currency { get; set; } = "PLN";

    /// <summary>
    /// Notes
    /// </summary>
    [MaxLength(2000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Items (required if no OrderId)
    /// </summary>
    public List<CreateDocumentItemRequest>? Items { get; set; }
}
