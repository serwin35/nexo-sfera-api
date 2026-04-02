using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Remainders / Inventory counts (Remanenty) management endpoints
/// </summary>
[ApiController]
[Route("api/remainders")]
[Authorize]
[Tags("Remainders")]
public class RemaindersController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<RemaindersController> _logger;

    public RemaindersController(ISferaService sferaService, ILogger<RemaindersController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get remainders (paged, with optional filters)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<RemainderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<RemainderDto>>> GetRemainders(
        [FromQuery] int? year = null,
        [FromQuery] string? type = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var (items, totalCount) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("Remanenty");
                if (manager == null) return (null, -1);

                var allItems = DynamicPropertyHelper.SafeGetAll((object)manager);

                // Filter by year
                if (year.HasValue)
                {
                    allItems = allItems.Where(e =>
                    {
                        var rok = DynamicPropertyHelper.GetNullableInt(e, "Rok");
                        return rok.HasValue && rok.Value == year.Value;
                    }).ToList();
                }

                // Filter by type
                if (!string.IsNullOrEmpty(type))
                {
                    allItems = allItems.Where(e =>
                    {
                        var typ = DynamicPropertyHelper.GetString(e, "Typ");
                        return typ != null && typ.Equals(type, StringComparison.OrdinalIgnoreCase);
                    }).ToList();
                }

                var count = allItems.Count;
                var pagedItems = allItems
                    .OrderByDescending(e => DynamicPropertyHelper.GetDateTime(e, "Data") ?? DateTime.MinValue)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var result = new List<RemainderDto>();
                foreach (var e in pagedItems)
                {
                    result.Add(MapRemainder(e));
                }

                return (result, count);
            });

            if (totalCount == -1)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Remanenty manager"));

            return Ok(new PagedResponse<RemainderDto>
            {
                Data = items!,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting remainders");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving remainders", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get remainder by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<RemainderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<RemainderDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<RemainderDto>>> GetRemainder(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("Remanenty");
                if (manager == null) return (managerNull: true, dto: (RemainderDto?)null);

                var allItems = DynamicPropertyHelper.SafeGetAll((object)manager);
                var entry = allItems.FirstOrDefault(e => DynamicPropertyHelper.GetId(e) == id);

                if (entry == null)
                    return (managerNull: false, dto: (RemainderDto?)null);

                return (managerNull: false, dto: (RemainderDto?)MapRemainder(entry));
            });

            if (result.managerNull)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Remanenty manager"));

            if (result.dto == null)
                return NotFound(ApiResponse<RemainderDto>.Error($"Remainder with ID {id} not found"));

            return Ok(ApiResponse<RemainderDto>.Ok(result.dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting remainder {Id}", id);
            return StatusCode(500, ApiResponse<RemainderDto>.Error("Error retrieving remainder", new List<string> { ex.Message }));
        }
    }

    #region Helpers

    private static RemainderDto MapRemainder(dynamic e)
    {
        return new RemainderDto
        {
            Id = DynamicPropertyHelper.GetId(e),
            Name = DynamicPropertyHelper.GetString(e, "Nazwa"),
            Type = DynamicPropertyHelper.GetString(e, "Typ"),
            Year = DynamicPropertyHelper.GetNullableInt(e, "Rok") ?? 0,
            Date = DynamicPropertyHelper.GetDateTime(e, "Data"),
            TotalValue = DynamicPropertyHelper.GetDecimal(e, "WartoscCalkowita"),
            Status = DynamicPropertyHelper.GetString(e, "Status"),
            Description = DynamicPropertyHelper.GetString(e, "Opis")
        };
    }

    #endregion
}

#region DTOs

/// <summary>
/// Remainder / Inventory count DTO
/// </summary>
public class RemainderDto
{
    public int Id { get; set; }
    /// <summary>
    /// Remainder name (Nazwa)
    /// </summary>
    public string? Name { get; set; }
    /// <summary>
    /// Remainder type (Typ)
    /// </summary>
    public string? Type { get; set; }
    /// <summary>
    /// Year (Rok)
    /// </summary>
    public int Year { get; set; }
    /// <summary>
    /// Date (Data)
    /// </summary>
    public DateTime? Date { get; set; }
    /// <summary>
    /// Total value (WartoscCalkowita)
    /// </summary>
    public decimal TotalValue { get; set; }
    /// <summary>
    /// Status (Status)
    /// </summary>
    public string? Status { get; set; }
    /// <summary>
    /// Description (Opis)
    /// </summary>
    public string? Description { get; set; }
}

#endregion
