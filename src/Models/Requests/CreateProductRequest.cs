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

/// <summary>
/// Partial update of a product. Omitted (null) fields are left unchanged. The update is all-or-nothing: any rejected
/// field returns 400 and nothing is saved. The response lists the outcome of every field sent (fieldResults).
/// </summary>
public class UpdateProductRequest
{
    /// <summary>Product name (Asortyment.Nazwa). Must not be empty.</summary>
    [MaxLength(200)]
    public string? Name { get; set; }

    /// <summary>Description (Asortyment.Opis). An empty string clears it.</summary>
    [MaxLength(2000)]
    public string? Description { get; set; }

    /// <summary>
    /// EAN: written as the primary barcode (PodstawowyKodKreskowy) of the unit that currently provides the product EAN
    /// (see ProductDto.eanUnitSymbol), or of <see cref="EanUnitSymbol"/>, or of the base unit when the product has no
    /// barcode yet. A code already owned by another product is rejected. Must not be empty.
    /// </summary>
    [MaxLength(20)]
    public string? EAN { get; set; }

    /// <summary>Optional: symbol of the product unit that receives <see cref="EAN"/>. Only allowed together with ean.</summary>
    [MaxLength(10)]
    public string? EanUnitSymbol { get; set; }

    /// <summary>PKWiU (Asortyment.PKWiU). An empty string clears it.</summary>
    [MaxLength(15)]
    public string? PKWiU { get; set; }

    /// <summary>Rejected with 400: Nexo keeps sales prices in price lists, which this endpoint does not write.</summary>
    [Range(0, double.MaxValue)]
    public decimal? PriceNet { get; set; }

    /// <summary>Rejected with 400: changing the VAT rate is not supported here.</summary>
    [MaxLength(5)]
    public string? VatRate { get; set; }

    /// <summary>
    /// Gross weight of one base unit in kilograms. Stored in the base unit (Masa), converted to its mass unit; when the
    /// base unit has no mass unit yet it is set to kilograms.
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? Weight { get; set; }

    /// <summary>Volume of one base unit, in the base unit's volume unit (ProductDto.volumeUnitSymbol), which must be set.</summary>
    [Range(0, double.MaxValue)]
    public decimal? Volume { get; set; }

    /// <summary>Rejected with 400: Nexo has no activity flag on products (inactive = in the recycle bin).</summary>
    public bool? IsActive { get; set; }
}
