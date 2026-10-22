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
    public async Task<ActionResult<PagedResponse<CustomerOrderListItemDto>>> GetCustomerOrders([FromQuery] CustomerOrderQueryRequest query)
    {
        try
        {
            var (items, totalCount) = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var zamowienia = _sferaService.GetManager("ZamowieniaOdKlientow");
                if (zamowienia == null) return (null, -1);

                var allZamowienia = DynamicPropertyHelper.SafeGetAll((object)zamowienia);

                // Apply filters
                var filteredList = new List<object>();
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

                var count = filteredList.Count;

                // Sorting
                IEnumerable<object> sortedList = filteredList;
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

                var result = new List<CustomerOrderListItemDto>();
                foreach (var z in pagedItems)
                {
                    result.Add(MapToListItemDto(z));
                }

                return (result, count);
            });

            if (totalCount == -1)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get ZamowieniaOdKlientow manager"));
            }

            var response = new PagedResponse<CustomerOrderListItemDto>
            {
                Data = items!,
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
    public async Task<ActionResult<ApiResponse<CustomerOrderDto>>> GetCustomerOrder(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var zamowienia = _sferaService.GetManager("ZamowieniaOdKlientow");
                if (zamowienia == null) return (CustomerOrderDto?)null;

                dynamic? zamowienie = DynamicPropertyHelper.FindById(zamowienia, id);

                if (zamowienie == null) return (CustomerOrderDto?)null;
                return (CustomerOrderDto?)MapToDto(zamowienie);
            });

            if (result == null)
                return NotFound(ApiResponse<CustomerOrderDto>.Error($"Customer order with ID {id} not found"));

            return Ok(ApiResponse<CustomerOrderDto>.Ok(result));
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
    public async Task<ActionResult<ApiResponse<CustomerOrderDto>>> GetCustomerOrderByNumber(string number)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var zamowienia = _sferaService.GetManager("ZamowieniaOdKlientow");
                if (zamowienia == null) return (CustomerOrderDto?)null;

                dynamic? zamowienie = null;
                foreach (var z in DynamicPropertyHelper.SafeGetAll((object)zamowienia))
                {
                    var numerWewn = DynamicPropertyHelper.GetProperty(z, "NumerWewnetrzny");
                    var fullNumber = numerWewn != null ? DynamicPropertyHelper.GetString(numerWewn, "PelnaSygnatura") : null;
                    if (fullNumber == number)
                    {
                        zamowienie = z;
                        break;
                    }
                }

                if (zamowienie == null) return (CustomerOrderDto?)null;
                return (CustomerOrderDto?)MapToDto(zamowienie);
            });

            if (result == null)
            {
                return NotFound(ApiResponse<CustomerOrderDto>.Error($"Customer order with number {number} not found"));
            }

            return Ok(ApiResponse<CustomerOrderDto>.Ok(result));
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
    /// <remarks>
    /// All-or-nothing: an unknown customer, recipient, warehouse or product returns 400 and nothing is created. Line prices
    /// (<c>priceNet</c> or <c>priceGross</c>) are set in the calculation mode of the order (net or gross); a price of the other
    /// kind is converted with the VAT rate of the line. Lines without a price keep the Nexo price list price.
    /// A line with <c>name</c> and no product is a one-off service.
    /// </remarks>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<CustomerOrderDto>>> CreateCustomerOrder([FromBody] CreateCustomerOrderRequest request)
    {
        var validationErrors = CustomerOrderCreator.Validate(request);
        if (validationErrors.Count > 0)
        {
            return BadRequest(ApiResponse<CustomerOrderDto>.Error("Invalid customer order; nothing was created", validationErrors));
        }

        try
        {
            var outcome = await _sferaService.ExecuteWithLockAsync(() =>
                CustomerOrderCreator.Create(_sferaService.GetSfera(), request,
                    bo => GetBusinessObjectErrors(bo),
                    dane => (CustomerOrderDto)MapToDto(dane)));

            if (outcome.OrderId == null)
            {
                return BadRequest(ApiResponse<CustomerOrderDto>.Error("Failed to create customer order; nothing was created", outcome.Errors));
            }

            _logger.LogInformation("Created customer order {Id} ({Number})", outcome.OrderId, outcome.Order?.Number);
            return CreatedAtAction(
                nameof(GetCustomerOrder),
                new { id = outcome.OrderId },
                ApiResponse<CustomerOrderDto>.Ok(outcome.Order!, "Customer order created successfully"));
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
    public async Task<ActionResult<ApiResponse<CustomerOrderDto>>> UpdateCustomerOrder(int id, [FromBody] UpdateCustomerOrderRequest request)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var zamowienia = _sferaService.GetManager("ZamowieniaOdKlientow");
                if (zamowienia == null) return (false, "manager_null", (CustomerOrderDto?)null, new List<string>());

                dynamic? zamowienieDane = DynamicPropertyHelper.FindById(zamowienia, id);

                if (zamowienieDane == null) return (false, "not_found", (CustomerOrderDto?)null, new List<string>());

                using (var zamowienie = zamowienia.Znajdz(zamowienieDane))
                {
                    if (zamowienie == null) return (false, "not_found", (CustomerOrderDto?)null, new List<string>());

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
                        var dto = MapToDto(dane);
                        return (true, "ok", (CustomerOrderDto?)dto, new List<string>());
                    }
                    else
                    {
                        var errors = GetBusinessObjectErrors(zamowienie);
                        return (false, "save_failed", (CustomerOrderDto?)null, errors);
                    }
                }
            });

            var (success, code, dto, errors) = result;

            if (code == "manager_null")
            {
                return StatusCode(500, ApiResponse<CustomerOrderDto>.Error("Failed to get ZamowieniaOdKlientow manager"));
            }
            if (code == "not_found")
            {
                return NotFound(ApiResponse<CustomerOrderDto>.Error($"Customer order with ID {id} not found"));
            }
            if (code == "save_failed")
            {
                return BadRequest(ApiResponse<CustomerOrderDto>.Error("Failed to update customer order", errors));
            }

            _logger.LogInformation("Updated customer order {Id}", id);
            return Ok(ApiResponse<CustomerOrderDto>.Ok(dto!, "Customer order updated successfully"));
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
    public async Task<ActionResult<ApiResponse<bool>>> DeleteCustomerOrder(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var zamowienia = _sferaService.GetManager("ZamowieniaOdKlientow");
                if (zamowienia == null) return (false, "manager_null", new List<string>());

                dynamic? zamowienieDane = DynamicPropertyHelper.FindById(zamowienia, id);

                if (zamowienieDane == null) return (false, "not_found", new List<string>());

                using (var zamowienie = zamowienia.Znajdz(zamowienieDane))
                {
                    if (zamowienie == null) return (false, "not_found", new List<string>());

                    if ((bool)zamowienie.Usun())
                    {
                        return (true, "ok", new List<string>());
                    }
                    else
                    {
                        var errors = GetBusinessObjectErrors(zamowienie);
                        return (false, "delete_failed", errors);
                    }
                }
            });

            var (success, code, errors) = result;

            if (code == "manager_null")
            {
                return StatusCode(500, ApiResponse<bool>.Error("Failed to get ZamowieniaOdKlientow manager"));
            }
            if (code == "not_found")
            {
                return NotFound(ApiResponse<bool>.Error($"Customer order with ID {id} not found"));
            }
            if (code == "delete_failed")
            {
                return BadRequest(ApiResponse<bool>.Error("Failed to delete customer order", errors));
            }

            _logger.LogInformation("Deleted customer order {Id}", id);
            return Ok(ApiResponse<bool>.Ok(true, "Customer order deleted successfully"));
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
    public async Task<ActionResult<ApiResponse<CustomerOrderDto>>> CloseCustomerOrder(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var zamowienia = _sferaService.GetManager("ZamowieniaOdKlientow");
                if (zamowienia == null) return (false, "manager_null", (CustomerOrderDto?)null, new List<string>());

                dynamic? zamowienieDane = DynamicPropertyHelper.FindById(zamowienia, id);

                if (zamowienieDane == null) return (false, "not_found", (CustomerOrderDto?)null, new List<string>());

                using (var zamowienie = zamowienia.Znajdz(zamowienieDane))
                {
                    if (zamowienie == null) return (false, "not_found", (CustomerOrderDto?)null, new List<string>());

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
                        var dto = MapToDto(zamowienie.Dane);
                        return (true, "ok", (CustomerOrderDto?)dto, new List<string>());
                    }
                    else
                    {
                        var errors = GetBusinessObjectErrors(zamowienie);
                        return (false, "save_failed", (CustomerOrderDto?)null, errors);
                    }
                }
            });

            var (success, code, dto, errors) = result;

            if (code == "manager_null")
            {
                return StatusCode(500, ApiResponse<CustomerOrderDto>.Error("Failed to get ZamowieniaOdKlientow manager"));
            }
            if (code == "not_found")
            {
                return NotFound(ApiResponse<CustomerOrderDto>.Error($"Customer order with ID {id} not found"));
            }
            if (code == "save_failed")
            {
                return BadRequest(ApiResponse<CustomerOrderDto>.Error("Failed to close customer order", errors));
            }

            _logger.LogInformation("Closed customer order {Id}", id);
            return Ok(ApiResponse<CustomerOrderDto>.Ok(dto!, "Customer order closed successfully"));
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
    public async Task<ActionResult<ApiResponse<CustomerOrderRealizationDto>>> GetOrderRealization(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var zamowienia = _sferaService.GetManager("ZamowieniaOdKlientow");
                if (zamowienia == null) return (CustomerOrderRealizationDto?)null;

                dynamic? zamowienie = DynamicPropertyHelper.FindById(zamowienia, id);

                if (zamowienie == null) return (CustomerOrderRealizationDto?)null;

                var numerWewn = DynamicPropertyHelper.GetProperty(zamowienie, "NumerWewnetrzny");
                var orderNumber = numerWewn != null ? DynamicPropertyHelper.GetString(numerWewn, "PelnaSygnatura") : null;

                // Quantities are summed in the line units (as before); per-line values, also in the base unit, are in Lines.
                var lines = new List<CustomerOrderItemDto>();
                var lineNumber = 1;
                foreach (var poz in DynamicPropertyHelper.GetCollection((object)zamowienie, "Pozycje"))
                {
                    var line = new CustomerOrderItemDto { Id = DynamicPropertyHelper.GetId(poz), LineNumber = lineNumber++ };
                    OrderReader.EnrichLine(line, (object)poz);
                    lines.Add(line);
                }

                var totalQty = lines.Sum(l => l.Quantity);
                var realizedQty = lines.Sum(l => l.QuantityRealized ?? 0m);
                var remainingQty = lines.Sum(l => l.QuantityRemaining ?? 0m);
                var header = new CustomerOrderDto();
                OrderReader.EnrichHeader(header, (object)zamowienie);

                return (CustomerOrderRealizationDto?)new CustomerOrderRealizationDto
                {
                    OrderId = id,
                    OrderNumber = orderNumber,
                    TotalQuantity = totalQty,
                    RealizedQuantity = realizedQty,
                    RemainingQuantity = remainingQty,
                    RealizationPercent = header.RealizationPercent
                                         ?? (totalQty > 0 ? Math.Round((realizedQty / totalQty) * 100, 2) : 0),
                    IsFullyRealized = lines.Count > 0 && lines.All(l => (l.QuantityRemaining ?? 0m) <= 0m),
                    Lines = lines
                };
            });

            if (result == null)
            {
                return NotFound(ApiResponse<CustomerOrderRealizationDto>.Error($"Customer order with ID {id} not found"));
            }

            return Ok(ApiResponse<CustomerOrderRealizationDto>.Ok(result));
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
        DateTime? deadline = DynamicPropertyHelper.GetDateTime(zamowienie, "TerminRealizacji");
        bool isClosed = DynamicPropertyHelper.GetNullableBool(zamowienie, "Zamkniety") ?? false;

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
        var pozycje = DynamicPropertyHelper.GetCollection((object)zamowienie, "Pozycje");
        int lineNum = 1;
        foreach (var poz in pozycje)
        {
            var dane = DynamicPropertyHelper.GetDane(poz);
            var asortyment = DynamicPropertyHelper.GetProperty(dane, "Asortyment")
                          ?? DynamicPropertyHelper.GetProperty(poz, "Asortyment");
            var jednostka = DynamicPropertyHelper.GetProperty(dane, "Jednostka")
                         ?? DynamicPropertyHelper.GetProperty(poz, "Jednostka");
            var stawkaVat = DynamicPropertyHelper.GetProperty(dane, "StawkaVat")
                         ?? DynamicPropertyHelper.GetProperty(poz, "StawkaVat");

            var quantity = DynamicPropertyHelper.GetDecimalFirstOf(dane, "Ilosc", "IloscJednostek");
            var priceNet = DynamicPropertyHelper.GetDecimalFirstOf(dane, "CenaNetto", "CenaJednostkowaNetto", "CenaJednostkowa", "Cena");
            var priceGross = DynamicPropertyHelper.GetDecimalFirstOf(dane, "CenaBrutto", "CenaJednostkowaBrutto");
            var valueNet = DynamicPropertyHelper.GetDecimalFirstOf(dane, "WartoscNetto", "Wartosc");
            var valueGross = DynamicPropertyHelper.GetDecimalFirstOf(dane, "WartoscBrutto");

            dto.Items.Add(new CustomerOrderItemDto
            {
                Id = DynamicPropertyHelper.GetId(poz),
                LineNumber = lineNum++,
                ProductId = asortyment != null ? DynamicPropertyHelper.GetId(asortyment) : null,
                ProductSymbol = asortyment != null ? DynamicPropertyHelper.GetString(asortyment, "Symbol") : null,
                ProductName = asortyment != null ? DynamicPropertyHelper.GetString(asortyment, "Nazwa") : null,
                Name = DynamicPropertyHelper.GetString(dane, "Nazwa")
                    ?? DynamicPropertyHelper.GetString(poz, "Nazwa") ?? "",
                Description = DynamicPropertyHelper.GetString(dane, "Opis")
                           ?? DynamicPropertyHelper.GetString(poz, "Opis"),
                Quantity = quantity,
                QuantityRealized = DynamicPropertyHelper.GetDecimal(dane, "IloscZrealizowana"),
                QuantityRemaining = quantity - DynamicPropertyHelper.GetDecimal(dane, "IloscZrealizowana"),
                Unit = (jednostka != null ? DynamicPropertyHelper.GetString(jednostka, "Symbol") : null) ?? "szt.",
                UnitId = jednostka != null ? DynamicPropertyHelper.GetId(jednostka) : null,
                PriceNet = priceNet != 0 ? priceNet : (valueNet != 0 && quantity != 0 ? valueNet / quantity : 0),
                PriceGross = priceGross != 0 ? priceGross : (valueGross != 0 && quantity != 0 ? valueGross / quantity : 0),
                DiscountPercent = DynamicPropertyHelper.GetDecimal(dane, "RabatProcent"),
                DiscountValue = DynamicPropertyHelper.GetDecimal(dane, "RabatWartosc"),
                VatRate = stawkaVat != null ? DynamicPropertyHelper.GetString(stawkaVat, "Symbol") : null,
                VatRateId = stawkaVat != null ? DynamicPropertyHelper.GetId(stawkaVat) : null,
                VatPercent = DynamicPropertyHelper.GetDecimal(dane, "StawkaVatProcent"),
                ValueNet = valueNet,
                ValueVat = DynamicPropertyHelper.GetDecimalFirstOf(dane, "WartoscVat"),
                ValueGross = valueGross,
                IsReserved = DynamicPropertyHelper.GetNullableBool(dane, "Zarezerwowana") ?? false,
                ReservedQuantity = DynamicPropertyHelper.GetDecimal(dane, "IloscZarezerwowana"),
                // SDK 60.0.0: PozycjaZamowieniaWysylkowego.WartoscKaucji/WalutaKaucji
                DepositValue = DynamicPropertyHelper.GetNullableDecimal(dane, "WartoscKaucji"),
                DepositCurrency = DynamicPropertyHelper.GetString(dane, "WalutaKaucji", "Symbol")
            });

            // Product, unit, prices, VAT, realization (StanRealizacjiZamowienia / IloscDoRealizacji) and reservation
            // (Rezerwacja) from the real PozycjaDokumentu members.
            OrderReader.EnrichLine(dto.Items[^1], (object)poz);
        }

        // Totals, currency, issue date, status, timestamps and realization from the real Dokument members.
        OrderReader.EnrichHeader(dto, (object)zamowienie);

        return dto;
    }

    private static List<string> GetBusinessObjectErrors(dynamic obiekt)
    {
        // Typed first: the entity errors are explicit interface members (ITypedDataErrorInfo), which the late-bound read
        // below cannot see, so it returned no errors for a refused save.
        if ((object?)obiekt is InsERT.Mox.ObiektyBiznesowe.IObiektBiznesowy businessObject)
        {
            var typed = SdkErrors.Of(businessObject);
            if (typed.Count > 0) return typed;
        }

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
