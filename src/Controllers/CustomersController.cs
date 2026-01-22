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
    /// Get all customers with optional filtering
    /// </summary>
    [HttpGet]
    public ActionResult<PagedResponse<CustomerDto>> GetCustomers(
        [FromQuery] string? search,
        [FromQuery] CustomerType? type,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
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

            if (!string.IsNullOrEmpty(search))
            {
                allPodmioty = allPodmioty.Where(p =>
                {
                    var nazwaSkrocona = DynamicPropertyHelper.GetString(p, "NazwaSkrocona") ?? "";
                    var nip = DynamicPropertyHelper.GetString(p, "NIP") ?? "";
                    var symbol = DynamicPropertyHelper.GetString(p, "Symbol") ?? "";
                    return nazwaSkrocona.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                           nip.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                           symbol.Contains(search, StringComparison.OrdinalIgnoreCase);
                }).ToList();
            }

            if (type.HasValue)
            {
                short podType = type.Value == CustomerType.Company ? (short)0 : (short)1; // 0=Firmy, 1=Osoby
                allPodmioty = allPodmioty.Where(p =>
                    DynamicPropertyHelper.GetNullableInt(p, "Typ") == podType).ToList();
            }

            var totalCount = allPodmioty.Count;
            var pagedPodmioty = allPodmioty
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var items = new List<CustomerDto>();
            foreach (var p in pagedPodmioty)
            {
                items.Add(MapToDto(p));
            }

            var response = new PagedResponse<CustomerDto>
            {
                Data = items,
                Page = page,
                PageSize = pageSize,
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

    private static CustomerDto MapToDto(dynamic podmiot)
    {
        var dto = new CustomerDto
        {
            Id = DynamicPropertyHelper.GetId(podmiot),
            Symbol = DynamicPropertyHelper.GetString(podmiot, "Symbol"),
            ShortName = DynamicPropertyHelper.GetString(podmiot, "NazwaSkrocona"),
            FullName = DynamicPropertyHelper.GetString(podmiot, "NazwaPelna"),
            NIP = DynamicPropertyHelper.GetString(podmiot, "NIP"),
            REGON = DynamicPropertyHelper.GetString(podmiot, "REGON"),
            Type = DynamicPropertyHelper.GetNullableInt(podmiot, "Typ") == 0 ? CustomerType.Company : CustomerType.Person,
            IsActive = DynamicPropertyHelper.GetNullableBool(podmiot, "Aktywny") ?? true
        };

        // Map address - try different property names
        dynamic? adresGlowny = DynamicPropertyHelper.GetProperty(podmiot, "AdresGlowny");
        if (adresGlowny == null)
        {
            adresGlowny = DynamicPropertyHelper.GetProperty(podmiot, "Adres");
        }
        if (adresGlowny == null)
        {
            // Try to get from Adresy collection
            var adresy = DynamicPropertyHelper.GetCollection(podmiot, "Adresy");
            foreach (var adr in adresy)
            {
                // Take first address or the one marked as main
                if (adresGlowny == null || DynamicPropertyHelper.GetBool(adr, "Glowny"))
                {
                    adresGlowny = adr;
                    if (DynamicPropertyHelper.GetBool(adr, "Glowny")) break;
                }
            }
        }
        if (adresGlowny != null)
        {
            dto.Address = new AddressDto
            {
                Street = DynamicPropertyHelper.GetString(adresGlowny, "Ulica"),
                BuildingNumber = DynamicPropertyHelper.GetString(adresGlowny, "NumerDomu"),
                ApartmentNumber = DynamicPropertyHelper.GetString(adresGlowny, "NumerLokalu"),
                City = DynamicPropertyHelper.GetString(adresGlowny, "Miejscowosc"),
                PostalCode = DynamicPropertyHelper.GetString(adresGlowny, "KodPocztowy"),
                Country = DynamicPropertyHelper.GetString(adresGlowny, "Kraj")
            };
        }

        // Map contacts
        var kontakty = DynamicPropertyHelper.GetCollection(podmiot, "Kontakty");
        foreach (var kontakt in kontakty)
        {
            var typ = DynamicPropertyHelper.GetNullableInt(kontakt, "Typ");
            var wartosc = DynamicPropertyHelper.GetString(kontakt, "Wartosc");
            if (!string.IsNullOrEmpty(wartosc))
            {
                if (typ == 1) // Email
                    dto.Email ??= wartosc;
                else if (typ == 2) // Telefon
                    dto.Phone ??= wartosc;
                else if (typ == 3) // WWW
                    dto.Website ??= wartosc;
            }
        }

        // Map bank account
        var rachunki = DynamicPropertyHelper.GetCollection(podmiot, "RachunkiBankowe");
        dynamic? glownyRachunek = null;
        foreach (var r in rachunki)
        {
            if (glownyRachunek == null)
            {
                glownyRachunek = r; // Take first as fallback
            }
            if (DynamicPropertyHelper.GetBool(r, "Glowny"))
            {
                glownyRachunek = r; // Prefer the main account
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
