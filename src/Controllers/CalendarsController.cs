using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Calendars (Kalendarze) management endpoints
/// </summary>
[ApiController]
[Route("api/calendars")]
[Authorize]
[Tags("Calendars")]
public class CalendarsController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<CalendarsController> _logger;

    public CalendarsController(ISferaService sferaService, ILogger<CalendarsController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get all calendars
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<CalendarDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<CalendarDto>>>> GetCalendars()
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("Kalendarze");
                if (manager == null) return (List<CalendarDto>?)null;

                var allCalendars = DynamicPropertyHelper.SafeGetAll((object)manager);

                var items = new List<CalendarDto>();
                foreach (var c in allCalendars.OrderBy(c => DynamicPropertyHelper.GetString(c, "Nazwa") ?? ""))
                {
                    items.Add(MapCalendar(c));
                }

                return items;
            });

            if (result == null)
                return StatusCode(500, ApiResponse<object>.Error("Kalendarze manager not available"));

            return Ok(ApiResponse<List<CalendarDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting calendars");
            return StatusCode(500, ApiResponse<List<CalendarDto>>.Error("Error retrieving calendars", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get calendar by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<CalendarDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CalendarDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CalendarDto>>> GetCalendar(int id)
    {
        try
        {
            var (managerNull, dto) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("Kalendarze");
                if (manager == null) return (true, (CalendarDto?)null);

                var allCalendars = DynamicPropertyHelper.SafeGetAll((object)manager);
                var calendar = allCalendars.FirstOrDefault(c => DynamicPropertyHelper.GetId(c) == id);

                if (calendar == null)
                    return (false, (CalendarDto?)null);

                return (false, MapCalendar(calendar));
            });

            if (managerNull)
                return StatusCode(500, ApiResponse<object>.Error("Kalendarze manager not available"));

            if (dto == null)
                return NotFound(ApiResponse<CalendarDto>.Error($"Calendar with ID {id} not found"));

            return Ok(ApiResponse<CalendarDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting calendar {Id}", id);
            return StatusCode(500, ApiResponse<CalendarDto>.Error("Error retrieving calendar", new List<string> { ex.Message }));
        }
    }

    #region Mapping

    private static CalendarDto MapCalendar(dynamic c)
    {
        return new CalendarDto
        {
            Id = DynamicPropertyHelper.GetId(c),
            Name = DynamicPropertyHelper.GetString(c, "Nazwa"),
            Description = DynamicPropertyHelper.GetString(c, "Opis"),
            IsActive = DynamicPropertyHelper.GetBool(c, "Aktywny"),
            CreatedDate = DynamicPropertyHelper.GetDateTime(c, "DataUtworzenia")
        };
    }

    #endregion
}

#region DTOs

/// <summary>
/// Calendar DTO
/// </summary>
public class CalendarDto
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime? CreatedDate { get; set; }
}

#endregion
