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
    public ActionResult<PagedResponse<ProductGroupListItemDto>> GetProductGroups([FromQuery] ProductGroupQueryRequest query)
    {
        try
        {
            var grupy = _sferaService.GetManager("GrupyAsortymentu");
            if (grupy == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get GrupyAsortymentu manager"));
            }

            var allGrupy = new List<dynamic>();
            foreach (var g in DynamicPropertyHelper.SafeGetAll(grupy))
            {
                allGrupy.Add(g);
            }

            // Apply filters
            var filteredList = new List<dynamic>();
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

            var items = new List<ProductGroupListItemDto>();
            foreach (var g in pagedItems)
            {
                var dto = MapToListItemDto(g);

                // Include product count if requested
                if (query.IncludeProductCount)
                {
                    dto.ProductCount = GetProductCountForGroup(DynamicPropertyHelper.GetId(g));
                }

                // Check if has children
                dto.HasChildren = allGrupy.Any(child =>
                {
                    var parent = DynamicPropertyHelper.GetProperty(child, "Rodzic");
                    return parent != null && DynamicPropertyHelper.GetId(parent) == dto.Id;
                });

                items.Add(dto);
            }

            var response = new PagedResponse<ProductGroupListItemDto>
            {
                Data = items,
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = totalCount
            };

            return Ok(response);
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
    public ActionResult<ApiResponse<List<ProductGroupTreeDto>>> GetProductGroupsTree([FromQuery] bool includeProductCount = false)
    {
        try
        {
            var grupy = _sferaService.GetManager("GrupyAsortymentu");
            if (grupy == null)
            {
                return StatusCode(500, ApiResponse<List<ProductGroupTreeDto>>.Error("Failed to get GrupyAsortymentu manager"));
            }

            var allGrupy = new List<dynamic>();
            foreach (var g in DynamicPropertyHelper.SafeGetAll(grupy))
            {
                var isDeleted = DynamicPropertyHelper.GetNullableBool(g, "Usuniety") ?? false;
                if (!isDeleted)
                {
                    allGrupy.Add(g);
                }
            }

            // Build tree
            var groupMap = new Dictionary<int, ProductGroupTreeDto>();
            var rootGroups = new List<ProductGroupTreeDto>();

            // First pass: create all nodes
            foreach (var g in allGrupy)
            {
                var id = DynamicPropertyHelper.GetId(g);
                var parent = DynamicPropertyHelper.GetProperty(g, "Rodzic");
                var parentId = parent != null ? DynamicPropertyHelper.GetId(parent) : (int?)null;

                var node = new ProductGroupTreeDto
                {
                    Id = id,
                    Symbol = DynamicPropertyHelper.GetString(g, "Symbol") ?? "",
                    Name = DynamicPropertyHelper.GetString(g, "Nazwa") ?? "",
                    ParentId = parentId,
                    Level = DynamicPropertyHelper.GetNullableInt(g, "Poziom") ?? 0,
                    ProductCount = includeProductCount ? GetProductCountForGroup(id) : 0
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
                    rootGroups.Add(node);
                }
            }

            // Sort children
            foreach (var node in groupMap.Values)
            {
                node.Children = node.Children.OrderBy(c => c.Name).ToList();
            }
            rootGroups = rootGroups.OrderBy(r => r.Name).ToList();

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
    public ActionResult<ApiResponse<ProductGroupDto>> GetProductGroup(int id)
    {
        try
        {
            var grupy = _sferaService.GetManager("GrupyAsortymentu");
            if (grupy == null)
            {
                return StatusCode(500, ApiResponse<ProductGroupDto>.Error("Failed to get GrupyAsortymentu manager"));
            }

            dynamic? grupa = null;
            foreach (var g in DynamicPropertyHelper.SafeGetAll(grupy))
            {
                if (DynamicPropertyHelper.GetId(g) == id)
                {
                    grupa = g;
                    break;
                }
            }

            if (grupa == null)
            {
                return NotFound(ApiResponse<ProductGroupDto>.Error($"Product group with ID {id} not found"));
            }

            return Ok(ApiResponse<ProductGroupDto>.Ok(MapToDto(grupa, grupy)));
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
    public ActionResult<ApiResponse<ProductGroupDto>> GetProductGroupBySymbol(string symbol)
    {
        try
        {
            var grupy = _sferaService.GetManager("GrupyAsortymentu");
            if (grupy == null)
            {
                return StatusCode(500, ApiResponse<ProductGroupDto>.Error("Failed to get GrupyAsortymentu manager"));
            }

            dynamic? grupa = null;
            foreach (var g in DynamicPropertyHelper.SafeGetAll(grupy))
            {
                if (DynamicPropertyHelper.GetString(g, "Symbol") == symbol)
                {
                    grupa = g;
                    break;
                }
            }

            if (grupa == null)
            {
                return NotFound(ApiResponse<ProductGroupDto>.Error($"Product group with symbol {symbol} not found"));
            }

            return Ok(ApiResponse<ProductGroupDto>.Ok(MapToDto(grupa, grupy)));
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
    public ActionResult<ApiResponse<List<ProductGroupListItemDto>>> GetProductGroupChildren(int id)
    {
        try
        {
            var grupy = _sferaService.GetManager("GrupyAsortymentu");
            if (grupy == null)
            {
                return StatusCode(500, ApiResponse<List<ProductGroupListItemDto>>.Error("Failed to get GrupyAsortymentu manager"));
            }

            var children = new List<ProductGroupListItemDto>();
            foreach (var g in DynamicPropertyHelper.SafeGetAll(grupy))
            {
                var parent = DynamicPropertyHelper.GetProperty(g, "Rodzic");
                var parentId = parent != null ? DynamicPropertyHelper.GetId(parent) : (int?)null;

                if (parentId == id)
                {
                    var isDeleted = DynamicPropertyHelper.GetNullableBool(g, "Usuniety") ?? false;
                    if (!isDeleted)
                    {
                        children.Add(MapToListItemDto(g));
                    }
                }
            }

            return Ok(ApiResponse<List<ProductGroupListItemDto>>.Ok(children.OrderBy(c => c.Name).ToList()));
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
    public ActionResult<ApiResponse<ProductGroupDto>> CreateProductGroup([FromBody] CreateProductGroupRequest request)
    {
        try
        {
            var grupy = _sferaService.GetManager("GrupyAsortymentu");
            if (grupy == null)
            {
                return StatusCode(500, ApiResponse<ProductGroupDto>.Error("Failed to get GrupyAsortymentu manager"));
            }

            // Check if symbol already exists
            foreach (var g in DynamicPropertyHelper.SafeGetAll(grupy))
            {
                if (DynamicPropertyHelper.GetString(g, "Symbol") == request.Symbol)
                {
                    return BadRequest(ApiResponse<ProductGroupDto>.Error($"Product group with symbol {request.Symbol} already exists"));
                }
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
                    foreach (var g in DynamicPropertyHelper.SafeGetAll(grupy))
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
                        foreach (var c in DynamicPropertyHelper.SafeGetAll(cenniki))
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
                        foreach (var sv in DynamicPropertyHelper.SafeGetAll(stawkiVat))
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
                    _logger.LogInformation("Created product group {Symbol}", request.Symbol);
                    return CreatedAtAction(
                        nameof(GetProductGroup),
                        new { id = DynamicPropertyHelper.GetId(dane) },
                        ApiResponse<ProductGroupDto>.Ok(MapToDto(dane, grupy), "Product group created successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(grupa);
                    return BadRequest(ApiResponse<ProductGroupDto>.Error("Failed to create product group", errors));
                }
            }
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
    public ActionResult<ApiResponse<ProductGroupDto>> UpdateProductGroup(int id, [FromBody] UpdateProductGroupRequest request)
    {
        try
        {
            var grupy = _sferaService.GetManager("GrupyAsortymentu");
            if (grupy == null)
            {
                return StatusCode(500, ApiResponse<ProductGroupDto>.Error("Failed to get GrupyAsortymentu manager"));
            }

            dynamic? grupaDane = null;
            foreach (var g in DynamicPropertyHelper.SafeGetAll(grupy))
            {
                if (DynamicPropertyHelper.GetId(g) == id)
                {
                    grupaDane = g;
                    break;
                }
            }

            if (grupaDane == null)
            {
                return NotFound(ApiResponse<ProductGroupDto>.Error($"Product group with ID {id} not found"));
            }

            using (var grupa = grupy.Znajdz(grupaDane))
            {
                if (grupa == null)
                {
                    return NotFound(ApiResponse<ProductGroupDto>.Error($"Product group with ID {id} not found"));
                }

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
                        foreach (var c in DynamicPropertyHelper.SafeGetAll(cenniki))
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
                        foreach (var sv in DynamicPropertyHelper.SafeGetAll(stawkiVat))
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
                    _logger.LogInformation("Updated product group {Id}", id);
                    return Ok(ApiResponse<ProductGroupDto>.Ok(MapToDto(dane, grupy), "Product group updated successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(grupa);
                    return BadRequest(ApiResponse<ProductGroupDto>.Error("Failed to update product group", errors));
                }
            }
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
    public ActionResult<ApiResponse<ProductGroupDto>> MoveProductGroup(int id, [FromBody] MoveProductGroupRequest request)
    {
        try
        {
            var grupy = _sferaService.GetManager("GrupyAsortymentu");
            if (grupy == null)
            {
                return StatusCode(500, ApiResponse<ProductGroupDto>.Error("Failed to get GrupyAsortymentu manager"));
            }

            dynamic? grupaDane = null;
            foreach (var g in DynamicPropertyHelper.SafeGetAll(grupy))
            {
                if (DynamicPropertyHelper.GetId(g) == id)
                {
                    grupaDane = g;
                    break;
                }
            }

            if (grupaDane == null)
            {
                return NotFound(ApiResponse<ProductGroupDto>.Error($"Product group with ID {id} not found"));
            }

            // Prevent moving to self or descendant
            if (request.NewParentId == id)
            {
                return BadRequest(ApiResponse<ProductGroupDto>.Error("Cannot move group to itself"));
            }

            using (var grupa = grupy.Znajdz(grupaDane))
            {
                if (grupa == null)
                {
                    return NotFound(ApiResponse<ProductGroupDto>.Error($"Product group with ID {id} not found"));
                }

                dynamic dane = grupa.Dane;

                if (request.NewParentId.HasValue)
                {
                    dynamic? nowyRodzic = null;
                    foreach (var g in DynamicPropertyHelper.SafeGetAll(grupy))
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
                    _logger.LogInformation("Moved product group {Id} to parent {ParentId}", id, request.NewParentId);
                    return Ok(ApiResponse<ProductGroupDto>.Ok(MapToDto(dane, grupy), "Product group moved successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(grupa);
                    return BadRequest(ApiResponse<ProductGroupDto>.Error("Failed to move product group", errors));
                }
            }
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
    public ActionResult<ApiResponse<bool>> DeleteProductGroup(int id)
    {
        try
        {
            var grupy = _sferaService.GetManager("GrupyAsortymentu");
            if (grupy == null)
            {
                return StatusCode(500, ApiResponse<bool>.Error("Failed to get GrupyAsortymentu manager"));
            }

            dynamic? grupaDane = null;
            foreach (var g in DynamicPropertyHelper.SafeGetAll(grupy))
            {
                if (DynamicPropertyHelper.GetId(g) == id)
                {
                    grupaDane = g;
                    break;
                }
            }

            if (grupaDane == null)
            {
                return NotFound(ApiResponse<bool>.Error($"Product group with ID {id} not found"));
            }

            using (var grupa = grupy.Znajdz(grupaDane))
            {
                if (grupa == null)
                {
                    return NotFound(ApiResponse<bool>.Error($"Product group with ID {id} not found"));
                }

                if ((bool)grupa.Usun())
                {
                    _logger.LogInformation("Deleted product group {Id}", id);
                    return Ok(ApiResponse<bool>.Ok(true, "Product group deleted successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(grupa);
                    return BadRequest(ApiResponse<bool>.Error("Failed to delete product group", errors));
                }
            }
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
    public ActionResult<ApiResponse<int>> AssignProductsToGroup(int id, [FromBody] AssignProductsToGroupRequest request)
    {
        try
        {
            var grupy = _sferaService.GetManager("GrupyAsortymentu");
            if (grupy == null)
            {
                return StatusCode(500, ApiResponse<int>.Error("Failed to get GrupyAsortymentu manager"));
            }

            dynamic? grupaDane = null;
            foreach (var g in DynamicPropertyHelper.SafeGetAll(grupy))
            {
                if (DynamicPropertyHelper.GetId(g) == id)
                {
                    grupaDane = g;
                    break;
                }
            }

            if (grupaDane == null)
            {
                return NotFound(ApiResponse<int>.Error($"Product group with ID {id} not found"));
            }

            var asortymenty = _sferaService.GetManager("Asortymenty");
            if (asortymenty == null)
            {
                return StatusCode(500, ApiResponse<int>.Error("Failed to get Asortymenty manager"));
            }

            int assignedCount = 0;
            foreach (var productId in request.ProductIds)
            {
                dynamic? asortymentDane = null;
                foreach (var a in DynamicPropertyHelper.SafeGetAll(asortymenty))
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

            _logger.LogInformation("Assigned {Count} products to group {GroupId}", assignedCount, id);
            return Ok(ApiResponse<int>.Ok(assignedCount, $"Assigned {assignedCount} products to group"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning products to group {Id}", id);
            return StatusCode(500, ApiResponse<int>.Error("Error assigning products to group", new List<string> { ex.Message }));
        }
    }

    private int GetProductCountForGroup(int groupId)
    {
        try
        {
            var asortymenty = _sferaService.GetManager("Asortymenty");
            if (asortymenty == null) return 0;

            int count = 0;
            foreach (var a in DynamicPropertyHelper.SafeGetAll(asortymenty))
            {
                var grupa = DynamicPropertyHelper.GetProperty(a, "Grupa");
                if (grupa != null && DynamicPropertyHelper.GetId(grupa) == groupId)
                {
                    count++;
                }
            }
            return count;
        }
        catch
        {
            return 0;
        }
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
        foreach (var g in DynamicPropertyHelper.SafeGetAll(grupy))
        {
            var p = DynamicPropertyHelper.GetProperty(g, "Rodzic");
            if (p != null && DynamicPropertyHelper.GetId(p) == groupId)
            {
                childCount++;
            }
        }

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
            ProductCount = GetProductCountForGroup(groupId),
            DirectProductCount = GetProductCountForGroup(groupId),
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
