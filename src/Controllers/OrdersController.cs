using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Requests;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using InsERT.Moria.Asortymenty;

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
            var sfera = _sferaService.GetSfera();
            var zamowienia = sfera.ZamowieniaDoDostawcow();

            var dataQuery = zamowienia.Dane.Wszystkie();

            if (supplierId.HasValue)
            {
                dataQuery = dataQuery.Where(z => z.Podmiot != null && z.Podmiot.Id == supplierId.Value);
            }

            if (dateFrom.HasValue)
            {
                dataQuery = dataQuery.Where(z => z.DataWystawienia >= dateFrom.Value);
            }

            if (dateTo.HasValue)
            {
                dataQuery = dataQuery.Where(z => z.DataWystawienia <= dateTo.Value);
            }

            var totalCount = dataQuery.Count();
            var items = dataQuery
                .OrderByDescending(z => z.DataWystawienia)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var response = new PagedResponse<SupplierOrderDto>
            {
                Data = items.Select(MapSupplierOrderToDto).ToList(),
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
            var sfera = _sferaService.GetSfera();
            var zamowienia = sfera.ZamowieniaDoDostawcow();

            var zamowienie = zamowienia.Dane.Wszystkie().FirstOrDefault(z => z.Id == id);
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
            var sfera = _sferaService.GetSfera();
            var zamowienia = sfera.ZamowieniaDoDostawcow();
            var konfiguracja = sfera.Konfiguracje().DaneDomyslne.ZamowienieDoDostawcy;

            using (var zamowienie = zamowienia.Utworz(konfiguracja))
            {
                // Set supplier
                if (request.SupplierId.HasValue)
                {
                    var podmiot = sfera.Podmioty().Dane.Wszystkie()
                        .FirstOrDefault(p => p.Id == request.SupplierId.Value);
                    if (podmiot != null)
                    {
                        zamowienie.Dane.Podmiot = podmiot;
                    }
                }
                else if (!string.IsNullOrEmpty(request.SupplierNIP))
                {
                    var podmiot = sfera.Podmioty().Dane.Pierwszy(p => p.NIP == request.SupplierNIP);
                    if (podmiot != null)
                    {
                        zamowienie.Dane.Podmiot = podmiot;
                    }
                }

                // Set warehouse
                if (!string.IsNullOrEmpty(request.WarehouseSymbol))
                {
                    var magazyn = sfera.Magazyny().Dane.Pierwszy(m => m.Symbol == request.WarehouseSymbol);
                    if (magazyn != null)
                    {
                        zamowienie.Dane.Magazyn = magazyn;
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
                var asortymenty = sfera.Asortymenty();
                foreach (var item in request.Items)
                {
                    Asortyment? asortyment = null;

                    if (item.ProductId.HasValue)
                    {
                        asortyment = asortymenty.Dane.Wszystkie()
                            .FirstOrDefault(a => a.Id == item.ProductId.Value);
                    }
                    else if (!string.IsNullOrEmpty(item.ProductSymbol))
                    {
                        asortyment = asortymenty.Dane.Wszystkie()
                            .FirstOrDefault(a => a.Symbol == item.ProductSymbol);
                    }

                    if (asortyment != null)
                    {
                        var pozycja = zamowienie.Pozycje.Dodaj(asortyment, item.Quantity,
                            asortyment.JednostkaZakupu ?? asortyment.JednostkaSprzedazy);

                        if (item.PriceNet.HasValue && pozycja != null)
                        {
                            pozycja.Dane.CenaNetto = item.PriceNet.Value;
                        }
                    }
                }

                if (zamowienie.Zapisz())
                {
                    _logger.LogInformation("Created supplier order {Number}",
                        zamowienie.Dane.NumerWewnetrzny?.PelnaSygnatura);

                    return CreatedAtAction(
                        nameof(GetSupplierOrder),
                        new { id = zamowienie.Dane.Id },
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
            var sfera = _sferaService.GetSfera();
            var oferty = sfera.OfertyDlaKlientow();

            var dataQuery = oferty.Dane.Wszystkie();

            if (customerId.HasValue)
            {
                dataQuery = dataQuery.Where(o => o.Podmiot != null && o.Podmiot.Id == customerId.Value);
            }

            if (dateFrom.HasValue)
            {
                dataQuery = dataQuery.Where(o => o.DataWystawienia >= dateFrom.Value);
            }

            if (dateTo.HasValue)
            {
                dataQuery = dataQuery.Where(o => o.DataWystawienia <= dateTo.Value);
            }

            var totalCount = dataQuery.Count();
            var items = dataQuery
                .OrderByDescending(o => o.DataWystawienia)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var response = new PagedResponse<CommercialOfferDto>
            {
                Data = items.Select(MapOfferToDto).ToList(),
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
            var sfera = _sferaService.GetSfera();
            var oferty = sfera.OfertyDlaKlientow();
            var konfiguracja = sfera.Konfiguracje().DaneDomyslne.OfertaDlaKlienta;

            using (var oferta = oferty.Utworz(konfiguracja))
            {
                // Set customer
                if (request.CustomerId.HasValue)
                {
                    var podmiot = sfera.Podmioty().Dane.Wszystkie()
                        .FirstOrDefault(p => p.Id == request.CustomerId.Value);
                    if (podmiot != null)
                    {
                        oferta.Dane.Podmiot = podmiot;
                    }
                }
                else if (!string.IsNullOrEmpty(request.CustomerNIP))
                {
                    var podmiot = sfera.Podmioty().Dane.Pierwszy(p => p.NIP == request.CustomerNIP);
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
                var asortymenty = sfera.Asortymenty();
                foreach (var item in request.Items)
                {
                    Asortyment? asortyment = null;

                    if (item.ProductId.HasValue)
                    {
                        asortyment = asortymenty.Dane.Wszystkie()
                            .FirstOrDefault(a => a.Id == item.ProductId.Value);
                    }
                    else if (!string.IsNullOrEmpty(item.ProductSymbol))
                    {
                        asortyment = asortymenty.Dane.Wszystkie()
                            .FirstOrDefault(a => a.Symbol == item.ProductSymbol);
                    }

                    if (asortyment != null)
                    {
                        var pozycja = oferta.Pozycje.Dodaj(asortyment, item.Quantity, asortyment.JednostkaSprzedazy);

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

                if (oferta.Zapisz())
                {
                    _logger.LogInformation("Created commercial offer {Number}",
                        oferta.Dane.NumerWewnetrzny?.PelnaSygnatura);

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

    private static SupplierOrderDto MapSupplierOrderToDto(DokumentZD dokument)
    {
        var dto = new SupplierOrderDto
        {
            Id = dokument.Id,
            Number = dokument.NumerWewnetrzny?.Numer.ToString() ?? "",
            FullNumber = dokument.NumerWewnetrzny?.PelnaSygnatura,
            OrderDate = dokument.DataWystawienia,
            SupplierId = dokument.Podmiot?.Id,
            SupplierName = dokument.Podmiot?.NazwaSkrocona,
            SupplierNIP = dokument.Podmiot?.NIP,
            WarehouseSymbol = dokument.Magazyn?.Symbol,
            TotalNet = dokument.WartoscNetto,
            TotalVat = dokument.WartoscVat,
            TotalGross = dokument.WartoscBrutto,
            Notes = dokument.Uwagi,
            CreatedAt = dokument.DataUtworzenia,
            Items = new List<DocumentItemDto>()
        };

        if (dokument.Pozycje != null)
        {
            int lineNum = 1;
            foreach (var poz in dokument.Pozycje)
            {
                dto.Items.Add(new DocumentItemDto
                {
                    Id = poz.Id,
                    LineNumber = lineNum++,
                    ProductId = poz.Asortyment?.Id,
                    ProductSymbol = poz.Asortyment?.Symbol,
                    Name = poz.Nazwa,
                    Quantity = poz.Ilosc,
                    Unit = poz.Jednostka?.Symbol ?? "szt.",
                    PriceNet = poz.CenaNetto,
                    PriceGross = poz.CenaBrutto,
                    ValueNet = poz.WartoscNetto,
                    ValueVat = poz.WartoscVat,
                    ValueGross = poz.WartoscBrutto
                });
            }
        }

        return dto;
    }

    private static CommercialOfferDto MapOfferToDto(DokumentOK dokument)
    {
        var dto = new CommercialOfferDto
        {
            Id = dokument.Id,
            Number = dokument.NumerWewnetrzny?.Numer.ToString() ?? "",
            FullNumber = dokument.NumerWewnetrzny?.PelnaSygnatura,
            IssueDate = dokument.DataWystawienia,
            ValidUntil = dokument.DataWaznosci,
            CustomerId = dokument.Podmiot?.Id,
            CustomerName = dokument.Podmiot?.NazwaSkrocona,
            CustomerNIP = dokument.Podmiot?.NIP,
            TotalNet = dokument.WartoscNetto,
            TotalVat = dokument.WartoscVat,
            TotalGross = dokument.WartoscBrutto,
            Notes = dokument.Uwagi,
            CreatedAt = dokument.DataUtworzenia,
            Items = new List<DocumentItemDto>()
        };

        if (dokument.Pozycje != null)
        {
            int lineNum = 1;
            foreach (var poz in dokument.Pozycje)
            {
                dto.Items.Add(new DocumentItemDto
                {
                    Id = poz.Id,
                    LineNumber = lineNum++,
                    ProductId = poz.Asortyment?.Id,
                    ProductSymbol = poz.Asortyment?.Symbol,
                    Name = poz.Nazwa,
                    Quantity = poz.Ilosc,
                    Unit = poz.Jednostka?.Symbol ?? "szt.",
                    PriceNet = poz.CenaNetto,
                    PriceGross = poz.CenaBrutto,
                    ValueNet = poz.WartoscNetto,
                    ValueVat = poz.WartoscVat,
                    ValueGross = poz.WartoscBrutto
                });
            }
        }

        return dto;
    }

    private static List<string> GetBusinessObjectErrors(InsERT.Mox.ObiektyBiznesowe.IObiektBiznesowy obiekt)
    {
        var errors = new List<string>();
        foreach (var encjaZBledami in obiekt.InvalidData)
        {
            foreach (var blad in encjaZBledami.Errors)
            {
                errors.Add(blad.ToString());
            }
            foreach (var bladNaPolach in encjaZBledami.MemberErrors)
            {
                errors.Add($"{bladNaPolach.Key}: {string.Join(", ", bladNaPolach)}");
            }
        }
        return errors;
    }
}
