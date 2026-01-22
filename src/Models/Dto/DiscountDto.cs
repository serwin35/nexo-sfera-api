namespace NexoSferaApi.Models.Dto;

/// <summary>
/// Discount (Rabat) DTO
/// </summary>
public class DiscountDto
{
    public int Id { get; set; }
    public string? Symbol { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public DiscountType Type { get; set; }
    public decimal? PercentValue { get; set; }
    public decimal? AmountValue { get; set; }
    public string? CurrencySymbol { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public bool IsActive { get; set; }
    public int? Priority { get; set; }
    public List<DiscountConditionDto>? Conditions { get; set; }
    public List<DiscountSubjectDto>? Subjects { get; set; }
}

/// <summary>
/// Discount type enum
/// </summary>
public enum DiscountType
{
    Percentage = 0,
    Amount = 1,
    FixedPrice = 2,
    Progressive = 3
}

/// <summary>
/// Discount condition DTO
/// </summary>
public class DiscountConditionDto
{
    public int Id { get; set; }
    public string? ConditionType { get; set; }
    public string? Description { get; set; }
    public decimal? MinQuantity { get; set; }
    public decimal? MaxQuantity { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
}

/// <summary>
/// Discount subject (who/what the discount applies to) DTO
/// </summary>
public class DiscountSubjectDto
{
    public int Id { get; set; }
    public string? SubjectType { get; set; }
    public int? SubjectId { get; set; }
    public string? SubjectSymbol { get; set; }
    public string? SubjectName { get; set; }
}

/// <summary>
/// Product attribute/feature (Cecha asortymentu) DTO
/// </summary>
public class ProductAttributeDto
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int ProductCount { get; set; }
    public List<ProductAttributeValueDto>? Values { get; set; }
}

/// <summary>
/// Product attribute value DTO
/// </summary>
public class ProductAttributeValueDto
{
    public int Id { get; set; }
    public string? Value { get; set; }
    public string? Description { get; set; }
    public int ProductCount { get; set; }
}

/// <summary>
/// Contractor group (Grupa kontrahentów) DTO
/// </summary>
public class ContractorGroupDto
{
    public int Id { get; set; }
    public string? Symbol { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int? ParentId { get; set; }
    public string? ParentSymbol { get; set; }
    public bool IsActive { get; set; }
    public int ContractorCount { get; set; }
    public List<ContractorGroupDto>? Children { get; set; }
}
