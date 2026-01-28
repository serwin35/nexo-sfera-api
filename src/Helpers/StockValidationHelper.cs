using NexoSferaApi.Services;

namespace NexoSferaApi.Helpers;

/// <summary>
/// Helper class for product lookup and stock validation.
/// Used for outgoing documents (WZ, RW, MM, FS, Paragon) to ensure
/// sufficient stock before document creation.
/// </summary>
public class StockValidationHelper
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<StockValidationHelper> _logger;

    public StockValidationHelper(ISferaService sferaService, ILogger<StockValidationHelper> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Result of product lookup
    /// </summary>
    public class ProductLookupResult
    {
        public bool Found { get; set; }
        public dynamic? Product { get; set; }
        public int ProductId { get; set; }
        public string? ProductSymbol { get; set; }
        public string? ProductName { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Result of stock validation for a single item
    /// </summary>
    public class StockValidationItemResult
    {
        public bool HasSufficientStock { get; set; }
        public int ProductId { get; set; }
        public string? ProductSymbol { get; set; }
        public string? ProductName { get; set; }
        public decimal RequestedQuantity { get; set; }
        public decimal AvailableQuantity { get; set; }
        public decimal Shortage => RequestedQuantity - AvailableQuantity;
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Result of stock validation for all items
    /// </summary>
    public class StockValidationResult
    {
        public bool AllItemsAvailable { get; set; }
        public List<StockValidationItemResult> Items { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    /// <summary>
    /// Finds a product by ID, Symbol, or EAN code.
    /// Priority: ID > Symbol > EAN
    /// </summary>
    public ProductLookupResult FindProduct(int? productId, string? productSymbol, string? productEan)
    {
        var result = new ProductLookupResult();

        if (!productId.HasValue && string.IsNullOrEmpty(productSymbol) && string.IsNullOrEmpty(productEan))
        {
            result.ErrorMessage = "At least one of ProductId, ProductSymbol, or ProductEan must be provided";
            return result;
        }

        var asortymentyManager = _sferaService.GetManager("Asortymenty");
        if (asortymentyManager == null)
        {
            result.ErrorMessage = "Failed to get Asortymenty manager";
            return result;
        }

        foreach (var a in asortymentyManager.Dane.Wszystkie())
        {
            bool match = false;

            // Try to match by ID first (most precise)
            if (productId.HasValue)
            {
                if (DynamicPropertyHelper.GetId(a) == productId.Value)
                    match = true;
            }
            // Then by symbol
            else if (!string.IsNullOrEmpty(productSymbol))
            {
                var symbol = DynamicPropertyHelper.GetString(a, "Symbol");
                if (symbol == productSymbol)
                    match = true;
            }
            // Finally by EAN
            else if (!string.IsNullOrEmpty(productEan))
            {
                var ean = DynamicPropertyHelper.GetString(a, "KodKreskowy")
                       ?? DynamicPropertyHelper.GetString(a, "KodEan");
                if (ean == productEan)
                    match = true;
            }

            if (match)
            {
                result.Found = true;
                result.Product = a;
                result.ProductId = DynamicPropertyHelper.GetId(a);
                result.ProductSymbol = DynamicPropertyHelper.GetString(a, "Symbol");
                result.ProductName = DynamicPropertyHelper.GetString(a, "Nazwa");
                return result;
            }
        }

        // Build error message based on what was searched
        if (productId.HasValue)
            result.ErrorMessage = $"Product with ID {productId} not found";
        else if (!string.IsNullOrEmpty(productSymbol))
            result.ErrorMessage = $"Product with symbol '{productSymbol}' not found";
        else if (!string.IsNullOrEmpty(productEan))
            result.ErrorMessage = $"Product with EAN '{productEan}' not found";

        return result;
    }

    /// <summary>
    /// Checks if a product is a service (Rodzaj = "Usluga").
    /// Services don't require stock validation.
    /// </summary>
    public bool IsService(dynamic product)
    {
        if (product == null) return false;
        
        try
        {
            var rodzaj = DynamicPropertyHelper.GetString(product, "Rodzaj");
            return rodzaj != null && rodzaj.Equals("Usluga", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to check if product is a service");
            return false;
        }
    }

    /// <summary>
    /// Gets available stock quantity for a product in a specific warehouse.
    /// Returns IloscDostepna (available quantity, excluding reservations).
    /// </summary>
    public decimal GetAvailableStock(dynamic product, string? warehouseSymbol)
    {
        if (product == null) return 0;

        int? warehouseId = null;
        if (!string.IsNullOrEmpty(warehouseSymbol))
        {
            var magazynyManager = _sferaService.GetManager("Magazyny");
            if (magazynyManager != null)
            {
                foreach (var m in magazynyManager.Dane.Wszystkie())
                {
                    if (DynamicPropertyHelper.GetString(m, "Symbol") == warehouseSymbol)
                    {
                        warehouseId = DynamicPropertyHelper.GetId(m);
                        break;
                    }
                }
            }
        }

        var stany = DynamicPropertyHelper.GetCollection(product, "StanyMagazynowe");
        decimal totalAvailable = 0;

        foreach (var stan in stany)
        {
            // Filter by warehouse if specified
            if (warehouseId.HasValue)
            {
                var magazynId = DynamicPropertyHelper.GetNullableInt(stan, "Magazyn_Id");
                if (magazynId != warehouseId.Value)
                    continue;
            }

            // IloscDostepna = actual available quantity (excluding reservations)
            totalAvailable += DynamicPropertyHelper.GetDecimal(stan, "IloscDostepna");
        }

        return totalAvailable;
    }

    /// <summary>
    /// Gets total stock quantity for a product in a specific warehouse.
    /// Returns total stock (including reservations).
    /// </summary>
    public decimal GetTotalStock(dynamic product, string? warehouseSymbol)
    {
        if (product == null) return 0;

        int? warehouseId = null;
        if (!string.IsNullOrEmpty(warehouseSymbol))
        {
            var magazynyManager = _sferaService.GetManager("Magazyny");
            if (magazynyManager != null)
            {
                foreach (var m in magazynyManager.Dane.Wszystkie())
                {
                    if (DynamicPropertyHelper.GetString(m, "Symbol") == warehouseSymbol)
                    {
                        warehouseId = DynamicPropertyHelper.GetId(m);
                        break;
                    }
                }
            }
        }

        var stany = DynamicPropertyHelper.GetCollection(product, "StanyMagazynowe");
        decimal total = 0;

        foreach (var stan in stany)
        {
            if (warehouseId.HasValue)
            {
                var magazynId = DynamicPropertyHelper.GetNullableInt(stan, "Magazyn_Id");
                if (magazynId != warehouseId.Value)
                    continue;
            }

            var iloscDostepna = DynamicPropertyHelper.GetDecimal(stan, "IloscDostepna");
            var iloscZarezerwowana = DynamicPropertyHelper.GetDecimal(stan, "IloscZarezerwowanaIlosciowo");
            var iloscZadysponowana = DynamicPropertyHelper.GetDecimal(stan, "IloscZadysponowana");
            total += iloscDostepna + iloscZarezerwowana + iloscZadysponowana;
        }

        return total;
    }

    /// <summary>
    /// Validates stock availability for a list of items.
    /// Used before creating outgoing documents (WZ, RW, MM, FS, Paragon).
    /// </summary>
    /// <typeparam name="T">Item type with ProductId, ProductSymbol, ProductEan, Quantity</typeparam>
    public StockValidationResult ValidateStock<T>(
        IEnumerable<T> items,
        string warehouseSymbol,
        Func<T, int?> getProductId,
        Func<T, string?> getProductSymbol,
        Func<T, string?> getProductEan,
        Func<T, decimal> getQuantity)
    {
        var result = new StockValidationResult { AllItemsAvailable = true };

        // Group items by product to aggregate quantities
        var itemsByProduct = new Dictionary<int, (decimal Quantity, string? Symbol, string? Name)>();

        foreach (var item in items)
        {
            var lookup = FindProduct(getProductId(item), getProductSymbol(item), getProductEan(item));

            if (!lookup.Found)
            {
                result.AllItemsAvailable = false;
                result.Errors.Add(lookup.ErrorMessage ?? "Product not found");
                result.Items.Add(new StockValidationItemResult
                {
                    HasSufficientStock = false,
                    ProductSymbol = getProductSymbol(item),
                    RequestedQuantity = getQuantity(item),
                    ErrorMessage = lookup.ErrorMessage
                });
                continue;
            }

            // Aggregate quantities for the same product
            if (itemsByProduct.ContainsKey(lookup.ProductId))
            {
                var existing = itemsByProduct[lookup.ProductId];
                itemsByProduct[lookup.ProductId] = (existing.Quantity + getQuantity(item), existing.Symbol, existing.Name);
            }
            else
            {
                itemsByProduct[lookup.ProductId] = (getQuantity(item), lookup.ProductSymbol, lookup.ProductName);
            }
        }

        // Now validate stock for aggregated quantities
        foreach (var kvp in itemsByProduct)
        {
            var productLookup = FindProduct(kvp.Key, null, null);
            if (!productLookup.Found)
            {
                // Shouldn't happen, but handle it
                continue;
            }

            // Skip stock validation for services
            if (IsService(productLookup.Product))
            {
                _logger.LogDebug("Skipping stock validation for service product '{Symbol}' (ID: {Id})", kvp.Value.Symbol, kvp.Key);
                var serviceResult = new StockValidationItemResult
                {
                    HasSufficientStock = true, // Services always have "stock"
                    ProductId = kvp.Key,
                    ProductSymbol = kvp.Value.Symbol,
                    ProductName = kvp.Value.Name,
                    RequestedQuantity = kvp.Value.Quantity,
                    AvailableQuantity = 0 // Not applicable for services
                };
                result.Items.Add(serviceResult);
                continue;
            }

            var available = GetAvailableStock(productLookup.Product!, warehouseSymbol);
            var requested = kvp.Value.Quantity;
            var hasStock = available >= requested;

            var itemResult = new StockValidationItemResult
            {
                HasSufficientStock = hasStock,
                ProductId = kvp.Key,
                ProductSymbol = kvp.Value.Symbol,
                ProductName = kvp.Value.Name,
                RequestedQuantity = requested,
                AvailableQuantity = available
            };

            if (!hasStock)
            {
                result.AllItemsAvailable = false;
                itemResult.ErrorMessage = $"Insufficient stock for '{kvp.Value.Symbol}': requested {requested}, available {available} (shortage: {itemResult.Shortage})";
                result.Errors.Add(itemResult.ErrorMessage);
            }

            result.Items.Add(itemResult);
        }

        return result;
    }

    /// <summary>
    /// Simple stock check for a single product.
    /// </summary>
    public bool HasSufficientStock(int productId, string warehouseSymbol, decimal requiredQuantity)
    {
        var lookup = FindProduct(productId, null, null);
        if (!lookup.Found) return false;

        // Services always have sufficient "stock"
        if (IsService(lookup.Product))
        {
            _logger.LogDebug("Skipping stock check for service product ID {Id} - services always pass validation", productId);
            return true;
        }

        var available = GetAvailableStock(lookup.Product!, warehouseSymbol);
        return available >= requiredQuantity;
    }

    /// <summary>
    /// Simple stock check for a single product by symbol.
    /// </summary>
    public bool HasSufficientStockBySymbol(string productSymbol, string warehouseSymbol, decimal requiredQuantity)
    {
        var lookup = FindProduct(null, productSymbol, null);
        if (!lookup.Found) return false;

        // Services always have sufficient "stock"
        if (IsService(lookup.Product))
        {
            _logger.LogDebug("Skipping stock check for service product '{Symbol}' - services always pass validation", productSymbol);
            return true;
        }

        var available = GetAvailableStock(lookup.Product!, warehouseSymbol);
        return available >= requiredQuantity;
    }
}
