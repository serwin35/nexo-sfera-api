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

    /// <summary>
    /// Direction of the e-invoice: "Created" (issued by my company) or "Imported" (received from KSeF).
    /// SDK: DokumentElektroniczny.Rodzaj (RodzajDokumentuElektronicznego: Utworzony=0, Importowany=1).
    /// </summary>
    public string? Direction { get; set; }

    /// <summary>
    /// Processing status of a received e-invoice (SDK: StatusPrzetworzenia / StatusPrzetworzeniaEFaktury):
    /// ToProcessInAccounting=1, ToProcessInSubiekt=2, Processed=3, ProcessedManually=4, Undefined=5, Rejected=6.
    /// </summary>
    public string? ProcessingStatus { get; set; }
    public int? ProcessingStatusCode { get; set; }

    /// <summary>
    /// Invoice kind from the e-invoice content (SDK: RodzajFaktury / RodzajEFaktury):
    /// VAT=0, KOR=1, ZAL=2, ROZ=3, UPR=4, KOR_ZAL=5, KOR_ROZ=6.
    /// </summary>
    public string? InvoiceKind { get; set; }
    public bool IsCorrection { get; set; }

    /// <summary>
    /// Role of my company on the e-invoice (SDK: RolaPodmiotu / RolaMojejFirmyDlaEFaktury): Seller=1, Buyer=2, Other=3, Authorized=4.
    /// </summary>
    public string? MyCompanyRole { get; set; }

    /// <summary>Nexo customer matched to the e-invoice (SDK: PodmiotId) and the match status (SDK: StatusDopasowaniaKlienta).</summary>
    public int? CustomerId { get; set; }
    public int? CustomerMatchStatus { get; set; }

    /// <summary>Warehouse assigned to the received e-invoice (SDK: MagazynId / Magazyn.Symbol).</summary>
    public int? WarehouseId { get; set; }
    public string? WarehouseSymbol { get; set; }

    /// <summary>Numbers of Subiekt documents manually linked to the e-invoice (SDK: DokumentyPowiazaneRecznie).</summary>
    public List<string> ManuallyLinkedDocumentNumbers { get; set; } = new();
}

/// <summary>
/// Request for pulling e-invoices from KSeF into the Nexo buffer (DokumentyElektroniczne).
/// Both dates empty = incremental download of everything new since the last pull.
/// </summary>
public class KsefReceiveRequest
{
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
}

/// <summary>
/// Result of one e-invoice pulled from KSeF (SDK: IWynikSynchronizacjiDokumentu).
/// </summary>
public class KsefReceivedDocumentDto
{
    public bool Success { get; set; }
    public string? KsefNumber { get; set; }
    public string? DocumentNumber { get; set; }
    public string? AdditionalInfo { get; set; }
    public bool UnexpectedProblem { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Summary of a KSeF pull operation.
/// </summary>
public class KsefReceiveResultDto
{
    public int Downloaded { get; set; }
    public int Failed { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public List<KsefReceivedDocumentDto> Documents { get; set; } = new();
}

/// <summary>
/// Request for importing a received e-invoice from the buffer into a Subiekt purchase document.
/// </summary>
public class KsefImportRequest
{
    /// <summary>Optional warehouse symbol for the created purchase document (defaults to the configuration default).</summary>
    public string? WarehouseSymbol { get; set; }
}

/// <summary>
/// Result of importing a received e-invoice into a Subiekt purchase invoice (FZ) or purchase correction (KFZ).
/// </summary>
public class KsefImportResultDto
{
    public int ElectronicDocumentId { get; set; }
    public string? KsefNumber { get; set; }
    public bool Success { get; set; }
    public bool IsCorrection { get; set; }
    public int? DocumentId { get; set; }
    public string? DocumentNumber { get; set; }
    public List<string> Errors { get; set; } = new();
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
