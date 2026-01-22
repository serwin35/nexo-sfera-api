using System.ComponentModel.DataAnnotations;
using NexoSferaApi.Models.Dto;

namespace NexoSferaApi.Models.Requests;

public class CreateProductRequest
{
    [Required]
    [MaxLength(50)]
    public string Symbol { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(20)]
    public string? EAN { get; set; }

    [MaxLength(15)]
    public string? PKWiU { get; set; }

    public ProductType Type { get; set; } = ProductType.Goods;

    [MaxLength(10)]
    public string SaleUnit { get; set; } = "szt.";

    [MaxLength(10)]
    public string? PurchaseUnit { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? PriceNet { get; set; }

    [MaxLength(5)]
    public string VatRate { get; set; } = "23%";

    [Range(0, double.MaxValue)]
    public decimal? Weight { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? Volume { get; set; }

    public string? TemplateSymbol { get; set; }
}

public class UpdateProductRequest
{
    [MaxLength(200)]
    public string? Name { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(20)]
    public string? EAN { get; set; }

    [MaxLength(15)]
    public string? PKWiU { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? PriceNet { get; set; }

    [MaxLength(5)]
    public string? VatRate { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? Weight { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? Volume { get; set; }

    public bool? IsActive { get; set; }
}
