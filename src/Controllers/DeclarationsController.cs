using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Tax declarations (VAT) management endpoints
/// </summary>
[ApiController]
[Route("api/declarations")]
[Authorize]
[Tags("Declarations")]
public class DeclarationsController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<DeclarationsController> _logger;

    public DeclarationsController(ISferaService sferaService, ILogger<DeclarationsController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    #region VAT Declarations

    /// <summary>
    /// Get VAT declarations list
    /// </summary>
    [HttpGet("vat")]
    [ProducesResponseType(typeof(PagedResponse<VatDeclarationDto>), StatusCodes.Status200OK)]
    public ActionResult<PagedResponse<VatDeclarationDto>> GetVatDeclarations(
        [FromQuery] int? year,
        [FromQuery] int? month,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var manager = _sferaService.GetManager("Deklaracje");
            if (manager == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Deklaracje manager"));
            }

            var allDeklaracje = ((IEnumerable<dynamic>)manager.Dane.Wszystkie()).ToList();

            // Filter VAT declarations only (type = VAT)
            allDeklaracje = allDeklaracje.Where(d =>
            {
                var typ = DynamicPropertyHelper.GetString(d, "TypDeklaracji");
                return typ != null && (typ.Contains("VAT") || typ.Contains("V7") || typ.Contains("JPK_V7"));
            }).ToList();

            // Filter by year
            if (year.HasValue)
            {
                allDeklaracje = allDeklaracje.Where(d =>
                {
                    var rok = DynamicPropertyHelper.GetNullableInt(d, "Rok");
                    return rok.HasValue && rok.Value == year.Value;
                }).ToList();
            }

            // Filter by month
            if (month.HasValue)
            {
                allDeklaracje = allDeklaracje.Where(d =>
                {
                    var miesiac = DynamicPropertyHelper.GetNullableInt(d, "Miesiac");
                    return miesiac.HasValue && miesiac.Value == month.Value;
                }).ToList();
            }

            // Filter by status
            if (!string.IsNullOrEmpty(status))
            {
                allDeklaracje = allDeklaracje.Where(d =>
                {
                    var declStatus = MapDeclarationStatus(DynamicPropertyHelper.GetNullableInt(d, "Status"));
                    return declStatus.Equals(status, StringComparison.OrdinalIgnoreCase);
                }).ToList();
            }

            var totalCount = allDeklaracje.Count;
            var pagedDeklaracje = allDeklaracje
                .OrderByDescending(d => DynamicPropertyHelper.GetNullableInt(d, "Rok") ?? 0)
                .ThenByDescending(d => DynamicPropertyHelper.GetNullableInt(d, "Miesiac") ?? 0)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var items = new List<VatDeclarationDto>();
            foreach (var d in pagedDeklaracje)
            {
                items.Add(MapVatDeclaration(d));
            }

            return Ok(new PagedResponse<VatDeclarationDto>
            {
                Data = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting VAT declarations");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving VAT declarations", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get VAT declaration by ID
    /// </summary>
    [HttpGet("vat/{id}")]
    [ProducesResponseType(typeof(ApiResponse<VatDeclarationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<VatDeclarationDto>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<VatDeclarationDto>> GetVatDeclaration(int id)
    {
        try
        {
            var manager = _sferaService.GetManager("Deklaracje");
            if (manager == null)
            {
                return StatusCode(500, ApiResponse<VatDeclarationDto>.Error("Failed to get Deklaracje manager"));
            }

            var allDeklaracje = ((IEnumerable<dynamic>)manager.Dane.Wszystkie()).ToList();
            var deklaracja = allDeklaracje.FirstOrDefault(d => DynamicPropertyHelper.GetId(d) == id);

            if (deklaracja == null)
            {
                return NotFound(ApiResponse<VatDeclarationDto>.Error($"VAT declaration with ID {id} not found"));
            }

            return Ok(ApiResponse<VatDeclarationDto>.Ok(MapVatDeclaration(deklaracja)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting VAT declaration {Id}", id);
            return StatusCode(500, ApiResponse<VatDeclarationDto>.Error("Error retrieving VAT declaration", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get VAT declaration summary (totals by period)
    /// </summary>
    [HttpGet("vat/summary")]
    [ProducesResponseType(typeof(ApiResponse<VatDeclarationSummaryDto>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<VatDeclarationSummaryDto>> GetVatDeclarationSummary([FromQuery] int year)
    {
        try
        {
            var manager = _sferaService.GetManager("Deklaracje");
            if (manager == null)
            {
                return StatusCode(500, ApiResponse<VatDeclarationSummaryDto>.Error("Failed to get Deklaracje manager"));
            }

            var allDeklaracje = ((IEnumerable<dynamic>)manager.Dane.Wszystkie()).ToList();

            // Filter VAT declarations for the specified year
            var yearDeklaracje = allDeklaracje.Where(d =>
            {
                var typ = DynamicPropertyHelper.GetString(d, "TypDeklaracji");
                var rok = DynamicPropertyHelper.GetNullableInt(d, "Rok");
                return rok.HasValue && rok.Value == year &&
                       typ != null && (typ.Contains("VAT") || typ.Contains("V7") || typ.Contains("JPK_V7"));
            }).ToList();

            var summary = new VatDeclarationSummaryDto
            {
                Year = year,
                TotalDeclarations = yearDeklaracje.Count,
                SubmittedCount = yearDeklaracje.Count(d =>
                    MapDeclarationStatus(DynamicPropertyHelper.GetNullableInt(d, "Status")) == "Submitted"),
                DraftCount = yearDeklaracje.Count(d =>
                    MapDeclarationStatus(DynamicPropertyHelper.GetNullableInt(d, "Status")) == "Draft"),
                TotalVatDue = 0,
                TotalVatRefund = 0
            };

            foreach (var d in yearDeklaracje)
            {
                var vatNalezny = DynamicPropertyHelper.GetDecimal(d, "VATNalezny");
                var vatNaliczony = DynamicPropertyHelper.GetDecimal(d, "VATNaliczony");
                var roznica = vatNalezny - vatNaliczony;

                if (roznica > 0)
                    summary.TotalVatDue += roznica;
                else
                    summary.TotalVatRefund += Math.Abs(roznica);
            }

            return Ok(ApiResponse<VatDeclarationSummaryDto>.Ok(summary));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting VAT declaration summary for year {Year}", year);
            return StatusCode(500, ApiResponse<VatDeclarationSummaryDto>.Error("Error retrieving VAT declaration summary", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get available declaration types
    /// </summary>
    [HttpGet("types")]
    [ProducesResponseType(typeof(ApiResponse<List<DeclarationTypeDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<DeclarationTypeDto>>> GetDeclarationTypes()
    {
        var types = new List<DeclarationTypeDto>
        {
            new() { Code = "JPK_V7M", Name = "JPK_V7M", Description = "Miesięczna deklaracja VAT z JPK (od 10/2020)", Period = "Monthly" },
            new() { Code = "JPK_V7K", Name = "JPK_V7K", Description = "Kwartalna deklaracja VAT z JPK (od 10/2020)", Period = "Quarterly" },
            new() { Code = "VAT-7", Name = "VAT-7", Description = "Miesięczna deklaracja VAT (do 09/2020)", Period = "Monthly" },
            new() { Code = "VAT-7K", Name = "VAT-7K", Description = "Kwartalna deklaracja VAT (do 09/2020)", Period = "Quarterly" },
            new() { Code = "VAT-7D", Name = "VAT-7D", Description = "Deklaracja VAT dla małych podatników", Period = "Quarterly" },
            new() { Code = "VAT-UE", Name = "VAT-UE", Description = "Informacja podsumowująca VAT-UE", Period = "Monthly" },
            new() { Code = "VAT-27", Name = "VAT-27", Description = "Informacja o WNT", Period = "Monthly" }
        };

        return Ok(ApiResponse<List<DeclarationTypeDto>>.Ok(types));
    }

    private static VatDeclarationDto MapVatDeclaration(dynamic d)
    {
        var numerWewnetrzny = DynamicPropertyHelper.GetProperty(d, "NumerWewnetrzny");

        return new VatDeclarationDto
        {
            Id = DynamicPropertyHelper.GetId(d),
            Number = numerWewnetrzny != null ? DynamicPropertyHelper.GetString(numerWewnetrzny, "PelnaSygnatura") : null,
            Type = DynamicPropertyHelper.GetString(d, "TypDeklaracji"),
            Year = DynamicPropertyHelper.GetNullableInt(d, "Rok") ?? 0,
            Month = DynamicPropertyHelper.GetNullableInt(d, "Miesiac"),
            Quarter = DynamicPropertyHelper.GetNullableInt(d, "Kwartal"),
            Status = MapDeclarationStatus(DynamicPropertyHelper.GetNullableInt(d, "Status")),
            CreatedDate = DynamicPropertyHelper.GetDateTime(d, "DataUtworzenia"),
            SubmittedDate = DynamicPropertyHelper.GetDateTime(d, "DataWyslania"),
            VatOutput = DynamicPropertyHelper.GetDecimal(d, "VATNalezny"),         // VAT należny (sprzedaż)
            VatInput = DynamicPropertyHelper.GetDecimal(d, "VATNaliczony"),        // VAT naliczony (zakupy)
            VatDue = DynamicPropertyHelper.GetDecimal(d, "VATDoZaplaty"),          // VAT do zapłaty
            VatRefund = DynamicPropertyHelper.GetDecimal(d, "VATDoZwrotu"),        // VAT do zwrotu
            TaxableBase = DynamicPropertyHelper.GetDecimal(d, "PodstawaOpodatkowania"),
            UpoNumber = DynamicPropertyHelper.GetString(d, "NumerUPO"),
            Description = DynamicPropertyHelper.GetString(d, "Opis")
        };
    }

    private static string MapDeclarationStatus(int? status)
    {
        return status switch
        {
            0 => "Draft",           // Bufor/Roboczy
            1 => "Ready",           // Gotowy do wysłania
            2 => "Submitted",       // Wysłany
            3 => "Accepted",        // Zaakceptowany przez US
            4 => "Rejected",        // Odrzucony
            5 => "Corrected",       // Skorygowany
            _ => "Unknown"
        };
    }

    #endregion
}

#region DTOs

/// <summary>
/// VAT declaration DTO
/// </summary>
public class VatDeclarationDto
{
    public int Id { get; set; }
    public string? Number { get; set; }
    public string? Type { get; set; }
    public int Year { get; set; }
    public int? Month { get; set; }
    public int? Quarter { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTime? CreatedDate { get; set; }
    public DateTime? SubmittedDate { get; set; }
    public decimal VatOutput { get; set; }      // VAT należny
    public decimal VatInput { get; set; }       // VAT naliczony
    public decimal VatDue { get; set; }         // Do zapłaty
    public decimal VatRefund { get; set; }      // Do zwrotu
    public decimal TaxableBase { get; set; }    // Podstawa opodatkowania
    public string? UpoNumber { get; set; }      // Numer UPO
    public string? Description { get; set; }
}

/// <summary>
/// VAT declaration summary DTO
/// </summary>
public class VatDeclarationSummaryDto
{
    public int Year { get; set; }
    public int TotalDeclarations { get; set; }
    public int SubmittedCount { get; set; }
    public int DraftCount { get; set; }
    public decimal TotalVatDue { get; set; }
    public decimal TotalVatRefund { get; set; }
}

/// <summary>
/// Declaration type DTO
/// </summary>
public class DeclarationTypeDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Period { get; set; } = "Monthly";
}

#endregion
