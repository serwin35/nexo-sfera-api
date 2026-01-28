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

    /// <summary>
    /// Common service name patterns for heuristic detection.
    /// Used as a fallback when Rodzaj_Id is not available or set incorrectly.
    /// These patterns are matched at the start of product names/symbols to minimize false positives.
    /// </summary>
    private static readonly string[] ServiceNamePatterns = new[]
    {
        "usługa", "usługi", "service",
        "koszt przesyłki", "shipping cost", "dostawa", "delivery",
        "obsługa", "obsługi", "handling",
        "montaż", "instalacja", "installation",
        "transport", "przewóz",
        "opłata", "fee", "charge"
    };

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
    /// Checks if a product is a service type.
    /// Services don't require stock validation as they are not physical items.
    /// In Nexo Sfera SDK, services are identified by:
    /// - Rodzaj.StanyMagazynowe = false (no stock tracking = service)
    /// - Rodzaj.StanyMagazynowe = true (has stock tracking = physical product)
    /// Alternative markers: GrupaTowaru = "US", TypTowaru = 2
    /// </summary>
    /// <param name="product">Product object to check</param>
    /// <param name="logger">Optional logger for debugging service detection</param>
    /// <returns>True if the product is a service, false otherwise</returns>
    public static bool IsService(dynamic product, ILogger? logger = null)
    {
        if (product == null)
            return false;

        try
        {
            var productName = DynamicPropertyHelper.GetString(product, "Nazwa") ?? "(unknown)";
            var productSymbol = DynamicPropertyHelper.GetString(product, "Symbol") ?? "(unknown)";
            var productId = DynamicPropertyHelper.GetId(product);

            // 1. Most reliable: Rodzaj.StanyMagazynowe
            // In Nexo Sfera SDK, services are identified by NOT having stock tracking:
            // - StanyMagazynowe = false means Service (no stock tracking)
            // - StanyMagazynowe = true means Product with stock (Towar/Komplet/Opakowanie)
            var rodzaj = DynamicPropertyHelper.GetProperty(product, "Rodzaj");
            if (rodzaj != null)
            {
                var stanyMagazynowe = DynamicPropertyHelper.GetNullableBool(rodzaj, "StanyMagazynowe");
                if (stanyMagazynowe.HasValue)
                {
                    bool isService = !stanyMagazynowe.Value;
                    if (logger != null)
                    {
                        if (isService)
                        {
                            logger.LogDebug("Product '{Symbol}' (ID: {Id}) detected as service via Rodzaj.StanyMagazynowe=false", 
                                (object)productSymbol, (object)productId);
                        }
                        else
                        {
                            logger.LogDebug("Product '{Symbol}' (ID: {Id}) is a physical product (Rodzaj.StanyMagazynowe=true)", 
                                (object)productSymbol, (object)productId);
                        }
                    }
                    return isService;
                }
            }

            // 2. GrupaTowaru - alternative check for service type
            // Some systems use "US" to mark services
            var grupaTowaru = DynamicPropertyHelper.GetString(product, "GrupaTowaru");
            if (!string.IsNullOrEmpty(grupaTowaru) && grupaTowaru.Equals("US", StringComparison.OrdinalIgnoreCase))
            {
                if (logger != null)
                {
                    logger.LogDebug("Product '{Symbol}' (ID: {Id}) detected as service via GrupaTowaru='{GrupaTowaru}'", 
                        (object)productSymbol, (object)productId, (object)grupaTowaru);
                }
                return true;
            }

            // 3. TypTowaru - another alternative marker
            // TypTowaru == 2 indicates a service in some configurations
            var typTowaru = DynamicPropertyHelper.GetNullableInt(product, "TypTowaru");
            if (typTowaru.HasValue && typTowaru.Value == 2)
            {
                if (logger != null)
                {
                    logger.LogDebug("Product '{Symbol}' (ID: {Id}) detected as service via TypTowaru={TypTowaru}", 
                        (object)productSymbol, (object)productId, (object)typTowaru.Value);
                }
                return true;
            }

            // 4. Heuristic based on name/symbol - fallback when metadata is missing
            // This is used as a safety net for cases where Rodzaj is not available
            foreach (var pattern in ServiceNamePatterns)
            {
                if (productName.StartsWith(pattern, StringComparison.OrdinalIgnoreCase) ||
                    productSymbol.StartsWith(pattern, StringComparison.OrdinalIgnoreCase) ||
                    (pattern.Contains(' ') && (productName.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
                                               productSymbol.Contains(pattern, StringComparison.OrdinalIgnoreCase))))
                {
                    if (logger != null)
                    {
                        logger.LogDebug("Product '{Symbol}' (ID: {Id}, Name: '{Name}') detected as service via name/symbol pattern '{Pattern}'", 
                            (object)productSymbol, (object)productId, (object)productName, (object)pattern);
                    }
                    return true;
                }
            }
            
            // Not a service - log for debugging
            if (logger != null)
            {
                var rodzajInfo = rodzaj != null ? "present but StanyMagazynowe unavailable" : "(null)";
                var typTowaruStr = typTowaru.HasValue ? typTowaru.Value.ToString() : "(null)";
                logger.LogDebug("Product '{Symbol}' (ID: {Id}) is NOT a service (Rodzaj={Rodzaj}, GrupaTowaru={GrupaTowaru}, TypTowaru={TypTowaru})", 
                    (object)productSymbol, (object)productId, (object)rodzajInfo, (object)(grupaTowaru ?? "(null)"), (object)typTowaruStr);
            }
        }
        catch (Exception ex)
        {
            // Ignore exceptions, treat as non-service
            // It's safer to require stock validation for unknown types
            if (logger != null)
            {
                logger.LogWarning(ex, "Exception while checking if product is a service - treating as non-service");
            }
        }
        return false;
    }

    /// <summary>
    /// Validates stock availability for a list of items.
    /// Used before creating outgoing documents (WZ, RW, MM, FS, Paragon).
    /// Services (Rodzaj_Id = 1) are automatically skipped as they don't require stock.
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
        var itemsByProduct = new Dictionary<int, (decimal Quantity, string? Symbol, string? Name, dynamic? Product)>();

        foreach (var item in items)
        {
            var productIdParam = getProductId(item);
            var productSymbolParam = getProductSymbol(item);
            var productEanParam = getProductEan(item);
            var quantityParam = getQuantity(item);
            
            _logger.LogDebug("Validating stock for item: ProductId={ProductId}, Symbol={Symbol}, EAN={EAN}, Qty={Qty}",
                productIdParam, productSymbolParam ?? "(null)", productEanParam ?? "(null)", quantityParam);
            
            var lookup = FindProduct(productIdParam, productSymbolParam, productEanParam);

            if (!lookup.Found)
            {
                _logger.LogWarning("Product not found during stock validation: {ErrorMessage}", lookup.ErrorMessage);
                result.AllItemsAvailable = false;
                result.Errors.Add(lookup.ErrorMessage ?? "Product not found");
                result.Items.Add(new StockValidationItemResult
                {
                    HasSufficientStock = false,
                    ProductSymbol = productSymbolParam,
                    RequestedQuantity = quantityParam,
                    ErrorMessage = lookup.ErrorMessage
                });
                continue;
            }

            _logger.LogDebug("Product found: ID={ProductId}, Symbol={Symbol}, Name={Name}",
                lookup.ProductId, lookup.ProductSymbol ?? "(null)", lookup.ProductName ?? "(null)");

            // Aggregate quantities for the same product
            if (itemsByProduct.ContainsKey(lookup.ProductId))
            {
                var existing = itemsByProduct[lookup.ProductId];
                itemsByProduct[lookup.ProductId] = (existing.Quantity + quantityParam, existing.Symbol, existing.Name, existing.Product);
            }
            else
            {
                itemsByProduct[lookup.ProductId] = (quantityParam, lookup.ProductSymbol, lookup.ProductName, lookup.Product);
            }
        }

        // Now validate stock for aggregated quantities
        foreach (var kvp in itemsByProduct)
        {
            var product = kvp.Value.Product;
            if (product == null)
            {
                // Shouldn't happen, but handle it
                continue;
            }

            // Skip stock validation for services
            if (IsService(product, _logger))
            {
                _logger.LogInformation("Skipping stock validation for service product '{Symbol}' (ID: {Id}, Name: '{Name}')", 
                    kvp.Value.Symbol ?? "(null)", kvp.Key, kvp.Value.Name ?? "(null)");
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

            var available = GetAvailableStock(product!, warehouseSymbol);
            var requested = kvp.Value.Quantity;
            var hasStock = available >= requested;

            _logger.LogDebug("Stock check for '{Symbol}' (ID: {ProductId}): requested={Requested}, available={Available}, hasStock={HasStock}",
                (object?)(kvp.Value.Symbol ?? "(null)"), kvp.Key, (object)requested, (object)available, (object)hasStock);

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
                _logger.LogWarning("Insufficient stock detected: {ErrorMessage}", itemResult.ErrorMessage);
            }

            result.Items.Add(itemResult);
        }

        return result;
    }

    /// <summary>
    /// Simple stock check for a single product.
    /// Automatically returns true for service items (Rodzaj_Id = 1).
    /// </summary>
    public bool HasSufficientStock(int productId, string warehouseSymbol, decimal requiredQuantity)
    {
        var lookup = FindProduct(productId, null, null);
        if (!lookup.Found) return false;

        // Services always have sufficient "stock"
        if (IsService(lookup.Product, _logger))
        {
            _logger.LogInformation("Skipping stock check for service product ID {Id} - services always pass validation", productId);
            return true;
        }

        var available = GetAvailableStock(lookup.Product!, warehouseSymbol);
        return available >= requiredQuantity;
    }

    /// <summary>
    /// Simple stock check for a single product by symbol.
    /// Automatically returns true for service items (Rodzaj_Id = 1).
    /// </summary>
    public bool HasSufficientStockBySymbol(string productSymbol, string warehouseSymbol, decimal requiredQuantity)
    {
        var lookup = FindProduct(null, productSymbol, null);
        if (!lookup.Found) return false;

        // Services always have sufficient "stock"
        if (IsService(lookup.Product, _logger))
        {
            _logger.LogInformation("Skipping stock check for service product '{Symbol}' - services always pass validation", productSymbol);
            return true;
        }

        var available = GetAvailableStock(lookup.Product!, warehouseSymbol);
        return available >= requiredQuantity;
    }
}
