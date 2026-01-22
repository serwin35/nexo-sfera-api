using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;

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
            var sfera = _sferaService.GetSfera();
            var stanowiska = sfera.StanowiskaKasowe().Dane.Wszystkie();

            if (activeOnly == true)
            {
                stanowiska = stanowiska.Where(s => s.Aktywne == true);
            }

            var dtos = stanowiska.Select(s => new CashRegisterDto
            {
                Id = s.Id,
                Symbol = s.Symbol,
                Name = s.Nazwa,
                CurrencySymbol = s.Waluta?.Symbol,
                CurrentBalance = s.StanKasy ?? 0,
                IsActive = s.Aktywne ?? false,
                IsDefault = s.Domyslne ?? false
            }).OrderBy(c => c.Symbol).ToList();

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
            var sfera = _sferaService.GetSfera();
            var raporty = sfera.RaportyKasowe().Dane.Wszystkie();

            if (!string.IsNullOrEmpty(cashRegisterSymbol))
            {
                raporty = raporty.Where(r => r.StanowiskoKasowe?.Symbol == cashRegisterSymbol);
            }

            if (dateFrom.HasValue)
            {
                raporty = raporty.Where(r => r.DataOd >= dateFrom.Value);
            }

            if (dateTo.HasValue)
            {
                raporty = raporty.Where(r => r.DataDo <= dateTo.Value);
            }

            if (closedOnly.HasValue && closedOnly.Value)
            {
                raporty = raporty.Where(r => r.Zamkniety == true);
            }

            var totalCount = raporty.Count();
            var items = raporty
                .OrderByDescending(r => r.DataOd)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList()
                .Select(r => MapCashReport(r, false))
                .ToList();

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
            var sfera = _sferaService.GetSfera();
            var raport = sfera.RaportyKasowe().Dane.Wszystkie()
                .FirstOrDefault(r => r.Id == id);

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

    private CashReportDto MapCashReport(RaportKasowy r, bool includeOperations)
    {
        var dto = new CashReportDto
        {
            Id = r.Id,
            Number = r.Numer?.PelnaSygnatura,
            CashRegisterSymbol = r.StanowiskoKasowe?.Symbol,
            CashRegisterName = r.StanowiskoKasowe?.Nazwa,
            DateFrom = r.DataOd,
            DateTo = r.DataDo,
            OpeningBalance = r.StanPoczatkowy ?? 0,
            TotalDeposits = r.SumaWplat ?? 0,
            TotalWithdrawals = r.SumaWyplat ?? 0,
            ClosingBalance = r.StanKoncowy ?? 0,
            Status = r.Zamkniety == true ? "Closed" : "Open",
            IsClosed = r.Zamkniety ?? false,
            OperationCount = r.OperacjeKasowe?.Count ?? 0
        };

        if (includeOperations && r.OperacjeKasowe != null)
        {
            dto.Operations = r.OperacjeKasowe.Select(o => new CashReportOperationDto
            {
                Id = o.Id,
                DocumentNumber = o.DokumentKasowy?.Numer?.PelnaSygnatura,
                Date = o.Data,
                Description = o.Opis,
                OperationType = o.Typ == 0 ? "Deposit" : "Withdrawal",
                Amount = o.Kwota ?? 0,
                ContractorName = o.Podmiot?.NazwaSkrocona,
                SourceDocumentNumber = o.DokumentZrodlowy?.NumerWewnetrzny?.PelnaSygnatura
            }).ToList();
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
            var sfera = _sferaService.GetSfera();
            var rachunki = sfera.RachunkiBankowe().Dane.Wszystkie();

            if (activeOnly == true)
            {
                rachunki = rachunki.Where(r => r.Aktywny == true);
            }

            var dtos = rachunki.Select(r => new BankAccountDto
            {
                Id = r.Id,
                Symbol = r.Symbol,
                Name = r.Nazwa,
                AccountNumber = r.NumerRachunku,
                BankName = r.NazwaBanku,
                BankSwift = r.Swift,
                CurrencySymbol = r.Waluta?.Symbol,
                CurrentBalance = r.StanRachunku ?? 0,
                IsActive = r.Aktywny ?? false,
                IsDefault = r.Domyslny ?? false
            }).OrderBy(b => b.Symbol).ToList();

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
            var sfera = _sferaService.GetSfera();
            var wyciagi = sfera.WyciagiBankowe().Dane.Wszystkie();

            if (!string.IsNullOrEmpty(bankAccountSymbol))
            {
                wyciagi = wyciagi.Where(w => w.RachunekBankowy?.Symbol == bankAccountSymbol);
            }

            if (dateFrom.HasValue)
            {
                wyciagi = wyciagi.Where(w => w.DataWyciagu >= dateFrom.Value);
            }

            if (dateTo.HasValue)
            {
                wyciagi = wyciagi.Where(w => w.DataWyciagu <= dateTo.Value);
            }

            if (closedOnly.HasValue && closedOnly.Value)
            {
                wyciagi = wyciagi.Where(w => w.Zamkniety == true);
            }

            var totalCount = wyciagi.Count();
            var items = wyciagi
                .OrderByDescending(w => w.DataWyciagu)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList()
                .Select(w => MapBankStatement(w, false))
                .ToList();

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
            var sfera = _sferaService.GetSfera();
            var wyciag = sfera.WyciagiBankowe().Dane.Wszystkie()
                .FirstOrDefault(w => w.Id == id);

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

    private BankStatementDto MapBankStatement(WyciagBankowy w, bool includeOperations)
    {
        var dto = new BankStatementDto
        {
            Id = w.Id,
            Number = w.NumerWyciagu,
            BankAccountNumber = w.RachunekBankowy?.NumerRachunku,
            BankAccountName = w.RachunekBankowy?.Nazwa,
            BankName = w.RachunekBankowy?.NazwaBanku,
            StatementDate = w.DataWyciagu,
            DateFrom = w.DataOd,
            DateTo = w.DataDo,
            OpeningBalance = w.SaldoPoczatkowe ?? 0,
            TotalDeposits = w.SumaWplat ?? 0,
            TotalWithdrawals = w.SumaWyplat ?? 0,
            ClosingBalance = w.SaldoKoncowe ?? 0,
            CurrencySymbol = w.RachunekBankowy?.Waluta?.Symbol,
            Status = w.Zamkniety == true ? "Closed" : "Open",
            IsClosed = w.Zamkniety ?? false,
            OperationCount = w.OperacjeBankowe?.Count ?? 0
        };

        if (includeOperations && w.OperacjeBankowe != null)
        {
            dto.Operations = w.OperacjeBankowe.Select(o => new BankStatementOperationDto
            {
                Id = o.Id,
                DocumentNumber = o.DokumentBankowy?.Numer?.PelnaSygnatura,
                Date = o.DataOperacji,
                ValueDate = o.DataKsiegowania,
                Description = o.Opis,
                OperationType = o.Typ == 0 ? "Deposit" : "Withdrawal",
                Amount = o.Kwota ?? 0,
                ContractorName = o.Podmiot?.NazwaSkrocona,
                ContractorAccountNumber = o.RachunekKontrahenta,
                SourceDocumentNumber = o.DokumentZrodlowy?.NumerWewnetrzny?.PelnaSygnatura,
                ReferenceNumber = o.NumerReferencyjny
            }).ToList();
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
            var sfera = _sferaService.GetSfera();

            // Get cash register balances
            var stanowiska = sfera.StanowiskaKasowe().Dane.Wszystkie()
                .Where(s => s.Aktywne == true)
                .ToList();

            var cashBalance = stanowiska.Sum(s => s.StanKasy ?? 0);

            // Get bank account balances
            var rachunki = sfera.RachunkiBankowe().Dane.Wszystkie()
                .Where(r => r.Aktywny == true)
                .ToList();

            var bankBalance = rachunki.Sum(r => r.StanRachunku ?? 0);

            var summary = new FinanceSummaryDto
            {
                TotalCashBalance = cashBalance,
                TotalBankBalance = bankBalance,
                TotalBalance = cashBalance + bankBalance,
                CashRegisterCount = stanowiska.Count,
                BankAccountCount = rachunki.Count,
                CashRegisterBalances = stanowiska.Select(s => new BalanceItemDto
                {
                    Name = s.Nazwa ?? s.Symbol ?? "Unknown",
                    Symbol = s.Symbol,
                    Balance = s.StanKasy ?? 0,
                    CurrencySymbol = s.Waluta?.Symbol ?? "PLN"
                }).ToList(),
                BankAccountBalances = rachunki.Select(r => new BalanceItemDto
                {
                    Name = r.Nazwa ?? r.Symbol ?? "Unknown",
                    Symbol = r.Symbol,
                    Balance = r.StanRachunku ?? 0,
                    CurrencySymbol = r.Waluta?.Symbol ?? "PLN"
                }).ToList()
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
