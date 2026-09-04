using System.ComponentModel;
using ModelContextProtocol.Server;
using NexoSferaApi.Helpers;
using NexoSferaApi.Services;

namespace NexoSferaApi.Mcp.Tools;

[McpServerToolType]
public class InventoryTools(ISferaService sferaService, ILogger<InventoryTools> logger)
{
    [McpServerTool, Description("Get stock levels across warehouses with optional filters. Shows quantity, reserved, and available amounts per product.")]
    public async Task<string> GetStockLevels(
        [Description("Filter to specific warehouse symbol")] string? warehouseSymbol = null,
        [Description("Filter to specific product symbol")] string? productSymbol = null,
        [Description("Only show products with low stock (available < 5)")] bool lowStockOnly = false,
        [Description("Maximum number of results")] int limit = 50)
    {
        try
        {
            return await sferaService.ExecuteWithLockAsync(() =>
            {
                var asortymentyManager = sferaService.GetManager("Asortymenty");
                if (asortymentyManager == null)
                    return McpToolHelper.Error("Asortymenty manager unavailable");

                var results = new List<object>();

                foreach (var a in DynamicPropertyHelper.SafeGetAll((object)asortymentyManager))
                {
                    if (!DynamicPropertyHelper.TracksStock(a))
                        continue;

                    if (!string.IsNullOrEmpty(productSymbol))
                    {
                        var sym = DynamicPropertyHelper.GetString(a, "Symbol") ?? "";
                        if (!sym.Equals(productSymbol, StringComparison.OrdinalIgnoreCase) &&
                            !sym.Contains(productSymbol, StringComparison.OrdinalIgnoreCase))
                            continue;
                    }

                    var stany = DynamicPropertyHelper.GetCollection((object)a, "StanyMagazynowe");
                    foreach (var s in stany)
                    {
                        var whSymbol = DynamicPropertyHelper.GetString(s, "Magazyn", "Symbol")
                            ?? DynamicPropertyHelper.GetNestedString(s, "Magazyn", "Symbol");

                        if (!string.IsNullOrEmpty(warehouseSymbol) && whSymbol != warehouseSymbol)
                            continue;

                        var qty = DynamicPropertyHelper.GetDecimal(s, "Stan");
                        var reserved = DynamicPropertyHelper.GetDecimal(s, "StanRezerwacji");
                        var available = DynamicPropertyHelper.GetDecimal(s, "StanDostepny");

                        if (lowStockOnly && available >= 5)
                            continue;

                        results.Add(new
                        {
                            productSymbol = DynamicPropertyHelper.GetString(a, "Symbol"),
                            productName = DynamicPropertyHelper.GetString(a, "Nazwa"),
                            warehouseSymbol = whSymbol,
                            quantity = qty,
                            reserved,
                            available,
                            unit = DynamicPropertyHelper.GetString(a, "JednostkaPodstawowa", "Symbol"),
                        });

                        if (results.Count >= limit) break;
                    }

                    if (results.Count >= limit) break;
                }

                return McpToolHelper.Success(results, $"Found {results.Count} stock entries");
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MCP GetStockLevels error");
            return McpToolHelper.Error("Failed to get stock levels", ex.Message);
        }
    }

    [McpServerTool, Description("Get warehouse documents (WZ, PZ, MM, RW, PW) with optional filters.")]
    public async Task<string> GetWarehouseDocuments(
        [Description("Document type filter: WZ (external release), PZ (external receipt), MM (inter-warehouse), RW (internal release), PW (internal receipt)")] string? type = null,
        [Description("Start date filter (YYYY-MM-DD)")] string? dateFrom = null,
        [Description("Maximum number of results")] int limit = 20)
    {
        try
        {
            return await sferaService.ExecuteWithLockAsync(() =>
            {
                var managerNames = type?.ToUpperInvariant() switch
                {
                    "WZ" => new[] { "WydaniaZewnetrzne" },
                    "PZ" => new[] { "PrzyjeciaZewnetrzne" },
                    "MM" => new[] { "WydaniaMiedzymagazynowe" },
                    "RW" => new[] { "RozchodyWewnetrzne" },
                    "PW" => new[] { "PrzychodyWewnetrzne" },
                    _ => new[] { "WydaniaZewnetrzne", "PrzyjeciaZewnetrzne", "WydaniaMiedzymagazynowe",
                                 "RozchodyWewnetrzne", "PrzychodyWewnetrzne" },
                };

                var allDocs = new List<object>();
                foreach (var managerName in managerNames)
                {
                    try
                    {
                        var manager = sferaService.GetManager(managerName);
                        if (manager != null)
                            allDocs.AddRange(DynamicPropertyHelper.SafeGetAll((object)manager));
                    }
                    catch { }
                }

                IEnumerable<object> filtered = allDocs;

                if (!string.IsNullOrEmpty(dateFrom) && DateTime.TryParse(dateFrom, out var dtFrom))
                {
                    filtered = filtered.Where(d =>
                    {
                        var data = DynamicPropertyHelper.GetDateTime(d, "DataWydaniaWystawienia")
                            ?? DynamicPropertyHelper.GetDateTime(d, "DataWystawienia");
                        return data.HasValue && data.Value >= dtFrom;
                    });
                }

                var results = filtered
                    .OrderByDescending(d =>
                        DynamicPropertyHelper.GetDateTime(d, "DataWydaniaWystawienia")
                        ?? DynamicPropertyHelper.GetDateTime(d, "DataWystawienia")
                        ?? DateTime.MinValue)
                    .Take(limit)
                    .Select(d =>
                    {
                        var podmiot = DynamicPropertyHelper.GetProperty(d, "Podmiot");
                        return new
                        {
                            id = DynamicPropertyHelper.GetId(d),
                            number = DynamicPropertyHelper.GetString(d, "NumerPelny")
                                ?? DynamicPropertyHelper.GetString(d, "NumerDokumentu"),
                            date = DynamicPropertyHelper.GetDateTime(d, "DataWydaniaWystawienia")
                                ?? DynamicPropertyHelper.GetDateTime(d, "DataWystawienia"),
                            customerName = podmiot != null ? DynamicPropertyHelper.GetString(podmiot, "NazwaSkrocona") : null,
                            warehouse = DynamicPropertyHelper.GetString(d, "Magazyn", "Symbol"),
                            totalNet = DynamicPropertyHelper.GetDecimal(d, "WartoscNetto"),
                            totalGross = DynamicPropertyHelper.GetDecimal(d, "WartoscBrutto"),
                            status = DynamicPropertyHelper.GetString(d, "StatusDokumentu", "Nazwa"),
                        };
                    })
                    .ToList();

                return McpToolHelper.Success(results, $"Found {results.Count} warehouse documents");
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MCP GetWarehouseDocuments error");
            return McpToolHelper.Error("Failed to get warehouse documents", ex.Message);
        }
    }
}
