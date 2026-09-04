namespace NexoSferaApi.Models.Dto;

/// <summary>
/// Lightweight document DTO for list views (minimal fields for performance)
/// Matches v1 API list view with essential business fields
/// </summary>
public class DocumentListItemDto
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string? Number { get; set; }
    public string? ExternalNumber { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Title { get; set; }
    public DateTime? IssueDate { get; set; }
    public DateTime? SaleDate { get; set; }
    public DateTime? DueDate { get; set; }
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerNIP { get; set; }
    public string? WarehouseSymbol { get; set; }
    public decimal TotalNet { get; set; }
    public decimal TotalGross { get; set; }
    public decimal AmountToPay { get; set; }
    public decimal? PaidAmount { get; set; }
    public string? Currency { get; set; }
    public int? StatusId { get; set; }
    public string? StatusSymbol { get; set; }
    public bool? IsPaid { get; set; }
    public bool? IsOverdue { get; set; }

    /// <summary>
    /// Relation type when returned in associations context (related, realization, correction)
    /// </summary>
    public string? RelationType { get; set; }
}

/// <summary>
/// Full document DTO with all available fields (for detail view)
/// Matches v1 API detail view with extended fields from InsERT SDK
/// </summary>
public class DocumentDto
{
    // Identity
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public string? FullNumber { get; set; }
    public string? ExternalNumber { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Title { get; set; }

    // Type and status
    public DocumentType Type { get; set; }
    public int? StatusId { get; set; }
    public string? Status { get; set; }
    public string? StatusSymbol { get; set; }
    public string? ConfigurationId { get; set; }
    public string? ConfigurationSymbol { get; set; }

    // Dates
    public DateTime? EntryDate { get; set; }
    public DateTime? IssueDate { get; set; }
    public DateTime? SaleDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? Deadline { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public DateTime? ReceiptDate { get; set; }

    // Customer/Supplier
    public int? CustomerId { get; set; }
    public int? SelectedCustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerNIP { get; set; }
    public CustomerDto? Customer { get; set; }

    // Recipient (different from customer - for delivery purposes)
    public int? RecipientId { get; set; }
    public string? RecipientName { get; set; }

    // Warehouse
    public int? WarehouseId { get; set; }
    public string? WarehouseSymbol { get; set; }
    public string? WarehouseName { get; set; }

    // Amounts - Goods
    public decimal GoodsAmountNet { get; set; }
    public decimal GoodsAmountGross { get; set; }

    // Amounts - Services
    public decimal ServicesAmountNet { get; set; }
    public decimal ServicesAmountGross { get; set; }

    // Amounts - Total
    public decimal TotalNet { get; set; }
    public decimal TotalVat { get; set; }
    public decimal TotalGross { get; set; }
    public decimal AmountToPay { get; set; }

    // Costs (for profit/margin calculation)
    public decimal GoodsCost { get; set; }
    public decimal GoodsCostBook { get; set; }
    public decimal GoodsCostWarehouse { get; set; }
    public decimal ServicesCost { get; set; }
    public decimal AdditionalCost { get; set; }

    // Currency
    public string Currency { get; set; } = "PLN";
    public decimal? ExchangeRate { get; set; }
    public DateTime? ExchangeRateDate { get; set; }

    // Payment
    public string? PaymentMethod { get; set; }
    public int? PaymentMethodId { get; set; }
    public DateTime? PaymentDate { get; set; }
    public int? PaymentDays { get; set; }
    public decimal? PaidAmount { get; set; }
    public decimal? RemainingAmount { get; set; }

    // Payment breakdown
    public PaymentBreakdownDto? PaymentBreakdown { get; set; }

    // Split payment (Mechanizm Podzielonej Platnosci)
    public bool? SplitPayment { get; set; }

    // KSeF (Krajowy System e-Faktur) - E-invoicing
    public KsefStatusDto? KsefStatus { get; set; }

    // Personnel
    public string? IssuedBy { get; set; }
    public string? ReceivedBy { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    // Delivery address
    public AddressDto? DeliveryAddress { get; set; }

    // Intrastat (EU trade statistics)
    public bool? SubjectToIntrastat { get; set; }
    public DateTime? IntrastatDate { get; set; }

    // JPK (Jednolity Plik Kontrolny) - Polish tax reporting
    public string? JpkProductGroup { get; set; }
    public string? JpkProcedure { get; set; }

    // Notes
    public string? Notes { get; set; }
    public string? InternalNotes { get; set; }

    // Items
    public List<DocumentItemDto> Items { get; set; } = new();

    // Related documents
    public List<RelatedDocumentDto>? RelatedDocuments { get; set; }

    // Timestamps
    public DateTime? CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }

    // Flags
    public bool? IsPrinted { get; set; }
    public bool? IsSent { get; set; }
    public bool? IsConfirmed { get; set; }
    public bool? IsCanceled { get; set; }

    // Payment state (verified against SDK 61.x: Dokument.PlatnosciDokumentow + Dokument.Rozrachunek)
    /// <summary>True when the linked settlement is fully settled (or, without a settlement, when AmountToPay is 0).</summary>
    public bool? IsPaid { get; set; }
    /// <summary>True when something remains to pay and the due date has passed.</summary>
    public bool? IsOverdue { get; set; }
    /// <summary>Settlement (rozrachunek) created by the document - the authoritative paid/unpaid state. Null when the document creates no settlement.</summary>
    public DocumentSettlementDto? Settlement { get; set; }
    /// <summary>Payments declared on the document (SDK: PlatnosciDokumentow): form, kind, amount, due date.</summary>
    public List<DocumentPaymentDto> Payments { get; set; } = new();
}

/// <summary>
/// Payment breakdown by method
/// </summary>
public class PaymentBreakdownDto
{
    public decimal Cash { get; set; }
    public decimal Card { get; set; }
    public decimal BankTransfer { get; set; }
    public decimal Prepayment { get; set; }
    public decimal QuickPayment { get; set; }
    public decimal OwnVoucher { get; set; }
    public decimal ExternalVoucher { get; set; }
    public decimal Other { get; set; }
}

/// <summary>
/// KSeF (National e-Invoice System) status
/// </summary>
public class KsefStatusDto
{
    public string? Status { get; set; }
    public string? KsefNumber { get; set; }
    public DateTime? SendDate { get; set; }
    public DateTime? AcceptanceDate { get; set; }
    public string? ErrorMessage { get; set; }
    public bool? IsRequired { get; set; }

    /// <summary>
    /// Seller NIP from e-invoice (SDK 60.0.0: DokumentElektroniczny.NIPSprzedawcy)
    /// </summary>
    public string? SellerNIP { get; set; }
}

/// <summary>
/// Related document reference
/// </summary>
public class RelatedDocumentDto
{
    public int Id { get; set; }
    public string? Number { get; set; }
    public string? Symbol { get; set; }
    public string? RelationType { get; set; }
    public DateTime? Date { get; set; }
}

/// <summary>
/// Document line item DTO with full fields
/// </summary>
public class DocumentItemDto
{
    public int Id { get; set; }
    public int LineNumber { get; set; }

    // Product reference
    public int? ProductId { get; set; }
    public string? ProductSymbol { get; set; }
    public string? ProductName { get; set; }

    // Item details
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Quantity and unit
    public decimal Quantity { get; set; }
    /// <summary>Unit the line is expressed in (SDK: PozycjaDokumentu.JednostkaMiaryAs.JednostkaMiary.Symbol).</summary>
    public string Unit { get; set; } = "szt.";
    public string? UnitSymbol { get; set; }
    public string? UnitName { get; set; }
    public int? UnitId { get; set; }
    /// <summary>Quantity converted to the product's base (stock) unit (SDK: PozycjaDokumentu.IloscWJednostceBazowej).</summary>
    public decimal? QuantityInBaseUnit { get; set; }
    /// <summary>Symbol of the product's base (stock) unit.</summary>
    public string? BaseUnit { get; set; }

    // Prices
    public decimal PriceNet { get; set; }
    public decimal PriceGross { get; set; }
    public decimal? OriginalPriceNet { get; set; }

    // Discount
    public decimal? Discount { get; set; }
    public decimal? DiscountPercent { get; set; }
    public decimal? DiscountValue { get; set; }

    // VAT
    public string VatRate { get; set; } = "23%";
    public int? VatRateId { get; set; }
    public decimal? VatPercent { get; set; }

    // Values
    public decimal ValueNet { get; set; }
    public decimal ValueVat { get; set; }
    public decimal ValueGross { get; set; }

    // Cost
    public decimal? Cost { get; set; }
    public decimal? CostValue { get; set; }
    public decimal? Margin { get; set; }
    public decimal? MarginPercent { get; set; }
}

public enum DocumentType
{
    // Dokumenty sprzedazy
    SalesInvoice = 1,           // Faktura sprzedazy
    SalesInvoiceCorrection = 2, // Korekta faktury sprzedazy
    ProformaInvoice = 3,        // Faktura proforma
    Receipt = 4,                // Paragon

    // Dokumenty zakupu
    PurchaseInvoice = 10,           // Faktura zakupu
    PurchaseInvoiceCorrection = 11, // Korekta faktury zakupu

    // Zamowienia
    CustomerOrder = 20,     // Zamowienie od klienta (ZK)
    SupplierOrder = 21,     // Zamowienie do dostawcy (ZD)

    // Dokumenty magazynowe
    WarehouseRelease = 30,  // Wydanie zewnetrzne (WZ)
    WarehouseReceipt = 31,  // Przyjecie zewnetrzne (PZ)
    InternalRelease = 32,   // Rozchod wewnetrzny (RW)
    InternalReceipt = 33,   // Przychod wewnetrzny (PW)
    Transfer = 34           // Przesuniecie miedzymagazynowe (MM)
}

/// <summary>
/// Settlement (rozrachunek) linked to a document. SDK: Dokument.Rozrachunek (InsERT.Moria.ModelDanych.Rozrachunek).
/// </summary>
public class DocumentSettlementDto
{
    public int Id { get; set; }
    /// <summary>"Receivable" (należność) or "Payable" (zobowiązanie). SDK: Rozrachunek.Typ (1/2).</summary>
    public string? Type { get; set; }
    public decimal Amount { get; set; }
    /// <summary>Amount still to be paid (SDK: KwotaPozostala).</summary>
    public decimal RemainingAmount { get; set; }
    /// <summary>Amount with no settlement at all, including preliminary ones (SDK: KwotaNierozliczona).</summary>
    public decimal UnsettledAmount { get; set; }
    public decimal SettledAmount { get; set; }
    public string? Currency { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? LastSettlementDate { get; set; }
    public bool IsSettled { get; set; }
    public bool IsOverdue { get; set; }
    public int DaysOverdue { get; set; }
}

/// <summary>
/// Payment declared on a document. SDK: PlatnoscDokumentu (Dokument.PlatnosciDokumentow).
/// </summary>
public class DocumentPaymentDto
{
    public int Id { get; set; }
    /// <summary>"Prepayment" | "Immediate" | "Deferred". SDK: RodzajPlatnosci (1/2/3).</summary>
    public string? Kind { get; set; }
    public string? PaymentMethod { get; set; }
    public int? PaymentMethodId { get; set; }
    /// <summary>Amount in the document currency (SDK: KwotaDokumentu).</summary>
    public decimal Amount { get; set; }
    /// <summary>Amount in the payment currency (SDK: KwotaPlatnosci).</summary>
    public decimal? AmountInPaymentCurrency { get; set; }
    public decimal? Percent { get; set; }
    /// <summary>Due date (SDK: Termin) and the term in days (SDK: TerminDni).</summary>
    public DateTime? DueDate { get; set; }
    public int? DueDays { get; set; }
    /// <summary>Payment date when different from the document date (SDK: Data).</summary>
    public DateTime? Date { get; set; }
    /// <summary>"Cash" | "Transfer". SDK: RodzajZaplaty (0/1).</summary>
    public string? SettlementKind { get; set; }
}
