using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Requests;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Product Attributes (Cechy Asortymentu) management endpoints
/// </summary>
[ApiController]
[Route("api/product-attributes")]
[Authorize]
[Tags("Product Attributes")]
public class ProductAttributesController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<ProductAttributesController> _logger;

    public ProductAttributesController(ISferaService sferaService, ILogger<ProductAttributesController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get all product attributes with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResponse<ProductAttributeListItemDto>>> GetProductAttributes([FromQuery] ProductAttributeQueryRequest query)
    {
        try
        {
            var (items, totalCount) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var cechy = _sferaService.GetManager("CechyAsortymentu");
                if (cechy == null) return (null, -1);

                var allCechy = new List<object>();
                foreach (var c in DynamicPropertyHelper.SafeGetAll((object)cechy))
                {
                    allCechy.Add(c);
                }

                // Apply filters
                var filteredList = new List<object>();
                foreach (var c in allCechy)
                {
                    // Deleted filter
                    if (!query.IncludeDeleted)
                    {
                        var isDeleted = DynamicPropertyHelper.GetNullableBool(c, "Usuniety") ?? false;
                        if (isDeleted) continue;
                    }

                    // Inactive filter
                    if (!query.IncludeInactive)
                    {
                        var isActive = DynamicPropertyHelper.GetNullableBool(c, "Aktywny") ?? true;
                        if (!isActive) continue;
                    }

                    // Type filter
                    if (query.Type.HasValue)
                    {
                        var typ = DynamicPropertyHelper.GetNullableInt(c, "Typ");
                        if (typ != query.Type.Value) continue;
                    }

                    // Group filter
                    if (query.GroupId.HasValue)
                    {
                        var grupa = DynamicPropertyHelper.GetProperty(c, "Grupa");
                        var groupId = grupa != null ? DynamicPropertyHelper.GetId(grupa) : (int?)null;
                        if (groupId != query.GroupId.Value) continue;
                    }

                    // Search filter
                    if (!string.IsNullOrEmpty(query.Search))
                    {
                        var searchLower = query.Search.ToLower();
                        var symbol = (DynamicPropertyHelper.GetString(c, "Symbol") ?? "").ToLower();
                        var nazwa = (DynamicPropertyHelper.GetString(c, "Nazwa") ?? "").ToLower();

                        if (!symbol.Contains(searchLower) && !nazwa.Contains(searchLower))
                            continue;
                    }

                    filteredList.Add(c);
                }

                var count = filteredList.Count;
                var pagedItems = filteredList
                    .OrderBy(c => DynamicPropertyHelper.GetNullableInt(c, "Kolejnosc") ?? 0)
                    .ThenBy(c => DynamicPropertyHelper.GetString(c, "Nazwa"))
                    .Skip((query.Page - 1) * query.PageSize)
                    .Take(query.PageSize)
                    .ToList();

                var result = new List<ProductAttributeListItemDto>();
                foreach (var c in pagedItems)
                {
                    result.Add(MapToListItemDto(c));
                }

                return (result, count);
            });

            if (totalCount == -1)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get CechyAsortymentu manager"));
            }

            var response = new PagedResponse<ProductAttributeListItemDto>
            {
                Data = items!,
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = totalCount
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product attributes");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving product attributes", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get product attribute by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<ProductAttributeDto>>> GetProductAttribute(int id, [FromQuery] bool includeValues = true)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var cechy = _sferaService.GetManager("CechyAsortymentu");
                if (cechy == null) return (ProductAttributeDto?)null;

                dynamic? cecha = null;
                foreach (var c in DynamicPropertyHelper.SafeGetAll((object)cechy))
                {
                    if (DynamicPropertyHelper.GetId(c) == id)
                    {
                        cecha = c;
                        break;
                    }
                }

                if (cecha == null) return (ProductAttributeDto?)null;
                return (ProductAttributeDto?)MapToDto(cecha, includeValues);
            });

            if (result == null)
            {
                return NotFound(ApiResponse<ProductAttributeDto>.Error($"Product attribute with ID {id} not found"));
            }

            return Ok(ApiResponse<ProductAttributeDto>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product attribute {Id}", id);
            return StatusCode(500, ApiResponse<ProductAttributeDto>.Error("Error retrieving product attribute", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get product attribute by symbol
    /// </summary>
    [HttpGet("by-symbol/{symbol}")]
    public async Task<ActionResult<ApiResponse<ProductAttributeDto>>> GetProductAttributeBySymbol(string symbol, [FromQuery] bool includeValues = true)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var cechy = _sferaService.GetManager("CechyAsortymentu");
                if (cechy == null) return (ProductAttributeDto?)null;

                dynamic? cecha = null;
                foreach (var c in DynamicPropertyHelper.SafeGetAll((object)cechy))
                {
                    if (DynamicPropertyHelper.GetString(c, "Symbol") == symbol)
                    {
                        cecha = c;
                        break;
                    }
                }

                if (cecha == null) return (ProductAttributeDto?)null;
                return (ProductAttributeDto?)MapToDto(cecha, includeValues);
            });

            if (result == null)
            {
                return NotFound(ApiResponse<ProductAttributeDto>.Error($"Product attribute with symbol {symbol} not found"));
            }

            return Ok(ApiResponse<ProductAttributeDto>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product attribute by symbol {Symbol}", symbol);
            return StatusCode(500, ApiResponse<ProductAttributeDto>.Error("Error retrieving product attribute", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create a new product attribute
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<ProductAttributeDto>>> CreateProductAttribute([FromBody] CreateProductAttributeRequest request)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var cechy = _sferaService.GetManager("CechyAsortymentu");
                if (cechy == null) return (false, "manager_null", 0, (ProductAttributeDto?)null, new List<string>());

                // Check if symbol already exists
                foreach (var c in DynamicPropertyHelper.SafeGetAll((object)cechy))
                {
                    if (DynamicPropertyHelper.GetString(c, "Symbol") == request.Symbol)
                    {
                        return (false, "duplicate", 0, (ProductAttributeDto?)null, new List<string>());
                    }
                }

                using (var cecha = cechy.Utworz())
                {
                    dynamic dane = cecha.Dane;
                    dane.Symbol = request.Symbol;
                    dane.Nazwa = request.Name;
                    dane.Typ = request.Type;

                    if (!string.IsNullOrEmpty(request.Description))
                    {
                        try { dane.Opis = request.Description; } catch { }
                    }

                    // Set group
                    if (request.GroupId.HasValue)
                    {
                        try { dane.GrupaId = request.GroupId.Value; } catch { }
                    }

                    // Set flags
                    try { dane.Wymagana = request.IsRequired; } catch { }
                    try { dane.Wielowartosciowa = request.IsMultiValue; } catch { }
                    try { dane.WyszukiwanaDlaProduktu = request.IsSearchable; } catch { }
                    try { dane.WyszukiwanaWFiltrze = request.IsFilterable; } catch { }
                    try { dane.WidocznaWLiscie = request.ShowOnList; } catch { }
                    try { dane.WidocznaWKarcie = request.ShowOnCard; } catch { }

                    // Set sort order
                    try { dane.Kolejnosc = request.SortOrder; } catch { }

                    if ((bool)cecha.Zapisz())
                    {
                        // Add predefined values if provided
                        if (request.Values != null && request.Values.Count > 0)
                        {
                            try
                            {
                                foreach (var value in request.Values)
                                {
                                    var wartosc = cecha.Wartosci?.Dodaj();
                                    if (wartosc != null)
                                    {
                                        wartosc.Dane.Wartosc = value.Value;
                                        wartosc.Dane.WartoscWyswietlana = value.DisplayValue ?? value.Value;
                                        wartosc.Dane.Kolejnosc = value.SortOrder;
                                        wartosc.Dane.Domyslna = value.IsDefault;
                                    }
                                }
                                cecha.Zapisz();
                            }
                            catch { }
                        }

                        var newId = DynamicPropertyHelper.GetId(dane);
                        var dto = MapToDto(dane, true);
                        return (true, "created", newId, (ProductAttributeDto?)dto, new List<string>());
                    }
                    else
                    {
                        var errors = GetBusinessObjectErrors(cecha);
                        return (false, "save_failed", 0, (ProductAttributeDto?)null, errors);
                    }
                }
            });

            var (success, code, newId, dto, errors) = result;

            if (code == "manager_null")
            {
                return StatusCode(500, ApiResponse<ProductAttributeDto>.Error("Failed to get CechyAsortymentu manager"));
            }
            if (code == "duplicate")
            {
                return BadRequest(ApiResponse<ProductAttributeDto>.Error($"Product attribute with symbol {request.Symbol} already exists"));
            }
            if (code == "save_failed")
            {
                return BadRequest(ApiResponse<ProductAttributeDto>.Error("Failed to create product attribute", errors));
            }

            _logger.LogInformation("Created product attribute {Symbol}", request.Symbol);
            return CreatedAtAction(
                nameof(GetProductAttribute),
                new { id = newId },
                ApiResponse<ProductAttributeDto>.Ok(dto!, "Product attribute created successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating product attribute");
            return StatusCode(500, ApiResponse<ProductAttributeDto>.Error("Error creating product attribute", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Update an existing product attribute
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<ProductAttributeDto>>> UpdateProductAttribute(int id, [FromBody] UpdateProductAttributeRequest request)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var cechy = _sferaService.GetManager("CechyAsortymentu");
                if (cechy == null) return (false, "manager_null", (ProductAttributeDto?)null, new List<string>());

                dynamic? cechaDane = null;
                foreach (var c in DynamicPropertyHelper.SafeGetAll((object)cechy))
                {
                    if (DynamicPropertyHelper.GetId(c) == id)
                    {
                        cechaDane = c;
                        break;
                    }
                }

                if (cechaDane == null) return (false, "not_found", (ProductAttributeDto?)null, new List<string>());

                using (var cecha = cechy.Znajdz(cechaDane))
                {
                    if (cecha == null) return (false, "not_found", (ProductAttributeDto?)null, new List<string>());

                    dynamic dane = cecha.Dane;

                    if (!string.IsNullOrEmpty(request.Name))
                    {
                        dane.Nazwa = request.Name;
                    }

                    if (!string.IsNullOrEmpty(request.Description))
                    {
                        try { dane.Opis = request.Description; } catch { }
                    }

                    if (request.GroupId.HasValue)
                    {
                        try { dane.GrupaId = request.GroupId.Value; } catch { }
                    }

                    if (request.IsRequired.HasValue)
                    {
                        try { dane.Wymagana = request.IsRequired.Value; } catch { }
                    }

                    if (request.IsSearchable.HasValue)
                    {
                        try { dane.WyszukiwanaDlaProduktu = request.IsSearchable.Value; } catch { }
                    }

                    if (request.IsFilterable.HasValue)
                    {
                        try { dane.WyszukiwanaWFiltrze = request.IsFilterable.Value; } catch { }
                    }

                    if (request.ShowOnList.HasValue)
                    {
                        try { dane.WidocznaWLiscie = request.ShowOnList.Value; } catch { }
                    }

                    if (request.ShowOnCard.HasValue)
                    {
                        try { dane.WidocznaWKarcie = request.ShowOnCard.Value; } catch { }
                    }

                    if (request.SortOrder.HasValue)
                    {
                        try { dane.Kolejnosc = request.SortOrder.Value; } catch { }
                    }

                    if (request.IsActive.HasValue)
                    {
                        try { dane.Aktywny = request.IsActive.Value; } catch { }
                    }

                    if ((bool)cecha.Zapisz())
                    {
                        var dto = MapToDto(dane, true);
                        return (true, "ok", (ProductAttributeDto?)dto, new List<string>());
                    }
                    else
                    {
                        var errors = GetBusinessObjectErrors(cecha);
                        return (false, "save_failed", (ProductAttributeDto?)null, errors);
                    }
                }
            });

            var (success, code, dto, errors) = result;

            if (code == "manager_null")
            {
                return StatusCode(500, ApiResponse<ProductAttributeDto>.Error("Failed to get CechyAsortymentu manager"));
            }
            if (code == "not_found")
            {
                return NotFound(ApiResponse<ProductAttributeDto>.Error($"Product attribute with ID {id} not found"));
            }
            if (code == "save_failed")
            {
                return BadRequest(ApiResponse<ProductAttributeDto>.Error("Failed to update product attribute", errors));
            }

            _logger.LogInformation("Updated product attribute {Id}", id);
            return Ok(ApiResponse<ProductAttributeDto>.Ok(dto!, "Product attribute updated successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating product attribute {Id}", id);
            return StatusCode(500, ApiResponse<ProductAttributeDto>.Error("Error updating product attribute", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Delete a product attribute
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteProductAttribute(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var cechy = _sferaService.GetManager("CechyAsortymentu");
                if (cechy == null) return (false, "manager_null", new List<string>());

                dynamic? cechaDane = null;
                foreach (var c in DynamicPropertyHelper.SafeGetAll((object)cechy))
                {
                    if (DynamicPropertyHelper.GetId(c) == id)
                    {
                        cechaDane = c;
                        break;
                    }
                }

                if (cechaDane == null) return (false, "not_found", new List<string>());

                using (var cecha = cechy.Znajdz(cechaDane))
                {
                    if (cecha == null) return (false, "not_found", new List<string>());

                    if ((bool)cecha.Usun())
                    {
                        return (true, "ok", new List<string>());
                    }
                    else
                    {
                        var errors = GetBusinessObjectErrors(cecha);
                        return (false, "delete_failed", errors);
                    }
                }
            });

            var (success, code, errors) = result;

            if (code == "manager_null")
            {
                return StatusCode(500, ApiResponse<bool>.Error("Failed to get CechyAsortymentu manager"));
            }
            if (code == "not_found")
            {
                return NotFound(ApiResponse<bool>.Error($"Product attribute with ID {id} not found"));
            }
            if (code == "delete_failed")
            {
                return BadRequest(ApiResponse<bool>.Error("Failed to delete product attribute", errors));
            }

            _logger.LogInformation("Deleted product attribute {Id}", id);
            return Ok(ApiResponse<bool>.Ok(true, "Product attribute deleted successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting product attribute {Id}", id);
            return StatusCode(500, ApiResponse<bool>.Error("Error deleting product attribute", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get values for a product attribute
    /// </summary>
    [HttpGet("{id}/values")]
    public async Task<ActionResult<ApiResponse<List<ProductAttributeValueDto>>>> GetAttributeValues(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var cechy = _sferaService.GetManager("CechyAsortymentu");
                if (cechy == null) return (List<ProductAttributeValueDto>?)null;

                dynamic? cecha = null;
                foreach (var c in DynamicPropertyHelper.SafeGetAll((object)cechy))
                {
                    if (DynamicPropertyHelper.GetId(c) == id)
                    {
                        cecha = c;
                        break;
                    }
                }

                if (cecha == null) return (List<ProductAttributeValueDto>?)null;

                var values = new List<ProductAttributeValueDto>();
                var wartosci = DynamicPropertyHelper.GetCollection((object)cecha, "Wartosci");
                foreach (var w in wartosci)
                {
                    values.Add(new ProductAttributeValueDto
                    {
                        Id = DynamicPropertyHelper.GetId(w),
                        Value = DynamicPropertyHelper.GetString(w, "Wartosc") ?? "",
                        DisplayValue = DynamicPropertyHelper.GetString(w, "WartoscWyswietlana"),
                        SortOrder = DynamicPropertyHelper.GetNullableInt(w, "Kolejnosc") ?? 0,
                        IsDefault = DynamicPropertyHelper.GetNullableBool(w, "Domyslna") ?? false,
                        IsActive = DynamicPropertyHelper.GetNullableBool(w, "Aktywny") ?? true
                    });
                }

                return (List<ProductAttributeValueDto>?)values.OrderBy(v => v.SortOrder).ToList();
            });

            if (result == null)
            {
                return NotFound(ApiResponse<List<ProductAttributeValueDto>>.Error($"Product attribute with ID {id} not found"));
            }

            return Ok(ApiResponse<List<ProductAttributeValueDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting values for product attribute {Id}", id);
            return StatusCode(500, ApiResponse<List<ProductAttributeValueDto>>.Error("Error retrieving attribute values", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Add a value to a product attribute
    /// </summary>
    [HttpPost("{id}/values")]
    public async Task<ActionResult<ApiResponse<ProductAttributeValueDto>>> AddAttributeValue(int id, [FromBody] CreateProductAttributeValueRequest request)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var cechy = _sferaService.GetManager("CechyAsortymentu");
                if (cechy == null) return (false, "manager_null", (ProductAttributeValueDto?)null, new List<string>());

                dynamic? cechaDane = null;
                foreach (var c in DynamicPropertyHelper.SafeGetAll((object)cechy))
                {
                    if (DynamicPropertyHelper.GetId(c) == id)
                    {
                        cechaDane = c;
                        break;
                    }
                }

                if (cechaDane == null) return (false, "not_found", (ProductAttributeValueDto?)null, new List<string>());

                using (var cecha = cechy.Znajdz(cechaDane))
                {
                    if (cecha == null) return (false, "not_found", (ProductAttributeValueDto?)null, new List<string>());

                    var wartosc = cecha.Wartosci?.Dodaj();
                    if (wartosc == null) return (false, "add_failed", (ProductAttributeValueDto?)null, new List<string>());

                    wartosc.Dane.Wartosc = request.Value;
                    wartosc.Dane.WartoscWyswietlana = request.DisplayValue ?? request.Value;
                    wartosc.Dane.Kolejnosc = request.SortOrder;
                    wartosc.Dane.Domyslna = request.IsDefault;

                    if ((bool)cecha.Zapisz())
                    {
                        var valueDto = new ProductAttributeValueDto
                        {
                            Id = DynamicPropertyHelper.GetId(wartosc.Dane),
                            Value = request.Value,
                            DisplayValue = request.DisplayValue ?? request.Value,
                            SortOrder = request.SortOrder,
                            IsDefault = request.IsDefault,
                            IsActive = true
                        };
                        return (true, "ok", (ProductAttributeValueDto?)valueDto, new List<string>());
                    }
                    else
                    {
                        var errors = GetBusinessObjectErrors(cecha);
                        return (false, "save_failed", (ProductAttributeValueDto?)null, errors);
                    }
                }
            });

            var (success, code, valueDto, errors) = result;

            if (code == "manager_null")
            {
                return StatusCode(500, ApiResponse<ProductAttributeValueDto>.Error("Failed to get CechyAsortymentu manager"));
            }
            if (code == "not_found")
            {
                return NotFound(ApiResponse<ProductAttributeValueDto>.Error($"Product attribute with ID {id} not found"));
            }
            if (code == "add_failed")
            {
                return BadRequest(ApiResponse<ProductAttributeValueDto>.Error("Failed to add value to attribute"));
            }
            if (code == "save_failed")
            {
                return BadRequest(ApiResponse<ProductAttributeValueDto>.Error("Failed to add value to attribute", errors));
            }

            _logger.LogInformation("Added value to product attribute {Id}", id);
            return Ok(ApiResponse<ProductAttributeValueDto>.Ok(valueDto!, "Value added successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding value to product attribute {Id}", id);
            return StatusCode(500, ApiResponse<ProductAttributeValueDto>.Error("Error adding attribute value", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Assign attribute to a product
    /// </summary>
    [HttpPost("assign")]
    public async Task<ActionResult<ApiResponse<ProductAttributeAssignmentDto>>> AssignAttributeToProduct([FromBody] AssignProductAttributeRequest request)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var asortymenty = _sferaService.GetManager("Asortymenty");
                if (asortymenty == null) return (false, "manager_asortymenty_null", (ProductAttributeAssignmentDto?)null, new List<string>());

                dynamic? asortymentDane = null;
                foreach (var a in DynamicPropertyHelper.SafeGetAll((object)asortymenty))
                {
                    if (DynamicPropertyHelper.GetId(a) == request.ProductId)
                    {
                        asortymentDane = a;
                        break;
                    }
                }

                if (asortymentDane == null) return (false, "product_not_found", (ProductAttributeAssignmentDto?)null, new List<string>());

                var cechy = _sferaService.GetManager("CechyAsortymentu");
                if (cechy == null) return (false, "manager_cechy_null", (ProductAttributeAssignmentDto?)null, new List<string>());

                dynamic? cechaDane = null;
                foreach (var c in DynamicPropertyHelper.SafeGetAll((object)cechy))
                {
                    if (DynamicPropertyHelper.GetId(c) == request.AttributeId)
                    {
                        cechaDane = c;
                        break;
                    }
                }

                if (cechaDane == null) return (false, "attribute_not_found", (ProductAttributeAssignmentDto?)null, new List<string>());

                using (var asortyment = asortymenty.Znajdz(asortymentDane))
                {
                    if (asortyment == null) return (false, "product_not_found", (ProductAttributeAssignmentDto?)null, new List<string>());

                    try
                    {
                        var cechaAsortymentu = asortyment.Cechy?.Dodaj(cechaDane);
                        if (cechaAsortymentu != null)
                        {
                            cechaAsortymentu.Dane.Wartosc = request.Value;
                            if (request.ValueId.HasValue)
                            {
                                cechaAsortymentu.Dane.WartoscZdefiniowanaId = request.ValueId.Value;
                            }
                        }
                    }
                    catch
                    {
                        // Try alternative approach
                        try
                        {
                            asortyment.DodajCeche(cechaDane, request.Value);
                        }
                        catch { }
                    }

                    if ((bool)asortyment.Zapisz())
                    {
                        var assignment = new ProductAttributeAssignmentDto
                        {
                            AttributeId = request.AttributeId,
                            AttributeSymbol = DynamicPropertyHelper.GetString(cechaDane, "Symbol"),
                            AttributeName = DynamicPropertyHelper.GetString(cechaDane, "Nazwa"),
                            Value = request.Value,
                            ValueId = request.ValueId
                        };
                        return (true, "ok", (ProductAttributeAssignmentDto?)assignment, new List<string>());
                    }
                    else
                    {
                        var errors = GetBusinessObjectErrors(asortyment);
                        return (false, "save_failed", (ProductAttributeAssignmentDto?)null, errors);
                    }
                }
            });

            var (success, code, assignment, errors) = result;

            if (code == "manager_asortymenty_null")
            {
                return StatusCode(500, ApiResponse<ProductAttributeAssignmentDto>.Error("Failed to get Asortymenty manager"));
            }
            if (code == "manager_cechy_null")
            {
                return StatusCode(500, ApiResponse<ProductAttributeAssignmentDto>.Error("Failed to get CechyAsortymentu manager"));
            }
            if (code == "product_not_found")
            {
                return NotFound(ApiResponse<ProductAttributeAssignmentDto>.Error($"Product with ID {request.ProductId} not found"));
            }
            if (code == "attribute_not_found")
            {
                return NotFound(ApiResponse<ProductAttributeAssignmentDto>.Error($"Attribute with ID {request.AttributeId} not found"));
            }
            if (code == "save_failed")
            {
                return BadRequest(ApiResponse<ProductAttributeAssignmentDto>.Error("Failed to assign attribute", errors));
            }

            _logger.LogInformation("Assigned attribute {AttributeId} to product {ProductId}", request.AttributeId, request.ProductId);
            return Ok(ApiResponse<ProductAttributeAssignmentDto>.Ok(assignment!, "Attribute assigned successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning attribute to product");
            return StatusCode(500, ApiResponse<ProductAttributeAssignmentDto>.Error("Error assigning attribute", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get attribute groups
    /// </summary>
    [HttpGet("groups")]
    public async Task<ActionResult<ApiResponse<List<AttributeGroupDto>>>> GetAttributeGroups()
    {
        try
        {
            var groups = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var grupyCech = _sferaService.GetManager("GrupyCechAsortymentu");
                if (grupyCech == null)
                {
                    // Return empty list if manager not available
                    return new List<AttributeGroupDto>();
                }

                var result = new List<AttributeGroupDto>();
                foreach (var g in DynamicPropertyHelper.SafeGetAll((object)grupyCech))
                {
                    result.Add(new AttributeGroupDto
                    {
                        Id = DynamicPropertyHelper.GetId(g),
                        Name = DynamicPropertyHelper.GetString(g, "Nazwa") ?? "",
                        Description = DynamicPropertyHelper.GetString(g, "Opis"),
                        SortOrder = DynamicPropertyHelper.GetNullableInt(g, "Kolejnosc") ?? 0
                    });
                }

                return result.OrderBy(g => g.SortOrder).ToList();
            });

            return Ok(ApiResponse<List<AttributeGroupDto>>.Ok(groups));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting attribute groups");
            return StatusCode(500, ApiResponse<List<AttributeGroupDto>>.Error("Error retrieving attribute groups", new List<string> { ex.Message }));
        }
    }

    private static ProductAttributeListItemDto MapToListItemDto(dynamic cecha)
    {
        var wartosci = DynamicPropertyHelper.GetCollection((object)cecha, "Wartosci");
        int valueCount = 0;
        foreach (var _ in wartosci) valueCount++;

        return new ProductAttributeListItemDto
        {
            Id = DynamicPropertyHelper.GetId(cecha),
            Symbol = DynamicPropertyHelper.GetString(cecha, "Symbol") ?? "",
            Name = DynamicPropertyHelper.GetString(cecha, "Nazwa") ?? "",
            AttributeType = GetTypeDescription(DynamicPropertyHelper.GetNullableInt(cecha, "Typ") ?? 0),
            ValueCount = valueCount,
            IsActive = DynamicPropertyHelper.GetNullableBool(cecha, "Aktywny") ?? true
        };
    }

    private static ProductAttributeDto MapToDto(dynamic cecha, bool includeValues)
    {
        var grupa = DynamicPropertyHelper.GetProperty(cecha, "Grupa");

        var dto = new ProductAttributeDto
        {
            // Identity
            Id = DynamicPropertyHelper.GetId(cecha),
            Symbol = DynamicPropertyHelper.GetString(cecha, "Symbol") ?? "",
            Name = DynamicPropertyHelper.GetString(cecha, "Nazwa") ?? "",
            Description = DynamicPropertyHelper.GetString(cecha, "Opis"),

            // Type
            Type = DynamicPropertyHelper.GetNullableInt(cecha, "Typ") ?? 0,
            TypeDescription = GetTypeDescription(DynamicPropertyHelper.GetNullableInt(cecha, "Typ") ?? 0),

            // Group
            GroupId = grupa != null ? DynamicPropertyHelper.GetId(grupa) : null,
            GroupName = grupa != null ? DynamicPropertyHelper.GetString(grupa, "Nazwa") : null,

            // Flags
            IsRequired = DynamicPropertyHelper.GetNullableBool(cecha, "Wymagana") ?? false,
            IsMultiValue = DynamicPropertyHelper.GetNullableBool(cecha, "Wielowartosciowa") ?? false,
            IsSearchable = DynamicPropertyHelper.GetNullableBool(cecha, "WyszukiwanaDlaProduktu") ?? true,
            IsFilterable = DynamicPropertyHelper.GetNullableBool(cecha, "WyszukiwanaWFiltrze") ?? true,
            ShowOnList = DynamicPropertyHelper.GetNullableBool(cecha, "WidocznaWLiscie") ?? false,
            ShowOnCard = DynamicPropertyHelper.GetNullableBool(cecha, "WidocznaWKarcie") ?? true,

            // Sort order
            SortOrder = DynamicPropertyHelper.GetNullableInt(cecha, "Kolejnosc") ?? 0,

            // Status
            IsActive = DynamicPropertyHelper.GetNullableBool(cecha, "Aktywny") ?? true,
            IsDeleted = DynamicPropertyHelper.GetNullableBool(cecha, "Usuniety") ?? false,

            // Timestamps
            CreatedAt = DynamicPropertyHelper.GetDateTime(cecha, "DataUtworzenia"),
            ModifiedAt = DynamicPropertyHelper.GetDateTime(cecha, "DataModyfikacji")
        };

        if (includeValues)
        {
            dto.Values = new List<ProductAttributeValueDto>();
            var wartosci = DynamicPropertyHelper.GetCollection((object)cecha, "Wartosci");
            foreach (var w in wartosci)
            {
                dto.Values.Add(new ProductAttributeValueDto
                {
                    Id = DynamicPropertyHelper.GetId(w),
                    Value = DynamicPropertyHelper.GetString(w, "Wartosc") ?? "",
                    DisplayValue = DynamicPropertyHelper.GetString(w, "WartoscWyswietlana"),
                    SortOrder = DynamicPropertyHelper.GetNullableInt(w, "Kolejnosc") ?? 0,
                    IsDefault = DynamicPropertyHelper.GetNullableBool(w, "Domyslna") ?? false,
                    IsActive = DynamicPropertyHelper.GetNullableBool(w, "Aktywny") ?? true
                });
            }
            dto.Values = dto.Values.OrderBy(v => v.SortOrder).ToList();
        }

        return dto;
    }

    private static string GetTypeDescription(int type)
    {
        return type switch
        {
            0 => "Tekst",
            1 => "Liczba",
            2 => "Data",
            3 => "Tak/Nie",
            4 => "Lista",
            _ => $"Typ {type}"
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
