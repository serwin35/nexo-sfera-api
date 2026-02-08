using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using NexoSferaApi.Helpers;
using NexoSferaApi.Services;

namespace NexoSferaApi.Mcp.Tools;

[McpServerToolType]
public class DocumentTools(ISferaService sferaService, ILogger<DocumentTools> logger)
{
    [McpServerTool, Description("Search documents (invoices, receipts, orders) with filters. Types: FS (sales invoice), FZ (purchase invoice), PA (receipt), ZK (customer order), ZD (supplier order).")]
    public async Task<string> SearchDocuments(
        [Description("Document type filter: FS, FZ, PA, ZK, ZD")] string? type = null,
        [Description("Filter by customer name (partial match)")] string? customerName = null,
        [Description("Start date filter (YYYY-MM-DD)")] string? dateFrom = null,
        [Description("End date filter (YYYY-MM-DD)")] string? dateTo = null,
        [Description("Show only unpaid documents")] bool unpaidOnly = false,
        [Description("Maximum number of results")] int limit = 20)
    {
        try
        {
            return await sferaService.ExecuteWithLockAsync(() =>
            {
                var managerNames = GetManagersForType(type);
                var allDocs = new List<object>();

                foreach (var managerName in managerNames)
                {
                    try
                    {
                        var manager = sferaService.GetManager(managerName);
                        if (manager != null)
                        {
                            var docs = DynamicPropertyHelper.SafeGetAll((object)manager);
                            allDocs.AddRange(docs);
                        }
                    }
                    catch { }
                }

                IEnumerable<object> filtered = allDocs;

                // Date filters
                DateTime? dtFrom = null, dtTo = null;
                if (!string.IsNullOrEmpty(dateFrom) && DateTime.TryParse(dateFrom, out var parsedFrom))
                    dtFrom = parsedFrom;
                if (!string.IsNullOrEmpty(dateTo) && DateTime.TryParse(dateTo, out var parsedTo))
                    dtTo = parsedTo;

                if (dtFrom.HasValue)
                {
                    filtered = filtered.Where(d =>
                    {
                        var data = DynamicPropertyHelper.GetDateTime(d, "DataWydaniaWystawienia")
                            ?? DynamicPropertyHelper.GetDateTime(d, "DataWystawienia");
                        return data.HasValue && data.Value >= dtFrom.Value;
                    });
                }

                if (dtTo.HasValue)
                {
                    filtered = filtered.Where(d =>
                    {
                        var data = DynamicPropertyHelper.GetDateTime(d, "DataWydaniaWystawienia")
                            ?? DynamicPropertyHelper.GetDateTime(d, "DataWystawienia");
                        return data.HasValue && data.Value <= dtTo.Value;
                    });
                }

                // Customer name filter
                if (!string.IsNullOrEmpty(customerName))
                {
                    var nameL = customerName.ToLowerInvariant();
                    filtered = filtered.Where(d =>
                    {
                        var podmiot = DynamicPropertyHelper.GetProperty(d, "Podmiot");
                        if (podmiot == null) return false;
                        var nazwa = (DynamicPropertyHelper.GetString(podmiot, "NazwaSkrocona") ?? "").ToLowerInvariant();
                        return nazwa.Contains(nameL);
                    });
                }

                // Unpaid filter
                if (unpaidOnly)
                {
                    filtered = filtered.Where(d =>
                    {
                        var doZaplaty = DynamicPropertyHelper.GetDecimal(d, "DoZaplaty");
                        if (doZaplaty > 0) return true;
                        var pozostalo = DynamicPropertyHelper.GetDecimal(d, "PozostaloDoZaplaty");
                        return pozostalo > 0;
                    });
                }

                var results = filtered
                    .OrderByDescending(d =>
                        DynamicPropertyHelper.GetDateTime(d, "DataWydaniaWystawienia")
                        ?? DynamicPropertyHelper.GetDateTime(d, "DataWystawienia")
                        ?? DateTime.MinValue)
                    .Take(limit)
                    .Select(d => MapDocumentBasic(d))
                    .ToList();

                return McpToolHelper.Success(results, $"Found {results.Count} documents");
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MCP SearchDocuments error");
            return McpToolHelper.Error("Search failed", ex.Message);
        }
    }

    [McpServerTool, Description("Get full details of a document by ID, including line items and payment info.")]
    public async Task<string> GetDocumentDetails(
        [Description("Document ID")] int documentId,
        [Description("Document type to search in: FS, FZ, PA, ZK, ZD (optional, searches all if omitted)")] string? type = null)
    {
        try
        {
            return await sferaService.ExecuteWithLockAsync(() =>
            {
                var managerNames = GetManagersForType(type);

                dynamic? doc = null;
                foreach (var managerName in managerNames)
                {
                    try
                    {
                        var manager = sferaService.GetManager(managerName);
                        if (manager != null)
                        {
                            doc = DynamicPropertyHelper.FindById((object)manager, documentId);
                            if (doc != null) break;
                        }
                    }
                    catch { }
                }

                if (doc == null)
                    return McpToolHelper.Error($"Document {documentId} not found");

                // Get line items
                var items = new List<object>();
                try
                {
                    var pozycje = DynamicPropertyHelper.GetCollection((object)doc, "Pozycje");
                    foreach (var poz in pozycje)
                    {
                        var pozDane = DynamicPropertyHelper.GetDane(poz);
                        var cena = DynamicPropertyHelper.GetProperty(pozDane, "Cena");
                        var wartosc = DynamicPropertyHelper.GetProperty(pozDane, "Wartosc");

                        items.Add(new
                        {
                            productId = DynamicPropertyHelper.GetNullableInt(pozDane, "Asortyment", "Id")
                                ?? DynamicPropertyHelper.GetNullableInt(pozDane, "AsortymentId"),
                            productSymbol = DynamicPropertyHelper.GetString(pozDane, "Asortyment", "Symbol")
                                ?? DynamicPropertyHelper.GetString(pozDane, "AsortymentWybrany", "Symbol"),
                            productName = DynamicPropertyHelper.GetString(pozDane, "Asortyment", "Nazwa")
                                ?? DynamicPropertyHelper.GetString(pozDane, "AsortymentWybrany", "Nazwa"),
                            quantity = DynamicPropertyHelper.GetDecimal(pozDane, "Ilosc"),
                            unit = DynamicPropertyHelper.GetString(pozDane, "Jednostka", "Symbol"),
                            priceNet = DynamicPropertyHelper.GetDecimal(cena, "NettoPoRabacie"),
                            priceGross = DynamicPropertyHelper.GetDecimal(cena, "BruttoPoRabacie"),
                            valueNet = DynamicPropertyHelper.GetDecimal(wartosc, "NettoPoRabacie"),
                            valueGross = DynamicPropertyHelper.GetDecimal(wartosc, "BruttoPoRabacie"),
                            vatRate = DynamicPropertyHelper.GetString(pozDane, "StawkaVat", "Wartosc"),
                        });
                    }
                }
                catch { }

                var basic = MapDocumentBasic(doc);
                var detail = new
                {
                    basic,
                    notes = DynamicPropertyHelper.GetString(doc, "Uwagi"),
                    paymentMethod = DynamicPropertyHelper.GetString(doc, "FormaPlatnosci", "Nazwa")
                        ?? DynamicPropertyHelper.GetNestedString(doc, "FormaPlatnosci", "Nazwa"),
                    dueDate = DynamicPropertyHelper.GetDateTime(doc, "TerminPlatnosci"),
                    status = DynamicPropertyHelper.GetString(doc, "StatusDokumentu", "Nazwa"),
                    items,
                };

                return McpToolHelper.Success(detail);
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MCP GetDocumentDetails error");
            return McpToolHelper.Error("Failed to get document details", ex.Message);
        }
    }

    [McpServerTool, Description("Create a sales invoice (Faktura Sprzedazy - FS). Items format: [{\"productSymbol\":\"ABC\",\"quantity\":10,\"priceNet\":100.00}]")]
    public async Task<string> CreateSalesInvoice(
        [Description("Customer ID")] int? customerId = null,
        [Description("Customer NIP (tax number) - used to find customer")] string? customerNip = null,
        [Description("Invoice items as JSON array: [{\"productSymbol\":\"...\",\"quantity\":1,\"priceNet\":100}]")] string items = "[]",
        [Description("Payment method name (e.g. 'przelew', 'gotówka')")] string? paymentMethod = null,
        [Description("Notes/remarks for the invoice")] string? notes = null)
    {
        if (customerId == null && string.IsNullOrEmpty(customerNip))
            return McpToolHelper.Error("Provide either customerId or customerNip");

        List<ItemInput>? parsedItems;
        try
        {
            parsedItems = JsonSerializer.Deserialize<List<ItemInput>>(items,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (parsedItems == null || parsedItems.Count == 0)
                return McpToolHelper.Error("Items array is empty");
        }
        catch (Exception ex)
        {
            return McpToolHelper.Error("Invalid items JSON format", ex.Message);
        }

        try
        {
            return await sferaService.ExecuteWithLockAsync(() =>
            {
                var dokumentySprzedazy = sferaService.GetManager("DokumentySprzedazy");
                if (dokumentySprzedazy == null)
                    return McpToolHelper.Error("DokumentySprzedazy manager unavailable");

                using var faktura = dokumentySprzedazy.UtworzFaktureSprzedazy();
                dynamic dane = faktura.Dane;

                // Set customer
                SetCustomer(faktura, customerId, customerNip);

                // Set notes
                if (!string.IsNullOrEmpty(notes))
                {
                    try { dane.Uwagi = notes; } catch { }
                }

                // Set payment method
                if (!string.IsNullOrEmpty(paymentMethod))
                    SetPaymentMethod(dane, paymentMethod);

                // Add items
                AddItems(faktura, parsedItems!);

                // Recalculate
                try { faktura.Przelicz(); } catch { }

                // Save
                if ((bool)faktura.Zapisz())
                {
                    var result = new
                    {
                        id = DynamicPropertyHelper.GetId(dane),
                        number = DynamicPropertyHelper.GetString(dane, "NumerPelny")
                            ?? DynamicPropertyHelper.GetString(dane, "NumerDokumentu"),
                        totalNet = DynamicPropertyHelper.GetDecimal(dane, "WartoscNetto"),
                        totalGross = DynamicPropertyHelper.GetDecimal(dane, "WartoscBrutto"),
                        date = DynamicPropertyHelper.GetDateTime(dane, "DataWydaniaWystawienia")
                            ?? DynamicPropertyHelper.GetDateTime(dane, "DataWystawienia"),
                    };
                    return McpToolHelper.Success(result, "Sales invoice created successfully");
                }

                var errors = CollectErrors(faktura);
                return McpToolHelper.Error("Failed to create sales invoice", string.Join("; ", errors));
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MCP CreateSalesInvoice error");
            return McpToolHelper.Error("Failed to create sales invoice", ex.Message);
        }
    }

    [McpServerTool, Description("Create a receipt (Paragon - PA). Items format: [{\"productSymbol\":\"ABC\",\"quantity\":1,\"priceNet\":100}]")]
    public async Task<string> CreateReceipt(
        [Description("Receipt items as JSON array: [{\"productSymbol\":\"...\",\"quantity\":1,\"priceNet\":100}]")] string items = "[]",
        [Description("Payment method name (e.g. 'gotówka', 'karta')")] string? paymentMethod = null)
    {
        List<ItemInput>? parsedItems;
        try
        {
            parsedItems = JsonSerializer.Deserialize<List<ItemInput>>(items,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (parsedItems == null || parsedItems.Count == 0)
                return McpToolHelper.Error("Items array is empty");
        }
        catch (Exception ex)
        {
            return McpToolHelper.Error("Invalid items JSON format", ex.Message);
        }

        try
        {
            return await sferaService.ExecuteWithLockAsync(() =>
            {
                var dokumentySprzedazy = sferaService.GetManager("DokumentySprzedazy");
                if (dokumentySprzedazy == null)
                    return McpToolHelper.Error("DokumentySprzedazy manager unavailable");

                using dynamic paragon = dokumentySprzedazy.UtworzParagon();
                dynamic dane = paragon.Dane;

                if (!string.IsNullOrEmpty(paymentMethod))
                    SetPaymentMethod(dane, paymentMethod);

                AddItems(paragon, parsedItems!);

                try { paragon.Przelicz(); } catch { }

                if ((bool)paragon.Zapisz())
                {
                    var result = new
                    {
                        id = DynamicPropertyHelper.GetId(dane),
                        number = DynamicPropertyHelper.GetString(dane, "NumerPelny")
                            ?? DynamicPropertyHelper.GetString(dane, "NumerDokumentu"),
                        totalGross = DynamicPropertyHelper.GetDecimal(dane, "WartoscBrutto"),
                    };
                    return McpToolHelper.Success(result, "Receipt created successfully");
                }

                var errors = CollectErrors(paragon);
                return McpToolHelper.Error("Failed to create receipt", string.Join("; ", errors));
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MCP CreateReceipt error");
            return McpToolHelper.Error("Failed to create receipt", ex.Message);
        }
    }

    [McpServerTool, Description("Create a customer order (Zamówienie od Klienta - ZK). Items format: [{\"productSymbol\":\"ABC\",\"quantity\":10,\"priceNet\":100}]")]
    public async Task<string> CreateCustomerOrder(
        [Description("Customer ID")] int? customerId = null,
        [Description("Customer NIP (tax number)")] string? customerNip = null,
        [Description("Order items as JSON array: [{\"productSymbol\":\"...\",\"quantity\":1,\"priceNet\":100}]")] string items = "[]",
        [Description("Notes/remarks for the order")] string? notes = null)
    {
        if (customerId == null && string.IsNullOrEmpty(customerNip))
            return McpToolHelper.Error("Provide either customerId or customerNip");

        List<ItemInput>? parsedItems;
        try
        {
            parsedItems = JsonSerializer.Deserialize<List<ItemInput>>(items,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (parsedItems == null || parsedItems.Count == 0)
                return McpToolHelper.Error("Items array is empty");
        }
        catch (Exception ex)
        {
            return McpToolHelper.Error("Invalid items JSON format", ex.Message);
        }

        try
        {
            return await sferaService.ExecuteWithLockAsync(() =>
            {
                var zamowieniaManager = sferaService.GetManager("ZamowieniaOdKlientow");
                if (zamowieniaManager == null)
                    return McpToolHelper.Error("ZamowieniaOdKlientow manager unavailable");

                using var zamowienie = zamowieniaManager.Utworz();
                dynamic dane = zamowienie.Dane;

                // Set customer
                SetCustomer(zamowienie, customerId, customerNip);

                if (!string.IsNullOrEmpty(notes))
                {
                    try { dane.Uwagi = notes; } catch { }
                }

                AddItems(zamowienie, parsedItems!);

                try { zamowienie.Przelicz(); } catch { }

                if ((bool)zamowienie.Zapisz())
                {
                    var result = new
                    {
                        id = DynamicPropertyHelper.GetId(dane),
                        number = DynamicPropertyHelper.GetString(dane, "NumerPelny")
                            ?? DynamicPropertyHelper.GetString(dane, "NumerDokumentu"),
                        totalNet = DynamicPropertyHelper.GetDecimal(dane, "WartoscNetto"),
                        totalGross = DynamicPropertyHelper.GetDecimal(dane, "WartoscBrutto"),
                    };
                    return McpToolHelper.Success(result, "Customer order created successfully");
                }

                var errors = CollectErrors(zamowienie);
                return McpToolHelper.Error("Failed to create customer order", string.Join("; ", errors));
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MCP CreateCustomerOrder error");
            return McpToolHelper.Error("Failed to create customer order", ex.Message);
        }
    }

    // --- Helpers ---

    private static object MapDocumentBasic(dynamic doc)
    {
        var podmiot = DynamicPropertyHelper.GetProperty(doc, "Podmiot");
        return new
        {
            id = DynamicPropertyHelper.GetId(doc),
            number = DynamicPropertyHelper.GetString(doc, "NumerPelny")
                ?? DynamicPropertyHelper.GetString(doc, "NumerDokumentu"),
            date = DynamicPropertyHelper.GetDateTime(doc, "DataWydaniaWystawienia")
                ?? DynamicPropertyHelper.GetDateTime(doc, "DataWystawienia"),
            customerName = podmiot != null ? DynamicPropertyHelper.GetString(podmiot, "NazwaSkrocona") : null,
            customerNip = podmiot != null ? DynamicPropertyHelper.GetString(podmiot, "NIP") : null,
            totalNet = DynamicPropertyHelper.GetDecimal(doc, "WartoscNetto"),
            totalGross = DynamicPropertyHelper.GetDecimal(doc, "WartoscBrutto"),
            remaining = DynamicPropertyHelper.GetDecimal(doc, "DoZaplaty") > 0
                ? DynamicPropertyHelper.GetDecimal(doc, "DoZaplaty")
                : DynamicPropertyHelper.GetDecimal(doc, "PozostaloDoZaplaty"),
            status = DynamicPropertyHelper.GetString(doc, "StatusDokumentu", "Nazwa"),
        };
    }

    private static string[] GetManagersForType(string? type)
    {
        return type?.ToUpperInvariant() switch
        {
            "FS" => ["DokumentySprzedazy"],
            "FZ" => ["DokumentyZakupu"],
            "PA" => ["DokumentySprzedazy"],
            "ZK" => ["ZamowieniaOdKlientow"],
            "ZD" => ["ZamowieniaDoDostawcow"],
            _ => ["DokumentySprzedazy", "DokumentyZakupu", "ZamowieniaOdKlientow"],
        };
    }

    private void SetCustomer(dynamic document, int? customerId, string? customerNip)
    {
        try
        {
            if (!string.IsNullOrEmpty(customerNip))
            {
                try
                {
                    document.PodmiotyDokumentu.UstawNabywceWedlugNIP(customerNip);
                    return;
                }
                catch { }
            }

            if (customerId.HasValue)
            {
                var podmiotyManager = sferaService.GetManager("Podmioty");
                if (podmiotyManager != null)
                {
                    foreach (var p in DynamicPropertyHelper.SafeGetAll((object)podmiotyManager))
                    {
                        if (DynamicPropertyHelper.GetId(p) == customerId.Value)
                        {
                            var symbol = DynamicPropertyHelper.GetString(p, "Sygnatura")
                                ?? DynamicPropertyHelper.GetString(DynamicPropertyHelper.GetProperty(p, "Sygnatura"), "PelnaSygnatura")
                                ?? DynamicPropertyHelper.GetString(p, "Symbol");

                            if (!string.IsNullOrEmpty(symbol))
                            {
                                try
                                {
                                    document.PodmiotyDokumentu.UstawNabywceWedlugSymbolu(symbol);
                                    return;
                                }
                                catch { }
                            }
                            break;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MCP SetCustomer failed");
        }
    }

    private void SetPaymentMethod(dynamic dane, string paymentMethod)
    {
        try
        {
            var formyManager = sferaService.GetManager("FormyPlatnosci");
            if (formyManager == null) return;

            var payLower = paymentMethod.ToLowerInvariant();
            foreach (var f in DynamicPropertyHelper.SafeGetAll((object)formyManager))
            {
                var nazwa = (DynamicPropertyHelper.GetString(f, "Nazwa") ?? "").ToLowerInvariant();
                if (nazwa.Contains(payLower))
                {
                    dane.FormaPlatnosci = f;
                    return;
                }
            }
        }
        catch { }
    }

    private void AddItems(dynamic document, List<ItemInput> itemsList)
    {
        var asortymentyManager = sferaService.GetManager("Asortymenty");
        if (asortymentyManager == null) return;

        foreach (var item in itemsList)
        {
            dynamic? asortyment = null;
            foreach (var a in DynamicPropertyHelper.SafeGetAll((object)asortymentyManager))
            {
                if (DynamicPropertyHelper.GetString(a, "Symbol") == item.ProductSymbol)
                {
                    asortyment = a;
                    break;
                }
            }

            if (asortyment == null)
            {
                logger.LogWarning("MCP: Product not found: {Symbol}", item.ProductSymbol);
                continue;
            }

            try
            {
                var jednostka = DynamicPropertyHelper.GetProperty(asortyment, "JednostkaSprzedazy");
                var pozycja = document.Pozycje.Dodaj(asortyment, item.Quantity, jednostka);

                if (item.PriceNet.HasValue && pozycja != null)
                {
                    pozycja.Dane.CenaNetto = item.PriceNet.Value;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "MCP: Failed to add item {Symbol}", item.ProductSymbol);
            }
        }
    }

    private static List<string> CollectErrors(dynamic document)
    {
        var errors = new List<string>();
        try
        {
            foreach (var err in document.WypiszBledy())
                errors.Add(err.ToString());
        }
        catch { }
        return errors;
    }

    private record ItemInput
    {
        public string ProductSymbol { get; init; } = "";
        public decimal Quantity { get; init; } = 1;
        public decimal? PriceNet { get; init; }
    }
}
