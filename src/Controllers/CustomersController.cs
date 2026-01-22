using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Requests;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using InsERT.Moria.Klienci;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;

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
            var sfera = _sferaService.GetSfera();
            var podmioty = sfera.Podmioty();

            var query = podmioty.Dane.Wszystkie();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p =>
                    p.NazwaSkrocona.Contains(search) ||
                    (p.NIP != null && p.NIP.Contains(search)) ||
                    p.Symbol.Contains(search));
            }

            if (type.HasValue)
            {
                var podType = type.Value == CustomerType.Company ? (short)TypPodmiotu.Firmy : (short)TypPodmiotu.Osoby;
                query = query.Where(p => p.Typ == podType);
            }

            var totalCount = query.Count();
            var items = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var response = new PagedResponse<CustomerDto>
            {
                Data = items.Select(MapToDto).ToList(),
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
            var sfera = _sferaService.GetSfera();
            var podmioty = sfera.Podmioty();

            var podmiot = podmioty.Dane.Wszystkie().FirstOrDefault(p => p.Id == id);
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
            var sfera = _sferaService.GetSfera();
            var podmioty = sfera.Podmioty();

            var cleanNip = nip.Replace("-", "").Replace(" ", "");
            var podmiot = podmioty.Dane.Pierwszy(p => p.NIP == cleanNip || p.NIP == nip);

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
            var sfera = _sferaService.GetSfera();
            var podmioty = sfera.Podmioty();

            // Check if symbol already exists
            var existing = podmioty.Dane.Pierwszy(p => p.Symbol == request.Symbol);
            if (existing != null)
            {
                return BadRequest(ApiResponse<CustomerDto>.Error($"Customer with symbol {request.Symbol} already exists"));
            }

            using (var nowyPodmiot = podmioty.Utworz())
            {
                nowyPodmiot.Dane.Symbol = request.Symbol;
                nowyPodmiot.Dane.NazwaSkrocona = request.ShortName;
                nowyPodmiot.Dane.NazwaPelna = request.FullName ?? request.ShortName;

                if (!string.IsNullOrEmpty(request.NIP))
                {
                    nowyPodmiot.Dane.NIP = request.NIP.Replace("-", "").Replace(" ", "");
                }

                if (!string.IsNullOrEmpty(request.REGON))
                {
                    nowyPodmiot.Dane.REGON = request.REGON;
                }

                // Set customer type
                nowyPodmiot.Dane.Typ = request.Type == CustomerType.Company
                    ? (short)TypPodmiotu.Firmy
                    : (short)TypPodmiotu.Osoby;

                // Set address
                if (request.Address != null)
                {
                    var adres = nowyPodmiot.Dane.AdresGlowny;
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
                    var kontakt = nowyPodmiot.Kontakty.DodajEmail(request.Email);
                    if (kontakt != null)
                    {
                        kontakt.Dane.Glowny = true;
                    }
                }

                if (!string.IsNullOrEmpty(request.Phone))
                {
                    var kontakt = nowyPodmiot.Kontakty.DodajTelefon(request.Phone);
                    if (kontakt != null)
                    {
                        kontakt.Dane.Glowny = true;
                    }
                }

                if (!string.IsNullOrEmpty(request.Website))
                {
                    nowyPodmiot.Kontakty.DodajWww(request.Website);
                }

                // Set bank account
                if (!string.IsNullOrEmpty(request.BankAccount))
                {
                    var rachunek = nowyPodmiot.RachunkiBankowe.Dodaj();
                    if (rachunek != null)
                    {
                        rachunek.Dane.NumerRachunku = request.BankAccount;
                        rachunek.Dane.NazwaBanku = request.BankName;
                        rachunek.Dane.Glowny = true;
                    }
                }

                if (nowyPodmiot.Zapisz())
                {
                    _logger.LogInformation("Created customer {Symbol}", request.Symbol);
                    return CreatedAtAction(
                        nameof(GetCustomer),
                        new { id = nowyPodmiot.Dane.Id },
                        ApiResponse<CustomerDto>.Ok(MapToDto(nowyPodmiot.Dane), "Customer created successfully"));
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
            var sfera = _sferaService.GetSfera();
            var podmioty = sfera.Podmioty();

            var podmiot = podmioty.Dane.Wszystkie().FirstOrDefault(p => p.Id == id);
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

                if (!string.IsNullOrEmpty(request.ShortName))
                {
                    edytowanyPodmiot.Dane.NazwaSkrocona = request.ShortName;
                }

                if (!string.IsNullOrEmpty(request.FullName))
                {
                    edytowanyPodmiot.Dane.NazwaPelna = request.FullName;
                }

                if (!string.IsNullOrEmpty(request.NIP))
                {
                    edytowanyPodmiot.Dane.NIP = request.NIP.Replace("-", "").Replace(" ", "");
                }

                if (!string.IsNullOrEmpty(request.REGON))
                {
                    edytowanyPodmiot.Dane.REGON = request.REGON;
                }

                if (edytowanyPodmiot.Zapisz())
                {
                    _logger.LogInformation("Updated customer {Id}", id);
                    return Ok(ApiResponse<CustomerDto>.Ok(MapToDto(edytowanyPodmiot.Dane), "Customer updated successfully"));
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
            var sfera = _sferaService.GetSfera();
            var podmioty = sfera.Podmioty();

            var podmiot = podmioty.Dane.Wszystkie().FirstOrDefault(p => p.Id == id);
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

                if (usuwanyPodmiot.Usun())
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

    private static CustomerDto MapToDto(Podmiot podmiot)
    {
        var dto = new CustomerDto
        {
            Id = podmiot.Id,
            Symbol = podmiot.Symbol,
            ShortName = podmiot.NazwaSkrocona,
            FullName = podmiot.NazwaPelna,
            NIP = podmiot.NIP,
            REGON = podmiot.REGON,
            Type = podmiot.Typ == (short)TypPodmiotu.Firmy ? CustomerType.Company : CustomerType.Person,
            IsActive = podmiot.Aktywny
        };

        // Map address
        if (podmiot.AdresGlowny != null)
        {
            dto.Address = new AddressDto
            {
                Street = podmiot.AdresGlowny.Ulica,
                BuildingNumber = podmiot.AdresGlowny.NumerDomu,
                ApartmentNumber = podmiot.AdresGlowny.NumerLokalu,
                City = podmiot.AdresGlowny.Miejscowosc,
                PostalCode = podmiot.AdresGlowny.KodPocztowy
            };
        }

        // Map contacts
        var emailKontakt = podmiot.Kontakty?.FirstOrDefault(k => k.Typ == (int)TypKontaktu.Email);
        if (emailKontakt != null)
        {
            dto.Email = emailKontakt.Wartosc;
        }

        var telefonKontakt = podmiot.Kontakty?.FirstOrDefault(k => k.Typ == (int)TypKontaktu.Telefon);
        if (telefonKontakt != null)
        {
            dto.Phone = telefonKontakt.Wartosc;
        }

        var wwwKontakt = podmiot.Kontakty?.FirstOrDefault(k => k.Typ == (int)TypKontaktu.WWW);
        if (wwwKontakt != null)
        {
            dto.Website = wwwKontakt.Wartosc;
        }

        // Map bank account
        var glownyRachunek = podmiot.RachunkiBankowe?.FirstOrDefault(r => r.Glowny);
        if (glownyRachunek != null)
        {
            dto.BankAccount = glownyRachunek.NumerRachunku;
            dto.BankName = glownyRachunek.NazwaBanku;
        }

        return dto;
    }

    private static List<string> GetBusinessObjectErrors(InsERT.Mox.ObiektyBiznesowe.IObiektBiznesowy obiekt)
    {
        var errors = new List<string>();
        foreach (var encjaZBledami in obiekt.InvalidData)
        {
            foreach (var blad in encjaZBledami.Errors)
            {
                errors.Add(blad.ToString());
            }
            foreach (var bladNaPolach in encjaZBledami.MemberErrors)
            {
                errors.Add($"{bladNaPolach.Key}: {string.Join(", ", bladNaPolach)}");
            }
        }
        return errors;
    }
}
