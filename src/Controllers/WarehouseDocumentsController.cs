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
    /// Update an existing warehouse document
    /// </summary>
    /// <param name="id">Document ID</param>
    /// <param name="request">Fields to update</param>
    /// <returns>Updated document</returns>
    [HttpPatch("{id}")]
    public async Task<ActionResult<ApiResponse<WarehouseDocumentDto>>> UpdateWarehouseDocument(int id, [FromBody] UpdateWarehouseDocumentRequest request)
    {
        try
        {
            var operatorCredentials = GetOperatorCredentialsFromClaims();

            var result = await _sferaService.ExecuteWithLockAsync<(bool Success, WarehouseDocumentDto? Data, string Message, List<string> Errors)>(() =>
            {
                if (!SwitchToRequestOperator(operatorCredentials))
                {
                    return (false, null, "Failed to switch to the operator associated with this API key", new List<string>());
                }

                // Try to find the document in different managers
                string[] managerNames = { "WydaniaZewnetrzne", "PrzyjeciaZewnetrzne", "RozchodyWewnetrzne", "PrzychodyWewnetrzne", "PrzesunieciaMiedzymagazynowe" };

                foreach (var managerName in managerNames)
                {
                    var manager = _sferaService.GetManager(managerName);
                    if (manager == null) continue;

                    dynamic? encja = null;
                    try
                    {
                        encja = manager.Dane.Wszystkie().FirstOrDefault((Func<dynamic, bool>)(d => (int)d.Id == id));
                    }
                    catch
                    {
                        continue;
                    }

                    if (encja == null) continue;

                    // Found the document - open for editing
                    _logger.LogInformation("Found warehouse document {Id} in {Manager}, opening for edit", (object)id, (object)managerName);

                    dynamic dokument;
                    try
                    {
                        dokument = manager.Edytuj(encja);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to open warehouse document {Id} for editing", (object)id);
                        return (false, null, $"Failed to open document for editing: {ex.Message}", new List<string> { ex.Message });
                    }

                    try
                    {
                        // Update fields if provided
                        if (!string.IsNullOrEmpty(request.Wystawil))
                        {
                            dokument.Dane.Wystawil = request.Wystawil;
                            _logger.LogDebug("Updated Wystawil to: {Wystawil}", (object)request.Wystawil);
                        }

                        if (request.WystawilaOsobaId.HasValue)
                        {
                            var pracownicyManager = _sferaService.GetManager("Pracownicy");
                            if (pracownicyManager != null)
                            {
                                dynamic? pracownik = null;
                                foreach (var p in pracownicyManager.Dane.Wszystkie())
                                {
                                    if ((int)p.Id == request.WystawilaOsobaId.Value)
                                    {
                                        pracownik = p;
                                        break;
                                    }
                                }
                                if (pracownik != null)
                                {
                                    dokument.Dane.WystawilaOsoba = pracownik;
                                    _logger.LogDebug("Updated WystawilaOsoba to employee ID: {Id}", (object)request.WystawilaOsobaId.Value);
                                }
                                else
                                {
                                    _logger.LogWarning("Employee with ID {Id} not found", (object)request.WystawilaOsobaId.Value);
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(request.Odebral))
                        {
                            dokument.Dane.Odebral = request.Odebral;
                            _logger.LogDebug("Updated Odebral to: {Odebral}", (object)request.Odebral);
                        }

                        if (!string.IsNullOrEmpty(request.Notes))
                        {
                            dokument.Dane.Uwagi = request.Notes;
                            _logger.LogDebug("Updated Uwagi (Notes)");
                        }

                        if (!string.IsNullOrEmpty(request.ExternalNumber))
                        {
                            dokument.Dane.NumerZewnetrzny = request.ExternalNumber;
                            _logger.LogDebug("Updated NumerZewnetrzny to: {Number}", (object)request.ExternalNumber);
                        }

                        if (request.ExternalDocumentDate.HasValue)
                        {
                            dokument.Dane.DataDokumentuZewnetrznego = request.ExternalDocumentDate.Value;
                            _logger.LogDebug("Updated DataDokumentuZewnetrznego to: {Date}", (object)request.ExternalDocumentDate.Value);
                        }

                        if (!string.IsNullOrEmpty(request.Description))
                        {
                            dokument.Dane.Opis = request.Description;
                            _logger.LogDebug("Updated Opis (Description)");
                        }

                        // Save changes
                        bool saved = dokument.Zapisz();
                        if (!saved)
                        {
                            var errors = BusinessObjectHelper.ExtractErrors(dokument);
                            _logger.LogWarning("Failed to save warehouse document updates. Errors: {Errors}", string.Join("; ", errors));
                            return (false, null, "Failed to save document updates", errors);
                        }

                        _logger.LogInformation("Successfully updated warehouse document {Id}", (object)id);

                        // Map to DTO based on document type
                        WarehouseDocumentDto dto;
                        switch (managerName)
                        {
                            case "WydaniaZewnetrzne":
                                dto = MapWZToDto(dokument.Dane);
                                break;
                            case "PrzyjeciaZewnetrzne":
                                dto = MapPZToDto(dokument.Dane);
                                break;
                            default:
                                dto = new WarehouseDocumentDto
                                {
                                    Id = (int)dokument.Dane.Id,
                                    Number = DynamicPropertyHelper.GetString(dokument.Dane, "NumerWewnetrzny") ?? "",
                                    Type = managerName switch
                                    {
                                        "RozchodyWewnetrzne" => "RW",
                                        "PrzychodyWewnetrzne" => "PW",
                                        "PrzesunieciaMiedzymagazynowe" => "MM",
                                        _ => "Unknown"
                                    },
                                    IssueDate = DynamicPropertyHelper.GetDateTime(dokument.Dane, "DataWystawienia"),
                                    Notes = DynamicPropertyHelper.GetString(dokument.Dane, "Uwagi")
                                };
                                break;
                        }

                        return (true, dto, "Document updated successfully", new List<string>());
                    }
                    finally
                    {
                        if (dokument is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                    }
                }

                return (false, null, $"Warehouse document with ID {id} not found", new List<string>());
            });

            if (!result.Success)
            {
                if (result.Message.Contains("not found"))
                {
                    return NotFound(ApiResponse<WarehouseDocumentDto>.Error(result.Message, result.Errors));
                }
                return BadRequest(ApiResponse<WarehouseDocumentDto>.Error(result.Message, result.Errors));
            }

            return Ok(ApiResponse<WarehouseDocumentDto>.Ok(result.Data!, result.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating warehouse document {Id}", (object)id);
            return StatusCode(500, ApiResponse<WarehouseDocumentDto>.Error("Error updating warehouse document", new List<string> { ex.Message }));
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
                    // SDK Pattern: Set contractor (Odbiorca) using PodmiotyDokumentu
                    SetWZContractor(wz, request.ContractorSymbol, request.ContractorNIP, request.ContractorId);

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
                    // Only if ReserveNumber flag is true - otherwise number is auto-assigned during Zapisz()
                    if (request.ReserveNumber)
                    {
                        wz.ZarezerwujNumer();
                        _logger.LogInformation("Reserved WZ number: {Number}", (string?)wz.PodajPodgladNumeru()?.ToString() ?? "");
                    }
                    else
                    {
                        _logger.LogInformation("WZ number will be auto-assigned during save (preview: {Number})", (string?)wz.PodajPodgladNumeru()?.ToString() ?? "Auto");
                    }

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

                    // Add items using SDK pattern (symbol first, then fallback)
                    AddWarehouseDocumentItemsById(wz, request.Items);

                    // SDK Pattern: Przelicz() before Zapisz()
                    wz.Przelicz();
                    _logger.LogInformation("WZ recalculated before save");

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

                // WORKAROUND for EF6 TransakcjaVAT materialization error in .NET 8:
                // Get PZ configuration and create document with Utworz(Konfiguracja) to avoid VAT loading issues
                // The standard UtworzPrzyjecieZewnetrzne() triggers VAT transaction loading which fails due to
                // EF6 column ordinal mismatch after SDK v59 schema changes (new WystepowanieFakturKsef column)
                dynamic? pzKonfiguracja = null;
                try
                {
                    var konfiguracje = _sferaService.GetManager("Konfiguracje");
                    if (konfiguracje?.DaneDomyslne != null)
                    {
                        // Try to get the standard PZ configuration (without VAT in name)
                        pzKonfiguracja = konfiguracje.DaneDomyslne.PrzyjecieZewnetrzne;
                        string configSymbol = pzKonfiguracja?.Symbol?.ToString() ?? "default";
                        _logger.LogInformation("Using PZ configuration: {Config}", (object)configSymbol);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Could not get PZ configuration: {Error}. Falling back to standard creation.", ex.Message);
                }

                // Try to create document using configuration to avoid VAT initialization issues
                dynamic pz;
                Exception? firstAttemptError = null;
                try
                {
                    if (pzKonfiguracja != null)
                    {
                        // Use Utworz(Konfiguracja) - may avoid some VAT loading paths
                        pz = przyjecia.Utworz(pzKonfiguracja);
                        _logger.LogInformation("Created PZ using Utworz(Konfiguracja)");
                    }
                    else
                    {
                        // Fallback to standard method
                        pz = przyjecia.UtworzPrzyjecieZewnetrzne();
                        _logger.LogInformation("Created PZ using UtworzPrzyjecieZewnetrzne()");
                    }
                }
                catch (Exception createEx)
                {
                    firstAttemptError = createEx;
                    // Check if this is the TransakcjaVAT EF6 error
                    bool isVatError = createEx.ToString().Contains("TransakcjaVAT") ||
                                      createEx.ToString().Contains("IdWInstancji") ||
                                      createEx.ToString().Contains("Byte[]");

                    if (isVatError)
                    {
                        _logger.LogError(createEx, "EF6 TransakcjaVAT materialization error during PZ creation. " +
                            "This is likely caused by SDK/database version mismatch. " +
                            "Ensure SDK v59 DLLs are deployed and server was restarted.");

                        // Return specific error for VAT issue
                        return (false, null,
                            "PZ creation failed due to EF6 TransakcjaVAT error. " +
                            "This is caused by SDK/database version mismatch (column WystepowanieFakturKsef). " +
                            "Ensure SDK v59 DLLs are deployed and application was restarted.",
                            new List<string> { createEx.Message });
                    }

                    // If configuration-based creation fails with different error, try standard method
                    _logger.LogWarning("Utworz(Konfiguracja) failed: {Error}. Trying UtworzPrzyjecieZewnetrzne().", createEx.Message);
                    try
                    {
                        pz = przyjecia.UtworzPrzyjecieZewnetrzne();
                        _logger.LogInformation("Created PZ using UtworzPrzyjecieZewnetrzne() as fallback");
                    }
                    catch (Exception fallbackEx)
                    {
                        // Both methods failed
                        _logger.LogError(fallbackEx, "Both PZ creation methods failed. First error: {FirstError}",
                            firstAttemptError?.Message);

                        bool isFallbackVatError = fallbackEx.ToString().Contains("TransakcjaVAT") ||
                                                  fallbackEx.ToString().Contains("IdWInstancji");

                        if (isFallbackVatError)
                        {
                            return (false, null,
                                "PZ creation failed due to EF6 TransakcjaVAT error. " +
                                "This is caused by SDK/database version mismatch. " +
                                "Ensure SDK v59 DLLs are deployed and application was restarted.",
                                new List<string> { fallbackEx.Message });
                        }

                        throw; // Re-throw for outer handler
                    }
                }

                using (pz)
                {
                    // WORKAROUND: Try to disable financial aspect to prevent VAT transaction loading
                    // This may help avoid the EF6 materialization error on TransakcjaVAT.IdWInstancji
                    try
                    {
                        // Try setting on Dane
                        pz.Dane.PosiadaAspektFinansowy = false;
                        _logger.LogInformation("Set PosiadaAspektFinansowy=false on Dane");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("Could not set PosiadaAspektFinansowy on Dane: {Error}", ex.Message);
                    }

                    try
                    {
                        // Try setting on Dokument
                        pz.Dokument.PosiadaAspektFinansowy = false;
                        _logger.LogInformation("Set PosiadaAspektFinansowy=false on Dokument");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("Could not set PosiadaAspektFinansowy on Dokument: {Error}", ex.Message);
                    }

                    // SDK Pattern: Set contractor (Dostawca) using PodmiotyDokumentu
                    SetPZContractor(pz, request.ContractorSymbol, request.ContractorNIP, request.ContractorId);

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
                    // Only if ReserveNumber flag is true - otherwise number is auto-assigned during Zapisz()
                    if (request.ReserveNumber)
                    {
                        pz.ZarezerwujNumer();
                        _logger.LogInformation("Reserved PZ number: {Number}", (string?)pz.PodajPodgladNumeru()?.ToString() ?? "");
                    }
                    else
                    {
                        _logger.LogInformation("PZ number will be auto-assigned during save (preview: {Number})", (string?)pz.PodajPodgladNumeru()?.ToString() ?? "Auto");
                    }

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

                    // CRITICAL for VAT: Set external document date (DataDokumentuZewnetrznego)
                    // This date is used for VAT settlement period and is required for PZ with financial aspect
                    var externalDocDate = request.ExternalDocumentDate ?? request.IssueDate ?? DateTime.Now;
                    try
                    {
                        // Try setting DataDokumentuZewnetrznego (primary property name)
                        pz.Dane.DataDokumentuZewnetrznego = externalDocDate;
                        _logger.LogInformation("Set DataDokumentuZewnetrznego: {Date}", externalDocDate);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("Could not set DataDokumentuZewnetrznego: {Error}", ex.Message);
                        // Try alternative property names
                        try
                        {
                            pz.Dane.DataZewnetrzna = externalDocDate;
                            _logger.LogInformation("Set DataZewnetrzna (alternative): {Date}", externalDocDate);
                        }
                        catch
                        {
                            try
                            {
                                pz.Dane.DataOtrzymania = externalDocDate;
                                _logger.LogInformation("Set DataOtrzymania (alternative): {Date}", externalDocDate);
                            }
                            catch
                            {
                                _logger.LogWarning("Could not set external document date on PZ");
                            }
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

                    // Add items using SDK pattern (symbol first, then fallback)
                    AddWarehouseDocumentItemsById(pz, request.Items);

                    // SDK Pattern: Przelicz() before Zapisz()
                    pz.Przelicz();
                    _logger.LogInformation("PZ recalculated before save");

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
                    // Only if ReserveNumber flag is true - otherwise number is auto-assigned during Zapisz()
                    if (request.ReserveNumber)
                    {
                        rw.ZarezerwujNumer();
                        _logger.LogInformation("Reserved RW number: {Number}", (string?)rw.PodajPodgladNumeru()?.ToString() ?? "");
                    }
                    else
                    {
                        _logger.LogInformation("RW number will be auto-assigned during save (preview: {Number})", (string?)rw.PodajPodgladNumeru()?.ToString() ?? "Auto");
                    }

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
                    // Only if ReserveNumber flag is true - otherwise number is auto-assigned during Zapisz()
                    if (request.ReserveNumber)
                    {
                        pw.ZarezerwujNumer();
                        _logger.LogInformation("Reserved PW number: {Number}", (string?)pw.PodajPodgladNumeru()?.ToString() ?? "");
                    }
                    else
                    {
                        _logger.LogInformation("PW number will be auto-assigned during save (preview: {Number})", (string?)pw.PodajPodgladNumeru()?.ToString() ?? "Auto");
                    }

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

                    // Reserve number - only if ReserveNumber flag is true
                    // Otherwise number is auto-assigned during Zapisz()
                    if (request.ReserveNumber)
                    {
                        mm.ZarezerwujNumer();
                        _logger.LogInformation("Reserved MM number: {Number}", (string?)mm.PodajPodgladNumeru()?.ToString() ?? "");
                    }
                    else
                    {
                        _logger.LogInformation("MM number will be auto-assigned during save (preview: {Number})", (string?)mm.PodajPodgladNumeru()?.ToString() ?? "Auto");
                    }

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

                // Find source document (read-only entity first)
                dynamic? sourceDocumentEntity = null;
                dynamic? sourceManager = null;
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
                                sourceDocumentEntity = d;
                                sourceManager = manager;
                                sourceManagerName = managerName;
                                break;
                            }
                        }
                        if (sourceDocumentEntity != null) break;
                    }
                    catch { continue; }
                }

                if (sourceDocumentEntity == null || sourceManager == null)
                {
                    return (false, $"Source warehouse document with ID {id} not found");
                }

                // Find target document (read-only entity)
                dynamic? targetDocumentEntity = null;

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
                                targetDocumentEntity = d;
                                break;
                            }
                        }
                        if (targetDocumentEntity != null) break;
                    }
                    catch { continue; }
                }

                if (targetDocumentEntity == null)
                {
                    return (false, $"Target warehouse document with ID {request.TargetDocumentId} not found");
                }

                string sourceNumber = DynamicPropertyHelper.GetNestedString(sourceDocumentEntity, "NumerWewnetrzny", "PelnaSygnatura") ?? id.ToString();
                string targetNumber = DynamicPropertyHelper.GetNestedString(targetDocumentEntity, "NumerWewnetrzny", "PelnaSygnatura") ?? request.TargetDocumentId.ToString();

                // SDK Pattern: Use Znajdz() to get editable business object, modify, then Zapisz()
                try
                {
                    // Get editable business object using Znajdz(encja)
                    using (dynamic editableDocument = sourceManager.Znajdz(sourceDocumentEntity))
                    {
                        if (editableDocument == null)
                        {
                            return (false, $"Could not open document {sourceNumber} for editing");
                        }

                        // Try to add association via DokumentyPowiazane.Add()
                        bool associationAdded = false;

                        // Pattern 1: Try Dane.DokumentyPowiazane.Add()
                        try
                        {
                            editableDocument.Dane.DokumentyPowiazane.Add(targetDocumentEntity);
                            associationAdded = true;
                            _logger.LogDebug("[ASSOC] Added via Dane.DokumentyPowiazane.Add()");
                        }
                        catch (Exception ex1)
                        {
                            _logger.LogDebug("[ASSOC] Dane.DokumentyPowiazane.Add() failed: {Msg}", ex1.Message);
                        }

                        // Pattern 2: Try DokumentyPowiazane.Dodaj() on business object
                        if (!associationAdded)
                        {
                            try
                            {
                                editableDocument.DokumentyPowiazane.Dodaj(targetDocumentEntity);
                                associationAdded = true;
                                _logger.LogDebug("[ASSOC] Added via DokumentyPowiazane.Dodaj()");
                            }
                            catch (Exception ex2)
                            {
                                _logger.LogDebug("[ASSOC] DokumentyPowiazane.Dodaj() failed: {Msg}", ex2.Message);
                            }
                        }

                        // Pattern 3: Try direct property access
                        if (!associationAdded)
                        {
                            try
                            {
                                var powiazane = DynamicPropertyHelper.GetProperty(editableDocument.Dane, "DokumentyPowiazane");
                                if (powiazane != null)
                                {
                                    powiazane.Add(targetDocumentEntity);
                                    associationAdded = true;
                                    _logger.LogDebug("[ASSOC] Added via DynamicPropertyHelper");
                                }
                            }
                            catch (Exception ex3)
                            {
                                _logger.LogDebug("[ASSOC] DynamicPropertyHelper failed: {Msg}", ex3.Message);
                            }
                        }

                        if (!associationAdded)
                        {
                            return (false, "Could not add document association - no compatible method found");
                        }

                        // CRITICAL: Save the changes using SDK pattern
                        if ((bool)editableDocument.Zapisz())
                        {
                            _logger.LogInformation("[ASSOC] Successfully associated {Source} with {Target}", sourceNumber, targetNumber);
                            return (true, $"Documents {sourceNumber} and {targetNumber} associated successfully");
                        }
                        else
                        {
                            var errors = GetBusinessObjectErrors(editableDocument);
                            string errorMsg = errors.Any() ? string.Join("; ", errors) : "Unknown error";
                            _logger.LogWarning("[ASSOC] Zapisz() failed: {Errors}", errorMsg);
                            return (false, $"Failed to save association: {errorMsg}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[ASSOC] Error creating document association");
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

    /// <summary>
    /// SDK Pattern: Set WZ contractor (Odbiorca) using PodmiotyDokumentu.UstawOdbiorceWedlugSymbolu/NIP
    /// </summary>
    private void SetWZContractor(dynamic wz, string? contractorSymbol, string? contractorNIP, int? contractorId)
    {
        bool success = false;

        // Pattern 1: UstawOdbiorceWedlugSymbolu (SDK preferred)
        if (!string.IsNullOrEmpty(contractorSymbol))
        {
            try
            {
                wz.PodmiotyDokumentu.UstawOdbiorceWedlugSymbolu(contractorSymbol);
                _logger.LogInformation("[WZ] Set contractor via PodmiotyDokumentu.UstawOdbiorceWedlugSymbolu({Symbol})", contractorSymbol);
                success = true;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("[WZ] UstawOdbiorceWedlugSymbolu failed: {Msg}", ex.Message);
            }
        }

        // Pattern 2: UstawOdbiorceWedlugNIP
        if (!success && !string.IsNullOrEmpty(contractorNIP))
        {
            try
            {
                wz.PodmiotyDokumentu.UstawOdbiorceWedlugNIP(contractorNIP);
                _logger.LogInformation("[WZ] Set contractor via PodmiotyDokumentu.UstawOdbiorceWedlugNIP({NIP})", contractorNIP);
                success = true;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("[WZ] UstawOdbiorceWedlugNIP failed: {Msg}", ex.Message);
            }
        }

        // Pattern 3: Lookup by ID, get symbol, then use UstawOdbiorceWedlugSymbolu
        if (!success && contractorId.HasValue)
        {
            var podmiotyManager = _sferaService.GetManager("Podmioty");
            if (podmiotyManager != null)
            {
                foreach (var p in podmiotyManager.Dane.Wszystkie())
                {
                    if (DynamicPropertyHelper.GetId(p) == contractorId.Value)
                    {
                        string? symbol = DynamicPropertyHelper.GetString(p, "Symbol")
                                      ?? DynamicPropertyHelper.GetString(p, "Sygnatura.PelnaSygnatura");
                        if (!string.IsNullOrEmpty(symbol))
                        {
                            try
                            {
                                wz.PodmiotyDokumentu.UstawOdbiorceWedlugSymbolu(symbol);
                                _logger.LogInformation("[WZ] Set contractor via lookup+UstawOdbiorceWedlugSymbolu({Symbol})", symbol);
                                success = true;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug("[WZ] UstawOdbiorceWedlugSymbolu (from ID) failed: {Msg}", ex.Message);
                            }
                        }

                        // Fallback to direct Dane.Podmiot assignment
                        if (!success)
                        {
                            try
                            {
                                wz.Dane.Podmiot = p;
                                _logger.LogInformation("[WZ] Set contractor via direct Dane.Podmiot (fallback)");
                                success = true;
                            }
                            catch (Exception ex2)
                            {
                                _logger.LogWarning("[WZ] Direct Dane.Podmiot assignment also failed: {Msg}", ex2.Message);
                            }
                        }
                        break;
                    }
                }
            }
        }

        if (!success && (contractorId.HasValue || !string.IsNullOrEmpty(contractorNIP) || !string.IsNullOrEmpty(contractorSymbol)))
        {
            _logger.LogWarning("[WZ] Failed to set contractor - all patterns failed");
        }
    }

    /// <summary>
    /// SDK Pattern: Set PZ contractor (Dostawca) using PodmiotyDokumentu.UstawDostawceWedlugSymbolu/NIP
    /// </summary>
    private void SetPZContractor(dynamic pz, string? contractorSymbol, string? contractorNIP, int? contractorId)
    {
        bool success = false;

        // Pattern 1: UstawDostawceWedlugSymbolu (SDK preferred)
        if (!string.IsNullOrEmpty(contractorSymbol))
        {
            try
            {
                pz.PodmiotyDokumentu.UstawDostawceWedlugSymbolu(contractorSymbol);
                _logger.LogInformation("[PZ] Set contractor via PodmiotyDokumentu.UstawDostawceWedlugSymbolu({Symbol})", contractorSymbol);
                success = true;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("[PZ] UstawDostawceWedlugSymbolu failed: {Msg}", ex.Message);
            }
        }

        // Pattern 2: UstawDostawceWedlugNIP
        if (!success && !string.IsNullOrEmpty(contractorNIP))
        {
            try
            {
                pz.PodmiotyDokumentu.UstawDostawceWedlugNIP(contractorNIP);
                _logger.LogInformation("[PZ] Set contractor via PodmiotyDokumentu.UstawDostawceWedlugNIP({NIP})", contractorNIP);
                success = true;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("[PZ] UstawDostawceWedlugNIP failed: {Msg}", ex.Message);
            }
        }

        // Pattern 3: Lookup by ID, get symbol, then use UstawDostawceWedlugSymbolu
        if (!success && contractorId.HasValue)
        {
            var podmiotyManager = _sferaService.GetManager("Podmioty");
            if (podmiotyManager != null)
            {
                foreach (var p in podmiotyManager.Dane.Wszystkie())
                {
                    if (DynamicPropertyHelper.GetId(p) == contractorId.Value)
                    {
                        string? symbol = DynamicPropertyHelper.GetString(p, "Symbol")
                                      ?? DynamicPropertyHelper.GetString(p, "Sygnatura.PelnaSygnatura");
                        if (!string.IsNullOrEmpty(symbol))
                        {
                            try
                            {
                                pz.PodmiotyDokumentu.UstawDostawceWedlugSymbolu(symbol);
                                _logger.LogInformation("[PZ] Set contractor via lookup+UstawDostawceWedlugSymbolu({Symbol})", symbol);
                                success = true;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug("[PZ] UstawDostawceWedlugSymbolu (from ID) failed: {Msg}", ex.Message);
                            }
                        }

                        // Fallback to direct Dane.Podmiot assignment
                        if (!success)
                        {
                            try
                            {
                                pz.Dane.Podmiot = p;
                                _logger.LogInformation("[PZ] Set contractor via direct Dane.Podmiot (fallback)");
                                success = true;
                            }
                            catch (Exception ex2)
                            {
                                _logger.LogWarning("[PZ] Direct Dane.Podmiot assignment also failed: {Msg}", ex2.Message);
                            }
                        }
                        break;
                    }
                }
            }
        }

        if (!success && (contractorId.HasValue || !string.IsNullOrEmpty(contractorNIP) || !string.IsNullOrEmpty(contractorSymbol)))
        {
            _logger.LogWarning("[PZ] Failed to set contractor - all patterns failed");
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

    private void AddWarehouseDocumentItemsById(dynamic dokument, List<CreateWarehouseDocumentItemRequest>? items)
    {
        if (items == null || !items.Any())
        {
            _logger.LogWarning("[WH-SDK] No items to add");
            return;
        }

        var asortymentyManager = _sferaService.GetManager("Asortymenty");
        if (asortymentyManager == null)
        {
            _logger.LogError("Asortymenty manager is null - cannot add items!");
            return;
        }

        int addedCount = 0;
        int skippedCount = 0;

        foreach (var item in items)
        {
            string searchKey = item.ProductSymbol ?? item.ProductId?.ToString() ?? item.ProductEan ?? "unknown";
            _logger.LogInformation("[WH-SDK] Adding item: Symbol={Symbol}, ProductId={Id}, Qty={Qty}",
                (object)(item.ProductSymbol ?? "(null)"), (object)(item.ProductId?.ToString() ?? "(null)"), item.Quantity);

            dynamic? pozycja = null;

            // Pattern 0 (SDK Pattern): Dodaj(symbol) - simplest SDK pattern from examples
            if (!string.IsNullOrEmpty(item.ProductSymbol))
            {
                try
                {
                    pozycja = dokument.Pozycje.Dodaj(item.ProductSymbol);
                    if (pozycja != null)
                    {
                        pozycja.Ilosc = item.Quantity;
                        _logger.LogDebug("[WH-SDK] Dodaj(symbol) + Ilosc succeeded for {Symbol}", (object)item.ProductSymbol);
                    }
                }
                catch (Exception ex0)
                {
                    _logger.LogDebug("[WH-SDK] Dodaj(symbol) failed: {Msg}", ex0.Message);
                }
            }

            // Pattern 1: Lookup asortyment and use Dodaj(towarId)
            if (pozycja == null)
            {
                var asortyment = FindAsortyment(asortymentyManager, item.ProductId, item.ProductSymbol, item.ProductEan);

                if (asortyment == null)
                {
                    _logger.LogError("[WH-SDK] Product NOT FOUND: {Search} - this should have been caught by validation!", (object)searchKey);
                    skippedCount++;
                    continue;
                }

                int towarId = DynamicPropertyHelper.GetId(asortyment);
                string towarSymbol = DynamicPropertyHelper.GetString(asortyment, "Symbol") ?? towarId.ToString();

                _logger.LogDebug("[WH-SDK] Found product: Id={Id}, Symbol={Symbol}", towarId, (object)towarSymbol);

                try
                {
                    pozycja = dokument.Pozycje.Dodaj(towarId);
                    if (pozycja != null)
                    {
                        pozycja.Ilosc = item.Quantity;
                        _logger.LogDebug("[WH-SDK] Dodaj(towarId) + Ilosc succeeded for {Symbol}", (object)towarSymbol);
                    }
                }
                catch (Exception ex1)
                {
                    _logger.LogDebug("[WH-SDK] Dodaj(towarId) failed: {Msg}", ex1.Message);
                }
            }

            if (pozycja == null)
            {
                _logger.LogWarning("[WH-SDK] All Dodaj patterns failed for: {SearchKey}", searchKey);
                skippedCount++;
                continue;
            }

            // Position added successfully - now set additional properties (price)
            // NOTE: Ilosc is already set in the Dodaj patterns above

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
                _logger.LogDebug("[WH-SDK] Position price properties: {Props}", string.Join(", ", priceProps));

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
                            _logger.LogDebug("[WH-SDK] Set Cena.NettoPrzedRabatem={Price}", item.PriceNet.Value);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug("[WH-SDK] Cena.NettoPrzedRabatem failed: {Error}", ex.Message);
                        }

                        if (!priceSet)
                        {
                            try
                            {
                                cenaObj.NettoPoRabacie = item.PriceNet.Value;
                                priceSet = true;
                                _logger.LogDebug("[WH-SDK] Set Cena.NettoPoRabacie={Price}", item.PriceNet.Value);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug("[WH-SDK] Cena.NettoPoRabacie failed: {Error}", ex.Message);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("[WH-SDK] Could not access Cena object: {Error}", ex.Message);
                }

                // 2. Try CenaEwidencyjna (record/accounting price)
                if (!priceSet)
                {
                    try
                    {
                        pozycja.CenaEwidencyjna = item.PriceNet.Value;
                        priceSet = true;
                        _logger.LogDebug("[WH-SDK] Set CenaEwidencyjna={Price}", item.PriceNet.Value);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("[WH-SDK] CenaEwidencyjna failed: {Error}", ex.Message);
                    }
                }

                // 3. Try CenaZCennika (price from price list)
                if (!priceSet)
                {
                    try
                    {
                        pozycja.CenaZCennika = item.PriceNet.Value;
                        priceSet = true;
                        _logger.LogDebug("[WH-SDK] Set CenaZCennika={Price}", item.PriceNet.Value);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("[WH-SDK] CenaZCennika failed: {Error}", ex.Message);
                    }
                }

                // If no price property worked, it's likely an internal document using inventory valuation
                if (!priceSet)
                {
                    _logger.LogDebug("[WH-SDK] Could not set price on position - document may use inventory valuation");
                }
            }

            addedCount++;
            _logger.LogInformation("[WH-SDK] Added item successfully: {SearchKey}, Qty={Qty}", (object)searchKey, item.Quantity);
        }

        _logger.LogInformation("[WH-SDK] AddWarehouseDocumentItemsById completed: {Added} added, {Skipped} skipped out of {Total} items",
            addedCount, skippedCount, items.Count);
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
