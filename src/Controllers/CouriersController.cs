using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Courier services management
/// </summary>
[ApiController]
[Route("api/couriers")]
[Authorize]
[Tags("Couriers")]
public class CouriersController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<CouriersController> _logger;

    public CouriersController(ISferaService sferaService, ILogger<CouriersController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get courier services
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<CourierDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<CourierDto>>>> GetCouriers()
    {
        try
        {
            var items = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("Kurierzy");
                if (manager == null)
                    return null;

                var allItems = DynamicPropertyHelper.SafeGetAll((object)manager);
                var result = new List<CourierDto>();

                foreach (var c in allItems)
                {
                    result.Add(new CourierDto
                    {
                        Id = DynamicPropertyHelper.GetId(c),
                        Name = DynamicPropertyHelper.GetString(c, "Nazwa") ?? "",
                        Code = DynamicPropertyHelper.GetString(c, "Kod"),
                        IsActive = DynamicPropertyHelper.GetBool(c, "Aktywny"),
                        TrackingUrlTemplate = DynamicPropertyHelper.GetString(c, "SzablonUrlSledzenia")
                    });
                }

                return result;
            });

            if (items == null)
                return StatusCode(500, ApiResponse<List<CourierDto>>.Error("Failed to get Kurierzy manager"));

            return Ok(ApiResponse<List<CourierDto>>.Ok(items));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting couriers");
            return StatusCode(500, ApiResponse<List<CourierDto>>.Error("Error retrieving couriers", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get courier by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<CourierDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CourierDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CourierDto>>> GetCourier(int id)
    {
        try
        {
            var (managerNull, dto) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("Kurierzy");
                if (manager == null)
                    return (true, (CourierDto?)null);

                var allItems = DynamicPropertyHelper.SafeGetAll((object)manager);
                var item = allItems.FirstOrDefault(c => DynamicPropertyHelper.GetId(c) == id);

                if (item == null)
                    return (false, (CourierDto?)null);

                return (false, new CourierDto
                {
                    Id = DynamicPropertyHelper.GetId(item),
                    Name = DynamicPropertyHelper.GetString(item, "Nazwa") ?? "",
                    Code = DynamicPropertyHelper.GetString(item, "Kod"),
                    IsActive = DynamicPropertyHelper.GetBool(item, "Aktywny"),
                    TrackingUrlTemplate = DynamicPropertyHelper.GetString(item, "SzablonUrlSledzenia")
                });
            });

            if (managerNull)
                return StatusCode(500, ApiResponse<CourierDto>.Error("Failed to get Kurierzy manager"));

            if (dto == null)
                return NotFound(ApiResponse<CourierDto>.Error($"Courier with ID {id} not found"));

            return Ok(ApiResponse<CourierDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting courier {Id}", id);
            return StatusCode(500, ApiResponse<CourierDto>.Error("Error retrieving courier", new List<string> { ex.Message }));
        }
    }
}

#region DTOs

/// <summary>
/// Courier service DTO
/// </summary>
public class CourierDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public bool IsActive { get; set; }
    public string? TrackingUrlTemplate { get; set; }
}

#endregion
