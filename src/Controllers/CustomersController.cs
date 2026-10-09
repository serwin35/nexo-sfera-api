using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Filters;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Requests;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using System.Diagnostics;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Customers (Kontrahenci) management endpoints
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Tags("Customers")]
public class CustomersController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<CustomersController> _logger;

    public CustomersController(ISferaService sferaService, ILogger<CustomersController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Diagnostic endpoint to inspect Sfera Uchwyt available members
    /// </summary>
    [HttpGet("debug/sfera-info")]
    [DevelopmentOnly]
    public async Task<ActionResult<object>> GetSferaInfo()
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                object sferaObj = _sferaService.GetSfera();
                Type type = sferaObj.GetType();

                var methodNames = new List<string>();
                foreach (var m in type.GetMethods())
                {
                    if (!methodNames.Contains(m.Name))
                        methodNames.Add(m.Name);
                }
                methodNames.Sort();

                var propertyNames = new List<string>();
                foreach (var p in type.GetProperties())
                {
                    if (!propertyNames.Contains(p.Name))
                        propertyNames.Add(p.Name);
                }
                propertyNames.Sort();

                return (object)new
                {
                    TypeName = type.FullName,
                    Methods = methodNames,
                    Properties = propertyNames
                };
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// Test accessing Podmioty (customers) manager
    /// </summary>
    [HttpGet("debug/test-podmioty")]
    [DevelopmentOnly]
    public async Task<ActionResult<object>> TestPodmioty()
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                dynamic sfera = _sferaService.GetSfera();
                var results = new Dictionary<string, object>();

                // Find Podmioty type from loaded assemblies
                Type? podmiotyType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        if (asm.FullName != null && asm.FullName.Contains("InsERT.Moria.Klienci"))
                        {
                            podmiotyType = asm.GetType("InsERT.Moria.Klienci.Podmioty");
                            if (podmiotyType != null) break;
                        }
                    }
                    catch { }
                }

                if (podmiotyType == null)
                {
                    return (object)new { Error = "Podmioty type not found in loaded assemblies" };
                }

                results["PodmiotyTypeFound"] = podmiotyType.FullName ?? "unknown";

                // Check PodajObiektTypu method signatures
                object sferaObj = _sferaService.GetSfera();
                Type sferaType = sferaObj.GetType();
                var podajMethods = new List<object>();
                foreach (var m in sferaType.GetMethods())
                {
                    if (m.Name == "PodajObiektTypu")
                    {
                        var paramList = new List<string>();
                        foreach (var p in m.GetParameters())
                        {
                            paramList.Add($"{p.ParameterType.Name} {p.Name}");
                        }
                        var paramInfo = string.Join(", ", paramList);
                        podajMethods.Add(new {
                            Signature = $"{m.Name}({paramInfo}) -> {m.ReturnType.Name}",
                            IsGeneric = m.IsGenericMethod,
                            GenericArgCount = m.IsGenericMethod ? m.GetGenericArguments().Length : 0,
                            ParamCount = m.GetParameters().Length
                        });
                    }
                }
                results["PodajObiektTypuSignatures"] = podajMethods;

                // Try to get Podmioty manager using generic method
                dynamic? podmioty = null;
                try
                {
                    // First try: find generic method with 0 parameters
                    System.Reflection.MethodInfo? genericMethod = null;
                    foreach (var m in sferaType.GetMethods())
                    {
                        if (m.Name == "PodajObiektTypu" && m.IsGenericMethod && m.GetParameters().Length == 0)
                        {
                            genericMethod = m;
                            break;
                        }
                    }

                    if (genericMethod != null)
                    {
                        var concreteMethod = genericMethod.MakeGenericMethod(podmiotyType);
                        podmioty = concreteMethod.Invoke(sferaObj, null);
                        results["MethodUsed"] = "Generic<T>() with 0 params";
                    }
                    else
                    {
                        // Second try: find method that takes Type parameter
                        System.Reflection.MethodInfo? typeParamMethod = null;
                        foreach (var m in sferaType.GetMethods())
                        {
                            if (m.Name == "PodajObiektTypu" && !m.IsGenericMethod && m.GetParameters().Length == 1)
                            {
                                var paramType = m.GetParameters()[0].ParameterType;
                                if (paramType == typeof(Type) || paramType.Name == "Type")
                                {
                                    typeParamMethod = m;
                                    break;
                                }
                            }
                        }

                        if (typeParamMethod != null)
                        {
                            podmioty = typeParamMethod.Invoke(sferaObj, new object[] { podmiotyType });
                            results["MethodUsed"] = "PodajObiektTypu(Type)";
                        }
                        else
                        {
                            results["MethodUsed"] = "No suitable method found";
                        }
                    }

                    if (podmioty != null)
                    {
                        Type mgrType = podmioty.GetType();
                        var mgrMethods = new List<string>();
                        foreach (var m in mgrType.GetMethods())
                        {
                            if (!mgrMethods.Contains(m.Name))
                                mgrMethods.Add(m.Name);
                        }
                        mgrMethods.Sort();

                        var mgrProps = new List<string>();
                        foreach (var p in mgrType.GetProperties())
                        {
                            if (!mgrProps.Contains(p.Name))
                                mgrProps.Add(p.Name);
                        }
                        mgrProps.Sort();

                        results["PodmiotyManager"] = new {
                            Found = true,
                            Type = mgrType.FullName,
                            Methods = mgrMethods,
                            Properties = mgrProps
                        };

                        // Try to access Dane property (common pattern)
                        try
                        {
                            var dane = podmioty.Dane;
                            if (dane != null)
                            {
                                Type daneType = dane.GetType();
                                var daneMethods = new List<string>();
                                foreach (var m in daneType.GetMethods())
                                {
                                    if (!daneMethods.Contains(m.Name))
                                        daneMethods.Add(m.Name);
                                }
                                daneMethods.Sort();

                                results["PodmiotyDane"] = new {
                                    Found = true,
                                    Type = daneType.FullName,
                                    Methods = daneMethods
                                };
                            }
                        }
                        catch (Exception ex)
                        {
                            results["PodmiotyDane"] = new { Found = false, Error = ex.Message };
                        }
                    }
                    else
                    {
                        results["PodmiotyManager"] = new { Found = false, Error = "podmioty object is null" };
                    }
                }
                catch (Exception ex)
                {
                    results["PodmiotyManager"] = new { Found = false, Error = ex.Message };
                }

                return (object)results;
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = ex.Message, Stack = ex.StackTrace });
        }
    }

    /// <summary>
    /// Explore Szczegoly (detailed address) properties
    /// </summary>
    [HttpGet("debug/address-details/{id}")]
    [DevelopmentOnly]
    public async Task<ActionResult<object>> GetAddressSzczegoly(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var podmioty = _sferaService.GetManager("Podmioty");
                if (podmioty == null)
                {
                    return (Status: 500, Data: (object)new { Error = "Failed to get Podmioty manager" });
                }

                dynamic? podmiot = null;
                foreach (var p in DynamicPropertyHelper.SafeGetAll((object)podmioty))
                {
                    if (DynamicPropertyHelper.GetId(p) == id)
                    {
                        podmiot = p;
                        break;
                    }
                }

                if (podmiot == null)
                {
                    return (Status: 404, Data: (object)new { Error = $"Customer {id} not found" });
                }

                var adresPodmiotu = DynamicPropertyHelper.GetProperty(podmiot, "AdresPodstawowy");
                if (adresPodmiotu == null)
                {
                    return (Status: 200, Data: (object)new { CustomerId = id, AdresPodstawowy = "null" });
                }

                var szczegoly = DynamicPropertyHelper.GetProperty(adresPodmiotu, "Szczegoly");
                if (szczegoly == null)
                {
                    return (Status: 200, Data: (object)new {
                        CustomerId = id,
                        Szczegoly = "null",
                        Linia1 = DynamicPropertyHelper.GetString(adresPodmiotu, "Linia1"),
                        Linia2 = DynamicPropertyHelper.GetString(adresPodmiotu, "Linia2"),
                        LiniaCalosc = DynamicPropertyHelper.GetString(adresPodmiotu, "LiniaCalosc")
                    });
                }

                Type szczegolyType = szczegoly.GetType();
                var props = new Dictionary<string, string>();
                foreach (var prop in szczegolyType.GetProperties())
                {
                    try
                    {
                        var value = prop.GetValue(szczegoly);
                        props[prop.Name] = value?.ToString() ?? "null";
                    }
                    catch (Exception ex)
                    {
                        props[prop.Name] = $"Error: {ex.Message}";
                    }
                }

                return (Status: 200, Data: (object)new
                {
                    CustomerId = id,
                    SzczegolyType = szczegolyType.FullName,
                    Properties = props.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value)
                });
            });

            return result.Status switch
            {
                404 => NotFound(result.Data),
                500 => StatusCode(500, result.Data),
                _ => Ok(result.Data)
            };
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// Explore AdresPodstawowy properties for a customer
    /// </summary>
    [HttpGet("debug/address/{id}")]
    [DevelopmentOnly]
    public async Task<ActionResult<object>> GetAddressDetails(int id)
    {
        try
        {
            var lockResult = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var podmioty = _sferaService.GetManager("Podmioty");
                if (podmioty == null)
                {
                    return (Status: 500, Data: (object)new { Error = "Failed to get Podmioty manager" });
                }

                dynamic? podmiot = null;
                foreach (var p in DynamicPropertyHelper.SafeGetAll((object)podmioty))
                {
                    if (DynamicPropertyHelper.GetId(p) == id)
                    {
                        podmiot = p;
                        break;
                    }
                }

                if (podmiot == null)
                {
                    return (Status: 404, Data: (object)new { Error = $"Customer {id} not found" });
                }

                var result = new Dictionary<string, object>();
                result["CustomerId"] = id;
                result["CustomerName"] = DynamicPropertyHelper.GetString(podmiot, "NazwaSkrocona") ?? "";

                // Check AdresPodstawowy
                var adresPodstawowy = DynamicPropertyHelper.GetProperty(podmiot, "AdresPodstawowy");
                if (adresPodstawowy != null)
                {
                    Type adresType = adresPodstawowy.GetType();
                    var adresProps = new Dictionary<string, string>();
                    foreach (var prop in adresType.GetProperties())
                    {
                        try
                        {
                            var value = prop.GetValue(adresPodstawowy);
                            adresProps[prop.Name] = value?.ToString() ?? "null";
                        }
                        catch (Exception ex)
                        {
                            adresProps[prop.Name] = $"Error: {ex.Message}";
                        }
                    }
                    result["AdresPodstawowy"] = new
                    {
                        Type = adresType.FullName,
                        Properties = adresProps.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value)
                    };
                }
                else
                {
                    result["AdresPodstawowy"] = "null";
                }

                // Check Adresy collection
                var adresy = DynamicPropertyHelper.GetCollection((object)podmiot, "Adresy");
                var adresyList = new List<object>();
                foreach (var adr in adresy)
                {
                    Type adrType = adr.GetType();
                    var adrProps = new Dictionary<string, string>();
                    foreach (var prop in adrType.GetProperties())
                    {
                        try
                        {
                            var value = prop.GetValue(adr);
                            if (value != null && !prop.PropertyType.IsPrimitive && prop.PropertyType != typeof(string))
                            {
                                adrProps[prop.Name] = $"[{value.GetType().Name}]";
                            }
                            else
                            {
                                adrProps[prop.Name] = value?.ToString() ?? "null";
                            }
                        }
                        catch
                        {
                            adrProps[prop.Name] = "Error reading";
                        }
                    }
                    adresyList.Add(new
                    {
                        Type = adrType.FullName,
                        Properties = adrProps.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value)
                    });
                }
                result["Adresy"] = adresyList;
                result["AdresyCount"] = adresyList.Count;

                return (Status: 200, Data: (object)result);
            });

            return lockResult.Status switch
            {
                404 => NotFound(lockResult.Data),
                500 => StatusCode(500, lockResult.Data),
                _ => Ok(lockResult.Data)
            };
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = ex.Message, Stack = ex.StackTrace });
        }
    }

    /// <summary>
    /// Test loading customer with Znajdz (gets full entity with relations)
    /// </summary>
    [HttpGet("debug/znajdz/{id}")]
    [DevelopmentOnly]
    public async Task<ActionResult<object>> TestZnajdz(int id)
    {
        try
        {
            var lockResult = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var podmioty = _sferaService.GetManager("Podmioty");
                if (podmioty == null)
                {
                    return (Status: 500, Data: (object)new { Error = "Failed to get Podmioty manager" });
                }

                // First find the entity
                dynamic? podmiotDane = null;
                foreach (var p in DynamicPropertyHelper.SafeGetAll((object)podmioty))
                {
                    if (DynamicPropertyHelper.GetId(p) == id)
                    {
                        podmiotDane = p;
                        break;
                    }
                }

                if (podmiotDane == null)
                {
                    return (Status: 404, Data: (object)new { Error = $"Customer {id} not found" });
                }

                // Use Znajdz to get full business object
                using (var podmiotBO = podmioty.Znajdz(podmiotDane))
                {
                    if (podmiotBO == null)
                    {
                        return (Status: 404, Data: (object)new { Error = $"Znajdz returned null for customer {id}" });
                    }

                    var result = new Dictionary<string, object>();

                    // Get Dane property
                    dynamic dane = podmiotBO.Dane;
                    Type daneType = dane.GetType();

                    result["BusinessObjectType"] = podmiotBO.GetType().FullName ?? "unknown";
                    result["DaneType"] = daneType.FullName ?? "unknown";

                    // List all properties on Dane
                    var daneProps = new Dictionary<string, object>();
                    foreach (var prop in daneType.GetProperties())
                    {
                        try
                        {
                            var value = prop.GetValue(dane);
                            if (value == null)
                            {
                                daneProps[prop.Name] = "null";
                            }
                            else if (prop.PropertyType.IsPrimitive || prop.PropertyType == typeof(string) || prop.PropertyType == typeof(DateTime) || prop.PropertyType == typeof(decimal))
                            {
                                daneProps[prop.Name] = value.ToString() ?? "null";
                            }
                            else
                            {
                                daneProps[prop.Name] = $"[{value.GetType().Name}]";
                            }
                        }
                        catch (Exception ex)
                        {
                            daneProps[prop.Name] = $"Error: {ex.Message}";
                        }
                    }
                    result["DaneProperties"] = daneProps.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);

                    // Try to get address
                    try
                    {
                        var adresGlowny = DynamicPropertyHelper.GetProperty(dane, "AdresGlowny");
                        if (adresGlowny != null)
                        {
                            result["AdresGlowny"] = new
                            {
                                Ulica = DynamicPropertyHelper.GetString(adresGlowny, "Ulica"),
                                NumerDomu = DynamicPropertyHelper.GetString(adresGlowny, "NumerDomu"),
                                Miejscowosc = DynamicPropertyHelper.GetString(adresGlowny, "Miejscowosc"),
                                KodPocztowy = DynamicPropertyHelper.GetString(adresGlowny, "KodPocztowy")
                            };
                        }
                        else
                        {
                            result["AdresGlowny"] = "null";
                        }
                    }
                    catch (Exception ex)
                    {
                        result["AdresGlowny"] = $"Error: {ex.Message}";
                    }

                    // Try Adresy collection on business object
                    try
                    {
                        var adresy = DynamicPropertyHelper.GetProperty(podmiotBO, "Adresy");
                        if (adresy != null)
                        {
                            var adresyList = new List<object>();
                            foreach (var adr in (dynamic)adresy)
                            {
                                adresyList.Add(new
                                {
                                    Id = DynamicPropertyHelper.GetId(adr),
                                    Ulica = DynamicPropertyHelper.GetString(adr, "Ulica"),
                                    Miejscowosc = DynamicPropertyHelper.GetString(adr, "Miejscowosc")
                                });
                            }
                            result["AdresyFromBO"] = adresyList;
                        }
                        else
                        {
                            result["AdresyFromBO"] = "null";
                        }
                    }
                    catch (Exception ex)
                    {
                        result["AdresyFromBO"] = $"Error: {ex.Message}";
                    }

                    // Try Kontakty
                    try
                    {
                        var kontakty = DynamicPropertyHelper.GetProperty(podmiotBO, "Kontakty");
                        if (kontakty != null)
                        {
                            result["KontaktyType"] = kontakty.GetType().FullName;
                        }
                        else
                        {
                            result["Kontakty"] = "null";
                        }
                    }
                    catch (Exception ex)
                    {
                        result["Kontakty"] = $"Error: {ex.Message}";
                    }

                    return (Status: 200, Data: (object)result);
                }
            });

            return lockResult.Status switch
            {
                404 => NotFound(lockResult.Data),
                500 => StatusCode(500, lockResult.Data),
                _ => Ok(lockResult.Data)
            };
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = ex.Message, Stack = ex.StackTrace });
        }
    }

    /// <summary>
    /// Explore properties of a single Podmiot entity
    /// </summary>
    [HttpGet("debug/podmiot-properties/{id}")]
    [DevelopmentOnly]
    public async Task<ActionResult<object>> GetPodmiotProperties(int id)
    {
        try
        {
            var lockResult = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var podmioty = _sferaService.GetManager("Podmioty");
                if (podmioty == null)
                {
                    return (Status: 500, Data: (object)new { Error = "Failed to get Podmioty manager" });
                }

                dynamic? podmiot = null;
                foreach (var p in DynamicPropertyHelper.SafeGetAll((object)podmioty))
                {
                    if (DynamicPropertyHelper.GetId(p) == id)
                    {
                        podmiot = p;
                        break;
                    }
                }

                if (podmiot == null)
                {
                    return (Status: 404, Data: (object)new { Error = $"Customer {id} not found" });
                }

                Type podmiotType = podmiot.GetType();
                var properties = new Dictionary<string, object>();

                foreach (var prop in podmiotType.GetProperties())
                {
                    try
                    {
                        var value = prop.GetValue(podmiot);
                        var valueType = value?.GetType().Name ?? "null";

                        // For collections, get count
                        if (value != null && value.GetType().Name.Contains("Collection"))
                        {
                            try
                            {
                                int count = 0;
                                foreach (var _ in (dynamic)value) count++;
                                properties[prop.Name] = new { Type = valueType, Count = count };
                            }
                            catch
                            {
                                properties[prop.Name] = new { Type = valueType, Value = "Collection (error reading)" };
                            }
                        }
                        else if (value != null && !prop.PropertyType.IsPrimitive && prop.PropertyType != typeof(string) && prop.PropertyType != typeof(DateTime) && prop.PropertyType != typeof(decimal))
                        {
                            properties[prop.Name] = new { Type = valueType, Value = "Complex object" };
                        }
                        else
                        {
                            properties[prop.Name] = new { Type = valueType, Value = value?.ToString() ?? "null" };
                        }
                    }
                    catch (Exception ex)
                    {
                        properties[prop.Name] = new { Error = ex.Message };
                    }
                }

                return (Status: 200, Data: (object)new
                {
                    Id = id,
                    EntityType = podmiotType.FullName,
                    PropertyCount = properties.Count,
                    Properties = properties.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value)
                });
            });

            return lockResult.Status switch
            {
                404 => NotFound(lockResult.Data),
                500 => StatusCode(500, lockResult.Data),
                _ => Ok(lockResult.Data)
            };
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = ex.Message, Stack = ex.StackTrace });
        }
    }

    /// <summary>
    /// Test GetManager method directly
    /// </summary>
    [HttpGet("debug/test-getmanager")]
    [DevelopmentOnly]
    public async Task<ActionResult<object>> TestGetManager()
    {
        try
        {
            var results = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var data = new Dictionary<string, object>();

                // Test each manager
                var managersToTest = new[] { "Podmioty", "Asortymenty", "Dokumenty", "Magazyny" };

                foreach (var managerName in managersToTest)
                {
                    try
                    {
                        var manager = _sferaService.GetManager(managerName);
                        if (manager != null)
                        {
                            Type mgrType = manager.GetType();
                            data[managerName] = new
                            {
                                Success = true,
                                Type = mgrType.FullName,
                                HasDane = mgrType.GetProperty("Dane") != null
                            };
                        }
                        else
                        {
                            data[managerName] = new { Success = false, Error = "Returned null" };
                        }
                    }
                    catch (Exception ex)
                    {
                        data[managerName] = new { Success = false, Error = ex.Message, ExType = ex.GetType().Name };
                    }
                }

                return (object)data;
            });

            return Ok(results);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = ex.Message, Stack = ex.StackTrace });
        }
    }

    /// <summary>
    /// Debug endpoint to explore all properties of a customer entity
    /// </summary>
    [HttpGet("debug/properties/{id}")]
    [DevelopmentOnly]
    public async Task<ActionResult<object>> GetCustomerProperties(int id)
    {
        try
        {
            var lockResult = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var podmioty = _sferaService.GetManager("Podmioty");
                if (podmioty == null)
                {
                    return (Status: 500, Data: (object)new { Error = "Failed to get Podmioty manager" });
                }

                dynamic? podmiot = null;
                foreach (var p in DynamicPropertyHelper.SafeGetAll((object)podmioty))
                {
                    if (DynamicPropertyHelper.GetId(p) == id)
                    {
                        podmiot = p;
                        break;
                    }
                }

                if (podmiot == null)
                {
                    return (Status: 404, Data: (object)new { Error = $"Customer {id} not found" });
                }

                Type podmiotType = podmiot.GetType();
                var properties = new SortedDictionary<string, object?>();

                foreach (var prop in podmiotType.GetProperties())
                {
                    try
                    {
                        var value = prop.GetValue(podmiot);
                        if (value == null)
                        {
                            properties[prop.Name] = null;
                        }
                        else if (prop.PropertyType.IsPrimitive || prop.PropertyType == typeof(string) ||
                                 prop.PropertyType == typeof(DateTime) || prop.PropertyType == typeof(decimal) ||
                                 Nullable.GetUnderlyingType(prop.PropertyType) != null)
                        {
                            properties[prop.Name] = value;
                        }
                        else if (value.GetType().Name.Contains("Collection"))
                        {
                            int count = 0;
                            try { foreach (var _ in (dynamic)value) count++; } catch { }
                            properties[prop.Name] = $"[Collection: {count} items]";
                        }
                        else
                        {
                            properties[prop.Name] = $"[{value.GetType().Name}]";
                        }
                    }
                    catch (Exception ex)
                    {
                        properties[prop.Name] = $"Error: {ex.Message}";
                    }
                }

                return (Status: 200, Data: (object)new
                {
                    Id = id,
                    EntityType = podmiotType.FullName,
                    PropertyCount = properties.Count,
                    Properties = properties
                });
            });

            return lockResult.Status switch
            {
                404 => NotFound(lockResult.Data),
                500 => StatusCode(500, lockResult.Data),
                _ => Ok(lockResult.Data)
            };
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = ex.Message, Stack = ex.StackTrace });
        }
    }

    /// <summary>
    /// Get all customers with optional filtering
    /// </summary>
    /// <remarks>
    /// full=false (default): light items (id, name, taxId, phone, isActive, contractorType), unchanged.
    ///
    /// full=true: the full contractor card per item (CustomerFullListItemDto = CustomerDto + name): symbol, shortName,
    /// fullName, email/emails, phone, website, nip/taxId, regon, address, deliveryAddress, credit limits, payment terms,
    /// bank account, consents, documentBlock/messageText, ... Filters, sorting (by id) and paging run in SQL; pageSize
    /// must be 1..200 (each card costs several lazy loads on the single SDK thread).
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<CustomerFullListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<CustomerListItemDto>>> GetCustomers([FromQuery] CustomerQueryRequest query)
    {
        if (query.Full)
        {
            return await GetCustomersFullAsync(query);
        }

        try
        {
            var response = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var podmioty = _sferaService.GetManager("Podmioty");
                if (podmioty == null)
                {
                    return (PagedResponse<CustomerListItemDto>?)null;
                }

                var allPodmioty = new List<object>();
                foreach (var p in DynamicPropertyHelper.SafeGetAll((object)podmioty))
                {
                    allPodmioty.Add(p);
                }

                // Apply filters using foreach to avoid LINQ issues with dynamic types
                var filteredList = new List<object>();

                foreach (var p in allPodmioty)
                {
                    // Filter by active status
                    if (query.ActiveOnly)
                    {
                        var isActive = DynamicPropertyHelper.GetNullableBool(p, "Aktywny") ?? true;
                        if (!isActive) continue;
                    }

                    // Filter by contractors only
                    if (query.ContractorsOnly)
                    {
                        var isContractor = DynamicPropertyHelper.GetNullableBool(p, "Kontrahent") ?? false;
                        if (!isContractor) continue;
                    }

                    // Search filter
                    if (!string.IsNullOrEmpty(query.Search))
                    {
                        var nazwaSkrocona = (DynamicPropertyHelper.GetString(p, "NazwaSkrocona") ?? "").ToLower();
                        var nip = (DynamicPropertyHelper.GetString(p, "NIP") ?? "").ToLower();
                        var symbol = (DynamicPropertyHelper.GetString(p, "Symbol") ?? "").ToLower();
                        var searchLower = query.Search.ToLower();

                        if (!nazwaSkrocona.Contains(searchLower) &&
                            !nip.Contains(searchLower) &&
                            !symbol.Contains(searchLower))
                        {
                            continue;
                        }
                    }

                    // Type filter
                    if (query.Type.HasValue)
                    {
                        var podType = DynamicPropertyHelper.GetNullableInt(p, "Typ");
                        // Podmiot.Typ holds TypObiektu: Firma = 2, Osoba = 1 (comparing with 0 never matched a company).
                        var expectedType = query.Type.Value == CustomerType.Company ? 2 : 1;
                        if (podType != expectedType) continue;
                    }

                    // Contractor type filter
                    if (query.ContractorType.HasValue)
                    {
                        var rodzaj = DynamicPropertyHelper.GetNullableInt(p, "RodzajKontrahenta") ?? 0;
                        if (rodzaj != (int)query.ContractorType.Value) continue;
                    }

                    // Document block filter
                    if (query.HasDocumentBlock.HasValue)
                    {
                        var hasBlock = DynamicPropertyHelper.GetNullableBool(p, "BlokadaWystawianiaDokumentow") ?? false;
                        if (hasBlock != query.HasDocumentBlock.Value) continue;
                    }

                    // EU taxpayer filter
                    if (query.IsEuTaxpayer.HasValue)
                    {
                        var isEu = DynamicPropertyHelper.GetNullableBool(p, "PodatnikUE") ?? false;
                        if (isEu != query.IsEuTaxpayer.Value) continue;
                    }

                    filteredList.Add(p);
                }

                var totalCount = filteredList.Count;
                var pagedPodmioty = filteredList
                    .Skip((query.Page - 1) * query.PageSize)
                    .Take(query.PageSize)
                    .ToList();

                var items = new List<CustomerListItemDto>();
                foreach (var p in pagedPodmioty)
                {
                    items.Add(MapToListItemDto(p));
                }

                return new PagedResponse<CustomerListItemDto>
                {
                    Data = items,
                    Page = query.Page,
                    PageSize = query.PageSize,
                    TotalCount = totalCount
                };
            });

            if (response == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Podmioty manager"));
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting customers");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving customers", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// GET /api/customers?full=true: filters, count, order and page in SQL, then the full card of the page rows only.
    /// </summary>
    private async Task<ActionResult> GetCustomersFullAsync(CustomerQueryRequest query)
    {
        if (query.Page < 1)
        {
            return BadRequest(ApiResponse<object>.Error("page must be >= 1"));
        }

        if (query.PageSize < 1 || query.PageSize > CustomerReader.MaxFullPageSize)
        {
            return BadRequest(ApiResponse<object>.Error($"pageSize must be between 1 and {CustomerReader.MaxFullPageSize} when full=true"));
        }

        try
        {
            var stopwatch = Stopwatch.StartNew();
            var response = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var sfera = _sferaService.GetSfera();
                var filtered = CustomerReader.Query(sfera, query);
                var totalCount = filtered.Count();
                var pageRows = filtered
                    .OrderBy(p => p.Id)
                    .Skip((query.Page - 1) * query.PageSize)
                    .Take(query.PageSize)
                    .ToList();

                var items = new List<CustomerFullListItemDto>(pageRows.Count);
                foreach (var podmiot in pageRows)
                {
                    try
                    {
                        var item = MapToDtoAs<CustomerFullListItemDto>(podmiot);
                        item.Name = item.ShortName;
                        items.Add(item);
                    }
                    catch (Exception ex)
                    {
                        // Never drop a contractor from a sync page: fall back to the light fields.
                        _logger.LogWarning(ex, "Full card of customer {Id} could not be mapped; returning the light fields", podmiot.Id);
                        items.Add(new CustomerFullListItemDto
                        {
                            Id = podmiot.Id,
                            Name = podmiot.NazwaSkrocona ?? string.Empty,
                            ShortName = podmiot.NazwaSkrocona ?? string.Empty,
                            NIP = podmiot.NIP,
                            Phone = podmiot.Telefon,
                            IsActive = podmiot.Aktywny,
                            ContractorType = (ContractorType)podmiot.RodzajKontrahenta,
                        });
                    }
                }

                return new PagedResponse<CustomerFullListItemDto>
                {
                    Data = items,
                    Page = query.Page,
                    PageSize = query.PageSize,
                    TotalCount = totalCount,
                };
            });

            _logger.LogInformation("GET customers full=true page {Page} size {PageSize}: {Count}/{Total} cards in {ElapsedMs} ms",
                query.Page, query.PageSize, response.Data.Count, response.TotalCount, stopwatch.ElapsedMilliseconds);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting customers (full)");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving customers", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get customer by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<CustomerDto>>> GetCustomer(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var podmioty = _sferaService.GetManager("Podmioty");
                if (podmioty == null)
                {
                    return (Found: false, ManagerMissing: true, Dto: (CustomerDto?)null);
                }

                dynamic? podmiot = null;
                foreach (var p in DynamicPropertyHelper.SafeGetAll((object)podmioty))
                {
                    if (DynamicPropertyHelper.GetId(p) == id)
                    {
                        podmiot = p;
                        break;
                    }
                }

                if (podmiot == null)
                {
                    return (Found: false, ManagerMissing: false, Dto: (CustomerDto?)null);
                }

                return (Found: true, ManagerMissing: false, Dto: (CustomerDto?)MapToDto(podmiot));
            });

            if (result.ManagerMissing)
            {
                return StatusCode(500, ApiResponse<CustomerDto>.Error("Failed to get Podmioty manager"));
            }

            if (!result.Found)
            {
                return NotFound(ApiResponse<CustomerDto>.Error($"Customer with ID {id} not found"));
            }

            return Ok(ApiResponse<CustomerDto>.Ok(result.Dto!));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting customer {Id}", id);
            return StatusCode(500, ApiResponse<CustomerDto>.Error("Error retrieving customer", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get customer by NIP
    /// </summary>
    [HttpGet("by-nip/{nip}")]
    public async Task<ActionResult<ApiResponse<CustomerDto>>> GetCustomerByNip(string nip)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var podmioty = _sferaService.GetManager("Podmioty");
                if (podmioty == null)
                {
                    return (Found: false, ManagerMissing: true, Dto: (CustomerDto?)null);
                }

                var cleanNip = nip.Replace("-", "").Replace(" ", "");
                dynamic? podmiot = null;
                foreach (var p in DynamicPropertyHelper.SafeGetAll((object)podmioty))
                {
                    var podmiotNip = DynamicPropertyHelper.GetString(p, "NIP") ?? "";
                    if (podmiotNip == cleanNip || podmiotNip == nip)
                    {
                        podmiot = p;
                        break;
                    }
                }

                if (podmiot == null)
                {
                    return (Found: false, ManagerMissing: false, Dto: (CustomerDto?)null);
                }

                return (Found: true, ManagerMissing: false, Dto: (CustomerDto?)MapToDto(podmiot));
            });

            if (result.ManagerMissing)
            {
                return StatusCode(500, ApiResponse<CustomerDto>.Error("Failed to get Podmioty manager"));
            }

            if (!result.Found)
            {
                return NotFound(ApiResponse<CustomerDto>.Error($"Customer with NIP {nip} not found"));
            }

            return Ok(ApiResponse<CustomerDto>.Ok(result.Dto!));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting customer by NIP {Nip}", nip);
            return StatusCode(500, ApiResponse<CustomerDto>.Error("Error retrieving customer", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create a new customer
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<CustomerDto>>> CreateCustomer([FromBody] CreateCustomerRequest request)
    {
        try
        {
            // Execute all SDK operations on the dedicated STA thread for EF6 thread-safety
            var result = await _sferaService.ExecuteWithLockAsync<(bool Success, CustomerDto? Data, string Message, List<string> Errors)>(() =>
            {
                var podmioty = _sferaService.GetManager("Podmioty");
                if (podmioty == null)
                {
                    return (false, null, "Failed to get Podmioty manager", new List<string>());
                }

                // Check if symbol already exists
                dynamic? existing = null;
                foreach (var p in DynamicPropertyHelper.SafeGetAll((object)podmioty))
                {
                    if (DynamicPropertyHelper.GetString(p, "Symbol") == request.Symbol)
                    {
                        existing = p;
                        break;
                    }
                }
                if (existing != null)
                {
                    return (false, null, $"Customer with symbol {request.Symbol} already exists", new List<string>());
                }

                // Get default configuration - based on working examples
                var konfiguracje = _sferaService.GetManager("Konfiguracje");
                dynamic? konfig = null;
                try
                {
                    konfig = konfiguracje?.DaneDomyslne?.Kontrahent;
                }
                catch { /* Configuration might not exist */ }

                using (var nowyPodmiot = konfig != null ? podmioty.Utworz(konfig) : podmioty.Utworz())
                {
                    dynamic dane = nowyPodmiot.Dane;
                    dane.Symbol = request.Symbol;
                    dane.NazwaSkrocona = request.ShortName;
                    dane.NazwaPelna = request.FullName ?? request.ShortName;

                    if (!string.IsNullOrEmpty(request.NIP))
                    {
                        dane.NIP = request.NIP.Replace("-", "").Replace(" ", "");
                    }

                    if (!string.IsNullOrEmpty(request.REGON))
                    {
                        dane.REGON = request.REGON;
                    }

                    // Set customer type (0=Firmy, 1=Osoby)
                    dane.Typ = request.Type == CustomerType.Company ? (short)0 : (short)1;

                    // Set address
                    if (request.Address != null)
                    {
                        var adres = DynamicPropertyHelper.GetProperty(dane, "AdresGlowny");
                        if (adres != null)
                        {
                            adres.Ulica = request.Address.Street;
                            adres.NumerDomu = request.Address.BuildingNumber;
                            adres.NumerLokalu = request.Address.ApartmentNumber;
                            adres.Miejscowosc = request.Address.City;
                            adres.KodPocztowy = request.Address.PostalCode;
                        }
                    }

                    // Set contacts
                    if (!string.IsNullOrEmpty(request.Email))
                    {
                        try
                        {
                            var kontakt = nowyPodmiot.Kontakty.DodajEmail(request.Email);
                            if (kontakt != null)
                            {
                                kontakt.Dane.Glowny = true;
                            }
                        }
                        catch { /* Ignore contact errors */ }
                    }

                    if (!string.IsNullOrEmpty(request.Phone))
                    {
                        try
                        {
                            var kontakt = nowyPodmiot.Kontakty.DodajTelefon(request.Phone);
                            if (kontakt != null)
                            {
                                kontakt.Dane.Glowny = true;
                            }
                        }
                        catch { /* Ignore contact errors */ }
                    }

                    if (!string.IsNullOrEmpty(request.Website))
                    {
                        try
                        {
                            nowyPodmiot.Kontakty.DodajWww(request.Website);
                        }
                        catch { /* Ignore contact errors */ }
                    }

                    // Set bank account
                    if (!string.IsNullOrEmpty(request.BankAccount))
                    {
                        try
                        {
                            var rachunek = nowyPodmiot.RachunkiBankowe.Dodaj();
                            if (rachunek != null)
                            {
                                rachunek.Dane.NumerRachunku = request.BankAccount;
                                rachunek.Dane.NazwaBanku = request.BankName;
                                rachunek.Dane.Glowny = true;
                            }
                        }
                        catch { /* Ignore bank account errors */ }
                    }

                    if ((bool)nowyPodmiot.Zapisz())
                    {
                        var symbolLog = request.Symbol;
                        _logger.LogInformation("Created customer {Symbol}", symbolLog);
                        return (true, MapToDto(dane), "Customer created successfully", new List<string>());
                    }
                    else
                    {
                        var errors = GetBusinessObjectErrors(nowyPodmiot);
                        return (false, null, "Failed to create customer", errors);
                    }
                }
            });

            if (result.Success && result.Data != null)
            {
                return CreatedAtAction(nameof(GetCustomer), new { id = result.Data.Id }, ApiResponse<CustomerDto>.Ok(result.Data, result.Message));
            }
            else
            {
                return BadRequest(ApiResponse<CustomerDto>.Error(result.Message, result.Errors));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating customer");
            return StatusCode(500, ApiResponse<CustomerDto>.Error("Error creating customer", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Update an existing customer
    /// </summary>
    /// <remarks>
    /// Writable: shortName, fullName (companies: Firma.Nazwa), nip, regon (companies: Firma.REGON). Every other field of
    /// the request is rejected with 400 and nothing is saved (it used to be accepted and ignored). fullName/regon on a
    /// person are rejected with 400.
    /// </remarks>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResponse<CustomerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CustomerDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<CustomerDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CustomerDto>>> UpdateCustomer(int id, [FromBody] UpdateCustomerRequest request)
    {
        var unsupported = UnsupportedCustomerUpdateFields(request);
        if (unsupported.Count > 0)
        {
            return BadRequest(ApiResponse<CustomerDto>.Error(
                "Invalid customer update; nothing was saved",
                unsupported.Select(f => $"{f}: not supported by PUT /api/customers/{{id}} (writable: shortName, fullName, nip, regon)").ToList()));
        }

        try
        {
            // Execute all SDK operations on the dedicated STA thread for EF6 thread-safety
            var result = await _sferaService.ExecuteWithLockAsync<(bool Success, CustomerDto? Data, string Message, List<string> Errors, int StatusCode)>(() =>
            {
                var podmioty = _sferaService.GetManager("Podmioty");
                if (podmioty == null)
                {
                    return (false, null, "Failed to get Podmioty manager", new List<string>(), 500);
                }

                dynamic? podmiot = null;
                foreach (var p in DynamicPropertyHelper.SafeGetAll((object)podmioty))
                {
                    if (DynamicPropertyHelper.GetId(p) == id)
                    {
                        podmiot = p;
                        break;
                    }
                }

                if (podmiot == null)
                {
                    return (false, null, $"Customer with ID {id} not found", new List<string>(), 404);
                }

                using (var edytowanyPodmiot = podmioty.Znajdz(podmiot))
                {
                    if (edytowanyPodmiot == null)
                    {
                        return (false, null, $"Customer with ID {id} not found", new List<string>(), 404);
                    }

                    dynamic dane = edytowanyPodmiot.Dane;

                    if (!string.IsNullOrEmpty(request.ShortName))
                    {
                        dane.NazwaSkrocona = request.ShortName;
                    }

                    // Full name and REGON live on Firma (Podmiot has no NazwaPelna/REGON: assigning them threw
                    // RuntimeBinderException → 500).
                    var firma = ((Podmiot)edytowanyPodmiot.Dane).Firma;
                    var fieldErrors = new List<string>();

                    if (!string.IsNullOrEmpty(request.FullName))
                    {
                        if (firma != null) firma.Nazwa = request.FullName;
                        else fieldErrors.Add("fullName: only companies have a full name in Nexo; for a person change the first/last name in Subiekt");
                    }

                    if (!string.IsNullOrEmpty(request.NIP))
                    {
                        dane.NIP = request.NIP.Replace("-", "").Replace(" ", "");
                    }

                    if (!string.IsNullOrEmpty(request.REGON))
                    {
                        if (firma != null) firma.REGON = request.REGON;
                        else fieldErrors.Add("regon: only companies have a REGON in Nexo");
                    }

                    if (fieldErrors.Count > 0)
                    {
                        // Not saved: disposing the business object discards the changes.
                        return (false, null, "Invalid customer update; nothing was saved", fieldErrors, 400);
                    }

                    if ((bool)edytowanyPodmiot.Zapisz())
                    {
                        _logger.LogInformation("Updated customer {Id}", id);
                        return (true, MapToDto(dane), "Customer updated successfully", new List<string>(), 200);
                    }
                    else
                    {
                        var errors = GetBusinessObjectErrors(edytowanyPodmiot);
                        return (false, null, "Failed to update customer", errors, 400);
                    }
                }
            });

            if (result.Success && result.Data != null)
            {
                return Ok(ApiResponse<CustomerDto>.Ok(result.Data, result.Message));
            }
            else if (result.StatusCode == 404)
            {
                return NotFound(ApiResponse<CustomerDto>.Error(result.Message));
            }
            else if (result.StatusCode == 500)
            {
                return StatusCode(500, ApiResponse<CustomerDto>.Error(result.Message));
            }
            else
            {
                return BadRequest(ApiResponse<CustomerDto>.Error(result.Message, result.Errors));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating customer {Id}", id);
            return StatusCode(500, ApiResponse<CustomerDto>.Error("Error updating customer", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Delete a customer
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteCustomer(int id)
    {
        try
        {
            // Execute all SDK operations on the dedicated STA thread for EF6 thread-safety
            var result = await _sferaService.ExecuteWithLockAsync<(bool Success, string Message, List<string> Errors, int StatusCode)>(() =>
            {
                var podmioty = _sferaService.GetManager("Podmioty");
                if (podmioty == null)
                {
                    return (false, "Failed to get Podmioty manager", new List<string>(), 500);
                }

                dynamic? podmiot = null;
                foreach (var p in DynamicPropertyHelper.SafeGetAll((object)podmioty))
                {
                    if (DynamicPropertyHelper.GetId(p) == id)
                    {
                        podmiot = p;
                        break;
                    }
                }

                if (podmiot == null)
                {
                    return (false, $"Customer with ID {id} not found", new List<string>(), 404);
                }

                using (var usuwanyPodmiot = podmioty.Znajdz(podmiot))
                {
                    if (usuwanyPodmiot == null)
                    {
                        return (false, $"Customer with ID {id} not found", new List<string>(), 404);
                    }

                    if ((bool)usuwanyPodmiot.Usun())
                    {
                        _logger.LogInformation("Deleted customer {Id}", id);
                        return (true, "Customer deleted successfully", new List<string>(), 200);
                    }
                    else
                    {
                        var errors = GetBusinessObjectErrors(usuwanyPodmiot);
                        return (false, "Failed to delete customer", errors, 400);
                    }
                }
            });

            if (result.Success)
            {
                return Ok(ApiResponse<bool>.Ok(true, result.Message));
            }
            else if (result.StatusCode == 404)
            {
                return NotFound(ApiResponse<bool>.Error(result.Message));
            }
            else if (result.StatusCode == 500)
            {
                return StatusCode(500, ApiResponse<bool>.Error(result.Message));
            }
            else
            {
                return BadRequest(ApiResponse<bool>.Error(result.Message, result.Errors));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting customer {Id}", id);
            return StatusCode(500, ApiResponse<bool>.Error("Error deleting customer", new List<string> { ex.Message }));
        }
    }

    /// <summary>Fields of <see cref="UpdateCustomerRequest"/> that PUT does not write (JSON names).</summary>
    private static List<string> UnsupportedCustomerUpdateFields(UpdateCustomerRequest r)
    {
        var fields = new List<string>();
        void Check(bool present, string name) { if (present) fields.Add(name); }

        Check(r.EuTaxId != null, "euTaxId");
        Check(r.SUN != null, "sun");
        Check(r.Email != null, "email");
        Check(r.Phone != null, "phone");
        Check(r.Website != null, "website");
        Check(r.ContractorType.HasValue, "contractorType");
        Check(r.IsActive.HasValue, "isActive");
        Check(r.IsOneTime.HasValue, "isOneTime");
        Check(r.Address != null, "address");
        Check(r.BankAccount != null, "bankAccount");
        Check(r.BankName != null, "bankName");
        Check(r.PaymentTermSales.HasValue, "paymentTermSales");
        Check(r.PaymentTermPurchase.HasValue, "paymentTermPurchase");
        Check(r.TradeCreditLimit.HasValue, "tradeCreditLimit");
        Check(r.AllowTradeCredit.HasValue, "allowTradeCredit");
        Check(r.IsEuTaxpayer.HasValue, "isEuTaxpayer");
        Check(r.AlwaysUseEuVat.HasValue, "alwaysUseEuVat");
        Check(r.VatDeductible.HasValue, "vatDeductible");
        Check(r.AgriculturalProducer.HasValue, "agriculturalProducer");
        Check(r.DocumentBlock.HasValue, "documentBlock");
        Check(r.DisplayMessage.HasValue, "displayMessage");
        Check(r.MessageText != null, "messageText");
        Check(r.Notes != null, "notes");
        Check(r.LoyaltyProgramParticipant.HasValue, "loyaltyProgramParticipant");

        return fields;
    }

    private static CustomerListItemDto MapToListItemDto(dynamic podmiot)
    {
        return new CustomerListItemDto
        {
            Id = DynamicPropertyHelper.GetId(podmiot),
            Name = DynamicPropertyHelper.GetString(podmiot, "NazwaSkrocona") ?? "",
            TaxId = DynamicPropertyHelper.GetString(podmiot, "NIP"),
            Phone = DynamicPropertyHelper.GetString(podmiot, "Telefon"),
            IsActive = DynamicPropertyHelper.GetNullableBool(podmiot, "Aktywny") ?? true,
            ContractorType = (ContractorType)(DynamicPropertyHelper.GetNullableInt(podmiot, "RodzajKontrahenta") ?? 0)
        };
    }

    private CustomerDto MapToDto(dynamic podmiot) => MapToDtoAs<CustomerDto>((object)podmiot);

    /// <summary>Maps a Podmiot to the full card. Must run on the SDK thread.</summary>
    private T MapToDtoAs<T>(object entity) where T : CustomerDto, new()
    {
        dynamic podmiot = entity;
        var dto = new T
        {
            // Basic info
            Id = DynamicPropertyHelper.GetId(podmiot),
            Symbol = DynamicPropertyHelper.GetString(podmiot, "Symbol") ?? "",
            ShortName = DynamicPropertyHelper.GetString(podmiot, "NazwaSkrocona") ?? "",
            FullName = DynamicPropertyHelper.GetString(podmiot, "NazwaPelna"),
            NIP = DynamicPropertyHelper.GetString(podmiot, "NIP"),
            TaxIdFormatted = DynamicPropertyHelper.GetString(podmiot, "NIPSformatowany"),
            EuTaxId = DynamicPropertyHelper.GetString(podmiot, "NIPUE"),
            SUN = DynamicPropertyHelper.GetString(podmiot, "SUN"),
            REGON = DynamicPropertyHelper.GetString(podmiot, "REGON"),

            // Personal/Company info
            Greeting = DynamicPropertyHelper.GetString(podmiot, "Powitanie"),
            AcademicTitle = DynamicPropertyHelper.GetString(podmiot, "TytulNaukowy"),

            // Contact (direct on entity)
            Phone = DynamicPropertyHelper.GetString(podmiot, "Telefon"),
            Website = DynamicPropertyHelper.GetString(podmiot, "Domena"),

            // Type & Status
            Type = DynamicPropertyHelper.GetNullableInt(podmiot, "Typ") == 0 ? CustomerType.Company : CustomerType.Person,
            Subtype = DynamicPropertyHelper.GetNullableInt(podmiot, "Podtyp"),
            IsContractor = DynamicPropertyHelper.GetNullableBool(podmiot, "Kontrahent") ?? false,
            ContractorType = (ContractorType)(DynamicPropertyHelper.GetNullableInt(podmiot, "RodzajKontrahenta") ?? 0),
            IsActive = DynamicPropertyHelper.GetNullableBool(podmiot, "Aktywny") ?? true,
            IsOneTime = DynamicPropertyHelper.GetNullableBool(podmiot, "Jednorazowy") ?? false,
            CustomerStatus = DynamicPropertyHelper.GetNullableInt(podmiot, "StatusKlienta"),

            // Credit & Limits
            TradeCreditLimit = DynamicPropertyHelper.GetDecimal(podmiot, "LimitKredytuKupieckiego"),
            AllowTradeCredit = DynamicPropertyHelper.GetNullableBool(podmiot, "ZezwalajNaKredytKupiecki") ?? false,
            SalesCreditLimitActive = DynamicPropertyHelper.GetNullableBool(podmiot, "LimitKredytuNaSprzedazyAktywny") ?? false,
            DeliveryCreditLimitActive = DynamicPropertyHelper.GetNullableBool(podmiot, "LimitKredytuNaWydaniuAktywny") ?? false,
            OrderCreditLimitActive = DynamicPropertyHelper.GetNullableBool(podmiot, "LimitKredytuNaZamowieniuAktywny") ?? false,
            MaxCreditPaymentTerm = DynamicPropertyHelper.GetNullableInt(podmiot, "MaksymalnyTerminPlatnosciKredytu"),
            MaxUnpaidDocuments = DynamicPropertyHelper.GetNullableInt(podmiot, "MaksymalnaLiczbaNiesplaconychDok"),
            MaxDelayDays = DynamicPropertyHelper.GetNullableInt(podmiot, "MaksymalnyLiczbaDniSpoznien"),

            // Pricing & Negotiation
            PriceNegotiationAllowed = DynamicPropertyHelper.GetNullableBool(podmiot, "MozliwoscNegocjacjiCeny") ?? false,
            PriceCalculationFunction = DynamicPropertyHelper.GetGuid(podmiot, "FunkcjaWyliczaniaCeny")?.ToString(),

            // Payment
            PaymentTermSales = DynamicPropertyHelper.GetNullableInt(podmiot, "TerminPlatnosciSprzedaz"),
            PaymentTermPurchase = DynamicPropertyHelper.GetNullableInt(podmiot, "TerminPlatnosciZakup"),
            PaymentDaySales = DynamicPropertyHelper.GetNullableInt(podmiot, "DzienTerminuPlatnosciSprzedaz"),
            PaymentDayPurchase = DynamicPropertyHelper.GetNullableInt(podmiot, "DzienTerminuPlatnosciZakup"),
            DefaultReceivablesSettlement = DynamicPropertyHelper.GetNullableInt(podmiot, "DomyslnySposobRozliczaniaNaleznosci"),
            DefaultLiabilitiesSettlement = DynamicPropertyHelper.GetNullableInt(podmiot, "DomyslnySposobRozliczaniaZobowiazan"),
            CashMethod = DynamicPropertyHelper.GetNullableBool(podmiot, "MetodaKasowa") ?? false,

            // Interest
            AppliedInterest = DynamicPropertyHelper.GetGuid(podmiot, "StosowaneOdsetek")?.ToString(),
            InterestCalculationParam = DynamicPropertyHelper.GetDecimal(podmiot, "ParametrWyliczaniaOdsetek"),

            // Programs & Features
            LoyaltyProgramParticipant = DynamicPropertyHelper.GetNullableBool(podmiot, "UczestnikProgramuLojalnosciowego") ?? false,

            // Data protection (GDPR)
            PersonalDataProcessing = DynamicPropertyHelper.GetNullableBool(podmiot, "PrzetwarzanieDanychOsobowych"),
            MarketingPurposes = DynamicPropertyHelper.GetNullableBool(podmiot, "PrzetwarzanieWCelachMarketingowych"),
            ElectronicProcessing = DynamicPropertyHelper.GetNullableBool(podmiot, "PrzetwarzanieDrogaElektroniczna"),
            AcquisitionDate = DynamicPropertyHelper.GetDateTime(podmiot, "DataPozyskania"),
            LossDate = DynamicPropertyHelper.GetDateTime(podmiot, "DataUtracenia"),

            // Blocking & Messages
            DocumentBlock = DynamicPropertyHelper.GetNullableBool(podmiot, "BlokadaWystawianiaDokumentow") ?? false,
            DisplayMessage = DynamicPropertyHelper.GetNullableBool(podmiot, "WyswietlajKomunikat") ?? false,
            MessageText = DynamicPropertyHelper.GetString(podmiot, "TekstKomunikatu"),
            MessageDisplayType = DynamicPropertyHelper.GetNullableInt(podmiot, "WyswietlajKomunikatJako"),

            // VAT & Tax
            IsEuTaxpayer = DynamicPropertyHelper.GetNullableBool(podmiot, "PodatnikUE") ?? false,
            AlwaysUseEuVat = DynamicPropertyHelper.GetNullableBool(podmiot, "ZawszeStosujNIPUE") ?? false,
            VatDeductible = DynamicPropertyHelper.GetNullableBool(podmiot, "VatPodlegaOdliczeniu") ?? true,
            JpkSalesProcedure = DynamicPropertyHelper.GetNullableInt(podmiot, "ProceduraJPKSprzedazy"),
            JpkPurchaseProcedure = DynamicPropertyHelper.GetNullableInt(podmiot, "ProceduraJPKZakupu"),
            AgriculturalProducer = DynamicPropertyHelper.GetNullableBool(podmiot, "ProducentRolny") ?? false,
            SugarTaxHandling = DynamicPropertyHelper.GetNullableInt(podmiot, "ObslugaOplatyCukrowej"),

            // Accounting
            UseAccountingParams = DynamicPropertyHelper.GetNullableBool(podmiot, "KorzystajZParametrowKsiegowosci") ?? false,
            UseAutoPostingParams = DynamicPropertyHelper.GetNullableBool(podmiot, "KorzystajZParametrowAutomatycznejDekretacji") ?? false,

            // E-commerce / Vendero
            VenderoCustomerId = DynamicPropertyHelper.GetNullableInt(podmiot, "VenderoIdKlienta"),
            EcommerceCustomer = DynamicPropertyHelper.GetNullableBool(podmiot, "KlientSklepuInternetowego") ?? false,
            UseDefaultEcommerceDiscount = DynamicPropertyHelper.GetNullableBool(podmiot, "StosujDomyslnyRabatWSklepieInternetowym") ?? false,
            UseIndividualEcommercePricing = DynamicPropertyHelper.GetNullableBool(podmiot, "StosujIndywidualnyCennikWSklepieInternetowym") ?? false,
            VenderoNewsletterConsent = DynamicPropertyHelper.GetNullableBool(podmiot, "ZgodaNaOtrzymywanieNewsletteraVendero") ?? false,

            // Delivery preferences
            PreferredTimeFrom = DynamicPropertyHelper.GetTime(podmiot, "PreferowanaGodzinaOd")?.ToString(@"hh\:mm"),
            PreferredTimeTo = DynamicPropertyHelper.GetTime(podmiot, "PreferowanaGodzinaDo")?.ToString(@"hh\:mm"),
            DefaultAdditionalAddressType = DynamicPropertyHelper.GetNullableInt(podmiot, "DomyslnyTypAdresuDodatkowego"),

            // Mobile
            MobileRemoteSourceId = DynamicPropertyHelper.GetNullableInt(podmiot, "Mobilny_ZdalneIdObiektuZrodlowego"),
            MobileCreatorId = DynamicPropertyHelper.GetGuid(podmiot, "Mobilny_TworcaId")?.ToString(),
            SendToMobile = DynamicPropertyHelper.GetNullableBool(podmiot, "WysylajDoMobile") ?? true,

            // Notes
            Notes = DynamicPropertyHelper.GetString(podmiot, "Uwagi"),
            TransactionTypeCodeId = DynamicPropertyHelper.GetNullableInt(podmiot, "KodRodzajuTransakcjiId"),
            CountedDocument = DynamicPropertyHelper.GetGuid(podmiot, "DokumentLiczony")?.ToString()
        };

        // Symbol, full name, REGON, e-mail/website contacts, bank account, credit limits and addresses from the real
        // SDK members (Symbol/NazwaPelna/REGON/Kontakt.Typ/Rachunki.NumerRachunku do not exist on Podmiot).
        CustomerReader.Enrich(dto, entity, CustomerReader.ContactKindsOf(_sferaService.GetSfera()));

        return dto;
    }

    private static List<string> GetBusinessObjectErrors(dynamic obiekt)
    {
        var errors = new List<string>();
        try
        {
            var invalidData = DynamicPropertyHelper.GetProperty(obiekt, "InvalidData");
            if (invalidData == null) return errors;

            foreach (var encjaZBledami in invalidData)
            {
                var entityErrors = DynamicPropertyHelper.GetProperty(encjaZBledami, "Errors");
                if (entityErrors != null)
                {
                    foreach (var blad in entityErrors)
                    {
                        errors.Add(blad?.ToString() ?? "Unknown error");
                    }
                }

                var memberErrors = DynamicPropertyHelper.GetProperty(encjaZBledami, "MemberErrors");
                if (memberErrors != null)
                {
                    foreach (var bladNaPolach in memberErrors)
                    {
                        try
                        {
                            var key = DynamicPropertyHelper.GetProperty(bladNaPolach, "Key");
                            errors.Add($"{key}: {bladNaPolach}");
                        }
                        catch
                        {
                            errors.Add(bladNaPolach?.ToString() ?? "Unknown error");
                        }
                    }
                }
            }
        }
        catch
        {
            errors.Add("Could not retrieve error details");
        }
        return errors;
    }
}
