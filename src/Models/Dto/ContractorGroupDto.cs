namespace NexoSferaApi.Models.Dto;

/// <summary>
/// Contractor group (GrupaPodmiotow/GrupaKontrahentow) list item DTO
/// </summary>
public class ContractorGroupListItemDto
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? ParentId { get; set; }
    public string? ParentSymbol { get; set; }
    public int Level { get; set; }
    public int ContractorCount { get; set; }
    public bool HasChildren { get; set; }
}

/// <summary>
/// Full contractor group DTO
/// Based on InsERT.Moria.ModelDanych.GrupaPodmiotow
/// </summary>
public class ContractorGroupDto
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
    public int ContractorCount { get; set; }
    public int DirectContractorCount { get; set; }
    public int ChildGroupCount { get; set; }

    // Default settings for contractors in this group
    public int? DefaultPaymentMethodId { get; set; }
    public string? DefaultPaymentMethodName { get; set; }
    public int? DefaultPaymentDays { get; set; }
    public decimal? DefaultDiscountPercent { get; set; }
    public int? DefaultPriceLevelId { get; set; }
    public string? DefaultPriceLevelName { get; set; }
    public decimal? DefaultCreditLimit { get; set; }

    // Status
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

    // Children (for tree view)
    public List<ContractorGroupListItemDto>? Children { get; set; }

    // Timestamps
    public DateTime? CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
}

/// <summary>
/// Contractor group tree node DTO
/// </summary>
public class ContractorGroupTreeDto
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? ParentId { get; set; }
    public int Level { get; set; }
    public int ContractorCount { get; set; }
    public List<ContractorGroupTreeDto> Children { get; set; } = new();
}

/// <summary>
/// Contractor's group membership DTO
/// </summary>
public class ContractorGroupMembershipDto
{
    public int ContractorId { get; set; }
    public string? ContractorSymbol { get; set; }
    public string? ContractorName { get; set; }
    public int GroupId { get; set; }
    public string? GroupSymbol { get; set; }
    public string? GroupName { get; set; }
    public DateTime? JoinDate { get; set; }
    public bool IsPrimary { get; set; }
}
