using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Service Orders (Zlecenia Serwisowe) management endpoints
/// </summary>
[ApiController]
[Route("api/service-orders")]
[Authorize]
[Tags("Service Orders")]
public class ServiceOrdersController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<ServiceOrdersController> _logger;

    public ServiceOrdersController(ISferaService sferaService, ILogger<ServiceOrdersController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get service orders (paged, with optional filters)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ServiceOrderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ServiceOrderDto>>> GetServiceOrders(
        [FromQuery] string? status = null,
        [FromQuery] int? customerId = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var (items, totalCount) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("ZleceniaSerwisowe");
                if (manager == null) return (null, -1);

                var allOrders = DynamicPropertyHelper.SafeGetAll((object)manager);

                if (!string.IsNullOrEmpty(status))
                {
                    var statusLower = status.ToLower();
                    allOrders = allOrders.Where(o =>
                    {
                        var s = DynamicPropertyHelper.GetString(o, "Status") ?? "";
                        return s.ToLower().Contains(statusLower);
                    }).ToList();
                }

                if (customerId.HasValue)
                {
                    allOrders = allOrders.Where(o =>
                    {
                        var kontrahent = DynamicPropertyHelper.GetProperty(o, "Kontrahent");
                        return kontrahent != null && DynamicPropertyHelper.GetId(kontrahent) == customerId.Value;
                    }).ToList();
                }

                if (dateFrom.HasValue)
                {
                    allOrders = allOrders.Where(o =>
                    {
                        var date = DynamicPropertyHelper.GetDateTime(o, "DataZgloszenia");
                        return date.HasValue && date.Value >= dateFrom.Value;
                    }).ToList();
                }

                if (dateTo.HasValue)
                {
                    allOrders = allOrders.Where(o =>
                    {
                        var date = DynamicPropertyHelper.GetDateTime(o, "DataZgloszenia");
                        return date.HasValue && date.Value <= dateTo.Value;
                    }).ToList();
                }

                var count = allOrders.Count;
                var pagedOrders = allOrders
                    .OrderByDescending(o => DynamicPropertyHelper.GetDateTime(o, "DataZgloszenia") ?? DateTime.MinValue)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var result = new List<ServiceOrderDto>();
                foreach (var o in pagedOrders)
                {
                    result.Add(MapServiceOrder(o));
                }

                return (result, count);
            });

            if (totalCount == -1)
                return StatusCode(500, ApiResponse<object>.Error("ZleceniaSerwisowe manager not available"));

            return Ok(new PagedResponse<ServiceOrderDto>
            {
                Data = items!,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting service orders");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving service orders", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get service order by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<ServiceOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ServiceOrderDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ServiceOrderDto>>> GetServiceOrder(int id)
    {
        try
        {
            var (managerNull, dto) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("ZleceniaSerwisowe");
                if (manager == null) return (true, (ServiceOrderDto?)null);

                var allOrders = DynamicPropertyHelper.SafeGetAll((object)manager);
                var order = allOrders.FirstOrDefault(o => DynamicPropertyHelper.GetId(o) == id);

                if (order == null)
                    return (false, (ServiceOrderDto?)null);

                return (false, MapServiceOrder(order));
            });

            if (managerNull)
                return StatusCode(500, ApiResponse<object>.Error("ZleceniaSerwisowe manager not available"));

            if (dto == null)
                return NotFound(ApiResponse<ServiceOrderDto>.Error($"Service order with ID {id} not found"));

            return Ok(ApiResponse<ServiceOrderDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting service order {Id}", id);
            return StatusCode(500, ApiResponse<ServiceOrderDto>.Error("Error retrieving service order", new List<string> { ex.Message }));
        }
    }

    #region Mapping

    private static ServiceOrderDto MapServiceOrder(dynamic o)
    {
        var kontrahent = DynamicPropertyHelper.GetProperty(o, "Kontrahent");

        return new ServiceOrderDto
        {
            Id = DynamicPropertyHelper.GetId(o),
            Number = DynamicPropertyHelper.GetString(o, "Numer"),
            CustomerId = kontrahent != null ? DynamicPropertyHelper.GetNullableInt(kontrahent, "Id") : null,
            CustomerName = kontrahent != null ? DynamicPropertyHelper.GetString(kontrahent, "NazwaSkrocona") : null,
            Subject = DynamicPropertyHelper.GetString(o, "Temat"),
            Description = DynamicPropertyHelper.GetString(o, "Opis"),
            Status = DynamicPropertyHelper.GetString(o, "Status"),
            Priority = DynamicPropertyHelper.GetString(o, "Priorytet"),
            AssignedTo = DynamicPropertyHelper.GetString(o, "PrzypisanyDo"),
            ReportedDate = DynamicPropertyHelper.GetDateTime(o, "DataZgloszenia"),
            DeadlineDate = DynamicPropertyHelper.GetDateTime(o, "TerminRealizacji"),
            CompletedDate = DynamicPropertyHelper.GetDateTime(o, "DataZakonczenia"),
            DeviceName = DynamicPropertyHelper.GetString(o, "NazwaUrzadzenia"),
            SerialNumber = DynamicPropertyHelper.GetString(o, "NumerSeryjny")
        };
    }

    #endregion
}

#region DTOs

/// <summary>
/// Service Order (Zlecenie Serwisowe) DTO
/// </summary>
public class ServiceOrderDto
{
    public int Id { get; set; }
    public string? Number { get; set; }
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? Subject { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public string? AssignedTo { get; set; }
    public DateTime? ReportedDate { get; set; }
    public DateTime? DeadlineDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string? DeviceName { get; set; }
    public string? SerialNumber { get; set; }
}

#endregion
