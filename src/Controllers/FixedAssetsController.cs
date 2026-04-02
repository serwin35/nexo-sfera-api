using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Fixed Assets (Srodki Trwale) management endpoints
/// </summary>
[ApiController]
[Route("api/fixed-assets")]
[Authorize]
[Tags("Fixed Assets")]
public class FixedAssetsController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<FixedAssetsController> _logger;

    public FixedAssetsController(ISferaService sferaService, ILogger<FixedAssetsController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get fixed assets (paged, with optional filters)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<FixedAssetDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<FixedAssetDto>>> GetFixedAssets(
        [FromQuery] string? group = null,
        [FromQuery] string? status = null,
        [FromQuery] bool? inUse = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var (items, totalCount) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("SrodkiTrwale");
                if (manager == null) return (null, -1);

                var allAssets = DynamicPropertyHelper.SafeGetAll((object)manager);

                if (!string.IsNullOrEmpty(group))
                {
                    var groupLower = group.ToLower();
                    allAssets = allAssets.Where(a =>
                    {
                        var g = DynamicPropertyHelper.GetString(a, "Grupa") ?? "";
                        return g.ToLower().Contains(groupLower);
                    }).ToList();
                }

                if (!string.IsNullOrEmpty(status))
                {
                    var statusLower = status.ToLower();
                    allAssets = allAssets.Where(a =>
                    {
                        var s = DynamicPropertyHelper.GetString(a, "Status") ?? "";
                        return s.ToLower().Contains(statusLower);
                    }).ToList();
                }

                if (inUse.HasValue)
                {
                    allAssets = allAssets.Where(a =>
                    {
                        // Assets in use have no disposal date
                        var disposalDate = DynamicPropertyHelper.GetDateTime(a, "DataLikwidacji");
                        return inUse.Value ? !disposalDate.HasValue : disposalDate.HasValue;
                    }).ToList();
                }

                var count = allAssets.Count;
                var pagedAssets = allAssets
                    .OrderBy(a => DynamicPropertyHelper.GetString(a, "NumerInwentarzowy") ?? "")
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var result = new List<FixedAssetDto>();
                foreach (var a in pagedAssets)
                {
                    result.Add(MapFixedAsset(a));
                }

                return (result, count);
            });

            if (totalCount == -1)
                return StatusCode(500, ApiResponse<object>.Error("SrodkiTrwale manager not available"));

            return Ok(new PagedResponse<FixedAssetDto>
            {
                Data = items!,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting fixed assets");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving fixed assets", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get fixed asset by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<FixedAssetDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<FixedAssetDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<FixedAssetDto>>> GetFixedAsset(int id)
    {
        try
        {
            var (managerNull, dto) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("SrodkiTrwale");
                if (manager == null) return (true, (FixedAssetDto?)null);

                var allAssets = DynamicPropertyHelper.SafeGetAll((object)manager);
                var asset = allAssets.FirstOrDefault(a => DynamicPropertyHelper.GetId(a) == id);

                if (asset == null)
                    return (false, (FixedAssetDto?)null);

                return (false, MapFixedAsset(asset));
            });

            if (managerNull)
                return StatusCode(500, ApiResponse<object>.Error("SrodkiTrwale manager not available"));

            if (dto == null)
                return NotFound(ApiResponse<FixedAssetDto>.Error($"Fixed asset with ID {id} not found"));

            return Ok(ApiResponse<FixedAssetDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting fixed asset {Id}", id);
            return StatusCode(500, ApiResponse<FixedAssetDto>.Error("Error retrieving fixed asset", new List<string> { ex.Message }));
        }
    }

    #region Mapping

    private static FixedAssetDto MapFixedAsset(dynamic a)
    {
        return new FixedAssetDto
        {
            Id = DynamicPropertyHelper.GetId(a),
            Name = DynamicPropertyHelper.GetString(a, "Nazwa"),
            InventoryNumber = DynamicPropertyHelper.GetString(a, "NumerInwentarzowy"),
            Group = DynamicPropertyHelper.GetString(a, "Grupa"),
            InitialValue = DynamicPropertyHelper.GetDecimal(a, "WartoscPoczatkowa"),
            CurrentValue = DynamicPropertyHelper.GetDecimal(a, "WartoscBiezaca"),
            DepreciationRate = DynamicPropertyHelper.GetDecimal(a, "StawkaAmortyzacji"),
            DepreciationMethod = DynamicPropertyHelper.GetString(a, "MetodaAmortyzacji"),
            PurchaseDate = DynamicPropertyHelper.GetDateTime(a, "DataNabycia"),
            DepreciationStartDate = DynamicPropertyHelper.GetDateTime(a, "DataRozpoczeciaAmortyzacji"),
            DisposalDate = DynamicPropertyHelper.GetDateTime(a, "DataLikwidacji"),
            Location = DynamicPropertyHelper.GetString(a, "Lokalizacja"),
            Status = DynamicPropertyHelper.GetString(a, "Status"),
            Description = DynamicPropertyHelper.GetString(a, "Opis")
        };
    }

    #endregion
}

#region DTOs

/// <summary>
/// Fixed Asset (Srodek Trwaly) DTO
/// </summary>
public class FixedAssetDto
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? InventoryNumber { get; set; }
    public string? Group { get; set; }
    public decimal InitialValue { get; set; }
    public decimal CurrentValue { get; set; }
    public decimal DepreciationRate { get; set; }
    public string? DepreciationMethod { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public DateTime? DepreciationStartDate { get; set; }
    public DateTime? DisposalDate { get; set; }
    public string? Location { get; set; }
    public string? Status { get; set; }
    public string? Description { get; set; }
}

#endregion
