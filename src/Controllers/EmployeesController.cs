using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Employees (Pracownicy) endpoints
/// </summary>
[ApiController]
[Route("api/employees")]
[Authorize]
[Tags("Employees")]
public class EmployeesController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<EmployeesController> _logger;

    public EmployeesController(ISferaService sferaService, ILogger<EmployeesController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get all employees
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<EmployeeSummaryDto>), StatusCodes.Status200OK)]
    public ActionResult<PagedResponse<EmployeeSummaryDto>> GetEmployees(
        [FromQuery] string? search = null,
        [FromQuery] bool? activeOnly = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var podmioty = _sferaService.GetManager("Podmioty");
            if (podmioty == null) return StatusCode(500, ApiResponse<object>.Error("Podmioty manager not available"));
            var allPracownicy = ((IEnumerable<dynamic>)podmioty.Dane.WszyscyPracownicy()).ToList();

            if (activeOnly == true)
            {
                allPracownicy = allPracownicy.Where(p =>
                    DynamicPropertyHelper.GetNullableBool(p, "Aktywny") == true).ToList();
            }

            if (!string.IsNullOrEmpty(search))
            {
                var searchLower = search.ToLower();
                allPracownicy = allPracownicy.Where(p =>
                {
                    var osoba = DynamicPropertyHelper.GetProperty(p, "Osoba");
                    var imie = osoba != null ? DynamicPropertyHelper.GetString(osoba, "Imie") ?? "" : "";
                    var nazwisko = osoba != null ? DynamicPropertyHelper.GetString(osoba, "Nazwisko") ?? "" : "";
                    var symbol = DynamicPropertyHelper.GetString(p, "Symbol") ?? "";
                    return imie.ToLower().Contains(searchLower) ||
                           nazwisko.ToLower().Contains(searchLower) ||
                           symbol.ToLower().Contains(searchLower);
                }).ToList();
            }

            var totalCount = allPracownicy.Count;
            var items = allPracownicy
                .OrderBy(p =>
                {
                    var osoba = DynamicPropertyHelper.GetProperty(p, "Osoba");
                    return osoba != null ? DynamicPropertyHelper.GetString(osoba, "Nazwisko") ?? "" : "";
                })
                .ThenBy(p =>
                {
                    var osoba = DynamicPropertyHelper.GetProperty(p, "Osoba");
                    return osoba != null ? DynamicPropertyHelper.GetString(osoba, "Imie") ?? "" : "";
                })
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var mappedItems = new List<EmployeeSummaryDto>();
            foreach (var p in items)
            {
                mappedItems.Add(MapEmployeeSummary(p));
            }

            return Ok(new PagedResponse<EmployeeSummaryDto>
            {
                Data = mappedItems,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting employees");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving employees", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get employee by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDto>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<EmployeeDto>> GetEmployee(int id, [FromQuery] bool includeContacts = true)
    {
        try
        {
            var podmioty = _sferaService.GetManager("Podmioty");
            if (podmioty == null) return StatusCode(500, ApiResponse<object>.Error("Podmioty manager not available"));
            var allPracownicy = ((IEnumerable<dynamic>)podmioty.Dane.WszyscyPracownicy()).ToList();
            var pracownik = allPracownicy.FirstOrDefault(p =>
            {
                var osoba = DynamicPropertyHelper.GetProperty(p, "Osoba");
                var pracownikObj = osoba != null ? DynamicPropertyHelper.GetProperty(osoba, "Pracownik") : null;
                return pracownikObj != null && DynamicPropertyHelper.GetId(pracownikObj) == id;
            });

            if (pracownik == null)
            {
                return NotFound(ApiResponse<EmployeeDto>.Error($"Employee with ID {id} not found"));
            }

            var dto = MapEmployee(pracownik, includeContacts);
            return Ok(ApiResponse<EmployeeDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting employee {Id}", id);
            return StatusCode(500, ApiResponse<EmployeeDto>.Error("Error retrieving employee", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get employee by symbol
    /// </summary>
    [HttpGet("by-symbol/{symbol}")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDto>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<EmployeeDto>> GetEmployeeBySymbol(string symbol, [FromQuery] bool includeContacts = true)
    {
        try
        {
            var podmioty = _sferaService.GetManager("Podmioty");
            if (podmioty == null) return StatusCode(500, ApiResponse<object>.Error("Podmioty manager not available"));
            var allPracownicy = ((IEnumerable<dynamic>)podmioty.Dane.WszyscyPracownicy()).ToList();
            var pracownik = allPracownicy.FirstOrDefault(p =>
                DynamicPropertyHelper.GetString(p, "Symbol") == symbol);

            if (pracownik == null)
            {
                return NotFound(ApiResponse<EmployeeDto>.Error($"Employee with symbol '{symbol}' not found"));
            }

            var dto = MapEmployee(pracownik, includeContacts);
            return Ok(ApiResponse<EmployeeDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting employee by symbol {Symbol}", symbol);
            return StatusCode(500, ApiResponse<EmployeeDto>.Error("Error retrieving employee", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Search employees by PESEL
    /// </summary>
    [HttpGet("by-pesel/{pesel}")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDto>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<EmployeeDto>> GetEmployeeByPesel(string pesel, [FromQuery] bool includeContacts = true)
    {
        try
        {
            var podmioty = _sferaService.GetManager("Podmioty");
            if (podmioty == null) return StatusCode(500, ApiResponse<object>.Error("Podmioty manager not available"));
            var allPracownicy = ((IEnumerable<dynamic>)podmioty.Dane.WszyscyPracownicy()).ToList();
            var pracownik = allPracownicy.FirstOrDefault(p =>
            {
                var osoba = DynamicPropertyHelper.GetProperty(p, "Osoba");
                return osoba != null && DynamicPropertyHelper.GetString(osoba, "PESEL") == pesel;
            });

            if (pracownik == null)
            {
                return NotFound(ApiResponse<EmployeeDto>.Error($"Employee with PESEL '{pesel}' not found"));
            }

            var dto = MapEmployee(pracownik, includeContacts);
            return Ok(ApiResponse<EmployeeDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting employee by PESEL");
            return StatusCode(500, ApiResponse<EmployeeDto>.Error("Error retrieving employee", new List<string> { ex.Message }));
        }
    }

    #region Mapping

    private static EmployeeSummaryDto MapEmployeeSummary(dynamic p)
    {
        var osoba = DynamicPropertyHelper.GetProperty(p, "Osoba");
        var pracownikObj = osoba != null ? DynamicPropertyHelper.GetProperty(osoba, "Pracownik") : null;
        var kontakty = DynamicPropertyHelper.GetCollection(p, "Kontakty");

        var primaryEmail = GetContactByType(kontakty, "email");
        var primaryPhone = GetContactByType(kontakty, "telefon");

        var imie = osoba != null ? DynamicPropertyHelper.GetString(osoba, "Imie") ?? "" : "";
        var nazwisko = osoba != null ? DynamicPropertyHelper.GetString(osoba, "Nazwisko") ?? "" : "";

        return new EmployeeSummaryDto
        {
            Id = pracownikObj != null ? DynamicPropertyHelper.GetId(pracownikObj) : 0,
            Symbol = DynamicPropertyHelper.GetString(p, "Symbol"),
            FirstName = imie,
            LastName = nazwisko,
            FullName = $"{imie} {nazwisko}".Trim(),
            Email = primaryEmail,
            Phone = primaryPhone,
            IsActive = DynamicPropertyHelper.GetNullableBool(p, "Aktywny") ?? false
        };
    }

    private static EmployeeDto MapEmployee(dynamic p, bool includeContacts)
    {
        var osoba = DynamicPropertyHelper.GetProperty(p, "Osoba");
        var pracownikObj = osoba != null ? DynamicPropertyHelper.GetProperty(osoba, "Pracownik") : null;
        var kontakty = DynamicPropertyHelper.GetCollection(p, "Kontakty");

        var primaryEmail = GetContactByType(kontakty, "email");
        var primaryPhone = GetContactByType(kontakty, "telefon");

        var imie = osoba != null ? DynamicPropertyHelper.GetString(osoba, "Imie") ?? "" : "";
        var nazwisko = osoba != null ? DynamicPropertyHelper.GetString(osoba, "Nazwisko") ?? "" : "";
        var pesel = osoba != null ? DynamicPropertyHelper.GetString(osoba, "PESEL") : null;
        var dataUrodzenia = osoba != null ? DynamicPropertyHelper.GetDateTime(osoba, "DataUrodzenia") : null;
        var plec = osoba != null ? DynamicPropertyHelper.GetNullableInt(osoba, "Plec") : null;

        var dto = new EmployeeDto
        {
            Id = pracownikObj != null ? DynamicPropertyHelper.GetId(pracownikObj) : 0,
            Symbol = DynamicPropertyHelper.GetString(p, "Symbol"),
            FirstName = imie,
            LastName = nazwisko,
            FullName = $"{imie} {nazwisko}".Trim(),
            Pesel = pesel,
            BirthDate = dataUrodzenia,
            Gender = plec switch
            {
                0 => "Male",
                1 => "Female",
                _ => "Unknown"
            },
            Email = primaryEmail,
            Phone = primaryPhone,
            IsActive = DynamicPropertyHelper.GetNullableBool(p, "Aktywny") ?? false
        };

        // Map address
        var adresPodstawowy = DynamicPropertyHelper.GetProperty(p, "AdresPodstawowy");
        if (adresPodstawowy != null)
        {
            dto.Address = MapAddress(adresPodstawowy);
        }

        // Map correspondence address
        var adresKorespondencyjny = DynamicPropertyHelper.GetProperty(p, "DomyslnyAdresKorespondencyjny");
        if (adresKorespondencyjny != null)
        {
            dto.CorrespondenceAddress = MapAddress(adresKorespondencyjny);
        }

        // Map contacts
        if (includeContacts && kontakty.Count > 0)
        {
            dto.Contacts = new List<EmployeeContactDto>();
            foreach (var k in kontakty)
            {
                var rodzaj = DynamicPropertyHelper.GetProperty(k, "Rodzaj");
                dto.Contacts.Add(new EmployeeContactDto
                {
                    Id = DynamicPropertyHelper.GetId(k),
                    Type = rodzaj != null ? DynamicPropertyHelper.GetString(rodzaj, "Nazwa") : null,
                    Value = DynamicPropertyHelper.GetString(k, "Wartosc"),
                    IsPrimary = DynamicPropertyHelper.GetNullableBool(k, "Podstawowy") ?? false,
                    Comment = DynamicPropertyHelper.GetString(k, "Komentarz")
                });
            }
        }

        return dto;
    }

    private static string? GetContactByType(List<dynamic> kontakty, string typeName)
    {
        // First try to find primary contact of given type
        var primary = kontakty.FirstOrDefault(k =>
        {
            var rodzaj = DynamicPropertyHelper.GetProperty(k, "Rodzaj");
            var nazwa = rodzaj != null ? DynamicPropertyHelper.GetString(rodzaj, "Nazwa")?.ToLower() ?? "" : "";
            var isPrimary = DynamicPropertyHelper.GetNullableBool(k, "Podstawowy") ?? false;
            return nazwa.Contains(typeName) && isPrimary;
        });

        if (primary != null)
        {
            return DynamicPropertyHelper.GetString(primary, "Wartosc");
        }

        // Fall back to any contact of given type
        var any = kontakty.FirstOrDefault(k =>
        {
            var rodzaj = DynamicPropertyHelper.GetProperty(k, "Rodzaj");
            var nazwa = rodzaj != null ? DynamicPropertyHelper.GetString(rodzaj, "Nazwa")?.ToLower() ?? "" : "";
            return nazwa.Contains(typeName);
        });

        return any != null ? DynamicPropertyHelper.GetString(any, "Wartosc") : null;
    }

    private static EmployeeAddressDto? MapAddress(dynamic adres)
    {
        if (adres == null) return null;

        var szczegoly = DynamicPropertyHelper.GetProperty(adres, "Szczegoly");
        var panstwo = DynamicPropertyHelper.GetProperty(adres, "Panstwo");
        var wojewodztwo = szczegoly != null ? DynamicPropertyHelper.GetProperty(szczegoly, "Wojewodztwo") : null;
        var gmina = szczegoly != null ? DynamicPropertyHelper.GetProperty(szczegoly, "Gmina") : null;
        var powiat = gmina != null ? DynamicPropertyHelper.GetProperty(gmina, "Powiat") : null;

        return new EmployeeAddressDto
        {
            Name = DynamicPropertyHelper.GetString(adres, "Nazwa"),
            Street = szczegoly != null ? DynamicPropertyHelper.GetString(szczegoly, "Ulica") : null,
            BuildingNumber = szczegoly != null ? DynamicPropertyHelper.GetString(szczegoly, "NrDomu") : null,
            ApartmentNumber = szczegoly != null ? DynamicPropertyHelper.GetString(szczegoly, "NrLokalu") : null,
            PostalCode = szczegoly != null ? DynamicPropertyHelper.GetString(szczegoly, "KodPocztowy") : null,
            City = szczegoly != null ? DynamicPropertyHelper.GetString(szczegoly, "Miejscowosc") : null,
            PostOffice = szczegoly != null ? DynamicPropertyHelper.GetString(szczegoly, "Poczta") : null,
            Country = panstwo != null ? DynamicPropertyHelper.GetString(panstwo, "Nazwa") : null,
            Province = wojewodztwo != null ? DynamicPropertyHelper.GetString(wojewodztwo, "Nazwa") : null,
            District = powiat != null ? DynamicPropertyHelper.GetString(powiat, "Nazwa") : null,
            Municipality = gmina != null ? DynamicPropertyHelper.GetString(gmina, "Nazwa") : null
        };
    }

    #endregion
}
