using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Absences (Absencje) management endpoints
/// </summary>
[ApiController]
[Route("api/hr/absences")]
[Authorize]
[Tags("Absences")]
public class AbsencesController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<AbsencesController> _logger;

    public AbsencesController(ISferaService sferaService, ILogger<AbsencesController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get absences list
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<AbsenceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<AbsenceDto>>> GetAbsences(
        [FromQuery] int? employeeId = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] string? type = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var (items, totalCount) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("Absencje");
                if (manager == null) return (null, -1);

                var allAbsences = DynamicPropertyHelper.SafeGetAll((object)manager);

                if (employeeId.HasValue)
                {
                    allAbsences = allAbsences.Where(a =>
                    {
                        var pracownik = DynamicPropertyHelper.GetProperty(a, "Pracownik");
                        return pracownik != null && DynamicPropertyHelper.GetId(pracownik) == employeeId.Value;
                    }).ToList();
                }

                if (dateFrom.HasValue)
                {
                    allAbsences = allAbsences.Where(a =>
                    {
                        var dataOd = DynamicPropertyHelper.GetDateTime(a, "DataOd");
                        return dataOd.HasValue && dataOd.Value >= dateFrom.Value;
                    }).ToList();
                }

                if (dateTo.HasValue)
                {
                    allAbsences = allAbsences.Where(a =>
                    {
                        var dataDo = DynamicPropertyHelper.GetDateTime(a, "DataDo");
                        return dataDo.HasValue && dataDo.Value <= dateTo.Value;
                    }).ToList();
                }

                if (!string.IsNullOrEmpty(type))
                {
                    var typeLower = type.ToLower();
                    allAbsences = allAbsences.Where(a =>
                    {
                        var typAbsencji = DynamicPropertyHelper.GetString(a, "TypAbsencji") ?? "";
                        return typAbsencji.ToLower().Contains(typeLower);
                    }).ToList();
                }

                var count = allAbsences.Count;

                var pagedAbsences = allAbsences
                    .OrderByDescending(a => DynamicPropertyHelper.GetDateTime(a, "DataOd") ?? DateTime.MinValue)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var result = new List<AbsenceDto>();
                foreach (var a in pagedAbsences)
                {
                    result.Add(MapAbsence(a));
                }

                return (result, count);
            });

            if (totalCount == -1)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Absencje manager"));

            return Ok(new PagedResponse<AbsenceDto>
            {
                Data = items!,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting absences");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving absences", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get absence by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<AbsenceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AbsenceDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AbsenceDto>>> GetAbsence(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("Absencje");
                if (manager == null) return (AbsenceDto?)null;

                var item = DynamicPropertyHelper.FindById((object)manager, id);
                if (item == null) return (AbsenceDto?)null;

                return (AbsenceDto?)MapAbsence(item);
            });

            if (result == null)
                return NotFound(ApiResponse<AbsenceDto>.Error($"Absence with ID {id} not found"));

            return Ok(ApiResponse<AbsenceDto>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting absence {Id}", id);
            return StatusCode(500, ApiResponse<AbsenceDto>.Error("Error retrieving absence", new List<string> { ex.Message }));
        }
    }

    #region Mapping

    private static AbsenceDto MapAbsence(dynamic a)
    {
        var pracownik = DynamicPropertyHelper.GetProperty(a, "Pracownik");
        var osoba = pracownik != null ? DynamicPropertyHelper.GetProperty(pracownik, "Osoba") : null;

        var imie = osoba != null ? DynamicPropertyHelper.GetString(osoba, "Imie") ?? "" : "";
        var nazwisko = osoba != null ? DynamicPropertyHelper.GetString(osoba, "Nazwisko") ?? "" : "";

        return new AbsenceDto
        {
            Id = DynamicPropertyHelper.GetId(a),
            EmployeeId = pracownik != null ? DynamicPropertyHelper.GetId(pracownik) : 0,
            EmployeeName = $"{imie} {nazwisko}".Trim(),
            AbsenceType = DynamicPropertyHelper.GetString(a, "TypAbsencji"),
            DateFrom = DynamicPropertyHelper.GetDateTime(a, "DataOd"),
            DateTo = DynamicPropertyHelper.GetDateTime(a, "DataDo"),
            DaysCount = DynamicPropertyHelper.GetNullableInt(a, "LiczbaDni"),
            Status = DynamicPropertyHelper.GetString(a, "Status"),
            Description = DynamicPropertyHelper.GetString(a, "Opis"),
            // SDK 60.0.0: New property for RSA integration
            SendRSAWithCode350 = DynamicPropertyHelper.GetNullableBool(a, "WysylajRSAZKodem350")
        };
    }

    #endregion
}

#region DTOs

/// <summary>
/// Absence (Absencja) DTO
/// </summary>
public class AbsenceDto
{
    public int Id { get; set; }
    /// <summary>PracownikId</summary>
    public int EmployeeId { get; set; }
    /// <summary>NazwaPracownika</summary>
    public string? EmployeeName { get; set; }
    /// <summary>TypAbsencji</summary>
    public string? AbsenceType { get; set; }
    /// <summary>DataOd</summary>
    public DateTime? DateFrom { get; set; }
    /// <summary>DataDo</summary>
    public DateTime? DateTo { get; set; }
    /// <summary>LiczbaDni</summary>
    public int? DaysCount { get; set; }
    /// <summary>Status</summary>
    public string? Status { get; set; }
    /// <summary>Opis</summary>
    public string? Description { get; set; }
    /// <summary>WysylajRSAZKodem350 - SDK 60.0.0: new property for RSA integration</summary>
    public bool? SendRSAWithCode350 { get; set; }
}

#endregion
