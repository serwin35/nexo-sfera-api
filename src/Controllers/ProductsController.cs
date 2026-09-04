using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using NexoSferaApi.Filters;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Requests;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Products (Asortyment) management endpoints
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Tags("Products")]
public class ProductsController : ControllerBase
{
    private readonly ISferaService _sferaService;

    /// <summary>
    /// Connection string for the SQL fallback of unit conversions (MapUnits is static and used by static mappers).
    /// </summary>
    private static Func<string?>? _unitConversionConnectionString;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(ISferaService sferaService, ILogger<ProductsController> logger)
    {
        _sferaService = sferaService;
        _unitConversionConnectionString = () => sferaService.GetConnectionString();
        _logger = logger;
    }

    /// <summary>
    /// Explore properties of a single Asortyment entity
    /// </summary>
    [HttpGet("debug/properties/{id}")]
    [DevelopmentOnly]
    public async Task<ActionResult<object>> GetProductProperties(int id, [FromQuery] string? path = null)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var asortymentyManager = _sferaService.GetManager("Asortymenty");
                if (asortymentyManager == null)
                {
                    return (object?)null;
                }

                dynamic? asortyment = null;
                foreach (var a in DynamicPropertyHelper.SafeGetAll((object)asortymentyManager))
                {
                    if (DynamicPropertyHelper.GetId(a) == id)
                    {
                        asortyment = a;
                        break;
                    }
                }

                if (asortyment == null)
                {
                    return (object?)new { NotFound = true, Id = id };
                }

