using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Internal Documents (Dowody Wewnetrzne) management endpoints
/// </summary>
[ApiController]
[Route("api/internal-documents")]
[Authorize]
[Tags("Internal Documents")]
public class InternalDocumentsController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<InternalDocumentsController> _logger;

    public InternalDocumentsController(ISferaService sferaService, ILogger<InternalDocumentsController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get internal documents (paged, with optional filters)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<InternalDocumentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<InternalDocumentDto>>> GetInternalDocuments(
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] string? type = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var (items, totalCount) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("DowodyWewnetrzne");
                if (manager == null) return (null, -1);

                var allItems = DynamicPropertyHelper.SafeGetAll((object)manager);

                // Filter by date range
                if (dateFrom.HasValue)
                {
                    allItems = allItems.Where(e =>
                    {
                        var date = DynamicPropertyHelper.GetDateTime(e, "DataWystawienia");
                        return date.HasValue && date.Value >= dateFrom.Value;
                    }).ToList();
                }

                if (dateTo.HasValue)
                {
                    allItems = allItems.Where(e =>
                    {
                        var date = DynamicPropertyHelper.GetDateTime(e, "DataWystawienia");
                        return date.HasValue && date.Value <= dateTo.Value;
                    }).ToList();
                }

                // Filter by type
                if (!string.IsNullOrEmpty(type))
                {
                    allItems = allItems.Where(e =>
                    {
                        var typ = DynamicPropertyHelper.GetString(e, "Typ");
                        return typ != null && typ.Equals(type, StringComparison.OrdinalIgnoreCase);
                    }).ToList();
                }

                var count = allItems.Count;
                var pagedItems = allItems
                    .OrderByDescending(e => DynamicPropertyHelper.GetDateTime(e, "DataWystawienia") ?? DateTime.MinValue)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var result = new List<InternalDocumentDto>();
                foreach (var e in pagedItems)
                {
                    result.Add(MapInternalDocument(e));
                }

                return (result, count);
            });

            if (totalCount == -1)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get DowodyWewnetrzne manager"));

            return Ok(new PagedResponse<InternalDocumentDto>
            {
                Data = items!,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting internal documents");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving internal documents", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get internal document by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<InternalDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InternalDocumentDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<InternalDocumentDto>>> GetInternalDocument(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("DowodyWewnetrzne");
                if (manager == null) return (managerNull: true, dto: (InternalDocumentDto?)null);

                var allItems = DynamicPropertyHelper.SafeGetAll((object)manager);
                var entry = allItems.FirstOrDefault(e => DynamicPropertyHelper.GetId(e) == id);

                if (entry == null)
                    return (managerNull: false, dto: (InternalDocumentDto?)null);

                return (managerNull: false, dto: (InternalDocumentDto?)MapInternalDocument(entry));
            });

            if (result.managerNull)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get DowodyWewnetrzne manager"));

            if (result.dto == null)
                return NotFound(ApiResponse<InternalDocumentDto>.Error($"Internal document with ID {id} not found"));

            return Ok(ApiResponse<InternalDocumentDto>.Ok(result.dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting internal document {Id}", id);
            return StatusCode(500, ApiResponse<InternalDocumentDto>.Error("Error retrieving internal document", new List<string> { ex.Message }));
        }
    }

    #region Helpers

    private static InternalDocumentDto MapInternalDocument(dynamic e)
    {
        return new InternalDocumentDto
        {
            Id = DynamicPropertyHelper.GetId(e),
            Number = DynamicPropertyHelper.GetString(e, "Numer"),
            Type = DynamicPropertyHelper.GetString(e, "Typ"),
            IssueDate = DynamicPropertyHelper.GetDateTime(e, "DataWystawienia"),
            Description = DynamicPropertyHelper.GetString(e, "Opis"),
            NetAmount = DynamicPropertyHelper.GetDecimal(e, "KwotaNetto"),
            GrossAmount = DynamicPropertyHelper.GetDecimal(e, "KwotaBrutto"),
            VatAmount = DynamicPropertyHelper.GetDecimal(e, "KwotaVat"),
            Status = DynamicPropertyHelper.GetString(e, "Status"),
            CreatedDate = DynamicPropertyHelper.GetDateTime(e, "DataUtworzenia")
        };
    }

    #endregion
}

#region DTOs

/// <summary>
/// Internal document DTO
/// </summary>
public class InternalDocumentDto
{
    public int Id { get; set; }
    /// <summary>
    /// Document number (Numer)
    /// </summary>
    public string? Number { get; set; }
    /// <summary>
    /// Document type (Typ)
    /// </summary>
    public string? Type { get; set; }
    /// <summary>
    /// Issue date (DataWystawienia)
    /// </summary>
    public DateTime? IssueDate { get; set; }
    /// <summary>
    /// Description (Opis)
    /// </summary>
    public string? Description { get; set; }
    /// <summary>
    /// Net amount (KwotaNetto)
    /// </summary>
    public decimal NetAmount { get; set; }
    /// <summary>
    /// Gross amount (KwotaBrutto)
    /// </summary>
    public decimal GrossAmount { get; set; }
    /// <summary>
    /// VAT amount (KwotaVat)
    /// </summary>
    public decimal VatAmount { get; set; }
    /// <summary>
    /// Document status (Status)
    /// </summary>
    public string? Status { get; set; }
    /// <summary>
    /// Created date (DataUtworzenia)
    /// </summary>
    public DateTime? CreatedDate { get; set; }
}

#endregion
