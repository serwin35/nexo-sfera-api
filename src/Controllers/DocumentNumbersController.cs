using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InsERT.Moria.Sfera;
using NexoSferaApi.Helpers;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Read-only numbering preview: the number Nexo would give the next document of a type on a day.
/// </summary>
/// <remarks>
/// A separate controller under <c>api/documents</c> keeps this read-only endpoint apart from the document writes.
/// A literal route ("next-number") takes precedence over <c>{id}</c> routes of <see cref="DocumentsController"/>.
/// </remarks>
[ApiController]
[Route("api/documents")]
[Authorize]
[Tags("Documents")]
public class DocumentNumbersController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<DocumentNumbersController> _logger;

    public DocumentNumbersController(ISferaService sferaService, ILogger<DocumentNumbersController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// The next number of a type (FS, PA, KFS, ZW, RW, PW) on a day, via <c>IZrodloNumerow.PodajPrzewidywanyNumer</c>:
    /// an exact preview without a reservation; nothing is saved (<see cref="NextNumberPreview"/>).
    /// </summary>
    /// <param name="type">FS, PA, KFS, ZW, RW or PW.</param>
    /// <param name="date">The issue date, yyyy-MM-dd: the series follows it.</param>
    /// <param name="warehouse">The warehouse set before the number, as the create endpoints do (default MG); a KFS / ZW takes
    /// it instead of its original's, which is not loaded (the number is then an approximation, see <c>basis</c>).</param>
    [HttpGet("next-number")]
    [ProducesResponseType(typeof(ApiResponse<NextNumberDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<NextNumberDto>>> GetNextNumber(
        [FromQuery] string? type,
        [FromQuery] string? date,
        [FromQuery] string? warehouse)
    {
        if (NextNumberPreview.CheckType(type, out var documentType) is { } typeError)
        {
            return BadRequest(ApiResponse<NextNumberDto>.Error(typeError));
        }

        if (!StockAtDate.TryParseDay(date, out var day))
        {
            return BadRequest(ApiResponse<NextNumberDto>.Error($"date is required as {StockAtDate.DateFormat}"));
        }

        var warehouseSymbol = string.IsNullOrWhiteSpace(warehouse) ? StockAtDate.DefaultWarehouse : warehouse.Trim();
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var sfera = _sferaService.GetSfera();
                var magazyn = sfera.Magazyny().Dane.Wszystkie().FirstOrDefault(m => m.Symbol == warehouseSymbol);
                if (magazyn == null)
                {
                    return (status: 404, dto: (NextNumberDto?)null, error: $"Warehouse '{warehouseSymbol}' not found");
                }

                var outcome = NextNumberPreview.Read(sfera, documentType, day, magazyn);
                if (outcome.Error != null || outcome.Sequence == null || outcome.FullNumber == null)
                {
                    _logger.LogWarning("Next-number preview {Type} {Date}: {Error}", documentType, day, outcome.Error);
                    return (status: 500, dto: (NextNumberDto?)null, error: outcome.Error ?? "Nexo predicted no number");
                }

                return (status: 200, dto: (NextNumberDto?)new NextNumberDto
                {
                    Type = documentType,
                    Date = day.ToString(StockAtDate.DateFormat, CultureInfo.InvariantCulture),
                    WarehouseSymbol = warehouseSymbol,
                    FullNumber = outcome.FullNumber,
                    Sequence = outcome.Sequence.Value,
                    Basis = outcome.Basis,
                }, error: (string?)null);
            });

            return result.status switch
            {
                200 => Ok(ApiResponse<NextNumberDto>.Ok(result.dto!)),
                404 => NotFound(ApiResponse<NextNumberDto>.Error(result.error!)),
                _ => StatusCode(500, ApiResponse<NextNumberDto>.Error(result.error!)),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error previewing the next {Type} number on {Date}", documentType, date);
            return StatusCode(500, ApiResponse<NextNumberDto>.Error(
                "The next number could not be read; the bridge log has the details", new List<string>()));
        }
    }
}
