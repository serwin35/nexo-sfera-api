using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Booking Documents (Dokumenty do Ksiegowania) management endpoints
/// </summary>
[ApiController]
[Route("api/booking-documents")]
[Authorize]
[Tags("Booking Documents")]
public class BookingDocumentsController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<BookingDocumentsController> _logger;

    public BookingDocumentsController(ISferaService sferaService, ILogger<BookingDocumentsController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get documents for booking list
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<BookingDocumentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<BookingDocumentDto>>> GetBookingDocuments(
        [FromQuery] string? status,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] string? type,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var (items, totalCount) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("DokumentyDoKsiegowania");
                if (manager == null) return (null, -1);

                var allDocuments = DynamicPropertyHelper.SafeGetAll((object)manager);

                // Filter by status
                if (!string.IsNullOrEmpty(status))
                {
                    allDocuments = allDocuments.Where(d =>
                    {
                        var docStatus = DynamicPropertyHelper.GetString(d, "Status");
                        return docStatus != null && docStatus.Equals(status, StringComparison.OrdinalIgnoreCase);
                    }).ToList();
                }

                // Filter by date range
                if (dateFrom.HasValue)
                {
                    allDocuments = allDocuments.Where(d =>
                    {
                        var data = DynamicPropertyHelper.GetDateTime(d, "DataWystawienia");
                        return data.HasValue && data.Value >= dateFrom.Value;
                    }).ToList();
                }

                if (dateTo.HasValue)
                {
                    allDocuments = allDocuments.Where(d =>
                    {
                        var data = DynamicPropertyHelper.GetDateTime(d, "DataWystawienia");
                        return data.HasValue && data.Value <= dateTo.Value;
                    }).ToList();
                }

                // Filter by document type
                if (!string.IsNullOrEmpty(type))
                {
                    allDocuments = allDocuments.Where(d =>
                    {
                        var docType = DynamicPropertyHelper.GetString(d, "TypDokumentu");
                        return docType != null && docType.Contains(type, StringComparison.OrdinalIgnoreCase);
                    }).ToList();
                }

                var count = allDocuments.Count;
                var pagedDocuments = allDocuments
                    .OrderByDescending(d => DynamicPropertyHelper.GetDateTime(d, "DataWystawienia") ?? DateTime.MinValue)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var result = new List<BookingDocumentDto>();
                foreach (var d in pagedDocuments)
                {
                    result.Add(MapBookingDocument(d));
                }

                return (result, count);
            });

            if (totalCount == -1)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get DokumentyDoKsiegowania manager"));

            return Ok(new PagedResponse<BookingDocumentDto>
            {
                Data = items!,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting booking documents");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving booking documents", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get booking document by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<BookingDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<BookingDocumentDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<BookingDocumentDto>>> GetBookingDocument(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("DokumentyDoKsiegowania");
                if (manager == null) return (BookingDocumentDto?)null;

                var allDocuments = DynamicPropertyHelper.SafeGetAll((object)manager);
                var document = allDocuments.FirstOrDefault(d => DynamicPropertyHelper.GetId(d) == id);

                if (document == null) return (BookingDocumentDto?)null;
                return (BookingDocumentDto?)MapBookingDocument(document);
            });

            if (result == null)
                return NotFound(ApiResponse<BookingDocumentDto>.Error($"Booking document with ID {id} not found"));

            return Ok(ApiResponse<BookingDocumentDto>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting booking document {Id}", id);
            return StatusCode(500, ApiResponse<BookingDocumentDto>.Error("Error retrieving booking document", new List<string> { ex.Message }));
        }
    }

    #region Helpers

    private static BookingDocumentDto MapBookingDocument(dynamic d)
    {
        return new BookingDocumentDto
        {
            Id = DynamicPropertyHelper.GetId(d),
            DocumentNumber = DynamicPropertyHelper.GetString(d, "NumerDokumentu"),
            DocumentType = DynamicPropertyHelper.GetString(d, "TypDokumentu"),
            Status = DynamicPropertyHelper.GetString(d, "Status") ?? "Unknown",
            SourceDocumentNumber = DynamicPropertyHelper.GetString(d, "NumerDokumentuZrodlowego"),
            IssueDate = DynamicPropertyHelper.GetDateTime(d, "DataWystawienia"),
            BookingDate = DynamicPropertyHelper.GetDateTime(d, "DataKsiegowania"),
            NetAmount = DynamicPropertyHelper.GetDecimal(d, "KwotaNetto"),
            GrossAmount = DynamicPropertyHelper.GetDecimal(d, "KwotaBrutto"),
            VatAmount = DynamicPropertyHelper.GetDecimal(d, "KwotaVat"),
            CustomerName = DynamicPropertyHelper.GetString(d, "NazwaKontrahenta"),
            Description = DynamicPropertyHelper.GetString(d, "Opis"),
            IsBooked = DynamicPropertyHelper.GetBool(d, "Zaksiegowany")
        };
    }

    #endregion
}

#region DTOs

/// <summary>
/// Booking document DTO
/// </summary>
public class BookingDocumentDto
{
    public int Id { get; set; }
    /// <summary>
    /// Document number (NumerDokumentu)
    /// </summary>
    public string? DocumentNumber { get; set; }
    /// <summary>
    /// Document type (TypDokumentu)
    /// </summary>
    public string? DocumentType { get; set; }
    /// <summary>
    /// Status (Status)
    /// </summary>
    public string Status { get; set; } = "Unknown";
    /// <summary>
    /// Source document number (NumerDokumentuZrodlowego)
    /// </summary>
    public string? SourceDocumentNumber { get; set; }
    /// <summary>
    /// Issue date (DataWystawienia)
    /// </summary>
    public DateTime? IssueDate { get; set; }
    /// <summary>
    /// Booking date (DataKsiegowania)
    /// </summary>
    public DateTime? BookingDate { get; set; }
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
    /// Customer name (NazwaKontrahenta)
    /// </summary>
    public string? CustomerName { get; set; }
    /// <summary>
    /// Description (Opis)
    /// </summary>
    public string? Description { get; set; }
    /// <summary>
    /// Whether the document has been booked (Zaksiegowany)
    /// </summary>
    public bool IsBooked { get; set; }
}

#endregion
