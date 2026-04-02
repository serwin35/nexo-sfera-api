using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Promotions and discount campaigns management
/// </summary>
[ApiController]
[Route("api/promotions")]
[Authorize]
[Tags("Promotions")]
public class PromotionsController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<PromotionsController> _logger;

    public PromotionsController(ISferaService sferaService, ILogger<PromotionsController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get promotions (paged, with optional active/inactive filter)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<PromotionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<PromotionDto>>> GetPromotions(
        [FromQuery] bool? active,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("Promocje");
                if (manager == null)
                    return (null, 0);

                var allItems = DynamicPropertyHelper.SafeGetAll((object)manager);

                // Filter by active/inactive
                if (active.HasValue)
                {
                    allItems = allItems.Where(p =>
                    {
                        var isActive = DynamicPropertyHelper.GetBool(p, "Aktywny");
                        return isActive == active.Value;
                    }).ToList();
                }

                var totalCount = allItems.Count;
                var pagedItems = allItems
                    .OrderByDescending(p => DynamicPropertyHelper.GetDateTime(p, "DataOd") ?? DateTime.MinValue)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var items = new List<PromotionDto>();
                foreach (var p in pagedItems)
                {
                    items.Add(new PromotionDto
                    {
                        Id = DynamicPropertyHelper.GetId(p),
                        Name = DynamicPropertyHelper.GetString(p, "Nazwa") ?? "",
                        Description = DynamicPropertyHelper.GetString(p, "Opis"),
                        IsActive = DynamicPropertyHelper.GetBool(p, "Aktywny"),
                        DateFrom = DynamicPropertyHelper.GetDateTime(p, "DataOd"),
                        DateTo = DynamicPropertyHelper.GetDateTime(p, "DataDo"),
                        DiscountType = DynamicPropertyHelper.GetString(p, "TypRabatu"),
                        DiscountValue = DynamicPropertyHelper.GetDecimal(p, "WartoscRabatu"),
                        DiscountPercent = DynamicPropertyHelper.GetDecimal(p, "ProcentRabatu"),
                        Priority = DynamicPropertyHelper.GetNullableInt(p, "Priorytet") ?? 0
                    });
                }

                return (items, totalCount);
            });

            if (result.Item1 == null)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Promocje manager"));

            return Ok(new PagedResponse<PromotionDto>
            {
                Data = result.Item1,
                Page = page,
                PageSize = pageSize,
                TotalCount = result.Item2
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting promotions");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving promotions", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get promotion by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<PromotionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PromotionDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PromotionDto>>> GetPromotion(int id)
    {
        try
        {
            var (managerNull, dto) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("Promocje");
                if (manager == null)
                    return (true, (PromotionDto?)null);

                var allItems = DynamicPropertyHelper.SafeGetAll((object)manager);
                var item = allItems.FirstOrDefault(p => DynamicPropertyHelper.GetId(p) == id);

                if (item == null)
                    return (false, (PromotionDto?)null);

                return (false, new PromotionDto
                {
                    Id = DynamicPropertyHelper.GetId(item),
                    Name = DynamicPropertyHelper.GetString(item, "Nazwa") ?? "",
                    Description = DynamicPropertyHelper.GetString(item, "Opis"),
                    IsActive = DynamicPropertyHelper.GetBool(item, "Aktywny"),
                    DateFrom = DynamicPropertyHelper.GetDateTime(item, "DataOd"),
                    DateTo = DynamicPropertyHelper.GetDateTime(item, "DataDo"),
                    DiscountType = DynamicPropertyHelper.GetString(item, "TypRabatu"),
                    DiscountValue = DynamicPropertyHelper.GetDecimal(item, "WartoscRabatu"),
                    DiscountPercent = DynamicPropertyHelper.GetDecimal(item, "ProcentRabatu"),
                    Priority = DynamicPropertyHelper.GetNullableInt(item, "Priorytet") ?? 0
                });
            });

            if (managerNull)
                return StatusCode(500, ApiResponse<PromotionDto>.Error("Failed to get Promocje manager"));

            if (dto == null)
                return NotFound(ApiResponse<PromotionDto>.Error($"Promotion with ID {id} not found"));

            return Ok(ApiResponse<PromotionDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting promotion {Id}", id);
            return StatusCode(500, ApiResponse<PromotionDto>.Error("Error retrieving promotion", new List<string> { ex.Message }));
        }
    }
}

#region DTOs

/// <summary>
/// Promotion DTO
/// </summary>
public class PromotionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal DiscountPercent { get; set; }
    public int Priority { get; set; }
}

#endregion
