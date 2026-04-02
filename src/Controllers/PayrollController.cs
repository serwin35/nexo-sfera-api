using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Payroll (Listy Plac / Rachunki do Umow) management endpoints
/// </summary>
[ApiController]
[Route("api/hr/payroll")]
[Authorize]
[Tags("Payroll")]
public class PayrollController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<PayrollController> _logger;

    public PayrollController(ISferaService sferaService, ILogger<PayrollController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get payroll runs (listy plac) list
    /// </summary>
    [HttpGet("payroll-lists")]
    [ProducesResponseType(typeof(PagedResponse<PayrollListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<PayrollListDto>>> GetPayrollLists(
        [FromQuery] int? year = null,
        [FromQuery] int? month = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var (items, totalCount) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("ListyPlac");
                if (manager == null) return (null, -1);

                var allLists = DynamicPropertyHelper.SafeGetAll((object)manager);

                if (year.HasValue)
                {
                    allLists = allLists.Where(l =>
                        DynamicPropertyHelper.GetNullableInt(l, "Rok") == year.Value).ToList();
                }

                if (month.HasValue)
                {
                    allLists = allLists.Where(l =>
                        DynamicPropertyHelper.GetNullableInt(l, "Miesiac") == month.Value).ToList();
                }

                if (!string.IsNullOrEmpty(status))
                {
                    var statusLower = status.ToLower();
                    allLists = allLists.Where(l =>
                    {
                        var s = DynamicPropertyHelper.GetString(l, "Status") ?? "";
                        return s.ToLower().Contains(statusLower);
                    }).ToList();
                }

                var count = allLists.Count;

                var pagedLists = allLists
                    .OrderByDescending(l => DynamicPropertyHelper.GetNullableInt(l, "Rok") ?? 0)
                    .ThenByDescending(l => DynamicPropertyHelper.GetNullableInt(l, "Miesiac") ?? 0)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var result = new List<PayrollListDto>();
                foreach (var l in pagedLists)
                {
                    result.Add(MapPayrollList(l));
                }

                return (result, count);
            });

            if (totalCount == -1)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get ListyPlac manager"));

            return Ok(new PagedResponse<PayrollListDto>
            {
                Data = items!,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting payroll lists");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving payroll lists", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get payroll run by ID
    /// </summary>
    [HttpGet("payroll-lists/{id}")]
    [ProducesResponseType(typeof(ApiResponse<PayrollListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PayrollListDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PayrollListDto>>> GetPayrollList(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("ListyPlac");
                if (manager == null) return (PayrollListDto?)null;

                var item = DynamicPropertyHelper.FindById((object)manager, id);
                if (item == null) return (PayrollListDto?)null;

                return (PayrollListDto?)MapPayrollList(item);
            });

            if (result == null)
                return NotFound(ApiResponse<PayrollListDto>.Error($"Payroll list with ID {id} not found"));

            return Ok(ApiResponse<PayrollListDto>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting payroll list {Id}", id);
            return StatusCode(500, ApiResponse<PayrollListDto>.Error("Error retrieving payroll list", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get pay slips / invoices for contracts (rachunki do umow)
    /// </summary>
    [HttpGet("pay-slips")]
    [ProducesResponseType(typeof(PagedResponse<PaySlipDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<PaySlipDto>>> GetPaySlips(
        [FromQuery] int? employeeId = null,
        [FromQuery] int? year = null,
        [FromQuery] int? month = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var (items, totalCount) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("RachunkiDoUmow");
                if (manager == null) return (null, -1);

                var allSlips = DynamicPropertyHelper.SafeGetAll((object)manager);

                if (employeeId.HasValue)
                {
                    allSlips = allSlips.Where(s =>
                    {
                        var pracownik = DynamicPropertyHelper.GetProperty(s, "Pracownik");
                        return pracownik != null && DynamicPropertyHelper.GetId(pracownik) == employeeId.Value;
                    }).ToList();
                }

                if (year.HasValue)
                {
                    allSlips = allSlips.Where(s =>
                    {
                        var okres = DynamicPropertyHelper.GetString(s, "Okres") ?? "";
                        return okres.Contains(year.Value.ToString());
                    }).ToList();
                }

                if (month.HasValue)
                {
                    allSlips = allSlips.Where(s =>
                    {
                        var okres = DynamicPropertyHelper.GetString(s, "Okres") ?? "";
                        var monthStr = month.Value.ToString("D2");
                        return okres.Contains(monthStr);
                    }).ToList();
                }

                var count = allSlips.Count;

                var pagedSlips = allSlips
                    .OrderByDescending(s => DynamicPropertyHelper.GetDateTime(s, "DataWyplaty") ?? DateTime.MinValue)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var result = new List<PaySlipDto>();
                foreach (var s in pagedSlips)
                {
                    result.Add(MapPaySlip(s));
                }

                return (result, count);
            });

            if (totalCount == -1)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get RachunkiDoUmow manager"));

            return Ok(new PagedResponse<PaySlipDto>
            {
                Data = items!,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pay slips");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving pay slips", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get pay slip by ID
    /// </summary>
    [HttpGet("pay-slips/{id}")]
    [ProducesResponseType(typeof(ApiResponse<PaySlipDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PaySlipDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PaySlipDto>>> GetPaySlip(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("RachunkiDoUmow");
                if (manager == null) return (PaySlipDto?)null;

                var item = DynamicPropertyHelper.FindById((object)manager, id);
                if (item == null) return (PaySlipDto?)null;

                return (PaySlipDto?)MapPaySlip(item);
            });

            if (result == null)
                return NotFound(ApiResponse<PaySlipDto>.Error($"Pay slip with ID {id} not found"));

            return Ok(ApiResponse<PaySlipDto>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pay slip {Id}", id);
            return StatusCode(500, ApiResponse<PaySlipDto>.Error("Error retrieving pay slip", new List<string> { ex.Message }));
        }
    }

    #region Mapping

    private static PayrollListDto MapPayrollList(dynamic l)
    {
        return new PayrollListDto
        {
            Id = DynamicPropertyHelper.GetId(l),
            Number = DynamicPropertyHelper.GetString(l, "Numer"),
            Period = DynamicPropertyHelper.GetString(l, "Okres"),
            Year = DynamicPropertyHelper.GetNullableInt(l, "Rok"),
            Month = DynamicPropertyHelper.GetNullableInt(l, "Miesiac"),
            Status = DynamicPropertyHelper.GetString(l, "Status"),
            TotalGross = DynamicPropertyHelper.GetNullableDecimal(l, "SumaBrutto"),
            TotalNet = DynamicPropertyHelper.GetNullableDecimal(l, "SumaNetto"),
            TotalTax = DynamicPropertyHelper.GetNullableDecimal(l, "SumaPodatku"),
            TotalZUS = DynamicPropertyHelper.GetNullableDecimal(l, "SumaZUS"),
            EmployeeCount = DynamicPropertyHelper.GetNullableInt(l, "LiczbaPracownikow"),
            CreatedDate = DynamicPropertyHelper.GetDateTime(l, "DataUtworzenia"),
            ApprovedDate = DynamicPropertyHelper.GetDateTime(l, "DataZatwierdzenia")
        };
    }

    private static PaySlipDto MapPaySlip(dynamic s)
    {
        var pracownik = DynamicPropertyHelper.GetProperty(s, "Pracownik");
        var osoba = pracownik != null ? DynamicPropertyHelper.GetProperty(pracownik, "Osoba") : null;

        var imie = osoba != null ? DynamicPropertyHelper.GetString(osoba, "Imie") ?? "" : "";
        var nazwisko = osoba != null ? DynamicPropertyHelper.GetString(osoba, "Nazwisko") ?? "" : "";

        return new PaySlipDto
        {
            Id = DynamicPropertyHelper.GetId(s),
            EmployeeId = pracownik != null ? DynamicPropertyHelper.GetId(pracownik) : 0,
            EmployeeName = $"{imie} {nazwisko}".Trim(),
            ContractNumber = DynamicPropertyHelper.GetString(s, "NumerUmowy"),
            Period = DynamicPropertyHelper.GetString(s, "Okres"),
            GrossAmount = DynamicPropertyHelper.GetNullableDecimal(s, "Brutto"),
            NetAmount = DynamicPropertyHelper.GetNullableDecimal(s, "Netto"),
            TaxAmount = DynamicPropertyHelper.GetNullableDecimal(s, "Podatek"),
            ZUSEmployee = DynamicPropertyHelper.GetNullableDecimal(s, "ZUSPracownik"),
            ZUSEmployer = DynamicPropertyHelper.GetNullableDecimal(s, "ZUSPracodawca"),
            PaymentDate = DynamicPropertyHelper.GetDateTime(s, "DataWyplaty")
        };
    }

    #endregion
}

#region DTOs

/// <summary>
/// Payroll list / payroll run (Lista Plac) DTO
/// </summary>
public class PayrollListDto
{
    public int Id { get; set; }
    /// <summary>Numer</summary>
    public string? Number { get; set; }
    /// <summary>Okres</summary>
    public string? Period { get; set; }
    /// <summary>Rok</summary>
    public int? Year { get; set; }
    /// <summary>Miesiac</summary>
    public int? Month { get; set; }
    /// <summary>Status</summary>
    public string? Status { get; set; }
    /// <summary>SumaBrutto</summary>
    public decimal? TotalGross { get; set; }
    /// <summary>SumaNetto</summary>
    public decimal? TotalNet { get; set; }
    /// <summary>SumaPodatku</summary>
    public decimal? TotalTax { get; set; }
    /// <summary>SumaZUS</summary>
    public decimal? TotalZUS { get; set; }
    /// <summary>LiczbaPracownikow</summary>
    public int? EmployeeCount { get; set; }
    /// <summary>DataUtworzenia</summary>
    public DateTime? CreatedDate { get; set; }
    /// <summary>DataZatwierdzenia</summary>
    public DateTime? ApprovedDate { get; set; }
}

/// <summary>
/// Pay slip / invoice for contract (Rachunek do Umowy) DTO
/// </summary>
public class PaySlipDto
{
    public int Id { get; set; }
    /// <summary>PracownikId</summary>
    public int EmployeeId { get; set; }
    /// <summary>NazwaPracownika</summary>
    public string? EmployeeName { get; set; }
    /// <summary>NumerUmowy</summary>
    public string? ContractNumber { get; set; }
    /// <summary>Okres</summary>
    public string? Period { get; set; }
    /// <summary>Brutto</summary>
    public decimal? GrossAmount { get; set; }
    /// <summary>Netto</summary>
    public decimal? NetAmount { get; set; }
    /// <summary>Podatek</summary>
    public decimal? TaxAmount { get; set; }
    /// <summary>ZUSPracownik</summary>
    public decimal? ZUSEmployee { get; set; }
    /// <summary>ZUSPracodawca</summary>
    public decimal? ZUSEmployer { get; set; }
    /// <summary>DataWyplaty</summary>
    public DateTime? PaymentDate { get; set; }
}

#endregion
