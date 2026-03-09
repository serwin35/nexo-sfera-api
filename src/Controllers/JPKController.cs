using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// JPK (Jednolity Plik Kontrolny) management endpoints
/// </summary>
[ApiController]
[Route("api/jpk")]
[Authorize]
[Tags("JPK")]
public class JPKController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<JPKController> _logger;

    public JPKController(ISferaService sferaService, ILogger<JPKController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    #region JPK Files

    /// <summary>
    /// Get JPK files list
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<JpkFileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetJpkFiles(
        [FromQuery] string? type,
        [FromQuery] int? year,
        [FromQuery] int? month,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("JednolitePlikiKontrolne");
                if (manager == null) return (managerNull: true, items: (List<JpkFileDto>?)null, totalCount: 0);

                var allJpk = DynamicPropertyHelper.SafeGetAll((object)manager);

                if (!string.IsNullOrEmpty(type))
                {
                    allJpk = allJpk.Where(j =>
                    {
                        var jpkType = DynamicPropertyHelper.GetString(j, "TypJPK");
                        return jpkType != null && jpkType.Contains(type, StringComparison.OrdinalIgnoreCase);
                    }).ToList();
                }

                if (year.HasValue)
                {
                    allJpk = allJpk.Where(j =>
                    {
                        var dataOd = DynamicPropertyHelper.GetDateTime(j, "DataOd");
                        return dataOd.HasValue && dataOd.Value.Year == year.Value;
                    }).ToList();
                }

                if (month.HasValue)
                {
                    allJpk = allJpk.Where(j =>
                    {
                        var dataOd = DynamicPropertyHelper.GetDateTime(j, "DataOd");
                        return dataOd.HasValue && dataOd.Value.Month == month.Value;
                    }).ToList();
                }

                if (!string.IsNullOrEmpty(status))
                {
                    allJpk = allJpk.Where(j =>
                    {
                        var jpkStatus = MapJpkStatus(DynamicPropertyHelper.GetNullableInt(j, "Status"));
                        return jpkStatus.Equals(status, StringComparison.OrdinalIgnoreCase);
                    }).ToList();
                }

                var totalCount = allJpk.Count;
                var pagedJpk = allJpk
                    .OrderByDescending(j => DynamicPropertyHelper.GetDateTime(j, "DataOd") ?? DateTime.MinValue)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var items = new List<JpkFileDto>();
                foreach (var j in pagedJpk)
                {
                    items.Add(MapJpkFile(j));
                }

                return (managerNull: false, items: (List<JpkFileDto>?)items, totalCount);
            });

            if (result.managerNull)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get JednolitePlikiKontrolne manager"));
            }

            return Ok(new PagedResponse<JpkFileDto>
            {
                Data = result.items!,
                Page = page,
                PageSize = pageSize,
                TotalCount = result.totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting JPK files");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving JPK files", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get JPK file by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<JpkFileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<JpkFileDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJpkFile(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("JednolitePlikiKontrolne");
                if (manager == null) return (found: false, managerNull: true, dto: (JpkFileDto?)null);

                var allJpk = DynamicPropertyHelper.SafeGetAll((object)manager);
                var jpk = allJpk.FirstOrDefault(j => DynamicPropertyHelper.GetId(j) == id);

                if (jpk == null) return (found: false, managerNull: false, dto: (JpkFileDto?)null);

                return (found: true, managerNull: false, dto: (JpkFileDto?)MapJpkFile(jpk));
            });

            if (result.managerNull)
                return StatusCode(500, ApiResponse<JpkFileDto>.Error("Failed to get JednolitePlikiKontrolne manager"));

            if (!result.found)
                return NotFound(ApiResponse<JpkFileDto>.Error($"JPK file with ID {id} not found"));

            return Ok(ApiResponse<JpkFileDto>.Ok(result.dto!));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting JPK file {Id}", id);
            return StatusCode(500, ApiResponse<JpkFileDto>.Error("Error retrieving JPK file", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get JPK file status
    /// </summary>
    [HttpGet("{id}/status")]
    [ProducesResponseType(typeof(ApiResponse<JpkStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<JpkStatusDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJpkFileStatus(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("JednolitePlikiKontrolne");
                if (manager == null) return (found: false, managerNull: true, dto: (JpkStatusDto?)null);

                var allJpk = DynamicPropertyHelper.SafeGetAll((object)manager);
                var jpk = allJpk.FirstOrDefault(j => DynamicPropertyHelper.GetId(j) == id);

                if (jpk == null) return (found: false, managerNull: false, dto: (JpkStatusDto?)null);

                var statusDto = new JpkStatusDto
                {
                    Id = id,
                    Status = MapJpkStatus(DynamicPropertyHelper.GetNullableInt(jpk, "Status")),
                    StatusCode = DynamicPropertyHelper.GetNullableInt(jpk, "Status") ?? 0,
                    ReferenceNumber = DynamicPropertyHelper.GetString(jpk, "NumerReferencyjny"),
                    UpoNumber = DynamicPropertyHelper.GetString(jpk, "NumerUPO"),
                    SubmittedDate = DynamicPropertyHelper.GetDateTime(jpk, "DataWyslania"),
                    AcceptedDate = DynamicPropertyHelper.GetDateTime(jpk, "DataPotwierdzenia"),
                    ErrorMessage = DynamicPropertyHelper.GetString(jpk, "KomunikatBledu")
                };

                return (found: true, managerNull: false, dto: (JpkStatusDto?)statusDto);
            });

            if (result.managerNull)
                return StatusCode(500, ApiResponse<JpkStatusDto>.Error("Failed to get JednolitePlikiKontrolne manager"));

            if (!result.found)
                return NotFound(ApiResponse<JpkStatusDto>.Error($"JPK file with ID {id} not found"));

            return Ok(ApiResponse<JpkStatusDto>.Ok(result.dto!));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting JPK status {Id}", id);
            return StatusCode(500, ApiResponse<JpkStatusDto>.Error("Error retrieving JPK status", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region JPK Types

    /// <summary>
    /// Get available JPK types
    /// </summary>
    [HttpGet("types")]
    [ProducesResponseType(typeof(ApiResponse<List<JpkTypeDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<JpkTypeDto>>> GetJpkTypes()
    {
        var types = new List<JpkTypeDto>
        {
            new() { Code = "JPK_V7M", Name = "JPK_V7M", Description = "Ewidencja VAT i deklaracja miesięczna", Category = "VAT" },
            new() { Code = "JPK_V7K", Name = "JPK_V7K", Description = "Ewidencja VAT i deklaracja kwartalna", Category = "VAT" },
            new() { Code = "JPK_VAT", Name = "JPK_VAT", Description = "Ewidencja zakupu i sprzedaży VAT (archiwalne)", Category = "VAT" },
            new() { Code = "JPK_FA", Name = "JPK_FA", Description = "Faktury VAT", Category = "Invoices" },
            new() { Code = "JPK_FA_RR", Name = "JPK_FA_RR", Description = "Faktury VAT RR", Category = "Invoices" },
            new() { Code = "JPK_MAG", Name = "JPK_MAG", Description = "Magazyn", Category = "Warehouse" },
            new() { Code = "JPK_WB", Name = "JPK_WB", Description = "Wyciąg bankowy", Category = "Bank" },
            new() { Code = "JPK_KR", Name = "JPK_KR", Description = "Księgi rachunkowe", Category = "Accounting" },
            new() { Code = "JPK_PKPIR", Name = "JPK_PKPIR", Description = "Podatkowa księga przychodów i rozchodów", Category = "Accounting" },
            new() { Code = "JPK_EWP", Name = "JPK_EWP", Description = "Ewidencja przychodów", Category = "Accounting" }
        };

        return Ok(ApiResponse<List<JpkTypeDto>>.Ok(types));
    }

    /// <summary>
    /// Get JPK versions for a type
    /// </summary>
    [HttpGet("types/{typeCode}/versions")]
    [ProducesResponseType(typeof(ApiResponse<List<JpkVersionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetJpkVersions(string typeCode)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("WersjeDefinicjiJPK");
                if (manager == null)
                {
                    return new List<JpkVersionDto>
                    {
                        new() { Version = "1", TypeCode = typeCode, ValidFrom = new DateTime(2020, 10, 1), IsCurrent = true }
                    };
                }

                var allVersions = DynamicPropertyHelper.SafeGetAll((object)manager);
                var typeVersions = allVersions.Where(v =>
                {
                    var typ = DynamicPropertyHelper.GetString(v, "TypJPK");
                    return typ != null && typ.Equals(typeCode, StringComparison.OrdinalIgnoreCase);
                }).ToList();

                var versions = new List<JpkVersionDto>();
                foreach (var v in typeVersions)
                {
                    versions.Add(new JpkVersionDto
                    {
                        Version = DynamicPropertyHelper.GetString(v, "Wersja") ?? "1",
                        TypeCode = typeCode,
                        ValidFrom = DynamicPropertyHelper.GetDateTime(v, "DataObowiazywaniaOd"),
                        ValidTo = DynamicPropertyHelper.GetDateTime(v, "DataObowiazywaniaDo"),
                        IsCurrent = DynamicPropertyHelper.GetBool(v, "Aktualna")
                    });
                }

                return versions.OrderByDescending(v => v.ValidFrom).ToList();
            });

            return Ok(ApiResponse<List<JpkVersionDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting JPK versions for type {TypeCode}", typeCode);
            return StatusCode(500, ApiResponse<List<JpkVersionDto>>.Error("Error retrieving JPK versions", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region JPK Packages

    /// <summary>
    /// Get JPK packages (paczki)
    /// </summary>
    [HttpGet("packages")]
    [ProducesResponseType(typeof(PagedResponse<JpkPackageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetJpkPackages(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("PaczkiJednolitychPlikowKontrolnych");
                if (manager == null) return (managerNull: true, items: (List<JpkPackageDto>?)null, totalCount: 0);

                var allPaczki = DynamicPropertyHelper.SafeGetAll((object)manager);

                if (!string.IsNullOrEmpty(status))
                {
                    allPaczki = allPaczki.Where(p =>
                    {
                        var paczkaStatus = MapJpkPackageStatus(DynamicPropertyHelper.GetNullableInt(p, "Status"));
                        return paczkaStatus.Equals(status, StringComparison.OrdinalIgnoreCase);
                    }).ToList();
                }

                var totalCount = allPaczki.Count;
                var pagedPaczki = allPaczki
                    .OrderByDescending(p => DynamicPropertyHelper.GetDateTime(p, "DataUtworzenia") ?? DateTime.MinValue)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var items = new List<JpkPackageDto>();
                foreach (var p in pagedPaczki)
                {
                    items.Add(MapJpkPackage(p));
                }

                return (managerNull: false, items: (List<JpkPackageDto>?)items, totalCount);
            });

            if (result.managerNull)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get PaczkiJednolitychPlikowKontrolnych manager"));

            return Ok(new PagedResponse<JpkPackageDto>
            {
                Data = result.items!,
                Page = page,
                PageSize = pageSize,
                TotalCount = result.totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting JPK packages");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving JPK packages", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get JPK package by ID
    /// </summary>
    [HttpGet("packages/{id}")]
    [ProducesResponseType(typeof(ApiResponse<JpkPackageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<JpkPackageDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJpkPackage(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("PaczkiJednolitychPlikowKontrolnych");
                if (manager == null) return (found: false, managerNull: true, dto: (JpkPackageDto?)null);

                var allPaczki = DynamicPropertyHelper.SafeGetAll((object)manager);
                var paczka = allPaczki.FirstOrDefault(p => DynamicPropertyHelper.GetId(p) == id);

                if (paczka == null) return (found: false, managerNull: false, dto: (JpkPackageDto?)null);

                return (found: true, managerNull: false, dto: (JpkPackageDto?)MapJpkPackage(paczka));
            });

            if (result.managerNull)
                return StatusCode(500, ApiResponse<JpkPackageDto>.Error("Failed to get PaczkiJednolitychPlikowKontrolnych manager"));

            if (!result.found)
                return NotFound(ApiResponse<JpkPackageDto>.Error($"JPK package with ID {id} not found"));

            return Ok(ApiResponse<JpkPackageDto>.Ok(result.dto!));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting JPK package {Id}", id);
            return StatusCode(500, ApiResponse<JpkPackageDto>.Error("Error retrieving JPK package", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Helpers

    private static JpkFileDto MapJpkFile(dynamic j)
    {
        var numerWewnetrzny = DynamicPropertyHelper.GetProperty(j, "NumerWewnetrzny");

        return new JpkFileDto
        {
            Id = DynamicPropertyHelper.GetId(j),
            Number = numerWewnetrzny != null ? DynamicPropertyHelper.GetString(numerWewnetrzny, "PelnaSygnatura") : null,
            Type = DynamicPropertyHelper.GetString(j, "TypJPK"),
            Version = DynamicPropertyHelper.GetString(j, "Wersja"),
            PeriodFrom = DynamicPropertyHelper.GetDateTime(j, "DataOd"),
            PeriodTo = DynamicPropertyHelper.GetDateTime(j, "DataDo"),
            Status = MapJpkStatus(DynamicPropertyHelper.GetNullableInt(j, "Status")),
            CreatedDate = DynamicPropertyHelper.GetDateTime(j, "DataUtworzenia"),
            SubmittedDate = DynamicPropertyHelper.GetDateTime(j, "DataWyslania"),
            ReferenceNumber = DynamicPropertyHelper.GetString(j, "NumerReferencyjny"),
            UpoNumber = DynamicPropertyHelper.GetString(j, "NumerUPO"),
            RecordCount = DynamicPropertyHelper.GetNullableInt(j, "LiczbaPozycji") ?? 0,
            Description = DynamicPropertyHelper.GetString(j, "Opis")
        };
    }

    private static JpkPackageDto MapJpkPackage(dynamic p)
    {
        return new JpkPackageDto
        {
            Id = DynamicPropertyHelper.GetId(p),
            Name = DynamicPropertyHelper.GetString(p, "Nazwa"),
            Status = MapJpkPackageStatus(DynamicPropertyHelper.GetNullableInt(p, "Status")),
            CreatedDate = DynamicPropertyHelper.GetDateTime(p, "DataUtworzenia"),
            SubmittedDate = DynamicPropertyHelper.GetDateTime(p, "DataWyslania"),
            ReferenceNumber = DynamicPropertyHelper.GetString(p, "NumerReferencyjny"),
            FileCount = DynamicPropertyHelper.GetCount(p, "PlikiJPK"),
            TotalSize = DynamicPropertyHelper.GetNullableLong(p, "RozmiarCalkowity")
        };
    }

    private static string MapJpkStatus(int? status)
    {
        return status switch
        {
            0 => "Draft",           // Roboczy
            1 => "Generated",       // Wygenerowany
            2 => "ReadyToSend",     // Gotowy do wysłania
            3 => "Sending",         // W trakcie wysyłania
            4 => "Submitted",       // Wysłany
            5 => "Accepted",        // Zaakceptowany
            6 => "Rejected",        // Odrzucony
            7 => "Error",           // Błąd
            _ => "Unknown"
        };
    }

    private static string MapJpkPackageStatus(int? status)
    {
        return status switch
        {
            0 => "Draft",
            1 => "Prepared",
            2 => "Submitted",
            3 => "Accepted",
            4 => "Rejected",
            _ => "Unknown"
        };
    }

    #endregion
}

#region DTOs

/// <summary>
/// JPK file DTO
/// </summary>
public class JpkFileDto
{
    public int Id { get; set; }
    public string? Number { get; set; }
    public string? Type { get; set; }
    public string? Version { get; set; }
    public DateTime? PeriodFrom { get; set; }
    public DateTime? PeriodTo { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTime? CreatedDate { get; set; }
    public DateTime? SubmittedDate { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? UpoNumber { get; set; }
    public int RecordCount { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// JPK status DTO
/// </summary>
public class JpkStatusDto
{
    public int Id { get; set; }
    public string Status { get; set; } = "Draft";
    public int StatusCode { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? UpoNumber { get; set; }
    public DateTime? SubmittedDate { get; set; }
    public DateTime? AcceptedDate { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// JPK type DTO
/// </summary>
public class JpkTypeDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = "Other";
}

/// <summary>
/// JPK version DTO
/// </summary>
public class JpkVersionDto
{
    public string Version { get; set; } = "1";
    public string TypeCode { get; set; } = string.Empty;
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public bool IsCurrent { get; set; }
}

/// <summary>
/// JPK package DTO
/// </summary>
public class JpkPackageDto
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTime? CreatedDate { get; set; }
    public DateTime? SubmittedDate { get; set; }
    public string? ReferenceNumber { get; set; }
    public int FileCount { get; set; }
    public long? TotalSize { get; set; }
}

#endregion
