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
    public ActionResult<PagedResponse<InventoryItemDto>> GetStockLevels(
        [FromQuery] string? warehouseSymbol,
        [FromQuery] int? productId,
        [FromQuery] string? productSymbol,
        [FromQuery] bool? lowStock,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var asortymentyManager = _sferaService.GetManager("Asortymenty");
            var magazynyManager = _sferaService.GetManager("Magazyny");
            if (asortymentyManager == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Asortymenty manager"));
            }

            // Get all products with their stock levels
            var produktyQuery = new List<dynamic>();
            foreach (var a in asortymentyManager.Dane.Wszystkie())
            {
                bool isHandlowy = DynamicPropertyHelper.GetBool(a, "JestHandlowy");
                bool isMagazynowy = DynamicPropertyHelper.GetBool(a, "JestMagazynowy");
                if (!isHandlowy && !isMagazynowy)
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
                foreach (var m in magazynyManager.Dane.Wszystkie())
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
                var stany = DynamicPropertyHelper.GetCollection(produkt, "StanyMagazynowe");

                if (magazynFilterId.HasValue)
                {
                    var filteredStany = new List<dynamic>();
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
                        foreach (var m in magazynyManager.Dane.Wszystkie())
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
                        Unit = DynamicPropertyHelper.GetString(produkt, "JednostkaMagazynowa", "Symbol") ?? "szt.",
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
                    var allWarehouses = new List<dynamic>();
                    if (magazynFilter != null)
                    {
                        allWarehouses.Add(magazynFilter);
                    }
                    else if (magazynyManager != null)
                    {
                        foreach (var m in magazynyManager.Dane.Wszystkie())
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
                            Unit = DynamicPropertyHelper.GetString(produkt, "JednostkaMagazynowa", "Symbol") ?? "szt.",
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

            var response = new PagedResponse<InventoryItemDto>
            {
                Data = pagedItems,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            return Ok(response);
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
    public ActionResult<ApiResponse<List<InventoryItemDto>>> GetProductStock(int productId, [FromQuery] string? warehouseSymbol)
    {
        try
        {
            var asortymentyManager = _sferaService.GetManager("Asortymenty");
            var magazynyManager = _sferaService.GetManager("Magazyny");
            if (asortymentyManager == null)
            {
                return StatusCode(500, ApiResponse<List<InventoryItemDto>>.Error("Failed to get Asortymenty manager"));
            }

            dynamic? produkt = null;
            foreach (var a in asortymentyManager.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetId(a) == productId)
                {
                    produkt = a;
                    break;
                }
            }
            if (produkt == null)
            {
                return NotFound(ApiResponse<List<InventoryItemDto>>.Error($"Product with ID {productId} not found"));
            }

            var stany = DynamicPropertyHelper.GetCollection(produkt, "StanyMagazynowe");

            if (!string.IsNullOrEmpty(warehouseSymbol) && magazynyManager != null)
            {
                int? magazynFilterId = null;
                foreach (var m in magazynyManager.Dane.Wszystkie())
                {
                    if (DynamicPropertyHelper.GetString(m, "Symbol") == warehouseSymbol)
                    {
                        magazynFilterId = DynamicPropertyHelper.GetId(m);
                        break;
                    }
                }
                if (magazynFilterId.HasValue)
                {
                    var filteredStany = new List<dynamic>();
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
                    foreach (var m in magazynyManager.Dane.Wszystkie())
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
                    Unit = DynamicPropertyHelper.GetString(produkt, "JednostkaMagazynowa", "Symbol") ?? "szt.",
                    MinStockLevel = DynamicPropertyHelper.GetNullableDecimal(produkt, "StanMinimalny"),
                    MaxStockLevel = DynamicPropertyHelper.GetNullableDecimal(produkt, "StanMaksymalny")
                });
            }

            return Ok(ApiResponse<List<InventoryItemDto>>.Ok(items));
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
    public ActionResult<PagedResponse<InventoryItemDto>> GetLowStockItems(
        [FromQuery] string? warehouseSymbol,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        return GetStockLevels(warehouseSymbol, null, null, true, page, pageSize);
    }

    #endregion

    #region Warehouses

    /// <summary>
    /// Get all warehouses
    /// </summary>
    [HttpGet("warehouses")]
    public ActionResult<ApiResponse<List<WarehouseDto>>> GetWarehouses()
    {
        try
        {
            var magazynyManager = _sferaService.GetManager("Magazyny");
            if (magazynyManager == null)
            {
                return StatusCode(500, ApiResponse<List<WarehouseDto>>.Error("Failed to get Magazyny manager"));
            }

            var dtos = new List<WarehouseDto>();
            foreach (var m in magazynyManager.Dane.Wszystkie())
            {
                dtos.Add(new WarehouseDto
                {
                    Id = DynamicPropertyHelper.GetId(m),
                    Symbol = DynamicPropertyHelper.GetString(m, "Symbol"),
                    Name = DynamicPropertyHelper.GetString(m, "Nazwa"),
                    IsActive = DynamicPropertyHelper.GetBool(m, "Aktywny")
                });
            }

            return Ok(ApiResponse<List<WarehouseDto>>.Ok(dtos));
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
    public ActionResult<ApiResponse<WarehouseDto>> GetWarehouse(string symbol)
    {
        try
        {
            var magazynyManager = _sferaService.GetManager("Magazyny");
            if (magazynyManager == null)
            {
                return StatusCode(500, ApiResponse<WarehouseDto>.Error("Failed to get Magazyny manager"));
            }

            dynamic? magazyn = null;
            foreach (var m in magazynyManager.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetString(m, "Symbol") == symbol)
                {
                    magazyn = m;
                    break;
                }
            }

            if (magazyn == null)
            {
                return NotFound(ApiResponse<WarehouseDto>.Error($"Warehouse with symbol '{symbol}' not found"));
            }

            var dto = new WarehouseDto
            {
                Id = DynamicPropertyHelper.GetId(magazyn),
                Symbol = DynamicPropertyHelper.GetString(magazyn, "Symbol"),
                Name = DynamicPropertyHelper.GetString(magazyn, "Nazwa"),
                IsActive = DynamicPropertyHelper.GetBool(magazyn, "Aktywny")
            };

            return Ok(ApiResponse<WarehouseDto>.Ok(dto));
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
    public ActionResult<PagedResponse<BatchDto>> GetBatches(
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
            var asortymentyManager = _sferaService.GetManager("Asortymenty");
            if (asortymentyManager == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Asortymenty manager"));
            }

            // Get products first
            var produktyQuery = new List<dynamic>();
            foreach (var a in asortymentyManager.Dane.Wszystkie())
            {
                bool isHandlowy = DynamicPropertyHelper.GetBool(a, "JestHandlowy");
                bool isMagazynowy = DynamicPropertyHelper.GetBool(a, "JestMagazynowy");
                if (!isHandlowy && !isMagazynowy)
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
                        Unit = DynamicPropertyHelper.GetString(produkt, "JednostkaMagazynowa", "Symbol") ?? "szt.",
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

            var response = new PagedResponse<BatchDto>
            {
                Data = pagedBatches,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            return Ok(response);
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
    public ActionResult<PagedResponse<BatchDto>> GetExpiringBatches(
        [FromQuery] string? warehouseSymbol = null,
        [FromQuery] int days = 30,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        return GetBatches(null, null, warehouseSymbol, null, true, days, page, pageSize);
    }

    private List<dynamic> GetProductBatches(dynamic produkt, string? warehouseSymbol)
    {
        var partie = new List<dynamic>();

        try
        {
            var produktId = DynamicPropertyHelper.GetId(produkt);
            var przyjeciaManager = _sferaService.GetManager("PrzyjeciaZewnetrzne");
            if (przyjeciaManager == null)
                return partie;

            // Get batches from przyjecia (warehouse receipts)
            var przyjecia = new List<dynamic>();
            foreach (var p in przyjeciaManager.Dane.Wszystkie())
            {
                var pozycje = DynamicPropertyHelper.GetCollection(p, "Pozycje");
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
                var allPozycje = DynamicPropertyHelper.GetCollection(przyjecie, "Pozycje");
                foreach (var poz in allPozycje)
                {
                    var asortyment = DynamicPropertyHelper.GetProperty(poz, "Asortyment");
                    if (asortyment != null && DynamicPropertyHelper.GetId(asortyment) == produktId)
                    {
                        var partieCollection = DynamicPropertyHelper.GetCollection(poz, "Partie");
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
    public ActionResult<PagedResponse<ReservationDto>> GetReservations(
        [FromQuery] int? productId,
        [FromQuery] int? customerId,
        [FromQuery] string? warehouseSymbol,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var zamowieniaManager = _sferaService.GetManager("ZamowieniaOdKlientow");
            if (zamowieniaManager == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get ZamowieniaOdKlientow manager"));
            }

            // Document status constants
            const int StatusAnulowany = 4;

            // Get reservations from customer orders (ZK)
            var zamowienia = new List<dynamic>();
            foreach (var z in zamowieniaManager.Dane.Wszystkie())
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
                var pozycje = DynamicPropertyHelper.GetCollection(zamowienie, "Pozycje");

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
                        Unit = jednostka != null ? DynamicPropertyHelper.GetString(jednostka, "Symbol") ?? "szt." : "szt.",
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

            var response = new PagedResponse<ReservationDto>
            {
                Data = pagedReservations,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            return Ok(response);
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
    public ActionResult<PagedResponse<ReservationDto>> GetProductReservations(
        int productId,
        [FromQuery] string? warehouseSymbol,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        return GetReservations(productId, null, warehouseSymbol, page, pageSize);
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
    public ActionResult<ApiResponse<InventorySummaryDto>> GetInventorySummary([FromQuery] string? warehouseSymbol)
    {
        try
        {
            var asortymentyManager = _sferaService.GetManager("Asortymenty");
            var magazynyManager = _sferaService.GetManager("Magazyny");
            if (asortymentyManager == null)
            {
                return StatusCode(500, ApiResponse<InventorySummaryDto>.Error("Failed to get Asortymenty manager"));
            }

            int? magazynFilterId = null;
            if (!string.IsNullOrEmpty(warehouseSymbol) && magazynyManager != null)
            {
                foreach (var m in magazynyManager.Dane.Wszystkie())
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

            foreach (var produkt in asortymentyManager.Dane.Wszystkie())
            {
                bool isHandlowy = DynamicPropertyHelper.GetBool(produkt, "JestHandlowy");
                bool isMagazynowy = DynamicPropertyHelper.GetBool(produkt, "JestMagazynowy");
                if (!isHandlowy && !isMagazynowy)
                    continue;

                var stany = DynamicPropertyHelper.GetCollection(produkt, "StanyMagazynowe");

                if (magazynFilterId.HasValue)
                {
                    var filteredStany = new List<dynamic>();
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

            return Ok(ApiResponse<InventorySummaryDto>.Ok(summary));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting inventory summary");
            return StatusCode(500, ApiResponse<InventorySummaryDto>.Error("Error retrieving inventory summary", new List<string> { ex.Message }));
        }
    }

    #endregion
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
