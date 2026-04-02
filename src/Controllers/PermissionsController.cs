using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Permissions (Uprawnienia) management endpoints
/// </summary>
[ApiController]
[Route("api/permissions")]
[Authorize]
[Tags("Permissions")]
public class PermissionsController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<PermissionsController> _logger;

    public PermissionsController(ISferaService sferaService, ILogger<PermissionsController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get all permission sets/roles
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<PermissionSetDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<PermissionSetDto>>>> GetPermissionSets()
    {
        try
        {
            var items = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("Uprawnienia");
                if (manager == null) return (List<PermissionSetDto>?)null;

                var allItems = DynamicPropertyHelper.SafeGetAll((object)manager);

                var result = new List<PermissionSetDto>();
                foreach (var e in allItems)
                {
                    result.Add(MapPermissionSet(e));
                }

                return result;
            });

            if (items == null)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Uprawnienia manager"));

            return Ok(ApiResponse<List<PermissionSetDto>>.Ok(items));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting permission sets");
            return StatusCode(500, ApiResponse<List<PermissionSetDto>>.Error("Error retrieving permission sets", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get permission set by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<PermissionSetDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PermissionSetDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PermissionSetDto>>> GetPermissionSet(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("Uprawnienia");
                if (manager == null) return (managerNull: true, dto: (PermissionSetDto?)null);

                var allItems = DynamicPropertyHelper.SafeGetAll((object)manager);
                var entry = allItems.FirstOrDefault(e => DynamicPropertyHelper.GetId(e) == id);

                if (entry == null)
                    return (managerNull: false, dto: (PermissionSetDto?)null);

                return (managerNull: false, dto: (PermissionSetDto?)MapPermissionSet(entry));
            });

            if (result.managerNull)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Uprawnienia manager"));

            if (result.dto == null)
                return NotFound(ApiResponse<PermissionSetDto>.Error($"Permission set with ID {id} not found"));

            return Ok(ApiResponse<PermissionSetDto>.Ok(result.dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting permission set {Id}", id);
            return StatusCode(500, ApiResponse<PermissionSetDto>.Error("Error retrieving permission set", new List<string> { ex.Message }));
        }
    }

    #region Helpers

    private static PermissionSetDto MapPermissionSet(dynamic e)
    {
        return new PermissionSetDto
        {
            Id = DynamicPropertyHelper.GetId(e),
            Name = DynamicPropertyHelper.GetString(e, "Nazwa"),
            Description = DynamicPropertyHelper.GetString(e, "Opis"),
            IsDefault = DynamicPropertyHelper.GetNullableBool(e, "Domyslny") ?? false,
            UserCount = DynamicPropertyHelper.GetNullableInt(e, "LiczbaUzytkownikow") ?? 0
        };
    }

    #endregion
}

#region DTOs

/// <summary>
/// Permission set/role DTO
/// </summary>
public class PermissionSetDto
{
    public int Id { get; set; }
    /// <summary>
    /// Permission set name (Nazwa)
    /// </summary>
    public string? Name { get; set; }
    /// <summary>
    /// Description (Opis)
    /// </summary>
    public string? Description { get; set; }
    /// <summary>
    /// Whether this is a default permission set (Domyslny)
    /// </summary>
    public bool IsDefault { get; set; }
    /// <summary>
    /// Number of users assigned to this permission set (LiczbaUzytkownikow)
    /// </summary>
    public int UserCount { get; set; }
}

#endregion
