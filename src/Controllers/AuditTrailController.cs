using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Audit trail and change tracking
/// </summary>
[ApiController]
[Route("api/audit-trail")]
[Authorize]
[Tags("Audit Trail")]
public class AuditTrailController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<AuditTrailController> _logger;

    public AuditTrailController(ISferaService sferaService, ILogger<AuditTrailController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get audit trail entries (paged, with optional filters)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<AuditTrailEntryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<AuditTrailEntryDto>>> GetAuditTrailEntries(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] string? objectType,
        [FromQuery] int? userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("SladRewizyjny");
                if (manager == null)
                    return (null, 0);

                var allItems = DynamicPropertyHelper.SafeGetAll((object)manager);

                // Filter by date range
                if (dateFrom.HasValue)
                {
                    allItems = allItems.Where(e =>
                    {
                        var date = DynamicPropertyHelper.GetDateTime(e, "Data");
                        return date.HasValue && date.Value >= dateFrom.Value;
                    }).ToList();
                }

                if (dateTo.HasValue)
                {
                    allItems = allItems.Where(e =>
                    {
                        var date = DynamicPropertyHelper.GetDateTime(e, "Data");
                        return date.HasValue && date.Value <= dateTo.Value;
                    }).ToList();
                }

                // Filter by object type
                if (!string.IsNullOrEmpty(objectType))
                {
                    allItems = allItems.Where(e =>
                    {
                        var type = DynamicPropertyHelper.GetString(e, "TypObiektu");
                        return type != null && type.Equals(objectType, StringComparison.OrdinalIgnoreCase);
                    }).ToList();
                }

                // Filter by user ID
                if (userId.HasValue)
                {
                    allItems = allItems.Where(e =>
                    {
                        var uId = DynamicPropertyHelper.GetNullableInt(e, "UzytkownikId");
                        return uId.HasValue && uId.Value == userId.Value;
                    }).ToList();
                }

                var totalCount = allItems.Count;
                var pagedItems = allItems
                    .OrderByDescending(e => DynamicPropertyHelper.GetDateTime(e, "Data") ?? DateTime.MinValue)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var items = new List<AuditTrailEntryDto>();
                foreach (var e in pagedItems)
                {
                    items.Add(new AuditTrailEntryDto
                    {
                        Id = DynamicPropertyHelper.GetId(e),
                        ObjectType = DynamicPropertyHelper.GetString(e, "TypObiektu") ?? "",
                        ObjectId = DynamicPropertyHelper.GetNullableInt(e, "ObiektId") ?? 0,
                        Action = DynamicPropertyHelper.GetString(e, "Akcja") ?? "",
                        UserName = DynamicPropertyHelper.GetString(e, "NazwaUzytkownika"),
                        Date = DynamicPropertyHelper.GetDateTime(e, "Data"),
                        OldValue = DynamicPropertyHelper.GetString(e, "StaraWartosc"),
                        NewValue = DynamicPropertyHelper.GetString(e, "NowaWartosc"),
                        Description = DynamicPropertyHelper.GetString(e, "Opis"),
                        FieldName = DynamicPropertyHelper.GetString(e, "NazwaPola")
                    });
                }

                return (items, totalCount);
            });

            if (result.Item1 == null)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get SladRewizyjny manager"));

            return Ok(new PagedResponse<AuditTrailEntryDto>
            {
                Data = result.Item1,
                Page = page,
                PageSize = pageSize,
                TotalCount = result.Item2
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting audit trail entries");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving audit trail entries", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get audit trail entries for a specific object
    /// </summary>
    [HttpGet("for-object/{objectType}/{objectId}")]
    [ProducesResponseType(typeof(PagedResponse<AuditTrailEntryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<AuditTrailEntryDto>>> GetAuditTrailForObject(
        string objectType,
        int objectId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("SladRewizyjny");
                if (manager == null)
                    return (null, 0);

                var allItems = DynamicPropertyHelper.SafeGetAll((object)manager);

                // Filter by object type and ID
                allItems = allItems.Where(e =>
                {
                    var type = DynamicPropertyHelper.GetString(e, "TypObiektu");
                    var id = DynamicPropertyHelper.GetNullableInt(e, "ObiektId");
                    return type != null && type.Equals(objectType, StringComparison.OrdinalIgnoreCase)
                        && id.HasValue && id.Value == objectId;
                }).ToList();

                var totalCount = allItems.Count;
                var pagedItems = allItems
                    .OrderByDescending(e => DynamicPropertyHelper.GetDateTime(e, "Data") ?? DateTime.MinValue)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var items = new List<AuditTrailEntryDto>();
                foreach (var e in pagedItems)
                {
                    items.Add(new AuditTrailEntryDto
                    {
                        Id = DynamicPropertyHelper.GetId(e),
                        ObjectType = DynamicPropertyHelper.GetString(e, "TypObiektu") ?? "",
                        ObjectId = DynamicPropertyHelper.GetNullableInt(e, "ObiektId") ?? 0,
                        Action = DynamicPropertyHelper.GetString(e, "Akcja") ?? "",
                        UserName = DynamicPropertyHelper.GetString(e, "NazwaUzytkownika"),
                        Date = DynamicPropertyHelper.GetDateTime(e, "Data"),
                        OldValue = DynamicPropertyHelper.GetString(e, "StaraWartosc"),
                        NewValue = DynamicPropertyHelper.GetString(e, "NowaWartosc"),
                        Description = DynamicPropertyHelper.GetString(e, "Opis"),
                        FieldName = DynamicPropertyHelper.GetString(e, "NazwaPola")
                    });
                }

                return (items, totalCount);
            });

            if (result.Item1 == null)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get SladRewizyjny manager"));

            return Ok(new PagedResponse<AuditTrailEntryDto>
            {
                Data = result.Item1,
                Page = page,
                PageSize = pageSize,
                TotalCount = result.Item2
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting audit trail for object {ObjectType}/{ObjectId}", objectType, objectId);
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving audit trail for object", new List<string> { ex.Message }));
        }
    }
}

#region DTOs

/// <summary>
/// Audit trail entry DTO
/// </summary>
public class AuditTrailEntryDto
{
    public int Id { get; set; }
    public string ObjectType { get; set; } = string.Empty;
    public int ObjectId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public DateTime? Date { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Description { get; set; }
    public string? FieldName { get; set; }
}

#endregion
