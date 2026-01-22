namespace NexoSferaApi.Models.Dto;

/// <summary>
/// Product template (SzablonAsortymentu) list item DTO
/// </summary>
public class ProductTemplateListItemDto
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Type { get; set; }
    public string? TypeDescription { get; set; }
    public string? UnitSymbol { get; set; }
    public string? VatRateSymbol { get; set; }
    public bool IsDeleted { get; set; }
}

/// <summary>
/// Full product template DTO
/// Based on InsERT.Moria.ModelDanych.SzablonAsortymentu
/// </summary>
public class ProductTemplateDto
{
    // Identity
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Type
    public int Type { get; set; }
    public string? TypeDescription { get; set; }

    // Unit
    public int? UnitId { get; set; }
    public string? UnitSymbol { get; set; }
    public string? UnitName { get; set; }
    public bool CopyUnitWithSubtree { get; set; }

    // VAT
    public int? VatRateId { get; set; }
    public string? VatRateSymbol { get; set; }
    public decimal? VatPercent { get; set; }

    // Product type
    public int? ProductTypeId { get; set; }
    public string? ProductTypeName { get; set; }

    // Pricing
    public byte CalculationFromValue { get; set; }
    public int? MinimalMarginId { get; set; }
    public decimal? MinimalMarginPercent { get; set; }

    // Tax/regulatory
    public byte ReverseCharge { get; set; }
    public byte SplitPayment { get; set; }
    public int? JpkProductGroup { get; set; }
    public string? JpkProductGroupName { get; set; }

    // Batches
    public byte BatchSplitMethod { get; set; }
    public int? BatchRegisterId { get; set; }

    // Intrastat
    public bool IncludeInIntrastat { get; set; }
    public byte IntrastatDescriptionSource { get; set; }
    public string? IntrastatDescription { get; set; }

    // Communication
    public bool SendToExternalDevice { get; set; }
    public bool ShowMessage { get; set; }
    public byte DefaultMessageForm { get; set; }
    public string? DefaultMessageContent { get; set; }

    // Flat-rate tax (ryczałt)
    public Guid? FlatRateId { get; set; }
    public string? FlatRateName { get; set; }

    // Reference product
    public int? ReferenceProductId { get; set; }
    public string? ReferenceProductSymbol { get; set; }
    public string? ReferenceProductName { get; set; }

    // Additional fee
    public int? AdditionalFeeProductId { get; set; }
    public string? AdditionalFeeProductSymbol { get; set; }

    // Status
    public bool IsDeleted { get; set; }

    // Timestamps
    public DateTime? CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
}
