using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Requests;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using InsERT.Moria.Asortymenty;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;

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
            var sfera = _sferaService.GetSfera();
            var asortymenty = sfera.Asortymenty();

            var query = activeOnly == true
                ? asortymenty.Dane.WszystkieDostepne()
                : asortymenty.Dane.Wszystkie();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(a =>
                    a.Symbol.Contains(search) ||
                    a.Nazwa.Contains(search) ||
                    (a.EAN != null && a.EAN.Contains(search)));
            }

            var totalCount = query.Count();
            var items = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var response = new PagedResponse<ProductDto>
            {
                Data = items.Select(MapToDto).ToList(),
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
            var sfera = _sferaService.GetSfera();
            var asortymenty = sfera.Asortymenty();

            var asortyment = asortymenty.Dane.Wszystkie().FirstOrDefault(a => a.Id == id);
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
            var sfera = _sferaService.GetSfera();
            var asortymenty = sfera.Asortymenty();

            var asortyment = asortymenty.Znajdz(symbol);
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
            var sfera = _sferaService.GetSfera();
            var asortymenty = sfera.Asortymenty();

            var asortyment = asortymenty.Dane.Wszystkie().FirstOrDefault(a => a.EAN == ean);
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
            var sfera = _sferaService.GetSfera();
            var asortymenty = sfera.Asortymenty();

            // Check if symbol already exists
            var existing = asortymenty.Znajdz(request.Symbol);
            if (existing != null)
            {
                return BadRequest(ApiResponse<ProductDto>.Error($"Product with symbol {request.Symbol} already exists"));
            }

            using (var nowyAsortyment = asortymenty.Utworz())
            {
                // Apply template if specified
                if (!string.IsNullOrEmpty(request.TemplateSymbol))
                {
                    var szablony = sfera.SzablonyAsortymentu();
                    var szablon = szablony.Dane.Wszystkie().FirstOrDefault(s => s.Symbol == request.TemplateSymbol);
                    if (szablon != null)
                    {
                        nowyAsortyment.WypelnijNaPodstawieSzablonu(szablon);
                    }
                }

                nowyAsortyment.Dane.Symbol = request.Symbol;
                nowyAsortyment.Dane.Nazwa = request.Name;

                if (!string.IsNullOrEmpty(request.Description))
                {
                    nowyAsortyment.Dane.Opis = request.Description;
                }

                if (!string.IsNullOrEmpty(request.EAN))
                {
                    nowyAsortyment.Dane.EAN = request.EAN;
                }

                if (!string.IsNullOrEmpty(request.PKWiU))
                {
                    nowyAsortyment.Dane.PKWIU = request.PKWiU;
                }

                // Set unit
                nowyAsortyment.Dane.JednostkaSprzedazy = GetOrCreateUnit(sfera, request.SaleUnit);

                if (!string.IsNullOrEmpty(request.PurchaseUnit))
                {
                    nowyAsortyment.Dane.JednostkaZakupu = GetOrCreateUnit(sfera, request.PurchaseUnit);
                }

                // Set price
                if (request.PriceNet.HasValue)
                {
                    nowyAsortyment.Dane.CenaNetto = request.PriceNet.Value;
                }

                // Set physical properties
                if (request.Weight.HasValue)
                {
                    nowyAsortyment.Dane.Masa = request.Weight.Value;
                }

                if (request.Volume.HasValue)
                {
                    nowyAsortyment.Dane.Objetosc = request.Volume.Value;
                }

                if (nowyAsortyment.Zapisz())
                {
                    _logger.LogInformation("Created product {Symbol}", request.Symbol);
                    return CreatedAtAction(
                        nameof(GetProduct),
                        new { id = nowyAsortyment.Dane.Id },
                        ApiResponse<ProductDto>.Ok(MapToDto(nowyAsortyment.Dane), "Product created successfully"));
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
            var sfera = _sferaService.GetSfera();
            var asortymenty = sfera.Asortymenty();

            var asortyment = asortymenty.Dane.Wszystkie().FirstOrDefault(a => a.Id == id);
            if (asortyment == null)
            {
                return NotFound(ApiResponse<ProductDto>.Error($"Product with ID {id} not found"));
            }

            using (var edytowanyAsortyment = asortymenty.Znajdz(asortyment))
            {
                if (edytowanyAsortyment == null)
                {
                    return NotFound(ApiResponse<ProductDto>.Error($"Product with ID {id} not found"));
                }

                if (!string.IsNullOrEmpty(request.Name))
                {
                    edytowanyAsortyment.Dane.Nazwa = request.Name;
                }

                if (!string.IsNullOrEmpty(request.Description))
                {
                    edytowanyAsortyment.Dane.Opis = request.Description;
                }

                if (!string.IsNullOrEmpty(request.EAN))
                {
                    edytowanyAsortyment.Dane.EAN = request.EAN;
                }

                if (!string.IsNullOrEmpty(request.PKWiU))
                {
                    edytowanyAsortyment.Dane.PKWIU = request.PKWiU;
                }

                if (request.PriceNet.HasValue)
                {
                    edytowanyAsortyment.Dane.CenaNetto = request.PriceNet.Value;
                }

                if (request.Weight.HasValue)
                {
                    edytowanyAsortyment.Dane.Masa = request.Weight.Value;
                }

                if (request.Volume.HasValue)
                {
                    edytowanyAsortyment.Dane.Objetosc = request.Volume.Value;
                }

                if (request.IsActive.HasValue)
                {
                    edytowanyAsortyment.Dane.Aktywny = request.IsActive.Value;
                }

                if (edytowanyAsortyment.Zapisz())
                {
                    _logger.LogInformation("Updated product {Id}", id);
                    return Ok(ApiResponse<ProductDto>.Ok(MapToDto(edytowanyAsortyment.Dane), "Product updated successfully"));
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
            var sfera = _sferaService.GetSfera();
            var asortymenty = sfera.Asortymenty();

            var asortyment = asortymenty.Dane.Wszystkie().FirstOrDefault(a => a.Id == id);
            if (asortyment == null)
            {
                return NotFound(ApiResponse<bool>.Error($"Product with ID {id} not found"));
            }

            using (var usuwanyAsortyment = asortymenty.Znajdz(asortyment))
            {
                if (usuwanyAsortyment == null)
                {
                    return NotFound(ApiResponse<bool>.Error($"Product with ID {id} not found"));
                }

                if (usuwanyAsortyment.Usun())
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

    private static dynamic? GetOrCreateUnit(dynamic sfera, string symbol)
    {
        var jednostki = sfera.Jednostki();
        return jednostki.Dane.Wszystkie().FirstOrDefault(j => j.Symbol == symbol);
    }

    private static ProductDto MapToDto(Asortyment asortyment)
    {
        return new ProductDto
        {
            Id = asortyment.Id,
            Symbol = asortyment.Symbol,
            Name = asortyment.Nazwa,
            Description = asortyment.Opis,
            EAN = asortyment.EAN,
            PKWiU = asortyment.PKWIU,
            SaleUnit = asortyment.JednostkaSprzedazy?.Symbol,
            PurchaseUnit = asortyment.JednostkaZakupu?.Symbol,
            PriceNet = asortyment.CenaNetto,
            Weight = asortyment.Masa,
            Volume = asortyment.Objetosc,
            IsActive = asortyment.Aktywny
        };
    }

    private static List<string> GetBusinessObjectErrors(InsERT.Mox.ObiektyBiznesowe.IObiektBiznesowy obiekt)
    {
        var errors = new List<string>();
        foreach (var encjaZBledami in obiekt.InvalidData)
        {
            foreach (var blad in encjaZBledami.Errors)
            {
                errors.Add(blad.ToString());
            }
            foreach (var bladNaPolach in encjaZBledami.MemberErrors)
            {
                errors.Add($"{bladNaPolach.Key}: {string.Join(", ", bladNaPolach)}");
            }
        }
        return errors;
    }
}
