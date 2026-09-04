using System.ComponentModel;
using ModelContextProtocol.Server;
using NexoSferaApi.Helpers;
using NexoSferaApi.Services;

namespace NexoSferaApi.Mcp.Tools;

[McpServerToolType]
public class ProductTools(ISferaService sferaService, ILogger<ProductTools> logger)
{
    [McpServerTool, Description("Search products (asortyment) by name, symbol, or EAN code. Returns a list of matching products with basic info.")]
    public async Task<string> SearchProducts(
        [Description("Search query - matches against name, symbol, EAN")] string query,
        [Description("Only return active (available) products")] bool activeOnly = true,
        [Description("Maximum number of results to return")] int limit = 10)
    {
        try
        {
            return await sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = sferaService.GetManager("Asortymenty");
                if (manager == null) return McpToolHelper.Error("Asortymenty manager unavailable");

                var queryLower = query.ToLowerInvariant();
                IEnumerable<object> source;

                if (activeOnly)
                {
                    var list = new List<object>();
                    try
                    {
                        foreach (var a in manager.Dane.WszystkieDostepne())
                            list.Add(a);
                    }
                    catch
                    {
                        list = DynamicPropertyHelper.SafeGetAll((object)manager);
                    }
                    source = list;
                }
                else
                {
                    source = DynamicPropertyHelper.SafeGetAll((object)manager);
                }

                var results = source
                    .Where(p =>
                    {
                        var symbol = (DynamicPropertyHelper.GetString(p, "Symbol") ?? "").ToLowerInvariant();
                        var nazwa = (DynamicPropertyHelper.GetString(p, "Nazwa") ?? "").ToLowerInvariant();
                        var ean = (DynamicPropertyHelper.GetString(p, "EAN") ?? "").ToLowerInvariant();
                        return symbol.Contains(queryLower) || nazwa.Contains(queryLower) || ean.Contains(queryLower);
                    })
                    .Take(limit)
                    .Select(p => new
                    {
                        id = DynamicPropertyHelper.GetId(p),
                        symbol = DynamicPropertyHelper.GetString(p, "Symbol"),
                        name = DynamicPropertyHelper.GetString(p, "Nazwa"),
                        ean = DynamicPropertyHelper.GetString(p, "EAN"),
                        type = DynamicPropertyHelper.GetEnumString(p, "Rodzaj"),
                        unit = DynamicPropertyHelper.GetString(p, "JednostkaPodstawowa", "Symbol"),
                        priceNet = DynamicPropertyHelper.GetDecimal(p, "CenaBazowaNetto"),
                        priceGross = DynamicPropertyHelper.GetDecimal(p, "CenaBazowaOdSprzedazy"),
                        vatRate = DynamicPropertyHelper.GetString(p, "StawkaVatSprzedazy", "Wartosc"),
                        isActive = !DynamicPropertyHelper.GetBool(p, "CzyZablokowany"),
                    })
                    .ToList();

                return McpToolHelper.Success(results, $"Found {results.Count} products matching '{query}'");
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MCP SearchProducts error");
            return McpToolHelper.Error("Search failed", ex.Message);
        }
    }

