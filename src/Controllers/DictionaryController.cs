using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Dictionary data endpoints (VAT rates, Units, Categories, Price levels, Currencies, Payment methods)
/// </summary>
[ApiController]
[Route("api/dictionary")]
[Authorize]
[Tags("Dictionary")]
public class DictionaryController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<DictionaryController> _logger;

    public DictionaryController(ISferaService sferaService, ILogger<DictionaryController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    #region VAT Rates

    /// <summary>
    /// Get all VAT rates
    /// </summary>
    [HttpGet("vat-rates")]
    [ProducesResponseType(typeof(ApiResponse<List<VatRateDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<VatRateDto>>> GetVatRates([FromQuery] bool? activeOnly = true)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var stawkiManager = sfera.StawkiVat();
            var allStawki = ((IEnumerable<dynamic>)stawkiManager.Dane.Wszystkie()).ToList();

            if (activeOnly == true)
            {
                allStawki = allStawki.Where(s => DynamicPropertyHelper.GetBool(s, "Aktywna")).ToList();
            }

            var dtos = allStawki.Select(s => new VatRateDto
            {
                Id = DynamicPropertyHelper.GetId(s),
                Symbol = DynamicPropertyHelper.GetString(s, "Symbol") ?? string.Empty,
                Name = DynamicPropertyHelper.GetString(s, "Nazwa"),
                Rate = DynamicPropertyHelper.GetNullableDecimal(s, "Procent") ?? 0,
                IsActive = DynamicPropertyHelper.GetBool(s, "Aktywna"),
                Type = MapVatRateType(DynamicPropertyHelper.GetString(s, "Symbol"))
            }).OrderBy(v => v.Rate).ToList();

            return Ok(ApiResponse<List<VatRateDto>>.Ok(dtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting VAT rates");
            return StatusCode(500, ApiResponse<List<VatRateDto>>.Error("Error retrieving VAT rates", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get VAT rate by symbol
    /// </summary>
    [HttpGet("vat-rates/{symbol}")]
    [ProducesResponseType(typeof(ApiResponse<VatRateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<VatRateDto>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<VatRateDto>> GetVatRate(string symbol)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var stawkiManager = sfera.StawkiVat();
            var allStawki = ((IEnumerable<dynamic>)stawkiManager.Dane.Wszystkie()).ToList();
            var stawka = allStawki.FirstOrDefault(s =>
                DynamicPropertyHelper.GetString(s, "Symbol") == symbol);

            if (stawka == null)
            {
                return NotFound(ApiResponse<VatRateDto>.Error($"VAT rate '{symbol}' not found"));
            }

            var dto = new VatRateDto
            {
                Id = DynamicPropertyHelper.GetId(stawka),
                Symbol = DynamicPropertyHelper.GetString(stawka, "Symbol") ?? string.Empty,
                Name = DynamicPropertyHelper.GetString(stawka, "Nazwa"),
                Rate = DynamicPropertyHelper.GetNullableDecimal(stawka, "Procent") ?? 0,
                IsActive = DynamicPropertyHelper.GetBool(stawka, "Aktywna"),
                Type = MapVatRateType(DynamicPropertyHelper.GetString(stawka, "Symbol"))
            };

            return Ok(ApiResponse<VatRateDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting VAT rate {Symbol}", symbol);
            return StatusCode(500, ApiResponse<VatRateDto>.Error("Error retrieving VAT rate", new List<string> { ex.Message }));
        }
    }

    private VatRateType MapVatRateType(string? symbol)
    {
        return symbol?.ToUpper() switch
        {
            "23%" or "22%" => VatRateType.Standard,
            "8%" or "7%" => VatRateType.Reduced,
            "5%" => VatRateType.SuperReduced,
            "0%" => VatRateType.Zero,
            "ZW" or "NP" => VatRateType.Exempt,
            _ => VatRateType.Standard
        };
    }

    #endregion

    #region Units of Measure

    /// <summary>
    /// Get all units of measure
    /// </summary>
    [HttpGet("units")]
    [ProducesResponseType(typeof(ApiResponse<List<UnitOfMeasureDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<UnitOfMeasureDto>>> GetUnits([FromQuery] bool? activeOnly = true)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var jednostkiManager = sfera.JednostkiMiar();
            var allJednostki = ((IEnumerable<dynamic>)jednostkiManager.Dane.Wszystkie()).ToList();

            if (activeOnly == true)
            {
                allJednostki = allJednostki.Where(j => DynamicPropertyHelper.GetBool(j, "Aktywna")).ToList();
            }

            var dtos = allJednostki.Select(j => new UnitOfMeasureDto
            {
                Id = DynamicPropertyHelper.GetId(j),
                Symbol = DynamicPropertyHelper.GetString(j, "Symbol") ?? string.Empty,
                Name = DynamicPropertyHelper.GetString(j, "Nazwa"),
                DecimalPlaces = DynamicPropertyHelper.GetInt(j, "MiejscPoPrzecinku"),
                IsActive = DynamicPropertyHelper.GetBool(j, "Aktywna")
            }).OrderBy(u => u.Symbol).ToList();

            return Ok(ApiResponse<List<UnitOfMeasureDto>>.Ok(dtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting units of measure");
            return StatusCode(500, ApiResponse<List<UnitOfMeasureDto>>.Error("Error retrieving units", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get unit of measure by symbol
    /// </summary>
    [HttpGet("units/{symbol}")]
    [ProducesResponseType(typeof(ApiResponse<UnitOfMeasureDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UnitOfMeasureDto>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<UnitOfMeasureDto>> GetUnit(string symbol)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var jednostkiManager = sfera.JednostkiMiar();
            var allJednostki = ((IEnumerable<dynamic>)jednostkiManager.Dane.Wszystkie()).ToList();
            var jednostka = allJednostki.FirstOrDefault(j =>
                DynamicPropertyHelper.GetString(j, "Symbol") == symbol);

            if (jednostka == null)
            {
                return NotFound(ApiResponse<UnitOfMeasureDto>.Error($"Unit '{symbol}' not found"));
            }

            var dto = new UnitOfMeasureDto
            {
                Id = DynamicPropertyHelper.GetId(jednostka),
                Symbol = DynamicPropertyHelper.GetString(jednostka, "Symbol") ?? string.Empty,
                Name = DynamicPropertyHelper.GetString(jednostka, "Nazwa"),
                DecimalPlaces = DynamicPropertyHelper.GetInt(jednostka, "MiejscPoPrzecinku"),
                IsActive = DynamicPropertyHelper.GetBool(jednostka, "Aktywna")
            };

            return Ok(ApiResponse<UnitOfMeasureDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unit {Symbol}", symbol);
            return StatusCode(500, ApiResponse<UnitOfMeasureDto>.Error("Error retrieving unit", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Product Groups (Categories)

    /// <summary>
    /// Get all product groups/categories
    /// </summary>
    [HttpGet("product-groups")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductGroupDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<ProductGroupDto>>> GetProductGroups(
        [FromQuery] bool? activeOnly = true,
        [FromQuery] bool? hierarchical = false)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var grupyManager = sfera.GrupyAsortymentu();
            var allGrupy = ((IEnumerable<dynamic>)grupyManager.Dane.Wszystkie()).ToList();

            if (activeOnly == true)
            {
                allGrupy = allGrupy.Where(g => DynamicPropertyHelper.GetBool(g, "Aktywna")).ToList();
            }

            var dtos = allGrupy.Select(g =>
            {
                var grupaNadrzedna = DynamicPropertyHelper.GetProperty(g, "GrupaNadrzedna");
                var asortymenty = DynamicPropertyHelper.GetCollection(g, "Asortymenty");

                return new ProductGroupDto
                {
                    Id = DynamicPropertyHelper.GetId(g),
                    Symbol = DynamicPropertyHelper.GetString(g, "Symbol") ?? string.Empty,
                    Name = DynamicPropertyHelper.GetString(g, "Nazwa"),
                    Description = DynamicPropertyHelper.GetString(g, "Opis"),
                    ParentId = grupaNadrzedna != null ? DynamicPropertyHelper.GetId(grupaNadrzedna) : null,
                    ParentSymbol = grupaNadrzedna != null ? DynamicPropertyHelper.GetString(grupaNadrzedna, "Symbol") : null,
                    IsActive = DynamicPropertyHelper.GetBool(g, "Aktywna"),
                    ProductCount = asortymenty.Count()
                };
            }).ToList();

            if (hierarchical == true)
            {
                // Build tree structure
                var rootGroups = dtos.Where(g => g.ParentId == null).ToList();
                foreach (var root in rootGroups)
                {
                    root.Children = GetChildGroups(root.Id, dtos);
                }
                return Ok(ApiResponse<List<ProductGroupDto>>.Ok(rootGroups));
            }

            return Ok(ApiResponse<List<ProductGroupDto>>.Ok(dtos.OrderBy(g => g.Symbol).ToList()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product groups");
            return StatusCode(500, ApiResponse<List<ProductGroupDto>>.Error("Error retrieving product groups", new List<string> { ex.Message }));
        }
    }

    private List<ProductGroupDto> GetChildGroups(int parentId, List<ProductGroupDto> allGroups)
    {
        var children = allGroups.Where(g => g.ParentId == parentId).ToList();
        foreach (var child in children)
        {
            child.Children = GetChildGroups(child.Id, allGroups);
        }
        return children.Any() ? children : null!;
    }

    /// <summary>
    /// Get product group by symbol
    /// </summary>
    [HttpGet("product-groups/{symbol}")]
    [ProducesResponseType(typeof(ApiResponse<ProductGroupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ProductGroupDto>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<ProductGroupDto>> GetProductGroup(string symbol)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var grupyManager = sfera.GrupyAsortymentu();
            var allGrupy = ((IEnumerable<dynamic>)grupyManager.Dane.Wszystkie()).ToList();
            var grupa = allGrupy.FirstOrDefault(g =>
                DynamicPropertyHelper.GetString(g, "Symbol") == symbol);

            if (grupa == null)
            {
                return NotFound(ApiResponse<ProductGroupDto>.Error($"Product group '{symbol}' not found"));
            }

            var grupaNadrzedna = DynamicPropertyHelper.GetProperty(grupa, "GrupaNadrzedna");
            var asortymenty = DynamicPropertyHelper.GetCollection(grupa, "Asortymenty");

            var dto = new ProductGroupDto
            {
                Id = DynamicPropertyHelper.GetId(grupa),
                Symbol = DynamicPropertyHelper.GetString(grupa, "Symbol") ?? string.Empty,
                Name = DynamicPropertyHelper.GetString(grupa, "Nazwa"),
                Description = DynamicPropertyHelper.GetString(grupa, "Opis"),
                ParentId = grupaNadrzedna != null ? DynamicPropertyHelper.GetId(grupaNadrzedna) : null,
                ParentSymbol = grupaNadrzedna != null ? DynamicPropertyHelper.GetString(grupaNadrzedna, "Symbol") : null,
                IsActive = DynamicPropertyHelper.GetBool(grupa, "Aktywna"),
                ProductCount = asortymenty.Count()
            };

            return Ok(ApiResponse<ProductGroupDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product group {Symbol}", symbol);
            return StatusCode(500, ApiResponse<ProductGroupDto>.Error("Error retrieving product group", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Price Levels

    /// <summary>
    /// Get all price levels
    /// </summary>
    [HttpGet("price-levels")]
    [ProducesResponseType(typeof(ApiResponse<List<PriceLevelDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<PriceLevelDto>>> GetPriceLevels([FromQuery] bool? activeOnly = true)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var poziomyManager = sfera.PoziomyCen();
            var allPoziomy = ((IEnumerable<dynamic>)poziomyManager.Dane.Wszystkie()).ToList();

            if (activeOnly == true)
            {
                allPoziomy = allPoziomy.Where(p => DynamicPropertyHelper.GetBool(p, "Aktywny")).ToList();
            }

            var dtos = allPoziomy.Select(p => new PriceLevelDto
            {
                Id = DynamicPropertyHelper.GetId(p),
                Symbol = DynamicPropertyHelper.GetString(p, "Symbol") ?? string.Empty,
                Name = DynamicPropertyHelper.GetString(p, "Nazwa"),
                Description = DynamicPropertyHelper.GetString(p, "Opis"),
                IsDefault = DynamicPropertyHelper.GetBool(p, "Domyslny"),
                IsActive = DynamicPropertyHelper.GetBool(p, "Aktywny"),
                Priority = DynamicPropertyHelper.GetNullableInt(p, "Priorytet") ?? 0
            }).OrderBy(p => p.Priority).ToList();

            return Ok(ApiResponse<List<PriceLevelDto>>.Ok(dtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting price levels");
            return StatusCode(500, ApiResponse<List<PriceLevelDto>>.Error("Error retrieving price levels", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get price level by symbol
    /// </summary>
    [HttpGet("price-levels/{symbol}")]
    [ProducesResponseType(typeof(ApiResponse<PriceLevelDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PriceLevelDto>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<PriceLevelDto>> GetPriceLevel(string symbol)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var poziomyManager = sfera.PoziomyCen();
            var allPoziomy = ((IEnumerable<dynamic>)poziomyManager.Dane.Wszystkie()).ToList();
            var poziom = allPoziomy.FirstOrDefault(p =>
                DynamicPropertyHelper.GetString(p, "Symbol") == symbol);

            if (poziom == null)
            {
                return NotFound(ApiResponse<PriceLevelDto>.Error($"Price level '{symbol}' not found"));
            }

            var dto = new PriceLevelDto
            {
                Id = DynamicPropertyHelper.GetId(poziom),
                Symbol = DynamicPropertyHelper.GetString(poziom, "Symbol") ?? string.Empty,
                Name = DynamicPropertyHelper.GetString(poziom, "Nazwa"),
                Description = DynamicPropertyHelper.GetString(poziom, "Opis"),
                IsDefault = DynamicPropertyHelper.GetBool(poziom, "Domyslny"),
                IsActive = DynamicPropertyHelper.GetBool(poziom, "Aktywny"),
                Priority = DynamicPropertyHelper.GetNullableInt(poziom, "Priorytet") ?? 0
            };

            return Ok(ApiResponse<PriceLevelDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting price level {Symbol}", symbol);
            return StatusCode(500, ApiResponse<PriceLevelDto>.Error("Error retrieving price level", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Price Lists

    /// <summary>
    /// Get all price lists
    /// </summary>
    [HttpGet("price-lists")]
    [ProducesResponseType(typeof(ApiResponse<List<PriceListDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<PriceListDto>>> GetPriceLists([FromQuery] bool? activeOnly = true)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var cennikiManager = sfera.Cenniki();
            var allCenniki = ((IEnumerable<dynamic>)cennikiManager.Dane.Wszystkie()).ToList();

            if (activeOnly == true)
            {
                allCenniki = allCenniki.Where(c => DynamicPropertyHelper.GetBool(c, "Aktywny")).ToList();
            }

            var dtos = allCenniki.Select(c =>
            {
                var waluta = DynamicPropertyHelper.GetProperty(c, "Waluta");
                var pozycje = DynamicPropertyHelper.GetCollection(c, "Pozycje");

                return new PriceListDto
                {
                    Id = DynamicPropertyHelper.GetId(c),
                    Symbol = DynamicPropertyHelper.GetString(c, "Symbol") ?? string.Empty,
                    Name = DynamicPropertyHelper.GetString(c, "Nazwa"),
                    Description = DynamicPropertyHelper.GetString(c, "Opis"),
                    ValidFrom = DynamicPropertyHelper.GetDateTime(c, "DataOd"),
                    ValidTo = DynamicPropertyHelper.GetDateTime(c, "DataDo"),
                    IsActive = DynamicPropertyHelper.GetBool(c, "Aktywny"),
                    CurrencySymbol = waluta != null ? DynamicPropertyHelper.GetString(waluta, "Symbol") : null,
                    ItemCount = pozycje.Count()
                };
            }).OrderBy(c => c.Symbol).ToList();

            return Ok(ApiResponse<List<PriceListDto>>.Ok(dtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting price lists");
            return StatusCode(500, ApiResponse<List<PriceListDto>>.Error("Error retrieving price lists", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get price list by symbol
    /// </summary>
    [HttpGet("price-lists/{symbol}")]
    [ProducesResponseType(typeof(ApiResponse<PriceListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PriceListDto>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<PriceListDto>> GetPriceList(string symbol)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var cennikiManager = sfera.Cenniki();
            var allCenniki = ((IEnumerable<dynamic>)cennikiManager.Dane.Wszystkie()).ToList();
            var cennik = allCenniki.FirstOrDefault(c =>
                DynamicPropertyHelper.GetString(c, "Symbol") == symbol);

            if (cennik == null)
            {
                return NotFound(ApiResponse<PriceListDto>.Error($"Price list '{symbol}' not found"));
            }

            var waluta = DynamicPropertyHelper.GetProperty(cennik, "Waluta");
            var pozycje = DynamicPropertyHelper.GetCollection(cennik, "Pozycje");

            var dto = new PriceListDto
            {
                Id = DynamicPropertyHelper.GetId(cennik),
                Symbol = DynamicPropertyHelper.GetString(cennik, "Symbol") ?? string.Empty,
                Name = DynamicPropertyHelper.GetString(cennik, "Nazwa"),
                Description = DynamicPropertyHelper.GetString(cennik, "Opis"),
                ValidFrom = DynamicPropertyHelper.GetDateTime(cennik, "DataOd"),
                ValidTo = DynamicPropertyHelper.GetDateTime(cennik, "DataDo"),
                IsActive = DynamicPropertyHelper.GetBool(cennik, "Aktywny"),
                CurrencySymbol = waluta != null ? DynamicPropertyHelper.GetString(waluta, "Symbol") : null,
                ItemCount = pozycje.Count()
            };

            return Ok(ApiResponse<PriceListDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting price list {Symbol}", symbol);
            return StatusCode(500, ApiResponse<PriceListDto>.Error("Error retrieving price list", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get price list items
    /// </summary>
    [HttpGet("price-lists/{symbol}/items")]
    [ProducesResponseType(typeof(PagedResponse<PriceListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public ActionResult<PagedResponse<PriceListItemDto>> GetPriceListItems(
        string symbol,
        [FromQuery] int? productId,
        [FromQuery] string? productSymbol,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var cennikiManager = sfera.Cenniki();
            var allCenniki = ((IEnumerable<dynamic>)cennikiManager.Dane.Wszystkie()).ToList();
            var cennik = allCenniki.FirstOrDefault(c =>
                DynamicPropertyHelper.GetString(c, "Symbol") == symbol);

            if (cennik == null)
            {
                return NotFound(ApiResponse<object>.Error($"Price list '{symbol}' not found"));
            }

            var pozycje = DynamicPropertyHelper.GetCollection(cennik, "Pozycje").ToList();

            if (productId.HasValue)
            {
                pozycje = pozycje.Where(p =>
                {
                    var asortyment = DynamicPropertyHelper.GetProperty(p, "Asortyment");
                    return asortyment != null && DynamicPropertyHelper.GetId(asortyment) == productId.Value;
                }).ToList();
            }

            if (!string.IsNullOrEmpty(productSymbol))
            {
                pozycje = pozycje.Where(p =>
                {
                    var asortyment = DynamicPropertyHelper.GetProperty(p, "Asortyment");
                    return asortyment != null && DynamicPropertyHelper.GetString(asortyment, "Symbol") == productSymbol;
                }).ToList();
            }

            var totalCount = pozycje.Count;
            var items = pozycje
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p =>
                {
                    var asortyment = DynamicPropertyHelper.GetProperty(p, "Asortyment");
                    var stawkaVat = asortyment != null ? DynamicPropertyHelper.GetProperty(asortyment, "StawkaVatSprzedazy") : null;

                    return new PriceListItemDto
                    {
                        Id = DynamicPropertyHelper.GetId(p),
                        PriceListId = DynamicPropertyHelper.GetId(cennik),
                        ProductId = asortyment != null ? DynamicPropertyHelper.GetId(asortyment) : 0,
                        ProductSymbol = asortyment != null ? DynamicPropertyHelper.GetString(asortyment, "Symbol") : null,
                        ProductName = asortyment != null ? DynamicPropertyHelper.GetString(asortyment, "Nazwa") : null,
                        PriceNet = DynamicPropertyHelper.GetNullableDecimal(p, "CenaNetto") ?? 0,
                        PriceGross = DynamicPropertyHelper.GetNullableDecimal(p, "CenaBrutto") ?? 0,
                        VatRate = stawkaVat != null ? DynamicPropertyHelper.GetString(stawkaVat, "Symbol") : null,
                        MinQuantity = DynamicPropertyHelper.GetNullableDecimal(p, "IloscOd"),
                        MaxQuantity = DynamicPropertyHelper.GetNullableDecimal(p, "IloscDo"),
                        ValidFrom = DynamicPropertyHelper.GetDateTime(p, "DataOd"),
                        ValidTo = DynamicPropertyHelper.GetDateTime(p, "DataDo")
                    };
                }).ToList();

            return Ok(new PagedResponse<PriceListItemDto>
            {
                Data = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting price list items for {Symbol}", symbol);
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving price list items", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Currencies

    /// <summary>
    /// Get all currencies
    /// </summary>
    [HttpGet("currencies")]
    [ProducesResponseType(typeof(ApiResponse<List<CurrencyDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<CurrencyDto>>> GetCurrencies([FromQuery] bool? activeOnly = true)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var walutyManager = sfera.Waluty();
            var allWaluty = ((IEnumerable<dynamic>)walutyManager.Dane.Wszystkie()).ToList();

            if (activeOnly == true)
            {
                allWaluty = allWaluty.Where(w => DynamicPropertyHelper.GetBool(w, "Aktywna")).ToList();
            }

            var dtos = allWaluty.Select(w => new CurrencyDto
            {
                Id = DynamicPropertyHelper.GetId(w),
                Symbol = DynamicPropertyHelper.GetString(w, "Symbol") ?? string.Empty,
                Name = DynamicPropertyHelper.GetString(w, "Nazwa"),
                IsoCode = DynamicPropertyHelper.GetString(w, "Symbol"),
                ExchangeRate = DynamicPropertyHelper.GetNullableDecimal(w, "OstatniKurs"),
                IsDefault = DynamicPropertyHelper.GetBool(w, "Bazowa"),
                IsActive = DynamicPropertyHelper.GetBool(w, "Aktywna")
            }).OrderBy(c => c.Symbol).ToList();

            return Ok(ApiResponse<List<CurrencyDto>>.Ok(dtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting currencies");
            return StatusCode(500, ApiResponse<List<CurrencyDto>>.Error("Error retrieving currencies", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Payment Methods

    /// <summary>
    /// Get all payment methods
    /// </summary>
    [HttpGet("payment-methods")]
    [ProducesResponseType(typeof(ApiResponse<List<PaymentMethodDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<PaymentMethodDto>>> GetPaymentMethods([FromQuery] bool? activeOnly = true)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var formyManager = sfera.FormyPlatnosci();
            var allFormy = ((IEnumerable<dynamic>)formyManager.Dane.Wszystkie()).ToList();

            if (activeOnly == true)
            {
                allFormy = allFormy.Where(f => DynamicPropertyHelper.GetBool(f, "Aktywna")).ToList();
            }

            var dtos = allFormy.Select(f => new PaymentMethodDto
            {
                Id = DynamicPropertyHelper.GetId(f),
                Symbol = DynamicPropertyHelper.GetString(f, "Symbol") ?? string.Empty,
                Name = DynamicPropertyHelper.GetString(f, "Nazwa"),
                Type = MapPaymentMethodType(DynamicPropertyHelper.GetNullableInt(f, "Typ")),
                DefaultDueDays = DynamicPropertyHelper.GetNullableInt(f, "DomyslnyTermin"),
                IsActive = DynamicPropertyHelper.GetBool(f, "Aktywna"),
                IsDefault = DynamicPropertyHelper.GetBool(f, "Domyslna")
            }).OrderBy(p => p.Symbol).ToList();

            return Ok(ApiResponse<List<PaymentMethodDto>>.Ok(dtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting payment methods");
            return StatusCode(500, ApiResponse<List<PaymentMethodDto>>.Error("Error retrieving payment methods", new List<string> { ex.Message }));
        }
    }

    private PaymentMethodType MapPaymentMethodType(int? typ)
    {
        return typ switch
        {
            0 => PaymentMethodType.Cash,
            1 => PaymentMethodType.BankTransfer,
            2 => PaymentMethodType.Card,
            3 => PaymentMethodType.DirectDebit,
            4 => PaymentMethodType.Compensation,
            _ => PaymentMethodType.Other
        };
    }

    #endregion
}
