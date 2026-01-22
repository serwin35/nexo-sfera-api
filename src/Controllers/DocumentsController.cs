using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Requests;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Commercial documents (Faktury, Paragony, Korekty) management endpoints
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Tags("Documents")]
public class DocumentsController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(ISferaService sferaService, ILogger<DocumentsController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get documents with filtering
    /// </summary>
    [HttpGet]
    public ActionResult<PagedResponse<DocumentDto>> GetDocuments([FromQuery] DocumentQueryRequest query)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var dokumentyManager = sfera.Dokumenty();

            var allDokumenty = ((IEnumerable<dynamic>)dokumentyManager.Dane.Wszystkie()).ToList();

            // Apply filters
            if (query.DateFrom.HasValue)
            {
                allDokumenty = allDokumenty.Where(d =>
                {
                    var data = DynamicPropertyHelper.GetDateTime(d, "DataWystawienia");
                    return data.HasValue && data.Value >= query.DateFrom.Value;
                }).ToList();
            }

            if (query.DateTo.HasValue)
            {
                allDokumenty = allDokumenty.Where(d =>
                {
                    var data = DynamicPropertyHelper.GetDateTime(d, "DataWystawienia");
                    return data.HasValue && data.Value <= query.DateTo.Value;
                }).ToList();
            }

            if (query.CustomerId.HasValue)
            {
                allDokumenty = allDokumenty.Where(d =>
                {
                    var podmiot = DynamicPropertyHelper.GetProperty(d, "Podmiot");
                    return podmiot != null && DynamicPropertyHelper.GetInt(podmiot, "Id") == query.CustomerId.Value;
                }).ToList();
            }

            var totalCount = allDokumenty.Count;

            // Sort and paginate
            var items = allDokumenty
                .OrderByDescending(d => DynamicPropertyHelper.GetDateTime(d, "DataWystawienia") ?? DateTime.MinValue)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToList();

            var response = new PagedResponse<DocumentDto>
            {
                Data = items.Select(MapDocumentToDto).ToList(),
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = totalCount
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting documents");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving documents", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get document by ID
    /// </summary>
    [HttpGet("{id}")]
    public ActionResult<ApiResponse<DocumentDto>> GetDocument(int id)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var dokumentyManager = sfera.Dokumenty();

            var allDokumenty = ((IEnumerable<dynamic>)dokumentyManager.Dane.Wszystkie()).ToList();
            var dokument = allDokumenty.FirstOrDefault(d => DynamicPropertyHelper.GetId(d) == id);

            if (dokument == null)
            {
                return NotFound(ApiResponse<DocumentDto>.Error($"Document with ID {id} not found"));
            }

            return Ok(ApiResponse<DocumentDto>.Ok(MapDocumentToDto(dokument)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting document {Id}", id);
            return StatusCode(500, ApiResponse<DocumentDto>.Error("Error retrieving document", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get document by number
    /// </summary>
    [HttpGet("by-number/{number}")]
    public ActionResult<ApiResponse<DocumentDto>> GetDocumentByNumber(string number)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var dokumentyManager = sfera.Dokumenty();

            var allDokumenty = ((IEnumerable<dynamic>)dokumentyManager.Dane.Wszystkie()).ToList();
            var dokument = allDokumenty.FirstOrDefault(d =>
            {
                var fullNum = DynamicPropertyHelper.GetString(d, "NumerWewnetrzny", "PelnaSygnatura");
                return fullNum != null && fullNum.Contains(number);
            });

            if (dokument == null)
            {
                return NotFound(ApiResponse<DocumentDto>.Error($"Document with number {number} not found"));
            }

            return Ok(ApiResponse<DocumentDto>.Ok(MapDocumentToDto(dokument)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting document by number {Number}", number);
            return StatusCode(500, ApiResponse<DocumentDto>.Error("Error retrieving document", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create a new sales invoice (Faktura sprzedazy)
    /// </summary>
    [HttpPost("sales-invoice")]
    public ActionResult<ApiResponse<DocumentDto>> CreateSalesInvoice([FromBody] CreateDocumentRequest request)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var dokumentySprzedazy = sfera.DokumentySprzedazy();

            using (var faktura = dokumentySprzedazy.UtworzFaktureSprzedazy())
            {
                dynamic dane = faktura.Dane;

                // Set customer
                SetCustomerOnDocument(sfera, dane, request.CustomerId, request.CustomerNIP);

                // Set warehouse
                if (!string.IsNullOrEmpty(request.WarehouseSymbol))
                {
                    var magazyny = ((IEnumerable<dynamic>)sfera.Magazyny().Dane.Wszystkie()).ToList();
                    var magazyn = magazyny.FirstOrDefault(m =>
                        DynamicPropertyHelper.GetString(m, "Symbol") == request.WarehouseSymbol);
                    if (magazyn != null)
                    {
                        dane.Magazyn = magazyn;
                    }
                }

                // Set dates
                if (request.IssueDate.HasValue)
                {
                    dane.DataWystawienia = request.IssueDate.Value;
                }

                if (request.SaleDate.HasValue)
                {
                    dane.DataSprzedazy = request.SaleDate.Value;
                }

                // Set notes
                if (!string.IsNullOrEmpty(request.Notes))
                {
                    dane.Uwagi = request.Notes;
                }

                // Add items
                AddItemsToDocument(sfera, faktura, request.Items);

                if ((bool)faktura.Zapisz())
                {
                    var fullNumber = DynamicPropertyHelper.GetString(dane, "NumerWewnetrzny", "PelnaSygnatura");
                    _logger.LogInformation("Created sales invoice {Number}", fullNumber);

                    return CreatedAtAction(
                        nameof(GetDocument),
                        new { id = DynamicPropertyHelper.GetId(dane) },
                        ApiResponse<DocumentDto>.Ok(MapSalesDocumentToDto(dane), "Sales invoice created successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(faktura);
                    return BadRequest(ApiResponse<DocumentDto>.Error("Failed to create sales invoice", errors));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating sales invoice");
            return StatusCode(500, ApiResponse<DocumentDto>.Error("Error creating sales invoice", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create a new customer order (Zamowienie od klienta)
    /// </summary>
    [HttpPost("customer-order")]
    public ActionResult<ApiResponse<DocumentDto>> CreateCustomerOrder([FromBody] CreateDocumentRequest request)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var zamowieniaManager = sfera.ZamowieniaOdKlientow();
            var konfiguracja = sfera.Konfiguracje().DaneDomyslne.ZamowienieOdKlienta;

            using (var zamowienie = zamowieniaManager.Utworz(konfiguracja))
            {
                dynamic dane = zamowienie.Dane;

                // Set customer
                SetCustomerOnDocument(sfera, dane, request.CustomerId, request.CustomerNIP);

                // Set warehouse
                if (!string.IsNullOrEmpty(request.WarehouseSymbol))
                {
                    var magazyny = ((IEnumerable<dynamic>)sfera.Magazyny().Dane.Wszystkie()).ToList();
                    var magazyn = magazyny.FirstOrDefault(m =>
                        DynamicPropertyHelper.GetString(m, "Symbol") == request.WarehouseSymbol);
                    if (magazyn != null)
                    {
                        dane.Magazyn = magazyn;
                    }
                }

                // Set notes
                if (!string.IsNullOrEmpty(request.Notes))
                {
                    dane.Uwagi = request.Notes;
                }

                // Add items
                AddItemsToDocument(sfera, zamowienie, request.Items);

                if ((bool)zamowienie.Zapisz())
                {
                    var fullNumber = DynamicPropertyHelper.GetString(dane, "NumerWewnetrzny", "PelnaSygnatura");
                    _logger.LogInformation("Created customer order {Number}", fullNumber);

                    return CreatedAtAction(
                        nameof(GetDocument),
                        new { id = DynamicPropertyHelper.GetId(dane) },
                        ApiResponse<DocumentDto>.Ok(MapOrderToDto(dane), "Customer order created successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(zamowienie);
                    return BadRequest(ApiResponse<DocumentDto>.Error("Failed to create customer order", errors));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating customer order");
            return StatusCode(500, ApiResponse<DocumentDto>.Error("Error creating customer order", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create a purchase invoice (Faktura zakupu)
    /// </summary>
    [HttpPost("purchase-invoice")]
    public ActionResult<ApiResponse<DocumentDto>> CreatePurchaseInvoice([FromBody] CreateDocumentRequest request)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var dokumentyZakupu = sfera.DokumentyZakupu();

            using (var faktura = dokumentyZakupu.UtworzFaktureZakupu())
            {
                dynamic dane = faktura.Dane;

                // Set supplier
                SetCustomerOnDocument(sfera, dane, request.CustomerId, request.CustomerNIP);

                // Set warehouse
                if (!string.IsNullOrEmpty(request.WarehouseSymbol))
                {
                    var magazyny = ((IEnumerable<dynamic>)sfera.Magazyny().Dane.Wszystkie()).ToList();
                    var magazyn = magazyny.FirstOrDefault(m =>
                        DynamicPropertyHelper.GetString(m, "Symbol") == request.WarehouseSymbol);
                    if (magazyn != null)
                    {
                        dane.Magazyn = magazyn;
                    }
                }

                // Set dates
                if (request.IssueDate.HasValue)
                {
                    dane.DataWystawienia = request.IssueDate.Value;
                }

                // Set notes
                if (!string.IsNullOrEmpty(request.Notes))
                {
                    dane.Uwagi = request.Notes;
                }

                // Add items (use JednostkaZakupu if available)
                AddItemsToDocument(sfera, faktura, request.Items, usePurchaseUnit: true);

                if ((bool)faktura.Zapisz())
                {
                    var fullNumber = DynamicPropertyHelper.GetString(dane, "NumerWewnetrzny", "PelnaSygnatura");
                    _logger.LogInformation("Created purchase invoice {Number}", fullNumber);

                    return CreatedAtAction(
                        nameof(GetDocument),
                        new { id = DynamicPropertyHelper.GetId(dane) },
                        ApiResponse<DocumentDto>.Ok(MapPurchaseDocumentToDto(dane), "Purchase invoice created successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(faktura);
                    return BadRequest(ApiResponse<DocumentDto>.Error("Failed to create purchase invoice", errors));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating purchase invoice");
            return StatusCode(500, ApiResponse<DocumentDto>.Error("Error creating purchase invoice", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create a sales invoice correction (Korekta faktury sprzedazy)
    /// </summary>
    [HttpPost("sales-invoice-correction")]
    public ActionResult<ApiResponse<CorrectionDto>> CreateSalesInvoiceCorrection([FromBody] CreateCorrectionRequest request)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var korektyManager = sfera.KorektyDokumentowSprzedazy();

            dynamic korekta;

            // If we have original document, create correction for it
            if (request.OriginalDocumentId.HasValue)
            {
                var dokumentySprzedazy = sfera.DokumentySprzedazy();
                var allDokumenty = ((IEnumerable<dynamic>)dokumentySprzedazy.Dane.Wszystkie()).ToList();
                var oryginal = allDokumenty.FirstOrDefault(d =>
                    DynamicPropertyHelper.GetId(d) == request.OriginalDocumentId.Value);

                if (oryginal == null)
                {
                    return NotFound(ApiResponse<CorrectionDto>.Error($"Original document with ID {request.OriginalDocumentId} not found"));
                }

                korekta = korektyManager.UtworzKorekteFakturySprzedazy(oryginal);
            }
            else
            {
                // Correction without original document
                korekta = korektyManager.UtworzKorekteFakturySprzedazy();
                SetCustomerOnDocument(sfera, korekta.Dane, request.CustomerId, request.CustomerNIP);
            }

            dynamic dane = korekta.Dane;

            // Set correction reason
            if (!string.IsNullOrEmpty(request.CorrectionReason))
            {
                dane.PrzyczynaKorekty = request.CorrectionReason;
            }

            if (!string.IsNullOrEmpty(request.Notes))
            {
                dane.Uwagi = request.Notes;
            }

            if (request.IssueDate.HasValue)
            {
                dane.DataWystawienia = request.IssueDate.Value;
            }

            // Add correction items
            AddCorrectionItems(sfera, korekta, request.Items);

            if ((bool)korekta.Zapisz())
            {
                var fullNumber = DynamicPropertyHelper.GetString(dane, "NumerWewnetrzny", "PelnaSygnatura");
                _logger.LogInformation("Created sales invoice correction {Number}", fullNumber);

                return CreatedAtAction(
                    nameof(GetDocument),
                    new { id = DynamicPropertyHelper.GetId(dane) },
                    ApiResponse<CorrectionDto>.Ok(MapCorrectionToDto(dane), "Sales invoice correction created successfully"));
            }
            else
            {
                var errors = GetBusinessObjectErrors(korekta);
                return BadRequest(ApiResponse<CorrectionDto>.Error("Failed to create sales invoice correction", errors));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating sales invoice correction");
            return StatusCode(500, ApiResponse<CorrectionDto>.Error("Error creating sales invoice correction", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create a purchase invoice correction (Korekta faktury zakupu)
    /// </summary>
    [HttpPost("purchase-invoice-correction")]
    public ActionResult<ApiResponse<CorrectionDto>> CreatePurchaseInvoiceCorrection([FromBody] CreateCorrectionRequest request)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var korektyManager = sfera.KorektyDokumentowZakupu();

            dynamic korekta;

            if (request.OriginalDocumentId.HasValue)
            {
                var dokumentyZakupu = sfera.DokumentyZakupu();
                var allDokumenty = ((IEnumerable<dynamic>)dokumentyZakupu.Dane.Wszystkie()).ToList();
                var oryginal = allDokumenty.FirstOrDefault(d =>
                    DynamicPropertyHelper.GetId(d) == request.OriginalDocumentId.Value);

                if (oryginal == null)
                {
                    return NotFound(ApiResponse<CorrectionDto>.Error($"Original document with ID {request.OriginalDocumentId} not found"));
                }

                korekta = korektyManager.UtworzKorekteFakturyZakupu(oryginal);
            }
            else
            {
                korekta = korektyManager.UtworzKorekteFakturyZakupu();
                SetCustomerOnDocument(sfera, korekta.Dane, request.CustomerId, request.CustomerNIP);
            }

            dynamic dane = korekta.Dane;

            if (!string.IsNullOrEmpty(request.CorrectionReason))
            {
                dane.PrzyczynaKorekty = request.CorrectionReason;
            }

            if (!string.IsNullOrEmpty(request.Notes))
            {
                dane.Uwagi = request.Notes;
            }

            if (request.IssueDate.HasValue)
            {
                dane.DataWystawienia = request.IssueDate.Value;
            }

            // Add correction items
            AddCorrectionItems(sfera, korekta, request.Items, usePurchaseUnit: true);

            if ((bool)korekta.Zapisz())
            {
                var fullNumber = DynamicPropertyHelper.GetString(dane, "NumerWewnetrzny", "PelnaSygnatura");
                _logger.LogInformation("Created purchase invoice correction {Number}", fullNumber);

                return CreatedAtAction(
                    nameof(GetDocument),
                    new { id = DynamicPropertyHelper.GetId(dane) },
                    ApiResponse<CorrectionDto>.Ok(MapPurchaseCorrectionToDto(dane), "Purchase invoice correction created successfully"));
            }
            else
            {
                var errors = GetBusinessObjectErrors(korekta);
                return BadRequest(ApiResponse<CorrectionDto>.Error("Failed to create purchase invoice correction", errors));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating purchase invoice correction");
            return StatusCode(500, ApiResponse<CorrectionDto>.Error("Error creating purchase invoice correction", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create a receipt (Paragon)
    /// </summary>
    [HttpPost("receipt")]
    public ActionResult<ApiResponse<DocumentDto>> CreateReceipt([FromBody] CreateReceiptRequest request)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var dokumentySprzedazy = sfera.DokumentySprzedazy();

            dynamic paragon = request.Type switch
            {
                ReceiptType.Named => dokumentySprzedazy.UtworzParagonImienny(),
                ReceiptType.Fiscal => dokumentySprzedazy.UtworzParagonFiskalny(),
                _ => dokumentySprzedazy.UtworzParagon()
            };

            dynamic dane = paragon.Dane;

            // Set customer for named receipts
            if (request.Type == ReceiptType.Named)
            {
                SetCustomerOnDocument(sfera, dane, request.CustomerId, request.CustomerNIP);
            }

            // Set warehouse
            if (!string.IsNullOrEmpty(request.WarehouseSymbol))
            {
                var magazyny = ((IEnumerable<dynamic>)sfera.Magazyny().Dane.Wszystkie()).ToList();
                var magazyn = magazyny.FirstOrDefault(m =>
                    DynamicPropertyHelper.GetString(m, "Symbol") == request.WarehouseSymbol);
                if (magazyn != null)
                {
                    dane.Magazyn = magazyn;
                }
            }

            if (request.IssueDate.HasValue)
            {
                dane.DataWystawienia = request.IssueDate.Value;
            }

            if (!string.IsNullOrEmpty(request.Notes))
            {
                dane.Uwagi = request.Notes;
            }

            // Add items
            AddReceiptItems(sfera, paragon, request.Items);

            if ((bool)paragon.Zapisz())
            {
                var fullNumber = DynamicPropertyHelper.GetString(dane, "NumerWewnetrzny", "PelnaSygnatura");
                _logger.LogInformation("Created receipt {Number}", fullNumber);

                return CreatedAtAction(
                    nameof(GetDocument),
                    new { id = DynamicPropertyHelper.GetId(dane) },
                    ApiResponse<DocumentDto>.Ok(MapSalesDocumentToDto(dane), "Receipt created successfully"));
            }
            else
            {
                var errors = GetBusinessObjectErrors(paragon);
                return BadRequest(ApiResponse<DocumentDto>.Error("Failed to create receipt", errors));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating receipt");
            return StatusCode(500, ApiResponse<DocumentDto>.Error("Error creating receipt", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create receipt return (Zwrot do paragonu)
    /// </summary>
    [HttpPost("receipt-return")]
    public ActionResult<ApiResponse<CorrectionDto>> CreateReceiptReturn([FromBody] CreateCorrectionRequest request)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var korektyManager = sfera.KorektyDokumentowSprzedazy();

            dynamic zwrot;

            if (request.OriginalDocumentId.HasValue)
            {
                var dokumentySprzedazy = sfera.DokumentySprzedazy();
                var allDokumenty = ((IEnumerable<dynamic>)dokumentySprzedazy.Dane.Wszystkie()).ToList();
                var paragon = allDokumenty.FirstOrDefault(d =>
                    DynamicPropertyHelper.GetId(d) == request.OriginalDocumentId.Value);

                if (paragon == null)
                {
                    return NotFound(ApiResponse<CorrectionDto>.Error($"Original receipt with ID {request.OriginalDocumentId} not found"));
                }

                zwrot = korektyManager.UtworzZwrotDoParagonu(paragon);
            }
            else
            {
                zwrot = korektyManager.UtworzZwrotDoParagonu();
            }

            dynamic dane = zwrot.Dane;

            if (!string.IsNullOrEmpty(request.CorrectionReason))
            {
                dane.PrzyczynaKorekty = request.CorrectionReason;
            }

            if (!string.IsNullOrEmpty(request.Notes))
            {
                dane.Uwagi = request.Notes;
            }

            // Add return items
            AddReturnItems(sfera, zwrot, request.Items);

            if ((bool)zwrot.Zapisz())
            {
                var fullNumber = DynamicPropertyHelper.GetString(dane, "NumerWewnetrzny", "PelnaSygnatura");
                _logger.LogInformation("Created receipt return {Number}", fullNumber);

                return CreatedAtAction(
                    nameof(GetDocument),
                    new { id = DynamicPropertyHelper.GetId(dane) },
                    ApiResponse<CorrectionDto>.Ok(MapCorrectionToDto(dane), "Receipt return created successfully"));
            }
            else
            {
                var errors = GetBusinessObjectErrors(zwrot);
                return BadRequest(ApiResponse<CorrectionDto>.Error("Failed to create receipt return", errors));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating receipt return");
            return StatusCode(500, ApiResponse<CorrectionDto>.Error("Error creating receipt return", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create an advance invoice (Faktura zaliczkowa)
    /// </summary>
    [HttpPost("advance-invoice")]
    public ActionResult<ApiResponse<DocumentDto>> CreateAdvanceInvoice([FromBody] CreateAdvanceInvoiceRequest request)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var dokumentySprzedazy = sfera.DokumentySprzedazy();

            using (var faktura = dokumentySprzedazy.UtworzFaktureZaliczkowa())
            {
                dynamic dane = faktura.Dane;

                SetCustomerOnDocument(sfera, dane, request.CustomerId, request.CustomerNIP);

                if (!string.IsNullOrEmpty(request.WarehouseSymbol))
                {
                    var magazyny = ((IEnumerable<dynamic>)sfera.Magazyny().Dane.Wszystkie()).ToList();
                    var magazyn = magazyny.FirstOrDefault(m =>
                        DynamicPropertyHelper.GetString(m, "Symbol") == request.WarehouseSymbol);
                    if (magazyn != null)
                    {
                        dane.Magazyn = magazyn;
                    }
                }

                if (request.IssueDate.HasValue)
                {
                    dane.DataWystawienia = request.IssueDate.Value;
                }

                if (request.SaleDate.HasValue)
                {
                    dane.DataSprzedazy = request.SaleDate.Value;
                }

                if (!string.IsNullOrEmpty(request.Notes))
                {
                    dane.Uwagi = request.Notes;
                }

                // Add items
                if (request.Items != null)
                {
                    AddItemsToDocument(sfera, faktura, request.Items);
                }

                if ((bool)faktura.Zapisz())
                {
                    var fullNumber = DynamicPropertyHelper.GetString(dane, "NumerWewnetrzny", "PelnaSygnatura");
                    _logger.LogInformation("Created advance invoice {Number}", fullNumber);

                    return CreatedAtAction(
                        nameof(GetDocument),
                        new { id = DynamicPropertyHelper.GetId(dane) },
                        ApiResponse<DocumentDto>.Ok(MapSalesDocumentToDto(dane), "Advance invoice created successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(faktura);
                    return BadRequest(ApiResponse<DocumentDto>.Error("Failed to create advance invoice", errors));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating advance invoice");
            return StatusCode(500, ApiResponse<DocumentDto>.Error("Error creating advance invoice", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create a VAT margin invoice (Faktura VAT marza)
    /// </summary>
    [HttpPost("vat-margin-invoice")]
    public ActionResult<ApiResponse<DocumentDto>> CreateVatMarginInvoice([FromBody] CreateDocumentRequest request)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var dokumentySprzedazy = sfera.DokumentySprzedazy();

            using (var faktura = dokumentySprzedazy.UtworzFaktureVATMarza())
            {
                dynamic dane = faktura.Dane;

                SetCustomerOnDocument(sfera, dane, request.CustomerId, request.CustomerNIP);

                if (!string.IsNullOrEmpty(request.WarehouseSymbol))
                {
                    var magazyny = ((IEnumerable<dynamic>)sfera.Magazyny().Dane.Wszystkie()).ToList();
                    var magazyn = magazyny.FirstOrDefault(m =>
                        DynamicPropertyHelper.GetString(m, "Symbol") == request.WarehouseSymbol);
                    if (magazyn != null)
                    {
                        dane.Magazyn = magazyn;
                    }
                }

                if (request.IssueDate.HasValue)
                {
                    dane.DataWystawienia = request.IssueDate.Value;
                }

                if (request.SaleDate.HasValue)
                {
                    dane.DataSprzedazy = request.SaleDate.Value;
                }

                if (!string.IsNullOrEmpty(request.Notes))
                {
                    dane.Uwagi = request.Notes;
                }

                // Add items
                AddItemsToDocument(sfera, faktura, request.Items);

                if ((bool)faktura.Zapisz())
                {
                    var fullNumber = DynamicPropertyHelper.GetString(dane, "NumerWewnetrzny", "PelnaSygnatura");
                    _logger.LogInformation("Created VAT margin invoice {Number}", fullNumber);

                    return CreatedAtAction(
                        nameof(GetDocument),
                        new { id = DynamicPropertyHelper.GetId(dane) },
                        ApiResponse<DocumentDto>.Ok(MapSalesDocumentToDto(dane), "VAT margin invoice created successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(faktura);
                    return BadRequest(ApiResponse<DocumentDto>.Error("Failed to create VAT margin invoice", errors));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating VAT margin invoice");
            return StatusCode(500, ApiResponse<DocumentDto>.Error("Error creating VAT margin invoice", new List<string> { ex.Message }));
        }
    }

    #region Helper Methods

    private void SetCustomerOnDocument(dynamic sfera, dynamic dokumentDane, int? customerId, string? customerNIP)
    {
        if (customerId.HasValue || !string.IsNullOrEmpty(customerNIP))
        {
            var podmiotyManager = sfera.Podmioty();
            var podmioty = ((IEnumerable<dynamic>)podmiotyManager.Dane.Wszystkie()).ToList();

            dynamic? podmiot = null;
            if (customerId.HasValue)
            {
                podmiot = podmioty.FirstOrDefault(p => DynamicPropertyHelper.GetId(p) == customerId.Value);
            }
            else if (!string.IsNullOrEmpty(customerNIP))
            {
                podmiot = podmioty.FirstOrDefault(p => DynamicPropertyHelper.GetString(p, "NIP") == customerNIP);
            }

            if (podmiot != null)
            {
                dokumentDane.Podmiot = podmiot;
            }
        }
    }

    private void AddItemsToDocument(dynamic sfera, dynamic dokument, List<CreateDocumentItemRequest> items, bool usePurchaseUnit = false)
    {
        if (items == null || !items.Any()) return;

        var asortymentyManager = sfera.Asortymenty();
        var asortymenty = ((IEnumerable<dynamic>)asortymentyManager.Dane.Wszystkie()).ToList();

        foreach (var item in items)
        {
            dynamic? asortyment = null;

            if (item.ProductId.HasValue)
            {
                asortyment = asortymenty.FirstOrDefault(a => DynamicPropertyHelper.GetId(a) == item.ProductId.Value);
            }
            else if (!string.IsNullOrEmpty(item.ProductSymbol))
            {
                asortyment = asortymenty.FirstOrDefault(a =>
                    DynamicPropertyHelper.GetString(a, "Symbol") == item.ProductSymbol);
            }

            if (asortyment != null)
            {
                var jednostka = usePurchaseUnit
                    ? (DynamicPropertyHelper.GetProperty(asortyment, "JednostkaZakupu") ??
                       DynamicPropertyHelper.GetProperty(asortyment, "JednostkaSprzedazy"))
                    : DynamicPropertyHelper.GetProperty(asortyment, "JednostkaSprzedazy");

                var pozycja = dokument.Pozycje.Dodaj(asortyment, item.Quantity, jednostka);

                if (item.PriceNet.HasValue && pozycja != null)
                {
                    pozycja.Dane.CenaNetto = item.PriceNet.Value;
                }

                if (item.DiscountPercent.HasValue && pozycja != null)
                {
                    pozycja.Dane.RabatProcent = item.DiscountPercent.Value;
                }
            }
            else if (!string.IsNullOrEmpty(item.Name))
            {
                _logger.LogWarning("Product not found for item: {Name}", item.Name);
            }
        }
    }

    private void AddReceiptItems(dynamic sfera, dynamic paragon, List<CreateDocumentItemRequest> items)
    {
        if (items == null || !items.Any()) return;

        var asortymentyManager = sfera.Asortymenty();
        var asortymenty = ((IEnumerable<dynamic>)asortymentyManager.Dane.Wszystkie()).ToList();

        foreach (var item in items)
        {
            dynamic? asortyment = null;

            if (item.ProductId.HasValue)
            {
                asortyment = asortymenty.FirstOrDefault(a => DynamicPropertyHelper.GetId(a) == item.ProductId.Value);
            }
            else if (!string.IsNullOrEmpty(item.ProductSymbol))
            {
                asortyment = asortymenty.FirstOrDefault(a =>
                    DynamicPropertyHelper.GetString(a, "Symbol") == item.ProductSymbol);
            }

            if (asortyment != null)
            {
                var jednostka = DynamicPropertyHelper.GetProperty(asortyment, "JednostkaSprzedazy");
                var pozycja = paragon.Pozycje.Dodaj(asortyment, item.Quantity, jednostka);

                if (item.PriceNet.HasValue && pozycja != null)
                {
                    pozycja.Dane.CenaNetto = item.PriceNet.Value;
                }
                else if (item.PriceGross.HasValue && pozycja != null)
                {
                    pozycja.Dane.CenaBrutto = item.PriceGross.Value;
                }

                if (item.DiscountPercent.HasValue && pozycja != null)
                {
                    pozycja.Dane.RabatProcent = item.DiscountPercent.Value;
                }
            }
        }
    }

    private void AddCorrectionItems(dynamic sfera, dynamic korekta, List<CreateCorrectionItemRequest> items, bool usePurchaseUnit = false)
    {
        if (items == null || !items.Any()) return;

        var asortymentyManager = sfera.Asortymenty();
        var asortymenty = ((IEnumerable<dynamic>)asortymentyManager.Dane.Wszystkie()).ToList();

        foreach (var item in items)
        {
            if (item.OriginalPositionId.HasValue)
            {
                // Find and correct existing position
                try
                {
                    var pozycjeKorygowane = DynamicPropertyHelper.GetProperty(korekta.Dane, "PozycjeKorygowane");
                    if (pozycjeKorygowane != null)
                    {
                        foreach (dynamic poz in pozycjeKorygowane)
                        {
                            if (DynamicPropertyHelper.GetId(poz) == item.OriginalPositionId.Value)
                            {
                                var pozycjaKorekty = korekta.Pozycje.Koryguj(poz);
                                if (pozycjaKorekty != null)
                                {
                                    var originalQty = DynamicPropertyHelper.GetDecimal(poz, "Ilosc");
                                    pozycjaKorekty.Dane.IloscPoKorekcie = originalQty + item.QuantityCorrection;
                                    if (item.PriceNetCorrection.HasValue)
                                    {
                                        pozycjaKorekty.Dane.CenaNettoPoKorekcie = item.PriceNetCorrection.Value;
                                    }
                                }
                                break;
                            }
                        }
                    }
                }
                catch
                {
                    // Position correction failed, continue
                }
            }
            else if (item.ProductId.HasValue || !string.IsNullOrEmpty(item.ProductSymbol))
            {
                // Add new correction position
                dynamic? asortyment = null;

                if (item.ProductId.HasValue)
                {
                    asortyment = asortymenty.FirstOrDefault(a => DynamicPropertyHelper.GetId(a) == item.ProductId.Value);
                }
                else if (!string.IsNullOrEmpty(item.ProductSymbol))
                {
                    asortyment = asortymenty.FirstOrDefault(a =>
                        DynamicPropertyHelper.GetString(a, "Symbol") == item.ProductSymbol);
                }

                if (asortyment != null)
                {
                    var jednostka = usePurchaseUnit
                        ? (DynamicPropertyHelper.GetProperty(asortyment, "JednostkaZakupu") ??
                           DynamicPropertyHelper.GetProperty(asortyment, "JednostkaSprzedazy"))
                        : DynamicPropertyHelper.GetProperty(asortyment, "JednostkaSprzedazy");

                    var pozycja = korekta.Pozycje.Dodaj(asortyment, item.QuantityCorrection, jednostka);
                    if (pozycja != null && item.PriceNetCorrection.HasValue)
                    {
                        pozycja.Dane.CenaNetto = item.PriceNetCorrection.Value;
                    }
                }
            }
        }
    }

    private void AddReturnItems(dynamic sfera, dynamic zwrot, List<CreateCorrectionItemRequest> items)
    {
        if (items == null || !items.Any()) return;

        var asortymentyManager = sfera.Asortymenty();
        var asortymenty = ((IEnumerable<dynamic>)asortymentyManager.Dane.Wszystkie()).ToList();

        foreach (var item in items)
        {
            dynamic? asortyment = null;

            if (item.ProductId.HasValue)
            {
                asortyment = asortymenty.FirstOrDefault(a => DynamicPropertyHelper.GetId(a) == item.ProductId.Value);
            }
            else if (!string.IsNullOrEmpty(item.ProductSymbol))
            {
                asortyment = asortymenty.FirstOrDefault(a =>
                    DynamicPropertyHelper.GetString(a, "Symbol") == item.ProductSymbol);
            }

            if (asortyment != null)
            {
                // For returns, quantity should be negative
                var qty = item.QuantityCorrection < 0 ? item.QuantityCorrection : -item.QuantityCorrection;
                var jednostka = DynamicPropertyHelper.GetProperty(asortyment, "JednostkaSprzedazy");
                zwrot.Pozycje.Dodaj(asortyment, qty, jednostka);
            }
        }
    }

    private static CorrectionDto MapCorrectionToDto(dynamic dokument)
    {
        try
        {
            return new CorrectionDto
            {
                Id = DynamicPropertyHelper.GetId(dokument),
                Number = DynamicPropertyHelper.GetString(dokument, "NumerWewnetrzny", "Numer") ?? "",
                FullNumber = DynamicPropertyHelper.GetString(dokument, "NumerWewnetrzny", "PelnaSygnatura"),
                Type = CorrectionType.SalesInvoiceCorrection,
                IssueDate = DynamicPropertyHelper.GetDateTime(dokument, "DataWystawienia"),
                OriginalDocumentId = DynamicPropertyHelper.GetNullableInt(dokument, "DokumentKorygowany", "Id"),
                OriginalDocumentNumber = DynamicPropertyHelper.GetNestedString(dokument, "DokumentKorygowany", "NumerWewnetrzny", "PelnaSygnatura"),
                CustomerName = DynamicPropertyHelper.GetString(dokument, "Podmiot", "NazwaSkrocona"),
                CustomerNIP = DynamicPropertyHelper.GetString(dokument, "Podmiot", "NIP"),
                WarehouseSymbol = DynamicPropertyHelper.GetString(dokument, "Magazyn", "Symbol"),
                CorrectionNet = DynamicPropertyHelper.GetDecimal(dokument, "WartoscNetto"),
                CorrectionVat = DynamicPropertyHelper.GetDecimal(dokument, "WartoscVat"),
                CorrectionGross = DynamicPropertyHelper.GetDecimal(dokument, "WartoscBrutto"),
                CorrectionReason = DynamicPropertyHelper.GetString(dokument, "PrzyczynaKorekty"),
                Notes = DynamicPropertyHelper.GetString(dokument, "Uwagi"),
                CreatedAt = DynamicPropertyHelper.GetDateTime(dokument, "DataUtworzenia")
            };
        }
        catch
        {
            return new CorrectionDto { Id = DynamicPropertyHelper.GetId(dokument) };
        }
    }

    private static CorrectionDto MapPurchaseCorrectionToDto(dynamic dokument)
    {
        try
        {
            return new CorrectionDto
            {
                Id = DynamicPropertyHelper.GetId(dokument),
                Number = DynamicPropertyHelper.GetString(dokument, "NumerWewnetrzny", "Numer") ?? "",
                FullNumber = DynamicPropertyHelper.GetString(dokument, "NumerWewnetrzny", "PelnaSygnatura"),
                Type = CorrectionType.PurchaseInvoiceCorrection,
                IssueDate = DynamicPropertyHelper.GetDateTime(dokument, "DataWystawienia"),
                OriginalDocumentId = DynamicPropertyHelper.GetNullableInt(dokument, "DokumentKorygowany", "Id"),
                OriginalDocumentNumber = DynamicPropertyHelper.GetNestedString(dokument, "DokumentKorygowany", "NumerWewnetrzny", "PelnaSygnatura"),
                CustomerName = DynamicPropertyHelper.GetString(dokument, "Podmiot", "NazwaSkrocona"),
                CustomerNIP = DynamicPropertyHelper.GetString(dokument, "Podmiot", "NIP"),
                WarehouseSymbol = DynamicPropertyHelper.GetString(dokument, "Magazyn", "Symbol"),
                CorrectionNet = DynamicPropertyHelper.GetDecimal(dokument, "WartoscNetto"),
                CorrectionVat = DynamicPropertyHelper.GetDecimal(dokument, "WartoscVat"),
                CorrectionGross = DynamicPropertyHelper.GetDecimal(dokument, "WartoscBrutto"),
                CorrectionReason = DynamicPropertyHelper.GetString(dokument, "PrzyczynaKorekty"),
                Notes = DynamicPropertyHelper.GetString(dokument, "Uwagi"),
                CreatedAt = DynamicPropertyHelper.GetDateTime(dokument, "DataUtworzenia")
            };
        }
        catch
        {
            return new CorrectionDto { Id = DynamicPropertyHelper.GetId(dokument) };
        }
    }

    private static DocumentDto MapDocumentToDto(dynamic dokument)
    {
        try
        {
            return new DocumentDto
            {
                Id = DynamicPropertyHelper.GetId(dokument),
                Number = DynamicPropertyHelper.GetString(dokument, "NumerWewnetrzny", "Numer") ?? "",
                FullNumber = DynamicPropertyHelper.GetString(dokument, "NumerWewnetrzny", "PelnaSygnatura"),
                IssueDate = DynamicPropertyHelper.GetDateTime(dokument, "DataWystawienia"),
                CustomerName = DynamicPropertyHelper.GetString(dokument, "Podmiot", "NazwaSkrocona"),
                CustomerNIP = DynamicPropertyHelper.GetString(dokument, "Podmiot", "NIP"),
                WarehouseSymbol = DynamicPropertyHelper.GetString(dokument, "Magazyn", "Symbol"),
                TotalNet = DynamicPropertyHelper.GetDecimal(dokument, "WartoscNetto"),
                TotalVat = DynamicPropertyHelper.GetDecimal(dokument, "WartoscVat"),
                TotalGross = DynamicPropertyHelper.GetDecimal(dokument, "WartoscBrutto"),
                Notes = DynamicPropertyHelper.GetString(dokument, "Uwagi"),
                CreatedAt = DynamicPropertyHelper.GetDateTime(dokument, "DataUtworzenia")
            };
        }
        catch
        {
            return new DocumentDto { Id = DynamicPropertyHelper.GetId(dokument) };
        }
    }

    private static DocumentDto MapSalesDocumentToDto(dynamic dokument)
    {
        try
        {
            var dto = new DocumentDto
            {
                Id = DynamicPropertyHelper.GetId(dokument),
                Number = DynamicPropertyHelper.GetString(dokument, "NumerWewnetrzny", "Numer") ?? "",
                FullNumber = DynamicPropertyHelper.GetString(dokument, "NumerWewnetrzny", "PelnaSygnatura"),
                Type = DocumentType.SalesInvoice,
                IssueDate = DynamicPropertyHelper.GetDateTime(dokument, "DataWystawienia"),
                SaleDate = DynamicPropertyHelper.GetDateTime(dokument, "DataSprzedazy"),
                CustomerName = DynamicPropertyHelper.GetString(dokument, "Podmiot", "NazwaSkrocona"),
                CustomerNIP = DynamicPropertyHelper.GetString(dokument, "Podmiot", "NIP"),
                WarehouseSymbol = DynamicPropertyHelper.GetString(dokument, "Magazyn", "Symbol"),
                TotalNet = DynamicPropertyHelper.GetDecimal(dokument, "WartoscNetto"),
                TotalVat = DynamicPropertyHelper.GetDecimal(dokument, "WartoscVat"),
                TotalGross = DynamicPropertyHelper.GetDecimal(dokument, "WartoscBrutto"),
                Notes = DynamicPropertyHelper.GetString(dokument, "Uwagi"),
                Items = new List<DocumentItemDto>()
            };

            var pozycje = DynamicPropertyHelper.GetProperty(dokument, "Pozycje");
            if (pozycje != null)
            {
                int lineNum = 1;
                foreach (dynamic poz in pozycje)
                {
                    dto.Items.Add(MapDocumentItemToDto(poz, lineNum++));
                }
            }

            return dto;
        }
        catch
        {
            return new DocumentDto { Id = DynamicPropertyHelper.GetId(dokument) };
        }
    }

    private static DocumentDto MapPurchaseDocumentToDto(dynamic dokument)
    {
        try
        {
            return new DocumentDto
            {
                Id = DynamicPropertyHelper.GetId(dokument),
                Number = DynamicPropertyHelper.GetString(dokument, "NumerWewnetrzny", "Numer") ?? "",
                FullNumber = DynamicPropertyHelper.GetString(dokument, "NumerWewnetrzny", "PelnaSygnatura"),
                Type = DocumentType.PurchaseInvoice,
                IssueDate = DynamicPropertyHelper.GetDateTime(dokument, "DataWystawienia"),
                CustomerName = DynamicPropertyHelper.GetString(dokument, "Podmiot", "NazwaSkrocona"),
                CustomerNIP = DynamicPropertyHelper.GetString(dokument, "Podmiot", "NIP"),
                WarehouseSymbol = DynamicPropertyHelper.GetString(dokument, "Magazyn", "Symbol"),
                TotalNet = DynamicPropertyHelper.GetDecimal(dokument, "WartoscNetto"),
                TotalVat = DynamicPropertyHelper.GetDecimal(dokument, "WartoscVat"),
                TotalGross = DynamicPropertyHelper.GetDecimal(dokument, "WartoscBrutto"),
                Notes = DynamicPropertyHelper.GetString(dokument, "Uwagi"),
                Items = new List<DocumentItemDto>()
            };
        }
        catch
        {
            return new DocumentDto { Id = DynamicPropertyHelper.GetId(dokument) };
        }
    }

    private static DocumentDto MapOrderToDto(dynamic dokument)
    {
        try
        {
            var dto = new DocumentDto
            {
                Id = DynamicPropertyHelper.GetId(dokument),
                Number = DynamicPropertyHelper.GetString(dokument, "NumerWewnetrzny", "Numer") ?? "",
                FullNumber = DynamicPropertyHelper.GetString(dokument, "NumerWewnetrzny", "PelnaSygnatura"),
                Type = DocumentType.CustomerOrder,
                IssueDate = DynamicPropertyHelper.GetDateTime(dokument, "DataWystawienia"),
                CustomerName = DynamicPropertyHelper.GetString(dokument, "Podmiot", "NazwaSkrocona"),
                CustomerNIP = DynamicPropertyHelper.GetString(dokument, "Podmiot", "NIP"),
                WarehouseSymbol = DynamicPropertyHelper.GetString(dokument, "Magazyn", "Symbol"),
                TotalNet = DynamicPropertyHelper.GetDecimal(dokument, "WartoscNetto"),
                TotalVat = DynamicPropertyHelper.GetDecimal(dokument, "WartoscVat"),
                TotalGross = DynamicPropertyHelper.GetDecimal(dokument, "WartoscBrutto"),
                Notes = DynamicPropertyHelper.GetString(dokument, "Uwagi"),
                Items = new List<DocumentItemDto>()
            };

            var pozycje = DynamicPropertyHelper.GetProperty(dokument, "Pozycje");
            if (pozycje != null)
            {
                int lineNum = 1;
                foreach (dynamic poz in pozycje)
                {
                    dto.Items.Add(MapDocumentItemToDto(poz, lineNum++));
                }
            }

            return dto;
        }
        catch
        {
            return new DocumentDto { Id = DynamicPropertyHelper.GetId(dokument) };
        }
    }

    private static DocumentItemDto MapDocumentItemToDto(dynamic poz, int lineNum)
    {
        return new DocumentItemDto
        {
            Id = DynamicPropertyHelper.GetId(poz),
            LineNumber = lineNum,
            ProductId = DynamicPropertyHelper.GetNullableInt(poz, "Asortyment", "Id"),
            ProductSymbol = DynamicPropertyHelper.GetString(poz, "Asortyment", "Symbol"),
            Name = DynamicPropertyHelper.GetString(poz, "Nazwa"),
            Quantity = DynamicPropertyHelper.GetDecimal(poz, "Ilosc"),
            Unit = DynamicPropertyHelper.GetString(poz, "Jednostka", "Symbol") ?? "szt.",
            PriceNet = DynamicPropertyHelper.GetDecimal(poz, "CenaNetto"),
            PriceGross = DynamicPropertyHelper.GetDecimal(poz, "CenaBrutto"),
            ValueNet = DynamicPropertyHelper.GetDecimal(poz, "WartoscNetto"),
            ValueVat = DynamicPropertyHelper.GetDecimal(poz, "WartoscVat"),
            ValueGross = DynamicPropertyHelper.GetDecimal(poz, "WartoscBrutto")
        };
    }

    private static List<string> GetBusinessObjectErrors(dynamic obiekt)
    {
        var errors = new List<string>();
        try
        {
            var invalidData = DynamicPropertyHelper.GetProperty(obiekt, "InvalidData");
            if (invalidData == null) return errors;

            foreach (var encjaZBledami in invalidData)
            {
                var entityErrors = DynamicPropertyHelper.GetProperty(encjaZBledami, "Errors");
                if (entityErrors != null)
                {
                    foreach (var blad in entityErrors)
                    {
                        errors.Add(blad?.ToString() ?? "Unknown error");
                    }
                }

                var memberErrors = DynamicPropertyHelper.GetProperty(encjaZBledami, "MemberErrors");
                if (memberErrors != null)
                {
                    foreach (var bladNaPolach in memberErrors)
                    {
                        try
                        {
                            var key = DynamicPropertyHelper.GetProperty(bladNaPolach, "Key");
                            errors.Add($"{key}: {bladNaPolach}");
                        }
                        catch
                        {
                            errors.Add(bladNaPolach?.ToString() ?? "Unknown error");
                        }
                    }
                }
            }
        }
        catch
        {
            errors.Add("Could not retrieve error details");
        }
        return errors;
    }

    #endregion
}
