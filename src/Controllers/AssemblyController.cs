using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Requests;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Assembly (ZPM - Zlecenie Produkcyjne Montowania) operations controller
/// Handles montaż (assembly) and demontaż (disassembly) operations using SDK pattern
/// </summary>
[ApiController]
[Route("api/assembly")]
[Authorize]
[Tags("Assembly (ZPM)")]
public class AssemblyController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<AssemblyController> _logger;

    public AssemblyController(ISferaService sferaService, ILogger<AssemblyController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    #region Assembly Operations (Montaż)

    /// <summary>
    /// Create assembly operation (montaż - ZPM)
    /// Creates a ZPM (Zlecenie Produkcyjne Montowania) document combining components into a finished product.
    /// If UseKompletDefinition=true (default), components are auto-populated from the komplet definition in Nexo.
    /// </summary>
    /// <param name="request">Assembly request with product and optional components</param>
    /// <returns>Created assembly details</returns>
    [HttpPost("montaz")]
    [ProducesResponseType(typeof(ApiResponse<AssemblyDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<AssemblyDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<AssemblyDto>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<AssemblyDto>> CreateAssembly([FromBody] CreateAssemblyRequest request)
    {
        try
        {
            _logger.LogInformation("[ZPM] Creating assembly for product {ProductId}/{ProductSymbol}, qty={Qty}, warehouse={Warehouse}",
                (object?)request.ProductId, (object?)request.ProductSymbol, (object)request.Quantity, (object)request.WarehouseSymbol);

            // Get the finished product (komplet)
            var produkt = FindAsortyment(request.ProductId, request.ProductSymbol, null);
            if (produkt == null)
            {
                return NotFound(ApiResponse<AssemblyDto>.Error("Product (komplet) not found"));
            }

            string produktSymbol = DynamicPropertyHelper.GetString(produkt, "Symbol") ?? "";
            _logger.LogInformation("[ZPM] Found product: {Symbol}", (object)produktSymbol);

            // Get warehouse
            var magazyn = FindWarehouse(request.WarehouseSymbol);
            if (magazyn == null)
            {
                return NotFound(ApiResponse<AssemblyDto>.Error($"Warehouse '{request.WarehouseSymbol}' not found"));
            }

            // Get Sfera and ZleceniaProdukcyjneMontowania manager
            dynamic sfera = _sferaService.GetSfera();

            // Use extension method pattern: sfera.ZleceniaProdukcyjneMontowania()
            dynamic? zleceniaManager = null;
            try
            {
                zleceniaManager = sfera.ZleceniaProdukcyjneMontowania();
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[ZPM] ZleceniaProdukcyjneMontowania() extension not available: {Msg}", (object)ex.Message);

                // Fallback: try PodajObiektTypu
                try
                {
                    zleceniaManager = sfera.PodajObiektTypu("InsERT.Moria.Dokumenty.Logistyka.IZleceniaProdukcyjneMontowania");
                }
                catch (Exception ex2)
                {
                    _logger.LogError("[ZPM] Failed to get ZleceniaProdukcyjneMontowania: {Msg}", (object)ex2.Message);
                    return StatusCode(500, ApiResponse<AssemblyDto>.Error("Failed to access assembly module",
                        new List<string> { ex.Message, ex2.Message }));
                }
            }

            if (zleceniaManager == null)
            {
                return StatusCode(500, ApiResponse<AssemblyDto>.Error("ZleceniaProdukcyjneMontowania manager not available"));
            }

            // Create new ZPM document
            _logger.LogInformation("[ZPM] Creating ZPM document...");
            dynamic zlecenie = zleceniaManager.Utworz();

            try
            {
                // Set warehouses (issues warehouse and receipts warehouse)
                try
                {
                    var profilowanie = DynamicPropertyHelper.GetProperty(zlecenie, "Profilowanie");
                    if (profilowanie != null)
                    {
                        // Set issue warehouse (magazyn wydań)
                        DynamicPropertyHelper.TrySetProperty(profilowanie, "MagazynWydan", magazyn);
                        // Set receipt warehouse (magazyn przyjęć)
                        DynamicPropertyHelper.TrySetProperty(profilowanie, "MagazynPrzyjec", magazyn);
                        _logger.LogInformation("[ZPM] Set warehouses via Profilowanie");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("[ZPM] Profilowanie warehouse setting failed: {Msg}", (object)ex.Message);
                }

                // Use Montuj method to fill document based on komplet (SDK pattern)
                if (request.UseKompletDefinition || request.Components == null || request.Components.Count == 0)
                {
                    _logger.LogInformation("[ZPM] Using Montuj() to auto-populate from komplet definition");
                    try
                    {
                        zlecenie.Montuj(produkt);
                        _logger.LogInformation("[ZPM] Montuj() completed successfully");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("[ZPM] Montuj() failed: {Msg}", (object)ex.Message);
                        return BadRequest(ApiResponse<AssemblyDto>.Error($"Failed to populate assembly from komplet definition: {ex.Message}"));
                    }
                }
                else
                {
                    // Manual mode - set komplet and add składniki manually
                    _logger.LogInformation("[ZPM] Manual mode - adding {Count} components", (object)request.Components.Count);

                    // Set the komplet (finished product) on PozycjaKomplet
                    try
                    {
                        var dane = zlecenie.Dane;
                        var pozycjaKomplet = DynamicPropertyHelper.GetProperty(dane, "PozycjaKomplet");
                        if (pozycjaKomplet != null)
                        {
                            DynamicPropertyHelper.TrySetProperty(pozycjaKomplet, "Asortyment", produkt);
                            DynamicPropertyHelper.TrySetProperty(pozycjaKomplet, "IloscWJednostceBazowej", request.Quantity);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("[ZPM] PozycjaKomplet setting failed: {Msg}", (object)ex.Message);
                    }

                    // Add składniki (components) via PozycjeSkladniki
                    var pozycjeSkladniki = DynamicPropertyHelper.GetProperty(zlecenie, "PozycjeSkladniki");
                    if (pozycjeSkladniki != null)
                    {
                        foreach (var component in request.Components)
                        {
                            var skladnik = FindAsortyment(component.ProductId, component.ProductSymbol, null);
                            if (skladnik == null)
                            {
                                string compRef = component.ProductSymbol ?? component.ProductId?.ToString() ?? "unknown";
                                _logger.LogWarning("[ZPM] Component not found: {Ref}", (object)compRef);
                                continue;
                            }

                            try
                            {
                                // Add position using Dodaj method
                                var pozycja = pozycjeSkladniki.Dodaj(skladnik);
                                if (pozycja != null)
                                {
                                    DynamicPropertyHelper.TrySetProperty(pozycja, "Ilosc", component.Quantity);
                                    DynamicPropertyHelper.TrySetProperty(pozycja, "IloscWJednostceBazowej", component.Quantity);
                                }
                                string skladnikSymbol = DynamicPropertyHelper.GetString(skladnik, "Symbol") ?? "";
                                _logger.LogInformation("[ZPM] Added component: {Symbol}, qty={Qty}",
                                    (object)skladnikSymbol, (object)component.Quantity);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning("[ZPM] Failed to add component: {Msg}", (object)ex.Message);
                            }
                        }
                    }
                }

                // Set quantity if different from default
                try
                {
                    var dane = zlecenie.Dane;
                    var pozycjaKomplet = DynamicPropertyHelper.GetProperty(dane, "PozycjaKomplet");
                    if (pozycjaKomplet != null)
                    {
                        DynamicPropertyHelper.TrySetProperty(pozycjaKomplet, "IloscWJednostceBazowej", request.Quantity);
                        DynamicPropertyHelper.TrySetProperty(pozycjaKomplet, "Ilosc", request.Quantity);
                        _logger.LogInformation("[ZPM] Set komplet quantity: {Qty}", (object)request.Quantity);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("[ZPM] Quantity setting failed: {Msg}", (object)ex.Message);
                }

                // Set status (use default "Do realizacji" status)
                try
                {
                    var statusyManager = _sferaService.GetManager("StatusyDokumentow");
                    if (statusyManager != null)
                    {
                        var daneDomyslne = DynamicPropertyHelper.GetProperty(statusyManager, "DaneDomyslne");
                        if (daneDomyslne != null)
                        {
                            var statusDoRealizacji = DynamicPropertyHelper.GetProperty(daneDomyslne, "ZlecenieProdukcyjne_DoRealizacji");
                            if (statusDoRealizacji != null)
                            {
                                DynamicPropertyHelper.TrySetProperty(zlecenie.Dane, "StatusDokumentu", statusDoRealizacji);
                                _logger.LogInformation("[ZPM] Set status to 'Do realizacji'");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("[ZPM] Status setting failed: {Msg}", (object)ex.Message);
                }

                // Set notes
                if (!string.IsNullOrEmpty(request.Notes))
                {
                    DynamicPropertyHelper.TrySetProperty(zlecenie.Dane, "Uwagi", request.Notes);
                }

                // Save the document
                _logger.LogInformation("[ZPM] Calling Zapisz()...");
                bool saved = false;
                try
                {
                    saved = (bool)zlecenie.Zapisz();
                }
                catch (Exception saveEx)
                {
                    _logger.LogError("[ZPM] Zapisz() threw exception: {Msg}", (object)saveEx.Message);
                    if (saveEx.InnerException != null)
                    {
                        _logger.LogError("[ZPM] Inner exception: {Msg}", (object)saveEx.InnerException.Message);
                    }
                    return BadRequest(ApiResponse<AssemblyDto>.Error($"Failed to save assembly: {saveEx.Message}"));
                }

                if (saved)
                {
                    // Get document details
                    var dane = zlecenie.Dane;
                    int docId = DynamicPropertyHelper.GetId(dane);

                    string? docNumber = null;
                    try
                    {
                        var numerWewn = DynamicPropertyHelper.GetProperty(dane, "NumerWewnetrzny");
                        docNumber = numerWewn?.PelnaSygnatura?.ToString();
                    }
                    catch { }
                    docNumber ??= zlecenie.PodajPodgladNumeru()?.ToString() ?? $"ZPM-{docId}";

                    _logger.LogInformation("[ZPM] Created assembly {Number}, Id={Id}", (object)docNumber, (object)docId);

                    // Build component list from saved document
                    var componentDtos = new List<AssemblyComponentDto>();
                    try
                    {
                        var pozycjeSkladniki = DynamicPropertyHelper.GetProperty(dane, "PozycjeSkladniki");
                        if (pozycjeSkladniki != null)
                        {
                            foreach (var poz in pozycjeSkladniki)
                            {
                                var asortyment = DynamicPropertyHelper.GetProperty(poz, "Asortyment")
                                    ?? DynamicPropertyHelper.GetProperty(poz, "AsortymentAktualny");
                                if (asortyment != null)
                                {
                                    componentDtos.Add(new AssemblyComponentDto
                                    {
                                        ProductId = DynamicPropertyHelper.GetId(asortyment),
                                        ProductSymbol = DynamicPropertyHelper.GetString(asortyment, "Symbol"),
                                        ProductName = DynamicPropertyHelper.GetString(asortyment, "Nazwa"),
                                        Quantity = DynamicPropertyHelper.GetDecimal(poz, "IloscWJednostceBazowej")
                                            ?? DynamicPropertyHelper.GetDecimal(poz, "Ilosc") ?? 0,
                                        Unit = DynamicPropertyHelper.GetString(asortyment, "JednostkaMagazynowa", "Symbol") ?? "szt."
                                    });
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("[ZPM] Failed to read components: {Msg}", (object)ex.Message);
                    }

                    var dto = new AssemblyDto
                    {
                        Id = docId,
                        DocumentNumber = docNumber,
                        Type = AssemblyType.Assembly,
                        ProductId = DynamicPropertyHelper.GetId(produkt),
                        ProductSymbol = produktSymbol,
                        ProductName = DynamicPropertyHelper.GetString(produkt, "Nazwa"),
                        Quantity = request.Quantity,
                        Unit = DynamicPropertyHelper.GetString(produkt, "JednostkaMagazynowa", "Symbol") ?? "szt.",
                        WarehouseSymbol = request.WarehouseSymbol,
                        WarehouseName = DynamicPropertyHelper.GetString(magazyn, "Nazwa"),
                        Date = DynamicPropertyHelper.GetDateTime(dane, "DataDokumentu") ?? DateTime.Now,
                        Status = DynamicPropertyHelper.GetString(DynamicPropertyHelper.GetProperty(dane, "StatusDokumentu"), "Nazwa") ?? "Utworzone",
                        Components = componentDtos,
                        Notes = request.Notes
                    };

                    return CreatedAtAction(nameof(GetAssembly), new { id = dto.Id }, ApiResponse<AssemblyDto>.Ok(dto));
                }
                else
                {
                    // Get errors
                    var errors = new List<string>();
                    try
                    {
                        var bledy = zlecenie.PobierzBledy();
                        if (bledy != null)
                        {
                            foreach (var blad in (System.Collections.IEnumerable)bledy)
                            {
                                errors.Add(blad?.ToString() ?? "Unknown error");
                            }
                        }
                    }
                    catch { }

                    try
                    {
                        var walidacja = zlecenie.WalidujDane();
                        if (walidacja != null)
                        {
                            foreach (var w in (System.Collections.IEnumerable)walidacja)
                            {
                                string errStr = w?.ToString() ?? "";
                                if (!string.IsNullOrEmpty(errStr) && !errors.Contains(errStr))
                                {
                                    errors.Add(errStr);
                                }
                            }
                        }
                    }
                    catch { }

                    _logger.LogWarning("[ZPM] Zapisz() returned false. Errors: {Errors}", (object)string.Join("; ", errors));
                    return BadRequest(ApiResponse<AssemblyDto>.Error("Failed to save assembly operation", errors));
                }
            }
            finally
            {
                // Dispose zlecenie if it implements IDisposable
                try
                {
                    if (zlecenie is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating assembly");
            return StatusCode(500, ApiResponse<AssemblyDto>.Error("Error creating assembly", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create disassembly operation (demontaż - ZPR)
    /// Creates a ZPR (Zlecenie Produkcyjne Rozkompletowania) document breaking down a product into components.
    /// </summary>
    /// <param name="request">Disassembly request</param>
    /// <returns>Created disassembly details</returns>
    [HttpPost("demontaz")]
    [ProducesResponseType(typeof(ApiResponse<AssemblyDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<AssemblyDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<AssemblyDto>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<AssemblyDto>> CreateDisassembly([FromBody] CreateDisassemblyRequest request)
    {
        try
        {
            _logger.LogInformation("[ZPR] Creating disassembly for product {ProductId}/{ProductSymbol}, qty={Qty}",
                (object?)request.ProductId, (object?)request.ProductSymbol, (object)request.Quantity);

            // Get the product to disassemble
            var produkt = FindAsortyment(request.ProductId, request.ProductSymbol, null);
            if (produkt == null)
            {
                return NotFound(ApiResponse<AssemblyDto>.Error("Product not found"));
            }

            string produktSymbol = DynamicPropertyHelper.GetString(produkt, "Symbol") ?? "";

            // Get warehouse
            var magazyn = FindWarehouse(request.WarehouseSymbol);
            if (magazyn == null)
            {
                return NotFound(ApiResponse<AssemblyDto>.Error($"Warehouse '{request.WarehouseSymbol}' not found"));
            }

            // Get Sfera and ZleceniaProdukcyjneRozkompletowania manager
            dynamic sfera = _sferaService.GetSfera();

            dynamic? zleceniaManager = null;
            try
            {
                zleceniaManager = sfera.ZleceniaProdukcyjneRozkompletowania();
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[ZPR] ZleceniaProdukcyjneRozkompletowania() extension not available: {Msg}", (object)ex.Message);

                try
                {
                    zleceniaManager = sfera.PodajObiektTypu("InsERT.Moria.Dokumenty.Logistyka.IZleceniaProdukcyjneRozkompletowania");
                }
                catch (Exception ex2)
                {
                    _logger.LogError("[ZPR] Failed to get ZleceniaProdukcyjneRozkompletowania: {Msg}", (object)ex2.Message);
                    return StatusCode(500, ApiResponse<AssemblyDto>.Error("Failed to access disassembly module",
                        new List<string> { ex.Message, ex2.Message }));
                }
            }

            if (zleceniaManager == null)
            {
                return StatusCode(500, ApiResponse<AssemblyDto>.Error("ZleceniaProdukcyjneRozkompletowania manager not available"));
            }

            // Create new ZPR document
            _logger.LogInformation("[ZPR] Creating ZPR document...");
            dynamic zlecenie = zleceniaManager.Utworz();

            try
            {
                // Set warehouses via Profilowanie
                try
                {
                    var profilowanie = DynamicPropertyHelper.GetProperty(zlecenie, "Profilowanie");
                    if (profilowanie != null)
                    {
                        DynamicPropertyHelper.TrySetProperty(profilowanie, "MagazynWydan", magazyn);
                        DynamicPropertyHelper.TrySetProperty(profilowanie, "MagazynPrzyjec", magazyn);
                    }
                }
                catch { }

                // Use Rozkompletuj method (similar to Montuj for assembly)
                try
                {
                    var rozkompletujMethod = zlecenie.GetType().GetMethod("Rozkompletuj");
                    if (rozkompletujMethod != null)
                    {
                        rozkompletujMethod.Invoke(zlecenie, new object[] { produkt });
                        _logger.LogInformation("[ZPR] Rozkompletuj() completed");
                    }
                    else
                    {
                        // Fallback: set komplet manually
                        var dane = zlecenie.Dane;
                        var pozycjaKomplet = DynamicPropertyHelper.GetProperty(dane, "PozycjaKomplet");
                        if (pozycjaKomplet != null)
                        {
                            DynamicPropertyHelper.TrySetProperty(pozycjaKomplet, "Asortyment", produkt);
                            DynamicPropertyHelper.TrySetProperty(pozycjaKomplet, "IloscWJednostceBazowej", request.Quantity);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("[ZPR] Rozkompletuj() failed: {Msg}", (object)ex.Message);
                }

                // Set quantity
                try
                {
                    var dane = zlecenie.Dane;
                    var pozycjaKomplet = DynamicPropertyHelper.GetProperty(dane, "PozycjaKomplet");
                    if (pozycjaKomplet != null)
                    {
                        DynamicPropertyHelper.TrySetProperty(pozycjaKomplet, "IloscWJednostceBazowej", request.Quantity);
                        DynamicPropertyHelper.TrySetProperty(pozycjaKomplet, "Ilosc", request.Quantity);
                    }
                }
                catch { }

                // Set notes
                if (!string.IsNullOrEmpty(request.Notes))
                {
                    DynamicPropertyHelper.TrySetProperty(zlecenie.Dane, "Uwagi", request.Notes);
                }

                // Save
                _logger.LogInformation("[ZPR] Calling Zapisz()...");
                bool saved = false;
                try
                {
                    saved = (bool)zlecenie.Zapisz();
                }
                catch (Exception saveEx)
                {
                    _logger.LogError("[ZPR] Zapisz() threw exception: {Msg}", (object)saveEx.Message);
                    return BadRequest(ApiResponse<AssemblyDto>.Error($"Failed to save disassembly: {saveEx.Message}"));
                }

                if (saved)
                {
                    var dane = zlecenie.Dane;
                    int docId = DynamicPropertyHelper.GetId(dane);

                    string? docNumber = null;
                    try
                    {
                        var numerWewn = DynamicPropertyHelper.GetProperty(dane, "NumerWewnetrzny");
                        docNumber = numerWewn?.PelnaSygnatura?.ToString();
                    }
                    catch { }
                    docNumber ??= zlecenie.PodajPodgladNumeru()?.ToString() ?? $"ZPR-{docId}";

                    _logger.LogInformation("[ZPR] Created disassembly {Number}, Id={Id}", (object)docNumber, (object)docId);

                    var dto = new AssemblyDto
                    {
                        Id = docId,
                        DocumentNumber = docNumber,
                        Type = AssemblyType.Disassembly,
                        ProductId = DynamicPropertyHelper.GetId(produkt),
                        ProductSymbol = produktSymbol,
                        ProductName = DynamicPropertyHelper.GetString(produkt, "Nazwa"),
                        Quantity = request.Quantity,
                        Unit = DynamicPropertyHelper.GetString(produkt, "JednostkaMagazynowa", "Symbol") ?? "szt.",
                        WarehouseSymbol = request.WarehouseSymbol,
                        WarehouseName = DynamicPropertyHelper.GetString(magazyn, "Nazwa"),
                        Date = DynamicPropertyHelper.GetDateTime(dane, "DataDokumentu") ?? DateTime.Now,
                        Status = DynamicPropertyHelper.GetString(DynamicPropertyHelper.GetProperty(dane, "StatusDokumentu"), "Nazwa") ?? "Utworzone",
                        Components = new List<AssemblyComponentDto>(),
                        Notes = request.Notes
                    };

                    return CreatedAtAction(nameof(GetAssembly), new { id = dto.Id }, ApiResponse<AssemblyDto>.Ok(dto));
                }
                else
                {
                    var errors = new List<string>();
                    try
                    {
                        var bledy = zlecenie.PobierzBledy();
                        if (bledy != null)
                        {
                            foreach (var blad in (System.Collections.IEnumerable)bledy)
                            {
                                errors.Add(blad?.ToString() ?? "Unknown error");
                            }
                        }
                    }
                    catch { }

                    _logger.LogWarning("[ZPR] Zapisz() returned false. Errors: {Errors}", (object)string.Join("; ", errors));
                    return BadRequest(ApiResponse<AssemblyDto>.Error("Failed to save disassembly operation", errors));
                }
            }
            finally
            {
                try
                {
                    if (zlecenie is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating disassembly");
            return StatusCode(500, ApiResponse<AssemblyDto>.Error("Error creating disassembly", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Query Operations

    /// <summary>
    /// Get assembly operations list (ZPM documents)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<AssemblyListItemDto>), StatusCodes.Status200OK)]
    public ActionResult<PagedResponse<AssemblyListItemDto>> GetAssemblies(
        [FromQuery] AssemblyType? type,
        [FromQuery] int? productId,
        [FromQuery] string? warehouseSymbol,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var assemblies = new List<AssemblyListItemDto>();

            // Get ZPM documents (assembly)
            if (!type.HasValue || type.Value == AssemblyType.Assembly)
            {
                try
                {
                    dynamic? zleceniaManager = null;
                    try { zleceniaManager = sfera.ZleceniaProdukcyjneMontowania(); }
                    catch { zleceniaManager = sfera.PodajObiektTypu("InsERT.Moria.Dokumenty.Logistyka.IZleceniaProdukcyjneMontowania"); }

                    if (zleceniaManager != null)
                    {
                        foreach (var zpm in DynamicPropertyHelper.SafeGetAll((object)zleceniaManager))
                        {
                            // Apply filters
                            if (dateFrom.HasValue)
                            {
                                var data = DynamicPropertyHelper.GetDateTime(zpm, "DataDokumentu");
                                if (!data.HasValue || data.Value < dateFrom.Value) continue;
                            }
                            if (dateTo.HasValue)
                            {
                                var data = DynamicPropertyHelper.GetDateTime(zpm, "DataDokumentu");
                                if (!data.HasValue || data.Value > dateTo.Value) continue;
                            }

                            var pozycjaKomplet = DynamicPropertyHelper.GetProperty(zpm, "PozycjaKomplet");
                            var asortyment = pozycjaKomplet != null
                                ? DynamicPropertyHelper.GetProperty(pozycjaKomplet, "AsortymentAktualny")
                                    ?? DynamicPropertyHelper.GetProperty(pozycjaKomplet, "Asortyment")
                                : null;

                            if (productId.HasValue && asortyment != null)
                            {
                                if (DynamicPropertyHelper.GetId(asortyment) != productId.Value) continue;
                            }

                            int kompletId = asortyment != null ? DynamicPropertyHelper.GetId(asortyment) : 0;
                            string? numStr = null;
                            try
                            {
                                var numWewn = DynamicPropertyHelper.GetProperty(zpm, "NumerWewnetrzny");
                                numStr = numWewn?.PelnaSygnatura?.ToString();
                            }
                            catch { }

                            assemblies.Add(new AssemblyListItemDto
                            {
                                Id = DynamicPropertyHelper.GetId(zpm),
                                DocumentNumber = numStr,
                                Type = AssemblyType.Assembly,
                                ProductId = kompletId,
                                ProductSymbol = asortyment != null ? DynamicPropertyHelper.GetString(asortyment, "Symbol") : null,
                                ProductName = asortyment != null ? DynamicPropertyHelper.GetString(asortyment, "Nazwa") : null,
                                Quantity = pozycjaKomplet != null
                                    ? DynamicPropertyHelper.GetDecimal(pozycjaKomplet, "IloscWJednostceBazowej") ?? 0
                                    : 0,
                                Unit = asortyment != null
                                    ? DynamicPropertyHelper.GetString(asortyment, "JednostkaMagazynowa", "Symbol") ?? "szt."
                                    : "szt.",
                                WarehouseSymbol = warehouseSymbol, // Filter is passed through
                                Date = DynamicPropertyHelper.GetDateTime(zpm, "DataDokumentu") ?? DateTime.MinValue,
                                Status = DynamicPropertyHelper.GetString(DynamicPropertyHelper.GetProperty(zpm, "StatusDokumentu"), "Nazwa") ?? "Unknown",
                                ComponentCount = 0 // Could count PozycjeSkladniki
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to query ZPM documents: {Msg}", (object)ex.Message);
                }
            }

            // Get ZPR documents (disassembly)
            if (!type.HasValue || type.Value == AssemblyType.Disassembly)
            {
                try
                {
                    dynamic? zprManager = null;
                    try { zprManager = sfera.ZleceniaProdukcyjneRozkompletowania(); }
                    catch { zprManager = sfera.PodajObiektTypu("InsERT.Moria.Dokumenty.Logistyka.IZleceniaProdukcyjneRozkompletowania"); }

                    if (zprManager != null)
                    {
                        foreach (var zpr in DynamicPropertyHelper.SafeGetAll((object)zprManager))
                        {
                            if (dateFrom.HasValue)
                            {
                                var data = DynamicPropertyHelper.GetDateTime(zpr, "DataDokumentu");
                                if (!data.HasValue || data.Value < dateFrom.Value) continue;
                            }
                            if (dateTo.HasValue)
                            {
                                var data = DynamicPropertyHelper.GetDateTime(zpr, "DataDokumentu");
                                if (!data.HasValue || data.Value > dateTo.Value) continue;
                            }

                            var pozycjaKomplet = DynamicPropertyHelper.GetProperty(zpr, "PozycjaKomplet");
                            var asortyment = pozycjaKomplet != null
                                ? DynamicPropertyHelper.GetProperty(pozycjaKomplet, "AsortymentAktualny")
                                    ?? DynamicPropertyHelper.GetProperty(pozycjaKomplet, "Asortyment")
                                : null;

                            if (productId.HasValue && asortyment != null)
                            {
                                if (DynamicPropertyHelper.GetId(asortyment) != productId.Value) continue;
                            }

                            int kompletId = asortyment != null ? DynamicPropertyHelper.GetId(asortyment) : 0;
                            string? numStr = null;
                            try
                            {
                                var numWewn = DynamicPropertyHelper.GetProperty(zpr, "NumerWewnetrzny");
                                numStr = numWewn?.PelnaSygnatura?.ToString();
                            }
                            catch { }

                            assemblies.Add(new AssemblyListItemDto
                            {
                                Id = DynamicPropertyHelper.GetId(zpr),
                                DocumentNumber = numStr,
                                Type = AssemblyType.Disassembly,
                                ProductId = kompletId,
                                ProductSymbol = asortyment != null ? DynamicPropertyHelper.GetString(asortyment, "Symbol") : null,
                                ProductName = asortyment != null ? DynamicPropertyHelper.GetString(asortyment, "Nazwa") : null,
                                Quantity = pozycjaKomplet != null
                                    ? DynamicPropertyHelper.GetDecimal(pozycjaKomplet, "IloscWJednostceBazowej") ?? 0
                                    : 0,
                                Unit = asortyment != null
                                    ? DynamicPropertyHelper.GetString(asortyment, "JednostkaMagazynowa", "Symbol") ?? "szt."
                                    : "szt.",
                                WarehouseSymbol = warehouseSymbol,
                                Date = DynamicPropertyHelper.GetDateTime(zpr, "DataDokumentu") ?? DateTime.MinValue,
                                Status = DynamicPropertyHelper.GetString(DynamicPropertyHelper.GetProperty(zpr, "StatusDokumentu"), "Nazwa") ?? "Unknown",
                                ComponentCount = 0
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to query ZPR documents: {Msg}", (object)ex.Message);
                }
            }

            var totalCount = assemblies.Count;
            var pagedAssemblies = assemblies
                .OrderByDescending(a => a.Date)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Ok(new PagedResponse<AssemblyListItemDto>
            {
                Data = pagedAssemblies,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting assemblies");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving assemblies", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get assembly by ID (ZPM or ZPR document)
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<AssemblyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AssemblyDto>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<AssemblyDto>> GetAssembly(int id)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();

            // Try ZPM first
            try
            {
                dynamic? zleceniaManager = null;
                try { zleceniaManager = sfera.ZleceniaProdukcyjneMontowania(); }
                catch { zleceniaManager = sfera.PodajObiektTypu("InsERT.Moria.Dokumenty.Logistyka.IZleceniaProdukcyjneMontowania"); }

                if (zleceniaManager != null)
                {
                    foreach (var zpm in DynamicPropertyHelper.SafeGetAll((object)zleceniaManager))
                    {
                        if (DynamicPropertyHelper.GetId(zpm) == id)
                        {
                            return Ok(ApiResponse<AssemblyDto>.Ok(MapZpmToDto(zpm, AssemblyType.Assembly)));
                        }
                    }
                }
            }
            catch { }

            // Try ZPR
            try
            {
                dynamic? zprManager = null;
                try { zprManager = sfera.ZleceniaProdukcyjneRozkompletowania(); }
                catch { zprManager = sfera.PodajObiektTypu("InsERT.Moria.Dokumenty.Logistyka.IZleceniaProdukcyjneRozkompletowania"); }

                if (zprManager != null)
                {
                    foreach (var zpr in DynamicPropertyHelper.SafeGetAll((object)zprManager))
                    {
                        if (DynamicPropertyHelper.GetId(zpr) == id)
                        {
                            return Ok(ApiResponse<AssemblyDto>.Ok(MapZpmToDto(zpr, AssemblyType.Disassembly)));
                        }
                    }
                }
            }
            catch { }

            return NotFound(ApiResponse<AssemblyDto>.Error($"Assembly with ID {id} not found"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting assembly {Id}", id);
            return StatusCode(500, ApiResponse<AssemblyDto>.Error("Error retrieving assembly", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Helpers

    private dynamic? FindAsortyment(int? id, string? symbol, string? ean)
    {
        var asortymentyManager = _sferaService.GetManager("Asortymenty");
        if (asortymentyManager == null) return null;

        // Try fast lookup methods first
        if (!string.IsNullOrEmpty(symbol))
        {
            try
            {
                var result = asortymentyManager.Dane.WyszukajPoSymbolu(symbol);
                if (result != null) return result;
            }
            catch { }
        }

        if (!string.IsNullOrEmpty(ean))
        {
            try
            {
                var result = asortymentyManager.Dane.WyszukajPoEAN(ean);
                if (result != null) return result;
            }
            catch { }
        }

        // Fallback to iteration
        foreach (var a in DynamicPropertyHelper.SafeGetAll((object)asortymentyManager))
        {
            if (id.HasValue && DynamicPropertyHelper.GetId(a) == id.Value)
            {
                return a;
            }

            if (!string.IsNullOrEmpty(symbol) && DynamicPropertyHelper.GetString(a, "Symbol") == symbol)
            {
                return a;
            }

            if (!string.IsNullOrEmpty(ean) && DynamicPropertyHelper.GetString(a, "EAN") == ean)
            {
                return a;
            }
        }

        return null;
    }

    private dynamic? FindWarehouse(string symbol)
    {
        var magazynyManager = _sferaService.GetManager("Magazyny");
        if (magazynyManager == null) return null;

        foreach (var m in DynamicPropertyHelper.SafeGetAll((object)magazynyManager))
        {
            if (DynamicPropertyHelper.GetString(m, "Symbol") == symbol)
            {
                return m;
            }
        }
        return null;
    }

    private AssemblyDto MapZpmToDto(dynamic zpm, AssemblyType type)
    {
        var pozycjaKomplet = DynamicPropertyHelper.GetProperty(zpm, "PozycjaKomplet");
        var asortyment = pozycjaKomplet != null
            ? DynamicPropertyHelper.GetProperty(pozycjaKomplet, "AsortymentAktualny")
                ?? DynamicPropertyHelper.GetProperty(pozycjaKomplet, "Asortyment")
            : null;

        string? docNumber = null;
        try
        {
            var numWewn = DynamicPropertyHelper.GetProperty(zpm, "NumerWewnetrzny");
            docNumber = numWewn?.PelnaSygnatura?.ToString();
        }
        catch { }

        var componentDtos = new List<AssemblyComponentDto>();
        try
        {
            var pozycjeSkladniki = DynamicPropertyHelper.GetProperty(zpm, "PozycjeSkladniki");
            if (pozycjeSkladniki != null)
            {
                foreach (var poz in pozycjeSkladniki)
                {
                    var skladnik = DynamicPropertyHelper.GetProperty(poz, "AsortymentAktualny")
                        ?? DynamicPropertyHelper.GetProperty(poz, "Asortyment");
                    if (skladnik != null)
                    {
                        componentDtos.Add(new AssemblyComponentDto
                        {
                            ProductId = DynamicPropertyHelper.GetId(skladnik),
                            ProductSymbol = DynamicPropertyHelper.GetString(skladnik, "Symbol"),
                            ProductName = DynamicPropertyHelper.GetString(skladnik, "Nazwa"),
                            Quantity = DynamicPropertyHelper.GetDecimal(poz, "IloscWJednostceBazowej")
                                ?? DynamicPropertyHelper.GetDecimal(poz, "Ilosc") ?? 0,
                            Unit = DynamicPropertyHelper.GetString(skladnik, "JednostkaMagazynowa", "Symbol") ?? "szt."
                        });
                    }
                }
            }
        }
        catch { }

        return new AssemblyDto
        {
            Id = DynamicPropertyHelper.GetId(zpm),
            DocumentNumber = docNumber,
            Type = type,
            ProductId = asortyment != null ? DynamicPropertyHelper.GetId(asortyment) : 0,
            ProductSymbol = asortyment != null ? DynamicPropertyHelper.GetString(asortyment, "Symbol") : null,
            ProductName = asortyment != null ? DynamicPropertyHelper.GetString(asortyment, "Nazwa") : null,
            Quantity = pozycjaKomplet != null
                ? DynamicPropertyHelper.GetDecimal(pozycjaKomplet, "IloscWJednostceBazowej") ?? 0
                : 0,
            Unit = asortyment != null
                ? DynamicPropertyHelper.GetString(asortyment, "JednostkaMagazynowa", "Symbol") ?? "szt."
                : "szt.",
            WarehouseSymbol = null, // Would need to trace from positions
            WarehouseName = null,
            Date = DynamicPropertyHelper.GetDateTime(zpm, "DataDokumentu") ?? DateTime.MinValue,
            Status = DynamicPropertyHelper.GetString(DynamicPropertyHelper.GetProperty(zpm, "StatusDokumentu"), "Nazwa") ?? "Unknown",
            Components = componentDtos,
            Notes = DynamicPropertyHelper.GetString(zpm, "Uwagi")
        };
    }

    #endregion
}
