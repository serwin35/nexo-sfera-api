using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Requests;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using InsERT.Moria.Asortymenty;
using InsERT.Moria.ModelOrganizacyjny;

namespace NexoSferaApi.Controllers;

[ApiController]
[Route("api/inventory")]
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
            var sfera = _sferaService.GetSfera();
            var asortymenty = sfera.Asortymenty();

            // Get all products with their stock levels
            var produktyQuery = asortymenty.Dane.Wszystkie()
                .Where(a => a.JestHandlowy == true || a.JestMagazynowy == true);

            if (productId.HasValue)
            {
                produktyQuery = produktyQuery.Where(a => a.Id == productId.Value);
            }

            if (!string.IsNullOrEmpty(productSymbol))
            {
                produktyQuery = produktyQuery.Where(a => a.Symbol == productSymbol || a.Symbol!.Contains(productSymbol));
            }

            var produkty = produktyQuery.ToList();

            // Get warehouse filter
            Magazyn? magazynFilter = null;
            if (!string.IsNullOrEmpty(warehouseSymbol))
            {
                magazynFilter = sfera.Magazyny().Dane.Wszystkie()
                    .FirstOrDefault(m => m.Symbol == warehouseSymbol);
            }

            var inventoryItems = new List<InventoryItemDto>();

            foreach (var produkt in produkty)
            {
                // Get stock levels for this product
                var stany = produkt.StanyMagazynowe ?? new List<StanMagazynowy>();

                if (magazynFilter != null)
                {
                    stany = stany.Where(s => s.Magazyn_Id == magazynFilter.Id).ToList();
                }

                foreach (var stan in stany)
                {
                    var magazyn = sfera.Magazyny().Dane.Wszystkie()
                        .FirstOrDefault(m => m.Id == stan.Magazyn_Id);

                    var item = new InventoryItemDto
                    {
                        ProductId = produkt.Id,
                        ProductSymbol = produkt.Symbol,
                        ProductName = produkt.Nazwa,
                        ProductEan = produkt.KodEan,
                        WarehouseSymbol = magazyn?.Symbol,
                        WarehouseName = magazyn?.Nazwa,
                        StockQuantity = stan.IloscDostepna + stan.IloscZarezerwowanaIlosciowo + stan.IloscZadysponowana,
                        ReservedQuantity = stan.IloscZarezerwowanaIlosciowo + stan.IloscZadysponowana,
                        AvailableQuantity = stan.IloscDostepna,
                        Unit = produkt.JednostkaMagazynowa?.Symbol ?? "szt.",
                        MinStockLevel = produkt.StanMinimalny,
                        MaxStockLevel = produkt.StanMaksymalny
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
                if (!stany.Any() && (productId.HasValue || !string.IsNullOrEmpty(productSymbol)))
                {
                    var allWarehouses = magazynFilter != null
                        ? new List<Magazyn> { magazynFilter }
                        : sfera.Magazyny().Dane.Wszystkie().ToList();

                    foreach (var magazyn in allWarehouses)
                    {
                        inventoryItems.Add(new InventoryItemDto
                        {
                            ProductId = produkt.Id,
                            ProductSymbol = produkt.Symbol,
                            ProductName = produkt.Nazwa,
                            ProductEan = produkt.KodEan,
                            WarehouseSymbol = magazyn?.Symbol,
                            WarehouseName = magazyn?.Nazwa,
                            StockQuantity = 0,
                            ReservedQuantity = 0,
                            AvailableQuantity = 0,
                            Unit = produkt.JednostkaMagazynowa?.Symbol ?? "szt.",
                            MinStockLevel = produkt.StanMinimalny,
                            MaxStockLevel = produkt.StanMaksymalny
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
            var sfera = _sferaService.GetSfera();
            var asortymenty = sfera.Asortymenty();

            var produkt = asortymenty.Dane.Wszystkie().FirstOrDefault(a => a.Id == productId);
            if (produkt == null)
            {
                return NotFound(ApiResponse<List<InventoryItemDto>>.Error($"Product with ID {productId} not found"));
            }

            var stany = produkt.StanyMagazynowe ?? new List<StanMagazynowy>();

            if (!string.IsNullOrEmpty(warehouseSymbol))
            {
                var magazynFilter = sfera.Magazyny().Dane.Wszystkie()
                    .FirstOrDefault(m => m.Symbol == warehouseSymbol);
                if (magazynFilter != null)
                {
                    stany = stany.Where(s => s.Magazyn_Id == magazynFilter.Id).ToList();
                }
            }

            var items = stany.Select(stan =>
            {
                var magazyn = sfera.Magazyny().Dane.Wszystkie()
                    .FirstOrDefault(m => m.Id == stan.Magazyn_Id);

                return new InventoryItemDto
                {
                    ProductId = produkt.Id,
                    ProductSymbol = produkt.Symbol,
                    ProductName = produkt.Nazwa,
                    ProductEan = produkt.KodEan,
                    WarehouseSymbol = magazyn?.Symbol,
                    WarehouseName = magazyn?.Nazwa,
                    StockQuantity = stan.IloscDostepna + stan.IloscZarezerwowanaIlosciowo + stan.IloscZadysponowana,
                    ReservedQuantity = stan.IloscZarezerwowanaIlosciowo + stan.IloscZadysponowana,
                    AvailableQuantity = stan.IloscDostepna,
                    Unit = produkt.JednostkaMagazynowa?.Symbol ?? "szt.",
                    MinStockLevel = produkt.StanMinimalny,
                    MaxStockLevel = produkt.StanMaksymalny
                };
            }).ToList();

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
            var sfera = _sferaService.GetSfera();
            var magazyny = sfera.Magazyny().Dane.Wszystkie().ToList();

            var dtos = magazyny.Select(m => new WarehouseDto
            {
                Id = m.Id,
                Symbol = m.Symbol,
                Name = m.Nazwa,
                IsActive = m.Aktywny
            }).ToList();

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
            var sfera = _sferaService.GetSfera();
            var magazyn = sfera.Magazyny().Dane.Wszystkie()
                .FirstOrDefault(m => m.Symbol == symbol);

            if (magazyn == null)
            {
                return NotFound(ApiResponse<WarehouseDto>.Error($"Warehouse with symbol '{symbol}' not found"));
            }

            var dto = new WarehouseDto
            {
                Id = magazyn.Id,
                Symbol = magazyn.Symbol,
                Name = magazyn.Nazwa,
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
            var sfera = _sferaService.GetSfera();
            var asortymenty = sfera.Asortymenty();

            // Get products first
            var produktyQuery = asortymenty.Dane.Wszystkie()
                .Where(a => a.JestHandlowy == true || a.JestMagazynowy == true);

            if (productId.HasValue)
            {
                produktyQuery = produktyQuery.Where(a => a.Id == productId.Value);
            }

            if (!string.IsNullOrEmpty(productSymbol))
            {
                produktyQuery = produktyQuery.Where(a => a.Symbol == productSymbol);
            }

            var produkty = produktyQuery.ToList();
            var batches = new List<BatchDto>();
            var today = DateTime.Today;
            var expirationThreshold = today.AddDays(expiringDays);

            // For each product, get its batches from acceptances (przyjecia)
            foreach (var produkt in produkty)
            {
                // Get product batches through stock movements
                var partie = GetProductBatches(sfera, produkt, warehouseSymbol);

                foreach (var partia in partie)
                {
                    // Filter by batch number
                    if (!string.IsNullOrEmpty(batchNumber) && partia.Numer != batchNumber)
                    {
                        continue;
                    }

                    // Calculate days until expiration
                    int? daysUntilExpiration = null;
                    if (partia.Termin.HasValue)
                    {
                        daysUntilExpiration = (int)(partia.Termin.Value - today).TotalDays;

                        // Filter for expiring soon
                        if (expiringSoon.HasValue && expiringSoon.Value)
                        {
                            if (partia.Termin.Value > expirationThreshold)
                            {
                                continue;
                            }
                        }
                    }

                    var magazyn = partia.Przyjecie?.Magazyn;

                    batches.Add(new BatchDto
                    {
                        Id = partia.Id,
                        BatchNumber = partia.Numer,
                        ProductId = produkt.Id,
                        ProductSymbol = produkt.Symbol,
                        ProductName = produkt.Nazwa,
                        WarehouseSymbol = magazyn?.Symbol,
                        Quantity = partia.Ilosc,
                        Unit = produkt.JednostkaMagazynowa?.Symbol ?? "szt.",
                        ExpirationDate = partia.Termin,
                        DaysUntilExpiration = daysUntilExpiration,
                        Notes = partia.Komentarz
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
        [FromQuery] int days = 30,
        [FromQuery] string? warehouseSymbol,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        return GetBatches(null, null, warehouseSymbol, null, true, days, page, pageSize);
    }

    private List<Partia> GetProductBatches(Uchwyt sfera, Asortyment produkt, string? warehouseSymbol)
    {
        var partie = new List<Partia>();

        try
        {
            // Get batches from przyjecia (warehouse receipts)
            var przyjecia = sfera.PrzyjeciaZewnetrzne().Dane.Wszystkie()
                .Where(p => p.Pozycje.Any(poz => poz.Asortyment?.Id == produkt.Id));

            if (!string.IsNullOrEmpty(warehouseSymbol))
            {
                przyjecia = przyjecia.Where(p => p.Magazyn?.Symbol == warehouseSymbol);
            }

            foreach (var przyjecie in przyjecia)
            {
                var pozycje = przyjecie.Pozycje.Where(poz => poz.Asortyment?.Id == produkt.Id);
                foreach (var pozycja in pozycje)
                {
                    if (pozycja.Partie != null)
                    {
                        partie.AddRange(pozycja.Partie);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting batches for product {ProductId}", produkt.Id);
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
            var sfera = _sferaService.GetSfera();

            // Get reservations from customer orders (ZK)
            var zamowienia = sfera.ZamowieniaOdKlientow().Dane.Wszystkie()
                .Where(z => z.Status != (int)StatusDokumentu.Anulowany);

            if (customerId.HasValue)
            {
                zamowienia = zamowienia.Where(z => z.Podmiot != null && z.Podmiot.Id == customerId.Value);
            }

            if (!string.IsNullOrEmpty(warehouseSymbol))
            {
                zamowienia = zamowienia.Where(z => z.Magazyn?.Symbol == warehouseSymbol);
            }

            var reservations = new List<ReservationDto>();

            foreach (var zamowienie in zamowienia.ToList())
            {
                foreach (var pozycja in zamowienie.Pozycje)
                {
                    if (productId.HasValue && pozycja.Asortyment?.Id != productId.Value)
                    {
                        continue;
                    }

                    // Check if position has reserved quantity
                    var rezerwowana = pozycja.IloscZarezerwowana;
                    if (rezerwowana <= 0)
                    {
                        continue;
                    }

                    reservations.Add(new ReservationDto
                    {
                        Id = pozycja.Id,
                        ProductId = pozycja.Asortyment?.Id ?? 0,
                        ProductSymbol = pozycja.Asortyment?.Symbol,
                        ProductName = pozycja.Nazwa,
                        WarehouseSymbol = zamowienie.Magazyn?.Symbol,
                        ReservedQuantity = rezerwowana,
                        Unit = pozycja.Jednostka?.Symbol ?? "szt.",
                        SourceDocumentId = zamowienie.Id,
                        SourceDocumentNumber = zamowienie.NumerWewnetrzny?.PelnaSygnatura,
                        CustomerId = zamowienie.Podmiot?.Id,
                        CustomerName = zamowienie.Podmiot?.NazwaSkrocona,
                        ReservationDate = zamowienie.DataWystawienia,
                        Status = GetReservationStatus(zamowienie.Status)
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
        return status switch
        {
            (int)StatusDokumentu.Bufor => "Pending",
            (int)StatusDokumentu.Zatwierdzony => "Confirmed",
            (int)StatusDokumentu.CzesciowoZrealizowany => "PartiallyFulfilled",
            (int)StatusDokumentu.Zrealizowany => "Fulfilled",
            (int)StatusDokumentu.Anulowany => "Cancelled",
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
            var sfera = _sferaService.GetSfera();
            var asortymenty = sfera.Asortymenty();

            var produkty = asortymenty.Dane.Wszystkie()
                .Where(a => a.JestHandlowy == true || a.JestMagazynowy == true)
                .ToList();

            Magazyn? magazynFilter = null;
            if (!string.IsNullOrEmpty(warehouseSymbol))
            {
                magazynFilter = sfera.Magazyny().Dane.Wszystkie()
                    .FirstOrDefault(m => m.Symbol == warehouseSymbol);
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

            foreach (var produkt in produkty)
            {
                var stany = produkt.StanyMagazynowe ?? new List<StanMagazynowy>();

                if (magazynFilter != null)
                {
                    stany = stany.Where(s => s.Magazyn_Id == magazynFilter.Id).ToList();
                }

                if (!stany.Any())
                {
                    summary.ProductsOutOfStock++;
                    continue;
                }

                var totalStock = stany.Sum(s => s.IloscDostepna + s.IloscZarezerwowanaIlosciowo + s.IloscZadysponowana);
                var totalReserved = stany.Sum(s => s.IloscZarezerwowanaIlosciowo + s.IloscZadysponowana);
                var totalAvailable = stany.Sum(s => s.IloscDostepna);

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

                if (produkt.StanMinimalny.HasValue && totalAvailable < produkt.StanMinimalny.Value)
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
    public bool Aktywny { get; set; }
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
/// Document status enum
/// </summary>
public enum StatusDokumentu
{
    Bufor = 0,
    Zatwierdzony = 1,
    CzesciowoZrealizowany = 2,
    Zrealizowany = 3,
    Anulowany = 4
}
