namespace NexoSferaApi.Models.Dto;

/// <summary>
/// VAT rate DTO
/// </summary>
public class VatRateDto
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string? Name { get; set; }
    public decimal Rate { get; set; }
    public bool IsActive { get; set; }
    public VatRateType Type { get; set; }
}

public enum VatRateType
{
    Standard = 0,
    Reduced = 1,
    SuperReduced = 2,
    Zero = 3,
    Exempt = 4
}

/// <summary>
/// Unit of measure DTO
/// </summary>
public class UnitOfMeasureDto
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string? Name { get; set; }
    public int? DecimalPlaces { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Product group/category DTO
/// </summary>
public class ProductGroupDto
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int? ParentId { get; set; }
    public string? ParentSymbol { get; set; }
    public bool IsActive { get; set; }
    public int ProductCount { get; set; }
    public List<ProductGroupDto>? Children { get; set; }
}

/// <summary>
/// Price level DTO
/// </summary>
public class PriceLevelDto
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public int Priority { get; set; }
}

/// <summary>
/// Price list DTO
/// </summary>
public class PriceListDto
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Description { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public bool IsActive { get; set; }
    public string? CurrencySymbol { get; set; }
    public int ItemCount { get; set; }
}

/// <summary>
/// Price list item DTO
/// </summary>
public class PriceListItemDto
{
    public int Id { get; set; }
    public int PriceListId { get; set; }
    public int ProductId { get; set; }
    public string? ProductSymbol { get; set; }
    public string? ProductName { get; set; }
    public decimal PriceNet { get; set; }
    public decimal PriceGross { get; set; }
    public string? VatRate { get; set; }
    public decimal? MinQuantity { get; set; }
    public decimal? MaxQuantity { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
}

/// <summary>
/// Currency DTO
/// </summary>
public class CurrencyDto
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? IsoCode { get; set; }
    public decimal? ExchangeRate { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Exchange rate DTO
/// </summary>
public class ExchangeRateDto
{
    public int Id { get; set; }
    public string CurrencySymbol { get; set; } = string.Empty;
    public DateTime? Date { get; set; }
    public decimal Rate { get; set; }
    public int Multiplier { get; set; } = 1;
    public string? Source { get; set; }
    public string? TableNumber { get; set; }
}

/// <summary>
/// Payment method DTO
/// </summary>
public class PaymentMethodDto
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string? Name { get; set; }
    public PaymentMethodType Type { get; set; }
    public int? DefaultDueDays { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
}

public enum PaymentMethodType
{
    Cash = 0,
    BankTransfer = 1,
    Card = 2,
    DirectDebit = 3,
    Compensation = 4,
    Other = 99
}
