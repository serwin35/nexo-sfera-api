using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using InsERT.Moria.Klienci;

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
            var sfera = _sferaService.GetSfera();
            var pracownicy = sfera.Podmioty().Dane.WszyscyPracownicy();

            if (activeOnly == true)
            {
                pracownicy = pracownicy.Where(p => p.Aktywny == true);
            }

            if (!string.IsNullOrEmpty(search))
            {
                var searchLower = search.ToLower();
                pracownicy = pracownicy.Where(p =>
                    (p.Osoba.Imie != null && p.Osoba.Imie.ToLower().Contains(searchLower)) ||
                    (p.Osoba.Nazwisko != null && p.Osoba.Nazwisko.ToLower().Contains(searchLower)) ||
                    (p.Symbol != null && p.Symbol.ToLower().Contains(searchLower)));
            }

            var totalCount = pracownicy.Count();
            var items = pracownicy
                .OrderBy(p => p.Osoba.Nazwisko)
                .ThenBy(p => p.Osoba.Imie)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList()
                .Select(MapEmployeeSummary)
                .ToList();

            return Ok(new PagedResponse<EmployeeSummaryDto>
            {
                Data = items,
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
            var sfera = _sferaService.GetSfera();
            var pracownik = sfera.Podmioty().Dane.WszyscyPracownicy()
                .FirstOrDefault(p => p.Osoba.Pracownik.Id == id);

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
            var sfera = _sferaService.GetSfera();
            var pracownik = sfera.Podmioty().Dane.WszyscyPracownicy()
                .FirstOrDefault(p => p.Symbol == symbol);

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
            var sfera = _sferaService.GetSfera();
            var pracownik = sfera.Podmioty().Dane.WszyscyPracownicy()
                .FirstOrDefault(p => p.Osoba.PESEL == pesel);

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

    private static EmployeeSummaryDto MapEmployeeSummary(IPodmiot p)
    {
        var primaryEmail = p.Kontakty?.FirstOrDefault(k => k.Rodzaj?.Nazwa?.ToLower().Contains("email") == true && k.Podstawowy == true)?.Wartosc
                        ?? p.Kontakty?.FirstOrDefault(k => k.Rodzaj?.Nazwa?.ToLower().Contains("email") == true)?.Wartosc;

        var primaryPhone = p.Kontakty?.FirstOrDefault(k => k.Rodzaj?.Nazwa?.ToLower().Contains("telefon") == true && k.Podstawowy == true)?.Wartosc
                        ?? p.Kontakty?.FirstOrDefault(k => k.Rodzaj?.Nazwa?.ToLower().Contains("telefon") == true)?.Wartosc;

        return new EmployeeSummaryDto
        {
            Id = p.Osoba?.Pracownik?.Id ?? 0,
            Symbol = p.Symbol,
            FirstName = p.Osoba?.Imie,
            LastName = p.Osoba?.Nazwisko,
            FullName = $"{p.Osoba?.Imie} {p.Osoba?.Nazwisko}".Trim(),
            Email = primaryEmail,
            Phone = primaryPhone,
            IsActive = p.Aktywny ?? false
        };
    }

    private static EmployeeDto MapEmployee(IPodmiot p, bool includeContacts)
    {
        var primaryEmail = p.Kontakty?.FirstOrDefault(k => k.Rodzaj?.Nazwa?.ToLower().Contains("email") == true && k.Podstawowy == true)?.Wartosc
                        ?? p.Kontakty?.FirstOrDefault(k => k.Rodzaj?.Nazwa?.ToLower().Contains("email") == true)?.Wartosc;

        var primaryPhone = p.Kontakty?.FirstOrDefault(k => k.Rodzaj?.Nazwa?.ToLower().Contains("telefon") == true && k.Podstawowy == true)?.Wartosc
                        ?? p.Kontakty?.FirstOrDefault(k => k.Rodzaj?.Nazwa?.ToLower().Contains("telefon") == true)?.Wartosc;

        var dto = new EmployeeDto
        {
            Id = p.Osoba?.Pracownik?.Id ?? 0,
            Symbol = p.Symbol,
            FirstName = p.Osoba?.Imie,
            LastName = p.Osoba?.Nazwisko,
            FullName = $"{p.Osoba?.Imie} {p.Osoba?.Nazwisko}".Trim(),
            Pesel = p.Osoba?.PESEL,
            BirthDate = p.Osoba?.DataUrodzenia,
            Gender = p.Osoba?.Plec switch
            {
                (int)Plec.Mezczyzna => "Male",
                (int)Plec.Kobieta => "Female",
                _ => "Unknown"
            },
            Email = primaryEmail,
            Phone = primaryPhone,
            IsActive = p.Aktywny ?? false
        };

        // Map address
        if (p.AdresPodstawowy != null)
        {
            dto.Address = MapAddress(p.AdresPodstawowy);
        }

        // Map correspondence address
        if (p.DomyslnyAdresKorespondencyjny != null)
        {
            dto.CorrespondenceAddress = MapAddress(p.DomyslnyAdresKorespondencyjny);
        }

        // Map contacts
        if (includeContacts && p.Kontakty != null)
        {
            dto.Contacts = p.Kontakty.Select(k => new EmployeeContactDto
            {
                Id = k.Id,
                Type = k.Rodzaj?.Nazwa,
                Value = k.Wartosc,
                IsPrimary = k.Podstawowy ?? false,
                Comment = k.Komentarz
            }).ToList();
        }

        return dto;
    }

    private static EmployeeAddressDto? MapAddress(dynamic? adres)
    {
        if (adres == null) return null;

        return new EmployeeAddressDto
        {
            Name = adres.Nazwa,
            Street = adres.Szczegoly?.Ulica,
            BuildingNumber = adres.Szczegoly?.NrDomu,
            ApartmentNumber = adres.Szczegoly?.NrLokalu,
            PostalCode = adres.Szczegoly?.KodPocztowy,
            City = adres.Szczegoly?.Miejscowosc,
            PostOffice = adres.Szczegoly?.Poczta,
            Country = adres.Panstwo?.Nazwa,
            Province = adres.Szczegoly?.Wojewodztwo?.Nazwa,
            District = adres.Szczegoly?.Gmina?.Powiat?.Nazwa,
            Municipality = adres.Szczegoly?.Gmina?.Nazwa
        };
    }

    #endregion
}
