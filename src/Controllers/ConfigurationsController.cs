using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// System configurations and settings
/// </summary>
[ApiController]
[Route("api/configurations")]
[Authorize]
[Tags("Configurations")]
public class ConfigurationsController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<ConfigurationsController> _logger;

    public ConfigurationsController(ISferaService sferaService, ILogger<ConfigurationsController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    #region Document Configurations

    /// <summary>
    /// Get document type configurations
    /// </summary>
    [HttpGet("documents")]
    [ProducesResponseType(typeof(ApiResponse<List<DocumentConfigDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<DocumentConfigDto>>> GetDocumentConfigurations()
    {
        // Static configuration types - actual SDK configurations may vary
        var configs = new List<DocumentConfigDto>
        {
            // Sales documents
            new() { DocumentType = "FV", Name = "Faktura VAT", Category = "Sales", IsActive = true,
                    DefaultPrintTemplate = "Faktura VAT", RequiresContractor = true, GeneratesPayment = true },
            new() { DocumentType = "FVK", Name = "Faktura korygująca", Category = "Sales", IsActive = true,
                    DefaultPrintTemplate = "Faktura korygująca", RequiresContractor = true, GeneratesPayment = true },
            new() { DocumentType = "PA", Name = "Paragon", Category = "Sales", IsActive = true,
                    DefaultPrintTemplate = "Paragon", RequiresContractor = false, GeneratesPayment = true },
            new() { DocumentType = "FP", Name = "Faktura proforma", Category = "Sales", IsActive = true,
                    DefaultPrintTemplate = "Faktura proforma", RequiresContractor = true, GeneratesPayment = false },

            // Purchase documents
            new() { DocumentType = "FZ", Name = "Faktura zakupu", Category = "Purchase", IsActive = true,
                    DefaultPrintTemplate = null, RequiresContractor = true, GeneratesPayment = true },
            new() { DocumentType = "FZK", Name = "Korekta faktury zakupu", Category = "Purchase", IsActive = true,
                    DefaultPrintTemplate = null, RequiresContractor = true, GeneratesPayment = true },

            // Warehouse documents
            new() { DocumentType = "WZ", Name = "Wydanie zewnętrzne", Category = "Warehouse", IsActive = true,
                    DefaultPrintTemplate = "WZ", RequiresContractor = true, GeneratesPayment = false },
            new() { DocumentType = "PZ", Name = "Przyjęcie zewnętrzne", Category = "Warehouse", IsActive = true,
                    DefaultPrintTemplate = "PZ", RequiresContractor = true, GeneratesPayment = false },
            new() { DocumentType = "MM", Name = "Przesunięcie międzymagazynowe", Category = "Warehouse", IsActive = true,
                    DefaultPrintTemplate = "MM", RequiresContractor = false, GeneratesPayment = false },
            new() { DocumentType = "RW", Name = "Rozchód wewnętrzny", Category = "Warehouse", IsActive = true,
                    DefaultPrintTemplate = "RW", RequiresContractor = false, GeneratesPayment = false },
            new() { DocumentType = "PW", Name = "Przychód wewnętrzny", Category = "Warehouse", IsActive = true,
                    DefaultPrintTemplate = "PW", RequiresContractor = false, GeneratesPayment = false },

            // Orders
            new() { DocumentType = "ZK", Name = "Zamówienie od klienta", Category = "Orders", IsActive = true,
                    DefaultPrintTemplate = "Zamówienie", RequiresContractor = true, GeneratesPayment = false },
            new() { DocumentType = "ZD", Name = "Zamówienie do dostawcy", Category = "Orders", IsActive = true,
                    DefaultPrintTemplate = "Zamówienie", RequiresContractor = true, GeneratesPayment = false },

            // Offers
            new() { DocumentType = "OF", Name = "Oferta", Category = "Sales", IsActive = true,
                    DefaultPrintTemplate = "Oferta", RequiresContractor = true, GeneratesPayment = false }
        };

        return Ok(ApiResponse<List<DocumentConfigDto>>.Ok(configs));
    }

    /// <summary>
    /// Get document type configuration by type code
    /// </summary>
    [HttpGet("documents/{typeCode}")]
    [ProducesResponseType(typeof(ApiResponse<DocumentConfigDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<DocumentConfigDto>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<DocumentConfigDto>> GetDocumentConfiguration(string typeCode)
    {
        var configs = GetDocumentConfigurations().Value as ApiResponse<List<DocumentConfigDto>>;
        var config = configs?.Data?.FirstOrDefault(c => c.DocumentType.Equals(typeCode, StringComparison.OrdinalIgnoreCase));

        if (config == null)
        {
            return NotFound(ApiResponse<DocumentConfigDto>.Error($"Document configuration for type {typeCode} not found"));
        }

        return Ok(ApiResponse<DocumentConfigDto>.Ok(config));
    }

    #endregion

    #region Numbering Configurations

    /// <summary>
    /// Get numbering configurations for documents
    /// </summary>
    [HttpGet("numbering")]
    [ProducesResponseType(typeof(ApiResponse<List<NumberingConfigDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<NumberingConfigDto>>> GetNumberingConfigurations()
    {
        // Typical numbering patterns
        var configs = new List<NumberingConfigDto>
        {
            new() { DocumentType = "FV", Pattern = "FV/{RRRR}/{MM}/{NNNN}", ResetPeriod = "Monthly",
                    Description = "Numeracja faktur VAT", CurrentNumber = 1 },
            new() { DocumentType = "FVK", Pattern = "FVK/{RRRR}/{MM}/{NNNN}", ResetPeriod = "Monthly",
                    Description = "Numeracja faktur korygujących", CurrentNumber = 1 },
            new() { DocumentType = "PA", Pattern = "PA/{RRRR}/{MM}/{NNNN}", ResetPeriod = "Monthly",
                    Description = "Numeracja paragonów", CurrentNumber = 1 },
            new() { DocumentType = "WZ", Pattern = "WZ/{RRRR}/{NNNN}", ResetPeriod = "Yearly",
                    Description = "Numeracja wydań zewnętrznych", CurrentNumber = 1 },
            new() { DocumentType = "PZ", Pattern = "PZ/{RRRR}/{NNNN}", ResetPeriod = "Yearly",
                    Description = "Numeracja przyjęć zewnętrznych", CurrentNumber = 1 },
            new() { DocumentType = "MM", Pattern = "MM/{RRRR}/{NNNN}", ResetPeriod = "Yearly",
                    Description = "Numeracja przesunięć", CurrentNumber = 1 },
            new() { DocumentType = "RW", Pattern = "RW/{RRRR}/{NNNN}", ResetPeriod = "Yearly",
                    Description = "Numeracja rozchodów wewnętrznych", CurrentNumber = 1 },
            new() { DocumentType = "ZK", Pattern = "ZK/{RRRR}/{NNNN}", ResetPeriod = "Yearly",
                    Description = "Numeracja zamówień od klientów", CurrentNumber = 1 },
            new() { DocumentType = "ZD", Pattern = "ZD/{RRRR}/{NNNN}", ResetPeriod = "Yearly",
                    Description = "Numeracja zamówień do dostawców", CurrentNumber = 1 },
            new() { DocumentType = "OF", Pattern = "OF/{RRRR}/{NNNN}", ResetPeriod = "Yearly",
                    Description = "Numeracja ofert", CurrentNumber = 1 },
            new() { DocumentType = "KP", Pattern = "KP/{RRRR}/{NNNN}", ResetPeriod = "Yearly",
                    Description = "Numeracja dokumentów KP", CurrentNumber = 1 },
            new() { DocumentType = "KW", Pattern = "KW/{RRRR}/{NNNN}", ResetPeriod = "Yearly",
                    Description = "Numeracja dokumentów KW", CurrentNumber = 1 }
        };

        return Ok(ApiResponse<List<NumberingConfigDto>>.Ok(configs));
    }

    /// <summary>
    /// Get numbering pattern placeholders
    /// </summary>
    [HttpGet("numbering/placeholders")]
    [ProducesResponseType(typeof(ApiResponse<List<NumberingPlaceholderDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<NumberingPlaceholderDto>>> GetNumberingPlaceholders()
    {
        var placeholders = new List<NumberingPlaceholderDto>
        {
            new() { Code = "{RRRR}", Name = "Rok (4 cyfry)", Example = "2024", Description = "Pełny rok, np. 2024" },
            new() { Code = "{RR}", Name = "Rok (2 cyfry)", Example = "24", Description = "Skrócony rok, np. 24" },
            new() { Code = "{MM}", Name = "Miesiąc", Example = "01", Description = "Miesiąc z zerem wiodącym, np. 01-12" },
            new() { Code = "{DD}", Name = "Dzień", Example = "15", Description = "Dzień z zerem wiodącym, np. 01-31" },
            new() { Code = "{NNNN}", Name = "Numer (4 cyfry)", Example = "0001", Description = "Numer kolejny z zerami wiodącymi" },
            new() { Code = "{NNN}", Name = "Numer (3 cyfry)", Example = "001", Description = "Numer kolejny z zerami wiodącymi" },
            new() { Code = "{NN}", Name = "Numer (2 cyfry)", Example = "01", Description = "Numer kolejny z zerami wiodącymi" },
            new() { Code = "{N}", Name = "Numer (bez zer)", Example = "1", Description = "Numer kolejny bez zer wiodących" },
            new() { Code = "{MAG}", Name = "Symbol magazynu", Example = "M1", Description = "Symbol magazynu" },
            new() { Code = "{KASA}", Name = "Symbol stanowiska", Example = "K1", Description = "Symbol stanowiska kasowego" },
            new() { Code = "{OPER}", Name = "Symbol operatora", Example = "JK", Description = "Symbol operatora" }
        };

        return Ok(ApiResponse<List<NumberingPlaceholderDto>>.Ok(placeholders));
    }

    #endregion

    #region VAT Configuration

    /// <summary>
    /// Get VAT configuration
    /// </summary>
    [HttpGet("vat")]
    [ProducesResponseType(typeof(ApiResponse<VatConfigDto>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<VatConfigDto>> GetVatConfiguration()
    {
        try
        {
            var vatManager = _sferaService.GetManager("StawkiVat");
            if (vatManager == null)
            {
                return StatusCode(500, ApiResponse<VatConfigDto>.Error("Failed to get StawkiVat manager"));
            }

            var allRates = DynamicPropertyHelper.SafeGetAll((object)vatManager);
            var rates = new List<VatRateConfigDto>();

            foreach (var rate in allRates)
            {
                rates.Add(new VatRateConfigDto
                {
                    Id = DynamicPropertyHelper.GetId(rate),
                    Symbol = DynamicPropertyHelper.GetString(rate, "Symbol") ?? "",
                    Name = DynamicPropertyHelper.GetString(rate, "Nazwa") ?? "",
                    Rate = DynamicPropertyHelper.GetDecimal(rate, "Wartosc"),
                    IsActive = DynamicPropertyHelper.GetBool(rate, "Aktywna")
                });
            }

            var config = new VatConfigDto
            {
                DefaultVatRate = rates.FirstOrDefault(r => r.Rate == 23)?.Symbol ?? "23%",
                VatRates = rates,
                VatPayer = true,
                JpkVatEnabled = true,
                KsefEnabled = true
            };

            return Ok(ApiResponse<VatConfigDto>.Ok(config));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting VAT configuration");
            return StatusCode(500, ApiResponse<VatConfigDto>.Error("Error retrieving VAT configuration", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Currency Configuration

    /// <summary>
    /// Get currency configuration
    /// </summary>
    [HttpGet("currencies")]
    [ProducesResponseType(typeof(ApiResponse<CurrencyConfigDto>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<CurrencyConfigDto>> GetCurrencyConfiguration()
    {
        try
        {
            var currencyManager = _sferaService.GetManager("Waluty");
            if (currencyManager == null)
            {
                return StatusCode(500, ApiResponse<CurrencyConfigDto>.Error("Failed to get Waluty manager"));
            }

            var allCurrencies = DynamicPropertyHelper.SafeGetAll((object)currencyManager);
            var currencies = new List<CurrencyItemDto>();

            foreach (var curr in allCurrencies)
            {
                currencies.Add(new CurrencyItemDto
                {
                    Id = DynamicPropertyHelper.GetId(curr),
                    Symbol = DynamicPropertyHelper.GetString(curr, "Symbol") ?? "",
                    Name = DynamicPropertyHelper.GetString(curr, "Nazwa") ?? "",
                    IsDefault = DynamicPropertyHelper.GetBool(curr, "Domyslna"),
                    IsActive = DynamicPropertyHelper.GetBool(curr, "Aktywna")
                });
            }

            var config = new CurrencyConfigDto
            {
                BaseCurrency = currencies.FirstOrDefault(c => c.IsDefault)?.Symbol ?? "PLN",
                Currencies = currencies,
                ExchangeRateSource = "NBP",
                AutoUpdateRates = true
            };

            return Ok(ApiResponse<CurrencyConfigDto>.Ok(config));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting currency configuration");
            return StatusCode(500, ApiResponse<CurrencyConfigDto>.Error("Error retrieving currency configuration", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Payment Configuration

    /// <summary>
    /// Get payment methods configuration
    /// </summary>
    [HttpGet("payments")]
    [ProducesResponseType(typeof(ApiResponse<PaymentConfigDto>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<PaymentConfigDto>> GetPaymentConfiguration()
    {
        try
        {
            var paymentManager = _sferaService.GetManager("FormyPlatnosci");
            if (paymentManager == null)
            {
                return StatusCode(500, ApiResponse<PaymentConfigDto>.Error("Failed to get FormyPlatnosci manager"));
            }

            var allPayments = DynamicPropertyHelper.SafeGetAll((object)paymentManager);
            var methods = new List<PaymentMethodConfigDto>();

            foreach (var pm in allPayments)
            {
                methods.Add(new PaymentMethodConfigDto
                {
                    Id = DynamicPropertyHelper.GetId(pm),
                    Symbol = DynamicPropertyHelper.GetString(pm, "Symbol") ?? "",
                    Name = DynamicPropertyHelper.GetString(pm, "Nazwa") ?? "",
                    DefaultDueDays = DynamicPropertyHelper.GetNullableInt(pm, "LiczbaDni") ?? 0,
                    IsDefault = DynamicPropertyHelper.GetBool(pm, "Domyslna"),
                    IsActive = DynamicPropertyHelper.GetBool(pm, "Aktywna")
                });
            }

            var config = new PaymentConfigDto
            {
                DefaultPaymentMethod = methods.FirstOrDefault(m => m.IsDefault)?.Symbol ?? "PRZELEW",
                PaymentMethods = methods,
                DefaultDueDays = 14
            };

            return Ok(ApiResponse<PaymentConfigDto>.Ok(config));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting payment configuration");
            return StatusCode(500, ApiResponse<PaymentConfigDto>.Error("Error retrieving payment configuration", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Configuration Categories

    /// <summary>
    /// Get all configuration categories
    /// </summary>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(ApiResponse<List<ConfigCategoryDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<ConfigCategoryDto>>> GetConfigurationCategories()
    {
        var categories = new List<ConfigCategoryDto>
        {
            new() { Code = "DOCUMENTS", Name = "Dokumenty", Description = "Konfiguracje typów dokumentów", Endpoint = "/api/configurations/documents" },
            new() { Code = "NUMBERING", Name = "Numeracja", Description = "Schematy numeracji dokumentów", Endpoint = "/api/configurations/numbering" },
            new() { Code = "VAT", Name = "VAT", Description = "Stawki i ustawienia VAT", Endpoint = "/api/configurations/vat" },
            new() { Code = "CURRENCIES", Name = "Waluty", Description = "Konfiguracja walut", Endpoint = "/api/configurations/currencies" },
            new() { Code = "PAYMENTS", Name = "Płatności", Description = "Formy płatności", Endpoint = "/api/configurations/payments" },
            new() { Code = "PRINT", Name = "Wydruki", Description = "Szablony wydruków", Endpoint = "/api/print" },
            new() { Code = "ECOMMERCE", Name = "E-commerce", Description = "Integracje e-commerce", Endpoint = "/api/ecommerce" }
        };

        return Ok(ApiResponse<List<ConfigCategoryDto>>.Ok(categories));
    }

    #endregion
}

#region DTOs

/// <summary>
/// Document configuration DTO
/// </summary>
public class DocumentConfigDto
{
    public string DocumentType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? DefaultPrintTemplate { get; set; }
    public bool RequiresContractor { get; set; }
    public bool GeneratesPayment { get; set; }
}

/// <summary>
/// Numbering configuration DTO
/// </summary>
public class NumberingConfigDto
{
    public string DocumentType { get; set; } = string.Empty;
    public string Pattern { get; set; } = string.Empty;
    public string ResetPeriod { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int CurrentNumber { get; set; }
}

/// <summary>
/// Numbering placeholder DTO
/// </summary>
public class NumberingPlaceholderDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Example { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>
/// VAT configuration DTO
/// </summary>
public class VatConfigDto
{
    public string DefaultVatRate { get; set; } = string.Empty;
    public List<VatRateConfigDto> VatRates { get; set; } = new();
    public bool VatPayer { get; set; }
    public bool JpkVatEnabled { get; set; }
    public bool KsefEnabled { get; set; }
}

/// <summary>
/// VAT rate configuration DTO
/// </summary>
public class VatRateConfigDto
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Currency configuration DTO
/// </summary>
public class CurrencyConfigDto
{
    public string BaseCurrency { get; set; } = string.Empty;
    public List<CurrencyItemDto> Currencies { get; set; } = new();
    public string ExchangeRateSource { get; set; } = string.Empty;
    public bool AutoUpdateRates { get; set; }
}

/// <summary>
/// Currency item DTO
/// </summary>
public class CurrencyItemDto
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Payment configuration DTO
/// </summary>
public class PaymentConfigDto
{
    public string DefaultPaymentMethod { get; set; } = string.Empty;
    public List<PaymentMethodConfigDto> PaymentMethods { get; set; } = new();
    public int DefaultDueDays { get; set; }
}

/// <summary>
/// Payment method configuration DTO
/// </summary>
public class PaymentMethodConfigDto
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int DefaultDueDays { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Configuration category DTO
/// </summary>
public class ConfigCategoryDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Endpoint { get; set; } = string.Empty;
}

#endregion
