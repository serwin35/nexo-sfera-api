using NexoSferaApi.Models.Requests;

namespace NexoSferaApi.Models.Dto;

/// <summary>
/// Warehouse document DTO (WZ, PZ, RW, PW, MM)
/// </summary>
public class WarehouseDocumentDto
{
    public int Id { get; set; }
    public string? Number { get; set; }
    public string? FullNumber { get; set; }
    public WarehouseDocumentType Type { get; set; }
    public DateTime? IssueDate { get; set; }
    public DateTime? OperationDate { get; set; }

    public string? ContractorName { get; set; }
    public string? ContractorNIP { get; set; }

    public string? WarehouseSymbol { get; set; }
    public string? WarehouseName { get; set; }

    /// <summary>
    /// Target warehouse for MM documents
    /// </summary>
    public string? TargetWarehouseSymbol { get; set; }
    public string? TargetWarehouseName { get; set; }

    public decimal TotalQuantity { get; set; }
    public decimal? TotalNet { get; set; }
    public decimal? TotalGross { get; set; }

    public string? RelatedDocumentNumber { get; set; }
    public string? Notes { get; set; }
    public DateTime? CreatedAt { get; set; }

    public List<WarehouseDocumentItemDto> Items { get; set; } = new();
}

/// <summary>
/// Warehouse document item DTO
/// </summary>
public class WarehouseDocumentItemDto
{
    public int Id { get; set; }
    public int LineNumber { get; set; }
    public int? ProductId { get; set; }
    public string? ProductSymbol { get; set; }
    public string? ProductEan { get; set; }
    public string? Name { get; set; }

    public decimal Quantity { get; set; }
    public string? Unit { get; set; }

    public decimal? PriceNet { get; set; }
    public decimal? ValueNet { get; set; }
    public decimal? ValueGross { get; set; }

    /// <summary>
    /// Batch/lot number
    /// </summary>
    public string? BatchNumber { get; set; }

    /// <summary>
    /// Serial number
    /// </summary>
    public string? SerialNumber { get; set; }

    /// <summary>
    /// Expiration date
    /// </summary>
    public DateTime? ExpirationDate { get; set; }

    /// <summary>
    /// Warehouse location
    /// </summary>
    public string? Location { get; set; }
}

/// <summary>
/// Supplier order DTO
/// </summary>
public class SupplierOrderDto
{
    public int Id { get; set; }
    public string? Number { get; set; }
    public string? FullNumber { get; set; }
    public DateTime? OrderDate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }

    public int? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public string? SupplierNIP { get; set; }

    public string? WarehouseSymbol { get; set; }
    public string? Currency { get; set; }

    public decimal TotalNet { get; set; }
    public decimal TotalVat { get; set; }
    public decimal TotalGross { get; set; }

    public string? Status { get; set; }
    public string? Notes { get; set; }
    public DateTime? CreatedAt { get; set; }

    public List<DocumentItemDto> Items { get; set; } = new();
}

/// <summary>
/// Commercial offer DTO
/// </summary>
public class CommercialOfferDto
{
    public int Id { get; set; }
    public string? Number { get; set; }
    public string? FullNumber { get; set; }
    public string? Title { get; set; }
    public DateTime? IssueDate { get; set; }
    public DateTime? ValidUntil { get; set; }

    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerNIP { get; set; }

    public string? Currency { get; set; }

    public decimal TotalNet { get; set; }
    public decimal TotalVat { get; set; }
    public decimal TotalGross { get; set; }

    public string? Status { get; set; }
    public string? Notes { get; set; }
    public DateTime? CreatedAt { get; set; }

    public List<DocumentItemDto> Items { get; set; } = new();
}
