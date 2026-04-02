using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Accruals/Deferrals (Rozliczenia Międzyokresowe) management endpoints
/// SDK 60.0.0: Major restructuring of RozliczenieMiedzyokresowe entity
/// </summary>
[ApiController]
[Route("api/accruals")]
[Authorize]
[Tags("Accruals")]
public class AccrualsController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<AccrualsController> _logger;

    public AccrualsController(ISferaService sferaService, ILogger<AccrualsController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get accruals/deferrals list
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<AccrualDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<AccrualDto>>> GetAccruals(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var (items, totalCount) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("RozliczeniaMiedzyokresowe");
                if (manager == null) return (null, -1);

                var allAccruals = DynamicPropertyHelper.SafeGetAll((object)manager);
                var count = allAccruals.Count;

                var pagedAccruals = allAccruals
                    .OrderByDescending(a => DynamicPropertyHelper.GetDateTime(a, "DataUtworzenia") ?? DateTime.MinValue)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var result = new List<AccrualDto>();
                foreach (var a in pagedAccruals)
                {
                    result.Add(MapAccrual(a));
                }

                return (result, count);
            });

            if (totalCount == -1)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get RozliczeniaMiedzyokresowe manager"));

            return Ok(new PagedResponse<AccrualDto>
            {
                Data = items!,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting accruals");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving accruals", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get accrual by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<AccrualDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AccrualDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AccrualDto>>> GetAccrual(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("RozliczeniaMiedzyokresowe");
                if (manager == null) return (AccrualDto?)null;

                var allAccruals = DynamicPropertyHelper.SafeGetAll((object)manager);
                var accrual = allAccruals.FirstOrDefault(a => DynamicPropertyHelper.GetId(a) == id);

                if (accrual == null) return (AccrualDto?)null;
                return (AccrualDto?)MapAccrual(accrual);
            });

            if (result == null)
                return NotFound(ApiResponse<AccrualDto>.Error($"Accrual with ID {id} not found"));

            return Ok(ApiResponse<AccrualDto>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting accrual {Id}", id);
            return StatusCode(500, ApiResponse<AccrualDto>.Error("Error retrieving accrual", new List<string> { ex.Message }));
        }
    }

    private static AccrualDto MapAccrual(dynamic a)
    {
        // SDK 60.0.0: NaglowekRozliczeniaMiedzyokresowego is the new header entity
        var naglowek = DynamicPropertyHelper.GetProperty(a, "NaglowekRozliczeniaMiedzyokresowego");
        var sygnatura = DynamicPropertyHelper.GetProperty(a, "Sygnatura");

        return new AccrualDto
        {
            Id = DynamicPropertyHelper.GetId(a),
            // SDK 60.0.0: Removed Nazwa, added NumerDokumentuZrodlowego
            SourceDocumentNumber = DynamicPropertyHelper.GetString(a, "NumerDokumentuZrodlowego"),
            SignatureNumber = sygnatura != null ? DynamicPropertyHelper.GetString(sygnatura, "PelnaSygnatura") : null,
            // SDK 60.0.0: NaglowekRozliczeniaMiedzyokresowego header
            HeaderId = naglowek != null ? DynamicPropertyHelper.GetNullableInt(naglowek, "Id") : null,
            CreatedDate = DynamicPropertyHelper.GetDateTime(a, "DataUtworzenia"),
            // SDK 60.0.0: Added FlagaWlasna support
            HasCustomFlag = DynamicPropertyHelper.GetProperty(a, "FlagaWlasna") != null
        };
    }
}

#region DTOs

/// <summary>
/// Accrual/Deferral DTO (SDK 60.0.0: restructured RozliczenieMiedzyokresowe)
/// </summary>
public class AccrualDto
{
    public int Id { get; set; }
    /// <summary>
    /// Source document number (SDK 60.0.0: replaces Nazwa/Typ with NumerDokumentuZrodlowego)
    /// </summary>
    public string? SourceDocumentNumber { get; set; }
    public string? SignatureNumber { get; set; }
    /// <summary>
    /// Header ID (SDK 60.0.0: NaglowekRozliczeniaMiedzyokresowego)
    /// </summary>
    public int? HeaderId { get; set; }
    public DateTime? CreatedDate { get; set; }
    public bool HasCustomFlag { get; set; }
}

#endregion
