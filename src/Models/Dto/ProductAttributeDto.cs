namespace NexoSferaApi.Models.Dto;

/// <summary>
/// Product attribute/feature (CechaAsortymentu) list item DTO
/// </summary>
public class ProductAttributeListItemDto
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? AttributeType { get; set; }
    public int ValueCount { get; set; }
    public int ProductCount { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Full product attribute DTO
/// Based on InsERT.Moria.ModelDanych.CechaAsortymentu
/// </summary>
public class ProductAttributeDto
{
    // Identity
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Type
    public int Type { get; set; }
    public string? TypeDescription { get; set; }

    // Group
    public int? GroupId { get; set; }
    public string? GroupName { get; set; }

    // Flags
    public bool IsRequired { get; set; }
    public bool IsMultiValue { get; set; }
    public bool IsSearchable { get; set; }
    public bool IsFilterable { get; set; }
    public bool ShowOnList { get; set; }
    public bool ShowOnCard { get; set; }

    // Sort order
    public int SortOrder { get; set; }

    // Status
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

    // Values (predefined options)
    public List<ProductAttributeValueDto>? Values { get; set; }

    // Statistics
    public int ProductCount { get; set; }

    // Timestamps
    public DateTime? CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
}

/// <summary>
/// Product attribute predefined value DTO
/// </summary>
public class ProductAttributeValueDto
{
    public int Id { get; set; }
    public string Value { get; set; } = string.Empty;
    public string? DisplayValue { get; set; }
    public int SortOrder { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public int ProductCount { get; set; }
}

/// <summary>
/// Product's attribute assignment DTO
/// </summary>
public class ProductAttributeAssignmentDto
{
    public int Id { get; set; }
    public int AttributeId { get; set; }
    public string? AttributeSymbol { get; set; }
    public string? AttributeName { get; set; }
    public string Value { get; set; } = string.Empty;
    public int? ValueId { get; set; }
}

/// <summary>
/// Attribute group DTO
/// </summary>
public class AttributeGroupDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public int AttributeCount { get; set; }
    public List<ProductAttributeListItemDto>? Attributes { get; set; }
}
