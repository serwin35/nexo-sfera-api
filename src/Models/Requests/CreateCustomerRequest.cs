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

    /// <summary>
    /// EU VAT number
    /// </summary>
    [MaxLength(20)]
    public string? EuTaxId { get; set; }

    [MaxLength(14)]
    public string? REGON { get; set; }

    /// <summary>
    /// Supplier identification number (SUN)
    /// </summary>
    [MaxLength(64)]
    public string? SUN { get; set; }

    [EmailAddress]
    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(200)]
    public string? Website { get; set; }

    public CustomerType Type { get; set; } = CustomerType.Company;

    /// <summary>
    /// Is this entity a contractor
    /// </summary>
    public bool IsContractor { get; set; } = true;

    /// <summary>
    /// Contractor type: Customer, Supplier, or Both
    /// </summary>
    public ContractorType ContractorType { get; set; } = ContractorType.Customer;

    /// <summary>
    /// Active status
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// One-time customer
    /// </summary>
    public bool IsOneTime { get; set; } = false;

    public CreateAddressRequest? Address { get; set; }

    [MaxLength(34)]
    public string? BankAccount { get; set; }

    [MaxLength(100)]
    public string? BankName { get; set; }

    // Payment terms
    /// <summary>
    /// Payment term in days for sales
    /// </summary>
    public int? PaymentTermSales { get; set; }

    /// <summary>
    /// Payment term in days for purchases
    /// </summary>
    public int? PaymentTermPurchase { get; set; }

    // Credit limits
    /// <summary>
    /// Trade credit limit
    /// </summary>
    public decimal? TradeCreditLimit { get; set; }

    /// <summary>
    /// Allow trade credit
    /// </summary>
    public bool AllowTradeCredit { get; set; } = false;

    // VAT & Tax
    /// <summary>
    /// EU taxpayer status
    /// </summary>
    public bool IsEuTaxpayer { get; set; } = false;

    /// <summary>
    /// Always use EU VAT number
    /// </summary>
    public bool AlwaysUseEuVat { get; set; } = false;

    /// <summary>
    /// VAT deductible status
    /// </summary>
    public bool VatDeductible { get; set; } = true;

    /// <summary>
    /// Agricultural producer (rolnik ryczałtowy)
    /// </summary>
    public bool AgriculturalProducer { get; set; } = false;

    // Blocking & Messages
    /// <summary>
    /// Block document issuance for this customer
    /// </summary>
    public bool DocumentBlock { get; set; } = false;

    /// <summary>
    /// Display warning message when selecting this customer
    /// </summary>
    public bool DisplayMessage { get; set; } = false;

    /// <summary>
    /// Warning message text
    /// </summary>
    [MaxLength(2000)]
    public string? MessageText { get; set; }

    /// <summary>
    /// Additional notes
    /// </summary>
    [MaxLength(256)]
    public string? Notes { get; set; }

    // Programs
    /// <summary>
    /// Participant of loyalty program
    /// </summary>
    public bool LoyaltyProgramParticipant { get; set; } = false;
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

    [MaxLength(20)]
    public string? EuTaxId { get; set; }

    [MaxLength(14)]
    public string? REGON { get; set; }

    [MaxLength(64)]
    public string? SUN { get; set; }

    [EmailAddress]
    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(200)]
    public string? Website { get; set; }

    public ContractorType? ContractorType { get; set; }

    public bool? IsActive { get; set; }

    public bool? IsOneTime { get; set; }

    public CreateAddressRequest? Address { get; set; }

    [MaxLength(34)]
    public string? BankAccount { get; set; }

    [MaxLength(100)]
    public string? BankName { get; set; }

    // Payment terms
    public int? PaymentTermSales { get; set; }
    public int? PaymentTermPurchase { get; set; }

    // Credit limits
    public decimal? TradeCreditLimit { get; set; }
    public bool? AllowTradeCredit { get; set; }

    // VAT & Tax
    public bool? IsEuTaxpayer { get; set; }
    public bool? AlwaysUseEuVat { get; set; }
    public bool? VatDeductible { get; set; }
    public bool? AgriculturalProducer { get; set; }

    // Blocking & Messages
    public bool? DocumentBlock { get; set; }
    public bool? DisplayMessage { get; set; }

    [MaxLength(2000)]
    public string? MessageText { get; set; }

    [MaxLength(256)]
    public string? Notes { get; set; }

    // Programs
    public bool? LoyaltyProgramParticipant { get; set; }
}

public class CustomerQueryRequest
{
    /// <summary>
    /// Search by code, name or NIP
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Filter by customer type (Company/Person)
    /// </summary>
    public CustomerType? Type { get; set; }

    /// <summary>
    /// Filter by contractor type (Customer/Supplier/Both)
    /// </summary>
    public ContractorType? ContractorType { get; set; }

    /// <summary>
    /// Show only active customers (default: true)
    /// </summary>
    public bool ActiveOnly { get; set; } = true;

    /// <summary>
    /// Filter: only contractors
    /// </summary>
    public bool ContractorsOnly { get; set; } = true;

    /// <summary>
    /// Filter: only customers with document block
    /// </summary>
    public bool? HasDocumentBlock { get; set; }

    /// <summary>
    /// Filter: only EU taxpayers
    /// </summary>
    public bool? IsEuTaxpayer { get; set; }

    /// <summary>
    /// Load all fields (default: false for better performance)
    /// </summary>
    public bool Full { get; set; } = false;

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
