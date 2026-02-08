using System.ComponentModel;
using ModelContextProtocol.Server;
using NexoSferaApi.Helpers;
using NexoSferaApi.Services;

namespace NexoSferaApi.Mcp.Tools;

[McpServerToolType]
public class FinanceTools(ISferaService sferaService, ILogger<FinanceTools> logger)
{
    [McpServerTool, Description("Get unpaid or overdue invoices. Shows remaining amounts and due dates.")]
    public async Task<string> GetUnpaidDocuments(
        [Description("Filter by customer name (partial match)")] string? customerName = null,
        [Description("Filter by customer NIP")] string? customerNip = null,
        [Description("Show only overdue documents (past due date)")] bool overdueOnly = false,
        [Description("Maximum number of results")] int limit = 20)
    {
        try
        {
            return await sferaService.ExecuteWithLockAsync(() =>
            {
                var managerNames = new[] { "DokumentySprzedazy", "DokumentyZakupu" };
                var unpaid = new List<object>();

                foreach (var managerName in managerNames)
                {
                    try
                    {
                        var manager = sferaService.GetManager(managerName);
                        if (manager == null) continue;

                        foreach (var doc in DynamicPropertyHelper.SafeGetAll((object)manager))
                        {
                            var doZaplaty = DynamicPropertyHelper.GetDecimal(doc, "DoZaplaty");
                            var pozostalo = DynamicPropertyHelper.GetDecimal(doc, "PozostaloDoZaplaty");
                            var remaining = doZaplaty > 0 ? doZaplaty : pozostalo;
                            if (remaining <= 0) continue;

                            // Customer filters
                            var podmiot = DynamicPropertyHelper.GetProperty(doc, "Podmiot");
                            if (!string.IsNullOrEmpty(customerName))
                            {
                                var nazwa = (podmiot != null ? DynamicPropertyHelper.GetString(podmiot, "NazwaSkrocona") : "") ?? "";
                                if (!nazwa.Contains(customerName, StringComparison.OrdinalIgnoreCase))
                                    continue;
                            }
                            if (!string.IsNullOrEmpty(customerNip))
                            {
                                var nip = (podmiot != null ? DynamicPropertyHelper.GetString(podmiot, "NIP") : "") ?? "";
                                if (nip != customerNip)
                                    continue;
                            }

                            var dueDate = DynamicPropertyHelper.GetDateTime(doc, "TerminPlatnosci");
                            if (overdueOnly && (!dueDate.HasValue || dueDate.Value >= DateTime.Today))
                                continue;

                            unpaid.Add(new
                            {
                                id = DynamicPropertyHelper.GetId(doc),
                                number = DynamicPropertyHelper.GetString(doc, "NumerPelny")
                                    ?? DynamicPropertyHelper.GetString(doc, "NumerDokumentu"),
                                date = DynamicPropertyHelper.GetDateTime(doc, "DataWydaniaWystawienia")
                                    ?? DynamicPropertyHelper.GetDateTime(doc, "DataWystawienia"),
                                dueDate,
                                isOverdue = dueDate.HasValue && dueDate.Value < DateTime.Today,
                                daysOverdue = dueDate.HasValue && dueDate.Value < DateTime.Today
                                    ? (DateTime.Today - dueDate.Value).Days : 0,
                                customerName = podmiot != null ? DynamicPropertyHelper.GetString(podmiot, "NazwaSkrocona") : null,
                                customerNip = podmiot != null ? DynamicPropertyHelper.GetString(podmiot, "NIP") : null,
                                totalGross = DynamicPropertyHelper.GetDecimal(doc, "WartoscBrutto"),
                                remaining,
                                source = managerName == "DokumentySprzedazy" ? "sales" : "purchase",
                            });

                            if (unpaid.Count >= limit) break;
                        }
                    }
                    catch { }

                    if (unpaid.Count >= limit) break;
                }

                // Sort by overdue days descending
                var sorted = unpaid
                    .OrderByDescending(u => ((dynamic)u).daysOverdue)
                    .ToList();

                var totalRemaining = sorted.Sum(u => (decimal)((dynamic)u).remaining);

                return McpToolHelper.Success(new
                {
                    documents = sorted,
                    summary = new
                    {
                        count = sorted.Count,
                        totalRemaining,
                        overdueCount = sorted.Count(u => (bool)((dynamic)u).isOverdue),
                    }
                }, $"Found {sorted.Count} unpaid documents, total remaining: {totalRemaining:N2}");
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MCP GetUnpaidDocuments error");
            return McpToolHelper.Error("Failed to get unpaid documents", ex.Message);
        }
    }

    [McpServerTool, Description("Get settlements (rozrachunki) - receivables and payables. Shows amounts owed to and by the company.")]
    public async Task<string> GetSettlements(
        [Description("Filter by customer name (partial match)")] string? customerName = null,
        [Description("Filter type: 'receivable' (naleznosci) or 'payable' (zobowiazania)")] string? type = null,
        [Description("Maximum number of results")] int limit = 20)
    {
        try
        {
            return await sferaService.ExecuteWithLockAsync(() =>
            {
                var rozrachunkiManager = sferaService.GetManager("Rozrachunki");
                if (rozrachunkiManager == null)
                    return McpToolHelper.Error("Rozrachunki manager unavailable");

                var all = DynamicPropertyHelper.SafeGetAll((object)rozrachunkiManager);
                IEnumerable<object> filtered = all;

                if (!string.IsNullOrEmpty(customerName))
                {
                    var nameLower = customerName.ToLowerInvariant();
                    filtered = filtered.Where(r =>
                    {
                        var podmiot = DynamicPropertyHelper.GetProperty(r, "Podmiot");
                        if (podmiot == null) return false;
                        var nazwa = (DynamicPropertyHelper.GetString(podmiot, "NazwaSkrocona") ?? "").ToLowerInvariant();
                        return nazwa.Contains(nameLower);
                    });
                }

                if (!string.IsNullOrEmpty(type))
                {
                    filtered = filtered.Where(r =>
                    {
                        var kwota = DynamicPropertyHelper.GetDecimal(r, "Kwota");
                        var typLower = type.ToLowerInvariant();
                        if (typLower == "receivable" || typLower == "naleznosci")
                            return kwota > 0;
                        if (typLower == "payable" || typLower == "zobowiazania")
                            return kwota < 0;
                        return true;
                    });
                }

                var results = filtered
                    .Take(limit)
                    .Select(r =>
                    {
                        var podmiot = DynamicPropertyHelper.GetProperty(r, "Podmiot");
                        return new
                        {
                            id = DynamicPropertyHelper.GetId(r),
                            customerName = podmiot != null ? DynamicPropertyHelper.GetString(podmiot, "NazwaSkrocona") : null,
                            customerNip = podmiot != null ? DynamicPropertyHelper.GetString(podmiot, "NIP") : null,
                            amount = DynamicPropertyHelper.GetDecimal(r, "Kwota"),
                            remaining = DynamicPropertyHelper.GetDecimal(r, "KwotaPozostala"),
                            dueDate = DynamicPropertyHelper.GetDateTime(r, "TerminPlatnosci"),
                            documentNumber = DynamicPropertyHelper.GetString(r, "NumerDokumentu"),
                            description = DynamicPropertyHelper.GetString(r, "Opis"),
                        };
                    })
                    .ToList();

                return McpToolHelper.Success(results, $"Found {results.Count} settlements");
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MCP GetSettlements error");
            return McpToolHelper.Error("Failed to get settlements", ex.Message);
        }
    }

    [McpServerTool, Description("Get a financial summary: total receivables, payables, overdue amounts, and cash register/bank balances.")]
    public async Task<string> GetFinanceSummary()
    {
        try
        {
            return await sferaService.ExecuteWithLockAsync(() =>
            {
                decimal totalReceivable = 0, totalPayable = 0, totalOverdue = 0;
                int overdueCount = 0, unpaidSalesCount = 0, unpaidPurchaseCount = 0;

                // Scan sales invoices for unpaid amounts
                try
                {
                    var salesManager = sferaService.GetManager("DokumentySprzedazy");
                    if (salesManager != null)
                    {
                        foreach (var doc in DynamicPropertyHelper.SafeGetAll((object)salesManager))
                        {
                            var remaining = DynamicPropertyHelper.GetDecimal(doc, "DoZaplaty");
                            if (remaining <= 0) remaining = DynamicPropertyHelper.GetDecimal(doc, "PozostaloDoZaplaty");
                            if (remaining <= 0) continue;

                            unpaidSalesCount++;
                            totalReceivable += remaining;

                            var dueDate = DynamicPropertyHelper.GetDateTime(doc, "TerminPlatnosci");
                            if (dueDate.HasValue && dueDate.Value < DateTime.Today)
                            {
                                totalOverdue += remaining;
                                overdueCount++;
                            }
                        }
                    }
                }
                catch { }

                // Scan purchase invoices for unpaid amounts
                try
                {
                    var purchaseManager = sferaService.GetManager("DokumentyZakupu");
                    if (purchaseManager != null)
                    {
                        foreach (var doc in DynamicPropertyHelper.SafeGetAll((object)purchaseManager))
                        {
                            var remaining = DynamicPropertyHelper.GetDecimal(doc, "DoZaplaty");
                            if (remaining <= 0) remaining = DynamicPropertyHelper.GetDecimal(doc, "PozostaloDoZaplaty");
                            if (remaining <= 0) continue;

                            unpaidPurchaseCount++;
                            totalPayable += remaining;
                        }
                    }
                }
                catch { }

                var summary = new
                {
                    receivables = new
                    {
                        total = totalReceivable,
                        unpaidInvoices = unpaidSalesCount,
                        overdue = totalOverdue,
                        overdueInvoices = overdueCount,
                    },
                    payables = new
                    {
                        total = totalPayable,
                        unpaidInvoices = unpaidPurchaseCount,
                    },
                    balance = totalReceivable - totalPayable,
                };

                return McpToolHelper.Success(summary, "Financial summary generated");
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MCP GetFinanceSummary error");
            return McpToolHelper.Error("Failed to get financial summary", ex.Message);
        }
    }
}