    [McpServerTool, Description("Get full details of a product by ID or symbol. Returns complete product data including prices, stock levels, and attributes.")]
    public async Task<string> GetProductDetails(
        [Description("Product ID in the system")] int? productId = null,
        [Description("Product symbol")] string? symbol = null)
    {
        if (productId == null && string.IsNullOrEmpty(symbol))
            return McpToolHelper.Error("Provide either productId or symbol");

        try
        {
            return await sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = sferaService.GetManager("Asortymenty");
                if (manager == null) return McpToolHelper.Error("Asortymenty manager unavailable");

                dynamic? produkt = null;
                if (productId.HasValue)
                {
                    produkt = DynamicPropertyHelper.FindById((object)manager, productId.Value);
                }
                else if (!string.IsNullOrEmpty(symbol))
                {
                    produkt = DynamicPropertyHelper.SafeGetAll((object)manager)
                        .FirstOrDefault(p => DynamicPropertyHelper.GetString(p, "Symbol") == symbol);
                }

                if (produkt == null)
                    return McpToolHelper.Error("Product not found");

                // Get stock levels
                var stockLevels = new List<object>();
                try
                {
                    var stany = DynamicPropertyHelper.GetCollection((object)produkt, "StanyMagazynowe");
                    foreach (var s in stany)
                    {
                        stockLevels.Add(new
                        {
                            warehouseSymbol = DynamicPropertyHelper.GetString(s, "Magazyn", "Symbol")
                                ?? DynamicPropertyHelper.GetNestedString(s, "Magazyn", "Symbol"),
                            quantity = DynamicPropertyHelper.GetDecimal(s, "Stan"),
                            reserved = DynamicPropertyHelper.GetDecimal(s, "StanRezerwacji"),
                            available = DynamicPropertyHelper.GetDecimal(s, "StanDostepny"),
                        });
                    }
                }
                catch { }

                var dto = new
                {
                    id = DynamicPropertyHelper.GetId(produkt),
                    symbol = DynamicPropertyHelper.GetString(produkt, "Symbol"),
                    name = DynamicPropertyHelper.GetString(produkt, "Nazwa"),
                    fullName = DynamicPropertyHelper.GetString(produkt, "NazwaPelna"),
                    ean = DynamicPropertyHelper.GetString(produkt, "EAN"),
                    type = DynamicPropertyHelper.GetEnumString(produkt, "Rodzaj"),
                    unit = DynamicPropertyHelper.GetString(produkt, "JednostkaPodstawowa", "Symbol"),
                    priceNet = DynamicPropertyHelper.GetDecimal(produkt, "CenaBazowaNetto"),
                    priceGross = DynamicPropertyHelper.GetDecimal(produkt, "CenaBazowaOdSprzedazy"),
                    vatRate = DynamicPropertyHelper.GetString(produkt, "StawkaVatSprzedazy", "Wartosc"),
                    isActive = !DynamicPropertyHelper.GetBool(produkt, "CzyZablokowany"),
                    isService = !DynamicPropertyHelper.TracksStock(produkt),
                    isTracked = DynamicPropertyHelper.TracksStock(produkt),
                    weight = DynamicPropertyHelper.GetNullableDecimal(produkt, "Waga"),
                    stockLevels,
                };

                return McpToolHelper.Success(dto);
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MCP GetProductDetails error");
            return McpToolHelper.Error("Failed to get product details", ex.Message);
        }
    }

    [McpServerTool, Description("Check stock levels for a product across warehouses. Returns available, reserved, and total quantities.")]
    public async Task<string> CheckStock(
        [Description("Product symbol")] string? productSymbol = null,
        [Description("Product ID")] int? productId = null,
        [Description("Filter to specific warehouse symbol")] string? warehouseSymbol = null)
    {
        if (string.IsNullOrEmpty(productSymbol) && productId == null)
            return McpToolHelper.Error("Provide either productSymbol or productId");

        try
        {
            return await sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = sferaService.GetManager("Asortymenty");
                if (manager == null) return McpToolHelper.Error("Asortymenty manager unavailable");

                dynamic? produkt = null;
                if (productId.HasValue)
                {
                    produkt = DynamicPropertyHelper.FindById((object)manager, productId.Value);
                }
                else if (!string.IsNullOrEmpty(productSymbol))
                {
                    produkt = DynamicPropertyHelper.SafeGetAll((object)manager)
                        .FirstOrDefault(p => DynamicPropertyHelper.GetString(p, "Symbol") == productSymbol);
                }

                if (produkt == null)
                    return McpToolHelper.Error("Product not found");

                var stockLevels = new List<object>();
                decimal totalQty = 0, totalReserved = 0, totalAvailable = 0;

                try
                {
                    var stany = DynamicPropertyHelper.GetCollection((object)produkt, "StanyMagazynowe");
                    foreach (var s in stany)
                    {
                        var whSymbol = DynamicPropertyHelper.GetString(s, "Magazyn", "Symbol")
                            ?? DynamicPropertyHelper.GetNestedString(s, "Magazyn", "Symbol");

                        if (!string.IsNullOrEmpty(warehouseSymbol) && whSymbol != warehouseSymbol)
                            continue;

                        var qty = DynamicPropertyHelper.GetDecimal(s, "Stan");
                        var reserved = DynamicPropertyHelper.GetDecimal(s, "StanRezerwacji");
                        var available = DynamicPropertyHelper.GetDecimal(s, "StanDostepny");
                        totalQty += qty;
                        totalReserved += reserved;
                        totalAvailable += available;

                        stockLevels.Add(new
                        {
                            warehouseSymbol = whSymbol,
                            quantity = qty,
                            reserved,
                            available,
                        });
                    }
                }
                catch { }

                var result = new
                {
                    productSymbol = DynamicPropertyHelper.GetString(produkt, "Symbol"),
                    productName = DynamicPropertyHelper.GetString(produkt, "Nazwa"),
                    unit = DynamicPropertyHelper.GetString(produkt, "JednostkaPodstawowa", "Symbol"),
                    totalQuantity = totalQty,
                    totalReserved,
                    totalAvailable,
                    warehouses = stockLevels,
                };

                return McpToolHelper.Success(result);
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MCP CheckStock error");
            return McpToolHelper.Error("Failed to check stock", ex.Message);
        }
    }
}
