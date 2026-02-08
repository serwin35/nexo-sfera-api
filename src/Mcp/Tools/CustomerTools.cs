using System.ComponentModel;
using ModelContextProtocol.Server;
using NexoSferaApi.Helpers;
using NexoSferaApi.Services;

namespace NexoSferaApi.Mcp.Tools;

[McpServerToolType]
public class CustomerTools(ISferaService sferaService, ILogger<CustomerTools> logger)
{
    [McpServerTool, Description("Search customers (kontrahenci) by name, NIP, or symbol. Returns a list of matching customers with basic info.")]
    public async Task<string> SearchCustomers(
        [Description("Search query - matches against name, NIP, symbol")] string query,
        [Description("Maximum number of results to return")] int limit = 10)
    {
        try
        {
            return await sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = sferaService.GetManager("Podmioty");
                if (manager == null) return McpToolHelper.Error("Podmioty manager unavailable");

                var queryLower = query.ToLowerInvariant();
                var results = DynamicPropertyHelper.SafeGetAll((object)manager)
                    .Where(p =>
                    {
                        var symbol = (DynamicPropertyHelper.GetString(p, "Symbol") ?? "").ToLowerInvariant();
                        var nazwa = (DynamicPropertyHelper.GetString(p, "NazwaSkrocona") ?? "").ToLowerInvariant();
                        var nip = (DynamicPropertyHelper.GetString(p, "NIP") ?? "").ToLowerInvariant();
                        var pelnaNazwa = (DynamicPropertyHelper.GetString(p, "NazwaPelna") ?? "").ToLowerInvariant();
                        return symbol.Contains(queryLower) || nazwa.Contains(queryLower) ||
                               nip.Contains(queryLower) || pelnaNazwa.Contains(queryLower);
                    })
                    .Take(limit)
                    .Select(p => new
                    {
                        id = DynamicPropertyHelper.GetId(p),
                        symbol = DynamicPropertyHelper.GetString(p, "Symbol"),
                        shortName = DynamicPropertyHelper.GetString(p, "NazwaSkrocona"),
                        nip = DynamicPropertyHelper.GetString(p, "NIP"),
                        email = DynamicPropertyHelper.GetString(p, "Email"),
                        phone = DynamicPropertyHelper.GetString(p, "Telefon"),
                        city = DynamicPropertyHelper.GetString(p, "AdresPodstawowy", "Miasto") ??
                               DynamicPropertyHelper.GetNestedString(p, "AdresPodstawowy", "Szczegoly", "Miejscowosc"),
                    })
                    .ToList();

                return McpToolHelper.Success(results, $"Found {results.Count} customers matching '{query}'");
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MCP SearchCustomers error");
            return McpToolHelper.Error("Search failed", ex.Message);
        }
    }

    [McpServerTool, Description("Get full details of a customer by ID or NIP. Returns all customer data including addresses, contacts, and financial info.")]
    public async Task<string> GetCustomerDetails(
        [Description("Customer ID in the system")] int? customerId = null,
        [Description("Customer NIP (tax number)")] string? nip = null)
    {
        if (customerId == null && string.IsNullOrEmpty(nip))
            return McpToolHelper.Error("Provide either customerId or nip");

        try
        {
            return await sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = sferaService.GetManager("Podmioty");
                if (manager == null) return McpToolHelper.Error("Podmioty manager unavailable");

                dynamic? podmiot = null;
                if (customerId.HasValue)
                {
                    podmiot = DynamicPropertyHelper.FindById((object)manager, customerId.Value);
                }
                else if (!string.IsNullOrEmpty(nip))
                {
                    podmiot = DynamicPropertyHelper.SafeGetAll((object)manager)
                        .FirstOrDefault(p => DynamicPropertyHelper.GetString(p, "NIP") == nip);
                }

                if (podmiot == null)
                    return McpToolHelper.Error("Customer not found");

                var adres = DynamicPropertyHelper.GetProperty(podmiot, "AdresPodstawowy");
                var szczegoly = adres != null ? DynamicPropertyHelper.GetProperty(adres, "Szczegoly") : null;

                var dto = new
                {
                    id = DynamicPropertyHelper.GetId(podmiot),
                    symbol = DynamicPropertyHelper.GetString(podmiot, "Symbol"),
                    shortName = DynamicPropertyHelper.GetString(podmiot, "NazwaSkrocona"),
                    fullName = DynamicPropertyHelper.GetString(podmiot, "NazwaPelna"),
                    nip = DynamicPropertyHelper.GetString(podmiot, "NIP"),
                    regon = DynamicPropertyHelper.GetString(podmiot, "REGON"),
                    email = DynamicPropertyHelper.GetString(podmiot, "Email"),
                    phone = DynamicPropertyHelper.GetString(podmiot, "Telefon"),
                    isSupplier = DynamicPropertyHelper.GetBool(podmiot, "JestDostawca"),
                    isCustomer = DynamicPropertyHelper.GetBool(podmiot, "JestOdbiorca"),
                    address = szczegoly != null ? new
                    {
                        city = DynamicPropertyHelper.GetString(szczegoly, "Miejscowosc"),
                        street = DynamicPropertyHelper.GetString(szczegoly, "Ulica"),
                        houseNumber = DynamicPropertyHelper.GetString(szczegoly, "NumerDomu"),
                        postalCode = DynamicPropertyHelper.GetString(szczegoly, "KodPocztowy"),
                        country = DynamicPropertyHelper.GetString(szczegoly, "Kraj"),
                    } : null,
                    paymentDeadlineDays = DynamicPropertyHelper.GetNullableInt(podmiot, "TerminPlatnosci"),
                    creditLimit = DynamicPropertyHelper.GetNullableDecimal(podmiot, "LimitKredytu"),
                };

                return McpToolHelper.Success(dto);
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MCP GetCustomerDetails error");
            return McpToolHelper.Error("Failed to get customer details", ex.Message);
        }
    }

