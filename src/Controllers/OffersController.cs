using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using InsERT.Moria.Dokumenty.Logistyka;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Offers (Oferty OE) management endpoints
/// </summary>
[ApiController]
[Route("api/offers")]
[Authorize]
[Tags("Offers")]
public class OffersController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<OffersController> _logger;

    public OffersController(ISferaService sferaService, ILogger<OffersController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    #region List and Get

    /// <summary>
    /// Get offers with filtering
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<OfferSummaryDto>), StatusCodes.Status200OK)]
    public ActionResult<PagedResponse<OfferSummaryDto>> GetOffers(
        [FromQuery] int? customerId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] bool? closedOnly,
        [FromQuery] bool? acceptedOnly,
        [FromQuery] bool? validOnly,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var sfera = _sferaService.GetSfera();
            var oferty = sfera.Oferty().Dane.Wszystkie();

            if (customerId.HasValue)
            {
                oferty = oferty.Where(o => o.Dokument != null && o.Dokument.Podmiot != null &&
                    o.Dokument.Podmiot.Id == customerId.Value);
            }

            if (dateFrom.HasValue)
            {
                oferty = oferty.Where(o => o.Dokument != null && o.Dokument.DataWystawienia >= dateFrom.Value);
            }

            if (dateTo.HasValue)
            {
                oferty = oferty.Where(o => o.Dokument != null && o.Dokument.DataWystawienia <= dateTo.Value);
            }

            if (closedOnly.HasValue && closedOnly.Value)
            {
                oferty = oferty.Where(o => o.Zamkniety == true);
            }

            if (acceptedOnly.HasValue && acceptedOnly.Value)
            {
                oferty = oferty.Where(o => o.Zaakceptowany == true);
            }

            if (validOnly.HasValue && validOnly.Value)
            {
                var now = DateTime.Now;
                oferty = oferty.Where(o =>
                    (!o.ObowiazujeOd.HasValue || o.ObowiazujeOd.Value <= now) &&
                    (!o.ObowiazujeDo.HasValue || o.ObowiazujeDo.Value >= now));
            }

            var totalCount = oferty.Count();
            var items = oferty
                .OrderByDescending(o => o.Dokument != null ? o.Dokument.DataWystawienia : null)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList()
                .Select(MapOfferSummary)
                .ToList();

            return Ok(new PagedResponse<OfferSummaryDto>
            {
                Data = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting offers");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving offers", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get offer by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<OfferDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<OfferDto>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<OfferDto>> GetOffer(int id, [FromQuery] bool includeItems = true)
    {
        try
        {
            var sfera = _sferaService.GetSfera();
            var oferta = sfera.Oferty().Dane.Wszystkie()
                .FirstOrDefault(o => o.Id == id);

            if (oferta == null)
            {
                return NotFound(ApiResponse<OfferDto>.Error($"Offer with ID {id} not found"));
            }

            var dto = MapOffer(oferta, includeItems);
            return Ok(ApiResponse<OfferDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting offer {Id}", id);
            return StatusCode(500, ApiResponse<OfferDto>.Error("Error retrieving offer", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get offer by number
    /// </summary>
    [HttpGet("by-number/{number}")]
    [ProducesResponseType(typeof(ApiResponse<OfferDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<OfferDto>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<OfferDto>> GetOfferByNumber(string number, [FromQuery] bool includeItems = true)
    {
        try
        {
            var sfera = _sferaService.GetSfera();
            var oferta = sfera.Oferty().Dane.Wszystkie()
                .FirstOrDefault(o => o.Dokument != null &&
                    o.Dokument.NumerWewnetrzny != null &&
                    o.Dokument.NumerWewnetrzny.PelnaSygnatura.Contains(number));

            if (oferta == null)
            {
                return NotFound(ApiResponse<OfferDto>.Error($"Offer with number {number} not found"));
            }

            var dto = MapOffer(oferta, includeItems);
            return Ok(ApiResponse<OfferDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting offer by number {Number}", number);
            return StatusCode(500, ApiResponse<OfferDto>.Error("Error retrieving offer", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get offers for a customer
    /// </summary>
    [HttpGet("for-customer/{customerId}")]
    [ProducesResponseType(typeof(ApiResponse<List<OfferSummaryDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<OfferSummaryDto>>> GetOffersForCustomer(
        int customerId,
        [FromQuery] bool? validOnly = true)
    {
        try
        {
            var sfera = _sferaService.GetSfera();
            var oferty = sfera.Oferty().Dane.Wszystkie()
                .Where(o => o.Dokument != null && o.Dokument.Podmiot != null &&
                    o.Dokument.Podmiot.Id == customerId);

            if (validOnly.HasValue && validOnly.Value)
            {
                var now = DateTime.Now;
                oferty = oferty.Where(o =>
                    (!o.ObowiazujeOd.HasValue || o.ObowiazujeOd.Value <= now) &&
                    (!o.ObowiazujeDo.HasValue || o.ObowiazujeDo.Value >= now) &&
                    o.Zamkniety != true);
            }

            var items = oferty
                .OrderByDescending(o => o.Dokument.DataWystawienia)
                .ToList()
                .Select(MapOfferSummary)
                .ToList();

            return Ok(ApiResponse<List<OfferSummaryDto>>.Ok(items));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting offers for customer {CustomerId}", customerId);
            return StatusCode(500, ApiResponse<List<OfferSummaryDto>>.Error("Error retrieving offers", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Create Offer

    /// <summary>
    /// Create a new offer
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<OfferDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<OfferDto>), StatusCodes.Status400BadRequest)]
    public ActionResult<ApiResponse<OfferDto>> CreateOffer([FromBody] CreateOfferRequest request)
    {
        try
        {
            var sfera = _sferaService.GetSfera();
            var oferty = sfera.Oferty();

            using (var oferta = oferty.Utwórz())
            {
                // Set customer
                var podmiot = sfera.Podmioty().Dane.Wszystkie()
                    .FirstOrDefault(p => p.Id == request.CustomerId);

                if (podmiot == null)
                {
                    return BadRequest(ApiResponse<OfferDto>.Error($"Customer with ID {request.CustomerId} not found"));
                }

                oferta.Dane.Dokument.Podmiot = podmiot;

                // Set validity dates
                if (request.ValidFrom.HasValue)
                {
                    oferta.Dane.ObowiazujeOd = request.ValidFrom.Value;
                }

                if (request.ValidTo.HasValue)
                {
                    oferta.Dane.ObowiazujeDo = request.ValidTo.Value;
                }

                // Set days to realization
                if (request.DaysToRealization.HasValue)
                {
                    oferta.Dane.DniDoRealizacji = request.DaysToRealization.Value;
                }

                // Set description
                if (!string.IsNullOrEmpty(request.Description))
                {
                    oferta.Dane.OpisKrotki = request.Description;
                }

                // Add items
                var asortymenty = sfera.Asortymenty();
                foreach (var item in request.Items)
                {
                    var asortyment = asortymenty.Dane.Wszystkie()
                        .FirstOrDefault(a => a.Id == item.ProductId);

                    if (asortyment != null)
                    {
                        var pozycja = oferta.Pozycje.Dodaj(asortyment, item.Quantity, asortyment.JednostkaSprzedazy);

                        if (item.UnitPriceNet.HasValue && pozycja != null)
                        {
                            pozycja.Dane.CenaNetto = item.UnitPriceNet.Value;
                        }

                        if (item.DiscountPercent.HasValue && pozycja != null)
                        {
                            pozycja.Dane.RabatProcent = item.DiscountPercent.Value;
                        }
                    }
                }

                if (oferta.Zapisz())
                {
                    _logger.LogInformation("Created offer {Number}",
                        oferta.Dane.Dokument?.NumerWewnetrzny?.PelnaSygnatura);

                    var dto = MapOffer(oferta.Dane, true);

                    return CreatedAtAction(
                        nameof(GetOffer),
                        new { id = oferta.Dane.Id },
                        ApiResponse<OfferDto>.Ok(dto, "Offer created successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(oferta);
                    return BadRequest(ApiResponse<OfferDto>.Error("Failed to create offer", errors));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating offer");
            return StatusCode(500, ApiResponse<OfferDto>.Error("Error creating offer", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Offer Actions

    /// <summary>
    /// Accept an offer (mark as accepted by customer)
    /// </summary>
    [HttpPost("{id}/accept")]
    [ProducesResponseType(typeof(ApiResponse<OfferDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<OfferDto>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<OfferDto>> AcceptOffer(int id)
    {
        try
        {
            var sfera = _sferaService.GetSfera();
            var oferty = sfera.Oferty();

            var ofertaDane = oferty.Dane.Wszystkie().FirstOrDefault(o => o.Id == id);
            if (ofertaDane == null)
            {
                return NotFound(ApiResponse<OfferDto>.Error($"Offer with ID {id} not found"));
            }

            using (var oferta = oferty.Znajdz(ofertaDane))
            {
                oferta.Dane.Zaakceptowany = true;
                oferta.Dane.DataZaakceptowania = DateTime.Now;
                oferta.Dane.DataOstatniejZmianyStatusu = DateTime.Now;

                if (oferta.Zapisz())
                {
                    _logger.LogInformation("Accepted offer {Id}", id);
                    var dto = MapOffer(oferta.Dane, false);
                    return Ok(ApiResponse<OfferDto>.Ok(dto, "Offer accepted successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(oferta);
                    return BadRequest(ApiResponse<OfferDto>.Error("Failed to accept offer", errors));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accepting offer {Id}", id);
            return StatusCode(500, ApiResponse<OfferDto>.Error("Error accepting offer", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Close an offer
    /// </summary>
    [HttpPost("{id}/close")]
    [ProducesResponseType(typeof(ApiResponse<OfferDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<OfferDto>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<OfferDto>> CloseOffer(int id)
    {
        try
        {
            var sfera = _sferaService.GetSfera();
            var oferty = sfera.Oferty();

            var ofertaDane = oferty.Dane.Wszystkie().FirstOrDefault(o => o.Id == id);
            if (ofertaDane == null)
            {
                return NotFound(ApiResponse<OfferDto>.Error($"Offer with ID {id} not found"));
            }

            using (var oferta = oferty.Znajdz(ofertaDane))
            {
                oferta.Dane.Zamkniety = true;
                oferta.Dane.DataOstatniejZmianyStatusu = DateTime.Now;

                if (oferta.Zapisz())
                {
                    _logger.LogInformation("Closed offer {Id}", id);
                    var dto = MapOffer(oferta.Dane, false);
                    return Ok(ApiResponse<OfferDto>.Ok(dto, "Offer closed successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(oferta);
                    return BadRequest(ApiResponse<OfferDto>.Error("Failed to close offer", errors));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing offer {Id}", id);
            return StatusCode(500, ApiResponse<OfferDto>.Error("Error closing offer", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Mapping

    private static OfferSummaryDto MapOfferSummary(DokumentOE o)
    {
        return new OfferSummaryDto
        {
            Id = o.Id,
            Number = o.Dokument?.NumerWewnetrzny?.PelnaSygnatura,
            IssueDate = o.Dokument?.DataWystawienia,
            ValidFrom = o.ObowiazujeOd,
            ValidTo = o.ObowiazujeDo,
            CustomerName = o.Dokument?.Podmiot?.NazwaSkrocona,
            Status = GetOfferStatus(o),
            IsClosed = o.Zamkniety ?? false,
            IsAccepted = o.Zaakceptowany ?? false,
            NetValue = o.Dokument?.WartoscNetto ?? 0,
            GrossValue = o.Dokument?.WartoscBrutto ?? 0,
            CurrencySymbol = o.Dokument?.Waluta?.Symbol ?? "PLN",
            ItemCount = o.Dokument?.Pozycje?.Count ?? 0
        };
    }

    private static OfferDto MapOffer(DokumentOE o, bool includeItems)
    {
        var dto = new OfferDto
        {
            Id = o.Id,
            Number = o.Dokument?.NumerWewnetrzny?.PelnaSygnatura,
            IssueDate = o.Dokument?.DataWystawienia,
            ValidFrom = o.ObowiazujeOd,
            ValidTo = o.ObowiazujeDo,
            CustomerId = o.Dokument?.Podmiot?.Id,
            CustomerSymbol = o.Dokument?.Podmiot?.Symbol,
            CustomerName = o.Dokument?.Podmiot?.NazwaSkrocona,
            CustomerTaxId = o.Dokument?.Podmiot?.NIP,
            Status = GetOfferStatus(o),
            IsClosed = o.Zamkniety ?? false,
            IsAccepted = o.Zaakceptowany ?? false,
            AcceptedDate = o.DataZaakceptowania,
            LastStatusChangeDate = o.DataOstatniejZmianyStatusu,
            SalesRepId = o.PrzedstawicielId,
            DaysToRealization = o.DniDoRealizacji,
            ShortDescription = o.OpisKrotki,
            EndDescription = o.OpisKoncowyKrotki,
            NetValue = o.Dokument?.WartoscNetto ?? 0,
            GrossValue = o.Dokument?.WartoscBrutto ?? 0,
            TaxValue = o.Dokument?.WartoscVat ?? 0,
            CurrencySymbol = o.Dokument?.Waluta?.Symbol ?? "PLN",
            ItemCount = o.Dokument?.Pozycje?.Count ?? 0
        };

        if (includeItems && o.Dokument?.Pozycje != null)
        {
            dto.Items = new List<OfferItemDto>();
            int lineNum = 1;
            foreach (var poz in o.Dokument.Pozycje)
            {
                dto.Items.Add(new OfferItemDto
                {
                    Id = poz.Id,
                    LineNumber = lineNum++,
                    ProductId = poz.Asortyment?.Id,
                    ProductSymbol = poz.Asortyment?.Symbol,
                    ProductName = poz.Nazwa,
                    ProductDescription = poz.Asortyment?.Opis,
                    Quantity = poz.Ilosc,
                    UnitSymbol = poz.Jednostka?.Symbol ?? "szt.",
                    UnitPriceNet = poz.CenaNetto,
                    UnitPriceGross = poz.CenaBrutto,
                    DiscountPercent = poz.RabatProcent,
                    DiscountValue = poz.RabatWartosc ?? 0,
                    NetValue = poz.WartoscNetto,
                    GrossValue = poz.WartoscBrutto,
                    TaxValue = poz.WartoscVat,
                    VatRateSymbol = poz.StawkaVat?.Symbol,
                    VatRate = poz.StawkaVat?.Stawka
                });
            }
        }

        return dto;
    }

    private static string GetOfferStatus(DokumentOE o)
    {
        if (o.Zamkniety == true)
        {
            return o.Zaakceptowany == true ? "Accepted & Closed" : "Rejected";
        }

        if (o.Zaakceptowany == true)
        {
            return "Accepted";
        }

        var now = DateTime.Now;
        if (o.ObowiazujeDo.HasValue && o.ObowiazujeDo.Value < now)
        {
            return "Expired";
        }

        if (o.ObowiazujeOd.HasValue && o.ObowiazujeOd.Value > now)
        {
            return "Pending";
        }

        return "Active";
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

    #endregion
}
