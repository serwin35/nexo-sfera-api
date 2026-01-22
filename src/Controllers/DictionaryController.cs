using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;

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
            var sfera = _sferaService.GetSfera();
            var stawki = sfera.StawkiVat().Dane.Wszystkie();

            if (activeOnly == true)
            {
                stawki = stawki.Where(s => s.Aktywna == true);
            }

            var dtos = stawki.Select(s => new VatRateDto
            {
                Id = s.Id,
                Symbol = s.Symbol ?? string.Empty,
                Name = s.Nazwa,
                Rate = s.Procent ?? 0,
                IsActive = s.Aktywna ?? false,
                Type = MapVatRateType(s.Symbol)
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
            var sfera = _sferaService.GetSfera();
            var stawka = sfera.StawkiVat().Dane.Wszystkie()
                .FirstOrDefault(s => s.Symbol == symbol);

            if (stawka == null)
            {
                return NotFound(ApiResponse<VatRateDto>.Error($"VAT rate '{symbol}' not found"));
            }

            var dto = new VatRateDto
            {
                Id = stawka.Id,
                Symbol = stawka.Symbol ?? string.Empty,
                Name = stawka.Nazwa,
                Rate = stawka.Procent ?? 0,
                IsActive = stawka.Aktywna ?? false,
                Type = MapVatRateType(stawka.Symbol)
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
            var sfera = _sferaService.GetSfera();
            var jednostki = sfera.JednostkiMiar().Dane.Wszystkie();

            if (activeOnly == true)
            {
                jednostki = jednostki.Where(j => j.Aktywna == true);
            }

            var dtos = jednostki.Select(j => new UnitOfMeasureDto
            {
                Id = j.Id,
                Symbol = j.Symbol ?? string.Empty,
                Name = j.Nazwa,
                DecimalPlaces = j.MiejscPoPrzecinku,
                IsActive = j.Aktywna ?? false
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
            var sfera = _sferaService.GetSfera();
            var jednostka = sfera.JednostkiMiar().Dane.Wszystkie()
                .FirstOrDefault(j => j.Symbol == symbol);

            if (jednostka == null)
            {
                return NotFound(ApiResponse<UnitOfMeasureDto>.Error($"Unit '{symbol}' not found"));
            }

            var dto = new UnitOfMeasureDto
            {
                Id = jednostka.Id,
                Symbol = jednostka.Symbol ?? string.Empty,
                Name = jednostka.Nazwa,
                DecimalPlaces = jednostka.MiejscPoPrzecinku,
                IsActive = jednostka.Aktywna ?? false
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
            var sfera = _sferaService.GetSfera();
            var grupy = sfera.GrupyAsortymentu().Dane.Wszystkie();

            if (activeOnly == true)
            {
                grupy = grupy.Where(g => g.Aktywna == true);
            }

            var allGroups = grupy.ToList();

            var dtos = allGroups.Select(g => new ProductGroupDto
            {
                Id = g.Id,
                Symbol = g.Symbol ?? string.Empty,
                Name = g.Nazwa,
                Description = g.Opis,
                ParentId = g.GrupaNadrzedna?.Id,
                ParentSymbol = g.GrupaNadrzedna?.Symbol,
                IsActive = g.Aktywna ?? false,
                ProductCount = g.Asortymenty?.Count ?? 0
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
            var sfera = _sferaService.GetSfera();
            var grupa = sfera.GrupyAsortymentu().Dane.Wszystkie()
                .FirstOrDefault(g => g.Symbol == symbol);

            if (grupa == null)
            {
                return NotFound(ApiResponse<ProductGroupDto>.Error($"Product group '{symbol}' not found"));
            }

            var dto = new ProductGroupDto
            {
                Id = grupa.Id,
                Symbol = grupa.Symbol ?? string.Empty,
                Name = grupa.Nazwa,
                Description = grupa.Opis,
                ParentId = grupa.GrupaNadrzedna?.Id,
                ParentSymbol = grupa.GrupaNadrzedna?.Symbol,
                IsActive = grupa.Aktywna ?? false,
                ProductCount = grupa.Asortymenty?.Count ?? 0
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
            var sfera = _sferaService.GetSfera();
            var poziomy = sfera.PoziomyCen().Dane.Wszystkie();

            if (activeOnly == true)
            {
                poziomy = poziomy.Where(p => p.Aktywny == true);
            }

            var dtos = poziomy.Select(p => new PriceLevelDto
            {
                Id = p.Id,
                Symbol = p.Symbol ?? string.Empty,
                Name = p.Nazwa,
                Description = p.Opis,
                IsDefault = p.Domyslny ?? false,
                IsActive = p.Aktywny ?? false,
                Priority = p.Priorytet ?? 0
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
            var sfera = _sferaService.GetSfera();
            var poziom = sfera.PoziomyCen().Dane.Wszystkie()
                .FirstOrDefault(p => p.Symbol == symbol);

            if (poziom == null)
            {
                return NotFound(ApiResponse<PriceLevelDto>.Error($"Price level '{symbol}' not found"));
            }

            var dto = new PriceLevelDto
            {
                Id = poziom.Id,
                Symbol = poziom.Symbol ?? string.Empty,
                Name = poziom.Nazwa,
                Description = poziom.Opis,
                IsDefault = poziom.Domyslny ?? false,
                IsActive = poziom.Aktywny ?? false,
                Priority = poziom.Priorytet ?? 0
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
            var sfera = _sferaService.GetSfera();
            var cenniki = sfera.Cenniki().Dane.Wszystkie();

            if (activeOnly == true)
            {
                cenniki = cenniki.Where(c => c.Aktywny == true);
            }

            var dtos = cenniki.Select(c => new PriceListDto
            {
                Id = c.Id,
                Symbol = c.Symbol ?? string.Empty,
                Name = c.Nazwa,
                Description = c.Opis,
                ValidFrom = c.DataOd,
                ValidTo = c.DataDo,
                IsActive = c.Aktywny ?? false,
                CurrencySymbol = c.Waluta?.Symbol,
                ItemCount = c.Pozycje?.Count ?? 0
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
            var sfera = _sferaService.GetSfera();
            var cennik = sfera.Cenniki().Dane.Wszystkie()
                .FirstOrDefault(c => c.Symbol == symbol);

            if (cennik == null)
            {
                return NotFound(ApiResponse<PriceListDto>.Error($"Price list '{symbol}' not found"));
            }

            var dto = new PriceListDto
            {
                Id = cennik.Id,
                Symbol = cennik.Symbol ?? string.Empty,
                Name = cennik.Nazwa,
                Description = cennik.Opis,
                ValidFrom = cennik.DataOd,
                ValidTo = cennik.DataDo,
                IsActive = cennik.Aktywny ?? false,
                CurrencySymbol = cennik.Waluta?.Symbol,
                ItemCount = cennik.Pozycje?.Count ?? 0
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
            var sfera = _sferaService.GetSfera();
            var cennik = sfera.Cenniki().Dane.Wszystkie()
                .FirstOrDefault(c => c.Symbol == symbol);

            if (cennik == null)
            {
                return NotFound(ApiResponse<object>.Error($"Price list '{symbol}' not found"));
            }

            var pozycje = cennik.Pozycje?.AsQueryable() ?? Enumerable.Empty<PozycjaCennika>().AsQueryable();

            if (productId.HasValue)
            {
                pozycje = pozycje.Where(p => p.Asortyment?.Id == productId.Value);
            }

            if (!string.IsNullOrEmpty(productSymbol))
            {
                pozycje = pozycje.Where(p => p.Asortyment?.Symbol == productSymbol);
            }

            var totalCount = pozycje.Count();
            var items = pozycje
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new PriceListItemDto
                {
                    Id = p.Id,
                    PriceListId = cennik.Id,
                    ProductId = p.Asortyment?.Id ?? 0,
                    ProductSymbol = p.Asortyment?.Symbol,
                    ProductName = p.Asortyment?.Nazwa,
                    PriceNet = p.CenaNetto ?? 0,
                    PriceGross = p.CenaBrutto ?? 0,
                    VatRate = p.Asortyment?.StawkaVatSprzedazy?.Symbol,
                    MinQuantity = p.IloscOd,
                    MaxQuantity = p.IloscDo,
                    ValidFrom = p.DataOd,
                    ValidTo = p.DataDo
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
            var sfera = _sferaService.GetSfera();
            var waluty = sfera.Waluty().Dane.Wszystkie();

            if (activeOnly == true)
            {
                waluty = waluty.Where(w => w.Aktywna == true);
            }

            var dtos = waluty.Select(w => new CurrencyDto
            {
                Id = w.Id,
                Symbol = w.Symbol ?? string.Empty,
                Name = w.Nazwa,
                IsoCode = w.Symbol,
                ExchangeRate = w.OstatniKurs,
                IsDefault = w.Bazowa ?? false,
                IsActive = w.Aktywna ?? false
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
            var sfera = _sferaService.GetSfera();
            var formy = sfera.FormyPlatnosci().Dane.Wszystkie();

            if (activeOnly == true)
            {
                formy = formy.Where(f => f.Aktywna == true);
            }

            var dtos = formy.Select(f => new PaymentMethodDto
            {
                Id = f.Id,
                Symbol = f.Symbol ?? string.Empty,
                Name = f.Nazwa,
                Type = MapPaymentMethodType(f.Typ),
                DefaultDueDays = f.DomyslnyTermin,
                IsActive = f.Aktywna ?? false,
                IsDefault = f.Domyslna ?? false
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
