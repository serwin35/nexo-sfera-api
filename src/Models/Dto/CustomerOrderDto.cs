namespace NexoSferaApi.Models.Dto;

/// <summary>
/// Customer order (ZK - Zamówienie od Klienta) list item DTO
/// </summary>
public class CustomerOrderListItemDto
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string? Number { get; set; }
    public string? ExternalNumber { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Title { get; set; }
    public DateTime? IssueDate { get; set; }
    public DateTime? DeadlineDate { get; set; }
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerNIP { get; set; }
    public string? WarehouseSymbol { get; set; }
    public decimal TotalNet { get; set; }
    public decimal TotalGross { get; set; }
    public decimal AmountToPay { get; set; }
    public string? Currency { get; set; }
    public int? StatusId { get; set; }
    public string? StatusSymbol { get; set; }
    public bool IsClosed { get; set; }
    public bool IsRealized { get; set; }
    public bool IsOverdue { get; set; }
}

/// <summary>
/// Full customer order DTO (ZK - Zamówienie od Klienta)
/// Based on InsERT.Moria.ModelDanych.DokumentZK
/// </summary>
public class CustomerOrderDto
{
    // Identity
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string? Number { get; set; }
    public string? ExternalNumber { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Title { get; set; }
    public string? Subtitle { get; set; }

    // Status
    public int StatusId { get; set; }
    public string? Status { get; set; }
    public string? StatusSymbol { get; set; }
    public bool IsClosed { get; set; }
    public bool IsRealizedWithoutDocument { get; set; }
    public bool ReceivedFromCustomer { get; set; }

    // Reservation
    public byte? ReservationType { get; set; }
    public string? ReservationTypeDescription { get; set; }

    // Dates
    public DateTime? IssueDate { get; set; }
    public DateTime EntryDate { get; set; }
    public DateTime? SaleDate { get; set; }
    public DateTime? DeadlineDate { get; set; }

    // Customer (Podmiot)
    public int? CustomerId { get; set; }
    public int? SelectedCustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerNIP { get; set; }

    // Recipient (Odbiorca)
    public int? RecipientId { get; set; }
    public int? SelectedRecipientId { get; set; }
    public string? RecipientName { get; set; }

    // Buyer/Seller (Nabywca/Sprzedawca)
    public int? BuyerId { get; set; }
    public int? SelectedBuyerId { get; set; }

    // Supplier (Dostawca)
    public int? SupplierId { get; set; }
    public int? SelectedSupplierId { get; set; }

    // Warehouse
    public int? WarehouseId { get; set; }
    public string? WarehouseSymbol { get; set; }
    public string? WarehouseName { get; set; }

    // Sales point
    public int? SalesPointId { get; set; }
    public string? SalesPointName { get; set; }

    // Price level
    public int? PriceLevelId { get; set; }
    public string? PriceLevelName { get; set; }

    // Amounts
    public decimal TotalNet { get; set; }
    public decimal TotalGross { get; set; }
    public decimal AmountToPay { get; set; }
    public decimal DiscountNet { get; set; }
    public decimal DiscountGross { get; set; }
    public decimal DefaultDiscountPercent { get; set; }

    // Deposits/Returns value
    public decimal DepositValueAdded { get; set; }
    public decimal DepositValueReturned { get; set; }
    public decimal PackagingValueReceived { get; set; }
    public decimal PackagingValueIssued { get; set; }

    // Currency
    public string Currency { get; set; } = "PLN";

    // Payment
    public bool SplitPayment { get; set; }

    // VAT calculation method
    public int VatCalculationMethod { get; set; }
    public int DocumentSummaryMethod { get; set; }
    public bool? VatDeductible { get; set; }

    // Warehouse effect
    public byte? WarehouseEffect { get; set; }

    // Personnel
    public string? IssuedBy { get; set; }
    public string? ReceivedBy { get; set; }
    public int? IssuedByPersonId { get; set; }

    // Place
    public string? IssuePlaceCity { get; set; }
    public int? EntryPlaceId { get; set; }

    // Addresses
    public int? CustomerAddressId { get; set; }
    public int? RecipientAddressId { get; set; }
    public int? BuyerAddressId { get; set; }
    public int? SupplierAddressId { get; set; }
    public int? DeliveryPlaceId { get; set; }
    public int? ExternalDeliveryPlaceId { get; set; }
    public int? CompanyAddressId { get; set; }

    // Configuration
    public Guid ConfigurationId { get; set; }
    public string? ConfigurationSymbol { get; set; }
    public Guid? RealizingConfigurationId { get; set; }

    // Related documents
    public int? SourceDocumentId { get; set; }
    public int? DifferenceDocumentId { get; set; }

    // CRM relations
    public int? CustomerAgreementId { get; set; }
    public int? LinkedCustomerAgreementId { get; set; }
    public int? CustomerRequestId { get; set; }
    public int? DocumentCategoryId { get; set; }

    // Notes
    public string? Notes { get; set; }

    // Items
    public List<CustomerOrderItemDto> Items { get; set; } = new();

    // Timestamps
    public DateTime? CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
}

/// <summary>
/// Customer order line item DTO
/// </summary>
public class CustomerOrderItemDto
{
    public int Id { get; set; }
    public int LineNumber { get; set; }

    // Product
    public int? ProductId { get; set; }
    public string? ProductSymbol { get; set; }
    public string? ProductName { get; set; }

    // Description
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Quantity
    public decimal Quantity { get; set; }
    public decimal? QuantityRealized { get; set; }
    public decimal? QuantityRemaining { get; set; }
    public string Unit { get; set; } = "szt.";
    public int? UnitId { get; set; }

    // Prices
    public decimal PriceNet { get; set; }
    public decimal PriceGross { get; set; }

    // Discount
    public decimal? DiscountPercent { get; set; }
    public decimal? DiscountValue { get; set; }

    // VAT
    public string? VatRate { get; set; }
    public int? VatRateId { get; set; }
    public decimal? VatPercent { get; set; }

    // Values
    public decimal ValueNet { get; set; }
    public decimal ValueVat { get; set; }
    public decimal ValueGross { get; set; }

    // Reservation
    public bool IsReserved { get; set; }
    public decimal? ReservedQuantity { get; set; }

    /// <summary>
    /// Deposit (kaucja) value (SDK 60.0.0: PozycjaZamowieniaWysylkowego.WartoscKaucji)
    /// </summary>
    public decimal? DepositValue { get; set; }

    /// <summary>
    /// Deposit currency symbol (SDK 60.0.0: PozycjaZamowieniaWysylkowego.WalutaKaucji)
    /// </summary>
    public string? DepositCurrency { get; set; }
}

/// <summary>
/// Customer order realization summary
/// </summary>
public class CustomerOrderRealizationDto
{
    public int OrderId { get; set; }
    public string? OrderNumber { get; set; }
    public decimal TotalQuantity { get; set; }
    public decimal RealizedQuantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    public decimal RealizationPercent { get; set; }
    public bool IsFullyRealized { get; set; }
    public List<RelatedDocumentDto>? RealizingDocuments { get; set; }
}
