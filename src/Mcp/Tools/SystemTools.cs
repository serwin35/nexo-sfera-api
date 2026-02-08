using System.ComponentModel;
using InsERT.Moria.Sfera;
using ModelContextProtocol.Server;
using NexoSferaApi.Helpers;
using NexoSferaApi.Services;

namespace NexoSferaApi.Mcp.Tools;

[McpServerToolType]
public class SystemTools(ISferaService sferaService, ILogger<SystemTools> logger)
{
    [McpServerTool, Description("Get system information: connection status, database, product, current operator")]
    public async Task<string> GetSystemInfo()
    {
        try
        {
            return await sferaService.ExecuteWithLockAsync(() =>
            {
                if (!sferaService.IsConnected)
                    return McpToolHelper.Error("Sfera is not connected");

                var sfera = sferaService.GetSfera();
                var kontekst = sfera.Kontekst();

                var info = new
                {
                    connected = true,
                    currentOperator = sferaService.GetCurrentOperatorLogin(),
                    context = new
                    {
                        warehouse = DynamicPropertyHelper.GetString(kontekst, "SymbolMagazynu"),
                        branch = DynamicPropertyHelper.GetString(kontekst, "SymbolOddzialu"),
                    }
                };

                return McpToolHelper.Success(info, "System information retrieved");
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MCP GetSystemInfo error");
            return McpToolHelper.Error("Failed to get system info", ex.Message);
        }
    }

    [McpServerTool, Description("List all available SDK managers (e.g. Asortymenty, Podmioty, DokumentySprzedazy, etc.)")]
    public Task<string> ListManagers()
    {
        var managers = new[]
        {
            new { name = "Podmioty", description = "Customers / Contractors (kontrahenci)" },
            new { name = "Grupy", description = "Customer groups" },
            new { name = "Asortymenty", description = "Products / Assortment" },
            new { name = "GrupyAsortymentu", description = "Product groups" },
            new { name = "SzablonyAsortymentu", description = "Product templates" },
            new { name = "DokumentySprzedazy", description = "Sales documents (FS - invoices)" },
            new { name = "DokumentyZakupu", description = "Purchase documents (FZ)" },
            new { name = "ZamowieniaOdKlientow", description = "Customer orders (ZK)" },
            new { name = "ZamowieniaDoDostawcow", description = "Supplier orders (ZD)" },
            new { name = "Oferty", description = "Offers" },
            new { name = "WydaniaZewnetrzne", description = "External releases (WZ)" },
            new { name = "PrzyjeciaZewnetrzne", description = "External receipts (PZ)" },
            new { name = "RozchodyWewnetrzne", description = "Internal releases (RW)" },
            new { name = "PrzychodyWewnetrzne", description = "Internal receipts (PW)" },
            new { name = "WydaniaMiedzymagazynowe", description = "Inter-warehouse transfers (MM)" },
            new { name = "Magazyny", description = "Warehouses" },
            new { name = "OperacjeKasowe", description = "Cash operations (KP/KW)" },
            new { name = "OperacjeBankowe", description = "Bank operations (BP/BW)" },
            new { name = "Rozrachunki", description = "Settlements / Receivables" },
            new { name = "StawkiVat", description = "VAT rates" },
            new { name = "JednostkiMiar", description = "Units of measure" },
            new { name = "Waluty", description = "Currencies" },
            new { name = "FormyPlatnosci", description = "Payment methods" },
            new { name = "Cenniki", description = "Price lists" },
            new { name = "PoziomyCen", description = "Price levels" },
            new { name = "StanowiskaKasowe", description = "Cash registers" },
            new { name = "RachunkiBankowe", description = "Bank accounts" },
        };

        return Task.FromResult(McpToolHelper.Success(managers, $"{managers.Length} managers available"));
    }
}
