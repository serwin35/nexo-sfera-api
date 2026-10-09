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
/// Item of GET /api/customers?full=true: the full contractor card plus the list item aliases
/// (<see cref="Name"/> = short name, taxId = NIP).
/// </summary>
public class CustomerFullListItemDto : CustomerDto
{
    /// <summary>Short name, as in the light list item (CustomerListItemDto.name).</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Full customer DTO with all available fields (for detail view)
/// Matches v1 API detail view
/// </summary>
public class CustomerDto
{
    // Basic info
    public int Id { get; set; }
    /// <summary>Contractor symbol (Podmiot.Sygnatura.PelnaSygnatura).</summary>
    public string Symbol { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    /// <summary>Company: full name (Firma.Nazwa). Person: first and last name (Osoba.Imie + Nazwisko).</summary>
    public string? FullName { get; set; }
    /// <summary>Person only: first name (Osoba.Imie).</summary>
    public string? FirstName { get; set; }
    /// <summary>Person only: last name (Osoba.Nazwisko).</summary>
    public string? LastName { get; set; }
    public string? NIP { get; set; }
    public string? TaxId { get => NIP; set => NIP = value; }
    public string? TaxIdFormatted { get; set; }
    public string? EuTaxId { get; set; }
    public string? SUN { get; set; }
    /// <summary>Company only (Firma.REGON).</summary>
    public string? REGON { get; set; }
    /// <summary>Company only (Firma.KRS).</summary>
    public string? KRS { get; set; }

    // Personal/Company info
    public string? Greeting { get; set; }
    public string? AcademicTitle { get; set; }

    // Contact
    /// <summary>Primary e-mail contact (Kontakty of kind E-mail, Podstawowy first).</summary>
    public string? Email { get; set; }
    /// <summary>All e-mail contacts, primary first.</summary>
    public List<string> Emails { get; set; } = new();
    /// <summary>Podmiot.Telefon (the primary phone copied by Nexo), else the first phone contact.</summary>
    public string? Phone { get; set; }
    /// <summary>Website contact (kind StronaInternetowa); falls back to <see cref="EmailDomain"/> (the legacy value).</summary>
    public string? Website { get; set; }
    /// <summary>Podmiot.Domena: domain Nexo uses to bind incoming e-mail to the contractor (not a URL).</summary>
    public string? EmailDomain { get; set; }

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
    /// <summary>Currency of the credit limits (Podmiot.WalutaLimitow.Symbol).</summary>
    public string? CreditLimitCurrency { get; set; }
    /// <summary>Trade credit limit for sales documents (LimitKredytuNaSprzedazy.Wartosc); see SalesCreditLimitActive.</summary>
    public decimal? SalesCreditLimit { get; set; }
    /// <summary>Trade credit limit for warehouse issues (LimitKredytuNaWydaniu.Wartosc); see DeliveryCreditLimitActive.</summary>
    public decimal? DeliveryCreditLimit { get; set; }
    /// <summary>Trade credit limit for orders (LimitKredytuNaZamowieniu.Wartosc); see OrderCreditLimitActive.</summary>
    public decimal? OrderCreditLimit { get; set; }
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
    /// <summary>Main address (AdresPodstawowy, else the first address).</summary>
    public AddressDto? Address { get; set; }
    /// <summary>Default delivery address (DomyslnyAdresDostaw); null when none is set.</summary>
    public AddressDto? DeliveryAddress { get; set; }

    // Bank account
    /// <summary>Number of the primary bank account (RachunekPodstawowy.Numer, else the first active account).</summary>
    public string? BankAccount { get; set; }
    /// <summary>Short name of the bank keeping the account (RachunekBankowy.PodmiotBankowy.NazwaSkrocona).</summary>
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

    /// <summary>EU country code of the address country (Panstwo.KodPanstwaUE, ISO 3166); null outside the EU.</summary>
    public string? CountryEuCode { get; set; }

    /// <summary>
    /// Global Location Number (SDK 61.0.0: Adres.GLN / AdresHistoria.GLN).
    /// </summary>
    public string? GLN { get; set; }
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
