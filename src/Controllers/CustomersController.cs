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
            dynamic sfera = _sferaService.GetSfera();
            var podmioty = sfera.Podmioty();
            var allPodmioty = ((IEnumerable<dynamic>)podmioty.Dane.Wszystkie()).ToList();

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
            dynamic sfera = _sferaService.GetSfera();
            var podmioty = sfera.Podmioty();
            var allPodmioty = ((IEnumerable<dynamic>)podmioty.Dane.Wszystkie()).ToList();
            var podmiot = allPodmioty.FirstOrDefault(p => DynamicPropertyHelper.GetId(p) == id);

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
            dynamic sfera = _sferaService.GetSfera();
            var podmioty = sfera.Podmioty();
            var allPodmioty = ((IEnumerable<dynamic>)podmioty.Dane.Wszystkie()).ToList();

            var cleanNip = nip.Replace("-", "").Replace(" ", "");
            var podmiot = allPodmioty.FirstOrDefault(p =>
            {
                var podmiotNip = DynamicPropertyHelper.GetString(p, "NIP") ?? "";
                return podmiotNip == cleanNip || podmiotNip == nip;
            });

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
            dynamic sfera = _sferaService.GetSfera();
            var podmioty = sfera.Podmioty();
            var allPodmioty = ((IEnumerable<dynamic>)podmioty.Dane.Wszystkie()).ToList();

            // Check if symbol already exists
            var existing = allPodmioty.FirstOrDefault(p =>
                DynamicPropertyHelper.GetString(p, "Symbol") == request.Symbol);
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
            dynamic sfera = _sferaService.GetSfera();
            var podmioty = sfera.Podmioty();
            var allPodmioty = ((IEnumerable<dynamic>)podmioty.Dane.Wszystkie()).ToList();
            var podmiot = allPodmioty.FirstOrDefault(p => DynamicPropertyHelper.GetId(p) == id);

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
            dynamic sfera = _sferaService.GetSfera();
            var podmioty = sfera.Podmioty();
            var allPodmioty = ((IEnumerable<dynamic>)podmioty.Dane.Wszystkie()).ToList();
            var podmiot = allPodmioty.FirstOrDefault(p => DynamicPropertyHelper.GetId(p) == id);

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

        // Map address
        var adresGlowny = DynamicPropertyHelper.GetProperty(podmiot, "AdresGlowny");
        if (adresGlowny != null)
        {
            dto.Address = new AddressDto
            {
                Street = DynamicPropertyHelper.GetString(adresGlowny, "Ulica"),
                BuildingNumber = DynamicPropertyHelper.GetString(adresGlowny, "NumerDomu"),
                ApartmentNumber = DynamicPropertyHelper.GetString(adresGlowny, "NumerLokalu"),
                City = DynamicPropertyHelper.GetString(adresGlowny, "Miejscowosc"),
                PostalCode = DynamicPropertyHelper.GetString(adresGlowny, "KodPocztowy")
            };
        }

        // Map contacts
        var kontakty = DynamicPropertyHelper.GetCollection(podmiot, "Kontakty").ToList();
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
        var rachunki = DynamicPropertyHelper.GetCollection(podmiot, "RachunkiBankowe").ToList();
        var glownyRachunek = rachunki.FirstOrDefault(r => DynamicPropertyHelper.GetBool(r, "Glowny"))
                           ?? rachunki.FirstOrDefault();
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
