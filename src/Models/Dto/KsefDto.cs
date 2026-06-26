namespace NexoSferaApi.Models.Dto;

/// <summary>
/// Electronic document (e-invoice for KSeF) DTO
/// </summary>
public class ElectronicDocumentDto
{
    public int Id { get; set; }
    public int? LinkedDocumentId { get; set; }
    public string? DocumentNumber { get; set; }
    public string? KsefNumber { get; set; }
    public string? DocumentType { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? IssueDate { get; set; }
    public DateTime? SendDate { get; set; }
    public DateTime? DeliveryToKsefDate { get; set; }
    public DateTime? KsefIdAssignedDate { get; set; }
    public string? Status { get; set; }
    public string? StatusDescription { get; set; }
    public string? ServiceType { get; set; }
    public string? CustomerTaxId { get; set; }
    public string? CustomerName { get; set; }
    public decimal? Value { get; set; }
    public string? CurrencySymbol { get; set; }
    public bool IsSchemaValid { get; set; }
    public string? InvoiceType { get; set; }
    public bool HasUpo { get; set; }

    /// <summary>
    /// Seller NIP (SDK 60.0.0: DokumentElektroniczny.NIPSprzedawcy)
    /// </summary>
    public string? SellerNIP { get; set; }

    /// <summary>
    /// Document checksum (SDK 60.0.0: DokumentElektroniczny.SumaKontrolna)
    /// </summary>
    public string? Checksum { get; set; }

    /// <summary>
    /// Cost invoice flag (SDK 61.0.0: DokumentElektroniczny.Kosztowa).
    /// </summary>
    public bool IsCostInvoice { get; set; }

    /// <summary>
    /// Payment status (SDK 61.0.0: DokumentElektroniczny.StanOplacenia, raw byte value).
    /// </summary>
    public int? PaymentStatus { get; set; }

    /// <summary>
    /// Payment due date (SDK 61.0.0: DokumentElektroniczny.TerminPlatnosci).
    /// </summary>
    public DateTime? PaymentDueDate { get; set; }

    /// <summary>
    /// Synchronization flag (SDK 61.0.0: DokumentElektroniczny.Zsynchronizowany).
    /// </summary>
    public bool? IsSynchronized { get; set; }
}

/// <summary>
/// KSeF send result DTO
/// </summary>
public class KsefSendResultDto
{
    public string? DocumentNumber { get; set; }
    public int? ElectronicDocumentId { get; set; }
    public bool Success { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// KSeF status check result DTO
/// </summary>
public class KsefStatusResultDto
{
    public int DocumentId { get; set; }
    public string? DocumentNumber { get; set; }
    public string? KsefNumber { get; set; }
    public bool ProcessingCompleted { get; set; }
    public bool Success { get; set; }
    public string? Status { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// KSeF UPO (confirmation of receipt) result DTO
/// </summary>
public class KsefUpoResultDto
{
    public string? DocumentNumber { get; set; }
    public string? KsefNumber { get; set; }
    public bool Success { get; set; }
    public string? UpoContent { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// E-invoice generation result DTO
/// </summary>
public class EInvoiceGenerationResultDto
{
    public int? DocumentId { get; set; }
    public int? ElectronicDocumentId { get; set; }
    public string? DocumentNumber { get; set; }
    public bool Success { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// KSeF summary DTO
/// </summary>
public class KsefSummaryDto
{
    public int TotalDocuments { get; set; }
    public int PendingSend { get; set; }
    public int Sent { get; set; }
    public int WithKsefNumber { get; set; }
    public int WithUpo { get; set; }
    public int Errors { get; set; }
}