    [McpServerTool, Description("Create a new customer (kontrahent) in Nexo. Returns the created customer's details.")]
    public async Task<string> CreateCustomer(
        [Description("Short name (NazwaSkrocona) - required")] string shortName,
        [Description("NIP (tax identification number)")] string? nip = null,
        [Description("Customer type: Firma or Osoba")] string? type = null,
        [Description("Email address")] string? email = null,
        [Description("Phone number")] string? phone = null,
        [Description("City")] string? city = null,
        [Description("Street with house number")] string? street = null,
        [Description("Postal code")] string? postalCode = null)
    {
        try
        {
            return await sferaService.ExecuteWithLockAsync(() =>
            {
                var podmioty = sferaService.GetManager("Podmioty");
                if (podmioty == null) return McpToolHelper.Error("Podmioty manager unavailable");

                using var nowyPodmiot = podmioty.Utworz();
                dynamic dane = nowyPodmiot.Dane;

                dane.NazwaSkrocona = shortName;
                dane.NazwaPelna = shortName;

                if (!string.IsNullOrEmpty(nip))
                    dane.NIP = nip;

                if (!string.IsNullOrEmpty(type))
                {
                    try
                    {
                        if (type.Equals("Osoba", StringComparison.OrdinalIgnoreCase))
                            dane.Typ = 1; // OsobaFizyczna
                    }
                    catch { }
                }

                // Set address
                if (!string.IsNullOrEmpty(city) || !string.IsNullOrEmpty(street) || !string.IsNullOrEmpty(postalCode))
                {
                    try
                    {
                        var adres = DynamicPropertyHelper.GetProperty(dane, "AdresPodstawowy");
                        if (adres != null)
                        {
                            var szczegoly = DynamicPropertyHelper.GetProperty(adres, "Szczegoly");
                            if (szczegoly != null)
                            {
                                if (!string.IsNullOrEmpty(city))
                                    DynamicPropertyHelper.TrySetProperty(szczegoly, "Miejscowosc", city);
                                if (!string.IsNullOrEmpty(street))
                                    DynamicPropertyHelper.TrySetProperty(szczegoly, "Ulica", street);
                                if (!string.IsNullOrEmpty(postalCode))
                                    DynamicPropertyHelper.TrySetProperty(szczegoly, "KodPocztowy", postalCode);
                            }
                        }
                    }
                    catch { }
                }

                // Set contacts
                if (!string.IsNullOrEmpty(email))
                {
                    try
                    {
                        var kontakt = nowyPodmiot.Kontakty.DodajEmail(email);
                        if (kontakt != null) kontakt.Dane.Glowny = true;
                    }
                    catch { }
                }

                if (!string.IsNullOrEmpty(phone))
                {
                    try
                    {
                        var kontakt = nowyPodmiot.Kontakty.DodajTelefon(phone);
                        if (kontakt != null) kontakt.Dane.Glowny = true;
                    }
                    catch { }
                }

                if ((bool)nowyPodmiot.Zapisz())
                {
                    var created = new
                    {
                        id = DynamicPropertyHelper.GetId(dane),
                        symbol = DynamicPropertyHelper.GetString(dane, "Symbol"),
                        shortName = DynamicPropertyHelper.GetString(dane, "NazwaSkrocona"),
                        nip = DynamicPropertyHelper.GetString(dane, "NIP"),
                    };
                    return McpToolHelper.Success(created, "Customer created successfully");
                }

                // Collect validation errors
                var errors = new List<string>();
                try
                {
                    foreach (var err in nowyPodmiot.WypiszBledy())
                        errors.Add(err.ToString());
                }
                catch { }

                return McpToolHelper.Error("Failed to create customer", string.Join("; ", errors));
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MCP CreateCustomer error");
            return McpToolHelper.Error("Failed to create customer", ex.Message);
        }
    }
}
