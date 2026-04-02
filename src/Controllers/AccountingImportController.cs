using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Accounting Import (Import Ksiegowy) management endpoints
/// </summary>
[ApiController]
[Route("api/accounting-import")]
[Authorize]
[Tags("Accounting Import")]
public class AccountingImportController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<AccountingImportController> _logger;

    public AccountingImportController(ISferaService sferaService, ILogger<AccountingImportController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get import schemas/configurations list
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<AccountingImportSchemaDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<AccountingImportSchemaDto>>> GetImportSchemas(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var (items, totalCount) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("ImportKsiegowy");
                if (manager == null) return (null, -1);

                var allSchemas = DynamicPropertyHelper.SafeGetAll((object)manager);
                var count = allSchemas.Count;

                var pagedSchemas = allSchemas
                    .OrderByDescending(s => DynamicPropertyHelper.GetDateTime(s, "DataUtworzenia") ?? DateTime.MinValue)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var result = new List<AccountingImportSchemaDto>();
                foreach (var s in pagedSchemas)
                {
                    result.Add(MapImportSchema(s));
                }

                return (result, count);
            });

            if (totalCount == -1)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get ImportKsiegowy manager"));

            return Ok(new PagedResponse<AccountingImportSchemaDto>
            {
                Data = items!,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting accounting import schemas");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving accounting import schemas", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get import schema by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<AccountingImportSchemaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AccountingImportSchemaDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AccountingImportSchemaDto>>> GetImportSchema(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("ImportKsiegowy");
                if (manager == null) return (AccountingImportSchemaDto?)null;

                var allSchemas = DynamicPropertyHelper.SafeGetAll((object)manager);
                var schema = allSchemas.FirstOrDefault(s => DynamicPropertyHelper.GetId(s) == id);

                if (schema == null) return (AccountingImportSchemaDto?)null;
                return (AccountingImportSchemaDto?)MapImportSchema(schema);
            });

            if (result == null)
                return NotFound(ApiResponse<AccountingImportSchemaDto>.Error($"Accounting import schema with ID {id} not found"));

            return Ok(ApiResponse<AccountingImportSchemaDto>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting accounting import schema {Id}", id);
            return StatusCode(500, ApiResponse<AccountingImportSchemaDto>.Error("Error retrieving accounting import schema", new List<string> { ex.Message }));
        }
    }

    #region Helpers

    private static AccountingImportSchemaDto MapImportSchema(dynamic s)
    {
        return new AccountingImportSchemaDto
        {
            Id = DynamicPropertyHelper.GetId(s),
            Name = DynamicPropertyHelper.GetString(s, "Nazwa"),
            Description = DynamicPropertyHelper.GetString(s, "Opis"),
            SchemaType = DynamicPropertyHelper.GetString(s, "TypSchematu"),
            IsActive = DynamicPropertyHelper.GetBool(s, "Aktywny"),
            CreatedDate = DynamicPropertyHelper.GetDateTime(s, "DataUtworzenia")
        };
    }

    #endregion
}

#region DTOs

/// <summary>
/// Accounting import schema DTO
/// </summary>
public class AccountingImportSchemaDto
{
    public int Id { get; set; }
    /// <summary>
    /// Schema name (Nazwa)
    /// </summary>
    public string? Name { get; set; }
    /// <summary>
    /// Description (Opis)
    /// </summary>
    public string? Description { get; set; }
    /// <summary>
    /// Schema type (TypSchematu)
    /// </summary>
    public string? SchemaType { get; set; }
    /// <summary>
    /// Whether the schema is active (Aktywny)
    /// </summary>
    public bool IsActive { get; set; }
    /// <summary>
    /// Created date (DataUtworzenia)
    /// </summary>
    public DateTime? CreatedDate { get; set; }
}

#endregion
