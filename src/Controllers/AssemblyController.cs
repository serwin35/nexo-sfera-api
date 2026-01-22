using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Requests;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using InsERT.Moria.Asortymenty;
using InsERT.Moria.ModelOrganizacyjny;
using InsERT.Moria.EgzekutorMagazynowy;

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
            var sfera = _sferaService.GetSfera();

            // Get the finished product
            var produkt = GetAsortyment(sfera, request.ProductId, request.ProductSymbol, null);
            if (produkt == null)
            {
                return NotFound(ApiResponse<AssemblyDto>.Error("Product not found"));
            }

            // Get warehouse
            var magazyn = sfera.Magazyny().Dane.Wszystkie()
                .FirstOrDefault(m => m.Symbol == request.WarehouseSymbol);
            if (magazyn == null)
            {
                return NotFound(ApiResponse<AssemblyDto>.Error($"Warehouse '{request.WarehouseSymbol}' not found"));
            }

            // Get magazynier for warehouse operations
            var magazynier = sfera.PodajObiektTypu<IMagazynier>();

            // Create assembly (montaż)
            var montaz = magazynier.UtworzMontaz(produkt, request.Quantity);

            // Add components (składniki)
            var componentDtos = new List<AssemblyComponentDto>();
            foreach (var component in request.Components)
            {
                var skladnik = GetAsortyment(sfera, component.ProductId, component.ProductSymbol, null);
                if (skladnik == null)
                {
                    return NotFound(ApiResponse<AssemblyDto>.Error($"Component product not found: {component.ProductSymbol ?? component.ProductId?.ToString()}"));
                }

                // Create issue (wydanie) for the component
                var wydanie = magazynier.UtworzWydanie(skladnik, component.Quantity);
                montaz.DodajSkladnik(wydanie);

                componentDtos.Add(new AssemblyComponentDto
                {
                    ProductId = skladnik.Id,
                    ProductSymbol = skladnik.Symbol,
                    ProductName = skladnik.Nazwa,
                    Quantity = component.Quantity,
                    Unit = skladnik.JednostkaMagazynowa?.Symbol ?? "szt."
                });
            }

            // Save the assembly
            if (magazynier.Zapisz())
            {
                var dto = new AssemblyDto
                {
                    Id = montaz.Przyjecie?.Id ?? 0,
                    Type = AssemblyType.Assembly,
                    ProductId = produkt.Id,
                    ProductSymbol = produkt.Symbol,
                    ProductName = produkt.Nazwa,
                    Quantity = request.Quantity,
                    Unit = produkt.JednostkaMagazynowa?.Symbol ?? "szt.",
                    WarehouseSymbol = magazyn.Symbol,
                    WarehouseName = magazyn.Nazwa,
                    Date = DateTime.Now,
                    Status = "Completed",
                    Components = componentDtos,
                    Notes = request.Notes
                };

                _logger.LogInformation("Created assembly for product {ProductSymbol} with {ComponentCount} components",
                    produkt.Symbol, request.Components.Count);

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
            var sfera = _sferaService.GetSfera();

            // Get the product to disassemble
            var produkt = GetAsortyment(sfera, request.ProductId, request.ProductSymbol, null);
            if (produkt == null)
            {
                return NotFound(ApiResponse<AssemblyDto>.Error("Product not found"));
            }

            // Get warehouse
            var magazyn = sfera.Magazyny().Dane.Wszystkie()
                .FirstOrDefault(m => m.Symbol == request.WarehouseSymbol);
            if (magazyn == null)
            {
                return NotFound(ApiResponse<AssemblyDto>.Error($"Warehouse '{request.WarehouseSymbol}' not found"));
            }

            // Get magazynier for warehouse operations
            var magazynier = sfera.PodajObiektTypu<IMagazynier>();

            // Create disassembly (demontaż)
            var demontaz = magazynier.UtworzDemontaz(produkt, magazyn);

            // If resulting components are specified, add them
            var componentDtos = new List<AssemblyComponentDto>();
            if (request.ResultingComponents != null && request.ResultingComponents.Any())
            {
                foreach (var component in request.ResultingComponents)
                {
                    var skladnik = GetAsortyment(sfera, component.ProductId, component.ProductSymbol, null);
                    if (skladnik != null)
                    {
                        // Create receipt (przyjęcie) for the resulting component
                        var przyjecie = magazynier.UtworzPrzyjecie(skladnik, component.Quantity);

                        componentDtos.Add(new AssemblyComponentDto
                        {
                            ProductId = skladnik.Id,
                            ProductSymbol = skladnik.Symbol,
                            ProductName = skladnik.Nazwa,
                            Quantity = component.Quantity,
                            Unit = skladnik.JednostkaMagazynowa?.Symbol ?? "szt."
                        });
                    }
                }
            }

            // Save the disassembly
            if (magazynier.Zapisz())
            {
                var dto = new AssemblyDto
                {
                    Id = 0, // ID from demontaz if available
                    Type = AssemblyType.Disassembly,
                    ProductId = produkt.Id,
                    ProductSymbol = produkt.Symbol,
                    ProductName = produkt.Nazwa,
                    Quantity = request.Quantity,
                    Unit = produkt.JednostkaMagazynowa?.Symbol ?? "szt.",
                    WarehouseSymbol = magazyn.Symbol,
                    WarehouseName = magazyn.Nazwa,
                    Date = DateTime.Now,
                    Status = "Completed",
                    Components = componentDtos,
                    Notes = request.Notes
                };

                _logger.LogInformation("Created disassembly for product {ProductSymbol}", produkt.Symbol);

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
            var sfera = _sferaService.GetSfera();
            var magazynier = sfera.PodajObiektTypu<IMagazynier>();

            var assemblies = new List<AssemblyListItemDto>();

            // Get przyjecia marked as from assembly (montaż)
            var przyjecia = magazynier.Dane.Przyjecia();

            // Filter przyjecia that are from assembly operations
            var assemblyPrzyjecia = przyjecia
                .Where(p => p.ZMontazu == true)
                .ToList();

            if (!string.IsNullOrEmpty(warehouseSymbol))
            {
                assemblyPrzyjecia = assemblyPrzyjecia
                    .Where(p => p.Magazyn?.Symbol == warehouseSymbol)
                    .ToList();
            }

            if (productId.HasValue)
            {
                assemblyPrzyjecia = assemblyPrzyjecia
                    .Where(p => p.Asortyment?.Id == productId.Value)
                    .ToList();
            }

            if (dateFrom.HasValue)
            {
                assemblyPrzyjecia = assemblyPrzyjecia
                    .Where(p => p.Data >= dateFrom.Value)
                    .ToList();
            }

            if (dateTo.HasValue)
            {
                assemblyPrzyjecia = assemblyPrzyjecia
                    .Where(p => p.Data <= dateTo.Value)
                    .ToList();
            }

            // Only include assembly type if filter is specified
            if (!type.HasValue || type.Value == AssemblyType.Assembly)
            {
                foreach (var przyjecie in assemblyPrzyjecia)
                {
                    assemblies.Add(new AssemblyListItemDto
                    {
                        Id = przyjecie.Id,
                        Type = AssemblyType.Assembly,
                        ProductId = przyjecie.Asortyment?.Id ?? 0,
                        ProductSymbol = przyjecie.Asortyment?.Symbol,
                        ProductName = przyjecie.Asortyment?.Nazwa,
                        Quantity = przyjecie.Ilosc,
                        Unit = przyjecie.Asortyment?.JednostkaMagazynowa?.Symbol ?? "szt.",
                        WarehouseSymbol = przyjecie.Magazyn?.Symbol,
                        Date = przyjecie.Data ?? DateTime.MinValue,
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
            var sfera = _sferaService.GetSfera();
            var magazynier = sfera.PodajObiektTypu<IMagazynier>();

            // Find the przyjecie (receipt) from assembly
            var przyjecie = magazynier.Dane.Przyjecia()
                .FirstOrDefault(p => p.Id == id && p.ZMontazu == true);

            if (przyjecie == null)
            {
                return NotFound(ApiResponse<AssemblyDto>.Error($"Assembly with ID {id} not found"));
            }

            var dto = new AssemblyDto
            {
                Id = przyjecie.Id,
                Type = AssemblyType.Assembly,
                ProductId = przyjecie.Asortyment?.Id ?? 0,
                ProductSymbol = przyjecie.Asortyment?.Symbol,
                ProductName = przyjecie.Asortyment?.Nazwa,
                Quantity = przyjecie.Ilosc,
                Unit = przyjecie.Asortyment?.JednostkaMagazynowa?.Symbol ?? "szt.",
                WarehouseSymbol = przyjecie.Magazyn?.Symbol,
                WarehouseName = przyjecie.Magazyn?.Nazwa,
                Date = przyjecie.Data ?? DateTime.MinValue,
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

    private Asortyment? GetAsortyment(Uchwyt sfera, int? id, string? symbol, string? ean)
    {
        var asortymenty = sfera.Asortymenty();

        if (id.HasValue)
        {
            return asortymenty.Dane.Wszystkie().FirstOrDefault(a => a.Id == id.Value);
        }

        if (!string.IsNullOrEmpty(symbol))
        {
            return asortymenty.Dane.Wszystkie().FirstOrDefault(a => a.Symbol == symbol);
        }

        if (!string.IsNullOrEmpty(ean))
        {
            return asortymenty.Dane.Wszystkie().FirstOrDefault(a => a.KodEan == ean);
        }

        return null;
    }

    #endregion
}
