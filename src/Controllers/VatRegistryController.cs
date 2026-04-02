using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// VAT Registry (Ewidencja VAT) management endpoints for sales and purchase records
/// </summary>
[ApiController]
[Route("api/vat-registry")]
[Authorize]
[Tags("VAT Registry")]
public class VatRegistryController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<VatRegistryController> _logger;

    public VatRegistryController(ISferaService sferaService, ILogger<VatRegistryController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    #region Sales

    /// <summary>
    /// Get VAT sales registry entries
    /// </summary>
    [HttpGet("sales")]
    [ProducesResponseType(typeof(PagedResponse<VatRegistryEntryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<VatRegistryEntryDto>>> GetSalesEntries(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int? year,
        [FromQuery] int? month,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var (items, totalCount) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("ZapisyWEwidencjiVATSprzedazy");
                if (manager == null) return (null, -1);

                var allEntries = DynamicPropertyHelper.SafeGetAll((object)manager);

                // Filter by date range
                if (dateFrom.HasValue)
                {
                    allEntries = allEntries.Where(e =>
                    {
                        var data = DynamicPropertyHelper.GetDateTime(e, "DataSprzedazy")
                                ?? DynamicPropertyHelper.GetDateTime(e, "DataWystawienia");
                        return data.HasValue && data.Value >= dateFrom.Value;
                    }).ToList();
                }

                if (dateTo.HasValue)
                {
                    allEntries = allEntries.Where(e =>
                    {
                        var data = DynamicPropertyHelper.GetDateTime(e, "DataSprzedazy")
                                ?? DynamicPropertyHelper.GetDateTime(e, "DataWystawienia");
                        return data.HasValue && data.Value <= dateTo.Value;
                    }).ToList();
                }

                // Filter by year
                if (year.HasValue)
                {
                    allEntries = allEntries.Where(e =>
                    {
                        var data = DynamicPropertyHelper.GetDateTime(e, "DataSprzedazy")
                                ?? DynamicPropertyHelper.GetDateTime(e, "DataWystawienia");
                        return data.HasValue && data.Value.Year == year.Value;
                    }).ToList();
                }

                // Filter by month
                if (month.HasValue)
                {
                    allEntries = allEntries.Where(e =>
                    {
                        var data = DynamicPropertyHelper.GetDateTime(e, "DataSprzedazy")
                                ?? DynamicPropertyHelper.GetDateTime(e, "DataWystawienia");
                        return data.HasValue && data.Value.Month == month.Value;
                    }).ToList();
                }

                var count = allEntries.Count;
                var pagedEntries = allEntries
                    .OrderByDescending(e => DynamicPropertyHelper.GetDateTime(e, "DataSprzedazy") ?? DateTime.MinValue)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var result = new List<VatRegistryEntryDto>();
                foreach (var e in pagedEntries)
                {
                    result.Add(MapVatRegistryEntry(e, "Sales"));
                }

                return (result, count);
            });

            if (totalCount == -1)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get ZapisyWEwidencjiVATSprzedazy manager"));

            return Ok(new PagedResponse<VatRegistryEntryDto>
            {
                Data = items!,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting VAT sales registry entries");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving VAT sales registry entries", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Purchase

    /// <summary>
    /// Get VAT purchase registry entries
    /// </summary>
    [HttpGet("purchase")]
    [ProducesResponseType(typeof(PagedResponse<VatRegistryEntryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<VatRegistryEntryDto>>> GetPurchaseEntries(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int? year,
        [FromQuery] int? month,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var (items, totalCount) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("ZapisyWEwidencjiVATZakupu");
                if (manager == null) return (null, -1);

                var allEntries = DynamicPropertyHelper.SafeGetAll((object)manager);

                // Filter by date range
                if (dateFrom.HasValue)
                {
                    allEntries = allEntries.Where(e =>
                    {
                        var data = DynamicPropertyHelper.GetDateTime(e, "DataWystawienia");
                        return data.HasValue && data.Value >= dateFrom.Value;
                    }).ToList();
                }

                if (dateTo.HasValue)
                {
                    allEntries = allEntries.Where(e =>
                    {
                        var data = DynamicPropertyHelper.GetDateTime(e, "DataWystawienia");
                        return data.HasValue && data.Value <= dateTo.Value;
                    }).ToList();
                }

                // Filter by year
                if (year.HasValue)
                {
                    allEntries = allEntries.Where(e =>
                    {
                        var data = DynamicPropertyHelper.GetDateTime(e, "DataWystawienia");
                        return data.HasValue && data.Value.Year == year.Value;
                    }).ToList();
                }

                // Filter by month
                if (month.HasValue)
                {
                    allEntries = allEntries.Where(e =>
                    {
                        var data = DynamicPropertyHelper.GetDateTime(e, "DataWystawienia");
                        return data.HasValue && data.Value.Month == month.Value;
                    }).ToList();
                }

                var count = allEntries.Count;
                var pagedEntries = allEntries
                    .OrderByDescending(e => DynamicPropertyHelper.GetDateTime(e, "DataWystawienia") ?? DateTime.MinValue)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var result = new List<VatRegistryEntryDto>();
                foreach (var e in pagedEntries)
                {
                    result.Add(MapVatRegistryEntry(e, "Purchase"));
                }

                return (result, count);
            });

            if (totalCount == -1)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get ZapisyWEwidencjiVATZakupu manager"));

            return Ok(new PagedResponse<VatRegistryEntryDto>
            {
                Data = items!,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting VAT purchase registry entries");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving VAT purchase registry entries", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Get By ID

    /// <summary>
    /// Get VAT registry entry by ID (searches both sales and purchase registries)
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<VatRegistryEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<VatRegistryEntryDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<VatRegistryEntryDto>>> GetVatRegistryEntry(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                // Try sales registry first
                var salesManager = _sferaService.GetManager("ZapisyWEwidencjiVATSprzedazy");
                if (salesManager != null)
                {
                    var salesEntries = DynamicPropertyHelper.SafeGetAll((object)salesManager);
                    var salesEntry = salesEntries.FirstOrDefault(e => DynamicPropertyHelper.GetId(e) == id);
                    if (salesEntry != null)
                        return (VatRegistryEntryDto?)MapVatRegistryEntry(salesEntry, "Sales");
                }

                // Try purchase registry
                var purchaseManager = _sferaService.GetManager("ZapisyWEwidencjiVATZakupu");
                if (purchaseManager != null)
                {
                    var purchaseEntries = DynamicPropertyHelper.SafeGetAll((object)purchaseManager);
                    var purchaseEntry = purchaseEntries.FirstOrDefault(e => DynamicPropertyHelper.GetId(e) == id);
                    if (purchaseEntry != null)
                        return (VatRegistryEntryDto?)MapVatRegistryEntry(purchaseEntry, "Purchase");
                }

                return (VatRegistryEntryDto?)null;
            });

            if (result == null)
                return NotFound(ApiResponse<VatRegistryEntryDto>.Error($"VAT registry entry with ID {id} not found"));

            return Ok(ApiResponse<VatRegistryEntryDto>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting VAT registry entry {Id}", id);
            return StatusCode(500, ApiResponse<VatRegistryEntryDto>.Error("Error retrieving VAT registry entry", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Helpers

    private static VatRegistryEntryDto MapVatRegistryEntry(dynamic e, string entryType)
    {
        return new VatRegistryEntryDto
        {
            Id = DynamicPropertyHelper.GetId(e),
            EntryType = entryType,
            DocumentNumber = DynamicPropertyHelper.GetString(e, "NumerDokumentu"),
            CustomerName = DynamicPropertyHelper.GetString(e, "NazwaKontrahenta"),
            CustomerNIP = DynamicPropertyHelper.GetString(e, "NIPKontrahenta"),
            IssueDate = DynamicPropertyHelper.GetDateTime(e, "DataWystawienia"),
            SaleDate = DynamicPropertyHelper.GetDateTime(e, "DataSprzedazy"),
            NetAmount = DynamicPropertyHelper.GetDecimal(e, "KwotaNetto"),
            VatAmount = DynamicPropertyHelper.GetDecimal(e, "KwotaVat"),
            GrossAmount = DynamicPropertyHelper.GetDecimal(e, "KwotaBrutto"),
            VatRate = DynamicPropertyHelper.GetString(e, "StawkaVat"),
            TransactionType = DynamicPropertyHelper.GetString(e, "TypTransakcji"),
            Description = DynamicPropertyHelper.GetString(e, "Opis")
        };
    }

    #endregion
}

#region DTOs

/// <summary>
/// VAT registry entry DTO
/// </summary>
public class VatRegistryEntryDto
{
    public int Id { get; set; }
    /// <summary>
    /// Entry type: "Sales" or "Purchase"
    /// </summary>
    public string EntryType { get; set; } = string.Empty;
    /// <summary>
    /// Document number (NumerDokumentu)
    /// </summary>
    public string? DocumentNumber { get; set; }
    /// <summary>
    /// Customer name (NazwaKontrahenta)
    /// </summary>
    public string? CustomerName { get; set; }
    /// <summary>
    /// Customer NIP (NIPKontrahenta)
    /// </summary>
    public string? CustomerNIP { get; set; }
    /// <summary>
    /// Issue date (DataWystawienia)
    /// </summary>
    public DateTime? IssueDate { get; set; }
    /// <summary>
    /// Sale date (DataSprzedazy)
    /// </summary>
    public DateTime? SaleDate { get; set; }
    /// <summary>
    /// Net amount (KwotaNetto)
    /// </summary>
    public decimal NetAmount { get; set; }
    /// <summary>
    /// VAT amount (KwotaVat)
    /// </summary>
    public decimal VatAmount { get; set; }
    /// <summary>
    /// Gross amount (KwotaBrutto)
    /// </summary>
    public decimal GrossAmount { get; set; }
    /// <summary>
    /// VAT rate (StawkaVat)
    /// </summary>
    public string? VatRate { get; set; }
    /// <summary>
    /// Transaction type (TypTransakcji)
    /// </summary>
    public string? TransactionType { get; set; }
    /// <summary>
    /// Description (Opis)
    /// </summary>
    public string? Description { get; set; }
}

#endregion
