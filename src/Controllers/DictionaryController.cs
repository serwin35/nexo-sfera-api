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
            var stawkiManager = _sferaService.GetManager("StawkiVat");
            if (stawkiManager == null)
            {
                return StatusCode(500, ApiResponse<List<VatRateDto>>.Error("Failed to get StawkiVat manager"));
            }
            var allStawki = ((IEnumerable<dynamic>)stawkiManager.Dane.Wszystkie()).ToList();

            if (activeOnly == true)
            {
                var filteredStawki = new List<dynamic>();
                foreach (var s in allStawki)
                {
                    if (DynamicPropertyHelper.GetBool(s, "Aktywna"))
                    {
                        filteredStawki.Add(s);
                    }
                }
                allStawki = filteredStawki;
            }

            var dtos = new List<VatRateDto>();
            foreach (var s in allStawki)
            {
                dtos.Add(new VatRateDto
                {
                    Id = DynamicPropertyHelper.GetId(s),
                    Symbol = DynamicPropertyHelper.GetString(s, "Symbol") ?? string.Empty,
                    Name = DynamicPropertyHelper.GetString(s, "Nazwa"),
                    Rate = DynamicPropertyHelper.GetNullableDecimal(s, "Procent") ?? 0,
                    IsActive = DynamicPropertyHelper.GetBool(s, "Aktywna"),
                    Type = MapVatRateType(DynamicPropertyHelper.GetString(s, "Symbol"))
                });
            }
            dtos = dtos.OrderBy(v => v.Rate).ToList();

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
            var stawkiManager = _sferaService.GetManager("StawkiVat");
            if (stawkiManager == null) return StatusCode(500, ApiResponse<VatRateDto>.Error("Failed to get StawkiVat manager"));
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
            var jednostkiManager = _sferaService.GetManager("JednostkiMiar");
            if (jednostkiManager == null)
            {
                return StatusCode(500, ApiResponse<List<UnitOfMeasureDto>>.Error("Failed to get JednostkiMiar manager"));
            }
            var allJednostki = ((IEnumerable<dynamic>)jednostkiManager.Dane.Wszystkie()).ToList();

            if (activeOnly == true)
            {
                var filteredJednostki = new List<dynamic>();
                foreach (var j in allJednostki)
                {
                    if (DynamicPropertyHelper.GetBool(j, "Aktywna"))
                    {
                        filteredJednostki.Add(j);
                    }
                }
                allJednostki = filteredJednostki;
            }

            var dtos = new List<UnitOfMeasureDto>();
            foreach (var j in allJednostki)
            {
                dtos.Add(new UnitOfMeasureDto
                {
                    Id = DynamicPropertyHelper.GetId(j),
                    Symbol = DynamicPropertyHelper.GetString(j, "Symbol") ?? string.Empty,
                    Name = DynamicPropertyHelper.GetString(j, "Nazwa"),
                    DecimalPlaces = DynamicPropertyHelper.GetInt(j, "MiejscPoPrzecinku"),
                    IsActive = DynamicPropertyHelper.GetBool(j, "Aktywna")
                });
            }
            dtos = dtos.OrderBy(u => u.Symbol).ToList();

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
            var jednostkiManager = _sferaService.GetManager("JednostkiMiar");
            if (jednostkiManager == null)
            {
                return StatusCode(500, ApiResponse<UnitOfMeasureDto>.Error("Failed to get JednostkiMiar manager"));
            }
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
            var grupyManager = _sferaService.GetManager("GrupyAsortymentu");
            if (grupyManager == null)
            {
                return StatusCode(500, ApiResponse<List<ProductGroupDto>>.Error("Failed to get GrupyAsortymentu manager"));
            }
            var allGrupy = ((IEnumerable<dynamic>)grupyManager.Dane.Wszystkie()).ToList();

            if (activeOnly == true)
            {
                var filteredGrupy = new List<dynamic>();
                foreach (var g in allGrupy)
                {
                    if (DynamicPropertyHelper.GetBool(g, "Aktywna"))
                    {
                        filteredGrupy.Add(g);
                    }
                }
                allGrupy = filteredGrupy;
            }

            var dtos = new List<ProductGroupDto>();
            foreach (var g in allGrupy)
            {
                var grupaNadrzedna = DynamicPropertyHelper.GetProperty(g, "GrupaNadrzedna");
                var asortymenty = DynamicPropertyHelper.GetCollection(g, "Asortymenty");

                dtos.Add(new ProductGroupDto
                {
                    Id = DynamicPropertyHelper.GetId(g),
                    Symbol = DynamicPropertyHelper.GetString(g, "Symbol") ?? string.Empty,
                    Name = DynamicPropertyHelper.GetString(g, "Nazwa"),
                    Description = DynamicPropertyHelper.GetString(g, "Opis"),
                    ParentId = grupaNadrzedna != null ? DynamicPropertyHelper.GetId(grupaNadrzedna) : null,
                    ParentSymbol = grupaNadrzedna != null ? DynamicPropertyHelper.GetString(grupaNadrzedna, "Symbol") : null,
                    IsActive = DynamicPropertyHelper.GetBool(g, "Aktywna"),
                    ProductCount = asortymenty.Count()
                });
            }

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

    private List<ProductGroupListItemDto>? GetChildGroups(int parentId, List<ProductGroupDto> allGroups)
    {
        var children = allGroups.Where(g => g.ParentId == parentId).ToList();
        if (!children.Any()) return null;

        var result = new List<ProductGroupListItemDto>();
        foreach (var child in children)
        {
            result.Add(new ProductGroupListItemDto
            {
                Id = child.Id,
                Symbol = child.Symbol,
                Name = child.Name ?? string.Empty,
                ParentId = child.ParentId,
                ParentSymbol = child.ParentSymbol,
                Level = 0, // Can be calculated if needed
                ProductCount = child.ProductCount,
                HasChildren = allGroups.Any(g => g.ParentId == child.Id)
            });
        }
        return result;
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
            var grupyManager = _sferaService.GetManager("GrupyAsortymentu");
            if (grupyManager == null)
            {
                return StatusCode(500, ApiResponse<ProductGroupDto>.Error("Failed to get GrupyAsortymentu manager"));
            }
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
            var poziomyManager = _sferaService.GetManager("PoziomyCen");
            if (poziomyManager == null)
            {
                return StatusCode(500, ApiResponse<List<PriceLevelDto>>.Error("Failed to get PoziomyCen manager"));
            }
            var allPoziomy = ((IEnumerable<dynamic>)poziomyManager.Dane.Wszystkie()).ToList();

            if (activeOnly == true)
            {
                var filteredPoziomy = new List<dynamic>();
                foreach (var p in allPoziomy)
                {
                    if (DynamicPropertyHelper.GetBool(p, "Aktywny"))
                    {
                        filteredPoziomy.Add(p);
                    }
                }
                allPoziomy = filteredPoziomy;
            }

            var dtos = new List<PriceLevelDto>();
            foreach (var p in allPoziomy)
            {
                dtos.Add(new PriceLevelDto
                {
                    Id = DynamicPropertyHelper.GetId(p),
                    Symbol = DynamicPropertyHelper.GetString(p, "Symbol") ?? string.Empty,
                    Name = DynamicPropertyHelper.GetString(p, "Nazwa"),
                    Description = DynamicPropertyHelper.GetString(p, "Opis"),
                    IsDefault = DynamicPropertyHelper.GetBool(p, "Domyslny"),
                    IsActive = DynamicPropertyHelper.GetBool(p, "Aktywny"),
                    Priority = DynamicPropertyHelper.GetNullableInt(p, "Priorytet") ?? 0
                });
            }
            dtos = dtos.OrderBy(p => p.Priority).ToList();

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
            var poziomyManager = _sferaService.GetManager("PoziomyCen");
            if (poziomyManager == null)
            {
                return StatusCode(500, ApiResponse<PriceLevelDto>.Error("Failed to get PoziomyCen manager"));
            }
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
            var cennikiManager = _sferaService.GetManager("Cenniki");
            if (cennikiManager == null)
            {
                return StatusCode(500, ApiResponse<List<PriceListDto>>.Error("Failed to get Cenniki manager"));
            }
            var allCenniki = ((IEnumerable<dynamic>)cennikiManager.Dane.Wszystkie()).ToList();

            if (activeOnly == true)
            {
                var filteredCenniki = new List<dynamic>();
                foreach (var c in allCenniki)
                {
                    if (DynamicPropertyHelper.GetBool(c, "Aktywny"))
                    {
                        filteredCenniki.Add(c);
                    }
                }
                allCenniki = filteredCenniki;
            }

            var dtos = new List<PriceListDto>();
            foreach (var c in allCenniki)
            {
                var waluta = DynamicPropertyHelper.GetProperty(c, "Waluta");
                var pozycje = DynamicPropertyHelper.GetCollection(c, "Pozycje");

                dtos.Add(new PriceListDto
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
                });
            }
            dtos = dtos.OrderBy(c => c.Symbol).ToList();

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
            var cennikiManager = _sferaService.GetManager("Cenniki");
            if (cennikiManager == null)
            {
                return StatusCode(500, ApiResponse<PriceListDto>.Error("Failed to get Cenniki manager"));
            }
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
            var cennikiManager = _sferaService.GetManager("Cenniki");
            if (cennikiManager == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Cenniki manager"));
            }
            var allCenniki = ((IEnumerable<dynamic>)cennikiManager.Dane.Wszystkie()).ToList();
            var cennik = allCenniki.FirstOrDefault(c =>
                DynamicPropertyHelper.GetString(c, "Symbol") == symbol);

            if (cennik == null)
            {
                return NotFound(ApiResponse<object>.Error($"Price list '{symbol}' not found"));
            }

            var pozycje = DynamicPropertyHelper.GetCollection(cennik, "Pozycje");

            if (productId.HasValue)
            {
                var filteredPozycje = new List<dynamic>();
                foreach (var p in pozycje)
                {
                    var asortyment = DynamicPropertyHelper.GetProperty(p, "Asortyment");
                    if (asortyment != null && DynamicPropertyHelper.GetId(asortyment) == productId.Value)
                    {
                        filteredPozycje.Add(p);
                    }
                }
                pozycje = filteredPozycje;
            }

            if (!string.IsNullOrEmpty(productSymbol))
            {
                var filteredPozycje = new List<dynamic>();
                foreach (var p in pozycje)
                {
                    var asortyment = DynamicPropertyHelper.GetProperty(p, "Asortyment");
                    if (asortyment != null && DynamicPropertyHelper.GetString(asortyment, "Symbol") == productSymbol)
                    {
                        filteredPozycje.Add(p);
                    }
                }
                pozycje = filteredPozycje;
            }

            var totalCount = pozycje.Count;
            var pagedPozycje = pozycje
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var items = new List<PriceListItemDto>();
            foreach (var p in pagedPozycje)
            {
                var asortyment = DynamicPropertyHelper.GetProperty(p, "Asortyment");
                var stawkaVat = asortyment != null ? DynamicPropertyHelper.GetProperty(asortyment, "StawkaVatSprzedazy") : null;

                items.Add(new PriceListItemDto
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
                });
            }

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
            var walutyManager = _sferaService.GetManager("Waluty");
            if (walutyManager == null)
            {
                return StatusCode(500, ApiResponse<List<CurrencyDto>>.Error("Failed to get Waluty manager"));
            }
            var allWaluty = ((IEnumerable<dynamic>)walutyManager.Dane.Wszystkie()).ToList();

            if (activeOnly == true)
            {
                var filteredWaluty = new List<dynamic>();
                foreach (var w in allWaluty)
                {
                    if (DynamicPropertyHelper.GetBool(w, "Aktywna"))
                    {
                        filteredWaluty.Add(w);
                    }
                }
                allWaluty = filteredWaluty;
            }

            var dtos = new List<CurrencyDto>();
            foreach (var w in allWaluty)
            {
                dtos.Add(new CurrencyDto
                {
                    Id = DynamicPropertyHelper.GetId(w),
                    Symbol = DynamicPropertyHelper.GetString(w, "Symbol") ?? string.Empty,
                    Name = DynamicPropertyHelper.GetString(w, "Nazwa"),
                    IsoCode = DynamicPropertyHelper.GetString(w, "Symbol"),
                    ExchangeRate = DynamicPropertyHelper.GetNullableDecimal(w, "OstatniKurs"),
                    IsDefault = DynamicPropertyHelper.GetBool(w, "Bazowa"),
                    IsActive = DynamicPropertyHelper.GetBool(w, "Aktywna")
                });
            }
            dtos = dtos.OrderBy(c => c.Symbol).ToList();

            return Ok(ApiResponse<List<CurrencyDto>>.Ok(dtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting currencies");
            return StatusCode(500, ApiResponse<List<CurrencyDto>>.Error("Error retrieving currencies", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get exchange rates for a currency
    /// </summary>
    [HttpGet("currencies/{symbol}/rates")]
    [ProducesResponseType(typeof(ApiResponse<List<ExchangeRateDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<List<ExchangeRateDto>>> GetExchangeRates(
        string symbol,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int limit = 30)
    {
        try
        {
            // Access exchange rates through Waluty manager and then Kursy collection on currency
            var walutyManager = _sferaService.GetManager("Waluty");
            if (walutyManager == null)
            {
                return StatusCode(500, ApiResponse<List<ExchangeRateDto>>.Error("Failed to get Waluty manager"));
            }
            var allWaluty = ((IEnumerable<dynamic>)walutyManager.Dane.Wszystkie()).ToList();

            var waluta = allWaluty.FirstOrDefault(w =>
                DynamicPropertyHelper.GetString(w, "Symbol") == symbol);

            if (waluta == null)
            {
                return NotFound(ApiResponse<List<ExchangeRateDto>>.Error($"Currency '{symbol}' not found"));
            }

            // Get exchange rates from the currency's Kursy collection
            var kursy = DynamicPropertyHelper.GetCollection(waluta, "Kursy");

            // Filter by date using explicit loops (dynamic types don't work with lambdas)
            var filteredKursy = new List<dynamic>();
            foreach (var k in kursy)
            {
                var data = DynamicPropertyHelper.GetDateTime(k, "Data");
                if (dateFrom.HasValue && (!data.HasValue || data.Value < dateFrom.Value))
                    continue;
                if (dateTo.HasValue && (!data.HasValue || data.Value > dateTo.Value))
                    continue;
                filteredKursy.Add(k);
            }

            // Map to DTOs first (typed), then sort
            var rates = new List<ExchangeRateDto>();
            foreach (var k in filteredKursy)
            {
                rates.Add(new ExchangeRateDto
                {
                    Id = DynamicPropertyHelper.GetId(k),
                    CurrencySymbol = symbol,
                    Date = DynamicPropertyHelper.GetDateTime(k, "Data"),
                    Rate = DynamicPropertyHelper.GetDecimal(k, "Kurs"),
                    Multiplier = DynamicPropertyHelper.GetInt(k, "Przelicznik"),
                    Source = DynamicPropertyHelper.GetString(k, "Zrodlo") ?? "NBP",
                    TableNumber = DynamicPropertyHelper.GetString(k, "NumerTabeli")
                });
            }

            // Now sort by date descending (typed list, lambdas work)
            rates = rates
                .OrderByDescending(r => r.Date ?? DateTime.MinValue)
                .Take(limit)
                .ToList();

            return Ok(ApiResponse<List<ExchangeRateDto>>.Ok(rates));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting exchange rates for {Symbol}", symbol);
            return StatusCode(500, ApiResponse<List<ExchangeRateDto>>.Error("Error retrieving exchange rates", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get current exchange rate for a currency on specific date
    /// </summary>
    [HttpGet("currencies/{symbol}/rate")]
    [ProducesResponseType(typeof(ApiResponse<ExchangeRateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<ExchangeRateDto>> GetExchangeRate(
        string symbol,
        [FromQuery] DateTime? date)
    {
        try
        {
            var targetDate = date ?? DateTime.Today;

            // Access exchange rates through Waluty manager
            var walutyManager = _sferaService.GetManager("Waluty");
            if (walutyManager == null)
            {
                return StatusCode(500, ApiResponse<ExchangeRateDto>.Error("Failed to get Waluty manager"));
            }
            var allWaluty = ((IEnumerable<dynamic>)walutyManager.Dane.Wszystkie()).ToList();

            var waluta = allWaluty.FirstOrDefault(w =>
                DynamicPropertyHelper.GetString(w, "Symbol") == symbol);

            if (waluta == null)
            {
                return NotFound(ApiResponse<ExchangeRateDto>.Error($"Currency '{symbol}' not found"));
            }

            // Get exchange rates from the currency's Kursy collection
            var kursy = DynamicPropertyHelper.GetCollection(waluta, "Kursy");

            dynamic? latestKurs = null;
            DateTime? latestDate = null;

            foreach (var k in kursy)
            {
                var kursDate = DynamicPropertyHelper.GetDateTime(k, "Data");
                if (kursDate.HasValue && kursDate.Value <= targetDate)
                {
                    if (!latestDate.HasValue || kursDate.Value > latestDate.Value)
                    {
                        latestDate = kursDate;
                        latestKurs = k;
                    }
                }
            }

            if (latestKurs == null)
            {
                return NotFound(ApiResponse<ExchangeRateDto>.Error($"No exchange rate found for {symbol}"));
            }

            var dto = new ExchangeRateDto
            {
                Id = DynamicPropertyHelper.GetId(latestKurs),
                CurrencySymbol = symbol,
                Date = latestDate,
                Rate = DynamicPropertyHelper.GetDecimal(latestKurs, "Kurs"),
                Multiplier = DynamicPropertyHelper.GetInt(latestKurs, "Przelicznik"),
                Source = DynamicPropertyHelper.GetString(latestKurs, "Zrodlo") ?? "NBP",
                TableNumber = DynamicPropertyHelper.GetString(latestKurs, "NumerTabeli")
            };

            return Ok(ApiResponse<ExchangeRateDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting exchange rate for {Symbol}", symbol);
            return StatusCode(500, ApiResponse<ExchangeRateDto>.Error("Error retrieving exchange rate", new List<string> { ex.Message }));
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
            var formyManager = _sferaService.GetManager("FormyPlatnosci");
            if (formyManager == null)
            {
                return StatusCode(500, ApiResponse<List<PaymentMethodDto>>.Error("Failed to get FormyPlatnosci manager"));
            }
            var allFormy = ((IEnumerable<dynamic>)formyManager.Dane.Wszystkie()).ToList();

            if (activeOnly == true)
            {
                var filteredFormy = new List<dynamic>();
                foreach (var f in allFormy)
                {
                    if (DynamicPropertyHelper.GetBool(f, "Aktywna"))
                    {
                        filteredFormy.Add(f);
                    }
                }
                allFormy = filteredFormy;
            }

            var dtos = new List<PaymentMethodDto>();
            foreach (var f in allFormy)
            {
                dtos.Add(new PaymentMethodDto
                {
                    Id = DynamicPropertyHelper.GetId(f),
                    Symbol = DynamicPropertyHelper.GetString(f, "Symbol") ?? string.Empty,
                    Name = DynamicPropertyHelper.GetString(f, "Nazwa"),
                    Type = MapPaymentMethodType(DynamicPropertyHelper.GetNullableInt(f, "Typ")),
                    DefaultDueDays = DynamicPropertyHelper.GetNullableInt(f, "DomyslnyTermin"),
                    IsActive = DynamicPropertyHelper.GetBool(f, "Aktywna"),
                    IsDefault = DynamicPropertyHelper.GetBool(f, "Domyslna")
                });
            }
            dtos = dtos.OrderBy(p => p.Symbol).ToList();

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

    #region Cash Operation Types (Phase 2)

    /// <summary>
    /// Get all cash operation types (rodzaje operacji kasowych)
    /// </summary>
    [HttpGet("cash-operation-types")]
    [ProducesResponseType(typeof(ApiResponse<List<OperationTypeDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<OperationTypeDto>>> GetCashOperationTypes([FromQuery] bool? activeOnly = true)
    {
        try
        {
            var manager = _sferaService.GetManager("RodzajeOperacjiKasowych");
            if (manager == null)
            {
                return StatusCode(500, ApiResponse<List<OperationTypeDto>>.Error("Failed to get RodzajeOperacjiKasowych manager"));
            }

            var allRodzaje = ((IEnumerable<dynamic>)manager.Dane.Wszystkie()).ToList();

            if (activeOnly == true)
            {
                var filtered = new List<dynamic>();
                foreach (var r in allRodzaje)
                {
                    if (DynamicPropertyHelper.GetBool(r, "Aktywny"))
                    {
                        filtered.Add(r);
                    }
                }
                allRodzaje = filtered;
            }

            var dtos = new List<OperationTypeDto>();
            foreach (var r in allRodzaje)
            {
                dtos.Add(new OperationTypeDto
                {
                    Id = DynamicPropertyHelper.GetId(r),
                    Symbol = DynamicPropertyHelper.GetString(r, "Symbol") ?? string.Empty,
                    Name = DynamicPropertyHelper.GetString(r, "Nazwa"),
                    Description = DynamicPropertyHelper.GetString(r, "Opis"),
                    OperationType = MapCashOperationType(DynamicPropertyHelper.GetNullableInt(r, "Typ")),
                    IsDeposit = DynamicPropertyHelper.GetNullableInt(r, "Typ") == 0,
                    IsWithdrawal = DynamicPropertyHelper.GetNullableInt(r, "Typ") == 1,
                    IsActive = DynamicPropertyHelper.GetBool(r, "Aktywny"),
                    IsDefault = DynamicPropertyHelper.GetBool(r, "Domyslny")
                });
            }

            return Ok(ApiResponse<List<OperationTypeDto>>.Ok(dtos.OrderBy(o => o.Symbol).ToList()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cash operation types");
            return StatusCode(500, ApiResponse<List<OperationTypeDto>>.Error("Error retrieving cash operation types", new List<string> { ex.Message }));
        }
    }

    private string MapCashOperationType(int? typ)
    {
        return typ switch
        {
            0 => "Deposit",      // Wpłata
            1 => "Withdrawal",   // Wypłata
            _ => "Other"
        };
    }

    #endregion

    #region Bank Operation Types (Phase 2)

    /// <summary>
    /// Get all bank operation types (rodzaje operacji bankowych)
    /// </summary>
    [HttpGet("bank-operation-types")]
    [ProducesResponseType(typeof(ApiResponse<List<OperationTypeDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<OperationTypeDto>>> GetBankOperationTypes([FromQuery] bool? activeOnly = true)
    {
        try
        {
            var manager = _sferaService.GetManager("RodzajeOperacjiBankowych");
            if (manager == null)
            {
                return StatusCode(500, ApiResponse<List<OperationTypeDto>>.Error("Failed to get RodzajeOperacjiBankowych manager"));
            }

            var allRodzaje = ((IEnumerable<dynamic>)manager.Dane.Wszystkie()).ToList();

            if (activeOnly == true)
            {
                var filtered = new List<dynamic>();
                foreach (var r in allRodzaje)
                {
                    if (DynamicPropertyHelper.GetBool(r, "Aktywny"))
                    {
                        filtered.Add(r);
                    }
                }
                allRodzaje = filtered;
            }

            var dtos = new List<OperationTypeDto>();
            foreach (var r in allRodzaje)
            {
                dtos.Add(new OperationTypeDto
                {
                    Id = DynamicPropertyHelper.GetId(r),
                    Symbol = DynamicPropertyHelper.GetString(r, "Symbol") ?? string.Empty,
                    Name = DynamicPropertyHelper.GetString(r, "Nazwa"),
                    Description = DynamicPropertyHelper.GetString(r, "Opis"),
                    OperationType = MapBankOperationType(DynamicPropertyHelper.GetNullableInt(r, "Typ")),
                    IsDeposit = DynamicPropertyHelper.GetNullableInt(r, "Typ") == 0,
                    IsWithdrawal = DynamicPropertyHelper.GetNullableInt(r, "Typ") == 1,
                    IsActive = DynamicPropertyHelper.GetBool(r, "Aktywny"),
                    IsDefault = DynamicPropertyHelper.GetBool(r, "Domyslny")
                });
            }

            return Ok(ApiResponse<List<OperationTypeDto>>.Ok(dtos.OrderBy(o => o.Symbol).ToList()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bank operation types");
            return StatusCode(500, ApiResponse<List<OperationTypeDto>>.Error("Error retrieving bank operation types", new List<string> { ex.Message }));
        }
    }

    private string MapBankOperationType(int? typ)
    {
        return typ switch
        {
            0 => "Deposit",      // Wpłata
            1 => "Withdrawal",   // Wypłata
            2 => "Transfer",     // Przelew
            _ => "Other"
        };
    }

    #endregion

    #region Countries (Phase 2)

    /// <summary>
    /// Get all countries (państwa)
    /// </summary>
    [HttpGet("countries")]
    [ProducesResponseType(typeof(ApiResponse<List<CountryDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<CountryDto>>> GetCountries(
        [FromQuery] bool? activeOnly = true,
        [FromQuery] bool? euOnly = null)
    {
        try
        {
            var manager = _sferaService.GetManager("Panstwa");
            if (manager == null)
            {
                return StatusCode(500, ApiResponse<List<CountryDto>>.Error("Failed to get Panstwa manager"));
            }

            var allPanstwa = ((IEnumerable<dynamic>)manager.Dane.Wszystkie()).ToList();

            if (activeOnly == true)
            {
                var filtered = new List<dynamic>();
                foreach (var p in allPanstwa)
                {
                    if (DynamicPropertyHelper.GetBool(p, "Aktywne"))
                    {
                        filtered.Add(p);
                    }
                }
                allPanstwa = filtered;
            }

            if (euOnly.HasValue)
            {
                var filtered = new List<dynamic>();
                foreach (var p in allPanstwa)
                {
                    bool isEu = DynamicPropertyHelper.GetBool(p, "CzlonekUE");
                    if (isEu == euOnly.Value)
                    {
                        filtered.Add(p);
                    }
                }
                allPanstwa = filtered;
            }

            var dtos = new List<CountryDto>();
            foreach (var p in allPanstwa)
            {
                dtos.Add(new CountryDto
                {
                    Id = DynamicPropertyHelper.GetId(p),
                    Symbol = DynamicPropertyHelper.GetString(p, "Symbol") ?? string.Empty,
                    Name = DynamicPropertyHelper.GetString(p, "Nazwa"),
                    IsoCode2 = DynamicPropertyHelper.GetString(p, "KodISO"),
                    IsoCode3 = DynamicPropertyHelper.GetString(p, "KodISO3"),
                    EuCode = DynamicPropertyHelper.GetString(p, "KodUE"),
                    IsEuMember = DynamicPropertyHelper.GetBool(p, "CzlonekUE"),
                    IsActive = DynamicPropertyHelper.GetBool(p, "Aktywne"),
                    IsDefault = DynamicPropertyHelper.GetBool(p, "Domyslne")
                });
            }

            return Ok(ApiResponse<List<CountryDto>>.Ok(dtos.OrderBy(c => c.Name).ToList()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting countries");
            return StatusCode(500, ApiResponse<List<CountryDto>>.Error("Error retrieving countries", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get country by ISO code
    /// </summary>
    [HttpGet("countries/{isoCode}")]
    [ProducesResponseType(typeof(ApiResponse<CountryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CountryDto>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<CountryDto>> GetCountry(string isoCode)
    {
        try
        {
            var manager = _sferaService.GetManager("Panstwa");
            if (manager == null)
            {
                return StatusCode(500, ApiResponse<CountryDto>.Error("Failed to get Panstwa manager"));
            }

            var allPanstwa = ((IEnumerable<dynamic>)manager.Dane.Wszystkie()).ToList();
            var panstwo = allPanstwa.FirstOrDefault(p =>
                DynamicPropertyHelper.GetString(p, "KodISO") == isoCode.ToUpper() ||
                DynamicPropertyHelper.GetString(p, "KodISO3") == isoCode.ToUpper() ||
                DynamicPropertyHelper.GetString(p, "Symbol") == isoCode);

            if (panstwo == null)
            {
                return NotFound(ApiResponse<CountryDto>.Error($"Country with code '{isoCode}' not found"));
            }

            var dto = new CountryDto
            {
                Id = DynamicPropertyHelper.GetId(panstwo),
                Symbol = DynamicPropertyHelper.GetString(panstwo, "Symbol") ?? string.Empty,
                Name = DynamicPropertyHelper.GetString(panstwo, "Nazwa"),
                IsoCode2 = DynamicPropertyHelper.GetString(panstwo, "KodISO"),
                IsoCode3 = DynamicPropertyHelper.GetString(panstwo, "KodISO3"),
                EuCode = DynamicPropertyHelper.GetString(panstwo, "KodUE"),
                IsEuMember = DynamicPropertyHelper.GetBool(panstwo, "CzlonekUE"),
                IsActive = DynamicPropertyHelper.GetBool(panstwo, "Aktywne"),
                IsDefault = DynamicPropertyHelper.GetBool(panstwo, "Domyslne")
            };

            return Ok(ApiResponse<CountryDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting country {IsoCode}", isoCode);
            return StatusCode(500, ApiResponse<CountryDto>.Error("Error retrieving country", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Document Types and Statuses (Phase 2)

    /// <summary>
    /// Get available document types
    /// </summary>
    [HttpGet("document-types")]
    [ProducesResponseType(typeof(ApiResponse<List<DocumentTypeDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<DocumentTypeDto>>> GetDocumentTypes([FromQuery] string? category = null)
    {
        // Document types are mostly predefined in the system
        // This endpoint provides a reference list
        var documentTypes = new List<DocumentTypeDto>
        {
            // Sales documents
            new() { Symbol = "FS", Name = "Faktura sprzedaży", Category = "Sales", Description = "Faktura VAT sprzedaży" },
            new() { Symbol = "FSK", Name = "Korekta faktury sprzedaży", Category = "Sales", Description = "Korekta faktury VAT sprzedaży" },
            new() { Symbol = "FZ", Name = "Faktura zaliczkowa sprzedaży", Category = "Sales", Description = "Faktura zaliczkowa" },
            new() { Symbol = "PA", Name = "Paragon", Category = "Sales", Description = "Paragon fiskalny" },
            new() { Symbol = "PAK", Name = "Korekta paragonu", Category = "Sales", Description = "Korekta paragonu fiskalnego" },
            new() { Symbol = "FP", Name = "Faktura proforma", Category = "Sales", Description = "Faktura proforma" },

            // Purchase documents
            new() { Symbol = "FZ", Name = "Faktura zakupu", Category = "Purchase", Description = "Faktura VAT zakupu" },
            new() { Symbol = "FZK", Name = "Korekta faktury zakupu", Category = "Purchase", Description = "Korekta faktury VAT zakupu" },

            // Warehouse documents
            new() { Symbol = "WZ", Name = "Wydanie zewnętrzne", Category = "Warehouse", Description = "Dokument wydania towaru" },
            new() { Symbol = "PZ", Name = "Przyjęcie zewnętrzne", Category = "Warehouse", Description = "Dokument przyjęcia towaru" },
            new() { Symbol = "RW", Name = "Rozchód wewnętrzny", Category = "Warehouse", Description = "Dokument rozchodu wewnętrznego" },
            new() { Symbol = "PW", Name = "Przychód wewnętrzny", Category = "Warehouse", Description = "Dokument przychodu wewnętrznego" },
            new() { Symbol = "MM", Name = "Przesunięcie międzymagazynowe", Category = "Warehouse", Description = "Dokument przesunięcia między magazynami" },

            // Orders
            new() { Symbol = "ZK", Name = "Zamówienie od klienta", Category = "Order", Description = "Zamówienie otrzymane od klienta" },
            new() { Symbol = "ZD", Name = "Zamówienie do dostawcy", Category = "Order", Description = "Zamówienie wysłane do dostawcy" },
            new() { Symbol = "OF", Name = "Oferta", Category = "Order", Description = "Oferta handlowa" },

            // Financial documents
            new() { Symbol = "KP", Name = "Kasa przyjmie", Category = "Finance", Description = "Dowód wpłaty kasowej" },
            new() { Symbol = "KW", Name = "Kasa wyda", Category = "Finance", Description = "Dowód wypłaty kasowej" },
            new() { Symbol = "BP", Name = "Bank przyjmie", Category = "Finance", Description = "Dowód wpłaty bankowej" },
            new() { Symbol = "BW", Name = "Bank wyda", Category = "Finance", Description = "Dowód wypłaty bankowej" },
            new() { Symbol = "RK", Name = "Raport kasowy", Category = "Finance", Description = "Raport kasowy" },
            new() { Symbol = "WB", Name = "Wyciąg bankowy", Category = "Finance", Description = "Wyciąg bankowy" }
        };

        if (!string.IsNullOrEmpty(category))
        {
            documentTypes = documentTypes.Where(d =>
                d.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return Ok(ApiResponse<List<DocumentTypeDto>>.Ok(documentTypes));
    }

    /// <summary>
    /// Get document statuses
    /// </summary>
    [HttpGet("document-statuses")]
    [ProducesResponseType(typeof(ApiResponse<List<DocumentStatusDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<DocumentStatusDto>>> GetDocumentStatuses()
    {
        // Document statuses are predefined in the system
        var statuses = new List<DocumentStatusDto>
        {
            new() { Code = 0, Symbol = "BUFOR", Name = "Bufor", Description = "Dokument w buforze (robocza wersja)" },
            new() { Code = 1, Symbol = "ZATWIERDZONY", Name = "Zatwierdzony", Description = "Dokument zatwierdzony" },
            new() { Code = 2, Symbol = "CZESCIOWO_ZREALIZOWANY", Name = "Częściowo zrealizowany", Description = "Dokument częściowo zrealizowany" },
            new() { Code = 3, Symbol = "ZREALIZOWANY", Name = "Zrealizowany", Description = "Dokument w pełni zrealizowany" },
            new() { Code = 4, Symbol = "ANULOWANY", Name = "Anulowany", Description = "Dokument anulowany" },
            new() { Code = 5, Symbol = "ZAMKNIETY", Name = "Zamknięty", Description = "Dokument zamknięty" }
        };

        return Ok(ApiResponse<List<DocumentStatusDto>>.Ok(statuses));
    }

    #endregion
}

/// <summary>
/// Operation type DTO (for cash and bank operations)
/// </summary>
public class OperationTypeDto
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string OperationType { get; set; } = "Other";
    public bool IsDeposit { get; set; }
    public bool IsWithdrawal { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
}

/// <summary>
/// Country DTO
/// </summary>
public class CountryDto
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? IsoCode2 { get; set; }
    public string? IsoCode3 { get; set; }
    public string? EuCode { get; set; }
    public bool IsEuMember { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
}

/// <summary>
/// Document type DTO
/// </summary>
public class DocumentTypeDto
{
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>
/// Document status DTO
/// </summary>
public class DocumentStatusDto
{
    public int Code { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
