using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Requests;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Products (Asortyment) management endpoints
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Tags("Products")]
public class ProductsController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(ISferaService sferaService, ILogger<ProductsController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Explore properties of a single Asortyment entity
    /// </summary>
    [HttpGet("debug/properties/{id}")]
    public ActionResult<object> GetProductProperties(int id)
    {
        try
        {
            var asortymentyManager = _sferaService.GetManager("Asortymenty");
            if (asortymentyManager == null)
            {
                return StatusCode(500, new { Error = "Failed to get Asortymenty manager" });
            }

            dynamic? asortyment = null;
            foreach (var a in asortymentyManager.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetId(a) == id)
                {
                    asortyment = a;
                    break;
                }
            }

            if (asortyment == null)
            {
                return NotFound(new { Error = $"Product {id} not found" });
            }

            Type asortymentType = asortyment.GetType();
            var properties = new Dictionary<string, object>();

            foreach (var prop in asortymentType.GetProperties())
            {
                try
                {
                    var value = prop.GetValue(asortyment);
                    var valueType = value?.GetType().Name ?? "null";

                    // For collections, get count
                    if (value != null && value.GetType().Name.Contains("Collection"))
                    {
                        try
                        {
                            int count = 0;
                            foreach (var _ in (dynamic)value) count++;
                            properties[prop.Name] = new { Type = valueType, Count = count };
                        }
                        catch
                        {
                            properties[prop.Name] = new { Type = valueType, Value = "Collection (error reading)" };
                        }
                    }
                    else if (value != null && !prop.PropertyType.IsPrimitive && prop.PropertyType != typeof(string)
                             && prop.PropertyType != typeof(DateTime) && prop.PropertyType != typeof(decimal)
                             && !prop.PropertyType.IsEnum && prop.PropertyType != typeof(Guid))
                    {
                        properties[prop.Name] = new { Type = valueType, Value = "Complex object" };
                    }
                    else
                    {
                        properties[prop.Name] = new { Type = valueType, Value = value?.ToString() ?? "null" };
                    }
                }
                catch (Exception ex)
                {
                    properties[prop.Name] = new { Error = ex.Message };
                }
            }

            return Ok(new
            {
                Id = id,
                EntityType = asortymentType.FullName,
                PropertyCount = properties.Count,
                Properties = properties.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value)
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = ex.Message, Stack = ex.StackTrace });
        }
    }

    /// <summary>
    /// Get all products with optional filtering (lightweight list view)
    /// </summary>
    [HttpGet]
    public ActionResult<PagedResponse<ProductListItemDto>> GetProducts(
        [FromQuery] string? search,
        [FromQuery] ProductType? type,
        [FromQuery] bool? activeOnly,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var asortymentyManager = _sferaService.GetManager("Asortymenty");
            if (asortymentyManager == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Asortymenty manager"));
            }

            var allAsortymenty = new List<dynamic>();
            var sourceData = activeOnly == true ? asortymentyManager.Dane.WszystkieDostepne() : asortymentyManager.Dane.Wszystkie();
            foreach (var a in sourceData)
            {
                allAsortymenty.Add(a);
            }

            if (!string.IsNullOrEmpty(search))
            {
                allAsortymenty = allAsortymenty.Where(a =>
                {
                    var symbol = DynamicPropertyHelper.GetString(a, "Symbol") ?? "";
                    var nazwa = DynamicPropertyHelper.GetString(a, "Nazwa") ?? "";
                    var ean = DynamicPropertyHelper.GetString(a, "EAN") ?? "";
                    return symbol.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                           nazwa.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                           ean.Contains(search, StringComparison.OrdinalIgnoreCase);
                }).ToList();
            }

            var totalCount = allAsortymenty.Count;
            var items = allAsortymenty
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var mappedItems = new List<ProductListItemDto>();
            foreach (var item in items)
            {
                mappedItems.Add(MapToListItemDto(item));
            }

            var response = new PagedResponse<ProductListItemDto>
            {
                Data = mappedItems,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting products");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving products", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get product by ID
    /// </summary>
    [HttpGet("{id}")]
    public ActionResult<ApiResponse<ProductDto>> GetProduct(int id)
    {
        try
        {
            var asortymentyManager = _sferaService.GetManager("Asortymenty");
            if (asortymentyManager == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Asortymenty manager"));
            }

            dynamic? asortyment = null;
            foreach (var a in asortymentyManager.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetId(a) == id)
                {
                    asortyment = a;
                    break;
                }
            }

            if (asortyment == null)
            {
                return NotFound(ApiResponse<ProductDto>.Error($"Product with ID {id} not found"));
            }

            return Ok(ApiResponse<ProductDto>.Ok(MapToDto(asortyment)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product {Id}", id);
            return StatusCode(500, ApiResponse<ProductDto>.Error("Error retrieving product", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get product by symbol
    /// </summary>
    [HttpGet("by-symbol/{symbol}")]
    public ActionResult<ApiResponse<ProductDto>> GetProductBySymbol(string symbol)
    {
        try
        {
            var asortymentyManager = _sferaService.GetManager("Asortymenty");
            if (asortymentyManager == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Asortymenty manager"));
            }

            var asortyment = asortymentyManager.Znajdz(symbol);
            if (asortyment == null)
            {
                return NotFound(ApiResponse<ProductDto>.Error($"Product with symbol {symbol} not found"));
            }

            return Ok(ApiResponse<ProductDto>.Ok(MapToDto(asortyment.Dane)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product by symbol {Symbol}", symbol);
            return StatusCode(500, ApiResponse<ProductDto>.Error("Error retrieving product", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get product by EAN
    /// </summary>
    [HttpGet("by-ean/{ean}")]
    public ActionResult<ApiResponse<ProductDto>> GetProductByEan(string ean)
    {
        try
        {
            var asortymentyManager = _sferaService.GetManager("Asortymenty");
            if (asortymentyManager == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Asortymenty manager"));
            }

            dynamic? asortyment = null;
            foreach (var a in asortymentyManager.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetString(a, "EAN") == ean)
                {
                    asortyment = a;
                    break;
                }
            }

            if (asortyment == null)
            {
                return NotFound(ApiResponse<ProductDto>.Error($"Product with EAN {ean} not found"));
            }

            return Ok(ApiResponse<ProductDto>.Ok(MapToDto(asortyment)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product by EAN {Ean}", ean);
            return StatusCode(500, ApiResponse<ProductDto>.Error("Error retrieving product", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create a new product
    /// </summary>
    [HttpPost]
    public ActionResult<ApiResponse<ProductDto>> CreateProduct([FromBody] CreateProductRequest request)
    {
        try
        {
            var asortymentyManager = _sferaService.GetManager("Asortymenty");
            if (asortymentyManager == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Asortymenty manager"));
            }

            // Check if symbol already exists
            var existing = asortymentyManager.Znajdz(request.Symbol);
            if (existing != null)
            {
                return BadRequest(ApiResponse<ProductDto>.Error($"Product with symbol {request.Symbol} already exists"));
            }

            using (var nowyAsortyment = asortymentyManager.Utworz())
            {
                dynamic dane = nowyAsortyment.Dane;

                // Apply template if specified
                if (!string.IsNullOrEmpty(request.TemplateSymbol))
                {
                    var szablonyManager = _sferaService.GetManager("SzablonyAsortymentu");
                    if (szablonyManager != null)
                    {
                        dynamic? szablon = null;
                        foreach (var s in szablonyManager.Dane.Wszystkie())
                        {
                            if (DynamicPropertyHelper.GetString(s, "Symbol") == request.TemplateSymbol)
                            {
                                szablon = s;
                                break;
                            }
                        }
                        if (szablon != null)
                        {
                            nowyAsortyment.WypelnijNaPodstawieSzablonu(szablon);
                        }
                    }
                }

                dane.Symbol = request.Symbol;
                dane.Nazwa = request.Name;

                if (!string.IsNullOrEmpty(request.Description))
                {
                    dane.Opis = request.Description;
                }

                if (!string.IsNullOrEmpty(request.EAN))
                {
                    dane.EAN = request.EAN;
                }

                if (!string.IsNullOrEmpty(request.PKWiU))
                {
                    dane.PKWIU = request.PKWiU;
                }

                // Set unit
                var jednostka = GetUnit(request.SaleUnit);
                if (jednostka != null)
                {
                    dane.JednostkaSprzedazy = jednostka;
                }

                if (!string.IsNullOrEmpty(request.PurchaseUnit))
                {
                    var jednostkaZakupu = GetUnit(request.PurchaseUnit);
                    if (jednostkaZakupu != null)
                    {
                        dane.JednostkaZakupu = jednostkaZakupu;
                    }
                }

                // Set price
                if (request.PriceNet.HasValue)
                {
                    dane.CenaNetto = request.PriceNet.Value;
                }

                // Set physical properties
                if (request.Weight.HasValue)
                {
                    dane.Masa = request.Weight.Value;
                }

                if (request.Volume.HasValue)
                {
                    dane.Objetosc = request.Volume.Value;
                }

                if ((bool)nowyAsortyment.Zapisz())
                {
                    _logger.LogInformation("Created product {Symbol}", request.Symbol);
                    return CreatedAtAction(
                        nameof(GetProduct),
                        new { id = DynamicPropertyHelper.GetId(dane) },
                        ApiResponse<ProductDto>.Ok(MapToDto(dane), "Product created successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(nowyAsortyment);
                    return BadRequest(ApiResponse<ProductDto>.Error("Failed to create product", errors));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating product");
            return StatusCode(500, ApiResponse<ProductDto>.Error("Error creating product", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Update an existing product
    /// </summary>
    [HttpPut("{id}")]
    public ActionResult<ApiResponse<ProductDto>> UpdateProduct(int id, [FromBody] UpdateProductRequest request)
    {
        try
        {
            var asortymentyManager = _sferaService.GetManager("Asortymenty");
            if (asortymentyManager == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Asortymenty manager"));
            }

            dynamic? asortyment = null;
            foreach (var a in asortymentyManager.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetId(a) == id)
                {
                    asortyment = a;
                    break;
                }
            }

            if (asortyment == null)
            {
                return NotFound(ApiResponse<ProductDto>.Error($"Product with ID {id} not found"));
            }

            using (var edytowanyAsortyment = asortymentyManager.Znajdz(asortyment))
            {
                if (edytowanyAsortyment == null)
                {
                    return NotFound(ApiResponse<ProductDto>.Error($"Product with ID {id} not found"));
                }

                dynamic dane = edytowanyAsortyment.Dane;

                if (!string.IsNullOrEmpty(request.Name))
                {
                    dane.Nazwa = request.Name;
                }

                if (!string.IsNullOrEmpty(request.Description))
                {
                    dane.Opis = request.Description;
                }

                if (!string.IsNullOrEmpty(request.EAN))
                {
                    dane.EAN = request.EAN;
                }

                if (!string.IsNullOrEmpty(request.PKWiU))
                {
                    dane.PKWIU = request.PKWiU;
                }

                if (request.PriceNet.HasValue)
                {
                    dane.CenaNetto = request.PriceNet.Value;
                }

                if (request.Weight.HasValue)
                {
                    dane.Masa = request.Weight.Value;
                }

                if (request.Volume.HasValue)
                {
                    dane.Objetosc = request.Volume.Value;
                }

                if (request.IsActive.HasValue)
                {
                    dane.Aktywny = request.IsActive.Value;
                }

                if ((bool)edytowanyAsortyment.Zapisz())
                {
                    _logger.LogInformation("Updated product {Id}", id);
                    return Ok(ApiResponse<ProductDto>.Ok(MapToDto(dane), "Product updated successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(edytowanyAsortyment);
                    return BadRequest(ApiResponse<ProductDto>.Error("Failed to update product", errors));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating product {Id}", id);
            return StatusCode(500, ApiResponse<ProductDto>.Error("Error updating product", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Delete a product
    /// </summary>
    [HttpDelete("{id}")]
    public ActionResult<ApiResponse<bool>> DeleteProduct(int id)
    {
        try
        {
            var asortymentyManager = _sferaService.GetManager("Asortymenty");
            if (asortymentyManager == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Asortymenty manager"));
            }

            dynamic? asortyment = null;
            foreach (var a in asortymentyManager.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetId(a) == id)
                {
                    asortyment = a;
                    break;
                }
            }

            if (asortyment == null)
            {
                return NotFound(ApiResponse<bool>.Error($"Product with ID {id} not found"));
            }

            using (var usuwanyAsortyment = asortymentyManager.Znajdz(asortyment))
            {
                if (usuwanyAsortyment == null)
                {
                    return NotFound(ApiResponse<bool>.Error($"Product with ID {id} not found"));
                }

                if ((bool)usuwanyAsortyment.Usun())
                {
                    _logger.LogInformation("Deleted product {Id}", id);
                    return Ok(ApiResponse<bool>.Ok(true, "Product deleted successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(usuwanyAsortyment);
                    return BadRequest(ApiResponse<bool>.Error("Failed to delete product", errors));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting product {Id}", id);
            return StatusCode(500, ApiResponse<bool>.Error("Error deleting product", new List<string> { ex.Message }));
        }
    }

    private dynamic? GetUnit(string symbol)
    {
        if (string.IsNullOrEmpty(symbol)) return null;

        var jednostkiManager = _sferaService.GetManagerByType("InsERT.Moria.ModelDanych", "InsERT.Moria.ModelDanych.Jednostki");
        if (jednostkiManager == null) return null;

        foreach (var j in jednostkiManager.Dane.Wszystkie())
        {
            if (DynamicPropertyHelper.GetString(j, "Symbol") == symbol)
            {
                return j;
            }
        }
        return null;
    }

    private static ProductDto MapToDto(dynamic asortyment)
    {
        var dto = new ProductDto
        {
            // Basic info
            Id = DynamicPropertyHelper.GetId(asortyment),
            Symbol = DynamicPropertyHelper.GetString(asortyment, "Symbol"),
            Name = DynamicPropertyHelper.GetString(asortyment, "Nazwa"),
            Description = DynamicPropertyHelper.GetString(asortyment, "Opis"),
            FullCharacteristics = DynamicPropertyHelper.GetString(asortyment, "PelnaCharakterystyka"),
            EAN = DynamicPropertyHelper.GetString(asortyment, "EAN"),
            PKWiU = DynamicPropertyHelper.GetString(asortyment, "PKWIU"),
            CnCode = DynamicPropertyHelper.GetString(asortyment, "KodCN"),
            SWW = DynamicPropertyHelper.GetString(asortyment, "SWW"),

            // Variant info
            VariantOriginalName = DynamicPropertyHelper.GetString(asortyment, "OryginalnaNazwaWariantu"),
            VariantOriginalDescription = DynamicPropertyHelper.GetString(asortyment, "OryginalnyOpisWariantu"),
            VariantNumber = DynamicPropertyHelper.GetNullableInt(asortyment, "LpWariantu"),
            ParentProductId = DynamicPropertyHelper.GetNullableInt(asortyment, "AsortymentId"),
            ModelId = DynamicPropertyHelper.GetNullableInt(asortyment, "Model_Id"),

            // Type and classification
            Type = (ProductType)(DynamicPropertyHelper.GetNullableInt(asortyment, "Rodzaj_Id") ?? 0),
            GroupId = DynamicPropertyHelper.GetNullableInt(asortyment, "Grupa_Id"),

            // Units
            SaleUnit = DynamicPropertyHelper.GetString(asortyment, "JednostkaSprzedazy", "Symbol"),
            PurchaseUnit = DynamicPropertyHelper.GetString(asortyment, "JednostkaZakupu", "Symbol"),
            DefaultSalesQuantity = DynamicPropertyHelper.GetNullableDecimal(asortyment, "DomyslnaIloscSprzedazy"),
            DefaultPurchaseQuantity = DynamicPropertyHelper.GetNullableDecimal(asortyment, "DomyslnaIloscZakupu"),

            // Pricing
            PriceNet = DynamicPropertyHelper.GetNullableDecimal(asortyment, "CenaNetto"),
            PriceGross = DynamicPropertyHelper.GetNullableDecimal(asortyment, "CenaBrutto"),
            RecordPrice = DynamicPropertyHelper.GetNullableDecimal(asortyment, "CenaEwidencyjna"),
            LaborCost = DynamicPropertyHelper.GetNullableDecimal(asortyment, "KosztRobocizny"),
            AutoCalculatePrice = DynamicPropertyHelper.GetBool(asortyment, "WyliczajAutomatycznieCene"),
            CalculationFromValue = DynamicPropertyHelper.GetNullableInt(asortyment, "WyliczenieZWartosci"),
            PriceLevelId = DynamicPropertyHelper.GetNullableInt(asortyment, "PoziomCenId"),

            // VAT
            VatMarginEnabled = DynamicPropertyHelper.GetBool(asortyment, "PodlegaObrotowiVATMarza"),
            ReverseCharge = DynamicPropertyHelper.GetNullableInt(asortyment, "PodlegaOdwrotnemuObciazeniu"),
            FeeSubjectToVat = DynamicPropertyHelper.GetNullableBool(asortyment, "OplataPodlegaVat") ?? true,

            // Physical properties
            Weight = DynamicPropertyHelper.GetNullableDecimal(asortyment, "Masa"),
            Volume = DynamicPropertyHelper.GetNullableDecimal(asortyment, "Objetosc"),

            // Status and flags
            IsActive = DynamicPropertyHelper.GetBool(asortyment, "Aktywny"),
            IsDeleted = DynamicPropertyHelper.GetBool(asortyment, "IsInRecycleBin"),
            IsDiscounted = DynamicPropertyHelper.GetBool(asortyment, "Przeceniony"),
            IsOpenPrice = DynamicPropertyHelper.GetBool(asortyment, "OtwartaCena"),
            RequiresWeighing = DynamicPropertyHelper.GetBool(asortyment, "PrzeznaczonyDoWazenia"),
            Markers = DynamicPropertyHelper.GetNullableInt(asortyment, "Znaczniki"),
            CustomFlagId = DynamicPropertyHelper.GetNullableInt(asortyment, "FlagaWlasna_Id"),

            // Sales channels
            EcommerceEnabled = DynamicPropertyHelper.GetBool(asortyment, "SklepInternetowy"),
            MobileSalesEnabled = DynamicPropertyHelper.GetBool(asortyment, "SprzedazMobilna"),
            AuctionServiceEnabled = DynamicPropertyHelper.GetBool(asortyment, "SerwisAukcyjny"),

            // Delivery times
            CustomerDeliveryDays = DynamicPropertyHelper.GetNullableInt(asortyment, "LiczbaDniDoRealizacjiOdbiorcy"),
            SupplierDeliveryDays = DynamicPropertyHelper.GetNullableInt(asortyment, "LiczbaDniDoRealizacjiDostawcy"),

            // Expiry control
            ExpiryControlEnabled = DynamicPropertyHelper.GetBool(asortyment, "TerminWaznosciKontrola"),
            ExpiryDays = DynamicPropertyHelper.GetNullableInt(asortyment, "TerminWaznosciLiczbaDni"),

            // Batch management
            BatchSplitMethod = DynamicPropertyHelper.GetNullableInt(asortyment, "SposobRozbiciaNaPartie"),
            RequireBatchNumber = DynamicPropertyHelper.GetNullableInt(asortyment, "WymagajNumeruPartii"),
            RequireBatchExpiry = DynamicPropertyHelper.GetNullableInt(asortyment, "WymagajTerminuWaznosciPartii"),
            CheckBatchUniqueness = DynamicPropertyHelper.GetNullableInt(asortyment, "SprawdzajUnikalnoscNumerowPartii"),
            BlockOnDuplicateBatch = DynamicPropertyHelper.GetBool(asortyment, "BlokujPrzyBrakuUnikalnosci"),

            // Additional fees and taxes
            AdditionalFeeType = DynamicPropertyHelper.GetNullableInt(asortyment, "RodzajDodatkowejOplaty"),
            SplitPayment = DynamicPropertyHelper.GetNullableInt(asortyment, "PodlegaPodzielonejPlatnosci"),
            JpkVatGroup = DynamicPropertyHelper.GetNullableInt(asortyment, "GrupaAsortymentuJpkVat"),
            SugarTax = DynamicPropertyHelper.GetBool(asortyment, "OplataCukrowa"),
            CaffeineTax = DynamicPropertyHelper.GetBool(asortyment, "OplataKofeinowa"),
            ForFee = DynamicPropertyHelper.GetBool(asortyment, "PodlegaOplacieNaFOR"),

            // Sugar tax details
            BeverageVolume = DynamicPropertyHelper.GetNullableDecimal(asortyment, "ObjetoscNapojuWJednostcePodstawowej"),
            SugarContent = DynamicPropertyHelper.GetNullableDecimal(asortyment, "ZawartoscCukru"),
            VariableSugarFee = DynamicPropertyHelper.GetBool(asortyment, "StosujTylkoCzescZmiennaOplatyCukrowej"),
            HasOtherSweeteners = DynamicPropertyHelper.GetBool(asortyment, "ZawieraInneSubstancjeSlodzace"),
            IsElectrolyteDrink = DynamicPropertyHelper.GetBool(asortyment, "NapojWeglowodanowoElektrolityczny"),

            // Intrastat
            IncludedInIntrastat = DynamicPropertyHelper.GetBool(asortyment, "UwzglednianyWIntrastacie"),
            DefaultCountryOfOriginId = DynamicPropertyHelper.GetNullableInt(asortyment, "DomyslnePanstwoPochodzeniaId"),
            OriginMethod = DynamicPropertyHelper.GetNullableInt(asortyment, "SposobPobieraniaPanstwaPochodzenia"),
            IntrastatDescMethod = DynamicPropertyHelper.GetNullableInt(asortyment, "SposobPobieraniaOpisuDoIntrastatu"),
            IntrastatDescription = DynamicPropertyHelper.GetString(asortyment, "OpisDoIntrastatu"),

            // Messages
            DisplayMessage = DynamicPropertyHelper.GetBool(asortyment, "WyswietlajKomunikat"),
            MessageText = DynamicPropertyHelper.GetString(asortyment, "TekstKomunikatu"),
            MessageDisplayType = DynamicPropertyHelper.GetNullableInt(asortyment, "WyswietlajKomunikatJako"),

            // External integration
            ExternalId = DynamicPropertyHelper.GetNullableInt(asortyment, "IdZewnetrzny"),
            WebsiteUrl = DynamicPropertyHelper.GetString(asortyment, "StronaWWW"),
            Notes = DynamicPropertyHelper.GetString(asortyment, "Uwagi"),

            // Related entities
            RelatedProductId = DynamicPropertyHelper.GetNullableInt(asortyment, "AsortymentPowiazany_Id"),
            RecServiceId = DynamicPropertyHelper.GetNullableInt(asortyment, "UslugaRec_Id"),
            FundId = DynamicPropertyHelper.GetNullableInt(asortyment, "Fundusz_Id"),
            IntegrationAccountId = DynamicPropertyHelper.GetNullableInt(asortyment, "KontoIntegracjiId")
        };

        // Determine if this is a variant
        dto.IsVariant = dto.ModelId != null;

        // Try to get VAT rate info from StawkaVatSprzedazy navigation property
        var stawkaVat = DynamicPropertyHelper.GetProperty(asortyment, "StawkaVatSprzedazy");
        if (stawkaVat != null)
        {
            dto.VatRate = DynamicPropertyHelper.GetString(stawkaVat, "Symbol");
            dto.VatRateSalesId = DynamicPropertyHelper.GetProperty(stawkaVat, "Id")?.ToString();
        }

        var stawkaVatKupno = DynamicPropertyHelper.GetProperty(asortyment, "StawkaVatKupno");
        if (stawkaVatKupno != null)
        {
            dto.VatRatePurchaseId = DynamicPropertyHelper.GetProperty(stawkaVatKupno, "Id")?.ToString();
        }

        // Try to get currency ID
        var waluta = DynamicPropertyHelper.GetProperty(asortyment, "WalutaCenyEwidencyjnej");
        if (waluta != null)
        {
            dto.CurrencyId = DynamicPropertyHelper.GetProperty(waluta, "Id")?.ToString();
        }

        // Try to get group name from Grupa navigation property
        var grupa = DynamicPropertyHelper.GetProperty(asortyment, "Grupa");
        if (grupa != null)
        {
            dto.GroupName = DynamicPropertyHelper.GetString(grupa, "Nazwa");
        }

        // Try to get substitutes group GUID
        var grupaZamiennikow = DynamicPropertyHelper.GetProperty(asortyment, "GrupaZamiennikow");
        if (grupaZamiennikow != null)
        {
            dto.SubstitutesGroup = grupaZamiennikow.ToString();
        }

        return dto;
    }

    /// <summary>
    /// Maps to lightweight DTO for list views (minimal fields for performance)
    /// </summary>
    private static ProductListItemDto MapToListItemDto(dynamic asortyment)
    {
        var dto = new ProductListItemDto
        {
            Id = DynamicPropertyHelper.GetId(asortyment),
            Symbol = DynamicPropertyHelper.GetString(asortyment, "Symbol") ?? "",
            Name = DynamicPropertyHelper.GetString(asortyment, "Nazwa") ?? "",
            Price = DynamicPropertyHelper.GetNullableDecimal(asortyment, "CenaEwidencyjna"),
            GroupId = DynamicPropertyHelper.GetNullableInt(asortyment, "Grupa_Id"),
            IsActive = DynamicPropertyHelper.GetBool(asortyment, "Aktywny")
        };

        // Try to get group name from Grupa navigation property
        var grupa = DynamicPropertyHelper.GetProperty(asortyment, "Grupa");
        if (grupa != null)
        {
            dto.GroupName = DynamicPropertyHelper.GetString(grupa, "Nazwa");
        }

        return dto;
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
