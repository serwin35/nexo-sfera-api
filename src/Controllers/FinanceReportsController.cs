using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Finance reports endpoints (Cash reports, Bank statements)
/// </summary>
[ApiController]
[Route("api/finance")]
[Authorize]
[Tags("Finance Reports")]
public class FinanceReportsController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<FinanceReportsController> _logger;

    public FinanceReportsController(ISferaService sferaService, ILogger<FinanceReportsController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    #region Cash Registers

    /// <summary>
    /// Get all cash registers
    /// </summary>
    [HttpGet("cash-registers")]
    [ProducesResponseType(typeof(ApiResponse<List<CashRegisterDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<CashRegisterDto>>> GetCashRegisters([FromQuery] bool? activeOnly = true)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var stanowiskaManager = sfera.StanowiskaKasowe();
            var allStanowiska = ((IEnumerable<dynamic>)stanowiskaManager.Dane.Wszystkie()).ToList();

            if (activeOnly == true)
            {
                var filteredStanowiska = new List<dynamic>();
                foreach (var s in allStanowiska)
                {
                    if (DynamicPropertyHelper.GetBool(s, "Aktywne"))
                    {
                        filteredStanowiska.Add(s);
                    }
                }
                allStanowiska = filteredStanowiska;
            }

            var dtos = new List<CashRegisterDto>();
            foreach (var s in allStanowiska)
            {
                dtos.Add(new CashRegisterDto
                {
                    Id = DynamicPropertyHelper.GetId(s),
                    Symbol = DynamicPropertyHelper.GetString(s, "Symbol"),
                    Name = DynamicPropertyHelper.GetString(s, "Nazwa"),
                    CurrencySymbol = DynamicPropertyHelper.GetString(s, "Waluta", "Symbol"),
                    CurrentBalance = DynamicPropertyHelper.GetDecimal(s, "StanKasy"),
                    IsActive = DynamicPropertyHelper.GetBool(s, "Aktywne"),
                    IsDefault = DynamicPropertyHelper.GetBool(s, "Domyslne")
                });
            }
            dtos = dtos.OrderBy(c => c.Symbol).ToList();

            return Ok(ApiResponse<List<CashRegisterDto>>.Ok(dtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cash registers");
            return StatusCode(500, ApiResponse<List<CashRegisterDto>>.Error("Error retrieving cash registers", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Cash Reports

    /// <summary>
    /// Get cash reports
    /// </summary>
    [HttpGet("cash-reports")]
    [ProducesResponseType(typeof(PagedResponse<CashReportDto>), StatusCodes.Status200OK)]
    public ActionResult<PagedResponse<CashReportDto>> GetCashReports(
        [FromQuery] string? cashRegisterSymbol,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] bool? closedOnly,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var raportyManager = sfera.RaportyKasowe();
            var allRaporty = ((IEnumerable<dynamic>)raportyManager.Dane.Wszystkie()).ToList();

            if (!string.IsNullOrEmpty(cashRegisterSymbol))
            {
                allRaporty = allRaporty.Where(r =>
                    DynamicPropertyHelper.GetString(r, "StanowiskoKasowe", "Symbol") == cashRegisterSymbol).ToList();
            }

            if (dateFrom.HasValue)
            {
                allRaporty = allRaporty.Where(r =>
                {
                    var data = DynamicPropertyHelper.GetDateTime(r, "DataOd");
                    return data.HasValue && data.Value >= dateFrom.Value;
                }).ToList();
            }

            if (dateTo.HasValue)
            {
                allRaporty = allRaporty.Where(r =>
                {
                    var data = DynamicPropertyHelper.GetDateTime(r, "DataDo");
                    return data.HasValue && data.Value <= dateTo.Value;
                }).ToList();
            }

            if (closedOnly.HasValue && closedOnly.Value)
            {
                allRaporty = allRaporty.Where(r => DynamicPropertyHelper.GetBool(r, "Zamkniety")).ToList();
            }

            var totalCount = allRaporty.Count;
            var pagedRaporty = allRaporty
                .OrderByDescending(r => DynamicPropertyHelper.GetDateTime(r, "DataOd") ?? DateTime.MinValue)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var items = new List<CashReportDto>();
            foreach (var r in pagedRaporty)
            {
                items.Add(MapCashReport(r, false));
            }

            return Ok(new PagedResponse<CashReportDto>
            {
                Data = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cash reports");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving cash reports", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get cash report by ID with operations
    /// </summary>
    [HttpGet("cash-reports/{id}")]
    [ProducesResponseType(typeof(ApiResponse<CashReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CashReportDto>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<CashReportDto>> GetCashReport(int id, [FromQuery] bool includeOperations = true)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var raportyManager = sfera.RaportyKasowe();
            var allRaporty = ((IEnumerable<dynamic>)raportyManager.Dane.Wszystkie()).ToList();
            var raport = allRaporty.FirstOrDefault(r => DynamicPropertyHelper.GetId(r) == id);

            if (raport == null)
            {
                return NotFound(ApiResponse<CashReportDto>.Error($"Cash report with ID {id} not found"));
            }

            var dto = MapCashReport(raport, includeOperations);
            return Ok(ApiResponse<CashReportDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cash report {Id}", id);
            return StatusCode(500, ApiResponse<CashReportDto>.Error("Error retrieving cash report", new List<string> { ex.Message }));
        }
    }

    private static CashReportDto MapCashReport(dynamic r, bool includeOperations)
    {
        var dto = new CashReportDto
        {
            Id = DynamicPropertyHelper.GetId(r),
            Number = DynamicPropertyHelper.GetString(r, "Numer", "PelnaSygnatura"),
            CashRegisterSymbol = DynamicPropertyHelper.GetString(r, "StanowiskoKasowe", "Symbol"),
            CashRegisterName = DynamicPropertyHelper.GetString(r, "StanowiskoKasowe", "Nazwa"),
            DateFrom = DynamicPropertyHelper.GetDateTime(r, "DataOd"),
            DateTo = DynamicPropertyHelper.GetDateTime(r, "DataDo"),
            OpeningBalance = DynamicPropertyHelper.GetDecimal(r, "StanPoczatkowy"),
            TotalDeposits = DynamicPropertyHelper.GetDecimal(r, "SumaWplat"),
            TotalWithdrawals = DynamicPropertyHelper.GetDecimal(r, "SumaWyplat"),
            ClosingBalance = DynamicPropertyHelper.GetDecimal(r, "StanKoncowy"),
            Status = DynamicPropertyHelper.GetBool(r, "Zamkniety") ? "Closed" : "Open",
            IsClosed = DynamicPropertyHelper.GetBool(r, "Zamkniety"),
            OperationCount = DynamicPropertyHelper.GetCount(r, "OperacjeKasowe")
        };

        if (includeOperations)
        {
            var operacje = DynamicPropertyHelper.GetProperty(r, "OperacjeKasowe");
            if (operacje != null)
            {
                dto.Operations = new List<CashReportOperationDto>();
                foreach (dynamic o in operacje)
                {
                    dto.Operations.Add(new CashReportOperationDto
                    {
                        Id = DynamicPropertyHelper.GetId(o),
                        DocumentNumber = DynamicPropertyHelper.GetNestedString(o, "DokumentKasowy", "Numer", "PelnaSygnatura"),
                        Date = DynamicPropertyHelper.GetDateTime(o, "Data"),
                        Description = DynamicPropertyHelper.GetString(o, "Opis"),
                        OperationType = DynamicPropertyHelper.GetInt(o, "Typ") == 0 ? "Deposit" : "Withdrawal",
                        Amount = DynamicPropertyHelper.GetDecimal(o, "Kwota"),
                        ContractorName = DynamicPropertyHelper.GetString(o, "Podmiot", "NazwaSkrocona"),
                        SourceDocumentNumber = DynamicPropertyHelper.GetNestedString(o, "DokumentZrodlowy", "NumerWewnetrzny", "PelnaSygnatura")
                    });
                }
            }
        }

        return dto;
    }

    #endregion

    #region Bank Accounts

    /// <summary>
    /// Get all bank accounts
    /// </summary>
    [HttpGet("bank-accounts")]
    [ProducesResponseType(typeof(ApiResponse<List<BankAccountDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<BankAccountDto>>> GetBankAccounts([FromQuery] bool? activeOnly = true)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var rachunkiManager = sfera.RachunkiBankowe();
            var allRachunki = ((IEnumerable<dynamic>)rachunkiManager.Dane.Wszystkie()).ToList();

            if (activeOnly == true)
            {
                var filteredRachunki = new List<dynamic>();
                foreach (var r in allRachunki)
                {
                    if (DynamicPropertyHelper.GetBool(r, "Aktywny"))
                    {
                        filteredRachunki.Add(r);
                    }
                }
                allRachunki = filteredRachunki;
            }

            var dtos = new List<BankAccountDto>();
            foreach (var r in allRachunki)
            {
                dtos.Add(new BankAccountDto
                {
                    Id = DynamicPropertyHelper.GetId(r),
                    Symbol = DynamicPropertyHelper.GetString(r, "Symbol"),
                    Name = DynamicPropertyHelper.GetString(r, "Nazwa"),
                    AccountNumber = DynamicPropertyHelper.GetString(r, "NumerRachunku"),
                    BankName = DynamicPropertyHelper.GetString(r, "NazwaBanku"),
                    BankSwift = DynamicPropertyHelper.GetString(r, "Swift"),
                    CurrencySymbol = DynamicPropertyHelper.GetString(r, "Waluta", "Symbol"),
                    CurrentBalance = DynamicPropertyHelper.GetDecimal(r, "StanRachunku"),
                    IsActive = DynamicPropertyHelper.GetBool(r, "Aktywny"),
                    IsDefault = DynamicPropertyHelper.GetBool(r, "Domyslny")
                });
            }
            dtos = dtos.OrderBy(b => b.Symbol).ToList();

            return Ok(ApiResponse<List<BankAccountDto>>.Ok(dtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bank accounts");
            return StatusCode(500, ApiResponse<List<BankAccountDto>>.Error("Error retrieving bank accounts", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Bank Statements

    /// <summary>
    /// Get bank statements
    /// </summary>
    [HttpGet("bank-statements")]
    [ProducesResponseType(typeof(PagedResponse<BankStatementDto>), StatusCodes.Status200OK)]
    public ActionResult<PagedResponse<BankStatementDto>> GetBankStatements(
        [FromQuery] string? bankAccountSymbol,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] bool? closedOnly,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var wyciagiManager = sfera.WyciagiBankowe();
            var allWyciagi = ((IEnumerable<dynamic>)wyciagiManager.Dane.Wszystkie()).ToList();

            if (!string.IsNullOrEmpty(bankAccountSymbol))
            {
                allWyciagi = allWyciagi.Where(w =>
                    DynamicPropertyHelper.GetString(w, "RachunekBankowy", "Symbol") == bankAccountSymbol).ToList();
            }

            if (dateFrom.HasValue)
            {
                allWyciagi = allWyciagi.Where(w =>
                {
                    var data = DynamicPropertyHelper.GetDateTime(w, "DataWyciagu");
                    return data.HasValue && data.Value >= dateFrom.Value;
                }).ToList();
            }

            if (dateTo.HasValue)
            {
                allWyciagi = allWyciagi.Where(w =>
                {
                    var data = DynamicPropertyHelper.GetDateTime(w, "DataWyciagu");
                    return data.HasValue && data.Value <= dateTo.Value;
                }).ToList();
            }

            if (closedOnly.HasValue && closedOnly.Value)
            {
                allWyciagi = allWyciagi.Where(w => DynamicPropertyHelper.GetBool(w, "Zamkniety")).ToList();
            }

            var totalCount = allWyciagi.Count;
            var pagedWyciagi = allWyciagi
                .OrderByDescending(w => DynamicPropertyHelper.GetDateTime(w, "DataWyciagu") ?? DateTime.MinValue)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var items = new List<BankStatementDto>();
            foreach (var w in pagedWyciagi)
            {
                items.Add(MapBankStatement(w, false));
            }

            return Ok(new PagedResponse<BankStatementDto>
            {
                Data = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bank statements");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving bank statements", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get bank statement by ID with operations
    /// </summary>
    [HttpGet("bank-statements/{id}")]
    [ProducesResponseType(typeof(ApiResponse<BankStatementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<BankStatementDto>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<BankStatementDto>> GetBankStatement(int id, [FromQuery] bool includeOperations = true)
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();
            var wyciagiManager = sfera.WyciagiBankowe();
            var allWyciagi = ((IEnumerable<dynamic>)wyciagiManager.Dane.Wszystkie()).ToList();
            var wyciag = allWyciagi.FirstOrDefault(w => DynamicPropertyHelper.GetId(w) == id);

            if (wyciag == null)
            {
                return NotFound(ApiResponse<BankStatementDto>.Error($"Bank statement with ID {id} not found"));
            }

            var dto = MapBankStatement(wyciag, includeOperations);
            return Ok(ApiResponse<BankStatementDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bank statement {Id}", id);
            return StatusCode(500, ApiResponse<BankStatementDto>.Error("Error retrieving bank statement", new List<string> { ex.Message }));
        }
    }

    private static BankStatementDto MapBankStatement(dynamic w, bool includeOperations)
    {
        var dto = new BankStatementDto
        {
            Id = DynamicPropertyHelper.GetId(w),
            Number = DynamicPropertyHelper.GetString(w, "NumerWyciagu"),
            BankAccountNumber = DynamicPropertyHelper.GetString(w, "RachunekBankowy", "NumerRachunku"),
            BankAccountName = DynamicPropertyHelper.GetString(w, "RachunekBankowy", "Nazwa"),
            BankName = DynamicPropertyHelper.GetString(w, "RachunekBankowy", "NazwaBanku"),
            StatementDate = DynamicPropertyHelper.GetDateTime(w, "DataWyciagu"),
            DateFrom = DynamicPropertyHelper.GetDateTime(w, "DataOd"),
            DateTo = DynamicPropertyHelper.GetDateTime(w, "DataDo"),
            OpeningBalance = DynamicPropertyHelper.GetDecimal(w, "SaldoPoczatkowe"),
            TotalDeposits = DynamicPropertyHelper.GetDecimal(w, "SumaWplat"),
            TotalWithdrawals = DynamicPropertyHelper.GetDecimal(w, "SumaWyplat"),
            ClosingBalance = DynamicPropertyHelper.GetDecimal(w, "SaldoKoncowe"),
            CurrencySymbol = DynamicPropertyHelper.GetNestedString(w, "RachunekBankowy", "Waluta", "Symbol"),
            Status = DynamicPropertyHelper.GetBool(w, "Zamkniety") ? "Closed" : "Open",
            IsClosed = DynamicPropertyHelper.GetBool(w, "Zamkniety"),
            OperationCount = DynamicPropertyHelper.GetCount(w, "OperacjeBankowe")
        };

        if (includeOperations)
        {
            var operacje = DynamicPropertyHelper.GetProperty(w, "OperacjeBankowe");
            if (operacje != null)
            {
                dto.Operations = new List<BankStatementOperationDto>();
                foreach (dynamic o in operacje)
                {
                    dto.Operations.Add(new BankStatementOperationDto
                    {
                        Id = DynamicPropertyHelper.GetId(o),
                        DocumentNumber = DynamicPropertyHelper.GetNestedString(o, "DokumentBankowy", "Numer", "PelnaSygnatura"),
                        Date = DynamicPropertyHelper.GetDateTime(o, "DataOperacji"),
                        ValueDate = DynamicPropertyHelper.GetDateTime(o, "DataKsiegowania"),
                        Description = DynamicPropertyHelper.GetString(o, "Opis"),
                        OperationType = DynamicPropertyHelper.GetInt(o, "Typ") == 0 ? "Deposit" : "Withdrawal",
                        Amount = DynamicPropertyHelper.GetDecimal(o, "Kwota"),
                        ContractorName = DynamicPropertyHelper.GetString(o, "Podmiot", "NazwaSkrocona"),
                        ContractorAccountNumber = DynamicPropertyHelper.GetString(o, "RachunekKontrahenta"),
                        SourceDocumentNumber = DynamicPropertyHelper.GetNestedString(o, "DokumentZrodlowy", "NumerWewnetrzny", "PelnaSygnatura"),
                        ReferenceNumber = DynamicPropertyHelper.GetString(o, "NumerReferencyjny")
                    });
                }
            }
        }

        return dto;
    }

    #endregion

    #region Finance Summary

    /// <summary>
    /// Get finance summary (cash and bank balances)
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<FinanceSummaryDto>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<FinanceSummaryDto>> GetFinanceSummary()
    {
        try
        {
            dynamic sfera = _sferaService.GetSfera();

            // Get cash register balances
            var stanowiskaManager = sfera.StanowiskaKasowe();
            var allStanowiska = ((IEnumerable<dynamic>)stanowiskaManager.Dane.Wszystkie()).ToList();
            var stanowiska = new List<dynamic>();
            foreach (var s in allStanowiska)
            {
                if (DynamicPropertyHelper.GetBool(s, "Aktywne"))
                {
                    stanowiska.Add(s);
                }
            }

            decimal cashBalance = 0;
            var cashRegisterBalances = new List<BalanceItemDto>();
            foreach (var s in stanowiska)
            {
                var stanKasy = DynamicPropertyHelper.GetDecimal(s, "StanKasy");
                cashBalance += stanKasy;
                cashRegisterBalances.Add(new BalanceItemDto
                {
                    Name = DynamicPropertyHelper.GetString(s, "Nazwa") ?? DynamicPropertyHelper.GetString(s, "Symbol") ?? "Unknown",
                    Symbol = DynamicPropertyHelper.GetString(s, "Symbol"),
                    Balance = stanKasy,
                    CurrencySymbol = DynamicPropertyHelper.GetString(s, "Waluta", "Symbol") ?? "PLN"
                });
            }

            // Get bank account balances
            var rachunkiManager = sfera.RachunkiBankowe();
            var allRachunki = ((IEnumerable<dynamic>)rachunkiManager.Dane.Wszystkie()).ToList();
            var rachunki = new List<dynamic>();
            foreach (var r in allRachunki)
            {
                if (DynamicPropertyHelper.GetBool(r, "Aktywny"))
                {
                    rachunki.Add(r);
                }
            }

            decimal bankBalance = 0;
            var bankAccountBalances = new List<BalanceItemDto>();
            foreach (var r in rachunki)
            {
                var stanRachunku = DynamicPropertyHelper.GetDecimal(r, "StanRachunku");
                bankBalance += stanRachunku;
                bankAccountBalances.Add(new BalanceItemDto
                {
                    Name = DynamicPropertyHelper.GetString(r, "Nazwa") ?? DynamicPropertyHelper.GetString(r, "Symbol") ?? "Unknown",
                    Symbol = DynamicPropertyHelper.GetString(r, "Symbol"),
                    Balance = stanRachunku,
                    CurrencySymbol = DynamicPropertyHelper.GetString(r, "Waluta", "Symbol") ?? "PLN"
                });
            }

            var summary = new FinanceSummaryDto
            {
                TotalCashBalance = cashBalance,
                TotalBankBalance = bankBalance,
                TotalBalance = cashBalance + bankBalance,
                CashRegisterCount = stanowiska.Count,
                BankAccountCount = rachunki.Count,
                CashRegisterBalances = cashRegisterBalances,
                BankAccountBalances = bankAccountBalances
            };

            return Ok(ApiResponse<FinanceSummaryDto>.Ok(summary));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting finance summary");
            return StatusCode(500, ApiResponse<FinanceSummaryDto>.Error("Error retrieving finance summary", new List<string> { ex.Message }));
        }
    }

    #endregion
}

/// <summary>
/// Finance summary DTO
/// </summary>
public class FinanceSummaryDto
{
    public decimal TotalCashBalance { get; set; }
    public decimal TotalBankBalance { get; set; }
    public decimal TotalBalance { get; set; }
    public int CashRegisterCount { get; set; }
    public int BankAccountCount { get; set; }
    public List<BalanceItemDto> CashRegisterBalances { get; set; } = new();
    public List<BalanceItemDto> BankAccountBalances { get; set; } = new();
}

/// <summary>
/// Balance item DTO
/// </summary>
public class BalanceItemDto
{
    public string Name { get; set; } = string.Empty;
    public string? Symbol { get; set; }
    public decimal Balance { get; set; }
    public string CurrencySymbol { get; set; } = "PLN";
}
