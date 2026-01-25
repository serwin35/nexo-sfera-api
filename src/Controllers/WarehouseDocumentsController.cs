using System.Linq;
using System.Security.Claims;
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
    /// Gets the Nexo operator credentials from the current user's claims (set by API key authentication).
    /// Returns null if no per-key credentials are configured.
    /// </summary>
    private (string? Login, string? Password) GetOperatorCredentialsFromClaims()
    {
        var nexoLogin = User.FindFirst("NexoLogin")?.Value;
        var nexoPassword = User.FindFirst("NexoPassword")?.Value;
        return (nexoLogin, nexoPassword);
    }

    /// <summary>
    /// Switches to the operator specified in API key claims (if any).
    /// Must be called inside ExecuteWithLockAsync on the SDK STA thread.
    /// </summary>
    private bool SwitchToRequestOperator((string? Login, string? Password) credentials)
    {
        if (string.IsNullOrEmpty(credentials.Login))
        {
            // No per-key credentials, use default operator
            return true;
        }

        if (!_sferaService.SwitchOperatorIfNeeded(credentials.Login, credentials.Password))
        {
            _logger.LogError("Failed to switch to operator {Login} for this request", (object)credentials.Login);
            return false;
        }

        return true;
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
            // Get operator credentials from API key claims BEFORE entering SDK thread
            var operatorCredentials = GetOperatorCredentialsFromClaims();

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
                // Switch to the operator specified in API key (if any)
                if (!SwitchToRequestOperator(operatorCredentials))
                {
                    return (false, null, "Failed to switch to the operator associated with this API key", new List<string>());
                }

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

                    // CRITICAL: Set date BEFORE reserving number (number depends on date!)
                    if (request.IssueDate.HasValue)
                    {
                        _logger.LogInformation("Setting WZ IssueDate to: {Date}", request.IssueDate.Value);
                        wz.Dane.DataWydaniaWystawienia = request.IssueDate.Value;
                        wz.Dane.DataWprowadzenia = request.IssueDate.Value;
                    }

                    // Reserve number AFTER setting date (number format includes year/month)
                    wz.ZarezerwujNumer();
                    _logger.LogInformation("Reserved WZ number: {Number}", (string?)wz.PodajPodgladNumeru()?.ToString() ?? "");

                    if (!string.IsNullOrEmpty(request.Notes))
                    {
                        wz.Dane.Uwagi = request.Notes;
                    }

                    // CRITICAL: Validate ALL products exist before adding any items
                    if (request.Items != null && request.Items.Any())
                    {
                        var missingProducts = ValidateProductsExist(request.Items);
                        if (missingProducts.Any())
                        {
                            _logger.LogWarning("WZ creation failed - {Count} product(s) not found: {Products}",
                                (object)missingProducts.Count, (object)string.Join(", ", missingProducts));
                            var errorList = new List<string>
                            {
                                $"Cannot create document - the following products were not found: {string.Join(", ", missingProducts)}"
                            };
                            return (false, null, "Products not found - document not created", errorList);
                        }
                    }

                    // Add items using product ID (all validated to exist)
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
            // Get operator credentials from API key claims BEFORE entering SDK thread
            var operatorCredentials = GetOperatorCredentialsFromClaims();

            // Use thread-safe execution - EF6 is NOT thread-safe
            var result = await _sferaService.ExecuteWithLockAsync<(bool Success, WarehouseDocumentDto? Data, string Message, List<string> Errors)>(() =>
            {
                // Switch to the operator specified in API key (if any)
                if (!SwitchToRequestOperator(operatorCredentials))
                {
                    return (false, null, "Failed to switch to the operator associated with this API key", new List<string>());
                }

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

                    // CRITICAL: Set date BEFORE reserving number (number depends on date!)
                    if (request.IssueDate.HasValue)
                    {
                        _logger.LogInformation("Setting PZ IssueDate to: {Date}", request.IssueDate.Value);
                        pz.Dane.DataWydaniaWystawienia = request.IssueDate.Value;
                        pz.Dane.DataWprowadzenia = request.IssueDate.Value;
                    }

                    // Reserve number AFTER setting date (number format includes year/month)
                    pz.ZarezerwujNumer();
                    _logger.LogInformation("Reserved PZ number: {Number}", (string?)pz.PodajPodgladNumeru()?.ToString() ?? "");

                    if (!string.IsNullOrEmpty(request.RelatedDocumentNumber))
                    {
                        // NumerZewnetrzny - correct property name for external document number
                        try
                        {
                            pz.Dane.NumerZewnetrzny = request.RelatedDocumentNumber;
                            _logger.LogInformation("Set NumerZewnetrzny: {Value}", request.RelatedDocumentNumber);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("Could not set NumerZewnetrzny: {Error}", ex.Message);
                        }
                    }

                    if (!string.IsNullOrEmpty(request.Notes))
                    {
                        pz.Dane.Uwagi = request.Notes;
                    }

                    // CRITICAL: Validate ALL products exist before adding any items
                    if (request.Items != null && request.Items.Any())
                    {
                        var missingProducts = ValidateProductsExist(request.Items);
                        if (missingProducts.Any())
                        {
                            _logger.LogWarning("PZ creation failed - {Count} product(s) not found: {Products}",
                                (object)missingProducts.Count, (object)string.Join(", ", missingProducts));
                            var errorList = new List<string>
                            {
                                $"Cannot create document - the following products were not found: {string.Join(", ", missingProducts)}"
                            };
                            return (false, null, "Products not found - document not created", errorList);
                        }
                    }

                    // Add items using product ID (all validated to exist)
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
            // Get operator credentials from API key claims BEFORE entering SDK thread
            var operatorCredentials = GetOperatorCredentialsFromClaims();

            // Validate stock availability for outgoing document
            // NOTE: Stock validation only works for current-date documents.
            // For historical documents, the SDK validates during Zapisz() using historical stock levels.
            bool isHistoricalDocument = request.IssueDate.HasValue && request.IssueDate.Value.Date < DateTime.Today.AddDays(-7);

            if (!isHistoricalDocument && request.Items != null && request.Items.Any() && !string.IsNullOrEmpty(request.WarehouseSymbol))
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
            else if (isHistoricalDocument)
            {
                _logger.LogInformation("RW with historical date {Date} - skipping current stock validation, SDK will validate historical stock", request.IssueDate);
            }

            // Use thread-safe execution - EF6 is NOT thread-safe
            var result = await _sferaService.ExecuteWithLockAsync<(bool Success, WarehouseDocumentDto? Data, string Message, List<string> Errors)>(() =>
            {
                // Switch to the operator specified in API key (if any)
                if (!SwitchToRequestOperator(operatorCredentials))
                {
                    return (false, null, "Failed to switch to the operator associated with this API key", new List<string>());
                }

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

                    // CRITICAL: Set date BEFORE reserving number (number depends on date!)
                    if (request.IssueDate.HasValue)
                    {
                        _logger.LogInformation("Setting RW IssueDate to: {Date}", request.IssueDate.Value);

                        // Log available properties on both Dane and Dokument for debugging
                        var daneType = ((object)rw.Dane).GetType();
                        var daneProps = daneType.GetProperties()
                            .Where(p => p.Name.Contains("Data") || p.Name.Contains("Date"))
                            .Select(p => $"Dane.{p.Name}")
                            .ToList();

                        var dokType = ((object)rw.Dokument).GetType();
                        var dokProps = dokType.GetProperties()
                            .Where(p => p.Name.Contains("Data") || p.Name.Contains("Date"))
                            .Select(p => $"Dokument.{p.Name}")
                            .ToList();

                        _logger.LogInformation("RW date properties: {Props}", string.Join(", ", daneProps.Concat(dokProps)));

                        // Set both issue date AND entry date (for numbering)
                        // DataWydaniaWystawienia = display/issue date
                        // DataWprowadzenia = entry date (may affect numbering)
                        try
                        {
                            rw.Dane.DataWydaniaWystawienia = request.IssueDate.Value;
                            _logger.LogInformation("Direct assignment Dane.DataWydaniaWystawienia: Success");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogInformation("Direct assignment Dane.DataWydaniaWystawienia: Failed - {Error}", ex.Message);
                        }

                        // Also set DataWprowadzenia (entry date) - this may affect document numbering
                        try
                        {
                            rw.Dane.DataWprowadzenia = request.IssueDate.Value;
                            _logger.LogInformation("Direct assignment Dane.DataWprowadzenia: Success");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogInformation("Direct assignment Dane.DataWprowadzenia: Failed - {Error}", ex.Message);
                        }

                        // Try on Dokument as well
                        try
                        {
                            rw.Dokument.DataWydaniaWystawienia = request.IssueDate.Value;
                            _logger.LogInformation("Direct assignment Dokument.DataWydaniaWystawienia: Success");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogInformation("Direct assignment Dokument.DataWydaniaWystawienia: Failed - {Error}", ex.Message);
                        }
                    }

                    // Reserve number AFTER setting date (number format includes year/month)
                    rw.ZarezerwujNumer();
                    _logger.LogInformation("Reserved RW number: {Number}", (string?)rw.PodajPodgladNumeru()?.ToString() ?? "");

                    if (!string.IsNullOrEmpty(request.Notes))
                    {
                        bool notesSet = DynamicPropertyHelper.TrySetProperty(rw.Dane, "Uwagi", request.Notes);
                        _logger.LogInformation("TrySetProperty Uwagi: {Result}", notesSet);
                    }

                    // CRITICAL: Validate ALL products exist before adding any items
                    if (request.Items != null && request.Items.Any())
                    {
                        var missingProducts = ValidateProductsExist(request.Items);
                        if (missingProducts.Any())
                        {
                            _logger.LogWarning("RW creation failed - {Count} product(s) not found: {Products}",
                                (object)missingProducts.Count, (object)string.Join(", ", missingProducts));
                            var errorList = new List<string>
                            {
                                $"Cannot create document - the following products were not found: {string.Join(", ", missingProducts)}"
                            };
                            return (false, null, "Products not found - document not created", errorList);
                        }
                    }

                    // Add items using product ID (all validated to exist)
                    AddWarehouseDocumentItemsById(rw, request.Items);

                    _logger.LogInformation("RW: Calling Zapisz() with {Count} positions", request.Items?.Count ?? 0);

                    try
                    {
                        bool saveResult = (bool)rw.Zapisz();
                        _logger.LogInformation("RW Zapisz() returned: {Result}", saveResult);

                        if (saveResult)
                        {
                            string docNumber = rw.PodajPodgladNumeru()?.ToString() ?? "";
                            int docId = (int)rw.Dokument.Id;
                            _logger.LogInformation("Created RW {Number}, Id={Id}", docNumber, docId);

                            return (true, MapRWToDto(rw), "RW created successfully", new List<string>());
                        }
                        else
                        {
                            List<string> errors = GetBusinessObjectErrors(rw);
                            _logger.LogWarning("RW Zapisz() failed. InvalidData errors: {Errors}", (object)string.Join("; ", errors));

                            // Try to get more error details from different sources
                            try
                            {
                                var validationErrors = rw.WalidujDane();
                                if (validationErrors != null)
                                {
                                    foreach (var err in validationErrors)
                                    {
                                        string errStr = (string)(err?.ToString() ?? "Unknown validation error");
                                        _logger.LogWarning("RW validation error: {Error}", (object)errStr);
                                        if (!errors.Contains(errStr))
                                            errors.Add(errStr);
                                    }
                                }
                            }
                            catch (Exception vex)
                            {
                                _logger.LogInformation("Could not call WalidujDane: {Error}", (object)vex.Message);
                            }

                            // Add context for historical documents
                            string message = "Failed to create RW";
                            if (isHistoricalDocument)
                            {
                                message = $"Failed to create RW for historical date {request.IssueDate:yyyy-MM-dd}. Products may not have had sufficient stock on that date.";
                            }

                            return (false, null, message, errors);
                        }
                    }
                    catch (Exception saveEx)
                    {
                        _logger.LogError(saveEx, "RW Zapisz() threw exception");
                        return (false, null, $"RW save exception: {saveEx.Message}", new List<string> { saveEx.ToString() });
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
            // Get operator credentials from API key claims BEFORE entering SDK thread
            var operatorCredentials = GetOperatorCredentialsFromClaims();

            // Use thread-safe execution - EF6 is NOT thread-safe
            var result = await _sferaService.ExecuteWithLockAsync<(bool Success, WarehouseDocumentDto? Data, string Message, List<string> Errors)>(() =>
            {
                // Switch to the operator specified in API key (if any)
                if (!SwitchToRequestOperator(operatorCredentials))
                {
                    return (false, null, "Failed to switch to the operator associated with this API key", new List<string>());
                }

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

                    // CRITICAL: Set date BEFORE reserving number (number depends on date!)
                    if (request.IssueDate.HasValue)
                    {
                        _logger.LogInformation("Setting PW IssueDate to: {Date}", request.IssueDate.Value);

                        // Log available properties on both Dane and Dokument for debugging
                        var daneType = ((object)pw.Dane).GetType();
                        var daneProps = daneType.GetProperties()
                            .Where(p => p.Name.Contains("Data") || p.Name.Contains("Date"))
                            .Select(p => $"Dane.{p.Name}")
                            .ToList();

                        var dokType = ((object)pw.Dokument).GetType();
                        var dokProps = dokType.GetProperties()
                            .Where(p => p.Name.Contains("Data") || p.Name.Contains("Date"))
                            .Select(p => $"Dokument.{p.Name}")
                            .ToList();

                        _logger.LogInformation("PW date properties: {Props}", string.Join(", ", daneProps.Concat(dokProps)));

                        // Set both issue date AND entry date (for numbering)
                        // DataWydaniaWystawienia = display/issue date
                        // DataWprowadzenia = entry date (may affect numbering)
                        try
                        {
                            pw.Dane.DataWydaniaWystawienia = request.IssueDate.Value;
                            _logger.LogInformation("Direct assignment Dane.DataWydaniaWystawienia: Success");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogInformation("Direct assignment Dane.DataWydaniaWystawienia: Failed - {Error}", ex.Message);
                        }

                        // Also set DataWprowadzenia (entry date) - this may affect document numbering
                        try
                        {
                            pw.Dane.DataWprowadzenia = request.IssueDate.Value;
                            _logger.LogInformation("Direct assignment Dane.DataWprowadzenia: Success");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogInformation("Direct assignment Dane.DataWprowadzenia: Failed - {Error}", ex.Message);
                        }

                        // Try on Dokument as well
                        try
                        {
                            pw.Dokument.DataWydaniaWystawienia = request.IssueDate.Value;
                            _logger.LogInformation("Direct assignment Dokument.DataWydaniaWystawienia: Success");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogInformation("Direct assignment Dokument.DataWydaniaWystawienia: Failed - {Error}", ex.Message);
                        }
                    }

                    // Reserve number AFTER setting date (number format includes year/month)
                    pw.ZarezerwujNumer();
                    _logger.LogInformation("Reserved PW number: {Number}", (string?)pw.PodajPodgladNumeru()?.ToString() ?? "");

                    if (!string.IsNullOrEmpty(request.Notes))
                    {
                        bool notesSet = DynamicPropertyHelper.TrySetProperty(pw.Dane, "Uwagi", request.Notes);
                        _logger.LogInformation("TrySetProperty Uwagi: {Result}", notesSet);
                    }

                    // CRITICAL: Validate ALL products exist before adding any items
                    if (request.Items != null && request.Items.Any())
                    {
                        var missingProducts = ValidateProductsExist(request.Items);
                        if (missingProducts.Any())
                        {
                            _logger.LogWarning("PW creation failed - {Count} product(s) not found: {Products}",
                                (object)missingProducts.Count, (object)string.Join(", ", missingProducts));
                            var errorList = new List<string>
                            {
                                $"Cannot create document - the following products were not found: {string.Join(", ", missingProducts)}"
                            };
                            return (false, null, "Products not found - document not created", errorList);
                        }
                    }

                    // Add items using product ID (all validated to exist)
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
            // Get operator credentials from API key claims BEFORE entering SDK thread
            var operatorCredentials = GetOperatorCredentialsFromClaims();

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
                // Switch to the operator specified in API key (if any)
                if (!SwitchToRequestOperator(operatorCredentials))
                {
                    return (false, null, "Failed to switch to the operator associated with this API key", new List<string>());
                }

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

                    // CRITICAL: Validate ALL products exist before adding any items
                    if (request.Items != null && request.Items.Any())
                    {
                        var missingProducts = ValidateProductsExist(request.Items);
                        if (missingProducts.Any())
                        {
                            _logger.LogWarning("MM creation failed - {Count} product(s) not found: {Products}",
                                (object)missingProducts.Count, (object)string.Join(", ", missingProducts));
                            var errorList = new List<string>
                            {
                                $"Cannot create document - the following products were not found: {string.Join(", ", missingProducts)}"
                            };
                            return (false, null, "Products not found - document not created", errorList);
                        }
                    }

                    // Add items using product ID (all validated to exist)
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

    /// <summary>
    /// Associate two warehouse documents (e.g., link RW with PW)
    /// </summary>
    /// <param name="id">Source document ID</param>
    /// <param name="request">Association request with target document ID</param>
    /// <returns>Success status</returns>
    [HttpPost("{id}/associate")]
    public async Task<ActionResult<ApiResponse<object>>> AssociateDocuments(int id, [FromBody] DocumentAssociationRequest request)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync<(bool Success, string Message)>(() =>
            {
                // Managers to check for warehouse documents
                var managersToCheck = new[]
                {
                    ("WydaniaZewnetrzne", WarehouseDocumentType.WZ),
                    ("PrzyjeciaZewnetrzne", WarehouseDocumentType.PZ),
                    ("RozchodyWewnetrzne", WarehouseDocumentType.RW),
                    ("PrzychodyWewnetrzne", WarehouseDocumentType.PW),
                    ("PrzesunieciaMiedzymagazynowe", WarehouseDocumentType.MM)
                };

                // Find source document
                dynamic? sourceDocument = null;
                string? sourceManagerName = null;

                foreach (var (managerName, docType) in managersToCheck)
                {
                    var manager = _sferaService.GetManager(managerName);
                    if (manager == null) continue;

                    try
                    {
                        foreach (var d in manager.Dane.Wszystkie())
                        {
                            if (DynamicPropertyHelper.GetId(d) == id)
                            {
                                sourceDocument = d;
                                sourceManagerName = managerName;
                                break;
                            }
                        }
                        if (sourceDocument != null) break;
                    }
                    catch { continue; }
                }

                if (sourceDocument == null)
                {
                    return (false, $"Source warehouse document with ID {id} not found");
                }

                // Find target document
                dynamic? targetDocument = null;

                foreach (var (managerName, docType) in managersToCheck)
                {
                    var manager = _sferaService.GetManager(managerName);
                    if (manager == null) continue;

                    try
                    {
                        foreach (var d in manager.Dane.Wszystkie())
                        {
                            if (DynamicPropertyHelper.GetId(d) == request.TargetDocumentId)
                            {
                                targetDocument = d;
                                break;
                            }
                        }
                        if (targetDocument != null) break;
                    }
                    catch { continue; }
                }

                if (targetDocument == null)
                {
                    return (false, $"Target warehouse document with ID {request.TargetDocumentId} not found");
                }

                // Create association using DokumentyPowiazane
                try
                {
                    var dokumentyPowiazane = DynamicPropertyHelper.GetProperty(sourceDocument, "DokumentyPowiazane");
                    if (dokumentyPowiazane == null)
                    {
                        return (false, "Source document does not support document associations (DokumentyPowiazane)");
                    }

                    // Add the target document to the collection
                    dokumentyPowiazane.Dodaj(targetDocument);

                    string sourceNumber = DynamicPropertyHelper.GetNestedString(sourceDocument, "NumerWewnetrzny", "PelnaSygnatura") ?? id.ToString();
                    string targetNumber = DynamicPropertyHelper.GetNestedString(targetDocument, "NumerWewnetrzny", "PelnaSygnatura") ?? request.TargetDocumentId.ToString();

                    _logger.LogInformation("Associated warehouse document {SourceDoc} with {TargetDoc}", sourceNumber, targetNumber);

                    return (true, $"Documents {sourceNumber} and {targetNumber} associated successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating document association");
                    return (false, $"Error creating association: {ex.Message}");
                }
            });

            if (result.Success)
            {
                return Ok(ApiResponse<object>.Ok(new
                {
                    SourceDocumentId = id,
                    TargetDocumentId = request.TargetDocumentId,
                    RelationType = request.RelationType
                }, result.Message));
            }
            else
            {
                return BadRequest(ApiResponse<object>.Error(result.Message));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error associating warehouse documents {SourceId} with {TargetId}", id, request.TargetDocumentId);
            return StatusCode(500, ApiResponse<object>.Error("Error associating documents", new List<string> { ex.Message }));
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
    /// <summary>
    /// Validates that all products in the request exist before document creation.
    /// Returns a list of missing product identifiers.
    /// </summary>
    private List<string> ValidateProductsExist(List<CreateWarehouseDocumentItemRequest> items)
    {
        var missingProducts = new List<string>();

        var asortymentyManager = _sferaService.GetManager("Asortymenty");
        if (asortymentyManager == null)
        {
            _logger.LogError("Asortymenty manager is null - cannot validate products!");
            missingProducts.Add("Unable to access products catalog");
            return missingProducts;
        }

        foreach (var item in items)
        {
            var asortyment = FindAsortyment(asortymentyManager, item.ProductId, item.ProductSymbol, item.ProductEan);

            if (asortyment == null)
            {
                string searchInfo = item.ProductSymbol ?? item.ProductId?.ToString() ?? item.ProductEan ?? "unknown";
                missingProducts.Add(searchInfo);
            }
        }

        return missingProducts;
    }

    private void AddWarehouseDocumentItemsById(dynamic dokument, List<CreateWarehouseDocumentItemRequest> items)
    {
        var asortymentyManager = _sferaService.GetManager("Asortymenty");
        if (asortymentyManager == null)
        {
            _logger.LogError("Asortymenty manager is null - cannot add items!");
            return;
        }

        int addedCount = 0;

        foreach (var item in items)
        {
            var asortyment = FindAsortyment(asortymentyManager, item.ProductId, item.ProductSymbol, item.ProductEan);

            // Products should already be validated by ValidateProductsExist, but handle just in case
            if (asortyment == null)
            {
                string searchInfo = item.ProductSymbol ?? item.ProductId?.ToString() ?? item.ProductEan ?? "unknown";
                _logger.LogError("Product NOT FOUND during add: {Search} - this should have been caught by validation!", (object)searchInfo);
                throw new InvalidOperationException($"Product not found: {searchInfo}. Validation should have caught this.");
            }

            {
                int towarId = DynamicPropertyHelper.GetId(asortyment);
                string towarSymbol = DynamicPropertyHelper.GetString(asortyment, "Symbol") ?? towarId.ToString();
                // CRITICAL: Use Pozycje.Dodaj(towarId) pattern for EF6 compatibility
                _logger.LogInformation("Adding position: TowarId={TowarId}, Symbol={Symbol}, Qty={Qty}", (object)towarId, (object)towarSymbol, (object)item.Quantity);
                var pozycja = dokument.Pozycje.Dodaj(towarId);

                if (pozycja != null)
                {
                    pozycja.Ilosc = item.Quantity;

                    // Try to set price - warehouse documents use complex Cena type
                    // NOTE: Internal documents (PW/RW) may ignore manual pricing and use inventory valuation instead
                    if (item.PriceNet.HasValue)
                    {
                        // Log available price properties for debugging
                        var pozType = ((object)pozycja).GetType();
                        var priceProps = pozType.GetProperties()
                            .Where(p => p.Name.Contains("Cena") || p.Name.Contains("Wartosc") || p.Name.Contains("Price"))
                            .Select(p => $"{p.Name}({p.PropertyType.Name})")
                            .ToList();
                        _logger.LogInformation("Position price properties: {Props}", string.Join(", ", priceProps));

                        // Try multiple approaches to set price
                        bool priceSet = false;

                        // 1. Try Cena.NettoPrzedRabatem (Net before discount)
                        try
                        {
                            var cenaObj = pozycja.Cena;
                            if (cenaObj != null)
                            {
                                try
                                {
                                    cenaObj.NettoPrzedRabatem = item.PriceNet.Value;
                                    priceSet = true;
                                    _logger.LogInformation("Direct assignment Cena.NettoPrzedRabatem={Price}: Success", item.PriceNet.Value);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogInformation("Direct assignment Cena.NettoPrzedRabatem: Failed - {Error}", ex.Message);
                                }

                                if (!priceSet)
                                {
                                    try
                                    {
                                        cenaObj.NettoPoRabacie = item.PriceNet.Value;
                                        priceSet = true;
                                        _logger.LogInformation("Direct assignment Cena.NettoPoRabacie: Success");
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogInformation("Direct assignment Cena.NettoPoRabacie: Failed - {Error}", ex.Message);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogInformation("Could not access Cena object: {Error}", ex.Message);
                        }

                        // 2. Try CenaEwidencyjna (record/accounting price)
                        if (!priceSet)
                        {
                            try
                            {
                                pozycja.CenaEwidencyjna = item.PriceNet.Value;
                                priceSet = true;
                                _logger.LogInformation("Direct assignment CenaEwidencyjna={Price}: Success", item.PriceNet.Value);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogInformation("Direct assignment CenaEwidencyjna: Failed - {Error}", ex.Message);
                            }
                        }

                        // 3. Try CenaZCennika (price from price list)
                        if (!priceSet)
                        {
                            try
                            {
                                pozycja.CenaZCennika = item.PriceNet.Value;
                                priceSet = true;
                                _logger.LogInformation("Direct assignment CenaZCennika={Price}: Success", item.PriceNet.Value);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogInformation("Direct assignment CenaZCennika: Failed - {Error}", ex.Message);
                            }
                        }

                        // If no price property worked, it's likely an internal document using inventory valuation
                        if (!priceSet)
                        {
                            _logger.LogWarning("Could not set price on position - document may use inventory valuation");
                        }
                    }
                    else
                    {
                        // Log when item has no price - this might cause validation errors on RW documents
                        string symbolInfo = item.ProductSymbol ?? item.ProductId?.ToString() ?? "unknown";
                        _logger.LogWarning("Position {Symbol} has no price (PriceNet is null) - may cause validation error", (object)symbolInfo);
                    }

                    addedCount++;
                }
            }
        }

        _logger.LogInformation("AddWarehouseDocumentItemsById completed: {Added} added, {Total} total requested",
            (object)addedCount, (object)(items?.Count ?? 0));
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
            RelatedDocumentNumber = DynamicPropertyHelper.GetString(dokument, "NumerZewnetrzny")
                                 ?? DynamicPropertyHelper.GetString(dokument, "NumerObcy"),
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

    private List<string> GetBusinessObjectErrors(dynamic obiekt)
    {
        var errors = new List<string>();
        try
        {
            // Try InvalidData property (standard SDK pattern)
            var invalidData = DynamicPropertyHelper.GetProperty(obiekt, "InvalidData");
            if (invalidData != null)
            {
                _logger.LogInformation("Found InvalidData property");

                // Log InvalidData type and count
                try
                {
                    var invalidDataType = ((object)invalidData).GetType();
                    _logger.LogInformation("InvalidData type: {Type}", (object)invalidDataType.FullName);

                    // Try to get count
                    var countProp = invalidDataType.GetProperty("Count");
                    if (countProp != null)
                    {
                        object? countValue = countProp.GetValue(invalidData);
                        string countStr = countValue?.ToString() ?? "null";
                        _logger.LogInformation("InvalidData count: {Count}", (object)countStr);
                    }

                    // Log all properties of InvalidData for debugging
                    var props = invalidDataType.GetProperties().Select(p => p.Name).ToList();
                    _logger.LogInformation("InvalidData properties: {Props}", (object)string.Join(", ", props));
                }
                catch (Exception ex)
                {
                    _logger.LogInformation("Could not inspect InvalidData: {Error}", (object)ex.Message);
                }

                int entityIndex = 0;
                var invalidProducts = new List<string>(); // Track which products are invalid
                foreach (var encjaZBledami in invalidData)
                {
                    entityIndex++;
                    _logger.LogInformation("Processing InvalidData entity #{Index}", (object)entityIndex);

                    // Log entity type for debugging
                    try
                    {
                        var entityType = ((object)encjaZBledami).GetType();
                        _logger.LogInformation("Entity type: {Type}", (object)entityType.Name);

                        // FIRST: Try to identify the product and quantity of the invalid position
                        string productSymbol = "unknown";
                        string productId = "?";
                        string quantity = "?";

                        try
                        {
                            var asortyment = DynamicPropertyHelper.GetProperty(encjaZBledami, "AsortymentWybrany")
                                ?? DynamicPropertyHelper.GetProperty(encjaZBledami, "AsortymentAktualny");
                            if (asortyment != null)
                            {
                                productSymbol = DynamicPropertyHelper.GetString(asortyment, "Symbol") ?? "unknown";
                                productId = DynamicPropertyHelper.GetId(asortyment).ToString();
                            }

                            var ilosc = DynamicPropertyHelper.GetDecimal(encjaZBledami, "Ilosc");
                            quantity = ilosc.ToString("0.##");
                        }
                        catch { }

                        _logger.LogWarning("INVALID POSITION: Product={Symbol} (Id={Id}), Qty={Qty}",
                            (object)productSymbol, (object)productId, (object)quantity);

                        // Track the invalid product
                        if (productSymbol != "unknown")
                            invalidProducts.Add($"{productSymbol} (qty: {quantity})");

                        // Check IDataErrorInfo.Error property (standard .NET validation)
                        try
                        {
                            var errorProp = entityType.GetProperty("Error");
                            if (errorProp != null)
                            {
                                var errorVal = errorProp.GetValue(encjaZBledami);
                                if (errorVal != null && !string.IsNullOrWhiteSpace(errorVal.ToString()))
                                {
                                    string errMsg = $"{productSymbol}: {errorVal}";
                                    _logger.LogWarning("IDataErrorInfo.Error: {Error}", (object)errorVal.ToString());
                                    if (!errors.Contains(errMsg))
                                        errors.Add(errMsg);
                                }
                            }
                        }
                        catch { }

                        // Check IDataErrorInfo indexer for specific properties
                        try
                        {
                            var indexer = entityType.GetProperty("Item", typeof(string), new[] { typeof(string) });
                            if (indexer != null)
                            {
                                var propsToCheck = new[] { "Ilosc", "Cena", "CenaEwidencyjna", "Asortyment", "Magazyn", "Stan", "Braki" };
                                foreach (var propToCheck in propsToCheck)
                                {
                                    try
                                    {
                                        var err = indexer.GetValue(encjaZBledami, new object[] { propToCheck });
                                        if (err != null && !string.IsNullOrWhiteSpace(err.ToString()))
                                        {
                                            string errMsg = $"{productSymbol}.{propToCheck}: {err}";
                                            _logger.LogWarning("Position[{Prop}]: {Error}", (object)propToCheck, (object)err.ToString());
                                            if (!errors.Contains(errMsg))
                                                errors.Add(errMsg);
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }
                        catch { }

                        // Check for Braki (shortages) on the position
                        try
                        {
                            var braki = DynamicPropertyHelper.GetProperty(encjaZBledami, "Braki");
                            if (braki != null)
                            {
                                _logger.LogWarning("Position has Braki (shortages): {Braki}", (object)(braki.ToString() ?? "yes"));
                            }
                        }
                        catch { }

                        // Check Rozbieznosc (discrepancy)
                        try
                        {
                            var rozbieznosc = DynamicPropertyHelper.GetProperty(encjaZBledami, "Rozbieznosc");
                            if (rozbieznosc != null)
                            {
                                _logger.LogWarning("Position has Rozbieznosc: {Rozbieznosc}", (object)(rozbieznosc.ToString() ?? "yes"));
                            }
                        }
                        catch { }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInformation("Could not inspect entity: {Error}", (object)ex.Message);
                    }

                    var entityErrors = DynamicPropertyHelper.GetProperty(encjaZBledami, "Errors");
                    if (entityErrors != null)
                    {
                        _logger.LogInformation("Found Errors collection on entity");
                        foreach (var blad in entityErrors)
                        {
                            string errStr = (string)(blad?.ToString() ?? "Unknown error");
                            _logger.LogWarning("InvalidData.Errors: {Error}", (object)errStr);
                            errors.Add(errStr);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("No Errors collection on entity");
                    }

                    var memberErrors = DynamicPropertyHelper.GetProperty(encjaZBledami, "MemberErrors");
                    if (memberErrors != null)
                    {
                        _logger.LogInformation("Found MemberErrors collection on entity");
                        foreach (var bladNaPolach in memberErrors)
                        {
                            try
                            {
                                var key = DynamicPropertyHelper.GetProperty(bladNaPolach, "Key");
                                string errStr = $"{key}: {bladNaPolach}";
                                _logger.LogWarning("InvalidData.MemberErrors: {Error}", (object)errStr);
                                errors.Add(errStr);
                            }
                            catch
                            {
                                string errStr = (string)(bladNaPolach?.ToString() ?? "Unknown error");
                                _logger.LogWarning("InvalidData.MemberErrors: {Error}", (object)errStr);
                                errors.Add(errStr);
                            }
                        }
                    }
                    else
                    {
                        _logger.LogInformation("No MemberErrors collection on entity");
                    }
                }

                if (entityIndex == 0)
                {
                    _logger.LogInformation("InvalidData collection is empty (no entities)");
                }
                else if (errors.Count == 0)
                {
                    // InvalidData has entities but no error messages extracted - add helpful message with product list
                    string productsInfo = invalidProducts.Count > 0
                        ? string.Join(", ", invalidProducts)
                        : $"{entityIndex} position(s)";
                    errors.Add($"Validation failed for: {productsInfo}. Check product data, prices, and stock availability.");
                }
            }

            // Try Bledy property (alternative error collection)
            try
            {
                var bledy = DynamicPropertyHelper.GetProperty(obiekt, "Bledy");
                if (bledy != null)
                {
                    _logger.LogInformation("Found Bledy property");
                    foreach (var blad in bledy)
                    {
                        string errStr = (string)(blad?.ToString() ?? "Unknown error");
                        _logger.LogWarning("Bledy: {Error}", (object)errStr);
                        if (!errors.Contains(errStr))
                            errors.Add(errStr);
                    }
                }
            }
            catch { }

            // Try Errors property (English naming)
            try
            {
                var errorsCol = DynamicPropertyHelper.GetProperty(obiekt, "Errors");
                if (errorsCol != null)
                {
                    _logger.LogInformation("Found Errors property");
                    foreach (var err in errorsCol)
                    {
                        string errStr = (string)(err?.ToString() ?? "Unknown error");
                        _logger.LogWarning("Errors: {Error}", (object)errStr);
                        if (!errors.Contains(errStr))
                            errors.Add(errStr);
                    }
                }
            }
            catch { }

            // Try to check Dokument and Dane for errors
            try
            {
                var dokument = DynamicPropertyHelper.GetProperty(obiekt, "Dokument");
                if (dokument != null)
                {
                    var dokInvalidData = DynamicPropertyHelper.GetProperty(dokument, "InvalidData");
                    if (dokInvalidData != null)
                    {
                        _logger.LogInformation("Found InvalidData on Dokument");
                        foreach (var err in dokInvalidData)
                        {
                            string errStr = (string)(err?.ToString() ?? "Unknown document error");
                            if (!errors.Contains(errStr))
                            {
                                _logger.LogWarning("Dokument.InvalidData: {Error}", (object)errStr);
                                errors.Add(errStr);
                            }
                        }
                    }
                }
            }
            catch { }

            // Try to check Pozycje for errors
            try
            {
                var pozycje = DynamicPropertyHelper.GetProperty(obiekt, "Pozycje");
                if (pozycje != null)
                {
                    int pozIndex = 0;
                    foreach (var poz in pozycje)
                    {
                        pozIndex++;
                        var pozInvalidData = DynamicPropertyHelper.GetProperty(poz, "InvalidData");
                        if (pozInvalidData != null)
                        {
                            foreach (var err in pozInvalidData)
                            {
                                string errStr = $"Pozycja #{pozIndex}: {err?.ToString() ?? "Unknown error"}";
                                if (!errors.Contains(errStr))
                                {
                                    _logger.LogWarning("Pozycja.InvalidData: {Error}", (object)errStr);
                                    errors.Add(errStr);
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            // Try Braki (shortages) property - visible in BO properties list
            try
            {
                var braki = DynamicPropertyHelper.GetProperty(obiekt, "Braki");
                if (braki != null)
                {
                    _logger.LogInformation("Found Braki property");
                    foreach (var brak in braki)
                    {
                        string brakStr = brak?.ToString() ?? "Unknown shortage";
                        _logger.LogWarning("Braki: {Brak}", (object)brakStr);
                        if (!errors.Contains(brakStr))
                            errors.Add(brakStr);
                    }
                }
            }
            catch { }

            // Log available properties for debugging if no errors found
            if (errors.Count == 0)
            {
                try
                {
                    var objType = ((object)obiekt).GetType();
                    var props = objType.GetProperties()
                        .Where(p => p.Name.Contains("Error") || p.Name.Contains("Blad") || p.Name.Contains("Invalid") || p.Name.Contains("Valid") || p.Name.Contains("Brak"))
                        .Select(p => p.Name)
                        .ToList();
                    _logger.LogInformation("Available error-related properties: {Props}", (object)string.Join(", ", props));

                    // Also log ALL properties for deeper debugging
                    var allProps = objType.GetProperties().Select(p => p.Name).ToList();
                    _logger.LogInformation("All BO properties: {Props}", (object)string.Join(", ", allProps));
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not retrieve error details");
            errors.Add($"Could not retrieve error details: {ex.Message}");
        }
        return errors;
    }
}
