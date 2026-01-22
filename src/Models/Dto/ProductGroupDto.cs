namespace NexoSferaApi.Models.Dto;

/// <summary>
/// Product group (GrupaAsortymentu) list item DTO
/// </summary>
public class ProductGroupListItemDto
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? ParentId { get; set; }
    public string? ParentSymbol { get; set; }
    public int Level { get; set; }
    public int ProductCount { get; set; }
    public bool HasChildren { get; set; }
}

/// <summary>
/// Full product group DTO
/// Based on InsERT.Moria.ModelDanych.GrupaAsortymentu
/// </summary>
public class ProductGroupDto
{
    // Identity
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Hierarchy
    public int? ParentId { get; set; }
    public string? ParentSymbol { get; set; }
    public string? ParentName { get; set; }
    public int Level { get; set; }
    public string? Path { get; set; }

    // Statistics
    public int ProductCount { get; set; }
    public int DirectProductCount { get; set; }
    public int ChildGroupCount { get; set; }

    // Margins
    public int? MinimalMarginId { get; set; }
    public decimal? MinimalMarginPercent { get; set; }

    // Pricing
    public int? DefaultPriceLevelId { get; set; }
    public string? DefaultPriceLevelName { get; set; }

    // VAT
    public int? DefaultVatRateId { get; set; }
    public string? DefaultVatRateSymbol { get; set; }

    // Status
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

    // Children (for tree view)
    public List<ProductGroupListItemDto>? Children { get; set; }

    // Timestamps
    public DateTime? CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
}

/// <summary>
/// Product group tree node DTO
/// </summary>
public class ProductGroupTreeDto
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? ParentId { get; set; }
    public int Level { get; set; }
    public int ProductCount { get; set; }
    public List<ProductGroupTreeDto> Children { get; set; } = new();
}
