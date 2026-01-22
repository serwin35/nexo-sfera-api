using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Requests;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Product Templates (Szablony Asortymentu) management endpoints
/// </summary>
[ApiController]
[Route("api/product-templates")]
[Authorize]
[Tags("Product Templates")]
public class ProductTemplatesController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<ProductTemplatesController> _logger;

    public ProductTemplatesController(ISferaService sferaService, ILogger<ProductTemplatesController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get all product templates with optional filtering
    /// </summary>
    [HttpGet]
    public ActionResult<PagedResponse<ProductTemplateListItemDto>> GetProductTemplates([FromQuery] ProductTemplateQueryRequest query)
    {
        try
        {
            var szablony = _sferaService.GetManager("SzablonyAsortymentu");
            if (szablony == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get SzablonyAsortymentu manager"));
            }

            var allSzablony = new List<dynamic>();
            foreach (var s in szablony.Dane.Wszystkie())
            {
                allSzablony.Add(s);
            }

            // Apply filters
            var filteredList = new List<dynamic>();
            foreach (var s in allSzablony)
            {
                // Deleted filter
                if (!query.IncludeDeleted)
                {
                    var isDeleted = DynamicPropertyHelper.GetNullableBool(s, "Usuniety") ?? false;
                    if (isDeleted) continue;
                }

                // Type filter
                if (query.Type.HasValue)
                {
                    var typ = DynamicPropertyHelper.GetNullableInt(s, "Typ");
                    if (typ != query.Type.Value) continue;
                }

                // Search filter
                if (!string.IsNullOrEmpty(query.Search))
                {
                    var searchLower = query.Search.ToLower();
                    var symbol = (DynamicPropertyHelper.GetString(s, "Symbol") ?? "").ToLower();
                    var nazwa = (DynamicPropertyHelper.GetString(s, "Nazwa") ?? "").ToLower();

                    if (!symbol.Contains(searchLower) && !nazwa.Contains(searchLower))
                        continue;
                }

                filteredList.Add(s);
            }

            var totalCount = filteredList.Count;
            var pagedItems = filteredList
                .OrderBy(s => DynamicPropertyHelper.GetString(s, "Nazwa"))
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToList();

            var items = new List<ProductTemplateListItemDto>();
            foreach (var s in pagedItems)
            {
                items.Add(MapToListItemDto(s));
            }

            var response = new PagedResponse<ProductTemplateListItemDto>
            {
                Data = items,
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = totalCount
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product templates");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving product templates", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get product template by ID
    /// </summary>
    [HttpGet("{id}")]
    public ActionResult<ApiResponse<ProductTemplateDto>> GetProductTemplate(int id)
    {
        try
        {
            var szablony = _sferaService.GetManager("SzablonyAsortymentu");
            if (szablony == null)
            {
                return StatusCode(500, ApiResponse<ProductTemplateDto>.Error("Failed to get SzablonyAsortymentu manager"));
            }

            dynamic? szablon = null;
            foreach (var s in szablony.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetId(s) == id)
                {
                    szablon = s;
                    break;
                }
            }

            if (szablon == null)
            {
                return NotFound(ApiResponse<ProductTemplateDto>.Error($"Product template with ID {id} not found"));
            }

            return Ok(ApiResponse<ProductTemplateDto>.Ok(MapToDto(szablon)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product template {Id}", id);
            return StatusCode(500, ApiResponse<ProductTemplateDto>.Error("Error retrieving product template", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get product template by symbol
    /// </summary>
    [HttpGet("by-symbol/{symbol}")]
    public ActionResult<ApiResponse<ProductTemplateDto>> GetProductTemplateBySymbol(string symbol)
    {
        try
        {
            var szablony = _sferaService.GetManager("SzablonyAsortymentu");
            if (szablony == null)
            {
                return StatusCode(500, ApiResponse<ProductTemplateDto>.Error("Failed to get SzablonyAsortymentu manager"));
            }

            dynamic? szablon = null;
            foreach (var s in szablony.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetString(s, "Symbol") == symbol)
                {
                    szablon = s;
                    break;
                }
            }

            if (szablon == null)
            {
                return NotFound(ApiResponse<ProductTemplateDto>.Error($"Product template with symbol {symbol} not found"));
            }

            return Ok(ApiResponse<ProductTemplateDto>.Ok(MapToDto(szablon)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product template by symbol {Symbol}", symbol);
            return StatusCode(500, ApiResponse<ProductTemplateDto>.Error("Error retrieving product template", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create a new product template
    /// </summary>
    [HttpPost]
    public ActionResult<ApiResponse<ProductTemplateDto>> CreateProductTemplate([FromBody] CreateProductTemplateRequest request)
    {
        try
        {
            var szablony = _sferaService.GetManager("SzablonyAsortymentu");
            if (szablony == null)
            {
                return StatusCode(500, ApiResponse<ProductTemplateDto>.Error("Failed to get SzablonyAsortymentu manager"));
            }

            // Check if symbol already exists
            foreach (var s in szablony.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetString(s, "Symbol") == request.Symbol)
                {
                    return BadRequest(ApiResponse<ProductTemplateDto>.Error($"Product template with symbol {request.Symbol} already exists"));
                }
            }

            using (var szablon = szablony.Utworz())
            {
                dynamic dane = szablon.Dane;
                dane.Symbol = request.Symbol;
                dane.Nazwa = request.Name;
                dane.Typ = request.Type;

                if (!string.IsNullOrEmpty(request.Description))
                {
                    try { dane.Opis = request.Description; } catch { }
                }

                // Set unit
                if (request.UnitId.HasValue)
                {
                    var jednostki = _sferaService.GetManager("Jednostki");
                    if (jednostki != null)
                    {
                        foreach (var j in jednostki.Dane.Wszystkie())
                        {
                            if (DynamicPropertyHelper.GetId(j) == request.UnitId.Value)
                            {
                                try { dane.Jednostka = j; } catch { }
                                break;
                            }
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(request.UnitSymbol))
                {
                    var jednostki = _sferaService.GetManager("Jednostki");
                    if (jednostki != null)
                    {
                        foreach (var j in jednostki.Dane.Wszystkie())
                        {
                            if (DynamicPropertyHelper.GetString(j, "Symbol") == request.UnitSymbol)
                            {
                                try { dane.Jednostka = j; } catch { }
                                break;
                            }
                        }
                    }
                }

                // Set VAT rate
                if (request.VatRateId.HasValue)
                {
                    var stawkiVat = _sferaService.GetManager("StawkiVat");
                    if (stawkiVat != null)
                    {
                        foreach (var sv in stawkiVat.Dane.Wszystkie())
                        {
                            if (DynamicPropertyHelper.GetId(sv) == request.VatRateId.Value)
                            {
                                try { dane.StawkaVat = sv; } catch { }
                                break;
                            }
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(request.VatRateSymbol))
                {
                    var stawkiVat = _sferaService.GetManager("StawkiVat");
                    if (stawkiVat != null)
                    {
                        foreach (var sv in stawkiVat.Dane.Wszystkie())
                        {
                            if (DynamicPropertyHelper.GetString(sv, "Symbol") == request.VatRateSymbol)
                            {
                                try { dane.StawkaVat = sv; } catch { }
                                break;
                            }
                        }
                    }
                }

                // Set product type
                if (request.ProductTypeId.HasValue)
                {
                    try { dane.TypAsortymentuId = request.ProductTypeId.Value; } catch { }
                }

                // Set regulatory fields
                try { dane.OdwrotneObciazenie = request.ReverseCharge; } catch { }
                try { dane.PodzielonaPlatnosc = request.SplitPayment; } catch { }

                if (request.JpkProductGroup.HasValue)
                {
                    try { dane.GrupaTowarowJPKId = request.JpkProductGroup.Value; } catch { }
                }

                // Intrastat
                try { dane.UwzgledniajWIntrastat = request.IncludeInIntrastat; } catch { }

                if (!string.IsNullOrEmpty(request.IntrastatDescription))
                {
                    try { dane.OpisIntrastat = request.IntrastatDescription; } catch { }
                }

                // Messages
                try { dane.WyswietlajKomunikat = request.ShowMessage; } catch { }

                if (!string.IsNullOrEmpty(request.DefaultMessageContent))
                {
                    try { dane.DomyslnaTrescKomunikatu = request.DefaultMessageContent; } catch { }
                }

                // Reference product
                if (request.ReferenceProductId.HasValue)
                {
                    var asortymenty = _sferaService.GetManager("Asortymenty");
                    if (asortymenty != null)
                    {
                        foreach (var a in asortymenty.Dane.Wszystkie())
                        {
                            if (DynamicPropertyHelper.GetId(a) == request.ReferenceProductId.Value)
                            {
                                try { dane.AsortymentReferencyjny = a; } catch { }
                                break;
                            }
                        }
                    }
                }

                if ((bool)szablon.Zapisz())
                {
                    _logger.LogInformation("Created product template {Symbol}", request.Symbol);
                    return CreatedAtAction(
                        nameof(GetProductTemplate),
                        new { id = DynamicPropertyHelper.GetId(dane) },
                        ApiResponse<ProductTemplateDto>.Ok(MapToDto(dane), "Product template created successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(szablon);
                    return BadRequest(ApiResponse<ProductTemplateDto>.Error("Failed to create product template", errors));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating product template");
            return StatusCode(500, ApiResponse<ProductTemplateDto>.Error("Error creating product template", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Update an existing product template
    /// </summary>
    [HttpPut("{id}")]
    public ActionResult<ApiResponse<ProductTemplateDto>> UpdateProductTemplate(int id, [FromBody] UpdateProductTemplateRequest request)
    {
        try
        {
            var szablony = _sferaService.GetManager("SzablonyAsortymentu");
            if (szablony == null)
            {
                return StatusCode(500, ApiResponse<ProductTemplateDto>.Error("Failed to get SzablonyAsortymentu manager"));
            }

            dynamic? szablonDane = null;
            foreach (var s in szablony.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetId(s) == id)
                {
                    szablonDane = s;
                    break;
                }
            }

            if (szablonDane == null)
            {
                return NotFound(ApiResponse<ProductTemplateDto>.Error($"Product template with ID {id} not found"));
            }

            using (var szablon = szablony.Znajdz(szablonDane))
            {
                if (szablon == null)
                {
                    return NotFound(ApiResponse<ProductTemplateDto>.Error($"Product template with ID {id} not found"));
                }

                dynamic dane = szablon.Dane;

                if (!string.IsNullOrEmpty(request.Name))
                {
                    dane.Nazwa = request.Name;
                }

                if (!string.IsNullOrEmpty(request.Description))
                {
                    try { dane.Opis = request.Description; } catch { }
                }

                // Update VAT rate
                if (request.VatRateId.HasValue)
                {
                    var stawkiVat = _sferaService.GetManager("StawkiVat");
                    if (stawkiVat != null)
                    {
                        foreach (var sv in stawkiVat.Dane.Wszystkie())
                        {
                            if (DynamicPropertyHelper.GetId(sv) == request.VatRateId.Value)
                            {
                                try { dane.StawkaVat = sv; } catch { }
                                break;
                            }
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(request.VatRateSymbol))
                {
                    var stawkiVat = _sferaService.GetManager("StawkiVat");
                    if (stawkiVat != null)
                    {
                        foreach (var sv in stawkiVat.Dane.Wszystkie())
                        {
                            if (DynamicPropertyHelper.GetString(sv, "Symbol") == request.VatRateSymbol)
                            {
                                try { dane.StawkaVat = sv; } catch { }
                                break;
                            }
                        }
                    }
                }

                // Update regulatory fields
                if (request.ReverseCharge.HasValue)
                {
                    try { dane.OdwrotneObciazenie = request.ReverseCharge.Value; } catch { }
                }

                if (request.SplitPayment.HasValue)
                {
                    try { dane.PodzielonaPlatnosc = request.SplitPayment.Value; } catch { }
                }

                if (request.JpkProductGroup.HasValue)
                {
                    try { dane.GrupaTowarowJPKId = request.JpkProductGroup.Value; } catch { }
                }

                // Intrastat
                if (request.IncludeInIntrastat.HasValue)
                {
                    try { dane.UwzgledniajWIntrastat = request.IncludeInIntrastat.Value; } catch { }
                }

                if (!string.IsNullOrEmpty(request.IntrastatDescription))
                {
                    try { dane.OpisIntrastat = request.IntrastatDescription; } catch { }
                }

                // Messages
                if (request.ShowMessage.HasValue)
                {
                    try { dane.WyswietlajKomunikat = request.ShowMessage.Value; } catch { }
                }

                if (!string.IsNullOrEmpty(request.DefaultMessageContent))
                {
                    try { dane.DomyslnaTrescKomunikatu = request.DefaultMessageContent; } catch { }
                }

                if ((bool)szablon.Zapisz())
                {
                    _logger.LogInformation("Updated product template {Id}", id);
                    return Ok(ApiResponse<ProductTemplateDto>.Ok(MapToDto(dane), "Product template updated successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(szablon);
                    return BadRequest(ApiResponse<ProductTemplateDto>.Error("Failed to update product template", errors));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating product template {Id}", id);
            return StatusCode(500, ApiResponse<ProductTemplateDto>.Error("Error updating product template", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Delete a product template
    /// </summary>
    [HttpDelete("{id}")]
    public ActionResult<ApiResponse<bool>> DeleteProductTemplate(int id)
    {
        try
        {
            var szablony = _sferaService.GetManager("SzablonyAsortymentu");
            if (szablony == null)
            {
                return StatusCode(500, ApiResponse<bool>.Error("Failed to get SzablonyAsortymentu manager"));
            }

            dynamic? szablonDane = null;
            foreach (var s in szablony.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetId(s) == id)
                {
                    szablonDane = s;
                    break;
                }
            }

            if (szablonDane == null)
            {
                return NotFound(ApiResponse<bool>.Error($"Product template with ID {id} not found"));
            }

            using (var szablon = szablony.Znajdz(szablonDane))
            {
                if (szablon == null)
                {
                    return NotFound(ApiResponse<bool>.Error($"Product template with ID {id} not found"));
                }

                if ((bool)szablon.Usun())
                {
                    _logger.LogInformation("Deleted product template {Id}", id);
                    return Ok(ApiResponse<bool>.Ok(true, "Product template deleted successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(szablon);
                    return BadRequest(ApiResponse<bool>.Error("Failed to delete product template", errors));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting product template {Id}", id);
            return StatusCode(500, ApiResponse<bool>.Error("Error deleting product template", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create a product from template
    /// </summary>
    [HttpPost("{id}/create-product")]
    public ActionResult<ApiResponse<ProductDto>> CreateProductFromTemplate(int id, [FromBody] CreateProductFromTemplateRequest request)
    {
        try
        {
            var szablony = _sferaService.GetManager("SzablonyAsortymentu");
            if (szablony == null)
            {
                return StatusCode(500, ApiResponse<ProductDto>.Error("Failed to get SzablonyAsortymentu manager"));
            }

            dynamic? szablonDane = null;
            foreach (var s in szablony.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetId(s) == id)
                {
                    szablonDane = s;
                    break;
                }
            }

            if (szablonDane == null)
            {
                return NotFound(ApiResponse<ProductDto>.Error($"Product template with ID {id} not found"));
            }

            var asortymenty = _sferaService.GetManager("Asortymenty");
            if (asortymenty == null)
            {
                return StatusCode(500, ApiResponse<ProductDto>.Error("Failed to get Asortymenty manager"));
            }

            // Check if symbol already exists
            foreach (var a in asortymenty.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetString(a, "Symbol") == request.Symbol)
                {
                    return BadRequest(ApiResponse<ProductDto>.Error($"Product with symbol {request.Symbol} already exists"));
                }
            }

            // Try to create product from template
            dynamic? asortyment = null;
            try
            {
                asortyment = asortymenty.UtworzZSzablonu(szablonDane);
            }
            catch
            {
                // Fallback: create regular product and copy template settings
                asortyment = asortymenty.Utworz();
            }

            if (asortyment == null)
            {
                return StatusCode(500, ApiResponse<ProductDto>.Error("Failed to create product from template"));
            }

            using (asortyment)
            {
                dynamic dane = asortyment.Dane;
                dane.Symbol = request.Symbol;
                dane.Nazwa = request.Name;

                if (!string.IsNullOrEmpty(request.Description))
                {
                    try { dane.Opis = request.Description; } catch { }
                }

                if (!string.IsNullOrEmpty(request.EAN))
                {
                    try { dane.EAN = request.EAN; } catch { }
                }

                if (!string.IsNullOrEmpty(request.PKWiU))
                {
                    try { dane.PKWiU = request.PKWiU; } catch { }
                }

                if (request.PurchasePrice.HasValue)
                {
                    try { dane.CenaZakupu = request.PurchasePrice.Value; } catch { }
                }

                if (request.SalePriceNet.HasValue)
                {
                    try { dane.CenaSprzedazyNetto = request.SalePriceNet.Value; } catch { }
                }

                // Set group if provided
                if (request.GroupId.HasValue)
                {
                    var grupy = _sferaService.GetManager("GrupyAsortymentu");
                    if (grupy != null)
                    {
                        foreach (var g in grupy.Dane.Wszystkie())
                        {
                            if (DynamicPropertyHelper.GetId(g) == request.GroupId.Value)
                            {
                                try { dane.Grupa = g; } catch { }
                                break;
                            }
                        }
                    }
                }

                if ((bool)asortyment.Zapisz())
                {
                    _logger.LogInformation("Created product {Symbol} from template {TemplateId}", request.Symbol, id);

                    var productDto = new ProductDto
                    {
                        Id = DynamicPropertyHelper.GetId(dane),
                        Symbol = DynamicPropertyHelper.GetString(dane, "Symbol") ?? "",
                        Name = DynamicPropertyHelper.GetString(dane, "Nazwa") ?? "",
                        Description = DynamicPropertyHelper.GetString(dane, "Opis"),
                        EAN = DynamicPropertyHelper.GetString(dane, "EAN"),
                        PKWiU = DynamicPropertyHelper.GetString(dane, "PKWiU")
                    };

                    return CreatedAtAction(
                        "GetProduct",
                        "Products",
                        new { id = productDto.Id },
                        ApiResponse<ProductDto>.Ok(productDto, "Product created from template successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(asortyment);
                    return BadRequest(ApiResponse<ProductDto>.Error("Failed to create product from template", errors));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating product from template {Id}", id);
            return StatusCode(500, ApiResponse<ProductDto>.Error("Error creating product from template", new List<string> { ex.Message }));
        }
    }

    private static ProductTemplateListItemDto MapToListItemDto(dynamic szablon)
    {
        var jednostka = DynamicPropertyHelper.GetProperty(szablon, "Jednostka");
        var stawkaVat = DynamicPropertyHelper.GetProperty(szablon, "StawkaVat");

        return new ProductTemplateListItemDto
        {
            Id = DynamicPropertyHelper.GetId(szablon),
            Symbol = DynamicPropertyHelper.GetString(szablon, "Symbol") ?? "",
            Name = DynamicPropertyHelper.GetString(szablon, "Nazwa") ?? "",
            Description = DynamicPropertyHelper.GetString(szablon, "Opis"),
            Type = DynamicPropertyHelper.GetNullableInt(szablon, "Typ") ?? 0,
            TypeDescription = GetTypeDescription(DynamicPropertyHelper.GetNullableInt(szablon, "Typ") ?? 0),
            UnitSymbol = jednostka != null ? DynamicPropertyHelper.GetString(jednostka, "Symbol") : null,
            VatRateSymbol = stawkaVat != null ? DynamicPropertyHelper.GetString(stawkaVat, "Symbol") : null,
            IsDeleted = DynamicPropertyHelper.GetNullableBool(szablon, "Usuniety") ?? false
        };
    }

    private static ProductTemplateDto MapToDto(dynamic szablon)
    {
        var jednostka = DynamicPropertyHelper.GetProperty(szablon, "Jednostka");
        var stawkaVat = DynamicPropertyHelper.GetProperty(szablon, "StawkaVat");
        var typAsortymentu = DynamicPropertyHelper.GetProperty(szablon, "TypAsortymentu");
        var grupaJPK = DynamicPropertyHelper.GetProperty(szablon, "GrupaTowarowJPK");
        var asortymentRef = DynamicPropertyHelper.GetProperty(szablon, "AsortymentReferencyjny");
        var dodatkowaOplata = DynamicPropertyHelper.GetProperty(szablon, "DodatkowaOplata");
        var minimalMargin = DynamicPropertyHelper.GetProperty(szablon, "MinimalnaMarza");

        return new ProductTemplateDto
        {
            // Identity
            Id = DynamicPropertyHelper.GetId(szablon),
            Symbol = DynamicPropertyHelper.GetString(szablon, "Symbol") ?? "",
            Name = DynamicPropertyHelper.GetString(szablon, "Nazwa") ?? "",
            Description = DynamicPropertyHelper.GetString(szablon, "Opis"),

            // Type
            Type = DynamicPropertyHelper.GetNullableInt(szablon, "Typ") ?? 0,
            TypeDescription = GetTypeDescription(DynamicPropertyHelper.GetNullableInt(szablon, "Typ") ?? 0),

            // Unit
            UnitId = jednostka != null ? DynamicPropertyHelper.GetId(jednostka) : null,
            UnitSymbol = jednostka != null ? DynamicPropertyHelper.GetString(jednostka, "Symbol") : null,
            UnitName = jednostka != null ? DynamicPropertyHelper.GetString(jednostka, "Nazwa") : null,
            CopyUnitWithSubtree = DynamicPropertyHelper.GetNullableBool(szablon, "KopiujJednostkeZPoddrzewem") ?? false,

            // VAT
            VatRateId = stawkaVat != null ? DynamicPropertyHelper.GetId(stawkaVat) : null,
            VatRateSymbol = stawkaVat != null ? DynamicPropertyHelper.GetString(stawkaVat, "Symbol") : null,
            VatPercent = stawkaVat != null ? DynamicPropertyHelper.GetDecimal(stawkaVat, "Stawka") : null,

            // Product type
            ProductTypeId = typAsortymentu != null ? DynamicPropertyHelper.GetId(typAsortymentu) : null,
            ProductTypeName = typAsortymentu != null ? DynamicPropertyHelper.GetString(typAsortymentu, "Nazwa") : null,

            // Pricing
            CalculationFromValue = DynamicPropertyHelper.GetByte(szablon, "WyliczanieZWartosci") ?? 0,
            MinimalMarginId = minimalMargin != null ? DynamicPropertyHelper.GetId(minimalMargin) : null,
            MinimalMarginPercent = minimalMargin != null ? DynamicPropertyHelper.GetDecimal(minimalMargin, "Procent") : null,

            // Tax/regulatory
            ReverseCharge = DynamicPropertyHelper.GetByte(szablon, "OdwrotneObciazenie") ?? 0,
            SplitPayment = DynamicPropertyHelper.GetByte(szablon, "PodzielonaPlatnosc") ?? 0,
            JpkProductGroup = grupaJPK != null ? DynamicPropertyHelper.GetId(grupaJPK) : null,
            JpkProductGroupName = grupaJPK != null ? DynamicPropertyHelper.GetString(grupaJPK, "Nazwa") : null,

            // Batches
            BatchSplitMethod = DynamicPropertyHelper.GetByte(szablon, "SposobRozdzielaniaPartii") ?? 0,
            BatchRegisterId = DynamicPropertyHelper.GetNullableInt(szablon, "RejestrPartiId"),

            // Intrastat
            IncludeInIntrastat = DynamicPropertyHelper.GetNullableBool(szablon, "UwzgledniajWIntrastat") ?? false,
            IntrastatDescriptionSource = DynamicPropertyHelper.GetByte(szablon, "ZrodloOpisuIntrastat") ?? 0,
            IntrastatDescription = DynamicPropertyHelper.GetString(szablon, "OpisIntrastat"),

            // Communication
            SendToExternalDevice = DynamicPropertyHelper.GetNullableBool(szablon, "WysylajDoUrzadzeniaZewnetrznego") ?? false,
            ShowMessage = DynamicPropertyHelper.GetNullableBool(szablon, "WyswietlajKomunikat") ?? false,
            DefaultMessageForm = DynamicPropertyHelper.GetByte(szablon, "DomyslnaFormaKomunikatu") ?? 0,
            DefaultMessageContent = DynamicPropertyHelper.GetString(szablon, "DomyslnaTrescKomunikatu"),

            // Reference product
            ReferenceProductId = asortymentRef != null ? DynamicPropertyHelper.GetId(asortymentRef) : null,
            ReferenceProductSymbol = asortymentRef != null ? DynamicPropertyHelper.GetString(asortymentRef, "Symbol") : null,
            ReferenceProductName = asortymentRef != null ? DynamicPropertyHelper.GetString(asortymentRef, "Nazwa") : null,

            // Additional fee
            AdditionalFeeProductId = dodatkowaOplata != null ? DynamicPropertyHelper.GetId(dodatkowaOplata) : null,
            AdditionalFeeProductSymbol = dodatkowaOplata != null ? DynamicPropertyHelper.GetString(dodatkowaOplata, "Symbol") : null,

            // Status
            IsDeleted = DynamicPropertyHelper.GetNullableBool(szablon, "Usuniety") ?? false,

            // Timestamps
            CreatedAt = DynamicPropertyHelper.GetDateTime(szablon, "DataUtworzenia"),
            ModifiedAt = DynamicPropertyHelper.GetDateTime(szablon, "DataModyfikacji")
        };
    }

    private static string GetTypeDescription(int type)
    {
        return type switch
        {
            0 => "Towar",
            1 => "Usługa",
            2 => "Komplet",
            3 => "Opakowanie",
            _ => $"Typ {type}"
        };
    }

    private static List<string> GetBusinessObjectErrors(dynamic obiekt)
    {
        var errors = new List<string>();
        try
        {
            var invalidData = DynamicPropertyHelper.GetProperty(obiekt, "InvalidData");
            if (invalidData == null) return errors;

            foreach (var encjaZBledami in invalidData)
            {
                var entityErrors = DynamicPropertyHelper.GetProperty(encjaZBledami, "Errors");
                if (entityErrors != null)
                {
                    foreach (var blad in entityErrors)
                    {
                        errors.Add(blad?.ToString() ?? "Unknown error");
                    }
                }

                var memberErrors = DynamicPropertyHelper.GetProperty(encjaZBledami, "MemberErrors");
                if (memberErrors != null)
                {
                    foreach (var bladNaPolach in memberErrors)
                    {
                        try
                        {
                            var key = DynamicPropertyHelper.GetProperty(bladNaPolach, "Key");
                            errors.Add($"{key}: {bladNaPolach}");
                        }
                        catch
                        {
                            errors.Add(bladNaPolach?.ToString() ?? "Unknown error");
                        }
                    }
                }
            }
        }
        catch
        {
            errors.Add("Could not retrieve error details");
        }
        return errors;
    }
}
