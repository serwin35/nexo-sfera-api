using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Requests;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Production orders controller:
/// ZPM (Zlecenie Produkcyjne Montowania - assembly) and ZPR (Zlecenie Produkcyjne Rozkompletowania - disassembly).
/// SDK: IZleceniaProdukcyjneMontowania / IZleceniaProdukcyjneRozkompletowania managers,
/// IZlecenieProdukcyjneMontowania / IZlecenieProdukcyjneRozkompletowania business objects over DokumentZPM.
/// </summary>
[ApiController]
[Route("api/assembly")]
[Authorize]
[Tags("Assembly (ZPM / ZPR)")]
public class AssemblyController : ControllerBase
{
    private const string DefaultUnit = "szt.";

    private readonly ISferaService _sferaService;
    private readonly StockValidationHelper _stockHelper;
    private readonly ILogger<AssemblyController> _logger;

    public AssemblyController(ISferaService sferaService, StockValidationHelper stockHelper, ILogger<AssemblyController> logger)
    {
        _sferaService = sferaService;
        _stockHelper = stockHelper;
        _logger = logger;
    }

    #region Internal result / draft types

    /// <summary>
    /// Outcome of an SDK operation executed on the STA thread. Concrete class (not a tuple) so the
    /// lambda return type is stable even though most values originate from dynamic SDK objects.
    /// </summary>
    private sealed class OperationResult<T> where T : class
    {
        public string Status { get; init; } = "ok";
        public T? Data { get; init; }
        public string? Message { get; init; }
        public List<string> Errors { get; init; } = new();

        public static OperationResult<T> Ok(T data, string? message = null) => new() { Status = "ok", Data = data, Message = message };
        public static OperationResult<T> Fail(string status, string? message = null, List<string>? errors = null)
            => new() { Status = status, Message = message, Errors = errors ?? new List<string>() };
    }

    /// <summary>
    /// Normalized header data for creating a production order (shared by assemble / disassemble).
    /// </summary>
    private sealed class ProductionOrderDraft
    {
        public AssemblyType Type { get; init; }
        public int? ProductId { get; init; }
        public string? ProductSymbol { get; init; }
        public decimal Quantity { get; init; }
        public string? Unit { get; init; }
        public string WarehouseSymbol { get; init; } = string.Empty;
        public string? ComponentsWarehouseSymbol { get; init; }
        public DateTime? IssueDate { get; init; }
        public bool ReserveNumber { get; init; }
        public bool? RecalculateComponentsOnQuantityChange { get; init; }
        public string? Notes { get; init; }
        public bool UseKitDefinition { get; init; } = true;
        public List<AssemblyComponentRequest>? Components { get; init; }
    }

    #endregion

    #region Create (assemble / disassemble)

