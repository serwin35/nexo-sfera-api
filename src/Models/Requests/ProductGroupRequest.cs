using System.ComponentModel.DataAnnotations;

namespace NexoSferaApi.Models.Requests;

/// <summary>
/// Request for creating a product group
/// </summary>
public class CreateProductGroupRequest
{
    /// <summary>
    /// Group symbol (unique identifier)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Group name
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
    /// Parent group ID (null for root groups)
    /// </summary>
    public int? ParentId { get; set; }

    /// <summary>
    /// Default minimal margin ID
    /// </summary>
    public int? MinimalMarginId { get; set; }

    /// <summary>
    /// Default price level ID
    /// </summary>
    public int? DefaultPriceLevelId { get; set; }

    /// <summary>
    /// Default VAT rate ID
    /// </summary>
    public int? DefaultVatRateId { get; set; }
}

/// <summary>
/// Request for updating a product group
/// </summary>
public class UpdateProductGroupRequest
{
    /// <summary>
    /// Group name
    /// </summary>
    [MaxLength(200)]
    public string? Name { get; set; }

    /// <summary>
    /// Description
    /// </summary>
    [MaxLength(2000)]
    public string? Description { get; set; }

    /// <summary>
    /// Default minimal margin ID
    /// </summary>
    public int? MinimalMarginId { get; set; }

    /// <summary>
    /// Default price level ID
    /// </summary>
    public int? DefaultPriceLevelId { get; set; }

    /// <summary>
    /// Default VAT rate ID
    /// </summary>
    public int? DefaultVatRateId { get; set; }

    /// <summary>
    /// Is group active
    /// </summary>
    public bool? IsActive { get; set; }
}

/// <summary>
/// Request for moving a product group in hierarchy
/// </summary>
public class MoveProductGroupRequest
{
    /// <summary>
    /// New parent group ID (null for root)
    /// </summary>
    public int? NewParentId { get; set; }

    /// <summary>
    /// Position in siblings (optional)
    /// </summary>
    public int? Position { get; set; }
}

/// <summary>
/// Request for assigning products to group
/// </summary>
public class AssignProductsToGroupRequest
{
    /// <summary>
    /// Product IDs to assign
    /// </summary>
    [Required]
    [MinLength(1)]
    public List<int> ProductIds { get; set; } = new();
}

/// <summary>
/// Query parameters for product groups
/// </summary>
public class ProductGroupQueryRequest
{
    /// <summary>
    /// Search by symbol or name
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Filter by parent group ID (null = root groups only)
    /// </summary>
    public int? ParentId { get; set; }

    /// <summary>
    /// Include all levels (tree)
    /// </summary>
    public bool IncludeAllLevels { get; set; }

    /// <summary>
    /// Include inactive groups
    /// </summary>
    public bool IncludeInactive { get; set; }

    /// <summary>
    /// Include deleted groups
    /// </summary>
    public bool IncludeDeleted { get; set; }

    /// <summary>
    /// Include product count
    /// </summary>
    public bool IncludeProductCount { get; set; } = true;

    /// <summary>
    /// Page number
    /// </summary>
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    /// <summary>
    /// Page size
    /// </summary>
    [Range(1, 1000)]
    public int PageSize { get; set; } = 100;
}
