using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Activities (Dzialania) management endpoints
/// </summary>
[ApiController]
[Route("api/activities")]
[Authorize]
[Tags("Activities")]
public class ActivitiesController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<ActivitiesController> _logger;

    public ActivitiesController(ISferaService sferaService, ILogger<ActivitiesController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get activities (paged, with optional filters)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ActivityDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ActivityDto>>> GetActivities(
        [FromQuery] string? type = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] string? assignedTo = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var (items, totalCount) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("Dzialania");
                if (manager == null) return (null, -1);

                var allActivities = DynamicPropertyHelper.SafeGetAll((object)manager);

                if (!string.IsNullOrEmpty(type))
                {
                    var typeLower = type.ToLower();
                    allActivities = allActivities.Where(a =>
                    {
                        var typ = DynamicPropertyHelper.GetString(a, "Typ") ?? "";
                        return typ.ToLower().Contains(typeLower);
                    }).ToList();
                }

                if (!string.IsNullOrEmpty(status))
                {
                    var statusLower = status.ToLower();
                    allActivities = allActivities.Where(a =>
                    {
                        var s = DynamicPropertyHelper.GetString(a, "Status") ?? "";
                        return s.ToLower().Contains(statusLower);
                    }).ToList();
                }

                if (dateFrom.HasValue)
                {
                    allActivities = allActivities.Where(a =>
                    {
                        var date = DynamicPropertyHelper.GetDateTime(a, "DataRozpoczecia");
                        return date.HasValue && date.Value >= dateFrom.Value;
                    }).ToList();
                }

                if (dateTo.HasValue)
                {
                    allActivities = allActivities.Where(a =>
                    {
                        var date = DynamicPropertyHelper.GetDateTime(a, "DataRozpoczecia");
                        return date.HasValue && date.Value <= dateTo.Value;
                    }).ToList();
                }

                if (!string.IsNullOrEmpty(assignedTo))
                {
                    var assignedLower = assignedTo.ToLower();
                    allActivities = allActivities.Where(a =>
                    {
                        var assigned = DynamicPropertyHelper.GetString(a, "PrzypisanyDo") ?? "";
                        return assigned.ToLower().Contains(assignedLower);
                    }).ToList();
                }

                var count = allActivities.Count;
                var pagedActivities = allActivities
                    .OrderByDescending(a => DynamicPropertyHelper.GetDateTime(a, "DataRozpoczecia") ?? DateTime.MinValue)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var result = new List<ActivityDto>();
                foreach (var a in pagedActivities)
                {
                    result.Add(MapActivity(a));
                }

                return (result, count);
            });

            if (totalCount == -1)
                return StatusCode(500, ApiResponse<object>.Error("Dzialania manager not available"));

            return Ok(new PagedResponse<ActivityDto>
            {
                Data = items!,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting activities");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving activities", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get activity by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<ActivityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ActivityDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ActivityDto>>> GetActivity(int id)
    {
        try
        {
            var (managerNull, dto) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("Dzialania");
                if (manager == null) return (true, (ActivityDto?)null);

                var allActivities = DynamicPropertyHelper.SafeGetAll((object)manager);
                var activity = allActivities.FirstOrDefault(a => DynamicPropertyHelper.GetId(a) == id);

                if (activity == null)
                    return (false, (ActivityDto?)null);

                return (false, MapActivity(activity));
            });

            if (managerNull)
                return StatusCode(500, ApiResponse<object>.Error("Dzialania manager not available"));

            if (dto == null)
                return NotFound(ApiResponse<ActivityDto>.Error($"Activity with ID {id} not found"));

            return Ok(ApiResponse<ActivityDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting activity {Id}", id);
            return StatusCode(500, ApiResponse<ActivityDto>.Error("Error retrieving activity", new List<string> { ex.Message }));
        }
    }

    #region Mapping

    private static ActivityDto MapActivity(dynamic a)
    {
        var kontrahent = DynamicPropertyHelper.GetProperty(a, "Kontrahent");

        return new ActivityDto
        {
            Id = DynamicPropertyHelper.GetId(a),
            Subject = DynamicPropertyHelper.GetString(a, "Temat"),
            Description = DynamicPropertyHelper.GetString(a, "Opis"),
            Type = DynamicPropertyHelper.GetString(a, "Typ"),
            Status = DynamicPropertyHelper.GetString(a, "Status"),
            Priority = DynamicPropertyHelper.GetString(a, "Priorytet"),
            AssignedTo = DynamicPropertyHelper.GetString(a, "PrzypisanyDo"),
            CustomerId = kontrahent != null ? DynamicPropertyHelper.GetNullableInt(kontrahent, "Id") : null,
            CustomerName = kontrahent != null ? DynamicPropertyHelper.GetString(kontrahent, "NazwaSkrocona") : null,
            StartDate = DynamicPropertyHelper.GetDateTime(a, "DataRozpoczecia"),
            EndDate = DynamicPropertyHelper.GetDateTime(a, "DataZakonczenia"),
            CreatedDate = DynamicPropertyHelper.GetDateTime(a, "DataUtworzenia"),
            CompletedDate = DynamicPropertyHelper.GetDateTime(a, "DataZakonczenia2")
        };
    }

    #endregion
}

#region DTOs

/// <summary>
/// Activity (Dzialanie) DTO
/// </summary>
public class ActivityDto
{
    public int Id { get; set; }
    public string? Subject { get; set; }
    public string? Description { get; set; }
    public string? Type { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public string? AssignedTo { get; set; }
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
}

#endregion
