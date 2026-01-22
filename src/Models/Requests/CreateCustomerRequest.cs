using System.ComponentModel.DataAnnotations;
using NexoSferaApi.Models.Dto;

namespace NexoSferaApi.Models.Requests;

public class CreateCustomerRequest
{
    [Required]
    [MaxLength(20)]
    public string Symbol { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string ShortName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? FullName { get; set; }

    [MaxLength(13)]
    public string? NIP { get; set; }

    [MaxLength(14)]
    public string? REGON { get; set; }

    [EmailAddress]
    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(200)]
    public string? Website { get; set; }

    public CustomerType Type { get; set; } = CustomerType.Company;

    public CreateAddressRequest? Address { get; set; }

    [MaxLength(34)]
    public string? BankAccount { get; set; }

    [MaxLength(100)]
    public string? BankName { get; set; }
}

public class CreateAddressRequest
{
    [MaxLength(100)]
    public string? Street { get; set; }

    [MaxLength(10)]
    public string? BuildingNumber { get; set; }

    [MaxLength(10)]
    public string? ApartmentNumber { get; set; }

    [Required]
    [MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [Required]
    [MaxLength(10)]
    public string PostalCode { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Country { get; set; } = "Polska";
}

public class UpdateCustomerRequest
{
    [MaxLength(200)]
    public string? ShortName { get; set; }

    [MaxLength(500)]
    public string? FullName { get; set; }

    [MaxLength(13)]
    public string? NIP { get; set; }

    [MaxLength(14)]
    public string? REGON { get; set; }

    [EmailAddress]
    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(200)]
    public string? Website { get; set; }

    public CreateAddressRequest? Address { get; set; }

    [MaxLength(34)]
    public string? BankAccount { get; set; }

    [MaxLength(100)]
    public string? BankName { get; set; }
}
