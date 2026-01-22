using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using InsERT.Moria.Logistyka;

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
            var sfera = _sferaService.GetSfera();
            var dokumenty = sfera.DokumentyElektroniczne().Dane.Wszystkie();

            if (!string.IsNullOrEmpty(status))
            {
                if (Enum.TryParse<StatusWysylkiElektronicznejEFaktur>(status, true, out var statusEnum))
                {
                    dokumenty = dokumenty.Where(d => d.EStatus == (byte)statusEnum);
                }
            }

            if (dateFrom.HasValue)
            {
                dokumenty = dokumenty.Where(d => d.DataUtworzenia >= dateFrom.Value);
            }

            if (dateTo.HasValue)
            {
                dokumenty = dokumenty.Where(d => d.DataUtworzenia <= dateTo.Value);
            }

            if (!string.IsNullOrEmpty(customerTaxId))
            {
                dokumenty = dokumenty.Where(d => d.IdentyfikatorPodatkowyKlienta == customerTaxId);
            }

            var totalCount = dokumenty.Count();
            var items = dokumenty
                .OrderByDescending(d => d.DataUtworzenia)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList()
                .Select(MapElectronicDocument)
                .ToList();

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
            var sfera = _sferaService.GetSfera();
            var dokument = sfera.DokumentyElektroniczne().Dane.Wszystkie()
                .FirstOrDefault(d => d.Id == id);

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
            var sfera = _sferaService.GetSfera();
            var dokument = sfera.DokumentyElektroniczne().Dane.Wszystkie()
                .FirstOrDefault(d => d.NumerKSeF == ksefNumber);

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
            var sfera = _sferaService.GetSfera();

            // Find the document
            var dokument = sfera.DokumentyHandlowe().Dane.Wszystkie()
                .FirstOrDefault(d => d.Id == documentId);

            if (dokument == null)
            {
                return NotFound(ApiResponse<EInvoiceGenerationResultDto>.Error($"Document with ID {documentId} not found"));
            }

            // Generate e-invoice
            var fabrykaGeneratorow = sfera.FabrykaGeneratorowEFaktury();
            var generator = fabrykaGeneratorow.PobierzAktualny();
            var wyniki = generator.WygenerujEFaktury(new[] { dokument }, new ParametryGenerowaniaEFaktury());
            var wynik = wyniki.FirstOrDefault();

            var result = new EInvoiceGenerationResultDto
            {
                DocumentId = documentId,
                DocumentNumber = dokument.Numer?.PelnaSygnatura,
                Success = wynik?.Sukces ?? false,
                ElectronicDocumentId = wynik?.DokumentElektronicznyId,
                Errors = wynik?.Bledy?.ToList() ?? new List<string>()
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
            var sfera = _sferaService.GetSfera();

            // Find documents
            var dokumenty = sfera.DokumentyHandlowe().Dane.Wszystkie()
                .Where(d => documentIds.Contains(d.Id))
                .ToList();

            if (!dokumenty.Any())
            {
                return Ok(ApiResponse<List<EInvoiceGenerationResultDto>>.Error("No documents found with the provided IDs"));
            }

            // Generate e-invoices
            var fabrykaGeneratorow = sfera.FabrykaGeneratorowEFaktury();
            var generator = fabrykaGeneratorow.PobierzAktualny();
            var wyniki = generator.WygenerujEFaktury(dokumenty, new ParametryGenerowaniaEFaktury());

            var results = wyniki.Select(w => new EInvoiceGenerationResultDto
            {
                DocumentId = dokumenty.FirstOrDefault(d => d.Id == w.DokumentId)?.Id,
                DocumentNumber = dokumenty.FirstOrDefault(d => d.Id == w.DokumentId)?.Numer?.PelnaSygnatura,
                Success = w.Sukces,
                ElectronicDocumentId = w.DokumentElektronicznyId,
                Errors = w.Bledy?.ToList() ?? new List<string>()
            }).ToList();

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
            var sfera = _sferaService.GetSfera();

            var dokument = sfera.DokumentyElektroniczne().Dane.Wszystkie()
                .FirstOrDefault(d => d.Id == electronicDocumentId);

            if (dokument == null)
            {
                return NotFound(ApiResponse<KsefSendResultDto>.Error($"Electronic document with ID {electronicDocumentId} not found"));
            }

            var koordynator = sfera.KoordynatorWysylaniaEFaktur();
            var wyniki = koordynator.PrzekazDoWysylki(new[] { dokument }, null);
            var wynik = wyniki.FirstOrDefault();

            var result = new KsefSendResultDto
            {
                DocumentNumber = wynik?.NumerDokumentu,
                ElectronicDocumentId = electronicDocumentId,
                Success = wynik?.Sukces ?? false,
                Errors = wynik?.Bledy?.ToList() ?? new List<string>()
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
            var sfera = _sferaService.GetSfera();

            var dokumenty = sfera.DokumentyElektroniczne().Dane.Wszystkie()
                .Where(d => electronicDocumentIds.Contains(d.Id))
                .ToList();

            if (!dokumenty.Any())
            {
                return Ok(ApiResponse<List<KsefSendResultDto>>.Error("No electronic documents found with the provided IDs"));
            }

            var koordynator = sfera.KoordynatorWysylaniaEFaktur();
            var wyniki = koordynator.PrzekazDoWysylki(dokumenty, null);

            var results = wyniki.Select(w => new KsefSendResultDto
            {
                DocumentNumber = w.NumerDokumentu,
                ElectronicDocumentId = dokumenty.FirstOrDefault(d => d.NumerDokumentu == w.NumerDokumentu)?.Id,
                Success = w.Sukces,
                Errors = w.Bledy?.ToList() ?? new List<string>()
            }).ToList();

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
            var sfera = _sferaService.GetSfera();
            var koordynator = sfera.KoordynatorWysylaniaEFaktur();

            var statusy = koordynator.SprawdzStatus();

            var results = statusy.Select(s => new KsefStatusResultDto
            {
                DocumentId = s.DokumentId,
                DocumentNumber = s.NumerDokumentu,
                KsefNumber = s.NumerKsef,
                ProcessingCompleted = s.PrzetwarzanieZakonczone,
                Success = s.Sukces,
                Status = s.OpisStatusu,
                Errors = s.Bledy?.ToList() ?? new List<string>()
            }).ToList();

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
            var sfera = _sferaService.GetSfera();

            var dokumenty = sfera.DokumentyElektroniczne().Dane.Wszystkie()
                .Where(d => electronicDocumentIds.Contains(d.Id))
                .ToList();

            if (!dokumenty.Any())
            {
                return Ok(ApiResponse<List<KsefStatusResultDto>>.Error("No electronic documents found with the provided IDs"));
            }

            var koordynator = sfera.KoordynatorWysylaniaEFaktur();
            var statusy = koordynator.SprawdzStatus(dokumenty);

            var results = statusy.Select(s => new KsefStatusResultDto
            {
                DocumentId = s.DokumentId,
                DocumentNumber = s.NumerDokumentu,
                KsefNumber = s.NumerKsef,
                ProcessingCompleted = s.PrzetwarzanieZakonczone,
                Success = s.Sukces,
                Status = s.OpisStatusu,
                Errors = s.Bledy?.ToList() ?? new List<string>()
            }).ToList();

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
            var sfera = _sferaService.GetSfera();

            var dokumenty = sfera.DokumentyElektroniczne().Dane.Wszystkie()
                .Where(d => electronicDocumentIds.Contains(d.Id) &&
                           d.EStatus == (byte)StatusWysylkiElektronicznejEFaktur.PobranyNumerKsef)
                .ToList();

            if (!dokumenty.Any())
            {
                return Ok(ApiResponse<List<KsefUpoResultDto>>.Error("No documents ready for UPO download found"));
            }

            var koordynator = sfera.KoordynatorWysylaniaEFaktur();
            var wyniki = koordynator.PobierzUpo(dokumenty);

            var results = wyniki.Select(w => new KsefUpoResultDto
            {
                DocumentNumber = w.NumerDokumentu,
                KsefNumber = w.NumerKsef,
                Success = w.Sukces,
                Errors = w.Bledy?.ToList() ?? new List<string>()
            }).ToList();

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
            var sfera = _sferaService.GetSfera();
            var dokumenty = sfera.DokumentyElektroniczne().Dane.Wszystkie().ToList();

            var summary = new KsefSummaryDto
            {
                TotalDocuments = dokumenty.Count,
                PendingSend = dokumenty.Count(d => d.EStatus == (byte)StatusWysylkiElektronicznejEFaktur.DoWyslania),
                Sent = dokumenty.Count(d => d.EStatus == (byte)StatusWysylkiElektronicznejEFaktur.Wyslana),
                WithKsefNumber = dokumenty.Count(d => d.EStatus == (byte)StatusWysylkiElektronicznejEFaktur.PobranyNumerKsef),
                WithUpo = dokumenty.Count(d => d.EStatus == (byte)StatusWysylkiElektronicznejEFaktur.PobraneUpo),
                Errors = dokumenty.Count(d => d.EStatus == (byte)StatusWysylkiElektronicznejEFaktur.Blad)
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

    #region Mapping

    private static ElectronicDocumentDto MapElectronicDocument(DokumentElektroniczny d)
    {
        return new ElectronicDocumentDto
        {
            Id = d.Id,
            LinkedDocumentId = d.DokumentPowiazanyId,
            DocumentNumber = d.NumerDokumentu,
            KsefNumber = d.NumerKSeF,
            DocumentType = GetDocumentTypeDescription(d.Rodzaj),
            CreatedDate = d.DataUtworzenia,
            IssueDate = d.DataWystawienia,
            SendDate = d.DataWysylki,
            DeliveryToKsefDate = d.DataDostarczeniaDoKsef,
            KsefIdAssignedDate = d.DataNadaniaKsefId,
            Status = GetStatusDescription((StatusWysylkiElektronicznejEFaktur?)d.EStatus),
            ServiceType = GetServiceTypeDescription(d.SerwisWysylki),
            CustomerTaxId = d.IdentyfikatorPodatkowyKlienta,
            CustomerName = d.NazwaKlienta,
            Value = d.Wartosc,
            IsSchemaValid = d.ZgodnyZeSchematem ?? false,
            HasUpo = d.EStatus == (byte)StatusWysylkiElektronicznejEFaktur.PobraneUpo
        };
    }

    private static string? GetDocumentTypeDescription(byte? rodzaj)
    {
        if (!rodzaj.HasValue) return null;
        return rodzaj.Value switch
        {
            0 => "Sales",
            1 => "Purchase",
            _ => "Other"
        };
    }

    private static string? GetStatusDescription(dynamic? status)
    {
        if (status == null) return null;
        var statusValue = (int)status;
        return statusValue switch
        {
            0 => "PendingSend",      // DoWyslania
            1 => "Sent",             // Wyslana
            2 => "KsefNumberReceived", // PobranyNumerKsef
            3 => "UpoReceived",      // PobraneUpo
            4 => "Error",            // Blad
            _ => status.ToString()
        };
    }

    private static string? GetServiceTypeDescription(byte? serwis)
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
