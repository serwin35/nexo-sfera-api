using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// PPK (Pracownicze Plany Kapitalowe / Employee Capital Plans) management endpoints
/// </summary>
[ApiController]
[Route("api/hr/ppk")]
[Authorize]
[Tags("PPK")]
public class PPKController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<PPKController> _logger;

    public PPKController(ISferaService sferaService, ILogger<PPKController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get PPK entries list
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<PPKEntryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<PPKEntryDto>>> GetPPKEntries(
        [FromQuery] int? employeeId = null,
        [FromQuery] int? year = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var (items, totalCount) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("PPK");
                if (manager == null) return (null, -1);

                var allEntries = DynamicPropertyHelper.SafeGetAll((object)manager);

                if (employeeId.HasValue)
                {
                    allEntries = allEntries.Where(e =>
                    {
                        var pracownik = DynamicPropertyHelper.GetProperty(e, "Pracownik");
                        return pracownik != null && DynamicPropertyHelper.GetId(pracownik) == employeeId.Value;
                    }).ToList();
                }

                if (year.HasValue)
                {
                    allEntries = allEntries.Where(e =>
                    {
                        var okres = DynamicPropertyHelper.GetString(e, "Okres") ?? "";
                        return okres.Contains(year.Value.ToString());
                    }).ToList();
                }

                var count = allEntries.Count;

                var pagedEntries = allEntries
                    .OrderByDescending(e => DynamicPropertyHelper.GetString(e, "Okres") ?? "")
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var result = new List<PPKEntryDto>();
                foreach (var e in pagedEntries)
                {
                    result.Add(MapPPKEntry(e));
                }

                return (result, count);
            });

            if (totalCount == -1)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get PPK manager"));

            return Ok(new PagedResponse<PPKEntryDto>
            {
                Data = items!,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting PPK entries");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving PPK entries", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get PPK entry by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<PPKEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PPKEntryDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PPKEntryDto>>> GetPPKEntry(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("PPK");
                if (manager == null) return (PPKEntryDto?)null;

                var item = DynamicPropertyHelper.FindById((object)manager, id);
                if (item == null) return (PPKEntryDto?)null;

                return (PPKEntryDto?)MapPPKEntry(item);
            });

            if (result == null)
                return NotFound(ApiResponse<PPKEntryDto>.Error($"PPK entry with ID {id} not found"));

            return Ok(ApiResponse<PPKEntryDto>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting PPK entry {Id}", id);
            return StatusCode(500, ApiResponse<PPKEntryDto>.Error("Error retrieving PPK entry", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get PPK summary (total contributions and participant counts)
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<PPKSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PPKSummaryDto>>> GetPPKSummary([FromQuery] int? year = null)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("PPK");
                if (manager == null) return (PPKSummaryDto?)null;

                var allEntries = DynamicPropertyHelper.SafeGetAll((object)manager);

                var filterYear = year ?? DateTime.Now.Year;

                var yearEntries = allEntries.Where(e =>
                {
                    var okres = DynamicPropertyHelper.GetString(e, "Okres") ?? "";
                    return okres.Contains(filterYear.ToString());
                }).ToList();

                var employeeIds = new HashSet<int>();
                var activeParticipantIds = new HashSet<int>();
                decimal totalEmployeeContributions = 0;
                decimal totalEmployerContributions = 0;

                foreach (var e in yearEntries)
                {
                    var pracownik = DynamicPropertyHelper.GetProperty(e, "Pracownik");
                    var empId = pracownik != null ? DynamicPropertyHelper.GetId(pracownik) : 0;
                    if (empId > 0)
                    {
                        employeeIds.Add(empId);
                        var status = DynamicPropertyHelper.GetString(e, "Status") ?? "";
                        if (!status.ToLower().Contains("rezygnacja"))
                        {
                            activeParticipantIds.Add(empId);
                        }
                    }

                    totalEmployeeContributions += DynamicPropertyHelper.GetDecimal(e, "SkladkaPracownika")
                        + DynamicPropertyHelper.GetDecimal(e, "DodatkowaSkladkaPracownika");
                    totalEmployerContributions += DynamicPropertyHelper.GetDecimal(e, "SkladkaPracodawcy")
                        + DynamicPropertyHelper.GetDecimal(e, "DodatkowaSkladkaPracodawcy");
                }

                return (PPKSummaryDto?)new PPKSummaryDto
                {
                    TotalEmployees = employeeIds.Count,
                    ActiveParticipants = activeParticipantIds.Count,
                    TotalEmployeeContributions = totalEmployeeContributions,
                    TotalEmployerContributions = totalEmployerContributions,
                    Year = filterYear
                };
            });

            if (result == null)
                return StatusCode(500, ApiResponse<PPKSummaryDto>.Error("Failed to get PPK manager"));

            return Ok(ApiResponse<PPKSummaryDto>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting PPK summary");
            return StatusCode(500, ApiResponse<PPKSummaryDto>.Error("Error retrieving PPK summary", new List<string> { ex.Message }));
        }
    }

    #region Mapping

    private static PPKEntryDto MapPPKEntry(dynamic e)
    {
        var pracownik = DynamicPropertyHelper.GetProperty(e, "Pracownik");
        var osoba = pracownik != null ? DynamicPropertyHelper.GetProperty(pracownik, "Osoba") : null;

        var imie = osoba != null ? DynamicPropertyHelper.GetString(osoba, "Imie") ?? "" : "";
        var nazwisko = osoba != null ? DynamicPropertyHelper.GetString(osoba, "Nazwisko") ?? "" : "";

        var skladkaPracownika = DynamicPropertyHelper.GetDecimal(e, "SkladkaPracownika");
        var skladkaPracodawcy = DynamicPropertyHelper.GetDecimal(e, "SkladkaPracodawcy");
        var dodSkladkaPracownika = DynamicPropertyHelper.GetDecimal(e, "DodatkowaSkladkaPracownika");
        var dodSkladkaPracodawcy = DynamicPropertyHelper.GetDecimal(e, "DodatkowaSkladkaPracodawcy");

        return new PPKEntryDto
        {
            Id = DynamicPropertyHelper.GetId(e),
            EmployeeId = pracownik != null ? DynamicPropertyHelper.GetId(pracownik) : 0,
            EmployeeName = $"{imie} {nazwisko}".Trim(),
            Period = DynamicPropertyHelper.GetString(e, "Okres"),
            EmployeeContribution = skladkaPracownika,
            EmployerContribution = skladkaPracodawcy,
            AdditionalEmployeeContribution = dodSkladkaPracownika,
            AdditionalEmployerContribution = dodSkladkaPracodawcy,
            TotalContribution = skladkaPracownika + skladkaPracodawcy + dodSkladkaPracownika + dodSkladkaPracodawcy,
            Status = DynamicPropertyHelper.GetString(e, "Status")
        };
    }

    #endregion
}

