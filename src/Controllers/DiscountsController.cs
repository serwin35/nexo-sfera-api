using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Discounts and promotions endpoints
/// </summary>
[ApiController]
[Route("api/discounts")]
[Authorize]
[Tags("Discounts")]
public class DiscountsController : ControllerBase
{
    /// <summary>Largest page of <c>GET /api/discounts</c>.</summary>
    private const int MaxPageSize = 500;

    private readonly ISferaService _sferaService;
    private readonly ILogger<DiscountsController> _logger;

    public DiscountsController(ISferaService sferaService, ILogger<DiscountsController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    #region Discounts

    /// <summary>
    /// Get all discounts, with the contractors each one is assigned to (<c>subjects</c>)
    /// </summary>
    /// <remarks>
    /// Nexo discounts have no validity period or activity flag, so <c>validAt</c> and <c>activeOnly</c> filter nothing;
    /// discounts in the recycle bin are never listed.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<DiscountDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<DiscountDto>>> GetDiscounts(
        [FromQuery] DateTime? validAt = null,
        [FromQuery] bool? activeOnly = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (page < 1 || pageSize < 1 || pageSize > MaxPageSize)
        {
            return BadRequest(ApiResponse<object>.Error($"page must be at least 1 and pageSize between 1 and {MaxPageSize}"));
        }

        try
        {
            var (items, totalCount) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var sfera = _sferaService.GetSfera();
                var discounts = DiscountReader.All(sfera);
                return (DiscountReader.Page(sfera, discounts, (page - 1) * pageSize, pageSize), discounts.Count());
            });

            return Ok(new PagedResponse<DiscountDto>
            {
                Data = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting discounts");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving discounts", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get discount by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<DiscountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<DiscountDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<DiscountDto>>> GetDiscount(int id, [FromQuery] bool includeDetails = true)
    {
        try
        {
            var dto = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var sfera = _sferaService.GetSfera();
                return DiscountReader.Map(sfera, DiscountReader.All(sfera).Where(r => r.Id == id)).FirstOrDefault();
            });

            if (dto == null)
                return NotFound(ApiResponse<DiscountDto>.Error($"Discount with ID {id} not found"));

            if (!includeDetails) dto.Subjects = null;
            return Ok(ApiResponse<DiscountDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting discount {Id}", id);
            return StatusCode(500, ApiResponse<DiscountDto>.Error("Error retrieving discount", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get discount by symbol
    /// </summary>
    [HttpGet("by-symbol/{symbol}")]
    [ProducesResponseType(typeof(ApiResponse<DiscountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<DiscountDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<DiscountDto>>> GetDiscountBySymbol(string symbol, [FromQuery] bool includeDetails = true)
    {
        try
        {
            var (managerNull, dto) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var rabatyManager = _sferaService.GetManager("Rabaty");
                if (rabatyManager == null) return (true, (DiscountDto?)null);
                var allRabaty = DynamicPropertyHelper.SafeGetAll((object)rabatyManager);
                var rabat = allRabaty.FirstOrDefault(r =>
                    DynamicPropertyHelper.GetString(r, "Symbol") == symbol);

                if (rabat == null)
                    return (false, (DiscountDto?)null);

                return (false, MapDiscount(rabat, includeDetails));
            });

            if (managerNull)
                return StatusCode(500, ApiResponse<object>.Error("Rabaty manager not available"));

            if (dto == null)
                return NotFound(ApiResponse<DiscountDto>.Error($"Discount with symbol '{symbol}' not found"));

            return Ok(ApiResponse<DiscountDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting discount by symbol {Symbol}", symbol);
            return StatusCode(500, ApiResponse<DiscountDto>.Error("Error retrieving discount", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get discounts applicable to a contractor: assigned to it, or assigned to no contractor
    /// </summary>
    [HttpGet("for-contractor/{contractorId}")]
    [ProducesResponseType(typeof(ApiResponse<List<DiscountDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<DiscountDto>>>> GetDiscountsForContractor(int contractorId, [FromQuery] bool? activeOnly = true)
    {
        try
        {
            var dtos = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var sfera = _sferaService.GetSfera();
                if (!DiscountReader.ContractorExists(sfera, contractorId))
                {
                    return null;
                }

                return DiscountReader.Map(sfera, DiscountReader.ForContractor(sfera, contractorId).OrderBy(r => r.Nazwa).ThenBy(r => r.Id));
            });

            if (dtos == null)
                return NotFound(ApiResponse<List<DiscountDto>>.Error($"Contractor with ID {contractorId} not found"));

            return Ok(ApiResponse<List<DiscountDto>>.Ok(dtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting discounts for contractor {ContractorId}", contractorId);
            return StatusCode(500, ApiResponse<List<DiscountDto>>.Error("Error retrieving discounts", new List<string> { ex.Message }));
        }
    }
    #endregion

    #region Mapping

    private static DiscountDto MapDiscount(dynamic r, bool includeDetails)
    {
        var waluta = DynamicPropertyHelper.GetProperty(r, "Waluta");
        var procentRabatu = DynamicPropertyHelper.GetNullableDecimal(r, "ProcentRabatu");
        var kwotaRabatu = DynamicPropertyHelper.GetNullableDecimal(r, "KwotaRabatu");

        var dto = new DiscountDto
        {
            Id = DynamicPropertyHelper.GetId(r),
            Symbol = DynamicPropertyHelper.GetString(r, "Symbol"),
            Name = DynamicPropertyHelper.GetString(r, "Nazwa"),
            Description = DynamicPropertyHelper.GetString(r, "Opis"),
            PercentValue = procentRabatu,
            AmountValue = kwotaRabatu,
            CurrencySymbol = waluta != null ? DynamicPropertyHelper.GetString(waluta, "Symbol") : null,
            ValidFrom = DynamicPropertyHelper.GetDateTime(r, "OdDaty"),
            ValidTo = DynamicPropertyHelper.GetDateTime(r, "DoDaty"),
            IsActive = DynamicPropertyHelper.GetBool(r, "Aktywny"),
            Priority = DynamicPropertyHelper.GetNullableInt(r, "Priorytet")
        };

        // Determine discount type
        if (procentRabatu.HasValue && procentRabatu > 0)
        {
            dto.Type = DiscountType.Percentage;
        }
        else if (kwotaRabatu.HasValue && kwotaRabatu > 0)
        {
            dto.Type = DiscountType.Amount;
        }

        if (includeDetails)
        {
            var podmioty = DynamicPropertyHelper.GetCollection((object)r, "Podmioty");
            if (podmioty.Count > 0)
            {
                dto.Subjects = new List<DiscountSubjectDto>();
                foreach (var p in podmioty)
                {
                    dto.Subjects.Add(new DiscountSubjectDto
                    {
                        Id = DynamicPropertyHelper.GetId(p),
                        SubjectType = "Contractor",
                        SubjectId = DynamicPropertyHelper.GetId(p),
                        SubjectName = DynamicPropertyHelper.GetString(p, "NazwaSkrocona")
                    });
                }
            }
        }

        return dto;
    }

    #endregion
}
