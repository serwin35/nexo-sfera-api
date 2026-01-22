namespace NexoSferaApi.Models.Dto;

/// <summary>
/// Document correction DTO
/// </summary>
public class CorrectionDto
{
    public int Id { get; set; }
    public string? Number { get; set; }
    public string? FullNumber { get; set; }
    public CorrectionType Type { get; set; }
    public DateTime? IssueDate { get; set; }

    /// <summary>
    /// Original document info
    /// </summary>
    public int? OriginalDocumentId { get; set; }
    public string? OriginalDocumentNumber { get; set; }

    public string? CustomerName { get; set; }
    public string? CustomerNIP { get; set; }
    public string? WarehouseSymbol { get; set; }

    /// <summary>
    /// Before correction values
    /// </summary>
    public decimal? OriginalTotalNet { get; set; }
    public decimal? OriginalTotalGross { get; set; }

    /// <summary>
    /// After correction values
    /// </summary>
    public decimal? CorrectedTotalNet { get; set; }
    public decimal? CorrectedTotalGross { get; set; }

    /// <summary>
    /// Difference (correction amount)
    /// </summary>
    public decimal CorrectionNet { get; set; }
    public decimal CorrectionVat { get; set; }
    public decimal CorrectionGross { get; set; }

    public string? CorrectionReason { get; set; }
    public string? Notes { get; set; }
    public DateTime? CreatedAt { get; set; }

    public List<CorrectionItemDto> Items { get; set; } = new();
}

/// <summary>
/// Correction item DTO
/// </summary>
public class CorrectionItemDto
{
    public int Id { get; set; }
    public int LineNumber { get; set; }
    public int? ProductId { get; set; }
    public string? ProductSymbol { get; set; }
    public string? Name { get; set; }

    /// <summary>
    /// Original values
    /// </summary>
    public decimal OriginalQuantity { get; set; }
    public decimal OriginalPriceNet { get; set; }
    public decimal OriginalValueNet { get; set; }

    /// <summary>
    /// After correction
    /// </summary>
    public decimal CorrectedQuantity { get; set; }
    public decimal CorrectedPriceNet { get; set; }
    public decimal CorrectedValueNet { get; set; }

    /// <summary>
    /// Correction difference
    /// </summary>
    public decimal QuantityDifference { get; set; }
    public decimal ValueNetDifference { get; set; }
    public decimal ValueGrossDifference { get; set; }

    public string? Unit { get; set; }
}

public enum CorrectionType
{
    /// <summary>Korekta faktury sprzedaży</summary>
    SalesInvoiceCorrection = 0,
    /// <summary>Korekta faktury zakupu</summary>
    PurchaseInvoiceCorrection = 1,
    /// <summary>Zwrot do paragonu</summary>
    ReceiptReturn = 2,
    /// <summary>Korekta WZ</summary>
    WZCorrection = 3,
    /// <summary>Korekta PZ</summary>
    PZCorrection = 4
}
