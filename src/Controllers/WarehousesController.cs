using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Warehouses (Magazyny) management endpoints
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Tags("Warehouses")]
public class WarehousesController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<WarehousesController> _logger;

    public WarehousesController(ISferaService sferaService, ILogger<WarehousesController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get all warehouses
    /// </summary>
    [HttpGet]
    public ActionResult<ApiResponse<List<WarehouseDto>>> GetWarehouses()
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var magazyny = sfera.Magazyny();
            var allMagazyny = ((IEnumerable<dynamic>)magazyny.Dane.Wszystkie()).ToList();

            var items = allMagazyny.Select(m => new WarehouseDto
            {
                Id = DynamicPropertyHelper.GetId(m),
                Symbol = DynamicPropertyHelper.GetString(m, "Symbol"),
                Name = DynamicPropertyHelper.GetString(m, "Nazwa"),
                Description = DynamicPropertyHelper.GetString(m, "Opis"),
                IsActive = DynamicPropertyHelper.GetNullableBool(m, "Aktywny") ?? true
            }).ToList();

            return Ok(ApiResponse<List<WarehouseDto>>.Ok(items));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting warehouses");
            return StatusCode(500, ApiResponse<List<WarehouseDto>>.Error("Error retrieving warehouses", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get warehouse by symbol
    /// </summary>
    [HttpGet("{symbol}")]
    public ActionResult<ApiResponse<WarehouseDto>> GetWarehouse(string symbol)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var magazyny = sfera.Magazyny();
            var allMagazyny = ((IEnumerable<dynamic>)magazyny.Dane.Wszystkie()).ToList();
            var magazyn = allMagazyny.FirstOrDefault(m => DynamicPropertyHelper.GetString(m, "Symbol") == symbol);

            if (magazyn == null)
            {
                return NotFound(ApiResponse<WarehouseDto>.Error($"Warehouse with symbol {symbol} not found"));
            }

            var dto = new WarehouseDto
            {
                Id = DynamicPropertyHelper.GetId(magazyn),
                Symbol = DynamicPropertyHelper.GetString(magazyn, "Symbol"),
                Name = DynamicPropertyHelper.GetString(magazyn, "Nazwa"),
                Description = DynamicPropertyHelper.GetString(magazyn, "Opis"),
                IsActive = DynamicPropertyHelper.GetNullableBool(magazyn, "Aktywny") ?? true
            };

            return Ok(ApiResponse<WarehouseDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting warehouse {Symbol}", symbol);
            return StatusCode(500, ApiResponse<WarehouseDto>.Error("Error retrieving warehouse", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get stock for a product in all warehouses
    /// </summary>
    [HttpGet("stock/{productSymbol}")]
    public ActionResult<ApiResponse<List<StockDto>>> GetStock(string productSymbol)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var asortymenty = sfera.Asortymenty();

            var asortyment = asortymenty.Znajdz(productSymbol);
            if (asortyment == null)
            {
                return NotFound(ApiResponse<List<StockDto>>.Error($"Product with symbol {productSymbol} not found"));
            }

            var stock = new List<StockDto>();

            // Get stock from all warehouses
            dynamic dane = asortyment.Dane;
            var stanyMagazynowe = DynamicPropertyHelper.GetCollection(dane, "StanyMagazynowe");

            foreach (var stan in stanyMagazynowe)
            {
                var magazyn = DynamicPropertyHelper.GetProperty(stan, "Magazyn");
                stock.Add(new StockDto
                {
                    ProductId = DynamicPropertyHelper.GetId(dane),
                    ProductSymbol = productSymbol,
                    WarehouseId = magazyn != null ? DynamicPropertyHelper.GetId(magazyn) : 0,
                    WarehouseSymbol = magazyn != null ? DynamicPropertyHelper.GetString(magazyn, "Symbol") : "",
                    Quantity = DynamicPropertyHelper.GetDecimal(stan, "IloscDostepna") +
                               DynamicPropertyHelper.GetDecimal(stan, "IloscZarezerwowanaIlosciowo") +
                               DynamicPropertyHelper.GetDecimal(stan, "IloscZadysponowana"),
                    Reserved = DynamicPropertyHelper.GetDecimal(stan, "IloscZarezerwowanaIlosciowo"),
                    Available = DynamicPropertyHelper.GetDecimal(stan, "IloscDostepna")
                });
            }

            return Ok(ApiResponse<List<StockDto>>.Ok(stock));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stock for product {ProductSymbol}", productSymbol);
            return StatusCode(500, ApiResponse<List<StockDto>>.Error("Error retrieving stock", new List<string> { ex.Message }));
        }
    }
}
