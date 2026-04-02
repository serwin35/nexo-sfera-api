using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Photo Gallery (Galeria Zdjec) management endpoints
/// </summary>
[ApiController]
[Route("api/photo-gallery")]
[Authorize]
[Tags("Photo Gallery")]
public class PhotoGalleryController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<PhotoGalleryController> _logger;

    public PhotoGalleryController(ISferaService sferaService, ILogger<PhotoGalleryController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get photos (paged, with optional filters)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<PhotoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<PhotoDto>>> GetPhotos(
        [FromQuery] string? objectType = null,
        [FromQuery] int? objectId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var (items, totalCount) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("GaleriaZdjec");
                if (manager == null) return (null, -1);

                var allPhotos = DynamicPropertyHelper.SafeGetAll((object)manager);

                if (!string.IsNullOrEmpty(objectType))
                {
                    var typeLower = objectType.ToLower();
                    allPhotos = allPhotos.Where(p =>
                    {
                        var typ = DynamicPropertyHelper.GetString(p, "TypObiektu") ?? "";
                        return typ.ToLower().Contains(typeLower);
                    }).ToList();
                }

                if (objectId.HasValue)
                {
                    allPhotos = allPhotos.Where(p =>
                        DynamicPropertyHelper.GetNullableInt(p, "ObiektId") == objectId.Value).ToList();
                }

                var count = allPhotos.Count;
                var pagedPhotos = allPhotos
                    .OrderByDescending(p => DynamicPropertyHelper.GetDateTime(p, "DataUtworzenia") ?? DateTime.MinValue)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var result = new List<PhotoDto>();
                foreach (var p in pagedPhotos)
                {
                    result.Add(MapPhoto(p));
                }

                return (result, count);
            });

            if (totalCount == -1)
                return StatusCode(500, ApiResponse<object>.Error("GaleriaZdjec manager not available"));

            return Ok(new PagedResponse<PhotoDto>
            {
                Data = items!,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting photos");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving photos", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get photo metadata by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<PhotoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PhotoDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PhotoDto>>> GetPhoto(int id)
    {
        try
        {
            var (managerNull, dto) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("GaleriaZdjec");
                if (manager == null) return (true, (PhotoDto?)null);

                var allPhotos = DynamicPropertyHelper.SafeGetAll((object)manager);
                var photo = allPhotos.FirstOrDefault(p => DynamicPropertyHelper.GetId(p) == id);

                if (photo == null)
                    return (false, (PhotoDto?)null);

                return (false, MapPhoto(photo));
            });

            if (managerNull)
                return StatusCode(500, ApiResponse<object>.Error("GaleriaZdjec manager not available"));

            if (dto == null)
                return NotFound(ApiResponse<PhotoDto>.Error($"Photo with ID {id} not found"));

            return Ok(ApiResponse<PhotoDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting photo {Id}", id);
            return StatusCode(500, ApiResponse<PhotoDto>.Error("Error retrieving photo", new List<string> { ex.Message }));
        }
    }

    #region Mapping

    private static PhotoDto MapPhoto(dynamic p)
    {
        return new PhotoDto
        {
            Id = DynamicPropertyHelper.GetId(p),
            FileName = DynamicPropertyHelper.GetString(p, "NazwaPliku"),
            ObjectType = DynamicPropertyHelper.GetString(p, "TypObiektu"),
            ObjectId = DynamicPropertyHelper.GetNullableInt(p, "ObiektId"),
            Description = DynamicPropertyHelper.GetString(p, "Opis"),
            Width = DynamicPropertyHelper.GetNullableInt(p, "Szerokosc"),
            Height = DynamicPropertyHelper.GetNullableInt(p, "Wysokosc"),
            FileSize = DynamicPropertyHelper.GetNullableInt(p, "RozmiarPliku"),
            CreatedDate = DynamicPropertyHelper.GetDateTime(p, "DataUtworzenia"),
            IsMain = DynamicPropertyHelper.GetBool(p, "Glowne")
        };
    }

    #endregion
}

#region DTOs

/// <summary>
/// Photo metadata DTO
/// </summary>
public class PhotoDto
{
    public int Id { get; set; }
    public string? FileName { get; set; }
    public string? ObjectType { get; set; }
    public int? ObjectId { get; set; }
    public string? Description { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int? FileSize { get; set; }
    public DateTime? CreatedDate { get; set; }
    public bool IsMain { get; set; }
}

#endregion
