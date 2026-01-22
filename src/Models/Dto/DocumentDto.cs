namespace NexoSferaApi.Models.Dto;

/// <summary>
/// Lightweight document DTO for list views (minimal fields for performance)
/// Matches v1 API list view
/// </summary>
public class DocumentListItemDto
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string? Number { get; set; }
    public string? ExternalNumber { get; set; }
    public string? ReferenceNumber { get; set; }
    public DateTime? IssueDate { get; set; }
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public decimal AmountToPay { get; set; }
    public int? StatusId { get; set; }
}

/// <summary>
/// Full document DTO with all available fields (for detail view)
/// Matches v1 API detail view
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

    // Type and status
    public DocumentType Type { get; set; }
    public int? StatusId { get; set; }
    public string? Status { get; set; }
    public string? ConfigurationId { get; set; }

    // Dates
    public DateTime? EntryDate { get; set; }
    public DateTime? IssueDate { get; set; }
    public DateTime? SaleDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? Deadline { get; set; }

    // Customer/Supplier
    public int? CustomerId { get; set; }
    public int? SelectedCustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerNIP { get; set; }
    public CustomerDto? Customer { get; set; }

    // Warehouse
    public int? WarehouseId { get; set; }
    public string? WarehouseSymbol { get; set; }

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

    // Payment
    public string? PaymentMethod { get; set; }
    public DateTime? PaymentDate { get; set; }
    public decimal? PaidAmount { get; set; }
    public decimal? RemainingAmount { get; set; }

    // Personnel
    public string? IssuedBy { get; set; }
    public string? ReceivedBy { get; set; }

    // Notes
    public string? Notes { get; set; }

    // Items
    public List<DocumentItemDto> Items { get; set; } = new();

    // Timestamps
    public DateTime? CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
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
    public string Unit { get; set; } = "szt.";
    public string? UnitSymbol { get; set; }
    public string? UnitName { get; set; }
    public int? UnitId { get; set; }

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
