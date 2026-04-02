using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Fleet / Vehicles (Pojazdy) management endpoints
/// </summary>
[ApiController]
[Route("api/fleet")]
[Authorize]
[Tags("Fleet")]
public class FleetController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<FleetController> _logger;

    public FleetController(ISferaService sferaService, ILogger<FleetController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get vehicles (paged, with optional filters)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<VehicleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<VehicleDto>>> GetVehicles(
        [FromQuery] string? status = null,
        [FromQuery] string? type = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var (items, totalCount) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("Pojazdy");
                if (manager == null) return (null, -1);

                var allVehicles = DynamicPropertyHelper.SafeGetAll((object)manager);

                if (!string.IsNullOrEmpty(status))
                {
                    var statusLower = status.ToLower();
                    allVehicles = allVehicles.Where(v =>
                    {
                        var isActive = DynamicPropertyHelper.GetBool(v, "Aktywny");
                        return statusLower == "active" ? isActive : !isActive;
                    }).ToList();
                }

                if (!string.IsNullOrEmpty(type))
                {
                    var typeLower = type.ToLower();
                    allVehicles = allVehicles.Where(v =>
                    {
                        var t = DynamicPropertyHelper.GetString(v, "TypPaliwa") ?? "";
                        return t.ToLower().Contains(typeLower);
                    }).ToList();
                }

                var count = allVehicles.Count;
                var pagedVehicles = allVehicles
                    .OrderBy(v => DynamicPropertyHelper.GetString(v, "NumerRejestracyjny") ?? "")
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var result = new List<VehicleDto>();
                foreach (var v in pagedVehicles)
                {
                    result.Add(MapVehicle(v));
                }

                return (result, count);
            });

            if (totalCount == -1)
                return StatusCode(500, ApiResponse<object>.Error("Pojazdy manager not available"));

            return Ok(new PagedResponse<VehicleDto>
            {
                Data = items!,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting vehicles");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving vehicles", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get vehicle by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<VehicleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<VehicleDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<VehicleDto>>> GetVehicle(int id)
    {
        try
        {
            var (managerNull, dto) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("Pojazdy");
                if (manager == null) return (true, (VehicleDto?)null);

                var allVehicles = DynamicPropertyHelper.SafeGetAll((object)manager);
                var vehicle = allVehicles.FirstOrDefault(v => DynamicPropertyHelper.GetId(v) == id);

                if (vehicle == null)
                    return (false, (VehicleDto?)null);

                return (false, MapVehicle(vehicle));
            });

            if (managerNull)
                return StatusCode(500, ApiResponse<object>.Error("Pojazdy manager not available"));

            if (dto == null)
                return NotFound(ApiResponse<VehicleDto>.Error($"Vehicle with ID {id} not found"));

            return Ok(ApiResponse<VehicleDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting vehicle {Id}", id);
            return StatusCode(500, ApiResponse<VehicleDto>.Error("Error retrieving vehicle", new List<string> { ex.Message }));
        }
    }

    #region Mapping

    private static VehicleDto MapVehicle(dynamic v)
    {
        return new VehicleDto
        {
            Id = DynamicPropertyHelper.GetId(v),
            RegistrationNumber = DynamicPropertyHelper.GetString(v, "NumerRejestracyjny"),
            Brand = DynamicPropertyHelper.GetString(v, "Marka"),
            Model = DynamicPropertyHelper.GetString(v, "Model"),
            VIN = DynamicPropertyHelper.GetString(v, "VIN"),
            ProductionYear = DynamicPropertyHelper.GetNullableInt(v, "RokProdukcji"),
            FuelType = DynamicPropertyHelper.GetString(v, "TypPaliwa"),
            Mileage = DynamicPropertyHelper.GetDecimal(v, "Przebieg"),
            InsuranceExpiryDate = DynamicPropertyHelper.GetDateTime(v, "DataWaznosci"),
            TechnicalInspectionDate = DynamicPropertyHelper.GetDateTime(v, "DataPrzegladu"),
            IsActive = DynamicPropertyHelper.GetBool(v, "Aktywny"),
            AssignedTo = DynamicPropertyHelper.GetString(v, "PrzypisanyDo"),
            Description = DynamicPropertyHelper.GetString(v, "Opis")
        };
    }

    #endregion
}

#region DTOs

/// <summary>
/// Vehicle (Pojazd) DTO
/// </summary>
public class VehicleDto
{
    public int Id { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? VIN { get; set; }
    public int? ProductionYear { get; set; }
    public string? FuelType { get; set; }
    public decimal Mileage { get; set; }
    public DateTime? InsuranceExpiryDate { get; set; }
    public DateTime? TechnicalInspectionDate { get; set; }
    public bool IsActive { get; set; }
    public string? AssignedTo { get; set; }
    public string? Description { get; set; }
}

#endregion
