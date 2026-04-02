using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Office / Accounting Firm Clients (Klienci Biura) management endpoints
/// </summary>
[ApiController]
[Route("api/office-clients")]
[Authorize]
[Tags("Office Clients")]
public class OfficeClientsController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<OfficeClientsController> _logger;

    public OfficeClientsController(ISferaService sferaService, ILogger<OfficeClientsController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get office clients (paged, with optional filters)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<OfficeClientDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<OfficeClientDto>>> GetOfficeClients(
        [FromQuery] bool? active = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var (items, totalCount) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("KlienciBiura");
                if (manager == null) return (null, -1);

                var allItems = DynamicPropertyHelper.SafeGetAll((object)manager);

                // Filter by active status
                if (active.HasValue)
                {
                    allItems = allItems.Where(e =>
                    {
                        var isActive = DynamicPropertyHelper.GetNullableBool(e, "Aktywny") ?? true;
                        return isActive == active.Value;
                    }).ToList();
                }

                var count = allItems.Count;
                var pagedItems = allItems
                    .OrderBy(e => DynamicPropertyHelper.GetString(e, "Nazwa") ?? "")
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var result = new List<OfficeClientDto>();
                foreach (var e in pagedItems)
                {
                    result.Add(MapOfficeClient(e));
                }

                return (result, count);
            });

            if (totalCount == -1)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get KlienciBiura manager"));

            return Ok(new PagedResponse<OfficeClientDto>
            {
                Data = items!,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting office clients");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving office clients", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get office client by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<OfficeClientDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<OfficeClientDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<OfficeClientDto>>> GetOfficeClient(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("KlienciBiura");
                if (manager == null) return (managerNull: true, dto: (OfficeClientDto?)null);

                var allItems = DynamicPropertyHelper.SafeGetAll((object)manager);
                var entry = allItems.FirstOrDefault(e => DynamicPropertyHelper.GetId(e) == id);

                if (entry == null)
                    return (managerNull: false, dto: (OfficeClientDto?)null);

                return (managerNull: false, dto: (OfficeClientDto?)MapOfficeClient(entry));
            });

            if (result.managerNull)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get KlienciBiura manager"));

            if (result.dto == null)
                return NotFound(ApiResponse<OfficeClientDto>.Error($"Office client with ID {id} not found"));

            return Ok(ApiResponse<OfficeClientDto>.Ok(result.dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting office client {Id}", id);
            return StatusCode(500, ApiResponse<OfficeClientDto>.Error("Error retrieving office client", new List<string> { ex.Message }));
        }
    }

    #region Helpers

    private static OfficeClientDto MapOfficeClient(dynamic e)
    {
        return new OfficeClientDto
        {
            Id = DynamicPropertyHelper.GetId(e),
            Name = DynamicPropertyHelper.GetString(e, "Nazwa"),
            NIP = DynamicPropertyHelper.GetString(e, "NIP"),
            CompanyName = DynamicPropertyHelper.GetString(e, "NazwaFirmy"),
            IsActive = DynamicPropertyHelper.GetNullableBool(e, "Aktywny") ?? true,
            ContractDate = DynamicPropertyHelper.GetDateTime(e, "DataUmowy"),
            Description = DynamicPropertyHelper.GetString(e, "Opis")
        };
    }

    #endregion
}

#region DTOs

/// <summary>
/// Office / Accounting firm client DTO
/// </summary>
public class OfficeClientDto
{
    public int Id { get; set; }
    /// <summary>
    /// Client name (Nazwa)
    /// </summary>
    public string? Name { get; set; }
    /// <summary>
    /// Tax identification number (NIP)
    /// </summary>
    public string? NIP { get; set; }
    /// <summary>
    /// Company name (NazwaFirmy)
    /// </summary>
    public string? CompanyName { get; set; }
    /// <summary>
    /// Whether the client is active (Aktywny)
    /// </summary>
    public bool IsActive { get; set; }
    /// <summary>
    /// Contract date (DataUmowy)
    /// </summary>
    public DateTime? ContractDate { get; set; }
    /// <summary>
    /// Description (Opis)
    /// </summary>
    public string? Description { get; set; }
}

#endregion
