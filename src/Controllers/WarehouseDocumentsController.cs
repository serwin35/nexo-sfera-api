using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Requests;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Warehouse documents (WZ, PZ, RW, PW, MM) management endpoints
/// </summary>
[ApiController]
[Route("api/warehouse-documents")]
[Authorize]
[Tags("Warehouse Documents")]
public class WarehouseDocumentsController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<WarehouseDocumentsController> _logger;

    public WarehouseDocumentsController(ISferaService sferaService, ILogger<WarehouseDocumentsController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get warehouse documents with filtering
    /// </summary>
    [HttpGet]
    public ActionResult<PagedResponse<WarehouseDocumentDto>> GetWarehouseDocuments([FromQuery] WarehouseDocumentQueryRequest query)
    {
        try
        {
            var documents = new List<WarehouseDocumentDto>();
            var totalCount = 0;

            // Query WZ documents
            if (!query.Type.HasValue || query.Type == WarehouseDocumentType.WZ)
            {
                var wzManager = _sferaService.GetManager("WydaniaZewnetrzne");
                if (wzManager != null)
                {
                    var allWz = new List<dynamic>();
                    foreach (var d in wzManager.Dane.Wszystkie())
                    {
                        bool include = true;

                        if (!string.IsNullOrEmpty(query.WarehouseSymbol))
                        {
                            var magazyn = DynamicPropertyHelper.GetProperty(d, "Magazyn");
                            if (magazyn == null || DynamicPropertyHelper.GetString(magazyn, "Symbol") != query.WarehouseSymbol)
                                include = false;
                        }

                        if (include && query.DateFrom.HasValue)
                        {
                            if (DynamicPropertyHelper.GetDateTime(d, "DataWystawienia") < query.DateFrom.Value)
                                include = false;
                        }

                        if (include && query.DateTo.HasValue)
                        {
                            if (DynamicPropertyHelper.GetDateTime(d, "DataWystawienia") > query.DateTo.Value)
                                include = false;
                        }

                        if (include)
                            allWz.Add(d);
                    }

                    totalCount += allWz.Count;
                    var wzDocs = allWz
                        .OrderByDescending(d => DynamicPropertyHelper.GetDateTime(d, "DataWystawienia"))
                        .Take(query.PageSize)
                        .ToList();
                    foreach (var d in wzDocs)
                    {
                        documents.Add(MapWZToDto(d));
                    }
                }
            }

            // Query PZ documents
            if (!query.Type.HasValue || query.Type == WarehouseDocumentType.PZ)
            {
                var pzManager = _sferaService.GetManager("PrzyjeciaZewnetrzne");
                if (pzManager != null)
                {
                    var allPz = new List<dynamic>();
                    foreach (var d in pzManager.Dane.Wszystkie())
                    {
                        bool include = true;

                        if (!string.IsNullOrEmpty(query.WarehouseSymbol))
                        {
                            var magazyn = DynamicPropertyHelper.GetProperty(d, "Magazyn");
                            if (magazyn == null || DynamicPropertyHelper.GetString(magazyn, "Symbol") != query.WarehouseSymbol)
                                include = false;
                        }

                        if (include && query.DateFrom.HasValue)
                        {
                            if (DynamicPropertyHelper.GetDateTime(d, "DataWystawienia") < query.DateFrom.Value)
                                include = false;
                        }

                        if (include && query.DateTo.HasValue)
                        {
                            if (DynamicPropertyHelper.GetDateTime(d, "DataWystawienia") > query.DateTo.Value)
                                include = false;
                        }

                        if (include)
                            allPz.Add(d);
                    }

                    totalCount += allPz.Count;
                    var pzDocs = allPz
                        .OrderByDescending(d => DynamicPropertyHelper.GetDateTime(d, "DataWystawienia"))
                        .Take(query.PageSize)
                        .ToList();
                    foreach (var d in pzDocs)
                    {
                        documents.Add(MapPZToDto(d));
                    }
                }
            }

            var response = new PagedResponse<WarehouseDocumentDto>
            {
                Data = documents.OrderByDescending(d => d.IssueDate).Take(query.PageSize).ToList(),
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = totalCount
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting warehouse documents");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving warehouse documents", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create external release (WZ - Wydanie zewnętrzne)
    /// </summary>
    [HttpPost("wz")]
    public ActionResult<ApiResponse<WarehouseDocumentDto>> CreateWZ([FromBody] CreateWarehouseDocumentRequest request)
    {
        try
        {
            var wydania = _sferaService.GetManager("WydaniaZewnetrzne");
            if (wydania == null)
            {
                return StatusCode(500, ApiResponse<WarehouseDocumentDto>.Error("Failed to get WydaniaZewnetrzne manager"));
            }

            // SDK pattern: use UtworzWydanieZewnetrzne() without configuration parameter
            using (var wz = wydania.UtworzWydanieZewnetrzne())
            {
                // Set contractor
                SetContractor(wz.Dane, request.ContractorId, request.ContractorNIP);

                // Set warehouse
                var magazynyManager = _sferaService.GetManager("Magazyny");
                if (magazynyManager != null)
                {
                    dynamic? magazyn = null;
                    foreach (var m in magazynyManager.Dane.Wszystkie())
                    {
                        if (DynamicPropertyHelper.GetString(m, "Symbol") == request.WarehouseSymbol)
                        {
                            magazyn = m;
                            break;
                        }
                    }
                    if (magazyn != null)
                    {
                        wz.Dane.Magazyn = magazyn;
                    }
                }

                if (request.IssueDate.HasValue)
                {
                    wz.Dane.DataWystawienia = request.IssueDate.Value;
                }

                if (!string.IsNullOrEmpty(request.Notes))
                {
                    wz.Dane.Uwagi = request.Notes;
                }

                // Add items
                AddWarehouseDocumentItems(wz, request.Items);

                if ((bool)wz.Zapisz())
                {
                    var numerWewnetrzny = DynamicPropertyHelper.GetProperty(wz.Dane, "NumerWewnetrzny");
                    string docNumber = numerWewnetrzny != null ? DynamicPropertyHelper.GetString(numerWewnetrzny, "PelnaSygnatura") : "";
                    _logger.LogInformation("Created WZ {Number}", docNumber);

                    return CreatedAtAction(
                        nameof(GetWarehouseDocuments),
                        ApiResponse<WarehouseDocumentDto>.Ok(MapWZToDto(wz.Dane), "WZ created successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(wz);
                    return BadRequest(ApiResponse<WarehouseDocumentDto>.Error("Failed to create WZ", errors));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating WZ");
            return StatusCode(500, ApiResponse<WarehouseDocumentDto>.Error("Error creating WZ", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create external receipt (PZ - Przyjęcie zewnętrzne)
    /// </summary>
    [HttpPost("pz")]
    public ActionResult<ApiResponse<WarehouseDocumentDto>> CreatePZ([FromBody] CreateWarehouseDocumentRequest request)
    {
        try
        {
            var przyjecia = _sferaService.GetManager("PrzyjeciaZewnetrzne");
            if (przyjecia == null)
            {
                return StatusCode(500, ApiResponse<WarehouseDocumentDto>.Error("Failed to get PrzyjeciaZewnetrzne manager"));
            }

            // SDK pattern: use UtworzPrzyjecieZewnetrzne() without configuration parameter
            using (var pz = przyjecia.UtworzPrzyjecieZewnetrzne())
            {
                // Set contractor (supplier)
                SetContractor(pz.Dane, request.ContractorId, request.ContractorNIP);

                // Set warehouse
                var magazynyManager = _sferaService.GetManager("Magazyny");
                if (magazynyManager != null)
                {
                    dynamic? magazyn = null;
                    foreach (var m in magazynyManager.Dane.Wszystkie())
                    {
                        if (DynamicPropertyHelper.GetString(m, "Symbol") == request.WarehouseSymbol)
                        {
                            magazyn = m;
                            break;
                        }
                    }
                    if (magazyn != null)
                    {
                        pz.Dane.Magazyn = magazyn;
                    }
                }

                if (request.IssueDate.HasValue)
                {
                    pz.Dane.DataWystawienia = request.IssueDate.Value;
                }

                if (!string.IsNullOrEmpty(request.RelatedDocumentNumber))
                {
                    pz.Dane.NumerObcy = request.RelatedDocumentNumber;
                }

                if (!string.IsNullOrEmpty(request.Notes))
                {
                    pz.Dane.Uwagi = request.Notes;
                }

                // Add items
                AddWarehouseDocumentItems(pz, request.Items);

                if ((bool)pz.Zapisz())
                {
                    var numerWewnetrzny = DynamicPropertyHelper.GetProperty(pz.Dane, "NumerWewnetrzny");
                    string docNumber = numerWewnetrzny != null ? DynamicPropertyHelper.GetString(numerWewnetrzny, "PelnaSygnatura") : "";
                    _logger.LogInformation("Created PZ {Number}", docNumber);

                    return CreatedAtAction(
                        nameof(GetWarehouseDocuments),
                        ApiResponse<WarehouseDocumentDto>.Ok(MapPZToDto(pz.Dane), "PZ created successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(pz);
                    return BadRequest(ApiResponse<WarehouseDocumentDto>.Error("Failed to create PZ", errors));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating PZ");
            return StatusCode(500, ApiResponse<WarehouseDocumentDto>.Error("Error creating PZ", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create internal consumption (RW - Rozchód wewnętrzny)
    /// </summary>
    [HttpPost("rw")]
    public async Task<ActionResult<ApiResponse<WarehouseDocumentDto>>> CreateRW([FromBody] CreateWarehouseDocumentRequest request)
    {
        try
        {
            // Use thread-safe execution - EF6 is NOT thread-safe
            var result = await _sferaService.ExecuteWithLockAsync<(bool Success, WarehouseDocumentDto? Data, string Message, List<string> Errors)>(() =>
            {
                var rozchody = _sferaService.GetManager("RozchodyWewnetrzne");
                if (rozchody == null)
                {
                    return (false, null, "Failed to get RozchodyWewnetrzne manager", new List<string>());
                }

                // Get default configuration - based on working example from GitHub
                // https://github.com/mariuszbyahoo/InsERTSubiektNexoAsortymenty
                var konfiguracje = _sferaService.GetManager("Konfiguracje");
                dynamic? konfig = konfiguracje?.DaneDomyslne?.RozchodWewnetrzny;

                // Use Utworz(konfig) with configuration like in working examples
                using (var rw = konfig != null ? rozchody.Utworz(konfig) : rozchody.Utworz())
                {
                    // Set warehouse
                    var magazynyManager = _sferaService.GetManager("Magazyny");
                    if (magazynyManager != null)
                    {
                        dynamic? magazyn = null;
                        foreach (var m in magazynyManager.Dane.Wszystkie())
                        {
                            if (DynamicPropertyHelper.GetString(m, "Symbol") == request.WarehouseSymbol)
                            {
                                magazyn = m;
                                break;
                            }
                        }
                        if (magazyn != null)
                        {
                            rw.Dane.Magazyn = magazyn;
                        }
                    }

                    if (request.IssueDate.HasValue)
                    {
                        rw.Dane.DataWystawienia = request.IssueDate.Value;
                    }

                    if (!string.IsNullOrEmpty(request.Notes))
                    {
                        rw.Dane.Uwagi = request.Notes;
                    }

                    // Add items
                    AddWarehouseDocumentItems(rw, request.Items);

                    if ((bool)rw.Zapisz())
                    {
                        var numerWewnetrzny = DynamicPropertyHelper.GetProperty(rw.Dane, "NumerWewnetrzny");
                        string docNumber = numerWewnetrzny != null ? DynamicPropertyHelper.GetString(numerWewnetrzny, "PelnaSygnatura") : "";
                        _logger.LogInformation("Created RW {Number}", docNumber);

                        return (true, MapWZToDto(rw.Dane), "RW created successfully", new List<string>());
                    }
                    else
                    {
                        var errors = GetBusinessObjectErrors(rw);
                        return (false, null, "Failed to create RW", errors);
                    }
                }
            });

            if (result.Success)
            {
                return CreatedAtAction(nameof(GetWarehouseDocuments), ApiResponse<WarehouseDocumentDto>.Ok(result.Data!, result.Message));
            }
            else if (result.Errors.Any())
            {
                return BadRequest(ApiResponse<WarehouseDocumentDto>.Error(result.Message, result.Errors));
            }
            else
            {
                return StatusCode(500, ApiResponse<WarehouseDocumentDto>.Error(result.Message));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating RW");
            return StatusCode(500, ApiResponse<WarehouseDocumentDto>.Error("Error creating RW", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create internal receipt (PW - Przychód wewnętrzny)
    /// </summary>
    [HttpPost("pw")]
    public async Task<ActionResult<ApiResponse<WarehouseDocumentDto>>> CreatePW([FromBody] CreateWarehouseDocumentRequest request)
    {
        try
        {
            // Use thread-safe execution - EF6 is NOT thread-safe
            var result = await _sferaService.ExecuteWithLockAsync<(bool Success, WarehouseDocumentDto? Data, string Message, List<string> Errors)>(() =>
            {
                var przychody = _sferaService.GetManager("PrzychodyWewnetrzne");
                if (przychody == null)
                {
                    return (false, null, "Failed to get PrzychodyWewnetrzne manager", new List<string>());
                }

                // Get default configuration - based on working example from GitHub
                var konfiguracje = _sferaService.GetManager("Konfiguracje");
                dynamic? konfig = konfiguracje?.DaneDomyslne?.PrzychodWewnetrzny;

                // Use Utworz(konfig) with configuration like in working examples
                using (var pw = konfig != null ? przychody.Utworz(konfig) : przychody.Utworz())
                {
                    // Set warehouse
                    var magazynyManager = _sferaService.GetManager("Magazyny");
                    if (magazynyManager != null)
                    {
                        dynamic? magazyn = null;
                        foreach (var m in magazynyManager.Dane.Wszystkie())
                        {
                            if (DynamicPropertyHelper.GetString(m, "Symbol") == request.WarehouseSymbol)
                            {
                                magazyn = m;
                                break;
                            }
                        }
                        if (magazyn != null)
                        {
                            pw.Dane.Magazyn = magazyn;
                        }
                    }

                    if (request.IssueDate.HasValue)
                    {
                        pw.Dane.DataWystawienia = request.IssueDate.Value;
                    }

                    if (!string.IsNullOrEmpty(request.Notes))
                    {
                        pw.Dane.Uwagi = request.Notes;
                    }

                    // Add items
                    AddWarehouseDocumentItems(pw, request.Items);

                    if ((bool)pw.Zapisz())
                    {
                        var numerWewnetrzny = DynamicPropertyHelper.GetProperty(pw.Dane, "NumerWewnetrzny");
                        string docNumber = numerWewnetrzny != null ? DynamicPropertyHelper.GetString(numerWewnetrzny, "PelnaSygnatura") : "";
                        _logger.LogInformation("Created PW {Number}", docNumber);

                        return (true, MapPZToDto(pw.Dane), "PW created successfully", new List<string>());
                    }
                    else
                    {
                        var errors = GetBusinessObjectErrors(pw);
                        return (false, null, "Failed to create PW", errors);
                    }
                }
            });

            if (result.Success)
            {
                return CreatedAtAction(nameof(GetWarehouseDocuments), ApiResponse<WarehouseDocumentDto>.Ok(result.Data!, result.Message));
            }
            else if (result.Errors.Any())
            {
                return BadRequest(ApiResponse<WarehouseDocumentDto>.Error(result.Message, result.Errors));
            }
            else
            {
                return StatusCode(500, ApiResponse<WarehouseDocumentDto>.Error(result.Message));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating PW");
            return StatusCode(500, ApiResponse<WarehouseDocumentDto>.Error("Error creating PW", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create inter-warehouse transfer (MM - Przesunięcie międzymagazynowe)
    /// </summary>
    [HttpPost("mm")]
    public ActionResult<ApiResponse<WarehouseDocumentDto>> CreateMM([FromBody] CreateWarehouseDocumentRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.TargetWarehouseSymbol))
            {
                return BadRequest(ApiResponse<WarehouseDocumentDto>.Error("Target warehouse symbol is required for MM"));
            }

            var wydania = _sferaService.GetManager("WydaniaMiedzymagazynowe");
            var konfiguracje = _sferaService.GetManager("Konfiguracje");
            if (wydania == null || konfiguracje == null)
            {
                return StatusCode(500, ApiResponse<WarehouseDocumentDto>.Error("Failed to get required managers"));
            }

            var konfiguracja = konfiguracje.DaneDomyslne.PrzesuniecieMiedzymagazynowe;

            using (var mm = wydania.Utworz(konfiguracja))
            {
                var magazynyManager = _sferaService.GetManager("Magazyny");
                if (magazynyManager != null)
                {
                    // Set source warehouse
                    dynamic? magazynZrodlowy = null;
                    dynamic? magazynDocelowy = null;
                    foreach (var m in magazynyManager.Dane.Wszystkie())
                    {
                        var symbol = DynamicPropertyHelper.GetString(m, "Symbol");
                        if (symbol == request.WarehouseSymbol)
                        {
                            magazynZrodlowy = m;
                        }
                        if (symbol == request.TargetWarehouseSymbol)
                        {
                            magazynDocelowy = m;
                        }
                        if (magazynZrodlowy != null && magazynDocelowy != null)
                            break;
                    }

                    if (magazynZrodlowy != null)
                    {
                        mm.Dane.Magazyn = magazynZrodlowy;
                    }
                    if (magazynDocelowy != null)
                    {
                        mm.Dane.MagazynDocelowy = magazynDocelowy;
                    }
                }

                if (request.IssueDate.HasValue)
                {
                    mm.Dane.DataWystawienia = request.IssueDate.Value;
                }

                if (!string.IsNullOrEmpty(request.Notes))
                {
                    mm.Dane.Uwagi = request.Notes;
                }

                // Add items
                var asortymentyManager = _sferaService.GetManager("Asortymenty");
                if (asortymentyManager != null)
                {
                    foreach (var item in request.Items)
                    {
                        var asortyment = FindAsortyment(asortymentyManager, item.ProductId, item.ProductSymbol, item.ProductEan);

                        if (asortyment != null)
                        {
                            var jednostka = DynamicPropertyHelper.GetProperty(asortyment, "JednostkaSprzedazy");
                            mm.Pozycje.Dodaj(asortyment, item.Quantity, jednostka);
                        }
                    }
                }

                if ((bool)mm.Zapisz())
                {
                    var numerWewnetrzny = DynamicPropertyHelper.GetProperty(mm.Dane, "NumerWewnetrzny");
                    string docNumber = numerWewnetrzny != null ? DynamicPropertyHelper.GetString(numerWewnetrzny, "PelnaSygnatura") : "";
                    _logger.LogInformation("Created MM {Number}", docNumber);

                    return CreatedAtAction(
                        nameof(GetWarehouseDocuments),
                        ApiResponse<WarehouseDocumentDto>.Ok(MapMMToDto(mm.Dane), "MM created successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(mm);
                    return BadRequest(ApiResponse<WarehouseDocumentDto>.Error("Failed to create MM", errors));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating MM");
            return StatusCode(500, ApiResponse<WarehouseDocumentDto>.Error("Error creating MM", new List<string> { ex.Message }));
        }
    }

    private void SetContractor(dynamic dokumentDane, int? contractorId, string? contractorNIP)
    {
        if (!contractorId.HasValue && string.IsNullOrEmpty(contractorNIP))
            return;

        var podmiotyManager = _sferaService.GetManager("Podmioty");
        if (podmiotyManager == null)
            return;

        dynamic? podmiot = null;
        if (contractorId.HasValue)
        {
            foreach (var p in podmiotyManager.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetId(p) == contractorId.Value)
                {
                    podmiot = p;
                    break;
                }
            }
        }
        else if (!string.IsNullOrEmpty(contractorNIP))
        {
            foreach (var p in podmiotyManager.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetString(p, "NIP") == contractorNIP)
                {
                    podmiot = p;
                    break;
                }
            }
        }

        if (podmiot != null)
        {
            dokumentDane.Podmiot = podmiot;
        }
    }

    private void AddWarehouseDocumentItems(dynamic dokument, List<CreateWarehouseDocumentItemRequest> items)
    {
        var asortymentyManager = _sferaService.GetManager("Asortymenty");
        if (asortymentyManager == null)
            return;

        foreach (var item in items)
        {
            var asortyment = FindAsortyment(asortymentyManager, item.ProductId, item.ProductSymbol, item.ProductEan);

            if (asortyment != null)
            {
                var jednostka = DynamicPropertyHelper.GetProperty(asortyment, "JednostkaSprzedazy");
                var pozycja = dokument.Pozycje.Dodaj(asortyment, item.Quantity, jednostka);

                if (item.PriceNet.HasValue && pozycja != null)
                {
                    pozycja.Dane.CenaNetto = item.PriceNet.Value;
                }
            }
        }
    }

    private static dynamic? FindAsortyment(dynamic asortymentyManager, int? productId, string? productSymbol, string? productEan)
    {
        foreach (var a in asortymentyManager.Dane.Wszystkie())
        {
            if (productId.HasValue)
            {
                if (DynamicPropertyHelper.GetId(a) == productId.Value)
                    return a;
            }
            else if (!string.IsNullOrEmpty(productSymbol))
            {
                if (DynamicPropertyHelper.GetString(a, "Symbol") == productSymbol)
                    return a;
            }
            else if (!string.IsNullOrEmpty(productEan))
            {
                if (DynamicPropertyHelper.GetString(a, "KodKreskowy") == productEan)
                    return a;
            }
        }
        return null;
    }

    private static WarehouseDocumentDto MapWZToDto(dynamic dokument)
    {
        var numerWewnetrzny = DynamicPropertyHelper.GetProperty(dokument, "NumerWewnetrzny");
        var podmiot = DynamicPropertyHelper.GetProperty(dokument, "Podmiot");
        var magazyn = DynamicPropertyHelper.GetProperty(dokument, "Magazyn");

        var dto = new WarehouseDocumentDto
        {
            Id = DynamicPropertyHelper.GetId(dokument),
            Number = numerWewnetrzny != null ? DynamicPropertyHelper.GetInt(numerWewnetrzny, "Numer").ToString() : "",
            FullNumber = numerWewnetrzny != null ? DynamicPropertyHelper.GetString(numerWewnetrzny, "PelnaSygnatura") : null,
            Type = WarehouseDocumentType.WZ,
            IssueDate = DynamicPropertyHelper.GetDateTime(dokument, "DataWystawienia"),
            ContractorName = podmiot != null ? DynamicPropertyHelper.GetString(podmiot, "NazwaSkrocona") : null,
            ContractorNIP = podmiot != null ? DynamicPropertyHelper.GetString(podmiot, "NIP") : null,
            WarehouseSymbol = magazyn != null ? DynamicPropertyHelper.GetString(magazyn, "Symbol") : null,
            WarehouseName = magazyn != null ? DynamicPropertyHelper.GetString(magazyn, "Nazwa") : null,
            TotalNet = DynamicPropertyHelper.GetDecimal(dokument, "WartoscNetto"),
            TotalGross = DynamicPropertyHelper.GetDecimal(dokument, "WartoscBrutto"),
            Notes = DynamicPropertyHelper.GetString(dokument, "Uwagi"),
            CreatedAt = DynamicPropertyHelper.GetDateTime(dokument, "DataUtworzenia"),
            Items = new List<WarehouseDocumentItemDto>()
        };

        var pozycje = DynamicPropertyHelper.GetCollection(dokument, "Pozycje");
        int lineNum = 1;
        foreach (var poz in pozycje)
        {
            var asortyment = DynamicPropertyHelper.GetProperty(poz, "Asortyment");
            var jednostka = DynamicPropertyHelper.GetProperty(poz, "Jednostka");

            dto.TotalQuantity += DynamicPropertyHelper.GetDecimal(poz, "Ilosc");
            dto.Items.Add(new WarehouseDocumentItemDto
            {
                Id = DynamicPropertyHelper.GetId(poz),
                LineNumber = lineNum++,
                ProductId = asortyment != null ? DynamicPropertyHelper.GetId(asortyment) : null,
                ProductSymbol = asortyment != null ? DynamicPropertyHelper.GetString(asortyment, "Symbol") : null,
                Name = DynamicPropertyHelper.GetString(poz, "Nazwa"),
                Quantity = DynamicPropertyHelper.GetDecimal(poz, "Ilosc"),
                Unit = jednostka != null ? DynamicPropertyHelper.GetString(jednostka, "Symbol") ?? "szt." : "szt.",
                PriceNet = DynamicPropertyHelper.GetDecimal(poz, "CenaNetto"),
                ValueNet = DynamicPropertyHelper.GetDecimal(poz, "WartoscNetto"),
                ValueGross = DynamicPropertyHelper.GetDecimal(poz, "WartoscBrutto")
            });
        }

        return dto;
    }

    private static WarehouseDocumentDto MapPZToDto(dynamic dokument)
    {
        var numerWewnetrzny = DynamicPropertyHelper.GetProperty(dokument, "NumerWewnetrzny");
        var podmiot = DynamicPropertyHelper.GetProperty(dokument, "Podmiot");
        var magazyn = DynamicPropertyHelper.GetProperty(dokument, "Magazyn");

        var dto = new WarehouseDocumentDto
        {
            Id = DynamicPropertyHelper.GetId(dokument),
            Number = numerWewnetrzny != null ? DynamicPropertyHelper.GetInt(numerWewnetrzny, "Numer").ToString() : "",
            FullNumber = numerWewnetrzny != null ? DynamicPropertyHelper.GetString(numerWewnetrzny, "PelnaSygnatura") : null,
            Type = WarehouseDocumentType.PZ,
            IssueDate = DynamicPropertyHelper.GetDateTime(dokument, "DataWystawienia"),
            ContractorName = podmiot != null ? DynamicPropertyHelper.GetString(podmiot, "NazwaSkrocona") : null,
            ContractorNIP = podmiot != null ? DynamicPropertyHelper.GetString(podmiot, "NIP") : null,
            WarehouseSymbol = magazyn != null ? DynamicPropertyHelper.GetString(magazyn, "Symbol") : null,
            WarehouseName = magazyn != null ? DynamicPropertyHelper.GetString(magazyn, "Nazwa") : null,
            RelatedDocumentNumber = DynamicPropertyHelper.GetString(dokument, "NumerObcy"),
            TotalNet = DynamicPropertyHelper.GetDecimal(dokument, "WartoscNetto"),
            TotalGross = DynamicPropertyHelper.GetDecimal(dokument, "WartoscBrutto"),
            Notes = DynamicPropertyHelper.GetString(dokument, "Uwagi"),
            CreatedAt = DynamicPropertyHelper.GetDateTime(dokument, "DataUtworzenia"),
            Items = new List<WarehouseDocumentItemDto>()
        };

        var pozycje = DynamicPropertyHelper.GetCollection(dokument, "Pozycje");
        int lineNum = 1;
        foreach (var poz in pozycje)
        {
            var asortyment = DynamicPropertyHelper.GetProperty(poz, "Asortyment");
            var jednostka = DynamicPropertyHelper.GetProperty(poz, "Jednostka");

            dto.TotalQuantity += DynamicPropertyHelper.GetDecimal(poz, "Ilosc");
            dto.Items.Add(new WarehouseDocumentItemDto
            {
                Id = DynamicPropertyHelper.GetId(poz),
                LineNumber = lineNum++,
                ProductId = asortyment != null ? DynamicPropertyHelper.GetId(asortyment) : null,
                ProductSymbol = asortyment != null ? DynamicPropertyHelper.GetString(asortyment, "Symbol") : null,
                Name = DynamicPropertyHelper.GetString(poz, "Nazwa"),
                Quantity = DynamicPropertyHelper.GetDecimal(poz, "Ilosc"),
                Unit = jednostka != null ? DynamicPropertyHelper.GetString(jednostka, "Symbol") ?? "szt." : "szt.",
                PriceNet = DynamicPropertyHelper.GetDecimal(poz, "CenaNetto"),
                ValueNet = DynamicPropertyHelper.GetDecimal(poz, "WartoscNetto"),
                ValueGross = DynamicPropertyHelper.GetDecimal(poz, "WartoscBrutto")
            });
        }

        return dto;
    }

    private static WarehouseDocumentDto MapMMToDto(dynamic dokument)
    {
        var numerWewnetrzny = DynamicPropertyHelper.GetProperty(dokument, "NumerWewnetrzny");
        var magazyn = DynamicPropertyHelper.GetProperty(dokument, "Magazyn");
        var magazynDocelowy = DynamicPropertyHelper.GetProperty(dokument, "MagazynDocelowy");

        var dto = new WarehouseDocumentDto
        {
            Id = DynamicPropertyHelper.GetId(dokument),
            Number = numerWewnetrzny != null ? DynamicPropertyHelper.GetInt(numerWewnetrzny, "Numer").ToString() : "",
            FullNumber = numerWewnetrzny != null ? DynamicPropertyHelper.GetString(numerWewnetrzny, "PelnaSygnatura") : null,
            Type = WarehouseDocumentType.MM,
            IssueDate = DynamicPropertyHelper.GetDateTime(dokument, "DataWystawienia"),
            WarehouseSymbol = magazyn != null ? DynamicPropertyHelper.GetString(magazyn, "Symbol") : null,
            WarehouseName = magazyn != null ? DynamicPropertyHelper.GetString(magazyn, "Nazwa") : null,
            TargetWarehouseSymbol = magazynDocelowy != null ? DynamicPropertyHelper.GetString(magazynDocelowy, "Symbol") : null,
            TargetWarehouseName = magazynDocelowy != null ? DynamicPropertyHelper.GetString(magazynDocelowy, "Nazwa") : null,
            Notes = DynamicPropertyHelper.GetString(dokument, "Uwagi"),
            CreatedAt = DynamicPropertyHelper.GetDateTime(dokument, "DataUtworzenia"),
            Items = new List<WarehouseDocumentItemDto>()
        };

        var pozycje = DynamicPropertyHelper.GetCollection(dokument, "Pozycje");
        int lineNum = 1;
        foreach (var poz in pozycje)
        {
            var asortyment = DynamicPropertyHelper.GetProperty(poz, "Asortyment");
            var jednostka = DynamicPropertyHelper.GetProperty(poz, "Jednostka");

            dto.TotalQuantity += DynamicPropertyHelper.GetDecimal(poz, "Ilosc");
            dto.Items.Add(new WarehouseDocumentItemDto
            {
                Id = DynamicPropertyHelper.GetId(poz),
                LineNumber = lineNum++,
                ProductId = asortyment != null ? DynamicPropertyHelper.GetId(asortyment) : null,
                ProductSymbol = asortyment != null ? DynamicPropertyHelper.GetString(asortyment, "Symbol") : null,
                Name = DynamicPropertyHelper.GetString(poz, "Nazwa"),
                Quantity = DynamicPropertyHelper.GetDecimal(poz, "Ilosc"),
                Unit = jednostka != null ? DynamicPropertyHelper.GetString(jednostka, "Symbol") ?? "szt." : "szt."
            });
        }

        return dto;
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
}
