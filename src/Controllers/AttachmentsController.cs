using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Attachment library management
/// </summary>
[ApiController]
[Route("api/attachments")]
[Authorize]
[Tags("Attachments")]
public class AttachmentsController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<AttachmentsController> _logger;

    public AttachmentsController(ISferaService sferaService, ILogger<AttachmentsController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get attachments (paged, with optional filters by objectType and objectId)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<AttachmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<AttachmentDto>>> GetAttachments(
        [FromQuery] string? objectType,
        [FromQuery] int? objectId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("BibliotekaZalacznikow");
                if (manager == null)
                    return (null, 0);

                var allItems = DynamicPropertyHelper.SafeGetAll((object)manager);

                // Filter by object type
                if (!string.IsNullOrEmpty(objectType))
                {
                    allItems = allItems.Where(a =>
                    {
                        var type = DynamicPropertyHelper.GetString(a, "TypObiektu");
                        return type != null && type.Equals(objectType, StringComparison.OrdinalIgnoreCase);
                    }).ToList();
                }

                // Filter by object ID
                if (objectId.HasValue)
                {
                    allItems = allItems.Where(a =>
                    {
                        var id = DynamicPropertyHelper.GetNullableInt(a, "ObiektId");
                        return id.HasValue && id.Value == objectId.Value;
                    }).ToList();
                }

                var totalCount = allItems.Count;
                var pagedItems = allItems
                    .OrderByDescending(a => DynamicPropertyHelper.GetDateTime(a, "DataUtworzenia") ?? DateTime.MinValue)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var items = new List<AttachmentDto>();
                foreach (var a in pagedItems)
                {
                    items.Add(new AttachmentDto
                    {
                        Id = DynamicPropertyHelper.GetId(a),
                        FileName = DynamicPropertyHelper.GetString(a, "NazwaPliku") ?? "",
                        FileSize = DynamicPropertyHelper.GetNullableInt(a, "RozmiarPliku") ?? 0,
                        MimeType = DynamicPropertyHelper.GetString(a, "TypMime"),
                        ObjectType = DynamicPropertyHelper.GetString(a, "TypObiektu"),
                        ObjectId = DynamicPropertyHelper.GetNullableInt(a, "ObiektId") ?? 0,
                        Description = DynamicPropertyHelper.GetString(a, "Opis"),
                        CreatedDate = DynamicPropertyHelper.GetDateTime(a, "DataUtworzenia"),
                        CreatedBy = DynamicPropertyHelper.GetString(a, "UtworzonyPrzez")
                    });
                }

                return (items, totalCount);
            });

            if (result.Item1 == null)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get BibliotekaZalacznikow manager"));

            return Ok(new PagedResponse<AttachmentDto>
            {
                Data = result.Item1,
                Page = page,
                PageSize = pageSize,
                TotalCount = result.Item2
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting attachments");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving attachments", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get attachment metadata by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<AttachmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AttachmentDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AttachmentDto>>> GetAttachment(int id)
    {
        try
        {
            var (managerNull, dto) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("BibliotekaZalacznikow");
                if (manager == null)
                    return (true, (AttachmentDto?)null);

                var allItems = DynamicPropertyHelper.SafeGetAll((object)manager);
                var item = allItems.FirstOrDefault(a => DynamicPropertyHelper.GetId(a) == id);

                if (item == null)
                    return (false, (AttachmentDto?)null);

                return (false, new AttachmentDto
                {
                    Id = DynamicPropertyHelper.GetId(item),
                    FileName = DynamicPropertyHelper.GetString(item, "NazwaPliku") ?? "",
                    FileSize = DynamicPropertyHelper.GetNullableInt(item, "RozmiarPliku") ?? 0,
                    MimeType = DynamicPropertyHelper.GetString(item, "TypMime"),
                    ObjectType = DynamicPropertyHelper.GetString(item, "TypObiektu"),
                    ObjectId = DynamicPropertyHelper.GetNullableInt(item, "ObiektId") ?? 0,
                    Description = DynamicPropertyHelper.GetString(item, "Opis"),
                    CreatedDate = DynamicPropertyHelper.GetDateTime(item, "DataUtworzenia"),
                    CreatedBy = DynamicPropertyHelper.GetString(item, "UtworzonyPrzez")
                });
            });

            if (managerNull)
                return StatusCode(500, ApiResponse<AttachmentDto>.Error("Failed to get BibliotekaZalacznikow manager"));

            if (dto == null)
                return NotFound(ApiResponse<AttachmentDto>.Error($"Attachment with ID {id} not found"));

            return Ok(ApiResponse<AttachmentDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting attachment {Id}", id);
            return StatusCode(500, ApiResponse<AttachmentDto>.Error("Error retrieving attachment", new List<string> { ex.Message }));
        }
    }
}

#region DTOs

/// <summary>
/// Attachment metadata DTO
/// </summary>
public class AttachmentDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public int FileSize { get; set; }
    public string? MimeType { get; set; }
    public string? ObjectType { get; set; }
    public int ObjectId { get; set; }
    public string? Description { get; set; }
    public DateTime? CreatedDate { get; set; }
    public string? CreatedBy { get; set; }
}

#endregion