                // ?path=JednostkiMiar.1.PrzelicznikJednostkiNadrzednej — walk properties / collection indexes before dumping.
                object? target = (object)asortyment;
                if (!string.IsNullOrWhiteSpace(path))
                {
                    foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (target == null) break;
                        if (int.TryParse(segment, out var index))
                        {
                            object? picked = null;
                            var i = 0;
                            foreach (var item in (System.Collections.IEnumerable)target)
                            {
                                if (i++ == index) { picked = item; break; }
                            }
                            target = picked;
                        }
                        else
                        {
                            target = DynamicPropertyHelper.GetProperty(target, segment);
                        }
                    }
                    if (target == null)
                    {
                        return (object?)new { NotFound = true, Id = id, Path = path };
                    }
                }
                Type asortymentType = target!.GetType();
                var properties = new Dictionary<string, object>();

                foreach (var prop in asortymentType.GetProperties())
                {
                    try
                    {
                        var value = prop.GetValue(target);
                        var valueType = value?.GetType().Name ?? "null";

                        // For collections, get count
                        if (value != null && value.GetType().Name.Contains("Collection"))
                        {
                            try
                            {
                                int count = 0;
                                foreach (var _ in (dynamic)value) count++;
                                properties[prop.Name] = new { Type = valueType, Count = count };
                            }
                            catch
                            {
                                properties[prop.Name] = new { Type = valueType, Value = "Collection (error reading)" };
                            }
                        }
                        else if (value != null && !prop.PropertyType.IsPrimitive && prop.PropertyType != typeof(string)
                                 && prop.PropertyType != typeof(DateTime) && prop.PropertyType != typeof(decimal)
                                 && !prop.PropertyType.IsEnum && prop.PropertyType != typeof(Guid))
                        {
                            properties[prop.Name] = new { Type = valueType, Value = "Complex object" };
                        }
                        else
                        {
                            properties[prop.Name] = new { Type = valueType, Value = value?.ToString() ?? "null" };
                        }
                    }
                    catch (Exception ex)
                    {
                        properties[prop.Name] = new { Error = ex.Message };
                    }
                }

                return (object?)new
                {
                    Id = id,
                    Path = path,
                    EntityType = asortymentType.FullName,
                    PropertyCount = properties.Count,
                    Properties = properties.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value)
                };
            });

            if (result == null)
            {
                return StatusCode(500, new { Error = "Failed to get Asortymenty manager" });
            }

            // Check if not found sentinel
            var notFoundCheck = result as dynamic;
            try
            {
                bool isNotFound = notFoundCheck?.NotFound == true;
                if (isNotFound)
                {
                    return NotFound(new { Error = $"Product {id} not found" });
                }
            }
            catch { /* not a not-found sentinel */ }

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = ex.Message, Stack = ex.StackTrace });
        }
    }

    /// <summary>
    /// Get all products with optional filtering (lightweight list view)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResponse<ProductListItemDto>>> GetProducts(
        [FromQuery] string? search,
        [FromQuery] ProductType? type,
        [FromQuery] bool? activeOnly,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var asortymentyManager = _sferaService.GetManager("Asortymenty");
                if (asortymentyManager == null)
                {
                    return (PagedResponse<ProductListItemDto>?)null;
                }

                var allAsortymenty = new List<object>();
                var sourceData = activeOnly == true ? asortymentyManager.Dane.WszystkieDostepne() : DynamicPropertyHelper.SafeGetAll((object)asortymentyManager);
                foreach (var a in sourceData)
                {
                    allAsortymenty.Add(a);
                }

                if (!string.IsNullOrEmpty(search))
                {
                    allAsortymenty = allAsortymenty.Where(a =>
                    {
                        var symbol = DynamicPropertyHelper.GetString(a, "Symbol") ?? "";
                        var nazwa = DynamicPropertyHelper.GetString(a, "Nazwa") ?? "";
                        var ean = DynamicPropertyHelper.GetString(a, "EAN") ?? "";
                        return symbol.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                               nazwa.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                               ean.Contains(search, StringComparison.OrdinalIgnoreCase);
                    }).ToList();
                }

                var totalCount = allAsortymenty.Count;
                var items = allAsortymenty
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var mappedItems = new List<ProductListItemDto>();
                foreach (var item in items)
                {
                    mappedItems.Add(MapToListItemDto(item));
                }

                return new PagedResponse<ProductListItemDto>
                {
                    Data = mappedItems,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };
            });

            if (result == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Asortymenty manager"));
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting products");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving products", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get product by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<ProductDto>>> GetProduct(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var asortymentyManager = _sferaService.GetManager("Asortymenty");
                if (asortymentyManager == null)
                {
                    return (ProductDto?)null;
                }

                dynamic? asortyment = null;
                foreach (var a in DynamicPropertyHelper.SafeGetAll((object)asortymentyManager))
                {
                    if (DynamicPropertyHelper.GetId(a) == id)
                    {
                        asortyment = a;
                        break;
                    }
                }

                if (asortyment == null)
                {
                    return (ProductDto?)null;
                }

                return (ProductDto?)MapToDto(asortyment);
            });

            if (result == null)
            {
                return NotFound(ApiResponse<ProductDto>.Error($"Product with ID {id} not found"));
            }

            return Ok(ApiResponse<ProductDto>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product {Id}", id);
            return StatusCode(500, ApiResponse<ProductDto>.Error("Error retrieving product", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get product by symbol
    /// </summary>
    [HttpGet("by-symbol/{symbol}")]
    public async Task<ActionResult<ApiResponse<ProductDto>>> GetProductBySymbol(string symbol)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var asortymentyManager = _sferaService.GetManager("Asortymenty");
                if (asortymentyManager == null)
                {
                    return (ProductDto?)null;
                }

                var asortyment = asortymentyManager.Znajdz(symbol);
                if (asortyment == null)
                {
                    return (ProductDto?)null;
                }

                return (ProductDto?)MapToDto(asortyment.Dane);
            });

            if (result == null)
            {
                return NotFound(ApiResponse<ProductDto>.Error($"Product with symbol {symbol} not found"));
            }

            return Ok(ApiResponse<ProductDto>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product by symbol {Symbol}", symbol);
            return StatusCode(500, ApiResponse<ProductDto>.Error("Error retrieving product", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get product by EAN
    /// </summary>
    [HttpGet("by-ean/{ean}")]
    public async Task<ActionResult<ApiResponse<ProductDto>>> GetProductByEan(string ean)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var asortymentyManager = _sferaService.GetManager("Asortymenty");
                if (asortymentyManager == null)
                {
                    return (ProductDto?)null;
                }

                dynamic? asortyment = null;
                foreach (var a in DynamicPropertyHelper.SafeGetAll((object)asortymentyManager))
                {
                    if (DynamicPropertyHelper.GetString(a, "EAN") == ean)
                    {
                        asortyment = a;
                        break;
                    }
                }

                if (asortyment == null)
                {
                    return (ProductDto?)null;
                }

                return (ProductDto?)MapToDto(asortyment);
            });

            if (result == null)
            {
                return NotFound(ApiResponse<ProductDto>.Error($"Product with EAN {ean} not found"));
            }

            return Ok(ApiResponse<ProductDto>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product by EAN {Ean}", ean);
            return StatusCode(500, ApiResponse<ProductDto>.Error("Error retrieving product", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create a new product
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<ProductDto>>> CreateProduct([FromBody] CreateProductRequest request)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var asortymentyManager = _sferaService.GetManager("Asortymenty");
                if (asortymentyManager == null)
                {
                    return (statusCode: 500, dto: (ProductDto?)null, error: "Failed to get Asortymenty manager", errors: (List<string>?)null);
                }

                // Check if symbol already exists
                var existing = asortymentyManager.Znajdz(request.Symbol);
                if (existing != null)
                {
                    return (statusCode: 400, dto: (ProductDto?)null, error: $"Product with symbol {request.Symbol} already exists", errors: (List<string>?)null);
                }

                using (var nowyAsortyment = asortymentyManager.Utworz())
                {
                    dynamic dane = nowyAsortyment.Dane;

                    // Apply template if specified
                    if (!string.IsNullOrEmpty(request.TemplateSymbol))
                    {
                        var szablonyManager = _sferaService.GetManager("SzablonyAsortymentu");
                        if (szablonyManager != null)
                        {
                            dynamic? szablon = null;
                            foreach (var s in DynamicPropertyHelper.SafeGetAll((object)szablonyManager))
                            {
                                if (DynamicPropertyHelper.GetString(s, "Symbol") == request.TemplateSymbol)
                                {
                                    szablon = s;
                                    break;
                                }
                            }
                            if (szablon != null)
                            {
                                nowyAsortyment.WypelnijNaPodstawieSzablonu(szablon);
                            }
                        }
                    }

                    dane.Symbol = request.Symbol;
                    dane.Nazwa = request.Name;

                    if (!string.IsNullOrEmpty(request.Description))
                    {
                        dane.Opis = request.Description;
                    }

                    if (!string.IsNullOrEmpty(request.EAN))
                    {
                        dane.EAN = request.EAN;
                    }

                    if (!string.IsNullOrEmpty(request.PKWiU))
                    {
                        dane.PKWIU = request.PKWiU;
                    }

                    // Set unit (GetUnit accesses SDK managers, must be called inside lock)
                    var jednostka = GetUnit(request.SaleUnit);
                    if (jednostka != null)
                    {
                        dane.JednostkaSprzedazy = jednostka;
                    }

                    if (!string.IsNullOrEmpty(request.PurchaseUnit))
                    {
                        var jednostkaZakupu = GetUnit(request.PurchaseUnit);
                        if (jednostkaZakupu != null)
                        {
                            dane.JednostkaZakupu = jednostkaZakupu;
                        }
                    }

                    // Set price
                    if (request.PriceNet.HasValue)
                    {
                        dane.CenaNetto = request.PriceNet.Value;
                    }

                    // Set physical properties
                    if (request.Weight.HasValue)
                    {
                        dane.Masa = request.Weight.Value;
                    }

                    if (request.Volume.HasValue)
                    {
                        dane.Objetosc = request.Volume.Value;
                    }

                    if ((bool)nowyAsortyment.Zapisz())
                    {
                        var dto = MapToDto(dane);
                        return (statusCode: 201, dto: (ProductDto?)dto, error: (string?)null, errors: (List<string>?)null);
                    }
                    else
                    {
                        var errors = GetBusinessObjectErrors(nowyAsortyment);
                        return (statusCode: 400, dto: (ProductDto?)null, error: "Failed to create product", errors: (List<string>?)errors);
                    }
                }
            });

            if (result.statusCode == 500)
            {
                return StatusCode(500, ApiResponse<object>.Error(result.error ?? "Internal error"));
            }
            if (result.statusCode == 400)
            {
                return BadRequest(ApiResponse<ProductDto>.Error(result.error ?? "Bad request", result.errors ?? new List<string>()));
            }

            _logger.LogInformation("Created product {Symbol}", request.Symbol);
            return CreatedAtAction(
                nameof(GetProduct),
                new { id = result.dto!.Id },
                ApiResponse<ProductDto>.Ok(result.dto, "Product created successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating product");
            return StatusCode(500, ApiResponse<ProductDto>.Error("Error creating product", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Update an existing product
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<ProductDto>>> UpdateProduct(int id, [FromBody] UpdateProductRequest request)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var asortymentyManager = _sferaService.GetManager("Asortymenty");
                if (asortymentyManager == null)
                {
                    return (statusCode: 500, dto: (ProductDto?)null, error: "Failed to get Asortymenty manager", errors: (List<string>?)null);
                }

                dynamic? asortyment = null;
                foreach (var a in DynamicPropertyHelper.SafeGetAll((object)asortymentyManager))
                {
                    if (DynamicPropertyHelper.GetId(a) == id)
                    {
                        asortyment = a;
                        break;
                    }
                }

                if (asortyment == null)
                {
                    return (statusCode: 404, dto: (ProductDto?)null, error: $"Product with ID {id} not found", errors: (List<string>?)null);
                }

                using (var edytowanyAsortyment = asortymentyManager.Znajdz(asortyment))
                {
                    if (edytowanyAsortyment == null)
                    {
                        return (statusCode: 404, dto: (ProductDto?)null, error: $"Product with ID {id} not found", errors: (List<string>?)null);
                    }

                    dynamic dane = edytowanyAsortyment.Dane;

                    if (!string.IsNullOrEmpty(request.Name))
                    {
                        dane.Nazwa = request.Name;
                    }

                    if (!string.IsNullOrEmpty(request.Description))
                    {
                        dane.Opis = request.Description;
                    }

                    if (!string.IsNullOrEmpty(request.EAN))
                    {
                        dane.EAN = request.EAN;
                    }

                    if (!string.IsNullOrEmpty(request.PKWiU))
                    {
                        dane.PKWIU = request.PKWiU;
                    }

                    if (request.PriceNet.HasValue)
                    {
                        dane.CenaNetto = request.PriceNet.Value;
                    }

                    if (request.Weight.HasValue)
                    {
                        dane.Masa = request.Weight.Value;
                    }

                    if (request.Volume.HasValue)
                    {
                        dane.Objetosc = request.Volume.Value;
                    }

                    if (request.IsActive.HasValue)
                    {
                        dane.Aktywny = request.IsActive.Value;
                    }

                    if ((bool)edytowanyAsortyment.Zapisz())
                    {
                        return (statusCode: 200, dto: (ProductDto?)MapToDto(dane), error: (string?)null, errors: (List<string>?)null);
                    }
                    else
                    {
                        var errors = GetBusinessObjectErrors(edytowanyAsortyment);
                        return (statusCode: 400, dto: (ProductDto?)null, error: "Failed to update product", errors: (List<string>?)errors);
                    }
                }
            });

            if (result.statusCode == 500)
            {
                return StatusCode(500, ApiResponse<object>.Error(result.error ?? "Internal error"));
            }
            if (result.statusCode == 404)
            {
                return NotFound(ApiResponse<ProductDto>.Error(result.error ?? "Not found"));
            }
            if (result.statusCode == 400)
            {
                return BadRequest(ApiResponse<ProductDto>.Error(result.error ?? "Bad request", result.errors ?? new List<string>()));
            }

            _logger.LogInformation("Updated product {Id}", id);
            return Ok(ApiResponse<ProductDto>.Ok(result.dto!, "Product updated successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating product {Id}", id);
            return StatusCode(500, ApiResponse<ProductDto>.Error("Error updating product", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Delete a product
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteProduct(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var asortymentyManager = _sferaService.GetManager("Asortymenty");
                if (asortymentyManager == null)
                {
                    return (statusCode: 500, success: false, error: "Failed to get Asortymenty manager", errors: (List<string>?)null);
                }

                dynamic? asortyment = null;
                foreach (var a in DynamicPropertyHelper.SafeGetAll((object)asortymentyManager))
                {
                    if (DynamicPropertyHelper.GetId(a) == id)
                    {
                        asortyment = a;
                        break;
                    }
                }

                if (asortyment == null)
                {
                    return (statusCode: 404, success: false, error: $"Product with ID {id} not found", errors: (List<string>?)null);
                }

                using (var usuwanyAsortyment = asortymentyManager.Znajdz(asortyment))
                {
                    if (usuwanyAsortyment == null)
                    {
                        return (statusCode: 404, success: false, error: $"Product with ID {id} not found", errors: (List<string>?)null);
                    }

                    if ((bool)usuwanyAsortyment.Usun())
                    {
                        return (statusCode: 200, success: true, error: (string?)null, errors: (List<string>?)null);
                    }
                    else
                    {
                        var errors = GetBusinessObjectErrors(usuwanyAsortyment);
                        return (statusCode: 400, success: false, error: "Failed to delete product", errors: (List<string>?)errors);
                    }
                }
            });

            if (result.statusCode == 500)
            {
                return StatusCode(500, ApiResponse<object>.Error(result.error ?? "Internal error"));
            }
            if (result.statusCode == 404)
            {
                return NotFound(ApiResponse<bool>.Error(result.error ?? "Not found"));
            }
            if (result.statusCode == 400)
            {
                return BadRequest(ApiResponse<bool>.Error(result.error ?? "Bad request", result.errors ?? new List<string>()));
            }

            _logger.LogInformation("Deleted product {Id}", id);
            return Ok(ApiResponse<bool>.Ok(true, "Product deleted successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting product {Id}", id);
            return StatusCode(500, ApiResponse<bool>.Error("Error deleting product", new List<string> { ex.Message }));
        }
    }

    #region Units of measure

    /// <summary>
    /// Get all units of measure assigned to a product with their conversions to the base unit
    /// </summary>
    /// <param name="id">Product ID</param>
    [HttpGet("{id}/units")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductUnitDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<ProductUnitDto>>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<List<ProductUnitDto>>>> GetProductUnits(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var asortymentyManager = _sferaService.GetManager("Asortymenty");
                if (asortymentyManager == null)
                {
                    return (statusCode: 500, data: (List<ProductUnitDto>?)null, error: (string?)"Failed to get Asortymenty manager", errors: (List<string>?)null);
                }

                var asortyment = DynamicPropertyHelper.FindById((object)asortymentyManager, id);
                if (asortyment == null)
                {
                    return (statusCode: 404, data: (List<ProductUnitDto>?)null, error: (string?)$"Product with ID {id} not found", errors: (List<string>?)null);
                }

                return (statusCode: 200, data: (List<ProductUnitDto>?)MapUnits(asortyment), error: (string?)null, errors: (List<string>?)null);
            });

            var failure = MapFailure<List<ProductUnitDto>>(result.statusCode, result.error, result.errors);
            if (failure != null) return failure;

            return Ok(ApiResponse<List<ProductUnitDto>>.Ok(result.data!));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting units of product {Id}", id);
            return StatusCode(500, ApiResponse<List<ProductUnitDto>>.Error("Error retrieving product units", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Add a unit of measure to a product with a conversion to the base unit
    /// </summary>
    /// <remarks>
    /// Example: 1 "op" = 12 "szt" → UnitSymbol "op", NewUnitCount 1, BaseUnitCount 12.
    /// When both counts are omitted the converter is copied from the unit dictionary (SDK 2-argument overload).
    /// Optionally sets the new unit as the default sale / purchase unit.
    /// </remarks>
    /// <param name="id">Product ID</param>
    /// <param name="request">Unit to add</param>
    [HttpPost("{id}/units")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductUnitDto>>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<List<ProductUnitDto>>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<List<ProductUnitDto>>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<List<ProductUnitDto>>>> AddProductUnit(int id, [FromBody] AddProductUnitRequest request)
    {
        if (request.NewUnitCount.HasValue != request.BaseUnitCount.HasValue)
        {
            return BadRequest(ApiResponse<List<ProductUnitDto>>.Error("NewUnitCount and BaseUnitCount must be provided together"));
        }

        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var asortymentyManager = _sferaService.GetManager("Asortymenty");
                if (asortymentyManager == null)
                {
                    return (statusCode: 500, data: (List<ProductUnitDto>?)null, error: (string?)"Failed to get Asortymenty manager", errors: (List<string>?)null);
                }

                var asortyment = DynamicPropertyHelper.FindById((object)asortymentyManager, id);
                if (asortyment == null)
                {
                    return (statusCode: 404, data: (List<ProductUnitDto>?)null, error: (string?)$"Product with ID {id} not found", errors: (List<string>?)null);
                }

                var newUnit = FindUnitOfMeasureBySymbol(request.UnitSymbol);
                if (newUnit == null)
                {
                    return (statusCode: 400, data: (List<ProductUnitDto>?)null, error: (string?)$"Unit of measure '{request.UnitSymbol}' not found in the units dictionary", errors: (List<string>?)null);
                }

                using (var edytowanyAsortyment = asortymentyManager.Znajdz(asortyment))
                {
                    if (edytowanyAsortyment == null)
                    {
                        return (statusCode: 404, data: (List<ProductUnitDto>?)null, error: (string?)$"Product with ID {id} not found", errors: (List<string>?)null);
                    }

                    dynamic dane = edytowanyAsortyment.Dane;
                    dynamic jednostki = edytowanyAsortyment.JednostkiMiary;
                    var newUnitId = DynamicPropertyHelper.GetId(newUnit);

                    if (FindProductUnitByDictionaryId(dane, newUnitId) != null)
                    {
                        return (statusCode: 400, data: (List<ProductUnitDto>?)null, error: (string?)$"Unit '{request.UnitSymbol}' is already assigned to product {id}", errors: (List<string>?)null);
                    }

                    dynamic? baseUnit;
                    if (!string.IsNullOrWhiteSpace(request.BaseUnitSymbol))
                    {
                        baseUnit = FindUnitOfMeasureBySymbol(request.BaseUnitSymbol);
                        if (baseUnit == null)
                        {
                            return (statusCode: 400, data: (List<ProductUnitDto>?)null, error: (string?)$"Base unit of measure '{request.BaseUnitSymbol}' not found in the units dictionary", errors: (List<string>?)null);
                        }
                    }
                    else
                    {
                        var podstawowa = DynamicPropertyHelper.GetProperty(dane, "PodstawowaJednostkaMiaryAsortymentu");
                        baseUnit = podstawowa != null ? DynamicPropertyHelper.GetProperty(podstawowa, "JednostkaMiary") : null;
                        if (baseUnit == null)
                        {
                            return (statusCode: 400, data: (List<ProductUnitDto>?)null, error: (string?)$"Product {id} has no base unit; provide BaseUnitSymbol explicitly", errors: (List<string>?)null);
                        }
                    }

                    if (DynamicPropertyHelper.GetId(baseUnit) == newUnitId)
                    {
                        return (statusCode: 400, data: (List<ProductUnitDto>?)null, error: (string?)"UnitSymbol and BaseUnitSymbol must be different units", errors: (List<string>?)null);
                    }

                    dynamic? addedUnit;
                    try
                    {
                        if (request.NewUnitCount.HasValue && request.BaseUnitCount.HasValue)
                        {
                            addedUnit = jednostki.DodajJednostkeMiary(newUnit, baseUnit, request.NewUnitCount.Value, request.BaseUnitCount.Value);
                        }
                        else
                        {
                            addedUnit = jednostki.DodajJednostkeMiary(newUnit, baseUnit);
                        }
                    }
                    catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
                    {
                        return (statusCode: 400, data: (List<ProductUnitDto>?)null, error: (string?)"Nexo rejected the unit", errors: (List<string>?)new List<string> { ex.Message });
                    }

                    if (request.SetAsSaleUnit || request.SetAsPurchaseUnit)
                    {
                        // DodajJednostkeMiary returns the new JednostkaMiaryAsortymentu; fall back to the entity collection.
                        var productUnit = addedUnit ?? FindProductUnitByDictionaryId(dane, newUnitId);
                        if (productUnit == null)
                        {
                            return (statusCode: 400, data: (List<ProductUnitDto>?)null, error: (string?)$"Unit '{request.UnitSymbol}' was not attached to product {id} by Nexo", errors: (List<string>?)null);
                        }

                        if (request.SetAsSaleUnit)
                        {
                            dane.JednostkaSprzedazy = productUnit;
                        }

                        if (request.SetAsPurchaseUnit)
                        {
                            dane.JednostkaZakupu = productUnit;
                        }
                    }

                    if ((bool)edytowanyAsortyment.Zapisz())
                    {
                        return (statusCode: 201, data: (List<ProductUnitDto>?)MapUnits(dane), error: (string?)null, errors: (List<string>?)null);
                    }

                    var errors = GetBusinessObjectErrors(edytowanyAsortyment);
                    return (statusCode: 400, data: (List<ProductUnitDto>?)null, error: (string?)"Failed to add unit to product", errors: (List<string>?)errors);
                }
            });

            var failure = MapFailure<List<ProductUnitDto>>(result.statusCode, result.error, result.errors);
            if (failure != null) return failure;

            _logger.LogInformation("Added unit {Unit} to product {Id}", request.UnitSymbol, id);
            return CreatedAtAction(
                nameof(GetProductUnits),
                new { id },
                ApiResponse<List<ProductUnitDto>>.Ok(result.data!, "Unit added successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding unit {Unit} to product {Id}", request.UnitSymbol, id);
            return StatusCode(500, ApiResponse<List<ProductUnitDto>>.Error("Error adding product unit", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Remove a unit of measure from a product
    /// </summary>
    /// <remarks>
    /// The base unit cannot be removed (400). Nexo may also refuse to remove a unit that is still referenced by
    /// other converters or documents — SDK errors are returned as 400.
    /// </remarks>
    /// <param name="id">Product ID</param>
    /// <param name="unitSymbol">Symbol of the unit to remove (alias accepted)</param>
    [HttpDelete("{id}/units/{unitSymbol}")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductUnitDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<ProductUnitDto>>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<List<ProductUnitDto>>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<List<ProductUnitDto>>>> RemoveProductUnit(int id, string unitSymbol)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var asortymentyManager = _sferaService.GetManager("Asortymenty");
                if (asortymentyManager == null)
                {
                    return (statusCode: 500, data: (List<ProductUnitDto>?)null, error: (string?)"Failed to get Asortymenty manager", errors: (List<string>?)null);
                }

                var asortyment = DynamicPropertyHelper.FindById((object)asortymentyManager, id);
                if (asortyment == null)
                {
                    return (statusCode: 404, data: (List<ProductUnitDto>?)null, error: (string?)$"Product with ID {id} not found", errors: (List<string>?)null);
                }

                using (var edytowanyAsortyment = asortymentyManager.Znajdz(asortyment))
                {
                    if (edytowanyAsortyment == null)
                    {
                        return (statusCode: 404, data: (List<ProductUnitDto>?)null, error: (string?)$"Product with ID {id} not found", errors: (List<string>?)null);
                    }

                    dynamic dane = edytowanyAsortyment.Dane;
                    dynamic jednostki = edytowanyAsortyment.JednostkiMiary;

                    var productUnit = ResolveProductUnit(dane, unitSymbol);
                    if (productUnit == null)
                    {
                        return (statusCode: 404, data: (List<ProductUnitDto>?)null, error: (string?)$"Unit '{unitSymbol}' is not assigned to product {id}", errors: (List<string>?)null);
                    }

                    var productUnitId = DynamicPropertyHelper.GetId(productUnit);
                    var baseUnit = DynamicPropertyHelper.GetProperty(dane, "PodstawowaJednostkaMiaryAsortymentu");
                    if (baseUnit != null && DynamicPropertyHelper.GetId(baseUnit) == productUnitId)
                    {
                        return (statusCode: 400, data: (List<ProductUnitDto>?)null, error: (string?)$"Unit '{unitSymbol}' is the base unit of product {id} and cannot be removed; change the base unit first", errors: (List<string>?)null);
                    }

                    var dictionaryUnit = DynamicPropertyHelper.GetProperty(productUnit, "JednostkaMiary");
                    if (dictionaryUnit == null)
                    {
                        return (statusCode: 400, data: (List<ProductUnitDto>?)null, error: (string?)$"Unit '{unitSymbol}' has no dictionary unit and cannot be removed", errors: (List<string>?)null);
                    }

                    try
                    {
                        jednostki.UsunJednostkeMiary(dictionaryUnit);
                    }
                    catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
                    {
                        return (statusCode: 400, data: (List<ProductUnitDto>?)null, error: (string?)"Nexo rejected removing the unit", errors: (List<string>?)new List<string> { ex.Message });
                    }

                    if (FindProductUnitByDictionaryId(dane, DynamicPropertyHelper.GetId(dictionaryUnit)) != null)
                    {
                        return (statusCode: 400, data: (List<ProductUnitDto>?)null, error: (string?)$"Nexo did not remove unit '{unitSymbol}' from product {id} (it is still referenced by unit converters)", errors: (List<string>?)null);
                    }

                    if ((bool)edytowanyAsortyment.Zapisz())
                    {
                        return (statusCode: 200, data: (List<ProductUnitDto>?)MapUnits(dane), error: (string?)null, errors: (List<string>?)null);
                    }

                    var errors = GetBusinessObjectErrors(edytowanyAsortyment);
                    return (statusCode: 400, data: (List<ProductUnitDto>?)null, error: (string?)"Failed to remove unit from product", errors: (List<string>?)errors);
                }
            });

            var failure = MapFailure<List<ProductUnitDto>>(result.statusCode, result.error, result.errors);
            if (failure != null) return failure;

            _logger.LogInformation("Removed unit {Unit} from product {Id}", unitSymbol, id);
            return Ok(ApiResponse<List<ProductUnitDto>>.Ok(result.data!, "Unit removed successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing unit {Unit} from product {Id}", unitSymbol, id);
            return StatusCode(500, ApiResponse<List<ProductUnitDto>>.Error("Error removing product unit", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Change the base unit of measure of a product
    /// </summary>
    /// <remarks>
    /// SDK: IJednostkiMiarAsortymentu.UstawPodstawowaJednostkeMiary. Nexo only swaps the base unit when the
    /// requested dictionary unit is not already bound to this product through another converter, and may refuse
    /// the change for products with stock movements — in both cases a 400 with the SDK errors is returned.
    /// Existing converters of the old base unit are left untouched by Nexo.
    /// </remarks>
    /// <param name="id">Product ID</param>
    /// <param name="request">New base unit</param>
    [HttpPut("{id}/units/base")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductUnitDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<ProductUnitDto>>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<List<ProductUnitDto>>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<List<ProductUnitDto>>>> SetProductBaseUnit(int id, [FromBody] SetProductBaseUnitRequest request)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var asortymentyManager = _sferaService.GetManager("Asortymenty");
                if (asortymentyManager == null)
                {
                    return (statusCode: 500, data: (List<ProductUnitDto>?)null, error: (string?)"Failed to get Asortymenty manager", errors: (List<string>?)null);
                }

                var asortyment = DynamicPropertyHelper.FindById((object)asortymentyManager, id);
                if (asortyment == null)
                {
                    return (statusCode: 404, data: (List<ProductUnitDto>?)null, error: (string?)$"Product with ID {id} not found", errors: (List<string>?)null);
                }

                var newBaseUnit = FindUnitOfMeasureBySymbol(request.UnitSymbol);
                if (newBaseUnit == null)
                {
                    return (statusCode: 400, data: (List<ProductUnitDto>?)null, error: (string?)$"Unit of measure '{request.UnitSymbol}' not found in the units dictionary", errors: (List<string>?)null);
                }

                using (var edytowanyAsortyment = asortymentyManager.Znajdz(asortyment))
                {
                    if (edytowanyAsortyment == null)
                    {
                        return (statusCode: 404, data: (List<ProductUnitDto>?)null, error: (string?)$"Product with ID {id} not found", errors: (List<string>?)null);
                    }

                    dynamic dane = edytowanyAsortyment.Dane;
                    dynamic jednostki = edytowanyAsortyment.JednostkiMiary;
                    var newBaseUnitId = DynamicPropertyHelper.GetId(newBaseUnit);

                    var currentBase = DynamicPropertyHelper.GetProperty(dane, "PodstawowaJednostkaMiaryAsortymentu");
                    if (currentBase != null && DynamicPropertyHelper.GetNullableInt(currentBase, "JednostkaMiary", "Id") == newBaseUnitId)
                    {
                        return (statusCode: 200, data: (List<ProductUnitDto>?)MapUnits(dane), error: (string?)null, errors: (List<string>?)null);
                    }

                    try
                    {
                        jednostki.UstawPodstawowaJednostkeMiary(newBaseUnit);
                    }
                    catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
                    {
                        return (statusCode: 400, data: (List<ProductUnitDto>?)null, error: (string?)"Nexo rejected the base unit change", errors: (List<string>?)new List<string> { ex.Message });
                    }

                    var baseAfter = DynamicPropertyHelper.GetProperty(dane, "PodstawowaJednostkaMiaryAsortymentu");
                    if (baseAfter == null || DynamicPropertyHelper.GetNullableInt(baseAfter, "JednostkaMiary", "Id") != newBaseUnitId)
                    {
                        return (statusCode: 400, data: (List<ProductUnitDto>?)null, error: (string?)$"Nexo did not change the base unit of product {id} to '{request.UnitSymbol}' (the unit is already bound through another converter of this product)", errors: (List<string>?)null);
                    }

                    if ((bool)edytowanyAsortyment.Zapisz())
                    {
                        return (statusCode: 200, data: (List<ProductUnitDto>?)MapUnits(dane), error: (string?)null, errors: (List<string>?)null);
                    }

                    var errors = GetBusinessObjectErrors(edytowanyAsortyment);
                    return (statusCode: 400, data: (List<ProductUnitDto>?)null, error: (string?)"Failed to change base unit", errors: (List<string>?)errors);
                }
            });

            var failure = MapFailure<List<ProductUnitDto>>(result.statusCode, result.error, result.errors);
            if (failure != null) return failure;

            _logger.LogInformation("Changed base unit of product {Id} to {Unit}", id, request.UnitSymbol);
            return Ok(ApiResponse<List<ProductUnitDto>>.Ok(result.data!, "Base unit updated successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing base unit of product {Id}", id);
            return StatusCode(500, ApiResponse<List<ProductUnitDto>>.Error("Error changing base unit", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Set the default sale and/or purchase unit of a product
    /// </summary>
    /// <remarks>
    /// Both units must already be assigned to the product (see POST {id}/units). Only provided fields are changed.
    /// </remarks>
    /// <param name="id">Product ID</param>
    /// <param name="request">Default units</param>
    [HttpPut("{id}/units/defaults")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductUnitDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<ProductUnitDto>>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<List<ProductUnitDto>>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<List<ProductUnitDto>>>> SetProductDefaultUnits(int id, [FromBody] SetProductDefaultUnitsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SaleUnitSymbol) && string.IsNullOrWhiteSpace(request.PurchaseUnitSymbol))
        {
            return BadRequest(ApiResponse<List<ProductUnitDto>>.Error("Provide SaleUnitSymbol and/or PurchaseUnitSymbol"));
        }

        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var asortymentyManager = _sferaService.GetManager("Asortymenty");
                if (asortymentyManager == null)
                {
                    return (statusCode: 500, data: (List<ProductUnitDto>?)null, error: (string?)"Failed to get Asortymenty manager", errors: (List<string>?)null);
                }

                var asortyment = DynamicPropertyHelper.FindById((object)asortymentyManager, id);
                if (asortyment == null)
                {
                    return (statusCode: 404, data: (List<ProductUnitDto>?)null, error: (string?)$"Product with ID {id} not found", errors: (List<string>?)null);
                }

                using (var edytowanyAsortyment = asortymentyManager.Znajdz(asortyment))
                {
                    if (edytowanyAsortyment == null)
                    {
                        return (statusCode: 404, data: (List<ProductUnitDto>?)null, error: (string?)$"Product with ID {id} not found", errors: (List<string>?)null);
                    }

                    dynamic dane = edytowanyAsortyment.Dane;

                    if (!string.IsNullOrWhiteSpace(request.SaleUnitSymbol))
                    {
                        var saleUnit = ResolveProductUnit(dane, request.SaleUnitSymbol);
                        if (saleUnit == null)
                        {
                            return (statusCode: 400, data: (List<ProductUnitDto>?)null, error: (string?)$"Unit '{request.SaleUnitSymbol}' is not assigned to product {id}; add it first", errors: (List<string>?)null);
                        }
                        dane.JednostkaSprzedazy = saleUnit;
                    }

                    if (!string.IsNullOrWhiteSpace(request.PurchaseUnitSymbol))
                    {
                        var purchaseUnit = ResolveProductUnit(dane, request.PurchaseUnitSymbol);
                        if (purchaseUnit == null)
                        {
                            return (statusCode: 400, data: (List<ProductUnitDto>?)null, error: (string?)$"Unit '{request.PurchaseUnitSymbol}' is not assigned to product {id}; add it first", errors: (List<string>?)null);
                        }
                        dane.JednostkaZakupu = purchaseUnit;
                    }

                    if ((bool)edytowanyAsortyment.Zapisz())
                    {
                        return (statusCode: 200, data: (List<ProductUnitDto>?)MapUnits(dane), error: (string?)null, errors: (List<string>?)null);
                    }

                    var errors = GetBusinessObjectErrors(edytowanyAsortyment);
                    return (statusCode: 400, data: (List<ProductUnitDto>?)null, error: (string?)"Failed to update default units", errors: (List<string>?)errors);
                }
            });

            var failure = MapFailure<List<ProductUnitDto>>(result.statusCode, result.error, result.errors);
            if (failure != null) return failure;

            _logger.LogInformation("Updated default units of product {Id}", id);
            return Ok(ApiResponse<List<ProductUnitDto>>.Ok(result.data!, "Default units updated successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating default units of product {Id}", id);
            return StatusCode(500, ApiResponse<List<ProductUnitDto>>.Error("Error updating default units", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Kit components

    /// <summary>
    /// Get the components of a kit (komplet) product
    /// </summary>
    /// <remarks>Returns an empty list for products that are not kits.</remarks>
    /// <param name="id">Product ID</param>
    [HttpGet("{id}/components")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductComponentDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<ProductComponentDto>>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<List<ProductComponentDto>>>> GetProductComponents(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var asortymentyManager = _sferaService.GetManager("Asortymenty");
                if (asortymentyManager == null)
                {
                    return (statusCode: 500, data: (List<ProductComponentDto>?)null, error: (string?)"Failed to get Asortymenty manager", errors: (List<string>?)null);
                }

                var asortyment = DynamicPropertyHelper.FindById((object)asortymentyManager, id);
                if (asortyment == null)
                {
                    return (statusCode: 404, data: (List<ProductComponentDto>?)null, error: (string?)$"Product with ID {id} not found", errors: (List<string>?)null);
                }

                return (statusCode: 200, data: (List<ProductComponentDto>?)MapComponents(asortyment), error: (string?)null, errors: (List<string>?)null);
            });

            var failure = MapFailure<List<ProductComponentDto>>(result.statusCode, result.error, result.errors);
            if (failure != null) return failure;

            return Ok(ApiResponse<List<ProductComponentDto>>.Ok(result.data!));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting components of product {Id}", id);
            return StatusCode(500, ApiResponse<List<ProductComponentDto>>.Error("Error retrieving product components", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Add a component to a kit (komplet) product
    /// </summary>
    /// <remarks>
    /// SDK: ISkladnikiKompletu.Dodaj(symbol, quantity, unitSymbol). The product must be a kit (400 otherwise).
    /// The component is identified by ComponentProductId or ComponentSymbol; UnitSymbol defaults to the
    /// component's base unit.
    /// </remarks>
    /// <param name="id">Kit product ID</param>
    /// <param name="request">Component to add</param>
    [HttpPost("{id}/components")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductComponentDto>>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<List<ProductComponentDto>>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<List<ProductComponentDto>>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<List<ProductComponentDto>>>> AddProductComponent(int id, [FromBody] AddProductComponentRequest request)
    {
        if (request.ComponentProductId == null && string.IsNullOrWhiteSpace(request.ComponentSymbol))
        {
            return BadRequest(ApiResponse<List<ProductComponentDto>>.Error("Provide ComponentProductId or ComponentSymbol"));
        }

        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var asortymentyManager = _sferaService.GetManager("Asortymenty");
                if (asortymentyManager == null)
                {
                    return (statusCode: 500, data: (List<ProductComponentDto>?)null, error: (string?)"Failed to get Asortymenty manager", errors: (List<string>?)null);
                }

                var asortyment = DynamicPropertyHelper.FindById((object)asortymentyManager, id);
                if (asortyment == null)
                {
                    return (statusCode: 404, data: (List<ProductComponentDto>?)null, error: (string?)$"Product with ID {id} not found", errors: (List<string>?)null);
                }

                if (!IsKitProduct(asortyment))
                {
                    return (statusCode: 400, data: (List<ProductComponentDto>?)null, error: (string?)$"Product {id} is not a kit (komplet); components can only be added to kits", errors: (List<string>?)null);
                }

                var component = request.ComponentProductId.HasValue
                    ? DynamicPropertyHelper.FindById((object)asortymentyManager, request.ComponentProductId.Value)
                    : FindProductEntityBySymbol(asortymentyManager, request.ComponentSymbol!);
                if (component == null)
                {
                    var componentRef = request.ComponentProductId.HasValue ? $"ID {request.ComponentProductId.Value}" : $"symbol '{request.ComponentSymbol}'";
                    return (statusCode: 400, data: (List<ProductComponentDto>?)null, error: (string?)$"Component product with {componentRef} not found", errors: (List<string>?)null);
                }

                var componentId = DynamicPropertyHelper.GetId(component);
                var componentSymbol = DynamicPropertyHelper.GetString(component, "Symbol");
                if (componentId == id)
                {
                    return (statusCode: 400, data: (List<ProductComponentDto>?)null, error: (string?)"A kit cannot contain itself as a component", errors: (List<string>?)null);
                }
                if (string.IsNullOrWhiteSpace(componentSymbol))
                {
                    return (statusCode: 400, data: (List<ProductComponentDto>?)null, error: (string?)$"Component product {componentId} has no symbol", errors: (List<string>?)null);
                }

                using (var edytowanyAsortyment = asortymentyManager.Znajdz(asortyment))
                {
                    if (edytowanyAsortyment == null)
                    {
                        return (statusCode: 404, data: (List<ProductComponentDto>?)null, error: (string?)$"Product with ID {id} not found", errors: (List<string>?)null);
                    }

                    dynamic dane = edytowanyAsortyment.Dane;
                    dynamic skladniki = edytowanyAsortyment.Skladniki;

                    if (FindComponentByProductId(dane, componentId) != null)
                    {
                        return (statusCode: 400, data: (List<ProductComponentDto>?)null, error: (string?)$"Product {componentId} is already a component of kit {id}; remove it first to change the quantity", errors: (List<string>?)null);
                    }

                    try
                    {
                        // (string symbol, decimal quantity, string unitSymbol) — empty unit symbol = component's base unit.
                        skladniki.Dodaj(componentSymbol, request.Quantity, request.UnitSymbol ?? string.Empty);
                    }
                    catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
                    {
                        return (statusCode: 400, data: (List<ProductComponentDto>?)null, error: (string?)"Nexo rejected the component", errors: (List<string>?)new List<string> { ex.Message });
                    }

                    if ((bool)edytowanyAsortyment.Zapisz())
                    {
                        return (statusCode: 201, data: (List<ProductComponentDto>?)MapComponents(dane), error: (string?)null, errors: (List<string>?)null);
                    }

                    var errors = GetBusinessObjectErrors(edytowanyAsortyment);
                    return (statusCode: 400, data: (List<ProductComponentDto>?)null, error: (string?)"Failed to add component to kit", errors: (List<string>?)errors);
                }
            });

            var failure = MapFailure<List<ProductComponentDto>>(result.statusCode, result.error, result.errors);
            if (failure != null) return failure;

            _logger.LogInformation("Added component {Component} to kit {Id}", request.ComponentSymbol ?? request.ComponentProductId?.ToString(), id);
            return CreatedAtAction(
                nameof(GetProductComponents),
                new { id },
                ApiResponse<List<ProductComponentDto>>.Ok(result.data!, "Component added successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding component to kit {Id}", id);
            return StatusCode(500, ApiResponse<List<ProductComponentDto>>.Error("Error adding kit component", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Remove a component from a kit (komplet) product
    /// </summary>
    /// <param name="id">Kit product ID</param>
    /// <param name="componentProductId">ID of the component product (ProductComponentDto.ComponentProductId)</param>
    [HttpDelete("{id}/components/{componentProductId}")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductComponentDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<ProductComponentDto>>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<List<ProductComponentDto>>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<List<ProductComponentDto>>>> RemoveProductComponent(int id, int componentProductId)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var asortymentyManager = _sferaService.GetManager("Asortymenty");
                if (asortymentyManager == null)
                {
                    return (statusCode: 500, data: (List<ProductComponentDto>?)null, error: (string?)"Failed to get Asortymenty manager", errors: (List<string>?)null);
                }

                var asortyment = DynamicPropertyHelper.FindById((object)asortymentyManager, id);
                if (asortyment == null)
                {
                    return (statusCode: 404, data: (List<ProductComponentDto>?)null, error: (string?)$"Product with ID {id} not found", errors: (List<string>?)null);
                }

                using (var edytowanyAsortyment = asortymentyManager.Znajdz(asortyment))
                {
                    if (edytowanyAsortyment == null)
                    {
                        return (statusCode: 404, data: (List<ProductComponentDto>?)null, error: (string?)$"Product with ID {id} not found", errors: (List<string>?)null);
                    }

                    dynamic dane = edytowanyAsortyment.Dane;
                    dynamic skladniki = edytowanyAsortyment.Skladniki;

                    if (FindComponentByProductId(dane, componentProductId) == null)
                    {
                        return (statusCode: 404, data: (List<ProductComponentDto>?)null, error: (string?)$"Product {componentProductId} is not a component of kit {id}", errors: (List<string>?)null);
                    }

                    bool removed;
                    try
                    {
                        removed = (bool)skladniki.Usun(componentProductId);
                    }
                    catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
                    {
                        return (statusCode: 400, data: (List<ProductComponentDto>?)null, error: (string?)"Nexo rejected removing the component", errors: (List<string>?)new List<string> { ex.Message });
                    }

                    if (!removed)
                    {
                        return (statusCode: 400, data: (List<ProductComponentDto>?)null, error: (string?)$"Nexo did not remove component {componentProductId} from kit {id}", errors: (List<string>?)null);
                    }

                    if ((bool)edytowanyAsortyment.Zapisz())
                    {
                        return (statusCode: 200, data: (List<ProductComponentDto>?)MapComponents(dane), error: (string?)null, errors: (List<string>?)null);
                    }

                    var errors = GetBusinessObjectErrors(edytowanyAsortyment);
                    return (statusCode: 400, data: (List<ProductComponentDto>?)null, error: (string?)"Failed to remove component from kit", errors: (List<string>?)errors);
                }
            });

            var failure = MapFailure<List<ProductComponentDto>>(result.statusCode, result.error, result.errors);
            if (failure != null) return failure;

            _logger.LogInformation("Removed component {Component} from kit {Id}", componentProductId, id);
            return Ok(ApiResponse<List<ProductComponentDto>>.Ok(result.data!, "Component removed successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing component {Component} from kit {Id}", componentProductId, id);
            return StatusCode(500, ApiResponse<List<ProductComponentDto>>.Error("Error removing kit component", new List<string> { ex.Message }));
        }
    }

    #endregion

    /// <summary>
    /// Translates the (statusCode, error, errors) part of a lock-lambda result into an error ActionResult,
    /// or null when the status is a success.
    /// </summary>
    private ActionResult? MapFailure<T>(int statusCode, string? error, List<string>? errors)
    {
        if (statusCode == 500) return StatusCode(500, ApiResponse<T>.Error(error ?? "Internal error", errors));
        if (statusCode == 404) return NotFound(ApiResponse<T>.Error(error ?? "Not found", errors));
        if (statusCode == 400) return BadRequest(ApiResponse<T>.Error(error ?? "Bad request", errors ?? new List<string>()));
        return null;
    }

    /// <summary>
    /// Finds a dictionary unit of measure (JednostkaMiary) by symbol; aliases are honoured. Must be called inside the lock.
    /// </summary>
    private dynamic? FindUnitOfMeasureBySymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol)) return null;

        var jednostkiManager = _sferaService.GetManager("JednostkiMiar");
        if (jednostkiManager == null) return null;

        var trimmed = symbol.Trim();

        try
        {
            var found = jednostkiManager.Dane.WyszukajPoSymbolu(trimmed);
            if (found != null) return found;
        }
        catch
        {
            // Fall back to scanning the dictionary below.
        }

        foreach (var unit in DynamicPropertyHelper.SafeGetAll((object)jednostkiManager))
        {
            if (string.Equals(DynamicPropertyHelper.GetString(unit, "Symbol"), trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return unit;
            }

            foreach (var alias in DynamicPropertyHelper.GetCollection(unit, "Aliasy"))
            {
                if (string.Equals(DynamicPropertyHelper.GetString(alias, "Alias"), trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    return unit;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Finds a product unit (JednostkaMiaryAsortymentu) in Asortyment.JednostkiMiar by the dictionary unit ID.
    /// </summary>
    private static dynamic? FindProductUnitByDictionaryId(dynamic asortyment, int dictionaryUnitId)
    {
        if (dictionaryUnitId == 0) return null;

        foreach (var unit in DynamicPropertyHelper.GetCollection((object)asortyment, "JednostkiMiar"))
        {
            if (DynamicPropertyHelper.GetNullableInt(unit, "JednostkaMiary", "Id") == dictionaryUnitId)
            {
                return unit;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds a product unit (JednostkaMiaryAsortymentu) by symbol — first by the exact symbol on the product,
    /// then through the dictionary (so aliases work too). Must be called inside the lock.
    /// </summary>
    private dynamic? ResolveProductUnit(dynamic asortyment, string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol)) return null;

        var trimmed = symbol.Trim();
        foreach (var unit in DynamicPropertyHelper.GetCollection((object)asortyment, "JednostkiMiar"))
        {
            if (string.Equals(DynamicPropertyHelper.GetString(unit, "JednostkaMiary", "Symbol"), trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return unit;
            }
        }

        var dictionaryUnit = FindUnitOfMeasureBySymbol(trimmed);
        if (dictionaryUnit == null) return null;

        return FindProductUnitByDictionaryId(asortyment, DynamicPropertyHelper.GetId(dictionaryUnit));
    }

    /// <summary>
    /// Finds an Asortyment entity by symbol (case-insensitive) in the manager's data set.
    /// </summary>
    private static dynamic? FindProductEntityBySymbol(dynamic asortymentyManager, string symbol)
    {
        var trimmed = symbol.Trim();
        foreach (var a in DynamicPropertyHelper.SafeGetAll((object)asortymentyManager))
        {
            if (string.Equals(DynamicPropertyHelper.GetString(a, "Symbol"), trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return a;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds a SkladnikKompletu in Asortyment.SkladnikiKompletu by the component product ID.
    /// </summary>
    private static dynamic? FindComponentByProductId(dynamic asortyment, int componentProductId)
    {
        foreach (var skladnik in DynamicPropertyHelper.GetCollection((object)asortyment, "SkladnikiKompletu"))
        {
            if (DynamicPropertyHelper.GetNullableInt(skladnik, "Skladnik", "Id") == componentProductId)
            {
                return skladnik;
            }
        }

        return null;
    }

    /// <summary>
    /// Determines whether the product kind (Asortyment.Rodzaj — a RodzajAsortymentu dictionary entity, not an enum)
    /// is a kit. Uses the SDK extension RodzajeAsortymentowExtensions.CzyKomplet via reflection (extension methods
    /// cannot be dispatched dynamically) and falls back to Rodzaj_Id == 2 (komplet, see ProductType.Set).
    /// </summary>
    private static bool IsKitProduct(dynamic asortyment)
    {
        var rodzaj = DynamicPropertyHelper.GetProperty(asortyment, "Rodzaj");
        if (rodzaj != null)
        {
            try
            {
                var extensions = Type.GetType("InsERT.Moria.ModelDanych.RodzajeAsortymentowExtensions, InsERT.Moria.API");
                var czyKomplet = extensions?.GetMethod("CzyKomplet");
                if (czyKomplet != null)
                {
                    object? kitFlag = czyKomplet.Invoke(null, new object[] { rodzaj });
                    if (kitFlag is bool isKit) return isKit;
                }
            }
            catch
            {
                // Fall back to the numeric kind below.
            }
        }

        return DynamicPropertyHelper.GetNullableInt(asortyment, "Rodzaj_Id") == (int)ProductType.Set;
    }

    private dynamic? GetUnit(string symbol)
    {
        if (string.IsNullOrEmpty(symbol)) return null;

        var jednostkiManager = _sferaService.GetManagerByType("InsERT.Moria.ModelDanych", "InsERT.Moria.ModelDanych.Jednostki");
        if (jednostkiManager == null) return null;

        foreach (var j in DynamicPropertyHelper.SafeGetAll((object)jednostkiManager))
        {
            if (DynamicPropertyHelper.GetString(j, "Symbol") == symbol)
            {
                return j;
            }
        }
        return null;
    }

    private static ProductDto MapToDto(dynamic asortyment)
    {
        var dto = new ProductDto
        {
            // Basic info
            Id = DynamicPropertyHelper.GetId(asortyment),
            Symbol = DynamicPropertyHelper.GetString(asortyment, "Symbol"),
            Name = DynamicPropertyHelper.GetString(asortyment, "Nazwa"),
            Description = DynamicPropertyHelper.GetString(asortyment, "Opis"),
            FullCharacteristics = DynamicPropertyHelper.GetString(asortyment, "PelnaCharakterystyka"),
            EAN = DynamicPropertyHelper.GetString(asortyment, "EAN"),
            PKWiU = DynamicPropertyHelper.GetString(asortyment, "PKWIU"),
            CnCode = DynamicPropertyHelper.GetString(asortyment, "KodCN"),
            SWW = DynamicPropertyHelper.GetString(asortyment, "SWW"),

            // Variant info
            VariantOriginalName = DynamicPropertyHelper.GetString(asortyment, "OryginalnaNazwaWariantu"),
            VariantOriginalDescription = DynamicPropertyHelper.GetString(asortyment, "OryginalnyOpisWariantu"),
            VariantNumber = DynamicPropertyHelper.GetNullableInt(asortyment, "LpWariantu"),
            ParentProductId = DynamicPropertyHelper.GetNullableInt(asortyment, "AsortymentId"),
            ModelId = DynamicPropertyHelper.GetNullableInt(asortyment, "Model_Id"),

            // Type and classification
            Type = (ProductType)(DynamicPropertyHelper.GetNullableInt(asortyment, "Rodzaj_Id") ?? 0),
            GroupId = DynamicPropertyHelper.GetNullableInt(asortyment, "Grupa_Id"),

            // Units
            SaleUnit = DynamicPropertyHelper.GetString(asortyment, "JednostkaSprzedazy", "Symbol"),
            PurchaseUnit = DynamicPropertyHelper.GetString(asortyment, "JednostkaZakupu", "Symbol"),
            DefaultSalesQuantity = DynamicPropertyHelper.GetNullableDecimal(asortyment, "DomyslnaIloscSprzedazy"),
            DefaultPurchaseQuantity = DynamicPropertyHelper.GetNullableDecimal(asortyment, "DomyslnaIloscZakupu"),

            // Pricing
            PriceNet = DynamicPropertyHelper.GetNullableDecimal(asortyment, "CenaNetto"),
            PriceGross = DynamicPropertyHelper.GetNullableDecimal(asortyment, "CenaBrutto"),
            RecordPrice = DynamicPropertyHelper.GetNullableDecimal(asortyment, "CenaEwidencyjna"),
            LaborCost = DynamicPropertyHelper.GetNullableDecimal(asortyment, "KosztRobocizny"),
            AutoCalculatePrice = DynamicPropertyHelper.GetBool(asortyment, "WyliczajAutomatycznieCene"),
            CalculationFromValue = DynamicPropertyHelper.GetNullableInt(asortyment, "WyliczenieZWartosci"),
            PriceLevelId = DynamicPropertyHelper.GetNullableInt(asortyment, "PoziomCenId"),

            // VAT
            VatMarginEnabled = DynamicPropertyHelper.GetBool(asortyment, "PodlegaObrotowiVATMarza"),
            ReverseCharge = DynamicPropertyHelper.GetNullableInt(asortyment, "PodlegaOdwrotnemuObciazeniu"),
            FeeSubjectToVat = DynamicPropertyHelper.GetNullableBool(asortyment, "OplataPodlegaVat") ?? true,

            // Physical properties
            Weight = DynamicPropertyHelper.GetNullableDecimal(asortyment, "Masa"),
            Volume = DynamicPropertyHelper.GetNullableDecimal(asortyment, "Objetosc"),

            // Status and flags
            IsActive = DynamicPropertyHelper.GetBool(asortyment, "Aktywny"),
            IsDeleted = DynamicPropertyHelper.GetBool(asortyment, "IsInRecycleBin"),
            IsDiscounted = DynamicPropertyHelper.GetBool(asortyment, "Przeceniony"),
            IsOpenPrice = DynamicPropertyHelper.GetBool(asortyment, "OtwartaCena"),
            RequiresWeighing = DynamicPropertyHelper.GetBool(asortyment, "PrzeznaczonyDoWazenia"),
            Markers = DynamicPropertyHelper.GetNullableInt(asortyment, "Znaczniki"),
            CustomFlagId = DynamicPropertyHelper.GetNullableInt(asortyment, "FlagaWlasna_Id"),

            // Sales channels
            EcommerceEnabled = DynamicPropertyHelper.GetBool(asortyment, "SklepInternetowy"),
            MobileSalesEnabled = DynamicPropertyHelper.GetBool(asortyment, "SprzedazMobilna"),
            AuctionServiceEnabled = DynamicPropertyHelper.GetBool(asortyment, "SerwisAukcyjny"),

            // Delivery times
            CustomerDeliveryDays = DynamicPropertyHelper.GetNullableInt(asortyment, "LiczbaDniDoRealizacjiOdbiorcy"),
            SupplierDeliveryDays = DynamicPropertyHelper.GetNullableInt(asortyment, "LiczbaDniDoRealizacjiDostawcy"),

            // Expiry control
            ExpiryControlEnabled = DynamicPropertyHelper.GetBool(asortyment, "TerminWaznosciKontrola"),
            ExpiryDays = DynamicPropertyHelper.GetNullableInt(asortyment, "TerminWaznosciLiczbaDni"),

            // Batch management
            BatchSplitMethod = DynamicPropertyHelper.GetNullableInt(asortyment, "SposobRozbiciaNaPartie"),
            RequireBatchNumber = DynamicPropertyHelper.GetNullableInt(asortyment, "WymagajNumeruPartii"),
            RequireBatchExpiry = DynamicPropertyHelper.GetNullableInt(asortyment, "WymagajTerminuWaznosciPartii"),
            CheckBatchUniqueness = DynamicPropertyHelper.GetNullableInt(asortyment, "SprawdzajUnikalnoscNumerowPartii"),
            BlockOnDuplicateBatch = DynamicPropertyHelper.GetBool(asortyment, "BlokujPrzyBrakuUnikalnosci"),

            // Additional fees and taxes
            AdditionalFeeType = DynamicPropertyHelper.GetNullableInt(asortyment, "RodzajDodatkowejOplaty"),
            SplitPayment = DynamicPropertyHelper.GetNullableInt(asortyment, "PodlegaPodzielonejPlatnosci"),
            JpkVatGroup = DynamicPropertyHelper.GetNullableInt(asortyment, "GrupaAsortymentuJpkVat"),
            SugarTax = DynamicPropertyHelper.GetBool(asortyment, "OplataCukrowa"),
            CaffeineTax = DynamicPropertyHelper.GetBool(asortyment, "OplataKofeinowa"),
            ForFee = DynamicPropertyHelper.GetBool(asortyment, "PodlegaOplacieNaFOR"),

            // Sugar tax details
            BeverageVolume = DynamicPropertyHelper.GetNullableDecimal(asortyment, "ObjetoscNapojuWJednostcePodstawowej"),
            SugarContent = DynamicPropertyHelper.GetNullableDecimal(asortyment, "ZawartoscCukru"),
            VariableSugarFee = DynamicPropertyHelper.GetBool(asortyment, "StosujTylkoCzescZmiennaOplatyCukrowej"),
            HasOtherSweeteners = DynamicPropertyHelper.GetBool(asortyment, "ZawieraInneSubstancjeSlodzace"),
            IsElectrolyteDrink = DynamicPropertyHelper.GetBool(asortyment, "NapojWeglowodanowoElektrolityczny"),

            // Intrastat
            IncludedInIntrastat = DynamicPropertyHelper.GetBool(asortyment, "UwzglednianyWIntrastacie"),
            DefaultCountryOfOriginId = DynamicPropertyHelper.GetNullableInt(asortyment, "DomyslnePanstwoPochodzeniaId"),
            OriginMethod = DynamicPropertyHelper.GetNullableInt(asortyment, "SposobPobieraniaPanstwaPochodzenia"),
            IntrastatDescMethod = DynamicPropertyHelper.GetNullableInt(asortyment, "SposobPobieraniaOpisuDoIntrastatu"),
            IntrastatDescription = DynamicPropertyHelper.GetString(asortyment, "OpisDoIntrastatu"),

            // Messages
            DisplayMessage = DynamicPropertyHelper.GetBool(asortyment, "WyswietlajKomunikat"),
            MessageText = DynamicPropertyHelper.GetString(asortyment, "TekstKomunikatu"),
            MessageDisplayType = DynamicPropertyHelper.GetNullableInt(asortyment, "WyswietlajKomunikatJako"),

            // External integration
            ExternalId = DynamicPropertyHelper.GetNullableInt(asortyment, "IdZewnetrzny"),
            WebsiteUrl = DynamicPropertyHelper.GetString(asortyment, "StronaWWW"),
            Notes = DynamicPropertyHelper.GetString(asortyment, "Uwagi"),

            // Related entities
            RelatedProductId = DynamicPropertyHelper.GetNullableInt(asortyment, "AsortymentPowiazany_Id"),
            RecServiceId = DynamicPropertyHelper.GetNullableInt(asortyment, "UslugaRec_Id"),
            FundId = DynamicPropertyHelper.GetNullableInt(asortyment, "Fundusz_Id"),
            IntegrationAccountId = DynamicPropertyHelper.GetNullableInt(asortyment, "KontoIntegracjiId")
        };

        // Determine if this is a variant
        dto.IsVariant = dto.ModelId != null;

        // Units of measure with conversions to the base unit (e.g. thread spool "szt" = 5000 "m").
        dto.Units = MapUnits(asortyment);

        // Kit (komplet) composition — components are only meaningful for kits.
        bool isKit = IsKitProduct(asortyment);
        dto.IsKit = isKit;
        if (isKit)
        {
            dto.Components = MapComponents(asortyment);
        }

        // Try to get VAT rate info from StawkaVatSprzedazy navigation property
        var stawkaVat = DynamicPropertyHelper.GetProperty(asortyment, "StawkaVatSprzedazy");
        if (stawkaVat != null)
        {
            dto.VatRate = DynamicPropertyHelper.GetString(stawkaVat, "Symbol");
            dto.VatRateSalesId = DynamicPropertyHelper.GetProperty(stawkaVat, "Id")?.ToString();
        }

        var stawkaVatKupno = DynamicPropertyHelper.GetProperty(asortyment, "StawkaVatKupno");
        if (stawkaVatKupno != null)
        {
            dto.VatRatePurchaseId = DynamicPropertyHelper.GetProperty(stawkaVatKupno, "Id")?.ToString();
        }

        // Try to get currency ID
        var waluta = DynamicPropertyHelper.GetProperty(asortyment, "WalutaCenyEwidencyjnej");
        if (waluta != null)
        {
            dto.CurrencyId = DynamicPropertyHelper.GetProperty(waluta, "Id")?.ToString();
        }

        // Try to get group name from Grupa navigation property
        var grupa = DynamicPropertyHelper.GetProperty(asortyment, "Grupa");
        if (grupa != null)
        {
            dto.GroupName = DynamicPropertyHelper.GetString(grupa, "Nazwa");
        }

        // Try to get substitutes group GUID
        var grupaZamiennikow = DynamicPropertyHelper.GetProperty(asortyment, "GrupaZamiennikow");
        if (grupaZamiennikow != null)
        {
            dto.SubstitutesGroup = grupaZamiennikow.ToString();
        }

        // SDK 61.0.0: external (e-commerce) warehouse stock levels.
        // The collection name (StanyMagazynoweZewnetrzne) is verified from the SDK 61.0.0
        // model diff; the StanMagazynowyZewnetrzny field names below are best-effort and
        // should be confirmed against a live instance via debug/item-properties.
        // Null-safe: on older SDKs / products without external warehouses this stays empty.
        foreach (var stan in DynamicPropertyHelper.GetCollection((object)asortyment, "StanyMagazynoweZewnetrzne"))
        {
            var magazynZewnetrzny = DynamicPropertyHelper.GetProperty(stan, "MagazynZewnetrzny");
            dto.ExternalStocks.Add(new ExternalWarehouseStockDto
            {
                Quantity = DynamicPropertyHelper.GetNullableDecimal(stan, "Ilosc"),
                ExternalWarehouseName = magazynZewnetrzny != null
                    ? DynamicPropertyHelper.GetString(magazynZewnetrzny, "Nazwa")
                    : null,
                ExternalWarehouseId = magazynZewnetrzny != null
                    ? DynamicPropertyHelper.GetString(magazynZewnetrzny, "IdZewnetrzny")
                    : null
            });
        }

        return dto;
    }

    /// <summary>
    /// Maps to lightweight DTO for list views (minimal fields for performance)
    /// </summary>
    private static ProductListItemDto MapToListItemDto(dynamic asortyment)
    {
        var dto = new ProductListItemDto
        {
            Id = DynamicPropertyHelper.GetId(asortyment),
            Symbol = DynamicPropertyHelper.GetString(asortyment, "Symbol") ?? "",
            Name = DynamicPropertyHelper.GetString(asortyment, "Nazwa") ?? "",
            Price = DynamicPropertyHelper.GetNullableDecimal(asortyment, "CenaEwidencyjna"),
            GroupId = DynamicPropertyHelper.GetNullableInt(asortyment, "Grupa_Id"),
            IsActive = DynamicPropertyHelper.GetBool(asortyment, "Aktywny")
        };

        // Try to get group name from Grupa navigation property
        var grupa = DynamicPropertyHelper.GetProperty(asortyment, "Grupa");
        if (grupa != null)
        {
            dto.GroupName = DynamicPropertyHelper.GetString(grupa, "Nazwa");
        }

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

    /// <summary>
    /// Maps Asortyment.JednostkiMiar (JednostkaMiaryAsortymentu) with PrzelicznikJednostekMiarAsortymentu
    /// (LiczbaJednostkiNadrzednej × parent = LiczbaJednostkiPodrzednej × child) to DTOs. Null-safe: any SDK
    /// shape difference yields an empty list instead of failing the whole product.
    /// </summary>
    private static List<ProductUnitDto> MapUnits(dynamic asortyment)
    {
        var result = new List<ProductUnitDto>();

        try
        {
            var baseUnit = DynamicPropertyHelper.GetProperty(asortyment, "PodstawowaJednostkaMiaryAsortymentu");
            var baseSymbol = baseUnit != null ? DynamicPropertyHelper.GetString(baseUnit, "JednostkaMiary", "Symbol") : null;
            var baseId = baseUnit != null ? DynamicPropertyHelper.GetId(baseUnit) : 0;
            var saleId = DynamicPropertyHelper.GetProperty(asortyment, "JednostkaSprzedazy") is { } sale ? DynamicPropertyHelper.GetId(sale) : 0;
            var purchaseId = DynamicPropertyHelper.GetProperty(asortyment, "JednostkaZakupu") is { } purchase ? DynamicPropertyHelper.GetId(purchase) : 0;
            var warehouseId = DynamicPropertyHelper.GetProperty(asortyment, "JednostkaMagazynowa") is { } warehouse ? DynamicPropertyHelper.GetId(warehouse) : 0;

            foreach (var unit in DynamicPropertyHelper.GetCollection(asortyment, "JednostkiMiar"))
            {
                var id = DynamicPropertyHelper.GetId(unit);
                var symbol = DynamicPropertyHelper.GetString(unit, "JednostkaMiary", "Symbol");
                var dto = new ProductUnitDto
                {
                    Id = id,
                    Symbol = symbol,
                    Name = DynamicPropertyHelper.GetString(unit, "JednostkaMiary", "Nazwa"),
                    IsBase = baseId != 0 && id == baseId,
                    IsSale = saleId != 0 && id == saleId,
                    IsPurchase = purchaseId != 0 && id == purchaseId,
                    IsWarehouse = warehouseId != 0 && id == warehouseId,
                    Precision = DynamicPropertyHelper.GetNullableInt(unit, "Precyzja"),
                    Barcode = DynamicPropertyHelper.GetString(unit, "KodKreskowyOpakowania"),
                    Weight = DynamicPropertyHelper.GetNullableDecimal(unit, "Masa"),
                    Volume = DynamicPropertyHelper.GetNullableDecimal(unit, "Objetosc"),
                    BaseUnitSymbol = baseSymbol,
                    ToBaseFactor = (baseId != 0 && id == baseId) ? 1m : null,
                };

                foreach (var converterName in new[] { "PrzelicznikJednostkiNadrzednej", "PrzelicznikJednostkiPodrzednej" })
                {
                    var converter = DynamicPropertyHelper.GetProperty(unit, converterName);
                    if (converter == null) continue;

                    var parentSymbol = DynamicPropertyHelper.GetNestedString(converter, "JednostkaNadrzedna", "JednostkaMiary", "Symbol");
                    var childSymbol = DynamicPropertyHelper.GetNestedString(converter, "JednostkaPodrzedna", "JednostkaMiary", "Symbol");
                    var parentQty = DynamicPropertyHelper.GetNullableDecimal(converter, "LiczbaJednostkiNadrzednej");
                    var childQty = DynamicPropertyHelper.GetNullableDecimal(converter, "LiczbaJednostkiPodrzednej");

                    // Reflection on lazy proxies may yield an empty converter — never emit a conversion with no data.
                    if (parentSymbol == null && childSymbol == null && parentQty == null && childQty == null)
                        continue;
                    if (dto.Conversions.Any(c => c.ParentUnitSymbol == parentSymbol && c.ChildUnitSymbol == childSymbol && c.ParentQuantity == parentQty && c.ChildQuantity == childQty))
                        continue;

                    dto.Conversions.Add(new ProductUnitConversionDto
                    {
                        ParentUnitSymbol = parentSymbol,
                        ParentQuantity = parentQty,
                        ChildUnitSymbol = childSymbol,
                        ChildQuantity = childQty,
                    });

                    // 1 szt (parent, qty 1) = 5000 m (child, qty 5000) → factor to base (m) for "szt" = 5000.
                    if (dto.ToBaseFactor == null && parentQty is > 0 && childQty is > 0 && baseSymbol != null)
                    {
                        if (parentSymbol == symbol && childSymbol == baseSymbol) dto.ToBaseFactor = childQty / parentQty;
                        else if (childSymbol == symbol && parentSymbol == baseSymbol) dto.ToBaseFactor = parentQty / childQty;
                    }
                }

                result.Add(dto);
            }
        }
        catch
        {
            // Units are auxiliary data — never fail the product for them.
        }

        FillMissingFactorsFromSql(result);

        return result;
    }

    /// <summary>
    /// Deterministic fallback for conversions: the EF lazy proxies behind `PrzelicznikJednostkiNadrzednej/Podrzednej`
    /// do not always expose their members through reflection (observed: converter non-null, all fields null), so when
    /// any non-base unit still has no <c>ToBaseFactor</c> we read the converter rows straight from
    /// <c>ModelDanychContainer.PrzelicznikiJednostekMiarAsortymentu</c> (unit ids = JednostkiMiarAsortymentow.Id).
    /// Read-only, ids are integers (no user input in the SQL text).
    /// </summary>
    private static void FillMissingFactorsFromSql(List<ProductUnitDto> units)
    {
        if (units.Count < 2 || units.All(u => u.ToBaseFactor != null)) return;

        var connectionString = _unitConversionConnectionString?.Invoke();
        if (string.IsNullOrEmpty(connectionString)) return;

        try
        {
            var byId = units.Where(u => u.Id > 0).GroupBy(u => u.Id).ToDictionary(g => g.Key, g => g.First());
            if (byId.Count == 0) return;

            var inList = string.Join(",", byId.Keys);
            using var connection = new SqlConnection(connectionString);
            connection.Open();
            using var command = new SqlCommand(
                "SELECT LiczbaJednostkiNadrzednej, LiczbaJednostkiPodrzednej, JednostkaNadrzedna_Id, JednostkaPodrzedna_Id " +
                "FROM ModelDanychContainer.PrzelicznikiJednostekMiarAsortymentu " +
                $"WHERE JednostkaNadrzedna_Id IN ({inList}) OR JednostkaPodrzedna_Id IN ({inList})",
                connection);
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                decimal? parentQty = reader.IsDBNull(0) ? null : reader.GetDecimal(0);
                decimal? childQty = reader.IsDBNull(1) ? null : reader.GetDecimal(1);
                var parentId = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                var childId = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                byId.TryGetValue(parentId, out var parent);
                byId.TryGetValue(childId, out var child);
                if (parent == null && child == null) continue;

                var conversion = new ProductUnitConversionDto
                {
                    ParentUnitSymbol = parent?.Symbol,
                    ParentQuantity = parentQty,
                    ChildUnitSymbol = child?.Symbol,
                    ChildQuantity = childQty,
                };

                foreach (var unit in new[] { parent, child })
                {
                    if (unit == null) continue;
                    if (!unit.Conversions.Any(c => c.ParentUnitSymbol == conversion.ParentUnitSymbol && c.ChildUnitSymbol == conversion.ChildUnitSymbol
                                                   && c.ParentQuantity == conversion.ParentQuantity && c.ChildQuantity == conversion.ChildQuantity))
                        unit.Conversions.Add(conversion);
                }

                // 1 szt (parent, 1) = 5000 m (child, 5000): factor to base for "szt" = 5000 / 1.
                if (parentQty is > 0 && childQty is > 0 && parent != null && child != null)
                {
                    if (child.IsBase && parent.ToBaseFactor == null)
                        parent.ToBaseFactor = childQty / parentQty;
                    else if (parent.IsBase && child.ToBaseFactor == null)
                        child.ToBaseFactor = parentQty / childQty;
                }
            }
        }
        catch
        {
            // Fallback only — the reflection result stays as is.
        }
    }

    /// <summary>
    /// Maps Asortyment.SkladnikiKompletu (SkladnikKompletu) to DTOs ordered by line number. Null-safe: any SDK
    /// shape difference yields an empty list instead of failing the whole product.
    /// </summary>
    private static List<ProductComponentDto> MapComponents(dynamic asortyment)
    {
        var result = new List<ProductComponentDto>();

        try
        {
            foreach (var skladnik in DynamicPropertyHelper.GetCollection((object)asortyment, "SkladnikiKompletu"))
            {
                var componentProduct = DynamicPropertyHelper.GetProperty(skladnik, "Skladnik");
                var unit = DynamicPropertyHelper.GetProperty(skladnik, "JednostkaMiaryAsortymentu");

                var dto = new ProductComponentDto
                {
                    ComponentProductId = componentProduct != null ? DynamicPropertyHelper.GetId(componentProduct) : 0,
                    ComponentSymbol = componentProduct != null ? DynamicPropertyHelper.GetString(componentProduct, "Symbol") : null,
                    ComponentName = componentProduct != null ? DynamicPropertyHelper.GetString(componentProduct, "Nazwa") : null,
                    Quantity = DynamicPropertyHelper.GetDecimal(skladnik, "Ilosc"),
                    UnitSymbol = unit != null ? DynamicPropertyHelper.GetString(unit, "JednostkaMiary", "Symbol") : null,
                    UnitId = unit != null ? DynamicPropertyHelper.GetId(unit) : (int?)null,
                    Price = DynamicPropertyHelper.GetNullableDecimal(skladnik, "Cena"),
                    Value = DynamicPropertyHelper.GetNullableDecimal(skladnik, "Wartosc"),
                    LineNumber = DynamicPropertyHelper.GetNullableInt(skladnik, "LiczbaPorzadkowa"),
                    LockQuantity = DynamicPropertyHelper.GetBool(skladnik, "BlokujIlosc"),
                };

                result.Add(dto);
            }

            result = result.OrderBy(c => c.LineNumber ?? int.MaxValue).ThenBy(c => c.ComponentProductId).ToList();
        }
        catch
        {
            // Components are auxiliary data — never fail the product for them.
        }

        return result;
    }
}
