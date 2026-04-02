using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Comments and notes management
/// </summary>
[ApiController]
[Route("api/comments")]
[Authorize]
[Tags("Comments")]
public class CommentsController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<CommentsController> _logger;

    public CommentsController(ISferaService sferaService, ILogger<CommentsController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get comments (paged, with optional filters by objectType and objectId)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<CommentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<CommentDto>>> GetComments(
        [FromQuery] string? objectType,
        [FromQuery] int? objectId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("Komentarze");
                if (manager == null)
                    return (null, 0);

                var allItems = DynamicPropertyHelper.SafeGetAll((object)manager);

                // Filter by object type
                if (!string.IsNullOrEmpty(objectType))
                {
                    allItems = allItems.Where(c =>
                    {
                        var type = DynamicPropertyHelper.GetString(c, "TypObiektu");
                        return type != null && type.Equals(objectType, StringComparison.OrdinalIgnoreCase);
                    }).ToList();
                }

                // Filter by object ID
                if (objectId.HasValue)
                {
                    allItems = allItems.Where(c =>
                    {
                        var id = DynamicPropertyHelper.GetNullableInt(c, "ObiektId");
                        return id.HasValue && id.Value == objectId.Value;
                    }).ToList();
                }

                var totalCount = allItems.Count;
                var pagedItems = allItems
                    .OrderByDescending(c => DynamicPropertyHelper.GetDateTime(c, "DataUtworzenia") ?? DateTime.MinValue)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var items = new List<CommentDto>();
                foreach (var c in pagedItems)
                {
                    items.Add(new CommentDto
                    {
                        Id = DynamicPropertyHelper.GetId(c),
                        ObjectType = DynamicPropertyHelper.GetString(c, "TypObiektu"),
                        ObjectId = DynamicPropertyHelper.GetNullableInt(c, "ObiektId") ?? 0,
                        Content = DynamicPropertyHelper.GetString(c, "Tresc") ?? "",
                        Author = DynamicPropertyHelper.GetString(c, "Autor"),
                        CreatedDate = DynamicPropertyHelper.GetDateTime(c, "DataUtworzenia"),
                        ModifiedDate = DynamicPropertyHelper.GetDateTime(c, "DataModyfikacji")
                    });
                }

                return (items, totalCount);
            });

            if (result.Item1 == null)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Komentarze manager"));

            return Ok(new PagedResponse<CommentDto>
            {
                Data = result.Item1,
                Page = page,
                PageSize = pageSize,
                TotalCount = result.Item2
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting comments");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving comments", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get comment by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<CommentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CommentDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CommentDto>>> GetComment(int id)
    {
        try
        {
            var (managerNull, dto) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("Komentarze");
                if (manager == null)
                    return (true, (CommentDto?)null);

                var allItems = DynamicPropertyHelper.SafeGetAll((object)manager);
                var item = allItems.FirstOrDefault(c => DynamicPropertyHelper.GetId(c) == id);

                if (item == null)
                    return (false, (CommentDto?)null);

                return (false, new CommentDto
                {
                    Id = DynamicPropertyHelper.GetId(item),
                    ObjectType = DynamicPropertyHelper.GetString(item, "TypObiektu"),
                    ObjectId = DynamicPropertyHelper.GetNullableInt(item, "ObiektId") ?? 0,
                    Content = DynamicPropertyHelper.GetString(item, "Tresc") ?? "",
                    Author = DynamicPropertyHelper.GetString(item, "Autor"),
                    CreatedDate = DynamicPropertyHelper.GetDateTime(item, "DataUtworzenia"),
                    ModifiedDate = DynamicPropertyHelper.GetDateTime(item, "DataModyfikacji")
                });
            });

            if (managerNull)
                return StatusCode(500, ApiResponse<CommentDto>.Error("Failed to get Komentarze manager"));

            if (dto == null)
                return NotFound(ApiResponse<CommentDto>.Error($"Comment with ID {id} not found"));

            return Ok(ApiResponse<CommentDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting comment {Id}", id);
            return StatusCode(500, ApiResponse<CommentDto>.Error("Error retrieving comment", new List<string> { ex.Message }));
        }
    }
}

#region DTOs

/// <summary>
/// Comment DTO
/// </summary>
public class CommentDto
{
    public int Id { get; set; }
    public string? ObjectType { get; set; }
    public int ObjectId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? Author { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}

#endregion
