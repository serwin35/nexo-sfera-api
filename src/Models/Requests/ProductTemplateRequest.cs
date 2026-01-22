using System.ComponentModel.DataAnnotations;

namespace NexoSferaApi.Models.Requests;

/// <summary>
/// Request for creating a product template
/// </summary>
public class CreateProductTemplateRequest
{
    /// <summary>
    /// Template symbol (unique identifier)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Template name
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description
    /// </summary>
    [MaxLength(2000)]
    public string? Description { get; set; }

    /// <summary>
    /// Product type (0=Goods, 1=Service, 2=Set, etc.)
    /// </summary>
    [Required]
    public int Type { get; set; }

    /// <summary>
    /// Unit symbol (e.g., "szt.", "kg", "m")
    /// </summary>
    [MaxLength(10)]
    public string? UnitSymbol { get; set; }

    /// <summary>
    /// Unit ID
    /// </summary>
    public int? UnitId { get; set; }

    /// <summary>
    /// VAT rate symbol (e.g., "23%", "8%", "ZW")
    /// </summary>
    [MaxLength(10)]
    public string? VatRateSymbol { get; set; }

    /// <summary>
    /// VAT rate ID
    /// </summary>
    public int? VatRateId { get; set; }

    /// <summary>
    /// Product type/category ID
    /// </summary>
    public int? ProductTypeId { get; set; }

    /// <summary>
    /// Reverse charge (0=No, 1=Yes, 2=Conditional)
    /// </summary>
    public byte ReverseCharge { get; set; } = 0;

    /// <summary>
    /// Split payment required (0=No, 1=Yes, 2=Conditional)
    /// </summary>
    public byte SplitPayment { get; set; } = 0;

    /// <summary>
    /// JPK product group ID
    /// </summary>
    public int? JpkProductGroup { get; set; }

    /// <summary>
    /// Include in Intrastat reports
    /// </summary>
    public bool IncludeInIntrastat { get; set; }

    /// <summary>
    /// Intrastat description
    /// </summary>
    [MaxLength(500)]
    public string? IntrastatDescription { get; set; }

    /// <summary>
    /// Reference product ID (for variants)
    /// </summary>
    public int? ReferenceProductId { get; set; }

    /// <summary>
    /// Show message when using this template
    /// </summary>
    public bool ShowMessage { get; set; }

    /// <summary>
    /// Default message content
    /// </summary>
    [MaxLength(500)]
    public string? DefaultMessageContent { get; set; }
}

/// <summary>
/// Request for updating a product template
/// </summary>
public class UpdateProductTemplateRequest
{
    /// <summary>
    /// Template name
    /// </summary>
    [MaxLength(200)]
    public string? Name { get; set; }

    /// <summary>
    /// Description
    /// </summary>
    [MaxLength(2000)]
    public string? Description { get; set; }

    /// <summary>
    /// VAT rate symbol
    /// </summary>
    [MaxLength(10)]
    public string? VatRateSymbol { get; set; }

    /// <summary>
    /// VAT rate ID
    /// </summary>
    public int? VatRateId { get; set; }

    /// <summary>
    /// Reverse charge setting
    /// </summary>
    public byte? ReverseCharge { get; set; }

    /// <summary>
    /// Split payment setting
    /// </summary>
    public byte? SplitPayment { get; set; }

    /// <summary>
    /// JPK product group ID
    /// </summary>
    public int? JpkProductGroup { get; set; }

    /// <summary>
    /// Include in Intrastat
    /// </summary>
    public bool? IncludeInIntrastat { get; set; }

    /// <summary>
    /// Intrastat description
    /// </summary>
    [MaxLength(500)]
    public string? IntrastatDescription { get; set; }

    /// <summary>
    /// Show message flag
    /// </summary>
    public bool? ShowMessage { get; set; }

    /// <summary>
    /// Message content
    /// </summary>
    [MaxLength(500)]
    public string? DefaultMessageContent { get; set; }
}

/// <summary>
/// Request for creating product from template
/// </summary>
public class CreateProductFromTemplateRequest
{
    /// <summary>
    /// Product symbol (unique)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Product name
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description
    /// </summary>
    [MaxLength(2000)]
    public string? Description { get; set; }

    /// <summary>
    /// EAN/barcode
    /// </summary>
    [MaxLength(20)]
    public string? EAN { get; set; }

    /// <summary>
    /// PKWiU code
    /// </summary>
    [MaxLength(20)]
    public string? PKWiU { get; set; }

    /// <summary>
    /// Base purchase price
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? PurchasePrice { get; set; }

    /// <summary>
    /// Base sale price (net)
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? SalePriceNet { get; set; }

    /// <summary>
    /// Product group ID
    /// </summary>
    public int? GroupId { get; set; }
}

/// <summary>
/// Query parameters for product templates
/// </summary>
public class ProductTemplateQueryRequest
{
    /// <summary>
    /// Search by symbol or name
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Filter by type
    /// </summary>
    public int? Type { get; set; }

    /// <summary>
    /// Include deleted templates
    /// </summary>
    public bool IncludeDeleted { get; set; }

    /// <summary>
    /// Page number
    /// </summary>
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    /// <summary>
    /// Page size
    /// </summary>
    [Range(1, 1000)]
    public int PageSize { get; set; } = 50;
}
