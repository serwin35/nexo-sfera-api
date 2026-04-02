using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Archives (Dane Archiwalne) management endpoints
/// </summary>
[ApiController]
[Route("api/archives")]
[Authorize]
[Tags("Archives")]
public class ArchivesController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<ArchivesController> _logger;

    public ArchivesController(ISferaService sferaService, ILogger<ArchivesController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get archive entries (paged, with optional filters)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ArchiveEntryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ArchiveEntryDto>>> GetArchiveEntries(
        [FromQuery] string? type = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var (items, totalCount) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("DaneArchiwalne");
                if (manager == null) return (null, -1);

                var allItems = DynamicPropertyHelper.SafeGetAll((object)manager);

                // Filter by type (TypDanych)
                if (!string.IsNullOrEmpty(type))
                {
                    allItems = allItems.Where(e =>
                    {
                        var typDanych = DynamicPropertyHelper.GetString(e, "TypDanych");
                        return typDanych != null && typDanych.Equals(type, StringComparison.OrdinalIgnoreCase);
                    }).ToList();
                }

                // Filter by date range
                if (dateFrom.HasValue)
                {
                    allItems = allItems.Where(e =>
                    {
                        var date = DynamicPropertyHelper.GetDateTime(e, "DataArchiwizacji")
                                ?? DynamicPropertyHelper.GetDateTime(e, "DataUtworzenia");
                        return date.HasValue && date.Value >= dateFrom.Value;
                    }).ToList();
                }

                if (dateTo.HasValue)
                {
                    allItems = allItems.Where(e =>
                    {
                        var date = DynamicPropertyHelper.GetDateTime(e, "DataArchiwizacji")
                                ?? DynamicPropertyHelper.GetDateTime(e, "DataUtworzenia");
                        return date.HasValue && date.Value <= dateTo.Value;
                    }).ToList();
                }

                var count = allItems.Count;
                var pagedItems = allItems
                    .OrderByDescending(e => DynamicPropertyHelper.GetDateTime(e, "DataArchiwizacji") ?? DateTime.MinValue)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var result = new List<ArchiveEntryDto>();
                foreach (var e in pagedItems)
                {
                    result.Add(MapArchiveEntry(e));
                }

                return (result, count);
            });

            if (totalCount == -1)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get DaneArchiwalne manager"));

            return Ok(new PagedResponse<ArchiveEntryDto>
            {
                Data = items!,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting archive entries");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving archive entries", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get archive entry by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<ArchiveEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ArchiveEntryDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ArchiveEntryDto>>> GetArchiveEntry(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("DaneArchiwalne");
                if (manager == null) return (managerNull: true, dto: (ArchiveEntryDto?)null);

                var allItems = DynamicPropertyHelper.SafeGetAll((object)manager);
                var entry = allItems.FirstOrDefault(e => DynamicPropertyHelper.GetId(e) == id);

                if (entry == null)
                    return (managerNull: false, dto: (ArchiveEntryDto?)null);

                return (managerNull: false, dto: (ArchiveEntryDto?)MapArchiveEntry(entry));
            });

            if (result.managerNull)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get DaneArchiwalne manager"));

            if (result.dto == null)
                return NotFound(ApiResponse<ArchiveEntryDto>.Error($"Archive entry with ID {id} not found"));

            return Ok(ApiResponse<ArchiveEntryDto>.Ok(result.dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting archive entry {Id}", id);
            return StatusCode(500, ApiResponse<ArchiveEntryDto>.Error("Error retrieving archive entry", new List<string> { ex.Message }));
        }
    }

    #region Helpers

    private static ArchiveEntryDto MapArchiveEntry(dynamic e)
    {
        return new ArchiveEntryDto
        {
            Id = DynamicPropertyHelper.GetId(e),
            DataType = DynamicPropertyHelper.GetString(e, "TypDanych"),
            DataTypeDescription = DynamicPropertyHelper.GetString(e, "OpisTypuDanych"),
            SourceId = DynamicPropertyHelper.GetNullableInt(e, "ZrodloId"),
            Name = DynamicPropertyHelper.GetString(e, "Nazwa"),
            CreatedDate = DynamicPropertyHelper.GetDateTime(e, "DataUtworzenia"),
            ArchivedDate = DynamicPropertyHelper.GetDateTime(e, "DataArchiwizacji"),
            Description = DynamicPropertyHelper.GetString(e, "Opis")
        };
    }

    #endregion
}

#region DTOs

/// <summary>
/// Archive entry DTO
/// </summary>
/// <remarks>
/// SDK 60.0.0 added new TypDanych.Aukcje (17) for auction data
/// </remarks>
public class ArchiveEntryDto
{
    public int Id { get; set; }
    /// <summary>
    /// Data type (TypDanych) - e.g. Aukcje (17) added in SDK 60.0.0
    /// </summary>
    public string? DataType { get; set; }
    /// <summary>
    /// Human-readable data type description (OpisTypuDanych)
    /// </summary>
    public string? DataTypeDescription { get; set; }
    /// <summary>
    /// Source entity ID (ZrodloId)
    /// </summary>
    public int? SourceId { get; set; }
    /// <summary>
    /// Entry name (Nazwa)
    /// </summary>
    public string? Name { get; set; }
    /// <summary>
    /// Created date (DataUtworzenia)
    /// </summary>
    public DateTime? CreatedDate { get; set; }
    /// <summary>
    /// Archived date (DataArchiwizacji)
    /// </summary>
    public DateTime? ArchivedDate { get; set; }
    /// <summary>
    /// Description (Opis)
    /// </summary>
    public string? Description { get; set; }
}

#endregion
