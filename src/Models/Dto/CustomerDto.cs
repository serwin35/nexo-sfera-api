namespace NexoSferaApi.Models.Dto;

/// <summary>
/// Lightweight customer DTO for list views (minimal fields for performance)
/// Matches v1 API list view
/// </summary>
public class CustomerListItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? TaxId { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; }
    public ContractorType ContractorType { get; set; }
}

/// <summary>
/// Full customer DTO with all available fields (for detail view)
/// Matches v1 API detail view
/// </summary>
public class CustomerDto
{
    // Basic info
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? NIP { get; set; }
    public string? TaxId { get => NIP; set => NIP = value; }
    public string? TaxIdFormatted { get; set; }
    public string? EuTaxId { get; set; }
    public string? SUN { get; set; }
    public string? REGON { get; set; }

    // Personal/Company info
    public string? Greeting { get; set; }
    public string? AcademicTitle { get; set; }

    // Contact
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Website { get; set; }

    // Type & Status
    public CustomerType Type { get; set; }
    public int? Subtype { get; set; }
    public bool IsContractor { get; set; }
    public ContractorType ContractorType { get; set; }
    public bool IsActive { get; set; }
    public bool IsOneTime { get; set; }
    public int? CustomerStatus { get; set; }

    // Credit & Limits
    public decimal? TradeCreditLimit { get; set; }
    public bool AllowTradeCredit { get; set; }
    public bool SalesCreditLimitActive { get; set; }
    public bool DeliveryCreditLimitActive { get; set; }
    public bool OrderCreditLimitActive { get; set; }
    public int? MaxCreditPaymentTerm { get; set; }
    public int? MaxUnpaidDocuments { get; set; }
    public int? MaxDelayDays { get; set; }

    // Pricing & Negotiation
    public bool PriceNegotiationAllowed { get; set; }
    public string? PriceCalculationFunction { get; set; }

    // Payment
    public int? PaymentTermSales { get; set; }
    public int? PaymentTermPurchase { get; set; }
    public int? PaymentDaySales { get; set; }
    public int? PaymentDayPurchase { get; set; }
    public int? DefaultReceivablesSettlement { get; set; }
    public int? DefaultLiabilitiesSettlement { get; set; }
    public bool CashMethod { get; set; }

    // Interest
    public string? AppliedInterest { get; set; }
    public decimal? InterestCalculationParam { get; set; }

    // Programs & Features
    public bool LoyaltyProgramParticipant { get; set; }

    // Data protection (GDPR)
    public bool? PersonalDataProcessing { get; set; }
    public bool? MarketingPurposes { get; set; }
    public bool? ElectronicProcessing { get; set; }
    public DateTime? AcquisitionDate { get; set; }
    public DateTime? LossDate { get; set; }

    // Blocking & Messages
    public bool DocumentBlock { get; set; }
    public bool DisplayMessage { get; set; }
    public string? MessageText { get; set; }
    public int? MessageDisplayType { get; set; }

    // VAT & Tax
    public bool IsEuTaxpayer { get; set; }
    public bool AlwaysUseEuVat { get; set; }
    public bool VatDeductible { get; set; }
    public int? JpkSalesProcedure { get; set; }
    public int? JpkPurchaseProcedure { get; set; }
    public bool AgriculturalProducer { get; set; }
    public int? SugarTaxHandling { get; set; }

    // Accounting
    public bool UseAccountingParams { get; set; }
    public bool UseAutoPostingParams { get; set; }

    // E-commerce / Vendero
    public int? VenderoCustomerId { get; set; }
    public bool EcommerceCustomer { get; set; }
    public bool UseDefaultEcommerceDiscount { get; set; }
    public bool UseIndividualEcommercePricing { get; set; }
    public bool VenderoNewsletterConsent { get; set; }

    // Delivery preferences
    public string? PreferredTimeFrom { get; set; }
    public string? PreferredTimeTo { get; set; }
    public int? DefaultAdditionalAddressType { get; set; }

    // Mobile
    public int? MobileRemoteSourceId { get; set; }
    public string? MobileCreatorId { get; set; }
    public bool SendToMobile { get; set; }

    // Address
    public AddressDto? Address { get; set; }

    // Bank account
    public string? BankAccount { get; set; }
    public string? BankName { get; set; }

    // Notes
    public string? Notes { get; set; }
    public int? TransactionTypeCodeId { get; set; }
    public string? CountedDocument { get; set; }

    // Timestamps
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

    // Alternative property names for compatibility
    public string? Building { get => BuildingNumber; set => BuildingNumber = value; }
    public string? Apartment { get => ApartmentNumber; set => ApartmentNumber = value; }
    public string? CountryCode { get; set; }
}

public enum CustomerType
{
    Company = 0,
    Person = 1
}

public enum ContractorType
{
    Customer = 0,
    Supplier = 1,
    Both = 2
}
