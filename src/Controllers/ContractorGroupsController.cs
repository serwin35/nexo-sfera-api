using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Requests;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Contractor Groups (Grupy Kontrahentów) management endpoints
/// </summary>
[ApiController]
[Route("api/contractor-groups")]
[Authorize]
[Tags("Contractor Groups")]
public class ContractorGroupsController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<ContractorGroupsController> _logger;

    public ContractorGroupsController(ISferaService sferaService, ILogger<ContractorGroupsController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get all contractor groups with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResponse<ContractorGroupListItemDto>>> GetContractorGroups([FromQuery] ContractorGroupQueryRequest query)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var grupy = _sferaService.GetManager("Grupy");
                if (grupy == null)
                {
                    return (managerNull: true, response: (PagedResponse<ContractorGroupListItemDto>?)null);
                }

                var allGrupy = DynamicPropertyHelper.SafeGetAll((object)grupy);

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

                var items = new List<ContractorGroupListItemDto>();
                foreach (var g in pagedItems)
                {
                    var dto = MapToListItemDto(g);

                    // Include contractor count if requested
                    if (query.IncludeContractorCount)
                    {
                        dto.ContractorCount = GetContractorCountForGroup(DynamicPropertyHelper.GetId(g), grupy);
                    }

                    // Check if has children
                    dto.HasChildren = false;
                    foreach (var child in allGrupy)
                    {
                        var parent = DynamicPropertyHelper.GetProperty(child, "Rodzic");
                        if (parent != null && DynamicPropertyHelper.GetId(parent) == dto.Id)
                        {
                            dto.HasChildren = true;
                            break;
                        }
                    }

                    items.Add(dto);
                }

                return (managerNull: false, response: new PagedResponse<ContractorGroupListItemDto>
                {
                    Data = items,
                    Page = query.Page,
                    PageSize = query.PageSize,
                    TotalCount = totalCount
                });
            });

            if (result.managerNull)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Grupy manager"));
            }

            return Ok(result.response!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting contractor groups");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving contractor groups", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get contractor groups as a tree structure
    /// </summary>
    [HttpGet("tree")]
    public async Task<ActionResult<ApiResponse<List<ContractorGroupTreeDto>>>> GetContractorGroupsTree([FromQuery] bool includeContractorCount = false)
    {
        try
        {
            var rootGroups = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var grupy = _sferaService.GetManager("Grupy");
                if (grupy == null)
                {
                    return null;
                }

                var allGrupyRaw = DynamicPropertyHelper.SafeGetAll((object)grupy);
                var allGrupy = new List<object>();
                foreach (var g in allGrupyRaw)
                {
                    if (!(DynamicPropertyHelper.GetNullableBool(g, "Usuniety") ?? false))
                    {
                        allGrupy.Add(g);
                    }
                }

                // Build tree
                var groupMap = new Dictionary<int, ContractorGroupTreeDto>();
                var roots = new List<ContractorGroupTreeDto>();

                // First pass: create all nodes
                foreach (var g in allGrupy)
                {
                    var id = DynamicPropertyHelper.GetId(g);
                    var parent = DynamicPropertyHelper.GetProperty(g, "Rodzic");
                    var parentId = parent != null ? DynamicPropertyHelper.GetId(parent) : (int?)null;

                    var node = new ContractorGroupTreeDto
                    {
                        Id = id,
                        Symbol = DynamicPropertyHelper.GetString(g, "Symbol") ?? "",
                        Name = DynamicPropertyHelper.GetString(g, "Nazwa") ?? "",
                        ParentId = parentId,
                        Level = DynamicPropertyHelper.GetNullableInt(g, "Poziom") ?? 0,
                        ContractorCount = includeContractorCount ? GetContractorCountForGroup(id, grupy) : 0
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
                        roots.Add(node);
                    }
                }

                // Sort children
                foreach (var node in groupMap.Values)
                {
                    node.Children = node.Children.OrderBy(c => c.Name).ToList();
                }
                roots = roots.OrderBy(r => r.Name).ToList();

                return roots;
            });

            if (rootGroups == null)
            {
                return StatusCode(500, ApiResponse<List<ContractorGroupTreeDto>>.Error("Failed to get Grupy manager"));
            }

            return Ok(ApiResponse<List<ContractorGroupTreeDto>>.Ok(rootGroups));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting contractor groups tree");
            return StatusCode(500, ApiResponse<List<ContractorGroupTreeDto>>.Error("Error retrieving contractor groups tree", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get contractor group by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<ContractorGroupDto>>> GetContractorGroup(int id)
    {
        try
        {
            var (managerNull, notFound, dto) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var grupy = _sferaService.GetManager("Grupy");
                if (grupy == null)
                {
                    return (true, false, (ContractorGroupDto?)null);
                }

                var grupa = DynamicPropertyHelper.FindById(grupy, id);
                if (grupa == null)
                {
                    return (false, true, (ContractorGroupDto?)null);
                }

                return (false, false, MapToDto(grupa, grupy));
            });

            if (managerNull)
            {
                return StatusCode(500, ApiResponse<ContractorGroupDto>.Error("Failed to get Grupy manager"));
            }

            if (notFound)
            {
                return NotFound(ApiResponse<ContractorGroupDto>.Error($"Contractor group with ID {id} not found"));
            }

            return Ok(ApiResponse<ContractorGroupDto>.Ok(dto!));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting contractor group {Id}", id);
            return StatusCode(500, ApiResponse<ContractorGroupDto>.Error("Error retrieving contractor group", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get contractor group by symbol
    /// </summary>
    [HttpGet("by-symbol/{symbol}")]
    public async Task<ActionResult<ApiResponse<ContractorGroupDto>>> GetContractorGroupBySymbol(string symbol)
    {
        try
        {
            var (managerNull, notFound, dto) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var grupy = _sferaService.GetManager("Grupy");
                if (grupy == null)
                {
                    return (true, false, (ContractorGroupDto?)null);
                }

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
                {
                    return (false, true, (ContractorGroupDto?)null);
                }

                return (false, false, MapToDto(grupa, grupy));
            });

            if (managerNull)
            {
                return StatusCode(500, ApiResponse<ContractorGroupDto>.Error("Failed to get Grupy manager"));
            }

            if (notFound)
            {
                return NotFound(ApiResponse<ContractorGroupDto>.Error($"Contractor group with symbol {symbol} not found"));
            }

            return Ok(ApiResponse<ContractorGroupDto>.Ok(dto!));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting contractor group by symbol {Symbol}", symbol);
            return StatusCode(500, ApiResponse<ContractorGroupDto>.Error("Error retrieving contractor group", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get children of a contractor group
    /// </summary>
    [HttpGet("{id}/children")]
    public async Task<ActionResult<ApiResponse<List<ContractorGroupListItemDto>>>> GetContractorGroupChildren(int id)
    {
        try
        {
            var children = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var grupy = _sferaService.GetManager("Grupy");
                if (grupy == null)
                {
                    return null;
                }

                var result = new List<ContractorGroupListItemDto>();
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

                return result.OrderBy(c => c.Name).ToList();
            });

            if (children == null)
            {
                return StatusCode(500, ApiResponse<List<ContractorGroupListItemDto>>.Error("Failed to get Grupy manager"));
            }

            return Ok(ApiResponse<List<ContractorGroupListItemDto>>.Ok(children));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting children for contractor group {Id}", id);
            return StatusCode(500, ApiResponse<List<ContractorGroupListItemDto>>.Error("Error retrieving contractor group children", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get contractors in a group
    /// </summary>
    [HttpGet("{id}/contractors")]
    public async Task<ActionResult<PagedResponse<CustomerListItemDto>>> GetGroupContractors(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var grupy = _sferaService.GetManager("Grupy");
                if (grupy == null)
                {
                    return (managerNull: true, notFound: false, response: (PagedResponse<CustomerListItemDto>?)null);
                }

                // Verify group exists
                var grupaDane = DynamicPropertyHelper.FindById(grupy, id);
                if (grupaDane == null)
                {
                    return (false, true, (PagedResponse<CustomerListItemDto>?)null);
                }

                var podmioty = _sferaService.GetManager("Podmioty");
                if (podmioty == null)
                {
                    return (true, false, (PagedResponse<CustomerListItemDto>?)null);
                }

                var contractors = new List<object>();
                foreach (var p in DynamicPropertyHelper.SafeGetAll((object)podmioty))
                {
                    var grupa = DynamicPropertyHelper.GetProperty(p, "Grupa");
                    if (grupa != null && DynamicPropertyHelper.GetId(grupa) == id)
                    {
                        contractors.Add(p);
                    }
                }

                var totalCount = contractors.Count;
                var pagedItems = contractors
                    .OrderBy(p => DynamicPropertyHelper.GetString(p, "NazwaSkrocona"))
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var items = new List<CustomerListItemDto>();
                foreach (var p in pagedItems)
                {
                    items.Add(new CustomerListItemDto
                    {
                        Id = DynamicPropertyHelper.GetId(p),
                        Name = DynamicPropertyHelper.GetString(p, "NazwaSkrocona") ?? "",
                        TaxId = DynamicPropertyHelper.GetString(p, "NIP"),
                        Phone = DynamicPropertyHelper.GetString(p, "Telefon"),
                        IsActive = DynamicPropertyHelper.GetNullableBool(p, "Aktywny") ?? true,
                        ContractorType = (ContractorType)(DynamicPropertyHelper.GetNullableInt(p, "RodzajKontrahenta") ?? 0)
                    });
                }

                return (false, false, new PagedResponse<CustomerListItemDto>
                {
                    Data = items,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                });
            });

            if (result.managerNull)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Grupy manager"));
            }

            if (result.notFound)
            {
                return NotFound(ApiResponse<object>.Error($"Contractor group with ID {id} not found"));
            }

            return Ok(result.response!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting contractors for group {Id}", id);
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving group contractors", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create a new contractor group
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<ContractorGroupDto>>> CreateContractorGroup([FromBody] CreateContractorGroupRequest request)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var grupy = _sferaService.GetManager("Grupy");
                if (grupy == null)
                {
                    return (status: "managerNull", id: 0, dto: (ContractorGroupDto?)null, errors: (List<string>?)null);
                }

                // Check if symbol already exists
                foreach (var g in DynamicPropertyHelper.SafeGetAll((object)grupy))
                {
                    if (DynamicPropertyHelper.GetString(g, "Symbol") == request.Symbol)
                    {
                        return ("symbolExists", 0, (ContractorGroupDto?)null, (List<string>?)null);
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
                        var rodzic = DynamicPropertyHelper.FindById(grupy, request.ParentId.Value);
                        if (rodzic != null)
                        {
                            dane.Rodzic = rodzic;
                        }
                    }

                    // Set default payment method
                    if (request.DefaultPaymentMethodId.HasValue)
                    {
                        var sposobyPlatnosci = _sferaService.GetManager("SposobyPlatnosci");
                        if (sposobyPlatnosci != null)
                        {
                            var sp = DynamicPropertyHelper.FindById(sposobyPlatnosci, request.DefaultPaymentMethodId.Value);
                            if (sp != null)
                            {
                                try { dane.DomyslnySposobPlatnosci = sp; } catch { }
                            }
                        }
                    }

                    // Set default payment days
                    if (request.DefaultPaymentDays.HasValue)
                    {
                        try { dane.DomyslnyTerminPlatnosci = request.DefaultPaymentDays.Value; } catch { }
                    }

                    // Set default discount
                    if (request.DefaultDiscountPercent.HasValue)
                    {
                        try { dane.DomyslnyRabatProcent = request.DefaultDiscountPercent.Value; } catch { }
                    }

                    // Set default price level
                    if (request.DefaultPriceLevelId.HasValue)
                    {
                        var cenniki = _sferaService.GetManager("Cenniki");
                        if (cenniki != null)
                        {
                            var c = DynamicPropertyHelper.FindById(cenniki, request.DefaultPriceLevelId.Value);
                            if (c != null)
                            {
                                try { dane.DomyslnyPoziomCen = c; } catch { }
                            }
                        }
                    }

                    // Set default credit limit
                    if (request.DefaultCreditLimit.HasValue)
                    {
                        try { dane.DomyslnyLimitKredytu = request.DefaultCreditLimit.Value; } catch { }
                    }

                    if ((bool)grupa.Zapisz())
                    {
                        var newId = DynamicPropertyHelper.GetId(dane);
                        return ("created", newId, MapToDto(dane, grupy), (List<string>?)null);
                    }
                    else
                    {
                        var errors = GetBusinessObjectErrors(grupa);
                        return ("saveFailed", 0, (ContractorGroupDto?)null, errors);
                    }
                }
            });

            if (result.status == "managerNull")
            {
                return StatusCode(500, ApiResponse<ContractorGroupDto>.Error("Failed to get Grupy manager"));
            }

            if (result.status == "symbolExists")
            {
                return BadRequest(ApiResponse<ContractorGroupDto>.Error($"Contractor group with symbol {request.Symbol} already exists"));
            }

            if (result.status == "saveFailed")
            {
                return BadRequest(ApiResponse<ContractorGroupDto>.Error("Failed to create contractor group", result.errors));
            }

            _logger.LogInformation("Created contractor group {Symbol}", request.Symbol);
            return CreatedAtAction(
                nameof(GetContractorGroup),
                new { id = result.id },
                ApiResponse<ContractorGroupDto>.Ok(result.dto!, "Contractor group created successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating contractor group");
            return StatusCode(500, ApiResponse<ContractorGroupDto>.Error("Error creating contractor group", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Update an existing contractor group
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<ContractorGroupDto>>> UpdateContractorGroup(int id, [FromBody] UpdateContractorGroupRequest request)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var grupy = _sferaService.GetManager("Grupy");
                if (grupy == null)
                {
                    return (status: "managerNull", dto: (ContractorGroupDto?)null, errors: (List<string>?)null);
                }

                var grupaDane = DynamicPropertyHelper.FindById(grupy, id);
                if (grupaDane == null)
                {
                    return ("notFound", (ContractorGroupDto?)null, (List<string>?)null);
                }

                using (var grupa = grupy.Znajdz(grupaDane))
                {
                    if (grupa == null)
                    {
                        return ("notFound", (ContractorGroupDto?)null, (List<string>?)null);
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

                    if (request.DefaultPaymentMethodId.HasValue)
                    {
                        var sposobyPlatnosci = _sferaService.GetManager("SposobyPlatnosci");
                        if (sposobyPlatnosci != null)
                        {
                            var sp = DynamicPropertyHelper.FindById(sposobyPlatnosci, request.DefaultPaymentMethodId.Value);
                            if (sp != null)
                            {
                                try { dane.DomyslnySposobPlatnosci = sp; } catch { }
                            }
                        }
                    }

                    if (request.DefaultPaymentDays.HasValue)
                    {
                        try { dane.DomyslnyTerminPlatnosci = request.DefaultPaymentDays.Value; } catch { }
                    }

                    if (request.DefaultDiscountPercent.HasValue)
                    {
                        try { dane.DomyslnyRabatProcent = request.DefaultDiscountPercent.Value; } catch { }
                    }

                    if (request.DefaultPriceLevelId.HasValue)
                    {
                        var cenniki = _sferaService.GetManager("Cenniki");
                        if (cenniki != null)
                        {
                            var c = DynamicPropertyHelper.FindById(cenniki, request.DefaultPriceLevelId.Value);
                            if (c != null)
                            {
                                try { dane.DomyslnyPoziomCen = c; } catch { }
                            }
                        }
                    }

                    if (request.DefaultCreditLimit.HasValue)
                    {
                        try { dane.DomyslnyLimitKredytu = request.DefaultCreditLimit.Value; } catch { }
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
                        return ("saveFailed", (ContractorGroupDto?)null, errors);
                    }
                }
            });

            if (result.status == "managerNull")
            {
                return StatusCode(500, ApiResponse<ContractorGroupDto>.Error("Failed to get Grupy manager"));
            }

            if (result.status == "notFound")
            {
                return NotFound(ApiResponse<ContractorGroupDto>.Error($"Contractor group with ID {id} not found"));
            }

            if (result.status == "saveFailed")
            {
                return BadRequest(ApiResponse<ContractorGroupDto>.Error("Failed to update contractor group", result.errors));
            }

            _logger.LogInformation("Updated contractor group {Id}", id);
            return Ok(ApiResponse<ContractorGroupDto>.Ok(result.dto!, "Contractor group updated successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating contractor group {Id}", id);
            return StatusCode(500, ApiResponse<ContractorGroupDto>.Error("Error updating contractor group", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Delete a contractor group
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteContractorGroup(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var grupy = _sferaService.GetManager("Grupy");
                if (grupy == null)
                {
                    return (status: "managerNull", errors: (List<string>?)null);
                }

                var grupaDane = DynamicPropertyHelper.FindById(grupy, id);
                if (grupaDane == null)
                {
                    return ("notFound", (List<string>?)null);
                }

                using (var grupa = grupy.Znajdz(grupaDane))
                {
                    if (grupa == null)
                    {
                        return ("notFound", (List<string>?)null);
                    }

                    if ((bool)grupa.Usun())
                    {
                        return ("ok", (List<string>?)null);
                    }
                    else
                    {
                        var errors = GetBusinessObjectErrors(grupa);
                        return ("deleteFailed", errors);
                    }
                }
            });

            if (result.status == "managerNull")
            {
                return StatusCode(500, ApiResponse<bool>.Error("Failed to get Grupy manager"));
            }

            if (result.status == "notFound")
            {
                return NotFound(ApiResponse<bool>.Error($"Contractor group with ID {id} not found"));
            }

            if (result.status == "deleteFailed")
            {
                return BadRequest(ApiResponse<bool>.Error("Failed to delete contractor group", result.errors));
            }

            _logger.LogInformation("Deleted contractor group {Id}", id);
            return Ok(ApiResponse<bool>.Ok(true, "Contractor group deleted successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting contractor group {Id}", id);
            return StatusCode(500, ApiResponse<bool>.Error("Error deleting contractor group", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Add a contractor to a group
    /// </summary>
    [HttpPost("{id}/contractors")]
    public async Task<ActionResult<ApiResponse<ContractorGroupMembershipDto>>> AddContractorToGroup(int id, [FromBody] AddContractorToGroupRequest request)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var grupy = _sferaService.GetManager("Grupy");
                if (grupy == null)
                {
                    return (status: "grupyNull", membership: (ContractorGroupMembershipDto?)null, errors: (List<string>?)null);
                }

                var grupaDane = DynamicPropertyHelper.FindById(grupy, id);
                if (grupaDane == null)
                {
                    return ("groupNotFound", (ContractorGroupMembershipDto?)null, (List<string>?)null);
                }

                var podmioty = _sferaService.GetManager("Podmioty");
                if (podmioty == null)
                {
                    return ("podmiotyNull", (ContractorGroupMembershipDto?)null, (List<string>?)null);
                }

                var podmiotDane = DynamicPropertyHelper.FindById(podmioty, request.ContractorId);
                if (podmiotDane == null)
                {
                    return ("contractorNotFound", (ContractorGroupMembershipDto?)null, (List<string>?)null);
                }

                using (var podmiot = podmioty.Znajdz(podmiotDane))
                {
                    if (podmiot == null)
                    {
                        return ("contractorNotFound", (ContractorGroupMembershipDto?)null, (List<string>?)null);
                    }

                    podmiot.Dane.Grupa = grupaDane;

                    if ((bool)podmiot.Zapisz())
                    {
                        var membership = new ContractorGroupMembershipDto
                        {
                            ContractorId = request.ContractorId,
                            ContractorSymbol = DynamicPropertyHelper.GetString(podmiotDane, "Symbol"),
                            ContractorName = DynamicPropertyHelper.GetString(podmiotDane, "NazwaSkrocona"),
                            GroupId = id,
                            GroupSymbol = DynamicPropertyHelper.GetString(grupaDane, "Symbol"),
                            GroupName = DynamicPropertyHelper.GetString(grupaDane, "Nazwa"),
                            JoinDate = DateTime.Now,
                            IsPrimary = request.IsPrimary
                        };

                        return ("ok", membership, (List<string>?)null);
                    }
                    else
                    {
                        var errors = GetBusinessObjectErrors(podmiot);
                        return ("saveFailed", (ContractorGroupMembershipDto?)null, errors);
                    }
                }
            });

            if (result.status == "grupyNull")
            {
                return StatusCode(500, ApiResponse<ContractorGroupMembershipDto>.Error("Failed to get Grupy manager"));
            }

            if (result.status == "groupNotFound")
            {
                return NotFound(ApiResponse<ContractorGroupMembershipDto>.Error($"Contractor group with ID {id} not found"));
            }

            if (result.status == "podmiotyNull")
            {
                return StatusCode(500, ApiResponse<ContractorGroupMembershipDto>.Error("Failed to get Podmioty manager"));
            }

            if (result.status == "contractorNotFound")
            {
                return NotFound(ApiResponse<ContractorGroupMembershipDto>.Error($"Contractor with ID {request.ContractorId} not found"));
            }

            if (result.status == "saveFailed")
            {
                return BadRequest(ApiResponse<ContractorGroupMembershipDto>.Error("Failed to add contractor to group", result.errors));
            }

            _logger.LogInformation("Added contractor {ContractorId} to group {GroupId}", request.ContractorId, id);
            return Ok(ApiResponse<ContractorGroupMembershipDto>.Ok(result.membership!, "Contractor added to group successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding contractor to group {Id}", id);
            return StatusCode(500, ApiResponse<ContractorGroupMembershipDto>.Error("Error adding contractor to group", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Bulk add contractors to a group
    /// </summary>
    [HttpPost("{id}/contractors/bulk")]
    public async Task<ActionResult<ApiResponse<int>>> BulkAddContractorsToGroup(int id, [FromBody] BulkAddContractorsToGroupRequest request)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var grupy = _sferaService.GetManager("Grupy");
                if (grupy == null)
                {
                    return (status: "grupyNull", addedCount: 0);
                }

                var grupaDane = DynamicPropertyHelper.FindById(grupy, id);
                if (grupaDane == null)
                {
                    return ("groupNotFound", 0);
                }

                var podmioty = _sferaService.GetManager("Podmioty");
                if (podmioty == null)
                {
                    return ("podmiotyNull", 0);
                }

                int addedCount = 0;
                foreach (var contractorId in request.ContractorIds)
                {
                    var podmiotDane = DynamicPropertyHelper.FindById(podmioty, contractorId);
                    if (podmiotDane != null)
                    {
                        try
                        {
                            using (var podmiot = podmioty.Znajdz(podmiotDane))
                            {
                                if (podmiot != null)
                                {
                                    podmiot.Dane.Grupa = grupaDane;
                                    if ((bool)podmiot.Zapisz())
                                    {
                                        addedCount++;
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // Continue with other contractors
                        }
                    }
                }

                return ("ok", addedCount);
            });

            if (result.status == "grupyNull")
            {
                return StatusCode(500, ApiResponse<int>.Error("Failed to get Grupy manager"));
            }

            if (result.status == "groupNotFound")
            {
                return NotFound(ApiResponse<int>.Error($"Contractor group with ID {id} not found"));
            }

            if (result.status == "podmiotyNull")
            {
                return StatusCode(500, ApiResponse<int>.Error("Failed to get Podmioty manager"));
            }

            _logger.LogInformation("Bulk added {Count} contractors to group {GroupId}", result.addedCount, id);
            return Ok(ApiResponse<int>.Ok(result.addedCount, $"Added {result.addedCount} contractors to group"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk adding contractors to group {Id}", id);
            return StatusCode(500, ApiResponse<int>.Error("Error adding contractors to group", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Remove a contractor from a group
    /// </summary>
    [HttpDelete("{id}/contractors/{contractorId}")]
    public async Task<ActionResult<ApiResponse<bool>>> RemoveContractorFromGroup(int id, int contractorId)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var podmioty = _sferaService.GetManager("Podmioty");
                if (podmioty == null)
                {
                    return (status: "podmiotyNull", errors: (List<string>?)null);
                }

                var podmiotDane = DynamicPropertyHelper.FindById(podmioty, contractorId);
                if (podmiotDane == null)
                {
                    return ("contractorNotFound", (List<string>?)null);
                }

                // Verify contractor is in this group
                var currentGroup = DynamicPropertyHelper.GetProperty(podmiotDane, "Grupa");
                if (currentGroup == null || DynamicPropertyHelper.GetId(currentGroup) != id)
                {
                    return ("notInGroup", (List<string>?)null);
                }

                using (var podmiot = podmioty.Znajdz(podmiotDane))
                {
                    if (podmiot == null)
                    {
                        return ("contractorNotFound", (List<string>?)null);
                    }

                    try { podmiot.Dane.Grupa = null; } catch { }

                    if ((bool)podmiot.Zapisz())
                    {
                        return ("ok", (List<string>?)null);
                    }
                    else
                    {
                        var errors = GetBusinessObjectErrors(podmiot);
                        return ("saveFailed", errors);
                    }
                }
            });

            if (result.status == "podmiotyNull")
            {
                return StatusCode(500, ApiResponse<bool>.Error("Failed to get Podmioty manager"));
            }

            if (result.status == "contractorNotFound")
            {
                return NotFound(ApiResponse<bool>.Error($"Contractor with ID {contractorId} not found"));
            }

            if (result.status == "notInGroup")
            {
                return BadRequest(ApiResponse<bool>.Error("Contractor is not in this group"));
            }

            if (result.status == "saveFailed")
            {
                return BadRequest(ApiResponse<bool>.Error("Failed to remove contractor from group", result.errors));
            }

            _logger.LogInformation("Removed contractor {ContractorId} from group {GroupId}", contractorId, id);
            return Ok(ApiResponse<bool>.Ok(true, "Contractor removed from group successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing contractor {ContractorId} from group {Id}", contractorId, id);
            return StatusCode(500, ApiResponse<bool>.Error("Error removing contractor from group", new List<string> { ex.Message }));
        }
    }

    private static int GetContractorCountForGroup(int groupId, dynamic podmioty)
    {
        try
        {
            int count = 0;
            foreach (var p in DynamicPropertyHelper.SafeGetAll((object)podmioty))
            {
                var grupa = DynamicPropertyHelper.GetProperty(p, "Grupa");
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

    private static ContractorGroupListItemDto MapToListItemDto(dynamic grupa)
    {
        var parent = DynamicPropertyHelper.GetProperty(grupa, "Rodzic");

        return new ContractorGroupListItemDto
        {
            Id = DynamicPropertyHelper.GetId(grupa),
            Symbol = DynamicPropertyHelper.GetString(grupa, "Symbol") ?? "",
            Name = DynamicPropertyHelper.GetString(grupa, "Nazwa") ?? "",
            ParentId = parent != null ? DynamicPropertyHelper.GetId(parent) : null,
            ParentSymbol = parent != null ? DynamicPropertyHelper.GetString(parent, "Symbol") : null,
            Level = DynamicPropertyHelper.GetNullableInt(grupa, "Poziom") ?? 0
        };
    }

    private static ContractorGroupDto MapToDto(dynamic grupa, dynamic grupy)
    {
        var parent = DynamicPropertyHelper.GetProperty(grupa, "Rodzic");
        var defaultPriceLevel = DynamicPropertyHelper.GetProperty(grupa, "DomyslnyPoziomCen");
        var defaultPaymentMethod = DynamicPropertyHelper.GetProperty(grupa, "DomyslnySposobPlatnosci");

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

        return new ContractorGroupDto
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
            ContractorCount = GetContractorCountForGroup(groupId, grupy),
            DirectContractorCount = GetContractorCountForGroup(groupId, grupy),
            ChildGroupCount = childCount,

            // Default settings
            DefaultPaymentMethodId = defaultPaymentMethod != null ? DynamicPropertyHelper.GetId(defaultPaymentMethod) : null,
            DefaultPaymentMethodName = defaultPaymentMethod != null ? DynamicPropertyHelper.GetString(defaultPaymentMethod, "Nazwa") : null,
            DefaultPaymentDays = DynamicPropertyHelper.GetNullableInt(grupa, "DomyslnyTerminPlatnosci"),
            DefaultDiscountPercent = DynamicPropertyHelper.GetDecimal(grupa, "DomyslnyRabatProcent"),
            DefaultPriceLevelId = defaultPriceLevel != null ? DynamicPropertyHelper.GetId(defaultPriceLevel) : null,
            DefaultPriceLevelName = defaultPriceLevel != null ? DynamicPropertyHelper.GetString(defaultPriceLevel, "Nazwa") : null,
            DefaultCreditLimit = DynamicPropertyHelper.GetDecimal(grupa, "DomyslnyLimitKredytu"),

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
