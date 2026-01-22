using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Requests;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Assembly (ZM - Zlecenie Montażu) operations controller
/// Handles montaż (assembly) and demontaż (disassembly) operations
/// </summary>
[ApiController]
[Route("api/assembly")]
[Authorize]
[Tags("Assembly (ZM)")]
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
    /// Create assembly operation (montaż - ZM)
    /// Combines components into a finished product
    /// </summary>
    /// <param name="request">Assembly request with product and components</param>
    /// <returns>Created assembly details</returns>
    [HttpPost("montaz")]
    [ProducesResponseType(typeof(ApiResponse<AssemblyDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<AssemblyDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<AssemblyDto>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<AssemblyDto>> CreateAssembly([FromBody] CreateAssemblyRequest request)
    {
        try
        {
            // Get the finished product
            var produkt = FindAsortyment(request.ProductId, request.ProductSymbol, null);
            if (produkt == null)
            {
                return NotFound(ApiResponse<AssemblyDto>.Error("Product not found"));
            }

            // Get warehouse
            var magazynyManager = _sferaService.GetManager("InsERT.Moria.Logistyka", "InsERT.Moria.Logistyka.Magazyny");
            if (magazynyManager == null)
            {
                return StatusCode(500, ApiResponse<AssemblyDto>.Error("Failed to get Magazyny manager"));
            }

            dynamic? magazyn = null;
            foreach (var m in magazynyManager.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetString(m, "Symbol") == request.WarehouseSymbol)
                {
                    magazyn = m;
                    break;
                }
            }
            if (magazyn == null)
            {
                return NotFound(ApiResponse<AssemblyDto>.Error($"Warehouse '{request.WarehouseSymbol}' not found"));
            }

            // Get magazynier for warehouse operations (requires Sfera for service access)
            dynamic sfera = _sferaService.GetSfera();
            var magazynier = sfera.PodajObiektTypu("InsERT.Moria.EgzekutorMagazynowy.IMagazynier");

            // Create assembly (montaż)
            var montaz = magazynier.UtworzMontaz(produkt, request.Quantity);

            // Add components (składniki)
            var componentDtos = new List<AssemblyComponentDto>();
            foreach (var component in request.Components)
            {
                var skladnik = FindAsortyment(component.ProductId, component.ProductSymbol, null);
                if (skladnik == null)
                {
                    return NotFound(ApiResponse<AssemblyDto>.Error($"Component product not found: {component.ProductSymbol ?? component.ProductId?.ToString()}"));
                }

                // Create issue (wydanie) for the component
                var wydanie = magazynier.UtworzWydanie(skladnik, component.Quantity);
                montaz.DodajSkladnik(wydanie);

                componentDtos.Add(new AssemblyComponentDto
                {
                    ProductId = DynamicPropertyHelper.GetId(skladnik),
                    ProductSymbol = DynamicPropertyHelper.GetString(skladnik, "Symbol"),
                    ProductName = DynamicPropertyHelper.GetString(skladnik, "Nazwa"),
                    Quantity = component.Quantity,
                    Unit = DynamicPropertyHelper.GetString(skladnik, "JednostkaMagazynowa", "Symbol") ?? "szt."
                });
            }

            // Save the assembly
            if ((bool)magazynier.Zapisz())
            {
                var przyjecie = DynamicPropertyHelper.GetProperty(montaz, "Przyjecie");
                var dto = new AssemblyDto
                {
                    Id = przyjecie != null ? DynamicPropertyHelper.GetId(przyjecie) : 0,
                    Type = AssemblyType.Assembly,
                    ProductId = DynamicPropertyHelper.GetId(produkt),
                    ProductSymbol = DynamicPropertyHelper.GetString(produkt, "Symbol"),
                    ProductName = DynamicPropertyHelper.GetString(produkt, "Nazwa"),
                    Quantity = request.Quantity,
                    Unit = DynamicPropertyHelper.GetString(produkt, "JednostkaMagazynowa", "Symbol") ?? "szt.",
                    WarehouseSymbol = DynamicPropertyHelper.GetString(magazyn, "Symbol"),
                    WarehouseName = DynamicPropertyHelper.GetString(magazyn, "Nazwa"),
                    Date = DateTime.Now,
                    Status = "Completed",
                    Components = componentDtos,
                    Notes = request.Notes
                };

                string productSymbol = DynamicPropertyHelper.GetString(produkt, "Symbol") ?? "";
                int componentCount = request.Components.Count;
                _logger.LogInformation("Created assembly for product {ProductSymbol} with {ComponentCount} components",
                    productSymbol, componentCount);

                return CreatedAtAction(nameof(GetAssembly), new { id = dto.Id }, ApiResponse<AssemblyDto>.Ok(dto));
            }
            else
            {
                return BadRequest(ApiResponse<AssemblyDto>.Error("Failed to save assembly operation"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating assembly");
            return StatusCode(500, ApiResponse<AssemblyDto>.Error("Error creating assembly", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create disassembly operation (demontaż)
    /// Breaks down a product into its components
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
            // Get the product to disassemble
            var produkt = FindAsortyment(request.ProductId, request.ProductSymbol, null);
            if (produkt == null)
            {
                return NotFound(ApiResponse<AssemblyDto>.Error("Product not found"));
            }

            // Get warehouse
            var magazynyManager = _sferaService.GetManager("InsERT.Moria.Logistyka", "InsERT.Moria.Logistyka.Magazyny");
            if (magazynyManager == null)
            {
                return StatusCode(500, ApiResponse<AssemblyDto>.Error("Failed to get Magazyny manager"));
            }

            dynamic? magazyn = null;
            foreach (var m in magazynyManager.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetString(m, "Symbol") == request.WarehouseSymbol)
                {
                    magazyn = m;
                    break;
                }
            }
            if (magazyn == null)
            {
                return NotFound(ApiResponse<AssemblyDto>.Error($"Warehouse '{request.WarehouseSymbol}' not found"));
            }

            // Get magazynier for warehouse operations (requires Sfera for service access)
            dynamic sfera = _sferaService.GetSfera();
            var magazynier = sfera.PodajObiektTypu("InsERT.Moria.EgzekutorMagazynowy.IMagazynier");

            // Create disassembly (demontaż)
            var demontaz = magazynier.UtworzDemontaz(produkt, magazyn);

            // If resulting components are specified, add them
            var componentDtos = new List<AssemblyComponentDto>();
            if (request.ResultingComponents != null && request.ResultingComponents.Count > 0)
            {
                foreach (var component in request.ResultingComponents)
                {
                    var skladnik = FindAsortyment(component.ProductId, component.ProductSymbol, null);
                    if (skladnik != null)
                    {
                        // Create receipt (przyjęcie) for the resulting component
                        var przyjecie = magazynier.UtworzPrzyjecie(skladnik, component.Quantity);

                        componentDtos.Add(new AssemblyComponentDto
                        {
                            ProductId = DynamicPropertyHelper.GetId(skladnik),
                            ProductSymbol = DynamicPropertyHelper.GetString(skladnik, "Symbol"),
                            ProductName = DynamicPropertyHelper.GetString(skladnik, "Nazwa"),
                            Quantity = component.Quantity,
                            Unit = DynamicPropertyHelper.GetString(skladnik, "JednostkaMagazynowa", "Symbol") ?? "szt."
                        });
                    }
                }
            }

            // Save the disassembly
            if ((bool)magazynier.Zapisz())
            {
                var dto = new AssemblyDto
                {
                    Id = 0, // ID from demontaz if available
                    Type = AssemblyType.Disassembly,
                    ProductId = DynamicPropertyHelper.GetId(produkt),
                    ProductSymbol = DynamicPropertyHelper.GetString(produkt, "Symbol"),
                    ProductName = DynamicPropertyHelper.GetString(produkt, "Nazwa"),
                    Quantity = request.Quantity,
                    Unit = DynamicPropertyHelper.GetString(produkt, "JednostkaMagazynowa", "Symbol") ?? "szt.",
                    WarehouseSymbol = DynamicPropertyHelper.GetString(magazyn, "Symbol"),
                    WarehouseName = DynamicPropertyHelper.GetString(magazyn, "Nazwa"),
                    Date = DateTime.Now,
                    Status = "Completed",
                    Components = componentDtos,
                    Notes = request.Notes
                };

                string productSymbol = DynamicPropertyHelper.GetString(produkt, "Symbol") ?? "";
                _logger.LogInformation("Created disassembly for product {ProductSymbol}", productSymbol);

                return CreatedAtAction(nameof(GetAssembly), new { id = dto.Id }, ApiResponse<AssemblyDto>.Ok(dto));
            }
            else
            {
                return BadRequest(ApiResponse<AssemblyDto>.Error("Failed to save disassembly operation"));
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
    /// Get assembly operations list
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
            // Requires Sfera for service access
            dynamic sfera = _sferaService.GetSfera();
            var magazynier = sfera.PodajObiektTypu("InsERT.Moria.EgzekutorMagazynowy.IMagazynier");

            var assemblies = new List<AssemblyListItemDto>();

            // Get przyjecia marked as from assembly (montaż) with filtering
            var assemblyPrzyjecia = new List<dynamic>();
            foreach (var p in magazynier.Dane.Przyjecia())
            {
                // Must be from assembly
                if (!DynamicPropertyHelper.GetBool(p, "ZMontazu"))
                    continue;

                // Filter by warehouse symbol
                if (!string.IsNullOrEmpty(warehouseSymbol))
                {
                    var mag = DynamicPropertyHelper.GetProperty(p, "Magazyn");
                    if (mag == null || DynamicPropertyHelper.GetString(mag, "Symbol") != warehouseSymbol)
                        continue;
                }

                // Filter by product ID
                if (productId.HasValue)
                {
                    var asortyment = DynamicPropertyHelper.GetProperty(p, "Asortyment");
                    if (asortyment == null || DynamicPropertyHelper.GetId(asortyment) != productId.Value)
                        continue;
                }

                // Filter by date range
                if (dateFrom.HasValue)
                {
                    var data = DynamicPropertyHelper.GetDateTime(p, "Data");
                    if (!data.HasValue || data.Value < dateFrom.Value)
                        continue;
                }

                if (dateTo.HasValue)
                {
                    var data = DynamicPropertyHelper.GetDateTime(p, "Data");
                    if (!data.HasValue || data.Value > dateTo.Value)
                        continue;
                }

                assemblyPrzyjecia.Add(p);
            }

            // Only include assembly type if filter is specified
            if (!type.HasValue || type.Value == AssemblyType.Assembly)
            {
                foreach (var przyjecie in assemblyPrzyjecia)
                {
                    var asortyment = DynamicPropertyHelper.GetProperty(przyjecie, "Asortyment");
                    var magazyn = DynamicPropertyHelper.GetProperty(przyjecie, "Magazyn");

                    assemblies.Add(new AssemblyListItemDto
                    {
                        Id = DynamicPropertyHelper.GetId(przyjecie),
                        Type = AssemblyType.Assembly,
                        ProductId = asortyment != null ? DynamicPropertyHelper.GetId(asortyment) : 0,
                        ProductSymbol = asortyment != null ? DynamicPropertyHelper.GetString(asortyment, "Symbol") : null,
                        ProductName = asortyment != null ? DynamicPropertyHelper.GetString(asortyment, "Nazwa") : null,
                        Quantity = DynamicPropertyHelper.GetDecimal(przyjecie, "Ilosc"),
                        Unit = asortyment != null ? DynamicPropertyHelper.GetString(asortyment, "JednostkaMagazynowa", "Symbol") ?? "szt." : "szt.",
                        WarehouseSymbol = magazyn != null ? DynamicPropertyHelper.GetString(magazyn, "Symbol") : null,
                        Date = DynamicPropertyHelper.GetDateTime(przyjecie, "Data") ?? DateTime.MinValue,
                        Status = "Completed",
                        ComponentCount = 0 // Would need to trace related wydania
                    });
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
    /// Get assembly by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<AssemblyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AssemblyDto>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<AssemblyDto>> GetAssembly(int id)
    {
        try
        {
            // Requires Sfera for service access
            dynamic sfera = _sferaService.GetSfera();
            var magazynier = sfera.PodajObiektTypu("InsERT.Moria.EgzekutorMagazynowy.IMagazynier");

            // Find the przyjecie (receipt) from assembly
            dynamic? przyjecie = null;
            foreach (var p in magazynier.Dane.Przyjecia())
            {
                if (DynamicPropertyHelper.GetId(p) == id && DynamicPropertyHelper.GetBool(p, "ZMontazu"))
                {
                    przyjecie = p;
                    break;
                }
            }

            if (przyjecie == null)
            {
                return NotFound(ApiResponse<AssemblyDto>.Error($"Assembly with ID {id} not found"));
            }

            var asortyment = DynamicPropertyHelper.GetProperty(przyjecie, "Asortyment");
            var magazyn = DynamicPropertyHelper.GetProperty(przyjecie, "Magazyn");

            var dto = new AssemblyDto
            {
                Id = DynamicPropertyHelper.GetId(przyjecie),
                Type = AssemblyType.Assembly,
                ProductId = asortyment != null ? DynamicPropertyHelper.GetId(asortyment) : 0,
                ProductSymbol = asortyment != null ? DynamicPropertyHelper.GetString(asortyment, "Symbol") : null,
                ProductName = asortyment != null ? DynamicPropertyHelper.GetString(asortyment, "Nazwa") : null,
                Quantity = DynamicPropertyHelper.GetDecimal(przyjecie, "Ilosc"),
                Unit = asortyment != null ? DynamicPropertyHelper.GetString(asortyment, "JednostkaMagazynowa", "Symbol") ?? "szt." : "szt.",
                WarehouseSymbol = magazyn != null ? DynamicPropertyHelper.GetString(magazyn, "Symbol") : null,
                WarehouseName = magazyn != null ? DynamicPropertyHelper.GetString(magazyn, "Nazwa") : null,
                Date = DynamicPropertyHelper.GetDateTime(przyjecie, "Data") ?? DateTime.MinValue,
                Status = "Completed",
                Components = new List<AssemblyComponentDto>()
            };

            return Ok(ApiResponse<AssemblyDto>.Ok(dto));
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
        var asortymentyManager = _sferaService.GetManager("InsERT.Moria.Asortymenty", "InsERT.Moria.Asortymenty.Asortymenty");
        if (asortymentyManager == null) return null;

        foreach (var a in asortymentyManager.Dane.Wszystkie())
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

    #endregion
}
