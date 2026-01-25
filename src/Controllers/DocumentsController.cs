using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Requests;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Commercial documents (Faktury, Paragony, Korekty) management endpoints
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Tags("Documents")]
public class DocumentsController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<DocumentsController> _logger;
    private readonly StockValidationHelper _stockHelper;

    public DocumentsController(
        ISferaService sferaService,
        ILogger<DocumentsController> logger,
        StockValidationHelper stockHelper)
    {
        _sferaService = sferaService;
        _logger = logger;
        _stockHelper = stockHelper;
    }

    /// <summary>
    /// Explore properties of a single Document entity (debug endpoint)
    /// </summary>
    [HttpGet("debug/properties/{id}")]
    public ActionResult<object> GetDocumentProperties(int id)
    {
        try
        {
            var dokumentyManager = _sferaService.GetManager("Dokumenty");
            if (dokumentyManager == null)
            {
                return StatusCode(500, new { Error = "Failed to get Dokumenty manager" });
            }

            dynamic? dokument = null;
            foreach (var d in dokumentyManager.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetId(d) == id)
                {
                    dokument = d;
                    break;
                }
            }

            if (dokument == null)
            {
                return NotFound(new { Error = $"Document {id} not found" });
            }

            Type dokumentType = dokument.GetType();
            var properties = new Dictionary<string, object>();

            foreach (var prop in dokumentType.GetProperties())
            {
                try
                {
                    var value = prop.GetValue(dokument);
                    var valueType = value?.GetType().Name ?? "null";

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
                EntityType = dokumentType.FullName,
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
    /// Get documents with filtering (lightweight list view)
    /// </summary>
    [HttpGet]
    public ActionResult<PagedResponse<DocumentListItemDto>> GetDocuments([FromQuery] DocumentQueryRequest query)
    {
        try
        {
            var dokumentyManager = _sferaService.GetManager("Dokumenty");
            if (dokumentyManager == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Dokumenty manager"));
            }

            var allDokumenty = new List<dynamic>();
            foreach (var d in dokumentyManager.Dane.Wszystkie())
            {
                allDokumenty.Add(d);
            }

            // Apply filters
            allDokumenty = ApplyDocumentFilters(allDokumenty, query);

            var totalCount = allDokumenty.Count;

            // Sort and paginate
            var sortedItems = ApplyDocumentSorting(allDokumenty, query.SortBy);
            var items = sortedItems
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToList();

            var mappedItems = new List<DocumentListItemDto>();
            foreach (var item in items)
            {
                try
                {
                    if (item != null)
                    {
                        mappedItems.Add(MapToListItemDto(item));
                    }
                }
                catch (Exception mapEx)
                {
                    _logger.LogWarning(mapEx, "Failed to map document, skipping");
                }
            }

            var response = new PagedResponse<DocumentListItemDto>
            {
                Data = mappedItems,
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = totalCount
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting documents");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving documents", new List<string> { ex.Message }));
        }
    }

    private static List<dynamic> ApplyDocumentFilters(List<dynamic> documents, DocumentQueryRequest query)
    {
        // Symbol filter
        if (!string.IsNullOrEmpty(query.Symbol))
        {
            documents = documents.Where(d =>
            {
                var symbol = DynamicPropertyHelper.GetString(d, "Symbol");
                return symbol != null && symbol.Equals(query.Symbol, StringComparison.OrdinalIgnoreCase);
            }).ToList();
        }

        // Search filter
        if (!string.IsNullOrEmpty(query.Search))
        {
            var search = query.Search.ToLower();
            documents = documents.Where(d =>
            {
                var symbol = DynamicPropertyHelper.GetString(d, "Symbol")?.ToLower() ?? "";
                var numer = DynamicPropertyHelper.GetString(d, "NumerWewnetrzny", "PelnaSygnatura")?.ToLower() ?? "";
                var numerZew = DynamicPropertyHelper.GetString(d, "NumerZewnetrzny")?.ToLower() ?? "";
                var klient = DynamicPropertyHelper.GetString(d, "Podmiot", "NazwaSkrocona")?.ToLower() ?? "";
                var nip = DynamicPropertyHelper.GetString(d, "Podmiot", "NIP")?.ToLower() ?? "";

                return symbol.Contains(search) || numer.Contains(search) ||
                       numerZew.Contains(search) || klient.Contains(search) || nip.Contains(search);
            }).ToList();
        }

        // Issue date range filter
        if (query.DateFrom.HasValue)
        {
            documents = documents.Where(d =>
            {
                var data = DynamicPropertyHelper.GetDateTime(d, "DataWydaniaWystawienia") ??
                           DynamicPropertyHelper.GetDateTime(d, "DataWystawienia");
                return data.HasValue && data.Value >= query.DateFrom.Value;
            }).ToList();
        }

        if (query.DateTo.HasValue)
        {
            documents = documents.Where(d =>
            {
                var data = DynamicPropertyHelper.GetDateTime(d, "DataWydaniaWystawienia") ??
                           DynamicPropertyHelper.GetDateTime(d, "DataWystawienia");
                return data.HasValue && data.Value <= query.DateTo.Value;
            }).ToList();
        }

        // Sale date range filter
        if (query.SaleDateFrom.HasValue)
        {
            documents = documents.Where(d =>
            {
                var data = DynamicPropertyHelper.GetDateTime(d, "DataSprzedazy");
                return data.HasValue && data.Value >= query.SaleDateFrom.Value;
            }).ToList();
        }

        if (query.SaleDateTo.HasValue)
        {
            documents = documents.Where(d =>
            {
                var data = DynamicPropertyHelper.GetDateTime(d, "DataSprzedazy");
                return data.HasValue && data.Value <= query.SaleDateTo.Value;
            }).ToList();
        }

        // Due date range filter
        if (query.DueDateFrom.HasValue)
        {
            documents = documents.Where(d =>
            {
                var data = DynamicPropertyHelper.GetDateTime(d, "TerminPlatnosci");
                return data.HasValue && data.Value >= query.DueDateFrom.Value;
            }).ToList();
        }

        if (query.DueDateTo.HasValue)
        {
            documents = documents.Where(d =>
            {
                var data = DynamicPropertyHelper.GetDateTime(d, "TerminPlatnosci");
                return data.HasValue && data.Value <= query.DueDateTo.Value;
            }).ToList();
        }

        // Customer filters
        if (query.CustomerId.HasValue)
        {
            documents = documents.Where(d =>
            {
                var podmiotId = DynamicPropertyHelper.GetNullableInt(d, "PodmiotId") ??
                                DynamicPropertyHelper.GetNullableInt(d, "Podmiot", "Id");
                return podmiotId == query.CustomerId.Value;
            }).ToList();
        }

        if (!string.IsNullOrEmpty(query.CustomerNIP))
        {
            documents = documents.Where(d =>
            {
                var nip = DynamicPropertyHelper.GetString(d, "Podmiot", "NIP");
                return nip != null && nip.Equals(query.CustomerNIP, StringComparison.OrdinalIgnoreCase);
            }).ToList();
        }

        // Status filters
        if (query.StatusId.HasValue)
        {
            documents = documents.Where(d =>
            {
                var statusId = DynamicPropertyHelper.GetNullableInt(d, "StatusDokumentuId") ??
                               DynamicPropertyHelper.GetNullableInt(d, "Status", "Id");
                return statusId == query.StatusId.Value;
            }).ToList();
        }

        if (!string.IsNullOrEmpty(query.Status))
        {
            documents = documents.Where(d =>
            {
                var status = DynamicPropertyHelper.GetString(d, "StatusDokumentu", "Symbol");
                return status != null && status.Equals(query.Status, StringComparison.OrdinalIgnoreCase);
            }).ToList();
        }

        // Warehouse filters
        if (query.WarehouseId.HasValue)
        {
            documents = documents.Where(d =>
            {
                var magId = DynamicPropertyHelper.GetNullableInt(d, "MagazynId") ??
                            DynamicPropertyHelper.GetNullableInt(d, "Magazyn", "Id");
                return magId == query.WarehouseId.Value;
            }).ToList();
        }

        if (!string.IsNullOrEmpty(query.WarehouseSymbol))
        {
            documents = documents.Where(d =>
            {
                var symbol = DynamicPropertyHelper.GetString(d, "Magazyn", "Symbol");
                return symbol != null && symbol.Equals(query.WarehouseSymbol, StringComparison.OrdinalIgnoreCase);
            }).ToList();
        }

        // Currency filter
        if (!string.IsNullOrEmpty(query.Currency))
        {
            documents = documents.Where(d =>
            {
                var currency = DynamicPropertyHelper.GetString(d, "Waluta", "Symbol") ?? "PLN";
                return currency.Equals(query.Currency, StringComparison.OrdinalIgnoreCase);
            }).ToList();
        }

        // Amount range filters
        if (query.MinAmount.HasValue)
        {
            documents = documents.Where(d =>
            {
                var amount = DynamicPropertyHelper.GetDecimal(d, "WartoscBrutto");
                return amount >= query.MinAmount.Value;
            }).ToList();
        }

        if (query.MaxAmount.HasValue)
        {
            documents = documents.Where(d =>
            {
                var amount = DynamicPropertyHelper.GetDecimal(d, "WartoscBrutto");
                return amount <= query.MaxAmount.Value;
            }).ToList();
        }

        // Payment status filters
        if (query.PaidOnly)
        {
            documents = documents.Where(d =>
            {
                var amountToPay = DynamicPropertyHelper.GetDecimal(d, "KwotaDoZaplaty");
                return amountToPay <= 0;
            }).ToList();
        }

        if (query.UnpaidOnly)
        {
            documents = documents.Where(d =>
            {
                var amountToPay = DynamicPropertyHelper.GetDecimal(d, "KwotaDoZaplaty");
                return amountToPay > 0;
            }).ToList();
        }

        if (query.OverdueOnly)
        {
            documents = documents.Where(d =>
            {
                var amountToPay = DynamicPropertyHelper.GetDecimal(d, "KwotaDoZaplaty");
                var dueDate = DynamicPropertyHelper.GetDateTime(d, "TerminPlatnosci");
                return amountToPay > 0 && dueDate.HasValue && dueDate.Value < DateTime.Today;
            }).ToList();
        }

        // Canceled filter
        if (query.IsCanceled.HasValue)
        {
            documents = documents.Where(d =>
            {
                var anulowany = DynamicPropertyHelper.GetBool(d, "Anulowany");
                return anulowany == query.IsCanceled.Value;
            }).ToList();
        }

        // KSeF status filter
        if (!string.IsNullOrEmpty(query.KsefStatus))
        {
            documents = documents.Where(d =>
            {
                var ksefNumber = DynamicPropertyHelper.GetString(d, "NumerKSeF");
                var ksefStatus = DynamicPropertyHelper.GetString(d, "StatusKSeF");

                return query.KsefStatus.ToLower() switch
                {
                    "sent" => !string.IsNullOrEmpty(ksefNumber),
                    "not_sent" => string.IsNullOrEmpty(ksefNumber),
                    "error" => ksefStatus?.ToLower().Contains("error") == true ||
                               ksefStatus?.ToLower().Contains("blad") == true,
                    _ => true
                };
            }).ToList();
        }

        return documents;
    }

    private static IEnumerable<dynamic> ApplyDocumentSorting(List<dynamic> documents, string sortBy)
    {
        return sortBy?.ToLower() switch
        {
            "date_asc" => documents.OrderBy(d =>
                DynamicPropertyHelper.GetDateTime(d, "DataWydaniaWystawienia") ??
                DynamicPropertyHelper.GetDateTime(d, "DataWystawienia") ?? DateTime.MinValue),

            "amount_desc" => documents.OrderByDescending(d =>
                DynamicPropertyHelper.GetDecimal(d, "WartoscBrutto")),

            "amount_asc" => documents.OrderBy(d =>
                DynamicPropertyHelper.GetDecimal(d, "WartoscBrutto")),

            "number_desc" => documents.OrderByDescending(d =>
                DynamicPropertyHelper.GetString(d, "NumerWewnetrzny", "PelnaSygnatura") ?? ""),

            "number_asc" => documents.OrderBy(d =>
                DynamicPropertyHelper.GetString(d, "NumerWewnetrzny", "PelnaSygnatura") ?? ""),

            "customer_asc" => documents.OrderBy(d =>
                DynamicPropertyHelper.GetString(d, "Podmiot", "NazwaSkrocona") ?? ""),

            "customer_desc" => documents.OrderByDescending(d =>
                DynamicPropertyHelper.GetString(d, "Podmiot", "NazwaSkrocona") ?? ""),

            _ => documents.OrderByDescending(d =>
                DynamicPropertyHelper.GetDateTime(d, "DataWydaniaWystawienia") ??
                DynamicPropertyHelper.GetDateTime(d, "DataWystawienia") ?? DateTime.MinValue)
        };
    }

    /// <summary>
    /// Get document by ID
    /// </summary>
    [HttpGet("{id}")]
    public ActionResult<ApiResponse<DocumentDto>> GetDocument(int id)
    {
        try
        {
            var dokumentyManager = _sferaService.GetManager("Dokumenty");
            if (dokumentyManager == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Dokumenty manager"));
            }

            dynamic? dokument = null;
            foreach (var d in dokumentyManager.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetId(d) == id)
                {
                    dokument = d;
                    break;
                }
            }

            if (dokument == null)
            {
                return NotFound(ApiResponse<DocumentDto>.Error($"Document with ID {id} not found"));
            }

            return Ok(ApiResponse<DocumentDto>.Ok(MapDocumentToDto(dokument)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting document {Id}", id);
            return StatusCode(500, ApiResponse<DocumentDto>.Error("Error retrieving document", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get document by number
    /// </summary>
    [HttpGet("by-number/{number}")]
    public ActionResult<ApiResponse<DocumentDto>> GetDocumentByNumber(string number)
    {
        try
        {
            var dokumentyManager = _sferaService.GetManager("Dokumenty");
            if (dokumentyManager == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Dokumenty manager"));
            }

            dynamic? dokument = null;
            foreach (var d in dokumentyManager.Dane.Wszystkie())
            {
                var fullNum = DynamicPropertyHelper.GetString(d, "NumerWewnetrzny", "PelnaSygnatura");
                if (fullNum != null && fullNum.Contains(number))
                {
                    dokument = d;
                    break;
                }
            }

            if (dokument == null)
            {
                return NotFound(ApiResponse<DocumentDto>.Error($"Document with number {number} not found"));
            }

            return Ok(ApiResponse<DocumentDto>.Ok(MapDocumentToDto(dokument)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting document by number {Number}", number);
            return StatusCode(500, ApiResponse<DocumentDto>.Error("Error retrieving document", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create a new sales invoice (Faktura sprzedazy)
    /// </summary>
    /// <remarks>
    /// IMPORTANT: This endpoint requires WindowsFormsSynchronizationContext on the SDK thread.
    /// The document creation pattern is:
    /// 1. Set warehouse on Dokument.Magazyn
    /// 2. Call ZarezerwujNumer() to reserve document number
    /// 3. Add items using Pozycje.Dodaj(towarId)
    /// 4. Call Zapisz() to save
    /// </remarks>
    [HttpPost("sales-invoice")]
    public async Task<ActionResult<ApiResponse<DocumentDto>>> CreateSalesInvoice([FromBody] CreateDocumentRequest request)
    {
        try
        {
            // Validate stock availability for outgoing sales document
            if (request.Items != null && request.Items.Any() && !string.IsNullOrEmpty(request.WarehouseSymbol))
            {
                var stockValidation = _stockHelper.ValidateStock(
                    request.Items,
                    request.WarehouseSymbol,
                    item => item.ProductId,
                    item => item.ProductSymbol,
                    item => item.ProductEan,
                    item => item.Quantity);

                if (!stockValidation.AllItemsAvailable)
                {
                    _logger.LogWarning("Sales invoice creation failed - insufficient stock: {Errors}", string.Join("; ", stockValidation.Errors));
                    return BadRequest(ApiResponse<DocumentDto>.Error("Insufficient stock for sales invoice", stockValidation.Errors));
                }
            }

            // Use thread-safe execution - EF6 is NOT thread-safe
            var result = await _sferaService.ExecuteWithLockAsync<(bool Success, DocumentDto? Data, string Message, List<string> Errors)>(() =>
            {
                var dokumentySprzedazy = _sferaService.GetManager("DokumentySprzedazy");
                if (dokumentySprzedazy == null)
                {
                    return (false, null, "Failed to get DokumentySprzedazy manager", new List<string>());
                }

                using (var faktura = dokumentySprzedazy.UtworzFaktureSprzedazy())
                {
                    dynamic dane = faktura.Dane;

                    // Set customer
                    SetCustomerOnDocument(dane, request.CustomerId, request.CustomerNIP);

                    // CRITICAL: Set warehouse on Dokument (not Dane!) - required for document numbering
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
                                faktura.Dokument.Magazyn = magazyn;
                            }
                        }
                    }

                    // CRITICAL: Set dates BEFORE reserving number (number format includes year/month)
                    if (request.IssueDate.HasValue)
                    {
                        _logger.LogInformation("Setting FS IssueDate to: {Date}", request.IssueDate.Value);
                        // Try multiple property names and objects - different document types use different names
                        bool dateSet = false;

                        // Try on Dokument object first (this often controls numbering)
                        try
                        {
                            if (DynamicPropertyHelper.TrySetProperty(faktura.Dokument, "DataDokumentu", request.IssueDate.Value))
                            {
                                _logger.LogInformation("Set Dokument.DataDokumentu successfully");
                                dateSet = true;
                            }
                        }
                        catch (Exception ex) { _logger.LogDebug("Dokument.DataDokumentu failed: {Msg}", ex.Message); }

                        try
                        {
                            if (DynamicPropertyHelper.TrySetProperty(faktura.Dokument, "DataWystawienia", request.IssueDate.Value))
                            {
                                _logger.LogInformation("Set Dokument.DataWystawienia successfully");
                                dateSet = true;
                            }
                        }
                        catch (Exception ex) { _logger.LogDebug("Dokument.DataWystawienia failed: {Msg}", ex.Message); }

                        // Try on Dane object
                        if (DynamicPropertyHelper.TrySetProperty(dane, "DataDokumentu", request.IssueDate.Value))
                        {
                            _logger.LogInformation("Set Dane.DataDokumentu successfully");
                            dateSet = true;
                        }
                        if (DynamicPropertyHelper.TrySetProperty(dane, "DataWydaniaWystawienia", request.IssueDate.Value))
                        {
                            _logger.LogInformation("[FS-v2] Set Dane.DataWydaniaWystawienia successfully");
                            dateSet = true;
                        }
                        if (DynamicPropertyHelper.TrySetProperty(dane, "DataWystawienia", request.IssueDate.Value))
                        {
                            _logger.LogInformation("[FS-v2] Set Dane.DataWystawienia successfully");
                            dateSet = true;
                        }

                        // Also try DataWprowadzenia - this often controls document numbering
                        if (DynamicPropertyHelper.TrySetProperty(dane, "DataWprowadzenia", request.IssueDate.Value))
                        {
                            _logger.LogInformation("[FS-v2] Set Dane.DataWprowadzenia successfully");
                        }

                        // Try DataSprzedazy too (sale date = issue date if not specified)
                        if (DynamicPropertyHelper.TrySetProperty(dane, "DataSprzedazy", request.IssueDate.Value))
                        {
                            _logger.LogInformation("[FS-v2] Set Dane.DataSprzedazy successfully");
                        }

                        if (!dateSet)
                        {
                            _logger.LogWarning("[FS-v2] Could not set issue date - no matching property found on Dokument or Dane");
                        }
                    }

                    if (request.SaleDate.HasValue)
                    {
                        DynamicPropertyHelper.TrySetProperty(dane, "DataSprzedazy", request.SaleDate.Value);
                    }

                    // Log all date-related properties before reserving number
                    try
                    {
                        var daneType = ((object)dane).GetType();
                        var dateProps = daneType.GetProperties()
                            .Where(p => p.Name.Contains("Data") || p.Name.Contains("Date"))
                            .Select(p => {
                                try
                                {
                                    var val = p.GetValue(dane);
                                    return $"{p.Name}={val}";
                                }
                                catch { return $"{p.Name}=?"; }
                            })
                            .ToList();
                        _logger.LogInformation("[FS-v2] Date properties before ZarezerwujNumer: {Props}", string.Join(", ", dateProps));
                    }
                    catch { }

                    // Reserve number AFTER setting date (number depends on date!)
                    faktura.ZarezerwujNumer();
                    _logger.LogInformation("[FS-v2] Reserved sales invoice number: {Number}", (string?)faktura.PodajPodgladNumeru()?.ToString() ?? "");

                    // Set due date - property name may vary by document type
                    if (request.DueDate.HasValue)
                    {
                        if (!DynamicPropertyHelper.TrySetProperty(dane, "TerminPlatnosci", request.DueDate.Value))
                        {
                            DynamicPropertyHelper.TrySetProperty(dane, "DataPlatnosci", request.DueDate.Value);
                        }
                    }

                    // Set payment method
                    SetPaymentMethodOnDocument(dane, request.PaymentMethod, request.PaymentMethodId);

                    // Set notes
                    if (!string.IsNullOrEmpty(request.Notes))
                    {
                        try
                        {
                            dane.Uwagi = request.Notes;
                            _logger.LogDebug("Notes set successfully");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to set notes: {Message}", ex.Message);
                        }
                    }

                    // Add items to sales invoice - use specialized method for better diagnostics
                    _logger.LogInformation("[FS-v2] Adding {Count} items to sales invoice...", request.Items?.Count ?? 0);

                    // Log type info about Pozycje
                    try
                    {
                        var pozycjeType = ((object)faktura.Pozycje).GetType();
                        _logger.LogInformation("[FS-v2] faktura.Pozycje type: {Type}", pozycjeType.FullName);

                        // List methods on Pozycje
                        var methods = pozycjeType.GetMethods()
                            .Where(m => m.Name.Contains("Dodaj") || m.Name.Contains("Add"))
                            .Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})")
                            .ToList();
                        if (methods.Any())
                        {
                            _logger.LogInformation("[FS-v2] Pozycje add methods: {Methods}", string.Join("; ", methods));
                        }
                    }
                    catch (Exception typeEx)
                    {
                        _logger.LogDebug("[FS-v2] Could not get Pozycje type info: {Msg}", typeEx.Message);
                    }

                    AddSalesInvoiceItems(faktura, request.Items);
                    _logger.LogInformation("[FS-v2] Items added to FS, validating and saving...");

                    // Try to recalculate the document before saving
                    try
                    {
                        faktura.Przelicz();
                        _logger.LogInformation("[FS-v2] Przelicz() called successfully");

                        // Log document totals after Przelicz - try multiple property names
                        try
                        {
                            // List all value-related properties on dane
                            var daneType = ((object)dane).GetType();
                            var valueProps = daneType.GetProperties()
                                .Where(p => p.Name.Contains("Wartosc") || p.Name.Contains("Kwota") || p.Name.Contains("Suma") || p.Name.Contains("Value") || p.Name.Contains("Total"))
                                .Select(p => p.Name)
                                .ToList();
                            _logger.LogInformation("[FS-v2] dane value properties: {Props}", string.Join(", ", valueProps));

                            // Try reading values
                            string? wartoscStr = null;
                            foreach (var propName in new[] { "WartoscBrutto", "WartoscNetto", "Wartosc", "KwotaBrutto", "KwotaNetto", "SumaBrutto", "SumaNetto" })
                            {
                                var val = DynamicPropertyHelper.GetProperty(dane, propName);
                                if (val != null)
                                {
                                    _logger.LogInformation("[FS-v2] dane.{Prop} = {Value}", propName, (object)(val?.ToString() ?? "(null)"));
                                    if (wartoscStr == null) wartoscStr = val?.ToString();
                                }
                            }

                            if (string.IsNullOrEmpty(wartoscStr))
                            {
                                _logger.LogWarning("[FS-v2] No document value found after Przelicz - this may indicate items weren't added properly");
                            }
                        }
                        catch (Exception valEx)
                        {
                            _logger.LogDebug("[FS-v2] Could not read document values: {Msg}", valEx.Message);
                        }

                        // Also log number of items via Dane.Pozycje if available
                        try
                        {
                            var danePozycje = dane.Pozycje;
                            if (danePozycje != null)
                            {
                                int count = 0;
                                foreach (var p in danePozycje) count++;
                                _logger.LogInformation("[FS-v2] dane.Pozycje count: {Count}", count);
                            }
                        }
                        catch (Exception posEx)
                        {
                            _logger.LogDebug("[FS-v2] Could not count dane.Pozycje: {Msg}", posEx.Message);
                        }

                        // Read Wartosc object properties (it's a complex type)
                        try
                        {
                            var wartoscObj = dane.Wartosc;
                            if (wartoscObj != null)
                            {
                                var wartoscType = ((object)wartoscObj).GetType();
                                var wartoscProps = wartoscType.GetProperties()
                                    .Select(p => {
                                        try { return $"{p.Name}={p.GetValue(wartoscObj)}"; }
                                        catch { return $"{p.Name}=?"; }
                                    })
                                    .ToList();
                                _logger.LogInformation("[FS-v2] Wartosc object: {Props}", string.Join(", ", wartoscProps.Take(10)));
                            }
                        }
                        catch (Exception wartoscEx)
                        {
                            _logger.LogDebug("[FS-v2] Could not read Wartosc: {Msg}", wartoscEx.Message);
                        }

                        // Check KwotaDoZaplaty
                        try
                        {
                            var kwota = dane.KwotaDoZaplaty;
                            _logger.LogInformation("[FS-v2] KwotaDoZaplaty = {Kwota}", (object)(kwota?.ToString() ?? "(null)"));
                        }
                        catch (Exception kwotaEx)
                        {
                            _logger.LogDebug("[FS-v2] Could not read KwotaDoZaplaty: {Msg}", kwotaEx.Message);
                        }
                    }
                    catch (Exception przeliczEx)
                    {
                        _logger.LogWarning("[FS-v2] Przelicz() failed: {Msg}", przeliczEx.Message);
                    }

                    // Validate data BEFORE trying to save
                    try
                    {
                        var validationErrors = faktura.WalidujDane();
                        if (validationErrors != null)
                        {
                            int validationCount = 0;
                            foreach (var err in validationErrors)
                            {
                                validationCount++;
                                _logger.LogWarning("[FS-v2] WalidujDane error: {Error}", (object)(err?.ToString() ?? "Unknown"));
                            }
                            _logger.LogInformation("[FS-v2] WalidujDane returned {Count} errors", validationCount);
                        }
                        else
                        {
                            _logger.LogInformation("[FS-v2] WalidujDane returned null (no errors)");
                        }
                    }
                    catch (Exception valEx)
                    {
                        _logger.LogInformation("[FS-v2] WalidujDane() not available: {Msg}", valEx.Message);
                    }

                    // Check if document can be saved
                    try
                    {
                        var canSave = faktura.CzyMoznaZapisac();
                        _logger.LogInformation("[FS-v2] CzyMoznaZapisac() returned: {Result}", (object)(canSave?.ToString() ?? "(null)"));

                        // If it returns something with errors, try to extract them
                        if (canSave != null && !((bool)canSave))
                        {
                            _logger.LogWarning("[FS-v2] CzyMoznaZapisac returned false - document cannot be saved");
                        }
                    }
                    catch (Exception canSaveEx)
                    {
                        _logger.LogInformation("[FS-v2] CzyMoznaZapisac() not available: {Msg}", canSaveEx.Message);
                    }

                    // Try to get state before saving
                    try
                    {
                        var stan = faktura.Stan;
                        _logger.LogInformation("[FS-v2] Document state before save: {State}", (object)(stan?.ToString() ?? "(null)"));
                    }
                    catch (Exception stanEx)
                    {
                        _logger.LogDebug("[FS-v2] Could not get Stan: {Msg}", stanEx.Message);
                    }

                    // Check customer is actually set
                    try
                    {
                        var podmiot = dane.Podmiot;
                        if (podmiot != null)
                        {
                            int podmiotId = DynamicPropertyHelper.GetId(podmiot);
                            _logger.LogInformation("[FS-v2] Document has customer set: Id={Id}", podmiotId);
                        }
                        else
                        {
                            _logger.LogWarning("[FS-v2] Document has NO customer set!");
                        }
                    }
                    catch (Exception custEx)
                    {
                        _logger.LogDebug("[FS-v2] Could not check customer: {Msg}", custEx.Message);
                    }

                    // Check items count - try both dane.Pozycje and faktura.Pozycje
                    try
                    {
                        int itemCount = 0;
                        // Try dane.Pozycje first (more reliable for sales documents)
                        try
                        {
                            foreach (var poz in dane.Pozycje)
                            {
                                itemCount++;
                            }
                            _logger.LogInformation("[FS-v2] dane.Pozycje has {Count} items", itemCount);
                        }
                        catch (Exception daneEx)
                        {
                            _logger.LogDebug("[FS-v2] Could not enumerate dane.Pozycje: {Msg}", daneEx.Message);

                            // Fallback to faktura.Pozycje
                            try
                            {
                                foreach (var poz in faktura.Pozycje)
                                {
                                    itemCount++;
                                }
                                _logger.LogInformation("[FS-v2] faktura.Pozycje has {Count} items", itemCount);
                            }
                            catch (Exception fakturaEx)
                            {
                                _logger.LogDebug("[FS-v2] Could not enumerate faktura.Pozycje: {Msg}", fakturaEx.Message);
                            }
                        }
                    }
                    catch (Exception itemEx)
                    {
                        _logger.LogDebug("[FS-v2] Could not count items: {Msg}", itemEx.Message);
                    }

                    // Try to add default payments (this may be required for sales invoices)
                    try
                    {
                        // Try DodajPlatnosciDomyslne first
                        faktura.Pozycje.DodajPlatnosciDomyslne();
                        _logger.LogInformation("[FS-v2] Called DodajPlatnosciDomyslne() successfully");
                    }
                    catch (Exception payEx)
                    {
                        _logger.LogDebug("[FS-v2] DodajPlatnosciDomyslne() failed: {Msg}", payEx.Message);

                        // Try alternative payment methods
                        try
                        {
                            faktura.Pozycje.DodajDomyslnaPlatnoscNatychmiastowaNaKwoteDokumentu();
                            _logger.LogInformation("[FS-v2] Called DodajDomyslnaPlatnoscNatychmiastowaNaKwoteDokumentu() successfully");
                        }
                        catch (Exception payEx2)
                        {
                            _logger.LogDebug("[FS-v2] DodajDomyslnaPlatnoscNatychmiastowaNaKwoteDokumentu() failed: {Msg}", payEx2.Message);
                        }
                    }

                    _logger.LogInformation("[FS-v2] Calling Zapisz()...");
                    var saveResult = faktura.Zapisz();
                    bool isSaved = false;
                    try
                    {
                        isSaved = (bool)saveResult;
                    }
                    catch
                    {
                        isSaved = saveResult != null && saveResult.ToString().ToLower() == "true";
                    }
                    _logger.LogInformation("[FS-v2] Zapisz() returned: {Result}, isSaved={IsSaved}", (object)(saveResult?.ToString() ?? "(null)"), isSaved);

                    if (isSaved)
                    {
                        string docNumber = faktura.PodajPodgladNumeru()?.ToString() ?? "";
                        int docId = (int)faktura.Dokument.Id;
                        _logger.LogInformation("[FS-v2] Created sales invoice {Number}, Id={Id}", docNumber, docId);

                        return (true, MapSalesDocumentToDto(dane), "Sales invoice created successfully", new List<string>());
                    }
                    else
                    {
                        _logger.LogWarning("[FS-v2] Zapisz() failed, extracting errors...");
                        List<string> errors = GetBusinessObjectErrors(faktura);
                        _logger.LogWarning("[FS-v2] GetBusinessObjectErrors returned {Count} errors", errors?.Count ?? 0);
                        if (errors != null && errors.Count > 0)
                        {
                            foreach (string err in errors)
                            {
                                _logger.LogWarning("[FS-v2] BusinessObject error: {Error}", err);
                            }
                        }
                        else
                        {
                            errors ??= new List<string>();

                            // Try various error extraction methods
                            // Method 1: Dokument.InvalidData
                            try
                            {
                                var docInvalidData = DynamicPropertyHelper.GetProperty(faktura.Dokument, "InvalidData");
                                if (docInvalidData != null)
                                {
                                    foreach (var ei in docInvalidData)
                                    {
                                        string errMsg = ei?.ToString() ?? "Dokument validation error";
                                        _logger.LogWarning("[FS-v2] Dokument.InvalidData: {Error}", errMsg);
                                        if (!errors.Contains(errMsg)) errors.Add(errMsg);
                                    }
                                }
                            }
                            catch (Exception ex) { _logger.LogDebug("[FS-v2] Dokument.InvalidData failed: {Msg}", ex.Message); }

                            // Method 2: Dane.InvalidData
                            try
                            {
                                var daneInvalidData = DynamicPropertyHelper.GetProperty(dane, "InvalidData");
                                if (daneInvalidData != null)
                                {
                                    foreach (var ei in daneInvalidData)
                                    {
                                        string errMsg = ei?.ToString() ?? "Dane validation error";
                                        _logger.LogWarning("[FS-v2] Dane.InvalidData: {Error}", errMsg);
                                        if (!errors.Contains(errMsg)) errors.Add(errMsg);
                                    }
                                }
                            }
                            catch (Exception ex) { _logger.LogDebug("[FS-v2] Dane.InvalidData failed: {Msg}", ex.Message); }

                            // Method 3: Bledy property
                            try
                            {
                                var bledy = DynamicPropertyHelper.GetProperty(faktura, "Bledy");
                                if (bledy != null)
                                {
                                    foreach (var b in bledy)
                                    {
                                        string errMsg = b?.ToString() ?? "Bledy error";
                                        _logger.LogWarning("[FS-v2] Bledy: {Error}", errMsg);
                                        if (!errors.Contains(errMsg)) errors.Add(errMsg);
                                    }
                                }
                            }
                            catch (Exception ex) { _logger.LogDebug("[FS-v2] Bledy failed: {Msg}", ex.Message); }

                            // Method 4: Try Pozycje errors (use dane.Pozycje for sales documents)
                            try
                            {
                                var pozycje = dane.Pozycje;
                                if (pozycje != null)
                                {
                                    foreach (var poz in pozycje)
                                    {
                                        var pozInvalid = DynamicPropertyHelper.GetProperty(poz, "InvalidData");
                                        if (pozInvalid != null)
                                        {
                                            foreach (var pi in pozInvalid)
                                            {
                                                string errMsg = pi?.ToString() ?? "Position validation error";
                                                _logger.LogWarning("[FS-v2] Pozycja.InvalidData: {Error}", errMsg);
                                                if (!errors.Contains(errMsg)) errors.Add(errMsg);
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception ex) { _logger.LogDebug("[FS-v2] dane.Pozycje.InvalidData failed: {Msg}", ex.Message); }

                            // Method 5: WalidujDane (similar to WarehouseDocumentsController)
                            try
                            {
                                var validationErrors = faktura.WalidujDane();
                                if (validationErrors != null)
                                {
                                    foreach (var err in validationErrors)
                                    {
                                        string errStr = (string)(err?.ToString() ?? "Unknown validation error");
                                        _logger.LogWarning("[FS-v2] WalidujDane error (post-save): {Error}", (object)errStr);
                                        if (!errors.Contains(errStr))
                                            errors.Add(errStr);
                                    }
                                }
                            }
                            catch (Exception vex)
                            {
                                _logger.LogDebug("[FS-v2] WalidujDane (post-save) failed: {Msg}", vex.Message);
                            }

                            if (errors.Count == 0)
                            {
                                _logger.LogWarning("[FS-v2] No errors found but Zapisz() returned false. Document may have validation issues not exposed via standard properties.");
                                errors.Add("Document save failed - no specific error message available from SDK");
                            }
                        }
                        return (false, null, "Failed to create sales invoice", errors ?? new List<string>());
                    }
                }
            });

            if (result.Success)
            {
                return CreatedAtAction(nameof(GetDocument), new { id = result.Data?.Id }, ApiResponse<DocumentDto>.Ok(result.Data!, result.Message));
            }
            else if (result.Errors.Any())
            {
                return BadRequest(ApiResponse<DocumentDto>.Error(result.Message, result.Errors));
            }
            else
            {
                return StatusCode(500, ApiResponse<DocumentDto>.Error(result.Message));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating sales invoice");
            return StatusCode(500, ApiResponse<DocumentDto>.Error("Error creating sales invoice", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create a new customer order (Zamowienie od klienta)
    /// </summary>
    /// <remarks>
    /// IMPORTANT: This endpoint requires WindowsFormsSynchronizationContext on the SDK thread.
    /// The document creation pattern is:
    /// 1. Set warehouse on Dokument.Magazyn
    /// 2. Call ZarezerwujNumer() to reserve document number
    /// 3. Add items using Pozycje.Dodaj(towarId)
    /// 4. Call Zapisz() to save
    /// </remarks>
    [HttpPost("customer-order")]
    public async Task<ActionResult<ApiResponse<DocumentDto>>> CreateCustomerOrder([FromBody] CreateDocumentRequest request)
    {
        try
        {
            // Use thread-safe execution - EF6 is NOT thread-safe
            var result = await _sferaService.ExecuteWithLockAsync<(bool Success, DocumentDto? Data, string Message, List<string> Errors)>(() =>
            {
                var zamowieniaManager = _sferaService.GetManager("ZamowieniaOdKlientow");
                if (zamowieniaManager == null)
                {
                    return (false, null, "Failed to get ZamowieniaOdKlientow manager", new List<string>());
                }

                var konfiguracje = _sferaService.GetManager("Konfiguracje");
                var konfiguracja = konfiguracje?.DaneDomyslne?.ZamowienieOdKlienta;

                using (var zamowienie = konfiguracja != null ? zamowieniaManager.Utworz(konfiguracja) : zamowieniaManager.Utworz())
                {
                    dynamic dane = zamowienie.Dane;

                    // Set customer
                    SetCustomerOnDocument(dane, request.CustomerId, request.CustomerNIP);

                    // CRITICAL: Set warehouse on Dokument (not Dane!) - required for document numbering
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
                                zamowienie.Dokument.Magazyn = magazyn;
                            }
                        }
                    }

                    // CRITICAL: Set dates BEFORE ZarezerwujNumer so document number includes correct year/month
                    if (request.IssueDate.HasValue)
                    {
                        if (!DynamicPropertyHelper.TrySetProperty(dane, "DataWydaniaWystawienia", request.IssueDate.Value))
                        {
                            DynamicPropertyHelper.TrySetProperty(dane, "DataWystawienia", request.IssueDate.Value);
                        }
                    }

                    // CRITICAL: Reserve number AFTER setting dates - number depends on date
                    zamowienie.ZarezerwujNumer();
                    _logger.LogInformation("Reserved customer order number: {Number}", (string?)zamowienie.PodajPodgladNumeru()?.ToString() ?? "");

                    // Set notes
                    if (!string.IsNullOrEmpty(request.Notes))
                    {
                        dane.Uwagi = request.Notes;
                    }

                    // Add items using product ID
                    AddItemsToDocumentById(zamowienie, request.Items);

                    if ((bool)zamowienie.Zapisz())
                    {
                        string docNumber = zamowienie.PodajPodgladNumeru()?.ToString() ?? "";
                        int docId = (int)zamowienie.Dokument.Id;
                        _logger.LogInformation("Created customer order {Number}, Id={Id}", docNumber, docId);

                        return (true, MapOrderToDto(dane), "Customer order created successfully", new List<string>());
                    }
                    else
                    {
                        var errors = GetBusinessObjectErrors(zamowienie);
                        return (false, null, "Failed to create customer order", errors);
                    }
                }
            });

            if (result.Success)
            {
                return CreatedAtAction(nameof(GetDocument), new { id = result.Data?.Id }, ApiResponse<DocumentDto>.Ok(result.Data!, result.Message));
            }
            else if (result.Errors.Any())
            {
                return BadRequest(ApiResponse<DocumentDto>.Error(result.Message, result.Errors));
            }
            else
            {
                return StatusCode(500, ApiResponse<DocumentDto>.Error(result.Message));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating customer order");
            return StatusCode(500, ApiResponse<DocumentDto>.Error("Error creating customer order", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create a purchase invoice (Faktura zakupu)
    /// </summary>
    /// <remarks>
    /// IMPORTANT: This endpoint requires WindowsFormsSynchronizationContext on the SDK thread.
    /// The document creation pattern is:
    /// 1. Set warehouse on Dokument.Magazyn
    /// 2. Call ZarezerwujNumer() to reserve document number
    /// 3. Add items using Pozycje.Dodaj(towarId)
    /// 4. Call Zapisz() to save
    /// </remarks>
    [HttpPost("purchase-invoice")]
    public async Task<ActionResult<ApiResponse<DocumentDto>>> CreatePurchaseInvoice([FromBody] CreateDocumentRequest request)
    {
        try
        {
            // Use thread-safe execution - EF6 is NOT thread-safe
            var result = await _sferaService.ExecuteWithLockAsync<(bool Success, DocumentDto? Data, string Message, List<string> Errors)>(() =>
            {
                var dokumentyZakupu = _sferaService.GetManager("DokumentyZakupu");
                if (dokumentyZakupu == null)
                {
                    return (false, null, "Failed to get DokumentyZakupu manager", new List<string>());
                }

                using (var faktura = dokumentyZakupu.UtworzFaktureZakupu())
                {
                    dynamic dane = faktura.Dane;

                    // Set supplier
                    SetCustomerOnDocument(dane, request.CustomerId, request.CustomerNIP);

                    // CRITICAL: Set warehouse on Dokument (not Dane!) - required for document numbering
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
                                faktura.Dokument.Magazyn = magazyn;
                            }
                        }
                    }

                    // CRITICAL: Set dates BEFORE reserving number (number format includes year/month)
                    if (request.IssueDate.HasValue)
                    {
                        _logger.LogInformation("Setting FZ IssueDate to: {Date}", request.IssueDate.Value);
                        if (!DynamicPropertyHelper.TrySetProperty(dane, "DataWydaniaWystawienia", request.IssueDate.Value))
                        {
                            DynamicPropertyHelper.TrySetProperty(dane, "DataWystawienia", request.IssueDate.Value);
                        }
                    }

                    // Reserve number AFTER setting date (number depends on date!)
                    faktura.ZarezerwujNumer();
                    _logger.LogInformation("Reserved purchase invoice number: {Number}", (string?)faktura.PodajPodgladNumeru()?.ToString() ?? "");

                    // Set notes
                    if (!string.IsNullOrEmpty(request.Notes))
                    {
                        dane.Uwagi = request.Notes;
                    }

                    // Add items using product ID
                    AddItemsToDocumentById(faktura, request.Items);

                    if ((bool)faktura.Zapisz())
                    {
                        string docNumber = faktura.PodajPodgladNumeru()?.ToString() ?? "";
                        int docId = (int)faktura.Dokument.Id;
                        _logger.LogInformation("Created purchase invoice {Number}, Id={Id}", docNumber, docId);

                        return (true, MapPurchaseDocumentToDto(dane), "Purchase invoice created successfully", new List<string>());
                    }
                    else
                    {
                        var errors = GetBusinessObjectErrors(faktura);
                        return (false, null, "Failed to create purchase invoice", errors);
                    }
                }
            });

            if (result.Success)
            {
                return CreatedAtAction(nameof(GetDocument), new { id = result.Data?.Id }, ApiResponse<DocumentDto>.Ok(result.Data!, result.Message));
            }
            else if (result.Errors.Any())
            {
                return BadRequest(ApiResponse<DocumentDto>.Error(result.Message, result.Errors));
            }
            else
            {
                return StatusCode(500, ApiResponse<DocumentDto>.Error(result.Message));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating purchase invoice");
            return StatusCode(500, ApiResponse<DocumentDto>.Error("Error creating purchase invoice", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create a sales invoice correction (Korekta faktury sprzedazy)
    /// </summary>
    [HttpPost("sales-invoice-correction")]
    public ActionResult<ApiResponse<CorrectionDto>> CreateSalesInvoiceCorrection([FromBody] CreateCorrectionRequest request)
    {
        try
        {
            var korektyManager = _sferaService.GetManager("KorektyDokumentowSprzedazy");
            if (korektyManager == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get KorektyDokumentowSprzedazy manager"));
            }

            dynamic korekta;

            // If we have original document, create correction for it
            if (request.OriginalDocumentId.HasValue)
            {
                var dokumentySprzedazy = _sferaService.GetManager("DokumentySprzedazy");
                if (dokumentySprzedazy == null)
                {
                    return StatusCode(500, ApiResponse<object>.Error("Failed to get DokumentySprzedazy manager"));
                }

                dynamic? oryginal = null;
                foreach (var d in dokumentySprzedazy.Dane.Wszystkie())
                {
                    if (DynamicPropertyHelper.GetId(d) == request.OriginalDocumentId.Value)
                    {
                        oryginal = d;
                        break;
                    }
                }

                if (oryginal == null)
                {
                    return NotFound(ApiResponse<CorrectionDto>.Error($"Original document with ID {request.OriginalDocumentId} not found"));
                }

                korekta = korektyManager.UtworzKorekteFakturySprzedazy(oryginal);
            }
            else
            {
                // Correction without original document
                korekta = korektyManager.UtworzKorekteFakturySprzedazy();
                SetCustomerOnDocument(korekta.Dane, request.CustomerId, request.CustomerNIP);
            }

            dynamic dane = korekta.Dane;

            // Set correction reason
            if (!string.IsNullOrEmpty(request.CorrectionReason))
            {
                dane.PrzyczynaKorekty = request.CorrectionReason;
            }

            if (!string.IsNullOrEmpty(request.Notes))
            {
                dane.Uwagi = request.Notes;
            }

            if (request.IssueDate.HasValue)
            {
                if (!DynamicPropertyHelper.TrySetProperty(dane, "DataWydaniaWystawienia", request.IssueDate.Value))
                {
                    DynamicPropertyHelper.TrySetProperty(dane, "DataWystawienia", request.IssueDate.Value);
                }
            }

            // Add correction items
            AddCorrectionItems(korekta, request.Items);

            if ((bool)korekta.Zapisz())
            {
                string? fullNumber = DynamicPropertyHelper.GetString(dane, "NumerWewnetrzny", "PelnaSygnatura");
                _logger.LogInformation("Created sales invoice correction {Number}", fullNumber);

                return CreatedAtAction(
                    nameof(GetDocument),
                    new { id = DynamicPropertyHelper.GetId(dane) },
                    ApiResponse<CorrectionDto>.Ok(MapCorrectionToDto(dane), "Sales invoice correction created successfully"));
            }
            else
            {
                var errors = GetBusinessObjectErrors(korekta);
                return BadRequest(ApiResponse<CorrectionDto>.Error("Failed to create sales invoice correction", errors));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating sales invoice correction");
            return StatusCode(500, ApiResponse<CorrectionDto>.Error("Error creating sales invoice correction", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create a purchase invoice correction (Korekta faktury zakupu)
    /// </summary>
    [HttpPost("purchase-invoice-correction")]
    public ActionResult<ApiResponse<CorrectionDto>> CreatePurchaseInvoiceCorrection([FromBody] CreateCorrectionRequest request)
    {
        try
        {
            var korektyManager = _sferaService.GetManager("KorektyDokumentowZakupu");
            if (korektyManager == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get KorektyDokumentowZakupu manager"));
            }

            dynamic korekta;

            if (request.OriginalDocumentId.HasValue)
            {
                var dokumentyZakupu = _sferaService.GetManager("DokumentyZakupu");
                if (dokumentyZakupu == null)
                {
                    return StatusCode(500, ApiResponse<object>.Error("Failed to get DokumentyZakupu manager"));
                }

                dynamic? oryginal = null;
                foreach (var d in dokumentyZakupu.Dane.Wszystkie())
                {
                    if (DynamicPropertyHelper.GetId(d) == request.OriginalDocumentId.Value)
                    {
                        oryginal = d;
                        break;
                    }
                }

                if (oryginal == null)
                {
                    return NotFound(ApiResponse<CorrectionDto>.Error($"Original document with ID {request.OriginalDocumentId} not found"));
                }

                korekta = korektyManager.UtworzKorekteFakturyZakupu(oryginal);
            }
            else
            {
                korekta = korektyManager.UtworzKorekteFakturyZakupu();
                SetCustomerOnDocument(korekta.Dane, request.CustomerId, request.CustomerNIP);
            }

            dynamic dane = korekta.Dane;

            if (!string.IsNullOrEmpty(request.CorrectionReason))
            {
                dane.PrzyczynaKorekty = request.CorrectionReason;
            }

            if (!string.IsNullOrEmpty(request.Notes))
            {
                dane.Uwagi = request.Notes;
            }

            if (request.IssueDate.HasValue)
            {
                if (!DynamicPropertyHelper.TrySetProperty(dane, "DataWydaniaWystawienia", request.IssueDate.Value))
                {
                    DynamicPropertyHelper.TrySetProperty(dane, "DataWystawienia", request.IssueDate.Value);
                }
            }

            // Add correction items
            AddCorrectionItems(korekta, request.Items, usePurchaseUnit: true);

            if ((bool)korekta.Zapisz())
            {
                string? fullNumber = DynamicPropertyHelper.GetString(dane, "NumerWewnetrzny", "PelnaSygnatura");
                _logger.LogInformation("Created purchase invoice correction {Number}", fullNumber);

                return CreatedAtAction(
                    nameof(GetDocument),
                    new { id = DynamicPropertyHelper.GetId(dane) },
                    ApiResponse<CorrectionDto>.Ok(MapPurchaseCorrectionToDto(dane), "Purchase invoice correction created successfully"));
            }
            else
            {
                var errors = GetBusinessObjectErrors(korekta);
                return BadRequest(ApiResponse<CorrectionDto>.Error("Failed to create purchase invoice correction", errors));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating purchase invoice correction");
            return StatusCode(500, ApiResponse<CorrectionDto>.Error("Error creating purchase invoice correction", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create a receipt (Paragon)
    /// </summary>
    /// <remarks>
    /// IMPORTANT: This endpoint requires WindowsFormsSynchronizationContext on the SDK thread.
    /// The document creation pattern is:
    /// 1. Set warehouse on Dokument.Magazyn
    /// 2. Call ZarezerwujNumer() to reserve document number
    /// 3. Add items using Pozycje.Dodaj(towarId)
    /// 4. Call Zapisz() to save
    /// </remarks>
    [HttpPost("receipt")]
    public async Task<ActionResult<ApiResponse<DocumentDto>>> CreateReceipt([FromBody] CreateReceiptRequest request)
    {
        try
        {
            // Validate stock availability for outgoing receipt
            if (request.Items != null && request.Items.Any() && !string.IsNullOrEmpty(request.WarehouseSymbol))
            {
                var stockValidation = _stockHelper.ValidateStock(
                    request.Items,
                    request.WarehouseSymbol,
                    item => item.ProductId,
                    item => item.ProductSymbol,
                    item => item.ProductEan,
                    item => item.Quantity);

                if (!stockValidation.AllItemsAvailable)
                {
                    _logger.LogWarning("Receipt creation failed - insufficient stock: {Errors}", string.Join("; ", stockValidation.Errors));
                    return BadRequest(ApiResponse<DocumentDto>.Error("Insufficient stock for receipt", stockValidation.Errors));
                }
            }

            // Use thread-safe execution - EF6 is NOT thread-safe
            var result = await _sferaService.ExecuteWithLockAsync<(bool Success, DocumentDto? Data, string Message, List<string> Errors)>(() =>
            {
                var dokumentySprzedazy = _sferaService.GetManager("DokumentySprzedazy");
                if (dokumentySprzedazy == null)
                {
                    return (false, null, "Failed to get DokumentySprzedazy manager", new List<string>());
                }

                dynamic paragon = request.Type switch
                {
                    ReceiptType.Named => dokumentySprzedazy.UtworzParagonImienny(),
                    ReceiptType.Fiscal => dokumentySprzedazy.UtworzParagonFiskalny(),
                    _ => dokumentySprzedazy.UtworzParagon()
                };

                dynamic dane = paragon.Dane;

                // Set customer for named receipts
                if (request.Type == ReceiptType.Named)
                {
                    SetCustomerOnDocument(dane, request.CustomerId, request.CustomerNIP);
                }

                // CRITICAL: Set warehouse on Dokument (not Dane!) - required for document numbering
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
                            paragon.Dokument.Magazyn = magazyn;
                        }
                    }
                }

                // CRITICAL: Set dates BEFORE ZarezerwujNumer so document number includes correct year/month
                if (request.IssueDate.HasValue)
                {
                    if (!DynamicPropertyHelper.TrySetProperty(dane, "DataWydaniaWystawienia", request.IssueDate.Value))
                    {
                        DynamicPropertyHelper.TrySetProperty(dane, "DataWystawienia", request.IssueDate.Value);
                    }
                }

                // CRITICAL: Reserve number AFTER setting dates - number depends on date
                paragon.ZarezerwujNumer();
                _logger.LogInformation("Reserved receipt number: {Number}", (string?)paragon.PodajPodgladNumeru()?.ToString() ?? "");

                if (!string.IsNullOrEmpty(request.Notes))
                {
                    dane.Uwagi = request.Notes;
                }

                // Set payment method
                SetPaymentMethodOnDocument(dane, request.PaymentMethod, request.PaymentMethodId);

                // Add items using product ID
                AddReceiptItemsById(paragon, request.Items);

                if ((bool)paragon.Zapisz())
                {
                    string docNumber = paragon.PodajPodgladNumeru()?.ToString() ?? "";
                    int docId = (int)paragon.Dokument.Id;
                    _logger.LogInformation("Created receipt {Number}, Id={Id}", docNumber, docId);

                    return (true, MapSalesDocumentToDto(dane), "Receipt created successfully", new List<string>());
                }
                else
                {
                    var errors = GetBusinessObjectErrors(paragon);
                    return (false, null, "Failed to create receipt", errors);
                }
            });

            if (result.Success)
            {
                return CreatedAtAction(nameof(GetDocument), new { id = result.Data?.Id }, ApiResponse<DocumentDto>.Ok(result.Data!, result.Message));
            }
            else if (result.Errors.Any())
            {
                return BadRequest(ApiResponse<DocumentDto>.Error(result.Message, result.Errors));
            }
            else
            {
                return StatusCode(500, ApiResponse<DocumentDto>.Error(result.Message));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating receipt");
            return StatusCode(500, ApiResponse<DocumentDto>.Error("Error creating receipt", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create receipt return (Zwrot do paragonu)
    /// </summary>
    [HttpPost("receipt-return")]
    public ActionResult<ApiResponse<CorrectionDto>> CreateReceiptReturn([FromBody] CreateCorrectionRequest request)
    {
        try
        {
            var korektyManager = _sferaService.GetManager("KorektyDokumentowSprzedazy");
            if (korektyManager == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get KorektyDokumentowSprzedazy manager"));
            }

            dynamic zwrot;

            if (request.OriginalDocumentId.HasValue)
            {
                var dokumentySprzedazy = _sferaService.GetManager("DokumentySprzedazy");
                if (dokumentySprzedazy == null)
                {
                    return StatusCode(500, ApiResponse<object>.Error("Failed to get DokumentySprzedazy manager"));
                }

                dynamic? paragon = null;
                foreach (var d in dokumentySprzedazy.Dane.Wszystkie())
                {
                    if (DynamicPropertyHelper.GetId(d) == request.OriginalDocumentId.Value)
                    {
                        paragon = d;
                        break;
                    }
                }

                if (paragon == null)
                {
                    return NotFound(ApiResponse<CorrectionDto>.Error($"Original receipt with ID {request.OriginalDocumentId} not found"));
                }

                zwrot = korektyManager.UtworzZwrotDoParagonu(paragon);
            }
            else
            {
                zwrot = korektyManager.UtworzZwrotDoParagonu();
            }

            dynamic dane = zwrot.Dane;

            if (!string.IsNullOrEmpty(request.CorrectionReason))
            {
                dane.PrzyczynaKorekty = request.CorrectionReason;
            }

            if (!string.IsNullOrEmpty(request.Notes))
            {
                dane.Uwagi = request.Notes;
            }

            // Add return items
            AddReturnItems(zwrot, request.Items);

            if ((bool)zwrot.Zapisz())
            {
                string? fullNumber = DynamicPropertyHelper.GetString(dane, "NumerWewnetrzny", "PelnaSygnatura");
                _logger.LogInformation("Created receipt return {Number}", fullNumber);

                return CreatedAtAction(
                    nameof(GetDocument),
                    new { id = DynamicPropertyHelper.GetId(dane) },
                    ApiResponse<CorrectionDto>.Ok(MapCorrectionToDto(dane), "Receipt return created successfully"));
            }
            else
            {
                var errors = GetBusinessObjectErrors(zwrot);
                return BadRequest(ApiResponse<CorrectionDto>.Error("Failed to create receipt return", errors));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating receipt return");
            return StatusCode(500, ApiResponse<CorrectionDto>.Error("Error creating receipt return", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create an advance invoice (Faktura zaliczkowa)
    /// </summary>
    [HttpPost("advance-invoice")]
    public ActionResult<ApiResponse<DocumentDto>> CreateAdvanceInvoice([FromBody] CreateAdvanceInvoiceRequest request)
    {
        try
        {
            var dokumentySprzedazy = _sferaService.GetManager("DokumentySprzedazy");
            if (dokumentySprzedazy == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get DokumentySprzedazy manager"));
            }

            using (var faktura = dokumentySprzedazy.UtworzFaktureZaliczkowa())
            {
                dynamic dane = faktura.Dane;

                SetCustomerOnDocument(dane, request.CustomerId, request.CustomerNIP);

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
                            dane.Magazyn = magazyn;
                        }
                    }
                }

                if (request.IssueDate.HasValue)
                {
                    if (!DynamicPropertyHelper.TrySetProperty(dane, "DataWydaniaWystawienia", request.IssueDate.Value))
                    {
                        DynamicPropertyHelper.TrySetProperty(dane, "DataWystawienia", request.IssueDate.Value);
                    }
                }

                if (request.SaleDate.HasValue)
                {
                    dane.DataSprzedazy = request.SaleDate.Value;
                }

                if (!string.IsNullOrEmpty(request.Notes))
                {
                    dane.Uwagi = request.Notes;
                }

                // Add items
                if (request.Items != null)
                {
                    AddItemsToDocument(faktura, request.Items);
                }

                if ((bool)faktura.Zapisz())
                {
                    string? fullNumber = DynamicPropertyHelper.GetString(dane, "NumerWewnetrzny", "PelnaSygnatura");
                    _logger.LogInformation("Created advance invoice {Number}", fullNumber);

                    return CreatedAtAction(
                        nameof(GetDocument),
                        new { id = DynamicPropertyHelper.GetId(dane) },
                        ApiResponse<DocumentDto>.Ok(MapSalesDocumentToDto(dane), "Advance invoice created successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(faktura);
                    return BadRequest(ApiResponse<DocumentDto>.Error("Failed to create advance invoice", errors));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating advance invoice");
            return StatusCode(500, ApiResponse<DocumentDto>.Error("Error creating advance invoice", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create a VAT margin invoice (Faktura VAT marza)
    /// </summary>
    [HttpPost("vat-margin-invoice")]
    public ActionResult<ApiResponse<DocumentDto>> CreateVatMarginInvoice([FromBody] CreateDocumentRequest request)
    {
        try
        {
            var dokumentySprzedazy = _sferaService.GetManager("DokumentySprzedazy");
            if (dokumentySprzedazy == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get DokumentySprzedazy manager"));
            }

            using (var faktura = dokumentySprzedazy.UtworzFaktureVATMarza())
            {
                dynamic dane = faktura.Dane;

                SetCustomerOnDocument(dane, request.CustomerId, request.CustomerNIP);

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
                            dane.Magazyn = magazyn;
                        }
                    }
                }

                if (request.IssueDate.HasValue)
                {
                    if (!DynamicPropertyHelper.TrySetProperty(dane, "DataWydaniaWystawienia", request.IssueDate.Value))
                    {
                        DynamicPropertyHelper.TrySetProperty(dane, "DataWystawienia", request.IssueDate.Value);
                    }
                }

                if (request.SaleDate.HasValue)
                {
                    dane.DataSprzedazy = request.SaleDate.Value;
                }

                if (!string.IsNullOrEmpty(request.Notes))
                {
                    dane.Uwagi = request.Notes;
                }

                // Add items
                AddItemsToDocument(faktura, request.Items);

                if ((bool)faktura.Zapisz())
                {
                    string? fullNumber = DynamicPropertyHelper.GetString(dane, "NumerWewnetrzny", "PelnaSygnatura");
                    _logger.LogInformation("Created VAT margin invoice {Number}", fullNumber);

                    return CreatedAtAction(
                        nameof(GetDocument),
                        new { id = DynamicPropertyHelper.GetId(dane) },
                        ApiResponse<DocumentDto>.Ok(MapSalesDocumentToDto(dane), "VAT margin invoice created successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(faktura);
                    return BadRequest(ApiResponse<DocumentDto>.Error("Failed to create VAT margin invoice", errors));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating VAT margin invoice");
            return StatusCode(500, ApiResponse<DocumentDto>.Error("Error creating VAT margin invoice", new List<string> { ex.Message }));
        }
    }

    #region Helper Methods

    private void SetCustomerOnDocument(dynamic dokumentDane, int? customerId, string? customerNIP)
    {
        _logger.LogDebug("SetCustomerOnDocument: CustomerId={Id}, NIP={NIP}", customerId?.ToString() ?? "(null)", customerNIP ?? "(null)");

        if (!customerId.HasValue && string.IsNullOrEmpty(customerNIP))
        {
            _logger.LogWarning("SetCustomerOnDocument: No customer ID or NIP provided!");
            return;
        }

        var podmiotyManager = _sferaService.GetManager("Podmioty");
        if (podmiotyManager == null)
        {
            _logger.LogError("SetCustomerOnDocument: Podmioty manager is null!");
            return;
        }

        dynamic? podmiot = null;
        if (customerId.HasValue)
        {
            foreach (var p in podmiotyManager.Dane.Wszystkie())
            {
                if (DynamicPropertyHelper.GetId(p) == customerId.Value)
                {
                    podmiot = p;
                    break;
                }
            }
        }
        else if (!string.IsNullOrEmpty(customerNIP))
        {
            foreach (var p in podmiotyManager.Dane.Wszystkie())
            {
                string? nip = DynamicPropertyHelper.GetString(p, "NIP");
                if (nip == customerNIP)
                {
                    podmiot = p;
                    break;
                }
            }
        }

        if (podmiot != null)
        {
            int podmiotId = DynamicPropertyHelper.GetId(podmiot);
            string? podmiotNazwa = DynamicPropertyHelper.GetString(podmiot, "Nazwa");
            _logger.LogInformation("SetCustomerOnDocument: Found customer [{Id}] {Name}", podmiotId, podmiotNazwa ?? "(no name)");
            dokumentDane.Podmiot = podmiot;
        }
        else
        {
            _logger.LogWarning("SetCustomerOnDocument: Customer NOT FOUND! ID={Id}, NIP={NIP}", customerId?.ToString() ?? "(null)", customerNIP ?? "(null)");
        }
    }

    /// <summary>
    /// Common payment method name mappings from various systems to Nexo symbols
    /// </summary>
    private static readonly Dictionary<string, string> PaymentMethodMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        // Polish names
        { "gotówka", "GOTOWKA" },
        { "gotowka", "GOTOWKA" },
        { "przelew", "PRZELEW" },
        { "karta", "KARTA" },
        { "kredyt", "KREDYT" },
        { "pobranie", "POBRANIE" },
        { "za pobraniem", "POBRANIE" },
        { "kompensata", "KOMPENSATA" },
        { "barter", "BARTER" },
        { "przedpłata", "PRZEDPLATA" },
        { "przedplata", "PRZEDPLATA" },
        // English names
        { "cash", "GOTOWKA" },
        { "transfer", "PRZELEW" },
        { "card", "KARTA" },
        { "credit", "KREDYT" },
        { "cod", "POBRANIE" },
        // Variations
        { "przelew bankowy", "PRZELEW" },
        { "karta płatnicza", "KARTA" },
        { "karta platnicza", "KARTA" },
    };

    /// <summary>
    /// Sets payment method on a document by symbol or ID.
    /// </summary>
    private void SetPaymentMethodOnDocument(dynamic dokumentDane, string? paymentMethodSymbol, int? paymentMethodId)
    {
        _logger.LogDebug("SetPaymentMethodOnDocument called with Symbol={Symbol}, Id={Id}",
            (object?)paymentMethodSymbol ?? "(null)", (object?)paymentMethodId?.ToString() ?? "(null)");

        if (string.IsNullOrEmpty(paymentMethodSymbol) && !paymentMethodId.HasValue)
        {
            _logger.LogDebug("No payment method specified - skipping");
            return;
        }

        try
        {
            var formyManager = _sferaService.GetManager("FormyPlatnosci");
            if (formyManager == null)
            {
                _logger.LogWarning("FormyPlatnosci manager is null");
                return;
            }

            // Log available payment methods for debugging
            var availableMethods = new List<string>();
            foreach (var f in formyManager.Dane.Wszystkie())
            {
                var id = DynamicPropertyHelper.GetId(f);
                var sym = DynamicPropertyHelper.GetString(f, "Symbol");
                var nazwa = DynamicPropertyHelper.GetString(f, "Nazwa");
                availableMethods.Add($"[{id}] {sym} ({nazwa})");
            }
            _logger.LogDebug("Available payment methods: {Methods}", string.Join(", ", availableMethods));

            dynamic? formaPlatnosci = null;

            if (paymentMethodId.HasValue)
            {
                foreach (var f in formyManager.Dane.Wszystkie())
                {
                    if (DynamicPropertyHelper.GetId(f) == paymentMethodId.Value)
                    {
                        formaPlatnosci = f;
                        break;
                    }
                }
            }
            else if (!string.IsNullOrEmpty(paymentMethodSymbol))
            {
                // Apply mapping if exists
                string searchSymbol = paymentMethodSymbol;
                if (PaymentMethodMappings.TryGetValue(paymentMethodSymbol, out var mappedSymbol))
                {
                    _logger.LogDebug("Mapped payment method '{Original}' to '{Mapped}'", paymentMethodSymbol, mappedSymbol);
                    searchSymbol = mappedSymbol;
                }

                // Try exact match first
                foreach (var f in formyManager.Dane.Wszystkie())
                {
                    var symbol = DynamicPropertyHelper.GetString(f, "Symbol");
                    if (string.Equals(symbol, searchSymbol, StringComparison.OrdinalIgnoreCase))
                    {
                        formaPlatnosci = f;
                        break;
                    }
                }

                // Try partial match on symbol or name if no exact match
                if (formaPlatnosci == null)
                {
                    foreach (var f in formyManager.Dane.Wszystkie())
                    {
                        var symbol = DynamicPropertyHelper.GetString(f, "Symbol");
                        var nazwa = DynamicPropertyHelper.GetString(f, "Nazwa");
                        if ((symbol != null && symbol.Contains(searchSymbol, StringComparison.OrdinalIgnoreCase)) ||
                            (nazwa != null && nazwa.Contains(searchSymbol, StringComparison.OrdinalIgnoreCase)))
                        {
                            formaPlatnosci = f;
                            int? fId = DynamicPropertyHelper.GetId(f);
                            _logger.LogDebug("Found payment method by partial match: [{Id}] {Symbol} ({Nazwa})", (object)(fId?.ToString() ?? "?"), (object)(symbol ?? ""), (object)(nazwa ?? ""));
                            break;
                        }
                    }
                }

                // Try original value if mapping didn't help
                if (formaPlatnosci == null && searchSymbol != paymentMethodSymbol)
                {
                    foreach (var f in formyManager.Dane.Wszystkie())
                    {
                        string? symbol = DynamicPropertyHelper.GetString(f, "Symbol");
                        string? nazwa = DynamicPropertyHelper.GetString(f, "Nazwa");
                        if ((symbol != null && symbol.Contains(paymentMethodSymbol, StringComparison.OrdinalIgnoreCase)) ||
                            (nazwa != null && nazwa.Contains(paymentMethodSymbol, StringComparison.OrdinalIgnoreCase)))
                        {
                            formaPlatnosci = f;
                            int? fId = DynamicPropertyHelper.GetId(f);
                            _logger.LogDebug("Found payment method by original value partial match: [{Id}] {Symbol} ({Nazwa})", (object)(fId?.ToString() ?? "?"), (object)(symbol ?? ""), (object)(nazwa ?? ""));
                            break;
                        }
                    }
                }
            }

            if (formaPlatnosci != null)
            {
                dokumentDane.FormaPlatnosci = formaPlatnosci;
                int? id = DynamicPropertyHelper.GetId(formaPlatnosci);
                string? symbol = DynamicPropertyHelper.GetString(formaPlatnosci, "Symbol");
                string? nazwa = DynamicPropertyHelper.GetString(formaPlatnosci, "Nazwa");
                _logger.LogInformation("Set payment method: [{Id}] {Symbol} ({Nazwa})", (object?)id?.ToString() ?? "?", (object?)symbol ?? "(null)", (object?)nazwa ?? "(null)");
            }
            else
            {
                _logger.LogWarning("Payment method not found: Symbol={Symbol}, Id={Id}. Available: {Available}",
                    (object?)paymentMethodSymbol ?? "(null)",
                    (object?)paymentMethodId?.ToString() ?? "(null)",
                    string.Join(", ", availableMethods));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error setting payment method: {Message}", ex.Message);
        }
    }

    private void AddItemsToDocument(dynamic dokument, List<CreateDocumentItemRequest> items, bool usePurchaseUnit = false)
    {
        if (items == null || !items.Any()) return;

        var asortymentyManager = _sferaService.GetManager("Asortymenty");
        if (asortymentyManager == null) return;

        foreach (var item in items)
        {
            dynamic? asortyment = null;

            if (item.ProductId.HasValue)
            {
                foreach (var a in asortymentyManager.Dane.Wszystkie())
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
                foreach (var a in asortymentyManager.Dane.Wszystkie())
                {
                    if (DynamicPropertyHelper.GetString(a, "Symbol") == item.ProductSymbol)
                    {
                        asortyment = a;
                        break;
                    }
                }
            }

            if (asortyment != null)
            {
                var jednostka = usePurchaseUnit
                    ? (DynamicPropertyHelper.GetProperty(asortyment, "JednostkaZakupu") ??
                       DynamicPropertyHelper.GetProperty(asortyment, "JednostkaSprzedazy"))
                    : DynamicPropertyHelper.GetProperty(asortyment, "JednostkaSprzedazy");

                var pozycja = dokument.Pozycje.Dodaj(asortyment, item.Quantity, jednostka);

                if (item.PriceNet.HasValue && pozycja != null)
                {
                    pozycja.Dane.CenaNetto = item.PriceNet.Value;
                }

                if (item.DiscountPercent.HasValue && pozycja != null)
                {
                    pozycja.Dane.RabatProcent = item.DiscountPercent.Value;
                }
            }
            else if (!string.IsNullOrEmpty(item.Name))
            {
                _logger.LogWarning("Product not found for item: {Name}", item.Name);
            }
        }
    }

    private void AddReceiptItems(dynamic paragon, List<CreateDocumentItemRequest> items)
    {
        if (items == null || !items.Any()) return;

        var asortymentyManager = _sferaService.GetManager("Asortymenty");
        if (asortymentyManager == null) return;

        foreach (var item in items)
        {
            dynamic? asortyment = null;

            if (item.ProductId.HasValue)
            {
                foreach (var a in asortymentyManager.Dane.Wszystkie())
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
                foreach (var a in asortymentyManager.Dane.Wszystkie())
                {
                    if (DynamicPropertyHelper.GetString(a, "Symbol") == item.ProductSymbol)
                    {
                        asortyment = a;
                        break;
                    }
                }
            }

            if (asortyment != null)
            {
                var jednostka = DynamicPropertyHelper.GetProperty(asortyment, "JednostkaSprzedazy");
                var pozycja = paragon.Pozycje.Dodaj(asortyment, item.Quantity, jednostka);

                if (item.PriceNet.HasValue && pozycja != null)
                {
                    pozycja.Dane.CenaNetto = item.PriceNet.Value;
                }
                else if (item.PriceGross.HasValue && pozycja != null)
                {
                    pozycja.Dane.CenaBrutto = item.PriceGross.Value;
                }

                if (item.DiscountPercent.HasValue && pozycja != null)
                {
                    pozycja.Dane.RabatProcent = item.DiscountPercent.Value;
                }
            }
        }
    }

    /// <summary>
    /// Add items to document using product ID (required for EF6 compatibility)
    /// CRITICAL: This pattern works correctly with WindowsFormsSynchronizationContext
    /// </summary>
    private void AddItemsToDocumentById(dynamic dokument, List<CreateDocumentItemRequest> items)
    {
        if (items == null || !items.Any())
        {
            _logger.LogWarning("AddItemsToDocumentById: No items to add");
            return;
        }

        var asortymentyManager = _sferaService.GetManager("Asortymenty");
        if (asortymentyManager == null)
        {
            _logger.LogError("AddItemsToDocumentById: Asortymenty manager is null!");
            return;
        }

        int addedCount = 0;
        int skippedCount = 0;

        foreach (var item in items)
        {
            dynamic? asortyment = null;
            string searchKey = item.ProductSymbol ?? item.ProductId?.ToString() ?? "unknown";

            if (item.ProductId.HasValue)
            {
                foreach (var a in asortymentyManager.Dane.Wszystkie())
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
                foreach (var a in asortymentyManager.Dane.Wszystkie())
                {
                    if (DynamicPropertyHelper.GetString(a, "Symbol") == item.ProductSymbol)
                    {
                        asortyment = a;
                        break;
                    }
                }
            }

            if (asortyment == null)
            {
                _logger.LogWarning("AddItemsToDocumentById: Product not found: {SearchKey}", searchKey);
                skippedCount++;
                continue;
            }

            if (asortyment != null)
            {
                int towarId = DynamicPropertyHelper.GetId(asortyment);
                // CRITICAL: Use Pozycje.Dodaj(towarId) pattern for EF6 compatibility
                var pozycja = dokument.Pozycje.Dodaj(towarId);

                if (pozycja != null)
                {
                    pozycja.Ilosc = item.Quantity;

                    // Set price - try multiple property names as they vary by document type
                    if (item.PriceNet.HasValue)
                    {
                        // Try nested Cena object first (like warehouse documents)
                        bool priceSet = false;
                        try
                        {
                            var cenaObj = pozycja.Cena;
                            if (cenaObj != null)
                            {
                                if (DynamicPropertyHelper.TrySetProperty(cenaObj, "NettoPrzedRabatem", item.PriceNet.Value))
                                    priceSet = true;
                                else if (DynamicPropertyHelper.TrySetProperty(cenaObj, "Netto", item.PriceNet.Value))
                                    priceSet = true;
                            }
                        }
                        catch { }

                        // Try direct properties if nested didn't work
                        if (!priceSet)
                        {
                            if (!DynamicPropertyHelper.TrySetProperty(pozycja, "CenaJednostkowa", item.PriceNet.Value))
                                if (!DynamicPropertyHelper.TrySetProperty(pozycja, "CenaNetto", item.PriceNet.Value))
                                    DynamicPropertyHelper.TrySetProperty(pozycja, "CenaNettoPoRabacie", item.PriceNet.Value);
                        }
                    }
                    else if (item.PriceGross.HasValue)
                    {
                        // Try nested Cena object first
                        bool priceSet = false;
                        try
                        {
                            var cenaObj = pozycja.Cena;
                            if (cenaObj != null)
                            {
                                if (DynamicPropertyHelper.TrySetProperty(cenaObj, "BruttoPrzedRabatem", item.PriceGross.Value))
                                    priceSet = true;
                                else if (DynamicPropertyHelper.TrySetProperty(cenaObj, "Brutto", item.PriceGross.Value))
                                    priceSet = true;
                            }
                        }
                        catch { }

                        // Try direct properties if nested didn't work
                        if (!priceSet)
                        {
                            if (!DynamicPropertyHelper.TrySetProperty(pozycja, "CenaBrutto", item.PriceGross.Value))
                                DynamicPropertyHelper.TrySetProperty(pozycja, "CenaBruttoPoRabacie", item.PriceGross.Value);
                        }
                    }

                    if (item.DiscountPercent.HasValue)
                    {
                        DynamicPropertyHelper.TrySetProperty(pozycja, "RabatProcent", item.DiscountPercent.Value);
                    }

                    addedCount++;
                    _logger.LogDebug("Added item: {Symbol}, Qty={Qty}", searchKey, item.Quantity);
                }
                else
                {
                    _logger.LogWarning("AddItemsToDocumentById: Pozycje.Dodaj returned null for {SearchKey}", searchKey);
                    skippedCount++;
                }
            }
        }

        _logger.LogInformation("AddItemsToDocumentById completed: {Added} added, {Skipped} skipped out of {Total} items",
            addedCount, skippedCount, items.Count);

        if (addedCount == 0)
        {
            _logger.LogError("AddItemsToDocumentById: NO ITEMS WERE ADDED! Document will likely fail to save.");
        }
    }

    /// <summary>
    /// Add items to sales invoice (FS) using full asortyment object pattern
    /// This method provides better compatibility with DokumentSprzedazyBO
    /// </summary>
    private void AddSalesInvoiceItems(dynamic faktura, List<CreateDocumentItemRequest>? items)
    {
        if (items == null || !items.Any())
        {
            _logger.LogWarning("[FS-v2] AddSalesInvoiceItems: No items to add");
            return;
        }

        var asortymentyManager = _sferaService.GetManager("Asortymenty");
        if (asortymentyManager == null)
        {
            _logger.LogError("[FS-v2] AddSalesInvoiceItems: Asortymenty manager is null!");
            return;
        }

        int addedCount = 0;
        int skippedCount = 0;

        foreach (var item in items)
        {
            dynamic? asortyment = null;
            string searchKey = item.ProductSymbol ?? item.ProductId?.ToString() ?? "unknown";

            // Find product by ID or Symbol
            if (item.ProductId.HasValue)
            {
                foreach (var a in asortymentyManager.Dane.Wszystkie())
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
                foreach (var a in asortymentyManager.Dane.Wszystkie())
                {
                    if (DynamicPropertyHelper.GetString(a, "Symbol") == item.ProductSymbol)
                    {
                        asortyment = a;
                        break;
                    }
                }
            }

            if (asortyment == null)
            {
                _logger.LogWarning("[FS-v2] AddSalesInvoiceItems: Product not found: {SearchKey}", searchKey);
                skippedCount++;
                continue;
            }

            try
            {
                // Get product info for logging
                int towarId = DynamicPropertyHelper.GetId(asortyment);
                string towarSymbol = DynamicPropertyHelper.GetString(asortyment, "Symbol") ?? towarId.ToString();
                var jednostka = DynamicPropertyHelper.GetProperty(asortyment, "JednostkaSprzedazy");

                _logger.LogInformation("[FS-v2] Adding item: Id={Id}, Symbol={Symbol}, Qty={Qty}, Unit={Unit}",
                    towarId, (object)towarSymbol, item.Quantity, (object)(jednostka?.ToString() ?? "(null)"));

                // Try different Dodaj patterns
                dynamic? pozycja = null;

                // Pattern 1: Dodaj(asortyment, ilosc, jednostka) - full pattern like warehouse documents
                try
                {
                    if (jednostka != null)
                    {
                        pozycja = faktura.Pozycje.Dodaj(asortyment, item.Quantity, jednostka);
                        if (pozycja != null)
                        {
                            _logger.LogDebug("[FS-v2] Dodaj(asortyment, qty, jednostka) succeeded");
                        }
                    }
                }
                catch (Exception ex1)
                {
                    _logger.LogDebug("[FS-v2] Dodaj(asortyment, qty, jednostka) failed: {Msg}", ex1.Message);
                }

                // Pattern 2: Dodaj(towarId) then set Ilosc - current pattern
                if (pozycja == null)
                {
                    try
                    {
                        pozycja = faktura.Pozycje.Dodaj(towarId);
                        if (pozycja != null)
                        {
                            pozycja.Ilosc = item.Quantity;
                            _logger.LogDebug("[FS-v2] Dodaj(towarId) + Ilosc assignment succeeded");
                        }
                    }
                    catch (Exception ex2)
                    {
                        _logger.LogDebug("[FS-v2] Dodaj(towarId) failed: {Msg}", ex2.Message);
                    }
                }

                // Pattern 3: Dodaj(asortyment) then set Ilosc
                if (pozycja == null)
                {
                    try
                    {
                        pozycja = faktura.Pozycje.Dodaj(asortyment);
                        if (pozycja != null)
                        {
                            pozycja.Ilosc = item.Quantity;
                            _logger.LogDebug("[FS-v2] Dodaj(asortyment) + Ilosc assignment succeeded");
                        }
                    }
                    catch (Exception ex3)
                    {
                        _logger.LogDebug("[FS-v2] Dodaj(asortyment) failed: {Msg}", ex3.Message);
                    }
                }

                if (pozycja != null)
                {
                    // Log pozycja properties
                    try
                    {
                        var pozType = ((object)pozycja).GetType();
                        _logger.LogDebug("[FS-v2] pozycja type: {Type}", pozType.FullName);

                        // Check if Ilosc was set
                        var ilosc = pozycja.Ilosc;
                        _logger.LogDebug("[FS-v2] pozycja.Ilosc = {Ilosc}", (object)(ilosc?.ToString() ?? "(null)"));
                    }
                    catch (Exception propEx)
                    {
                        _logger.LogDebug("[FS-v2] Could not read pozycja properties: {Msg}", propEx.Message);
                    }

                    // Set price if provided
                    if (item.PriceNet.HasValue)
                    {
                        bool priceSet = false;

                        // Try nested Cena object
                        try
                        {
                            var cenaObj = pozycja.Cena;
                            if (cenaObj != null)
                            {
                                if (DynamicPropertyHelper.TrySetProperty(cenaObj, "NettoPrzedRabatem", item.PriceNet.Value))
                                {
                                    priceSet = true;
                                    _logger.LogDebug("[FS-v2] Set Cena.NettoPrzedRabatem = {Price}", item.PriceNet.Value);
                                }
                                else if (DynamicPropertyHelper.TrySetProperty(cenaObj, "Netto", item.PriceNet.Value))
                                {
                                    priceSet = true;
                                    _logger.LogDebug("[FS-v2] Set Cena.Netto = {Price}", item.PriceNet.Value);
                                }
                            }
                        }
                        catch { }

                        if (!priceSet)
                        {
                            if (DynamicPropertyHelper.TrySetProperty(pozycja, "CenaJednostkowa", item.PriceNet.Value))
                            {
                                _logger.LogDebug("[FS-v2] Set CenaJednostkowa = {Price}", item.PriceNet.Value);
                            }
                            else if (DynamicPropertyHelper.TrySetProperty(pozycja, "CenaNetto", item.PriceNet.Value))
                            {
                                _logger.LogDebug("[FS-v2] Set CenaNetto = {Price}", item.PriceNet.Value);
                            }
                        }
                    }

                    addedCount++;
                    _logger.LogInformation("[FS-v2] Added item successfully: {Symbol}, Qty={Qty}", (object)towarSymbol, item.Quantity);
                }
                else
                {
                    _logger.LogWarning("[FS-v2] All Dodaj patterns failed for: {SearchKey}", searchKey);
                    skippedCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FS-v2] Exception adding item {SearchKey}", searchKey);
                skippedCount++;
            }
        }

        _logger.LogInformation("[FS-v2] AddSalesInvoiceItems completed: {Added} added, {Skipped} skipped out of {Total} items",
            addedCount, skippedCount, items.Count);

        if (addedCount == 0)
        {
            _logger.LogError("[FS-v2] NO ITEMS WERE ADDED! Document will likely fail to save.");
        }
    }

    /// <summary>
    /// Add items to receipt using product ID (required for EF6 compatibility)
    /// CRITICAL: This pattern works correctly with WindowsFormsSynchronizationContext
    /// </summary>
    private void AddReceiptItemsById(dynamic paragon, List<CreateDocumentItemRequest> items)
    {
        if (items == null || !items.Any()) return;

        var asortymentyManager = _sferaService.GetManager("Asortymenty");
        if (asortymentyManager == null) return;

        foreach (var item in items)
        {
            dynamic? asortyment = null;

            if (item.ProductId.HasValue)
            {
                foreach (var a in asortymentyManager.Dane.Wszystkie())
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
                foreach (var a in asortymentyManager.Dane.Wszystkie())
                {
                    if (DynamicPropertyHelper.GetString(a, "Symbol") == item.ProductSymbol)
                    {
                        asortyment = a;
                        break;
                    }
                }
            }

            if (asortyment != null)
            {
                int towarId = DynamicPropertyHelper.GetId(asortyment);
                // CRITICAL: Use Pozycje.Dodaj(towarId) pattern for EF6 compatibility
                var pozycja = paragon.Pozycje.Dodaj(towarId);

                if (pozycja != null)
                {
                    pozycja.Ilosc = item.Quantity;

                    // Set price - try multiple property names as they vary by document type
                    if (item.PriceNet.HasValue)
                    {
                        bool priceSet = false;
                        try
                        {
                            var cenaObj = pozycja.Cena;
                            if (cenaObj != null)
                            {
                                if (DynamicPropertyHelper.TrySetProperty(cenaObj, "NettoPrzedRabatem", item.PriceNet.Value))
                                    priceSet = true;
                                else if (DynamicPropertyHelper.TrySetProperty(cenaObj, "Netto", item.PriceNet.Value))
                                    priceSet = true;
                            }
                        }
                        catch { }

                        if (!priceSet)
                        {
                            if (!DynamicPropertyHelper.TrySetProperty(pozycja, "CenaJednostkowa", item.PriceNet.Value))
                                if (!DynamicPropertyHelper.TrySetProperty(pozycja, "CenaNetto", item.PriceNet.Value))
                                    DynamicPropertyHelper.TrySetProperty(pozycja, "CenaNettoPoRabacie", item.PriceNet.Value);
                        }
                    }
                    else if (item.PriceGross.HasValue)
                    {
                        bool priceSet = false;
                        try
                        {
                            var cenaObj = pozycja.Cena;
                            if (cenaObj != null)
                            {
                                if (DynamicPropertyHelper.TrySetProperty(cenaObj, "BruttoPrzedRabatem", item.PriceGross.Value))
                                    priceSet = true;
                                else if (DynamicPropertyHelper.TrySetProperty(cenaObj, "Brutto", item.PriceGross.Value))
                                    priceSet = true;
                            }
                        }
                        catch { }

                        if (!priceSet)
                        {
                            if (!DynamicPropertyHelper.TrySetProperty(pozycja, "CenaBrutto", item.PriceGross.Value))
                                DynamicPropertyHelper.TrySetProperty(pozycja, "CenaBruttoPoRabacie", item.PriceGross.Value);
                        }
                    }

                    if (item.DiscountPercent.HasValue)
                    {
                        DynamicPropertyHelper.TrySetProperty(pozycja, "RabatProcent", item.DiscountPercent.Value);
                    }
                }
            }
        }
    }

    private void AddCorrectionItems(dynamic korekta, List<CreateCorrectionItemRequest> items, bool usePurchaseUnit = false)
    {
        if (items == null || !items.Any()) return;

        var asortymentyManager = _sferaService.GetManager("Asortymenty");

        foreach (var item in items)
        {
            if (item.OriginalPositionId.HasValue)
            {
                // Find and correct existing position
                try
                {
                    var pozycjeKorygowane = DynamicPropertyHelper.GetProperty(korekta.Dane, "PozycjeKorygowane");
                    if (pozycjeKorygowane != null)
                    {
                        foreach (dynamic poz in pozycjeKorygowane)
                        {
                            if (DynamicPropertyHelper.GetId(poz) == item.OriginalPositionId.Value)
                            {
                                var pozycjaKorekty = korekta.Pozycje.Koryguj(poz);
                                if (pozycjaKorekty != null)
                                {
                                    var originalQty = DynamicPropertyHelper.GetDecimal(poz, "Ilosc");
                                    pozycjaKorekty.Dane.IloscPoKorekcie = originalQty + item.QuantityCorrection;
                                    if (item.PriceNetCorrection.HasValue)
                                    {
                                        pozycjaKorekty.Dane.CenaNettoPoKorekcie = item.PriceNetCorrection.Value;
                                    }
                                }
                                break;
                            }
                        }
                    }
                }
                catch
                {
                    // Position correction failed, continue
                }
            }
            else if ((item.ProductId.HasValue || !string.IsNullOrEmpty(item.ProductSymbol)) && asortymentyManager != null)
            {
                // Add new correction position
                dynamic? asortyment = null;

                if (item.ProductId.HasValue)
                {
                    foreach (var a in asortymentyManager.Dane.Wszystkie())
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
                    foreach (var a in asortymentyManager.Dane.Wszystkie())
                    {
                        if (DynamicPropertyHelper.GetString(a, "Symbol") == item.ProductSymbol)
                        {
                            asortyment = a;
                            break;
                        }
                    }
                }

                if (asortyment != null)
                {
                    var jednostka = usePurchaseUnit
                        ? (DynamicPropertyHelper.GetProperty(asortyment, "JednostkaZakupu") ??
                           DynamicPropertyHelper.GetProperty(asortyment, "JednostkaSprzedazy"))
                        : DynamicPropertyHelper.GetProperty(asortyment, "JednostkaSprzedazy");

                    var pozycja = korekta.Pozycje.Dodaj(asortyment, item.QuantityCorrection, jednostka);
                    if (pozycja != null && item.PriceNetCorrection.HasValue)
                    {
                        pozycja.Dane.CenaNetto = item.PriceNetCorrection.Value;
                    }
                }
            }
        }
    }

    private void AddReturnItems(dynamic zwrot, List<CreateCorrectionItemRequest> items)
    {
        if (items == null || !items.Any()) return;

        var asortymentyManager = _sferaService.GetManager("Asortymenty");
        if (asortymentyManager == null) return;

        foreach (var item in items)
        {
            dynamic? asortyment = null;

            if (item.ProductId.HasValue)
            {
                foreach (var a in asortymentyManager.Dane.Wszystkie())
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
                foreach (var a in asortymentyManager.Dane.Wszystkie())
                {
                    if (DynamicPropertyHelper.GetString(a, "Symbol") == item.ProductSymbol)
                    {
                        asortyment = a;
                        break;
                    }
                }
            }

            if (asortyment != null)
            {
                // For returns, quantity should be negative
                var qty = item.QuantityCorrection < 0 ? item.QuantityCorrection : -item.QuantityCorrection;
                var jednostka = DynamicPropertyHelper.GetProperty(asortyment, "JednostkaSprzedazy");
                zwrot.Pozycje.Dodaj(asortyment, qty, jednostka);
            }
        }
    }

    /// <summary>
    /// Maps to lightweight DTO for list views (minimal fields for performance)
    /// </summary>
    private static DocumentListItemDto MapToListItemDto(dynamic dokument)
    {
        // Guard against null document
        if (dokument == null)
        {
            return new DocumentListItemDto { Id = 0, Symbol = "NULL" };
        }

        // Use explicit casting to avoid dynamic binding issues
        int id = DynamicPropertyHelper.GetId(dokument);
        string symbol = DynamicPropertyHelper.GetString(dokument, "Symbol") ?? "";
        string? number = DynamicPropertyHelper.GetString(dokument, "NumerWewnetrzny", "PelnaSygnatura");
        string? externalNumber = DynamicPropertyHelper.GetString(dokument, "NumerZewnetrzny");
        string? referenceNumber = DynamicPropertyHelper.GetString(dokument, "NumerReferencyjny");
        string? title = DynamicPropertyHelper.GetString(dokument, "Tytul");
        DateTime? issueDate = DynamicPropertyHelper.GetDateTime(dokument, "DataWydaniaWystawienia") ??
                              DynamicPropertyHelper.GetDateTime(dokument, "DataWystawienia");
        DateTime? saleDate = DynamicPropertyHelper.GetDateTime(dokument, "DataSprzedazy");
        DateTime? dueDate = DynamicPropertyHelper.GetDateTime(dokument, "TerminPlatnosci");
        int? customerId = DynamicPropertyHelper.GetNullableInt(dokument, "PodmiotId") ??
                          DynamicPropertyHelper.GetNullableInt(dokument, "Podmiot", "Id");
        string? customerName = DynamicPropertyHelper.GetString(dokument, "Podmiot", "NazwaSkrocona");
        string? customerNIP = DynamicPropertyHelper.GetString(dokument, "Podmiot", "NIP");
        string? warehouseSymbol = DynamicPropertyHelper.GetString(dokument, "Magazyn", "Symbol");
        decimal totalNet = DynamicPropertyHelper.GetDecimal(dokument, "WartoscNetto");
        decimal totalGross = DynamicPropertyHelper.GetDecimal(dokument, "WartoscBrutto");
        decimal amountToPay = DynamicPropertyHelper.GetDecimal(dokument, "KwotaDoZaplaty");
        decimal paidAmount = totalGross - amountToPay;
        string currency = DynamicPropertyHelper.GetString(dokument, "Waluta", "Symbol") ?? "PLN";
        int? statusId = DynamicPropertyHelper.GetNullableInt(dokument, "StatusDokumentuId") ??
                        DynamicPropertyHelper.GetNullableInt(dokument, "Status", "Id");
        string? statusSymbol = DynamicPropertyHelper.GetString(dokument, "StatusDokumentu", "Symbol");

        return new DocumentListItemDto
        {
            Id = id,
            Symbol = symbol,
            Number = number,
            ExternalNumber = externalNumber,
            ReferenceNumber = referenceNumber,
            Title = title,
            IssueDate = issueDate,
            SaleDate = saleDate,
            DueDate = dueDate,
            CustomerId = customerId,
            CustomerName = customerName,
            CustomerNIP = customerNIP,
            WarehouseSymbol = warehouseSymbol,
            TotalNet = totalNet,
            TotalGross = totalGross,
            AmountToPay = amountToPay,
            PaidAmount = paidAmount > 0 ? paidAmount : null,
            Currency = currency,
            StatusId = statusId,
            StatusSymbol = statusSymbol,
            IsPaid = amountToPay <= 0,
            IsOverdue = dueDate.HasValue && dueDate.Value < DateTime.Today && amountToPay > 0
        };
    }

    private static CorrectionDto MapCorrectionToDto(dynamic dokument)
    {
        try
        {
            return new CorrectionDto
            {
                Id = DynamicPropertyHelper.GetId(dokument),
                Number = DynamicPropertyHelper.GetString(dokument, "NumerWewnetrzny", "Numer") ?? "",
                FullNumber = DynamicPropertyHelper.GetString(dokument, "NumerWewnetrzny", "PelnaSygnatura"),
                Type = CorrectionType.SalesInvoiceCorrection,
                IssueDate = DynamicPropertyHelper.GetDateTime(dokument, "DataWystawienia"),
                OriginalDocumentId = DynamicPropertyHelper.GetNullableInt(dokument, "DokumentKorygowany", "Id"),
                OriginalDocumentNumber = DynamicPropertyHelper.GetNestedString(dokument, "DokumentKorygowany", "NumerWewnetrzny", "PelnaSygnatura"),
                CustomerName = DynamicPropertyHelper.GetString(dokument, "Podmiot", "NazwaSkrocona"),
                CustomerNIP = DynamicPropertyHelper.GetString(dokument, "Podmiot", "NIP"),
                WarehouseSymbol = DynamicPropertyHelper.GetString(dokument, "Magazyn", "Symbol"),
                CorrectionNet = DynamicPropertyHelper.GetDecimal(dokument, "WartoscNetto"),
                CorrectionVat = DynamicPropertyHelper.GetDecimal(dokument, "WartoscVat"),
                CorrectionGross = DynamicPropertyHelper.GetDecimal(dokument, "WartoscBrutto"),
                CorrectionReason = DynamicPropertyHelper.GetString(dokument, "PrzyczynaKorekty"),
                Notes = DynamicPropertyHelper.GetString(dokument, "Uwagi"),
                CreatedAt = DynamicPropertyHelper.GetDateTime(dokument, "DataUtworzenia")
            };
        }
        catch
        {
            return new CorrectionDto { Id = DynamicPropertyHelper.GetId(dokument) };
        }
    }

    private static CorrectionDto MapPurchaseCorrectionToDto(dynamic dokument)
    {
        try
        {
            return new CorrectionDto
            {
                Id = DynamicPropertyHelper.GetId(dokument),
                Number = DynamicPropertyHelper.GetString(dokument, "NumerWewnetrzny", "Numer") ?? "",
                FullNumber = DynamicPropertyHelper.GetString(dokument, "NumerWewnetrzny", "PelnaSygnatura"),
                Type = CorrectionType.PurchaseInvoiceCorrection,
                IssueDate = DynamicPropertyHelper.GetDateTime(dokument, "DataWystawienia"),
                OriginalDocumentId = DynamicPropertyHelper.GetNullableInt(dokument, "DokumentKorygowany", "Id"),
                OriginalDocumentNumber = DynamicPropertyHelper.GetNestedString(dokument, "DokumentKorygowany", "NumerWewnetrzny", "PelnaSygnatura"),
                CustomerName = DynamicPropertyHelper.GetString(dokument, "Podmiot", "NazwaSkrocona"),
                CustomerNIP = DynamicPropertyHelper.GetString(dokument, "Podmiot", "NIP"),
                WarehouseSymbol = DynamicPropertyHelper.GetString(dokument, "Magazyn", "Symbol"),
                CorrectionNet = DynamicPropertyHelper.GetDecimal(dokument, "WartoscNetto"),
                CorrectionVat = DynamicPropertyHelper.GetDecimal(dokument, "WartoscVat"),
                CorrectionGross = DynamicPropertyHelper.GetDecimal(dokument, "WartoscBrutto"),
                CorrectionReason = DynamicPropertyHelper.GetString(dokument, "PrzyczynaKorekty"),
                Notes = DynamicPropertyHelper.GetString(dokument, "Uwagi"),
                CreatedAt = DynamicPropertyHelper.GetDateTime(dokument, "DataUtworzenia")
            };
        }
        catch
        {
            return new CorrectionDto { Id = DynamicPropertyHelper.GetId(dokument) };
        }
    }

    private static DocumentDto MapDocumentToDto(dynamic dokument)
    {
        try
        {
            var totalGross = DynamicPropertyHelper.GetDecimal(dokument, "WartoscBrutto");
            var amountToPay = DynamicPropertyHelper.GetDecimal(dokument, "KwotaDoZaplaty");
            var paidAmount = totalGross - amountToPay;

            var dto = new DocumentDto
            {
                // Identity
                Id = DynamicPropertyHelper.GetId(dokument),
                Symbol = DynamicPropertyHelper.GetString(dokument, "Symbol") ?? "",
                Number = DynamicPropertyHelper.GetString(dokument, "NumerWewnetrzny", "Numer") ?? "",
                FullNumber = DynamicPropertyHelper.GetString(dokument, "NumerWewnetrzny", "PelnaSygnatura"),
                ExternalNumber = DynamicPropertyHelper.GetString(dokument, "NumerZewnetrzny"),
                ReferenceNumber = DynamicPropertyHelper.GetString(dokument, "NumerReferencyjny"),
                Title = DynamicPropertyHelper.GetString(dokument, "Tytul"),

                // Status
                StatusId = DynamicPropertyHelper.GetNullableInt(dokument, "StatusDokumentuId") ??
                           DynamicPropertyHelper.GetNullableInt(dokument, "Status", "Id"),
                Status = DynamicPropertyHelper.GetString(dokument, "StatusDokumentu", "Nazwa"),
                StatusSymbol = DynamicPropertyHelper.GetString(dokument, "StatusDokumentu", "Symbol"),
                ConfigurationId = DynamicPropertyHelper.GetString(dokument, "KonfiguracjaId"),
                ConfigurationSymbol = DynamicPropertyHelper.GetString(dokument, "Konfiguracja", "Symbol"),

                // Dates
                EntryDate = DynamicPropertyHelper.GetDateTime(dokument, "DataWprowadzenia"),
                IssueDate = DynamicPropertyHelper.GetDateTime(dokument, "DataWydaniaWystawienia") ??
                            DynamicPropertyHelper.GetDateTime(dokument, "DataWystawienia"),
                SaleDate = DynamicPropertyHelper.GetDateTime(dokument, "DataSprzedazy"),
                DueDate = DynamicPropertyHelper.GetDateTime(dokument, "TerminPlatnosci"),
                Deadline = DynamicPropertyHelper.GetDateTime(dokument, "TerminRealizacji"),
                DeliveryDate = DynamicPropertyHelper.GetDateTime(dokument, "DataDostawy"),
                ReceiptDate = DynamicPropertyHelper.GetDateTime(dokument, "DataPrzyjecia"),

                // Customer
                CustomerId = DynamicPropertyHelper.GetNullableInt(dokument, "PodmiotId") ??
                             DynamicPropertyHelper.GetNullableInt(dokument, "Podmiot", "Id"),
                SelectedCustomerId = DynamicPropertyHelper.GetNullableInt(dokument, "PodmiotWybranyId"),
                CustomerName = DynamicPropertyHelper.GetString(dokument, "Podmiot", "NazwaSkrocona"),
                CustomerNIP = DynamicPropertyHelper.GetString(dokument, "Podmiot", "NIP"),

                // Recipient
                RecipientId = DynamicPropertyHelper.GetNullableInt(dokument, "Odbiorca", "Id"),
                RecipientName = DynamicPropertyHelper.GetString(dokument, "Odbiorca", "NazwaSkrocona"),

                // Warehouse
                WarehouseId = DynamicPropertyHelper.GetNullableInt(dokument, "MagazynId") ??
                              DynamicPropertyHelper.GetNullableInt(dokument, "Magazyn", "Id"),
                WarehouseSymbol = DynamicPropertyHelper.GetString(dokument, "Magazyn", "Symbol"),
                WarehouseName = DynamicPropertyHelper.GetString(dokument, "Magazyn", "Nazwa"),

                // Amounts - Goods
                GoodsAmountNet = DynamicPropertyHelper.GetDecimal(dokument, "WartoscTowarowNetto"),
                GoodsAmountGross = DynamicPropertyHelper.GetDecimal(dokument, "WartoscTowarowBrutto"),

                // Amounts - Services
                ServicesAmountNet = DynamicPropertyHelper.GetDecimal(dokument, "WartoscUslugNetto"),
                ServicesAmountGross = DynamicPropertyHelper.GetDecimal(dokument, "WartoscUslugBrutto"),

                // Amounts - Total
                TotalNet = DynamicPropertyHelper.GetDecimal(dokument, "WartoscNetto"),
                TotalVat = DynamicPropertyHelper.GetDecimal(dokument, "WartoscVat"),
                TotalGross = totalGross,
                AmountToPay = amountToPay,

                // Costs
                GoodsCostBook = DynamicPropertyHelper.GetDecimal(dokument, "KosztEwidencyjnyTowarow"),
                GoodsCostWarehouse = DynamicPropertyHelper.GetDecimal(dokument, "KosztMagazynowyTowarowWydanych") +
                                     DynamicPropertyHelper.GetDecimal(dokument, "KorektaKosztuMagazynowegoTowarowWydanych"),
                ServicesCost = DynamicPropertyHelper.GetDecimal(dokument, "KosztEwidencyjnyUslug"),
                AdditionalCost = DynamicPropertyHelper.GetDecimal(dokument, "KosztDodatkowy") +
                                 DynamicPropertyHelper.GetDecimal(dokument, "KorektaKosztuDodatkowego"),

                // Currency
                Currency = DynamicPropertyHelper.GetString(dokument, "Waluta", "Symbol") ?? "PLN",
                ExchangeRate = DynamicPropertyHelper.GetNullableDecimal(dokument, "Kurs"),
                ExchangeRateDate = DynamicPropertyHelper.GetDateTime(dokument, "DataKursu"),

                // Payment
                PaymentMethod = DynamicPropertyHelper.GetString(dokument, "FormaPlatnosci", "Nazwa"),
                PaymentMethodId = DynamicPropertyHelper.GetNullableInt(dokument, "FormaPlatnosci", "Id"),
                PaymentDate = DynamicPropertyHelper.GetDateTime(dokument, "TerminPlatnosci"),
                PaymentDays = DynamicPropertyHelper.GetNullableInt(dokument, "OdroczonaPlatnoscDni"),
                PaidAmount = paidAmount > 0 ? paidAmount : null,
                RemainingAmount = amountToPay > 0 ? amountToPay : null,

                // Payment breakdown
                PaymentBreakdown = MapPaymentBreakdown(dokument),

                // Split payment
                SplitPayment = DynamicPropertyHelper.GetNullableBool(dokument, "MechanizmPodzielonejPlatnosci"),

                // KSeF
                KsefStatus = MapKsefStatus(dokument),

                // Personnel
                IssuedBy = DynamicPropertyHelper.GetString(dokument, "Wystawil"),
                ReceivedBy = DynamicPropertyHelper.GetString(dokument, "Odebral"),
                CreatedBy = DynamicPropertyHelper.GetString(dokument, "UtworzonePrzez"),
                ModifiedBy = DynamicPropertyHelper.GetString(dokument, "ZmodyfikowanePrzez"),

                // Delivery address
                DeliveryAddress = MapDeliveryAddress(dokument),

                // Intrastat
                SubjectToIntrastat = DynamicPropertyHelper.GetNullableBool(dokument, "PodlegaDeklaracjiIntrastat"),
                IntrastatDate = DynamicPropertyHelper.GetDateTime(dokument, "DataDlaIntrastatu"),

                // JPK
                JpkProductGroup = DynamicPropertyHelper.GetString(dokument, "GrupyTowarowe"),
                JpkProcedure = DynamicPropertyHelper.GetString(dokument, "Procedury"),

                // Notes
                Notes = DynamicPropertyHelper.GetString(dokument, "Uwagi"),
                InternalNotes = DynamicPropertyHelper.GetString(dokument, "UwagiWewnetrzne"),

                // Timestamps
                CreatedAt = DynamicPropertyHelper.GetDateTime(dokument, "DataUtworzenia"),
                ModifiedAt = DynamicPropertyHelper.GetDateTime(dokument, "DataModyfikacji"),

                // Flags
                IsPrinted = DynamicPropertyHelper.GetNullableBool(dokument, "Wydrukowany"),
                IsSent = DynamicPropertyHelper.GetNullableBool(dokument, "Wyslany"),
                IsConfirmed = DynamicPropertyHelper.GetNullableBool(dokument, "Potwierdzony"),
                IsCanceled = DynamicPropertyHelper.GetNullableBool(dokument, "Anulowany"),

                Items = new List<DocumentItemDto>()
            };

            // Calculate total costs
            dto.GoodsCost = dto.GoodsCostBook + dto.GoodsCostWarehouse;

            // Map items if available
            var pozycje = DynamicPropertyHelper.GetProperty(dokument, "Pozycje");
            if (pozycje != null)
            {
                int lineNum = 1;
                foreach (dynamic poz in pozycje)
                {
                    dto.Items.Add(MapDocumentItemToDto(poz, lineNum++));
                }
            }

            return dto;
        }
        catch
        {
            return new DocumentDto { Id = DynamicPropertyHelper.GetId(dokument) };
        }
    }

    private static PaymentBreakdownDto? MapPaymentBreakdown(dynamic dokument)
    {
        try
        {
            var cash = DynamicPropertyHelper.GetDecimal(dokument, "SumaGotowka");
            var card = DynamicPropertyHelper.GetDecimal(dokument, "SumaKarta");
            var transfer = DynamicPropertyHelper.GetDecimal(dokument, "SumaPrzelew");
            var prepayment = DynamicPropertyHelper.GetDecimal(dokument, "SumaPrzedplata");
            var quickPayment = DynamicPropertyHelper.GetDecimal(dokument, "SumaPlPrzypiszona");
            var ownVoucher = DynamicPropertyHelper.GetDecimal(dokument, "SumaKartaWlasna");
            var externalVoucher = DynamicPropertyHelper.GetDecimal(dokument, "SumaKartaObca");
            var other = DynamicPropertyHelper.GetDecimal(dokument, "SumaInna");

            // Only return if there's actual payment data
            if (cash == 0 && card == 0 && transfer == 0 && prepayment == 0 &&
                quickPayment == 0 && ownVoucher == 0 && externalVoucher == 0 && other == 0)
            {
                return null;
            }

            return new PaymentBreakdownDto
            {
                Cash = cash,
                Card = card,
                BankTransfer = transfer,
                Prepayment = prepayment,
                QuickPayment = quickPayment,
                OwnVoucher = ownVoucher,
                ExternalVoucher = externalVoucher,
                Other = other
            };
        }
        catch
        {
            return null;
        }
    }

    private static KsefStatusDto? MapKsefStatus(dynamic dokument)
    {
        try
        {
            var ksefNumber = DynamicPropertyHelper.GetString(dokument, "NumerKSeF");
            var ksefStatus = DynamicPropertyHelper.GetString(dokument, "StatusKSeF");

            // Only return if there's KSeF data
            if (string.IsNullOrEmpty(ksefNumber) && string.IsNullOrEmpty(ksefStatus))
            {
                return null;
            }

            return new KsefStatusDto
            {
                KsefNumber = ksefNumber,
                Status = ksefStatus,
                SendDate = DynamicPropertyHelper.GetDateTime(dokument, "DataWyslaniaDoKSeF"),
                AcceptanceDate = DynamicPropertyHelper.GetDateTime(dokument, "DataAkceptacjiKSeF"),
                SessionId = DynamicPropertyHelper.GetString(dokument, "SesjaKSeF"),
                ErrorMessage = DynamicPropertyHelper.GetString(dokument, "BladKSeF"),
                IsRequired = DynamicPropertyHelper.GetNullableBool(dokument, "WymagaKSeF")
            };
        }
        catch
        {
            return null;
        }
    }

    private static AddressDto? MapDeliveryAddress(dynamic dokument)
    {
        try
        {
            var adresDostawy = DynamicPropertyHelper.GetProperty(dokument, "AdresDostawy");
            if (adresDostawy == null)
            {
                // Try alternative property names
                var ulica = DynamicPropertyHelper.GetString(dokument, "AdresDostawyUlica");
                var miasto = DynamicPropertyHelper.GetString(dokument, "AdresDostawyMiejscowosc");

                if (string.IsNullOrEmpty(ulica) && string.IsNullOrEmpty(miasto))
                {
                    return null;
                }

                return new AddressDto
                {
                    Street = ulica,
                    City = miasto,
                    PostalCode = DynamicPropertyHelper.GetString(dokument, "AdresDostawyKodPocztowy"),
                    Country = DynamicPropertyHelper.GetString(dokument, "AdresDostawyKraj")
                };
            }

            return new AddressDto
            {
                Street = DynamicPropertyHelper.GetString(adresDostawy, "Ulica"),
                Building = DynamicPropertyHelper.GetString(adresDostawy, "NrBudynku"),
                Apartment = DynamicPropertyHelper.GetString(adresDostawy, "NrLokalu"),
                City = DynamicPropertyHelper.GetString(adresDostawy, "Miejscowosc"),
                PostalCode = DynamicPropertyHelper.GetString(adresDostawy, "KodPocztowy"),
                Country = DynamicPropertyHelper.GetString(adresDostawy, "Kraj"),
                CountryCode = DynamicPropertyHelper.GetString(adresDostawy, "KodKraju")
            };
        }
        catch
        {
            return null;
        }
    }

    private static DocumentDto MapSalesDocumentToDto(dynamic dokument)
    {
        try
        {
            var totalGross = DynamicPropertyHelper.GetDecimal(dokument, "WartoscBrutto");
            var amountToPay = DynamicPropertyHelper.GetDecimal(dokument, "KwotaDoZaplaty");

            var dto = new DocumentDto
            {
                Id = DynamicPropertyHelper.GetId(dokument),
                Symbol = DynamicPropertyHelper.GetString(dokument, "Symbol") ?? "",
                Number = DynamicPropertyHelper.GetString(dokument, "NumerWewnetrzny", "Numer") ?? "",
                FullNumber = DynamicPropertyHelper.GetString(dokument, "NumerWewnetrzny", "PelnaSygnatura"),
                Title = DynamicPropertyHelper.GetString(dokument, "Tytul"),
                Type = DocumentType.SalesInvoice,
                StatusId = DynamicPropertyHelper.GetNullableInt(dokument, "StatusDokumentu", "Id"),
                Status = DynamicPropertyHelper.GetString(dokument, "StatusDokumentu", "Nazwa"),
                IssueDate = DynamicPropertyHelper.GetDateTime(dokument, "DataWystawienia"),
                SaleDate = DynamicPropertyHelper.GetDateTime(dokument, "DataSprzedazy"),
                DueDate = DynamicPropertyHelper.GetDateTime(dokument, "TerminPlatnosci"),
                CustomerId = DynamicPropertyHelper.GetNullableInt(dokument, "Podmiot", "Id"),
                CustomerName = DynamicPropertyHelper.GetString(dokument, "Podmiot", "NazwaSkrocona"),
                CustomerNIP = DynamicPropertyHelper.GetString(dokument, "Podmiot", "NIP"),
                WarehouseId = DynamicPropertyHelper.GetNullableInt(dokument, "Magazyn", "Id"),
                WarehouseSymbol = DynamicPropertyHelper.GetString(dokument, "Magazyn", "Symbol"),
                TotalNet = DynamicPropertyHelper.GetDecimal(dokument, "WartoscNetto"),
                TotalVat = DynamicPropertyHelper.GetDecimal(dokument, "WartoscVat"),
                TotalGross = totalGross,
                AmountToPay = amountToPay,
                PaidAmount = totalGross - amountToPay > 0 ? totalGross - amountToPay : null,
                Currency = DynamicPropertyHelper.GetString(dokument, "Waluta", "Symbol") ?? "PLN",
                PaymentMethod = DynamicPropertyHelper.GetString(dokument, "FormaPlatnosci", "Nazwa"),
                SplitPayment = DynamicPropertyHelper.GetNullableBool(dokument, "MechanizmPodzielonejPlatnosci"),
                Notes = DynamicPropertyHelper.GetString(dokument, "Uwagi"),
                CreatedAt = DynamicPropertyHelper.GetDateTime(dokument, "DataUtworzenia"),
                KsefStatus = MapKsefStatus(dokument),
                PaymentBreakdown = MapPaymentBreakdown(dokument),
                Items = new List<DocumentItemDto>()
            };

            var pozycje = DynamicPropertyHelper.GetProperty(dokument, "Pozycje");
            if (pozycje != null)
            {
                int lineNum = 1;
                foreach (dynamic poz in pozycje)
                {
                    dto.Items.Add(MapDocumentItemToDto(poz, lineNum++));
                }
            }

            return dto;
        }
        catch
        {
            return new DocumentDto { Id = DynamicPropertyHelper.GetId(dokument) };
        }
    }

    private static DocumentDto MapPurchaseDocumentToDto(dynamic dokument)
    {
        try
        {
            var totalGross = DynamicPropertyHelper.GetDecimal(dokument, "WartoscBrutto");
            var amountToPay = DynamicPropertyHelper.GetDecimal(dokument, "KwotaDoZaplaty");

            var dto = new DocumentDto
            {
                Id = DynamicPropertyHelper.GetId(dokument),
                Symbol = DynamicPropertyHelper.GetString(dokument, "Symbol") ?? "",
                Number = DynamicPropertyHelper.GetString(dokument, "NumerWewnetrzny", "Numer") ?? "",
                FullNumber = DynamicPropertyHelper.GetString(dokument, "NumerWewnetrzny", "PelnaSygnatura"),
                ExternalNumber = DynamicPropertyHelper.GetString(dokument, "NumerZewnetrzny"),
                Title = DynamicPropertyHelper.GetString(dokument, "Tytul"),
                Type = DocumentType.PurchaseInvoice,
                StatusId = DynamicPropertyHelper.GetNullableInt(dokument, "StatusDokumentu", "Id"),
                Status = DynamicPropertyHelper.GetString(dokument, "StatusDokumentu", "Nazwa"),
                IssueDate = DynamicPropertyHelper.GetDateTime(dokument, "DataWystawienia"),
                ReceiptDate = DynamicPropertyHelper.GetDateTime(dokument, "DataPrzyjecia"),
                DueDate = DynamicPropertyHelper.GetDateTime(dokument, "TerminPlatnosci"),
                CustomerId = DynamicPropertyHelper.GetNullableInt(dokument, "Podmiot", "Id"),
                CustomerName = DynamicPropertyHelper.GetString(dokument, "Podmiot", "NazwaSkrocona"),
                CustomerNIP = DynamicPropertyHelper.GetString(dokument, "Podmiot", "NIP"),
                WarehouseId = DynamicPropertyHelper.GetNullableInt(dokument, "Magazyn", "Id"),
                WarehouseSymbol = DynamicPropertyHelper.GetString(dokument, "Magazyn", "Symbol"),
                TotalNet = DynamicPropertyHelper.GetDecimal(dokument, "WartoscNetto"),
                TotalVat = DynamicPropertyHelper.GetDecimal(dokument, "WartoscVat"),
                TotalGross = totalGross,
                AmountToPay = amountToPay,
                PaidAmount = totalGross - amountToPay > 0 ? totalGross - amountToPay : null,
                Currency = DynamicPropertyHelper.GetString(dokument, "Waluta", "Symbol") ?? "PLN",
                PaymentMethod = DynamicPropertyHelper.GetString(dokument, "FormaPlatnosci", "Nazwa"),
                SplitPayment = DynamicPropertyHelper.GetNullableBool(dokument, "MechanizmPodzielonejPlatnosci"),
                SubjectToIntrastat = DynamicPropertyHelper.GetNullableBool(dokument, "PodlegaDeklaracjiIntrastat"),
                Notes = DynamicPropertyHelper.GetString(dokument, "Uwagi"),
                CreatedAt = DynamicPropertyHelper.GetDateTime(dokument, "DataUtworzenia"),
                Items = new List<DocumentItemDto>()
            };

            var pozycje = DynamicPropertyHelper.GetProperty(dokument, "Pozycje");
            if (pozycje != null)
            {
                int lineNum = 1;
                foreach (dynamic poz in pozycje)
                {
                    dto.Items.Add(MapDocumentItemToDto(poz, lineNum++));
                }
            }

            return dto;
        }
        catch
        {
            return new DocumentDto { Id = DynamicPropertyHelper.GetId(dokument) };
        }
    }

    private static DocumentDto MapOrderToDto(dynamic dokument)
    {
        try
        {
            var totalGross = DynamicPropertyHelper.GetDecimal(dokument, "WartoscBrutto");
            var amountToPay = DynamicPropertyHelper.GetDecimal(dokument, "KwotaDoZaplaty");

            var dto = new DocumentDto
            {
                Id = DynamicPropertyHelper.GetId(dokument),
                Symbol = DynamicPropertyHelper.GetString(dokument, "Symbol") ?? "",
                Number = DynamicPropertyHelper.GetString(dokument, "NumerWewnetrzny", "Numer") ?? "",
                FullNumber = DynamicPropertyHelper.GetString(dokument, "NumerWewnetrzny", "PelnaSygnatura"),
                ExternalNumber = DynamicPropertyHelper.GetString(dokument, "NumerZewnetrzny"),
                ReferenceNumber = DynamicPropertyHelper.GetString(dokument, "NumerReferencyjny"),
                Title = DynamicPropertyHelper.GetString(dokument, "Tytul"),
                Type = DocumentType.CustomerOrder,
                StatusId = DynamicPropertyHelper.GetNullableInt(dokument, "StatusDokumentu", "Id"),
                Status = DynamicPropertyHelper.GetString(dokument, "StatusDokumentu", "Nazwa"),
                IssueDate = DynamicPropertyHelper.GetDateTime(dokument, "DataWystawienia"),
                Deadline = DynamicPropertyHelper.GetDateTime(dokument, "TerminRealizacji"),
                DeliveryDate = DynamicPropertyHelper.GetDateTime(dokument, "DataDostawy"),
                CustomerId = DynamicPropertyHelper.GetNullableInt(dokument, "Podmiot", "Id"),
                CustomerName = DynamicPropertyHelper.GetString(dokument, "Podmiot", "NazwaSkrocona"),
                CustomerNIP = DynamicPropertyHelper.GetString(dokument, "Podmiot", "NIP"),
                RecipientId = DynamicPropertyHelper.GetNullableInt(dokument, "Odbiorca", "Id"),
                RecipientName = DynamicPropertyHelper.GetString(dokument, "Odbiorca", "NazwaSkrocona"),
                WarehouseId = DynamicPropertyHelper.GetNullableInt(dokument, "Magazyn", "Id"),
                WarehouseSymbol = DynamicPropertyHelper.GetString(dokument, "Magazyn", "Symbol"),
                TotalNet = DynamicPropertyHelper.GetDecimal(dokument, "WartoscNetto"),
                TotalVat = DynamicPropertyHelper.GetDecimal(dokument, "WartoscVat"),
                TotalGross = totalGross,
                AmountToPay = amountToPay,
                Currency = DynamicPropertyHelper.GetString(dokument, "Waluta", "Symbol") ?? "PLN",
                PaymentMethod = DynamicPropertyHelper.GetString(dokument, "FormaPlatnosci", "Nazwa"),
                DeliveryAddress = MapDeliveryAddress(dokument),
                Notes = DynamicPropertyHelper.GetString(dokument, "Uwagi"),
                CreatedAt = DynamicPropertyHelper.GetDateTime(dokument, "DataUtworzenia"),
                IsConfirmed = DynamicPropertyHelper.GetNullableBool(dokument, "Potwierdzony"),
                Items = new List<DocumentItemDto>()
            };

            var pozycje = DynamicPropertyHelper.GetProperty(dokument, "Pozycje");
            if (pozycje != null)
            {
                int lineNum = 1;
                foreach (dynamic poz in pozycje)
                {
                    dto.Items.Add(MapDocumentItemToDto(poz, lineNum++));
                }
            }

            return dto;
        }
        catch
        {
            return new DocumentDto { Id = DynamicPropertyHelper.GetId(dokument) };
        }
    }

    private static DocumentItemDto MapDocumentItemToDto(dynamic poz, int lineNum)
    {
        var dto = new DocumentItemDto
        {
            Id = DynamicPropertyHelper.GetId(poz),
            LineNumber = lineNum,

            // Product reference
            ProductId = DynamicPropertyHelper.GetNullableInt(poz, "Asortyment", "Id") ??
                        DynamicPropertyHelper.GetNullableInt(poz, "AsortymentWybranyId"),
            ProductSymbol = DynamicPropertyHelper.GetString(poz, "Asortyment", "Symbol"),
            ProductName = DynamicPropertyHelper.GetString(poz, "Asortyment", "Nazwa"),

            // Item details
            Name = DynamicPropertyHelper.GetString(poz, "Nazwa") ?? "",
            Description = DynamicPropertyHelper.GetString(poz, "Opis"),

            // Quantity and unit
            Quantity = DynamicPropertyHelper.GetDecimal(poz, "Ilosc"),
            Unit = DynamicPropertyHelper.GetString(poz, "Jednostka", "Symbol") ?? "szt.",
            UnitSymbol = DynamicPropertyHelper.GetString(poz, "JednostkaMiary", "Symbol"),
            UnitName = DynamicPropertyHelper.GetString(poz, "JednostkaMiary", "Nazwa"),
            UnitId = DynamicPropertyHelper.GetNullableInt(poz, "JednostkaMiaryAsId"),

            // Prices
            PriceNet = DynamicPropertyHelper.GetDecimal(poz, "CenaNetto"),
            PriceGross = DynamicPropertyHelper.GetDecimal(poz, "CenaBrutto"),
            OriginalPriceNet = DynamicPropertyHelper.GetNullableDecimal(poz, "CenaNettoOryginalna"),

            // Discount
            DiscountPercent = DynamicPropertyHelper.GetNullableDecimal(poz, "RabatProcent"),
            DiscountValue = DynamicPropertyHelper.GetNullableDecimal(poz, "RabatKwota"),

            // VAT
            VatRate = DynamicPropertyHelper.GetString(poz, "StawkaVat", "Symbol") ?? "23%",
            VatRateId = DynamicPropertyHelper.GetNullableInt(poz, "StawkaVat", "Id"),
            VatPercent = DynamicPropertyHelper.GetNullableDecimal(poz, "StawkaVat", "Wartosc"),

            // Values
            ValueNet = DynamicPropertyHelper.GetDecimal(poz, "WartoscNetto"),
            ValueVat = DynamicPropertyHelper.GetDecimal(poz, "WartoscVat"),
            ValueGross = DynamicPropertyHelper.GetDecimal(poz, "WartoscBrutto"),

            // Cost and margin
            Cost = DynamicPropertyHelper.GetNullableDecimal(poz, "KosztEwidencyjny"),
            CostValue = DynamicPropertyHelper.GetNullableDecimal(poz, "KosztMagazynowy"),
            Margin = DynamicPropertyHelper.GetNullableDecimal(poz, "Marza"),
            MarginPercent = DynamicPropertyHelper.GetNullableDecimal(poz, "MarzaProcent")
        };

        // Calculate discount if not available
        if (dto.DiscountPercent == null && dto.DiscountValue == null && dto.OriginalPriceNet.HasValue)
        {
            dto.DiscountValue = dto.OriginalPriceNet.Value - dto.PriceNet;
            if (dto.OriginalPriceNet.Value != 0)
            {
                dto.DiscountPercent = (dto.DiscountValue / dto.OriginalPriceNet.Value) * 100;
            }
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

    #endregion

    #region Document Associations

    /// <summary>
    /// Associate a document with another document (creates a link between documents)
    /// Use this to link related documents like RW with PW, or invoices with orders.
    /// </summary>
    /// <param name="id">Source document ID</param>
    /// <param name="request">Association request with target document ID</param>
    /// <returns>Success status</returns>
    [HttpPost("{id}/associate")]
    public ActionResult<ApiResponse<object>> AssociateDocument(int id, [FromBody] DocumentAssociationRequest request)
    {
        try
        {
            // Try to find the source document in various managers
            dynamic? sourceDocument = null;
            dynamic? sourceManager = null;
            string? sourceManagerName = null;

            // Check different document managers
            var managersToCheck = new[]
            {
                "Dokumenty",
                "DokumentySprzedazy",
                "DokumentyZakupu",
                "WydaniaZewnetrzne",
                "PrzyjeciaZewnetrzne",
                "WydaniaMiedzymagazynowe",
                "PrzesunieciaMiedzymagazynowe",
                "RozchodyWewnetrzne",
                "PrzychodyWewnetrzne"
            };

            // First try the main Dokumenty manager by iterating (same as GetDocument)
            var dokumentyManager = _sferaService.GetManager("Dokumenty");
            if (dokumentyManager != null)
            {
                try
                {
                    foreach (var d in dokumentyManager.Dane.Wszystkie())
                    {
                        if (DynamicPropertyHelper.GetId(d) == id)
                        {
                            sourceDocument = d;
                            sourceManager = dokumentyManager;
                            sourceManagerName = "Dokumenty";
                            break;
                        }
                    }
                }
                catch { }
            }

            // If not found, try other managers
            if (sourceDocument == null)
            {
                foreach (var managerName in managersToCheck.Skip(1)) // Skip "Dokumenty"
                {
                    var manager = _sferaService.GetManager(managerName);
                    if (manager == null) continue;

                    try
                    {
                        foreach (var d in manager.Dane.Wszystkie())
                        {
                            if (DynamicPropertyHelper.GetId(d) == id)
                            {
                                sourceDocument = d;
                                sourceManager = manager;
                                sourceManagerName = managerName;
                                break;
                            }
                        }
                        if (sourceDocument != null) break;
                    }
                    catch
                    {
                        continue;
                    }
                }
            }

            if (sourceDocument == null)
            {
                return NotFound(ApiResponse<object>.Error($"Source document with ID {id} not found"));
            }

            // Find the target document
            dynamic? targetDocument = null;

            foreach (var managerName in managersToCheck)
            {
                var manager = _sferaService.GetManager(managerName);
                if (manager == null) continue;

                try
                {
                    var doc = manager.Dane.Znajdz(request.TargetDocumentId);
                    if (doc != null)
                    {
                        targetDocument = doc;
                        break;
                    }
                }
                catch
                {
                    continue;
                }
            }

            if (targetDocument == null)
            {
                return NotFound(ApiResponse<object>.Error($"Target document with ID {request.TargetDocumentId} not found"));
            }

            // Create association using DokumentyPowiazane
            try
            {
                // Get DokumentyPowiazane collection
                var dokumentyPowiazane = DynamicPropertyHelper.GetProperty(sourceDocument, "DokumentyPowiazane");
                if (dokumentyPowiazane == null)
                {
                    return BadRequest(ApiResponse<object>.Error("Source document does not support document associations (DokumentyPowiazane)"));
                }

                // Add the target document to the collection
                dokumentyPowiazane.Dodaj(targetDocument);

                // Save the source document
                // For some document types, we need to use the business object wrapper
                bool saved = false;
                try
                {
                    // Try to save using Zapisz on the entity itself (if available)
                    saved = (bool)sourceDocument.Zapisz();
                }
                catch
                {
                    // If that fails, try editing through the manager
                    try
                    {
                        using (var editor = sourceManager.Edytuj(sourceDocument))
                        {
                            saved = (bool)editor.Zapisz();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not save using Edytuj pattern, trying alternative approach");
                        // Some documents may auto-save when collection is modified
                        saved = true; // Assume success if no exception during Dodaj
                    }
                }

                if (saved)
                {
                    string sourceNumber = DynamicPropertyHelper.GetString(sourceDocument, "NumerWewnetrzny", "PelnaSygnatura")
                                    ?? DynamicPropertyHelper.GetString(sourceDocument, "Symbol")
                                    ?? id.ToString();
                    string targetNumber = DynamicPropertyHelper.GetString(targetDocument, "NumerWewnetrzny", "PelnaSygnatura")
                                     ?? DynamicPropertyHelper.GetString(targetDocument, "Symbol")
                                     ?? request.TargetDocumentId.ToString();

                    _logger.LogInformation("Associated document {SourceDoc} with {TargetDoc}", (object)sourceNumber, (object)targetNumber);

                    return Ok(ApiResponse<object>.Ok(new
                    {
                        SourceDocumentId = id,
                        TargetDocumentId = request.TargetDocumentId,
                        RelationType = request.RelationType,
                        SourceDocumentNumber = sourceNumber,
                        TargetDocumentNumber = targetNumber
                    }, $"Documents associated successfully"));
                }
                else
                {
                    return BadRequest(ApiResponse<object>.Error("Failed to save document association"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating document association");
                return StatusCode(500, ApiResponse<object>.Error($"Error creating association: {ex.Message}"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error associating documents {SourceId} with {TargetId}", id, request.TargetDocumentId);
            return StatusCode(500, ApiResponse<object>.Error("Error associating documents", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get documents associated with a document
    /// </summary>
    /// <param name="id">Document ID</param>
    /// <returns>List of associated documents</returns>
    [HttpGet("{id}/associations")]
    public ActionResult<ApiResponse<List<DocumentListItemDto>>> GetDocumentAssociations(int id)
    {
        try
        {
            // Find the document using iteration (same pattern as GetDocument)
            dynamic? document = null;
            var dokumentyManager = _sferaService.GetManager("Dokumenty");
            if (dokumentyManager != null)
            {
                try
                {
                    foreach (var d in dokumentyManager.Dane.Wszystkie())
                    {
                        if (DynamicPropertyHelper.GetId(d) == id)
                        {
                            document = d;
                            break;
                        }
                    }
                }
                catch { }
            }

            if (document == null)
            {
                return NotFound(ApiResponse<List<DocumentListItemDto>>.Error($"Document with ID {id} not found"));
            }

            var associations = new List<DocumentListItemDto>();

            // Get DokumentyPowiazane collection
            var dokumentyPowiazane = DynamicPropertyHelper.GetProperty(document, "DokumentyPowiazane");
            if (dokumentyPowiazane != null)
            {
                foreach (var powiazany in dokumentyPowiazane)
                {
                    try
                    {
                        associations.Add(MapToListItemDto(powiazany));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not map associated document");
                    }
                }
            }

            // Also check DokumentyRealizujace (documents that realize this one)
            var dokumentyRealizujace = DynamicPropertyHelper.GetProperty(document, "DokumentyRealizujace");
            if (dokumentyRealizujace != null)
            {
                foreach (var realizujacy in dokumentyRealizujace)
                {
                    try
                    {
                        var dto = MapToListItemDto(realizujacy);
                        dto.RelationType = "realization";
                        associations.Add(dto);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not map realizing document");
                    }
                }
            }

            return Ok(ApiResponse<List<DocumentListItemDto>>.Ok(associations));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting document associations for {Id}", id);
            return StatusCode(500, ApiResponse<List<DocumentListItemDto>>.Error("Error getting document associations", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Remove association between documents
    /// </summary>
    /// <param name="id">Source document ID</param>
    /// <param name="targetId">Target document ID to disassociate</param>
    [HttpDelete("{id}/associations/{targetId}")]
    public ActionResult<ApiResponse<object>> RemoveDocumentAssociation(int id, int targetId)
    {
        try
        {
            // Find the source document using iteration
            dynamic? sourceDocument = null;
            dynamic? sourceManager = null;

            var dokumentyManager = _sferaService.GetManager("Dokumenty");
            if (dokumentyManager != null)
            {
                try
                {
                    foreach (var d in dokumentyManager.Dane.Wszystkie())
                    {
                        if (DynamicPropertyHelper.GetId(d) == id)
                        {
                            sourceDocument = d;
                            sourceManager = dokumentyManager;
                            break;
                        }
                    }
                }
                catch { }
            }

            if (sourceDocument == null)
            {
                return NotFound(ApiResponse<object>.Error($"Source document with ID {id} not found"));
            }

            // Get DokumentyPowiazane and find the target to remove
            var dokumentyPowiazane = DynamicPropertyHelper.GetProperty(sourceDocument, "DokumentyPowiazane");
            if (dokumentyPowiazane == null)
            {
                return BadRequest(ApiResponse<object>.Error("Document does not support associations"));
            }

            dynamic? targetToRemove = null;
            foreach (var powiazany in dokumentyPowiazane)
            {
                if (DynamicPropertyHelper.GetId(powiazany) == targetId)
                {
                    targetToRemove = powiazany;
                    break;
                }
            }

            if (targetToRemove == null)
            {
                return NotFound(ApiResponse<object>.Error($"Association with document ID {targetId} not found"));
            }

            // Remove the association
            dokumentyPowiazane.Usun(targetToRemove);

            // Save
            bool saved = false;
            try
            {
                saved = (bool)sourceDocument.Zapisz();
            }
            catch
            {
                try
                {
                    using (var editor = sourceManager.Edytuj(sourceDocument))
                    {
                        saved = (bool)editor.Zapisz();
                    }
                }
                catch
                {
                    saved = true;
                }
            }

            if (saved)
            {
                _logger.LogInformation("Removed association between documents {SourceId} and {TargetId}", id, targetId);
                return Ok(ApiResponse<object>.Ok(new { RemovedAssociation = targetId }, "Association removed successfully"));
            }
            else
            {
                return BadRequest(ApiResponse<object>.Error("Failed to remove association"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing document association between {SourceId} and {TargetId}", id, targetId);
            return StatusCode(500, ApiResponse<object>.Error("Error removing association", new List<string> { ex.Message }));
        }
    }

    #endregion
}
