using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Requests;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

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
    public ActionResult<object> GetSferaInfo()
    {
        try
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

            return Ok(new
            {
                TypeName = type.FullName,
                Methods = methodNames,
                Properties = propertyNames
            });
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
    public ActionResult<object> TestPodmioty()
    {
        try
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
                return Ok(new { Error = "Podmioty type not found in loaded assemblies" });
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

            return Ok(results);
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
    public ActionResult<object> GetAddressSzczegoly(int id)
    {
        try
        {
            var podmioty = _sferaService.GetManager("Podmioty");
            if (podmioty == null)
            {
                return StatusCode(500, new { Error = "Failed to get Podmioty manager" });
            }

            dynamic? podmiot = null;
            foreach (var p in podmioty.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetId(p) == id)
                {
                    podmiot = p;
                    break;
                }
            }

            if (podmiot == null)
            {
                return NotFound(new { Error = $"Customer {id} not found" });
            }

            var adresPodmiotu = DynamicPropertyHelper.GetProperty(podmiot, "AdresPodstawowy");
            if (adresPodmiotu == null)
            {
                return Ok(new { CustomerId = id, AdresPodstawowy = "null" });
            }

            var szczegoly = DynamicPropertyHelper.GetProperty(adresPodmiotu, "Szczegoly");
            if (szczegoly == null)
            {
                return Ok(new {
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

            return Ok(new
            {
                CustomerId = id,
                SzczegolyType = szczegolyType.FullName,
                Properties = props.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value)
            });
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
    public ActionResult<object> GetAddressDetails(int id)
    {
        try
        {
            var podmioty = _sferaService.GetManager("Podmioty");
            if (podmioty == null)
            {
                return StatusCode(500, new { Error = "Failed to get Podmioty manager" });
            }

            dynamic? podmiot = null;
            foreach (var p in podmioty.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetId(p) == id)
                {
                    podmiot = p;
                    break;
                }
            }

            if (podmiot == null)
            {
                return NotFound(new { Error = $"Customer {id} not found" });
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
            var adresy = DynamicPropertyHelper.GetCollection(podmiot, "Adresy");
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

            return Ok(result);
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
    public ActionResult<object> TestZnajdz(int id)
    {
        try
        {
            var podmioty = _sferaService.GetManager("Podmioty");
            if (podmioty == null)
            {
                return StatusCode(500, new { Error = "Failed to get Podmioty manager" });
            }

            // First find the entity
            dynamic? podmiotDane = null;
            foreach (var p in podmioty.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetId(p) == id)
                {
                    podmiotDane = p;
                    break;
                }
            }

            if (podmiotDane == null)
            {
                return NotFound(new { Error = $"Customer {id} not found" });
            }

            // Use Znajdz to get full business object
            using (var podmiotBO = podmioty.Znajdz(podmiotDane))
            {
                if (podmiotBO == null)
                {
                    return NotFound(new { Error = $"Znajdz returned null for customer {id}" });
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

                return Ok(result);
            }
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
    public ActionResult<object> GetPodmiotProperties(int id)
    {
        try
        {
            var podmioty = _sferaService.GetManager("Podmioty");
            if (podmioty == null)
            {
                return StatusCode(500, new { Error = "Failed to get Podmioty manager" });
            }

            dynamic? podmiot = null;
            foreach (var p in podmioty.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetId(p) == id)
                {
                    podmiot = p;
                    break;
                }
            }

            if (podmiot == null)
            {
                return NotFound(new { Error = $"Customer {id} not found" });
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

            return Ok(new
            {
                Id = id,
                EntityType = podmiotType.FullName,
                PropertyCount = properties.Count,
                Properties = properties.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value)
            });
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
    public ActionResult<object> TestGetManager()
    {
        try
        {
            var results = new Dictionary<string, object>();

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
                        results[managerName] = new
                        {
                            Success = true,
                            Type = mgrType.FullName,
                            HasDane = mgrType.GetProperty("Dane") != null
                        };
                    }
                    else
                    {
                        results[managerName] = new { Success = false, Error = "Returned null" };
                    }
                }
                catch (Exception ex)
                {
                    results[managerName] = new { Success = false, Error = ex.Message, ExType = ex.GetType().Name };
                }
            }

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
    public ActionResult<object> GetCustomerProperties(int id)
    {
        try
        {
            var podmioty = _sferaService.GetManager("Podmioty");
            if (podmioty == null)
            {
                return StatusCode(500, new { Error = "Failed to get Podmioty manager" });
            }

            dynamic? podmiot = null;
            foreach (var p in podmioty.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetId(p) == id)
                {
                    podmiot = p;
                    break;
                }
            }

            if (podmiot == null)
            {
                return NotFound(new { Error = $"Customer {id} not found" });
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

            return Ok(new
            {
                Id = id,
                EntityType = podmiotType.FullName,
                PropertyCount = properties.Count,
                Properties = properties
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = ex.Message, Stack = ex.StackTrace });
        }
    }

    /// <summary>
    /// Get all customers with optional filtering (returns lightweight DTO for performance)
    /// </summary>
    [HttpGet]
    public ActionResult<PagedResponse<CustomerListItemDto>> GetCustomers([FromQuery] CustomerQueryRequest query)
    {
        try
        {
            var podmioty = _sferaService.GetManager("Podmioty");
            if (podmioty == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Podmioty manager"));
            }

            var allPodmioty = new List<dynamic>();
            foreach (var p in podmioty.Dane.Wszystkie())
            {
                allPodmioty.Add(p);
            }

            // Apply filters using foreach to avoid LINQ issues with dynamic types
            var filteredList = new List<dynamic>();

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
                    var expectedType = query.Type.Value == CustomerType.Company ? 0 : 1;
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

            var response = new PagedResponse<CustomerListItemDto>
            {
                Data = items,
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = totalCount
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting customers");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving customers", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get customer by ID
    /// </summary>
    [HttpGet("{id}")]
    public ActionResult<ApiResponse<CustomerDto>> GetCustomer(int id)
    {
        try
        {
            var podmioty = _sferaService.GetManager("Podmioty");
            if (podmioty == null)
            {
                return StatusCode(500, ApiResponse<CustomerDto>.Error("Failed to get Podmioty manager"));
            }

            dynamic? podmiot = null;
            foreach (var p in podmioty.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetId(p) == id)
                {
                    podmiot = p;
                    break;
                }
            }

            if (podmiot == null)
            {
                return NotFound(ApiResponse<CustomerDto>.Error($"Customer with ID {id} not found"));
            }

            return Ok(ApiResponse<CustomerDto>.Ok(MapToDto(podmiot)));
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
    public ActionResult<ApiResponse<CustomerDto>> GetCustomerByNip(string nip)
    {
        try
        {
            var podmioty = _sferaService.GetManager("Podmioty");
            if (podmioty == null)
            {
                return StatusCode(500, ApiResponse<CustomerDto>.Error("Failed to get Podmioty manager"));
            }

            var cleanNip = nip.Replace("-", "").Replace(" ", "");
            dynamic? podmiot = null;
            foreach (var p in podmioty.Dane.Wszystkie())
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
                return NotFound(ApiResponse<CustomerDto>.Error($"Customer with NIP {nip} not found"));
            }

            return Ok(ApiResponse<CustomerDto>.Ok(MapToDto(podmiot)));
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
    public ActionResult<ApiResponse<CustomerDto>> CreateCustomer([FromBody] CreateCustomerRequest request)
    {
        try
        {
            var podmioty = _sferaService.GetManager("Podmioty");
            if (podmioty == null)
            {
                return StatusCode(500, ApiResponse<CustomerDto>.Error("Failed to get Podmioty manager"));
            }

            // Check if symbol already exists
            dynamic? existing = null;
            foreach (var p in podmioty.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetString(p, "Symbol") == request.Symbol)
                {
                    existing = p;
                    break;
                }
            }
            if (existing != null)
            {
                return BadRequest(ApiResponse<CustomerDto>.Error($"Customer with symbol {request.Symbol} already exists"));
            }

            using (var nowyPodmiot = podmioty.Utworz())
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
                    return CreatedAtAction(
                        nameof(GetCustomer),
                        new { id = DynamicPropertyHelper.GetId(dane) },
                        ApiResponse<CustomerDto>.Ok(MapToDto(dane), "Customer created successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(nowyPodmiot);
                    return BadRequest(ApiResponse<CustomerDto>.Error("Failed to create customer", errors));
                }
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
    [HttpPut("{id}")]
    public ActionResult<ApiResponse<CustomerDto>> UpdateCustomer(int id, [FromBody] UpdateCustomerRequest request)
    {
        try
        {
            var podmioty = _sferaService.GetManager("Podmioty");
            if (podmioty == null)
            {
                return StatusCode(500, ApiResponse<CustomerDto>.Error("Failed to get Podmioty manager"));
            }

            dynamic? podmiot = null;
            foreach (var p in podmioty.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetId(p) == id)
                {
                    podmiot = p;
                    break;
                }
            }

            if (podmiot == null)
            {
                return NotFound(ApiResponse<CustomerDto>.Error($"Customer with ID {id} not found"));
            }

            using (var edytowanyPodmiot = podmioty.Znajdz(podmiot))
            {
                if (edytowanyPodmiot == null)
                {
                    return NotFound(ApiResponse<CustomerDto>.Error($"Customer with ID {id} not found"));
                }

                dynamic dane = edytowanyPodmiot.Dane;

                if (!string.IsNullOrEmpty(request.ShortName))
                {
                    dane.NazwaSkrocona = request.ShortName;
                }

                if (!string.IsNullOrEmpty(request.FullName))
                {
                    dane.NazwaPelna = request.FullName;
                }

                if (!string.IsNullOrEmpty(request.NIP))
                {
                    dane.NIP = request.NIP.Replace("-", "").Replace(" ", "");
                }

                if (!string.IsNullOrEmpty(request.REGON))
                {
                    dane.REGON = request.REGON;
                }

                if ((bool)edytowanyPodmiot.Zapisz())
                {
                    _logger.LogInformation("Updated customer {Id}", id);
                    return Ok(ApiResponse<CustomerDto>.Ok(MapToDto(dane), "Customer updated successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(edytowanyPodmiot);
                    return BadRequest(ApiResponse<CustomerDto>.Error("Failed to update customer", errors));
                }
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
    public ActionResult<ApiResponse<bool>> DeleteCustomer(int id)
    {
        try
        {
            var podmioty = _sferaService.GetManager("Podmioty");
            if (podmioty == null)
            {
                return StatusCode(500, ApiResponse<bool>.Error("Failed to get Podmioty manager"));
            }

            dynamic? podmiot = null;
            foreach (var p in podmioty.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetId(p) == id)
                {
                    podmiot = p;
                    break;
                }
            }

            if (podmiot == null)
            {
                return NotFound(ApiResponse<bool>.Error($"Customer with ID {id} not found"));
            }

            using (var usuwanyPodmiot = podmioty.Znajdz(podmiot))
            {
                if (usuwanyPodmiot == null)
                {
                    return NotFound(ApiResponse<bool>.Error($"Customer with ID {id} not found"));
                }

                if ((bool)usuwanyPodmiot.Usun())
                {
                    _logger.LogInformation("Deleted customer {Id}", id);
                    return Ok(ApiResponse<bool>.Ok(true, "Customer deleted successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(usuwanyPodmiot);
                    return BadRequest(ApiResponse<bool>.Error("Failed to delete customer", errors));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting customer {Id}", id);
            return StatusCode(500, ApiResponse<bool>.Error("Error deleting customer", new List<string> { ex.Message }));
        }
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

    private static CustomerDto MapToDto(dynamic podmiot)
    {
        var dto = new CustomerDto
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

        // Map address - AdresPodstawowy is directly available on entity
        dynamic? adresPodmiotu = DynamicPropertyHelper.GetProperty(podmiot, "AdresPodstawowy");

        // Fallback: try first from Adresy collection
        if (adresPodmiotu == null)
        {
            var adresy = DynamicPropertyHelper.GetCollection(podmiot, "Adresy");
            foreach (var adr in adresy)
            {
                adresPodmiotu = adr;
                break;
            }
        }

        if (adresPodmiotu != null)
        {
            // SDK uses AdresPodmiotu with Szczegoly (AdresSzczegoly) for detailed address
            // or Linia1/Linia2/LiniaCalosc for formatted lines
            var szczegoly = DynamicPropertyHelper.GetProperty(adresPodmiotu, "Szczegoly");

            if (szczegoly != null)
            {
                // Get detailed address from Szczegoly (AdresSzczegoly)
                // Property names: Ulica, NrDomu, NrLokalu, Miejscowosc, KodPocztowy, Poczta
                dto.Address = new AddressDto
                {
                    Street = DynamicPropertyHelper.GetString(szczegoly, "Ulica"),
                    BuildingNumber = DynamicPropertyHelper.GetString(szczegoly, "NrDomu"),
                    ApartmentNumber = DynamicPropertyHelper.GetString(szczegoly, "NrLokalu"),
                    City = DynamicPropertyHelper.GetString(szczegoly, "Miejscowosc"),
                    PostalCode = DynamicPropertyHelper.GetString(szczegoly, "KodPocztowy")
                };

                // Try to get country from Panstwo relation on AdresPodmiotu
                var panstwo = DynamicPropertyHelper.GetProperty(adresPodmiotu, "Panstwo");
                if (panstwo != null)
                {
                    dto.Address.Country = DynamicPropertyHelper.GetString(panstwo, "Nazwa");
                }
            }

            // If Szczegoly didn't provide data, use Linia fields
            if (dto.Address == null || string.IsNullOrEmpty(dto.Address.Street))
            {
                var linia1 = DynamicPropertyHelper.GetString(adresPodmiotu, "Linia1"); // Usually street + number
                var linia2 = DynamicPropertyHelper.GetString(adresPodmiotu, "Linia2"); // Usually postal + city

                // Try to get country from Panstwo relation
                var panstwo = DynamicPropertyHelper.GetProperty(adresPodmiotu, "Panstwo");
                var country = panstwo != null ? DynamicPropertyHelper.GetString(panstwo, "Nazwa") : null;

                dto.Address = new AddressDto
                {
                    Street = linia1,
                    City = linia2,
                    Country = country
                };
            }
        }

        // Map contacts from Kontakty collection
        var kontakty = DynamicPropertyHelper.GetCollection(podmiot, "Kontakty");
        foreach (var kontakt in kontakty)
        {
            var typ = DynamicPropertyHelper.GetNullableInt(kontakt, "Typ");
            var wartosc = DynamicPropertyHelper.GetString(kontakt, "Wartosc");
            if (!string.IsNullOrEmpty(wartosc))
            {
                if (typ == 1) // Email
                    dto.Email ??= wartosc;
                else if (typ == 2) // Telefon - override direct Telefon if exists in Kontakty
                    dto.Phone ??= wartosc;
                else if (typ == 3) // WWW
                    dto.Website ??= wartosc;
            }
        }

        // Map bank account from Rachunki collection
        var rachunki = DynamicPropertyHelper.GetCollection(podmiot, "Rachunki");
        dynamic? glownyRachunek = null;
        foreach (var r in rachunki)
        {
            if (glownyRachunek == null)
            {
                glownyRachunek = r;
            }
            if (DynamicPropertyHelper.GetBool(r, "Glowny"))
            {
                glownyRachunek = r;
                break;
            }
        }
        if (glownyRachunek != null)
        {
            dto.BankAccount = DynamicPropertyHelper.GetString(glownyRachunek, "NumerRachunku");
            dto.BankName = DynamicPropertyHelper.GetString(glownyRachunek, "NazwaBanku");
        }

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
