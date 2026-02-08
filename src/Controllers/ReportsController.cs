using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Business reports endpoints
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize]
[Tags("Reports")]
public class ReportsController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(ISferaService sferaService, ILogger<ReportsController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    #region Report Definitions

    /// <summary>
    /// Get available report types
    /// </summary>
    [HttpGet("types")]
    [ProducesResponseType(typeof(ApiResponse<List<ReportTypeDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<ReportTypeDto>>> GetReportTypes()
    {
        var types = new List<ReportTypeDto>
        {
            new() { Code = "SALES_SUMMARY", Name = "Podsumowanie sprzedaży", Category = "Sales", Description = "Zestawienie sprzedaży za okres" },
            new() { Code = "SALES_BY_PRODUCT", Name = "Sprzedaż wg produktów", Category = "Sales", Description = "Ranking sprzedaży produktów" },
            new() { Code = "SALES_BY_CUSTOMER", Name = "Sprzedaż wg klientów", Category = "Sales", Description = "Ranking sprzedaży dla klientów" },
            new() { Code = "SALES_BY_SALESPERSON", Name = "Sprzedaż wg sprzedawców", Category = "Sales", Description = "Zestawienie sprzedaży wg sprzedawców" },
            new() { Code = "PURCHASE_SUMMARY", Name = "Podsumowanie zakupów", Category = "Purchase", Description = "Zestawienie zakupów za okres" },
            new() { Code = "PURCHASE_BY_SUPPLIER", Name = "Zakupy wg dostawców", Category = "Purchase", Description = "Ranking zakupów od dostawców" },
            new() { Code = "INVENTORY_TURNOVER", Name = "Rotacja magazynowa", Category = "Inventory", Description = "Analiza rotacji towarów" },
            new() { Code = "INVENTORY_VALUE", Name = "Wartość magazynu", Category = "Inventory", Description = "Wycena stanów magazynowych" },
            new() { Code = "CUSTOMER_RANKING", Name = "Ranking klientów", Category = "Customers", Description = "Ranking klientów wg obrotów" },
            new() { Code = "PRODUCT_BESTSELLERS", Name = "Bestsellery", Category = "Products", Description = "Najlepiej sprzedające się produkty" },
            new() { Code = "RECEIVABLES_AGING", Name = "Wiekowanie należności", Category = "Finance", Description = "Analiza wiekowania należności" },
            new() { Code = "PAYABLES_AGING", Name = "Wiekowanie zobowiązań", Category = "Finance", Description = "Analiza wiekowania zobowiązań" },
            new() { Code = "CASH_FLOW", Name = "Przepływy pieniężne", Category = "Finance", Description = "Raport przepływów kasowych" },
            new() { Code = "VAT_REGISTER", Name = "Rejestr VAT", Category = "Tax", Description = "Rejestr VAT sprzedaży i zakupów" }
        };

        return Ok(ApiResponse<List<ReportTypeDto>>.Ok(types));
    }

    #endregion

    #region Sales Reports

    /// <summary>
    /// Get sales summary report
    /// </summary>
    [HttpGet("sales/summary")]
    [ProducesResponseType(typeof(ApiResponse<SalesSummaryReportDto>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<SalesSummaryReportDto>> GetSalesSummary(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo)
    {
        try
        {
            var from = dateFrom ?? DateTime.Today.AddMonths(-1);
            var to = dateTo ?? DateTime.Today;

            var manager = _sferaService.GetManager("DokumentySprzedazy");
            if (manager == null)
            {
                return StatusCode(500, ApiResponse<SalesSummaryReportDto>.Error("Failed to get DokumentySprzedazy manager"));
            }

            var allDocs = DynamicPropertyHelper.SafeGetAll(manager);

            // Filter by date range
            var filteredDocs = allDocs.Where(d =>
            {
                var dataWystawienia = DynamicPropertyHelper.GetDateTime(d, "DataWystawienia");
                return dataWystawienia.HasValue && dataWystawienia.Value >= from && dataWystawienia.Value <= to;
            }).ToList();

            var summary = new SalesSummaryReportDto
            {
                DateFrom = from,
                DateTo = to,
                TotalDocuments = filteredDocs.Count,
                TotalNetValue = 0,
                TotalGrossValue = 0,
                TotalVatValue = 0,
                ByDocumentType = new List<SalesByTypeDto>()
            };

            var byType = new Dictionary<string, (int count, decimal net, decimal gross)>();

            foreach (var doc in filteredDocs)
            {
                var netValue = DynamicPropertyHelper.GetDecimal(doc, "WartoscNetto");
                var grossValue = DynamicPropertyHelper.GetDecimal(doc, "WartoscBrutto");
                var docType = DynamicPropertyHelper.GetString(doc, "TypDokumentu") ?? "Unknown";

                summary.TotalNetValue += netValue;
                summary.TotalGrossValue += grossValue;

                if (!byType.ContainsKey(docType))
                    byType[docType] = (0, 0, 0);

                var current = byType[docType];
                byType[docType] = (current.count + 1, current.net + netValue, current.gross + grossValue);
            }

            summary.TotalVatValue = summary.TotalGrossValue - summary.TotalNetValue;

            foreach (var kvp in byType)
            {
                summary.ByDocumentType.Add(new SalesByTypeDto
                {
                    DocumentType = kvp.Key,
                    Count = kvp.Value.count,
                    NetValue = kvp.Value.net,
                    GrossValue = kvp.Value.gross
                });
            }

            return Ok(ApiResponse<SalesSummaryReportDto>.Ok(summary));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sales summary report");
            return StatusCode(500, ApiResponse<SalesSummaryReportDto>.Error("Error generating sales summary", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get sales by product report (bestsellers)
    /// </summary>
    [HttpGet("sales/by-product")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductSalesDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<ProductSalesDto>>> GetSalesByProduct(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int top = 20)
    {
        try
        {
            var from = dateFrom ?? DateTime.Today.AddMonths(-1);
            var to = dateTo ?? DateTime.Today;

            var manager = _sferaService.GetManager("DokumentySprzedazy");
            if (manager == null)
            {
                return StatusCode(500, ApiResponse<List<ProductSalesDto>>.Error("Failed to get DokumentySprzedazy manager"));
            }

            var allDocs = DynamicPropertyHelper.SafeGetAll(manager);

            // Filter by date range
            var filteredDocs = allDocs.Where(d =>
            {
                var dataWystawienia = DynamicPropertyHelper.GetDateTime(d, "DataWystawienia");
                return dataWystawienia.HasValue && dataWystawienia.Value >= from && dataWystawienia.Value <= to;
            }).ToList();

            var productSales = new Dictionary<int, ProductSalesDto>();

            foreach (var doc in filteredDocs)
            {
                var pozycje = DynamicPropertyHelper.GetProperty(doc, "Pozycje");
                if (pozycje == null) continue;

                foreach (var poz in (IEnumerable<dynamic>)pozycje)
                {
                    var asortyment = DynamicPropertyHelper.GetProperty(poz, "Asortyment");
                    if (asortyment == null) continue;

                    var productId = DynamicPropertyHelper.GetId(asortyment);
                    var productSymbol = DynamicPropertyHelper.GetString(asortyment, "Symbol") ?? "";
                    var productName = DynamicPropertyHelper.GetString(asortyment, "Nazwa") ?? "";
                    var quantity = DynamicPropertyHelper.GetDecimal(poz, "Ilosc");
                    var netValue = DynamicPropertyHelper.GetDecimal(poz, "WartoscNetto");
                    var grossValue = DynamicPropertyHelper.GetDecimal(poz, "WartoscBrutto");

                    if (!productSales.ContainsKey(productId))
                    {
                        productSales[productId] = new ProductSalesDto
                        {
                            ProductId = productId,
                            Symbol = productSymbol,
                            Name = productName,
                            TotalQuantity = 0,
                            TotalNetValue = 0,
                            TotalGrossValue = 0,
                            TransactionCount = 0
                        };
                    }

                    productSales[productId].TotalQuantity += quantity;
                    productSales[productId].TotalNetValue += netValue;
                    productSales[productId].TotalGrossValue += grossValue;
                    productSales[productId].TransactionCount++;
                }
            }

            var result = productSales.Values
                .OrderByDescending(p => p.TotalGrossValue)
                .Take(top)
                .ToList();

            return Ok(ApiResponse<List<ProductSalesDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sales by product report");
            return StatusCode(500, ApiResponse<List<ProductSalesDto>>.Error("Error generating sales by product report", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get sales by customer report (customer ranking)
    /// </summary>
    [HttpGet("sales/by-customer")]
    [ProducesResponseType(typeof(ApiResponse<List<CustomerSalesDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<CustomerSalesDto>>> GetSalesByCustomer(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int top = 20)
    {
        try
        {
            var from = dateFrom ?? DateTime.Today.AddMonths(-1);
            var to = dateTo ?? DateTime.Today;

            var manager = _sferaService.GetManager("DokumentySprzedazy");
            if (manager == null)
            {
                return StatusCode(500, ApiResponse<List<CustomerSalesDto>>.Error("Failed to get DokumentySprzedazy manager"));
            }

            var allDocs = DynamicPropertyHelper.SafeGetAll(manager);

            // Filter by date range
            var filteredDocs = allDocs.Where(d =>
            {
                var dataWystawienia = DynamicPropertyHelper.GetDateTime(d, "DataWystawienia");
                return dataWystawienia.HasValue && dataWystawienia.Value >= from && dataWystawienia.Value <= to;
            }).ToList();

            var customerSales = new Dictionary<int, CustomerSalesDto>();

            foreach (var doc in filteredDocs)
            {
                var kontrahent = DynamicPropertyHelper.GetProperty(doc, "Kontrahent");
                if (kontrahent == null) continue;

                var customerId = DynamicPropertyHelper.GetId(kontrahent);
                var customerSymbol = DynamicPropertyHelper.GetString(kontrahent, "Symbol") ?? "";
                var customerName = DynamicPropertyHelper.GetString(kontrahent, "Nazwa") ?? "";
                var netValue = DynamicPropertyHelper.GetDecimal(doc, "WartoscNetto");
                var grossValue = DynamicPropertyHelper.GetDecimal(doc, "WartoscBrutto");

                if (!customerSales.ContainsKey(customerId))
                {
                    customerSales[customerId] = new CustomerSalesDto
                    {
                        CustomerId = customerId,
                        Symbol = customerSymbol,
                        Name = customerName,
                        TotalNetValue = 0,
                        TotalGrossValue = 0,
                        DocumentCount = 0
                    };
                }

                customerSales[customerId].TotalNetValue += netValue;
                customerSales[customerId].TotalGrossValue += grossValue;
                customerSales[customerId].DocumentCount++;
            }

            var result = customerSales.Values
                .OrderByDescending(c => c.TotalGrossValue)
                .Take(top)
                .ToList();

            return Ok(ApiResponse<List<CustomerSalesDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sales by customer report");
            return StatusCode(500, ApiResponse<List<CustomerSalesDto>>.Error("Error generating sales by customer report", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Purchase Reports

    /// <summary>
    /// Get purchase summary report
    /// </summary>
    [HttpGet("purchases/summary")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseSummaryReportDto>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<PurchaseSummaryReportDto>> GetPurchaseSummary(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo)
    {
        try
        {
            var from = dateFrom ?? DateTime.Today.AddMonths(-1);
            var to = dateTo ?? DateTime.Today;

            var manager = _sferaService.GetManager("DokumentyZakupu");
            if (manager == null)
            {
                return StatusCode(500, ApiResponse<PurchaseSummaryReportDto>.Error("Failed to get DokumentyZakupu manager"));
            }

            var allDocs = DynamicPropertyHelper.SafeGetAll(manager);

            // Filter by date range
            var filteredDocs = allDocs.Where(d =>
            {
                var dataWystawienia = DynamicPropertyHelper.GetDateTime(d, "DataWystawienia");
                return dataWystawienia.HasValue && dataWystawienia.Value >= from && dataWystawienia.Value <= to;
            }).ToList();

            var summary = new PurchaseSummaryReportDto
            {
                DateFrom = from,
                DateTo = to,
                TotalDocuments = filteredDocs.Count,
                TotalNetValue = 0,
                TotalGrossValue = 0,
                TotalVatValue = 0,
                BySupplier = new List<PurchaseBySupplierDto>()
            };

            var bySupplier = new Dictionary<int, (string symbol, string name, int count, decimal net, decimal gross)>();

            foreach (var doc in filteredDocs)
            {
                var netValue = DynamicPropertyHelper.GetDecimal(doc, "WartoscNetto");
                var grossValue = DynamicPropertyHelper.GetDecimal(doc, "WartoscBrutto");

                summary.TotalNetValue += netValue;
                summary.TotalGrossValue += grossValue;

                var kontrahent = DynamicPropertyHelper.GetProperty(doc, "Kontrahent");
                if (kontrahent != null)
                {
                    var supplierId = DynamicPropertyHelper.GetId(kontrahent);
                    var supplierSymbol = DynamicPropertyHelper.GetString(kontrahent, "Symbol") ?? "";
                    var supplierName = DynamicPropertyHelper.GetString(kontrahent, "Nazwa") ?? "";

                    if (!bySupplier.ContainsKey(supplierId))
                        bySupplier[supplierId] = (supplierSymbol, supplierName, 0, 0, 0);

                    var current = bySupplier[supplierId];
                    bySupplier[supplierId] = (current.symbol, current.name, current.count + 1, current.net + netValue, current.gross + grossValue);
                }
            }

            summary.TotalVatValue = summary.TotalGrossValue - summary.TotalNetValue;

            foreach (var kvp in bySupplier.OrderByDescending(x => x.Value.gross).Take(10))
            {
                summary.BySupplier.Add(new PurchaseBySupplierDto
                {
                    SupplierId = kvp.Key,
                    Symbol = kvp.Value.symbol,
                    Name = kvp.Value.name,
                    DocumentCount = kvp.Value.count,
                    NetValue = kvp.Value.net,
                    GrossValue = kvp.Value.gross
                });
            }

            return Ok(ApiResponse<PurchaseSummaryReportDto>.Ok(summary));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting purchase summary report");
            return StatusCode(500, ApiResponse<PurchaseSummaryReportDto>.Error("Error generating purchase summary", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Inventory Reports

    /// <summary>
    /// Get inventory turnover report
    /// </summary>
    [HttpGet("inventory/turnover")]
    [ProducesResponseType(typeof(ApiResponse<List<InventoryTurnoverDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<InventoryTurnoverDto>>> GetInventoryTurnover(
        [FromQuery] int? warehouseId,
        [FromQuery] int top = 50)
    {
        try
        {
            var productManager = _sferaService.GetManager("Asortymenty");
            if (productManager == null)
            {
                return StatusCode(500, ApiResponse<List<InventoryTurnoverDto>>.Error("Failed to get Asortymenty manager"));
            }

            var allProducts = DynamicPropertyHelper.SafeGetAll(productManager);

            var result = new List<InventoryTurnoverDto>();

            foreach (var prod in allProducts.Take(top))
            {
                var stanyMagazynowe = DynamicPropertyHelper.GetProperty(prod, "StanyMagazynowe");
                decimal currentStock = 0;

                if (stanyMagazynowe != null)
                {
                    foreach (var stan in (IEnumerable<dynamic>)stanyMagazynowe)
                    {
                        if (warehouseId.HasValue)
                        {
                            var magazyn = DynamicPropertyHelper.GetProperty(stan, "Magazyn");
                            if (magazyn != null && DynamicPropertyHelper.GetId(magazyn) != warehouseId.Value)
                                continue;
                        }
                        currentStock += DynamicPropertyHelper.GetDecimal(stan, "Ilosc");
                    }
                }

                result.Add(new InventoryTurnoverDto
                {
                    ProductId = DynamicPropertyHelper.GetId(prod),
                    Symbol = DynamicPropertyHelper.GetString(prod, "Symbol") ?? "",
                    Name = DynamicPropertyHelper.GetString(prod, "Nazwa") ?? "",
                    CurrentStock = currentStock,
                    // Note: Full turnover calculation requires historical sales data
                    SalesQuantity30Days = 0,
                    TurnoverDays = currentStock > 0 ? 0 : -1
                });
            }

            return Ok(ApiResponse<List<InventoryTurnoverDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting inventory turnover report");
            return StatusCode(500, ApiResponse<List<InventoryTurnoverDto>>.Error("Error generating inventory turnover report", new List<string> { ex.Message }));
        }
    }

    #endregion
}

#region DTOs

/// <summary>
/// Report type definition
/// </summary>
public class ReportTypeDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>
/// Sales summary report
/// </summary>
public class SalesSummaryReportDto
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public int TotalDocuments { get; set; }
    public decimal TotalNetValue { get; set; }
    public decimal TotalGrossValue { get; set; }
    public decimal TotalVatValue { get; set; }
    public List<SalesByTypeDto> ByDocumentType { get; set; } = new();
}

/// <summary>
/// Sales by document type
/// </summary>
public class SalesByTypeDto
{
    public string DocumentType { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal NetValue { get; set; }
    public decimal GrossValue { get; set; }
}

/// <summary>
/// Product sales ranking
/// </summary>
public class ProductSalesDto
{
    public int ProductId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal TotalQuantity { get; set; }
    public decimal TotalNetValue { get; set; }
    public decimal TotalGrossValue { get; set; }
    public int TransactionCount { get; set; }
}

/// <summary>
/// Customer sales ranking
/// </summary>
public class CustomerSalesDto
{
    public int CustomerId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal TotalNetValue { get; set; }
    public decimal TotalGrossValue { get; set; }
    public int DocumentCount { get; set; }
}

/// <summary>
/// Purchase summary report
/// </summary>
public class PurchaseSummaryReportDto
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public int TotalDocuments { get; set; }
    public decimal TotalNetValue { get; set; }
    public decimal TotalGrossValue { get; set; }
    public decimal TotalVatValue { get; set; }
    public List<PurchaseBySupplierDto> BySupplier { get; set; } = new();
}

/// <summary>
/// Purchase by supplier
/// </summary>
public class PurchaseBySupplierDto
{
    public int SupplierId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int DocumentCount { get; set; }
    public decimal NetValue { get; set; }
    public decimal GrossValue { get; set; }
}

/// <summary>
/// Inventory turnover report item
/// </summary>
public class InventoryTurnoverDto
{
    public int ProductId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal SalesQuantity30Days { get; set; }
    public decimal TurnoverDays { get; set; }
}

#endregion
