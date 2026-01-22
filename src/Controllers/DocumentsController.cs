using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Requests;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using InsERT.Moria.Asortymenty;

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
            var sfera = _sferaService.GetSfera();
            var dokumenty = sfera.Dokumenty();

            var dataQuery = dokumenty.Dane.Wszystkie();

            if (query.DateFrom.HasValue)
            {
                dataQuery = dataQuery.Where(d => d.DataWystawienia >= query.DateFrom.Value);
            }

            if (query.DateTo.HasValue)
            {
                dataQuery = dataQuery.Where(d => d.DataWystawienia <= query.DateTo.Value);
            }

            if (query.CustomerId.HasValue)
            {
                dataQuery = dataQuery.Where(d => d.Podmiot != null && d.Podmiot.Id == query.CustomerId.Value);
            }

            var totalCount = dataQuery.Count();
            var items = dataQuery
                .OrderByDescending(d => d.DataWystawienia)
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
            var sfera = _sferaService.GetSfera();
            var dokumenty = sfera.Dokumenty();

            var dokument = dokumenty.Dane.Wszystkie().FirstOrDefault(d => d.Id == id);
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
            var sfera = _sferaService.GetSfera();
            var dokumenty = sfera.Dokumenty();

            var dokument = dokumenty.Dane.Wszystkie()
                .FirstOrDefault(d => d.NumerWewnetrzny != null &&
                    d.NumerWewnetrzny.PelnaSygnatura.Contains(number));

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
            var sfera = _sferaService.GetSfera();
            var dokumentySprzedazy = sfera.DokumentySprzedazy();

            using (var faktura = dokumentySprzedazy.UtworzFaktureSprzedazy())
            {
                // Set customer
                if (request.CustomerId.HasValue)
                {
                    var podmiot = sfera.Podmioty().Dane.Wszystkie()
                        .FirstOrDefault(p => p.Id == request.CustomerId.Value);
                    if (podmiot != null)
                    {
                        faktura.Dane.Podmiot = podmiot;
                    }
                }
                else if (!string.IsNullOrEmpty(request.CustomerNIP))
                {
                    var podmiot = sfera.Podmioty().Dane
                        .Pierwszy(p => p.NIP == request.CustomerNIP);
                    if (podmiot != null)
                    {
                        faktura.Dane.Podmiot = podmiot;
                    }
                }

                // Set warehouse
                if (!string.IsNullOrEmpty(request.WarehouseSymbol))
                {
                    var magazyn = sfera.Magazyny().Dane
                        .Pierwszy(m => m.Symbol == request.WarehouseSymbol);
                    if (magazyn != null)
                    {
                        faktura.Dane.Magazyn = magazyn;
                    }
                }

                // Set dates
                if (request.IssueDate.HasValue)
                {
                    faktura.Dane.DataWystawienia = request.IssueDate.Value;
                }

                if (request.SaleDate.HasValue)
                {
                    faktura.Dane.DataSprzedazy = request.SaleDate.Value;
                }

                // Set notes
                if (!string.IsNullOrEmpty(request.Notes))
                {
                    faktura.Dane.Uwagi = request.Notes;
                }

                // Add items
                var asortymenty = sfera.Asortymenty();
                foreach (var item in request.Items)
                {
                    Asortyment? asortyment = null;

                    if (item.ProductId.HasValue)
                    {
                        asortyment = asortymenty.Dane.Wszystkie()
                            .FirstOrDefault(a => a.Id == item.ProductId.Value);
                    }
                    else if (!string.IsNullOrEmpty(item.ProductSymbol))
                    {
                        asortyment = asortymenty.Dane.Wszystkie()
                            .FirstOrDefault(a => a.Symbol == item.ProductSymbol);
                    }

                    if (asortyment != null)
                    {
                        var pozycja = faktura.Pozycje.Dodaj(asortyment, item.Quantity, asortyment.JednostkaSprzedazy);

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
                        // Add as one-time item
                        _logger.LogWarning("Product not found for item, consider using one-time product: {Name}", item.Name);
                    }
                }

                if (faktura.Zapisz())
                {
                    _logger.LogInformation("Created sales invoice {Number}",
                        faktura.Dane.NumerWewnetrzny?.PelnaSygnatura);

                    return CreatedAtAction(
                        nameof(GetDocument),
                        new { id = faktura.Dane.Id },
                        ApiResponse<DocumentDto>.Ok(MapSalesDocumentToDto(faktura.Dane), "Sales invoice created successfully"));
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
            var sfera = _sferaService.GetSfera();
            var zamowienia = sfera.ZamowieniaOdKlientow();
            var konfiguracja = sfera.Konfiguracje().DaneDomyslne.ZamowienieOdKlienta;

            using (var zamowienie = zamowienia.Utworz(konfiguracja))
            {
                // Set customer
                if (request.CustomerId.HasValue)
                {
                    var podmiot = sfera.Podmioty().Dane.Wszystkie()
                        .FirstOrDefault(p => p.Id == request.CustomerId.Value);
                    if (podmiot != null)
                    {
                        zamowienie.Dane.Podmiot = podmiot;
                    }
                }
                else if (!string.IsNullOrEmpty(request.CustomerNIP))
                {
                    var podmiot = sfera.Podmioty().Dane
                        .Pierwszy(p => p.NIP == request.CustomerNIP);
                    if (podmiot != null)
                    {
                        zamowienie.Dane.Podmiot = podmiot;
                    }
                }

                // Set warehouse
                if (!string.IsNullOrEmpty(request.WarehouseSymbol))
                {
                    var magazyn = sfera.Magazyny().Dane
                        .Pierwszy(m => m.Symbol == request.WarehouseSymbol);
                    if (magazyn != null)
                    {
                        zamowienie.Dane.Magazyn = magazyn;
                    }
                }

                // Set notes
                if (!string.IsNullOrEmpty(request.Notes))
                {
                    zamowienie.Dane.Uwagi = request.Notes;
                }

                // Add items
                var asortymenty = sfera.Asortymenty();
                foreach (var item in request.Items)
                {
                    Asortyment? asortyment = null;

                    if (item.ProductId.HasValue)
                    {
                        asortyment = asortymenty.Dane.Wszystkie()
                            .FirstOrDefault(a => a.Id == item.ProductId.Value);
                    }
                    else if (!string.IsNullOrEmpty(item.ProductSymbol))
                    {
                        asortyment = asortymenty.Dane.Wszystkie()
                            .FirstOrDefault(a => a.Symbol == item.ProductSymbol);
                    }

                    if (asortyment != null)
                    {
                        var pozycja = zamowienie.Pozycje.Dodaj(asortyment, item.Quantity, asortyment.JednostkaSprzedazy);

                        if (item.PriceNet.HasValue && pozycja != null)
                        {
                            pozycja.Dane.CenaNetto = item.PriceNet.Value;
                        }

                        if (item.DiscountPercent.HasValue && pozycja != null)
                        {
                            pozycja.Dane.RabatProcent = item.DiscountPercent.Value;
                        }
                    }
                }

                if (zamowienie.Zapisz())
                {
                    _logger.LogInformation("Created customer order {Number}",
                        zamowienie.Dane.NumerWewnetrzny?.PelnaSygnatura);

                    return CreatedAtAction(
                        nameof(GetDocument),
                        new { id = zamowienie.Dane.Id },
                        ApiResponse<DocumentDto>.Ok(MapOrderToDto(zamowienie.Dane), "Customer order created successfully"));
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
            var sfera = _sferaService.GetSfera();
            var dokumentyZakupu = sfera.DokumentyZakupu();

            using (var faktura = dokumentyZakupu.UtworzFaktureZakupu())
            {
                // Set supplier
                if (request.CustomerId.HasValue)
                {
                    var podmiot = sfera.Podmioty().Dane.Wszystkie()
                        .FirstOrDefault(p => p.Id == request.CustomerId.Value);
                    if (podmiot != null)
                    {
                        faktura.Dane.Podmiot = podmiot;
                    }
                }
                else if (!string.IsNullOrEmpty(request.CustomerNIP))
                {
                    var podmiot = sfera.Podmioty().Dane
                        .Pierwszy(p => p.NIP == request.CustomerNIP);
                    if (podmiot != null)
                    {
                        faktura.Dane.Podmiot = podmiot;
                    }
                }

                // Set warehouse
                if (!string.IsNullOrEmpty(request.WarehouseSymbol))
                {
                    var magazyn = sfera.Magazyny().Dane
                        .Pierwszy(m => m.Symbol == request.WarehouseSymbol);
                    if (magazyn != null)
                    {
                        faktura.Dane.Magazyn = magazyn;
                    }
                }

                // Set dates
                if (request.IssueDate.HasValue)
                {
                    faktura.Dane.DataWystawienia = request.IssueDate.Value;
                }

                // Set notes
                if (!string.IsNullOrEmpty(request.Notes))
                {
                    faktura.Dane.Uwagi = request.Notes;
                }

                // Add items
                var asortymenty = sfera.Asortymenty();
                foreach (var item in request.Items)
                {
                    Asortyment? asortyment = null;

                    if (item.ProductId.HasValue)
                    {
                        asortyment = asortymenty.Dane.Wszystkie()
                            .FirstOrDefault(a => a.Id == item.ProductId.Value);
                    }
                    else if (!string.IsNullOrEmpty(item.ProductSymbol))
                    {
                        asortyment = asortymenty.Dane.Wszystkie()
                            .FirstOrDefault(a => a.Symbol == item.ProductSymbol);
                    }

                    if (asortyment != null)
                    {
                        var pozycja = faktura.Pozycje.Dodaj(asortyment, item.Quantity, asortyment.JednostkaZakupu ?? asortyment.JednostkaSprzedazy);

                        if (item.PriceNet.HasValue && pozycja != null)
                        {
                            pozycja.Dane.CenaNetto = item.PriceNet.Value;
                        }
                    }
                }

                if (faktura.Zapisz())
                {
                    _logger.LogInformation("Created purchase invoice {Number}",
                        faktura.Dane.NumerWewnetrzny?.PelnaSygnatura);

                    return CreatedAtAction(
                        nameof(GetDocument),
                        new { id = faktura.Dane.Id },
                        ApiResponse<DocumentDto>.Ok(MapPurchaseDocumentToDto(faktura.Dane), "Purchase invoice created successfully"));
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
            var sfera = _sferaService.GetSfera();
            var korekty = sfera.KorektyDokumentowSprzedazy();

            IKorektaDokumentuSprzedazy korekta;

            // If we have original document, create correction for it
            if (request.OriginalDocumentId.HasValue)
            {
                var dokumentySprzedazy = sfera.DokumentySprzedazy();
                var oryginal = dokumentySprzedazy.Dane.Wszystkie()
                    .FirstOrDefault(d => d.Id == request.OriginalDocumentId.Value);

                if (oryginal == null)
                {
                    return NotFound(ApiResponse<CorrectionDto>.Error($"Original document with ID {request.OriginalDocumentId} not found"));
                }

                korekta = korekty.UtworzKorekteFakturySprzedazy(oryginal);
            }
            else
            {
                // Correction without original document
                korekta = korekty.UtworzKorekteFakturySprzedazy();

                // Set customer
                SetCustomerOnDocument(sfera, korekta.Dane, request.CustomerId, request.CustomerNIP);
            }

            // Set correction reason
            if (!string.IsNullOrEmpty(request.CorrectionReason))
            {
                korekta.Dane.PrzyczynaKorekty = request.CorrectionReason;
            }

            if (!string.IsNullOrEmpty(request.Notes))
            {
                korekta.Dane.Uwagi = request.Notes;
            }

            if (request.IssueDate.HasValue)
            {
                korekta.Dane.DataWystawienia = request.IssueDate.Value;
            }

            // Add correction items
            foreach (var item in request.Items)
            {
                if (item.OriginalPositionId.HasValue)
                {
                    // Find and correct existing position
                    var pozycjaOryginalna = korekta.Dane.PozycjeKorygowane?
                        .FirstOrDefault(p => p.Id == item.OriginalPositionId.Value);

                    if (pozycjaOryginalna != null)
                    {
                        var pozycjaKorekty = korekta.Pozycje.Koryguj(pozycjaOryginalna);
                        if (pozycjaKorekty != null)
                        {
                            pozycjaKorekty.Dane.IloscPoKorekcie = pozycjaOryginalna.Ilosc + item.QuantityCorrection;
                            if (item.PriceNetCorrection.HasValue)
                            {
                                pozycjaKorekty.Dane.CenaNettoPoKorekcie = item.PriceNetCorrection.Value;
                            }
                        }
                    }
                }
                else if (item.ProductId.HasValue || !string.IsNullOrEmpty(item.ProductSymbol))
                {
                    // Add new correction position
                    var asortymenty = sfera.Asortymenty();
                    Asortyment? asortyment = null;

                    if (item.ProductId.HasValue)
                    {
                        asortyment = asortymenty.Dane.Wszystkie()
                            .FirstOrDefault(a => a.Id == item.ProductId.Value);
                    }
                    else if (!string.IsNullOrEmpty(item.ProductSymbol))
                    {
                        asortyment = asortymenty.Dane.Wszystkie()
                            .FirstOrDefault(a => a.Symbol == item.ProductSymbol);
                    }

                    if (asortyment != null)
                    {
                        var pozycja = korekta.Pozycje.Dodaj(asortyment, item.QuantityCorrection, asortyment.JednostkaSprzedazy);
                        if (pozycja != null && item.PriceNetCorrection.HasValue)
                        {
                            pozycja.Dane.CenaNetto = item.PriceNetCorrection.Value;
                        }
                    }
                }
            }

            if (korekta.Zapisz())
            {
                _logger.LogInformation("Created sales invoice correction {Number}",
                    korekta.Dane.NumerWewnetrzny?.PelnaSygnatura);

                return CreatedAtAction(
                    nameof(GetDocument),
                    new { id = korekta.Dane.Id },
                    ApiResponse<CorrectionDto>.Ok(MapCorrectionToDto(korekta.Dane), "Sales invoice correction created successfully"));
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
            var sfera = _sferaService.GetSfera();
            var korekty = sfera.KorektyDokumentowZakupu();

            IKorektaDokumentuZakupu korekta;

            if (request.OriginalDocumentId.HasValue)
            {
                var dokumentyZakupu = sfera.DokumentyZakupu();
                var oryginal = dokumentyZakupu.Dane.Wszystkie()
                    .FirstOrDefault(d => d.Id == request.OriginalDocumentId.Value);

                if (oryginal == null)
                {
                    return NotFound(ApiResponse<CorrectionDto>.Error($"Original document with ID {request.OriginalDocumentId} not found"));
                }

                korekta = korekty.UtworzKorekteFakturyZakupu(oryginal);
            }
            else
            {
                korekta = korekty.UtworzKorekteFakturyZakupu();
                SetCustomerOnDocument(sfera, korekta.Dane, request.CustomerId, request.CustomerNIP);
            }

            if (!string.IsNullOrEmpty(request.CorrectionReason))
            {
                korekta.Dane.PrzyczynaKorekty = request.CorrectionReason;
            }

            if (!string.IsNullOrEmpty(request.Notes))
            {
                korekta.Dane.Uwagi = request.Notes;
            }

            if (request.IssueDate.HasValue)
            {
                korekta.Dane.DataWystawienia = request.IssueDate.Value;
            }

            // Add correction items (simplified - similar to sales correction)
            var asortymenty = sfera.Asortymenty();
            foreach (var item in request.Items)
            {
                if (item.ProductId.HasValue || !string.IsNullOrEmpty(item.ProductSymbol))
                {
                    Asortyment? asortyment = null;

                    if (item.ProductId.HasValue)
                    {
                        asortyment = asortymenty.Dane.Wszystkie()
                            .FirstOrDefault(a => a.Id == item.ProductId.Value);
                    }
                    else if (!string.IsNullOrEmpty(item.ProductSymbol))
                    {
                        asortyment = asortymenty.Dane.Wszystkie()
                            .FirstOrDefault(a => a.Symbol == item.ProductSymbol);
                    }

                    if (asortyment != null)
                    {
                        var pozycja = korekta.Pozycje.Dodaj(asortyment, item.QuantityCorrection,
                            asortyment.JednostkaZakupu ?? asortyment.JednostkaSprzedazy);
                        if (pozycja != null && item.PriceNetCorrection.HasValue)
                        {
                            pozycja.Dane.CenaNetto = item.PriceNetCorrection.Value;
                        }
                    }
                }
            }

            if (korekta.Zapisz())
            {
                _logger.LogInformation("Created purchase invoice correction {Number}",
                    korekta.Dane.NumerWewnetrzny?.PelnaSygnatura);

                return CreatedAtAction(
                    nameof(GetDocument),
                    new { id = korekta.Dane.Id },
                    ApiResponse<CorrectionDto>.Ok(MapPurchaseCorrectionToDto(korekta.Dane), "Purchase invoice correction created successfully"));
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
            var sfera = _sferaService.GetSfera();
            var dokumentySprzedazy = sfera.DokumentySprzedazy();

            IDokumentSprzedazy paragon = request.Type switch
            {
                ReceiptType.Named => dokumentySprzedazy.UtworzParagonImienny(),
                ReceiptType.Fiscal => dokumentySprzedazy.UtworzParagonFiskalny(),
                _ => dokumentySprzedazy.UtworzParagon()
            };

            // Set customer for named receipts
            if (request.Type == ReceiptType.Named)
            {
                SetCustomerOnDocument(sfera, paragon.Dane, request.CustomerId, request.CustomerNIP);
            }

            // Set warehouse
            if (!string.IsNullOrEmpty(request.WarehouseSymbol))
            {
                var magazyn = sfera.Magazyny().Dane.Pierwszy(m => m.Symbol == request.WarehouseSymbol);
                if (magazyn != null)
                {
                    paragon.Dane.Magazyn = magazyn;
                }
            }

            if (request.IssueDate.HasValue)
            {
                paragon.Dane.DataWystawienia = request.IssueDate.Value;
            }

            if (!string.IsNullOrEmpty(request.Notes))
            {
                paragon.Dane.Uwagi = request.Notes;
            }

            // Add items
            var asortymenty = sfera.Asortymenty();
            foreach (var item in request.Items)
            {
                Asortyment? asortyment = null;

                if (item.ProductId.HasValue)
                {
                    asortyment = asortymenty.Dane.Wszystkie()
                        .FirstOrDefault(a => a.Id == item.ProductId.Value);
                }
                else if (!string.IsNullOrEmpty(item.ProductSymbol))
                {
                    asortyment = asortymenty.Dane.Wszystkie()
                        .FirstOrDefault(a => a.Symbol == item.ProductSymbol);
                }

                if (asortyment != null)
                {
                    var pozycja = paragon.Pozycje.Dodaj(asortyment, item.Quantity, asortyment.JednostkaSprzedazy);

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

            if (paragon.Zapisz())
            {
                _logger.LogInformation("Created receipt {Number}", paragon.Dane.NumerWewnetrzny?.PelnaSygnatura);

                return CreatedAtAction(
                    nameof(GetDocument),
                    new { id = paragon.Dane.Id },
                    ApiResponse<DocumentDto>.Ok(MapSalesDocumentToDto(paragon.Dane), "Receipt created successfully"));
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
            var sfera = _sferaService.GetSfera();
            var korekty = sfera.KorektyDokumentowSprzedazy();

            IKorektaDokumentuSprzedazy zwrot;

            if (request.OriginalDocumentId.HasValue)
            {
                var dokumentySprzedazy = sfera.DokumentySprzedazy();
                var paragon = dokumentySprzedazy.Dane.Wszystkie()
                    .FirstOrDefault(d => d.Id == request.OriginalDocumentId.Value);

                if (paragon == null)
                {
                    return NotFound(ApiResponse<CorrectionDto>.Error($"Original receipt with ID {request.OriginalDocumentId} not found"));
                }

                zwrot = korekty.UtworzZwrotDoParagonu(paragon);
            }
            else
            {
                zwrot = korekty.UtworzZwrotDoParagonu();
            }

            if (!string.IsNullOrEmpty(request.CorrectionReason))
            {
                zwrot.Dane.PrzyczynaKorekty = request.CorrectionReason;
            }

            if (!string.IsNullOrEmpty(request.Notes))
            {
                zwrot.Dane.Uwagi = request.Notes;
            }

            // Add return items
            var asortymenty = sfera.Asortymenty();
            foreach (var item in request.Items)
            {
                Asortyment? asortyment = null;

                if (item.ProductId.HasValue)
                {
                    asortyment = asortymenty.Dane.Wszystkie()
                        .FirstOrDefault(a => a.Id == item.ProductId.Value);
                }
                else if (!string.IsNullOrEmpty(item.ProductSymbol))
                {
                    asortyment = asortymenty.Dane.Wszystkie()
                        .FirstOrDefault(a => a.Symbol == item.ProductSymbol);
                }

                if (asortyment != null)
                {
                    // For returns, quantity should be negative
                    var qty = item.QuantityCorrection < 0 ? item.QuantityCorrection : -item.QuantityCorrection;
                    zwrot.Pozycje.Dodaj(asortyment, qty, asortyment.JednostkaSprzedazy);
                }
            }

            if (zwrot.Zapisz())
            {
                _logger.LogInformation("Created receipt return {Number}", zwrot.Dane.NumerWewnetrzny?.PelnaSygnatura);

                return CreatedAtAction(
                    nameof(GetDocument),
                    new { id = zwrot.Dane.Id },
                    ApiResponse<CorrectionDto>.Ok(MapCorrectionToDto(zwrot.Dane), "Receipt return created successfully"));
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
            var sfera = _sferaService.GetSfera();
            var dokumentySprzedazy = sfera.DokumentySprzedazy();

            using (var faktura = dokumentySprzedazy.UtworzFaktureZaliczkowa())
            {
                SetCustomerOnDocument(sfera, faktura.Dane, request.CustomerId, request.CustomerNIP);

                if (!string.IsNullOrEmpty(request.WarehouseSymbol))
                {
                    var magazyn = sfera.Magazyny().Dane.Pierwszy(m => m.Symbol == request.WarehouseSymbol);
                    if (magazyn != null)
                    {
                        faktura.Dane.Magazyn = magazyn;
                    }
                }

                if (request.IssueDate.HasValue)
                {
                    faktura.Dane.DataWystawienia = request.IssueDate.Value;
                }

                if (request.SaleDate.HasValue)
                {
                    faktura.Dane.DataSprzedazy = request.SaleDate.Value;
                }

                if (!string.IsNullOrEmpty(request.Notes))
                {
                    faktura.Dane.Uwagi = request.Notes;
                }

                // Add items
                if (request.Items != null)
                {
                    var asortymenty = sfera.Asortymenty();
                    foreach (var item in request.Items)
                    {
                        Asortyment? asortyment = null;

                        if (item.ProductId.HasValue)
                        {
                            asortyment = asortymenty.Dane.Wszystkie()
                                .FirstOrDefault(a => a.Id == item.ProductId.Value);
                        }
                        else if (!string.IsNullOrEmpty(item.ProductSymbol))
                        {
                            asortyment = asortymenty.Dane.Wszystkie()
                                .FirstOrDefault(a => a.Symbol == item.ProductSymbol);
                        }

                        if (asortyment != null)
                        {
                            var pozycja = faktura.Pozycje.Dodaj(asortyment, item.Quantity, asortyment.JednostkaSprzedazy);

                            if (item.PriceNet.HasValue && pozycja != null)
                            {
                                pozycja.Dane.CenaNetto = item.PriceNet.Value;
                            }
                        }
                    }
                }

                if (faktura.Zapisz())
                {
                    _logger.LogInformation("Created advance invoice {Number}",
                        faktura.Dane.NumerWewnetrzny?.PelnaSygnatura);

                    return CreatedAtAction(
                        nameof(GetDocument),
                        new { id = faktura.Dane.Id },
                        ApiResponse<DocumentDto>.Ok(MapSalesDocumentToDto(faktura.Dane), "Advance invoice created successfully"));
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
            var sfera = _sferaService.GetSfera();
            var dokumentySprzedazy = sfera.DokumentySprzedazy();

            using (var faktura = dokumentySprzedazy.UtworzFaktureVATMarza())
            {
                SetCustomerOnDocument(sfera, faktura.Dane, request.CustomerId, request.CustomerNIP);

                if (!string.IsNullOrEmpty(request.WarehouseSymbol))
                {
                    var magazyn = sfera.Magazyny().Dane.Pierwszy(m => m.Symbol == request.WarehouseSymbol);
                    if (magazyn != null)
                    {
                        faktura.Dane.Magazyn = magazyn;
                    }
                }

                if (request.IssueDate.HasValue)
                {
                    faktura.Dane.DataWystawienia = request.IssueDate.Value;
                }

                if (request.SaleDate.HasValue)
                {
                    faktura.Dane.DataSprzedazy = request.SaleDate.Value;
                }

                if (!string.IsNullOrEmpty(request.Notes))
                {
                    faktura.Dane.Uwagi = request.Notes;
                }

                // Add items
                var asortymenty = sfera.Asortymenty();
                foreach (var item in request.Items)
                {
                    Asortyment? asortyment = null;

                    if (item.ProductId.HasValue)
                    {
                        asortyment = asortymenty.Dane.Wszystkie()
                            .FirstOrDefault(a => a.Id == item.ProductId.Value);
                    }
                    else if (!string.IsNullOrEmpty(item.ProductSymbol))
                    {
                        asortyment = asortymenty.Dane.Wszystkie()
                            .FirstOrDefault(a => a.Symbol == item.ProductSymbol);
                    }

                    if (asortyment != null)
                    {
                        var pozycja = faktura.Pozycje.Dodaj(asortyment, item.Quantity, asortyment.JednostkaSprzedazy);

                        if (item.PriceNet.HasValue && pozycja != null)
                        {
                            pozycja.Dane.CenaNetto = item.PriceNet.Value;
                        }

                        if (item.DiscountPercent.HasValue && pozycja != null)
                        {
                            pozycja.Dane.RabatProcent = item.DiscountPercent.Value;
                        }
                    }
                }

                if (faktura.Zapisz())
                {
                    _logger.LogInformation("Created VAT margin invoice {Number}",
                        faktura.Dane.NumerWewnetrzny?.PelnaSygnatura);

                    return CreatedAtAction(
                        nameof(GetDocument),
                        new { id = faktura.Dane.Id },
                        ApiResponse<DocumentDto>.Ok(MapSalesDocumentToDto(faktura.Dane), "VAT margin invoice created successfully"));
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

    private void SetCustomerOnDocument(Uchwyt sfera, dynamic dokumentDane, int? customerId, string? customerNIP)
    {
        if (customerId.HasValue)
        {
            var podmiot = sfera.Podmioty().Dane.Wszystkie()
                .FirstOrDefault(p => p.Id == customerId.Value);
            if (podmiot != null)
            {
                dokumentDane.Podmiot = podmiot;
            }
        }
        else if (!string.IsNullOrEmpty(customerNIP))
        {
            var podmiot = sfera.Podmioty().Dane.Pierwszy(p => p.NIP == customerNIP);
            if (podmiot != null)
            {
                dokumentDane.Podmiot = podmiot;
            }
        }
    }

    private static CorrectionDto MapCorrectionToDto(DokumentKDS dokument)
    {
        return new CorrectionDto
        {
            Id = dokument.Id,
            Number = dokument.NumerWewnetrzny?.Numer.ToString() ?? "",
            FullNumber = dokument.NumerWewnetrzny?.PelnaSygnatura,
            Type = CorrectionType.SalesInvoiceCorrection,
            IssueDate = dokument.DataWystawienia,
            OriginalDocumentId = dokument.DokumentKorygowany?.Id,
            OriginalDocumentNumber = dokument.DokumentKorygowany?.NumerWewnetrzny?.PelnaSygnatura,
            CustomerName = dokument.Podmiot?.NazwaSkrocona,
            CustomerNIP = dokument.Podmiot?.NIP,
            WarehouseSymbol = dokument.Magazyn?.Symbol,
            CorrectionNet = dokument.WartoscNetto,
            CorrectionVat = dokument.WartoscVat,
            CorrectionGross = dokument.WartoscBrutto,
            CorrectionReason = dokument.PrzyczynaKorekty,
            Notes = dokument.Uwagi,
            CreatedAt = dokument.DataUtworzenia
        };
    }

    private static CorrectionDto MapPurchaseCorrectionToDto(DokumentKDZ dokument)
    {
        return new CorrectionDto
        {
            Id = dokument.Id,
            Number = dokument.NumerWewnetrzny?.Numer.ToString() ?? "",
            FullNumber = dokument.NumerWewnetrzny?.PelnaSygnatura,
            Type = CorrectionType.PurchaseInvoiceCorrection,
            IssueDate = dokument.DataWystawienia,
            OriginalDocumentId = dokument.DokumentKorygowany?.Id,
            OriginalDocumentNumber = dokument.DokumentKorygowany?.NumerWewnetrzny?.PelnaSygnatura,
            CustomerName = dokument.Podmiot?.NazwaSkrocona,
            CustomerNIP = dokument.Podmiot?.NIP,
            WarehouseSymbol = dokument.Magazyn?.Symbol,
            CorrectionNet = dokument.WartoscNetto,
            CorrectionVat = dokument.WartoscVat,
            CorrectionGross = dokument.WartoscBrutto,
            CorrectionReason = dokument.PrzyczynaKorekty,
            Notes = dokument.Uwagi,
            CreatedAt = dokument.DataUtworzenia
        };
    }

    private static DocumentDto MapDocumentToDto(Dokument dokument)
    {
        return new DocumentDto
        {
            Id = dokument.Id,
            Number = dokument.NumerWewnetrzny?.Numer.ToString() ?? "",
            FullNumber = dokument.NumerWewnetrzny?.PelnaSygnatura,
            IssueDate = dokument.DataWystawienia,
            CustomerName = dokument.Podmiot?.NazwaSkrocona,
            CustomerNIP = dokument.Podmiot?.NIP,
            WarehouseSymbol = dokument.Magazyn?.Symbol,
            TotalNet = dokument.WartoscNetto,
            TotalVat = dokument.WartoscVat,
            TotalGross = dokument.WartoscBrutto,
            Notes = dokument.Uwagi,
            CreatedAt = dokument.DataUtworzenia
        };
    }

    private static DocumentDto MapSalesDocumentToDto(DokumentDS dokument)
    {
        var dto = new DocumentDto
        {
            Id = dokument.Id,
            Number = dokument.NumerWewnetrzny?.Numer.ToString() ?? "",
            FullNumber = dokument.NumerWewnetrzny?.PelnaSygnatura,
            Type = DocumentType.SalesInvoice,
            IssueDate = dokument.DataWystawienia,
            SaleDate = dokument.DataSprzedazy,
            CustomerName = dokument.Podmiot?.NazwaSkrocona,
            CustomerNIP = dokument.Podmiot?.NIP,
            WarehouseSymbol = dokument.Magazyn?.Symbol,
            TotalNet = dokument.WartoscNetto,
            TotalVat = dokument.WartoscVat,
            TotalGross = dokument.WartoscBrutto,
            Notes = dokument.Uwagi,
            Items = new List<DocumentItemDto>()
        };

        if (dokument.Pozycje != null)
        {
            int lineNum = 1;
            foreach (var poz in dokument.Pozycje)
            {
                dto.Items.Add(new DocumentItemDto
                {
                    Id = poz.Id,
                    LineNumber = lineNum++,
                    ProductId = poz.Asortyment?.Id,
                    ProductSymbol = poz.Asortyment?.Symbol,
                    Name = poz.Nazwa,
                    Quantity = poz.Ilosc,
                    Unit = poz.Jednostka?.Symbol ?? "szt.",
                    PriceNet = poz.CenaNetto,
                    PriceGross = poz.CenaBrutto,
                    ValueNet = poz.WartoscNetto,
                    ValueVat = poz.WartoscVat,
                    ValueGross = poz.WartoscBrutto
                });
            }
        }

        return dto;
    }

    private static DocumentDto MapPurchaseDocumentToDto(DokumentDZ dokument)
    {
        var dto = new DocumentDto
        {
            Id = dokument.Id,
            Number = dokument.NumerWewnetrzny?.Numer.ToString() ?? "",
            FullNumber = dokument.NumerWewnetrzny?.PelnaSygnatura,
            Type = DocumentType.PurchaseInvoice,
            IssueDate = dokument.DataWystawienia,
            CustomerName = dokument.Podmiot?.NazwaSkrocona,
            CustomerNIP = dokument.Podmiot?.NIP,
            WarehouseSymbol = dokument.Magazyn?.Symbol,
            TotalNet = dokument.WartoscNetto,
            TotalVat = dokument.WartoscVat,
            TotalGross = dokument.WartoscBrutto,
            Notes = dokument.Uwagi,
            Items = new List<DocumentItemDto>()
        };

        return dto;
    }

    private static DocumentDto MapOrderToDto(DokumentZK dokument)
    {
        var dto = new DocumentDto
        {
            Id = dokument.Id,
            Number = dokument.NumerWewnetrzny?.Numer.ToString() ?? "",
            FullNumber = dokument.NumerWewnetrzny?.PelnaSygnatura,
            Type = DocumentType.CustomerOrder,
            IssueDate = dokument.DataWystawienia,
            CustomerName = dokument.Podmiot?.NazwaSkrocona,
            CustomerNIP = dokument.Podmiot?.NIP,
            WarehouseSymbol = dokument.Magazyn?.Symbol,
            TotalNet = dokument.WartoscNetto,
            TotalVat = dokument.WartoscVat,
            TotalGross = dokument.WartoscBrutto,
            Notes = dokument.Uwagi,
            Items = new List<DocumentItemDto>()
        };

        if (dokument.Pozycje != null)
        {
            int lineNum = 1;
            foreach (var poz in dokument.Pozycje)
            {
                dto.Items.Add(new DocumentItemDto
                {
                    Id = poz.Id,
                    LineNumber = lineNum++,
                    ProductId = poz.Asortyment?.Id,
                    ProductSymbol = poz.Asortyment?.Symbol,
                    Name = poz.Nazwa,
                    Quantity = poz.Ilosc,
                    Unit = poz.Jednostka?.Symbol ?? "szt.",
                    PriceNet = poz.CenaNetto,
                    PriceGross = poz.CenaBrutto,
                    ValueNet = poz.WartoscNetto,
                    ValueVat = poz.WartoscVat,
                    ValueGross = poz.WartoscBrutto
                });
            }
        }

        return dto;
    }

    private static List<string> GetBusinessObjectErrors(InsERT.Mox.ObiektyBiznesowe.IObiektBiznesowy obiekt)
    {
        var errors = new List<string>();
        foreach (var encjaZBledami in obiekt.InvalidData)
        {
            foreach (var blad in encjaZBledami.Errors)
            {
                errors.Add(blad.ToString());
            }
            foreach (var bladNaPolach in encjaZBledami.MemberErrors)
            {
                errors.Add($"{bladNaPolach.Key}: {string.Join(", ", bladNaPolach)}");
            }
        }
        return errors;
    }
}
