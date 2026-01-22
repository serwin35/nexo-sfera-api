namespace NexoSferaApi.Models.Dto;

public class CustomerDto
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? NIP { get; set; }
    public string? TaxId { get => NIP; set => NIP = value; }
    public string? REGON { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Website { get; set; }
    public CustomerType Type { get; set; }
    public AddressDto? Address { get; set; }
    public string? BankAccount { get; set; }
    public string? BankName { get; set; }
    public bool IsActive { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
}

public class AddressDto
{
    public string? Street { get; set; }
    public string? BuildingNumber { get; set; }
    public string? ApartmentNumber { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
}

public enum CustomerType
{
    Company = 0,
    Person = 1
}