    /// <summary>
    /// Create an assembly order (montaż - ZPM).
    /// Components are taken from the kit definition in Nexo (Montuj) unless UseKompletDefinition=false and Components are provided.
    /// </summary>
    [HttpPost("assemble")]
    [HttpPost("montaz")]
    [ProducesResponseType(typeof(ApiResponse<AssemblyDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<AssemblyDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<AssemblyDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateAssembly([FromBody] CreateAssemblyRequest request)
    {
        var draft = new ProductionOrderDraft
        {
            Type = AssemblyType.Assembly,
            ProductId = request.ProductId,
            ProductSymbol = request.ProductSymbol,
            Quantity = request.Quantity,
            Unit = request.Unit,
            WarehouseSymbol = request.WarehouseSymbol,
            ComponentsWarehouseSymbol = request.ComponentsWarehouseSymbol,
            IssueDate = request.IssueDate,
            ReserveNumber = request.ReserveNumber,
            RecalculateComponentsOnQuantityChange = request.RecalculateComponentsOnQuantityChange,
            Notes = request.Notes,
            UseKitDefinition = request.UseKompletDefinition || request.Components == null || request.Components.Count == 0,
            Components = request.Components
        };

        return await CreateProductionOrderAsync(draft);
    }

    /// <summary>
    /// Create a disassembly order (demontaż / rozkompletowanie - ZPR).
    /// Resulting components are taken from the kit definition (Rozkompletuj) unless ResultingComponents are provided.
    /// </summary>
    [HttpPost("disassemble")]
    [HttpPost("demontaz")]
    [ProducesResponseType(typeof(ApiResponse<AssemblyDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<AssemblyDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<AssemblyDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateDisassembly([FromBody] CreateDisassemblyRequest request)
    {
        var components = request.ResultingComponents?.Cast<AssemblyComponentRequest>().ToList();

        var draft = new ProductionOrderDraft
        {
            Type = AssemblyType.Disassembly,
            ProductId = request.ProductId,
            ProductSymbol = request.ProductSymbol,
            Quantity = request.Quantity,
            Unit = request.Unit,
            WarehouseSymbol = request.WarehouseSymbol,
            ComponentsWarehouseSymbol = request.ComponentsWarehouseSymbol,
            IssueDate = request.IssueDate,
            ReserveNumber = request.ReserveNumber,
            RecalculateComponentsOnQuantityChange = request.RecalculateComponentsOnQuantityChange,
            Notes = request.Notes,
            UseKitDefinition = components == null || components.Count == 0,
            Components = components
        };

        return await CreateProductionOrderAsync(draft);
    }

    private async Task<IActionResult> CreateProductionOrderAsync(ProductionOrderDraft draft)
    {
        string tag = draft.Type == AssemblyType.Assembly ? "ZPM" : "ZPR";
        try
        {
            _logger.LogInformation("[{Tag}] Creating production order for product {ProductId}/{ProductSymbol}, qty={Qty} {Unit}, warehouse={Warehouse}, componentsWarehouse={ComponentsWarehouse}",
                (object)tag, (object?)draft.ProductId, (object?)draft.ProductSymbol, (object)draft.Quantity, (object?)draft.Unit,
                (object)draft.WarehouseSymbol, (object?)draft.ComponentsWarehouseSymbol);

            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                dynamic? produkt = FindAsortyment(draft.ProductId, draft.ProductSymbol, null);
                if (produkt == null) return OperationResult<AssemblyDto>.Fail("productNotFound");

                dynamic? magazyn = FindWarehouse(draft.WarehouseSymbol);
                if (magazyn == null) return OperationResult<AssemblyDto>.Fail("warehouseNotFound", draft.WarehouseSymbol);

                dynamic? magazynSkladnikow = magazyn;
                if (!string.IsNullOrWhiteSpace(draft.ComponentsWarehouseSymbol)
                    && !string.Equals(draft.ComponentsWarehouseSymbol, draft.WarehouseSymbol, StringComparison.OrdinalIgnoreCase))
                {
                    magazynSkladnikow = FindWarehouse(draft.ComponentsWarehouseSymbol!);
                    if (magazynSkladnikow == null) return OperationResult<AssemblyDto>.Fail("warehouseNotFound", draft.ComponentsWarehouseSymbol);
                }

                dynamic? kitUnit = null;
                if (!string.IsNullOrWhiteSpace(draft.Unit))
                {
                    kitUnit = FindUnitOfMeasure(produkt, draft.Unit!);
                    if (kitUnit == null) return OperationResult<AssemblyDto>.Fail("unitNotFound", draft.Unit);
                }

                dynamic? manager = GetProductionOrdersManager(draft.Type);
                if (manager == null) return OperationResult<AssemblyDto>.Fail("managerNull");

                dynamic? zlecenie = null;
                try
                {
                    zlecenie = CreateProductionOrder(manager, draft.Type);
                    if (zlecenie == null) return OperationResult<AssemblyDto>.Fail("managerNull");

                    ApplyWarehouses(zlecenie, magazyn, magazynSkladnikow);

                    // Kit selection: Montuj / Rozkompletuj populate PozycjaKomplet and the component lines from the kit definition
                    try
                    {
                        if (draft.Type == AssemblyType.Assembly)
                            zlecenie.Montuj(produkt);
                        else
                            zlecenie.Rozkompletuj(produkt);
                    }
                    catch (Exception ex)
                    {
                        return OperationResult<AssemblyDto>.Fail("kitFailed", ex.Message);
                    }

                    if (draft.RecalculateComponentsOnQuantityChange.HasValue)
                    {
                        // Inverted semantics: SDK flag disables recalculation
                        DynamicPropertyHelper.TrySetProperty(zlecenie, "NiePrzeliczajSkladnikowPoZmianieIlosciKompletu", !draft.RecalculateComponentsOnQuantityChange.Value);
                    }

                    // Explicit static types below: a dynamic-typed return expression would poison the lambda's inferred return type
                    List<string> unitErrors = SetKitQuantityAndUnit(zlecenie, draft.Quantity, kitUnit);
                    if (unitErrors.Count > 0)
                        return OperationResult<AssemblyDto>.Fail("kitFailed", string.Join("; ", unitErrors));

                    if (!draft.UseKitDefinition && draft.Components != null && draft.Components.Count > 0)
                    {
                        List<string> replaceErrors = ReplaceComponents(zlecenie, draft.Components);
                        if (replaceErrors.Count > 0)
                            return OperationResult<AssemblyDto>.Fail("componentsFailed", null, replaceErrors);
                    }

                    ApplyHeader(zlecenie, draft.IssueDate, draft.Notes, draft.ReserveNumber, tag);

                    (bool saved, string? exception, List<string> errors) save = SaveBusinessObject(zlecenie, tag);
                    if (!save.saved)
                        return OperationResult<AssemblyDto>.Fail(save.exception != null ? "saveException" : "saveFailed", save.exception, save.errors);

                    AssemblyDto dto = MapToDto(zlecenie.Dane, draft.Type);
                    return OperationResult<AssemblyDto>.Ok(dto, dto.Number);
                }
                finally
                {
                    DisposeQuietly(zlecenie);
                }
            });

            return ToCreatedResponse(result, tag, draft.Type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Tag}] Error creating production order", (object)tag);
            return StatusCode(500, ApiResponse<AssemblyDto>.Error("Error creating production order", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create an assembly order (ZPM) from a customer order (ZK) line - SDK WypelnijnaPodstawieZK.
    /// </summary>
    [HttpPost("from-order-line")]
    [ProducesResponseType(typeof(ApiResponse<AssemblyDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<AssemblyDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<AssemblyDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateAssemblyFromOrderLine([FromBody] CreateAssemblyFromOrderLineRequest request)
    {
        const string tag = "ZPM";
        try
        {
            _logger.LogInformation("[ZPM] Creating assembly from ZK {OrderId} line {LineId}, qty={Qty}, warehouse={Warehouse}",
                (object)request.OrderId, (object)request.LineId, (object?)request.Quantity, (object)request.WarehouseSymbol);

            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                dynamic? magazyn = FindWarehouse(request.WarehouseSymbol);
                if (magazyn == null) return OperationResult<AssemblyDto>.Fail("warehouseNotFound", request.WarehouseSymbol);

                dynamic? magazynSkladnikow = magazyn;
                if (!string.IsNullOrWhiteSpace(request.ComponentsWarehouseSymbol)
                    && !string.Equals(request.ComponentsWarehouseSymbol, request.WarehouseSymbol, StringComparison.OrdinalIgnoreCase))
                {
                    magazynSkladnikow = FindWarehouse(request.ComponentsWarehouseSymbol!);
                    if (magazynSkladnikow == null) return OperationResult<AssemblyDto>.Fail("warehouseNotFound", request.ComponentsWarehouseSymbol);
                }

                dynamic? zamowienia = _sferaService.GetManager("ZamowieniaOdKlientow");
                if (zamowienia == null) return OperationResult<AssemblyDto>.Fail("orderManagerNull");

                dynamic? zamowienie = DynamicPropertyHelper.FindById((object)zamowienia, request.OrderId);
                if (zamowienie == null) return OperationResult<AssemblyDto>.Fail("orderNotFound");

                dynamic? pozycjaZk = null;
                try
                {
                    dynamic? pozycje = DynamicPropertyHelper.GetProperty(zamowienie, "Pozycje");
                    if (pozycje != null)
                    {
                        foreach (dynamic poz in pozycje)
                        {
                            if (DynamicPropertyHelper.GetId(poz) == request.LineId)
                            {
                                pozycjaZk = poz;
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("[ZPM] Failed to enumerate ZK {OrderId} lines: {Msg}", (object)request.OrderId, (object)ex.Message);
                }
                if (pozycjaZk == null) return OperationResult<AssemblyDto>.Fail("lineNotFound");

                dynamic? manager = GetProductionOrdersManager(AssemblyType.Assembly);
                if (manager == null) return OperationResult<AssemblyDto>.Fail("managerNull");

                dynamic? zlecenie = null;
                try
                {
                    zlecenie = CreateProductionOrder(manager, AssemblyType.Assembly);
                    if (zlecenie == null) return OperationResult<AssemblyDto>.Fail("managerNull");

                    ApplyWarehouses(zlecenie, magazyn, magazynSkladnikow);

                    try
                    {
                        // SDK method name really has lowercase 'na': WypelnijnaPodstawieZK
                        if (request.Quantity.HasValue)
                        {
                            decimal? quantity = request.Quantity.Value;
                            zlecenie.WypelnijnaPodstawieZK(pozycjaZk, quantity);
                        }
                        else
                        {
                            zlecenie.WypelnijnaPodstawieZK(pozycjaZk);
                        }
                    }
                    catch (Exception ex)
                    {
                        return OperationResult<AssemblyDto>.Fail("kitFailed", ex.Message);
                    }

                    // Re-apply warehouses: filling from ZK may overwrite them with the ZK warehouse
                    ApplyWarehouses(zlecenie, magazyn, magazynSkladnikow);
                    ApplyHeader(zlecenie, request.IssueDate, request.Notes, request.ReserveNumber, tag);

                    (bool saved, string? exception, List<string> errors) save = SaveBusinessObject(zlecenie, tag);
                    if (!save.saved)
                        return OperationResult<AssemblyDto>.Fail(save.exception != null ? "saveException" : "saveFailed", save.exception, save.errors);

                    AssemblyDto dto = MapToDto(zlecenie.Dane, AssemblyType.Assembly);
                    return OperationResult<AssemblyDto>.Ok(dto, dto.Number);
                }
                finally
                {
                    DisposeQuietly(zlecenie);
                }
            });

            if (result.Status == "orderManagerNull")
                return StatusCode(500, ApiResponse<AssemblyDto>.Error("ZamowieniaOdKlientow manager not available"));
            if (result.Status == "orderNotFound")
                return NotFound(ApiResponse<AssemblyDto>.Error($"Customer order with ID {request.OrderId} not found"));
            if (result.Status == "lineNotFound")
                return NotFound(ApiResponse<AssemblyDto>.Error($"Line with ID {request.LineId} not found on customer order {request.OrderId}"));

            return ToCreatedResponse(result, tag, AssemblyType.Assembly);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ZPM] Error creating assembly from order line");
            return StatusCode(500, ApiResponse<AssemblyDto>.Error("Error creating assembly from order line", new List<string> { ex.Message }));
        }
    }

    private IActionResult ToCreatedResponse(OperationResult<AssemblyDto> result, string tag, AssemblyType type)
    {
        string what = type == AssemblyType.Assembly ? "assembly" : "disassembly";
        switch (result.Status)
        {
            case "ok":
                _logger.LogInformation("[{Tag}] Created {Number}, Id={Id}", (object)tag, (object)(result.Message ?? ""), (object)result.Data!.Id);
                return CreatedAtAction(nameof(GetAssembly), new { id = result.Data.Id }, ApiResponse<AssemblyDto>.Ok(result.Data));
            case "productNotFound":
                return NotFound(ApiResponse<AssemblyDto>.Error("Product (komplet) not found"));
            case "warehouseNotFound":
                return NotFound(ApiResponse<AssemblyDto>.Error($"Warehouse '{result.Message}' not found"));
            case "unitNotFound":
                return BadRequest(ApiResponse<AssemblyDto>.Error($"Unit '{result.Message}' is not defined for the kit product"));
            case "managerNull":
                _logger.LogError("[{Tag}] Production orders manager not available", (object)tag);
                return StatusCode(500, ApiResponse<AssemblyDto>.Error($"{tag} manager not available", result.Errors));
            case "kitFailed":
                return BadRequest(ApiResponse<AssemblyDto>.Error($"Failed to populate {what} from kit definition: {result.Message}"));
            case "componentsFailed":
                return BadRequest(ApiResponse<AssemblyDto>.Error($"Failed to set {what} components", result.Errors));
            case "saveException":
                return BadRequest(ApiResponse<AssemblyDto>.Error($"Failed to save {what}: {result.Message}", result.Errors));
            case "saveFailed":
                _logger.LogWarning("[{Tag}] Zapisz() returned false. Errors: {Errors}", (object)tag, (object)string.Join("; ", result.Errors));
                return BadRequest(ApiResponse<AssemblyDto>.Error($"Failed to save {what}", result.Errors));
            default:
                return StatusCode(500, ApiResponse<AssemblyDto>.Error($"Unexpected status '{result.Status}'", result.Errors));
        }
    }

    #endregion

    #region Query

    /// <summary>
    /// List production orders (ZPM / ZPR). type = assemble | disassemble | all (default all).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<AssemblyListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAssemblies(
        [FromQuery] string? type,
        [FromQuery] int? productId,
        [FromQuery] string? warehouseSymbol,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        AssemblyType? typeFilter;
        try
        {
            typeFilter = AssemblyTypeCodes.Parse(type);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Error(ex.Message));
        }

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > 500) pageSize = 500;

        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var items = new List<AssemblyListItemDto>();

                foreach (var orderType in new[] { AssemblyType.Assembly, AssemblyType.Disassembly })
                {
                    if (typeFilter.HasValue && typeFilter.Value != orderType) continue;

                    dynamic? manager = GetProductionOrdersManager(orderType);
                    if (manager == null)
                    {
                        _logger.LogWarning("Production orders manager for {Type} not available", (object)orderType);
                        continue;
                    }

                    foreach (var entity in DynamicPropertyHelper.SafeGetAll((object)manager))
                    {
                        try
                        {
                            if (!MatchesFilters(entity, warehouseSymbol, dateFrom, dateTo, productId)) continue;
                            items.Add(MapToListItem(entity, orderType));
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("Failed to map production order: {Msg}", (object)ex.Message);
                        }
                    }
                }

                int totalCount = items.Count;
                var paged = items
                    .OrderByDescending(a => a.IssueDate)
                    .ThenByDescending(a => a.Id)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return (paged, totalCount);
            });

            return Ok(new PagedResponse<AssemblyListItemDto>
            {
                Data = result.paged,
                Page = page,
                PageSize = pageSize,
                TotalCount = result.totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting production orders");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving production orders", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get production order (ZPM or ZPR) by ID
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<AssemblyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AssemblyDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAssembly(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var found = FindProductionOrderEntity(id);
                if (found.entity == null) return (AssemblyDto?)null;
                AssemblyDto dto = MapToDto(found.entity, found.type);
                return (AssemblyDto?)dto;
            });

            if (result == null)
                return NotFound(ApiResponse<AssemblyDto>.Error($"Production order with ID {id} not found"));

            return Ok(ApiResponse<AssemblyDto>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting production order {Id}", id);
            return StatusCode(500, ApiResponse<AssemblyDto>.Error("Error retrieving production order", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Maximum number of kits assemblable from current component stock (SDK PodajMaksymalnaIloscKompletu).
    /// Creates a transient ZPM (never saved) for the calculation.
    /// </summary>
    [HttpGet("max-quantity")]
    [ProducesResponseType(typeof(ApiResponse<AssemblyMaxQuantityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AssemblyMaxQuantityDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<AssemblyMaxQuantityDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMaxQuantity(
        [FromQuery] int? productId,
        [FromQuery] string? productSymbol,
        [FromQuery] string? warehouseSymbol,
        [FromQuery] string? componentsWarehouseSymbol)
    {
        if (!productId.HasValue && string.IsNullOrWhiteSpace(productSymbol))
            return BadRequest(ApiResponse<AssemblyMaxQuantityDto>.Error("productId or productSymbol is required"));

        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                dynamic? produkt = FindAsortyment(productId, productSymbol, null);
                if (produkt == null) return OperationResult<AssemblyMaxQuantityDto>.Fail("productNotFound");

                dynamic? magazyn = null;
                if (!string.IsNullOrWhiteSpace(warehouseSymbol))
                {
                    magazyn = FindWarehouse(warehouseSymbol!);
                    if (magazyn == null) return OperationResult<AssemblyMaxQuantityDto>.Fail("warehouseNotFound", warehouseSymbol);
                }

                dynamic? magazynSkladnikow = magazyn;
                if (!string.IsNullOrWhiteSpace(componentsWarehouseSymbol))
                {
                    magazynSkladnikow = FindWarehouse(componentsWarehouseSymbol!);
                    if (magazynSkladnikow == null) return OperationResult<AssemblyMaxQuantityDto>.Fail("warehouseNotFound", componentsWarehouseSymbol);
                }

                dynamic? manager = GetProductionOrdersManager(AssemblyType.Assembly);
                if (manager == null) return OperationResult<AssemblyMaxQuantityDto>.Fail("managerNull");

                dynamic? zlecenie = null;
                try
                {
                    zlecenie = CreateProductionOrder(manager, AssemblyType.Assembly);
                    if (zlecenie == null) return OperationResult<AssemblyMaxQuantityDto>.Fail("managerNull");

                    if (magazyn != null)
                        ApplyWarehouses(zlecenie, magazyn, magazynSkladnikow ?? magazyn);

                    try
                    {
                        zlecenie.Montuj(produkt);
                    }
                    catch (Exception ex)
                    {
                        return OperationResult<AssemblyMaxQuantityDto>.Fail("kitFailed", ex.Message);
                    }

                    decimal maxQuantity;
                    try
                    {
                        maxQuantity = (decimal)zlecenie.PodajMaksymalnaIloscKompletu();
                    }
                    catch (Exception ex)
                    {
                        return OperationResult<AssemblyMaxQuantityDto>.Fail("calcFailed", ex.Message);
                    }

                    dynamic dane = zlecenie.Dane;
                    dynamic? pozycjaKomplet = DynamicPropertyHelper.GetProperty(dane, "PozycjaKomplet");
                    decimal kitQuantity = pozycjaKomplet != null ? DynamicPropertyHelper.GetDecimal(pozycjaKomplet, "Ilosc") : 0m;
                    if (kitQuantity <= 0) kitQuantity = 1m;

                    string effectiveComponentsWarehouse = ResolveWarehouseSymbol(dane, "MagazynSkladnikow")
                        ?? componentsWarehouseSymbol
                        ?? warehouseSymbol
                        ?? string.Empty;

                    var dto = new AssemblyMaxQuantityDto
                    {
                        ProductId = DynamicPropertyHelper.GetId(produkt),
                        ProductSymbol = DynamicPropertyHelper.GetString(produkt, "Symbol"),
                        ProductName = DynamicPropertyHelper.GetString(produkt, "Nazwa"),
                        WarehouseSymbol = ResolveWarehouseSymbol(dane, "Magazyn") ?? warehouseSymbol,
                        ComponentsWarehouseSymbol = string.IsNullOrEmpty(effectiveComponentsWarehouse) ? null : effectiveComponentsWarehouse,
                        MaxQuantity = maxQuantity,
                        Unit = pozycjaKomplet != null ? GetLineUnit(pozycjaKomplet) : GetProductBaseUnit(produkt)
                    };

                    foreach (dynamic poz in EnumerateComponentLines(dane))
                    {
                        dynamic? skladnik = DynamicPropertyHelper.GetProperty(poz, "AsortymentAktualny");
                        if (skladnik == null) continue;

                        decimal lineQty = DynamicPropertyHelper.GetDecimal(poz, "Ilosc");
                        decimal requiredPerKit = kitQuantity > 0 ? lineQty / kitQuantity : lineQty;

                        decimal? available = null;
                        try
                        {
                            available = _stockHelper.GetAvailableStock(skladnik, string.IsNullOrEmpty(effectiveComponentsWarehouse) ? null : effectiveComponentsWarehouse);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug("Stock lookup failed for component: {Msg}", (object)ex.Message);
                        }

                        dto.Components.Add(new AssemblyMaxQuantityComponentDto
                        {
                            ProductId = DynamicPropertyHelper.GetId(skladnik),
                            Symbol = DynamicPropertyHelper.GetString(skladnik, "Symbol"),
                            Name = DynamicPropertyHelper.GetString(skladnik, "Nazwa"),
                            RequiredPerKit = requiredPerKit,
                            Unit = GetLineUnit(poz),
                            AvailableStock = available,
                            MaxKitsFromComponent = available.HasValue && requiredPerKit > 0 ? Math.Floor(available.Value / requiredPerKit) : null
                        });
                    }

                    return OperationResult<AssemblyMaxQuantityDto>.Ok(dto);
                }
                finally
                {
                    // Transient object - never saved
                    DisposeQuietly(zlecenie);
                }
            });

            switch (result.Status)
            {
                case "ok":
                    return Ok(ApiResponse<AssemblyMaxQuantityDto>.Ok(result.Data!));
                case "productNotFound":
                    return NotFound(ApiResponse<AssemblyMaxQuantityDto>.Error("Product (komplet) not found"));
                case "warehouseNotFound":
                    return NotFound(ApiResponse<AssemblyMaxQuantityDto>.Error($"Warehouse '{result.Message}' not found"));
                case "managerNull":
                    return StatusCode(500, ApiResponse<AssemblyMaxQuantityDto>.Error("ZPM manager not available"));
                case "kitFailed":
                    return BadRequest(ApiResponse<AssemblyMaxQuantityDto>.Error($"Failed to populate from kit definition: {result.Message}"));
                default:
                    return BadRequest(ApiResponse<AssemblyMaxQuantityDto>.Error($"Failed to calculate max quantity: {result.Message}"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating max assembly quantity");
            return StatusCode(500, ApiResponse<AssemblyMaxQuantityDto>.Error("Error calculating max assembly quantity", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Component shortages of a production order.
    /// Primary source: SDK IBraki (Braki.Lista of IBrakujacaPozycja). Fallback: comparison of component quantities with current available stock.
    /// </summary>
    [HttpGet("{id:int}/shortages")]
    [ProducesResponseType(typeof(ApiResponse<AssemblyShortageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AssemblyShortageDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetShortages(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var found = FindProductionOrderEntity(id);
                if (found.entity == null) return OperationResult<AssemblyShortageDto>.Fail("notFound");

                dynamic entity = found.entity;
                string? componentsWarehouse = ResolveWarehouseSymbol(entity, "MagazynSkladnikow") ?? ResolveWarehouseSymbol(entity, "Magazyn");

                var dto = new AssemblyShortageDto
                {
                    AssemblyId = DynamicPropertyHelper.GetId(entity),
                    Number = GetDocumentNumber(entity),
                    Type = found.type,
                    ComponentsWarehouseSymbol = componentsWarehouse
                };

                bool sdkPathSucceeded = false;
                dynamic? zlecenie = null;
                try
                {
                    zlecenie = found.manager!.Znajdz(entity);
                    if (zlecenie != null)
                    {
                        dynamic? braki = DynamicPropertyHelper.GetProperty(zlecenie, "Braki");
                        if (braki != null)
                        {
                            sdkPathSucceeded = true;
                            bool hasShortages = DynamicPropertyHelper.GetBool(braki, "PosiadaBraki");
                            dynamic? lista = DynamicPropertyHelper.GetProperty(braki, "Lista");
                            if (lista != null)
                            {
                                foreach (dynamic brak in lista)
                                {
                                    AssemblyShortageItemDto item = MapShortageItem(brak);
                                    dto.Items.Add(item);
                                }
                            }
                            dto.HasShortages = hasShortages || dto.Items.Count > 0;
                            dto.Source = "sdk";
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to read Braki for production order {Id}: {Msg}", (object)id, (object)ex.Message);
                }
                finally
                {
                    DisposeQuietly(zlecenie);
                }

                if (!sdkPathSucceeded)
                {
                    // Fallback: compare component quantities against current available stock (IloscDostepna)
                    dto.Source = "stock-comparison";
                    foreach (dynamic poz in EnumerateComponentLines(entity))
                    {
                        dynamic? skladnik = DynamicPropertyHelper.GetProperty(poz, "AsortymentAktualny");
                        if (skladnik == null) continue;

                        decimal required = DynamicPropertyHelper.GetNullableDecimal(poz, "IloscSumaryczna")
                            ?? DynamicPropertyHelper.GetDecimal(poz, "Ilosc");
                        decimal available = 0m;
                        try
                        {
                            available = _stockHelper.GetAvailableStock(skladnik, componentsWarehouse);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug("Stock lookup failed for component: {Msg}", (object)ex.Message);
                        }

                        if (available >= required) continue;

                        dto.Items.Add(new AssemblyShortageItemDto
                        {
                            LineId = DynamicPropertyHelper.GetId(poz),
                            ProductId = DynamicPropertyHelper.GetId(skladnik),
                            Symbol = DynamicPropertyHelper.GetString(skladnik, "Symbol"),
                            Name = DynamicPropertyHelper.GetString(skladnik, "Nazwa"),
                            Unit = GetLineUnit(poz),
                            RequiredQuantity = required,
                            AllocatedQuantity = available,
                            MissingQuantity = required - available
                        });
                    }
                    dto.HasShortages = dto.Items.Count > 0;
                }

                return OperationResult<AssemblyShortageDto>.Ok(dto);
            });

            if (result.Status == "notFound")
                return NotFound(ApiResponse<AssemblyShortageDto>.Error($"Production order with ID {id} not found"));

            return Ok(ApiResponse<AssemblyShortageDto>.Ok(result.Data!));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting shortages for production order {Id}", id);
            return StatusCode(500, ApiResponse<AssemblyShortageDto>.Error("Error retrieving shortages", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Delete

    /// <summary>
    /// Delete a production order (ZPM or ZPR)
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAssembly(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var found = FindProductionOrderEntity(id);
                if (found.entity == null) return OperationResult<string>.Fail("notFound");

                string number = GetDocumentNumber(found.entity) ?? id.ToString();

                dynamic? zlecenie = null;
                try
                {
                    zlecenie = found.manager!.Znajdz(found.entity);
                    if (zlecenie == null) return OperationResult<string>.Fail("notFound");

                    // ASSUMED: IObiektBiznesowy.MoznaUsunac() is parameterless and returns bool (member name confirmed in CHM, signature not).
                    try
                    {
                        bool canDelete = (bool)zlecenie.MoznaUsunac();
                        if (!canDelete)
                        {
                            List<string> reasons = GetBusinessObjectErrors(zlecenie);
                            return OperationResult<string>.Fail("cannotDelete", number, reasons);
                        }
                    }
                    catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException ex)
                    {
                        _logger.LogDebug("MoznaUsunac() not bindable, proceeding with Usun(): {Msg}", (object)ex.Message);
                    }

                    bool deleted;
                    try
                    {
                        deleted = (bool)zlecenie.Usun();
                    }
                    catch (Exception ex)
                    {
                        return OperationResult<string>.Fail("deleteException", ex.Message);
                    }

                    if (!deleted)
                    {
                        List<string> deleteErrors = GetBusinessObjectErrors(zlecenie);
                        return OperationResult<string>.Fail("deleteFailed", number, deleteErrors);
                    }

                    return OperationResult<string>.Ok(number);
                }
                finally
                {
                    DisposeQuietly(zlecenie);
                }
            });

            switch (result.Status)
            {
                case "ok":
                    _logger.LogInformation("Deleted production order {Number} (Id={Id})", (object)(result.Data ?? ""), (object)id);
                    return Ok(ApiResponse<bool>.Ok(true, $"Production order {result.Data} deleted"));
                case "notFound":
                    return NotFound(ApiResponse<bool>.Error($"Production order with ID {id} not found"));
                case "cannotDelete":
                    return BadRequest(ApiResponse<bool>.Error($"Production order {result.Message} cannot be deleted", result.Errors));
                case "deleteException":
                    return BadRequest(ApiResponse<bool>.Error($"Failed to delete production order: {result.Message}"));
                default:
                    return BadRequest(ApiResponse<bool>.Error($"Failed to delete production order {result.Message}", result.Errors));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting production order {Id}", id);
            return StatusCode(500, ApiResponse<bool>.Error("Error deleting production order", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region SDK access helpers

    /// <summary>
    /// Resolves the ZPM / ZPR manager. Primary: ISferaService.GetManager (reflection over Uchwyt extension methods);
    /// fallback: Uchwyt.PodajObiektTypu with the interface full name.
    /// </summary>
    private dynamic? GetProductionOrdersManager(AssemblyType type)
    {
        string managerName = type == AssemblyType.Assembly ? "ZleceniaProdukcyjneMontowania" : "ZleceniaProdukcyjneRozkompletowania";
        string interfaceName = type == AssemblyType.Assembly
            ? "InsERT.Moria.Dokumenty.Logistyka.IZleceniaProdukcyjneMontowania"
            : "InsERT.Moria.Dokumenty.Logistyka.IZleceniaProdukcyjneRozkompletowania";

        try
        {
            dynamic? manager = _sferaService.GetManager(managerName);
            if (manager != null) return manager;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("GetManager({Manager}) failed: {Msg}", (object)managerName, (object)ex.Message);
        }

        try
        {
            dynamic sfera = _sferaService.GetSfera();
            return sfera.PodajObiektTypu(interfaceName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("PodajObiektTypu({Interface}) failed: {Msg}", (object)interfaceName, (object)ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Default document configuration for the order type.
    /// ASSUMED: Konfiguracje().DaneDomyslne exposes ZlecenieProdukcyjneMontowania / ZlecenieProdukcyjneRozkompletowania
    /// (pattern of DaneDomyslne.RozchodWewnetrzny used by WarehouseDocumentsController; the type is not in InsERT.Moria.API.XML).
    /// Verified fallback: IKonfiguracjeDane.WszystkieOTypieDokumentu(TypDokumentu) with the ZPM / ZPR enum values.
    /// </summary>
    private dynamic? GetDefaultConfiguration(AssemblyType type)
    {
        dynamic? konfiguracje = null;
        try
        {
            konfiguracje = _sferaService.GetManager("Konfiguracje");
        }
        catch (Exception ex)
        {
            _logger.LogDebug("GetManager(Konfiguracje) failed: {Msg}", (object)ex.Message);
        }
        if (konfiguracje == null) return null;

        try
        {
            dynamic? daneDomyslne = DynamicPropertyHelper.GetProperty(konfiguracje, "DaneDomyslne");
            if (daneDomyslne != null)
            {
                string[] candidates = type == AssemblyType.Assembly
                    ? new[] { "ZlecenieProdukcyjneMontowania", "ZlecenieProdukcyjneMontazu" }
                    : new[] { "ZlecenieProdukcyjneRozkompletowania", "ZlecenieProdukcyjneRozmontowania" };

                foreach (var name in candidates)
                {
                    dynamic? konfig = DynamicPropertyHelper.GetProperty(daneDomyslne, name);
                    if (konfig != null) return konfig;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("DaneDomyslne lookup failed: {Msg}", (object)ex.Message);
        }

        try
        {
            var typDokumentu = type == AssemblyType.Assembly
                ? InsERT.Moria.Dokumenty.Logistyka.TypDokumentu.ZlecenieProdukcyjneMontowania
                : InsERT.Moria.Dokumenty.Logistyka.TypDokumentu.ZlecenieProdukcyjneRozmontowania;

            dynamic? dane = DynamicPropertyHelper.GetProperty(konfiguracje, "Dane");
            if (dane != null)
            {
                dynamic? wszystkie = dane.WszystkieOTypieDokumentu(typDokumentu);
                if (wszystkie != null)
                {
                    foreach (dynamic konfig in wszystkie)
                    {
                        return konfig;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("WszystkieOTypieDokumentu lookup failed: {Msg}", (object)ex.Message);
        }

        return null;
    }

    private dynamic? CreateProductionOrder(dynamic manager, AssemblyType type)
    {
        dynamic? konfig = GetDefaultConfiguration(type);
        if (konfig != null)
        {
            try
            {
                return manager.Utworz(konfig);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Utworz(konfiguracja) failed, falling back to Utworz(): {Msg}", (object)ex.Message);
            }
        }
        return manager.Utworz();
    }

    /// <summary>
    /// Sets kit warehouse (Dokument.Magazyn), components warehouse (Dane.MagazynSkladnikow) and the profiling defaults.
    /// </summary>
    private void ApplyWarehouses(dynamic zlecenie, dynamic magazyn, dynamic magazynSkladnikow)
    {
        try
        {
            dynamic? profilowanie = DynamicPropertyHelper.GetProperty(zlecenie, "Profilowanie");
            if (profilowanie != null)
            {
                // Assembly: components issued (wydania) from the components warehouse, kit received (przyjecia) on the kit warehouse.
                // Disassembly: the kit is issued from the kit warehouse and components are received on the components warehouse.
                DynamicPropertyHelper.TrySetProperty(profilowanie, "MagazynWydan", (object)magazynSkladnikow);
                DynamicPropertyHelper.TrySetProperty(profilowanie, "MagazynPrzyjec", (object)magazyn);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Profilowanie warehouses not set: {Msg}", (object)ex.Message);
        }

        try
        {
            zlecenie.Dokument.Magazyn = magazyn;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Dokument.Magazyn assignment failed, trying Dane: {Msg}", (object)ex.Message);
            DynamicPropertyHelper.TrySetProperty(zlecenie.Dane, "Magazyn", (object)magazyn);
        }

        DynamicPropertyHelper.TrySetProperty(zlecenie.Dane, "MagazynSkladnikow", (object)magazynSkladnikow);
    }

    /// <summary>
    /// Sets kit quantity (and unit when requested). Unit change goes through IObslugaPozycjiDokumentu.ZmienJednostkePozycji
    /// on PozycjeSkladniki (ASSUMED to accept the kit line - falls back to assigning JednostkaMiaryAs on the entity).
    /// </summary>
    private List<string> SetKitQuantityAndUnit(dynamic zlecenie, decimal quantity, dynamic? kitUnit)
    {
        var errors = new List<string>();
        dynamic? pozycjaKomplet = DynamicPropertyHelper.GetProperty(zlecenie.Dane, "PozycjaKomplet");
        if (pozycjaKomplet == null)
        {
            errors.Add("Kit line (PozycjaKomplet) was not created by the SDK");
            return errors;
        }

        if (kitUnit != null)
        {
            bool unitChanged = false;
            try
            {
                dynamic pozycjeSkladniki = zlecenie.PozycjeSkladniki;
                InsERT.Moria.Dokumenty.Logistyka.OperacjaPrzeliczeniaCenyPoZmianieJednostki? priceOperation = null;
                pozycjeSkladniki.ZmienJednostkePozycji(pozycjaKomplet, kitUnit, true, priceOperation);
                unitChanged = true;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("ZmienJednostkePozycji on kit line failed, assigning JednostkaMiaryAs directly: {Msg}", (object)ex.Message);
            }

            if (!unitChanged)
            {
                unitChanged = DynamicPropertyHelper.TrySetProperty(pozycjaKomplet, "JednostkaMiaryAs", (object)kitUnit);
            }

            if (!unitChanged)
            {
                errors.Add("Failed to change kit line unit");
                return errors;
            }
        }

        bool quantitySet = false;
        try
        {
            pozycjaKomplet.Ilosc = quantity;
            quantitySet = true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Direct Ilosc assignment failed: {Msg}", (object)ex.Message);
        }
        if (!quantitySet)
            quantitySet = DynamicPropertyHelper.TrySetProperty(pozycjaKomplet, "Ilosc", quantity);

        if (!quantitySet)
        {
            errors.Add("Failed to set kit quantity");
            return errors;
        }

        try
        {
            zlecenie.Przelicz();
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Przelicz() after quantity change failed: {Msg}", (object)ex.Message);
        }

        return errors;
    }

    /// <summary>
    /// Replaces auto-populated component lines with the explicitly requested ones.
    /// ASSUMED: IObslugaPozycjiDokumentu exposes Usun(PozycjaDokumentu) (not present in InsERT.Moria.API.XML);
    /// if removal is not possible the operation is refused so no document with wrong components is created.
    /// </summary>
    private List<string> ReplaceComponents(dynamic zlecenie, List<AssemblyComponentRequest> components)
    {
        var errors = new List<string>();

        dynamic pozycjeSkladniki;
        try
        {
            pozycjeSkladniki = zlecenie.PozycjeSkladniki;
        }
        catch (Exception ex)
        {
            errors.Add($"PozycjeSkladniki not available: {ex.Message}");
            return errors;
        }

        var existing = new List<object>();
        foreach (dynamic poz in EnumerateComponentLines(zlecenie.Dane))
        {
            existing.Add((object)poz);
        }

        foreach (var poz in existing)
        {
            try
            {
                // (dynamic) cast: poz is statically typed object, which the runtime binder would not convert to PozycjaDokumentu
                pozycjeSkladniki.Usun((dynamic)poz);
            }
            catch (Exception ex)
            {
                errors.Add($"Custom components are not supported: SDK refused to remove kit-definition component ({ex.Message}). Use UseKompletDefinition=true.");
                return errors;
            }
        }

        foreach (var component in components)
        {
            dynamic? skladnik = FindAsortyment(component.ProductId, component.ProductSymbol, null);
            if (skladnik == null)
            {
                errors.Add($"Component product not found: {component.ProductId?.ToString() ?? component.ProductSymbol}");
                continue;
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(component.Unit))
                {
                    dynamic? jednostka = FindUnitOfMeasure(skladnik, component.Unit!);
                    if (jednostka == null)
                    {
                        errors.Add($"Unit '{component.Unit}' is not defined for component {DynamicPropertyHelper.GetString(skladnik, "Symbol")}");
                        continue;
                    }
                    pozycjeSkladniki.Dodaj(skladnik, component.Quantity, jednostka);
                }
                else
                {
                    int skladnikId = DynamicPropertyHelper.GetId(skladnik);
                    pozycjeSkladniki.Dodaj(skladnikId, component.Quantity);
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to add component {DynamicPropertyHelper.GetString(skladnik, "Symbol")}: {ex.Message}");
            }
        }

        return errors;
    }

    private void ApplyHeader(dynamic zlecenie, DateTime? issueDate, string? notes, bool reserveNumber, string tag)
    {
        if (issueDate.HasValue)
        {
            // Date before number reservation - numbering depends on the period
            bool dateSet = DynamicPropertyHelper.TrySetProperty(zlecenie.Dane, "DataWprowadzenia", issueDate.Value);
            DynamicPropertyHelper.TrySetProperty(zlecenie.Dane, "DataWydaniaWystawienia", issueDate.Value);
            if (!dateSet)
                _logger.LogWarning("[{Tag}] Failed to set DataWprowadzenia", (object)tag);
        }

        if (!string.IsNullOrEmpty(notes))
        {
            DynamicPropertyHelper.TrySetProperty(zlecenie.Dane, "Uwagi", notes);
        }

        if (reserveNumber)
        {
            try
            {
                zlecenie.ZarezerwujNumer();
                _logger.LogInformation("[{Tag}] Reserved number: {Number}", (object)tag, (object)(zlecenie.PodajPodgladNumeru()?.ToString() ?? ""));
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[{Tag}] ZarezerwujNumer() failed: {Msg}", (object)tag, (object)ex.Message);
            }
        }
    }

    private (bool saved, string? exception, List<string> errors) SaveBusinessObject(dynamic zlecenie, string tag)
    {
        try
        {
            bool saved = (bool)zlecenie.Zapisz();
            if (saved) return (true, null, new List<string>());

            List<string> errors = GetBusinessObjectErrors(zlecenie);
            _logger.LogWarning("[{Tag}] Zapisz() returned false: {Errors}", (object)tag, (object)string.Join("; ", errors));
            return (false, null, errors);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[{Tag}] Zapisz() threw: {Msg}", (object)tag, (object)ex.Message);
            List<string> errors = GetBusinessObjectErrors(zlecenie);
            return (false, ex.Message, errors);
        }
    }

    private static List<string> GetBusinessObjectErrors(dynamic obiekt)
    {
        var errors = new List<string>();

        void AddAll(dynamic? collection)
        {
            if (collection == null) return;
            try
            {
                foreach (dynamic item in collection)
                {
                    string text = (string)(item?.ToString() ?? "");
                    if (!string.IsNullOrWhiteSpace(text) && !errors.Contains(text))
                        errors.Add(text);
                }
            }
            catch
            {
                // ignore - best effort
            }
        }

        // (object?) casts keep the local-function calls statically bound
        try { AddAll((object?)obiekt.PodajBledy()); } catch { }
        try { AddAll((object?)obiekt.WalidujDane()); } catch { }
        try { AddAll((object?)DynamicPropertyHelper.GetProperty(obiekt, "InvalidData")); } catch { }

        return errors;
    }

    private static void DisposeQuietly(dynamic? obj)
    {
        if (obj == null) return;
        try
        {
            if ((object)obj is IDisposable disposable)
                disposable.Dispose();
        }
        catch
        {
            // ignore
        }
    }

    /// <summary>
    /// Finds a DokumentZPM entity by ID in the ZPM manager, then in the ZPR manager.
    /// </summary>
    private (dynamic? manager, dynamic? entity, AssemblyType type) FindProductionOrderEntity(int id)
    {
        foreach (var type in new[] { AssemblyType.Assembly, AssemblyType.Disassembly })
        {
            dynamic? manager = GetProductionOrdersManager(type);
            if (manager == null) continue;

            dynamic? entity = DynamicPropertyHelper.FindById((object)manager, id);
            if (entity != null) return (manager, entity, type);
        }
        return (null, null, AssemblyType.Assembly);
    }

    private dynamic? FindAsortyment(int? id, string? symbol, string? ean)
    {
        dynamic? asortymentyManager = _sferaService.GetManager("Asortymenty");
        if (asortymentyManager == null) return null;

        if (!string.IsNullOrEmpty(symbol))
        {
            try
            {
                dynamic? result = asortymentyManager.Dane.WyszukajPoSymbolu(symbol);
                if (result != null) return result;
            }
            catch { }
        }

        if (!string.IsNullOrEmpty(ean))
        {
            try
            {
                dynamic? result = asortymentyManager.Dane.WyszukajPoEAN(ean);
                if (result != null) return result;
            }
            catch { }
        }

        if (id.HasValue)
        {
            dynamic? byId = DynamicPropertyHelper.FindById((object)asortymentyManager, id.Value);
            if (byId != null) return byId;
        }

        if (string.IsNullOrEmpty(symbol) && string.IsNullOrEmpty(ean)) return null;

        foreach (var a in DynamicPropertyHelper.SafeGetAll((object)asortymentyManager))
        {
            if (!string.IsNullOrEmpty(symbol) && string.Equals(DynamicPropertyHelper.GetString(a, "Symbol"), symbol, StringComparison.OrdinalIgnoreCase))
                return a;

            if (!string.IsNullOrEmpty(ean) && DynamicPropertyHelper.GetString(a, "EAN") == ean)
                return a;
        }

        return null;
    }

    private dynamic? FindWarehouse(string symbol)
    {
        dynamic? magazynyManager = _sferaService.GetManager("Magazyny");
        if (magazynyManager == null) return null;

        foreach (var m in DynamicPropertyHelper.SafeGetAll((object)magazynyManager))
        {
            if (string.Equals(DynamicPropertyHelper.GetString(m, "Symbol"), symbol, StringComparison.OrdinalIgnoreCase))
                return m;
        }
        return null;
    }

    /// <summary>
    /// Finds the product's JednostkaMiaryAsortymentu whose JednostkaMiary.Symbol matches (case-insensitive).
    /// </summary>
    private static dynamic? FindUnitOfMeasure(dynamic asortyment, string unitSymbol)
    {
        try
        {
            dynamic? jednostki = DynamicPropertyHelper.GetProperty(asortyment, "JednostkiMiar");
            if (jednostki == null) return null;

            foreach (dynamic jm in jednostki)
            {
                string? symbol = DynamicPropertyHelper.GetString(jm, "JednostkaMiary", "Symbol");
                if (symbol != null && string.Equals(symbol.Trim(), unitSymbol.Trim(), StringComparison.OrdinalIgnoreCase))
                    return jm;
            }
        }
        catch
        {
            // ignore
        }
        return null;
    }

    #endregion

    #region Mapping helpers

    private static bool MatchesFilters(dynamic entity, string? warehouseSymbol, DateTime? dateFrom, DateTime? dateTo, int? productId)
    {
        if (dateFrom.HasValue || dateTo.HasValue)
        {
            DateTime? issueDate = DynamicPropertyHelper.GetDateTime(entity, "DataWprowadzenia");
            if (dateFrom.HasValue && (!issueDate.HasValue || issueDate.Value.Date < dateFrom.Value.Date)) return false;
            if (dateTo.HasValue && (!issueDate.HasValue || issueDate.Value.Date > dateTo.Value.Date)) return false;
        }

        if (!string.IsNullOrWhiteSpace(warehouseSymbol))
        {
            string? kitWarehouse = ResolveWarehouseSymbol(entity, "Magazyn");
            string? componentsWarehouse = ResolveWarehouseSymbol(entity, "MagazynSkladnikow");
            bool matches = string.Equals(kitWarehouse, warehouseSymbol, StringComparison.OrdinalIgnoreCase)
                || string.Equals(componentsWarehouse, warehouseSymbol, StringComparison.OrdinalIgnoreCase);
            if (!matches) return false;
        }

        if (productId.HasValue)
        {
            dynamic? pozycjaKomplet = DynamicPropertyHelper.GetProperty(entity, "PozycjaKomplet");
            dynamic? asortyment = pozycjaKomplet != null ? DynamicPropertyHelper.GetProperty(pozycjaKomplet, "AsortymentAktualny") : null;
            int kitProductId = asortyment != null ? DynamicPropertyHelper.GetId(asortyment) : 0;
            if (kitProductId != productId.Value) return false;
        }

        return true;
    }

    private static AssemblyListItemDto MapToListItem(dynamic entity, AssemblyType type)
    {
        dynamic? pozycjaKomplet = DynamicPropertyHelper.GetProperty(entity, "PozycjaKomplet");
        dynamic? asortyment = pozycjaKomplet != null ? DynamicPropertyHelper.GetProperty(pozycjaKomplet, "AsortymentAktualny") : null;

        int componentCount = 0;
        foreach (dynamic line in EnumerateComponentLines(entity)) componentCount++;

        return new AssemblyListItemDto
        {
            Id = DynamicPropertyHelper.GetId(entity),
            Number = GetDocumentNumber(entity),
            Type = type,
            ProductId = asortyment != null ? DynamicPropertyHelper.GetId(asortyment) : 0,
            ProductSymbol = asortyment != null ? DynamicPropertyHelper.GetString(asortyment, "Symbol") : null,
            ProductName = asortyment != null ? DynamicPropertyHelper.GetString(asortyment, "Nazwa") : null,
            Quantity = pozycjaKomplet != null ? DynamicPropertyHelper.GetDecimal(pozycjaKomplet, "Ilosc") : 0m,
            QuantityInBaseUnit = pozycjaKomplet != null ? DynamicPropertyHelper.GetDecimal(pozycjaKomplet, "IloscWJednostceBazowej") : 0m,
            Unit = pozycjaKomplet != null ? GetLineUnit(pozycjaKomplet) : (asortyment != null ? GetProductBaseUnit(asortyment) : DefaultUnit),
            WarehouseSymbol = ResolveWarehouseSymbol(entity, "Magazyn"),
            ComponentsWarehouseSymbol = ResolveWarehouseSymbol(entity, "MagazynSkladnikow"),
            IssueDate = DynamicPropertyHelper.GetDateTime(entity, "DataWprowadzenia") ?? DateTime.MinValue,
            Status = GetStatusName(entity),
            TotalCost = pozycjaKomplet != null ? DynamicPropertyHelper.GetNullableDecimal(pozycjaKomplet, "Wartosc", "NettoPoRabacie") : null,
            ComponentCount = componentCount
        };
    }

    private static AssemblyDto MapToDto(dynamic entity, AssemblyType type)
    {
        dynamic? pozycjaKomplet = DynamicPropertyHelper.GetProperty(entity, "PozycjaKomplet");
        dynamic? asortyment = pozycjaKomplet != null ? DynamicPropertyHelper.GetProperty(pozycjaKomplet, "AsortymentAktualny") : null;

        var components = new List<AssemblyComponentDto>();
        foreach (dynamic poz in EnumerateComponentLines(entity))
        {
            AssemblyComponentDto? component = MapComponent(poz);
            if (component != null) components.Add(component);
        }

        return new AssemblyDto
        {
            Id = DynamicPropertyHelper.GetId(entity),
            Number = GetDocumentNumber(entity),
            Type = type,
            Status = GetStatusName(entity),
            StatusSymbol = DynamicPropertyHelper.GetString(entity, "StatusDokumentu", "Symbol"),
            IssueDate = DynamicPropertyHelper.GetDateTime(entity, "DataWprowadzenia") ?? DateTime.MinValue,
            ReceiptDate = DynamicPropertyHelper.GetDateTime(entity, "DataPrzychodu"),
            IssueMovementDate = DynamicPropertyHelper.GetDateTime(entity, "DataRozchodu"),
            WarehouseSymbol = ResolveWarehouseSymbol(entity, "Magazyn"),
            WarehouseName = DynamicPropertyHelper.GetString(entity, "Magazyn", "Nazwa"),
            ComponentsWarehouseSymbol = ResolveWarehouseSymbol(entity, "MagazynSkladnikow"),
            ComponentsWarehouseName = DynamicPropertyHelper.GetString(entity, "MagazynSkladnikow", "Nazwa"),
            ProductId = asortyment != null ? DynamicPropertyHelper.GetId(asortyment) : 0,
            ProductSymbol = asortyment != null ? DynamicPropertyHelper.GetString(asortyment, "Symbol") : null,
            ProductName = asortyment != null ? DynamicPropertyHelper.GetString(asortyment, "Nazwa") : null,
            Quantity = pozycjaKomplet != null ? DynamicPropertyHelper.GetDecimal(pozycjaKomplet, "Ilosc") : 0m,
            QuantityInBaseUnit = pozycjaKomplet != null ? DynamicPropertyHelper.GetDecimal(pozycjaKomplet, "IloscWJednostceBazowej") : 0m,
            Unit = pozycjaKomplet != null ? GetLineUnit(pozycjaKomplet) : (asortyment != null ? GetProductBaseUnit(asortyment) : DefaultUnit),
            UnitCost = pozycjaKomplet != null ? DynamicPropertyHelper.GetNullableDecimal(pozycjaKomplet, "Cena", "NettoPoRabacie") : null,
            TotalCost = pozycjaKomplet != null ? DynamicPropertyHelper.GetNullableDecimal(pozycjaKomplet, "Wartosc", "NettoPoRabacie") : null,
            DocumentNetValue = DynamicPropertyHelper.GetNullableDecimal(entity, "Wartosc", "NettoPoRabacie"),
            Currency = DynamicPropertyHelper.GetString(entity, "Waluta", "Symbol"),
            AutoReceiptDocument = MapRelatedDocument(DynamicPropertyHelper.GetProperty(entity, "DokumentPrzychodujacyPW")),
            AutoIssueDocument = MapRelatedDocument(DynamicPropertyHelper.GetProperty(entity, "DokumentRozchodujacy")),
            Components = components,
            Notes = DynamicPropertyHelper.GetString(entity, "Uwagi"),
            Title = DynamicPropertyHelper.GetString(entity, "Tytul")
        };
    }

    private static AssemblyComponentDto? MapComponent(dynamic poz)
    {
        dynamic? skladnik = DynamicPropertyHelper.GetProperty(poz, "AsortymentAktualny");
        if (skladnik == null) return null;

        return new AssemblyComponentDto
        {
            LineId = DynamicPropertyHelper.GetId(poz),
            ProductId = DynamicPropertyHelper.GetId(skladnik),
            ProductSymbol = DynamicPropertyHelper.GetString(skladnik, "Symbol"),
            ProductName = DynamicPropertyHelper.GetString(skladnik, "Nazwa"),
            Quantity = DynamicPropertyHelper.GetDecimal(poz, "Ilosc"),
            QuantityInBaseUnit = DynamicPropertyHelper.GetDecimal(poz, "IloscWJednostceBazowej"),
            Unit = GetLineUnit(poz),
            TotalQuantity = DynamicPropertyHelper.GetNullableDecimal(poz, "IloscSumaryczna"),
            UnitCost = DynamicPropertyHelper.GetNullableDecimal(poz, "Cena", "NettoPoRabacie"),
            TotalCost = DynamicPropertyHelper.GetNullableDecimal(poz, "Wartosc", "NettoPoRabacie"),
            TotalValue = DynamicPropertyHelper.GetNullableDecimal(poz, "WartoscSumaryczna"),
            CostShare = DynamicPropertyHelper.GetNullableDecimal(poz, "UdzialKosztu")
        };
    }

    private static AssemblyShortageItemDto MapShortageItem(dynamic brak)
    {
        dynamic? poz = DynamicPropertyHelper.GetProperty(brak, "Pozycja");
        dynamic? skladnik = poz != null ? DynamicPropertyHelper.GetProperty(poz, "AsortymentAktualny") : null;

        decimal missing = DynamicPropertyHelper.GetDecimal(brak, "IloscBrakujaca");
        decimal allocated = DynamicPropertyHelper.GetDecimal(brak, "IloscZadysponowana");

        var item = new AssemblyShortageItemDto
        {
            LineId = poz != null ? DynamicPropertyHelper.GetId(poz) : 0,
            ProductId = skladnik != null ? DynamicPropertyHelper.GetId(skladnik) : 0,
            Symbol = skladnik != null ? DynamicPropertyHelper.GetString(skladnik, "Symbol") : null,
            Name = skladnik != null ? DynamicPropertyHelper.GetString(skladnik, "Nazwa") : null,
            Unit = poz != null ? GetLineUnit(poz) : null,
            RequiredQuantity = allocated + missing,
            AllocatedQuantity = allocated,
            MissingQuantity = missing,
            MissingQuantityInBaseUnit = DynamicPropertyHelper.GetNullableDecimal(brak, "IloscBrakujacaWJednostceBazowej")
        };

        try
        {
            dynamic? bledy = DynamicPropertyHelper.GetProperty(brak, "Bledy");
            if (bledy != null)
            {
                foreach (dynamic blad in bledy)
                {
                    string text = (string)(blad?.ToString() ?? "");
                    if (!string.IsNullOrWhiteSpace(text)) item.Errors.Add(text);
                }
            }
        }
        catch
        {
            // ignore
        }

        return item;
    }

    private static RelatedDocumentRefDto? MapRelatedDocument(dynamic? dokument)
    {
        if (dokument == null) return null;
        int id = DynamicPropertyHelper.GetId(dokument);
        if (id == 0) return null;
        return new RelatedDocumentRefDto
        {
            Id = id,
            Number = GetDocumentNumber(dokument)
        };
    }

    /// <summary>
    /// Component lines: DokumentZPM.PozycjaKomplet.PozycjeSkladnik (entity). Yields nothing when unavailable.
    /// </summary>
    private static IEnumerable<object> EnumerateComponentLines(dynamic entity)
    {
        var result = new List<object>();
        try
        {
            dynamic? pozycjaKomplet = DynamicPropertyHelper.GetProperty(entity, "PozycjaKomplet");
            dynamic? pozycje = pozycjaKomplet != null ? DynamicPropertyHelper.GetProperty(pozycjaKomplet, "PozycjeSkladnik") : null;
            if (pozycje != null)
            {
                foreach (dynamic poz in pozycje)
                {
                    result.Add((object)poz);
                }
            }
        }
        catch
        {
            // ignore
        }
        return result;
    }

    private static string? GetDocumentNumber(dynamic entity)
    {
        string? number = DynamicPropertyHelper.GetString(entity, "NumerWewnetrzny", "PelnaSygnatura");
        if (!string.IsNullOrWhiteSpace(number)) return number;
        return DynamicPropertyHelper.GetString(entity, "Symbol");
    }

    private static string? GetStatusName(dynamic entity)
    {
        return DynamicPropertyHelper.GetString(entity, "StatusDokumentu", "Nazwa")
            ?? DynamicPropertyHelper.GetString(entity, "StatusDokumentu", "Symbol");
    }

    /// <summary>
    /// Unit symbol of a document line: PozycjaDokumentu.JednostkaMiaryAs.JednostkaMiary.Symbol, fallback to the product's base unit.
    /// </summary>
    private static string GetLineUnit(dynamic poz)
    {
        dynamic? jednostkaAs = DynamicPropertyHelper.GetProperty(poz, "JednostkaMiaryAs");
        string? symbol = jednostkaAs != null ? DynamicPropertyHelper.GetString(jednostkaAs, "JednostkaMiary", "Symbol") : null;
        if (!string.IsNullOrWhiteSpace(symbol)) return symbol;

        dynamic? asortyment = DynamicPropertyHelper.GetProperty(poz, "AsortymentAktualny");
        return asortyment != null ? GetProductBaseUnit(asortyment) : DefaultUnit;
    }

    private static string GetProductBaseUnit(dynamic asortyment)
    {
        string? symbol = DynamicPropertyHelper.GetString(asortyment, "JednostkaMagazynowa", "Symbol");
        if (string.IsNullOrWhiteSpace(symbol))
        {
            dynamic? podstawowa = DynamicPropertyHelper.GetProperty(asortyment, "PodstawowaJednostkaMiaryAsortymentu");
            symbol = podstawowa != null ? DynamicPropertyHelper.GetString(podstawowa, "JednostkaMiary", "Symbol") : null;
        }
        return string.IsNullOrWhiteSpace(symbol) ? DefaultUnit : symbol;
    }

    private static string? ResolveWarehouseSymbol(dynamic entity, string propertyName)
    {
        return DynamicPropertyHelper.GetString(entity, propertyName, "Symbol");
    }

    #endregion
}
