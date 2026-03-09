using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Intrastat declarations management endpoints
/// </summary>
[ApiController]
[Route("api/intrastat")]
[Authorize]
[Tags("Intrastat")]
public class IntrastatController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<IntrastatController> _logger;

    public IntrastatController(ISferaService sferaService, ILogger<IntrastatController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    #region Intrastat Declarations

    /// <summary>
    /// Get Intrastat declarations list
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<IntrastatDeclarationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<IntrastatDeclarationDto>>> GetIntrastatDeclarations(
        [FromQuery] int? year,
        [FromQuery] int? month,
        [FromQuery] string? direction,  // Arrival/Dispatch
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var (items, totalCount) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("DeklaracjeIntrastat");
                if (manager == null)
                {
                    return (null, -1);
                }

                var allDeklaracje = DynamicPropertyHelper.SafeGetAll((object)manager);

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

                // Filter by direction (Arrival = przywóz, Dispatch = wywóz)
                if (!string.IsNullOrEmpty(direction))
                {
                    bool isArrival = direction.Equals("arrival", StringComparison.OrdinalIgnoreCase) ||
                                    direction.Equals("przywoz", StringComparison.OrdinalIgnoreCase);
                    allDeklaracje = allDeklaracje.Where(d =>
                    {
                        var typ = DynamicPropertyHelper.GetNullableInt(d, "Typ");
                        // 0 = Arrival (Przywóz), 1 = Dispatch (Wywóz)
                        return isArrival ? typ == 0 : typ == 1;
                    }).ToList();
                }

                // Filter by status
                if (!string.IsNullOrEmpty(status))
                {
                    allDeklaracje = allDeklaracje.Where(d =>
                    {
                        var declStatus = MapIntrastatStatus(DynamicPropertyHelper.GetNullableInt(d, "Status"));
                        return declStatus.Equals(status, StringComparison.OrdinalIgnoreCase);
                    }).ToList();
                }

                var count = allDeklaracje.Count;
                var pagedDeklaracje = allDeklaracje
                    .OrderByDescending(d => DynamicPropertyHelper.GetNullableInt(d, "Rok") ?? 0)
                    .ThenByDescending(d => DynamicPropertyHelper.GetNullableInt(d, "Miesiac") ?? 0)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var result = new List<IntrastatDeclarationDto>();
                foreach (var d in pagedDeklaracje)
                {
                    result.Add(MapIntrastatDeclaration(d, false));
                }

                return (result, count);
            });

            if (totalCount == -1)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get DeklaracjeIntrastat manager"));
            }

            return Ok(new PagedResponse<IntrastatDeclarationDto>
            {
                Data = items!,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Intrastat declarations");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving Intrastat declarations", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get Intrastat declaration by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<IntrastatDeclarationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IntrastatDeclarationDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IntrastatDeclarationDto>>> GetIntrastatDeclaration(int id, [FromQuery] bool includeItems = true)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("DeklaracjeIntrastat");
                if (manager == null) return (IntrastatDeclarationDto?)null;

                var allDeklaracje = DynamicPropertyHelper.SafeGetAll((object)manager);
                var deklaracja = allDeklaracje.FirstOrDefault(d => DynamicPropertyHelper.GetId(d) == id);

                if (deklaracja == null) return (IntrastatDeclarationDto?)null;
                return (IntrastatDeclarationDto?)MapIntrastatDeclaration(deklaracja, includeItems);
            });

            if (result == null)
            {
                return NotFound(ApiResponse<IntrastatDeclarationDto>.Error($"Intrastat declaration with ID {id} not found"));
            }

            return Ok(ApiResponse<IntrastatDeclarationDto>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Intrastat declaration {Id}", id);
            return StatusCode(500, ApiResponse<IntrastatDeclarationDto>.Error("Error retrieving Intrastat declaration", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get Intrastat declaration items
    /// </summary>
    [HttpGet("{id}/items")]
    [ProducesResponseType(typeof(PagedResponse<IntrastatItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<IntrastatItemDto>>> GetIntrastatItems(
        int id,
        [FromQuery] string? countryCode,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("DeklaracjeIntrastat");
                if (manager == null) return (null, -1);

                var allDeklaracje = DynamicPropertyHelper.SafeGetAll((object)manager);
                var deklaracja = allDeklaracje.FirstOrDefault(d => DynamicPropertyHelper.GetId(d) == id);

                if (deklaracja == null) return (null, -2);

                var pozycje = DynamicPropertyHelper.GetCollection((object)deklaracja, "Pozycje");
                var items = new List<IntrastatItemDto>();

                foreach (var p in pozycje)
                {
                    var item = MapIntrastatItem(p);

                    // Filter by country
                    if (!string.IsNullOrEmpty(countryCode) &&
                        !string.Equals(item.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    items.Add(item);
                }

                var totalCount = items.Count;
                var pagedItems = items
                    .OrderBy(i => i.CnCode)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return (pagedItems, totalCount);
            });

            if (result.Item2 == -1)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get DeklaracjeIntrastat manager"));
            }

            if (result.Item2 == -2)
            {
                return NotFound(ApiResponse<object>.Error($"Intrastat declaration with ID {id} not found"));
            }

            return Ok(new PagedResponse<IntrastatItemDto>
            {
                Data = result.Item1!,
                Page = page,
                PageSize = pageSize,
                TotalCount = result.Item2
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Intrastat items for {Id}", id);
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving Intrastat items", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get Intrastat summary by country
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<IntrastatSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IntrastatSummaryDto>>> GetIntrastatSummary(
        [FromQuery] int year,
        [FromQuery] int? month,
        [FromQuery] string? direction)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("DeklaracjeIntrastat");
                if (manager == null) return (IntrastatSummaryDto?)null;

                var allDeklaracje = DynamicPropertyHelper.SafeGetAll((object)manager);

                // Filter by year
                allDeklaracje = allDeklaracje.Where(d =>
                {
                    var rok = DynamicPropertyHelper.GetNullableInt(d, "Rok");
                    return rok.HasValue && rok.Value == year;
                }).ToList();

                // Filter by month
                if (month.HasValue)
                {
                    allDeklaracje = allDeklaracje.Where(d =>
                    {
                        var miesiac = DynamicPropertyHelper.GetNullableInt(d, "Miesiac");
                        return miesiac.HasValue && miesiac.Value == month.Value;
                    }).ToList();
                }

                // Filter by direction
                if (!string.IsNullOrEmpty(direction))
                {
                    bool isArrival = direction.Equals("arrival", StringComparison.OrdinalIgnoreCase);
                    allDeklaracje = allDeklaracje.Where(d =>
                    {
                        var typ = DynamicPropertyHelper.GetNullableInt(d, "Typ");
                        return isArrival ? typ == 0 : typ == 1;
                    }).ToList();
                }

                decimal totalArrivalValue = 0;
                decimal totalDispatchValue = 0;
                decimal totalArrivalWeight = 0;
                decimal totalDispatchWeight = 0;
                var countryBreakdown = new Dictionary<string, IntrastatCountrySummaryDto>();

                foreach (var d in allDeklaracje)
                {
                    var typ = DynamicPropertyHelper.GetNullableInt(d, "Typ");
                    bool isArrival = typ == 0;

                    var pozycje = DynamicPropertyHelper.GetCollection((object)d, "Pozycje");
                    foreach (var p in pozycje)
                    {
                        var wartosc = DynamicPropertyHelper.GetDecimal(p, "WartoscStatystyczna");
                        var masa = DynamicPropertyHelper.GetDecimal(p, "MasaNetto");
                        var kodKraju = DynamicPropertyHelper.GetString(p, "KodKraju") ?? "XX";

                        if (isArrival)
                        {
                            totalArrivalValue += wartosc;
                            totalArrivalWeight += masa;
                        }
                        else
                        {
                            totalDispatchValue += wartosc;
                            totalDispatchWeight += masa;
                        }

                        if (!countryBreakdown.ContainsKey(kodKraju))
                        {
                            countryBreakdown[kodKraju] = new IntrastatCountrySummaryDto { CountryCode = kodKraju };
                        }

                        if (isArrival)
                        {
                            countryBreakdown[kodKraju].ArrivalValue += wartosc;
                            countryBreakdown[kodKraju].ArrivalWeight += masa;
                        }
                        else
                        {
                            countryBreakdown[kodKraju].DispatchValue += wartosc;
                            countryBreakdown[kodKraju].DispatchWeight += masa;
                        }
                    }
                }

                return (IntrastatSummaryDto?)new IntrastatSummaryDto
                {
                    Year = year,
                    Month = month,
                    DeclarationCount = allDeklaracje.Count,
                    TotalArrivalValue = totalArrivalValue,
                    TotalDispatchValue = totalDispatchValue,
                    TotalArrivalWeight = totalArrivalWeight,
                    TotalDispatchWeight = totalDispatchWeight,
                    CountryBreakdown = countryBreakdown.Values
                        .OrderByDescending(c => c.ArrivalValue + c.DispatchValue)
                        .Take(20)
                        .ToList()
                };
            });

            if (result == null)
            {
                return StatusCode(500, ApiResponse<IntrastatSummaryDto>.Error("Failed to get DeklaracjeIntrastat manager"));
            }

            return Ok(ApiResponse<IntrastatSummaryDto>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Intrastat summary for year {Year}", year);
            return StatusCode(500, ApiResponse<IntrastatSummaryDto>.Error("Error retrieving Intrastat summary", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Helpers

    private static IntrastatDeclarationDto MapIntrastatDeclaration(dynamic d, bool includeItems)
    {
        var numerWewnetrzny = DynamicPropertyHelper.GetProperty(d, "NumerWewnetrzny");
        var pozycje = DynamicPropertyHelper.GetCollection((object)d, "Pozycje");

        var dto = new IntrastatDeclarationDto
        {
            Id = DynamicPropertyHelper.GetId(d),
            Number = numerWewnetrzny != null ? DynamicPropertyHelper.GetString(numerWewnetrzny, "PelnaSygnatura") : null,
            Year = DynamicPropertyHelper.GetNullableInt(d, "Rok") ?? 0,
            Month = DynamicPropertyHelper.GetNullableInt(d, "Miesiac") ?? 0,
            Direction = DynamicPropertyHelper.GetNullableInt(d, "Typ") == 0 ? "Arrival" : "Dispatch",
            Status = MapIntrastatStatus(DynamicPropertyHelper.GetNullableInt(d, "Status")),
            CreatedDate = DynamicPropertyHelper.GetDateTime(d, "DataUtworzenia"),
            SubmittedDate = DynamicPropertyHelper.GetDateTime(d, "DataWyslania"),
            TotalValue = DynamicPropertyHelper.GetDecimal(d, "WartoscCalkowita"),
            TotalWeight = DynamicPropertyHelper.GetDecimal(d, "MasaCalkowita"),
            ItemCount = pozycje.Count(),
            ReferenceNumber = DynamicPropertyHelper.GetString(d, "NumerReferencyjny"),
            Description = DynamicPropertyHelper.GetString(d, "Opis")
        };

        if (includeItems)
        {
            dto.Items = new List<IntrastatItemDto>();
            foreach (var p in pozycje)
            {
                dto.Items.Add(MapIntrastatItem(p));
            }
        }

        return dto;
    }

    private static IntrastatItemDto MapIntrastatItem(dynamic p)
    {
        return new IntrastatItemDto
        {
            Id = DynamicPropertyHelper.GetId(p),
            CnCode = DynamicPropertyHelper.GetString(p, "KodCN"),
            Description = DynamicPropertyHelper.GetString(p, "OpisTowarow"),
            CountryCode = DynamicPropertyHelper.GetString(p, "KodKraju"),
            CountryName = DynamicPropertyHelper.GetString(p, "NazwaKraju"),
            NetMass = DynamicPropertyHelper.GetDecimal(p, "MasaNetto"),
            SupplementaryUnit = DynamicPropertyHelper.GetNullableDecimal(p, "JednostkaDodatkowa"),
            StatisticalValue = DynamicPropertyHelper.GetDecimal(p, "WartoscStatystyczna"),
            InvoiceValue = DynamicPropertyHelper.GetNullableDecimal(p, "WartoscFakturowa"),
            TransactionType = DynamicPropertyHelper.GetString(p, "RodzajTransakcji"),
            DeliveryTerms = DynamicPropertyHelper.GetString(p, "WarunkiDostawy"),
            TransportMode = DynamicPropertyHelper.GetString(p, "RodzajTransportu")
        };
    }

    private static string MapIntrastatStatus(int? status)
    {
        return status switch
        {
            0 => "Draft",           // Roboczy
            1 => "Ready",           // Gotowy
            2 => "Submitted",       // Wysłany
            3 => "Accepted",        // Zaakceptowany
            4 => "Rejected",        // Odrzucony
            5 => "Corrected",       // Skorygowany
            _ => "Unknown"
        };
    }

    #endregion
}

#region DTOs

/// <summary>
/// Intrastat declaration DTO
/// </summary>
public class IntrastatDeclarationDto
{
    public int Id { get; set; }
    public string? Number { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string Direction { get; set; } = "Arrival";  // Arrival (Przywóz) / Dispatch (Wywóz)
    public string Status { get; set; } = "Draft";
    public DateTime? CreatedDate { get; set; }
    public DateTime? SubmittedDate { get; set; }
    public decimal TotalValue { get; set; }
    public decimal TotalWeight { get; set; }
    public int ItemCount { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Description { get; set; }
    public List<IntrastatItemDto>? Items { get; set; }
}

/// <summary>
/// Intrastat item DTO
/// </summary>
public class IntrastatItemDto
{
    public int Id { get; set; }
    public string? CnCode { get; set; }          // Kod CN (Combined Nomenclature)
    public string? Description { get; set; }     // Opis towarów
    public string? CountryCode { get; set; }     // Kod kraju (ISO)
    public string? CountryName { get; set; }     // Nazwa kraju
    public decimal NetMass { get; set; }         // Masa netto (kg)
    public decimal? SupplementaryUnit { get; set; } // Jednostka dodatkowa
    public decimal StatisticalValue { get; set; } // Wartość statystyczna (PLN)
    public decimal? InvoiceValue { get; set; }   // Wartość fakturowa
    public string? TransactionType { get; set; } // Rodzaj transakcji
    public string? DeliveryTerms { get; set; }   // Warunki dostawy (Incoterms)
    public string? TransportMode { get; set; }   // Rodzaj transportu
}

/// <summary>
/// Intrastat summary DTO
/// </summary>
public class IntrastatSummaryDto
{
    public int Year { get; set; }
    public int? Month { get; set; }
    public int DeclarationCount { get; set; }
    public decimal TotalArrivalValue { get; set; }
    public decimal TotalDispatchValue { get; set; }
    public decimal TotalArrivalWeight { get; set; }
    public decimal TotalDispatchWeight { get; set; }
    public List<IntrastatCountrySummaryDto> CountryBreakdown { get; set; } = new();
}

/// <summary>
/// Intrastat country summary DTO
/// </summary>
public class IntrastatCountrySummaryDto
{
    public string CountryCode { get; set; } = string.Empty;
    public decimal ArrivalValue { get; set; }
    public decimal DispatchValue { get; set; }
    public decimal ArrivalWeight { get; set; }
    public decimal DispatchWeight { get; set; }
}

#endregion
