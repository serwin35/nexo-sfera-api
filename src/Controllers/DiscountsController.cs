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
    private readonly ISferaService _sferaService;
    private readonly ILogger<DiscountsController> _logger;

    public DiscountsController(ISferaService sferaService, ILogger<DiscountsController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    #region Discounts

    /// <summary>
    /// Get all discounts
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<DiscountDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<DiscountDto>>> GetDiscounts(
        [FromQuery] DateTime? validAt = null,
        [FromQuery] bool? activeOnly = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var rabatyManager = _sferaService.GetManager("Rabaty");
                if (rabatyManager == null) return (null, 0);
                var allRabaty = DynamicPropertyHelper.SafeGetAll((object)rabatyManager);

                if (activeOnly == true)
                {
                    allRabaty = allRabaty.Where(r => DynamicPropertyHelper.GetBool(r, "Aktywny")).ToList();
                }

                var checkDate = validAt ?? DateTime.Now;
                allRabaty = allRabaty.Where(r =>
                {
                    var odDaty = DynamicPropertyHelper.GetDateTime(r, "OdDaty");
                    var doDaty = DynamicPropertyHelper.GetDateTime(r, "DoDaty");
                    return (!odDaty.HasValue || odDaty.Value <= checkDate) &&
                           (!doDaty.HasValue || doDaty.Value >= checkDate);
                }).ToList();

                var totalCount = allRabaty.Count;
                var pagedRabaty = allRabaty
                    .OrderBy(r => DynamicPropertyHelper.GetString(r, "Symbol"))
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var items = new List<DiscountDto>();
                foreach (var r in pagedRabaty)
                {
                    items.Add(MapDiscount(r, false));
                }

                return (items, totalCount);
            });

            if (result.Item1 == null)
                return StatusCode(500, ApiResponse<object>.Error("Rabaty manager not available"));

            return Ok(new PagedResponse<DiscountDto>
            {
                Data = result.Item1,
                Page = page,
                PageSize = pageSize,
                TotalCount = result.Item2
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
            var (managerNull, dto) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var rabatyManager = _sferaService.GetManager("Rabaty");
                if (rabatyManager == null) return (true, (DiscountDto?)null);
                var allRabaty = DynamicPropertyHelper.SafeGetAll((object)rabatyManager);
                var rabat = allRabaty.FirstOrDefault(r => DynamicPropertyHelper.GetId(r) == id);

                if (rabat == null)
                    return (false, (DiscountDto?)null);

                return (false, MapDiscount(rabat, includeDetails));
            });

            if (managerNull)
                return StatusCode(500, ApiResponse<object>.Error("Rabaty manager not available"));

            if (dto == null)
                return NotFound(ApiResponse<DiscountDto>.Error($"Discount with ID {id} not found"));

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
    /// Get discounts applicable to a contractor
    /// </summary>
    [HttpGet("for-contractor/{contractorId}")]
    [ProducesResponseType(typeof(ApiResponse<List<DiscountDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<DiscountDto>>>> GetDiscountsForContractor(int contractorId, [FromQuery] bool? activeOnly = true)
    {
        try
        {
            var (status, dtos) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                // Verify contractor exists
                var kontrahenciManager = _sferaService.GetManager("Podmioty");
                if (kontrahenciManager == null) return ("podmioty_null", (List<DiscountDto>?)null);
                var allKontrahenci = DynamicPropertyHelper.SafeGetAll((object)kontrahenciManager);
                var kontrahent = allKontrahenci.FirstOrDefault(k => DynamicPropertyHelper.GetId(k) == contractorId);

                if (kontrahent == null)
                    return ("not_found", (List<DiscountDto>?)null);

                var rabatyManager = _sferaService.GetManager("Rabaty");
                if (rabatyManager == null) return ("rabaty_null", (List<DiscountDto>?)null);
                var allRabaty = DynamicPropertyHelper.SafeGetAll((object)rabatyManager);

                if (activeOnly == true)
                {
                    allRabaty = allRabaty.Where(r => DynamicPropertyHelper.GetBool(r, "Aktywny")).ToList();
                }

                // Filter by contractor
                var now = DateTime.Now;
                var applicableDiscounts = allRabaty
                    .Where(r =>
                    {
                        var odDaty = DynamicPropertyHelper.GetDateTime(r, "OdDaty");
                        var doDaty = DynamicPropertyHelper.GetDateTime(r, "DoDaty");
                        return (!odDaty.HasValue || odDaty.Value <= now) &&
                               (!doDaty.HasValue || doDaty.Value >= now);
                    })
                    .Where(r =>
                    {
                        var podmioty = DynamicPropertyHelper.GetCollection((object)r, "Podmioty");
                        // No subjects = applies to all, OR contractor is in the list
                        if (!podmioty.Any()) return true;
                        foreach (var p in podmioty)
                        {
                            if (DynamicPropertyHelper.GetId(p) == contractorId) return true;
                        }
                        return false;
                    })
                    .ToList();

                var applicableDiscountDtos = new List<DiscountDto>();
                foreach (var r in applicableDiscounts)
                {
                    applicableDiscountDtos.Add(MapDiscount(r, false));
                }

                return ("ok", applicableDiscountDtos);
            });

            if (status == "podmioty_null")
                return StatusCode(500, ApiResponse<List<DiscountDto>>.Error("Podmioty manager not available"));

            if (status == "not_found")
                return NotFound(ApiResponse<List<DiscountDto>>.Error($"Contractor with ID {contractorId} not found"));

            if (status == "rabaty_null")
                return StatusCode(500, ApiResponse<List<DiscountDto>>.Error("Rabaty manager not available"));

            return Ok(ApiResponse<List<DiscountDto>>.Ok(dtos!));
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
