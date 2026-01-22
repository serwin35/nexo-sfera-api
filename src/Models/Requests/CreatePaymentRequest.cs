using System.ComponentModel.DataAnnotations;

namespace NexoSferaApi.Models.Requests;

/// <summary>
/// Request for payment document creation (KP, KW, BP, BW)
/// </summary>
public class CreatePaymentRequest
{
    /// <summary>
    /// Payment type: KP (cash receipt), KW (cash disbursement),
    /// BP (bank receipt), BW (bank disbursement)
    /// </summary>
    [Required]
    public PaymentType Type { get; set; }

    /// <summary>
    /// Contractor (customer/supplier) ID
    /// </summary>
    public int? ContractorId { get; set; }

    /// <summary>
    /// Contractor NIP (alternative to ContractorId)
    /// </summary>
    [MaxLength(13)]
    public string? ContractorNIP { get; set; }

    /// <summary>
    /// Contractor name (for one-time contractors)
    /// </summary>
    [MaxLength(200)]
    public string? ContractorName { get; set; }

    /// <summary>
    /// Payment amount
    /// </summary>
    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    /// <summary>
    /// Currency
    /// </summary>
    [MaxLength(3)]
    public string Currency { get; set; } = "PLN";

    /// <summary>
    /// Cash register ID (for KP, KW)
    /// </summary>
    public int? CashRegisterId { get; set; }

    /// <summary>
    /// Cash register symbol (alternative to CashRegisterId)
    /// </summary>
    [MaxLength(20)]
    public string? CashRegisterSymbol { get; set; }

    /// <summary>
    /// Bank account ID (for BP, BW)
    /// </summary>
    public int? BankAccountId { get; set; }

    /// <summary>
    /// Bank account symbol (alternative to BankAccountId)
    /// </summary>
    [MaxLength(50)]
    public string? BankAccountSymbol { get; set; }

    /// <summary>
    /// Payment date
    /// </summary>
    public DateTime? PaymentDate { get; set; }

    /// <summary>
    /// Payment title/description
    /// </summary>
    [MaxLength(500)]
    public string? Title { get; set; }

    /// <summary>
    /// Document IDs to settle (rozliczenie)
    /// </summary>
    public List<int>? DocumentIdsToSettle { get; set; }

    /// <summary>
    /// Notes
    /// </summary>
    [MaxLength(2000)]
    public string? Notes { get; set; }
}

/// <summary>
/// Payment document types
/// </summary>
public enum PaymentType
{
    /// <summary>KP - Kasa przyjmie (cash receipt)</summary>
    KP = 0,
    /// <summary>KW - Kasa wyda (cash disbursement)</summary>
    KW = 1,
    /// <summary>BP - Bank przyjmie (bank receipt)</summary>
    BP = 2,
    /// <summary>BW - Bank wyda (bank disbursement)</summary>
    BW = 3
}

/// <summary>
/// Query parameters for payments
/// </summary>
public class PaymentQueryRequest
{
    public PaymentType? Type { get; set; }
    public int? ContractorId { get; set; }
    public string? CashRegisterSymbol { get; set; }
    public string? BankAccountSymbol { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public decimal? AmountMin { get; set; }
    public decimal? AmountMax { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

/// <summary>
/// Request for receivable/payable (rozrachunek) operations
/// </summary>
public class SettlementRequest
{
    /// <summary>
    /// Contractor ID
    /// </summary>
    [Required]
    public int ContractorId { get; set; }

    /// <summary>
    /// Document IDs to settle together (kompensata)
    /// </summary>
    [Required]
    [MinLength(2)]
    public List<int> DocumentIds { get; set; } = new();

    /// <summary>
    /// Settlement date
    /// </summary>
    public DateTime? SettlementDate { get; set; }

    /// <summary>
    /// Notes
    /// </summary>
    [MaxLength(2000)]
    public string? Notes { get; set; }
}

/// <summary>
/// Query parameters for receivables/payables
/// </summary>
public class ReceivableQueryRequest
{
    public int? ContractorId { get; set; }
    public ReceivableType? Type { get; set; }
    public ReceivableStatus? Status { get; set; }
    public DateTime? DueDateFrom { get; set; }
    public DateTime? DueDateTo { get; set; }
    public bool? Overdue { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public enum ReceivableType
{
    /// <summary>Należność - receivable (customer owes us)</summary>
    Receivable = 0,
    /// <summary>Zobowiązanie - payable (we owe supplier)</summary>
    Payable = 1
}

public enum ReceivableStatus
{
    /// <summary>Nierozliczone - unsettled</summary>
    Unsettled = 0,
    /// <summary>Częściowo rozliczone - partially settled</summary>
    PartiallySettled = 1,
    /// <summary>Rozliczone - fully settled</summary>
    Settled = 2
}
