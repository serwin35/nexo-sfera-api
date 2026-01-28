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
    /// Checks if a product is a service type (Rodzaj_Id = 1).
    /// Services don't require stock validation as they are not physical items.
    /// In Nexo Sfera SDK:
    /// - Rodzaj_Id = 1 means Service (Usługa)
    /// - Rodzaj_Id = 2 means Product (Towar)
    /// - Rodzaj_Id = 3 means Set (Komplet)
    /// </summary>
    public bool IsService(dynamic product)
    {
        if (product == null)
        {
            _logger.LogDebug("IsService: product is null, returning false");
            return false;
        }
        
        try
        {
            // PRIORITY 1: Check Rodzaj_Id property (FK) - most reliable
            // This is a direct foreign key and won't cause navigation issues
            var rodzajId = DynamicPropertyHelper.GetNullableInt(product, "Rodzaj_Id");
            if (rodzajId.HasValue)
            {
                bool isService = (rodzajId.Value == 1);
                _logger.LogDebug("IsService: Rodzaj_Id = {RodzajId}, isService = {IsService}", 
                    (object)rodzajId.Value, (object)isService);
                return isService;
            }
            
            _logger.LogDebug("IsService: Rodzaj_Id is null, trying alternative methods");
            
            // PRIORITY 2: Try to check Rodzaj navigation property
            // This may be null if not loaded, so handle carefully
            try
            {
                var rodzaj = DynamicPropertyHelper.GetProperty(product, "Rodzaj");
                if (rodzaj != null)
                {
                    // Check Symbol on the loaded Rodzaj entity
                    var rodzajSymbol = DynamicPropertyHelper.GetString(rodzaj, "Symbol");
                    if (!string.IsNullOrEmpty(rodzajSymbol))
                    {
                        bool isService = (rodzajSymbol.Equals("Usluga", StringComparison.OrdinalIgnoreCase) || 
                                        rodzajSymbol.Equals("U", StringComparison.OrdinalIgnoreCase));
                        _logger.LogDebug("IsService: Rodzaj.Symbol = {Symbol}, isService = {IsService}", 
                            (object)rodzajSymbol, (object)isService);
                        return isService;
                    }
                    
                    // Check Id on the loaded Rodzaj entity
                    var rodzajEntityId = DynamicPropertyHelper.GetNullableInt(rodzaj, "Id");
                    if (rodzajEntityId.HasValue)
                    {
                        bool isService = (rodzajEntityId.Value == 1);
                        _logger.LogDebug("IsService: Rodzaj.Id = {Id}, isService = {IsService}", 
                            (object)rodzajEntityId.Value, (object)isService);
                        return isService;
                    }
                }
                else
                {
                    _logger.LogDebug("IsService: Rodzaj navigation property is null (not loaded)");
                }
            }
            catch (Exception navEx)
            {
                _logger.LogDebug(navEx, "IsService: Could not navigate to Rodzaj property");
            }
            
            // PRIORITY 3: Use heuristics based on product name/symbol patterns
            // Some service items might not have Rodzaj_Id set properly
            var productName = DynamicPropertyHelper.GetString(product, "Nazwa") ?? "";
            var productSymbol = DynamicPropertyHelper.GetString(product, "Symbol") ?? "";
            
            // Common service name patterns (case-insensitive)
            var serviceNamePatterns = new[]
            {
                "usługa", "uslugi", "service",
                "koszt przesyłki", "shipping", "dostawa", "delivery",
                "obsługa", "handling", "obsługi",
                "montaż", "instalacja", "installation",
                "transport", "przewóz",
                "opłata", "fee", "charge"
            };
            
            foreach (var pattern in serviceNamePatterns)
            {
                if (productName.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
                    productSymbol.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("IsService: Detected service by name/symbol pattern '{Pattern}' in '{Name}' (Symbol: {Symbol})",
                        (object)pattern, (object)productName, (object)productSymbol);
                    return true;
                }
            }
            
            // If we can't determine the product type, log a warning and assume it's NOT a service
            // This is safer - we prefer to validate stock for unknown types
            _logger.LogWarning("IsService: Could not determine product type for '{Name}' (Symbol: {Symbol}) - assuming it's not a service",
                (object)productName, (object)productSymbol);
            return false;
        }
        catch (Exception ex)
        {
            // Unexpected exception - log and assume not a service (safer)
            var productName = DynamicPropertyHelper.GetString(product, "Nazwa") ?? "(unknown)";
            var productSymbol = DynamicPropertyHelper.GetString(product, "Symbol") ?? "(unknown)";
            _logger.LogWarning(ex, "IsService: Exception while checking if product '{Name}' (Symbol: {Symbol}) is a service",
                (object)productName, (object)productSymbol);
            return false;
        }
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
            if (IsService(product))
            {
                _logger.LogInformation("Skipping stock validation for service product '{Symbol}' (ID: {Id})", kvp.Value.Symbol, kvp.Key);
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
        if (IsService(lookup.Product))
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
        if (IsService(lookup.Product))
        {
            _logger.LogInformation("Skipping stock check for service product '{Symbol}' - services always pass validation", productSymbol);
            return true;
        }

        var available = GetAvailableStock(lookup.Product!, warehouseSymbol);
        return available >= requiredQuantity;
    }
}
