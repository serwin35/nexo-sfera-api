using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Requests;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Customer Orders (ZK - Zamówienia od Klientów) management endpoints
/// </summary>
[ApiController]
[Route("api/customer-orders")]
[Authorize]
[Tags("Customer Orders")]
public class CustomerOrdersController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<CustomerOrdersController> _logger;

    public CustomerOrdersController(ISferaService sferaService, ILogger<CustomerOrdersController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get all customer orders with optional filtering
    /// </summary>
    [HttpGet]
    public ActionResult<PagedResponse<CustomerOrderListItemDto>> GetCustomerOrders([FromQuery] CustomerOrderQueryRequest query)
    {
        try
        {
            var zamowienia = _sferaService.GetManager("ZamowieniaOdKlientow");
            if (zamowienia == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get ZamowieniaOdKlientow manager"));
            }

            var allZamowienia = new List<dynamic>();
            foreach (var z in zamowienia.Dane.Wszystkie())
            {
                allZamowienia.Add(z);
            }

            // Apply filters
            var filteredList = new List<dynamic>();
            foreach (var z in allZamowienia)
            {
                // Customer ID filter
                if (query.CustomerId.HasValue)
                {
                    var podmiot = DynamicPropertyHelper.GetProperty(z, "Podmiot");
                    if (podmiot == null || DynamicPropertyHelper.GetId(podmiot) != query.CustomerId.Value)
                        continue;
                }

                // Customer NIP filter
                if (!string.IsNullOrEmpty(query.CustomerNIP))
                {
                    var podmiot = DynamicPropertyHelper.GetProperty(z, "Podmiot");
                    var nip = podmiot != null ? DynamicPropertyHelper.GetString(podmiot, "NIP") : null;
                    if (nip != query.CustomerNIP)
                        continue;
                }

                // Warehouse symbol filter
                if (!string.IsNullOrEmpty(query.WarehouseSymbol))
                {
                    var magazyn = DynamicPropertyHelper.GetProperty(z, "Magazyn");
                    var symbol = magazyn != null ? DynamicPropertyHelper.GetString(magazyn, "Symbol") : null;
                    if (symbol != query.WarehouseSymbol)
                        continue;
                }

                // Date range filter (issue date)
                if (query.DateFrom.HasValue)
                {
                    var issueDate = DynamicPropertyHelper.GetDateTime(z, "DataWystawienia");
                    if (issueDate == null || issueDate < query.DateFrom.Value)
                        continue;
                }

                if (query.DateTo.HasValue)
                {
                    var issueDate = DynamicPropertyHelper.GetDateTime(z, "DataWystawienia");
                    if (issueDate == null || issueDate > query.DateTo.Value)
                        continue;
                }

                // Deadline date range filter
                if (query.DeadlineFrom.HasValue)
                {
                    var deadline = DynamicPropertyHelper.GetDateTime(z, "TerminRealizacji");
                    if (deadline == null || deadline < query.DeadlineFrom.Value)
                        continue;
                }

                if (query.DeadlineTo.HasValue)
                {
                    var deadline = DynamicPropertyHelper.GetDateTime(z, "TerminRealizacji");
                    if (deadline == null || deadline > query.DeadlineTo.Value)
                        continue;
                }

                // Status filter
                if (query.StatusId.HasValue)
                {
                    var status = DynamicPropertyHelper.GetProperty(z, "Status");
                    var statusId = status != null ? DynamicPropertyHelper.GetId(status) : 0;
                    if (statusId != query.StatusId.Value)
                        continue;
                }

                // Closed filter
                if (query.IsClosed.HasValue)
                {
                    var isClosed = DynamicPropertyHelper.GetNullableBool(z, "Zamkniety") ?? false;
                    if (isClosed != query.IsClosed.Value)
                        continue;
                }

                // Overdue filter
                if (query.IsOverdue.HasValue && query.IsOverdue.Value)
                {
                    var deadline = DynamicPropertyHelper.GetDateTime(z, "TerminRealizacji");
                    var isClosed = DynamicPropertyHelper.GetNullableBool(z, "Zamkniety") ?? false;
                    if (isClosed || deadline == null || deadline >= DateTime.Today)
                        continue;
                }

                // Search filter
                if (!string.IsNullOrEmpty(query.Search))
                {
                    var searchLower = query.Search.ToLower();
                    var numerWewn = DynamicPropertyHelper.GetProperty(z, "NumerWewnetrzny");
                    var number = numerWewn != null ? DynamicPropertyHelper.GetString(numerWewn, "PelnaSygnatura")?.ToLower() ?? "" : "";
                    var externalNum = (DynamicPropertyHelper.GetString(z, "NumerZewnetrzny") ?? "").ToLower();
                    var podmiot = DynamicPropertyHelper.GetProperty(z, "Podmiot");
                    var customerName = podmiot != null ? (DynamicPropertyHelper.GetString(podmiot, "NazwaSkrocona") ?? "").ToLower() : "";

                    if (!number.Contains(searchLower) && !externalNum.Contains(searchLower) && !customerName.Contains(searchLower))
                        continue;
                }

                filteredList.Add(z);
            }

            var totalCount = filteredList.Count;

            // Sorting
            IEnumerable<dynamic> sortedList = filteredList;
            if (!string.IsNullOrEmpty(query.SortBy))
            {
                sortedList = query.SortBy.ToLower() switch
                {
                    "deadline" => query.SortDesc
                        ? filteredList.OrderByDescending(z => DynamicPropertyHelper.GetDateTime(z, "TerminRealizacji"))
                        : filteredList.OrderBy(z => DynamicPropertyHelper.GetDateTime(z, "TerminRealizacji")),
                    "customer" => query.SortDesc
                        ? filteredList.OrderByDescending(z => DynamicPropertyHelper.GetProperty(z, "Podmiot") != null
                            ? DynamicPropertyHelper.GetString(DynamicPropertyHelper.GetProperty(z, "Podmiot"), "NazwaSkrocona") : "")
                        : filteredList.OrderBy(z => DynamicPropertyHelper.GetProperty(z, "Podmiot") != null
                            ? DynamicPropertyHelper.GetString(DynamicPropertyHelper.GetProperty(z, "Podmiot"), "NazwaSkrocona") : ""),
                    "total" => query.SortDesc
                        ? filteredList.OrderByDescending(z => DynamicPropertyHelper.GetDecimal(z, "WartoscBrutto"))
                        : filteredList.OrderBy(z => DynamicPropertyHelper.GetDecimal(z, "WartoscBrutto")),
                    _ => query.SortDesc
                        ? filteredList.OrderByDescending(z => DynamicPropertyHelper.GetDateTime(z, "DataWystawienia"))
                        : filteredList.OrderBy(z => DynamicPropertyHelper.GetDateTime(z, "DataWystawienia"))
                };
            }
            else
            {
                sortedList = filteredList.OrderByDescending(z => DynamicPropertyHelper.GetDateTime(z, "DataWystawienia"));
            }

            var pagedItems = sortedList
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToList();

            var items = new List<CustomerOrderListItemDto>();
            foreach (var z in pagedItems)
            {
                items.Add(MapToListItemDto(z));
            }

            var response = new PagedResponse<CustomerOrderListItemDto>
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
            _logger.LogError(ex, "Error getting customer orders");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving customer orders", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get customer order by ID
    /// </summary>
    [HttpGet("{id}")]
    public ActionResult<ApiResponse<CustomerOrderDto>> GetCustomerOrder(int id)
    {
        try
        {
            var zamowienia = _sferaService.GetManager("ZamowieniaOdKlientow");
            if (zamowienia == null)
            {
                return StatusCode(500, ApiResponse<CustomerOrderDto>.Error("Failed to get ZamowieniaOdKlientow manager"));
            }

            dynamic? zamowienie = null;
            foreach (var z in zamowienia.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetId(z) == id)
                {
                    zamowienie = z;
                    break;
                }
            }

            if (zamowienie == null)
            {
                return NotFound(ApiResponse<CustomerOrderDto>.Error($"Customer order with ID {id} not found"));
            }

            return Ok(ApiResponse<CustomerOrderDto>.Ok(MapToDto(zamowienie)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting customer order {Id}", id);
            return StatusCode(500, ApiResponse<CustomerOrderDto>.Error("Error retrieving customer order", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get customer order by number
    /// </summary>
    [HttpGet("by-number/{number}")]
    public ActionResult<ApiResponse<CustomerOrderDto>> GetCustomerOrderByNumber(string number)
    {
        try
        {
            var zamowienia = _sferaService.GetManager("ZamowieniaOdKlientow");
            if (zamowienia == null)
            {
                return StatusCode(500, ApiResponse<CustomerOrderDto>.Error("Failed to get ZamowieniaOdKlientow manager"));
            }

            dynamic? zamowienie = null;
            foreach (var z in zamowienia.Dane.Wszystkie())
            {
                var numerWewn = DynamicPropertyHelper.GetProperty(z, "NumerWewnetrzny");
                var fullNumber = numerWewn != null ? DynamicPropertyHelper.GetString(numerWewn, "PelnaSygnatura") : null;
                if (fullNumber == number)
                {
                    zamowienie = z;
                    break;
                }
            }

            if (zamowienie == null)
            {
                return NotFound(ApiResponse<CustomerOrderDto>.Error($"Customer order with number {number} not found"));
            }

            return Ok(ApiResponse<CustomerOrderDto>.Ok(MapToDto(zamowienie)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting customer order by number {Number}", number);
            return StatusCode(500, ApiResponse<CustomerOrderDto>.Error("Error retrieving customer order", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create a new customer order (ZK)
    /// </summary>
    [HttpPost]
    public ActionResult<ApiResponse<CustomerOrderDto>> CreateCustomerOrder([FromBody] CreateCustomerOrderRequest request)
    {
        try
        {
            var zamowienia = _sferaService.GetManager("ZamowieniaOdKlientow");
            if (zamowienia == null)
            {
                return StatusCode(500, ApiResponse<CustomerOrderDto>.Error("Failed to get ZamowieniaOdKlientow manager"));
            }

            // Get default configuration
            var konfiguracje = _sferaService.GetManagerByType("InsERT.Moria.Sfera", "InsERT.Moria.Sfera.Konfiguracje");
            var konfiguracja = konfiguracje?.DaneDomyslne?.ZamowienieOdKlienta;

            using (var zamowienie = konfiguracja != null ? zamowienia.Utworz(konfiguracja) : zamowienia.Utworz())
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
                        zamowienie.Dane.Podmiot = podmiot;
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
                        zamowienie.Dane.Podmiot = podmiot;
                    }
                }

                // Set recipient if different from customer
                if (request.RecipientId.HasValue && podmioty != null)
                {
                    dynamic? odbiorca = null;
                    foreach (var p in podmioty.Dane.Wszystkie())
                    {
                        if (DynamicPropertyHelper.GetId(p) == request.RecipientId.Value)
                        {
                            odbiorca = p;
                            break;
                        }
                    }
                    if (odbiorca != null)
                    {
                        try { zamowienie.Dane.Odbiorca = odbiorca; } catch { }
                    }
                }

                // Set warehouse
                if (request.WarehouseId.HasValue || !string.IsNullOrEmpty(request.WarehouseSymbol))
                {
                    var magazyny = _sferaService.GetManager("Magazyny");
                    if (magazyny != null)
                    {
                        dynamic? magazyn = null;
                        foreach (var m in magazyny.Dane.Wszystkie())
                        {
                            if (request.WarehouseId.HasValue && DynamicPropertyHelper.GetId(m) == request.WarehouseId.Value)
                            {
                                magazyn = m;
                                break;
                            }
                            if (!string.IsNullOrEmpty(request.WarehouseSymbol) && DynamicPropertyHelper.GetString(m, "Symbol") == request.WarehouseSymbol)
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

                // Set dates
                if (request.IssueDate.HasValue)
                {
                    zamowienie.Dane.DataWystawienia = request.IssueDate.Value;
                }

                if (request.DeadlineDate.HasValue)
                {
                    zamowienie.Dane.TerminRealizacji = request.DeadlineDate.Value;
                }

                // Set external/reference numbers
                if (!string.IsNullOrEmpty(request.ExternalNumber))
                {
                    try { zamowienie.Dane.NumerZewnetrzny = request.ExternalNumber; } catch { }
                }

                if (!string.IsNullOrEmpty(request.ReferenceNumber))
                {
                    try { zamowienie.Dane.NumerReferencyjny = request.ReferenceNumber; } catch { }
                }

                if (!string.IsNullOrEmpty(request.Title))
                {
                    try { zamowienie.Dane.Tytul = request.Title; } catch { }
                }

                // Set notes
                if (!string.IsNullOrEmpty(request.Notes))
                {
                    zamowienie.Dane.Uwagi = request.Notes;
                }

                // Set reservation type
                if (request.ReservationType.HasValue)
                {
                    try { zamowienie.Dane.TypRezerwacji = request.ReservationType.Value; } catch { }
                }

                // Set split payment
                if (request.SplitPayment.HasValue)
                {
                    try { zamowienie.Dane.MechanizmPodzielonejPlatnosci = request.SplitPayment.Value; } catch { }
                }

                // Set price level
                if (request.PriceLevelId.HasValue)
                {
                    var cenniki = _sferaService.GetManager("Cenniki");
                    if (cenniki != null)
                    {
                        dynamic? poziom = null;
                        foreach (var c in cenniki.Dane.Wszystkie())
                        {
                            if (DynamicPropertyHelper.GetId(c) == request.PriceLevelId.Value)
                            {
                                poziom = c;
                                break;
                            }
                        }
                        if (poziom != null)
                        {
                            try { zamowienie.Dane.PoziomCen = poziom; } catch { }
                        }
                    }
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
                        var pozycja = zamowienie.Pozycje.Dodaj(asortyment, item.Quantity, jednostka);

                        if (pozycja != null)
                        {
                            if (item.PriceNet.HasValue)
                            {
                                try { pozycja.Dane.CenaNetto = item.PriceNet.Value; } catch { }
                            }
                            else if (item.PriceGross.HasValue)
                            {
                                try { pozycja.Dane.CenaBrutto = item.PriceGross.Value; } catch { }
                            }

                            if (item.DiscountPercent.HasValue)
                            {
                                try { pozycja.Dane.RabatProcent = item.DiscountPercent.Value; } catch { }
                            }

                            if (!string.IsNullOrEmpty(item.Description))
                            {
                                try { pozycja.Dane.Opis = item.Description; } catch { }
                            }
                        }
                    }
                    else if (!string.IsNullOrEmpty(item.Name))
                    {
                        // Add item without product reference (manual entry)
                        try
                        {
                            var pozycja = zamowienie.Pozycje.DodajUslugeJednorazowa(item.Name, item.PriceNet ?? 0, item.Quantity);
                            if (pozycja != null && item.DiscountPercent.HasValue)
                            {
                                pozycja.Dane.RabatProcent = item.DiscountPercent.Value;
                            }
                        }
                        catch { }
                    }
                }

                if ((bool)zamowienie.Zapisz())
                {
                    var numerWewnetrzny = DynamicPropertyHelper.GetProperty(zamowienie.Dane, "NumerWewnetrzny");
                    string orderNumber = numerWewnetrzny != null ? DynamicPropertyHelper.GetString(numerWewnetrzny, "PelnaSygnatura") ?? "" : "";
                    _logger.LogInformation("Created customer order {Number}", orderNumber);

                    return CreatedAtAction(
                        nameof(GetCustomerOrder),
                        new { id = DynamicPropertyHelper.GetId(zamowienie.Dane) },
                        ApiResponse<CustomerOrderDto>.Ok(MapToDto(zamowienie.Dane), "Customer order created successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(zamowienie);
                    return BadRequest(ApiResponse<CustomerOrderDto>.Error("Failed to create customer order", errors));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating customer order");
            return StatusCode(500, ApiResponse<CustomerOrderDto>.Error("Error creating customer order", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Update an existing customer order
    /// </summary>
    [HttpPut("{id}")]
    public ActionResult<ApiResponse<CustomerOrderDto>> UpdateCustomerOrder(int id, [FromBody] UpdateCustomerOrderRequest request)
    {
        try
        {
            var zamowienia = _sferaService.GetManager("ZamowieniaOdKlientow");
            if (zamowienia == null)
            {
                return StatusCode(500, ApiResponse<CustomerOrderDto>.Error("Failed to get ZamowieniaOdKlientow manager"));
            }

            dynamic? zamowienieDane = null;
            foreach (var z in zamowienia.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetId(z) == id)
                {
                    zamowienieDane = z;
                    break;
                }
            }

            if (zamowienieDane == null)
            {
                return NotFound(ApiResponse<CustomerOrderDto>.Error($"Customer order with ID {id} not found"));
            }

            using (var zamowienie = zamowienia.Znajdz(zamowienieDane))
            {
                if (zamowienie == null)
                {
                    return NotFound(ApiResponse<CustomerOrderDto>.Error($"Customer order with ID {id} not found"));
                }

                dynamic dane = zamowienie.Dane;

                if (request.DeadlineDate.HasValue)
                {
                    dane.TerminRealizacji = request.DeadlineDate.Value;
                }

                if (!string.IsNullOrEmpty(request.ExternalNumber))
                {
                    try { dane.NumerZewnetrzny = request.ExternalNumber; } catch { }
                }

                if (!string.IsNullOrEmpty(request.ReferenceNumber))
                {
                    try { dane.NumerReferencyjny = request.ReferenceNumber; } catch { }
                }

                if (!string.IsNullOrEmpty(request.Title))
                {
                    try { dane.Tytul = request.Title; } catch { }
                }

                if (!string.IsNullOrEmpty(request.Notes))
                {
                    dane.Uwagi = request.Notes;
                }

                if ((bool)zamowienie.Zapisz())
                {
                    _logger.LogInformation("Updated customer order {Id}", id);
                    return Ok(ApiResponse<CustomerOrderDto>.Ok(MapToDto(dane), "Customer order updated successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(zamowienie);
                    return BadRequest(ApiResponse<CustomerOrderDto>.Error("Failed to update customer order", errors));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating customer order {Id}", id);
            return StatusCode(500, ApiResponse<CustomerOrderDto>.Error("Error updating customer order", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Delete a customer order
    /// </summary>
    [HttpDelete("{id}")]
    public ActionResult<ApiResponse<bool>> DeleteCustomerOrder(int id)
    {
        try
        {
            var zamowienia = _sferaService.GetManager("ZamowieniaOdKlientow");
            if (zamowienia == null)
            {
                return StatusCode(500, ApiResponse<bool>.Error("Failed to get ZamowieniaOdKlientow manager"));
            }

            dynamic? zamowienieDane = null;
            foreach (var z in zamowienia.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetId(z) == id)
                {
                    zamowienieDane = z;
                    break;
                }
            }

            if (zamowienieDane == null)
            {
                return NotFound(ApiResponse<bool>.Error($"Customer order with ID {id} not found"));
            }

            using (var zamowienie = zamowienia.Znajdz(zamowienieDane))
            {
                if (zamowienie == null)
                {
                    return NotFound(ApiResponse<bool>.Error($"Customer order with ID {id} not found"));
                }

                if ((bool)zamowienie.Usun())
                {
                    _logger.LogInformation("Deleted customer order {Id}", id);
                    return Ok(ApiResponse<bool>.Ok(true, "Customer order deleted successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(zamowienie);
                    return BadRequest(ApiResponse<bool>.Error("Failed to delete customer order", errors));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting customer order {Id}", id);
            return StatusCode(500, ApiResponse<bool>.Error("Error deleting customer order", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Close a customer order (mark as completed/cancelled)
    /// </summary>
    [HttpPost("{id}/close")]
    public ActionResult<ApiResponse<CustomerOrderDto>> CloseCustomerOrder(int id)
    {
        try
        {
            var zamowienia = _sferaService.GetManager("ZamowieniaOdKlientow");
            if (zamowienia == null)
            {
                return StatusCode(500, ApiResponse<CustomerOrderDto>.Error("Failed to get ZamowieniaOdKlientow manager"));
            }

            dynamic? zamowienieDane = null;
            foreach (var z in zamowienia.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetId(z) == id)
                {
                    zamowienieDane = z;
                    break;
                }
            }

            if (zamowienieDane == null)
            {
                return NotFound(ApiResponse<CustomerOrderDto>.Error($"Customer order with ID {id} not found"));
            }

            using (var zamowienie = zamowienia.Znajdz(zamowienieDane))
            {
                if (zamowienie == null)
                {
                    return NotFound(ApiResponse<CustomerOrderDto>.Error($"Customer order with ID {id} not found"));
                }

                try
                {
                    zamowienie.Dane.Zamkniety = true;
                }
                catch
                {
                    try { zamowienie.Zamknij(); } catch { }
                }

                if ((bool)zamowienie.Zapisz())
                {
                    _logger.LogInformation("Closed customer order {Id}", id);
                    return Ok(ApiResponse<CustomerOrderDto>.Ok(MapToDto(zamowienie.Dane), "Customer order closed successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(zamowienie);
                    return BadRequest(ApiResponse<CustomerOrderDto>.Error("Failed to close customer order", errors));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing customer order {Id}", id);
            return StatusCode(500, ApiResponse<CustomerOrderDto>.Error("Error closing customer order", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get realization status of a customer order
    /// </summary>
    [HttpGet("{id}/realization")]
    public ActionResult<ApiResponse<CustomerOrderRealizationDto>> GetOrderRealization(int id)
    {
        try
        {
            var zamowienia = _sferaService.GetManager("ZamowieniaOdKlientow");
            if (zamowienia == null)
            {
                return StatusCode(500, ApiResponse<CustomerOrderRealizationDto>.Error("Failed to get ZamowieniaOdKlientow manager"));
            }

            dynamic? zamowienie = null;
            foreach (var z in zamowienia.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetId(z) == id)
                {
                    zamowienie = z;
                    break;
                }
            }

            if (zamowienie == null)
            {
                return NotFound(ApiResponse<CustomerOrderRealizationDto>.Error($"Customer order with ID {id} not found"));
            }

            var numerWewn = DynamicPropertyHelper.GetProperty(zamowienie, "NumerWewnetrzny");
            var orderNumber = numerWewn != null ? DynamicPropertyHelper.GetString(numerWewn, "PelnaSygnatura") : null;

            decimal totalQty = 0;
            decimal realizedQty = 0;

            var pozycje = DynamicPropertyHelper.GetCollection(zamowienie, "Pozycje");
            foreach (var poz in pozycje)
            {
                var qty = DynamicPropertyHelper.GetDecimal(poz, "Ilosc");
                var realQty = DynamicPropertyHelper.GetDecimal(poz, "IloscZrealizowana");
                totalQty += qty;
                realizedQty += realQty;
            }

            var realization = new CustomerOrderRealizationDto
            {
                OrderId = id,
                OrderNumber = orderNumber,
                TotalQuantity = totalQty,
                RealizedQuantity = realizedQty,
                RemainingQuantity = totalQty - realizedQty,
                RealizationPercent = totalQty > 0 ? Math.Round((realizedQty / totalQty) * 100, 2) : 0,
                IsFullyRealized = totalQty > 0 && realizedQty >= totalQty
            };

            return Ok(ApiResponse<CustomerOrderRealizationDto>.Ok(realization));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting realization for customer order {Id}", id);
            return StatusCode(500, ApiResponse<CustomerOrderRealizationDto>.Error("Error retrieving realization status", new List<string> { ex.Message }));
        }
    }

    private static CustomerOrderListItemDto MapToListItemDto(dynamic zamowienie)
    {
        var numerWewn = DynamicPropertyHelper.GetProperty(zamowienie, "NumerWewnetrzny");
        var podmiot = DynamicPropertyHelper.GetProperty(zamowienie, "Podmiot");
        var magazyn = DynamicPropertyHelper.GetProperty(zamowienie, "Magazyn");
        var status = DynamicPropertyHelper.GetProperty(zamowienie, "Status");
        var deadline = DynamicPropertyHelper.GetDateTime(zamowienie, "TerminRealizacji");
        var isClosed = DynamicPropertyHelper.GetNullableBool(zamowienie, "Zamkniety") ?? false;

        return new CustomerOrderListItemDto
        {
            Id = DynamicPropertyHelper.GetId(zamowienie),
            Symbol = DynamicPropertyHelper.GetString(zamowienie, "Symbol") ?? "",
            Number = numerWewn != null ? DynamicPropertyHelper.GetString(numerWewn, "PelnaSygnatura") : null,
            ExternalNumber = DynamicPropertyHelper.GetString(zamowienie, "NumerZewnetrzny"),
            ReferenceNumber = DynamicPropertyHelper.GetString(zamowienie, "NumerReferencyjny"),
            Title = DynamicPropertyHelper.GetString(zamowienie, "Tytul"),
            IssueDate = DynamicPropertyHelper.GetDateTime(zamowienie, "DataWystawienia"),
            DeadlineDate = deadline,
            CustomerId = podmiot != null ? DynamicPropertyHelper.GetId(podmiot) : null,
            CustomerName = podmiot != null ? DynamicPropertyHelper.GetString(podmiot, "NazwaSkrocona") : null,
            CustomerNIP = podmiot != null ? DynamicPropertyHelper.GetString(podmiot, "NIP") : null,
            WarehouseSymbol = magazyn != null ? DynamicPropertyHelper.GetString(magazyn, "Symbol") : null,
            TotalNet = DynamicPropertyHelper.GetDecimal(zamowienie, "WartoscNetto"),
            TotalGross = DynamicPropertyHelper.GetDecimal(zamowienie, "WartoscBrutto"),
            AmountToPay = DynamicPropertyHelper.GetDecimal(zamowienie, "DoZaplaty"),
            Currency = DynamicPropertyHelper.GetString(zamowienie, "Waluta") ?? "PLN",
            StatusId = status != null ? DynamicPropertyHelper.GetId(status) : null,
            StatusSymbol = status != null ? DynamicPropertyHelper.GetString(status, "Symbol") : null,
            IsClosed = isClosed,
            IsRealized = DynamicPropertyHelper.GetNullableBool(zamowienie, "Zrealizowane") ?? false,
            IsOverdue = !isClosed && deadline.HasValue && deadline.Value < DateTime.Today
        };
    }

    private static CustomerOrderDto MapToDto(dynamic zamowienie)
    {
        var numerWewn = DynamicPropertyHelper.GetProperty(zamowienie, "NumerWewnetrzny");
        var podmiot = DynamicPropertyHelper.GetProperty(zamowienie, "Podmiot");
        var odbiorca = DynamicPropertyHelper.GetProperty(zamowienie, "Odbiorca");
        var magazyn = DynamicPropertyHelper.GetProperty(zamowienie, "Magazyn");
        var status = DynamicPropertyHelper.GetProperty(zamowienie, "Status");
        var poziomCen = DynamicPropertyHelper.GetProperty(zamowienie, "PoziomCen");
        var konfiguracja = DynamicPropertyHelper.GetProperty(zamowienie, "Konfiguracja");

        var dto = new CustomerOrderDto
        {
            // Identity
            Id = DynamicPropertyHelper.GetId(zamowienie),
            Symbol = DynamicPropertyHelper.GetString(zamowienie, "Symbol") ?? "",
            Number = numerWewn != null ? DynamicPropertyHelper.GetString(numerWewn, "PelnaSygnatura") : null,
            ExternalNumber = DynamicPropertyHelper.GetString(zamowienie, "NumerZewnetrzny"),
            ReferenceNumber = DynamicPropertyHelper.GetString(zamowienie, "NumerReferencyjny"),
            Title = DynamicPropertyHelper.GetString(zamowienie, "Tytul"),
            Subtitle = DynamicPropertyHelper.GetString(zamowienie, "Podtytul"),

            // Status
            StatusId = status != null ? DynamicPropertyHelper.GetId(status) : 0,
            Status = status != null ? DynamicPropertyHelper.GetString(status, "Nazwa") : null,
            StatusSymbol = status != null ? DynamicPropertyHelper.GetString(status, "Symbol") : null,
            IsClosed = DynamicPropertyHelper.GetNullableBool(zamowienie, "Zamkniety") ?? false,
            IsRealizedWithoutDocument = DynamicPropertyHelper.GetNullableBool(zamowienie, "ZrealizowanyBezDokumentu") ?? false,
            ReceivedFromCustomer = DynamicPropertyHelper.GetNullableBool(zamowienie, "PrzyjetyOdKlienta") ?? false,

            // Reservation
            ReservationType = DynamicPropertyHelper.GetByte(zamowienie, "TypRezerwacji"),

            // Dates
            IssueDate = DynamicPropertyHelper.GetDateTime(zamowienie, "DataWystawienia"),
            EntryDate = DynamicPropertyHelper.GetDateTime(zamowienie, "DataWprowadzenia") ?? DateTime.MinValue,
            SaleDate = DynamicPropertyHelper.GetDateTime(zamowienie, "DataSprzedazy"),
            DeadlineDate = DynamicPropertyHelper.GetDateTime(zamowienie, "TerminRealizacji"),

            // Customer
            CustomerId = podmiot != null ? DynamicPropertyHelper.GetId(podmiot) : null,
            CustomerName = podmiot != null ? DynamicPropertyHelper.GetString(podmiot, "NazwaSkrocona") : null,
            CustomerNIP = podmiot != null ? DynamicPropertyHelper.GetString(podmiot, "NIP") : null,

            // Recipient
            RecipientId = odbiorca != null ? DynamicPropertyHelper.GetId(odbiorca) : null,
            RecipientName = odbiorca != null ? DynamicPropertyHelper.GetString(odbiorca, "NazwaSkrocona") : null,

            // Warehouse
            WarehouseId = magazyn != null ? DynamicPropertyHelper.GetId(magazyn) : null,
            WarehouseSymbol = magazyn != null ? DynamicPropertyHelper.GetString(magazyn, "Symbol") : null,
            WarehouseName = magazyn != null ? DynamicPropertyHelper.GetString(magazyn, "Nazwa") : null,

            // Price level
            PriceLevelId = poziomCen != null ? DynamicPropertyHelper.GetId(poziomCen) : null,
            PriceLevelName = poziomCen != null ? DynamicPropertyHelper.GetString(poziomCen, "Nazwa") : null,

            // Amounts
            TotalNet = DynamicPropertyHelper.GetDecimal(zamowienie, "WartoscNetto"),
            TotalGross = DynamicPropertyHelper.GetDecimal(zamowienie, "WartoscBrutto"),
            AmountToPay = DynamicPropertyHelper.GetDecimal(zamowienie, "DoZaplaty"),
            DiscountNet = DynamicPropertyHelper.GetDecimal(zamowienie, "RabatNetto"),
            DiscountGross = DynamicPropertyHelper.GetDecimal(zamowienie, "RabatBrutto"),
            DefaultDiscountPercent = DynamicPropertyHelper.GetDecimal(zamowienie, "DomyslnyRabatProcent"),

            // Currency
            Currency = DynamicPropertyHelper.GetString(zamowienie, "Waluta") ?? "PLN",

            // Payment
            SplitPayment = DynamicPropertyHelper.GetNullableBool(zamowienie, "MechanizmPodzielonejPlatnosci") ?? false,

            // Configuration
            ConfigurationId = DynamicPropertyHelper.GetGuid(konfiguracja, "Id") ?? Guid.Empty,
            ConfigurationSymbol = konfiguracja != null ? DynamicPropertyHelper.GetString(konfiguracja, "Symbol") : null,

            // Notes
            Notes = DynamicPropertyHelper.GetString(zamowienie, "Uwagi"),

            // Timestamps
            CreatedAt = DynamicPropertyHelper.GetDateTime(zamowienie, "DataUtworzenia"),
            ModifiedAt = DynamicPropertyHelper.GetDateTime(zamowienie, "DataModyfikacji"),

            // Items
            Items = new List<CustomerOrderItemDto>()
        };

        // Map items
        var pozycje = DynamicPropertyHelper.GetCollection(zamowienie, "Pozycje");
        int lineNum = 1;
        foreach (var poz in pozycje)
        {
            var asortyment = DynamicPropertyHelper.GetProperty(poz, "Asortyment");
            var jednostka = DynamicPropertyHelper.GetProperty(poz, "Jednostka");
            var stawkaVat = DynamicPropertyHelper.GetProperty(poz, "StawkaVat");

            dto.Items.Add(new CustomerOrderItemDto
            {
                Id = DynamicPropertyHelper.GetId(poz),
                LineNumber = lineNum++,
                ProductId = asortyment != null ? DynamicPropertyHelper.GetId(asortyment) : null,
                ProductSymbol = asortyment != null ? DynamicPropertyHelper.GetString(asortyment, "Symbol") : null,
                ProductName = asortyment != null ? DynamicPropertyHelper.GetString(asortyment, "Nazwa") : null,
                Name = DynamicPropertyHelper.GetString(poz, "Nazwa") ?? "",
                Description = DynamicPropertyHelper.GetString(poz, "Opis"),
                Quantity = DynamicPropertyHelper.GetDecimal(poz, "Ilosc"),
                QuantityRealized = DynamicPropertyHelper.GetDecimal(poz, "IloscZrealizowana"),
                QuantityRemaining = DynamicPropertyHelper.GetDecimal(poz, "Ilosc") - DynamicPropertyHelper.GetDecimal(poz, "IloscZrealizowana"),
                Unit = jednostka != null ? DynamicPropertyHelper.GetString(jednostka, "Symbol") ?? "szt." : "szt.",
                UnitId = jednostka != null ? DynamicPropertyHelper.GetId(jednostka) : null,
                PriceNet = DynamicPropertyHelper.GetDecimal(poz, "CenaNetto"),
                PriceGross = DynamicPropertyHelper.GetDecimal(poz, "CenaBrutto"),
                DiscountPercent = DynamicPropertyHelper.GetDecimal(poz, "RabatProcent"),
                DiscountValue = DynamicPropertyHelper.GetDecimal(poz, "RabatWartosc"),
                VatRate = stawkaVat != null ? DynamicPropertyHelper.GetString(stawkaVat, "Symbol") : null,
                VatRateId = stawkaVat != null ? DynamicPropertyHelper.GetId(stawkaVat) : null,
                VatPercent = DynamicPropertyHelper.GetDecimal(poz, "StawkaVatProcent"),
                ValueNet = DynamicPropertyHelper.GetDecimal(poz, "WartoscNetto"),
                ValueVat = DynamicPropertyHelper.GetDecimal(poz, "WartoscVat"),
                ValueGross = DynamicPropertyHelper.GetDecimal(poz, "WartoscBrutto"),
                IsReserved = DynamicPropertyHelper.GetNullableBool(poz, "Zarezerwowana") ?? false,
                ReservedQuantity = DynamicPropertyHelper.GetDecimal(poz, "IloscZarezerwowana")
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
