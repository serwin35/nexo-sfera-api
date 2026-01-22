using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;

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
            var sfera = _sferaService.GetSfera();
            var magazyny = sfera.Magazyny();

            var items = magazyny.Dane.Wszystkie()
                .Select(m => new WarehouseDto
                {
                    Id = m.Id,
                    Symbol = m.Symbol,
                    Name = m.Nazwa,
                    Description = m.Opis,
                    IsActive = m.Aktywny
                })
                .ToList();

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
            var sfera = _sferaService.GetSfera();
            var magazyny = sfera.Magazyny();

            var magazyn = magazyny.Dane.Pierwszy(m => m.Symbol == symbol);
            if (magazyn == null)
            {
                return NotFound(ApiResponse<WarehouseDto>.Error($"Warehouse with symbol {symbol} not found"));
            }

            var dto = new WarehouseDto
            {
                Id = magazyn.Id,
                Symbol = magazyn.Symbol,
                Name = magazyn.Nazwa,
                Description = magazyn.Opis,
                IsActive = magazyn.Aktywny
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
            var sfera = _sferaService.GetSfera();
            var asortymenty = sfera.Asortymenty();

            var asortyment = asortymenty.Znajdz(productSymbol);
            if (asortyment == null)
            {
                return NotFound(ApiResponse<List<StockDto>>.Error($"Product with symbol {productSymbol} not found"));
            }

            var stock = new List<StockDto>();

            // Get stock from all warehouses
            var stanyMagazynowe = asortyment.Dane.StanyMagazynowe;
            if (stanyMagazynowe != null)
            {
                foreach (var stan in stanyMagazynowe)
                {
                    stock.Add(new StockDto
                    {
                        ProductId = asortyment.Dane.Id,
                        ProductSymbol = productSymbol,
                        WarehouseId = stan.Magazyn?.Id ?? 0,
                        WarehouseSymbol = stan.Magazyn?.Symbol ?? "",
                        Quantity = stan.Ilosc,
                        Reserved = stan.IloscZarezerwowana,
                        Available = stan.IloscDostepna
                    });
                }
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

public class WarehouseDto
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class StockDto
{
    public int ProductId { get; set; }
    public string ProductSymbol { get; set; } = string.Empty;
    public int WarehouseId { get; set; }
    public string WarehouseSymbol { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Reserved { get; set; }
    public decimal Available { get; set; }
}
