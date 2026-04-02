using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// HR Contracts (Umowy) management endpoints
/// </summary>
[ApiController]
[Route("api/hr/contracts")]
[Authorize]
[Tags("HR Contracts")]
public class ContractsController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<ContractsController> _logger;

    public ContractsController(ISferaService sferaService, ILogger<ContractsController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get employment contracts list
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<EmploymentContractDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<EmploymentContractDto>>> GetContracts(
        [FromQuery] int? employeeId = null,
        [FromQuery] string? type = null,
        [FromQuery] bool? active = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var (items, totalCount) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("Umowy");
                if (manager == null) return (null, -1);

                var allContracts = DynamicPropertyHelper.SafeGetAll((object)manager);

                if (employeeId.HasValue)
                {
                    allContracts = allContracts.Where(c =>
                    {
                        var pracownik = DynamicPropertyHelper.GetProperty(c, "Pracownik");
                        return pracownik != null && DynamicPropertyHelper.GetId(pracownik) == employeeId.Value;
                    }).ToList();
                }

                if (!string.IsNullOrEmpty(type))
                {
                    var typeLower = type.ToLower();
                    allContracts = allContracts.Where(c =>
                    {
                        var typUmowy = DynamicPropertyHelper.GetString(c, "TypUmowy") ?? "";
                        return typUmowy.ToLower().Contains(typeLower);
                    }).ToList();
                }

                if (active.HasValue)
                {
                    allContracts = allContracts.Where(c =>
                        DynamicPropertyHelper.GetBool(c, "Aktywna") == active.Value).ToList();
                }

                var count = allContracts.Count;

                var pagedContracts = allContracts
                    .OrderByDescending(c => DynamicPropertyHelper.GetDateTime(c, "DataOd") ?? DateTime.MinValue)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var result = new List<EmploymentContractDto>();
                foreach (var c in pagedContracts)
                {
                    result.Add(MapContract(c));
                }

                return (result, count);
            });

            if (totalCount == -1)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Umowy manager"));

            return Ok(new PagedResponse<EmploymentContractDto>
            {
                Data = items!,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting contracts");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving contracts", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get employment contract by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<EmploymentContractDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EmploymentContractDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<EmploymentContractDto>>> GetContract(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("Umowy");
                if (manager == null) return (EmploymentContractDto?)null;

                var contract = DynamicPropertyHelper.FindById((object)manager, id);
                if (contract == null) return (EmploymentContractDto?)null;

                return (EmploymentContractDto?)MapContract(contract);
            });

            if (result == null)
                return NotFound(ApiResponse<EmploymentContractDto>.Error($"Contract with ID {id} not found"));

            return Ok(ApiResponse<EmploymentContractDto>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting contract {Id}", id);
            return StatusCode(500, ApiResponse<EmploymentContractDto>.Error("Error retrieving contract", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get contracts for a specific employee
    /// </summary>
    [HttpGet("for-employee/{employeeId}")]
    [ProducesResponseType(typeof(ApiResponse<List<EmploymentContractDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<EmploymentContractDto>>>> GetContractsForEmployee(int employeeId)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("Umowy");
                if (manager == null) return (List<EmploymentContractDto>?)null;

                var allContracts = DynamicPropertyHelper.SafeGetAll((object)manager);

                var employeeContracts = allContracts.Where(c =>
                {
                    var pracownik = DynamicPropertyHelper.GetProperty(c, "Pracownik");
                    return pracownik != null && DynamicPropertyHelper.GetId(pracownik) == employeeId;
                })
                .OrderByDescending(c => DynamicPropertyHelper.GetDateTime(c, "DataOd") ?? DateTime.MinValue)
                .ToList();

                var mapped = new List<EmploymentContractDto>();
                foreach (var c in employeeContracts)
                {
                    mapped.Add(MapContract(c));
                }

                return mapped;
            });

            if (result == null)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Umowy manager"));

            return Ok(ApiResponse<List<EmploymentContractDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting contracts for employee {EmployeeId}", employeeId);
            return StatusCode(500, ApiResponse<List<EmploymentContractDto>>.Error("Error retrieving contracts", new List<string> { ex.Message }));
        }
    }

    #region Mapping

    private static EmploymentContractDto MapContract(dynamic c)
    {
        var pracownik = DynamicPropertyHelper.GetProperty(c, "Pracownik");
        var osoba = pracownik != null ? DynamicPropertyHelper.GetProperty(pracownik, "Osoba") : null;

        var imie = osoba != null ? DynamicPropertyHelper.GetString(osoba, "Imie") ?? "" : "";
        var nazwisko = osoba != null ? DynamicPropertyHelper.GetString(osoba, "Nazwisko") ?? "" : "";

        return new EmploymentContractDto
        {
            Id = DynamicPropertyHelper.GetId(c),
            EmployeeId = pracownik != null ? DynamicPropertyHelper.GetId(pracownik) : 0,
            EmployeeName = $"{imie} {nazwisko}".Trim(),
            ContractType = DynamicPropertyHelper.GetString(c, "TypUmowy"),
            ContractNumber = DynamicPropertyHelper.GetString(c, "NumerUmowy"),
            StartDate = DynamicPropertyHelper.GetDateTime(c, "DataOd"),
            EndDate = DynamicPropertyHelper.GetDateTime(c, "DataDo"),
            Position = DynamicPropertyHelper.GetString(c, "Stanowisko"),
            Department = DynamicPropertyHelper.GetString(c, "Dzial"),
            BaseSalary = DynamicPropertyHelper.GetNullableDecimal(c, "WynagrodzeniePodstawowe"),
            WorkingHours = DynamicPropertyHelper.GetString(c, "WymiarEtatu"),
            IsActive = DynamicPropertyHelper.GetBool(c, "Aktywna"),
            Description = DynamicPropertyHelper.GetString(c, "Opis")
        };
    }

    #endregion
}

#region DTOs

/// <summary>
/// Employment contract (Umowa) DTO
/// </summary>
public class EmploymentContractDto
{
    public int Id { get; set; }
    /// <summary>PracownikId</summary>
    public int EmployeeId { get; set; }
    /// <summary>NazwaPracownika</summary>
    public string? EmployeeName { get; set; }
    /// <summary>TypUmowy</summary>
    public string? ContractType { get; set; }
    /// <summary>NumerUmowy</summary>
    public string? ContractNumber { get; set; }
    /// <summary>DataOd</summary>
    public DateTime? StartDate { get; set; }
    /// <summary>DataDo</summary>
    public DateTime? EndDate { get; set; }
    /// <summary>Stanowisko</summary>
    public string? Position { get; set; }
    /// <summary>Dzial</summary>
    public string? Department { get; set; }
    /// <summary>WynagrodzeniePodstawowe</summary>
    public decimal? BaseSalary { get; set; }
    /// <summary>WymiarEtatu</summary>
    public string? WorkingHours { get; set; }
    /// <summary>Aktywna</summary>
    public bool IsActive { get; set; }
    /// <summary>Opis</summary>
    public string? Description { get; set; }
}

#endregion
