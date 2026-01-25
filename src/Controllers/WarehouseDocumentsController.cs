using System.Linq;
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
    private readonly StockValidationHelper _stockHelper;

    public WarehouseDocumentsController(
        ISferaService sferaService,
        ILogger<WarehouseDocumentsController> logger,
        StockValidationHelper stockHelper)
    {
        _sferaService = sferaService;
        _logger = logger;
        _stockHelper = stockHelper;
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
    /// <remarks>
    /// IMPORTANT: This endpoint requires WindowsFormsSynchronizationContext on the SDK thread.
    /// The document creation pattern is:
    /// 1. Set warehouse on Dokument.Magazyn
    /// 2. Call ZarezerwujNumer() to reserve document number
    /// 3. Add items using Pozycje.Dodaj(towarId)
    /// 4. Call Zapisz() to save
    /// </remarks>
    [HttpPost("wz")]
    public async Task<ActionResult<ApiResponse<WarehouseDocumentDto>>> CreateWZ([FromBody] CreateWarehouseDocumentRequest request)
    {
        try
        {
            // Validate stock availability for outgoing document
            if (request.Items != null && request.Items.Any() && !string.IsNullOrEmpty(request.WarehouseSymbol))
            {
                var stockValidation = _stockHelper.ValidateStock(
                    request.Items,
                    request.WarehouseSymbol,
                    item => item.ProductId,
                    item => item.ProductSymbol,
                    item => item.ProductEan,
                    item => item.Quantity);

                if (!stockValidation.AllItemsAvailable)
                {
                    _logger.LogWarning("WZ creation failed - insufficient stock: {Errors}", string.Join("; ", stockValidation.Errors));
                    return BadRequest(ApiResponse<WarehouseDocumentDto>.Error("Insufficient stock for WZ", stockValidation.Errors));
                }
            }

            // Use thread-safe execution - EF6 is NOT thread-safe
            var result = await _sferaService.ExecuteWithLockAsync<(bool Success, WarehouseDocumentDto? Data, string Message, List<string> Errors)>(() =>
            {
                var wydania = _sferaService.GetManager("WydaniaZewnetrzne");
                if (wydania == null)
                {
                    return (false, null, "Failed to get WydaniaZewnetrzne manager", new List<string>());
                }

                using (var wz = wydania.UtworzWydanieZewnetrzne())
                {
                    // Set contractor
                    SetContractor(wz.Dane, request.ContractorId, request.ContractorNIP);

                    // CRITICAL: Set warehouse on Dokument (not Dane!) - required for document numbering
                    var magazynyManager = _sferaService.GetManager("Magazyny");
                    if (magazynyManager != null && !string.IsNullOrEmpty(request.WarehouseSymbol))
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
                            // Set on Dokument for proper numbering (e.g., "WZ MG/2026/01/1")
                            wz.Dokument.Magazyn = magazyn;
                        }
                    }

                    // CRITICAL: Reserve number BEFORE adding items
                    wz.ZarezerwujNumer();
                    _logger.LogInformation("Reserved WZ number: {Number}", (string?)wz.PodajPodgladNumeru()?.ToString() ?? "");

                    if (request.IssueDate.HasValue)
                    {
                        wz.Dane.DataWystawienia = request.IssueDate.Value;
                    }

                    if (!string.IsNullOrEmpty(request.Notes))
                    {
                        wz.Dane.Uwagi = request.Notes;
                    }

                    // Add items using product ID
                    AddWarehouseDocumentItemsById(wz, request.Items);

                    if ((bool)wz.Zapisz())
                    {
                        string docNumber = wz.PodajPodgladNumeru()?.ToString() ?? "";
                        int docId = (int)wz.Dokument.Id;
                        _logger.LogInformation("Created WZ {Number}, Id={Id}", docNumber, docId);

                        return (true, MapWZToDto(wz.Dane), "WZ created successfully", new List<string>());
                    }
                    else
                    {
                        var errors = GetBusinessObjectErrors(wz);
                        return (false, null, "Failed to create WZ", errors);
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
            _logger.LogError(ex, "Error creating WZ");
            return StatusCode(500, ApiResponse<WarehouseDocumentDto>.Error("Error creating WZ", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create external receipt (PZ - Przyjęcie zewnętrzne)
    /// </summary>
    /// <remarks>
    /// IMPORTANT: This endpoint requires WindowsFormsSynchronizationContext on the SDK thread.
    /// The document creation pattern is:
    /// 1. Set warehouse on Dokument.Magazyn
    /// 2. Call ZarezerwujNumer() to reserve document number
    /// 3. Add items using Pozycje.Dodaj(towarId)
    /// 4. Call Zapisz() to save
    /// </remarks>
    [HttpPost("pz")]
    public async Task<ActionResult<ApiResponse<WarehouseDocumentDto>>> CreatePZ([FromBody] CreateWarehouseDocumentRequest request)
    {
        try
        {
            // Use thread-safe execution - EF6 is NOT thread-safe
            var result = await _sferaService.ExecuteWithLockAsync<(bool Success, WarehouseDocumentDto? Data, string Message, List<string> Errors)>(() =>
            {
                var przyjecia = _sferaService.GetManager("PrzyjeciaZewnetrzne");
                if (przyjecia == null)
                {
                    return (false, null, "Failed to get PrzyjeciaZewnetrzne manager", new List<string>());
                }

                using (var pz = przyjecia.UtworzPrzyjecieZewnetrzne())
                {
                    // Set contractor (supplier)
                    SetContractor(pz.Dane, request.ContractorId, request.ContractorNIP);

                    // CRITICAL: Set warehouse on Dokument (not Dane!) - required for document numbering
                    var magazynyManager = _sferaService.GetManager("Magazyny");
                    if (magazynyManager != null && !string.IsNullOrEmpty(request.WarehouseSymbol))
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
                            // Set on Dokument for proper numbering (e.g., "PZ MG/2026/01/1")
                            pz.Dokument.Magazyn = magazyn;
                        }
                    }

                    // CRITICAL: Reserve number BEFORE adding items
                    pz.ZarezerwujNumer();
                    _logger.LogInformation("Reserved PZ number: {Number}", (string?)pz.PodajPodgladNumeru()?.ToString() ?? "");

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

                    // Add items using product ID
                    AddWarehouseDocumentItemsById(pz, request.Items);

                    if ((bool)pz.Zapisz())
                    {
                        string docNumber = pz.PodajPodgladNumeru()?.ToString() ?? "";
                        int docId = (int)pz.Dokument.Id;
                        _logger.LogInformation("Created PZ {Number}, Id={Id}", docNumber, docId);

                        return (true, MapPZToDto(pz.Dane), "PZ created successfully", new List<string>());
                    }
                    else
                    {
                        var errors = GetBusinessObjectErrors(pz);
                        return (false, null, "Failed to create PZ", errors);
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
            _logger.LogError(ex, "Error creating PZ");
            return StatusCode(500, ApiResponse<WarehouseDocumentDto>.Error("Error creating PZ", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create internal consumption (RW - Rozchód wewnętrzny)
    /// </summary>
    /// <remarks>
    /// IMPORTANT: This endpoint requires WindowsFormsSynchronizationContext on the SDK thread.
    /// The document creation pattern is:
    /// 1. Set warehouse on Dokument.Magazyn
    /// 2. Call ZarezerwujNumer() to reserve document number
    /// 3. Add items using Pozycje.Dodaj(towarId)
    /// 4. Call Zapisz() to save
    /// </remarks>
    [HttpPost("rw")]
    public async Task<ActionResult<ApiResponse<WarehouseDocumentDto>>> CreateRW([FromBody] CreateWarehouseDocumentRequest request)
    {
        try
        {
            // Validate stock availability for outgoing document
            if (request.Items != null && request.Items.Any() && !string.IsNullOrEmpty(request.WarehouseSymbol))
            {
                var stockValidation = _stockHelper.ValidateStock(
                    request.Items,
                    request.WarehouseSymbol,
                    item => item.ProductId,
                    item => item.ProductSymbol,
                    item => item.ProductEan,
                    item => item.Quantity);

                if (!stockValidation.AllItemsAvailable)
                {
                    _logger.LogWarning("RW creation failed - insufficient stock: {Errors}", string.Join("; ", stockValidation.Errors));
                    return BadRequest(ApiResponse<WarehouseDocumentDto>.Error("Insufficient stock for RW", stockValidation.Errors));
                }
            }

            // Use thread-safe execution - EF6 is NOT thread-safe
            var result = await _sferaService.ExecuteWithLockAsync<(bool Success, WarehouseDocumentDto? Data, string Message, List<string> Errors)>(() =>
            {
                var rozchody = _sferaService.GetManager("RozchodyWewnetrzne");
                if (rozchody == null)
                {
                    return (false, null, "Failed to get RozchodyWewnetrzne manager", new List<string>());
                }

                // Get default configuration
                var konfiguracje = _sferaService.GetManager("Konfiguracje");
                dynamic? konfig = konfiguracje?.DaneDomyslne?.RozchodWewnetrzny;

                using (var rw = konfig != null ? rozchody.Utworz(konfig) : rozchody.Utworz())
                {
                    // CRITICAL: Set warehouse on Dokument (not Dane!) - required for document numbering
                    var magazynyManager = _sferaService.GetManager("Magazyny");
                    if (magazynyManager != null && !string.IsNullOrEmpty(request.WarehouseSymbol))
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
                            // Set on Dokument for proper numbering (e.g., "RW MG/2026/01/1")
                            rw.Dokument.Magazyn = magazyn;
                        }
                    }

                    // CRITICAL: Reserve number BEFORE adding items
                    rw.ZarezerwujNumer();
                    _logger.LogInformation("Reserved RW number: {Number}", (string?)rw.PodajPodgladNumeru()?.ToString() ?? "");

                    // Set dates - RW may use different property names
                    if (request.IssueDate.HasValue)
                    {
                        _logger.LogInformation("Setting RW IssueDate to: {Date}", request.IssueDate.Value);

                        // Log available properties for debugging
                        var daneType = ((object)rw.Dane).GetType();
                        var dateProps = daneType.GetProperties()
                            .Where(p => p.Name.Contains("Data") || p.Name.Contains("Date"))
                            .Select(p => p.Name)
                            .ToList();
                        _logger.LogInformation("RW.Dane date properties: {Props}", string.Join(", ", dateProps));

                        bool dateSet = DynamicPropertyHelper.TrySetProperty(rw.Dane, "DataWystawienia", request.IssueDate.Value);
                        _logger.LogInformation("TrySetProperty DataWystawienia: {Result}", dateSet);

                        if (!dateSet)
                        {
                            dateSet = DynamicPropertyHelper.TrySetProperty(rw.Dane, "Data", request.IssueDate.Value);
                            _logger.LogInformation("TrySetProperty Data: {Result}", dateSet);
                        }
                    }

                    if (!string.IsNullOrEmpty(request.Notes))
                    {
                        bool notesSet = DynamicPropertyHelper.TrySetProperty(rw.Dane, "Uwagi", request.Notes);
                        _logger.LogInformation("TrySetProperty Uwagi: {Result}", notesSet);
                    }

                    // Add items using product ID
                    AddWarehouseDocumentItemsById(rw, request.Items);

                    if ((bool)rw.Zapisz())
                    {
                        string docNumber = rw.PodajPodgladNumeru()?.ToString() ?? "";
                        int docId = (int)rw.Dokument.Id;
                        _logger.LogInformation("Created RW {Number}, Id={Id}", docNumber, docId);

                        return (true, MapRWToDto(rw), "RW created successfully", new List<string>());
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
    /// <remarks>
    /// IMPORTANT: This endpoint requires WindowsFormsSynchronizationContext on the SDK thread.
    /// The document creation pattern is:
    /// 1. Set warehouse on Dokument.Magazyn
    /// 2. Call ZarezerwujNumer() to reserve document number
    /// 3. Add items using Pozycje.Dodaj(towarId)
    /// 4. Call Zapisz() to save
    /// </remarks>
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

                // Get default configuration
                var konfiguracje = _sferaService.GetManager("Konfiguracje");
                dynamic? konfig = konfiguracje?.DaneDomyslne?.PrzychodWewnetrzny;

                using (var pw = konfig != null ? przychody.Utworz(konfig) : przychody.Utworz())
                {
                    // CRITICAL: Set warehouse on Dokument (not Dane!) - required for document numbering
                    var magazynyManager = _sferaService.GetManager("Magazyny");
                    if (magazynyManager != null && !string.IsNullOrEmpty(request.WarehouseSymbol))
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
                            // Set on Dokument for proper numbering (e.g., "PW MG/2026/01/1")
                            pw.Dokument.Magazyn = magazyn;
                        }
                    }

                    // CRITICAL: Reserve number BEFORE adding items
                    pw.ZarezerwujNumer();
                    _logger.LogInformation("Reserved PW number: {Number}", (string?)pw.PodajPodgladNumeru()?.ToString() ?? "");

                    // Set dates - PW may use different property names
                    if (request.IssueDate.HasValue)
                    {
                        _logger.LogInformation("Setting PW IssueDate to: {Date}", request.IssueDate.Value);

                        // Log available properties for debugging
                        var daneType = ((object)pw.Dane).GetType();
                        var dateProps = daneType.GetProperties()
                            .Where(p => p.Name.Contains("Data") || p.Name.Contains("Date"))
                            .Select(p => p.Name)
                            .ToList();
                        _logger.LogInformation("PW.Dane date properties: {Props}", string.Join(", ", dateProps));

                        bool dateSet = DynamicPropertyHelper.TrySetProperty(pw.Dane, "DataWystawienia", request.IssueDate.Value);
                        _logger.LogInformation("TrySetProperty DataWystawienia: {Result}", dateSet);

                        if (!dateSet)
                        {
                            dateSet = DynamicPropertyHelper.TrySetProperty(pw.Dane, "Data", request.IssueDate.Value);
                            _logger.LogInformation("TrySetProperty Data: {Result}", dateSet);
                        }
                    }

                    if (!string.IsNullOrEmpty(request.Notes))
                    {
                        bool notesSet = DynamicPropertyHelper.TrySetProperty(pw.Dane, "Uwagi", request.Notes);
                        _logger.LogInformation("TrySetProperty Uwagi: {Result}", notesSet);
                    }

                    // Add items using product ID
                    AddWarehouseDocumentItemsById(pw, request.Items);

                    if ((bool)pw.Zapisz())
                    {
                        string docNumber = pw.PodajPodgladNumeru()?.ToString() ?? "";
                        int docId = (int)pw.Dokument.Id;
                        _logger.LogInformation("Created PW {Number}, Id={Id}", docNumber, docId);

                        return (true, MapPWToDto(pw), "PW created successfully", new List<string>());
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
    /// <remarks>
    /// IMPORTANT: This endpoint requires WindowsFormsSynchronizationContext on the SDK thread.
    /// The document creation pattern is:
    /// 1. Set source warehouse on Dokument.Magazyn
    /// 2. Set target warehouse on Dokument.MagazynDocelowy
    /// 3. Call ZarezerwujNumer() to reserve document number
    /// 4. Add items using Pozycje.Dodaj(towarId)
    /// 5. Call Zapisz() to save
    /// </remarks>
    [HttpPost("mm")]
    public async Task<ActionResult<ApiResponse<WarehouseDocumentDto>>> CreateMM([FromBody] CreateWarehouseDocumentRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.TargetWarehouseSymbol))
            {
                return BadRequest(ApiResponse<WarehouseDocumentDto>.Error("Target warehouse symbol is required for MM"));
            }

            // Validate stock availability in SOURCE warehouse for outgoing transfer
            if (request.Items != null && request.Items.Any() && !string.IsNullOrEmpty(request.WarehouseSymbol))
            {
                var stockValidation = _stockHelper.ValidateStock(
                    request.Items,
                    request.WarehouseSymbol, // Source warehouse
                    item => item.ProductId,
                    item => item.ProductSymbol,
                    item => item.ProductEan,
                    item => item.Quantity);

                if (!stockValidation.AllItemsAvailable)
                {
                    _logger.LogWarning("MM creation failed - insufficient stock in source warehouse: {Errors}", string.Join("; ", stockValidation.Errors));
                    return BadRequest(ApiResponse<WarehouseDocumentDto>.Error($"Insufficient stock in source warehouse '{request.WarehouseSymbol}' for MM", stockValidation.Errors));
                }
            }

            // Use thread-safe execution - EF6 is NOT thread-safe
            var result = await _sferaService.ExecuteWithLockAsync<(bool Success, WarehouseDocumentDto? Data, string Message, List<string> Errors)>(() =>
            {
                var wydania = _sferaService.GetManager("WydaniaMiedzymagazynowe");
                var konfiguracje = _sferaService.GetManager("Konfiguracje");
                if (wydania == null || konfiguracje == null)
                {
                    return (false, null, "Failed to get required managers", new List<string>());
                }

                var konfiguracja = konfiguracje.DaneDomyslne.PrzesuniecieMiedzymagazynowe;

                using (var mm = wydania.Utworz(konfiguracja))
                {
                    // CRITICAL: Set warehouses on Dokument (not Dane!) - required for document numbering
                    var magazynyManager = _sferaService.GetManager("Magazyny");
                    if (magazynyManager != null)
                    {
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
                            // Set on Dokument for proper numbering
                            mm.Dokument.Magazyn = magazynZrodlowy;
                        }
                        if (magazynDocelowy != null)
                        {
                            mm.Dokument.MagazynDocelowy = magazynDocelowy;
                        }
                    }

                    // CRITICAL: Reserve number BEFORE adding items
                    mm.ZarezerwujNumer();
                    _logger.LogInformation("Reserved MM number: {Number}", (string?)mm.PodajPodgladNumeru()?.ToString() ?? "");

                    // Set dates - MM may use different property names
                    if (request.IssueDate.HasValue)
                    {
                        if (!DynamicPropertyHelper.TrySetProperty(mm.Dane, "DataWystawienia", request.IssueDate.Value))
                        {
                            DynamicPropertyHelper.TrySetProperty(mm.Dane, "Data", request.IssueDate.Value);
                        }
                    }

                    if (!string.IsNullOrEmpty(request.Notes))
                    {
                        DynamicPropertyHelper.TrySetProperty(mm.Dane, "Uwagi", request.Notes);
                    }

                    // Add items using product ID
                    AddWarehouseDocumentItemsById(mm, request.Items);

                    if ((bool)mm.Zapisz())
                    {
                        string docNumber = mm.PodajPodgladNumeru()?.ToString() ?? "";
                        int docId = (int)mm.Dokument.Id;
                        _logger.LogInformation("Created MM {Number}, Id={Id}", docNumber, docId);

                        return (true, MapMMToDto(mm.Dane), "MM created successfully", new List<string>());
                    }
                    else
                    {
                        var errors = GetBusinessObjectErrors(mm);
                        return (false, null, "Failed to create MM", errors);
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
                    // Try various property names used by different document types
                    if (!DynamicPropertyHelper.TrySetProperty(pozycja, "Cena", item.PriceNet.Value))
                    {
                        if (!DynamicPropertyHelper.TrySetProperty(pozycja, "CenaJednostkowa", item.PriceNet.Value))
                        {
                            DynamicPropertyHelper.TrySetProperty(pozycja, "WartoscJednostkowa", item.PriceNet.Value);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Add items to warehouse document using product ID (required for EF6 compatibility)
    /// CRITICAL: This pattern works correctly with WindowsFormsSynchronizationContext
    /// </summary>
    private void AddWarehouseDocumentItemsById(dynamic dokument, List<CreateWarehouseDocumentItemRequest> items)
    {
        var asortymentyManager = _sferaService.GetManager("Asortymenty");
        if (asortymentyManager == null)
            return;

        foreach (var item in items)
        {
            var asortyment = FindAsortyment(asortymentyManager, item.ProductId, item.ProductSymbol, item.ProductEan);

            if (asortyment != null)
            {
                int towarId = DynamicPropertyHelper.GetId(asortyment);
                // CRITICAL: Use Pozycje.Dodaj(towarId) pattern for EF6 compatibility
                var pozycja = dokument.Pozycje.Dodaj(towarId);

                if (pozycja != null)
                {
                    pozycja.Ilosc = item.Quantity;

                    // Try to set price - warehouse documents may use different property names
                    if (item.PriceNet.HasValue)
                    {
                        // Log available price properties for debugging
                        var pozType = ((object)pozycja).GetType();
                        var priceProps = pozType.GetProperties()
                            .Where(p => p.Name.Contains("Cena") || p.Name.Contains("Wartosc") || p.Name.Contains("Price"))
                            .Select(p => $"{p.Name}({p.PropertyType.Name})")
                            .ToList();
                        _logger.LogInformation("Position price properties: {Props}", string.Join(", ", priceProps));

                        // Try various property names used by different document types
                        bool priceSet = DynamicPropertyHelper.TrySetProperty(pozycja, "Cena", item.PriceNet.Value);
                        _logger.LogInformation("TrySetProperty Cena={Price}: {Result}", item.PriceNet.Value, priceSet);

                        if (!priceSet)
                        {
                            priceSet = DynamicPropertyHelper.TrySetProperty(pozycja, "CenaJednostkowa", item.PriceNet.Value);
                            _logger.LogInformation("TrySetProperty CenaJednostkowa: {Result}", priceSet);

                            if (!priceSet)
                            {
                                priceSet = DynamicPropertyHelper.TrySetProperty(pozycja, "WartoscJednostkowa", item.PriceNet.Value);
                                _logger.LogInformation("TrySetProperty WartoscJednostkowa: {Result}", priceSet);
                            }
                        }
                    }
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

    /// <summary>
    /// Map RW business object to DTO
    /// Works with both rw.Dane and rw.Dokument (uses dynamic to access properties)
    /// </summary>
    private static WarehouseDocumentDto MapRWToDto(dynamic rw)
    {
        // RW uses Dokument for document data and Dane for warehouse-related data
        var dokument = DynamicPropertyHelper.GetProperty(rw, "Dokument");
        var dane = DynamicPropertyHelper.GetProperty(rw, "Dane");
        var magazyn = dokument != null ? DynamicPropertyHelper.GetProperty(dokument, "Magazyn") : null;

        string fullNumber = "";
        try
        {
            fullNumber = rw.PodajPodgladNumeru()?.ToString() ?? "";
        }
        catch
        {
            // Fallback if PodajPodgladNumeru() is not available
        }

        var dto = new WarehouseDocumentDto
        {
            Id = dokument != null ? DynamicPropertyHelper.GetId(dokument) : 0,
            Number = fullNumber,
            FullNumber = fullNumber,
            Type = WarehouseDocumentType.RW,
            IssueDate = dane != null ? DynamicPropertyHelper.GetDateTime(dane, "DataWystawienia") : DateTime.Now,
            WarehouseSymbol = magazyn != null ? DynamicPropertyHelper.GetString(magazyn, "Symbol") : null,
            WarehouseName = magazyn != null ? DynamicPropertyHelper.GetString(magazyn, "Nazwa") : null,
            Notes = dane != null ? DynamicPropertyHelper.GetString(dane, "Uwagi") : null,
            CreatedAt = DateTime.Now,
            Items = new List<WarehouseDocumentItemDto>()
        };

        try
        {
            var pozycje = rw.Pozycje;
            int lineNum = 1;
            foreach (var poz in pozycje)
            {
                dto.TotalQuantity += (decimal)(poz.Ilosc ?? 0);
                dto.Items.Add(new WarehouseDocumentItemDto
                {
                    LineNumber = lineNum++,
                    Name = poz.Nazwa?.ToString(),
                    Quantity = (decimal)(poz.Ilosc ?? 0),
                    Unit = "szt."
                });
            }
        }
        catch
        {
            // Ignore errors when reading positions
        }

        return dto;
    }

    /// <summary>
    /// Map PW business object to DTO
    /// </summary>
    private static WarehouseDocumentDto MapPWToDto(dynamic pw)
    {
        var dokument = DynamicPropertyHelper.GetProperty(pw, "Dokument");
        var dane = DynamicPropertyHelper.GetProperty(pw, "Dane");
        var magazyn = dokument != null ? DynamicPropertyHelper.GetProperty(dokument, "Magazyn") : null;

        string fullNumber = "";
        try
        {
            fullNumber = pw.PodajPodgladNumeru()?.ToString() ?? "";
        }
        catch { }

        var dto = new WarehouseDocumentDto
        {
            Id = dokument != null ? DynamicPropertyHelper.GetId(dokument) : 0,
            Number = fullNumber,
            FullNumber = fullNumber,
            Type = WarehouseDocumentType.PW,
            IssueDate = dane != null ? DynamicPropertyHelper.GetDateTime(dane, "DataWystawienia") : DateTime.Now,
            WarehouseSymbol = magazyn != null ? DynamicPropertyHelper.GetString(magazyn, "Symbol") : null,
            WarehouseName = magazyn != null ? DynamicPropertyHelper.GetString(magazyn, "Nazwa") : null,
            Notes = dane != null ? DynamicPropertyHelper.GetString(dane, "Uwagi") : null,
            CreatedAt = DateTime.Now,
            Items = new List<WarehouseDocumentItemDto>()
        };

        try
        {
            var pozycje = pw.Pozycje;
            int lineNum = 1;
            foreach (var poz in pozycje)
            {
                dto.TotalQuantity += (decimal)(poz.Ilosc ?? 0);
                dto.Items.Add(new WarehouseDocumentItemDto
                {
                    LineNumber = lineNum++,
                    Name = poz.Nazwa?.ToString(),
                    Quantity = (decimal)(poz.Ilosc ?? 0),
                    Unit = "szt."
                });
            }
        }
        catch { }

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
