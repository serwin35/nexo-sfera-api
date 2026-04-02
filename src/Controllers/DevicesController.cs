using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Devices (Urzadzenia) management endpoints - fiscal printers, readers, etc.
/// </summary>
[ApiController]
[Route("api/devices")]
[Authorize]
[Tags("Devices")]
public class DevicesController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<DevicesController> _logger;

    public DevicesController(ISferaService sferaService, ILogger<DevicesController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get all devices (fiscal printers, readers, etc.)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<DeviceDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<DeviceDto>>>> GetDevices()
    {
        try
        {
            var items = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("Urzadzenia");
                if (manager == null) return (List<DeviceDto>?)null;

                var allItems = DynamicPropertyHelper.SafeGetAll((object)manager);

                var result = new List<DeviceDto>();
                foreach (var e in allItems)
                {
                    result.Add(MapDevice(e));
                }

                return result;
            });

            if (items == null)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Urzadzenia manager"));

            return Ok(ApiResponse<List<DeviceDto>>.Ok(items));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting devices");
            return StatusCode(500, ApiResponse<List<DeviceDto>>.Error("Error retrieving devices", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get device by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<DeviceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<DeviceDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<DeviceDto>>> GetDevice(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("Urzadzenia");
                if (manager == null) return (managerNull: true, dto: (DeviceDto?)null);

                var allItems = DynamicPropertyHelper.SafeGetAll((object)manager);
                var entry = allItems.FirstOrDefault(e => DynamicPropertyHelper.GetId(e) == id);

                if (entry == null)
                    return (managerNull: false, dto: (DeviceDto?)null);

                return (managerNull: false, dto: (DeviceDto?)MapDevice(entry));
            });

            if (result.managerNull)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Urzadzenia manager"));

            if (result.dto == null)
                return NotFound(ApiResponse<DeviceDto>.Error($"Device with ID {id} not found"));

            return Ok(ApiResponse<DeviceDto>.Ok(result.dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting device {Id}", id);
            return StatusCode(500, ApiResponse<DeviceDto>.Error("Error retrieving device", new List<string> { ex.Message }));
        }
    }

    #region Helpers

    private static DeviceDto MapDevice(dynamic e)
    {
        return new DeviceDto
        {
            Id = DynamicPropertyHelper.GetId(e),
            Name = DynamicPropertyHelper.GetString(e, "Nazwa"),
            Type = DynamicPropertyHelper.GetString(e, "Typ"),
            SerialNumber = DynamicPropertyHelper.GetString(e, "NumerSeryjny"),
            IsActive = DynamicPropertyHelper.GetNullableBool(e, "Aktywny") ?? false,
            IsConnected = DynamicPropertyHelper.GetNullableBool(e, "Polaczony") ?? false,
            Location = DynamicPropertyHelper.GetString(e, "Lokalizacja"),
            LastActivityDate = DynamicPropertyHelper.GetDateTime(e, "DataOstatniejAktywnosci")
        };
    }

    #endregion
}

#region DTOs

/// <summary>
/// Device DTO (fiscal printers, readers, etc.)
/// </summary>
public class DeviceDto
{
    public int Id { get; set; }
    /// <summary>
    /// Device name (Nazwa)
    /// </summary>
    public string? Name { get; set; }
    /// <summary>
    /// Device type (Typ) - e.g. fiscal printer, barcode reader
    /// </summary>
    public string? Type { get; set; }
    /// <summary>
    /// Serial number (NumerSeryjny)
    /// </summary>
    public string? SerialNumber { get; set; }
    /// <summary>
    /// Whether the device is active (Aktywny)
    /// </summary>
    public bool IsActive { get; set; }
    /// <summary>
    /// Whether the device is currently connected (Polaczony)
    /// </summary>
    public bool IsConnected { get; set; }
    /// <summary>
    /// Device location (Lokalizacja)
    /// </summary>
    public string? Location { get; set; }
    /// <summary>
    /// Last activity date (DataOstatniejAktywnosci)
    /// </summary>
    public DateTime? LastActivityDate { get; set; }
}

#endregion
