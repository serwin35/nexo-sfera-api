using NexoSferaApi.Models.Requests;

namespace NexoSferaApi.Models.Dto;

/// <summary>
/// Payment document DTO (KP, KW, BP, BW)
/// </summary>
public class PaymentDto
{
    public int Id { get; set; }
    public string? Number { get; set; }
    public string? FullNumber { get; set; }
    public PaymentType Type { get; set; }
    public DateTime? PaymentDate { get; set; }

    public int? ContractorId { get; set; }
    public string? ContractorName { get; set; }
    public string? ContractorNIP { get; set; }

    public decimal Amount { get; set; }
    public string? Currency { get; set; }

    /// <summary>
    /// Cash register info (for KP, KW)
    /// </summary>
    public string? CashRegisterSymbol { get; set; }
    public string? CashRegisterName { get; set; }

    /// <summary>
    /// Bank account info (for BP, BW)
    /// </summary>
    public string? BankAccountSymbol { get; set; }
    public string? BankAccountName { get; set; }
    public string? BankAccountNumber { get; set; }

    public string? Title { get; set; }
    public string? Notes { get; set; }
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// Settled documents
    /// </summary>
    public List<SettledDocumentDto> SettledDocuments { get; set; } = new();
}

/// <summary>
/// Settled document reference
/// </summary>
public class SettledDocumentDto
{
    public int DocumentId { get; set; }
    public string? DocumentNumber { get; set; }
    public decimal SettledAmount { get; set; }
}

/// <summary>
/// Receivable/Payable (rozrachunek) DTO
/// </summary>
public class ReceivableDto
{
    public int Id { get; set; }
    public ReceivableType Type { get; set; }
    public ReceivableStatus Status { get; set; }

    /// <summary>
    /// Source document
    /// </summary>
    public int? DocumentId { get; set; }
    public string? DocumentNumber { get; set; }
    public string? DocumentType { get; set; }

    public int? ContractorId { get; set; }
    public string? ContractorName { get; set; }
    public string? ContractorNIP { get; set; }

    /// <summary>
    /// Original amount
    /// </summary>
    public decimal OriginalAmount { get; set; }

    /// <summary>
    /// Already settled
    /// </summary>
    public decimal SettledAmount { get; set; }

    /// <summary>
    /// Remaining to settle
    /// </summary>
    public decimal RemainingAmount { get; set; }

    public string? Currency { get; set; }

    public DateTime? IssueDate { get; set; }
    public DateTime? DueDate { get; set; }

    /// <summary>
    /// Days overdue (0 if not overdue)
    /// </summary>
    public int DaysOverdue { get; set; }
    public bool IsOverdue { get; set; }
    public bool IsSettled { get; set; }

    /// <summary>Amount not yet settled at all (SDK: Rozrachunek.KwotaNierozliczona) - differs from RemainingAmount when a settlement is only preliminary.</summary>
    public decimal UnsettledAmount { get; set; }
    public decimal? VatAmount { get; set; }

    /// <summary>Date of the source document (SDK: DataDokumentuZrodlowego).</summary>
    public DateTime? DocumentDate { get; set; }
    /// <summary>Date of the most recent settlement operation (SDK: DataOstatniegoRozliczenia).</summary>
    public DateTime? LastSettlementDate { get; set; }

    /// <summary>Settlement title (SDK: Tytul) and subtype name (SDK: Podtyp.Nazwa).</summary>
    public string? Title { get; set; }
    public string? Subtype { get; set; }
    public bool? IsCollectible { get; set; }
    public bool? SplitPayment { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// Contractor balance summary
/// </summary>
public class ContractorBalanceDto
{
    public int ContractorId { get; set; }
    public string? ContractorName { get; set; }
    public string? ContractorNIP { get; set; }

    /// <summary>
    /// Total receivables (customer owes us)
    /// </summary>
    public decimal TotalReceivables { get; set; }

    /// <summary>
    /// Total payables (we owe supplier)
    /// </summary>
    public decimal TotalPayables { get; set; }

    /// <summary>
    /// Net balance (receivables - payables)
    /// </summary>
    public decimal Balance { get; set; }

    /// <summary>
    /// Overdue receivables
    /// </summary>
    public decimal OverdueReceivables { get; set; }

    /// <summary>
    /// Overdue payables
    /// </summary>
    public decimal OverduePayables { get; set; }

    public int OpenReceivablesCount { get; set; }
    public int OpenPayablesCount { get; set; }
}

/// <summary>
/// Inventory/stock item DTO
/// </summary>
public class InventoryItemDto
{
    public int ProductId { get; set; }
    public string? ProductSymbol { get; set; }
    public string? ProductName { get; set; }
    public string? ProductEan { get; set; }

    public string? WarehouseSymbol { get; set; }
    public string? WarehouseName { get; set; }

    /// <summary>
    /// Current stock quantity
    /// </summary>
    public decimal StockQuantity { get; set; }

    /// <summary>
    /// Reserved quantity
    /// </summary>
    public decimal ReservedQuantity { get; set; }

    /// <summary>
    /// Available quantity (stock - reserved)
    /// </summary>
    public decimal AvailableQuantity { get; set; }

    /// <summary>
    /// On order quantity (incoming)
    /// </summary>
    public decimal OnOrderQuantity { get; set; }

    public string? Unit { get; set; }

    /// <summary>
    /// Stock value
    /// </summary>
    public decimal? StockValue { get; set; }

    /// <summary>
    /// Average purchase price
    /// </summary>
    public decimal? AveragePurchasePrice { get; set; }

    /// <summary>
    /// Last purchase price
    /// </summary>
    public decimal? LastPurchasePrice { get; set; }

    /// <summary>
    /// Minimum stock level
    /// </summary>
    public decimal? MinStockLevel { get; set; }

    /// <summary>
    /// Maximum stock level
    /// </summary>
    public decimal? MaxStockLevel { get; set; }

    /// <summary>
    /// Reorder point
    /// </summary>
    public decimal? ReorderPoint { get; set; }
}

/// <summary>
/// Reservation DTO
/// </summary>
public class ReservationDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string? ProductSymbol { get; set; }
    public string? ProductName { get; set; }

    public string? WarehouseSymbol { get; set; }
    public decimal ReservedQuantity { get; set; }
    public string? Unit { get; set; }

    /// <summary>
    /// Source document (usually customer order ZK)
    /// </summary>
    public int? SourceDocumentId { get; set; }
    public string? SourceDocumentNumber { get; set; }

    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }

    public DateTime? ReservationDate { get; set; }
    public DateTime? ExpirationDate { get; set; }

    public string? Status { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Batch/Lot DTO
/// </summary>
public class BatchDto
{
    public int Id { get; set; }
    public string? BatchNumber { get; set; }

    public int ProductId { get; set; }
    public string? ProductSymbol { get; set; }
    public string? ProductName { get; set; }

    public string? WarehouseSymbol { get; set; }

    public decimal Quantity { get; set; }
    public string? Unit { get; set; }

    public DateTime? ProductionDate { get; set; }
    public DateTime? ExpirationDate { get; set; }

    /// <summary>
    /// Days until expiration (negative if expired)
    /// </summary>
    public int? DaysUntilExpiration { get; set; }

    public string? SupplierName { get; set; }
    public string? SupplierBatchNumber { get; set; }

    public string? Notes { get; set; }
}
