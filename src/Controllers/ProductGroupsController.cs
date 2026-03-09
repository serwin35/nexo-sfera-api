using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Requests;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Product Groups (Grupy Asortymentu) management endpoints
/// </summary>
[ApiController]
[Route("api/product-groups")]
[Authorize]
[Tags("Product Groups")]
public class ProductGroupsController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<ProductGroupsController> _logger;

    public ProductGroupsController(ISferaService sferaService, ILogger<ProductGroupsController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get all product groups with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResponse<ProductGroupListItemDto>>> GetProductGroups([FromQuery] ProductGroupQueryRequest query)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var grupy = _sferaService.GetManager("GrupyAsortymentu");
                if (grupy == null)
                    return (null, 0);

                var allGrupy = new List<object>();
                foreach (var g in DynamicPropertyHelper.SafeGetAll((object)grupy))
                {
                    allGrupy.Add(g);
                }

                // Apply filters
                var filteredList = new List<object>();
                foreach (var g in allGrupy)
                {
                    // Deleted filter
                    if (!query.IncludeDeleted)
                    {
                        var isDeleted = DynamicPropertyHelper.GetNullableBool(g, "Usuniety") ?? false;
                        if (isDeleted) continue;
                    }

                    // Inactive filter
                    if (!query.IncludeInactive)
                    {
                        var isActive = DynamicPropertyHelper.GetNullableBool(g, "Aktywny") ?? true;
                        if (!isActive) continue;
                    }

                    // Parent filter (root groups if ParentId not specified and not IncludeAllLevels)
                    if (!query.IncludeAllLevels)
                    {
                        var parent = DynamicPropertyHelper.GetProperty(g, "Rodzic");
                        var parentId = parent != null ? DynamicPropertyHelper.GetId(parent) : (int?)null;

                        if (query.ParentId.HasValue)
                        {
                            if (parentId != query.ParentId.Value) continue;
                        }
                        else
                        {
                            // Only root groups (no parent)
                            if (parentId.HasValue && parentId > 0) continue;
                        }
                    }

                    // Search filter
                    if (!string.IsNullOrEmpty(query.Search))
                    {
                        var searchLower = query.Search.ToLower();
                        var symbol = (DynamicPropertyHelper.GetString(g, "Symbol") ?? "").ToLower();
                        var nazwa = (DynamicPropertyHelper.GetString(g, "Nazwa") ?? "").ToLower();

                        if (!symbol.Contains(searchLower) && !nazwa.Contains(searchLower))
                            continue;
                    }

                    filteredList.Add(g);
                }

                var totalCount = filteredList.Count;
                var pagedItems = filteredList
                    .OrderBy(g => DynamicPropertyHelper.GetString(g, "Nazwa"))
                    .Skip((query.Page - 1) * query.PageSize)
                    .Take(query.PageSize)
                    .ToList();

                // Build product count cache if needed
                Dictionary<int, int>? productCountCache = null;
                if (query.IncludeProductCount)
                {
                    productCountCache = BuildProductCountCache(grupy);
                }

                var items = new List<ProductGroupListItemDto>();
                foreach (var g in pagedItems)
                {
                    var dto = MapToListItemDto(g);

                    // Include product count if requested
                    if (query.IncludeProductCount && productCountCache != null)
                    {
                        productCountCache.TryGetValue(dto.Id, out var count);
                        dto.ProductCount = count;
                    }

                    // Check if has children
                    dto.HasChildren = allGrupy.Any(child =>
                    {
                        var parent = DynamicPropertyHelper.GetProperty(child, "Rodzic");
                        return parent != null && DynamicPropertyHelper.GetId(parent) == dto.Id;
                    });

                    items.Add(dto);
                }

                return (items, totalCount);
            });

            if (result.Item1 == null)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get GrupyAsortymentu manager"));

            return Ok(new PagedResponse<ProductGroupListItemDto>
            {
                Data = result.Item1,
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = result.Item2
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product groups");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving product groups", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get product groups as a tree structure
    /// </summary>
    [HttpGet("tree")]
    public async Task<ActionResult<ApiResponse<List<ProductGroupTreeDto>>>> GetProductGroupsTree([FromQuery] bool includeProductCount = false)
    {
        try
        {
            var rootGroups = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var grupy = _sferaService.GetManager("GrupyAsortymentu");
                if (grupy == null)
                    return null;

                var allGrupy = new List<object>();
                foreach (var g in DynamicPropertyHelper.SafeGetAll((object)grupy))
                {
                    var isDeleted = DynamicPropertyHelper.GetNullableBool(g, "Usuniety") ?? false;
                    if (!isDeleted)
                    {
                        allGrupy.Add(g);
                    }
                }

                // Build product count cache if needed
                Dictionary<int, int>? productCountCache = null;
                if (includeProductCount)
                {
                    productCountCache = BuildProductCountCache(grupy);
                }

                // Build tree
                var groupMap = new Dictionary<int, ProductGroupTreeDto>();
                var result = new List<ProductGroupTreeDto>();

                // First pass: create all nodes
                foreach (var g in allGrupy)
                {
                    var id = DynamicPropertyHelper.GetId(g);
                    var parent = DynamicPropertyHelper.GetProperty(g, "Rodzic");
                    var parentId = parent != null ? DynamicPropertyHelper.GetId(parent) : (int?)null;

                    int count = 0;
                    if (includeProductCount && productCountCache != null)
                        productCountCache.TryGetValue(id, out count);

                    var node = new ProductGroupTreeDto
                    {
                        Id = id,
                        Symbol = DynamicPropertyHelper.GetString(g, "Symbol") ?? "",
                        Name = DynamicPropertyHelper.GetString(g, "Nazwa") ?? "",
                        ParentId = parentId,
                        Level = DynamicPropertyHelper.GetNullableInt(g, "Poziom") ?? 0,
                        ProductCount = count
                    };

                    groupMap[id] = node;
                }

                // Second pass: build hierarchy
                foreach (var node in groupMap.Values)
                {
                    if (node.ParentId.HasValue && node.ParentId.Value > 0 && groupMap.ContainsKey(node.ParentId.Value))
                    {
                        groupMap[node.ParentId.Value].Children.Add(node);
                    }
                    else
                    {
                        result.Add(node);
                    }
                }

                // Sort children
                foreach (var node in groupMap.Values)
                {
                    node.Children = node.Children.OrderBy(c => c.Name).ToList();
                }
                result = result.OrderBy(r => r.Name).ToList();

                return result;
            });

            if (rootGroups == null)
                return StatusCode(500, ApiResponse<List<ProductGroupTreeDto>>.Error("Failed to get GrupyAsortymentu manager"));

            return Ok(ApiResponse<List<ProductGroupTreeDto>>.Ok(rootGroups));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product groups tree");
            return StatusCode(500, ApiResponse<List<ProductGroupTreeDto>>.Error("Error retrieving product groups tree", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get product group by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<ProductGroupDto>>> GetProductGroup(int id)
    {
        try
        {
            var (managerNull, dto) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var grupy = _sferaService.GetManager("GrupyAsortymentu");
                if (grupy == null)
                    return (true, (ProductGroupDto?)null);

                dynamic? grupa = null;
                foreach (var g in DynamicPropertyHelper.SafeGetAll((object)grupy))
                {
                    if (DynamicPropertyHelper.GetId(g) == id)
                    {
                        grupa = g;
                        break;
                    }
                }

                if (grupa == null)
                    return (false, (ProductGroupDto?)null);

                return (false, MapToDto(grupa, grupy));
            });

            if (managerNull)
                return StatusCode(500, ApiResponse<ProductGroupDto>.Error("Failed to get GrupyAsortymentu manager"));

            if (dto == null)
                return NotFound(ApiResponse<ProductGroupDto>.Error($"Product group with ID {id} not found"));

            return Ok(ApiResponse<ProductGroupDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product group {Id}", id);
            return StatusCode(500, ApiResponse<ProductGroupDto>.Error("Error retrieving product group", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get product group by symbol
    /// </summary>
    [HttpGet("by-symbol/{symbol}")]
    public async Task<ActionResult<ApiResponse<ProductGroupDto>>> GetProductGroupBySymbol(string symbol)
    {
        try
        {
            var (managerNull, dto) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var grupy = _sferaService.GetManager("GrupyAsortymentu");
                if (grupy == null)
                    return (true, (ProductGroupDto?)null);

                dynamic? grupa = null;
                foreach (var g in DynamicPropertyHelper.SafeGetAll((object)grupy))
                {
                    if (DynamicPropertyHelper.GetString(g, "Symbol") == symbol)
                    {
                        grupa = g;
                        break;
                    }
                }

                if (grupa == null)
                    return (false, (ProductGroupDto?)null);

                return (false, MapToDto(grupa, grupy));
            });

            if (managerNull)
                return StatusCode(500, ApiResponse<ProductGroupDto>.Error("Failed to get GrupyAsortymentu manager"));

            if (dto == null)
                return NotFound(ApiResponse<ProductGroupDto>.Error($"Product group with symbol {symbol} not found"));

            return Ok(ApiResponse<ProductGroupDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product group by symbol {Symbol}", symbol);
            return StatusCode(500, ApiResponse<ProductGroupDto>.Error("Error retrieving product group", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get children of a product group
    /// </summary>
    [HttpGet("{id}/children")]
    public async Task<ActionResult<ApiResponse<List<ProductGroupListItemDto>>>> GetProductGroupChildren(int id)
    {
        try
        {
            var (managerNull, children) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var grupy = _sferaService.GetManager("GrupyAsortymentu");
                if (grupy == null)
                    return (true, (List<ProductGroupListItemDto>?)null);

                var result = new List<ProductGroupListItemDto>();
                foreach (var g in DynamicPropertyHelper.SafeGetAll((object)grupy))
                {
                    var parent = DynamicPropertyHelper.GetProperty(g, "Rodzic");
                    var parentId = parent != null ? DynamicPropertyHelper.GetId(parent) : (int?)null;

                    if (parentId == id)
                    {
                        var isDeleted = DynamicPropertyHelper.GetNullableBool(g, "Usuniety") ?? false;
                        if (!isDeleted)
                        {
                            result.Add(MapToListItemDto(g));
                        }
                    }
                }

                return (false, result);
            });

            if (managerNull)
                return StatusCode(500, ApiResponse<List<ProductGroupListItemDto>>.Error("Failed to get GrupyAsortymentu manager"));

            return Ok(ApiResponse<List<ProductGroupListItemDto>>.Ok(children!.OrderBy(c => c.Name).ToList()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting children for product group {Id}", id);
            return StatusCode(500, ApiResponse<List<ProductGroupListItemDto>>.Error("Error retrieving product group children", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create a new product group
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<ProductGroupDto>>> CreateProductGroup([FromBody] CreateProductGroupRequest request)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync<(string, ProductGroupDto?, List<string>?)>(() =>
            {
                var grupy = _sferaService.GetManager("GrupyAsortymentu");
                if (grupy == null)
                    return ("manager_null", (ProductGroupDto?)null, (List<string>?)null);

                // Check if symbol already exists
                foreach (var g in DynamicPropertyHelper.SafeGetAll((object)grupy))
                {
                    if (DynamicPropertyHelper.GetString(g, "Symbol") == request.Symbol)
                        return ("duplicate", (ProductGroupDto?)null, (List<string>?)null);
                }

                using (var grupa = grupy.Utworz())
                {
                    dynamic dane = grupa.Dane;
                    dane.Symbol = request.Symbol;
                    dane.Nazwa = request.Name;

                    if (!string.IsNullOrEmpty(request.Description))
                    {
                        try { dane.Opis = request.Description; } catch { }
                    }

                    // Set parent
                    if (request.ParentId.HasValue)
                    {
                        dynamic? rodzic = null;
                        foreach (var g in DynamicPropertyHelper.SafeGetAll((object)grupy))
                        {
                            if (DynamicPropertyHelper.GetId(g) == request.ParentId.Value)
                            {
                                rodzic = g;
                                break;
                            }
                        }
                        if (rodzic != null)
                        {
                            dane.Rodzic = rodzic;
                        }
                    }

                    // Set minimal margin
                    if (request.MinimalMarginId.HasValue)
                    {
                        try { dane.MinimalnaMarza = request.MinimalMarginId.Value; } catch { }
                    }

                    // Set default price level
                    if (request.DefaultPriceLevelId.HasValue)
                    {
                        var cenniki = _sferaService.GetManager("Cenniki");
                        if (cenniki != null)
                        {
                            foreach (var c in DynamicPropertyHelper.SafeGetAll((object)cenniki))
                            {
                                if (DynamicPropertyHelper.GetId(c) == request.DefaultPriceLevelId.Value)
                                {
                                    try { dane.DomyslnyPoziomCen = c; } catch { }
                                    break;
                                }
                            }
                        }
                    }

                    // Set default VAT rate
                    if (request.DefaultVatRateId.HasValue)
                    {
                        var stawkiVat = _sferaService.GetManager("StawkiVat");
                        if (stawkiVat != null)
                        {
                            foreach (var sv in DynamicPropertyHelper.SafeGetAll((object)stawkiVat))
                            {
                                if (DynamicPropertyHelper.GetId(sv) == request.DefaultVatRateId.Value)
                                {
                                    try { dane.DomyslnaStawkaVat = sv; } catch { }
                                    break;
                                }
                            }
                        }
                    }

                    if ((bool)grupa.Zapisz())
                    {
                        return ("ok", MapToDto(dane, grupy), (List<string>?)null);
                    }
                    else
                    {
                        var errors = GetBusinessObjectErrors(grupa);
                        return ("save_failed", (ProductGroupDto?)null, errors);
                    }
                }
            });

            if (result.Item1 == "manager_null")
                return StatusCode(500, ApiResponse<ProductGroupDto>.Error("Failed to get GrupyAsortymentu manager"));

            if (result.Item1 == "duplicate")
                return BadRequest(ApiResponse<ProductGroupDto>.Error($"Product group with symbol {request.Symbol} already exists"));

            if (result.Item1 == "save_failed")
                return BadRequest(ApiResponse<ProductGroupDto>.Error("Failed to create product group", result.Item3!));

            _logger.LogInformation("Created product group {Symbol}", request.Symbol);
            return CreatedAtAction(
                nameof(GetProductGroup),
                new { id = result.Item2!.Id },
                ApiResponse<ProductGroupDto>.Ok(result.Item2, "Product group created successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating product group");
            return StatusCode(500, ApiResponse<ProductGroupDto>.Error("Error creating product group", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Update an existing product group
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<ProductGroupDto>>> UpdateProductGroup(int id, [FromBody] UpdateProductGroupRequest request)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync<(string, ProductGroupDto?, List<string>?)>(() =>
            {
                var grupy = _sferaService.GetManager("GrupyAsortymentu");
                if (grupy == null)
                    return ("manager_null", (ProductGroupDto?)null, (List<string>?)null);

                dynamic? grupaDane = null;
                foreach (var g in DynamicPropertyHelper.SafeGetAll((object)grupy))
                {
                    if (DynamicPropertyHelper.GetId(g) == id)
                    {
                        grupaDane = g;
                        break;
                    }
                }

                if (grupaDane == null)
                    return ("not_found", (ProductGroupDto?)null, (List<string>?)null);

                using (var grupa = grupy.Znajdz(grupaDane))
                {
                    if (grupa == null)
                        return ("not_found", (ProductGroupDto?)null, (List<string>?)null);

                    dynamic dane = grupa.Dane;

                    if (!string.IsNullOrEmpty(request.Name))
                    {
                        dane.Nazwa = request.Name;
                    }

                    if (!string.IsNullOrEmpty(request.Description))
                    {
                        try { dane.Opis = request.Description; } catch { }
                    }

                    if (request.MinimalMarginId.HasValue)
                    {
                        try { dane.MinimalnaMarza = request.MinimalMarginId.Value; } catch { }
                    }

                    if (request.DefaultPriceLevelId.HasValue)
                    {
                        var cenniki = _sferaService.GetManager("Cenniki");
                        if (cenniki != null)
                        {
                            foreach (var c in DynamicPropertyHelper.SafeGetAll((object)cenniki))
                            {
                                if (DynamicPropertyHelper.GetId(c) == request.DefaultPriceLevelId.Value)
                                {
                                    try { dane.DomyslnyPoziomCen = c; } catch { }
                                    break;
                                }
                            }
                        }
                    }

                    if (request.DefaultVatRateId.HasValue)
                    {
                        var stawkiVat = _sferaService.GetManager("StawkiVat");
                        if (stawkiVat != null)
                        {
                            foreach (var sv in DynamicPropertyHelper.SafeGetAll((object)stawkiVat))
                            {
                                if (DynamicPropertyHelper.GetId(sv) == request.DefaultVatRateId.Value)
                                {
                                    try { dane.DomyslnaStawkaVat = sv; } catch { }
                                    break;
                                }
                            }
                        }
                    }

                    if (request.IsActive.HasValue)
                    {
                        try { dane.Aktywny = request.IsActive.Value; } catch { }
                    }

                    if ((bool)grupa.Zapisz())
                    {
                        return ("ok", MapToDto(dane, grupy), (List<string>?)null);
                    }
                    else
                    {
                        var errors = GetBusinessObjectErrors(grupa);
                        return ("save_failed", (ProductGroupDto?)null, errors);
                    }
                }
            });

            if (result.Item1 == "manager_null")
                return StatusCode(500, ApiResponse<ProductGroupDto>.Error("Failed to get GrupyAsortymentu manager"));

            if (result.Item1 == "not_found")
                return NotFound(ApiResponse<ProductGroupDto>.Error($"Product group with ID {id} not found"));

            if (result.Item1 == "save_failed")
                return BadRequest(ApiResponse<ProductGroupDto>.Error("Failed to update product group", result.Item3!));

            _logger.LogInformation("Updated product group {Id}", id);
            return Ok(ApiResponse<ProductGroupDto>.Ok(result.Item2!, "Product group updated successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating product group {Id}", id);
            return StatusCode(500, ApiResponse<ProductGroupDto>.Error("Error updating product group", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Move a product group to a new parent
    /// </summary>
    [HttpPost("{id}/move")]
    public async Task<ActionResult<ApiResponse<ProductGroupDto>>> MoveProductGroup(int id, [FromBody] MoveProductGroupRequest request)
    {
        // Prevent moving to self or descendant
        if (request.NewParentId == id)
        {
            return BadRequest(ApiResponse<ProductGroupDto>.Error("Cannot move group to itself"));
        }

        try
        {
            var result = await _sferaService.ExecuteWithLockAsync<(string, ProductGroupDto?, List<string>?)>(() =>
            {
                var grupy = _sferaService.GetManager("GrupyAsortymentu");
                if (grupy == null)
                    return ("manager_null", (ProductGroupDto?)null, (List<string>?)null);

                dynamic? grupaDane = null;
                foreach (var g in DynamicPropertyHelper.SafeGetAll((object)grupy))
                {
                    if (DynamicPropertyHelper.GetId(g) == id)
                    {
                        grupaDane = g;
                        break;
                    }
                }

                if (grupaDane == null)
                    return ("not_found", (ProductGroupDto?)null, (List<string>?)null);

                using (var grupa = grupy.Znajdz(grupaDane))
                {
                    if (grupa == null)
                        return ("not_found", (ProductGroupDto?)null, (List<string>?)null);

                    dynamic dane = grupa.Dane;

                    if (request.NewParentId.HasValue)
                    {
                        dynamic? nowyRodzic = null;
                        foreach (var g in DynamicPropertyHelper.SafeGetAll((object)grupy))
                        {
                            if (DynamicPropertyHelper.GetId(g) == request.NewParentId.Value)
                            {
                                nowyRodzic = g;
                                break;
                            }
                        }
                        if (nowyRodzic != null)
                        {
                            dane.Rodzic = nowyRodzic;
                        }
                    }
                    else
                    {
                        // Move to root
                        try { dane.Rodzic = null; } catch { }
                    }

                    if ((bool)grupa.Zapisz())
                    {
                        return ("ok", MapToDto(dane, grupy), (List<string>?)null);
                    }
                    else
                    {
                        var errors = GetBusinessObjectErrors(grupa);
                        return ("save_failed", (ProductGroupDto?)null, errors);
                    }
                }
            });

            if (result.Item1 == "manager_null")
                return StatusCode(500, ApiResponse<ProductGroupDto>.Error("Failed to get GrupyAsortymentu manager"));

            if (result.Item1 == "not_found")
                return NotFound(ApiResponse<ProductGroupDto>.Error($"Product group with ID {id} not found"));

            if (result.Item1 == "save_failed")
                return BadRequest(ApiResponse<ProductGroupDto>.Error("Failed to move product group", result.Item3!));

            _logger.LogInformation("Moved product group {Id} to parent {ParentId}", id, request.NewParentId);
            return Ok(ApiResponse<ProductGroupDto>.Ok(result.Item2!, "Product group moved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error moving product group {Id}", id);
            return StatusCode(500, ApiResponse<ProductGroupDto>.Error("Error moving product group", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Delete a product group
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteProductGroup(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var grupy = _sferaService.GetManager("GrupyAsortymentu");
                if (grupy == null)
                    return ("manager_null", (List<string>?)null);

                dynamic? grupaDane = null;
                foreach (var g in DynamicPropertyHelper.SafeGetAll((object)grupy))
                {
                    if (DynamicPropertyHelper.GetId(g) == id)
                    {
                        grupaDane = g;
                        break;
                    }
                }

                if (grupaDane == null)
                    return ("not_found", (List<string>?)null);

                using (var grupa = grupy.Znajdz(grupaDane))
                {
                    if (grupa == null)
                        return ("not_found", (List<string>?)null);

                    if ((bool)grupa.Usun())
                    {
                        return ("ok", (List<string>?)null);
                    }
                    else
                    {
                        var errors = GetBusinessObjectErrors(grupa);
                        return ("delete_failed", errors);
                    }
                }
            });

            if (result.Item1 == "manager_null")
                return StatusCode(500, ApiResponse<bool>.Error("Failed to get GrupyAsortymentu manager"));

            if (result.Item1 == "not_found")
                return NotFound(ApiResponse<bool>.Error($"Product group with ID {id} not found"));

            if (result.Item1 == "delete_failed")
                return BadRequest(ApiResponse<bool>.Error("Failed to delete product group", result.Item2!));

            _logger.LogInformation("Deleted product group {Id}", id);
            return Ok(ApiResponse<bool>.Ok(true, "Product group deleted successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting product group {Id}", id);
            return StatusCode(500, ApiResponse<bool>.Error("Error deleting product group", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Assign products to a group
    /// </summary>
    [HttpPost("{id}/assign-products")]
    public async Task<ActionResult<ApiResponse<int>>> AssignProductsToGroup(int id, [FromBody] AssignProductsToGroupRequest request)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var grupy = _sferaService.GetManager("GrupyAsortymentu");
                if (grupy == null)
                    return ("grupy_null", 0);

                dynamic? grupaDane = null;
                foreach (var g in DynamicPropertyHelper.SafeGetAll((object)grupy))
                {
                    if (DynamicPropertyHelper.GetId(g) == id)
                    {
                        grupaDane = g;
                        break;
                    }
                }

                if (grupaDane == null)
                    return ("not_found", 0);

                var asortymenty = _sferaService.GetManager("Asortymenty");
                if (asortymenty == null)
                    return ("asortymenty_null", 0);

                int assignedCount = 0;
                foreach (var productId in request.ProductIds)
                {
                    dynamic? asortymentDane = null;
                    foreach (var a in DynamicPropertyHelper.SafeGetAll((object)asortymenty))
                    {
                        if (DynamicPropertyHelper.GetId(a) == productId)
                        {
                            asortymentDane = a;
                            break;
                        }
                    }

                    if (asortymentDane != null)
                    {
                        try
                        {
                            using (var asortyment = asortymenty.Znajdz(asortymentDane))
                            {
                                if (asortyment != null)
                                {
                                    asortyment.Dane.Grupa = grupaDane;
                                    if ((bool)asortyment.Zapisz())
                                    {
                                        assignedCount++;
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // Continue with other products
                        }
                    }
                }

                return ("ok", assignedCount);
            });

            if (result.Item1 == "grupy_null")
                return StatusCode(500, ApiResponse<int>.Error("Failed to get GrupyAsortymentu manager"));

            if (result.Item1 == "not_found")
                return NotFound(ApiResponse<int>.Error($"Product group with ID {id} not found"));

            if (result.Item1 == "asortymenty_null")
                return StatusCode(500, ApiResponse<int>.Error("Failed to get Asortymenty manager"));

            _logger.LogInformation("Assigned {Count} products to group {GroupId}", result.Item2, id);
            return Ok(ApiResponse<int>.Ok(result.Item2, $"Assigned {result.Item2} products to group"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning products to group {Id}", id);
            return StatusCode(500, ApiResponse<int>.Error("Error assigning products to group", new List<string> { ex.Message }));
        }
    }

    private Dictionary<int, int> BuildProductCountCache(dynamic asortymenty)
    {
        var cache = new Dictionary<int, int>();
        try
        {
            var asortymentyManager = _sferaService.GetManager("Asortymenty");
            if (asortymentyManager == null) return cache;

            foreach (var a in DynamicPropertyHelper.SafeGetAll((object)asortymentyManager))
            {
                var grupa = DynamicPropertyHelper.GetProperty(a, "Grupa");
                if (grupa != null)
                {
                    var groupId = DynamicPropertyHelper.GetId(grupa);
                    cache.TryGetValue(groupId, out int current);
                    cache[groupId] = current + 1;
                }
            }
        }
        catch { }
        return cache;
    }

    private static ProductGroupListItemDto MapToListItemDto(dynamic grupa)
    {
        var parent = DynamicPropertyHelper.GetProperty(grupa, "Rodzic");

        return new ProductGroupListItemDto
        {
            Id = DynamicPropertyHelper.GetId(grupa),
            Symbol = DynamicPropertyHelper.GetString(grupa, "Symbol") ?? "",
            Name = DynamicPropertyHelper.GetString(grupa, "Nazwa") ?? "",
            ParentId = parent != null ? DynamicPropertyHelper.GetId(parent) : null,
            ParentSymbol = parent != null ? DynamicPropertyHelper.GetString(parent, "Symbol") : null,
            Level = DynamicPropertyHelper.GetNullableInt(grupa, "Poziom") ?? 0
        };
    }

    private ProductGroupDto MapToDto(dynamic grupa, dynamic grupy)
    {
        var parent = DynamicPropertyHelper.GetProperty(grupa, "Rodzic");
        var defaultPriceLevel = DynamicPropertyHelper.GetProperty(grupa, "DomyslnyPoziomCen");
        var defaultVatRate = DynamicPropertyHelper.GetProperty(grupa, "DomyslnaStawkaVat");
        var minimalMargin = DynamicPropertyHelper.GetProperty(grupa, "MinimalnaMarza");

        var groupId = DynamicPropertyHelper.GetId(grupa);

        // Count children
        int childCount = 0;
        foreach (var g in DynamicPropertyHelper.SafeGetAll((object)grupy))
        {
            var p = DynamicPropertyHelper.GetProperty(g, "Rodzic");
            if (p != null && DynamicPropertyHelper.GetId(p) == groupId)
            {
                childCount++;
            }
        }

        // Get product count from Asortymenty manager (already inside lock when called from action methods)
        int productCount = 0;
        try
        {
            var asortymentyManager = _sferaService.GetManager("Asortymenty");
            if (asortymentyManager != null)
            {
                foreach (var a in DynamicPropertyHelper.SafeGetAll((object)asortymentyManager))
                {
                    var g = DynamicPropertyHelper.GetProperty(a, "Grupa");
                    if (g != null && DynamicPropertyHelper.GetId(g) == groupId)
                        productCount++;
                }
            }
        }
        catch { }

        return new ProductGroupDto
        {
            // Identity
            Id = groupId,
            Symbol = DynamicPropertyHelper.GetString(grupa, "Symbol") ?? "",
            Name = DynamicPropertyHelper.GetString(grupa, "Nazwa") ?? "",
            Description = DynamicPropertyHelper.GetString(grupa, "Opis"),

            // Hierarchy
            ParentId = parent != null ? DynamicPropertyHelper.GetId(parent) : null,
            ParentSymbol = parent != null ? DynamicPropertyHelper.GetString(parent, "Symbol") : null,
            ParentName = parent != null ? DynamicPropertyHelper.GetString(parent, "Nazwa") : null,
            Level = DynamicPropertyHelper.GetNullableInt(grupa, "Poziom") ?? 0,
            Path = DynamicPropertyHelper.GetString(grupa, "Sciezka"),

            // Statistics
            ProductCount = productCount,
            DirectProductCount = productCount,
            ChildGroupCount = childCount,

            // Margins
            MinimalMarginId = minimalMargin != null ? DynamicPropertyHelper.GetId(minimalMargin) : null,
            MinimalMarginPercent = minimalMargin != null ? DynamicPropertyHelper.GetDecimal(minimalMargin, "Procent") : null,

            // Pricing
            DefaultPriceLevelId = defaultPriceLevel != null ? DynamicPropertyHelper.GetId(defaultPriceLevel) : null,
            DefaultPriceLevelName = defaultPriceLevel != null ? DynamicPropertyHelper.GetString(defaultPriceLevel, "Nazwa") : null,

            // VAT
            DefaultVatRateId = defaultVatRate != null ? DynamicPropertyHelper.GetId(defaultVatRate) : null,
            DefaultVatRateSymbol = defaultVatRate != null ? DynamicPropertyHelper.GetString(defaultVatRate, "Symbol") : null,

            // Status
            IsActive = DynamicPropertyHelper.GetNullableBool(grupa, "Aktywny") ?? true,
            IsDeleted = DynamicPropertyHelper.GetNullableBool(grupa, "Usuniety") ?? false,

            // Timestamps
            CreatedAt = DynamicPropertyHelper.GetDateTime(grupa, "DataUtworzenia"),
            ModifiedAt = DynamicPropertyHelper.GetDateTime(grupa, "DataModyfikacji")
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
}
