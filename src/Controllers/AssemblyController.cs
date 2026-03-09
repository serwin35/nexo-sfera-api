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
    public async Task<IActionResult> CreateAssembly([FromBody] CreateAssemblyRequest request)
    {
        try
        {
            _logger.LogInformation("[ZPM] Creating assembly for product {ProductId}/{ProductSymbol}, qty={Qty}, warehouse={Warehouse}",
                (object?)request.ProductId, (object?)request.ProductSymbol, (object)request.Quantity, (object)request.WarehouseSymbol);

            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var produkt = FindAsortyment(request.ProductId, request.ProductSymbol, null);
                if (produkt == null) return (status: "productNotFound", dto: (AssemblyDto?)null, errors: (List<string>?)null, logMsg: (string?)null);

                string produktSymbol = DynamicPropertyHelper.GetString(produkt, "Symbol") ?? "";

                var magazyn = FindWarehouse(request.WarehouseSymbol);
                if (magazyn == null) return (status: "warehouseNotFound", dto: (AssemblyDto?)null, errors: (List<string>?)null, logMsg: (string?)null);

                dynamic sfera = _sferaService.GetSfera();

                dynamic? zleceniaManager = null;
                try
                {
                    zleceniaManager = sfera.ZleceniaProdukcyjneMontowania();
                }
                catch (Exception ex)
                {
                    try
                    {
                        zleceniaManager = sfera.PodajObiektTypu("InsERT.Moria.Dokumenty.Logistyka.IZleceniaProdukcyjneMontowania");
                    }
                    catch (Exception ex2)
                    {
                        return (status: "managerFailed", dto: (AssemblyDto?)null,
                            errors: (List<string>?)new List<string> { ex.Message, ex2.Message }, logMsg: (string?)null);
                    }
                }

                if (zleceniaManager == null) return (status: "managerNull", dto: (AssemblyDto?)null, errors: (List<string>?)null, logMsg: (string?)null);

                dynamic zlecenie = zleceniaManager.Utworz();

                try
                {
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

                    if (request.UseKompletDefinition || request.Components == null || request.Components.Count == 0)
                    {
                        try
                        {
                            zlecenie.Montuj(produkt);
                        }
                        catch (Exception ex)
                        {
                            return (status: "montujFailed", dto: (AssemblyDto?)null,
                                errors: (List<string>?)null, logMsg: (string?)ex.Message);
                        }
                    }
                    else
                    {
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
                        catch { }

                        var pozycjeSkladniki = DynamicPropertyHelper.GetProperty(zlecenie, "PozycjeSkladniki");
                        if (pozycjeSkladniki != null)
                        {
                            foreach (var component in request.Components)
                            {
                                var skladnik = FindAsortyment(component.ProductId, component.ProductSymbol, null);
                                if (skladnik == null) continue;

                                try
                                {
                                    var pozycja = pozycjeSkladniki.Dodaj(skladnik);
                                    if (pozycja != null)
                                    {
                                        DynamicPropertyHelper.TrySetProperty(pozycja, "Ilosc", component.Quantity);
                                        DynamicPropertyHelper.TrySetProperty(pozycja, "IloscWJednostceBazowej", component.Quantity);
                                    }
                                }
                                catch { }
                            }
                        }
                    }

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
                                }
                            }
                        }
                    }
                    catch { }

                    if (!string.IsNullOrEmpty(request.Notes))
                    {
                        DynamicPropertyHelper.TrySetProperty(zlecenie.Dane, "Uwagi", request.Notes);
                    }

                    bool saved = false;
                    string? saveError = null;
                    try
                    {
                        saved = (bool)zlecenie.Zapisz();
                    }
                    catch (Exception saveEx)
                    {
                        saveError = saveEx.Message;
                    }

                    if (saveError != null)
                        return (status: "saveException", dto: (AssemblyDto?)null,
                            errors: (List<string>?)null, logMsg: (string?)saveError);

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
                        docNumber ??= zlecenie.PodajPodgladNumeru()?.ToString() ?? $"ZPM-{docId}";

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
                        catch { }

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

                        return (status: "created", dto: (AssemblyDto?)dto, errors: (List<string>?)null, logMsg: (string?)docNumber);
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
                                    errors.Add(blad?.ToString() ?? "Unknown error");
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
                                        errors.Add(errStr);
                                }
                            }
                        }
                        catch { }

                        return (status: "saveFailed", dto: (AssemblyDto?)null, errors: (List<string>?)errors, logMsg: (string?)null);
                    }
                }
                finally
                {
                    try
                    {
                        if (zlecenie is IDisposable disposable)
                            disposable.Dispose();
                    }
                    catch { }
                }
            });

            if (result.status == "productNotFound")
                return NotFound(ApiResponse<AssemblyDto>.Error("Product (komplet) not found"));

            if (result.status == "warehouseNotFound")
                return NotFound(ApiResponse<AssemblyDto>.Error($"Warehouse '{request.WarehouseSymbol}' not found"));

            if (result.status == "managerFailed")
            {
                _logger.LogError("[ZPM] Failed to get ZleceniaProdukcyjneMontowania");
                return StatusCode(500, ApiResponse<AssemblyDto>.Error("Failed to access assembly module", result.errors!));
            }

            if (result.status == "managerNull")
                return StatusCode(500, ApiResponse<AssemblyDto>.Error("ZleceniaProdukcyjneMontowania manager not available"));

            if (result.status == "montujFailed")
                return BadRequest(ApiResponse<AssemblyDto>.Error($"Failed to populate assembly from komplet definition: {result.logMsg}"));

            if (result.status == "saveException")
                return BadRequest(ApiResponse<AssemblyDto>.Error($"Failed to save assembly: {result.logMsg}"));

            if (result.status == "saveFailed")
            {
                _logger.LogWarning("[ZPM] Zapisz() returned false. Errors: {Errors}", (object)string.Join("; ", result.errors!));
                return BadRequest(ApiResponse<AssemblyDto>.Error("Failed to save assembly operation", result.errors!));
            }

            _logger.LogInformation("[ZPM] Created assembly {Number}, Id={Id}", (object)(result.logMsg ?? ""), (object)result.dto!.Id);
            return CreatedAtAction(nameof(GetAssembly), new { id = result.dto.Id }, ApiResponse<AssemblyDto>.Ok(result.dto));
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
    public async Task<IActionResult> CreateDisassembly([FromBody] CreateDisassemblyRequest request)
    {
        try
        {
            _logger.LogInformation("[ZPR] Creating disassembly for product {ProductId}/{ProductSymbol}, qty={Qty}",
                (object?)request.ProductId, (object?)request.ProductSymbol, (object)request.Quantity);

            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var produkt = FindAsortyment(request.ProductId, request.ProductSymbol, null);
                if (produkt == null) return (status: "productNotFound", dto: (AssemblyDto?)null, errors: (List<string>?)null, logMsg: (string?)null);

                string produktSymbol = DynamicPropertyHelper.GetString(produkt, "Symbol") ?? "";

                var magazyn = FindWarehouse(request.WarehouseSymbol);
                if (magazyn == null) return (status: "warehouseNotFound", dto: (AssemblyDto?)null, errors: (List<string>?)null, logMsg: (string?)null);

                dynamic sfera = _sferaService.GetSfera();

                dynamic? zleceniaManager = null;
                try
                {
                    zleceniaManager = sfera.ZleceniaProdukcyjneRozkompletowania();
                }
                catch (Exception ex)
                {
                    try
                    {
                        zleceniaManager = sfera.PodajObiektTypu("InsERT.Moria.Dokumenty.Logistyka.IZleceniaProdukcyjneRozkompletowania");
                    }
                    catch (Exception ex2)
                    {
                        return (status: "managerFailed", dto: (AssemblyDto?)null,
                            errors: (List<string>?)new List<string> { ex.Message, ex2.Message }, logMsg: (string?)null);
                    }
                }

                if (zleceniaManager == null) return (status: "managerNull", dto: (AssemblyDto?)null, errors: (List<string>?)null, logMsg: (string?)null);

                dynamic zlecenie = zleceniaManager.Utworz();

                try
                {
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

                    try
                    {
                        var rozkompletujMethod = zlecenie.GetType().GetMethod("Rozkompletuj");
                        if (rozkompletujMethod != null)
                        {
                            rozkompletujMethod.Invoke(zlecenie, new object[] { produkt });
                        }
                        else
                        {
                            var dane = zlecenie.Dane;
                            var pozycjaKomplet = DynamicPropertyHelper.GetProperty(dane, "PozycjaKomplet");
                            if (pozycjaKomplet != null)
                            {
                                DynamicPropertyHelper.TrySetProperty(pozycjaKomplet, "Asortyment", produkt);
                                DynamicPropertyHelper.TrySetProperty(pozycjaKomplet, "IloscWJednostceBazowej", request.Quantity);
                            }
                        }
                    }
                    catch { }

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

                    if (!string.IsNullOrEmpty(request.Notes))
                    {
                        DynamicPropertyHelper.TrySetProperty(zlecenie.Dane, "Uwagi", request.Notes);
                    }

                    bool saved = false;
                    string? saveError = null;
                    try
                    {
                        saved = (bool)zlecenie.Zapisz();
                    }
                    catch (Exception saveEx)
                    {
                        saveError = saveEx.Message;
                    }

                    if (saveError != null)
                        return (status: "saveException", dto: (AssemblyDto?)null,
                            errors: (List<string>?)null, logMsg: (string?)saveError);

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

                        return (status: "created", dto: (AssemblyDto?)dto, errors: (List<string>?)null, logMsg: (string?)docNumber);
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
                                    errors.Add(blad?.ToString() ?? "Unknown error");
                            }
                        }
                        catch { }

                        return (status: "saveFailed", dto: (AssemblyDto?)null, errors: (List<string>?)errors, logMsg: (string?)null);
                    }
                }
                finally
                {
                    try
                    {
                        if (zlecenie is IDisposable disposable)
                            disposable.Dispose();
                    }
                    catch { }
                }
            });

            if (result.status == "productNotFound")
                return NotFound(ApiResponse<AssemblyDto>.Error("Product not found"));

            if (result.status == "warehouseNotFound")
                return NotFound(ApiResponse<AssemblyDto>.Error($"Warehouse '{request.WarehouseSymbol}' not found"));

            if (result.status == "managerFailed")
            {
                _logger.LogError("[ZPR] Failed to get ZleceniaProdukcyjneRozkompletowania");
                return StatusCode(500, ApiResponse<AssemblyDto>.Error("Failed to access disassembly module", result.errors!));
            }

            if (result.status == "managerNull")
                return StatusCode(500, ApiResponse<AssemblyDto>.Error("ZleceniaProdukcyjneRozkompletowania manager not available"));

            if (result.status == "saveException")
                return BadRequest(ApiResponse<AssemblyDto>.Error($"Failed to save disassembly: {result.logMsg}"));

            if (result.status == "saveFailed")
            {
                _logger.LogWarning("[ZPR] Zapisz() returned false. Errors: {Errors}", (object)string.Join("; ", result.errors!));
                return BadRequest(ApiResponse<AssemblyDto>.Error("Failed to save disassembly operation", result.errors!));
            }

            _logger.LogInformation("[ZPR] Created disassembly {Number}, Id={Id}", (object)(result.logMsg ?? ""), (object)result.dto!.Id);
            return CreatedAtAction(nameof(GetAssembly), new { id = result.dto.Id }, ApiResponse<AssemblyDto>.Ok(result.dto));
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
    public async Task<IActionResult> GetAssemblies(
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
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                dynamic sfera = _sferaService.GetSfera();
                var assemblies = new List<AssemblyListItemDto>();

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
                                    WarehouseSymbol = warehouseSymbol,
                                    Date = DynamicPropertyHelper.GetDateTime(zpm, "DataDokumentu") ?? DateTime.MinValue,
                                    Status = DynamicPropertyHelper.GetString(DynamicPropertyHelper.GetProperty(zpm, "StatusDokumentu"), "Nazwa") ?? "Unknown",
                                    ComponentCount = 0
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Failed to query ZPM documents: {Msg}", (object)ex.Message);
                    }
                }

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

                return (pagedAssemblies, totalCount);
            });

            return Ok(new PagedResponse<AssemblyListItemDto>
            {
                Data = result.pagedAssemblies,
                Page = page,
                PageSize = pageSize,
                TotalCount = result.totalCount
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
    public async Task<IActionResult> GetAssembly(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                dynamic sfera = _sferaService.GetSfera();

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
                                return (found: true, dto: (AssemblyDto?)MapZpmToDto(zpm, AssemblyType.Assembly));
                            }
                        }
                    }
                }
                catch { }

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
                                return (found: true, dto: (AssemblyDto?)MapZpmToDto(zpr, AssemblyType.Disassembly));
                            }
                        }
                    }
                }
                catch { }

                return (found: false, dto: (AssemblyDto?)null);
            });

            if (!result.found)
                return NotFound(ApiResponse<AssemblyDto>.Error($"Assembly with ID {id} not found"));

            return Ok(ApiResponse<AssemblyDto>.Ok(result.dto!));
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
            WarehouseSymbol = null,
            WarehouseName = null,
            Date = DynamicPropertyHelper.GetDateTime(zpm, "DataDokumentu") ?? DateTime.MinValue,
            Status = DynamicPropertyHelper.GetString(DynamicPropertyHelper.GetProperty(zpm, "StatusDokumentu"), "Nazwa") ?? "Unknown",
            Components = componentDtos,
            Notes = DynamicPropertyHelper.GetString(zpm, "Uwagi")
        };
    }

    #endregion
}
