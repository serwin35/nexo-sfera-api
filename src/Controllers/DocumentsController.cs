Warning: truncated output (original token count: 89820)
Total output lines: 7351

Paperclip: GitHub capability_missing; continuing without managed credentials.
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Filters;
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
    /// Determines if a document type should NOT have import remarks added.
    /// Only standard invoices and receipts should skip "Import ILUO" remarks.
    /// </summary>
    private bool ShouldSkipImportRemarks(DocumentType documentType)
    {
        return documentType == DocumentType.SalesInvoice ||
               documentType == DocumentType.PurchaseInvoice ||
               documentType == DocumentType.Receipt ||
               documentType == DocumentType.SalesInvoiceCorrection ||
               documentType == DocumentType.PurchaseInvoiceCorrection;
    }

    /// <summary>
    /// Determines if import remarks should be skipped for specialized invoice and receipt types.
    /// Used for document types like advance invoices, VAT margin invoices, and receipt returns
    /// that don't have a direct DocumentType enum value but should still skip import remarks.
    /// </summary>
    private bool ShouldSkipImportRemarksForContext(string context)
    {
        // Skip remarks only for specialized invoice and receipt types
        return context.Contains("Invoice", StringComparison.OrdinalIgnoreCase) ||
               context.Contains("Receipt", StringComparison.OrdinalIgnoreCase) ||
               context.Contains("Paragon", StringComparison.OrdinalIgnoreCase) ||
               context.Contains("Faktura", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Explore properties of a single Document entity (debug endpoint)
    /// </summary>
    [HttpGet("debug/properties/{id}")]
    [DevelopmentOnly]
    public async Task<ActionResult<object>> GetDocumentProperties(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var dokumentyManager = _sferaService.GetManager("Dokumenty");
                if (dokumentyManager == null)
                {
                    return (object)new { ManagerMissing = true, NotFound = false, Id = id };
                }

                dynamic? dokument = DynamicPropertyHelper.FindById(dokumentyManager, id);

                if (dokument == null)
                {
                    return (object)new { ManagerMissing = false, NotFound = true, Id = id };
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
                        properties[prop.Name] = new { Error = "Diagnostic operation failed." };
                    }
                }

                return (object)new
                {
                    ManagerMissing = false,
                    NotFound = false,
                    Id = id,
                    EntityType = dokumentType.FullName,
                    PropertyCount = properties.Count,
                    Properties = properties.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value)
                };
            });

            dynamic r = result!;
            if (r.ManagerMissing == true)
                return StatusCode(500, new { Error = "Failed to get Dokumenty manager" });
            if (r.NotFound == true)
                return NotFound(new { Error = $"Document {id} not found" });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError("Document diagnostic endpoint failed with exception type {ExceptionType}", ex.GetType().Name);
            return StatusCode(500, new { Error = "Diagnostic operation failed." });
        }
    }

    /// <summary>
    /// Debug: Discover all properties and their values on line items (Pozycje) of a document.
    /// Searches across all document managers (FS, FZ, ZK, ZD, WZ, PZ, Oferty, etc.)
    /// Use this to find the correct property names for item mapping.
    /// </summary>
    [HttpGet("debug/item-properties/{id}")]
    [DevelopmentOnly]
    public async Task<ActionResult<object>> GetDocumentItemProperties(int id, [FromQuery] string? manager = null)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                // Search across all manager types to find the document
                var managerNames = !string.IsNullOrEmpty(manager)
                    ? new[] { manager }
                    : new[] { "DokumentySprzedazy", "DokumentyZakupu", "ZamowieniaOdKlientow",
                              "ZamowieniaDoDostawcow", "Oferty", "WydaniaZewnetrzne",
                              "PrzyjeciaZewnetrzne", "RozchodyWewnetrzne", "PrzychodyWewnetrzne",
                              "WydaniaMiedzymagazynowe" };

                dynamic? dokument = null;
                string? foundInManager = null;

                foreach (var mgrName in managerNames)
                {
                    try
                    {
                        var mgr = _sferaService.GetManager(mgrName);
                        if (mgr == null) continue;
                        var found = DynamicPropertyHelper.FindById((object)mgr, id);
                        if (found != null)
                        {
                            dokument = found;
                            foundInManager = mgrName;
                            break;
                        }
                    }
                    catch { }
                }

                if (dokument == null)
                    return (object)new { Error = $"Document {id} not found in any manager", NotFound = true };

                // Get Pozycje collection
                var pozycje = DynamicPropertyHelper.GetCollection((object)dokument, "Pozycje");
                if (pozycje.Count == 0)
                    return new { Id = id, Manager = foundInManager, ItemCount = 0, Message = "No line items found", NotFound = false };

                // Inspect first item's properties with values
                var firstItem = pozycje[0];
                Type itemType = firstItem.GetType();
                var props = new Dictionary<string, object>();

                // Check direct properties
                foreach (var prop in itemType.GetProperties())
                {
                    try
                    {
                        var value = prop.GetValue(firstItem);
                        var valueType = value?.GetType().Name ?? "null";

                        if (value == null)
                        {
                            props[prop.Name] = new { Type = prop.PropertyType.Name, Value = (string?)null, Source = "direct" };
                        }
                        else if (value.GetType().Name.Contains("Collection"))
                        {
                            try
                            {
                                int count = 0;
                                foreach (var _ in (dynamic)value) count++;
                                props[prop.Name] = new { Type = valueType, Count = count, Source = "direct" };
                            }
                            catch
                            {
                                props[prop.Name] = new { Type = valueType, Value = "Collection (error)", Source = "direct" };
                            }
                        }
                        else if (prop.PropertyType.IsPrimitive || prop.PropertyType == typeof(string)
                                 || prop.PropertyType == typeof(DateTime) || prop.PropertyType == typeof(DateTime?)
                                 || prop.PropertyType == typeof(decimal) || prop.PropertyType == typeof(decimal?)
                                 || prop.PropertyType == typeof(Guid) || prop.PropertyType.IsEnum)
                        {
                            props[prop.Name] = new { Type = valueType, Value = value.ToString(), Source = "direct" };
                        }
                        else
                        {
                            // For complex objects, try to extract useful sub-properties
                            var subProps = new Dictionary<string, string?>();
                            try
                            {
                                var subType = value.GetType();
                                foreach (var subProp in subType.GetProperties())
                                {
                                    if (subProp.PropertyType.IsPrimitive || subProp.PropertyType == typeof(string)
                                        || subProp.PropertyType == typeof(decimal) || subProp.PropertyType == typeof(decimal?)
                                        || subProp.PropertyType == typeof(DateTime) || subProp.PropertyType == typeof(DateTime?)
                                        || subProp.PropertyType.IsEnum || subProp.PropertyType == typeof(Guid))
                                    {
                                        try
                                        {
                                            var subVal = subProp.GetValue(value);
                                            subProps[subProp.Name] = subVal?.ToString();
                                        }
                                        catch { subProps[subProp.Name] = "(error)"; }
                                    }
                                }
                            }
                            catch { }

                            props[prop.Name] = new { Type = valueType, SubProperties = subProps, Source = "direct" };
                        }
                    }
                    catch (Exception ex)
                    {
                        props[prop.Name] = new { Error = "Diagnostic operation failed.", Source = "direct" };
                    }
                }

                // Also check .Dane sub-object properties
                var daneProps = new Dictionary<string, object>();
                try
                {
                    var daneObj = DynamicPropertyHelper.GetProperty(firstItem, "Dane");
                    if (daneObj != null)
                    {
                        Type daneType = ((object)daneObj).GetType();
                        foreach (var prop in daneType.GetProperties())
                        {
                            try
                            {
                                var value = prop.GetValue(daneObj);
                                if (value != null && (prop.PropertyType.IsPrimitive || prop.PropertyType == typeof(string)
                                    || prop.PropertyType == typeof(decimal) || prop.PropertyType == typeof(decimal?)
                                    || prop.PropertyType == typeof(DateTime) || prop.PropertyType == typeof(DateTime?)
                                    || prop.PropertyType.IsEnum))
                                {
                                    daneProps[prop.Name] = new { Type = value.GetType().Name, Value = value.ToString() };
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }

                return (object)new
                {
                    DocumentId = id,
                    Manager = foundInManager,
                    ItemCount = pozycje.Count,
                    FirstItemType = itemType.FullName,
                    PropertyCount = props.Count,
                    Properties = props.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value),
                    DaneProperties = daneProps.Count > 0 ? daneProps.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value) : null,
                    NotFound = false
                };
            });

            if (result is { } r && DynamicPropertyHelper.GetBool(r, "NotFound"))
                return NotFound(result);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError("Document diagnostic endpoint failed with exception type {ExceptionType}", ex.GetType().Name);
            return StatusCode(500, new { Error = "Diagnostic operation failed." });
        }
    }

    /// <summary>
    /// Get documents with filtering (lightweight list view)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResponse<DocumentListItemDto>>> GetDocuments([FromQuery] DocumentQueryRequest query)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var dokumentyManager = _sferaService.GetManager("Dokumenty");
                if (dokumentyManager == null)
                {
                    return (PagedResponse<DocumentListItemDto>?)null;
                }

                var sfera = _sferaService.GetSfera();

                // Delta sync: only the changed documents are loaded (one SQL query over the header and settlement change
                // times) instead of the whole table; the filters below run on them unchanged. The dynamic members
                // DataModyfikacji/Anulowany do not exist.
                var allDokumenty = query.ModifiedSince.HasValue
                    ? DocumentReader.ChangedDocuments(sfera, query.ModifiedSince.Value).Cast<object>().ToList()
                    : DynamicPropertyHelper.SafeGetAll((object)dokumentyManager);

                if (query.IsCanceled.HasValue)
                {
                    var canceled = DocumentReader.CanceledIds(sfera);
                    allDokumenty = allDokumenty.Where(d => canceled.Contains(DynamicPropertyHelper.GetId(d)) == query.IsCanceled.Value).ToList();
                }

                // Apply filters
                allDokumenty = ApplyDocumentFilters(allDokumenty, query);

                var totalCount = allDokumenty.Count;

                // Sort and paginate
                var sortBy = query.SortBy?.ToLowerInvariant();
                var sortedItems = sortBy is "modified_asc" or "modified_desc"
                    ? SortByModification(allDokumenty, ModificationStamps(sfera, allDokumenty, query.ModifiedSince.HasValue), sortBy == "modified_desc")
                    : ApplyDocumentSorting(allDokumenty, query.SortBy);
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

                try
                {
                    DocumentReader.ApplyStamps(mappedItems, DocumentReader.Stamps(sfera, mappedItems.Select(i => i.Id).ToList()));
                }
                catch (Exception stampEx)
                {
                    // Stamps are additive; leave them null rather than failing the list.
                    _logger.LogWarning(stampEx, "Could not read document header stamps");
                }

                return (PagedResponse<DocumentListItemDto>?)new PagedResponse<DocumentListItemDto>
                {
                    Data = mappedItems,
                    Page = query.Page,
                    PageSize = query.PageSize,
                    TotalCount = totalCount
                };
            });

            if (result == null)
            {
                return StatusCode(500, ApiResponse<object>.Error("Failed to get Dokumenty manager"));
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting documents");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving documents", new List<string> { ex.Message }));
        }
    }

    private static List<object> ApplyDocumentFilters(List<object> documents, DocumentQueryRequest query)
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
                var data = GetDocumentDueDate(d);
                return data.HasValue && data.Value >= query.DueDateFrom.Value;
            }).ToList();
        }

        if (query.DueDateTo.HasValue)
        {
            documents = documents.Where(d =>
            {
                var data = GetDocumentDueDate(d);
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
                var dueDate = GetDocumentDueDate(d);
                return amountToPay > 0 && dueDate.HasValue && dueDate.Value < DateTime.Today;
            }).ToList();
        }

        // Canceled filter: applied in GetDocuments from StatusDokumentu.Uniewazniony (one SQL query).

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

    /// <summary>Largest delta result whose stamps are read by id; a bigger one reads the stamps of every document.</summary>
    private const int MaxStampIdsPerQuery = 2000;

    /// <summary>
    /// Stamps for sorting: of the listed documents only when the list is a delta (usually a handful), otherwise of every
    /// document (one projection over the id and header columns).
    /// </summary>
    private static IReadOnlyDictionary<int, DocumentReader.Stamp> ModificationStamps(InsERT.Moria.Sfera.Uchwyt sfera, List<object> documents, bool isDelta) =>
        isDelta && documents.Count <= MaxStampIdsPerQuery
            ? DocumentReader.Stamps(sfera, documents.Select(d => (int)DynamicPropertyHelper.GetId(d)).ToList())
            : DocumentReader.AllStamps(sfera);

    /// <summary>Orders by the latest of the document and settlement change times (documents without stamps last).</summary>
    private static IEnumerable<object> SortByModification(List<object> documents, IReadOnlyDictionary<int, DocumentReader.Stamp> stamps, bool descending)
    {
        DateTimeOffset Key(object d)
        {
            if (!stamps.TryGetValue(DynamicPropertyHelper.GetId(d), out var s)) return DateTimeOffset.MinValue;
            var document = s.ModifiedAt ?? DateTimeOffset.MinValue;
            var settlement = s.SettlementModifiedAt ?? DateTimeOffset.MinValue;
            return document > settlement ? document : settlement;
        }

        return descending
            ? documents.OrderByDescending(Key).ThenByDescending(d => (int)DynamicPropertyHelper.GetId(d))
            : documents.OrderBy(Key).ThenBy(d => (int)DynamicPropertyHelper.GetId(d));
    }

    private static IEnumerable<object> ApplyDocumentSorting(List<object> documents, string sortBy)
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
    /// Settlement state of many documents: paid, remaining, due date and overdue instalments (Dokument.Rozrachunek)
    /// </summary>
    /// <remarks>
    /// One SQL query for the page and one count; every filter runs in SQL. Read only.
    /// Example: <c>GET /api/documents/settlements?symbols=FS,KFS&amp;type=Receivable&amp;overdue=true&amp;sortBy=due_date</c>
    /// </remarks>
    [HttpGet("settlements")]
    public async Task<ActionResult<PagedResponse<DocumentSettlementStateDto>>> GetDocumentSettlements([FromQuery] DocumentSettlementQueryRequest query)
    {
        var errors = DocumentSettlementReader.Validate(query, out var ids);
        if (errors.Count > 0)
        {
            return BadRequest(ApiResponse<object>.Error("Invalid settlement query", errors));
        }

        try
        {
            var response = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var (totalCount, items) = DocumentSettlementReader.Read(_sferaService.GetSfera(), query, ids, DateTime.Today);
                return new PagedResponse<DocumentSettlementStateDto>
                {
                    Data = items,
                    Page = query.Page,
                    PageSize = query.PageSize,
                    TotalCount = totalCount
                };
            });

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading document settlements");
            return StatusCode(500, ApiResponse<object>.Error("Error reading document settlements", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get document by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<DocumentDto>>> GetDocument(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync<(bool ManagerMissing, DocumentDto? Dto)>(() =>
            {
                var dokumentyManager = _sferaService.GetManager("Dokumenty");
                if (dokumentyManager == null)
                    return (true, null);

                dynamic? dokument = DynamicPropertyHelper.FindById(dokumentyManager, id);

                if (dokument == null)
                    return (false, null);

                return (false, (DocumentDto?)MapDocumentToDto(dokument));
            });

            if (result.ManagerMissing)
                return StatusCode(500, ApiResponse<DocumentDto>.Error("Failed to get Dokumenty manager"));

            if (result.Dto == null)
                return NotFound(ApiResponse<DocumentDto>.Error($"Document with ID {id} not found"));

            return Ok(ApiResponse<DocumentDto>.Ok(result.Dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting document {Id}", id);
            return StatusCode(500, ApiResponse<DocumentDto>.Error("Error retrieving document", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Update an existing document
    /// </summary>
    /// <remarks>
    /// Allows updating editable fields on a document such as:
    /// - Wystawil (person who issued the document - text)
    /// - WystawilaOsobaId (employee ID reference)
    /// - Odebral (person who received - for warehouse documents)
    /// - Notes (remarks/uwagi)
    /// - ExternalNumber (external document number)
    /// - DueDate (payment due date)
    /// - SaleDate
    /// - StatusId
    /// - Description
    ///
    /// Note: Some fields may not be editable depending on document type and status.
    /// </remarks>
    [HttpPatch("{id}")]
    [ProducesResponseType(typeof(ApiResponse<DocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<DocumentDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<DocumentDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<DocumentDto>>> UpdateDocument(int id, [FromBody] UpdateDocumentRequest request)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync<(bool Success, DocumentDto? Data, string Message, List<string> Errors)>(() =>
            {
                var dokumentyManager = _sferaService.GetManager("Dokumenty");
                if (dokumentyManager == null)
                {
                    return (false, null, "Failed to get Dokumenty manager", new List<string>());
                }

                // Find the document
                dynamic? encja = DynamicPropertyHelper.FindById(dokumentyManager, id);

                if (encja == null)
                {
                    return (false, null, $"Document with ID {id} not found", new List<string>());
                }

                // Open for editing
                dynamic dokument;
                try
                {
                    dokument = dokumentyManager.Edytuj(encja);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to open document {Id} for editing", id);
                    return (false, null, $"Cannot edit document: {ex.Message}", new List<string> { ex.Message });
                }

                var updateErrors = new List<string>();

                // Update Wystawil (text field - person who issued)
                if (request.Wystawil != null)
                {
                    try
                    {
                        dokument.Dokument.Wystawil = request.Wystawil;
                        _logger.LogInformation("Updated Wystawil to: {Value}", request.Wystawil);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to set Wystawil");
                        updateErrors.Add($"Failed to update Wystawil: {ex.Message}");
                    }
                }

                // Update WystawilaOsobaId (employee reference)
                if (request.WystawilaOsobaId.HasValue)
                {
                    try
                    {
                        // First, get the Pracownik (employee) by ID
                        var pracownicyManager = _sferaService.GetManager("Pracownicy");
                        if (pracownicyManager != null)
                        {
                            dynamic? pracownik = DynamicPropertyHelper.FindById(pracownicyManager, request.WystawilaOsobaId.Value);

                            if (pracownik != null)
                            {
                                dokument.Dokument.WystawilaOsoba = pracownik;
                                _logger.LogInformation("Updated WystawilaOsoba to employee ID: {Id}", request.WystawilaOsobaId.Value);
                            }
                            else
                            {
                                updateErrors.Add($"Employee with ID {request.WystawilaOsobaId.Value} not found");
                            }
                        }
                        else
                        {
                            updateErrors.Add("Cannot access Pracownicy manager");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to set WystawilaOsoba");
                        updateErrors.Add($"Failed to update WystawilaOsoba: {ex.Message}");
                    }
                }

                // Update Odebral (person who received)
                if (request.Odebral != null)
                {
                    try
                    {
                        if (!DynamicPropertyHelper.TrySetProperty(dokument.Dokument, "Odebral", request.Odebral))
                        {
                            DynamicPropertyHelper.TrySetProperty(dokument.Dane, "Odebral", request.Odebral);
                        }
                        _logger.LogInformation("Updated Odebral to: {Value}", request.Odebral);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to set Odebral");
                        updateErrors.Add($"Failed to update Odebral: {ex.Message}");
                    }
                }

                // Update Notes/Uwagi
                if (request.Notes != null)
                {
                    try
                    {
                        dokument.Dane.Uwagi = request.Notes;
                        _logger.LogInformation("Updated Notes");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to set Notes");
                        updateErrors.Add($"Failed to update Notes: {ex.Message}");
                    }
                }

                // Update ExternalNumber
                if (request.ExternalNumber != null)
                {
                    try
                    {
                        if (!DynamicPropertyHelper.TrySetProperty(dokument.Dane, "NumerZewnetrzny", request.ExternalNumber))
                        {
                            DynamicPropertyHelper.TrySetProperty(dokument.Dane, "NumerOryginalny", request.ExternalNumber);
                        }
                        _logger.LogInformation("Upda…69820 tokens truncated…bol") ?? "PLN",
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

            ApplyPaymentInfo(dto, dokument);
            return dto;
        }
        catch
        {
            return new DocumentDto { Id = DynamicPropertyHelper.GetId(dokument) };
        }
    }

    private static DocumentItemDto MapDocumentItemToDto(dynamic poz, int lineNum)
    {
        // Get the effective data object (.Dane if available, otherwise the object itself)
        var dane = DynamicPropertyHelper.GetDane(poz);

        var dto = new DocumentItemDto
        {
            Id = DynamicPropertyHelper.GetId(poz),
            LineNumber = lineNum,

            // AsortymentAktualny = base product (type Asortyment, matches Products API)
            // AsortymentWybrany = historical snapshot (type AsortymentHistoria, different ID!)
            ProductId = DynamicPropertyHelper.GetNullableInt(dane, "AsortymentAktualny", "Id") ??
                        DynamicPropertyHelper.GetNullableInt(poz, "AsortymentAktualny", "Id"),
            ProductSymbol = DynamicPropertyHelper.GetString(dane, "AsortymentAktualny", "Symbol")
                         ?? DynamicPropertyHelper.GetString(dane, "AsortymentWybrany", "Symbol"),
            ProductName = DynamicPropertyHelper.GetString(dane, "AsortymentAktualny", "Nazwa")
                       ?? DynamicPropertyHelper.GetString(dane, "AsortymentWybrany", "Nazwa"),

            // Item details
            Name = DynamicPropertyHelper.GetString(dane, "Nazwa")
                ?? DynamicPropertyHelper.GetString(poz, "Nazwa") ?? "",
            Description = DynamicPropertyHelper.GetString(dane, "Opis")
                       ?? DynamicPropertyHelper.GetString(poz, "Opis"),

            // Quantity and unit
            Quantity = DynamicPropertyHelper.GetDecimalFirstOf(dane, "Ilosc", "IloscJednostek"),
            // SDK: PozycjaDokumentu.JednostkaMiaryAs (JednostkaMiaryAsortymentu) -> JednostkaMiary (dictionary unit)
            Unit = DynamicPropertyHelper.GetLineUnitSymbol(poz) ?? "szt.",
            UnitSymbol = DynamicPropertyHelper.GetLineUnitSymbol(poz),
            UnitName = DynamicPropertyHelper.GetString(DynamicPropertyHelper.GetProperty(dane, "JednostkaMiaryAs"), "JednostkaMiary", "Nazwa"),
            UnitId = DynamicPropertyHelper.GetNullableInt(dane, "JednostkaMiaryAsId"),
            QuantityInBaseUnit = DynamicPropertyHelper.GetNullableDecimal(dane, "IloscWJednostceBazowej"),
            BaseUnit = DynamicPropertyHelper.GetProductBaseUnitSymbol(DynamicPropertyHelper.GetProperty(dane, "AsortymentAktualny")),

            // Prices - Cena sub-object has NettoPoRabacie/BruttoPoRabacie
            PriceNet = DynamicPropertyHelper.GetDecimal(DynamicPropertyHelper.GetProperty(dane, "Cena"), "NettoPoRabacie") > 0
                     ? DynamicPropertyHelper.GetDecimal(DynamicPropertyHelper.GetProperty(dane, "Cena"), "NettoPoRabacie")
                     : DynamicPropertyHelper.GetDecimalFirstOf(dane, "CenaNetto", "CenaJednostkowaNetto", "Cena"),
            PriceGross = DynamicPropertyHelper.GetDecimal(DynamicPropertyHelper.GetProperty(dane, "Cena"), "BruttoPoRabacie") > 0
                       ? DynamicPropertyHelper.GetDecimal(DynamicPropertyHelper.GetProperty(dane, "Cena"), "BruttoPoRabacie")
                       : DynamicPropertyHelper.GetDecimalFirstOf(dane, "CenaBrutto", "CenaJednostkowaBrutto"),
            OriginalPriceNet = DynamicPropertyHelper.GetNullableDecimal(DynamicPropertyHelper.GetProperty(dane, "Cena"), "NettoPrzedRabatem")
                            ?? DynamicPropertyHelper.GetNullableDecimal(dane, "CenaNettoOryginalna"),

            // Discount
            // SDK: discount lives in the Cena sub-object (Cena.RabatProcent / Cena.RabatWartosc)
            DiscountPercent = DynamicPropertyHelper.GetNullableDecimal(DynamicPropertyHelper.GetProperty(dane, "Cena"), "RabatProcent")
                           ?? DynamicPropertyHelper.GetNullableDecimal(dane, "RabatProcent"),
            DiscountValue = DynamicPropertyHelper.GetNullableDecimal(DynamicPropertyHelper.GetProperty(dane, "Cena"), "RabatWartosc")
                         ?? DynamicPropertyHelper.GetNullableDecimal(dane, "RabatWartosc"),

            // VAT
            VatRate = DynamicPropertyHelper.GetString(dane, "StawkaVat", "Symbol")
                   ?? DynamicPropertyHelper.GetString(poz, "StawkaVat", "Symbol") ?? "23%",
            VatRateId = DynamicPropertyHelper.GetNullableInt(dane, "StawkaVat", "Id")
                     ?? DynamicPropertyHelper.GetNullableInt(poz, "StawkaVat", "Id"),
            VatPercent = DynamicPropertyHelper.GetNullableDecimal(dane, "StawkaVat", "Wartosc")
                      ?? DynamicPropertyHelper.GetNullableDecimal(dane, "StawkaVatProcent"),

            // Values - Wartosc sub-object has NettoPoRabacie/BruttoPoRabacie/VatPoRabacie
            ValueNet = DynamicPropertyHelper.GetDecimal(DynamicPropertyHelper.GetProperty(dane, "Wartosc"), "NettoPoRabacie") > 0
                     ? DynamicPropertyHelper.GetDecimal(DynamicPropertyHelper.GetProperty(dane, "Wartosc"), "NettoPoRabacie")
                     : DynamicPropertyHelper.GetDecimalFirstOf(dane, "WartoscNetto", "Wartosc"),
            ValueVat = DynamicPropertyHelper.GetDecimal(DynamicPropertyHelper.GetProperty(dane, "Wartosc"), "VatPoRabacie") > 0
                     ? DynamicPropertyHelper.GetDecimal(DynamicPropertyHelper.GetProperty(dane, "Wartosc"), "VatPoRabacie")
                     : DynamicPropertyHelper.GetDecimalFirstOf(dane, "WartoscVat"),
            ValueGross = DynamicPropertyHelper.GetDecimal(DynamicPropertyHelper.GetProperty(dane, "Wartosc"), "BruttoPoRabacie") > 0
                       ? DynamicPropertyHelper.GetDecimal(DynamicPropertyHelper.GetProperty(dane, "Wartosc"), "BruttoPoRabacie")
                       : DynamicPropertyHelper.GetDecimalFirstOf(dane, "WartoscBrutto"),

            // Cost and margin
            Cost = DynamicPropertyHelper.GetNullableDecimal(dane, "KosztEwidencyjny")
                ?? DynamicPropertyHelper.GetNullableDecimal(poz, "KosztEwidencyjny"),
            CostValue = DynamicPropertyHelper.GetNullableDecimal(dane, "KosztMagazynowy")
                     ?? DynamicPropertyHelper.GetNullableDecimal(poz, "KosztMagazynowy"),
            Margin = DynamicPropertyHelper.GetNullableDecimal(dane, "Marza"),
            MarginPercent = DynamicPropertyHelper.GetNullableDecimal(dane, "MarzaProcent")
        };

        // Fallback: if PriceNet is still 0, try to calculate from ValueNet / Quantity
        if (dto.PriceNet == 0 && dto.ValueNet != 0 && dto.Quantity != 0)
        {
            dto.PriceNet = dto.ValueNet / dto.Quantity;
        }
        if (dto.PriceGross == 0 && dto.ValueGross != 0 && dto.Quantity != 0)
        {
            dto.PriceGross = dto.ValueGross / dto.Quantity;
        }

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

    #region Header helpers (property names verified against SDK 61.1.0.9431 metadata)

    // Dokument.Wartosc is a sub-object (Wartosc: NettoPoRabacie/BruttoPoRabacie/VatPoRabacie); flat WartoscNetto/WartoscBrutto/WartoscVat
    // do NOT exist on Dokument - kept only as last-resort fallbacks for exotic document classes.
    private static decimal GetHeaderTotalNet(dynamic dokument)
    {
        var wartosc = DynamicPropertyHelper.GetProperty(dokument, "Wartosc");
        if (wartosc != null)
        {
            decimal? v = DynamicPropertyHelper.GetNullableDecimal(wartosc, "NettoPoRabacie");
            if (v.HasValue) return v.Value;
        }
        return DynamicPropertyHelper.GetDecimal(dokument, "WartoscNetto");
    }

    private static decimal GetHeaderTotalVat(dynamic dokument)
    {
        var wartosc = DynamicPropertyHelper.GetProperty(dokument, "Wartosc");
        if (wartosc != null)
        {
            decimal? v = DynamicPropertyHelper.GetNullableDecimal(wartosc, "VatPoRabacie");
            if (v.HasValue) return v.Value;
        }
        return DynamicPropertyHelper.GetDecimal(dokument, "WartoscVat");
    }

    private static decimal GetHeaderTotalGross(dynamic dokument)
    {
        var wartosc = DynamicPropertyHelper.GetProperty(dokument, "Wartosc");
        if (wartosc != null)
        {
            decimal? v = DynamicPropertyHelper.GetNullableDecimal(wartosc, "BruttoPoRabacie");
            if (v.HasValue) return v.Value;
        }
        return DynamicPropertyHelper.GetDecimal(dokument, "WartoscBrutto");
    }

    // Dates: Dokument.DataWydaniaWystawienia = issue / goods-issue date shown as "Data wystawienia" in Subiekt,
    // Dokument.DataWprowadzenia = entry date, DokumentHandlowy.DataSprzedazy = sale date (FS/FZ/PA only).
    private static DateTime? GetDocumentIssueDate(dynamic dokument) =>
        DynamicPropertyHelper.GetDateTime(dokument, "DataWydaniaWystawienia")
        ?? DynamicPropertyHelper.GetDateTime(dokument, "DataWprowadzenia")
        ?? DynamicPropertyHelper.GetDateTime(dokument, "DataWystawienia");

    private static DateTime? GetDocumentSaleDate(dynamic dokument) =>
        DynamicPropertyHelper.GetDateTime(dokument, "DataSprzedazy")
        ?? DynamicPropertyHelper.GetDateTime(dokument, "DataMagazynowa")
        ?? DynamicPropertyHelper.GetDateTime(dokument, "DataWydaniaWystawienia");

    /// <summary>
    /// Due date of a document: the latest term among its payments (PlatnosciDokumentow[].Termin),
    /// else the settlement term (Rozrachunek.TerminPlatnosci). There is no TerminPlatnosci on Dokument itself.
    /// </summary>
    private static DateTime? GetDocumentDueDate(dynamic dokument)
    {
        DateTime? result = null;
        try
        {
            var platnosci = DynamicPropertyHelper.GetProperty(dokument, "PlatnosciDokumentow");
            if (platnosci != null)
            {
                foreach (var platnosc in platnosci)
                {
                    DateTime? termin = DynamicPropertyHelper.GetDateTime(platnosc, "Termin");
                    if (termin.HasValue && (!result.HasValue || termin.Value > result.Value)) result = termin;
                }
            }
        }
        catch { /* payments not loaded */ }

        if (!result.HasValue)
        {
            var rozrachunek = DynamicPropertyHelper.GetProperty(dokument, "Rozrachunek");
            if (rozrachunek != null) result = DynamicPropertyHelper.GetDateTime(rozrachunek, "TerminPlatnosci");
        }
        return result ?? DynamicPropertyHelper.GetDateTime(dokument, "TerminPlatnosci");
    }

    /// <summary>Term in days of the (first) deferred payment on the document (PlatnoscDokumentu.TerminDni).</summary>
    private static int? GetDocumentDueDays(dynamic dokument)
    {
        try
        {
            var platnosci = DynamicPropertyHelper.GetProperty(dokument, "PlatnosciDokumentow");
            if (platnosci == null) return null;
            int? fallback = null;
            foreach (var platnosc in platnosci)
            {
                int? dni = DynamicPropertyHelper.GetNullableInt(platnosc, "TerminDni");
                if (!dni.HasValue) continue;
                if (DynamicPropertyHelper.GetNullableInt(platnosc, "RodzajPlatnosci") == 3) return dni; // Odroczona
                fallback ??= dni;
            }
            return fallback;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Remaining amount of the document's settlement (Rozrachunek.KwotaPozostala) or null when there is no settlement.</summary>
    private static decimal? GetSettlementRemainingAmount(dynamic dokument)
    {
        var rozrachunek = DynamicPropertyHelper.GetProperty(dokument, "Rozrachunek");
        if (rozrachunek == null) return null;
        return DynamicPropertyHelper.GetNullableDecimal(rozrachunek, "KwotaPozostala");
    }

    private static DocumentSettlementDto? MapDocumentSettlement(dynamic dokument)
    {
        var rozrachunek = DynamicPropertyHelper.GetProperty(dokument, "Rozrachunek");
        if (rozrachunek == null) return null;
        try
        {
            decimal amount = DynamicPropertyHelper.GetDecimal(rozrachunek, "Kwota");
            decimal remaining = DynamicPropertyHelper.GetDecimal(rozrachunek, "KwotaPozostala");
            DateTime? dueDate = DynamicPropertyHelper.GetDateTime(rozrachunek, "TerminPlatnosci");
            int daysOverdue = dueDate.HasValue && dueDate.Value.Date < DateTime.Today && remaining > 0
                ? (int)(DateTime.Today - dueDate.Value.Date).TotalDays
                : 0;
            int typ = DynamicPropertyHelper.GetInt(rozrachunek, "Typ");
            return new DocumentSettlementDto
            {
                Id = DynamicPropertyHelper.GetId(rozrachunek),
                Type = typ == 1 ? "Receivable" : typ == 2 ? "Payable" : null,
                Amount = amount,
                RemainingAmount = remaining,
                UnsettledAmount = DynamicPropertyHelper.GetDecimal(rozrachunek, "KwotaNierozliczona"),
                SettledAmount = amount - remaining,
                Currency = DynamicPropertyHelper.GetString(rozrachunek, "Waluta", "Symbol"),
                DueDate = dueDate,
                LastSettlementDate = DynamicPropertyHelper.GetDateTime(rozrachunek, "DataOstatniegoRozliczenia"),
                IsSettled = remaining <= 0,
                IsOverdue = daysOverdue > 0,
                DaysOverdue = daysOverdue
            };
        }
        catch
        {
            return null;
        }
    }

    private static List<DocumentPaymentDto> MapDocumentPayments(dynamic dokument)
    {
        var result = new List<DocumentPaymentDto>();
        try
        {
            var platnosci = DynamicPropertyHelper.GetProperty(dokument, "PlatnosciDokumentow");
            if (platnosci == null) return result;
            foreach (var platnosc in platnosci)
            {
                int? rodzaj = DynamicPropertyHelper.GetNullableInt(platnosc, "RodzajPlatnosci");
                int? zaplata = DynamicPropertyHelper.GetNullableInt(platnosc, "RodzajZaplaty");
                result.Add(new DocumentPaymentDto
                {
                    Id = DynamicPropertyHelper.GetId(platnosc),
                    Kind = rodzaj switch { 1 => "Prepayment", 2 => "Immediate", 3 => "Deferred", _ => null },
                    PaymentMethod = DynamicPropertyHelper.GetString(platnosc, "FormaPlatnosci", "Nazwa"),
                    PaymentMethodId = DynamicPropertyHelper.GetNullableInt(platnosc, "FormaPlatnosci", "Id"),
                    Amount = DynamicPropertyHelper.GetDecimal(platnosc, "KwotaDokumentu"),
                    AmountInPaymentCurrency = DynamicPropertyHelper.GetNullableDecimal(platnosc, "KwotaPlatnosci"),
                    Percent = DynamicPropertyHelper.GetNullableDecimal(platnosc, "Procent"),
                    DueDate = DynamicPropertyHelper.GetDateTime(platnosc, "Termin"),
                    DueDays = DynamicPropertyHelper.GetNullableInt(platnosc, "TerminDni"),
                    Date = DynamicPropertyHelper.GetDateTime(platnosc, "Data"),
                    SettlementKind = zaplata switch { 0 => "Cash", 1 => "Transfer", _ => null }
                });
            }
        }
        catch { /* payments not loaded */ }
        return result.OrderBy(p => p.DueDate ?? DateTime.MinValue).ToList();
    }

    /// <summary>
    /// Fills the payment-state part of a DocumentDto from the document's payments and settlement.
    /// </summary>
    private static void ApplyPaymentInfo(DocumentDto dto, dynamic dokument)
    {
        try
        {
            dto.Payments = MapDocumentPayments(dokument);
            dto.Settlement = MapDocumentSettlement(dokument);

            DateTime? dueDate = dto.DueDate ?? dto.Payments.Where(p => p.DueDate.HasValue).Select(p => p.DueDate).Max() ?? dto.Settlement?.DueDate;
            dto.DueDate ??= dueDate;
            dto.PaymentDate ??= dueDate;
            dto.PaymentDays ??= dto.Payments.FirstOrDefault(p => p.Kind == "Deferred" && p.DueDays.HasValue)?.DueDays
                              ?? dto.Payments.FirstOrDefault(p => p.DueDays.HasValue)?.DueDays;

            if (dto.Settlement != null)
            {
                dto.RemainingAmount = dto.Settlement.RemainingAmount > 0 ? dto.Settlement.RemainingAmount : null;
                dto.PaidAmount = dto.TotalGross - dto.Settlement.RemainingAmount > 0 ? dto.TotalGross - dto.Settlement.RemainingAmount : null;
                dto.IsPaid = dto.Settlement.IsSettled;
                dto.IsOverdue = dto.Settlement.IsOverdue;
            }
            else
            {
                dto.IsPaid = dto.AmountToPay <= 0;
                dto.IsOverdue = dueDate.HasValue && dueDate.Value.Date < DateTime.Today && dto.AmountToPay > 0;
            }
        }
        catch
        {
            // payment info is best-effort; never break the main mapping
        }
    }

    #endregion

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
                // Get the type name for better error messages
                string typeName = "(unknown)";
                try
                {
                    typeName = ((object)encjaZBledami).GetType().Name;
                }
                catch { }

                // Extract entity-level errors (Errors property)
                var entityErrors = DynamicPropertyHelper.GetProperty(encjaZBledami, "Errors");
                if (entityErrors != null)
                {
                    foreach (var blad in entityErrors)
                    {
                        string errorMsg = blad?.ToString() ?? "Unknown error";
                        errors.Add($"{typeName}: {errorMsg}");
                    }
                }

                // Extract field-level errors (MemberErrors property)
                // MemberErrors is IEnumerable<IGrouping<string, string>> where Key = error message, Value = field names
                var memberErrors = DynamicPropertyHelper.GetProperty(encjaZBledami, "MemberErrors");
                if (memberErrors != null)
                {
                    try
                    {
                        // MemberErrors is a collection of IGrouping where Key is the error message
                        foreach (var errorGroup in memberErrors)
                        {
                            try
                            {
                                // Get the error message (Key of the grouping)
                                string? errorMessage = null;
                                var keyProp = ((object)errorGroup).GetType().GetProperty("Key");
                                if (keyProp != null)
                                {
                                    errorMessage = keyProp.GetValue(errorGroup)?.ToString();
                                }

                                // Get the field names (items in the grouping)
                                List<string> fieldNames = new();
                                foreach (var fieldName in errorGroup)
                                {
                                    if (fieldName != null)
                                    {
                                        fieldNames.Add(fieldName.ToString() ?? "");
                                    }
                                }

                                if (!string.IsNullOrEmpty(errorMessage))
                                {
                                    string fieldsStr = fieldNames.Any() 
                                        ? $" on fields: {string.Join(", ", fieldNames.Select(f => $"{typeName}.{f}"))}"
                                        : "";
                                    errors.Add($"{errorMessage}{fieldsStr}");
                                }
                            }
                            catch (Exception groupEx)
                            {
                                // Fallback if we can't parse the grouping
                                errors.Add($"{typeName}: {errorGroup?.ToString() ?? "Unknown field error"}");
                            }
                        }
                    }
                    catch (Exception memberEx)
                    {
                        errors.Add($"{typeName}: Could not parse MemberErrors - {memberEx.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Could not retrieve error details: {ex.Message}");
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
    public async Task<ActionResult<ApiResponse<object>>> AssociateDocument(int id, [FromBody] DocumentAssociationRequest request)
    {
        try
        {
            // Capture for use inside lambda
            int targetDocumentId = request.TargetDocumentId;
            string? relationType = request.RelationType;

            var result = await _sferaService.ExecuteWithLockAsync<(
                bool Success,
                bool SourceNotFound,
                bool TargetNotFound,
                bool NoPowiazane,
                string Message,
                object? Data)>(() =>
            {
                // Try to find the source document in various managers
                dynamic? sourceDocument = null;
                dynamic? sourceManager = null;

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
                        foreach (var d in DynamicPropertyHelper.SafeGetAll((object)dokumentyManager))
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

                // If not found, try other managers
                if (sourceDocument == null)
                {
                    foreach (var managerName in managersToCheck.Skip(1)) // Skip "Dokumenty"
                    {
                        var manager = _sferaService.GetManager(managerName);
                        if (manager == null) continue;

                        try
                        {
                            foreach (var d in DynamicPropertyHelper.SafeGetAll((object)manager))
                            {
                                if (DynamicPropertyHelper.GetId(d) == id)
                                {
                                    sourceDocument = d;
                                    sourceManager = manager;
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
                    return (false, true, false, false, $"Source document with ID {id} not found", null);
                }

                // Find the target document
                dynamic? targetDocument = null;

                foreach (var managerName in managersToCheck)
                {
                    var manager = _sferaService.GetManager(managerName);
                    if (manager == null) continue;

                    try
                    {
                        var doc = manager.Dane.Znajdz(targetDocumentId);
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
                    return (false, false, true, false, $"Target document with ID {targetDocumentId} not found", null);
                }

                // Create association using DokumentyPowiazane
                try
                {
                    // Get DokumentyPowiazane collection
                    var dokumentyPowiazane = DynamicPropertyHelper.GetProperty(sourceDocument, "DokumentyPowiazane");
                    if (dokumentyPowiazane == null)
                    {
                        return (false, false, false, true, "Source document does not support document associations (DokumentyPowiazane)", null);
                    }

                    // Add the target document to the collection
                    dokumentyPowiazane.Dodaj(targetDocument);

                    // Save the source document
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
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Could not save using Edytuj pattern, trying alternative approach");
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
                                         ?? targetDocumentId.ToString();

                        _logger.LogInformation("Associated document {SourceDoc} with {TargetDoc}", (object)sourceNumber, (object)targetNumber);

                        return (true, false, false, false, "Documents associated successfully", (object)new
                        {
                            SourceDocumentId = id,
                            TargetDocumentId = targetDocumentId,
                            RelationType = relationType,
                            SourceDocumentNumber = sourceNumber,
                            TargetDocumentNumber = targetNumber
                        });
                    }
                    else
                    {
                        return (false, false, false, false, "Failed to save document association", null);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating document association");
                    return (false, false, false, false, $"Error creating association: {ex.Message}", null);
                }
            });

            if (result.SourceNotFound)
            {
                return NotFound(ApiResponse<object>.Error(result.Message));
            }
            else if (result.TargetNotFound)
            {
                return NotFound(ApiResponse<object>.Error(result.Message));
            }
            else if (result.NoPowiazane)
            {
                return BadRequest(ApiResponse<object>.Error(result.Message));
            }
            else if (result.Success)
            {
                return Ok(ApiResponse<object>.Ok(result.Data!, result.Message));
            }
            else
            {
                return BadRequest(ApiResponse<object>.Error(result.Message));
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
    public async Task<ActionResult<ApiResponse<List<DocumentListItemDto>>>> GetDocumentAssociations(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync<(bool Found, List<DocumentListItemDto> Associations)>(() =>
            {
                // Find the document using iteration (same pattern as GetDocument)
                dynamic? document = null;
                var dokumentyManager = _sferaService.GetManager("Dokumenty");
                if (dokumentyManager != null)
                {
                    try
                    {
                        foreach (var d in DynamicPropertyHelper.SafeGetAll((object)dokumentyManager))
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
                    return (false, new List<DocumentListItemDto>());
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

                return (true, associations);
            });

            if (!result.Found)
            {
                return NotFound(ApiResponse<List<DocumentListItemDto>>.Error($"Document with ID {id} not found"));
            }

            return Ok(ApiResponse<List<DocumentListItemDto>>.Ok(result.Associations));
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
    public async Task<ActionResult<ApiResponse<object>>> RemoveDocumentAssociation(int id, int targetId)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync<(
                bool Success,
                bool SourceNotFound,
                bool NoPowiazane,
                bool AssocNotFound,
                string Message)>(() =>
            {
                // Find the source document using iteration
                dynamic? sourceDocument = null;
                dynamic? sourceManager = null;

                var dokumentyManager = _sferaService.GetManager("Dokumenty");
                if (dokumentyManager != null)
                {
                    try
                    {
                        foreach (var d in DynamicPropertyHelper.SafeGetAll((object)dokumentyManager))
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
                    return (false, true, false, false, $"Source document with ID {id} not found");
                }

                // Get DokumentyPowiazane and find the target to remove
                var dokumentyPowiazane = DynamicPropertyHelper.GetProperty(sourceDocument, "DokumentyPowiazane");
                if (dokumentyPowiazane == null)
                {
                    return (false, false, true, false, "Document does not support associations");
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
                    return (false, false, false, true, $"Association with document ID {targetId} not found");
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
                    return (true, false, false, false, "Association removed successfully");
                }
                else
                {
                    return (false, false, false, false, "Failed to remove association");
                }
            });

            if (result.SourceNotFound)
            {
                return NotFound(ApiResponse<object>.Error(result.Message));
            }
            else if (result.NoPowiazane)
            {
                return BadRequest(ApiResponse<object>.Error(result.Message));
            }
            else if (result.AssocNotFound)
            {
                return NotFound(ApiResponse<object>.Error(result.Message));
            }
            else if (result.Success)
            {
                return Ok(ApiResponse<object>.Ok(new { RemovedAssociation = targetId }, result.Message));
            }
            else
            {
                return BadRequest(ApiResponse<object>.Error(result.Message));
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
