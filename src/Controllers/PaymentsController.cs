using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Requests;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using InsERT.Moria.Kasa;
using InsERT.Moria.Bank;
using InsERT.Moria.Rozrachunki;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Payments (KP, KW, BP, BW) and Receivables (Rozrachunki) management endpoints
/// </summary>
[ApiController]
[Route("api/payments")]
[Authorize]
[Tags("Payments")]
public class PaymentsController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(ISferaService sferaService, ILogger<PaymentsController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    #region Cash Operations (KP, KW)

    /// <summary>
    /// Get cash operations (operacje kasowe)
    /// </summary>
    [HttpGet("cash")]
    public ActionResult<PagedResponse<PaymentDto>> GetCashOperations(
        [FromQuery] PaymentType? type,
        [FromQuery] int? contractorId,
        [FromQuery] string? cashRegisterSymbol,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var sfera = _sferaService.GetSfera();
            var operacje = sfera.OperacjeKasowe();

            var dataQuery = operacje.Dane.Wszystkie();

            if (contractorId.HasValue)
            {
                dataQuery = dataQuery.Where(o => o.Podmiot != null && o.Podmiot.Id == contractorId.Value);
            }

            if (!string.IsNullOrEmpty(cashRegisterSymbol))
            {
                dataQuery = dataQuery.Where(o => o.Stanowisko != null && o.Stanowisko.Symbol == cashRegisterSymbol);
            }

            if (dateFrom.HasValue)
            {
                dataQuery = dataQuery.Where(o => o.DataUtworzenia >= dateFrom.Value);
            }

            if (dateTo.HasValue)
            {
                dataQuery = dataQuery.Where(o => o.DataUtworzenia <= dateTo.Value);
            }

            // Filter by type (KP = income, KW = expense) based on Rodzaj
            if (type.HasValue)
            {
                if (type.Value == PaymentType.KP)
                {
                    dataQuery = dataQuery.Where(o => o.Rodzaj != null && o.Rodzaj.Typ == (int)TypOperacjiKasowejEnum.Wplyw);
                }
                else if (type.Value == PaymentType.KW)
                {
                    dataQuery = dataQuery.Where(o => o.Rodzaj != null && o.Rodzaj.Typ == (int)TypOperacjiKasowejEnum.Wyplyw);
                }
            }

            var totalCount = dataQuery.Count();
            var items = dataQuery
                .OrderByDescending(o => o.DataUtworzenia)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var response = new PagedResponse<PaymentDto>
            {
                Data = items.Select(o => MapCashOperationToDto(o)).ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cash operations");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving cash operations", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get cash operation by ID
    /// </summary>
    [HttpGet("cash/{id}")]
    public ActionResult<ApiResponse<PaymentDto>> GetCashOperation(int id)
    {
        try
        {
            var sfera = _sferaService.GetSfera();
            var operacje = sfera.OperacjeKasowe();

            var operacja = operacje.Dane.Wszystkie().FirstOrDefault(o => o.Id == id);
            if (operacja == null)
            {
                return NotFound(ApiResponse<PaymentDto>.Error($"Cash operation with ID {id} not found"));
            }

            return Ok(ApiResponse<PaymentDto>.Ok(MapCashOperationToDto(operacja)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cash operation {Id}", id);
            return StatusCode(500, ApiResponse<PaymentDto>.Error("Error retrieving cash operation", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create KP (cash receipt - wpłata gotówkowa)
    /// </summary>
    [HttpPost("cash/kp")]
    public ActionResult<ApiResponse<PaymentDto>> CreateKP([FromBody] CreatePaymentRequest request)
    {
        return CreateCashOperation(request, TypOperacjiKasowejEnum.Wplyw);
    }

    /// <summary>
    /// Create KW (cash disbursement - wypłata gotówkowa)
    /// </summary>
    [HttpPost("cash/kw")]
    public ActionResult<ApiResponse<PaymentDto>> CreateKW([FromBody] CreatePaymentRequest request)
    {
        return CreateCashOperation(request, TypOperacjiKasowejEnum.Wyplyw);
    }

    private ActionResult<ApiResponse<PaymentDto>> CreateCashOperation(CreatePaymentRequest request, TypOperacjiKasowejEnum typ)
    {
        try
        {
            var sfera = _sferaService.GetSfera();
            var operacje = sfera.OperacjeKasowe();

            using (var operacja = operacje.Utworz())
            {
                // Set cash register (stanowisko kasowe)
                StanowiskoKasowe? stanowisko = null;
                if (request.CashRegisterId.HasValue)
                {
                    stanowisko = sfera.StanowiskaKasowe().Dane.Wszystkie()
                        .FirstOrDefault(s => s.Id == request.CashRegisterId.Value);
                }
                else if (!string.IsNullOrEmpty(request.CashRegisterSymbol))
                {
                    stanowisko = sfera.StanowiskaKasowe().Dane.Wszystkie()
                        .FirstOrDefault(s => s.Symbol == request.CashRegisterSymbol);
                }
                else
                {
                    // Get default cash register
                    stanowisko = sfera.StanowiskaKasowe().Dane.Wszystkie().FirstOrDefault();
                }

                if (stanowisko != null)
                {
                    operacja.Dane.Stanowisko = stanowisko;
                }

                // Set contractor
                if (request.ContractorId.HasValue)
                {
                    var podmiot = sfera.Podmioty().Dane.Wszystkie()
                        .FirstOrDefault(p => p.Id == request.ContractorId.Value);
                    if (podmiot != null)
                    {
                        operacja.UstawPodmiot(podmiot);
                    }
                }
                else if (!string.IsNullOrEmpty(request.ContractorNIP))
                {
                    var podmiot = sfera.Podmioty().Dane.Pierwszy(p => p.NIP == request.ContractorNIP);
                    if (podmiot != null)
                    {
                        operacja.UstawPodmiot(podmiot);
                    }
                }

                // Set operation type (Rodzaj)
                var rodzaje = sfera.RodzajeOperacjiKasowych().Dane.Wszystkie()
                    .Where(r => r.Typ == (int)typ);
                var rodzaj = rodzaje.FirstOrDefault();
                if (rodzaj != null)
                {
                    operacja.Dane.Rodzaj = rodzaj;
                }

                // Set amount - cash operations typically handle amount through the form of payment
                // The amount is set via related elements or directly if the property exists

                // Set description
                if (!string.IsNullOrEmpty(request.Title))
                {
                    operacja.Dane.Opis = request.Title;
                }

                // Set payment date
                if (request.PaymentDate.HasValue)
                {
                    operacja.Dane.DataUtworzenia = request.PaymentDate.Value;
                }

                if (operacja.Zapisz())
                {
                    _logger.LogInformation("Created cash operation {Type} with ID {Id}",
                        typ == TypOperacjiKasowejEnum.Wplyw ? "KP" : "KW",
                        operacja.Dane.Id);

                    return CreatedAtAction(
                        nameof(GetCashOperation),
                        new { id = operacja.Dane.Id },
                        ApiResponse<PaymentDto>.Ok(MapCashOperationToDto(operacja.Dane),
                            $"Cash operation {(typ == TypOperacjiKasowejEnum.Wplyw ? "KP" : "KW")} created successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(operacja);
                    return BadRequest(ApiResponse<PaymentDto>.Error("Failed to create cash operation", errors));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating cash operation");
            return StatusCode(500, ApiResponse<PaymentDto>.Error("Error creating cash operation", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Bank Operations (BP, BW)

    /// <summary>
    /// Get bank operations (operacje bankowe)
    /// </summary>
    [HttpGet("bank")]
    public ActionResult<PagedResponse<PaymentDto>> GetBankOperations(
        [FromQuery] PaymentType? type,
        [FromQuery] int? contractorId,
        [FromQuery] string? bankAccountSymbol,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var sfera = _sferaService.GetSfera();
            var operacje = sfera.OperacjeBankowe();

            var dataQuery = operacje.Dane.Wszystkie();

            if (contractorId.HasValue)
            {
                dataQuery = dataQuery.Where(o => o.Podmiot != null && o.Podmiot.Id == contractorId.Value);
            }

            if (!string.IsNullOrEmpty(bankAccountSymbol))
            {
                dataQuery = dataQuery.Where(o => o.Rachunek != null && o.Rachunek.Symbol == bankAccountSymbol);
            }

            if (dateFrom.HasValue)
            {
                dataQuery = dataQuery.Where(o => o.DataEfektywna >= dateFrom.Value);
            }

            if (dateTo.HasValue)
            {
                dataQuery = dataQuery.Where(o => o.DataEfektywna <= dateTo.Value);
            }

            // Filter by type (BP = income, BW = expense) based on RodzajOperacji.Typ
            if (type.HasValue)
            {
                if (type.Value == PaymentType.BP)
                {
                    dataQuery = dataQuery.Where(o => o.RodzajOperacji != null &&
                        o.RodzajOperacji.Typ == (int)TypOperacjiBankowejEnum.Przelew &&
                        o.Kwota > 0);
                }
                else if (type.Value == PaymentType.BW)
                {
                    dataQuery = dataQuery.Where(o => o.RodzajOperacji != null &&
                        o.RodzajOperacji.Typ == (int)TypOperacjiBankowejEnum.Przelew &&
                        o.Kwota < 0);
                }
            }

            var totalCount = dataQuery.Count();
            var items = dataQuery
                .OrderByDescending(o => o.DataEfektywna)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var response = new PagedResponse<PaymentDto>
            {
                Data = items.Select(o => MapBankOperationToDto(o)).ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bank operations");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving bank operations", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get bank operation by ID
    /// </summary>
    [HttpGet("bank/{id}")]
    public ActionResult<ApiResponse<PaymentDto>> GetBankOperation(int id)
    {
        try
        {
            var sfera = _sferaService.GetSfera();
            var operacje = sfera.OperacjeBankowe();

            var operacja = operacje.Dane.Wszystkie().FirstOrDefault(o => o.Id == id);
            if (operacja == null)
            {
                return NotFound(ApiResponse<PaymentDto>.Error($"Bank operation with ID {id} not found"));
            }

            return Ok(ApiResponse<PaymentDto>.Ok(MapBankOperationToDto(operacja)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bank operation {Id}", id);
            return StatusCode(500, ApiResponse<PaymentDto>.Error("Error retrieving bank operation", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create BP (bank receipt - wpływ bankowy)
    /// </summary>
    [HttpPost("bank/bp")]
    public ActionResult<ApiResponse<PaymentDto>> CreateBP([FromBody] CreatePaymentRequest request)
    {
        return CreateBankOperation(request, true);
    }

    /// <summary>
    /// Create BW (bank disbursement - wypłata bankowa)
    /// </summary>
    [HttpPost("bank/bw")]
    public ActionResult<ApiResponse<PaymentDto>> CreateBW([FromBody] CreatePaymentRequest request)
    {
        return CreateBankOperation(request, false);
    }

    private ActionResult<ApiResponse<PaymentDto>> CreateBankOperation(CreatePaymentRequest request, bool isIncome)
    {
        try
        {
            var sfera = _sferaService.GetSfera();
            var operacje = sfera.OperacjeBankowe();

            using (var operacja = operacje.Utworz())
            {
                // Set bank account
                RachunekBankowy? rachunek = null;
                if (request.BankAccountId.HasValue)
                {
                    rachunek = sfera.RachunkiBankowe().Dane.Wszystkie()
                        .FirstOrDefault(r => r.Id == request.BankAccountId.Value);
                }
                else if (!string.IsNullOrEmpty(request.BankAccountSymbol))
                {
                    rachunek = sfera.RachunkiBankowe().Dane.Wszystkie()
                        .FirstOrDefault(r => r.Symbol == request.BankAccountSymbol);
                }
                else
                {
                    // Get default bank account
                    rachunek = sfera.RachunkiBankowe().Dane.Wszystkie().FirstOrDefault();
                }

                if (rachunek != null)
                {
                    operacja.Dane.Rachunek = rachunek;
                }

                // Set contractor
                if (request.ContractorId.HasValue)
                {
                    var podmiot = sfera.Podmioty().Dane.Wszystkie()
                        .FirstOrDefault(p => p.Id == request.ContractorId.Value);
                    if (podmiot != null)
                    {
                        operacja.Dane.Podmiot = podmiot;
                    }
                }
                else if (!string.IsNullOrEmpty(request.ContractorNIP))
                {
                    var podmiot = sfera.Podmioty().Dane.Pierwszy(p => p.NIP == request.ContractorNIP);
                    if (podmiot != null)
                    {
                        operacja.Dane.Podmiot = podmiot;
                    }
                }

                // Set operation type (Rodzaj)
                var rodzaje = sfera.RodzajeOperacjiBankowych().Dane.Wszystkie();
                var rodzaj = rodzaje.FirstOrDefault(r => r.Typ == (int)TypOperacjiBankowejEnum.Przelew);
                if (rodzaj != null)
                {
                    operacja.Dane.RodzajOperacji = rodzaj;
                }

                // Set amount (positive for income, negative for expense)
                operacja.Dane.Kwota = isIncome ? request.Amount : -request.Amount;

                // Set description
                if (!string.IsNullOrEmpty(request.Title))
                {
                    operacja.Dane.Opis = request.Title;
                }

                // Set payment date
                if (request.PaymentDate.HasValue)
                {
                    operacja.Dane.DataEfektywna = request.PaymentDate.Value;
                }

                if (operacja.Zapisz())
                {
                    _logger.LogInformation("Created bank operation {Type} with ID {Id}",
                        isIncome ? "BP" : "BW",
                        operacja.Dane.Id);

                    return CreatedAtAction(
                        nameof(GetBankOperation),
                        new { id = operacja.Dane.Id },
                        ApiResponse<PaymentDto>.Ok(MapBankOperationToDto(operacja.Dane),
                            $"Bank operation {(isIncome ? "BP" : "BW")} created successfully"));
                }
                else
                {
                    var errors = GetBusinessObjectErrors(operacja);
                    return BadRequest(ApiResponse<PaymentDto>.Error("Failed to create bank operation", errors));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating bank operation");
            return StatusCode(500, ApiResponse<PaymentDto>.Error("Error creating bank operation", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Receivables (Rozrachunki)

    /// <summary>
    /// Get receivables/payables (rozrachunki)
    /// </summary>
    [HttpGet("receivables")]
    public ActionResult<PagedResponse<ReceivableDto>> GetReceivables(
        [FromQuery] int? contractorId,
        [FromQuery] ReceivableType? type,
        [FromQuery] ReceivableStatus? status,
        [FromQuery] DateTime? dueDateFrom,
        [FromQuery] DateTime? dueDateTo,
        [FromQuery] bool? overdue,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var sfera = _sferaService.GetSfera();
            var rozrachunki = sfera.Rozrachunki();

            var dataQuery = rozrachunki.Dane.Wszystkie();

            if (contractorId.HasValue)
            {
                dataQuery = dataQuery.Where(r => r.Podmiot != null && r.Podmiot.Id == contractorId.Value);
            }

            // Filter by type (receivable = Naleznosc, payable = Zobowiazanie)
            if (type.HasValue)
            {
                if (type.Value == ReceivableType.Receivable)
                {
                    dataQuery = dataQuery.Where(r => r.Typ == (int)TypRozrachunku.Naleznosc);
                }
                else
                {
                    dataQuery = dataQuery.Where(r => r.Typ == (int)TypRozrachunku.Zobowiazanie);
                }
            }

            // Filter by status
            if (status.HasValue)
            {
                if (status.Value == ReceivableStatus.Settled)
                {
                    dataQuery = dataQuery.Where(r => r.KwotaDoRozliczenia == 0);
                }
                else if (status.Value == ReceivableStatus.Unsettled)
                {
                    dataQuery = dataQuery.Where(r => r.KwotaDoRozliczenia == r.Kwota);
                }
                else // PartiallySettled
                {
                    dataQuery = dataQuery.Where(r => r.KwotaDoRozliczenia > 0 && r.KwotaDoRozliczenia < r.Kwota);
                }
            }

            if (dueDateFrom.HasValue)
            {
                dataQuery = dataQuery.Where(r => r.DataPlatnosci >= dueDateFrom.Value);
            }

            if (dueDateTo.HasValue)
            {
                dataQuery = dataQuery.Where(r => r.DataPlatnosci <= dueDateTo.Value);
            }

            if (overdue.HasValue && overdue.Value)
            {
                dataQuery = dataQuery.Where(r => r.DataPlatnosci < DateTime.Today && r.KwotaDoRozliczenia > 0);
            }

            var totalCount = dataQuery.Count();
            var items = dataQuery
                .OrderByDescending(r => r.DataWystawienia)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var response = new PagedResponse<ReceivableDto>
            {
                Data = items.Select(MapReceivableToDto).ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting receivables");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving receivables", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get receivable by ID
    /// </summary>
    [HttpGet("receivables/{id}")]
    public ActionResult<ApiResponse<ReceivableDto>> GetReceivable(int id)
    {
        try
        {
            var sfera = _sferaService.GetSfera();
            var rozrachunki = sfera.Rozrachunki();

            var rozrachunek = rozrachunki.Dane.Wszystkie().FirstOrDefault(r => r.Id == id);
            if (rozrachunek == null)
            {
                return NotFound(ApiResponse<ReceivableDto>.Error($"Receivable with ID {id} not found"));
            }

            return Ok(ApiResponse<ReceivableDto>.Ok(MapReceivableToDto(rozrachunek)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting receivable {Id}", id);
            return StatusCode(500, ApiResponse<ReceivableDto>.Error("Error retrieving receivable", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get contractor balance summary
    /// </summary>
    [HttpGet("balance/{contractorId}")]
    public ActionResult<ApiResponse<ContractorBalanceDto>> GetContractorBalance(int contractorId)
    {
        try
        {
            var sfera = _sferaService.GetSfera();
            var rozrachunki = sfera.Rozrachunki();
            var kontrahent = sfera.Podmioty().Dane.Wszystkie().FirstOrDefault(p => p.Id == contractorId);

            if (kontrahent == null)
            {
                return NotFound(ApiResponse<ContractorBalanceDto>.Error($"Contractor with ID {contractorId} not found"));
            }

            var allReceivables = rozrachunki.Dane.Wszystkie()
                .Where(r => r.Podmiot != null && r.Podmiot.Id == contractorId)
                .ToList();

            var today = DateTime.Today;
            var receivables = allReceivables.Where(r => r.Typ == (int)TypRozrachunku.Naleznosc).ToList();
            var payables = allReceivables.Where(r => r.Typ == (int)TypRozrachunku.Zobowiazanie).ToList();

            var balance = new ContractorBalanceDto
            {
                ContractorId = contractorId,
                ContractorName = kontrahent.NazwaSkrocona,
                ContractorNIP = kontrahent.NIP,
                TotalReceivables = receivables.Sum(r => r.KwotaDoRozliczenia),
                TotalPayables = payables.Sum(r => r.KwotaDoRozliczenia),
                OverdueReceivables = receivables
                    .Where(r => r.DataPlatnosci < today && r.KwotaDoRozliczenia > 0)
                    .Sum(r => r.KwotaDoRozliczenia),
                OverduePayables = payables
                    .Where(r => r.DataPlatnosci < today && r.KwotaDoRozliczenia > 0)
                    .Sum(r => r.KwotaDoRozliczenia),
                OpenReceivablesCount = receivables.Count(r => r.KwotaDoRozliczenia > 0),
                OpenPayablesCount = payables.Count(r => r.KwotaDoRozliczenia > 0)
            };

            balance.Balance = balance.TotalReceivables - balance.TotalPayables;

            return Ok(ApiResponse<ContractorBalanceDto>.Ok(balance));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting contractor balance for {ContractorId}", contractorId);
            return StatusCode(500, ApiResponse<ContractorBalanceDto>.Error("Error retrieving contractor balance", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get overdue receivables summary
    /// </summary>
    [HttpGet("receivables/overdue")]
    public ActionResult<PagedResponse<ReceivableDto>> GetOverdueReceivables(
        [FromQuery] int? contractorId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var sfera = _sferaService.GetSfera();
            var rozrachunki = sfera.Rozrachunki();

            var today = DateTime.Today;
            var dataQuery = rozrachunki.Dane.Wszystkie()
                .Where(r => r.DataPlatnosci < today && r.KwotaDoRozliczenia > 0);

            if (contractorId.HasValue)
            {
                dataQuery = dataQuery.Where(r => r.Podmiot != null && r.Podmiot.Id == contractorId.Value);
            }

            var totalCount = dataQuery.Count();
            var items = dataQuery
                .OrderBy(r => r.DataPlatnosci) // Oldest first
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var response = new PagedResponse<ReceivableDto>
            {
                Data = items.Select(MapReceivableToDto).ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting overdue receivables");
            return StatusCode(500, ApiResponse<object>.Error("Error retrieving overdue receivables", new List<string> { ex.Message }));
        }
    }

    #endregion

    #region Helpers

    private PaymentDto MapCashOperationToDto(OperacjaKasowa operacja)
    {
        var isIncome = operacja.Rodzaj?.Typ == (int)TypOperacjiKasowejEnum.Wplyw;

        return new PaymentDto
        {
            Id = operacja.Id,
            Number = operacja.Numer?.ToString(),
            FullNumber = operacja.DokumentKasowy?.NumerWewnetrzny?.PelnaSygnatura,
            Type = isIncome ? PaymentType.KP : PaymentType.KW,
            PaymentDate = operacja.DataUtworzenia,
            ContractorId = operacja.Podmiot?.Id,
            ContractorName = operacja.Podmiot?.NazwaSkrocona,
            ContractorNIP = operacja.Podmiot?.NIP,
            Amount = Math.Abs(operacja.Kwota),
            Currency = operacja.Waluta?.Symbol ?? "PLN",
            CashRegisterSymbol = operacja.Stanowisko?.Symbol,
            CashRegisterName = operacja.Stanowisko?.Nazwa,
            Title = operacja.Opis,
            CreatedAt = operacja.DataUtworzenia
        };
    }

    private PaymentDto MapBankOperationToDto(OperacjaBankowa operacja)
    {
        var isIncome = operacja.Kwota > 0;

        return new PaymentDto
        {
            Id = operacja.Id,
            Number = operacja.Numer?.ToString(),
            FullNumber = operacja.NumerWewnetrzny?.PelnaSygnatura,
            Type = isIncome ? PaymentType.BP : PaymentType.BW,
            PaymentDate = operacja.DataEfektywna,
            ContractorId = operacja.Podmiot?.Id,
            ContractorName = operacja.Podmiot?.NazwaSkrocona ?? operacja.NazwaKontrahentaZaimportowanego,
            ContractorNIP = operacja.Podmiot?.NIP,
            Amount = Math.Abs(operacja.Kwota),
            Currency = operacja.Waluta?.Symbol ?? "PLN",
            BankAccountSymbol = operacja.Rachunek?.Symbol,
            BankAccountName = operacja.Rachunek?.Nazwa,
            BankAccountNumber = operacja.Rachunek?.NumerRachunku,
            Title = operacja.Opis,
            CreatedAt = operacja.DataUtworzenia
        };
    }

    private ReceivableDto MapReceivableToDto(Rozrachunek rozrachunek)
    {
        var today = DateTime.Today;
        var daysOverdue = 0;

        if (rozrachunek.DataPlatnosci.HasValue && rozrachunek.DataPlatnosci.Value < today && rozrachunek.KwotaDoRozliczenia > 0)
        {
            daysOverdue = (int)(today - rozrachunek.DataPlatnosci.Value).TotalDays;
        }

        var status = ReceivableStatus.Unsettled;
        if (rozrachunek.KwotaDoRozliczenia == 0)
        {
            status = ReceivableStatus.Settled;
        }
        else if (rozrachunek.KwotaDoRozliczenia < rozrachunek.Kwota)
        {
            status = ReceivableStatus.PartiallySettled;
        }

        return new ReceivableDto
        {
            Id = rozrachunek.Id,
            Type = rozrachunek.Typ == (int)TypRozrachunku.Naleznosc ? ReceivableType.Receivable : ReceivableType.Payable,
            Status = status,
            DocumentId = rozrachunek.DokumentZrodlowy?.Id,
            DocumentNumber = rozrachunek.NumerDokumentuZrodlowego,
            ContractorId = rozrachunek.Podmiot?.Id,
            ContractorName = rozrachunek.Podmiot?.NazwaSkrocona,
            ContractorNIP = rozrachunek.Podmiot?.NIP,
            OriginalAmount = rozrachunek.Kwota,
            SettledAmount = rozrachunek.Kwota - rozrachunek.KwotaDoRozliczenia,
            RemainingAmount = rozrachunek.KwotaDoRozliczenia,
            Currency = rozrachunek.Waluta?.Symbol ?? "PLN",
            IssueDate = rozrachunek.DataWystawienia,
            DueDate = rozrachunek.DataPlatnosci,
            DaysOverdue = daysOverdue
        };
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

/// <summary>
/// Cash operation type enum
/// </summary>
public enum TypOperacjiKasowejEnum
{
    Wplyw = 1,
    Wyplyw = 2
}

/// <summary>
/// Bank operation type enum
/// </summary>
public enum TypOperacjiBankowejEnum
{
    Przelew = 1,
    Gotowka = 2,
    Karta = 3,
    PozostaleOperacje = 4,
    Transfer = 5,
    PrzelewPlatnoscPodzielona = 6,
    PrzelewPlatnoscPodzielonaWewnetrzna = 7
}

/// <summary>
/// Receivable type enum
/// </summary>
public enum TypRozrachunku
{
    Naleznosc = 1,
    Zobowiazanie = 2
}
