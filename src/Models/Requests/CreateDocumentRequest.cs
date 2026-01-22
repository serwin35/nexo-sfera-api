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

    [MaxLength(10)]
    public string? PaymentMethod { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "PLN";

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [Required]
    [MinLength(1)]
    public List<CreateDocumentItemRequest> Items { get; set; } = new();
}

public class CreateDocumentItemRequest
{
    public int? ProductId { get; set; }

    [MaxLength(50)]
    public string? ProductSymbol { get; set; }

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
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? Status { get; set; }
    public int? StatusId { get; set; }
    public string? WarehouseSymbol { get; set; }
    public int? WarehouseId { get; set; }

    /// <summary>
    /// Show only paid documents
    /// </summary>
    public bool PaidOnly { get; set; }

    /// <summary>
    /// Show only unpaid documents
    /// </summary>
    public bool UnpaidOnly { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
