using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Print templates and printing endpoints
/// </summary>
[ApiController]
[Route("api/print")]
[Authorize]
[Tags("Print")]
public class PrintController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<PrintController> _logger;

    public PrintController(ISferaService sferaService, ILogger<PrintController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    #region Print Headers/Footers

    /// <summary>
    /// Get print headers
    /// </summary>
    [HttpGet("headers")]
    [ProducesResponseType(typeof(ApiResponse<List<PrintHeaderDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<PrintHeaderDto>>> GetPrintHeaders()
    {
        try
        {
            var manager = _sferaService.GetManager("NaglowkiWydruku");
            if (manager == null)
            {
                return StatusCode(500, ApiResponse<List<PrintHeaderDto>>.Error("Failed to get NaglowkiWydruku manager"));
            }

            var allHeaders = DynamicPropertyHelper.SafeGetAll(manager);
            var items = new List<PrintHeaderDto>();

            foreach (var h in allHeaders)
            {
                items.Add(new PrintHeaderDto
                {
                    Id = DynamicPropertyHelper.GetId(h),
                    Name = DynamicPropertyHelper.GetString(h, "Nazwa") ?? "",
                    Content = DynamicPropertyHelper.GetString(h, "Tresc") ?? "",
                    IsDefault = DynamicPropertyHelper.GetBool(h, "Domyslny"),
                    IsActive = DynamicPropertyHelper.GetBool(h, "Aktywny")
                });
            }

            return Ok(ApiResponse<List<PrintHeaderDto>>.Ok(items));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting print headers");
            return StatusCode(500, ApiResponse<List<PrintHeaderDto>>.Error("Error retrieving print headers", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get print header by ID
    /// </summary>
    [HttpGet("headers/{id}")]
    [ProducesResponseType(typeof(ApiResponse<PrintHeaderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PrintHeaderDto>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<PrintHeaderDto>> GetPrintHeader(int id)
    {
        try
        {
            var manager = _sferaService.GetManager("NaglowkiWydruku");
            if (manager == null)
            {
                return StatusCode(500, ApiResponse<PrintHeaderDto>.Error("Failed to get NaglowkiWydruku manager"));
            }

            var allHeaders = DynamicPropertyHelper.SafeGetAll(manager);
            var header = allHeaders.FirstOrDefault(h => DynamicPropertyHelper.GetId(h) == id);

            if (header == null)
            {
                return NotFound(ApiResponse<PrintHeaderDto>.Error($"Print header with ID {id} not found"));
            }

            var dto = new PrintHeaderDto
            {
                Id = DynamicPropertyHelper.GetId(header),
                Name = DynamicPropertyHelper.GetString(header, "Nazwa") ?? "",
                Content = DynamicPropertyHelper.GetString(header, "Tresc") ?? "",
                IsDefault = DynamicPropertyHelper.GetBool(header, "Domyslny"),
                IsActive = DynamicPropertyHelper.GetBool(header, "Aktywny")
            };

            return Ok(ApiResponse<PrintHeaderDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting print header {Id}", id);
            return StatusCode(500, ApiResponse<PrintHeaderDto>.Error("Error retrieving print header", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get print footers
    /// </summary>
    [HttpGet("footers")]
    [ProducesResponseType(typeof(ApiResponse<List<PrintFooterDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<PrintFooterDto>>> GetPrintFooters()
    {
        try
        {
            var manager = _sferaService.GetManager("StopkiWydruku");
            if (manager == null)
            {
                return StatusCode(500, ApiResponse<List<PrintFooterDto>>.Error("Failed to get StopkiWydruku manager"));
            }

            var allFooters = DynamicPropertyHelper.SafeGetAll(manager);
            var items = new List<PrintFooterDto>();

            foreach (var f in allFooters)
            {
                items.Add(new PrintFooterDto
                {
                    Id = DynamicPropertyHelper.GetId(f),
                    Name = DynamicPropertyHelper.GetString(f, "Nazwa") ?? "",
                    Content = DynamicPropertyHelper.GetString(f, "Tresc") ?? "",
                    IsDefault = DynamicPropertyHelper.GetBool(f, "Domyslny"),
                    IsActive = DynamicPropertyHelper.GetBool(f, "Aktywny")
                });
            }

            return Ok(ApiResponse<List<PrintFooterDto>>.Ok(items));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting print footers");
            return StatusCode(500, ApiResponse<List<PrintFooterDto>>.Error("Error retrieving print footers", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get print footer by ID
    /// </summary>
    [HttpGet("footers/{id}")]
    [ProducesResponseType(typeof(ApiResponse<PrintFooterDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PrintFooterDto>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<PrintFooterDto>> GetPrintFooter(int id)
    {
        try
        {
            var manager = _sferaService.GetManager("StopkiWydruku");
            if (manager == null)
            {
                return StatusCode(500, ApiResponse<PrintFooterDto>.Error("Failed to get StopkiWydruku manager"));
            }

            var allFooters = DynamicPropertyHelper.SafeGetAll(manager);
            var footer = allFooters.FirstOrDefault(f => DynamicPropertyHelper.GetId(f) == id);

            if (footer == null)
            {
                return NotFound(ApiResponse<PrintFooterDto>.Error($"Print footer with ID {id} not found"));
            }

            var dto = new PrintFooterDto
            {
                Id = DynamicPropertyHelper.GetId(footer),
                Name = DynamicPropertyHelper.GetString(footer, "Nazwa") ?? "",
                Content = DynamicPropertyHelper.GetString(footer, "Tresc") ?? "",
                IsDefault = DynamicPropertyHelper.GetBool(footer, "Domyslny"),
                IsActive = DynamicPropertyHelper.GetBool(footer, "Aktywny")
            };

            return Ok(ApiResponse<PrintFooterDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting print footer {Id}", id);
            return StatusCode(500, ApiResponse<PrintFooterDto>.Error("Error retrieving print footer", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Print Parameters

    /// <summary>
    /// Get print parameters/settings
    /// </summary>
    [HttpGet("parameters")]
    [ProducesResponseType(typeof(ApiResponse<List<PrintParameterDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<PrintParameterDto>>> GetPrintParameters()
    {
        try
        {
            var manager = _sferaService.GetManager("ParametryWydruku");
            if (manager == null)
            {
                return StatusCode(500, ApiResponse<List<PrintParameterDto>>.Error("Failed to get ParametryWydruku manager"));
            }

            var allParams = DynamicPropertyHelper.SafeGetAll(manager);
            var items = new List<PrintParameterDto>();

            foreach (var p in allParams)
            {
                items.Add(new PrintParameterDto
                {
                    Id = DynamicPropertyHelper.GetId(p),
                    Name = DynamicPropertyHelper.GetString(p, "Nazwa") ?? "",
                    Value = DynamicPropertyHelper.GetString(p, "Wartosc") ?? "",
                    Description = DynamicPropertyHelper.GetString(p, "Opis")
                });
            }

            return Ok(ApiResponse<List<PrintParameterDto>>.Ok(items));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting print parameters");
            return StatusCode(500, ApiResponse<List<PrintParameterDto>>.Error("Error retrieving print parameters", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Print Logs

    /// <summary>
    /// Get print history/logs
    /// </summary>
    [HttpGet("logs")]
    [ProducesResponseType(typeof(PagedResponse<PrintLogDto>), StatusCodes.Status200OK)]
    public ActionResult<PagedResponse<PrintLogDto>> GetPrintLogs(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] string? documentType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var manager = _sferaService.GetManager("LogaWydruku");
            if (manager == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get LogaWydruku manager"));
            }

            var allLogs = DynamicPropertyHelper.SafeGetAll(manager);

            // Filter by date range
            if (dateFrom.HasValue)
            {
                allLogs = allLogs.Where(l =>
                {
                    var date = DynamicPropertyHelper.GetDateTime(l, "DataWydruku");
                    return date.HasValue && date.Value >= dateFrom.Value;
                }).ToList();
            }

            if (dateTo.HasValue)
            {
                allLogs = allLogs.Where(l =>
                {
                    var date = DynamicPropertyHelper.GetDateTime(l, "DataWydruku");
                    return date.HasValue && date.Value <= dateTo.Value;
                }).ToList();
            }

            // Filter by document type
            if (!string.IsNullOrEmpty(documentType))
            {
                allLogs = allLogs.Where(l =>
                {
                    var type = DynamicPropertyHelper.GetString(l, "TypDokumentu");
                    return type != null && type.Contains(documentType, StringComparison.OrdinalIgnoreCase);
                }).ToList();
            }

            var totalCount = allLogs.Count;
            var pagedLogs = allLogs
                .OrderByDescending(l => DynamicPropertyHelper.GetDateTime(l, "DataWydruku") ?? DateTime.MinValue)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var items = new List<PrintLogDto>();
            foreach (var l in pagedLogs)
            {
                items.Add(new PrintLogDto
                {
                    Id = DynamicPropertyHelper.GetId(l),
                    DocumentType = DynamicPropertyHelper.GetString(l, "TypDokumentu") ?? "",
                    DocumentNumber = DynamicPropertyHelper.GetString(l, "NumerDokumentu") ?? "",
                    PrintDate = DynamicPropertyHelper.GetDateTime(l, "DataWydruku"),
                    PrintedBy = DynamicPropertyHelper.GetString(l, "Operator") ?? "",
                    TemplateName = DynamicPropertyHelper.GetString(l, "NazwaSzablonu") ?? "",
                    PrinterName = DynamicPropertyHelper.GetString(l, "NazwaDrukarki")
                });
            }

            return Ok(new PagedResponse<PrintLogDto>
            {
                Data = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting print logs");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving print logs", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Label Templates

    /// <summary>
    /// Get label templates
    /// </summary>
    [HttpGet("labels")]
    [ProducesResponseType(typeof(ApiResponse<List<LabelTemplateDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<LabelTemplateDto>>> GetLabelTemplates()
    {
        try
        {
            var manager = _sferaService.GetManager("SzablonyNaklejek");
            if (manager == null)
            {
                return StatusCode(500, ApiResponse<List<LabelTemplateDto>>.Error("Failed to get SzablonyNaklejek manager"));
            }

            var allTemplates = DynamicPropertyHelper.SafeGetAll(manager);
            var items = new List<LabelTemplateDto>();

            foreach (var t in allTemplates)
            {
                items.Add(new LabelTemplateDto
                {
                    Id = DynamicPropertyHelper.GetId(t),
                    Name = DynamicPropertyHelper.GetString(t, "Nazwa") ?? "",
                    Description = DynamicPropertyHelper.GetString(t, "Opis"),
                    Width = DynamicPropertyHelper.GetDecimal(t, "Szerokosc"),
                    Height = DynamicPropertyHelper.GetDecimal(t, "Wysokosc"),
                    IsActive = DynamicPropertyHelper.GetBool(t, "Aktywny")
                });
            }

            return Ok(ApiResponse<List<LabelTemplateDto>>.Ok(items));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting label templates");
            return StatusCode(500, ApiResponse<List<LabelTemplateDto>>.Error("Error retrieving label templates", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get label template by ID
    /// </summary>
    [HttpGet("labels/{id}")]
    [ProducesResponseType(typeof(ApiResponse<LabelTemplateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<LabelTemplateDto>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<LabelTemplateDto>> GetLabelTemplate(int id)
    {
        try
        {
            var manager = _sferaService.GetManager("SzablonyNaklejek");
            if (manager == null)
            {
                return StatusCode(500, ApiResponse<LabelTemplateDto>.Error("Failed to get SzablonyNaklejek manager"));
            }

            var allTemplates = DynamicPropertyHelper.SafeGetAll(manager);
            var template = allTemplates.FirstOrDefault(t => DynamicPropertyHelper.GetId(t) == id);

            if (template == null)
            {
                return NotFound(ApiResponse<LabelTemplateDto>.Error($"Label template with ID {id} not found"));
            }

            var dto = new LabelTemplateDto
            {
                Id = DynamicPropertyHelper.GetId(template),
                Name = DynamicPropertyHelper.GetString(template, "Nazwa") ?? "",
                Description = DynamicPropertyHelper.GetString(template, "Opis"),
                Width = DynamicPropertyHelper.GetDecimal(template, "Szerokosc"),
                Height = DynamicPropertyHelper.GetDecimal(template, "Wysokosc"),
                IsActive = DynamicPropertyHelper.GetBool(template, "Aktywny")
            };

            return Ok(ApiResponse<LabelTemplateDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting label template {Id}", id);
            return StatusCode(500, ApiResponse<LabelTemplateDto>.Error("Error retrieving label template", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Print Template Types (Static)

    /// <summary>
    /// Get available print template types by document
    /// </summary>
    [HttpGet("template-types")]
    [ProducesResponseType(typeof(ApiResponse<List<PrintTemplateTypeDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<PrintTemplateTypeDto>>> GetPrintTemplateTypes()
    {
        var types = new List<PrintTemplateTypeDto>
        {
            new() { Code = "INVOICE", Name = "Faktura VAT", Category = "Sales" },
            new() { Code = "INVOICE_CORRECTION", Name = "Faktura korygująca", Category = "Sales" },
            new() { Code = "RECEIPT", Name = "Paragon", Category = "Sales" },
            new() { Code = "PROFORMA", Name = "Faktura proforma", Category = "Sales" },
            new() { Code = "OFFER", Name = "Oferta", Category = "Sales" },
            new() { Code = "ORDER", Name = "Zamówienie", Category = "Sales" },
            new() { Code = "WZ", Name = "Wydanie zewnętrzne (WZ)", Category = "Warehouse" },
            new() { Code = "PZ", Name = "Przyjęcie zewnętrzne (PZ)", Category = "Warehouse" },
            new() { Code = "MM", Name = "Przesunięcie międzymagazynowe (MM)", Category = "Warehouse" },
            new() { Code = "RW", Name = "Rozchód wewnętrzny (RW)", Category = "Warehouse" },
            new() { Code = "PW", Name = "Przychód wewnętrzny (PW)", Category = "Warehouse" },
            new() { Code = "CASH_RECEIPT", Name = "Dowód kasowy KP", Category = "Finance" },
            new() { Code = "CASH_DISBURSEMENT", Name = "Dowód kasowy KW", Category = "Finance" },
            new() { Code = "CASH_REPORT", Name = "Raport kasowy", Category = "Finance" },
            new() { Code = "BANK_TRANSFER", Name = "Przelew bankowy", Category = "Finance" },
            new() { Code = "INVENTORY_SHEET", Name = "Arkusz inwentaryzacyjny", Category = "Inventory" },
            new() { Code = "PRICE_LIST", Name = "Cennik", Category = "Products" },
            new() { Code = "PRODUCT_LABEL", Name = "Etykieta produktu", Category = "Products" },
            new() { Code = "SHIPPING_LABEL", Name = "Etykieta wysyłkowa", Category = "Shipping" }
        };

        return Ok(ApiResponse<List<PrintTemplateTypeDto>>.Ok(types));
    }

    #endregion
}

#region DTOs

/// <summary>
/// Print header DTO
/// </summary>
public class PrintHeaderDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Print footer DTO
/// </summary>
public class PrintFooterDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Print parameter DTO
/// </summary>
public class PrintParameterDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>
/// Print log DTO
/// </summary>
public class PrintLogDto
{
    public int Id { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string DocumentNumber { get; set; } = string.Empty;
    public DateTime? PrintDate { get; set; }
    public string PrintedBy { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string? PrinterName { get; set; }
}

/// <summary>
/// Label template DTO
/// </summary>
public class LabelTemplateDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Width { get; set; }
    public decimal Height { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Print template type DTO
/// </summary>
public class PrintTemplateTypeDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

#endregion
