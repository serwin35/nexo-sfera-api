using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Custom Fields (Pola Wlasne) management endpoints
/// </summary>
[ApiController]
[Route("api/custom-fields")]
[Authorize]
[Tags("Custom Fields")]
public class CustomFieldsController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<CustomFieldsController> _logger;

    public CustomFieldsController(ISferaService sferaService, ILogger<CustomFieldsController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get all custom field definitions
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<CustomFieldDefinitionDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<CustomFieldDefinitionDto>>>> GetCustomFields(
        [FromQuery] string? entityType = null)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("PolaWlasne");
                if (manager == null) return (List<CustomFieldDefinitionDto>?)null;

                var allFields = DynamicPropertyHelper.SafeGetAll((object)manager);

                if (!string.IsNullOrEmpty(entityType))
                {
                    var entityTypeLower = entityType.ToLower();
                    allFields = allFields.Where(f =>
                    {
                        var typ = DynamicPropertyHelper.GetString(f, "TypEncji") ?? "";
                        return typ.ToLower().Contains(entityTypeLower);
                    }).ToList();
                }

                var items = new List<CustomFieldDefinitionDto>();
                foreach (var f in allFields.OrderBy(f => DynamicPropertyHelper.GetNullableInt(f, "Kolejnosc") ?? 0))
                {
                    items.Add(MapCustomField(f));
                }

                return items;
            });

            if (result == null)
                return StatusCode(500, ApiResponse<object>.Error("PolaWlasne manager not available"));

            return Ok(ApiResponse<List<CustomFieldDefinitionDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting custom fields");
            return StatusCode(500, ApiResponse<List<CustomFieldDefinitionDto>>.Error("Error retrieving custom fields", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get custom field definition by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<CustomFieldDefinitionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CustomFieldDefinitionDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CustomFieldDefinitionDto>>> GetCustomField(int id)
    {
        try
        {
            var (managerNull, dto) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("PolaWlasne");
                if (manager == null) return (true, (CustomFieldDefinitionDto?)null);

                var allFields = DynamicPropertyHelper.SafeGetAll((object)manager);
                var field = allFields.FirstOrDefault(f => DynamicPropertyHelper.GetId(f) == id);

                if (field == null)
                    return (false, (CustomFieldDefinitionDto?)null);

                return (false, MapCustomField(field));
            });

            if (managerNull)
                return StatusCode(500, ApiResponse<object>.Error("PolaWlasne manager not available"));

            if (dto == null)
                return NotFound(ApiResponse<CustomFieldDefinitionDto>.Error($"Custom field with ID {id} not found"));

            return Ok(ApiResponse<CustomFieldDefinitionDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting custom field {Id}", id);
            return StatusCode(500, ApiResponse<CustomFieldDefinitionDto>.Error("Error retrieving custom field", new List<string> { ex.Message }));
        }
    }

    #region Mapping

    private static CustomFieldDefinitionDto MapCustomField(dynamic f)
    {
        return new CustomFieldDefinitionDto
        {
            Id = DynamicPropertyHelper.GetId(f),
            Name = DynamicPropertyHelper.GetString(f, "Nazwa"),
            FieldType = DynamicPropertyHelper.GetString(f, "TypPola"),
            EntityType = DynamicPropertyHelper.GetString(f, "TypEncji"),
            IsRequired = DynamicPropertyHelper.GetBool(f, "Wymagane"),
            DefaultValue = DynamicPropertyHelper.GetString(f, "WartoscDomyslna"),
            Description = DynamicPropertyHelper.GetString(f, "Opis"),
            SortOrder = DynamicPropertyHelper.GetNullableInt(f, "Kolejnosc")
        };
    }

    #endregion
}

#region DTOs

/// <summary>
/// Custom field definition DTO
/// </summary>
public class CustomFieldDefinitionDto
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? FieldType { get; set; }
    public string? EntityType { get; set; }
    public bool IsRequired { get; set; }
    public string? DefaultValue { get; set; }
    public string? Description { get; set; }
    public int? SortOrder { get; set; }
}

#endregion
