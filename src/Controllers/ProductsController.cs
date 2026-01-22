using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(ISferaService sferaService, ILogger<ProductsController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get all products with optional filtering
    /// </summary>
    [HttpGet]
    public ActionResult<PagedResponse<ProductDto>> GetProducts(
        [FromQuery] string? search,
        [FromQuery] ProductType? type,
        [FromQuery] bool? activeOnly,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var asortymentyManager = _sferaService.GetManager("InsERT.Moria.Asortymenty", "InsERT.Moria.Asortymenty.Asortymenty");
            if (asortymentyManager == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Asortymenty manager"));
            }

            var allAsortymenty = new List<dynamic>();
            var sourceData = activeOnly == true ? asortymentyManager.Dane.WszystkieDostepne() : asortymentyManager.Dane.Wszystkie();
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

            var mappedItems = new List<ProductDto>();
            foreach (var item in items)
            {
                mappedItems.Add(MapToDto(item));
            }

            var response = new PagedResponse<ProductDto>
            {
                Data = mappedItems,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            return Ok(response);
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
    public ActionResult<ApiResponse<ProductDto>> GetProduct(int id)
    {
        try
        {
            var asortymentyManager = _sferaService.GetManager("InsERT.Moria.Asortymenty", "InsERT.Moria.Asortymenty.Asortymenty");
            if (asortymentyManager == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Asortymenty manager"));
            }

            dynamic? asortyment = null;
            foreach (var a in asortymentyManager.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetId(a) == id)
                {
                    asortyment = a;
                    break;
                }
            }

            if (asortyment == null)
            {
                return NotFound(ApiResponse<ProductDto>.Error($"Product with ID {id} not found"));
            }

            return Ok(ApiResponse<ProductDto>.Ok(MapToDto(asortyment)));
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
    public ActionResult<ApiResponse<ProductDto>> GetProductBySymbol(string symbol)
    {
        try
        {
            var asortymentyManager = _sferaService.GetManager("InsERT.Moria.Asortymenty", "InsERT.Moria.Asortymenty.Asortymenty");
            if (asortymentyManager == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Asortymenty manager"));
            }

            var asortyment = asortymentyManager.Znajdz(symbol);
            if (asortyment == null)
            {
                return NotFound(ApiResponse<ProductDto>.Error($"Product with symbol {symbol} not found"));
            }

            return Ok(ApiResponse<ProductDto>.Ok(MapToDto(asortyment.Dane)));
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
    public ActionResult<ApiResponse<ProductDto>> GetProductByEan(string ean)
    {
        try
        {
            var asortymentyManager = _sferaService.GetManager("InsERT.Moria.Asortymenty", "InsERT.Moria.Asortymenty.Asortymenty");
            if (asortymentyManager == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Asortymenty manager"));
            }

            dynamic? asortyment = null;
            foreach (var a in asortymentyManager.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetString(a, "EAN") == ean)
                {
                    asortyment = a;
                    break;
                }
            }

            if (asortyment == null)
            {
                return NotFound(ApiResponse<ProductDto>.Error($"Product with EAN {ean} not found"));
            }

            return Ok(ApiResponse<ProductDto>.Ok(MapToDto(asortyment)));
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
    public ActionResult<ApiResponse<ProductDto>> CreateProduct([FromBody] CreateProductRequest request)
    {
        try
        {
            var asortymentyManager = _sferaService.GetManager("InsERT.Moria.Asortymenty", "InsERT.Moria.Asortymenty.Asortymenty");
            if (asortymentyManager == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Asortymenty manager"));
            }

            // Check if symbol already exists
            var existing = asortymentyManager.Znajdz(request.Symbol);
            if (existing != null)
            {
                return BadRequest(ApiResponse<ProductDto>.Error($"Product with symbol {request.Symbol} already exists"));
            }

            using (var nowyAsortyment = asortymentyManager.Utworz())
            {
                dynamic dane = nowyAsortyment.Dane;

                // Apply template if specified
                if (!string.IsNullOrEmpty(request.TemplateSymbol))
                {
                    var szablonyManager = _sferaService.GetManager("InsERT.Moria.Asortymenty", "InsERT.Moria.Asortymenty.SzablonyAsortymentu");
                    if (szablonyManager != null)
                    {
                        dynamic? szablon = null;
                        foreach (var s in szablonyManager.Dane.Wszystkie())
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

                // Set unit
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
                    _logger.LogInformation("Created product {Symbol}", request.Symbol);
                    return CreatedAtAction(
                        nameof(GetProduct),
                        new { id = DynamicPropertyHelper.GetId(dane) },
                        ApiResponse<ProductDto>.Ok(MapToDto(dane), "Product created successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(nowyAsortyment);
                    return BadRequest(ApiResponse<ProductDto>.Error("Failed to create product", errors));
                }
            }
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
    public ActionResult<ApiResponse<ProductDto>> UpdateProduct(int id, [FromBody] UpdateProductRequest request)
    {
        try
        {
            var asortymentyManager = _sferaService.GetManager("InsERT.Moria.Asortymenty", "InsERT.Moria.Asortymenty.Asortymenty");
            if (asortymentyManager == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Asortymenty manager"));
            }

            dynamic? asortyment = null;
            foreach (var a in asortymentyManager.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetId(a) == id)
                {
                    asortyment = a;
                    break;
                }
            }

            if (asortyment == null)
            {
                return NotFound(ApiResponse<ProductDto>.Error($"Product with ID {id} not found"));
            }

            using (var edytowanyAsortyment = asortymentyManager.Znajdz(asortyment))
            {
                if (edytowanyAsortyment == null)
                {
                    return NotFound(ApiResponse<ProductDto>.Error($"Product with ID {id} not found"));
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
                    _logger.LogInformation("Updated product {Id}", id);
                    return Ok(ApiResponse<ProductDto>.Ok(MapToDto(dane), "Product updated successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(edytowanyAsortyment);
                    return BadRequest(ApiResponse<ProductDto>.Error("Failed to update product", errors));
                }
            }
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
    public ActionResult<ApiResponse<bool>> DeleteProduct(int id)
    {
        try
        {
            var asortymentyManager = _sferaService.GetManager("InsERT.Moria.Asortymenty", "InsERT.Moria.Asortymenty.Asortymenty");
            if (asortymentyManager == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Asortymenty manager"));
            }

            dynamic? asortyment = null;
            foreach (var a in asortymentyManager.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetId(a) == id)
                {
                    asortyment = a;
                    break;
                }
            }

            if (asortyment == null)
            {
                return NotFound(ApiResponse<bool>.Error($"Product with ID {id} not found"));
            }

            using (var usuwanyAsortyment = asortymentyManager.Znajdz(asortyment))
            {
                if (usuwanyAsortyment == null)
                {
                    return NotFound(ApiResponse<bool>.Error($"Product with ID {id} not found"));
                }

                if ((bool)usuwanyAsortyment.Usun())
                {
                    _logger.LogInformation("Deleted product {Id}", id);
                    return Ok(ApiResponse<bool>.Ok(true, "Product deleted successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(usuwanyAsortyment);
                    return BadRequest(ApiResponse<bool>.Error("Failed to delete product", errors));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting product {Id}", id);
            return StatusCode(500, ApiResponse<bool>.Error("Error deleting product", new List<string> { ex.Message }));
        }
    }

    private dynamic? GetUnit(string symbol)
    {
        if (string.IsNullOrEmpty(symbol)) return null;

        var jednostkiManager = _sferaService.GetManager("InsERT.Moria.ModelDanych", "InsERT.Moria.ModelDanych.Jednostki");
        if (jednostkiManager == null) return null;

        foreach (var j in jednostkiManager.Dane.Wszystkie())
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
        return new ProductDto
        {
            Id = DynamicPropertyHelper.GetId(asortyment),
            Symbol = DynamicPropertyHelper.GetString(asortyment, "Symbol"),
            Name = DynamicPropertyHelper.GetString(asortyment, "Nazwa"),
            Description = DynamicPropertyHelper.GetString(asortyment, "Opis"),
            EAN = DynamicPropertyHelper.GetString(asortyment, "EAN"),
            PKWiU = DynamicPropertyHelper.GetString(asortyment, "PKWIU"),
            SaleUnit = DynamicPropertyHelper.GetString(asortyment, "JednostkaSprzedazy", "Symbol"),
            PurchaseUnit = DynamicPropertyHelper.GetString(asortyment, "JednostkaZakupu", "Symbol"),
            PriceNet = DynamicPropertyHelper.GetNullableDecimal(asortyment, "CenaNetto"),
            Weight = DynamicPropertyHelper.GetNullableDecimal(asortyment, "Masa"),
            Volume = DynamicPropertyHelper.GetNullableDecimal(asortyment, "Objetosc"),
            IsActive = DynamicPropertyHelper.GetBool(asortyment, "Aktywny")
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
