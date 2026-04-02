using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Financial Statements (Sprawozdania Finansowe) management endpoints
/// </summary>
[ApiController]
[Route("api/financial-statements")]
[Authorize]
[Tags("Financial Statements")]
public class FinancialStatementsController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<FinancialStatementsController> _logger;

    public FinancialStatementsController(ISferaService sferaService, ILogger<FinancialStatementsController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get financial statements list
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<FinancialStatementDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<FinancialStatementDto>>> GetFinancialStatements(
        [FromQuery] int? year,
        [FromQuery] string? type,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var (items, totalCount) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("SprawozdaniaFinansowe");
                if (manager == null) return (null, -1);

                var allStatements = DynamicPropertyHelper.SafeGetAll((object)manager);

                // Filter by year
                if (year.HasValue)
                {
                    allStatements = allStatements.Where(s =>
                    {
                        var rok = DynamicPropertyHelper.GetNullableInt(s, "Rok");
                        return rok.HasValue && rok.Value == year.Value;
                    }).ToList();
                }

                // Filter by type
                if (!string.IsNullOrEmpty(type))
                {
                    allStatements = allStatements.Where(s =>
                    {
                        var typ = DynamicPropertyHelper.GetString(s, "Typ");
                        return typ != null && typ.Contains(type, StringComparison.OrdinalIgnoreCase);
                    }).ToList();
                }

                var count = allStatements.Count;
                var pagedStatements = allStatements
                    .OrderByDescending(s => DynamicPropertyHelper.GetNullableInt(s, "Rok") ?? 0)
                    .ThenByDescending(s => DynamicPropertyHelper.GetDateTime(s, "DataUtworzenia") ?? DateTime.MinValue)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var result = new List<FinancialStatementDto>();
                foreach (var s in pagedStatements)
                {
                    result.Add(MapFinancialStatement(s));
                }

                return (result, count);
            });

            if (totalCount == -1)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get SprawozdaniaFinansowe manager"));

            return Ok(new PagedResponse<FinancialStatementDto>
            {
                Data = items!,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting financial statements");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving financial statements", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get financial statement by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<FinancialStatementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<FinancialStatementDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<FinancialStatementDto>>> GetFinancialStatement(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("SprawozdaniaFinansowe");
                if (manager == null) return (FinancialStatementDto?)null;

                var allStatements = DynamicPropertyHelper.SafeGetAll((object)manager);
                var statement = allStatements.FirstOrDefault(s => DynamicPropertyHelper.GetId(s) == id);

                if (statement == null) return (FinancialStatementDto?)null;
                return (FinancialStatementDto?)MapFinancialStatement(statement);
            });

            if (result == null)
                return NotFound(ApiResponse<FinancialStatementDto>.Error($"Financial statement with ID {id} not found"));

            return Ok(ApiResponse<FinancialStatementDto>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting financial statement {Id}", id);
            return StatusCode(500, ApiResponse<FinancialStatementDto>.Error("Error retrieving financial statement", new List<string> { ex.Message }));
        }
    }

    #region Helpers

    private static FinancialStatementDto MapFinancialStatement(dynamic s)
    {
        return new FinancialStatementDto
        {
            Id = DynamicPropertyHelper.GetId(s),
            Name = DynamicPropertyHelper.GetString(s, "Nazwa"),
            Type = DynamicPropertyHelper.GetString(s, "Typ"),
            Year = DynamicPropertyHelper.GetNullableInt(s, "Rok") ?? 0,
            Period = DynamicPropertyHelper.GetString(s, "Okres"),
            Status = DynamicPropertyHelper.GetString(s, "Status") ?? "Unknown",
            CreatedDate = DynamicPropertyHelper.GetDateTime(s, "DataUtworzenia"),
            Description = DynamicPropertyHelper.GetString(s, "Opis")
        };
    }

    #endregion
}

#region DTOs

/// <summary>
/// Financial statement DTO
/// </summary>
public class FinancialStatementDto
{
    public int Id { get; set; }
    /// <summary>
    /// Statement name (Nazwa)
    /// </summary>
    public string? Name { get; set; }
    /// <summary>
    /// Statement type (Typ)
    /// </summary>
    public string? Type { get; set; }
    /// <summary>
    /// Year (Rok)
    /// </summary>
    public int Year { get; set; }
    /// <summary>
    /// Period (Okres)
    /// </summary>
    public string? Period { get; set; }
    /// <summary>
    /// Status (Status)
    /// </summary>
    public string Status { get; set; } = "Unknown";
    /// <summary>
    /// Created date (DataUtworzenia)
    /// </summary>
    public DateTime? CreatedDate { get; set; }
    /// <summary>
    /// Description (Opis)
    /// </summary>
    public string? Description { get; set; }
}

#endregion