#region DTOs

/// <summary>
/// PPK (Employee Capital Plans) entry DTO
/// </summary>
public class PPKEntryDto
{
    public int Id { get; set; }
    /// <summary>PracownikId</summary>
    public int EmployeeId { get; set; }
    /// <summary>NazwaPracownika</summary>
    public string? EmployeeName { get; set; }
    /// <summary>Okres</summary>
    public string? Period { get; set; }
    /// <summary>SkladkaPracownika</summary>
    public decimal EmployeeContribution { get; set; }
    /// <summary>SkladkaPracodawcy</summary>
    public decimal EmployerContribution { get; set; }
    /// <summary>DodatkowaSkladkaPracownika</summary>
    public decimal AdditionalEmployeeContribution { get; set; }
    /// <summary>DodatkowaSkladkaPracodawcy</summary>
    public decimal AdditionalEmployerContribution { get; set; }
    /// <summary>SumaSkadek</summary>
    public decimal TotalContribution { get; set; }
    /// <summary>Status</summary>
    public string? Status { get; set; }
}

/// <summary>
/// PPK summary DTO
/// </summary>
public class PPKSummaryDto
{
    public int TotalEmployees { get; set; }
    public int ActiveParticipants { get; set; }
    public decimal TotalEmployeeContributions { get; set; }
    public decimal TotalEmployerContributions { get; set; }
    public int Year { get; set; }
}

#endregion
