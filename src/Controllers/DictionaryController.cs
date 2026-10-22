using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Requests;
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
    public async Task<IActionResult> GetVatRates([FromQuery] bool? activeOnly = false)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var stawkiManager = _sferaService.GetManager("StawkiVat");
                if (stawkiManager == null) return (List<VatRateDto>?)null;

                var allStawki = DynamicPropertyHelper.SafeGetAll((object)stawkiManager);

                if (activeOnly == true)
                {
                    var filteredStawki = new List<object>();
                    foreach (var s in allStawki)
                    {
                        if (DynamicPropertyHelper.GetBool(s, "Aktywna"))
                            filteredStawki.Add(s);
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
                return (List<VatRateDto>?)dtos.OrderBy(v => v.Rate).ToList();
            });

            if (result == null)
                return StatusCode(500, ApiResponse<List<VatRateDto>>.Error("Failed to get StawkiVat manager"));

            return Ok(ApiResponse<List<VatRateDto>>.Ok(result));
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
    public async Task<IActionResult> GetVatRate(string symbol)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var stawkiManager = _sferaService.GetManager("StawkiVat");
                if (stawkiManager == null) return (found: false, managerNull: true, dto: (VatRateDto?)null);

                var allStawki = DynamicPropertyHelper.SafeGetAll((object)stawkiManager);
                var stawka = allStawki.FirstOrDefault(s => DynamicPropertyHelper.GetString(s, "Symbol") == symbol);

                if (stawka == null) return (found: false, managerNull: false, dto: (VatRateDto?)null);

                var dto = new VatRateDto
                {
                    Id = DynamicPropertyHelper.GetId(stawka),
                    Symbol = DynamicPropertyHelper.GetString(stawka, "Symbol") ?? string.Empty,
                    Name = DynamicPropertyHelper.GetString(stawka, "Nazwa"),
                    Rate = DynamicPropertyHelper.GetNullableDecimal(stawka, "Procent") ?? 0,
                    IsActive = DynamicPropertyHelper.GetBool(stawka, "Aktywna"),
                    Type = MapVatRateType(DynamicPropertyHelper.GetString(stawka, "Symbol"))
                };

                return (found: true, managerNull: false, dto: (VatRateDto?)dto);
            });

            if (result.managerNull) return StatusCode(500, ApiResponse<VatRateDto>.Error("Failed to get StawkiVat manager"));
            if (!result.found) return NotFound(ApiResponse<VatRateDto>.Error($"VAT rate '{symbol}' not found"));

            return Ok(ApiResponse<VatRateDto>.Ok(result.dto!));
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
    public async Task<IActionResult> GetUnits([FromQuery] bool? activeOnly = false)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var jednostkiManager = _sferaService.GetManager("JednostkiMiar");
                if (jednostkiManager == null) return (List<UnitOfMeasureDto>?)null;

                var allJednostki = DynamicPropertyHelper.SafeGetAll((object)jednostkiManager);

                if (activeOnly == true)
                {
                    var filteredJednostki = new List<object>();
                    foreach (var j in allJednostki)
                    {
                        if (DynamicPropertyHelper.GetBool(j, "Aktywna"))
                            filteredJednostki.Add(j);
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
                return (List<UnitOfMeasureDto>?)dtos.OrderBy(u => u.Symbol).ToList();
            });

            if (result == null)
                return StatusCode(500, ApiResponse<List<UnitOfMeasureDto>>.Error("Failed to get JednostkiMiar manager"));

            return Ok(ApiResponse<List<UnitOfMeasureDto>>.Ok(result));
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
    public async Task<IActionResult> GetUnit(string symbol)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var jednostkiManager = _sferaService.GetManager("JednostkiMiar");
                if (jednostkiManager == null) return (found: false, managerNull: true, dto: (UnitOfMeasureDto?)null);

                var allJednostki = DynamicPropertyHelper.SafeGetAll((object)jednostkiManager);
                var jednostka = allJednostki.FirstOrDefault(j => DynamicPropertyHelper.GetString(j, "Symbol") == symbol);

                if (jednostka == null) return (found: false, managerNull: false, dto: (UnitOfMeasureDto?)null);

                var dto = new UnitOfMeasureDto
                {
                    Id = DynamicPropertyHelper.GetId(jednostka),
                    Symbol = DynamicPropertyHelper.GetString(jednostka, "Symbol") ?? string.Empty,
                    Name = DynamicPropertyHelper.GetString(jednostka, "Nazwa"),
                    DecimalPlaces = DynamicPropertyHelper.GetInt(jednostka, "MiejscPoPrzecinku"),
                    IsActive = DynamicPropertyHelper.GetBool(jednostka, "Aktywna")
                };

                return (found: true, managerNull: false, dto: (UnitOfMeasureDto?)dto);
            });

            if (result.managerNull) return StatusCode(500, ApiResponse<UnitOfMeasureDto>.Error("Failed to get JednostkiMiar manager"));
            if (!result.found) return NotFound(ApiResponse<UnitOfMeasureDto>.Error($"Unit '{symbol}' not found"));

            return Ok(ApiResponse<UnitOfMeasureDto>.Ok(result.dto!));
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
    public async Task<IActionResult> GetProductGroups(
        [FromQuery] bool? activeOnly = false,
        [FromQuery] bool? hierarchical = false)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var grupyManager = _sferaService.GetManager("GrupyAsortymentu");
                if (grupyManager == null) return (List<ProductGroupDto>?)null;

                var allGrupy = DynamicPropertyHelper.SafeGetAll((object)grupyManager);

                if (activeOnly == true)
                {
                    var filteredGrupy = new List<object>();
                    foreach (var g in allGrupy)
                    {
                        if (DynamicPropertyHelper.GetBool(g, "Aktywna"))
                            filteredGrupy.Add(g);
                    }
                    allGrupy = filteredGrupy;
                }

                var dtos = new List<ProductGroupDto>();
                foreach (var g in allGrupy)
                {
                    var grupaNadrzedna = DynamicPropertyHelper.GetProperty(g, "GrupaNadrzedna");
                    var asortymenty = DynamicPropertyHelper.GetCollection((object)g, "Asortymenty");

                    dtos.Add(new ProductGroupDto
                    {
                        Id = DynamicPropertyHelper.GetId(g),
                        Symbol = DynamicPropertyHelper.GetString(g, "Symbol") ?? string.Empty,
                        Name = DynamicPropertyHelper.GetString(g, "Nazwa"),
                        Description = DynamicPropertyHelper.GetString(g, "Opis"),
                        ParentId = grupaNadrzedna != null ? DynamicPropertyHelper.GetId(grupaNadrzedna) : null,
                        ParentSymbol = grupaNadrzedna != null ? DynamicPropertyHelper.GetString(grupaNadrzedna, "Symbol") : null,
                        IsActive = DynamicPropertyHelper.GetBool(g, "Aktywna"),
                        ProductCount = asortymenty.Count
                    });
                }

                return (List<ProductGroupDto>?)dtos;
            });

            if (result == null)
                return StatusCode(500, ApiResponse<List<ProductGroupDto>>.Error("Failed to get GrupyAsortymentu manager"));

            if (hierarchical == true)
            {
                var rootGroups = result.Where(g => g.ParentId == null).ToList();
                foreach (var root in rootGroups)
                {
                    root.Children = GetChildGroups(root.Id, result);
                }
                return Ok(ApiResponse<List<ProductGroupDto>>.Ok(rootGroups));
            }

            return Ok(ApiResponse<List<ProductGroupDto>>.Ok(result.OrderBy(g => g.Symbol).ToList()));
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
                Level = 0,
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
    public async Task<IActionResult> GetProductGroup(string symbol)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var grupyManager = _sferaService.GetManager("GrupyAsortymentu");
                if (grupyManager == null) return (found: false, managerNull: true, dto: (ProductGroupDto?)null);

                var allGrupy = DynamicPropertyHelper.SafeGetAll((object)grupyManager);
                var grupa = allGrupy.FirstOrDefault(g => DynamicPropertyHelper.GetString(g, "Symbol") == symbol);

                if (grupa == null) return (found: false, managerNull: false, dto: (ProductGroupDto?)null);

                var grupaNadrzedna = DynamicPropertyHelper.GetProperty(grupa, "GrupaNadrzedna");
                var asortymenty = DynamicPropertyHelper.GetCollection((object)grupa, "Asortymenty");

                var dto = new ProductGroupDto
                {
                    Id = DynamicPropertyHelper.GetId(grupa),
                    Symbol = DynamicPropertyHelper.GetString(grupa, "Symbol") ?? string.Empty,
                    Name = DynamicPropertyHelper.GetString(grupa, "Nazwa"),
                    Description = DynamicPropertyHelper.GetString(grupa, "Opis"),
                    ParentId = grupaNadrzedna != null ? DynamicPropertyHelper.GetId(grupaNadrzedna) : null,
                    ParentSymbol = grupaNadrzedna != null ? DynamicPropertyHelper.GetString(grupaNadrzedna, "Symbol") : null,
                    IsActive = DynamicPropertyHelper.GetBool(grupa, "Aktywna"),
                    ProductCount = asortymenty.Count
                };

                return (found: true, managerNull: false, dto: (ProductGroupDto?)dto);
            });

            if (result.managerNull) return StatusCode(500, ApiResponse<ProductGroupDto>.Error("Failed to get GrupyAsortymentu manager"));
            if (!result.found) return NotFound(ApiResponse<ProductGroupDto>.Error($"Product group '{symbol}' not found"));

            return Ok(ApiResponse<ProductGroupDto>.Ok(result.dto!));
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
    public async Task<IActionResult> GetPriceLevels([FromQuery] bool? activeOnly = false)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var poziomyManager = _sferaService.GetManager("PoziomyCen");
                if (poziomyManager == null) return (List<PriceLevelDto>?)null;

                var allPoziomy = DynamicPropertyHelper.SafeGetAll((object)poziomyManager);

                if (activeOnly == true)
                {
                    var filteredPoziomy = new List<object>();
                    foreach (var p in allPoziomy)
                    {
                        if (DynamicPropertyHelper.GetBool(p, "Aktywny"))
                            filteredPoziomy.Add(p);
                    }
                    allPoziomy = filteredPoziomy;
                }

                var dtos = new List<PriceLevelDto>();
                var withEntities = new List<(PriceLevelDto Dto, object Entity)>();
                foreach (var p in allPoziomy)
                {
                    var dto = new PriceLevelDto
                    {
                        Id = DynamicPropertyHelper.GetId(p),
                        Symbol = DynamicPropertyHelper.GetString(p, "Symbol") ?? string.Empty,
                        Name = DynamicPropertyHelper.GetString(p, "Nazwa"),
                        Description = DynamicPropertyHelper.GetString(p, "Opis"),
                        IsDefault = DynamicPropertyHelper.GetBool(p, "Domyslny"),
                        IsActive = DynamicPropertyHelper.GetBool(p, "Aktywny"),
                        Priority = DynamicPropertyHelper.GetNullableInt(p, "Priorytet") ?? 0
                    };
                    dtos.Add(dto);
                    withEntities.Add((dto, (object)p));
                }
                EnrichPriceLevelsSafely(withEntities);
                return (List<PriceLevelDto>?)dtos.OrderBy(p => p.Priority).ToList();
            });

            if (result == null)
                return StatusCode(500, ApiResponse<List<PriceLevelDto>>.Error("Failed to get PoziomyCen manager"));

            return Ok(ApiResponse<List<PriceLevelDto>>.Ok(result));
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
    public async Task<IActionResult> GetPriceLevel(string symbol)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var poziomyManager = _sferaService.GetManager("PoziomyCen");
                if (poziomyManager == null) return (found: false, managerNull: true, dto: (PriceLevelDto?)null);

                var allPoziomy = DynamicPropertyHelper.SafeGetAll((object)poziomyManager);
                var poziom = allPoziomy.FirstOrDefault(p => DynamicPropertyHelper.GetString(p, "Symbol") == symbol);

                if (poziom == null) return (found: false, managerNull: false, dto: (PriceLevelDto?)null);

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
                EnrichPriceLevelsSafely(new List<(PriceLevelDto Dto, object Entity)> { (dto, (object)poziom) });

                return (found: true, managerNull: false, dto: (PriceLevelDto?)dto);
            });

            if (result.managerNull) return StatusCode(500, ApiResponse<PriceLevelDto>.Error("Failed to get PoziomyCen manager"));
            if (!result.found) return NotFound(ApiResponse<PriceLevelDto>.Error($"Price level '{symbol}' not found"));

            return Ok(ApiResponse<PriceLevelDto>.Ok(result.dto!));
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
    public async Task<IActionResult> GetPriceLists([FromQuery] bool? activeOnly = false)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var cennikiManager = _sferaService.GetManager("Cenniki");
                if (cennikiManager == null) return (List<PriceListDto>?)null;

                var allCenniki = DynamicPropertyHelper.SafeGetAll((object)cennikiManager);

                if (activeOnly == true)
                {
                    var filteredCenniki = new List<object>();
                    foreach (var c in allCenniki)
                    {
                        if (DynamicPropertyHelper.GetBool(c, "Aktywny"))
                            filteredCenniki.Add(c);
                    }
                    allCenniki = filteredCenniki;
                }

                var baseIndex = BuildBaseIndexSafely();
                var dtos = new List<PriceListDto>();
                foreach (var c in allCenniki)
                {
                    var waluta = DynamicPropertyHelper.GetProperty(c, "Waluta");
                    var pozycje = DynamicPropertyHelper.GetCollection((object)c, "Pozycje");

                    var dto = new PriceListDto
                    {
                        Id = DynamicPropertyHelper.GetId(c),
                        Symbol = DynamicPropertyHelper.GetString(c, "Symbol") ?? string.Empty,
                        Name = DynamicPropertyHelper.GetString(c, "Nazwa"),
                        Description = DynamicPropertyHelper.GetString(c, "Opis"),
                        ValidFrom = DynamicPropertyHelper.GetDateTime(c, "DataOd"),
                        ValidTo = DynamicPropertyHelper.GetDateTime(c, "DataDo"),
                        IsActive = DynamicPropertyHelper.GetBool(c, "Aktywny"),
                        CurrencySymbol = waluta != null ? DynamicPropertyHelper.GetString(waluta, "Symbol") : null,
                        ItemCount = pozycje.Count
                    };
                    EnrichPriceListSafely(dto, c, baseIndex);
                    dtos.Add(dto);
                }
                return (List<PriceListDto>?)dtos.OrderBy(c => c.Symbol).ToList();
            });

            if (result == null)
                return StatusCode(500, ApiResponse<List<PriceListDto>>.Error("Failed to get Cenniki manager"));

            return Ok(ApiResponse<List<PriceListDto>>.Ok(result));
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
    public async Task<IActionResult> GetPriceList(string symbol)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var cennikiManager = _sferaService.GetManager("Cenniki");
                if (cennikiManager == null) return (found: false, managerNull: true, dto: (PriceListDto?)null);

                var allCenniki = DynamicPropertyHelper.SafeGetAll((object)cennikiManager);
                var cennik = allCenniki.FirstOrDefault(c => DynamicPropertyHelper.GetString(c, "Symbol") == symbol);

                if (cennik == null) return (found: false, managerNull: false, dto: (PriceListDto?)null);

                var waluta = DynamicPropertyHelper.GetProperty(cennik, "Waluta");
                var pozycje = DynamicPropertyHelper.GetCollection((object)cennik, "Pozycje");

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
                    ItemCount = pozycje.Count
                };
                EnrichPriceListSafely(dto, (object)cennik, BuildBaseIndexSafely());

                return (found: true, managerNull: false, dto: (PriceListDto?)dto);
            });

            if (result.managerNull) return StatusCode(500, ApiResponse<PriceListDto>.Error("Failed to get Cenniki manager"));
            if (!result.found) return NotFound(ApiResponse<PriceListDto>.Error($"Price list '{symbol}' not found"));

            return Ok(ApiResponse<PriceListDto>.Ok(result.dto!));
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
    public async Task<IActionResult> GetPriceListItems(
        string symbol,
        [FromQuery] int? productId,
        [FromQuery] string? productSymbol,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var cennikiManager = _sferaService.GetManager("Cenniki");
                if (cennikiManager == null) return (managerNull: true, notFound: false, items: (List<PriceListItemDto>?)null, totalCount: 0);

                var allCenniki = DynamicPropertyHelper.SafeGetAll((object)cennikiManager);
                var cennik = allCenniki.FirstOrDefault(c => DynamicPropertyHelper.GetString(c, "Symbol") == symbol);

                if (cennik == null) return (managerNull: false, notFound: true, items: (List<PriceListItemDto>?)null, totalCount: 0);

                var pozycje = DynamicPropertyHelper.GetCollection((object)cennik, "Pozycje");

                if (productId.HasValue)
                {
                    var filteredPozycje = new List<object>();
                    foreach (var p in pozycje)
                    {
                        var asortyment = DynamicPropertyHelper.GetProperty(p, "Asortyment");
                        if (asortyment != null && DynamicPropertyHelper.GetId(asortyment) == productId.Value)
                            filteredPozycje.Add(p);
                    }
                    pozycje = filteredPozycje;
                }

                if (!string.IsNullOrEmpty(productSymbol))
                {
                    var filteredPozycje = new List<object>();
                    foreach (var p in pozycje)
                    {
                        var asortyment = DynamicPropertyHelper.GetProperty(p, "Asortyment");
                        if (asortyment != null && DynamicPropertyHelper.GetString(asortyment, "Symbol") == productSymbol)
                            filteredPozycje.Add(p);
                    }
                    pozycje = filteredPozycje;
                }

                var totalCount = pozycje.Count;
                var pagedPozycje = pozycje
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var cennikId = DynamicPropertyHelper.GetId(cennik);
                var items = new List<PriceListItemDto>();
                foreach (var p in pagedPozycje)
                {
                    var asortyment = DynamicPropertyHelper.GetProperty(p, "Asortyment");
                    var stawkaVat = asortyment != null ? DynamicPropertyHelper.GetProperty(asortyment, "StawkaVatSprzedazy") : null;

                    items.Add(new PriceListItemDto
                    {
                        Id = DynamicPropertyHelper.GetId(p),
                        PriceListId = cennikId,
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

                return (managerNull: false, notFound: false, items: (List<PriceListItemDto>?)items, totalCount);
            });

            if (result.managerNull)
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Cenniki manager"));

            if (result.notFound)
                return NotFound(ApiResponse<object>.Error($"Price list '{symbol}' not found"));

            return Ok(new PagedResponse<PriceListItemDto>
            {
                Data = result.items!,
                Page = page,
                PageSize = pageSize,
                TotalCount = result.totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting price list items for {Symbol}", symbol);
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving price list items", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get a price list header by id.
    /// </summary>
    /// <remarks>
    /// Price lists have no symbol in Nexo, so the id is their only stable key. The header carries the real
    /// <c>Cennik</c> members: <c>title</c>, <c>status</c>, <c>isBase</c>, <c>priceLevelId</c>, <c>mainPriceListId</c>,
    /// currency, calculation defaults (<c>dynamicPricing</c>, <c>defaultCalculationMethod</c>,
    /// <c>defaultBasePriceFunction</c>, <c>defaultBasePriceSourcePriceListId</c>, rounding) and the validity schedule.
    /// The route has its own template (<c>by-id</c>) so it never captures a numeric symbol of the legacy routes.
    /// </remarks>
    /// <param name="id">Nexo price list id (<c>Cennik.Id</c>)</param>
    [HttpGet("price-lists/by-id/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<PriceListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PriceListDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPriceListById(int id)
    {
        try
        {
            var dto = await _sferaService.ExecuteWithLockAsync(() => PriceListReader.GetPriceList(_sferaService.GetSfera(), id));

            if (dto == null) return NotFound(ApiResponse<PriceListDto>.Error($"Price list {id} not found"));

            return Ok(ApiResponse<PriceListDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting price list {PriceListId}", id);
            return StatusCode(500, ApiResponse<PriceListDto>.Error("Error retrieving price list", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create an additional price list that supplements a main price list
    /// </summary>
    /// <remarks>
    /// Creates a price list linked to the main price list <c>mainPriceListId</c> (SDK: <c>ICenniki.Utworz()</c>,
    /// <c>ICennik.UstawJakoDodatkowy</c>), as in Subiekt "Dodaj cennik dodatkowy". <c>productIds</c> (0-200) get positions
    /// with the main price list's calculation parameters (<c>DodajIPrzepiszParametryPozycjiZGlownego</c>), so they start
    /// at the main prices; change them with <c>PUT .../items</c> or <c>POST .../reprice</c>.
    ///
    /// The price list is created as defined (draft), which takes no part in pricing. <c>approve: true</c> approves it on
    /// creation as the API operator; otherwise approve it later with <c>PUT .../status</c> or in Subiekt. Main price lists
    /// (a new price level) are not created here.
    ///
    /// All-or-nothing: an unknown or closed main price list, an unknown product, a product without a main position in the
    /// main price list, or a refused save return 400, and nothing is created.
    ///
    /// Example: <c>{"title": "Promocja listopad", "mainPriceListId": 1, "productIds": [105003, 105004]}</c>
    /// </remarks>
    /// <param name="request">Title, main price list and products</param>
    [HttpPost("price-lists")]
    [ProducesResponseType(typeof(ApiResponse<PriceListDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePriceList([FromBody] CreatePriceListRequest request)
    {
        var errors = PriceListCreator.ValidateCreate(request);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<object>.Error("Invalid price list", errors));

        try
        {
            var (outcome, dto) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var sfera = _sferaService.GetSfera();
                var created = PriceListCreator.Create(sfera, _sferaService.GetCurrentOperatorLogin(), request);
                return (created, created.PriceListId is int newId ? PriceListReader.GetPriceList(sfera, newId) : null);
            });

            if (outcome.Errors.Count > 0 || outcome.PriceListId == null)
                return BadRequest(ApiResponse<object>.Error("Price list not created", outcome.Errors));

            _logger.LogInformation("Created price list {PriceListId} supplementing {MainPriceListId} with {Positions} positions (approved: {Approved})",
                outcome.PriceListId, request.MainPriceListId, outcome.PositionCount, request.Approve);
            return CreatedAtAction(nameof(GetPriceListById), new { id = outcome.PriceListId },
                ApiResponse<PriceListDto?>.Ok(dto, $"Price list created with {outcome.PositionCount} positions"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating a price list supplementing {MainPriceListId}", request.MainPriceListId);
            return StatusCode(500, ApiResponse<object>.Error("Error creating the price list"));
        }
    }

    /// <summary>
    /// Change the status of an additional price list
    /// </summary>
    /// <remarks>
    /// <c>defined</c> (takes no part in pricing), <c>approved</c> (used for pricing) or <c>closed</c>, set as the API
    /// operator (SDK: <c>ICennik.UstawStatus</c>, which also stamps the approval or closing date). Main price lists are
    /// refused with 400. The current status saves nothing. The response is the price list header.
    /// </remarks>
    /// <param name="id">Nexo price list id (<c>Cennik.Id</c>)</param>
    /// <param name="request">The new status</param>
    [HttpPut("price-lists/by-id/{id:int}/status")]
    [ProducesResponseType(typeof(ApiResponse<PriceListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPriceListStatus(int id, [FromBody] SetPriceListStatusRequest request)
    {
        var errors = PriceListCreator.ValidateStatus(request);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<object>.Error("Invalid price list status", errors));

        try
        {
            var (outcome, dto) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var sfera = _sferaService.GetSfera();
                var result = PriceListCreator.SetStatus(sfera, _sferaService.GetCurrentOperatorLogin(), id, request.Status!);
                return (result, result.PriceListFound && result.Errors.Count == 0 ? PriceListReader.GetPriceList(sfera, id) : null);
            });

            if (!outcome.PriceListFound)
                return NotFound(ApiResponse<object>.Error($"Price list {id} not found"));

            if (outcome.Errors.Count > 0)
                return BadRequest(ApiResponse<object>.Error("Price list status not changed", outcome.Errors));

            _logger.LogInformation("Price list {PriceListId} status {Status} ({Result})", id, request.Status, outcome.Changed ? "updated" : "unchanged");
            return Ok(ApiResponse<PriceListDto?>.Ok(dto, outcome.Changed ? "Status changed" : "Status unchanged"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing the status of price list {PriceListId}", id);
            return StatusCode(500, ApiResponse<object>.Error("Error changing the price list status"));
        }
    }

    /// <summary>
    /// Get price list positions by price list id (read through <c>ICennik.Pozycje</c>).
    /// </summary>
    /// <remarks>
    /// Each product has one main position (<c>isMain</c>) and optional quantity tiers (<c>isMain = false</c>,
    /// <c>minQuantity</c> &gt; 0); a main position reports its tier count in <c>quantityTierCount</c>.
    /// Positions carry the sales VAT rate (<c>vatRateSymbol</c>, <c>vatRatePercent</c>), base price, calculation
    /// parameter/method, rounding, <c>updatedAt</c> and supplier/manufacturer ids.
    ///
    /// With <c>productIds</c> (comma-separated Nexo product ids, at most 200) only those products are looked up and
    /// <c>missingProductIds</c> lists the ids without a position. Without it the whole price list is returned in pages
    /// ordered by product id, then minimum quantity. The response also carries the price list header (<c>priceList</c>)
    /// read in the same SDK call.
    /// </remarks>
    /// <param name="id">Nexo price list id (<c>Cennik.Id</c>)</param>
    /// <param name="productIds">Optional comma-separated Nexo product ids (max 200)</param>
    /// <param name="mainOnly">Return main positions only (quantity tiers are still counted)</param>
    /// <param name="activeProductsOnly">Without <c>productIds</c>: skip deactivated products (<c>WszystkieAktywne</c>)</param>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Page size (1-1000, default 100)</param>
    [HttpGet("price-lists/by-id/{id:int}/items")]
    [ProducesResponseType(typeof(PriceListPositionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPriceListPositionsById(
        int id,
        [FromQuery] string? productIds,
        [FromQuery] bool mainOnly = false,
        [FromQuery] bool activeProductsOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        if (page < 1)
            return BadRequest(ApiResponse<object>.Error("page must be >= 1"));

        if (pageSize < 1 || pageSize > PriceListReader.MaxPageSize)
            return BadRequest(ApiResponse<object>.Error($"pageSize must be between 1 and {PriceListReader.MaxPageSize}"));

        if (!TryParseProductIds(productIds, out var ids, out var error))
            return BadRequest(ApiResponse<object>.Error(error!));

        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() => PriceListReader.GetPositions(
                _sferaService.GetSfera(), id, ids, mainOnly, activeProductsOnly, page, pageSize));

            if (result == null)
                return NotFound(ApiResponse<object>.Error($"Price list {id} not found"));

            return Ok(new PriceListPositionsResponse
            {
                Data = result.Items,
                Page = page,
                PageSize = pageSize,
                TotalCount = result.TotalCount,
                PriceList = result.PriceList,
                MissingProductIds = result.MissingProductIds,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting positions of price list {PriceListId}", id);
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving price list positions", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Set sales prices of main price list positions
    /// </summary>
    /// <remarks>
    /// Sets the gross (<c>priceGross</c>) or net (<c>priceNet</c>) price of the main position (<c>Glowna</c>, no quantity
    /// tier) of each product through the price list business object (<c>ICennik.Pozycje</c>). Nexo derives the other
    /// price and the calculation parameter (markup / margin / profit); the calculation method, rounding function and
    /// quantity tiers stay as they are, so a later base price change recalculates the position like a price set in Subiekt.
    ///
    /// All-or-nothing: unknown products, a missing or ambiguous main position, or a kit position priced from its
    /// components (<c>WyliczCeneKompletuZeSkladnikow</c>) return 400 before anything is edited, and every edit is saved
    /// with one <c>ICennik.Zapisz()</c>. Prices are rounded to the currency precision. 1-200 items per request.
    ///
    /// Conditional write: an item may carry <c>expectedPriceGross</c> and/or <c>expectedPriceNet</c>, the price the client
    /// last read. When any of them differs from the current price, nothing is saved and the request returns 409 with one
    /// message per changed product. Example:
    /// <c>{"items": [{"productId": 105003, "priceGross": 38.00, "expectedPriceGross": 39.99}]}</c>
    /// </remarks>
    /// <param name="id">Nexo price list id (<c>Cennik.Id</c>)</param>
    /// <param name="request">Products and their new prices</param>
    [HttpPut("price-lists/by-id/{id:int}/items")]
    [ProducesResponseType(typeof(ApiResponse<List<PriceListPriceChangeDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SetPriceListPrices(int id, [FromBody] SetPriceListPricesRequest request)
    {
        var errors = PriceListWriter.Validate(request);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<object>.Error("Invalid price request", errors));

        try
        {
            var outcome = await _sferaService.ExecuteWithLockAsync(() =>
                PriceListWriter.SetMainPrices(_sferaService.GetSfera(), id, request.Items));

            if (!outcome.PriceListFound)
                return NotFound(ApiResponse<object>.Error($"Price list {id} not found"));

            if (outcome.Errors.Count > 0)
                return BadRequest(ApiResponse<object>.Error("Prices not saved", outcome.Errors));

            if (outcome.Conflicts.Count > 0)
                return Conflict(ApiResponse<object>.Error("Prices not saved: the current price differs from the expected price", outcome.Conflicts));

            _logger.LogInformation("Set {Updated} of {Total} prices in price list {PriceListId}",
                outcome.Changes.Count(c => c.Status == PriceListWriter.Updated), outcome.Changes.Count, id);
            return Ok(ApiResponse<List<PriceListPriceChangeDto>>.Ok(outcome.Changes, "Prices saved"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting prices in price list {PriceListId}", id);
            return StatusCode(500, ApiResponse<object>.Error("Error saving price list prices"));
        }
    }

    /// <summary>
    /// Price change (Subiekt: Przecena) of price list positions
    /// </summary>
    /// <remarks>
    /// Runs Nexo's price change (<c>ICennik.Przecen</c>) on the positions of the given products: a percent or amount
    /// decrease or increase (<c>kind</c>, <c>value</c>; percent as a fraction, 0.10 = 10%), an optional rounding function
    /// (<c>roundingFunction</c>, the names positions report, e.g. <c>WyrownywanieDoJednostek</c>) and an optional signed
    /// correction after rounding (<c>priceCorrection</c>, e.g. -0.01 for .99 endings). Quantity tiers are repriced too
    /// unless <c>mainOnly</c>. Nexo recalculates the calculation parameter as after a price change in Subiekt.
    ///
    /// All-or-nothing: a closed price list, unknown products, products without a position, kit positions priced from
    /// their components, or any position Nexo cannot reprice return 400, and nothing is saved. Otherwise all positions are
    /// saved with one <c>ICennik.Zapisz()</c>. The response lists every position with its old and new prices.
    ///
    /// Example: <c>{"productIds": [105003, 105004], "kind": "decreasePercent", "value": 0.10,
    /// "roundingFunction": "WyrownywanieDoJednostek", "priceCorrection": -0.01}</c>
    /// </remarks>
    /// <param name="id">Nexo price list id (<c>Cennik.Id</c>)</param>
    /// <param name="request">Products and the price change</param>
    [HttpPost("price-lists/by-id/{id:int}/reprice")]
    [ProducesResponseType(typeof(ApiResponse<List<PriceListRepriceChangeDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RepricePriceList(int id, [FromBody] RepricePriceListRequest request)
    {
        var errors = PriceListWriter.ValidateReprice(request);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<object>.Error("Invalid price change request", errors));

        try
        {
            var outcome = await _sferaService.ExecuteWithLockAsync(() =>
                PriceListWriter.Reprice(_sferaService.GetSfera(), id, request));

            if (!outcome.PriceListFound)
                return NotFound(ApiResponse<object>.Error($"Price list {id} not found"));

            if (outcome.Errors.Count > 0)
                return BadRequest(ApiResponse<object>.Error("Price change not saved", outcome.Errors));

            _logger.LogInformation("Repriced {Updated} of {Total} positions in price list {PriceListId} ({Kind} {Value})",
                outcome.Changes.Count(c => c.Status == PriceListWriter.Updated), outcome.Changes.Count, id, request.Kind, request.Value);
            return Ok(ApiResponse<List<PriceListRepriceChangeDto>>.Ok(outcome.Changes, "Price change saved"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error repricing price list {PriceListId}", id);
            return StatusCode(500, ApiResponse<object>.Error("Error saving the price change"));
        }
    }

    /// <summary>Parses "1,2,3" (commas, semicolons or spaces) into distinct positive ids, at most <see cref="PriceListReader.MaxProductIds"/>.</summary>
    private static bool TryParseProductIds(string? raw, out List<int>? ids, out string? error)
    {
        ids = null;
        error = null;

        if (string.IsNullOrWhiteSpace(raw)) return true;

        var parsed = new List<int>();
        foreach (var token in raw.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(token, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var value) || value <= 0)
            {
                error = $"productIds contains an invalid id: '{token}'";
                return false;
            }

            parsed.Add(value);
        }

        parsed = parsed.Distinct().ToList();
        if (parsed.Count > PriceListReader.MaxProductIds)
        {
            error = $"productIds accepts at most {PriceListReader.MaxProductIds} ids, got {parsed.Count}";
            return false;
        }

        ids = parsed.Count > 0 ? parsed : null;
        return true;
    }

    /// <summary>
    /// The legacy endpoints must keep answering even if a new header field fails to load, so enrichment never throws.
    /// </summary>
    private void EnrichPriceListSafely(PriceListDto dto, object entity, IReadOnlyDictionary<int, List<int>> baseIndex)
    {
        try
        {
            PriceListReader.EnrichPriceList(dto, entity, baseIndex);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not enrich price list {PriceListId}", dto.Id);
        }
    }

    private IReadOnlyDictionary<int, List<int>> BuildBaseIndexSafely()
    {
        try
        {
            return PriceListReader.BuildBaseIndex(_sferaService.GetSfera());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not build the base price list index");
            return new Dictionary<int, List<int>>();
        }
    }

    private void EnrichPriceLevelsSafely(List<(PriceLevelDto Dto, object Entity)> levels)
    {
        try
        {
            PriceListReader.EnrichPriceLevels(_sferaService.GetSfera(), levels);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not enrich price levels");
        }
    }

    #endregion

    #region Currencies

    /// <summary>
    /// Get all currencies
    /// </summary>
    [HttpGet("currencies")]
    [ProducesResponseType(typeof(ApiResponse<List<CurrencyDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrencies([FromQuery] bool? activeOnly = false)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var walutyManager = _sferaService.GetManager("Waluty");
                if (walutyManager == null) return (List<CurrencyDto>?)null;

                var allWaluty = DynamicPropertyHelper.SafeGetAll((object)walutyManager);

                if (activeOnly == true)
                {
                    var filteredWaluty = new List<object>();
                    foreach (var w in allWaluty)
                    {
                        if (DynamicPropertyHelper.GetBool(w, "Aktywna"))
                            filteredWaluty.Add(w);
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
                return (List<CurrencyDto>?)dtos.OrderBy(c => c.Symbol).ToList();
            });

            if (result == null)
                return StatusCode(500, ApiResponse<List<CurrencyDto>>.Error("Failed to get Waluty manager"));

            return Ok(ApiResponse<List<CurrencyDto>>.Ok(result));
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
    public async Task<IActionResult> GetExchangeRates(
        string symbol,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int limit = 30)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var walutyManager = _sferaService.GetManager("Waluty");
                if (walutyManager == null) return (found: false, managerNull: true, rates: (List<ExchangeRateDto>?)null);

                var allWaluty = DynamicPropertyHelper.SafeGetAll((object)walutyManager);
                var waluta = allWaluty.FirstOrDefault(w => DynamicPropertyHelper.GetString(w, "Symbol") == symbol);

                if (waluta == null) return (found: false, managerNull: false, rates: (List<ExchangeRateDto>?)null);

                var kursy = DynamicPropertyHelper.GetCollection((object)waluta, "Kursy");

                var filteredKursy = new List<object>();
                foreach (var k in kursy)
                {
                    var data = DynamicPropertyHelper.GetDateTime(k, "Data");
                    if (dateFrom.HasValue && (!data.HasValue || data.Value < dateFrom.Value))
                        continue;
                    if (dateTo.HasValue && (!data.HasValue || data.Value > dateTo.Value))
                        continue;
                    filteredKursy.Add(k);
                }

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

                rates = rates
                    .OrderByDescending(r => r.Date ?? DateTime.MinValue)
                    .Take(limit)
                    .ToList();

                return (found: true, managerNull: false, rates: (List<ExchangeRateDto>?)rates);
            });

            if (result.managerNull) return StatusCode(500, ApiResponse<List<ExchangeRateDto>>.Error("Failed to get Waluty manager"));
            if (!result.found) return NotFound(ApiResponse<List<ExchangeRateDto>>.Error($"Currency '{symbol}' not found"));

            return Ok(ApiResponse<List<ExchangeRateDto>>.Ok(result.rates!));
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
    public async Task<IActionResult> GetExchangeRate(
        string symbol,
        [FromQuery] DateTime? date)
    {
        try
        {
            var targetDate = date ?? DateTime.Today;

            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var walutyManager = _sferaService.GetManager("Waluty");
                if (walutyManager == null) return (found: false, managerNull: true, noRate: false, dto: (ExchangeRateDto?)null);

                var allWaluty = DynamicPropertyHelper.SafeGetAll((object)walutyManager);
                var waluta = allWaluty.FirstOrDefault(w => DynamicPropertyHelper.GetString(w, "Symbol") == symbol);

                if (waluta == null) return (found: false, managerNull: false, noRate: false, dto: (ExchangeRateDto?)null);

                var kursy = DynamicPropertyHelper.GetCollection((object)waluta, "Kursy");

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

                if (latestKurs == null) return (found: true, managerNull: false, noRate: true, dto: (ExchangeRateDto?)null);

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

                return (found: true, managerNull: false, noRate: false, dto: (ExchangeRateDto?)dto);
            });

            if (result.managerNull) return StatusCode(500, ApiResponse<ExchangeRateDto>.Error("Failed to get Waluty manager"));
            if (!result.found) return NotFound(ApiResponse<ExchangeRateDto>.Error($"Currency '{symbol}' not found"));
            if (result.noRate) return NotFound(ApiResponse<ExchangeRateDto>.Error($"No exchange rate found for {symbol}"));

            return Ok(ApiResponse<ExchangeRateDto>.Ok(result.dto!));
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
    public async Task<IActionResult> GetPaymentMethods([FromQuery] bool? activeOnly = false)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var formyManager = _sferaService.GetManager("FormyPlatnosci");
                if (formyManager == null) return (List<PaymentMethodDto>?)null;

                var allFormy = DynamicPropertyHelper.SafeGetAll((object)formyManager);

                if (activeOnly == true)
                {
                    var filteredFormy = new List<object>();
                    foreach (var f in allFormy)
                    {
                        if (DynamicPropertyHelper.GetBool(f, "Aktywna"))
                            filteredFormy.Add(f);
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
                return (List<PaymentMethodDto>?)dtos.OrderBy(p => p.Symbol).ToList();
            });

            if (result == null)
                return StatusCode(500, ApiResponse<List<PaymentMethodDto>>.Error("Failed to get FormyPlatnosci manager"));

            return Ok(ApiResponse<List<PaymentMethodDto>>.Ok(result));
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
            5 => PaymentMethodType.ElectronicPayment,
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
    public async Task<IActionResult> GetCashOperationTypes([FromQuery] bool? activeOnly = false)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("RodzajeOperacjiKasowych");
                if (manager == null) return (List<OperationTypeDto>?)null;

                var allRodzaje = DynamicPropertyHelper.SafeGetAll((object)manager);

                if (activeOnly == true)
                {
                    var filtered = new List<object>();
                    foreach (var r in allRodzaje)
                    {
                        if (DynamicPropertyHelper.GetBool(r, "Aktywny"))
                            filtered.Add(r);
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

                return (List<OperationTypeDto>?)dtos.OrderBy(o => o.Symbol).ToList();
            });

            if (result == null)
                return StatusCode(500, ApiResponse<List<OperationTypeDto>>.Error("Failed to get RodzajeOperacjiKasowych manager"));

            return Ok(ApiResponse<List<OperationTypeDto>>.Ok(result));
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
    public async Task<IActionResult> GetBankOperationTypes([FromQuery] bool? activeOnly = false)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("RodzajeOperacjiBankowych");
                if (manager == null) return (List<OperationTypeDto>?)null;

                var allRodzaje = DynamicPropertyHelper.SafeGetAll((object)manager);

                if (activeOnly == true)
                {
                    var filtered = new List<object>();
                    foreach (var r in allRodzaje)
                    {
                        if (DynamicPropertyHelper.GetBool(r, "Aktywny"))
                            filtered.Add(r);
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

                return (List<OperationTypeDto>?)dtos.OrderBy(o => o.Symbol).ToList();
            });

            if (result == null)
                return StatusCode(500, ApiResponse<List<OperationTypeDto>>.Error("Failed to get RodzajeOperacjiBankowych manager"));

            return Ok(ApiResponse<List<OperationTypeDto>>.Ok(result));
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
    public async Task<IActionResult> GetCountries(
        [FromQuery] bool? activeOnly = false,
        [FromQuery] bool? euOnly = null)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("Panstwa");
                if (manager == null) return (List<CountryDto>?)null;

                var allPanstwa = DynamicPropertyHelper.SafeGetAll((object)manager);

                if (activeOnly == true)
                {
                    var filtered = new List<object>();
                    foreach (var p in allPanstwa)
                    {
                        if (DynamicPropertyHelper.GetBool(p, "Aktywne"))
                            filtered.Add(p);
                    }
                    allPanstwa = filtered;
                }

                if (euOnly.HasValue)
                {
                    var filtered = new List<object>();
                    foreach (var p in allPanstwa)
                    {
                        bool isEu = DynamicPropertyHelper.GetBool(p, "CzlonekUE");
                        if (isEu == euOnly.Value)
                            filtered.Add(p);
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

                return (List<CountryDto>?)dtos.OrderBy(c => c.Name).ToList();
            });

            if (result == null)
                return StatusCode(500, ApiResponse<List<CountryDto>>.Error("Failed to get Panstwa manager"));

            return Ok(ApiResponse<List<CountryDto>>.Ok(result));
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
    public async Task<IActionResult> GetCountry(string isoCode)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var manager = _sferaService.GetManager("Panstwa");
                if (manager == null) return (found: false, managerNull: true, dto: (CountryDto?)null);

                var allPanstwa = DynamicPropertyHelper.SafeGetAll((object)manager);
                var panstwo = allPanstwa.FirstOrDefault(p =>
                    DynamicPropertyHelper.GetString(p, "KodISO") == isoCode.ToUpper() ||
                    DynamicPropertyHelper.GetString(p, "KodISO3") == isoCode.ToUpper() ||
                    DynamicPropertyHelper.GetString(p, "Symbol") == isoCode);

                if (panstwo == null) return (found: false, managerNull: false, dto: (CountryDto?)null);

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

                return (found: true, managerNull: false, dto: (CountryDto?)dto);
            });

            if (result.managerNull) return StatusCode(500, ApiResponse<CountryDto>.Error("Failed to get Panstwa manager"));
            if (!result.found) return NotFound(ApiResponse<CountryDto>.Error($"Country with code '{isoCode}' not found"));

            return Ok(ApiResponse<CountryDto>.Ok(result.dto!));
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
