using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// KSeF (National e-Invoice System) integration endpoints
/// </summary>
[ApiController]
[Route("api/ksef")]
[Authorize]
[Tags("KSeF")]
public class KsefController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<KsefController> _logger;

    // KSeF status constants
    private const int StatusDoWyslania = 0;
    private const int StatusWyslana = 1;
    private const int StatusPobranyNumerKsef = 2;
    private const int StatusPobraneUpo = 3;
    private const int StatusBlad = 4;

    public KsefController(ISferaService sferaService, ILogger<KsefController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    #region Electronic Documents

    /// <summary>
    /// Get all electronic documents (e-invoices)
    /// </summary>
    [HttpGet("documents")]
    [ProducesResponseType(typeof(PagedResponse<ElectronicDocumentDto>), StatusCodes.Status200OK)]
    public ActionResult<PagedResponse<ElectronicDocumentDto>> GetElectronicDocuments(
        [FromQuery] string? status,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] string? customerTaxId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var dokumentyManager = sfera.DokumentyElektroniczne();
            var allDokumenty = ((IEnumerable<dynamic>)dokumentyManager.Dane.Wszystkie()).ToList();

            if (!string.IsNullOrEmpty(status))
            {
                var statusValue = GetStatusValue(status);
                if (statusValue.HasValue)
                {
                    allDokumenty = allDokumenty.Where(d =>
                        DynamicPropertyHelper.GetInt(d, "EStatus") == statusValue.Value).ToList();
                }
            }

            if (dateFrom.HasValue)
            {
                allDokumenty = allDokumenty.Where(d =>
                {
                    var data = DynamicPropertyHelper.GetDateTime(d, "DataUtworzenia");
                    return data.HasValue && data.Value >= dateFrom.Value;
                }).ToList();
            }

            if (dateTo.HasValue)
            {
                allDokumenty = allDokumenty.Where(d =>
                {
                    var data = DynamicPropertyHelper.GetDateTime(d, "DataUtworzenia");
                    return data.HasValue && data.Value <= dateTo.Value;
                }).ToList();
            }

            if (!string.IsNullOrEmpty(customerTaxId))
            {
                allDokumenty = allDokumenty.Where(d =>
                    DynamicPropertyHelper.GetString(d, "IdentyfikatorPodatkowyKlienta") == customerTaxId).ToList();
            }

            var totalCount = allDokumenty.Count;
            var pagedDokumenty = allDokumenty
                .OrderByDescending(d => DynamicPropertyHelper.GetDateTime(d, "DataUtworzenia") ?? DateTime.MinValue)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var items = new List<ElectronicDocumentDto>();
            foreach (var d in pagedDokumenty)
            {
                items.Add(MapElectronicDocument(d));
            }

            return Ok(new PagedResponse<ElectronicDocumentDto>
            {
                Data = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting electronic documents");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving electronic documents", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get electronic document by ID
    /// </summary>
    [HttpGet("documents/{id}")]
    [ProducesResponseType(typeof(ApiResponse<ElectronicDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ElectronicDocumentDto>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<ElectronicDocumentDto>> GetElectronicDocument(int id)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var dokumentyManager = sfera.DokumentyElektroniczne();
            var allDokumenty = ((IEnumerable<dynamic>)dokumentyManager.Dane.Wszystkie()).ToList();
            var dokument = allDokumenty.FirstOrDefault(d => DynamicPropertyHelper.GetId(d) == id);

            if (dokument == null)
            {
                return NotFound(ApiResponse<ElectronicDocumentDto>.Error($"Electronic document with ID {id} not found"));
            }

            var dto = MapElectronicDocument(dokument);
            return Ok(ApiResponse<ElectronicDocumentDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting electronic document {Id}", id);
            return StatusCode(500, ApiResponse<ElectronicDocumentDto>.Error("Error retrieving electronic document", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get electronic document by KSeF number
    /// </summary>
    [HttpGet("documents/by-ksef-number/{ksefNumber}")]
    [ProducesResponseType(typeof(ApiResponse<ElectronicDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ElectronicDocumentDto>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<ElectronicDocumentDto>> GetElectronicDocumentByKsefNumber(string ksefNumber)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var dokumentyManager = sfera.DokumentyElektroniczne();
            var allDokumenty = ((IEnumerable<dynamic>)dokumentyManager.Dane.Wszystkie()).ToList();
            var dokument = allDokumenty.FirstOrDefault(d =>
                DynamicPropertyHelper.GetString(d, "NumerKSeF") == ksefNumber);

            if (dokument == null)
            {
                return NotFound(ApiResponse<ElectronicDocumentDto>.Error($"Electronic document with KSeF number {ksefNumber} not found"));
            }

            var dto = MapElectronicDocument(dokument);
            return Ok(ApiResponse<ElectronicDocumentDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting electronic document by KSeF number {KsefNumber}", ksefNumber);
            return StatusCode(500, ApiResponse<ElectronicDocumentDto>.Error("Error retrieving electronic document", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region E-Invoice Generation

    /// <summary>
    /// Generate e-invoice for a document
    /// </summary>
    [HttpPost("generate/{documentId}")]
    [ProducesResponseType(typeof(ApiResponse<EInvoiceGenerationResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EInvoiceGenerationResultDto>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<EInvoiceGenerationResultDto>> GenerateEInvoice(int documentId)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();

            // Find the document
            var dokumentyManager = sfera.DokumentyHandlowe();
            var allDokumenty = ((IEnumerable<dynamic>)dokumentyManager.Dane.Wszystkie()).ToList();
            var dokument = allDokumenty.FirstOrDefault(d => DynamicPropertyHelper.GetId(d) == documentId);

            if (dokument == null)
            {
                return NotFound(ApiResponse<EInvoiceGenerationResultDto>.Error($"Document with ID {documentId} not found"));
            }

            // Generate e-invoice
            var fabrykaGeneratorow = sfera.FabrykaGeneratorowEFaktury();
            var generator = fabrykaGeneratorow.PobierzAktualny();

            // Create parameters object dynamically
            dynamic parametry = Activator.CreateInstance(
                sfera.GetType().Assembly.GetType("InsERT.Moria.Sfera.ParametryGenerowaniaEFaktury")
                ?? typeof(object));

            var wyniki = generator.WygenerujEFaktury(new[] { dokument }, parametry);
            var wynik = ((IEnumerable<dynamic>)wyniki).FirstOrDefault();

            var result = new EInvoiceGenerationResultDto
            {
                DocumentId = documentId,
                DocumentNumber = DynamicPropertyHelper.GetString(dokument, "Numer", "PelnaSygnatura"),
                Success = wynik != null && DynamicPropertyHelper.GetBool(wynik, "Sukces"),
                ElectronicDocumentId = wynik != null ? DynamicPropertyHelper.GetNullableInt(wynik, "DokumentElektronicznyId") : null,
                Errors = GetErrorsFromResult(wynik)
            };

            if (!result.Success)
            {
                return Ok(ApiResponse<EInvoiceGenerationResultDto>.Error("Failed to generate e-invoice", result.Errors));
            }

            return Ok(ApiResponse<EInvoiceGenerationResultDto>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating e-invoice for document {DocumentId}", documentId);
            return StatusCode(500, ApiResponse<EInvoiceGenerationResultDto>.Error("Error generating e-invoice", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Generate e-invoices for multiple documents
    /// </summary>
    [HttpPost("generate/batch")]
    [ProducesResponseType(typeof(ApiResponse<List<EInvoiceGenerationResultDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<EInvoiceGenerationResultDto>>> GenerateEInvoicesBatch([FromBody] List<int> documentIds)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();

            // Find documents
            var dokumentyManager = sfera.DokumentyHandlowe();
            var allDokumenty = ((IEnumerable<dynamic>)dokumentyManager.Dane.Wszystkie()).ToList();
            var dokumenty = allDokumenty.Where(d => documentIds.Contains(DynamicPropertyHelper.GetId(d))).ToList();

            if (!dokumenty.Any())
            {
                return Ok(ApiResponse<List<EInvoiceGenerationResultDto>>.Error("No documents found with the provided IDs"));
            }

            // Generate e-invoices
            var fabrykaGeneratorow = sfera.FabrykaGeneratorowEFaktury();
            var generator = fabrykaGeneratorow.PobierzAktualny();

            dynamic parametry = Activator.CreateInstance(
                sfera.GetType().Assembly.GetType("InsERT.Moria.Sfera.ParametryGenerowaniaEFaktury")
                ?? typeof(object));

            var wyniki = generator.WygenerujEFaktury(dokumenty.ToArray(), parametry);

            var results = new List<EInvoiceGenerationResultDto>();
            foreach (var w in (IEnumerable<dynamic>)wyniki)
            {
                var docId = DynamicPropertyHelper.GetInt(w, "DokumentId");
                string? docNumber = null;
                foreach (var d in dokumenty)
                {
                    if (DynamicPropertyHelper.GetId(d) == docId)
                    {
                        docNumber = DynamicPropertyHelper.GetString(d, "Numer", "PelnaSygnatura");
                        break;
                    }
                }
                results.Add(new EInvoiceGenerationResultDto
                {
                    DocumentId = DynamicPropertyHelper.GetNullableInt(w, "DokumentId"),
                    DocumentNumber = docNumber,
                    Success = DynamicPropertyHelper.GetBool(w, "Sukces"),
                    ElectronicDocumentId = DynamicPropertyHelper.GetNullableInt(w, "DokumentElektronicznyId"),
                    Errors = GetErrorsFromResult(w)
                });
            }

            return Ok(ApiResponse<List<EInvoiceGenerationResultDto>>.Ok(results));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating e-invoices batch");
            return StatusCode(500, ApiResponse<List<EInvoiceGenerationResultDto>>.Error("Error generating e-invoices", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region KSeF Send

    /// <summary>
    /// Send e-invoice to KSeF
    /// </summary>
    [HttpPost("send/{electronicDocumentId}")]
    [ProducesResponseType(typeof(ApiResponse<KsefSendResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<KsefSendResultDto>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<KsefSendResultDto>> SendToKsef(int electronicDocumentId)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();

            var dokumentyManager = sfera.DokumentyElektroniczne();
            var allDokumenty = ((IEnumerable<dynamic>)dokumentyManager.Dane.Wszystkie()).ToList();
            var dokument = allDokumenty.FirstOrDefault(d => DynamicPropertyHelper.GetId(d) == electronicDocumentId);

            if (dokument == null)
            {
                return NotFound(ApiResponse<KsefSendResultDto>.Error($"Electronic document with ID {electronicDocumentId} not found"));
            }

            var koordynator = sfera.KoordynatorWysylaniaEFaktur();
            var wyniki = koordynator.PrzekazDoWysylki(new[] { dokument }, null);
            var wynik = ((IEnumerable<dynamic>)wyniki).FirstOrDefault();

            var result = new KsefSendResultDto
            {
                DocumentNumber = wynik != null ? DynamicPropertyHelper.GetString(wynik, "NumerDokumentu") : null,
                ElectronicDocumentId = electronicDocumentId,
                Success = wynik != null && DynamicPropertyHelper.GetBool(wynik, "Sukces"),
                Errors = GetErrorsFromResult(wynik)
            };

            return Ok(ApiResponse<KsefSendResultDto>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending e-invoice to KSeF {ElectronicDocumentId}", electronicDocumentId);
            return StatusCode(500, ApiResponse<KsefSendResultDto>.Error("Error sending to KSeF", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Send multiple e-invoices to KSeF
    /// </summary>
    [HttpPost("send/batch")]
    [ProducesResponseType(typeof(ApiResponse<List<KsefSendResultDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<KsefSendResultDto>>> SendToKsefBatch([FromBody] List<int> electronicDocumentIds)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();

            var dokumentyManager = sfera.DokumentyElektroniczne();
            var allDokumenty = ((IEnumerable<dynamic>)dokumentyManager.Dane.Wszystkie()).ToList();
            var dokumenty = allDokumenty.Where(d => electronicDocumentIds.Contains(DynamicPropertyHelper.GetId(d))).ToList();

            if (!dokumenty.Any())
            {
                return Ok(ApiResponse<List<KsefSendResultDto>>.Error("No electronic documents found with the provided IDs"));
            }

            var koordynator = sfera.KoordynatorWysylaniaEFaktur();
            var wyniki = koordynator.PrzekazDoWysylki(dokumenty.ToArray(), null);

            var results = new List<KsefSendResultDto>();
            foreach (var w in (IEnumerable<dynamic>)wyniki)
            {
                var docNumber = DynamicPropertyHelper.GetString(w, "NumerDokumentu");
                int? elecDocId = null;
                foreach (var d in dokumenty)
                {
                    if (DynamicPropertyHelper.GetString(d, "NumerDokumentu") == docNumber)
                    {
                        elecDocId = DynamicPropertyHelper.GetId(d);
                        break;
                    }
                }
                results.Add(new KsefSendResultDto
                {
                    DocumentNumber = docNumber,
                    ElectronicDocumentId = elecDocId,
                    Success = DynamicPropertyHelper.GetBool(w, "Sukces"),
                    Errors = GetErrorsFromResult(w)
                });
            }

            return Ok(ApiResponse<List<KsefSendResultDto>>.Ok(results));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending e-invoices batch to KSeF");
            return StatusCode(500, ApiResponse<List<KsefSendResultDto>>.Error("Error sending to KSeF", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region KSeF Status

    /// <summary>
    /// Check KSeF processing status for pending documents
    /// </summary>
    [HttpPost("status/check")]
    [ProducesResponseType(typeof(ApiResponse<List<KsefStatusResultDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<KsefStatusResultDto>>> CheckKsefStatus()
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var koordynator = sfera.KoordynatorWysylaniaEFaktur();

            var statusy = koordynator.SprawdzStatus();

            var results = new List<KsefStatusResultDto>();
            foreach (var s in (IEnumerable<dynamic>)statusy)
            {
                results.Add(new KsefStatusResultDto
                {
                    DocumentId = DynamicPropertyHelper.GetNullableInt(s, "DokumentId"),
                    DocumentNumber = DynamicPropertyHelper.GetString(s, "NumerDokumentu"),
                    KsefNumber = DynamicPropertyHelper.GetString(s, "NumerKsef"),
                    ProcessingCompleted = DynamicPropertyHelper.GetBool(s, "PrzetwarzanieZakonczone"),
                    Success = DynamicPropertyHelper.GetBool(s, "Sukces"),
                    Status = DynamicPropertyHelper.GetString(s, "OpisStatusu"),
                    Errors = GetErrorsFromResult(s)
                });
            }

            return Ok(ApiResponse<List<KsefStatusResultDto>>.Ok(results));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking KSeF status");
            return StatusCode(500, ApiResponse<List<KsefStatusResultDto>>.Error("Error checking KSeF status", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Check KSeF processing status for specific documents
    /// </summary>
    [HttpPost("status/check/batch")]
    [ProducesResponseType(typeof(ApiResponse<List<KsefStatusResultDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<KsefStatusResultDto>>> CheckKsefStatusBatch([FromBody] List<int> electronicDocumentIds)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();

            var dokumentyManager = sfera.DokumentyElektroniczne();
            var allDokumenty = ((IEnumerable<dynamic>)dokumentyManager.Dane.Wszystkie()).ToList();
            var dokumenty = allDokumenty.Where(d => electronicDocumentIds.Contains(DynamicPropertyHelper.GetId(d))).ToList();

            if (!dokumenty.Any())
            {
                return Ok(ApiResponse<List<KsefStatusResultDto>>.Error("No electronic documents found with the provided IDs"));
            }

            var koordynator = sfera.KoordynatorWysylaniaEFaktur();
            var statusy = koordynator.SprawdzStatus(dokumenty.ToArray());

            var results = new List<KsefStatusResultDto>();
            foreach (var s in (IEnumerable<dynamic>)statusy)
            {
                results.Add(new KsefStatusResultDto
                {
                    DocumentId = DynamicPropertyHelper.GetNullableInt(s, "DokumentId"),
                    DocumentNumber = DynamicPropertyHelper.GetString(s, "NumerDokumentu"),
                    KsefNumber = DynamicPropertyHelper.GetString(s, "NumerKsef"),
                    ProcessingCompleted = DynamicPropertyHelper.GetBool(s, "PrzetwarzanieZakonczone"),
                    Success = DynamicPropertyHelper.GetBool(s, "Sukces"),
                    Status = DynamicPropertyHelper.GetString(s, "OpisStatusu"),
                    Errors = GetErrorsFromResult(s)
                });
            }

            return Ok(ApiResponse<List<KsefStatusResultDto>>.Ok(results));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking KSeF status for batch");
            return StatusCode(500, ApiResponse<List<KsefStatusResultDto>>.Error("Error checking KSeF status", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region UPO

    /// <summary>
    /// Download UPO (confirmation of receipt) for documents
    /// </summary>
    [HttpPost("upo/download")]
    [ProducesResponseType(typeof(ApiResponse<List<KsefUpoResultDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<KsefUpoResultDto>>> DownloadUpo([FromBody] List<int> electronicDocumentIds)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();

            var dokumentyManager = sfera.DokumentyElektroniczne();
            var allDokumenty = ((IEnumerable<dynamic>)dokumentyManager.Dane.Wszystkie()).ToList();
            var dokumenty = allDokumenty.Where(d =>
                electronicDocumentIds.Contains(DynamicPropertyHelper.GetId(d)) &&
                DynamicPropertyHelper.GetInt(d, "EStatus") == StatusPobranyNumerKsef).ToList();

            if (!dokumenty.Any())
            {
                return Ok(ApiResponse<List<KsefUpoResultDto>>.Error("No documents ready for UPO download found"));
            }

            var koordynator = sfera.KoordynatorWysylaniaEFaktur();
            var wyniki = koordynator.PobierzUpo(dokumenty.ToArray());

            var results = new List<KsefUpoResultDto>();
            foreach (var w in (IEnumerable<dynamic>)wyniki)
            {
                results.Add(new KsefUpoResultDto
                {
                    DocumentNumber = DynamicPropertyHelper.GetString(w, "NumerDokumentu"),
                    KsefNumber = DynamicPropertyHelper.GetString(w, "NumerKsef"),
                    Success = DynamicPropertyHelper.GetBool(w, "Sukces"),
                    Errors = GetErrorsFromResult(w)
                });
            }

            return Ok(ApiResponse<List<KsefUpoResultDto>>.Ok(results));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading UPO");
            return StatusCode(500, ApiResponse<List<KsefUpoResultDto>>.Error("Error downloading UPO", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Summary

    /// <summary>
    /// Get KSeF summary statistics
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<KsefSummaryDto>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<KsefSummaryDto>> GetKsefSummary()
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var dokumentyManager = sfera.DokumentyElektroniczne();
            var dokumenty = ((IEnumerable<dynamic>)dokumentyManager.Dane.Wszystkie()).ToList();

            int pendingSend = 0, sent = 0, withKsefNumber = 0, withUpo = 0, errors = 0;
            foreach (var d in dokumenty)
            {
                var status = DynamicPropertyHelper.GetInt(d, "EStatus");
                if (status == StatusDoWyslania) pendingSend++;
                else if (status == StatusWyslana) sent++;
                else if (status == StatusPobranyNumerKsef) withKsefNumber++;
                else if (status == StatusPobraneUpo) withUpo++;
                else if (status == StatusBlad) errors++;
            }

            var summary = new KsefSummaryDto
            {
                TotalDocuments = dokumenty.Count,
                PendingSend = pendingSend,
                Sent = sent,
                WithKsefNumber = withKsefNumber,
                WithUpo = withUpo,
                Errors = errors
            };

            return Ok(ApiResponse<KsefSummaryDto>.Ok(summary));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting KSeF summary");
            return StatusCode(500, ApiResponse<KsefSummaryDto>.Error("Error retrieving KSeF summary", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Helpers

    private static int? GetStatusValue(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "dowylania" or "pendingsend" => StatusDoWyslania,
            "wyslana" or "sent" => StatusWyslana,
            "pobranynumerksef" or "ksefnumberreceived" => StatusPobranyNumerKsef,
            "pobraneupo" or "uporeceived" => StatusPobraneUpo,
            "blad" or "error" => StatusBlad,
            _ => null
        };
    }

    private static List<string> GetErrorsFromResult(dynamic? result)
    {
        if (result == null) return new List<string>();

        try
        {
            var bledy = DynamicPropertyHelper.GetProperty(result, "Bledy");
            if (bledy == null) return new List<string>();

            var errors = new List<string>();
            foreach (var b in bledy)
            {
                errors.Add(b?.ToString() ?? "Unknown error");
            }
            return errors;
        }
        catch
        {
            return new List<string>();
        }
    }

    private static ElectronicDocumentDto MapElectronicDocument(dynamic d)
    {
        var status = DynamicPropertyHelper.GetInt(d, "EStatus");
        return new ElectronicDocumentDto
        {
            Id = DynamicPropertyHelper.GetId(d),
            LinkedDocumentId = DynamicPropertyHelper.GetNullableInt(d, "DokumentPowiazanyId"),
            DocumentNumber = DynamicPropertyHelper.GetString(d, "NumerDokumentu"),
            KsefNumber = DynamicPropertyHelper.GetString(d, "NumerKSeF"),
            DocumentType = GetDocumentTypeDescription(DynamicPropertyHelper.GetNullableInt(d, "Rodzaj")),
            CreatedDate = DynamicPropertyHelper.GetDateTime(d, "DataUtworzenia"),
            IssueDate = DynamicPropertyHelper.GetDateTime(d, "DataWystawienia"),
            SendDate = DynamicPropertyHelper.GetDateTime(d, "DataWysylki"),
            DeliveryToKsefDate = DynamicPropertyHelper.GetDateTime(d, "DataDostarczeniaDoKsef"),
            KsefIdAssignedDate = DynamicPropertyHelper.GetDateTime(d, "DataNadaniaKsefId"),
            Status = GetStatusDescription(status),
            ServiceType = GetServiceTypeDescription(DynamicPropertyHelper.GetNullableInt(d, "SerwisWysylki")),
            CustomerTaxId = DynamicPropertyHelper.GetString(d, "IdentyfikatorPodatkowyKlienta"),
            CustomerName = DynamicPropertyHelper.GetString(d, "NazwaKlienta"),
            Value = DynamicPropertyHelper.GetNullableDecimal(d, "Wartosc"),
            IsSchemaValid = DynamicPropertyHelper.GetBool(d, "ZgodnyZeSchematem"),
            HasUpo = status == StatusPobraneUpo
        };
    }

    private static string? GetDocumentTypeDescription(int? rodzaj)
    {
        if (!rodzaj.HasValue) return null;
        return rodzaj.Value switch
        {
            0 => "Sales",
            1 => "Purchase",
            _ => "Other"
        };
    }

    private static string? GetStatusDescription(int status)
    {
        return status switch
        {
            StatusDoWyslania => "PendingSend",
            StatusWyslana => "Sent",
            StatusPobranyNumerKsef => "KsefNumberReceived",
            StatusPobraneUpo => "UpoReceived",
            StatusBlad => "Error",
            _ => status.ToString()
        };
    }

    private static string? GetServiceTypeDescription(int? serwis)
    {
        if (!serwis.HasValue) return null;
        return serwis.Value switch
        {
            0 => "Production",
            1 => "Test",
            _ => "Other"
        };
    }

    #endregion
}
