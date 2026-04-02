using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Organization structure (Oddzialy, Centra Kosztow) management endpoints
/// </summary>
[ApiController]
[Route("api/organization")]
[Authorize]
[Tags("Organization")]
public class OrganizationController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<OrganizationController> _logger;

    public OrganizationController(ISferaService sferaService, ILogger<OrganizationController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    #region Branches

    /// <summary>
    /// Get all branches
    /// </summary>
    [HttpGet("branches")]
    [ProducesResponseType(typeof(ApiResponse<List<BranchDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<BranchDto>>>> GetBranches()
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("Oddzialy");
                if (manager == null) return (List<BranchDto>?)null;

                var allBranches = DynamicPropertyHelper.SafeGetAll((object)manager);

                var items = new List<BranchDto>();
                foreach (var b in allBranches.OrderBy(b => DynamicPropertyHelper.GetString(b, "Symbol") ?? ""))
                {
                    items.Add(MapBranch(b));
                }

                return items;
            });

            if (result == null)
                return StatusCode(500, ApiResponse<object>.Error("Oddzialy manager not available"));

            return Ok(ApiResponse<List<BranchDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting branches");
            return StatusCode(500, ApiResponse<List<BranchDto>>.Error("Error retrieving branches", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get branch by ID
    /// </summary>
    [HttpGet("branches/{id}")]
    [ProducesResponseType(typeof(ApiResponse<BranchDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<BranchDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<BranchDto>>> GetBranch(int id)
    {
        try
        {
            var (managerNull, dto) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("Oddzialy");
                if (manager == null) return (true, (BranchDto?)null);

                var allBranches = DynamicPropertyHelper.SafeGetAll((object)manager);
                var branch = allBranches.FirstOrDefault(b => DynamicPropertyHelper.GetId(b) == id);

                if (branch == null)
                    return (false, (BranchDto?)null);

                return (false, MapBranch(branch));
            });

            if (managerNull)
                return StatusCode(500, ApiResponse<object>.Error("Oddzialy manager not available"));

            if (dto == null)
                return NotFound(ApiResponse<BranchDto>.Error($"Branch with ID {id} not found"));

            return Ok(ApiResponse<BranchDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting branch {Id}", id);
            return StatusCode(500, ApiResponse<BranchDto>.Error("Error retrieving branch", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Cost Centers

    /// <summary>
    /// Get all cost centers
    /// </summary>
    [HttpGet("cost-centers")]
    [ProducesResponseType(typeof(ApiResponse<List<CostCenterDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<CostCenterDto>>>> GetCostCenters()
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("CentraKosztow");
                if (manager == null) return (List<CostCenterDto>?)null;

                var allCenters = DynamicPropertyHelper.SafeGetAll((object)manager);

                var items = new List<CostCenterDto>();
                foreach (var c in allCenters.OrderBy(c => DynamicPropertyHelper.GetString(c, "Symbol") ?? ""))
                {
                    items.Add(MapCostCenter(c));
                }

                return items;
            });

            if (result == null)
                return StatusCode(500, ApiResponse<object>.Error("CentraKosztow manager not available"));

            return Ok(ApiResponse<List<CostCenterDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cost centers");
            return StatusCode(500, ApiResponse<List<CostCenterDto>>.Error("Error retrieving cost centers", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get cost center by ID
    /// </summary>
    [HttpGet("cost-centers/{id}")]
    [ProducesResponseType(typeof(ApiResponse<CostCenterDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CostCenterDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CostCenterDto>>> GetCostCenter(int id)
    {
        try
        {
            var (managerNull, dto) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("CentraKosztow");
                if (manager == null) return (true, (CostCenterDto?)null);

                var allCenters = DynamicPropertyHelper.SafeGetAll((object)manager);
                var center = allCenters.FirstOrDefault(c => DynamicPropertyHelper.GetId(c) == id);

                if (center == null)
                    return (false, (CostCenterDto?)null);

                return (false, MapCostCenter(center));
            });

            if (managerNull)
                return StatusCode(500, ApiResponse<object>.Error("CentraKosztow manager not available"));

            if (dto == null)
                return NotFound(ApiResponse<CostCenterDto>.Error($"Cost center with ID {id} not found"));

            return Ok(ApiResponse<CostCenterDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cost center {Id}", id);
            return StatusCode(500, ApiResponse<CostCenterDto>.Error("Error retrieving cost center", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Mapping

    private static BranchDto MapBranch(dynamic b)
    {
        var rodzic = DynamicPropertyHelper.GetProperty(b, "Rodzic");

        return new BranchDto
        {
            Id = DynamicPropertyHelper.GetId(b),
            Name = DynamicPropertyHelper.GetString(b, "Nazwa"),
            Symbol = DynamicPropertyHelper.GetString(b, "Symbol"),
            Address = DynamicPropertyHelper.GetString(b, "Adres"),
            IsActive = DynamicPropertyHelper.GetBool(b, "Aktywny"),
            ParentId = rodzic != null ? DynamicPropertyHelper.GetNullableInt(rodzic, "Id") : null
        };
    }

    private static CostCenterDto MapCostCenter(dynamic c)
    {
        return new CostCenterDto
        {
            Id = DynamicPropertyHelper.GetId(c),
            Name = DynamicPropertyHelper.GetString(c, "Nazwa"),
            Symbol = DynamicPropertyHelper.GetString(c, "Symbol"),
            Description = DynamicPropertyHelper.GetString(c, "Opis"),
            IsActive = DynamicPropertyHelper.GetBool(c, "Aktywny")
        };
    }

    #endregion
}

#region DTOs

/// <summary>
/// Branch (Oddzial) DTO
/// </summary>
public class BranchDto
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Symbol { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; }
    public int? ParentId { get; set; }
}

/// <summary>
/// Cost Center (Centrum Kosztow) DTO
/// </summary>
public class CostCenterDto
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Symbol { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

#endregion
