using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// System Parameters (Parametry) management endpoints
/// </summary>
[ApiController]
[Route("api/system-parameters")]
[Authorize]
[Tags("System Parameters")]
public class SystemParametersController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<SystemParametersController> _logger;

    public SystemParametersController(ISferaService sferaService, ILogger<SystemParametersController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get all system parameters
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<SystemParameterDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SystemParameterDto>>>> GetSystemParameters()
    {
        try
        {
            var items = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("Parametry");
                if (manager == null) return (List<SystemParameterDto>?)null;

                var allItems = DynamicPropertyHelper.SafeGetAll((object)manager);

                var result = new List<SystemParameterDto>();
                foreach (var e in allItems)
                {
                    result.Add(MapSystemParameter(e));
                }

                return result;
            });

            if (items == null)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Parametry manager"));

            return Ok(ApiResponse<List<SystemParameterDto>>.Ok(items));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system parameters");
            return StatusCode(500, ApiResponse<List<SystemParameterDto>>.Error("Error retrieving system parameters", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get system parameter by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<SystemParameterDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SystemParameterDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SystemParameterDto>>> GetSystemParameter(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("Parametry");
                if (manager == null) return (managerNull: true, dto: (SystemParameterDto?)null);

                var allItems = DynamicPropertyHelper.SafeGetAll((object)manager);
                var entry = allItems.FirstOrDefault(e => DynamicPropertyHelper.GetId(e) == id);

                if (entry == null)
                    return (managerNull: false, dto: (SystemParameterDto?)null);

                return (managerNull: false, dto: (SystemParameterDto?)MapSystemParameter(entry));
            });

            if (result.managerNull)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Parametry manager"));

            if (result.dto == null)
                return NotFound(ApiResponse<SystemParameterDto>.Error($"System parameter with ID {id} not found"));

            return Ok(ApiResponse<SystemParameterDto>.Ok(result.dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system parameter {Id}", id);
            return StatusCode(500, ApiResponse<SystemParameterDto>.Error("Error retrieving system parameter", new List<string> { ex.Message }));
        }
    }

    #region Helpers

    private static SystemParameterDto MapSystemParameter(dynamic e)
    {
        return new SystemParameterDto
        {
            Id = DynamicPropertyHelper.GetId(e),
            Name = DynamicPropertyHelper.GetString(e, "Nazwa"),
            Value = DynamicPropertyHelper.GetString(e, "Wartosc"),
            Category = DynamicPropertyHelper.GetString(e, "Kategoria"),
            Description = DynamicPropertyHelper.GetString(e, "Opis"),
            IsReadOnly = DynamicPropertyHelper.GetNullableBool(e, "TylkoDoOdczytu") ?? false
        };
    }

    #endregion
}

#region DTOs

/// <summary>
/// System parameter DTO
/// </summary>
public class SystemParameterDto
{
    public int Id { get; set; }
    /// <summary>
    /// Parameter name (Nazwa)
    /// </summary>
    public string? Name { get; set; }
    /// <summary>
    /// Parameter value (Wartosc)
    /// </summary>
    public string? Value { get; set; }
    /// <summary>
    /// Parameter category (Kategoria)
    /// </summary>
    public string? Category { get; set; }
    /// <summary>
    /// Description (Opis)
    /// </summary>
    public string? Description { get; set; }
    /// <summary>
    /// Whether this parameter is read-only (TylkoDoOdczytu)
    /// </summary>
    public bool IsReadOnly { get; set; }
}

#endregion
