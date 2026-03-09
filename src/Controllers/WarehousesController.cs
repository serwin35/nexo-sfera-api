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
    public async Task<IActionResult> GetWarehouses()
    {
        try
        {
            var items = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var magazyny = _sferaService.GetManager("Magazyny");
                if (magazyny == null) return (List<WarehouseDto>?)null;

                var result = new List<WarehouseDto>();
                foreach (var m in DynamicPropertyHelper.SafeGetAll((object)magazyny))
                {
                    result.Add(new WarehouseDto
                    {
                        Id = DynamicPropertyHelper.GetId(m),
                        Symbol = DynamicPropertyHelper.GetString(m, "Symbol"),
                        Name = DynamicPropertyHelper.GetString(m, "Nazwa"),
                        Description = DynamicPropertyHelper.GetString(m, "Opis"),
                        IsActive = DynamicPropertyHelper.GetNullableBool(m, "Aktywny") ?? true
                    });
                }
                return result;
            });

            if (items == null)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Magazyny manager"));

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
    public async Task<IActionResult> GetWarehouse(string symbol)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var magazyny = _sferaService.GetManager("Magazyny");
                if (magazyny == null) return (found: false, managerNull: true, dto: (WarehouseDto?)null);

                dynamic? magazyn = null;
                foreach (var m in DynamicPropertyHelper.SafeGetAll((object)magazyny))
                {
                    if (DynamicPropertyHelper.GetString(m, "Symbol") == symbol)
                    {
                        magazyn = m;
                        break;
                    }
                }

                if (magazyn == null) return (found: false, managerNull: false, dto: (WarehouseDto?)null);

                var dto = new WarehouseDto
                {
                    Id = DynamicPropertyHelper.GetId(magazyn),
                    Symbol = DynamicPropertyHelper.GetString(magazyn, "Symbol"),
                    Name = DynamicPropertyHelper.GetString(magazyn, "Nazwa"),
                    Description = DynamicPropertyHelper.GetString(magazyn, "Opis"),
                    IsActive = DynamicPropertyHelper.GetNullableBool(magazyn, "Aktywny") ?? true
                };

                return (found: true, managerNull: false, dto: (WarehouseDto?)dto);
            });

            if (result.managerNull)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Magazyny manager"));

            if (!result.found)
                return NotFound(ApiResponse<WarehouseDto>.Error($"Warehouse with symbol {symbol} not found"));

            return Ok(ApiResponse<WarehouseDto>.Ok(result.dto!));
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
    public async Task<IActionResult> GetStock(string productSymbol)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var asortymenty = _sferaService.GetManager("Asortymenty");
                if (asortymenty == null) return (managerNull: true, notFound: false, stock: (List<StockDto>?)null);

                var asortyment = asortymenty.Znajdz(productSymbol);
                if (asortyment == null) return (managerNull: false, notFound: true, stock: (List<StockDto>?)null);

                var stock = new List<StockDto>();

                dynamic dane = asortyment.Dane;
                var stanyMagazynowe = DynamicPropertyHelper.GetCollection((object)dane, "StanyMagazynowe");

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

                return (managerNull: false, notFound: false, stock: (List<StockDto>?)stock);
            });

            if (result.managerNull)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Asortymenty manager"));

            if (result.notFound)
                return NotFound(ApiResponse<List<StockDto>>.Error($"Product with symbol {productSymbol} not found"));

            return Ok(ApiResponse<List<StockDto>>.Ok(result.stock!));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stock for product {ProductSymbol}", productSymbol);
            return StatusCode(500, ApiResponse<List<StockDto>>.Error("Error retrieving stock", new List<string> { ex.Message }));
        }
    }
}
