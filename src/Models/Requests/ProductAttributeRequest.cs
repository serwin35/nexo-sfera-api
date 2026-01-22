using System.ComponentModel.DataAnnotations;

namespace NexoSferaApi.Models.Requests;

/// <summary>
/// Request for creating a product attribute
/// </summary>
public class CreateProductAttributeRequest
{
    /// <summary>
    /// Attribute symbol (unique identifier)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Attribute name
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
    /// Attribute type (0=Text, 1=Number, 2=Date, 3=Boolean, 4=List)
    /// </summary>
    public int Type { get; set; }

    /// <summary>
    /// Attribute group ID
    /// </summary>
    public int? GroupId { get; set; }

    /// <summary>
    /// Is attribute required
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Allow multiple values
    /// </summary>
    public bool IsMultiValue { get; set; }

    /// <summary>
    /// Include in search
    /// </summary>
    public bool IsSearchable { get; set; } = true;

    /// <summary>
    /// Include in filters
    /// </summary>
    public bool IsFilterable { get; set; } = true;

    /// <summary>
    /// Show on product list
    /// </summary>
    public bool ShowOnList { get; set; }

    /// <summary>
    /// Show on product card
    /// </summary>
    public bool ShowOnCard { get; set; } = true;

    /// <summary>
    /// Sort order
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Predefined values (for list type)
    /// </summary>
    public List<CreateProductAttributeValueRequest>? Values { get; set; }
}

/// <summary>
/// Request for creating attribute predefined value
/// </summary>
public class CreateProductAttributeValueRequest
{
    /// <summary>
    /// Value
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Display value (optional, uses Value if not provided)
    /// </summary>
    [MaxLength(200)]
    public string? DisplayValue { get; set; }

    /// <summary>
    /// Sort order
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Is default value
    /// </summary>
    public bool IsDefault { get; set; }
}

/// <summary>
/// Request for updating a product attribute
/// </summary>
public class UpdateProductAttributeRequest
{
    /// <summary>
    /// Attribute name
    /// </summary>
    [MaxLength(200)]
    public string? Name { get; set; }

    /// <summary>
    /// Description
    /// </summary>
    [MaxLength(2000)]
    public string? Description { get; set; }

    /// <summary>
    /// Attribute group ID
    /// </summary>
    public int? GroupId { get; set; }

    /// <summary>
    /// Is required flag
    /// </summary>
    public bool? IsRequired { get; set; }

    /// <summary>
    /// Is searchable flag
    /// </summary>
    public bool? IsSearchable { get; set; }

    /// <summary>
    /// Is filterable flag
    /// </summary>
    public bool? IsFilterable { get; set; }

    /// <summary>
    /// Show on list flag
    /// </summary>
    public bool? ShowOnList { get; set; }

    /// <summary>
    /// Show on card flag
    /// </summary>
    public bool? ShowOnCard { get; set; }

    /// <summary>
    /// Sort order
    /// </summary>
    public int? SortOrder { get; set; }

    /// <summary>
    /// Is active flag
    /// </summary>
    public bool? IsActive { get; set; }
}

/// <summary>
/// Request for assigning attribute to product
/// </summary>
public class AssignProductAttributeRequest
{
    /// <summary>
    /// Product ID
    /// </summary>
    [Required]
    public int ProductId { get; set; }

    /// <summary>
    /// Attribute ID
    /// </summary>
    [Required]
    public int AttributeId { get; set; }

    /// <summary>
    /// Value (text)
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Predefined value ID (for list type)
    /// </summary>
    public int? ValueId { get; set; }
}

/// <summary>
/// Query parameters for product attributes
/// </summary>
public class ProductAttributeQueryRequest
{
    /// <summary>
    /// Search by symbol or name
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Filter by group ID
    /// </summary>
    public int? GroupId { get; set; }

    /// <summary>
    /// Filter by type
    /// </summary>
    public int? Type { get; set; }

    /// <summary>
    /// Include inactive
    /// </summary>
    public bool IncludeInactive { get; set; }

    /// <summary>
    /// Include deleted
    /// </summary>
    public bool IncludeDeleted { get; set; }

    /// <summary>
    /// Include values
    /// </summary>
    public bool IncludeValues { get; set; }

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
