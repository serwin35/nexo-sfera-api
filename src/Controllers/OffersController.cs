using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

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
    public async Task<ActionResult<PagedResponse<OfferSummaryDto>>> GetOffers(
        [FromQuery] int? customerId = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] bool? closedOnly = null,
        [FromQuery] bool? acceptedOnly = null,
        [FromQuery] bool? validOnly = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var response = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var ofertyManager = _sferaService.GetManager("Oferty");
                if (ofertyManager == null)
                {
                    return (PagedResponse<OfferSummaryDto>?)null;
                }

                var oferty = new List<object>();
                foreach (dynamic o in DynamicPropertyHelper.SafeGetAll((object)ofertyManager))
                {
                    oferty.Add(o);
                }

                if (customerId.HasValue)
                {
                    var filtered = new List<object>();
                    foreach (dynamic o in oferty)
                    {
                        try { if (o.Podmiot?.Id == customerId.Value) filtered.Add(o); }
                        catch { }
                    }
                    oferty = filtered;
                }

                if (dateFrom.HasValue)
                {
                    var filtered = new List<object>();
                    foreach (dynamic o in oferty)
                    {
                        try { if ((DateTime?)o.DataWystawienia >= dateFrom.Value) filtered.Add(o); }
                        catch { }
                    }
                    oferty = filtered;
                }

                if (dateTo.HasValue)
                {
                    var filtered = new List<object>();
                    foreach (dynamic o in oferty)
                    {
                        try { if ((DateTime?)o.DataWystawienia <= dateTo.Value) filtered.Add(o); }
                        catch { }
                    }
                    oferty = filtered;
                }

                if (closedOnly.HasValue && closedOnly.Value)
                {
                    var filtered = new List<object>();
                    foreach (dynamic o in oferty)
                    {
                        try { if ((bool?)o.Zamkniety == true) filtered.Add(o); }
                        catch { }
                    }
                    oferty = filtered;
                }

                if (acceptedOnly.HasValue && acceptedOnly.Value)
                {
                    var filtered = new List<object>();
                    foreach (dynamic o in oferty)
                    {
                        try { if ((bool?)o.Zaakceptowany == true) filtered.Add(o); }
                        catch { }
                    }
                    oferty = filtered;
                }

                if (validOnly.HasValue && validOnly.Value)
                {
                    var now = DateTime.Now;
                    var filtered = new List<object>();
                    foreach (dynamic o in oferty)
                    {
                        try
                        {
                            var od = (DateTime?)o.ObowiazujeOd;
                            var doo = (DateTime?)o.ObowiazujeDo;
                            if ((!od.HasValue || od.Value <= now) && (!doo.HasValue || doo.Value >= now))
                            {
                                filtered.Add(o);
                            }
                        }
                        catch { }
                    }
                    oferty = filtered;
                }

                // Sort by DataWystawienia descending
                var sortedOferty = new List<(dynamic oferta, DateTime? data)>();
                foreach (dynamic o in oferty)
                {
                    DateTime? data = null;
                    try { data = (DateTime?)o.DataWystawienia; } catch { }
                    sortedOferty.Add((o, data));
                }
                sortedOferty = sortedOferty.OrderByDescending(x => x.data).ToList();

                var totalCount = oferty.Count;
                var pagedOferty = sortedOferty
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var items = new List<OfferSummaryDto>();
                foreach (var item in pagedOferty)
                {
                    items.Add(MapOfferSummary(item.oferta));
                }

                return new PagedResponse<OfferSummaryDto>
                {
                    Data = items,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };
            });

            if (response == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Oferty manager"));
            }

            return Ok(response);
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
    public async Task<ActionResult<ApiResponse<OfferDto>>> GetOffer(int id, [FromQuery] bool includeItems = true)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var ofertyManager = _sferaService.GetManager("Oferty");
                if (ofertyManager == null)
                    return (Found: false, ManagerMissing: true, Dto: (OfferDto?)null);

                dynamic? oferta = null;
                foreach (dynamic o in DynamicPropertyHelper.SafeGetAll((object)ofertyManager))
                {
                    if ((int)o.Id == id)
                    {
                        oferta = o;
                        break;
                    }
                }

                if (oferta == null)
                    return (Found: false, ManagerMissing: false, Dto: (OfferDto?)null);

                return (Found: true, ManagerMissing: false, Dto: (OfferDto?)MapOffer(oferta, includeItems));
            });

            if (result.ManagerMissing)
                return StatusCode(500, ApiResponse<OfferDto>.Error("Failed to get Oferty manager"));
            if (!result.Found)
                return NotFound(ApiResponse<OfferDto>.Error($"Offer with ID {id} not found"));
            return Ok(ApiResponse<OfferDto>.Ok(result.Dto!));
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
    public async Task<ActionResult<ApiResponse<OfferDto>>> GetOfferByNumber(string number, [FromQuery] bool includeItems = true)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var ofertyManager = _sferaService.GetManager("Oferty");
                if (ofertyManager == null)
                    return (Found: false, ManagerMissing: true, Dto: (OfferDto?)null);

                dynamic? oferta = null;
                foreach (dynamic o in DynamicPropertyHelper.SafeGetAll((object)ofertyManager))
                {
                    try
                    {
                        string? sygnatura = o.NumerWewnetrzny?.PelnaSygnatura;
                        if (sygnatura != null && sygnatura.Contains(number))
                        {
                            oferta = o;
                            break;
                        }
                    }
                    catch { }
                }

                if (oferta == null)
                    return (Found: false, ManagerMissing: false, Dto: (OfferDto?)null);

                return (Found: true, ManagerMissing: false, Dto: (OfferDto?)MapOffer(oferta, includeItems));
            });

            if (result.ManagerMissing)
                return StatusCode(500, ApiResponse<OfferDto>.Error("Failed to get Oferty manager"));
            if (!result.Found)
                return NotFound(ApiResponse<OfferDto>.Error($"Offer with number {number} not found"));
            return Ok(ApiResponse<OfferDto>.Ok(result.Dto!));
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
    public async Task<ActionResult<ApiResponse<List<OfferSummaryDto>>>> GetOffersForCustomer(
        int customerId,
        [FromQuery] bool? validOnly = true)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var ofertyManager = _sferaService.GetManager("Oferty");
                if (ofertyManager == null)
                    return (Found: false, Items: (List<OfferSummaryDto>?)null);

                var oferty = new List<object>();
                foreach (dynamic o in DynamicPropertyHelper.SafeGetAll((object)ofertyManager))
                {
                    try { if (o.Podmiot?.Id == customerId) oferty.Add(o); }
                    catch { }
                }

                if (validOnly.HasValue && validOnly.Value)
                {
                    var now = DateTime.Now;
                    var filtered = new List<object>();
                    foreach (dynamic o in oferty)
                    {
                        try
                        {
                            var od = (DateTime?)o.ObowiazujeOd;
                            var doo = (DateTime?)o.ObowiazujeDo;
                            var zamkniety = (bool?)o.Zamkniety;
                            if ((!od.HasValue || od.Value <= now) && (!doo.HasValue || doo.Value >= now) && zamkniety != true)
                            {
                                filtered.Add(o);
                            }
                        }
                        catch { }
                    }
                    oferty = filtered;
                }

                // Sort by DataWystawienia descending
                var sortedOferty = new List<(dynamic oferta, DateTime? data)>();
                foreach (dynamic o in oferty)
                {
                    DateTime? data = null;
                    try { data = (DateTime?)o.DataWystawienia; } catch { }
                    sortedOferty.Add((o, data));
                }
                sortedOferty = sortedOferty.OrderByDescending(x => x.data).ToList();

                var items = new List<OfferSummaryDto>();
                foreach (var item in sortedOferty)
                {
                    items.Add(MapOfferSummary(item.oferta));
                }

                return (Found: true, Items: (List<OfferSummaryDto>?)items);
            });

            if (!result.Found)
                return StatusCode(500, ApiResponse<List<OfferSummaryDto>>.Error("Failed to get Oferty manager"));
            return Ok(ApiResponse<List<OfferSummaryDto>>.Ok(result.Items!));
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
    /// Create a new offer (Oferta OE)
    /// </summary>
    /// <remarks>
    /// IMPORTANT: This endpoint requires WindowsFormsSynchronizationContext on the SDK thread.
    /// The document creation pattern is:
    /// 1. Set customer on Dane.Podmiot
    /// 2. Call ZarezerwujNumer() to reserve document number
    /// 3. Add items using Pozycje.Dodaj(towarId)
    /// 4. Call Zapisz() to save
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<OfferDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<OfferDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<OfferDto>>> CreateOffer([FromBody] CreateOfferRequest request)
    {
        try
        {
            // Use thread-safe execution - EF6 is NOT thread-safe
            var result = await _sferaService.ExecuteWithLockAsync<(bool Success, OfferDto? Data, string Message, List<string> Errors)>(() =>
            {
                var ofertyManager = _sferaService.GetManager("Oferty");
                var podmiotyManager = _sferaService.GetManager("Podmioty");
                var asortymentyManager = _sferaService.GetManager("Asortymenty");
                if (ofertyManager == null)
                {
                    return (false, null, "Failed to get Oferty manager", new List<string>());
                }

                using (var oferta = ofertyManager.Utwórz())
                {
                    // Set customer
                    dynamic? podmiot = null;
                    if (podmiotyManager != null)
                    {
                        foreach (dynamic p in DynamicPropertyHelper.SafeGetAll((object)podmiotyManager))
                        {
                            if ((int)p.Id == request.CustomerId)
                            {
                                podmiot = p;
                                break;
                            }
                        }
                    }

                    if (podmiot == null)
                    {
                        return (false, null, $"Customer with ID {request.CustomerId} not found", new List<string>());
                    }

                    oferta.Dane.Podmiot = podmiot;

                    // Reserve number - only if ReserveNumber flag is true
                    // Otherwise number is auto-assigned during Zapisz()
                    if (request.ReserveNumber)
                    {
                        oferta.ZarezerwujNumer();
                        _logger.LogInformation("Reserved offer number: {Number}", (string?)oferta.PodajPodgladNumeru()?.ToString() ?? "");
                    }
                    else
                    {
                        _logger.LogInformation("Offer number will be auto-assigned during save (preview: {Number})", (string?)oferta.PodajPodgladNumeru()?.ToString() ?? "Auto");
                    }

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

                    // Add items using product ID pattern
                    if (asortymentyManager != null)
                    {
                        foreach (var item in request.Items)
                        {
                            dynamic? asortyment = null;
                            foreach (dynamic a in DynamicPropertyHelper.SafeGetAll((object)asortymentyManager))
                            {
                                if ((int)a.Id == item.ProductId)
                                {
                                    asortyment = a;
                                    break;
                                }
                            }

                            if (asortyment != null)
                            {
                                int towarId = (int)asortyment.Id;
                                // CRITICAL: Use Pozycje.Dodaj(towarId) pattern for EF6 compatibility
                                var pozycja = oferta.Pozycje.Dodaj(towarId);

                                if (pozycja != null)
                                {
                                    pozycja.Ilosc = item.Quantity;

                                    if (item.UnitPriceNet.HasValue)
                                    {
                                        pozycja.CenaNetto = item.UnitPriceNet.Value;
                                    }

                                    if (item.DiscountPercent.HasValue)
                                    {
                                        pozycja.RabatProcent = item.DiscountPercent.Value;
                                    }
                                }
                            }
                        }
                    }

                    if (oferta.Zapisz())
                    {
                        string docNumber = oferta.PodajPodgladNumeru()?.ToString() ?? "";
                        int docId = (int)oferta.Dane.Id;
                        _logger.LogInformation("Created offer {Number}, Id={Id}", docNumber, docId);

                        var dto = MapOffer(oferta.Dane, true);
                        return (true, dto, "Offer created successfully", new List<string>());
                    }
                    else
                    {
                        var errors = GetBusinessObjectErrors(oferta);
                        return (false, null, "Failed to create offer", errors);
                    }
                }
            });

            if (result.Success)
            {
                return CreatedAtAction(nameof(GetOffer), new { id = result.Data?.Id }, ApiResponse<OfferDto>.Ok(result.Data!, result.Message));
            }
            else if (result.Errors.Any())
            {
                return BadRequest(ApiResponse<OfferDto>.Error(result.Message, result.Errors));
            }
            else
            {
                return StatusCode(500, ApiResponse<OfferDto>.Error(result.Message));
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
    public async Task<ActionResult<ApiResponse<OfferDto>>> AcceptOffer(int id)
    {
        try
        {
            // Use thread-safe execution - EF6 is NOT thread-safe
            var result = await _sferaService.ExecuteWithLockAsync<(bool Success, OfferDto? Data, string Message, List<string> Errors)>(() =>
            {
                var ofertyManager = _sferaService.GetManager("Oferty");
                if (ofertyManager == null)
                {
                    return (false, null, "Failed to get Oferty manager", new List<string>());
                }

                dynamic? ofertaDane = null;
                foreach (dynamic o in DynamicPropertyHelper.SafeGetAll((object)ofertyManager))
                {
                    if ((int)o.Id == id)
                    {
                        ofertaDane = o;
                        break;
                    }
                }

                if (ofertaDane == null)
                {
                    return (false, null, $"Offer with ID {id} not found", new List<string>());
                }

                using (var oferta = ofertyManager.Znajdz(ofertaDane))
                {
                    oferta.Dane.Zaakceptowany = true;
                    oferta.Dane.DataZaakceptowania = DateTime.Now;
                    oferta.Dane.DataOstatniejZmianyStatusu = DateTime.Now;

                    if (oferta.Zapisz())
                    {
                        _logger.LogInformation("Accepted offer {Id}", id);
                        var dto = MapOffer(oferta.Dane, false);
                        return (true, dto, "Offer accepted successfully", new List<string>());
                    }
                    else
                    {
                        var errors = GetBusinessObjectErrors(oferta);
                        return (false, null, "Failed to accept offer", errors);
                    }
                }
            });

            if (result.Success)
            {
                return Ok(ApiResponse<OfferDto>.Ok(result.Data!, result.Message));
            }
            else if (result.Message.Contains("not found"))
            {
                return NotFound(ApiResponse<OfferDto>.Error(result.Message));
            }
            else if (result.Errors.Any())
            {
                return BadRequest(ApiResponse<OfferDto>.Error(result.Message, result.Errors));
            }
            else
            {
                return StatusCode(500, ApiResponse<OfferDto>.Error(result.Message));
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
    public async Task<ActionResult<ApiResponse<OfferDto>>> CloseOffer(int id)
    {
        try
        {
            // Use thread-safe execution - EF6 is NOT thread-safe
            var result = await _sferaService.ExecuteWithLockAsync<(bool Success, OfferDto? Data, string Message, List<string> Errors)>(() =>
            {
                var ofertyManager = _sferaService.GetManager("Oferty");
                if (ofertyManager == null)
                {
                    return (false, null, "Failed to get Oferty manager", new List<string>());
                }

                dynamic? ofertaDane = null;
                foreach (dynamic o in DynamicPropertyHelper.SafeGetAll((object)ofertyManager))
                {
                    if ((int)o.Id == id)
                    {
                        ofertaDane = o;
                        break;
                    }
                }

                if (ofertaDane == null)
                {
                    return (false, null, $"Offer with ID {id} not found", new List<string>());
                }

                using (var oferta = ofertyManager.Znajdz(ofertaDane))
                {
                    oferta.Dane.Zamkniety = true;
                    oferta.Dane.DataOstatniejZmianyStatusu = DateTime.Now;

                    if (oferta.Zapisz())
                    {
                        _logger.LogInformation("Closed offer {Id}", id);
                        var dto = MapOffer(oferta.Dane, false);
                        return (true, dto, "Offer closed successfully", new List<string>());
                    }
                    else
                    {
                        var errors = GetBusinessObjectErrors(oferta);
                        return (false, null, "Failed to close offer", errors);
                    }
                }
            });

            if (result.Success)
            {
                return Ok(ApiResponse<OfferDto>.Ok(result.Data!, result.Message));
            }
            else if (result.Message.Contains("not found"))
            {
                return NotFound(ApiResponse<OfferDto>.Error(result.Message));
            }
            else if (result.Errors.Any())
            {
                return BadRequest(ApiResponse<OfferDto>.Error(result.Message, result.Errors));
            }
            else
            {
                return StatusCode(500, ApiResponse<OfferDto>.Error(result.Message));
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

    private static OfferSummaryDto MapOfferSummary(dynamic o)
    {
        try
        {
            return new OfferSummaryDto
            {
                Id = (int)o.Id,
                Number = GetDynamicString(o, "NumerWewnetrzny", "PelnaSygnatura"),
                IssueDate = GetDynamicDateTime(o, "DataWystawienia"),
                ValidFrom = GetDynamicDateTime(o, "ObowiazujeOd"),
                ValidTo = GetDynamicDateTime(o, "ObowiazujeDo"),
                CustomerName = GetDynamicString(o, "Podmiot", "NazwaSkrocona"),
                Status = GetOfferStatus(o),
                IsClosed = GetDynamicBool(o, "Zamkniety"),
                IsAccepted = GetDynamicBool(o, "Zaakceptowany"),
                NetValue = GetDynamicDecimal(o, "WartoscNetto"),
                GrossValue = GetDynamicDecimal(o, "WartoscBrutto"),
                CurrencySymbol = GetDynamicString(o, "Waluta", "Symbol") ?? "PLN",
                ItemCount = GetDynamicInt(o, "Pozycje", "Count")
            };
        }
        catch
        {
            return new OfferSummaryDto { Id = (int)o.Id };
        }
    }

    private static OfferDto MapOffer(dynamic o, bool includeItems)
    {
        var dto = new OfferDto
        {
            Id = (int)o.Id,
            Number = GetDynamicString(o, "NumerWewnetrzny", "PelnaSygnatura"),
            IssueDate = GetDynamicDateTime(o, "DataWystawienia"),
            ValidFrom = GetDynamicDateTime(o, "ObowiazujeOd"),
            ValidTo = GetDynamicDateTime(o, "ObowiazujeDo"),
            CustomerId = GetDynamicNullableInt(o, "Podmiot", "Id"),
            CustomerSymbol = GetDynamicString(o, "Podmiot", "Symbol"),
            CustomerName = GetDynamicString(o, "Podmiot", "NazwaSkrocona"),
            CustomerTaxId = GetDynamicString(o, "Podmiot", "NIP"),
            Status = GetOfferStatus(o),
            IsClosed = GetDynamicBool(o, "Zamkniety"),
            IsAccepted = GetDynamicBool(o, "Zaakceptowany"),
            AcceptedDate = GetDynamicDateTime(o, "DataZaakceptowania"),
            LastStatusChangeDate = GetDynamicDateTime(o, "DataOstatniejZmianyStatusu"),
            SalesRepId = GetDynamicNullableInt(o, "PrzedstawicielId"),
            DaysToRealization = GetDynamicNullableInt(o, "DniDoRealizacji"),
            ShortDescription = GetDynamicString(o, "OpisKrotki"),
            EndDescription = GetDynamicString(o, "OpisKoncowyKrotki"),
            NetValue = GetDynamicDecimal(o, "WartoscNetto"),
            GrossValue = GetDynamicDecimal(o, "WartoscBrutto"),
            TaxValue = GetDynamicDecimal(o, "WartoscVat"),
            CurrencySymbol = GetDynamicString(o, "Waluta", "Symbol") ?? "PLN",
            ItemCount = GetDynamicInt(o, "Pozycje", "Count"),
            // SDK 60.0.0: ProcesOfertowy.Utworzono
            CreatedAt = GetDynamicDateTimeOffset(o, "Utworzono")
        };

        if (includeItems)
        {
            try
            {
                var pozycje = o.Pozycje;
                if (pozycje != null)
                {
                    dto.Items = new List<OfferItemDto>();
                    int lineNum = 1;
                    foreach (var poz in pozycje)
                    {
                        var dane = DynamicPropertyHelper.GetDane(poz);
                        var quantity = DynamicPropertyHelper.GetDecimalFirstOf(dane, "Ilosc", "IloscJednostek");
                        var priceNet = DynamicPropertyHelper.GetDecimalFirstOf(dane, "CenaNetto", "CenaJednostkowaNetto", "CenaJednostkowa", "Cena");
                        var priceGross = DynamicPropertyHelper.GetDecimalFirstOf(dane, "CenaBrutto", "CenaJednostkowaBrutto");
                        var valueNet = DynamicPropertyHelper.GetDecimalFirstOf(dane, "WartoscNetto", "Wartosc");
                        var valueGross = DynamicPropertyHelper.GetDecimalFirstOf(dane, "WartoscBrutto");

                        dto.Items.Add(new OfferItemDto
                        {
                            Id = DynamicPropertyHelper.GetId(poz),
                            LineNumber = lineNum++,
                            ProductId = DynamicPropertyHelper.GetNullableInt(dane, "Asortyment", "Id")
                                     ?? GetDynamicNullableInt(poz, "Asortyment", "Id"),
                            ProductSymbol = DynamicPropertyHelper.GetString(dane, "Asortyment", "Symbol")
                                         ?? GetDynamicString(poz, "Asortyment", "Symbol"),
                            ProductName = DynamicPropertyHelper.GetString(dane, "Nazwa")
                                       ?? GetDynamicString(poz, "Nazwa"),
                            ProductDescription = DynamicPropertyHelper.GetString(dane, "Asortyment", "Opis")
                                              ?? GetDynamicString(poz, "Asortyment", "Opis"),
                            Quantity = quantity,
                            UnitSymbol = DynamicPropertyHelper.GetString(dane, "Jednostka", "Symbol")
                                      ?? GetDynamicString(poz, "Jednostka", "Symbol") ?? "szt.",
                            UnitPriceNet = priceNet != 0 ? priceNet : (valueNet != 0 && quantity != 0 ? valueNet / quantity : 0),
                            UnitPriceGross = priceGross != 0 ? priceGross : (valueGross != 0 && quantity != 0 ? valueGross / quantity : 0),
                            DiscountPercent = DynamicPropertyHelper.GetNullableDecimal(dane, "RabatProcent")
                                           ?? GetDynamicNullableDecimal(poz, "RabatProcent"),
                            DiscountValue = DynamicPropertyHelper.GetDecimalFirstOf(dane, "RabatWartosc", "RabatKwota"),
                            NetValue = valueNet,
                            GrossValue = valueGross,
                            TaxValue = DynamicPropertyHelper.GetDecimalFirstOf(dane, "WartoscVat"),
                            VatRateSymbol = DynamicPropertyHelper.GetString(dane, "StawkaVat", "Symbol")
                                         ?? GetDynamicString(poz, "StawkaVat", "Symbol"),
                            VatRate = DynamicPropertyHelper.GetNullableDecimal(dane, "StawkaVat", "Stawka")
                                   ?? GetDynamicNullableDecimal(poz, "StawkaVat", "Stawka")
                        });
                    }
                }
            }
            catch { }
        }

        return dto;
    }

    private static string GetOfferStatus(dynamic o)
    {
        try
        {
            bool zamkniety = GetDynamicBool(o, "Zamkniety");
            bool zaakceptowany = GetDynamicBool(o, "Zaakceptowany");

            if (zamkniety)
            {
                return zaakceptowany ? "Accepted & Closed" : "Rejected";
            }

            if (zaakceptowany)
            {
                return "Accepted";
            }

            var now = DateTime.Now;
            var obowiazujeDo = GetDynamicDateTime(o, "ObowiazujeDo");
            var obowiazujeOd = GetDynamicDateTime(o, "ObowiazujeOd");

            if (obowiazujeDo.HasValue && obowiazujeDo.Value < now)
            {
                return "Expired";
            }

            if (obowiazujeOd.HasValue && obowiazujeOd.Value > now)
            {
                return "Pending";
            }

            return "Active";
        }
        catch
        {
            return "Unknown";
        }
    }

    private static List<string> GetBusinessObjectErrors(dynamic obiekt)
    {
        var errors = new List<string>();
        try
        {
            var invalidData = obiekt.InvalidData;
            if (invalidData != null)
            {
                foreach (var encjaZBledami in invalidData)
                {
                    try
                    {
                        foreach (var blad in encjaZBledami.Errors)
                        {
                            errors.Add(blad.ToString());
                        }
                    }
                    catch { }
                    try
                    {
                        foreach (var bladNaPolach in encjaZBledami.MemberErrors)
                        {
                            errors.Add($"{bladNaPolach.Key}: {string.Join(", ", bladNaPolach)}");
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }
        return errors;
    }

    #region Dynamic Property Helpers

    private static string? GetDynamicString(dynamic obj, string prop1, string? prop2 = null)
    {
        try
        {
            dynamic val = GetProperty(obj, prop1);
            if (val == null) return null;
            if (prop2 != null) val = GetProperty(val, prop2);
            return val?.ToString();
        }
        catch { return null; }
    }

    private static DateTime? GetDynamicDateTime(dynamic obj, string prop1, string? prop2 = null)
    {
        try
        {
            dynamic val = GetProperty(obj, prop1);
            if (val == null) return null;
            if (prop2 != null) val = GetProperty(val, prop2);
            return (DateTime?)val;
        }
        catch { return null; }
    }

    private static DateTimeOffset? GetDynamicDateTimeOffset(dynamic obj, string prop1)
    {
        try
        {
            dynamic val = GetProperty(obj, prop1);
            if (val == null) return null;
            return (DateTimeOffset?)val;
        }
        catch { return null; }
    }

    private static decimal GetDynamicDecimal(dynamic obj, string prop1, string? prop2 = null)
    {
        try
        {
            dynamic val = GetProperty(obj, prop1);
            if (val == null) return 0;
            if (prop2 != null) val = GetProperty(val, prop2);
            return (decimal)val;
        }
        catch { return 0; }
    }

    private static decimal? GetDynamicNullableDecimal(dynamic obj, string prop1, string? prop2 = null)
    {
        try
        {
            dynamic val = GetProperty(obj, prop1);
            if (val == null) return null;
            if (prop2 != null) val = GetProperty(val, prop2);
            return (decimal?)val;
        }
        catch { return null; }
    }

    private static int GetDynamicInt(dynamic obj, string prop1, string? prop2 = null)
    {
        try
        {
            dynamic val = GetProperty(obj, prop1);
            if (val == null) return 0;
            if (prop2 != null) val = GetProperty(val, prop2);
            return (int)val;
        }
        catch { return 0; }
    }

    private static int? GetDynamicNullableInt(dynamic obj, string prop1, string? prop2 = null)
    {
        try
        {
            dynamic val = GetProperty(obj, prop1);
            if (val == null) return null;
            if (prop2 != null) val = GetProperty(val, prop2);
            return (int?)val;
        }
        catch { return null; }
    }

    private static bool GetDynamicBool(dynamic obj, string prop1, string? prop2 = null)
    {
        try
        {
            dynamic val = GetProperty(obj, prop1);
            if (val == null) return false;
            if (prop2 != null) val = GetProperty(val, prop2);
            return (bool)val;
        }
        catch { return false; }
    }

    private static dynamic? GetProperty(dynamic obj, string propertyName)
    {
        try
        {
            var type = obj.GetType();
            var prop = type.GetProperty(propertyName);
            return prop?.GetValue(obj);
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #endregion
}
