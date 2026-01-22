using System.ComponentModel.DataAnnotations;

namespace NexoSferaApi.Models.Requests;

/// <summary>
/// Request for creating a contractor group
/// </summary>
public class CreateContractorGroupRequest
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
    /// Default payment method ID for contractors in this group
    /// </summary>
    public int? DefaultPaymentMethodId { get; set; }

    /// <summary>
    /// Default payment days
    /// </summary>
    [Range(0, 365)]
    public int? DefaultPaymentDays { get; set; }

    /// <summary>
    /// Default discount percent
    /// </summary>
    [Range(0, 100)]
    public decimal? DefaultDiscountPercent { get; set; }

    /// <summary>
    /// Default price level ID
    /// </summary>
    public int? DefaultPriceLevelId { get; set; }

    /// <summary>
    /// Default credit limit
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? DefaultCreditLimit { get; set; }
}

/// <summary>
/// Request for updating a contractor group
/// </summary>
public class UpdateContractorGroupRequest
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
    /// Default payment method ID
    /// </summary>
    public int? DefaultPaymentMethodId { get; set; }

    /// <summary>
    /// Default payment days
    /// </summary>
    [Range(0, 365)]
    public int? DefaultPaymentDays { get; set; }

    /// <summary>
    /// Default discount percent
    /// </summary>
    [Range(0, 100)]
    public decimal? DefaultDiscountPercent { get; set; }

    /// <summary>
    /// Default price level ID
    /// </summary>
    public int? DefaultPriceLevelId { get; set; }

    /// <summary>
    /// Default credit limit
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? DefaultCreditLimit { get; set; }

    /// <summary>
    /// Is active flag
    /// </summary>
    public bool? IsActive { get; set; }
}

/// <summary>
/// Request for adding contractor to group
/// </summary>
public class AddContractorToGroupRequest
{
    /// <summary>
    /// Contractor ID
    /// </summary>
    [Required]
    public int ContractorId { get; set; }

    /// <summary>
    /// Is this the primary group for contractor
    /// </summary>
    public bool IsPrimary { get; set; }
}

/// <summary>
/// Request for bulk adding contractors to group
/// </summary>
public class BulkAddContractorsToGroupRequest
{
    /// <summary>
    /// Contractor IDs
    /// </summary>
    [Required]
    [MinLength(1)]
    public List<int> ContractorIds { get; set; } = new();
}

/// <summary>
/// Query parameters for contractor groups
/// </summary>
public class ContractorGroupQueryRequest
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
    /// Include contractor count
    /// </summary>
    public bool IncludeContractorCount { get; set; } = true;

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
