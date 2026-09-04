using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Requests;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Inventory (Stock levels, Batches, Reservations) management endpoints
/// </summary>
[ApiController]
[Route("api/inventory")]
[Authorize]
[Tags("Inventory")]
public class InventoryController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(ISferaService sferaService, ILogger<InventoryController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    #region Stock Levels

    /// <summary>
    /// Get inventory stock levels
    /// </summary>
    [HttpGet("stock")]
    public async Task<ActionResult<PagedResponse<InventoryItemDto>>> GetStockLevels(
        [FromQuery] string? warehouseSymbol,
        [FromQuery] int? productId,
        [FromQuery] string? productSymbol,
        [FromQuery] bool? lowStock,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var asortymentyManager = _sferaService.GetManager("Asortymenty");
                var magazynyManager = _sferaService.GetManager("Magazyny");
                if (asortymentyManager == null)
                {
                    return (PagedResponse<InventoryItemDto>?)null;
                }

                // Get all products with their stock levels
                var produktyQuery = new List<object>();
                foreach (var a in DynamicPropertyHelper.SafeGetAll((object)asortymentyManager))
                {
                    if (!DynamicPropertyHelper.TracksStock(a))

                        continue;

                    if (productId.HasValue && DynamicPropertyHelper.GetId(a) != productId.Value)
                        continue;

                    if (!string.IsNullOrEmpty(productSymbol))
                    {
                        var s = DynamicPropertyHelper.GetString(a, "Symbol") ?? "";
                        if (s != productSymbol && !s.Contains(productSymbol))
                            continue;
                    }

                    produktyQuery.Add(a);
                }

                // Get warehouse filter
                dynamic? magazynFilter = null;
                int? magazynFilterId = null;
                if (!string.IsNullOrEmpty(warehouseSymbol) && magazynyManager != null)
                {
                    foreach (var m in DynamicPropertyHelper.SafeGetAll((object)magazynyManager))
                    {
                        if (DynamicPropertyHelper.GetString(m, "Symbol") == warehouseSymbol)
                        {
                            magazynFilter = m;
                            magazynFilterId = DynamicPropertyHelper.GetId(m);
                            break;
                        }
                    }
                }

                var inventoryItems = new List<InventoryItemDto>();

                foreach (var produkt in produktyQuery)
                {
                    // Get stock levels for this product
                    var stany = DynamicPropertyHelper.GetCollection((object)produkt, "StanyMagazynowe");

                    if (magazynFilterId.HasValue)
                    {
                        var filteredStany = new List<object>();
                        foreach (var s in stany)
                        {
                            if (DynamicPropertyHelper.GetNullableInt(s, "Magazyn_Id") == magazynFilterId.Value)
                            {
                                filteredStany.Add(s);
                            }
                        }
                        stany = filteredStany;
                    }

                    foreach (var stan in stany)
                    {
                        var magazynId = DynamicPropertyHelper.GetNullableInt(stan, "Magazyn_Id");
                        dynamic? magazyn = null;
                        if (magazynId.HasValue && magazynyManager != null)
                        {
                            foreach (var m in DynamicPropertyHelper.SafeGetAll((object)magazynyManager))
                            {
                                if (DynamicPropertyHelper.GetId(m) == magazynId.Value)
                                {
                                    magazyn = m;
                                    break;
                                }
                            }
                        }

                        var iloscDostepna = DynamicPropertyHelper.GetDecimal(stan, "IloscDostepna");
                        var iloscZarezerwowanaIlosciowo = DynamicPropertyHelper.GetDecimal(stan, "IloscZarezerwowanaIlosciowo");
                        var iloscZadysponowana = DynamicPropertyHelper.GetDecimal(stan, "IloscZadysponowana");

                        var item = new InventoryItemDto
                        {
                            ProductId = DynamicPropertyHelper.GetId(produkt),
                            ProductSymbol = DynamicPropertyHelper.GetString(produkt, "Symbol"),
                            ProductName = DynamicPropertyHelper.GetString(produkt, "Nazwa"),
                            ProductEan = DynamicPropertyHelper.GetString(produkt, "KodEan"),
                            WarehouseSymbol = magazyn != null ? DynamicPropertyHelper.GetString(magazyn, "Symbol") : null,
                            WarehouseName = magazyn != null ? DynamicPropertyHelper.GetString(magazyn, "Nazwa") : null,
                            StockQuantity = iloscDostepna + iloscZarezerwowanaIlosciowo + iloscZadysponowana,
                            ReservedQuantity = iloscZarezerwowanaIlosciowo + iloscZadysponowana,
                            AvailableQuantity = iloscDostepna,
                            Unit = DynamicPropertyHelper.StockUnitSymbol(produkt),
                            MinStockLevel = DynamicPropertyHelper.GetNullableDecimal(produkt, "StanMinimalny"),
                            MaxStockLevel = DynamicPropertyHelper.GetNullableDecimal(produkt, "StanMaksymalny")
                        };

                        // Check for low stock
                        if (lowStock.HasValue && lowStock.Value)
                        {
                            if (item.MinStockLevel.HasValue && item.AvailableQuantity >= item.MinStockLevel.Value)
                            {
                                continue; // Skip if not low stock
                            }
                            else if (!item.MinStockLevel.HasValue)
                            {
                                continue; // Skip if no min level defined
                            }
                        }

                        inventoryItems.Add(item);
                    }

                    // If no stock exists for this product but it's requested specifically
                    if (stany.Count == 0 && (productId.HasValue || !string.IsNullOrEmpty(productSymbol)))
                    {
                        var allWarehouses = new List<object>();
                        if (magazynFilter != null)
                        {
                            allWarehouses.Add(magazynFilter);
                        }
                        else if (magazynyManager != null)
                        {
                            foreach (var m in DynamicPropertyHelper.SafeGetAll((object)magazynyManager))
                            {
                                allWarehouses.Add(m);
                            }
                        }

                        foreach (var magazyn in allWarehouses)
                        {
                            inventoryItems.Add(new InventoryItemDto
                            {
                                ProductId = DynamicPropertyHelper.GetId(produkt),
                                ProductSymbol = DynamicPropertyHelper.GetString(produkt, "Symbol"),
                                ProductName = DynamicPropertyHelper.GetString(produkt, "Nazwa"),
                                ProductEan = DynamicPropertyHelper.GetString(produkt, "KodEan"),
                                WarehouseSymbol = DynamicPropertyHelper.GetString(magazyn, "Symbol"),
                                WarehouseName = DynamicPropertyHelper.GetString(magazyn, "Nazwa"),
                                StockQuantity = 0,
                                ReservedQuantity = 0,
                                AvailableQuantity = 0,
                                Unit = DynamicPropertyHelper.StockUnitSymbol(produkt),
                                MinStockLevel = DynamicPropertyHelper.GetNullableDecimal(produkt, "StanMinimalny"),
                                MaxStockLevel = DynamicPropertyHelper.GetNullableDecimal(produkt, "StanMaksymalny")
                            });
                        }
                    }
                }

                var totalCount = inventoryItems.Count;
                var pagedItems = inventoryItems
                    .OrderBy(i => i.ProductSymbol)
                    .ThenBy(i => i.WarehouseSymbol)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return new PagedResponse<InventoryItemDto>
                {
                    Data = pagedItems,
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
            _logger.LogError(ex, "Error getting stock levels");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving stock levels", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get stock level for specific product
    /// </summary>
    [HttpGet("stock/product/{productId}")]
    public async Task<ActionResult<ApiResponse<List<InventoryItemDto>>>> GetProductStock(int productId, [FromQuery] string? warehouseSymbol)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var asortymentyManager = _sferaService.GetManager("Asortymenty");
                var magazynyManager = _sferaService.GetManager("Magazyny");
                if (asortymentyManager == null)
                {
                    return (found: true, managerMissing: true, items: (List<InventoryItemDto>?)null);
                }

                dynamic? produkt = null;
                foreach (var a in DynamicPropertyHelper.SafeGetAll((object)asortymentyManager))
                {
                    if (DynamicPropertyHelper.GetId(a) == productId)
                    {
                        produkt = a;
                        break;
                    }
                }
                if (produkt == null)
                {
                    return (found: false, managerMissing: false, items: (List<InventoryItemDto>?)null);
                }

                var stany = DynamicPropertyHelper.GetCollection((object)produkt, "StanyMagazynowe");

                if (!string.IsNullOrEmpty(warehouseSymbol) && magazynyManager != null)
                {
                    int? magazynFilterId = null;
                    foreach (var m in DynamicPropertyHelper.SafeGetAll((object)magazynyManager))
                    {
                        if (DynamicPropertyHelper.GetString(m, "Symbol") == warehouseSymbol)
                        {
                            magazynFilterId = DynamicPropertyHelper.GetId(m);
                            break;
                        }
                    }
                    if (magazynFilterId.HasValue)
                    {
                        var filteredStany = new List<object>();
                        foreach (var s in stany)
                        {
                            if (DynamicPropertyHelper.GetNullableInt(s, "Magazyn_Id") == magazynFilterId)
                            {
                                filteredStany.Add(s);
                            }
                        }
                        stany = filteredStany;
                    }
                }

                var items = new List<InventoryItemDto>();
                foreach (var stan in stany)
                {
                    var magazynId = DynamicPropertyHelper.GetNullableInt(stan, "Magazyn_Id");
                    dynamic? magazyn = null;
                    if (magazynId.HasValue && magazynyManager != null)
                    {
                        foreach (var m in DynamicPropertyHelper.SafeGetAll((object)magazynyManager))
                        {
                            if (DynamicPropertyHelper.GetId(m) == magazynId.Value)
                            {
                                magazyn = m;
                                break;
                            }
                        }
                    }

                    var iloscDostepna = DynamicPropertyHelper.GetDecimal(stan, "IloscDostepna");
                    var iloscZarezerwowanaIlosciowo = DynamicPropertyHelper.GetDecimal(stan, "IloscZarezerwowanaIlosciowo");
                    var iloscZadysponowana = DynamicPropertyHelper.GetDecimal(stan, "IloscZadysponowana");

                    items.Add(new InventoryItemDto
                    {
                        ProductId = DynamicPropertyHelper.GetId(produkt),
                        ProductSymbol = DynamicPropertyHelper.GetString(produkt, "Symbol"),
                        ProductName = DynamicPropertyHelper.GetString(produkt, "Nazwa"),
                        ProductEan = DynamicPropertyHelper.GetString(produkt, "KodEan"),
                        WarehouseSymbol = magazyn != null ? DynamicPropertyHelper.GetString(magazyn, "Symbol") : null,
                        WarehouseName = magazyn != null ? DynamicPropertyHelper.GetString(magazyn, "Nazwa") : null,
                        StockQuantity = iloscDostepna + iloscZarezerwowanaIlosciowo + iloscZadysponowana,
                        ReservedQuantity = iloscZarezerwowanaIlosciowo + iloscZadysponowana,
                        AvailableQuantity = iloscDostepna,
                        Unit = DynamicPropertyHelper.StockUnitSymbol(produkt),
                        MinStockLevel = DynamicPropertyHelper.GetNullableDecimal(produkt, "StanMinimalny"),
                        MaxStockLevel = DynamicPropertyHelper.GetNullableDecimal(produkt, "StanMaksymalny")
                    });
                }

                return (found: true, managerMissing: false, items: (List<InventoryItemDto>?)items);
            });

            if (result.managerMissing)
            {
                return StatusCode(500, ApiResponse<List<InventoryItemDto>>.Error("Failed to get Asortymenty manager"));
            }
            if (!result.found)
            {
                return NotFound(ApiResponse<List<InventoryItemDto>>.Error($"Product with ID {productId} not found"));
            }

            return Ok(ApiResponse<List<InventoryItemDto>>.Ok(result.items!));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product stock for {ProductId}", productId);
            return StatusCode(500, ApiResponse<List<InventoryItemDto>>.Error("Error retrieving product stock", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get low stock items (below minimum level)
    /// </summary>
    [HttpGet("stock/low")]
    public async Task<ActionResult<PagedResponse<InventoryItemDto>>> GetLowStockItems(
        [FromQuery] string? warehouseSymbol,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        return await GetStockLevels(warehouseSymbol, null, null, true, page, pageSize);
    }

    #endregion

    #region Warehouses

    /// <summary>
    /// Get all warehouses
    /// </summary>
    [HttpGet("warehouses")]
    public async Task<ActionResult<ApiResponse<List<WarehouseDto>>>> GetWarehouses()
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var magazynyManager = _sferaService.GetManager("Magazyny");
                if (magazynyManager == null)
                {
                    return (List<WarehouseDto>?)null;
                }

                var dtos = new List<WarehouseDto>();
                foreach (var m in DynamicPropertyHelper.SafeGetAll((object)magazynyManager))
                {
                    dtos.Add(new WarehouseDto
                    {
                        Id = DynamicPropertyHelper.GetId(m),
                        Symbol = DynamicPropertyHelper.GetString(m, "Symbol"),
                        Name = DynamicPropertyHelper.GetString(m, "Nazwa"),
                        IsActive = DynamicPropertyHelper.GetBool(m, "Aktywny")
                    });
                }

                return (List<WarehouseDto>?)dtos;
            });

            if (result == null)
            {
                return StatusCode(500, ApiResponse<List<WarehouseDto>>.Error("Failed to get Magazyny manager"));
            }

            return Ok(ApiResponse<List<WarehouseDto>>.Ok(result));
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
    [HttpGet("warehouses/{symbol}")]
    public async Task<ActionResult<ApiResponse<WarehouseDto>>> GetWarehouse(string symbol)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var magazynyManager = _sferaService.GetManager("Magazyny");
                if (magazynyManager == null)
                {
                    return (found: true, managerMissing: true, dto: (WarehouseDto?)null);
                }

                dynamic? magazyn = null;
                foreach (var m in DynamicPropertyHelper.SafeGetAll((object)magazynyManager))
                {
                    if (DynamicPropertyHelper.GetString(m, "Symbol") == symbol)
                    {
                        magazyn = m;
                        break;
                    }
                }

                if (magazyn == null)
                {
                    return (found: false, managerMissing: false, dto: (WarehouseDto?)null);
                }

                var dto = new WarehouseDto
                {
                    Id = DynamicPropertyHelper.GetId(magazyn),
                    Symbol = DynamicPropertyHelper.GetString(magazyn, "Symbol"),
                    Name = DynamicPropertyHelper.GetString(magazyn, "Nazwa"),
                    IsActive = DynamicPropertyHelper.GetBool(magazyn, "Aktywny")
                };

                return (found: true, managerMissing: false, dto: (WarehouseDto?)dto);
            });

            if (result.managerMissing)
            {
                return StatusCode(500, ApiResponse<WarehouseDto>.Error("Failed to get Magazyny manager"));
            }
            if (!result.found)
            {
                return NotFound(ApiResponse<WarehouseDto>.Error($"Warehouse with symbol '{symbol}' not found"));
            }

            return Ok(ApiResponse<WarehouseDto>.Ok(result.dto!));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting warehouse {Symbol}", symbol);
            return StatusCode(500, ApiResponse<WarehouseDto>.Error("Error retrieving warehouse", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Batches / Lots

    /// <summary>
    /// Get batches (partie) for a product
    /// </summary>
    [HttpGet("batches")]
    public async Task<ActionResult<PagedResponse<BatchDto>>> GetBatches(
        [FromQuery] int? productId,
        [FromQuery] string? productSymbol,
        [FromQuery] string? warehouseSymbol,
        [FromQuery] string? batchNumber,
        [FromQuery] bool? expiringSoon,
        [FromQuery] int expiringDays = 30,
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
                    return (PagedResponse<BatchDto>?)null;
                }

                // Get products first
                var produktyQuery = new List<object>();
                foreach (var a in DynamicPropertyHelper.SafeGetAll((object)asortymentyManager))
                {
                    if (!DynamicPropertyHelper.TracksStock(a))

                        continue;

                    if (productId.HasValue && DynamicPropertyHelper.GetId(a) != productId.Value)
                        continue;

                    if (!string.IsNullOrEmpty(productSymbol) && DynamicPropertyHelper.GetString(a, "Symbol") != productSymbol)
                        continue;

                    produktyQuery.Add(a);
                }

                var batches = new List<BatchDto>();
                var today = DateTime.Today;
                var expirationThreshold = today.AddDays(expiringDays);

                // For each product, get its batches from acceptances (przyjecia)
                foreach (var produkt in produktyQuery)
                {
                    // Get product batches through stock movements
                    var partie = GetProductBatches(produkt, warehouseSymbol);

                    foreach (var partia in partie)
                    {
                        var partiaNumer = DynamicPropertyHelper.GetString(partia, "Numer");

                        // Filter by batch number
                        if (!string.IsNullOrEmpty(batchNumber) && partiaNumer != batchNumber)
                        {
                            continue;
                        }

                        var partiaTermin = DynamicPropertyHelper.GetDateTime(partia, "Termin");

                        // Calculate days until expiration
                        int? daysUntilExpiration = null;
                        if (partiaTermin.HasValue)
                        {
                            daysUntilExpiration = (int)(partiaTermin.Value - today).TotalDays;

                            // Filter for expiring soon
                            if (expiringSoon.HasValue && expiringSoon.Value)
                            {
                                if (partiaTermin.Value > expirationThreshold)
                                {
                                    continue;
                                }
                            }
                        }

                        var przyjecie = DynamicPropertyHelper.GetProperty(partia, "Przyjecie");
                        var magazyn = przyjecie != null ? DynamicPropertyHelper.GetProperty(przyjecie, "Magazyn") : null;

                        batches.Add(new BatchDto
                        {
                            Id = DynamicPropertyHelper.GetId(partia),
                            BatchNumber = partiaNumer,
                            ProductId = DynamicPropertyHelper.GetId(produkt),
                            ProductSymbol = DynamicPropertyHelper.GetString(produkt, "Symbol"),
                            ProductName = DynamicPropertyHelper.GetString(produkt, "Nazwa"),
                            WarehouseSymbol = magazyn != null ? DynamicPropertyHelper.GetString(magazyn, "Symbol") : null,
                            Quantity = DynamicPropertyHelper.GetDecimal(partia, "Ilosc"),
                            Unit = DynamicPropertyHelper.StockUnitSymbol(produkt),
                            ExpirationDate = partiaTermin,
                            DaysUntilExpiration = daysUntilExpiration,
                            Notes = DynamicPropertyHelper.GetString(partia, "Komentarz")
                        });
                    }
                }

                var totalCount = batches.Count;
                var pagedBatches = batches
                    .OrderBy(b => b.ExpirationDate ?? DateTime.MaxValue)
                    .ThenBy(b => b.ProductSymbol)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return new PagedResponse<BatchDto>
                {
                    Data = pagedBatches,
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
            _logger.LogError(ex, "Error getting batches");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving batches", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get expiring batches
    /// </summary>
    [HttpGet("batches/expiring")]
    public async Task<ActionResult<PagedResponse<BatchDto>>> GetExpiringBatches(
        [FromQuery] string? warehouseSymbol = null,
        [FromQuery] int days = 30,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        return await GetBatches(null, null, warehouseSymbol, null, true, days, page, pageSize);
    }

    private List<object> GetProductBatches(dynamic produkt, string? warehouseSymbol)
    {
        var partie = new List<object>();

        try
        {
            var produktId = DynamicPropertyHelper.GetId(produkt);
            var przyjeciaManager = _sferaService.GetManager("PrzyjeciaZewnetrzne");
            if (przyjeciaManager == null)
                return partie;

            // Get batches from przyjecia (warehouse receipts)
            var przyjecia = new List<object>();
            foreach (var p in DynamicPropertyHelper.SafeGetAll((object)przyjeciaManager))
            {
                var pozycje = DynamicPropertyHelper.GetCollection((object)p, "Pozycje");
                bool hasProduct = false;
                foreach (var poz in pozycje)
                {
                    var asortyment = DynamicPropertyHelper.GetProperty(poz, "Asortyment");
                    if (asortyment != null && DynamicPropertyHelper.GetId(asortyment) == produktId)
                    {
                        hasProduct = true;
                        break;
                    }
                }
                if (hasProduct)
                {
                    if (!string.IsNullOrEmpty(warehouseSymbol))
                    {
                        var magazyn = DynamicPropertyHelper.GetProperty(p, "Magazyn");
                        if (magazyn != null && DynamicPropertyHelper.GetString(magazyn, "Symbol") == warehouseSymbol)
                        {
                            przyjecia.Add(p);
                        }
                    }
                    else
                    {
                        przyjecia.Add(p);
                    }
                }
            }

            foreach (var przyjecie in przyjecia)
            {
                var allPozycje = DynamicPropertyHelper.GetCollection((object)przyjecie, "Pozycje");
                foreach (var poz in allPozycje)
                {
                    var asortyment = DynamicPropertyHelper.GetProperty(poz, "Asortyment");
                    if (asortyment != null && DynamicPropertyHelper.GetId(asortyment) == produktId)
                    {
                        var partieCollection = DynamicPropertyHelper.GetCollection((object)poz, "Partie");
                        partie.AddRange(partieCollection);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            int productIdValue = DynamicPropertyHelper.GetId(produkt);
            _logger.LogWarning(ex, "Error getting batches for product {ProductId}", productIdValue);
        }

        return partie;
    }

    #endregion

    #region Reservations

    /// <summary>
    /// Get reservations
    /// </summary>
    [HttpGet("reservations")]
    public async Task<ActionResult<PagedResponse<ReservationDto>>> GetReservations(
        [FromQuery] int? productId,
        [FromQuery] int? customerId,
        [FromQuery] string? warehouseSymbol,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var zamowieniaManager = _sferaService.GetManager("ZamowieniaOdKlientow");
                if (zamowieniaManager == null)
                {
                    return (PagedResponse<ReservationDto>?)null;
                }

                // Document status constants
                const int StatusAnulowany = 4;

                // Get reservations from customer orders (ZK)
                var zamowienia = new List<object>();
                foreach (var z in DynamicPropertyHelper.SafeGetAll((object)zamowieniaManager))
                {
                    if (DynamicPropertyHelper.GetInt(z, "Status") == StatusAnulowany)
                        continue;

                    if (customerId.HasValue)
                    {
                        var podmiot = DynamicPropertyHelper.GetProperty(z, "Podmiot");
                        if (podmiot == null || DynamicPropertyHelper.GetId(podmiot) != customerId.Value)
                            continue;
                    }

                    if (!string.IsNullOrEmpty(warehouseSymbol))
                    {
                        var magazyn = DynamicPropertyHelper.GetProperty(z, "Magazyn");
                        if (magazyn == null || DynamicPropertyHelper.GetString(magazyn, "Symbol") != warehouseSymbol)
                            continue;
                    }

                    zamowienia.Add(z);
                }

                var reservations = new List<ReservationDto>();

                foreach (var zamowienie in zamowienia)
                {
                    var pozycje = DynamicPropertyHelper.GetCollection((object)zamowienie, "Pozycje");

                    foreach (var pozycja in pozycje)
                    {
                        var asortyment = DynamicPropertyHelper.GetProperty(pozycja, "Asortyment");
                        if (productId.HasValue && (asortyment == null || DynamicPropertyHelper.GetId(asortyment) != productId.Value))
                        {
                            continue;
                        }

                        // Check if position has reserved quantity
                        var rezerwowana = DynamicPropertyHelper.GetDecimal(pozycja, "IloscZarezerwowana");
                        if (rezerwowana <= 0)
                        {
                            continue;
                        }

                        var podmiot = DynamicPropertyHelper.GetProperty(zamowienie, "Podmiot");
                        var magazyn = DynamicPropertyHelper.GetProperty(zamowienie, "Magazyn");
                        var numerWewnetrzny = DynamicPropertyHelper.GetProperty(zamowienie, "NumerWewnetrzny");
                        var jednostka = DynamicPropertyHelper.GetProperty(pozycja, "Jednostka");

                        reservations.Add(new ReservationDto
                        {
                            Id = DynamicPropertyHelper.GetId(pozycja),
                            ProductId = asortyment != null ? DynamicPropertyHelper.GetId(asortyment) : 0,
                            ProductSymbol = asortyment != null ? DynamicPropertyHelper.GetString(asortyment, "Symbol") : null,
                            ProductName = DynamicPropertyHelper.GetString(pozycja, "Nazwa"),
                            WarehouseSymbol = magazyn != null ? DynamicPropertyHelper.GetString(magazyn, "Symbol") : null,
                            ReservedQuantity = rezerwowana,
                            Unit = DynamicPropertyHelper.UnitSymbol(jednostka) ?? "szt.",
                            SourceDocumentId = DynamicPropertyHelper.GetId(zamowienie),
                            SourceDocumentNumber = numerWewnetrzny != null ? DynamicPropertyHelper.GetString(numerWewnetrzny, "PelnaSygnatura") : null,
                            CustomerId = podmiot != null ? DynamicPropertyHelper.GetId(podmiot) : null,
                            CustomerName = podmiot != null ? DynamicPropertyHelper.GetString(podmiot, "NazwaSkrocona") : null,
                            ReservationDate = DynamicPropertyHelper.GetDateTime(zamowienie, "DataWystawienia"),
                            Status = GetReservationStatus(DynamicPropertyHelper.GetInt(zamowienie, "Status"))
                        });
                    }
                }

                var totalCount = reservations.Count;
                var pagedReservations = reservations
                    .OrderByDescending(r => r.ReservationDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return new PagedResponse<ReservationDto>
                {
                    Data = pagedReservations,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };
            });

            if (result == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get ZamowieniaOdKlientow manager"));
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reservations");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving reservations", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get reservations for a specific product
    /// </summary>
    [HttpGet("reservations/product/{productId}")]
    public async Task<ActionResult<PagedResponse<ReservationDto>>> GetProductReservations(
        int productId,
        [FromQuery] string? warehouseSymbol,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        return await GetReservations(productId, null, warehouseSymbol, page, pageSize);
    }

    private string GetReservationStatus(int status)
    {
        // Document status constants
        const int StatusBufor = 0;
        const int StatusZatwierdzony = 1;
        const int StatusCzesciowoZrealizowany = 2;
        const int StatusZrealizowany = 3;
        const int StatusAnulowany = 4;

        return status switch
        {
            StatusBufor => "Pending",
            StatusZatwierdzony => "Confirmed",
            StatusCzesciowoZrealizowany => "PartiallyFulfilled",
            StatusZrealizowany => "Fulfilled",
            StatusAnulowany => "Cancelled",
            _ => "Unknown"
        };
    }

    #endregion

    #region Inventory Summary

    /// <summary>
    /// Get inventory summary for a warehouse
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<ApiResponse<InventorySummaryDto>>> GetInventorySummary([FromQuery] string? warehouseSymbol)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var asortymentyManager = _sferaService.GetManager("Asortymenty");
                var magazynyManager = _sferaService.GetManager("Magazyny");
                if (asortymentyManager == null)
                {
                    return (InventorySummaryDto?)null;
                }

                int? magazynFilterId = null;
                if (!string.IsNullOrEmpty(warehouseSymbol) && magazynyManager != null)
                {
                    foreach (var m in DynamicPropertyHelper.SafeGetAll((object)magazynyManager))
                    {
                        if (DynamicPropertyHelper.GetString(m, "Symbol") == warehouseSymbol)
                        {
                            magazynFilterId = DynamicPropertyHelper.GetId(m);
                            break;
                        }
                    }
                }

                var summary = new InventorySummaryDto
                {
                    WarehouseSymbol = warehouseSymbol,
                    TotalProducts = 0,
                    ProductsInStock = 0,
                    ProductsOutOfStock = 0,
                    ProductsLowStock = 0,
                    TotalStockQuantity = 0,
                    TotalReservedQuantity = 0,
                    TotalAvailableQuantity = 0
                };

                foreach (var produkt in DynamicPropertyHelper.SafeGetAll((object)asortymentyManager))
                {
                    if (!DynamicPropertyHelper.TracksStock(produkt))

                        continue;

                    var stany = DynamicPropertyHelper.GetCollection((object)produkt, "StanyMagazynowe");

                    if (magazynFilterId.HasValue)
                    {
                        var filteredStany = new List<object>();
                        foreach (var s in stany)
                        {
                            if (DynamicPropertyHelper.GetNullableInt(s, "Magazyn_Id") == magazynFilterId.Value)
                            {
                                filteredStany.Add(s);
                            }
                        }
                        stany = filteredStany;
                    }

                    if (stany.Count == 0)
                    {
                        summary.ProductsOutOfStock++;
                        continue;
                    }

                    decimal totalStock = 0;
                    decimal totalReserved = 0;
                    decimal totalAvailable = 0;
                    foreach (var s in stany)
                    {
                        var iloscDostepna = DynamicPropertyHelper.GetDecimal(s, "IloscDostepna");
                        var iloscZarezerwowanaIlosciowo = DynamicPropertyHelper.GetDecimal(s, "IloscZarezerwowanaIlosciowo");
                        var iloscZadysponowana = DynamicPropertyHelper.GetDecimal(s, "IloscZadysponowana");
                        totalStock += iloscDostepna + iloscZarezerwowanaIlosciowo + iloscZadysponowana;
                        totalReserved += iloscZarezerwowanaIlosciowo + iloscZadysponowana;
                        totalAvailable += iloscDostepna;
                    }

                    summary.TotalProducts++;
                    summary.TotalStockQuantity += totalStock;
                    summary.TotalReservedQuantity += totalReserved;
                    summary.TotalAvailableQuantity += totalAvailable;

                    if (totalAvailable > 0)
                    {
                        summary.ProductsInStock++;
                    }
                    else
                    {
                        summary.ProductsOutOfStock++;
                    }

                    var stanMinimalny = DynamicPropertyHelper.GetNullableDecimal(produkt, "StanMinimalny");
                    if (stanMinimalny.HasValue && totalAvailable < stanMinimalny.Value)
                    {
                        summary.ProductsLowStock++;
                    }
                }

                return (InventorySummaryDto?)summary;
            });

            if (result == null)
            {
                return StatusCode(500, ApiResponse<InventorySummaryDto>.Error("Failed to get Asortymenty manager"));
            }

            return Ok(ApiResponse<InventorySummaryDto>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting inventory summary");
            return StatusCode(500, ApiResponse<InventorySummaryDto>.Error("Error retrieving inventory summary", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Inventory Movements (Phase 2)

    /// <summary>
    /// Get inventory movements history
    /// </summary>
    [HttpGet("movements")]
    [ProducesResponseType(typeof(PagedResponse<InventoryMovementDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<InventoryMovementDto>>> GetInventoryMovements(
        [FromQuery] int? productId,
        [FromQuery] string? productSymbol,
        [FromQuery] string? warehouseSymbol,
        [FromQuery] string? documentType,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var movements = new List<InventoryMovementDto>();

                // Get movements from different document types
                AddMovementsFromDocuments("WydaniaZewnetrzne", movements, productId, productSymbol, warehouseSymbol, documentType, dateFrom, dateTo, "WZ", false);
                AddMovementsFromDocuments("PrzyjeciaZewnetrzne", movements, productId, productSymbol, warehouseSymbol, documentType, dateFrom, dateTo, "PZ", true);
                AddMovementsFromDocuments("RozchodyWewnetrzne", movements, productId, productSymbol, warehouseSymbol, documentType, dateFrom, dateTo, "RW", false);

                // Sort by date descending
                movements = movements
                    .OrderByDescending(m => m.Date)
                    .ToList();

                var totalCount = movements.Count;
                var pagedMovements = movements
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return new PagedResponse<InventoryMovementDto>
                {
                    Data = pagedMovements,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting inventory movements");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving inventory movements", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get inventory movements for a specific product
    /// </summary>
    [HttpGet("movements/product/{productId}")]
    [ProducesResponseType(typeof(PagedResponse<InventoryMovementDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<InventoryMovementDto>>> GetProductMovements(
        int productId,
        [FromQuery] string? warehouseSymbol,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        return await GetInventoryMovements(productId, null, warehouseSymbol, null, dateFrom, dateTo, page, pageSize);
    }

    private void AddMovementsFromDocuments(
        string managerName,
        List<InventoryMovementDto> movements,
        int? productId,
        string? productSymbol,
        string? warehouseSymbol,
        string? documentType,
        DateTime? dateFrom,
        DateTime? dateTo,
        string docType,
        bool isIncoming)
    {
        try
        {
            // Skip if filtering by document type and this isn't the right type
            if (!string.IsNullOrEmpty(documentType) && !documentType.Equals(docType, StringComparison.OrdinalIgnoreCase))
                return;

            var manager = _sferaService.GetManager(managerName);
            if (manager == null) return;

            foreach (var doc in DynamicPropertyHelper.SafeGetAll((object)manager))
            {
                // Filter by warehouse
                if (!string.IsNullOrEmpty(warehouseSymbol))
                {
                    var magazyn = DynamicPropertyHelper.GetProperty(doc, "Magazyn");
                    if (magazyn == null || DynamicPropertyHelper.GetString(magazyn, "Symbol") != warehouseSymbol)
                        continue;
                }

                // Filter by date
                var dataWystawienia = DynamicPropertyHelper.GetDateTime(doc, "DataWystawienia");
                if (dateFrom.HasValue && (!dataWystawienia.HasValue || dataWystawienia.Value < dateFrom.Value))
                    continue;
                if (dateTo.HasValue && (!dataWystawienia.HasValue || dataWystawienia.Value > dateTo.Value))
                    continue;

                var pozycje = DynamicPropertyHelper.GetCollection((object)doc, "Pozycje");
                var numerWewnetrzny = DynamicPropertyHelper.GetProperty(doc, "NumerWewnetrzny");
                var docMagazyn = DynamicPropertyHelper.GetProperty(doc, "Magazyn");

                foreach (var poz in pozycje)
                {
                    var asortyment = DynamicPropertyHelper.GetProperty(poz, "Asortyment");
                    if (asortyment == null) continue;

                    // Filter by product
                    if (productId.HasValue && DynamicPropertyHelper.GetId(asortyment) != productId.Value)
                        continue;
                    if (!string.IsNullOrEmpty(productSymbol) && DynamicPropertyHelper.GetString(asortyment, "Symbol") != productSymbol)
                        continue;

                    var jednostka = DynamicPropertyHelper.GetProperty(poz, "Jednostka");
                    var ilosc = DynamicPropertyHelper.GetDecimal(poz, "Ilosc");

                    movements.Add(new InventoryMovementDto
                    {
                        Id = DynamicPropertyHelper.GetId(poz),
                        DocumentId = DynamicPropertyHelper.GetId(doc),
                        DocumentNumber = numerWewnetrzny != null ? DynamicPropertyHelper.GetString(numerWewnetrzny, "PelnaSygnatura") : null,
                        DocumentType = docType,
                        Date = dataWystawienia,
                        ProductId = DynamicPropertyHelper.GetId(asortyment),
                        ProductSymbol = DynamicPropertyHelper.GetString(asortyment, "Symbol"),
                        ProductName = DynamicPropertyHelper.GetString(asortyment, "Nazwa"),
                        WarehouseSymbol = docMagazyn != null ? DynamicPropertyHelper.GetString(docMagazyn, "Symbol") : null,
                        WarehouseName = docMagazyn != null ? DynamicPropertyHelper.GetString(docMagazyn, "Nazwa") : null,
                        Quantity = ilosc,
                        Unit = DynamicPropertyHelper.UnitSymbol(jednostka) ?? "szt.",
                        MovementType = isIncoming ? "In" : "Out",
                        UnitPrice = DynamicPropertyHelper.GetNullableDecimal(poz, "CenaNetto"),
                        TotalValue = DynamicPropertyHelper.GetNullableDecimal(poz, "WartoscNetto")
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting movements from {Manager}", managerName);
        }
    }

    #endregion

    #region Inventory Valuation (Phase 2)

    /// <summary>
    /// Get inventory valuation summary
    /// </summary>
    [HttpGet("valuation")]
    [ProducesResponseType(typeof(ApiResponse<InventoryValuationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<InventoryValuationDto>>> GetInventoryValuation(
        [FromQuery] string? warehouseSymbol,
        [FromQuery] string? method = "AVG")
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var asortymentyManager = _sferaService.GetManager("Asortymenty");
                var magazynyManager = _sferaService.GetManager("Magazyny");
                if (asortymentyManager == null)
                {
                    return (InventoryValuationDto?)null;
                }

                int? magazynFilterId = null;
                if (!string.IsNullOrEmpty(warehouseSymbol) && magazynyManager != null)
                {
                    foreach (var m in DynamicPropertyHelper.SafeGetAll((object)magazynyManager))
                    {
                        if (DynamicPropertyHelper.GetString(m, "Symbol") == warehouseSymbol)
                        {
                            magazynFilterId = DynamicPropertyHelper.GetId(m);
                            break;
                        }
                    }
                }

                decimal totalValue = 0;
                int productCount = 0;
                var itemsByCategory = new Dictionary<string, decimal>();

                foreach (var produkt in DynamicPropertyHelper.SafeGetAll((object)asortymentyManager))
                {
                    if (!DynamicPropertyHelper.TracksStock(produkt))

                        continue;

                    var stany = DynamicPropertyHelper.GetCollection((object)produkt, "StanyMagazynowe");

                    if (magazynFilterId.HasValue)
                    {
                        var filteredStany = new List<object>();
                        foreach (var s in stany)
                        {
                            if (DynamicPropertyHelper.GetNullableInt(s, "Magazyn_Id") == magazynFilterId.Value)
                            {
                                filteredStany.Add(s);
                            }
                        }
                        stany = filteredStany;
                    }

                    decimal productQuantity = 0;
                    foreach (var stan in stany)
                    {
                        var iloscDostepna = DynamicPropertyHelper.GetDecimal(stan, "IloscDostepna");
                        var iloscZarezerwowana = DynamicPropertyHelper.GetDecimal(stan, "IloscZarezerwowanaIlosciowo");
                        var iloscZadysponowana = DynamicPropertyHelper.GetDecimal(stan, "IloscZadysponowana");
                        productQuantity += iloscDostepna + iloscZarezerwowana + iloscZadysponowana;
                    }

                    if (productQuantity <= 0) continue;

                    // Get unit cost (using average cost or last purchase price)
                    var cenaZakupu = DynamicPropertyHelper.GetNullableDecimal(produkt, "OstatniaCenaZakupuNetto") ?? 0;
                    var productValue = productQuantity * cenaZakupu;

                    totalValue += productValue;
                    productCount++;

                    // Group by product group
                    var grupa = DynamicPropertyHelper.GetProperty(produkt, "Grupa");
                    var groupName = grupa != null ? DynamicPropertyHelper.GetString(grupa, "Nazwa") ?? "Bez grupy" : "Bez grupy";
                    if (!itemsByCategory.ContainsKey(groupName))
                        itemsByCategory[groupName] = 0;
                    itemsByCategory[groupName] += productValue;
                }

                return (InventoryValuationDto?)new InventoryValuationDto
                {
                    WarehouseSymbol = warehouseSymbol,
                    ValuationMethod = method?.ToUpper() ?? "AVG",
                    ValuationDate = DateTime.Today,
                    TotalValue = totalValue,
                    ProductCount = productCount,
                    CategoryBreakdown = itemsByCategory
                        .Select(kvp => new ValuationCategoryDto { Category = kvp.Key, Value = kvp.Value })
                        .OrderByDescending(c => c.Value)
                        .ToList()
                };
            });

            if (result == null)
            {
                return StatusCode(500, ApiResponse<InventoryValuationDto>.Error("Failed to get Asortymenty manager"));
            }

            return Ok(ApiResponse<InventoryValuationDto>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting inventory valuation");
            return StatusCode(500, ApiResponse<InventoryValuationDto>.Error("Error retrieving inventory valuation", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Stocktaking (Inwentaryzacja) - Phase 2

    /// <summary>
    /// Get stocktaking documents (spisy inwentaryzacyjne)
    /// </summary>
    [HttpGet("stocktaking")]
    [ProducesResponseType(typeof(PagedResponse<StocktakingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<StocktakingDto>>> GetStocktakingDocuments(
        [FromQuery] string? warehouseSymbol,
        [FromQuery] string? status,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("SpisyInwentaryzacyjne");
                if (manager == null)
                {
                    return (PagedResponse<StocktakingDto>?)null;
                }

                var allSpisy = DynamicPropertyHelper.SafeGetAll((object)manager);

                // Filter by warehouse
                if (!string.IsNullOrEmpty(warehouseSymbol))
                {
                    allSpisy = allSpisy.Where(s =>
                    {
                        var magazyn = DynamicPropertyHelper.GetProperty(s, "Magazyn");
                        return magazyn != null && DynamicPropertyHelper.GetString(magazyn, "Symbol") == warehouseSymbol;
                    }).ToList();
                }

                // Filter by date
                if (dateFrom.HasValue)
                {
                    allSpisy = allSpisy.Where(s =>
                    {
                        var data = DynamicPropertyHelper.GetDateTime(s, "DataInwentaryzacji");
                        return data.HasValue && data.Value >= dateFrom.Value;
                    }).ToList();
                }

                if (dateTo.HasValue)
                {
                    allSpisy = allSpisy.Where(s =>
                    {
                        var data = DynamicPropertyHelper.GetDateTime(s, "DataInwentaryzacji");
                        return data.HasValue && data.Value <= dateTo.Value;
                    }).ToList();
                }

                // Filter by status
                if (!string.IsNullOrEmpty(status))
                {
                    allSpisy = allSpisy.Where(s =>
                    {
                        var spisStatus = MapStocktakingStatus(DynamicPropertyHelper.GetNullableInt(s, "Status"));
                        return spisStatus.Equals(status, StringComparison.OrdinalIgnoreCase);
                    }).ToList();
                }

                var totalCount = allSpisy.Count;
                var pagedSpisy = allSpisy
                    .OrderByDescending(s => DynamicPropertyHelper.GetDateTime(s, "DataInwentaryzacji") ?? DateTime.MinValue)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var items = new List<StocktakingDto>();
                foreach (var s in pagedSpisy)
                {
                    items.Add(MapStocktaking(s, false));
                }

                return new PagedResponse<StocktakingDto>
                {
                    Data = items,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };
            });

            if (result == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get SpisyInwentaryzacyjne manager"));
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stocktaking documents");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving stocktaking documents", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get stocktaking document by ID
    /// </summary>
    [HttpGet("stocktaking/{id}")]
    [ProducesResponseType(typeof(ApiResponse<StocktakingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<StocktakingDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<StocktakingDto>>> GetStocktaking(int id, [FromQuery] bool includeItems = true)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("SpisyInwentaryzacyjne");
                if (manager == null)
                {
                    return (found: true, managerMissing: true, dto: (StocktakingDto?)null);
                }

                var allSpisy = DynamicPropertyHelper.SafeGetAll((object)manager);
                var spis = allSpisy.FirstOrDefault(s => DynamicPropertyHelper.GetId(s) == id);

                if (spis == null)
                {
                    return (found: false, managerMissing: false, dto: (StocktakingDto?)null);
                }

                return (found: true, managerMissing: false, dto: (StocktakingDto?)MapStocktaking(spis, includeItems));
            });

            if (result.managerMissing)
            {
                return StatusCode(500, ApiResponse<StocktakingDto>.Error("Failed to get SpisyInwentaryzacyjne manager"));
            }
            if (!result.found)
            {
                return NotFound(ApiResponse<StocktakingDto>.Error($"Stocktaking document with ID {id} not found"));
            }

            return Ok(ApiResponse<StocktakingDto>.Ok(result.dto!));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stocktaking document {Id}", id);
            return StatusCode(500, ApiResponse<StocktakingDto>.Error("Error retrieving stocktaking document", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get stocktaking items (pozycje spisu inwentaryzacyjnego)
    /// </summary>
    [HttpGet("stocktaking/{id}/items")]
    [ProducesResponseType(typeof(PagedResponse<StocktakingItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<StocktakingItemDto>>> GetStocktakingItems(
        int id,
        [FromQuery] bool? withDifference,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("SpisyInwentaryzacyjne");
                if (manager == null)
                {
                    return (found: true, managerMissing: true, response: (PagedResponse<StocktakingItemDto>?)null);
                }

                var allSpisy = DynamicPropertyHelper.SafeGetAll((object)manager);
                var spis = allSpisy.FirstOrDefault(s => DynamicPropertyHelper.GetId(s) == id);

                if (spis == null)
                {
                    return (found: false, managerMissing: false, response: (PagedResponse<StocktakingItemDto>?)null);
                }

                var pozycje = DynamicPropertyHelper.GetCollection((object)spis, "Pozycje");
                var items = new List<StocktakingItemDto>();

                foreach (var poz in pozycje)
                {
                    var item = MapStocktakingItem(poz);

                    // Filter items with difference
                    if (withDifference.HasValue && withDifference.Value)
                    {
                        if (item.Difference == 0)
                            continue;
                    }

                    items.Add(item);
                }

                var totalCount = items.Count;
                var pagedItems = items
                    .OrderBy(i => i.ProductSymbol)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return (found: true, managerMissing: false, response: (PagedResponse<StocktakingItemDto>?)new PagedResponse<StocktakingItemDto>
                {
                    Data = pagedItems,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                });
            });

            if (result.managerMissing)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get SpisyInwentaryzacyjne manager"));
            }
            if (!result.found)
            {
                return NotFound(ApiResponse<object>.Error($"Stocktaking document with ID {id} not found"));
            }

            return Ok(result.response!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stocktaking items for {Id}", id);
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving stocktaking items", new List<string> { ex.Message }));
        }
    }

    private static StocktakingDto MapStocktaking(dynamic s, bool includeItems)
    {
        var magazyn = DynamicPropertyHelper.GetProperty(s, "Magazyn");
        var numerWewnetrzny = DynamicPropertyHelper.GetProperty(s, "NumerWewnetrzny");
        var pozycje = DynamicPropertyHelper.GetCollection((object)s, "Pozycje");

        var dto = new StocktakingDto
        {
            Id = DynamicPropertyHelper.GetId(s),
            Number = numerWewnetrzny != null ? DynamicPropertyHelper.GetString(numerWewnetrzny, "PelnaSygnatura") : null,
            WarehouseSymbol = magazyn != null ? DynamicPropertyHelper.GetString(magazyn, "Symbol") : null,
            WarehouseName = magazyn != null ? DynamicPropertyHelper.GetString(magazyn, "Nazwa") : null,
            Date = DynamicPropertyHelper.GetDateTime(s, "DataInwentaryzacji"),
            Status = MapStocktakingStatus(DynamicPropertyHelper.GetNullableInt(s, "Status")),
            ItemCount = pozycje.Count(),
            Description = DynamicPropertyHelper.GetString(s, "Opis")
        };

        if (includeItems)
        {
            dto.Items = new List<StocktakingItemDto>();
            foreach (var poz in pozycje)
            {
                dto.Items.Add(MapStocktakingItem(poz));
            }
        }

        return dto;
    }

    private static StocktakingItemDto MapStocktakingItem(dynamic poz)
    {
        var asortyment = DynamicPropertyHelper.GetProperty(poz, "Asortyment");
        var jednostka = DynamicPropertyHelper.GetProperty(poz, "Jednostka");

        var iloscEwidencyjna = DynamicPropertyHelper.GetDecimal(poz, "IloscEwidencyjna");
        var iloscRzeczywista = DynamicPropertyHelper.GetDecimal(poz, "IloscRzeczywista");

        return new StocktakingItemDto
        {
            Id = DynamicPropertyHelper.GetId(poz),
            ProductId = asortyment != null ? DynamicPropertyHelper.GetId(asortyment) : 0,
            ProductSymbol = asortyment != null ? DynamicPropertyHelper.GetString(asortyment, "Symbol") : null,
            ProductName = asortyment != null ? DynamicPropertyHelper.GetString(asortyment, "Nazwa") : null,
            Unit = DynamicPropertyHelper.UnitSymbol(jednostka) ?? "szt.",
            BookQuantity = iloscEwidencyjna,
            ActualQuantity = iloscRzeczywista,
            Difference = iloscRzeczywista - iloscEwidencyjna,
            UnitPrice = DynamicPropertyHelper.GetNullableDecimal(poz, "Cena"),
            Notes = DynamicPropertyHelper.GetString(poz, "Uwagi")
        };
    }

    private static string MapStocktakingStatus(int? status)
    {
        return status switch
        {
            0 => "Draft",       // Bufor
            1 => "InProgress",  // W trakcie
            2 => "Completed",   // Zakończony
            3 => "Closed",      // Zamknięty
            _ => "Unknown"
        };
    }

    #endregion

    #region Free Quantity (IMagazynier)

    /// <summary>
    /// Get free (available) quantity for a product via IMagazynier service
    /// </summary>
    [HttpGet("free-quantity")]
    [ProducesResponseType(typeof(ApiResponse<FreeQuantityDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<FreeQuantityDto>>> GetFreeQuantity(
        [FromQuery] int productId,
        [FromQuery] string? warehouseSymbol)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                // Get product
                var asortymentyManager = _sferaService.GetManager("Asortymenty");
                if (asortymentyManager == null)
                {
                    return (statusCode: 500, dto: (FreeQuantityDto?)null, error: "Failed to get Asortymenty manager");
                }

                dynamic? produkt = null;
                foreach (var a in DynamicPropertyHelper.SafeGetAll((object)asortymentyManager))
                {
                    if (DynamicPropertyHelper.GetId(a) == productId)
                    {
                        produkt = a;
                        break;
                    }
                }

                if (produkt == null)
                {
                    return (statusCode: 404, dto: (FreeQuantityDto?)null, error: $"Product with ID {productId} not found");
                }

                // Get warehouse if specified
                dynamic? magazyn = null;
                if (!string.IsNullOrEmpty(warehouseSymbol))
                {
                    var magazynyManager = _sferaService.GetManager("Magazyny");
                    if (magazynyManager != null)
                    {
                        foreach (var m in DynamicPropertyHelper.SafeGetAll((object)magazynyManager))
                        {
                            if (DynamicPropertyHelper.GetString(m, "Symbol") == warehouseSymbol)
                            {
                                magazyn = m;
                                break;
                            }
                        }
                    }

                    if (magazyn == null)
                    {
                        return (statusCode: 404, dto: (FreeQuantityDto?)null, error: $"Warehouse '{warehouseSymbol}' not found");
                    }
                }

                // Try to use IMagazynier.IloscWolna if available
                decimal freeQuantity = 0;
                try
                {
                    var magazynier = _sferaService.GetManagerByType("InsERT.Moria.API", "InsERT.Moria.EgzekutorMagazynowy.IMagazynier");
                    if (magazynier != null)
                    {
                        if (magazyn != null)
                        {
                            freeQuantity = magazynier.IloscWolna(produkt, magazyn);
                        }
                        else
                        {
                            freeQuantity = magazynier.IloscWolna(produkt);
                        }
                    }
                    else
                    {
                        // Fallback: calculate from stock levels
                        freeQuantity = CalculateFreeQuantityFromStock(produkt, warehouseSymbol);
                    }
                }
                catch
                {
                    // Fallback: calculate from stock levels
                    freeQuantity = CalculateFreeQuantityFromStock(produkt, warehouseSymbol);
                }

                var dto = new FreeQuantityDto
                {
                    ProductId = productId,
                    ProductSymbol = DynamicPropertyHelper.GetString(produkt, "Symbol"),
                    ProductName = DynamicPropertyHelper.GetString(produkt, "Nazwa"),
                    WarehouseSymbol = warehouseSymbol,
                    FreeQuantity = freeQuantity,
                    Unit = DynamicPropertyHelper.StockUnitSymbol(produkt)
                };

                return (statusCode: 200, dto: (FreeQuantityDto?)dto, error: (string?)"");
            });

            if (result.statusCode == 500)
            {
                return StatusCode(500, ApiResponse<FreeQuantityDto>.Error(result.error ?? "Internal error"));
            }
            if (result.statusCode == 404)
            {
                return NotFound(ApiResponse<FreeQuantityDto>.Error(result.error ?? "Not found"));
            }

            return Ok(ApiResponse<FreeQuantityDto>.Ok(result.dto!));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting free quantity for product {ProductId}", productId);
            return StatusCode(500, ApiResponse<FreeQuantityDto>.Error("Error retrieving free quantity", new List<string> { ex.Message }));
        }
    }

    private decimal CalculateFreeQuantityFromStock(dynamic produkt, string? warehouseSymbol)
    {
        var stany = DynamicPropertyHelper.GetCollection((object)produkt, "StanyMagazynowe");
        decimal total = 0;

        int? magazynFilterId = null;
        if (!string.IsNullOrEmpty(warehouseSymbol))
        {
            var magazynyManager = _sferaService.GetManager("Magazyny");
            if (magazynyManager != null)
            {
                foreach (var m in DynamicPropertyHelper.SafeGetAll((object)magazynyManager))
                {
                    if (DynamicPropertyHelper.GetString(m, "Symbol") == warehouseSymbol)
                    {
                        magazynFilterId = DynamicPropertyHelper.GetId(m);
                        break;
                    }
                }
            }
        }

        foreach (var stan in stany)
        {
            if (magazynFilterId.HasValue)
            {
                var magazynId = DynamicPropertyHelper.GetNullableInt(stan, "Magazyn_Id");
                if (magazynId != magazynFilterId.Value)
                    continue;
            }
            total += DynamicPropertyHelper.GetDecimal(stan, "IloscDostepna");
        }

        return total;
    }

    #endregion
}

/// <summary>
/// Free quantity DTO
/// </summary>
public class FreeQuantityDto
{
    public int ProductId { get; set; }
    public string? ProductSymbol { get; set; }
    public string? ProductName { get; set; }
    public string? WarehouseSymbol { get; set; }
    public decimal FreeQuantity { get; set; }
    public string Unit { get; set; } = "szt.";
}

/// <summary>
/// Warehouse DTO
/// </summary>
public class WarehouseDto
{
    public int Id { get; set; }
    public string? Symbol { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Inventory summary DTO
/// </summary>
public class InventorySummaryDto
{
    public string? WarehouseSymbol { get; set; }
    public int TotalProducts { get; set; }
    public int ProductsInStock { get; set; }
    public int ProductsOutOfStock { get; set; }
    public int ProductsLowStock { get; set; }
    public decimal TotalStockQuantity { get; set; }
    public decimal TotalReservedQuantity { get; set; }
    public decimal TotalAvailableQuantity { get; set; }
}

/// <summary>
/// Inventory movement DTO
/// </summary>
public class InventoryMovementDto
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public string? DocumentNumber { get; set; }
    public string? DocumentType { get; set; }
    public DateTime? Date { get; set; }
    public int ProductId { get; set; }
    public string? ProductSymbol { get; set; }
    public string? ProductName { get; set; }
    public string? WarehouseSymbol { get; set; }
    public string? WarehouseName { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "szt.";
    public string MovementType { get; set; } = "Out"; // In/Out
    public decimal? UnitPrice { get; set; }
    public decimal? TotalValue { get; set; }
}

/// <summary>
/// Inventory valuation DTO
/// </summary>
public class InventoryValuationDto
{
    public string? WarehouseSymbol { get; set; }
    public string ValuationMethod { get; set; } = "AVG";
    public DateTime ValuationDate { get; set; }
    public decimal TotalValue { get; set; }
    public int ProductCount { get; set; }
    public List<ValuationCategoryDto> CategoryBreakdown { get; set; } = new();
}

/// <summary>
/// Valuation category breakdown DTO
/// </summary>
public class ValuationCategoryDto
{
    public string Category { get; set; } = string.Empty;
    public decimal Value { get; set; }
}

/// <summary>
/// Stocktaking document DTO
/// </summary>
public class StocktakingDto
{
    public int Id { get; set; }
    public string? Number { get; set; }
    public string? WarehouseSymbol { get; set; }
    public string? WarehouseName { get; set; }
    public DateTime? Date { get; set; }
    public string Status { get; set; } = "Draft";
    public int ItemCount { get; set; }
    public string? Description { get; set; }
    public List<StocktakingItemDto>? Items { get; set; }
}

/// <summary>
/// Stocktaking item DTO
/// </summary>
public class StocktakingItemDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string? ProductSymbol { get; set; }
    public string? ProductName { get; set; }
    public string Unit { get; set; } = "szt.";
    public decimal BookQuantity { get; set; }      // Ilość ewidencyjna (systemowa)
    public decimal ActualQuantity { get; set; }    // Ilość rzeczywista (spisana)
    public decimal Difference { get; set; }        // Różnica
    public decimal? UnitPrice { get; set; }
    public string? Notes { get; set; }
}
