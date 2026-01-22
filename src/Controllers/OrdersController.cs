using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Requests;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Orders (ZD, ZK, Oferty) management endpoints
/// </summary>
[ApiController]
[Route("api/orders")]
[Authorize]
[Tags("Orders")]
public class OrdersController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(ISferaService sferaService, ILogger<OrdersController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get supplier orders (ZD - Zamówienia do dostawców)
    /// </summary>
    [HttpGet("supplier")]
    public ActionResult<PagedResponse<SupplierOrderDto>> GetSupplierOrders(
        [FromQuery] int? supplierId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var zamowienia = _sferaService.GetManager("ZamowieniaDoDostawcow");
            if (zamowienia == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get ZamowieniaDoDostawcow manager"));
            }

            var allZamowienia = new List<dynamic>();
            foreach (var z in zamowienia.Dane.Wszystkie())
            {
                allZamowienia.Add(z);
            }
            var dataQuery = allZamowienia.AsEnumerable();

            if (supplierId.HasValue)
            {
                dataQuery = dataQuery.Where(z =>
                {
                    var podmiot = DynamicPropertyHelper.GetProperty(z, "Podmiot");
                    return podmiot != null && DynamicPropertyHelper.GetId(podmiot) == supplierId.Value;
                });
            }

            if (dateFrom.HasValue)
            {
                dataQuery = dataQuery.Where(z =>
                    DynamicPropertyHelper.GetDateTime(z, "DataWystawienia") >= dateFrom.Value);
            }

            if (dateTo.HasValue)
            {
                dataQuery = dataQuery.Where(z =>
                    DynamicPropertyHelper.GetDateTime(z, "DataWystawienia") <= dateTo.Value);
            }

            var filteredList = dataQuery.ToList();
            var totalCount = filteredList.Count;
            var items = filteredList
                .OrderByDescending(z => DynamicPropertyHelper.GetDateTime(z, "DataWystawienia"))
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var mappedItems = new List<SupplierOrderDto>();
            foreach (var item in items)
            {
                mappedItems.Add(MapSupplierOrderToDto(item));
            }

            var response = new PagedResponse<SupplierOrderDto>
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
            _logger.LogError(ex, "Error getting supplier orders");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving supplier orders", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get supplier order by ID
    /// </summary>
    [HttpGet("supplier/{id}")]
    public ActionResult<ApiResponse<SupplierOrderDto>> GetSupplierOrder(int id)
    {
        try
        {
            var zamowienia = _sferaService.GetManager("ZamowieniaDoDostawcow");
            if (zamowienia == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get ZamowieniaDoDostawcow manager"));
            }

            var allZamowienia = new List<dynamic>();
            foreach (var z in zamowienia.Dane.Wszystkie())
            {
                allZamowienia.Add(z);
            }
            var zamowienie = allZamowienia.FirstOrDefault(z => DynamicPropertyHelper.GetId(z) == id);
            if (zamowienie == null)
            {
                return NotFound(ApiResponse<SupplierOrderDto>.Error($"Supplier order with ID {id} not found"));
            }

            return Ok(ApiResponse<SupplierOrderDto>.Ok(MapSupplierOrderToDto(zamowienie)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting supplier order {Id}", id);
            return StatusCode(500, ApiResponse<SupplierOrderDto>.Error("Error retrieving supplier order", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create a supplier order (ZD - Zamówienie do dostawcy)
    /// </summary>
    [HttpPost("supplier")]
    public ActionResult<ApiResponse<SupplierOrderDto>> CreateSupplierOrder([FromBody] CreateSupplierOrderRequest request)
    {
        try
        {
            var zamowienia = _sferaService.GetManager("ZamowieniaDoDostawcow");
            if (zamowienia == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get ZamowieniaDoDostawcow manager"));
            }

            var konfiguracje = _sferaService.GetManagerByType("InsERT.Moria.Sfera", "InsERT.Moria.Sfera.Konfiguracje");
            var konfiguracja = konfiguracje?.DaneDomyslne?.ZamowienieDoDostawcy;

            using (var zamowienie = konfiguracja != null ? zamowienia.Utworz(konfiguracja) : zamowienia.Utworz())
            {
                var podmioty = _sferaService.GetManager("Podmioty");

                // Set supplier
                if (request.SupplierId.HasValue && podmioty != null)
                {
                    dynamic? podmiot = null;
                    foreach (var p in podmioty.Dane.Wszystkie())
                    {
                        if (DynamicPropertyHelper.GetId(p) == request.SupplierId.Value)
                        {
                            podmiot = p;
                            break;
                        }
                    }
                    if (podmiot != null)
                    {
                        zamowienie.Dane.Podmiot = podmiot;
                    }
                }
                else if (!string.IsNullOrEmpty(request.SupplierNIP) && podmioty != null)
                {
                    dynamic? podmiot = null;
                    foreach (var p in podmioty.Dane.Wszystkie())
                    {
                        if (DynamicPropertyHelper.GetString(p, "NIP") == request.SupplierNIP)
                        {
                            podmiot = p;
                            break;
                        }
                    }
                    if (podmiot != null)
                    {
                        zamowienie.Dane.Podmiot = podmiot;
                    }
                }

                // Set warehouse
                if (!string.IsNullOrEmpty(request.WarehouseSymbol))
                {
                    var magazyny = _sferaService.GetManager("Magazyny");
                    if (magazyny != null)
                    {
                        dynamic? magazyn = null;
                        foreach (var m in magazyny.Dane.Wszystkie())
                        {
                            if (DynamicPropertyHelper.GetString(m, "Symbol") == request.WarehouseSymbol)
                            {
                                magazyn = m;
                                break;
                            }
                        }
                        if (magazyn != null)
                        {
                            zamowienie.Dane.Magazyn = magazyn;
                        }
                    }
                }

                if (request.OrderDate.HasValue)
                {
                    zamowienie.Dane.DataWystawienia = request.OrderDate.Value;
                }

                if (!string.IsNullOrEmpty(request.Notes))
                {
                    zamowienie.Dane.Uwagi = request.Notes;
                }

                // Add items
                var asortymenty = _sferaService.GetManager("Asortymenty");
                foreach (var item in request.Items)
                {
                    dynamic? asortyment = null;

                    if (asortymenty != null)
                    {
                        if (item.ProductId.HasValue)
                        {
                            foreach (var a in asortymenty.Dane.Wszystkie())
                            {
                                if (DynamicPropertyHelper.GetId(a) == item.ProductId.Value)
                                {
                                    asortyment = a;
                                    break;
                                }
                            }
                        }
                        else if (!string.IsNullOrEmpty(item.ProductSymbol))
                        {
                            foreach (var a in asortymenty.Dane.Wszystkie())
                            {
                                if (DynamicPropertyHelper.GetString(a, "Symbol") == item.ProductSymbol)
                                {
                                    asortyment = a;
                                    break;
                                }
                            }
                        }
                    }

                    if (asortyment != null)
                    {
                        var jednostkaZakupu = DynamicPropertyHelper.GetProperty(asortyment, "JednostkaZakupu");
                        var jednostkaSprzedazy = DynamicPropertyHelper.GetProperty(asortyment, "JednostkaSprzedazy");
                        var pozycja = zamowienie.Pozycje.Dodaj(asortyment, item.Quantity,
                            jednostkaZakupu ?? jednostkaSprzedazy);

                        if (item.PriceNet.HasValue && pozycja != null)
                        {
                            pozycja.Dane.CenaNetto = item.PriceNet.Value;
                        }
                    }
                }

                if ((bool)zamowienie.Zapisz())
                {
                    var numerWewnetrzny = DynamicPropertyHelper.GetProperty(zamowienie.Dane, "NumerWewnetrzny");
                    string orderNumber = numerWewnetrzny != null ? DynamicPropertyHelper.GetString(numerWewnetrzny, "PelnaSygnatura") : "";
                    _logger.LogInformation("Created supplier order {Number}", orderNumber);

                    return CreatedAtAction(
                        nameof(GetSupplierOrder),
                        new { id = DynamicPropertyHelper.GetId(zamowienie.Dane) },
                        ApiResponse<SupplierOrderDto>.Ok(MapSupplierOrderToDto(zamowienie.Dane), "Supplier order created successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(zamowienie);
                    return BadRequest(ApiResponse<SupplierOrderDto>.Error("Failed to create supplier order", errors));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating supplier order");
            return StatusCode(500, ApiResponse<SupplierOrderDto>.Error("Error creating supplier order", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get commercial offers (Oferty handlowe)
    /// </summary>
    [HttpGet("offers")]
    public ActionResult<PagedResponse<CommercialOfferDto>> GetCommercialOffers(
        [FromQuery] int? customerId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var oferty = _sferaService.GetManager("OfertyDlaKlientow");
            if (oferty == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get OfertyDlaKlientow manager"));
            }

            var allOferty = new List<dynamic>();
            foreach (var o in oferty.Dane.Wszystkie())
            {
                allOferty.Add(o);
            }
            var dataQuery = allOferty.AsEnumerable();

            if (customerId.HasValue)
            {
                dataQuery = dataQuery.Where(o =>
                {
                    var podmiot = DynamicPropertyHelper.GetProperty(o, "Podmiot");
                    return podmiot != null && DynamicPropertyHelper.GetId(podmiot) == customerId.Value;
                });
            }

            if (dateFrom.HasValue)
            {
                dataQuery = dataQuery.Where(o =>
                    DynamicPropertyHelper.GetDateTime(o, "DataWystawienia") >= dateFrom.Value);
            }

            if (dateTo.HasValue)
            {
                dataQuery = dataQuery.Where(o =>
                    DynamicPropertyHelper.GetDateTime(o, "DataWystawienia") <= dateTo.Value);
            }

            var filteredList = dataQuery.ToList();
            var totalCount = filteredList.Count;
            var items = filteredList
                .OrderByDescending(o => DynamicPropertyHelper.GetDateTime(o, "DataWystawienia"))
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var mappedItems = new List<CommercialOfferDto>();
            foreach (var item in items)
            {
                mappedItems.Add(MapOfferToDto(item));
            }

            var response = new PagedResponse<CommercialOfferDto>
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
            _logger.LogError(ex, "Error getting commercial offers");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving commercial offers", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create a commercial offer (Oferta dla klienta)
    /// </summary>
    [HttpPost("offers")]
    public ActionResult<ApiResponse<CommercialOfferDto>> CreateCommercialOffer([FromBody] CreateCommercialOfferRequest request)
    {
        try
        {
            var oferty = _sferaService.GetManager("OfertyDlaKlientow");
            if (oferty == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get OfertyDlaKlientow manager"));
            }

            var konfiguracje = _sferaService.GetManagerByType("InsERT.Moria.Sfera", "InsERT.Moria.Sfera.Konfiguracje");
            var konfiguracja = konfiguracje?.DaneDomyslne?.OfertaDlaKlienta;

            using (var oferta = konfiguracja != null ? oferty.Utworz(konfiguracja) : oferty.Utworz())
            {
                var podmioty = _sferaService.GetManager("Podmioty");

                // Set customer
                if (request.CustomerId.HasValue && podmioty != null)
                {
                    dynamic? podmiot = null;
                    foreach (var p in podmioty.Dane.Wszystkie())
                    {
                        if (DynamicPropertyHelper.GetId(p) == request.CustomerId.Value)
                        {
                            podmiot = p;
                            break;
                        }
                    }
                    if (podmiot != null)
                    {
                        oferta.Dane.Podmiot = podmiot;
                    }
                }
                else if (!string.IsNullOrEmpty(request.CustomerNIP) && podmioty != null)
                {
                    dynamic? podmiot = null;
                    foreach (var p in podmioty.Dane.Wszystkie())
                    {
                        if (DynamicPropertyHelper.GetString(p, "NIP") == request.CustomerNIP)
                        {
                            podmiot = p;
                            break;
                        }
                    }
                    if (podmiot != null)
                    {
                        oferta.Dane.Podmiot = podmiot;
                    }
                }

                if (request.IssueDate.HasValue)
                {
                    oferta.Dane.DataWystawienia = request.IssueDate.Value;
                }

                if (request.ValidUntil.HasValue)
                {
                    oferta.Dane.DataWaznosci = request.ValidUntil.Value;
                }

                if (!string.IsNullOrEmpty(request.Notes))
                {
                    oferta.Dane.Uwagi = request.Notes;
                }

                // Add items
                var asortymenty = _sferaService.GetManager("Asortymenty");
                foreach (var item in request.Items)
                {
                    dynamic? asortyment = null;

                    if (asortymenty != null)
                    {
                        if (item.ProductId.HasValue)
                        {
                            foreach (var a in asortymenty.Dane.Wszystkie())
                            {
                                if (DynamicPropertyHelper.GetId(a) == item.ProductId.Value)
                                {
                                    asortyment = a;
                                    break;
                                }
                            }
                        }
                        else if (!string.IsNullOrEmpty(item.ProductSymbol))
                        {
                            foreach (var a in asortymenty.Dane.Wszystkie())
                            {
                                if (DynamicPropertyHelper.GetString(a, "Symbol") == item.ProductSymbol)
                                {
                                    asortyment = a;
                                    break;
                                }
                            }
                        }
                    }

                    if (asortyment != null)
                    {
                        var jednostka = DynamicPropertyHelper.GetProperty(asortyment, "JednostkaSprzedazy");
                        var pozycja = oferta.Pozycje.Dodaj(asortyment, item.Quantity, jednostka);

                        if (item.PriceNet.HasValue && pozycja != null)
                        {
                            pozycja.Dane.CenaNetto = item.PriceNet.Value;
                        }

                        if (item.DiscountPercent.HasValue && pozycja != null)
                        {
                            pozycja.Dane.RabatProcent = item.DiscountPercent.Value;
                        }
                    }
                }

                if ((bool)oferta.Zapisz())
                {
                    var numerWewnetrzny = DynamicPropertyHelper.GetProperty(oferta.Dane, "NumerWewnetrzny");
                    string offerNumber = numerWewnetrzny != null ? DynamicPropertyHelper.GetString(numerWewnetrzny, "PelnaSygnatura") : "";
                    _logger.LogInformation("Created commercial offer {Number}", offerNumber);

                    return CreatedAtAction(
                        nameof(GetCommercialOffers),
                        ApiResponse<CommercialOfferDto>.Ok(MapOfferToDto(oferta.Dane), "Commercial offer created successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(oferta);
                    return BadRequest(ApiResponse<CommercialOfferDto>.Error("Failed to create commercial offer", errors));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating commercial offer");
            return StatusCode(500, ApiResponse<CommercialOfferDto>.Error("Error creating commercial offer", new List<string> { ex.Message }));
        }
    }

    private static SupplierOrderDto MapSupplierOrderToDto(dynamic dokument)
    {
        var numerWewnetrzny = DynamicPropertyHelper.GetProperty(dokument, "NumerWewnetrzny");
        var podmiot = DynamicPropertyHelper.GetProperty(dokument, "Podmiot");
        var magazyn = DynamicPropertyHelper.GetProperty(dokument, "Magazyn");

        var dto = new SupplierOrderDto
        {
            Id = DynamicPropertyHelper.GetId(dokument),
            Number = numerWewnetrzny != null ? DynamicPropertyHelper.GetInt(numerWewnetrzny, "Numer").ToString() : "",
            FullNumber = numerWewnetrzny != null ? DynamicPropertyHelper.GetString(numerWewnetrzny, "PelnaSygnatura") : null,
            OrderDate = DynamicPropertyHelper.GetDateTime(dokument, "DataWystawienia"),
            SupplierId = podmiot != null ? DynamicPropertyHelper.GetId(podmiot) : null,
            SupplierName = podmiot != null ? DynamicPropertyHelper.GetString(podmiot, "NazwaSkrocona") : null,
            SupplierNIP = podmiot != null ? DynamicPropertyHelper.GetString(podmiot, "NIP") : null,
            WarehouseSymbol = magazyn != null ? DynamicPropertyHelper.GetString(magazyn, "Symbol") : null,
            TotalNet = DynamicPropertyHelper.GetDecimal(dokument, "WartoscNetto"),
            TotalVat = DynamicPropertyHelper.GetDecimal(dokument, "WartoscVat"),
            TotalGross = DynamicPropertyHelper.GetDecimal(dokument, "WartoscBrutto"),
            Notes = DynamicPropertyHelper.GetString(dokument, "Uwagi"),
            CreatedAt = DynamicPropertyHelper.GetDateTime(dokument, "DataUtworzenia"),
            Items = new List<DocumentItemDto>()
        };

        var pozycje = DynamicPropertyHelper.GetCollection(dokument, "Pozycje");
        int lineNum = 1;
        foreach (var poz in pozycje)
        {
            var asortyment = DynamicPropertyHelper.GetProperty(poz, "Asortyment");
            var jednostka = DynamicPropertyHelper.GetProperty(poz, "Jednostka");

            dto.Items.Add(new DocumentItemDto
            {
                Id = DynamicPropertyHelper.GetId(poz),
                LineNumber = lineNum++,
                ProductId = asortyment != null ? DynamicPropertyHelper.GetId(asortyment) : null,
                ProductSymbol = asortyment != null ? DynamicPropertyHelper.GetString(asortyment, "Symbol") : null,
                Name = DynamicPropertyHelper.GetString(poz, "Nazwa"),
                Quantity = DynamicPropertyHelper.GetDecimal(poz, "Ilosc"),
                Unit = jednostka != null ? DynamicPropertyHelper.GetString(jednostka, "Symbol") ?? "szt." : "szt.",
                PriceNet = DynamicPropertyHelper.GetDecimal(poz, "CenaNetto"),
                PriceGross = DynamicPropertyHelper.GetDecimal(poz, "CenaBrutto"),
                ValueNet = DynamicPropertyHelper.GetDecimal(poz, "WartoscNetto"),
                ValueVat = DynamicPropertyHelper.GetDecimal(poz, "WartoscVat"),
                ValueGross = DynamicPropertyHelper.GetDecimal(poz, "WartoscBrutto")
            });
        }

        return dto;
    }

    private static CommercialOfferDto MapOfferToDto(dynamic dokument)
    {
        var numerWewnetrzny = DynamicPropertyHelper.GetProperty(dokument, "NumerWewnetrzny");
        var podmiot = DynamicPropertyHelper.GetProperty(dokument, "Podmiot");

        var dto = new CommercialOfferDto
        {
            Id = DynamicPropertyHelper.GetId(dokument),
            Number = numerWewnetrzny != null ? DynamicPropertyHelper.GetInt(numerWewnetrzny, "Numer").ToString() : "",
            FullNumber = numerWewnetrzny != null ? DynamicPropertyHelper.GetString(numerWewnetrzny, "PelnaSygnatura") : null,
            IssueDate = DynamicPropertyHelper.GetDateTime(dokument, "DataWystawienia"),
            ValidUntil = DynamicPropertyHelper.GetDateTime(dokument, "DataWaznosci"),
            CustomerId = podmiot != null ? DynamicPropertyHelper.GetId(podmiot) : null,
            CustomerName = podmiot != null ? DynamicPropertyHelper.GetString(podmiot, "NazwaSkrocona") : null,
            CustomerNIP = podmiot != null ? DynamicPropertyHelper.GetString(podmiot, "NIP") : null,
            TotalNet = DynamicPropertyHelper.GetDecimal(dokument, "WartoscNetto"),
            TotalVat = DynamicPropertyHelper.GetDecimal(dokument, "WartoscVat"),
            TotalGross = DynamicPropertyHelper.GetDecimal(dokument, "WartoscBrutto"),
            Notes = DynamicPropertyHelper.GetString(dokument, "Uwagi"),
            CreatedAt = DynamicPropertyHelper.GetDateTime(dokument, "DataUtworzenia"),
            Items = new List<DocumentItemDto>()
        };

        var pozycje = DynamicPropertyHelper.GetCollection(dokument, "Pozycje");
        int lineNum = 1;
        foreach (var poz in pozycje)
        {
            var asortyment = DynamicPropertyHelper.GetProperty(poz, "Asortyment");
            var jednostka = DynamicPropertyHelper.GetProperty(poz, "Jednostka");

            dto.Items.Add(new DocumentItemDto
            {
                Id = DynamicPropertyHelper.GetId(poz),
                LineNumber = lineNum++,
                ProductId = asortyment != null ? DynamicPropertyHelper.GetId(asortyment) : null,
                ProductSymbol = asortyment != null ? DynamicPropertyHelper.GetString(asortyment, "Symbol") : null,
                Name = DynamicPropertyHelper.GetString(poz, "Nazwa"),
                Quantity = DynamicPropertyHelper.GetDecimal(poz, "Ilosc"),
                Unit = jednostka != null ? DynamicPropertyHelper.GetString(jednostka, "Symbol") ?? "szt." : "szt.",
                PriceNet = DynamicPropertyHelper.GetDecimal(poz, "CenaNetto"),
                PriceGross = DynamicPropertyHelper.GetDecimal(poz, "CenaBrutto"),
                ValueNet = DynamicPropertyHelper.GetDecimal(poz, "WartoscNetto"),
                ValueVat = DynamicPropertyHelper.GetDecimal(poz, "WartoscVat"),
                ValueGross = DynamicPropertyHelper.GetDecimal(poz, "WartoscBrutto")
            });
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
