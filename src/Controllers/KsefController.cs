using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;
using Microsoft.AspNetCore.Mvc.ModelBinding;

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
    private const int StatusNieokreslony = 11; // SDK 60.0.0: StatusKSeF.Nieokreslony

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
    public async Task<ActionResult<PagedResponse<ElectronicDocumentDto>>> GetElectronicDocuments(
        [FromQuery] string? status,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] string? customerTaxId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var dokumentyManager = _sferaService.GetManager("DokumentyElektroniczne");
                if (dokumentyManager == null)
                {
                    return null;
                }

                int? statusFilter = !string.IsNullOrEmpty(status) ? GetStatusValue(status) : null;

                var allDokumenty = new List<object>();
                foreach (var d in DynamicPropertyHelper.SafeGetAll((object)dokumentyManager))
                {
                    bool include = true;

                    if (statusFilter.HasValue && DynamicPropertyHelper.GetInt(d, "EStatus") != statusFilter.Value)
                        include = false;

                    if (include && dateFrom.HasValue)
                    {
                        var data = DynamicPropertyHelper.GetDateTime(d, "DataUtworzenia");
                        if (!data.HasValue || data.Value < dateFrom.Value)
                            include = false;
                    }

                    if (include && dateTo.HasValue)
                    {
                        var data = DynamicPropertyHelper.GetDateTime(d, "DataUtworzenia");
                        if (!data.HasValue || data.Value > dateTo.Value)
                            include = false;
                    }

                    if (include && !string.IsNullOrEmpty(customerTaxId))
                    {
                        if (DynamicPropertyHelper.GetString(d, "IdentyfikatorPodatkowyKlienta") != customerTaxId)
                            include = false;
                    }

                    if (include)
                        allDokumenty.Add(d);
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

                return new PagedResponse<ElectronicDocumentDto>
                {
                    Data = items,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };
            });

            if (result == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get DokumentyElektroniczne manager"));
            }

            return Ok(result);
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
    public async Task<ActionResult<ApiResponse<ElectronicDocumentDto>>> GetElectronicDocument(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var dokumentyManager = _sferaService.GetManager("DokumentyElektroniczne");
                if (dokumentyManager == null)
                {
                    return (found: false, managerMissing: true, dto: (ElectronicDocumentDto?)null);
                }

                dynamic? dokument = null;
                foreach (var d in DynamicPropertyHelper.SafeGetAll((object)dokumentyManager))
                {
                    if (DynamicPropertyHelper.GetId(d) == id)
                    {
                        dokument = d;
                        break;
                    }
                }

                if (dokument == null)
                {
                    return (found: false, managerMissing: false, dto: (ElectronicDocumentDto?)null);
                }

                return (found: true, managerMissing: false, dto: (ElectronicDocumentDto?)MapElectronicDocument(dokument));
            });

            if (result.managerMissing)
            {
                return StatusCode(500, ApiResponse<ElectronicDocumentDto>.Error("Failed to get DokumentyElektroniczne manager"));
            }

            if (!result.found)
            {
                return NotFound(ApiResponse<ElectronicDocumentDto>.Error($"Electronic document with ID {id} not found"));
            }

            return Ok(ApiResponse<ElectronicDocumentDto>.Ok(result.dto));
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
    public async Task<ActionResult<ApiResponse<ElectronicDocumentDto>>> GetElectronicDocumentByKsefNumber(string ksefNumber)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var dokumentyManager = _sferaService.GetManager("DokumentyElektroniczne");
                if (dokumentyManager == null)
                {
                    return (found: false, managerMissing: true, dto: (ElectronicDocumentDto?)null);
                }

                dynamic? dokument = null;
                foreach (var d in DynamicPropertyHelper.SafeGetAll((object)dokumentyManager))
                {
                    if (DynamicPropertyHelper.GetString(d, "NumerKSeF") == ksefNumber)
                    {
                        dokument = d;
                        break;
                    }
                }

                if (dokument == null)
                {
                    return (found: false, managerMissing: false, dto: (ElectronicDocumentDto?)null);
                }

                return (found: true, managerMissing: false, dto: (ElectronicDocumentDto?)MapElectronicDocument(dokument));
            });

            if (result.managerMissing)
            {
                return StatusCode(500, ApiResponse<ElectronicDocumentDto>.Error("Failed to get DokumentyElektroniczne manager"));
            }

            if (!result.found)
            {
                return NotFound(ApiResponse<ElectronicDocumentDto>.Error($"Electronic document with KSeF number {ksefNumber} not found"));
            }

            return Ok(ApiResponse<ElectronicDocumentDto>.Ok(result.dto));
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
    public async Task<ActionResult<ApiResponse<EInvoiceGenerationResultDto>>> GenerateEInvoice(int documentId)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                // Find the document
                var dokumentyManager = _sferaService.GetManager("DokumentySprzedazy");
                if (dokumentyManager == null)
                {
                    return (status: "managerMissing", managerName: "DokumentySprzedazy", dto: (EInvoiceGenerationResultDto?)null);
                }

                dynamic? dokument = null;
                foreach (var d in DynamicPropertyHelper.SafeGetAll((object)dokumentyManager))
                {
                    if (DynamicPropertyHelper.GetId(d) == documentId)
                    {
                        dokument = d;
                        break;
                    }
                }

                if (dokument == null)
                {
                    return (status: "notFound", managerName: "", dto: (EInvoiceGenerationResultDto?)null);
                }

                // Generate e-invoice (requires factory access)
                var fabrykaGeneratorow = _sferaService.GetManager("FabrykaGeneratorowEFaktury");
                if (fabrykaGeneratorow == null)
                {
                    return (status: "managerMissing", managerName: "FabrykaGeneratorowEFaktury", dto: (EInvoiceGenerationResultDto?)null);
                }
                var generator = fabrykaGeneratorow.PobierzAktualny();

                // Create parameters object dynamically
                var sfera = _sferaService.GetSfera();
                dynamic parametry = Activator.CreateInstance(
                    sfera.GetType().Assembly.GetType("InsERT.Moria.Sfera.ParametryGenerowaniaEFaktury")
                    ?? typeof(object));

                var wyniki = generator.WygenerujEFaktury(new[] { dokument }, parametry);

                dynamic? wynik = null;
                foreach (var w in (IEnumerable<object>)wyniki)
                {
                    wynik = w;
                    break;
                }

                var dto = new EInvoiceGenerationResultDto
                {
                    DocumentId = documentId,
                    DocumentNumber = DynamicPropertyHelper.GetString(dokument, "Numer", "PelnaSygnatura"),
                    Success = wynik != null && DynamicPropertyHelper.GetBool(wynik, "Sukces"),
                    ElectronicDocumentId = wynik != null ? DynamicPropertyHelper.GetNullableInt(wynik, "DokumentElektronicznyId") : null,
                    Errors = GetErrorsFromResult(wynik)
                };

                return (status: "ok", managerName: "", dto: (EInvoiceGenerationResultDto?)dto);
            });

            if (result.status == "managerMissing")
            {
                return StatusCode(500, ApiResponse<EInvoiceGenerationResultDto>.Error($"Failed to get {result.managerName} manager"));
            }

            if (result.status == "notFound")
            {
                return NotFound(ApiResponse<EInvoiceGenerationResultDto>.Error($"Document with ID {documentId} not found"));
            }

            if (!result.dto!.Success)
            {
                return Ok(ApiResponse<EInvoiceGenerationResultDto>.Error("Failed to generate e-invoice", result.dto.Errors));
            }

            return Ok(ApiResponse<EInvoiceGenerationResultDto>.Ok(result.dto));
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
    public async Task<ActionResult<ApiResponse<List<EInvoiceGenerationResultDto>>>> GenerateEInvoicesBatch([FromBody] List<int> documentIds)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                // Find documents
                var dokumentyManager = _sferaService.GetManager("DokumentySprzedazy");
                if (dokumentyManager == null)
                {
                    return (status: "managerMissing", managerName: "DokumentySprzedazy", results: (List<EInvoiceGenerationResultDto>?)null);
                }

                var dokumenty = new List<object>();
                foreach (var d in DynamicPropertyHelper.SafeGetAll((object)dokumentyManager))
                {
                    if (documentIds.Contains(DynamicPropertyHelper.GetId(d)))
                    {
                        dokumenty.Add(d);
                    }
                }

                if (dokumenty.Count == 0)
                {
                    return (status: "notFound", managerName: "", results: (List<EInvoiceGenerationResultDto>?)null);
                }

                // Generate e-invoices (requires factory access)
                var fabrykaGeneratorow = _sferaService.GetManager("FabrykaGeneratorowEFaktury");
                if (fabrykaGeneratorow == null)
                {
                    return (status: "managerMissing", managerName: "FabrykaGeneratorowEFaktury", results: (List<EInvoiceGenerationResultDto>?)null);
                }
                var generator = fabrykaGeneratorow.PobierzAktualny();

                var sfera = _sferaService.GetSfera();
                dynamic parametry = Activator.CreateInstance(
                    sfera.GetType().Assembly.GetType("InsERT.Moria.Sfera.ParametryGenerowaniaEFaktury")
                    ?? typeof(object));

                var wyniki = generator.WygenerujEFaktury(dokumenty.ToArray(), parametry);

                var results = new List<EInvoiceGenerationResultDto>();
                foreach (var w in (IEnumerable<object>)wyniki)
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

                return (status: "ok", managerName: "", results: (List<EInvoiceGenerationResultDto>?)results);
            });

            if (result.status == "managerMissing")
            {
                return StatusCode(500, ApiResponse<List<EInvoiceGenerationResultDto>>.Error($"Failed to get {result.managerName} manager"));
            }

            if (result.status == "notFound")
            {
                return Ok(ApiResponse<List<EInvoiceGenerationResultDto>>.Error("No documents found with the provided IDs"));
            }

            return Ok(ApiResponse<List<EInvoiceGenerationResultDto>>.Ok(result.results));
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
    public async Task<ActionResult<ApiResponse<KsefSendResultDto>>> SendToKsef(int electronicDocumentId)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var dokumentyManager = _sferaService.GetManager("DokumentyElektroniczne");
                if (dokumentyManager == null)
                {
                    return (status: "managerMissing", managerName: "DokumentyElektroniczne", dto: (KsefSendResultDto?)null);
                }

                dynamic? dokument = null;
                foreach (var d in DynamicPropertyHelper.SafeGetAll((object)dokumentyManager))
                {
                    if (DynamicPropertyHelper.GetId(d) == electronicDocumentId)
                    {
                        dokument = d;
                        break;
                    }
                }

                if (dokument == null)
                {
                    return (status: "notFound", managerName: "", dto: (KsefSendResultDto?)null);
                }

                // Get coordinator for KSeF sending
                var koordynator = _sferaService.GetManager("KoordynatorWysylaniaEFaktur");
                if (koordynator == null)
                {
                    return (status: "managerMissing", managerName: "KoordynatorWysylaniaEFaktur", dto: (KsefSendResultDto?)null);
                }
                var wyniki = koordynator.PrzekazDoWysylki(new[] { dokument }, null);

                dynamic? wynik = null;
                foreach (var w in (IEnumerable<object>)wyniki)
                {
                    wynik = w;
                    break;
                }

                var dto = new KsefSendResultDto
                {
                    DocumentNumber = wynik != null ? DynamicPropertyHelper.GetString(wynik, "NumerDokumentu") : null,
                    ElectronicDocumentId = electronicDocumentId,
                    Success = wynik != null && DynamicPropertyHelper.GetBool(wynik, "Sukces"),
                    Errors = GetErrorsFromResult(wynik)
                };

                return (status: "ok", managerName: "", dto: (KsefSendResultDto?)dto);
            });

            if (result.status == "managerMissing")
            {
                return StatusCode(500, ApiResponse<KsefSendResultDto>.Error($"Failed to get {result.managerName} manager"));
            }

            if (result.status == "notFound")
            {
                return NotFound(ApiResponse<KsefSendResultDto>.Error($"Electronic document with ID {electronicDocumentId} not found"));
            }

            return Ok(ApiResponse<KsefSendResultDto>.Ok(result.dto));
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
    public async Task<ActionResult<ApiResponse<List<KsefSendResultDto>>>> SendToKsefBatch([FromBody] List<int> electronicDocumentIds)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var dokumentyManager = _sferaService.GetManager("DokumentyElektroniczne");
                if (dokumentyManager == null)
                {
                    return (status: "managerMissing", managerName: "DokumentyElektroniczne", results: (List<KsefSendResultDto>?)null);
                }

                var dokumenty = new List<object>();
                foreach (var d in DynamicPropertyHelper.SafeGetAll((object)dokumentyManager))
                {
                    if (electronicDocumentIds.Contains(DynamicPropertyHelper.GetId(d)))
                    {
                        dokumenty.Add(d);
                    }
                }

                if (dokumenty.Count == 0)
                {
                    return (status: "notFound", managerName: "", results: (List<KsefSendResultDto>?)null);
                }

                // Get coordinator for KSeF sending
                var koordynator = _sferaService.GetManager("KoordynatorWysylaniaEFaktur");
                if (koordynator == null)
                {
                    return (status: "managerMissing", managerName: "KoordynatorWysylaniaEFaktur", results: (List<KsefSendResultDto>?)null);
                }
                var wyniki = koordynator.PrzekazDoWysylki(dokumenty.ToArray(), null);

                var results = new List<KsefSendResultDto>();
                foreach (var w in (IEnumerable<object>)wyniki)
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

                return (status: "ok", managerName: "", results: (List<KsefSendResultDto>?)results);
            });

            if (result.status == "managerMissing")
            {
                return StatusCode(500, ApiResponse<List<KsefSendResultDto>>.Error($"Failed to get {result.managerName} manager"));
            }

            if (result.status == "notFound")
            {
                return Ok(ApiResponse<List<KsefSendResultDto>>.Error("No electronic documents found with the provided IDs"));
            }

            return Ok(ApiResponse<List<KsefSendResultDto>>.Ok(result.results));
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
    public async Task<ActionResult<ApiResponse<List<KsefStatusResultDto>>>> CheckKsefStatus()
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var koordynator = _sferaService.GetManager("KoordynatorWysylaniaEFaktur");
                if (koordynator == null)
                {
                    return (managerMissing: true, results: (List<KsefStatusResultDto>?)null);
                }

                var statusy = koordynator.SprawdzStatus();

                var results = new List<KsefStatusResultDto>();
                foreach (var s in (IEnumerable<object>)statusy)
                {
                    results.Add(new KsefStatusResultDto
                    {
                        DocumentId = DynamicPropertyHelper.GetNullableInt(s, "DokumentId") ?? 0,
                        DocumentNumber = DynamicPropertyHelper.GetString(s, "NumerDokumentu"),
                        KsefNumber = DynamicPropertyHelper.GetString(s, "NumerKsef"),
                        ProcessingCompleted = DynamicPropertyHelper.GetBool(s, "PrzetwarzanieZakonczone"),
                        Success = DynamicPropertyHelper.GetBool(s, "Sukces"),
                        Status = DynamicPropertyHelper.GetString(s, "OpisStatusu"),
                        Errors = GetErrorsFromResult(s)
                    });
                }

                return (managerMissing: false, results: (List<KsefStatusResultDto>?)results);
            });

            if (result.managerMissing)
            {
                return StatusCode(500, ApiResponse<List<KsefStatusResultDto>>.Error("KoordynatorWysylaniaEFaktur manager not available"));
            }

            return Ok(ApiResponse<List<KsefStatusResultDto>>.Ok(result.results));
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
    public async Task<ActionResult<ApiResponse<List<KsefStatusResultDto>>>> CheckKsefStatusBatch([FromBody] List<int> electronicDocumentIds)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var dokumentyManager = _sferaService.GetManager("DokumentyElektroniczne");
                if (dokumentyManager == null)
                {
                    return (status: "managerMissing", managerName: "DokumentyElektroniczne", results: (List<KsefStatusResultDto>?)null);
                }

                var dokumenty = new List<object>();
                foreach (var d in DynamicPropertyHelper.SafeGetAll((object)dokumentyManager))
                {
                    if (electronicDocumentIds.Contains(DynamicPropertyHelper.GetId(d)))
                    {
                        dokumenty.Add(d);
                    }
                }

                if (dokumenty.Count == 0)
                {
                    return (status: "notFound", managerName: "", results: (List<KsefStatusResultDto>?)null);
                }

                // Get coordinator for KSeF status check
                var koordynator = _sferaService.GetManager("KoordynatorWysylaniaEFaktur");
                if (koordynator == null)
                {
                    return (status: "managerMissing", managerName: "KoordynatorWysylaniaEFaktur", results: (List<KsefStatusResultDto>?)null);
                }
                var statusy = koordynator.SprawdzStatus(dokumenty.ToArray());

                var results = new List<KsefStatusResultDto>();
                foreach (var s in (IEnumerable<object>)statusy)
                {
                    results.Add(new KsefStatusResultDto
                    {
                        DocumentId = DynamicPropertyHelper.GetNullableInt(s, "DokumentId") ?? 0,
                        DocumentNumber = DynamicPropertyHelper.GetString(s, "NumerDokumentu"),
                        KsefNumber = DynamicPropertyHelper.GetString(s, "NumerKsef"),
                        ProcessingCompleted = DynamicPropertyHelper.GetBool(s, "PrzetwarzanieZakonczone"),
                        Success = DynamicPropertyHelper.GetBool(s, "Sukces"),
                        Status = DynamicPropertyHelper.GetString(s, "OpisStatusu"),
                        Errors = GetErrorsFromResult(s)
                    });
                }

                return (status: "ok", managerName: "", results: (List<KsefStatusResultDto>?)results);
            });

            if (result.status == "managerMissing")
            {
                return StatusCode(500, ApiResponse<List<KsefStatusResultDto>>.Error($"Failed to get {result.managerName} manager"));
            }

            if (result.status == "notFound")
            {
                return Ok(ApiResponse<List<KsefStatusResultDto>>.Error("No electronic documents found with the provided IDs"));
            }

            return Ok(ApiResponse<List<KsefStatusResultDto>>.Ok(result.results));
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
    public async Task<ActionResult<ApiResponse<List<KsefUpoResultDto>>>> DownloadUpo([FromBody] List<int> electronicDocumentIds)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var dokumentyManager = _sferaService.GetManager("DokumentyElektroniczne");
                if (dokumentyManager == null)
                {
                    return (status: "managerMissing", managerName: "DokumentyElektroniczne", results: (List<KsefUpoResultDto>?)null);
                }

                var dokumenty = new List<object>();
                foreach (var d in DynamicPropertyHelper.SafeGetAll((object)dokumentyManager))
                {
                    if (electronicDocumentIds.Contains(DynamicPropertyHelper.GetId(d)) &&
                        DynamicPropertyHelper.GetInt(d, "EStatus") == StatusPobranyNumerKsef)
                    {
                        dokumenty.Add(d);
                    }
                }

                if (dokumenty.Count == 0)
                {
                    return (status: "notFound", managerName: "", results: (List<KsefUpoResultDto>?)null);
                }

                // Get coordinator for UPO download
                var koordynator = _sferaService.GetManager("KoordynatorWysylaniaEFaktur");
                if (koordynator == null)
                {
                    return (status: "managerMissing", managerName: "KoordynatorWysylaniaEFaktur", results: (List<KsefUpoResultDto>?)null);
                }
                var wyniki = koordynator.PobierzUpo(dokumenty.ToArray());

                var results = new List<KsefUpoResultDto>();
                foreach (var w in (IEnumerable<object>)wyniki)
                {
                    results.Add(new KsefUpoResultDto
                    {
                        DocumentNumber = DynamicPropertyHelper.GetString(w, "NumerDokumentu"),
                        KsefNumber = DynamicPropertyHelper.GetString(w, "NumerKsef"),
                        Success = DynamicPropertyHelper.GetBool(w, "Sukces"),
                        Errors = GetErrorsFromResult(w)
                    });
                }

                return (status: "ok", managerName: "", results: (List<KsefUpoResultDto>?)results);
            });

            if (result.status == "managerMissing")
            {
                return StatusCode(500, ApiResponse<List<KsefUpoResultDto>>.Error($"Failed to get {result.managerName} manager"));
            }

            if (result.status == "notFound")
            {
                return Ok(ApiResponse<List<KsefUpoResultDto>>.Error("No documents ready for UPO download found"));
            }

            return Ok(ApiResponse<List<KsefUpoResultDto>>.Ok(result.results));
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
    public async Task<ActionResult<ApiResponse<KsefSummaryDto>>> GetKsefSummary()
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var dokumentyManager = _sferaService.GetManager("DokumentyElektroniczne");
                if (dokumentyManager == null)
                {
                    return (managerMissing: true, summary: (KsefSummaryDto?)null);
                }

                var dokumenty = new List<object>();
                foreach (var d in DynamicPropertyHelper.SafeGetAll((object)dokumentyManager))
                {
                    dokumenty.Add(d);
                }

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

                return (managerMissing: false, summary: (KsefSummaryDto?)summary);
            });

            if (result.managerMissing)
            {
                return StatusCode(500, ApiResponse<KsefSummaryDto>.Error("Failed to get DokumentyElektroniczne manager"));
            }

            return Ok(ApiResponse<KsefSummaryDto>.Ok(result.summary));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting KSeF summary");
            return StatusCode(500, ApiResponse<KsefSummaryDto>.Error("Error retrieving KSeF summary", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region KSeF Receive (inbound e-invoices)

    // SDK enum values (InsERT.Moria.Dokumenty.Logistyka), verified against SDK 61.1.0.9431 metadata
    private const int RodzajImportowany = 1;                 // RodzajDokumentuElektronicznego.Importowany
    private const int ProcessingToProcessInSubiekt = 2;      // StatusPrzetworzeniaEFaktury.DoPrzetworzeniaWSubiekcie
    private const int InvoiceKindKor = 1;                    // RodzajEFaktury.KOR
    private const int InvoiceKindKorZal = 5;                 // RodzajEFaktury.KOR_ZAL
    private const int InvoiceKindKorRoz = 6;                 // RodzajEFaktury.KOR_ROZ

    /// <summary>
    /// Pull e-invoices issued to my company from KSeF into the Nexo buffer (Dokumenty elektroniczne / "e-Faktury odebrane").
    /// Without dates the SDK performs an incremental download of everything new since the last pull;
    /// with DateFrom/DateTo it downloads the given issue-date range. Requires an active KSeF session configured in Nexo.
    /// Pulled invoices land in the buffer with processing status "ToProcessInSubiekt" and can then be imported
    /// as purchase documents via POST inbox/{id}/import.
    /// </summary>
    [HttpPost("receive")]
    [ProducesResponseType(typeof(ApiResponse<KsefReceiveResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<KsefReceiveResultDto>>> ReceiveFromKsef([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] KsefReceiveRequest? request)
    {
        try
        {
            DateTime? dateFrom = request?.DateFrom;
            DateTime? dateTo = request?.DateTo;
            if (dateFrom.HasValue && dateTo.HasValue && dateFrom.Value > dateTo.Value)
            {
                return BadRequest(ApiResponse<KsefReceiveResultDto>.Error("DateFrom must not be later than DateTo"));
            }

            var result = await _sferaService.ExecuteWithLockAsync<(bool managerMissing, KsefReceiveResultDto? dto, string? error)>(() =>
            {
                var koordynator = _sferaService.GetManager("KoordynatorOdbioruEFaktur");
                if (koordynator == null)
                {
                    return (true, null, null);
                }

                try
                {
                    dynamic wyniki = (dateFrom.HasValue || dateTo.HasValue)
                        ? koordynator.Pobierz(dateFrom, dateTo)
                        : koordynator.Pobierz();

                    var dto = new KsefReceiveResultDto { DateFrom = dateFrom, DateTo = dateTo };
                    if (wyniki != null)
                    {
                        foreach (dynamic w in wyniki)
                        {
                            KsefReceivedDocumentDto item = MapReceivedDocument(w);
                            dto.Documents.Add(item);
                            if (item.Success) dto.Downloaded++; else dto.Failed++;
                        }
                    }
                    return (false, dto, null);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "KSeF receive failed (from={From}, to={To})", (object?)dateFrom, (object?)dateTo);
                    return (false, null, ex.Message);
                }
            });

            if (result.managerMissing)
            {
                return StatusCode(500, ApiResponse<KsefReceiveResultDto>.Error("Failed to get KoordynatorOdbioruEFaktur manager - KSeF inbound is not available in this SDK/configuration"));
            }
            if (result.dto == null)
            {
                return StatusCode(500, ApiResponse<KsefReceiveResultDto>.Error("KSeF receive failed", new List<string> { result.error ?? "Unknown error" }));
            }

            _logger.LogInformation("KSeF receive finished: {Downloaded} downloaded, {Failed} failed", result.dto.Downloaded, result.dto.Failed);
            return Ok(ApiResponse<KsefReceiveResultDto>.Ok(result.dto,
                $"Pobrano {result.dto.Downloaded} dokumentów z KSeF ({result.dto.Failed} błędnych)"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error receiving e-invoices from KSeF");
            return StatusCode(500, ApiResponse<KsefReceiveResultDto>.Error("Error receiving e-invoices from KSeF", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Pull a single e-invoice from KSeF by its KSeF number into the buffer.
    /// </summary>
    [HttpPost("receive/{ksefNumber}")]
    [ProducesResponseType(typeof(ApiResponse<KsefReceivedDocumentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<KsefReceivedDocumentDto>>> ReceiveSingleFromKsef(string ksefNumber)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ksefNumber))
            {
                return BadRequest(ApiResponse<KsefReceivedDocumentDto>.Error("KSeF number is required"));
            }

            var result = await _sferaService.ExecuteWithLockAsync<(bool managerMissing, KsefReceivedDocumentDto? dto, string? error)>(() =>
            {
                var koordynator = _sferaService.GetManager("KoordynatorOdbioruEFaktur");
                if (koordynator == null)
                {
                    return (true, null, null);
                }

                try
                {
                    dynamic wynik = koordynator.Pobierz(ksefNumber);
                    KsefReceivedDocumentDto dto = MapReceivedDocument(wynik);
                    if (string.IsNullOrEmpty(dto.KsefNumber)) dto.KsefNumber = ksefNumber;
                    return (false, dto, null);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "KSeF single receive failed for {KsefNumber}", ksefNumber);
                    return (false, null, ex.Message);
                }
            });

            if (result.managerMissing)
            {
                return StatusCode(500, ApiResponse<KsefReceivedDocumentDto>.Error("Failed to get KoordynatorOdbioruEFaktur manager"));
            }
            if (result.dto == null)
            {
                return StatusCode(500, ApiResponse<KsefReceivedDocumentDto>.Error("KSeF receive failed", new List<string> { result.error ?? "Unknown error" }));
            }
            if (!result.dto.Success)
            {
                return Ok(ApiResponse<KsefReceivedDocumentDto>.Error($"Failed to receive e-invoice {ksefNumber} from KSeF", result.dto.Errors));
            }
            return Ok(ApiResponse<KsefReceivedDocumentDto>.Ok(result.dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error receiving e-invoice {KsefNumber} from KSeF", ksefNumber);
            return StatusCode(500, ApiResponse<KsefReceivedDocumentDto>.Error("Error receiving e-invoice from KSeF", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// List received (imported from KSeF) e-invoices waiting in the buffer.
    /// processingStatus: 1=ToProcessInAccounting, 2=ToProcessInSubiekt (default when pendingOnly=true), 3=Processed, 4=ProcessedManually, 5=Undefined, 6=Rejected.
    /// </summary>
    [HttpGet("inbox")]
    [ProducesResponseType(typeof(PagedResponse<ElectronicDocumentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ElectronicDocumentDto>>> GetInbox(
        [FromQuery] int? processingStatus,
        [FromQuery] bool pendingOnly = false,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] string? sellerTaxId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 500) pageSize = 50;
            int? statusFilter = processingStatus ?? (pendingOnly ? (int?)ProcessingToProcessInSubiekt : null);

            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var dokumentyManager = _sferaService.GetManager("DokumentyElektroniczne");
                if (dokumentyManager == null)
                {
                    return null;
                }

                var matching = new List<object>();
                foreach (var d in DynamicPropertyHelper.SafeGetAll((object)dokumentyManager))
                {
                    if (DynamicPropertyHelper.GetNullableInt(d, "Rodzaj") != RodzajImportowany) continue;
                    if (statusFilter.HasValue && DynamicPropertyHelper.GetNullableInt(d, "StatusPrzetworzenia") != statusFilter.Value) continue;

                    var issueDate = DynamicPropertyHelper.GetDateTime(d, "DataWystawienia") ?? DynamicPropertyHelper.GetDateTime(d, "DataUtworzenia");
                    if (dateFrom.HasValue && (!issueDate.HasValue || issueDate.Value < dateFrom.Value)) continue;
                    if (dateTo.HasValue && (!issueDate.HasValue || issueDate.Value > dateTo.Value)) continue;
                    if (!string.IsNullOrEmpty(sellerTaxId) && DynamicPropertyHelper.GetString(d, "NIPSprzedawcy") != sellerTaxId) continue;

                    matching.Add(d);
                }

                var totalCount = matching.Count;
                var paged = matching
                    .OrderByDescending(d => DynamicPropertyHelper.GetDateTime(d, "DataUtworzenia") ?? DateTime.MinValue)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var items = new List<ElectronicDocumentDto>();
                foreach (var d in paged)
                {
                    items.Add(MapElectronicDocument(d));
                }

                return new PagedResponse<ElectronicDocumentDto>
                {
                    Data = items,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };
            });

            if (result == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get DokumentyElektroniczne manager"));
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing KSeF inbox");
            return StatusCode(500, ApiResponse<object>.Error("Error listing KSeF inbox", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Import a received e-invoice from the buffer into Subiekt as a purchase invoice (FZ) or,
    /// for correction e-invoices (KOR/KOR_ZAL/KOR_ROZ), as a purchase correction (KFZ).
    /// Uses the SDK e-invoice import (ObslugaImportuEFaktur.WypelnijNaPodstawieDokumentuElektronicznego): the counterparty,
    /// items and VAT table are filled from the e-invoice content the same way the Subiekt UI does it.
    /// </summary>
    [HttpPost("inbox/{id}/import")]
    [ProducesResponseType(typeof(ApiResponse<KsefImportResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<KsefImportResultDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<KsefImportResultDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<KsefImportResultDto>>> ImportFromInbox(int id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] KsefImportRequest? request)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync<(string status, KsefImportResultDto? dto)>(() =>
            {
                var dokumentyManager = _sferaService.GetManager("DokumentyElektroniczne");
                if (dokumentyManager == null)
                {
                    return ("managerMissing", null);
                }

                dynamic? eDokument = null;
                foreach (var d in DynamicPropertyHelper.SafeGetAll((object)dokumentyManager))
                {
                    if (DynamicPropertyHelper.GetId(d) == id)
                    {
                        eDokument = d;
                        break;
                    }
                }
                if (eDokument == null)
                {
                    return ("notFound", null);
                }
                if (DynamicPropertyHelper.GetNullableInt(eDokument, "Rodzaj") != RodzajImportowany)
                {
                    return ("notImported", null);
                }

                return ("ok", ImportElectronicDocument(eDokument, request?.WarehouseSymbol));
            });

            if (result.status == "managerMissing")
            {
                return StatusCode(500, ApiResponse<KsefImportResultDto>.Error("Failed to get DokumentyElektroniczne manager"));
            }
            if (result.status == "notFound")
            {
                return NotFound(ApiResponse<KsefImportResultDto>.Error($"Electronic document with ID {id} not found"));
            }
            if (result.status == "notImported")
            {
                return BadRequest(ApiResponse<KsefImportResultDto>.Error($"Electronic document {id} was created by my company - only e-invoices received from KSeF can be imported"));
            }
            if (result.dto == null || !result.dto.Success)
            {
                return BadRequest(ApiResponse<KsefImportResultDto>.Error("Failed to import e-invoice into Subiekt", result.dto?.Errors));
            }

            return Ok(ApiResponse<KsefImportResultDto>.Ok(result.dto,
                $"Zaimportowano e-Fakturę jako {(result.dto.IsCorrection ? "korektę zakupu" : "fakturę zakupu")} {result.dto.DocumentNumber}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing electronic document {Id}", id);
            return StatusCode(500, ApiResponse<KsefImportResultDto>.Error("Error importing e-invoice", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Import every received e-invoice with processing status "ToProcessInSubiekt" into Subiekt purchase documents.
    /// Intended for scheduled automation together with POST receive. Each e-invoice is processed independently;
    /// failures are reported per item and do not stop the batch. Use maxCount to limit one run.
    /// </summary>
    [HttpPost("inbox/import-pending")]
    [ProducesResponseType(typeof(ApiResponse<List<KsefImportResultDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<KsefImportResultDto>>>> ImportPendingFromInbox(
        [FromQuery] int maxCount = 100,
        [FromQuery] string? warehouseSymbol = null)
    {
        try
        {
            if (maxCount < 1) maxCount = 1;

            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var dokumentyManager = _sferaService.GetManager("DokumentyElektroniczne");
                if (dokumentyManager == null)
                {
                    return null;
                }

                var pending = new List<object>();
                foreach (var d in DynamicPropertyHelper.SafeGetAll((object)dokumentyManager))
                {
                    if (DynamicPropertyHelper.GetNullableInt(d, "Rodzaj") != RodzajImportowany) continue;
                    if (DynamicPropertyHelper.GetNullableInt(d, "StatusPrzetworzenia") != ProcessingToProcessInSubiekt) continue;
                    pending.Add(d);
                }

                var ordered = pending
                    .OrderBy(d => DynamicPropertyHelper.GetDateTime(d, "DataWystawienia") ?? DateTime.MaxValue)
                    .ThenBy(d => DynamicPropertyHelper.GetId(d))
                    .Take(maxCount)
                    .ToList();

                var results = new List<KsefImportResultDto>();
                foreach (var d in ordered)
                {
                    results.Add(ImportElectronicDocument(d, warehouseSymbol));
                }
                return results;
            });

            if (result == null)
            {
                return StatusCode(500, ApiResponse<List<KsefImportResultDto>>.Error("Failed to get DokumentyElektroniczne manager"));
            }

            int ok = result.Count(r => r.Success);
            return Ok(ApiResponse<List<KsefImportResultDto>>.Ok(result, $"Zaimportowano {ok} z {result.Count} oczekujących e-Faktur"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing pending e-invoices");
            return StatusCode(500, ApiResponse<List<KsefImportResultDto>>.Error("Error importing pending e-invoices", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Imports one received e-invoice as FZ (or KFZ for corrections). Must run inside ExecuteWithLockAsync.
    /// Mirrors the SDK sample PrzykladyKsef/Operacje/ImportOdebranejEFaktury.cs.
    /// </summary>
    private KsefImportResultDto ImportElectronicDocument(dynamic eDokument, string? warehouseSymbol)
    {
        var dto = new KsefImportResultDto
        {
            ElectronicDocumentId = DynamicPropertyHelper.GetId(eDokument),
            KsefNumber = DynamicPropertyHelper.GetString(eDokument, "NumerKSeF")
        };

        int invoiceKind = DynamicPropertyHelper.GetInt(eDokument, "RodzajFaktury");
        dto.IsCorrection = invoiceKind == InvoiceKindKor || invoiceKind == InvoiceKindKorZal || invoiceKind == InvoiceKindKorRoz;

        try
        {
            var manager = _sferaService.GetManager(dto.IsCorrection ? "KorektyDokumentowZakupu" : "DokumentyZakupu");
            if (manager == null)
            {
                dto.Errors.Add(dto.IsCorrection ? "Failed to get KorektyDokumentowZakupu manager" : "Failed to get DokumentyZakupu manager");
                return dto;
            }

            using (dynamic dokument = dto.IsCorrection ? manager.UtworzKorekteFakturyZakupu() : manager.UtworzFaktureZakupu())
            {
                dokument.ObslugaImportuEFaktur.WypelnijNaPodstawieDokumentuElektronicznego(eDokument);

                if (!string.IsNullOrEmpty(warehouseSymbol))
                {
                    var magazynyManager = _sferaService.GetManager("Magazyny");
                    if (magazynyManager != null)
                    {
                        foreach (var m in DynamicPropertyHelper.SafeGetAll((object)magazynyManager))
                        {
                            if (string.Equals(DynamicPropertyHelper.GetString(m, "Symbol"), warehouseSymbol, StringComparison.OrdinalIgnoreCase))
                            {
                                dokument.Dane.Magazyn = m;
                                break;
                            }
                        }
                    }
                }

                if ((bool)dokument.Zapisz())
                {
                    dto.Success = true;
                    dto.DocumentId = DynamicPropertyHelper.GetNullableInt(dokument.Dane, "Id");
                    dto.DocumentNumber = DynamicPropertyHelper.GetString(dokument.Dane, "NumerWewnetrzny", "PelnaSygnatura");
                    _logger.LogInformation("Imported e-invoice {KsefNumber} as {Number} (Id={Id})", (object?)dto.KsefNumber, (object?)dto.DocumentNumber, (object?)dto.DocumentId);
                }
                else
                {
                    dto.Errors.AddRange(GetBusinessObjectErrors(dokument));
                    if (dto.Errors.Count == 0) dto.Errors.Add("Zapisz() returned false without error details");
                    _logger.LogWarning("Import of e-invoice {KsefNumber} failed: {Errors}", (object?)dto.KsefNumber, (object)string.Join("; ", dto.Errors));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import of e-invoice {KsefNumber} threw", (object?)dto.KsefNumber);
            dto.Errors.Add(ex.Message);
        }

        return dto;
    }

    private static KsefReceivedDocumentDto MapReceivedDocument(dynamic w)
    {
        var dto = new KsefReceivedDocumentDto
        {
            Success = DynamicPropertyHelper.GetBool(w, "Sukces"),
            KsefNumber = DynamicPropertyHelper.GetString(w, "NumerKSeF"),
            DocumentNumber = DynamicPropertyHelper.GetString(w, "NumerPelny"),
            AdditionalInfo = DynamicPropertyHelper.GetString(w, "DodatkowaInformacja"),
            UnexpectedProblem = DynamicPropertyHelper.GetBool(w, "NieoczekiwanyProblem"),
            Errors = GetErrorsFromResult(w)
        };
        return dto;
    }

    /// <summary>
    /// Collects validation errors of an SDK business object after a failed Zapisz(): InvalidData entries plus PodajBledy() text.
    /// </summary>
    private static List<string> GetBusinessObjectErrors(dynamic obiekt)
    {
        var errors = new List<string>();
        try
        {
            var invalidData = DynamicPropertyHelper.GetProperty(obiekt, "InvalidData");
            if (invalidData != null)
            {
                foreach (var encjaZBledami in invalidData)
                {
                    var entityErrors = DynamicPropertyHelper.GetProperty(encjaZBledami, "Errors");
                    if (entityErrors == null) continue;
                    foreach (var blad in entityErrors)
                    {
                        var text = blad?.ToString();
                        if (!string.IsNullOrWhiteSpace(text) && !errors.Contains(text)) errors.Add(text);
                    }
                }
            }
        }
        catch { /* best effort */ }

        try
        {
            string? bledy = obiekt.PodajBledy()?.ToString();
            if (!string.IsNullOrWhiteSpace(bledy))
            {
                foreach (var line in bledy.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var text = line.Trim();
                    if (text.Length > 0 && !errors.Contains(text)) errors.Add(text);
                }
            }
        }
        catch { /* PodajBledy not available on this object */ }

        return errors;
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
            "nieokreslony" or "undefined" => StatusNieokreslony,
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
        int status = DynamicPropertyHelper.GetInt(d, "EStatus");
        int? rodzaj = DynamicPropertyHelper.GetNullableInt(d, "Rodzaj");
        int? rodzajFaktury = DynamicPropertyHelper.GetNullableInt(d, "RodzajFaktury");
        int? rolaPodmiotu = DynamicPropertyHelper.GetNullableInt(d, "RolaPodmiotu");
        int? statusPrzetworzenia = DynamicPropertyHelper.GetNullableInt(d, "StatusPrzetworzenia");
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
            HasUpo = status == StatusPobraneUpo,
            // SDK 60.0.0 new fields
            SellerNIP = DynamicPropertyHelper.GetString(d, "NIPSprzedawcy"),
            Checksum = ConvertChecksum(DynamicPropertyHelper.GetProperty(d, "SumaKontrolna")),
            // SDK 61.0.0 new fields
            IsCostInvoice = DynamicPropertyHelper.GetBool(d, "Kosztowa"),
            PaymentStatus = DynamicPropertyHelper.GetNullableInt(d, "StanOplacenia"),
            PaymentDueDate = DynamicPropertyHelper.GetDateTime(d, "TerminPlatnosci"),
            IsSynchronized = DynamicPropertyHelper.GetNullableBool(d, "Zsynchronizowany"),
            // Inbound (KSeF receive) fields
            Direction = rodzaj == 0 ? "Created" : rodzaj == 1 ? "Imported" : null,
            ProcessingStatusCode = statusPrzetworzenia,
            ProcessingStatus = GetProcessingStatusDescription(statusPrzetworzenia),
            InvoiceKind = GetInvoiceKindDescription(rodzajFaktury),
            IsCorrection = rodzajFaktury == InvoiceKindKor || rodzajFaktury == InvoiceKindKorZal || rodzajFaktury == InvoiceKindKorRoz,
            MyCompanyRole = rolaPodmiotu == 1 ? "Seller" : rolaPodmiotu == 2 ? "Buyer" : rolaPodmiotu == 3 ? "Other" : rolaPodmiotu == 4 ? "Authorized" : null,
            CustomerId = DynamicPropertyHelper.GetNullableInt(d, "PodmiotId"),
            CustomerMatchStatus = DynamicPropertyHelper.GetNullableInt(d, "StatusDopasowaniaKlienta"),
            WarehouseId = DynamicPropertyHelper.GetNullableInt(d, "MagazynId"),
            WarehouseSymbol = DynamicPropertyHelper.GetString(d, "Magazyn", "Symbol"),
            CurrencySymbol = DynamicPropertyHelper.GetString(d, "Waluta", "Symbol"),
            ManuallyLinkedDocumentNumbers = GetLinkedDocumentNumbers(DynamicPropertyHelper.GetProperty(d, "DokumentyPowiazaneRecznie"))
        };
    }

    private static List<string> GetLinkedDocumentNumbers(dynamic? documents)
    {
        var numbers = new List<string>();
        if (documents == null) return numbers;
        try
        {
            foreach (var doc in documents)
            {
                var number = DynamicPropertyHelper.GetString(doc, "NumerWewnetrzny", "PelnaSygnatura")
                             ?? DynamicPropertyHelper.GetString(doc, "NumerPelny");
                if (!string.IsNullOrEmpty(number)) numbers.Add(number);
            }
        }
        catch { /* collection not loaded */ }
        return numbers;
    }

    private static string? GetProcessingStatusDescription(int? status)
    {
        if (!status.HasValue) return null;
        return status.Value switch
        {
            1 => "ToProcessInAccounting",
            2 => "ToProcessInSubiekt",
            3 => "Processed",
            4 => "ProcessedManually",
            5 => "Undefined",
            6 => "Rejected",
            _ => status.Value.ToString()
        };
    }

    private static string? GetInvoiceKindDescription(int? kind)
    {
        if (!kind.HasValue) return null;
        return kind.Value switch
        {
            0 => "VAT",
            1 => "KOR",
            2 => "ZAL",
            3 => "ROZ",
            4 => "UPR",
            5 => "KOR_ZAL",
            6 => "KOR_ROZ",
            _ => kind.Value.ToString()
        };
    }

    private static string? ConvertChecksum(dynamic? checksumBytes)
    {
        if (checksumBytes == null) return null;
        try
        {
            byte[] bytes = (byte[])checksumBytes;
            return Convert.ToBase64String(bytes);
        }
        catch
        {
            return null;
        }
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
            StatusNieokreslony => "Undefined",
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
