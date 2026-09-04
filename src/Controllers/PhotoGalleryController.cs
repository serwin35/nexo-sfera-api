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

                var allPhotos = GetAllPhotos((object)manager);

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

                var allPhotos = GetAllPhotos((object)manager);
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

    /// <summary>
    /// Mark a photo as AI-generated (or not). SDK 61.1.0: IGaleriaZdjec.UstawWygenerowanePrzezAI
    /// </summary>
    [HttpPut("{id}/ai-generated")]
    [ProducesResponseType(typeof(ApiResponse<PhotoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PhotoDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<PhotoDto>), StatusCodes.Status501NotImplemented)]
    public async Task<ActionResult<ApiResponse<PhotoDto>>> SetPhotoAiGenerated(int id, [FromBody] SetPhotoAiGeneratedRequest request)
    {
        if (request == null)
            return BadRequest(ApiResponse<PhotoDto>.Error("Request body is required"));

        try
        {
            var (managerNull, unsupported, dto) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("GaleriaZdjec");
                if (manager == null) return (true, false, (PhotoDto?)null);

                object managerObj = (object)manager;
                var photo = GetAllPhotos(managerObj).FirstOrDefault(p => DynamicPropertyHelper.GetId(p) == id);
                if (photo == null)
                    return (false, false, (PhotoDto?)null);

                var setter = FindMethod(managerObj.GetType(), "UstawWygenerowanePrzezAI", 2);
                if (setter == null)
                    return (false, true, (PhotoDto?)null);

                setter.Invoke(managerObj, new object[] { photo, request.IsAiGenerated });

                return (false, false, (PhotoDto?)MapPhoto(photo));
            });

            if (managerNull)
                return StatusCode(500, ApiResponse<PhotoDto>.Error("GaleriaZdjec manager not available"));

            if (unsupported)
                return StatusCode(501, ApiResponse<PhotoDto>.Error("IGaleriaZdjec.UstawWygenerowanePrzezAI requires Sfera SDK 61.1.0 or newer"));

            if (dto == null)
                return NotFound(ApiResponse<PhotoDto>.Error($"Photo with ID {id} not found"));

            _logger.LogInformation("Photo {Id} marked IsAiGenerated={IsAiGenerated}", id, request.IsAiGenerated);
            return Ok(ApiResponse<PhotoDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting AI-generated flag on photo {Id}", id);
            return StatusCode(500, ApiResponse<PhotoDto>.Error("Error updating photo", new List<string> { ex.Message }));
        }
    }

    #region Mapping

    /// <summary>
    /// IGaleriaZdjec exposes photos via WszystkieZdjecia() (it has no Dane repository);
    /// falls back to the generic Dane.Wszystkie() enumeration for other manager shapes.
    /// </summary>
    private static List<object> GetAllPhotos(object manager)
    {
        try
        {
            var method = FindMethod(manager.GetType(), "WszystkieZdjecia", 0);
            if (method != null && method.Invoke(manager, null) is System.Collections.IEnumerable photos)
            {
                var result = new List<object>();
                foreach (var photo in photos)
                {
                    if (photo != null) result.Add(photo);
                }
                return result;
            }
        }
        catch (Exception)
        {
            // Fall through to the generic enumeration
        }

        return DynamicPropertyHelper.SafeGetAll(manager);
    }

    /// <summary>
    /// Finds a public method by name/arity on the runtime type or any interface it implements
    /// (SDK managers are often explicit interface implementations).
    /// </summary>
    private static System.Reflection.MethodInfo? FindMethod(Type type, string name, int parameterCount)
    {
        var candidates = new List<Type> { type };
        candidates.AddRange(type.GetInterfaces());

        foreach (var candidate in candidates)
        {
            var method = candidate.GetMethods()
                .FirstOrDefault(m => m.Name == name && m.GetParameters().Length == parameterCount);
            if (method != null)
                return method;
        }

        return null;
    }

    private static PhotoDto MapPhoto(object p)
    {
        return new PhotoDto
        {
            Id = DynamicPropertyHelper.GetId(p),
            // IZdjecie exposes Nazwa/Typ/RozmiarBajty/CzyGlowneZdjecie - keep legacy names as first choice
            FileName = DynamicPropertyHelper.GetString(p, "NazwaPliku")
                    ?? DynamicPropertyHelper.GetString(p, "Nazwa"),
            FileType = DynamicPropertyHelper.GetString(p, "Typ"),
            ObjectType = DynamicPropertyHelper.GetString(p, "TypObiektu"),
            ObjectId = DynamicPropertyHelper.GetNullableInt(p, "ObiektId"),
            Description = DynamicPropertyHelper.GetString(p, "Opis"),
            Width = DynamicPropertyHelper.GetNullableInt(p, "Szerokosc"),
            Height = DynamicPropertyHelper.GetNullableInt(p, "Wysokosc"),
            FileSize = DynamicPropertyHelper.GetNullableInt(p, "RozmiarPliku")
                    ?? DynamicPropertyHelper.GetNullableInt(p, "RozmiarBajty"),
            SortOrder = DynamicPropertyHelper.GetNullableInt(p, "NumerZdjecia"),
            CreatedDate = DynamicPropertyHelper.GetDateTime(p, "DataUtworzenia"),
            IsMain = DynamicPropertyHelper.GetNullableBool(p, "Glowne")
                  ?? DynamicPropertyHelper.GetNullableBool(p, "CzyGlowneZdjecie")
                  ?? false,
            // SDK 61.1.0: IZdjecie.WygenerowanePrzezAI (null on older SDKs)
            IsAiGenerated = DynamicPropertyHelper.GetNullableBool(p, "WygenerowanePrzezAI")
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

    /// <summary>File type / extension (IZdjecie.Typ, e.g. jpg, png)</summary>
    public string? FileType { get; set; }

    /// <summary>Position within the object's gallery (IZdjecie.NumerZdjecia)</summary>
    public int? SortOrder { get; set; }

    /// <summary>Whether the photo was generated by AI (SDK 61.1.0: IZdjecie.WygenerowanePrzezAI); null on older SDKs</summary>
    public bool? IsAiGenerated { get; set; }
}

/// <summary>
/// Request body for PUT api/photo-gallery/{id}/ai-generated
/// </summary>
public class SetPhotoAiGeneratedRequest
{
    public bool IsAiGenerated { get; set; }
}

#endregion
