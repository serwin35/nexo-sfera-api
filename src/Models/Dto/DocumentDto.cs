namespace NexoSferaApi.Models.Dto;

public class DocumentDto
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public string? FullNumber { get; set; }
    public DocumentType Type { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime? SaleDate { get; set; }
    public DateTime? DueDate { get; set; }
    public CustomerDto? Customer { get; set; }
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerNIP { get; set; }
    public string? WarehouseSymbol { get; set; }
    public decimal TotalNet { get; set; }
    public decimal TotalVat { get; set; }
    public decimal TotalGross { get; set; }
    public string Currency { get; set; } = "PLN";
    public decimal? ExchangeRate { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
    public List<DocumentItemDto> Items { get; set; } = new();
    public string? IssuedBy { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
}

public class DocumentItemDto
{
    public int Id { get; set; }
    public int LineNumber { get; set; }
    public int? ProductId { get; set; }
    public string? ProductSymbol { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "szt.";
    public decimal PriceNet { get; set; }
    public decimal PriceGross { get; set; }
    public decimal? Discount { get; set; }
    public string VatRate { get; set; } = "23%";
    public decimal ValueNet { get; set; }
    public decimal ValueVat { get; set; }
    public decimal ValueGross { get; set; }
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
