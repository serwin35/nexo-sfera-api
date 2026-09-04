using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Requests;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

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
    public async Task<ActionResult<PagedResponse<PaymentDto>>> GetCashOperations(
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
            var response = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var operacjeManager = _sferaService.GetManager("OperacjeKasowe");
                if (operacjeManager == null) return (PagedResponse<PaymentDto>?)null;

                var allOperations = DynamicPropertyHelper.SafeGetAll((object)operacjeManager);

                // Apply filters
                if (contractorId.HasValue)
                {
                    allOperations = allOperations.Where(o =>
                    {
                        var podmiot = DynamicPropertyHelper.GetProperty(o, "Podmiot");
                        return podmiot != null && DynamicPropertyHelper.GetInt(podmiot, "Id") == contractorId.Value;
                    }).ToList();
                }

                if (!string.IsNullOrEmpty(cashRegisterSymbol))
                {
                    allOperations = allOperations.Where(o =>
                    {
                        var stanowisko = DynamicPropertyHelper.GetProperty(o, "Stanowisko");
                        return stanowisko != null && DynamicPropertyHelper.GetString(stanowisko, "Symbol") == cashRegisterSymbol;
                    }).ToList();
                }

                if (dateFrom.HasValue)
                {
                    allOperations = allOperations.Where(o =>
                    {
                        var data = DynamicPropertyHelper.GetDateTime(o, "DataUtworzenia");
                        return data.HasValue && data.Value >= dateFrom.Value;
                    }).ToList();
                }

                if (dateTo.HasValue)
                {
                    allOperations = allOperations.Where(o =>
                    {
                        var data = DynamicPropertyHelper.GetDateTime(o, "DataUtworzenia");
                        return data.HasValue && data.Value <= dateTo.Value;
                    }).ToList();
                }

                // Filter by type (KP = income, KW = expense) based on Rodzaj
                if (type.HasValue)
                {
                    if (type.Value == PaymentType.KP)
                    {
                        allOperations = allOperations.Where(o =>
                        {
                            var rodzaj = DynamicPropertyHelper.GetProperty(o, "Rodzaj");
                            return rodzaj != null && DynamicPropertyHelper.GetInt(rodzaj, "Typ") == (int)TypOperacjiKasowejEnum.Wplyw;
                        }).ToList();
                    }
                    else if (type.Value == PaymentType.KW)
                    {
                        allOperations = allOperations.Where(o =>
                        {
                            var rodzaj = DynamicPropertyHelper.GetProperty(o, "Rodzaj");
                            return rodzaj != null && DynamicPropertyHelper.GetInt(rodzaj, "Typ") == (int)TypOperacjiKasowejEnum.Wyplyw;
                        }).ToList();
                    }
                }

                var totalCount = allOperations.Count;

                // Sort and paginate
                var items = allOperations
                    .OrderByDescending(o => DynamicPropertyHelper.GetDateTime(o, "DataUtworzenia") ?? DateTime.MinValue)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var mappedItems = new List<PaymentDto>();
                foreach (var o in items)
                {
                    mappedItems.Add(MapCashOperationToDto(o));
                }

                return new PagedResponse<PaymentDto>
                {
                    Data = mappedItems,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };
            });

            if (response == null) return StatusCode(500, ApiResponse<object>.Error("OperacjeKasowe manager not available"));
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
    public async Task<ActionResult<ApiResponse<PaymentDto>>> GetCashOperation(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var operacjeManager = _sferaService.GetManager("OperacjeKasowe");
                if (operacjeManager == null) return (Found: false, ManagerMissing: true, Dto: (PaymentDto?)null);

                var allOperations = DynamicPropertyHelper.SafeGetAll((object)operacjeManager);
                var operacja = allOperations.FirstOrDefault(o => DynamicPropertyHelper.GetId(o) == id);

                if (operacja == null) return (Found: false, ManagerMissing: false, Dto: (PaymentDto?)null);

                return (Found: true, ManagerMissing: false, Dto: (PaymentDto?)MapCashOperationToDto(operacja));
            });

            if (result.ManagerMissing) return StatusCode(500, ApiResponse<object>.Error("OperacjeKasowe manager not available"));
            if (!result.Found) return NotFound(ApiResponse<PaymentDto>.Error($"Cash operation with ID {id} not found"));
            return Ok(ApiResponse<PaymentDto>.Ok(result.Dto!));
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
    public async Task<ActionResult<ApiResponse<PaymentDto>>> CreateKP([FromBody] CreatePaymentRequest request)
    {
        return await CreateCashOperationAsync(request, TypOperacjiKasowejEnum.Wplyw);
    }

    /// <summary>
    /// Create KW (cash disbursement - wypłata gotówkowa)
    /// </summary>
    [HttpPost("cash/kw")]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> CreateKW([FromBody] CreatePaymentRequest request)
    {
        return await CreateCashOperationAsync(request, TypOperacjiKasowejEnum.Wyplyw);
    }

    /// <remarks>
    /// IMPORTANT: This method uses ExecuteWithLockAsync for thread safety with EF6.
    /// </remarks>
    private async Task<ActionResult<ApiResponse<PaymentDto>>> CreateCashOperationAsync(CreatePaymentRequest request, TypOperacjiKasowejEnum typ)
    {
        try
        {
            // Use thread-safe execution - EF6 is NOT thread-safe
            var result = await _sferaService.ExecuteWithLockAsync<(bool Success, PaymentDto? Data, string Message, List<string> Errors)>(() =>
            {
                var operacjeManager = _sferaService.GetManager("OperacjeKasowe");
                if (operacjeManager == null)
                {
                    return (false, null, "OperacjeKasowe manager not available", new List<string>());
                }

                using (var operacja = operacjeManager.Utworz())
                {
                    dynamic dane = operacja.Dane;

                    // Set cash register (stanowisko kasowe)
                    dynamic? stanowisko = null;
                    var stanowiskaManager = _sferaService.GetManager("StanowiskaKasowe");
                    if (stanowiskaManager != null)
                    {
                        var stanowiska = DynamicPropertyHelper.SafeGetAll((object)stanowiskaManager);

                        if (request.CashRegisterId.HasValue)
                        {
                            stanowisko = stanowiska.FirstOrDefault(s => DynamicPropertyHelper.GetId(s) == request.CashRegisterId.Value);
                        }
                        else if (!string.IsNullOrEmpty(request.CashRegisterSymbol))
                        {
                            stanowisko = stanowiska.FirstOrDefault(s => DynamicPropertyHelper.GetString(s, "Symbol") == request.CashRegisterSymbol);
                        }
                        else
                        {
                            stanowisko = stanowiska.FirstOrDefault();
                        }
                    }

                    if (stanowisko != null)
                    {
                        dane.Stanowisko = stanowisko;
                    }

                    // Set contractor
                    if (request.ContractorId.HasValue || !string.IsNullOrEmpty(request.ContractorNIP))
                    {
                        var podmiotyManager = _sferaService.GetManager("Podmioty");
                        if (podmiotyManager != null)
                        {
                            var podmioty = DynamicPropertyHelper.SafeGetAll((object)podmiotyManager);

                            dynamic? podmiot = null;
                            if (request.ContractorId.HasValue)
                            {
                                podmiot = podmioty.FirstOrDefault(p => DynamicPropertyHelper.GetId(p) == request.ContractorId.Value);
                            }
                            else if (!string.IsNullOrEmpty(request.ContractorNIP))
                            {
                                podmiot = podmioty.FirstOrDefault(p => DynamicPropertyHelper.GetString(p, "NIP") == request.ContractorNIP);
                            }

                            if (podmiot != null)
                            {
                                try { operacja.UstawPodmiot(podmiot); } catch { /* Method may not exist */ }
                            }
                        }
                    }

                    // Set operation type (Rodzaj)
                    var rodzajeManager = _sferaService.GetManager("RodzajeOperacjiKasowych");
                    var rodzaje = rodzajeManager != null ? DynamicPropertyHelper.SafeGetAll((object)rodzajeManager) : new List<object>();
                    var rodzaj = rodzaje.FirstOrDefault(r => DynamicPropertyHelper.GetInt(r, "Typ") == (int)typ);

                    if (rodzaj != null)
                    {
                        dane.Rodzaj = rodzaj;
                    }

                    // Set description
                    if (!string.IsNullOrEmpty(request.Title))
                    {
                        dane.Opis = request.Title;
                    }

                    // Set payment date
                    if (request.PaymentDate.HasValue)
                    {
                        dane.DataUtworzenia = request.PaymentDate.Value;
                    }

                    if ((bool)operacja.Zapisz())
                    {
                        int newId = DynamicPropertyHelper.GetId(dane);
                        _logger.LogInformation("Created cash operation {Type} with ID {Id}",
                            typ == TypOperacjiKasowejEnum.Wplyw ? "KP" : "KW",
                            newId);

                        return (true, MapCashOperationToDto(dane),
                            $"Cash operation {(typ == TypOperacjiKasowejEnum.Wplyw ? "KP" : "KW")} created successfully",
                            new List<string>());
                    }
                    else
                    {
                        var errors = GetBusinessObjectErrors(operacja);
                        return (false, null, "Failed to create cash operation", errors);
                    }
                }
            });

            if (result.Success)
            {
                return CreatedAtAction(nameof(GetCashOperation), new { id = result.Data?.Id }, ApiResponse<PaymentDto>.Ok(result.Data!, result.Message));
            }
            else if (result.Errors.Any())
            {
                return BadRequest(ApiResponse<PaymentDto>.Error(result.Message, result.Errors));
            }
            else
            {
                return StatusCode(500, ApiResponse<PaymentDto>.Error(result.Message));
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
    public async Task<ActionResult<PagedResponse<PaymentDto>>> GetBankOperations(
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
            var response = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var operacjeManager = _sferaService.GetManager("OperacjeBankowe");
                if (operacjeManager == null) return (PagedResponse<PaymentDto>?)null;

                var allOperations = DynamicPropertyHelper.SafeGetAll((object)operacjeManager);

                // Apply filters
                if (contractorId.HasValue)
                {
                    allOperations = allOperations.Where(o =>
                    {
                        var podmiot = DynamicPropertyHelper.GetProperty(o, "Podmiot");
                        return podmiot != null && DynamicPropertyHelper.GetInt(podmiot, "Id") == contractorId.Value;
                    }).ToList();
                }

                if (!string.IsNullOrEmpty(bankAccountSymbol))
                {
                    allOperations = allOperations.Where(o =>
                    {
                        var rachunek = DynamicPropertyHelper.GetProperty(o, "Rachunek");
                        return rachunek != null && DynamicPropertyHelper.GetString(rachunek, "Symbol") == bankAccountSymbol;
                    }).ToList();
                }

                if (dateFrom.HasValue)
                {
                    allOperations = allOperations.Where(o =>
                    {
                        var data = DynamicPropertyHelper.GetDateTime(o, "DataEfektywna");
                        return data.HasValue && data.Value >= dateFrom.Value;
                    }).ToList();
                }

                if (dateTo.HasValue)
                {
                    allOperations = allOperations.Where(o =>
                    {
                        var data = DynamicPropertyHelper.GetDateTime(o, "DataEfektywna");
                        return data.HasValue && data.Value <= dateTo.Value;
                    }).ToList();
                }

                // Filter by type (BP = income, BW = expense) based on Kwota sign
                if (type.HasValue)
                {
                    if (type.Value == PaymentType.BP)
                    {
                        allOperations = allOperations.Where(o => DynamicPropertyHelper.GetDecimal(o, "Kwota") > 0).ToList();
                    }
                    else if (type.Value == PaymentType.BW)
                    {
                        allOperations = allOperations.Where(o => DynamicPropertyHelper.GetDecimal(o, "Kwota") < 0).ToList();
                    }
                }

                var totalCount = allOperations.Count;

                // Sort and paginate
                var items = allOperations
                    .OrderByDescending(o => DynamicPropertyHelper.GetDateTime(o, "DataEfektywna") ?? DateTime.MinValue)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var mappedItems = new List<PaymentDto>();
                foreach (var o in items)
                {
                    mappedItems.Add(MapBankOperationToDto(o));
                }

                return new PagedResponse<PaymentDto>
                {
                    Data = mappedItems,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };
            });

            if (response == null) return StatusCode(500, ApiResponse<object>.Error("OperacjeBankowe manager not available"));
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
    public async Task<ActionResult<ApiResponse<PaymentDto>>> GetBankOperation(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var operacjeManager = _sferaService.GetManager("OperacjeBankowe");
                if (operacjeManager == null) return (Found: false, ManagerMissing: true, Dto: (PaymentDto?)null);

                var allOperations = DynamicPropertyHelper.SafeGetAll((object)operacjeManager);
                var operacja = allOperations.FirstOrDefault(o => DynamicPropertyHelper.GetId(o) == id);

                if (operacja == null) return (Found: false, ManagerMissing: false, Dto: (PaymentDto?)null);

                return (Found: true, ManagerMissing: false, Dto: (PaymentDto?)MapBankOperationToDto(operacja));
            });

            if (result.ManagerMissing) return StatusCode(500, ApiResponse<PaymentDto>.Error("OperacjeBankowe manager not available"));
            if (!result.Found) return NotFound(ApiResponse<PaymentDto>.Error($"Bank operation with ID {id} not found"));
            return Ok(ApiResponse<PaymentDto>.Ok(result.Dto!));
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
    public async Task<ActionResult<ApiResponse<PaymentDto>>> CreateBP([FromBody] CreatePaymentRequest request)
    {
        return await CreateBankOperationAsync(request, true);
    }

    /// <summary>
    /// Create BW (bank disbursement - wypłata bankowa)
    /// </summary>
    [HttpPost("bank/bw")]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> CreateBW([FromBody] CreatePaymentRequest request)
    {
        return await CreateBankOperationAsync(request, false);
    }

    /// <remarks>
    /// IMPORTANT: This method uses ExecuteWithLockAsync for thread safety with EF6.
    /// </remarks>
    private async Task<ActionResult<ApiResponse<PaymentDto>>> CreateBankOperationAsync(CreatePaymentRequest request, bool isIncome)
    {
        try
        {
            // Use thread-safe execution - EF6 is NOT thread-safe
            var result = await _sferaService.ExecuteWithLockAsync<(bool Success, PaymentDto? Data, string Message, List<string> Errors)>(() =>
            {
                var operacjeManager = _sferaService.GetManager("OperacjeBankowe");
                if (operacjeManager == null)
                {
                    return (false, null, "OperacjeBankowe manager not available", new List<string>());
                }

                using (var operacja = operacjeManager.Utworz())
                {
                    dynamic dane = operacja.Dane;

                    // Set bank account
                    dynamic? rachunek = null;
                    var rachunkiManager = _sferaService.GetManager("RachunkiBankowe");
                    if (rachunkiManager != null)
                    {
                        var rachunki = DynamicPropertyHelper.SafeGetAll((object)rachunkiManager);

                        if (request.BankAccountId.HasValue)
                        {
                            rachunek = rachunki.FirstOrDefault(r => DynamicPropertyHelper.GetId(r) == request.BankAccountId.Value);
                        }
                        else if (!string.IsNullOrEmpty(request.BankAccountSymbol))
                        {
                            rachunek = rachunki.FirstOrDefault(r => DynamicPropertyHelper.GetString(r, "Symbol") == request.BankAccountSymbol);
                        }
                        else
                        {
                            rachunek = rachunki.FirstOrDefault();
                        }
                    }

                    if (rachunek != null)
                    {
                        dane.Rachunek = rachunek;
                    }

                    // Set contractor
                    if (request.ContractorId.HasValue || !string.IsNullOrEmpty(request.ContractorNIP))
                    {
                        var podmiotyManager = _sferaService.GetManager("Podmioty");
                        if (podmiotyManager != null)
                        {
                            var podmioty = DynamicPropertyHelper.SafeGetAll((object)podmiotyManager);

                            dynamic? podmiot = null;
                            if (request.ContractorId.HasValue)
                            {
                                podmiot = podmioty.FirstOrDefault(p => DynamicPropertyHelper.GetId(p) == request.ContractorId.Value);
                            }
                            else if (!string.IsNullOrEmpty(request.ContractorNIP))
                            {
                                podmiot = podmioty.FirstOrDefault(p => DynamicPropertyHelper.GetString(p, "NIP") == request.ContractorNIP);
                            }

                            if (podmiot != null)
                            {
                                dane.Podmiot = podmiot;
                            }
                        }
                    }

                    // Set operation type (Rodzaj)
                    var rodzajeManager = _sferaService.GetManager("RodzajeOperacjiBankowych");
                    var rodzaje = rodzajeManager != null ? DynamicPropertyHelper.SafeGetAll((object)rodzajeManager) : new List<object>();
                    var rodzaj = rodzaje.FirstOrDefault(r => DynamicPropertyHelper.GetInt(r, "Typ") == (int)TypOperacjiBankowejEnum.Przelew);

                    if (rodzaj != null)
                    {
                        dane.RodzajOperacji = rodzaj;
                    }

                    // Set amount (positive for income, negative for expense)
                    dane.Kwota = isIncome ? request.Amount : -request.Amount;

                    // Set description
                    if (!string.IsNullOrEmpty(request.Title))
                    {
                        dane.Opis = request.Title;
                    }

                    // Set payment date
                    if (request.PaymentDate.HasValue)
                    {
                        dane.DataEfektywna = request.PaymentDate.Value;
                    }

                    if ((bool)operacja.Zapisz())
                    {
                        int newId = DynamicPropertyHelper.GetId(dane);
                        _logger.LogInformation("Created bank operation {Type} with ID {Id}",
                            isIncome ? "BP" : "BW",
                            newId);

                        return (true, MapBankOperationToDto(dane),
                            $"Bank operation {(isIncome ? "BP" : "BW")} created successfully",
                            new List<string>());
                    }
                    else
                    {
                        var errors = GetBusinessObjectErrors(operacja);
                        return (false, null, "Failed to create bank operation", errors);
                    }
                }
            });

            if (result.Success)
            {
                return CreatedAtAction(nameof(GetBankOperation), new { id = result.Data?.Id }, ApiResponse<PaymentDto>.Ok(result.Data!, result.Message));
            }
            else if (result.Errors.Any())
            {
                return BadRequest(ApiResponse<PaymentDto>.Error(result.Message, result.Errors));
            }
            else
            {
                return StatusCode(500, ApiResponse<PaymentDto>.Error(result.Message));
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
    public async Task<ActionResult<PagedResponse<ReceivableDto>>> GetReceivables(
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
            var response = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var rozrachunkiManager = _sferaService.GetManager("Rozrachunki");
                if (rozrachunkiManager == null) return (PagedResponse<ReceivableDto>?)null;

                var allRozrachunki = DynamicPropertyHelper.SafeGetAll((object)rozrachunkiManager);

                // Apply filters
                if (contractorId.HasValue)
                {
                    allRozrachunki = allRozrachunki.Where(r =>
                    {
                        var podmiot = DynamicPropertyHelper.GetProperty(r, "Podmiot");
                        return podmiot != null && DynamicPropertyHelper.GetInt(podmiot, "Id") == contractorId.Value;
                    }).ToList();
                }

                // Filter by type (receivable = Naleznosc, payable = Zobowiazanie)
                if (type.HasValue)
                {
                    if (type.Value == ReceivableType.Receivable)
                    {
                        allRozrachunki = allRozrachunki.Where(r =>
                            DynamicPropertyHelper.GetInt(r, "Typ") == (int)TypRozrachunku.Naleznosc).ToList();
                    }
                    else
                    {
                        allRozrachunki = allRozrachunki.Where(r =>
                            DynamicPropertyHelper.GetInt(r, "Typ") == (int)TypRozrachunku.Zobowiazanie).ToList();
                    }
                }

                // Filter by status
                if (status.HasValue)
                {
                    if (status.Value == ReceivableStatus.Settled)
                    {
                        allRozrachunki = allRozrachunki.Where(r =>
                            DynamicPropertyHelper.GetDecimal(r, "KwotaPozostala") == 0).ToList();
                    }
                    else if (status.Value == ReceivableStatus.Unsettled)
                    {
                        allRozrachunki = allRozrachunki.Where(r =>
                        {
                            var doRozl = DynamicPropertyHelper.GetDecimal(r, "KwotaPozostala");
                            var kwota = DynamicPropertyHelper.GetDecimal(r, "Kwota");
                            return doRozl == kwota;
                        }).ToList();
                    }
                    else // PartiallySettled
                    {
                        allRozrachunki = allRozrachunki.Where(r =>
                        {
                            var doRozl = DynamicPropertyHelper.GetDecimal(r, "KwotaPozostala");
                            var kwota = DynamicPropertyHelper.GetDecimal(r, "Kwota");
                            return doRozl > 0 && doRozl < kwota;
                        }).ToList();
                    }
                }

                if (dueDateFrom.HasValue)
                {
                    allRozrachunki = allRozrachunki.Where(r =>
                    {
                        var data = DynamicPropertyHelper.GetDateTime(r, "TerminPlatnosci");
                        return data.HasValue && data.Value >= dueDateFrom.Value;
                    }).ToList();
                }

                if (dueDateTo.HasValue)
                {
                    allRozrachunki = allRozrachunki.Where(r =>
                    {
                        var data = DynamicPropertyHelper.GetDateTime(r, "TerminPlatnosci");
                        return data.HasValue && data.Value <= dueDateTo.Value;
                    }).ToList();
                }

                if (overdue.HasValue && overdue.Value)
                {
                    var today = DateTime.Today;
                    allRozrachunki = allRozrachunki.Where(r =>
                    {
                        var data = DynamicPropertyHelper.GetDateTime(r, "TerminPlatnosci");
                        var doRozl = DynamicPropertyHelper.GetDecimal(r, "KwotaPozostala");
                        return data.HasValue && data.Value < today && doRozl > 0;
                    }).ToList();
                }

                var totalCount = allRozrachunki.Count;

                // Sort and paginate
                var items = allRozrachunki
                    .OrderByDescending(r => DynamicPropertyHelper.GetDateTime(r, "DataDokumentuZrodlowego") ?? DynamicPropertyHelper.GetDateTime(r, "DataPowstania") ?? DateTime.MinValue)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var mappedItems = new List<ReceivableDto>();
                foreach (var item in items)
                {
                    mappedItems.Add(MapReceivableToDto(item));
                }

                return new PagedResponse<ReceivableDto>
                {
                    Data = mappedItems,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };
            });

            if (response == null) return StatusCode(500, ApiResponse<object>.Error("Rozrachunki manager not available"));
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
    public async Task<ActionResult<ApiResponse<ReceivableDto>>> GetReceivable(int id)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var rozrachunkiManager = _sferaService.GetManager("Rozrachunki");
                if (rozrachunkiManager == null) return (Found: false, ManagerMissing: true, Dto: (ReceivableDto?)null);

                var allRozrachunki = DynamicPropertyHelper.SafeGetAll((object)rozrachunkiManager);
                var rozrachunek = allRozrachunki.FirstOrDefault(r => DynamicPropertyHelper.GetId(r) == id);

                if (rozrachunek == null) return (Found: false, ManagerMissing: false, Dto: (ReceivableDto?)null);

                return (Found: true, ManagerMissing: false, Dto: (ReceivableDto?)MapReceivableToDto(rozrachunek));
            });

            if (result.ManagerMissing) return StatusCode(500, ApiResponse<object>.Error("Rozrachunki manager not available"));
            if (!result.Found) return NotFound(ApiResponse<ReceivableDto>.Error($"Receivable with ID {id} not found"));
            return Ok(ApiResponse<ReceivableDto>.Ok(result.Dto!));
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
    /// <summary>
    /// Get the settlements (rozrachunki) linked to a Subiekt document - tells whether an invoice is paid,
    /// how much remains and when it is due. Returns an empty list for documents that do not create settlements
    /// (e.g. cash-paid receipts, warehouse documents).
    /// </summary>
    [HttpGet("receivables/by-document/{documentId}")]
    public async Task<ActionResult<ApiResponse<List<ReceivableDto>>>> GetReceivablesByDocument(int documentId)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var rozrachunkiManager = _sferaService.GetManager("Rozrachunki");
                if (rozrachunkiManager == null) return (List<ReceivableDto>?)null;

                var items = new List<ReceivableDto>();
                foreach (var r in DynamicPropertyHelper.SafeGetAll((object)rozrachunkiManager))
                {
                    var dokument = DynamicPropertyHelper.GetProperty(r, "Dokument");
                    if (dokument == null) continue;
                    if (DynamicPropertyHelper.GetNullableInt(dokument, "Id") != documentId) continue;
                    items.Add(MapReceivableToDto(r));
                }
                return items;
            });

            if (result == null) return StatusCode(500, ApiResponse<List<ReceivableDto>>.Error("Rozrachunki manager not available"));
            return Ok(ApiResponse<List<ReceivableDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting settlements for document {DocumentId}", documentId);
            return StatusCode(500, ApiResponse<List<ReceivableDto>>.Error("Error retrieving document settlements", new List<string> { ex.Message }));
        }
    }

    [HttpGet("balance/{contractorId}")]
    public async Task<ActionResult<ApiResponse<ContractorBalanceDto>>> GetContractorBalance(int contractorId)
    {
        try
        {
            var result = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var rozrachunkiManager = _sferaService.GetManager("Rozrachunki");
                var podmiotyManager = _sferaService.GetManager("Podmioty");
                if (rozrachunkiManager == null || podmiotyManager == null)
                    return (Found: false, ManagerMissing: true, Dto: (ContractorBalanceDto?)null);

                var podmioty = DynamicPropertyHelper.SafeGetAll((object)podmiotyManager);
                var kontrahent = podmioty.FirstOrDefault(p => DynamicPropertyHelper.GetId(p) == contractorId);

                if (kontrahent == null)
                    return (Found: false, ManagerMissing: false, Dto: (ContractorBalanceDto?)null);

                var allRozrachunki = DynamicPropertyHelper.SafeGetAll((object)rozrachunkiManager)
                    .Where(r =>
                    {
                        var podmiot = DynamicPropertyHelper.GetProperty(r, "Podmiot");
                        return podmiot != null && DynamicPropertyHelper.GetInt(podmiot, "Id") == contractorId;
                    })
                    .ToList();

                var today = DateTime.Today;
                var receivables = new List<object>();
                var payables = new List<object>();
                foreach (var r in allRozrachunki)
                {
                    var typ = DynamicPropertyHelper.GetInt(r, "Typ");
                    if (typ == (int)TypRozrachunku.Naleznosc)
                    {
                        receivables.Add(r);
                    }
                    else if (typ == (int)TypRozrachunku.Zobowiazanie)
                    {
                        payables.Add(r);
                    }
                }

                decimal totalReceivables = 0;
                decimal overdueReceivables = 0;
                int openReceivablesCount = 0;
                foreach (var r in receivables)
                {
                    var doRozl = DynamicPropertyHelper.GetDecimal(r, "KwotaPozostala");
                    totalReceivables += doRozl;
                    if (doRozl > 0)
                    {
                        openReceivablesCount++;
                        var data = DynamicPropertyHelper.GetDateTime(r, "TerminPlatnosci");
                        if (data.HasValue && data.Value < today)
                        {
                            overdueReceivables += doRozl;
                        }
                    }
                }

                decimal totalPayables = 0;
                decimal overduePayables = 0;
                int openPayablesCount = 0;
                foreach (var r in payables)
                {
                    var doRozl = DynamicPropertyHelper.GetDecimal(r, "KwotaPozostala");
                    totalPayables += doRozl;
                    if (doRozl > 0)
                    {
                        openPayablesCount++;
                        var data = DynamicPropertyHelper.GetDateTime(r, "TerminPlatnosci");
                        if (data.HasValue && data.Value < today)
                        {
                            overduePayables += doRozl;
                        }
                    }
                }

                var balance = new ContractorBalanceDto
                {
                    ContractorId = contractorId,
                    ContractorName = DynamicPropertyHelper.GetString(kontrahent, "NazwaSkrocona"),
                    ContractorNIP = DynamicPropertyHelper.GetString(kontrahent, "NIP"),
                    TotalReceivables = totalReceivables,
                    TotalPayables = totalPayables,
                    OverdueReceivables = overdueReceivables,
                    OverduePayables = overduePayables,
                    OpenReceivablesCount = openReceivablesCount,
                    OpenPayablesCount = openPayablesCount
                };
                balance.Balance = balance.TotalReceivables - balance.TotalPayables;

                return (Found: true, ManagerMissing: false, Dto: (ContractorBalanceDto?)balance);
            });

            if (result.ManagerMissing)
                return StatusCode(500, ApiResponse<ContractorBalanceDto>.Error("Required managers not available"));
            if (!result.Found)
                return NotFound(ApiResponse<ContractorBalanceDto>.Error($"Contractor with ID {contractorId} not found"));
            return Ok(ApiResponse<ContractorBalanceDto>.Ok(result.Dto!));
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
    public async Task<ActionResult<PagedResponse<ReceivableDto>>> GetOverdueReceivables(
        [FromQuery] int? contractorId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var response = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var rozrachunkiManager = _sferaService.GetManager("Rozrachunki");
                if (rozrachunkiManager == null) return (PagedResponse<ReceivableDto>?)null;

                var today = DateTime.Today;
                var allRozrachunki = DynamicPropertyHelper.SafeGetAll((object)rozrachunkiManager)
                    .Where(r =>
                    {
                        var data = DynamicPropertyHelper.GetDateTime(r, "TerminPlatnosci");
                        var doRozl = DynamicPropertyHelper.GetDecimal(r, "KwotaPozostala");
                        return data.HasValue && data.Value < today && doRozl > 0;
                    })
                    .ToList();

                if (contractorId.HasValue)
                {
                    allRozrachunki = allRozrachunki.Where(r =>
                    {
                        var podmiot = DynamicPropertyHelper.GetProperty(r, "Podmiot");
                        return podmiot != null && DynamicPropertyHelper.GetInt(podmiot, "Id") == contractorId.Value;
                    }).ToList();
                }

                var totalCount = allRozrachunki.Count;

                // Sort by oldest first and paginate
                var items = allRozrachunki
                    .OrderBy(r => DynamicPropertyHelper.GetDateTime(r, "TerminPlatnosci") ?? DateTime.MaxValue)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var mappedItems = new List<ReceivableDto>();
                foreach (var item in items)
                {
                    mappedItems.Add(MapReceivableToDto(item));
                }

                return new PagedResponse<ReceivableDto>
                {
                    Data = mappedItems,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };
            });

            if (response == null) return StatusCode(500, ApiResponse<object>.Error("Rozrachunki manager not available"));
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

    private static PaymentDto MapCashOperationToDto(dynamic operacja)
    {
        try
        {
            var rodzaj = DynamicPropertyHelper.GetProperty(operacja, "Rodzaj");
            var isIncome = rodzaj != null && DynamicPropertyHelper.GetInt(rodzaj, "Typ") == (int)TypOperacjiKasowejEnum.Wplyw;

            return new PaymentDto
            {
                Id = DynamicPropertyHelper.GetId(operacja),
                Number = DynamicPropertyHelper.GetString(operacja, "Numer"),
                FullNumber = DynamicPropertyHelper.GetNestedString(operacja, "DokumentKasowy", "NumerWewnetrzny", "PelnaSygnatura"),
                Type = isIncome ? PaymentType.KP : PaymentType.KW,
                PaymentDate = DynamicPropertyHelper.GetDateTime(operacja, "DataUtworzenia"),
                ContractorId = DynamicPropertyHelper.GetNullableInt(operacja, "Podmiot", "Id"),
                ContractorName = DynamicPropertyHelper.GetString(operacja, "Podmiot", "NazwaSkrocona"),
                ContractorNIP = DynamicPropertyHelper.GetString(operacja, "Podmiot", "NIP"),
                Amount = Math.Abs(DynamicPropertyHelper.GetDecimal(operacja, "Kwota")),
                Currency = DynamicPropertyHelper.GetString(operacja, "Waluta", "Symbol") ?? "PLN",
                CashRegisterSymbol = DynamicPropertyHelper.GetString(operacja, "Stanowisko", "Symbol"),
                CashRegisterName = DynamicPropertyHelper.GetString(operacja, "Stanowisko", "Nazwa"),
                Title = DynamicPropertyHelper.GetString(operacja, "Opis"),
                CreatedAt = DynamicPropertyHelper.GetDateTime(operacja, "DataUtworzenia")
            };
        }
        catch
        {
            return new PaymentDto { Id = DynamicPropertyHelper.GetId(operacja) };
        }
    }

    private static PaymentDto MapBankOperationToDto(dynamic operacja)
    {
        try
        {
            var kwota = DynamicPropertyHelper.GetDecimal(operacja, "Kwota");
            var isIncome = kwota > 0;

            return new PaymentDto
            {
                Id = DynamicPropertyHelper.GetId(operacja),
                Number = DynamicPropertyHelper.GetString(operacja, "Numer"),
                FullNumber = DynamicPropertyHelper.GetNestedString(operacja, "NumerWewnetrzny", "PelnaSygnatura"),
                Type = isIncome ? PaymentType.BP : PaymentType.BW,
                PaymentDate = DynamicPropertyHelper.GetDateTime(operacja, "DataEfektywna"),
                ContractorId = DynamicPropertyHelper.GetNullableInt(operacja, "Podmiot", "Id"),
                ContractorName = DynamicPropertyHelper.GetString(operacja, "Podmiot", "NazwaSkrocona")
                    ?? DynamicPropertyHelper.GetString(operacja, "NazwaKontrahentaZaimportowanego"),
                ContractorNIP = DynamicPropertyHelper.GetString(operacja, "Podmiot", "NIP"),
                Amount = Math.Abs(kwota),
                Currency = DynamicPropertyHelper.GetString(operacja, "Waluta", "Symbol") ?? "PLN",
                BankAccountSymbol = DynamicPropertyHelper.GetString(operacja, "Rachunek", "Symbol"),
                BankAccountName = DynamicPropertyHelper.GetString(operacja, "Rachunek", "Nazwa"),
                BankAccountNumber = DynamicPropertyHelper.GetString(operacja, "Rachunek", "NumerRachunku"),
                Title = DynamicPropertyHelper.GetString(operacja, "Opis"),
                CreatedAt = DynamicPropertyHelper.GetDateTime(operacja, "DataUtworzenia")
            };
        }
        catch
        {
            return new PaymentDto { Id = DynamicPropertyHelper.GetId(operacja) };
        }
    }

    private static ReceivableDto MapReceivableToDto(dynamic rozrachunek)
    {
        try
        {
            var today = DateTime.Today;
            var dataPlatnosci = DynamicPropertyHelper.GetDateTime(rozrachunek, "TerminPlatnosci");
            var kwotaDoRozliczenia = DynamicPropertyHelper.GetDecimal(rozrachunek, "KwotaPozostala");
            var kwota = DynamicPropertyHelper.GetDecimal(rozrachunek, "Kwota");

            var daysOverdue = 0;
            if (dataPlatnosci.HasValue && dataPlatnosci.Value < today && kwotaDoRozliczenia > 0)
            {
                daysOverdue = (int)(today - dataPlatnosci.Value).TotalDays;
            }

            var status = ReceivableStatus.Unsettled;
            if (kwotaDoRozliczenia == 0)
            {
                status = ReceivableStatus.Settled;
            }
            else if (kwotaDoRozliczenia < kwota)
            {
                status = ReceivableStatus.PartiallySettled;
            }

            var typ = DynamicPropertyHelper.GetInt(rozrachunek, "Typ");
            // Rozrachunek.Dokument = linked Subiekt document (entity); DokumentZrodlowy = source document number (string)
            var dokument = DynamicPropertyHelper.GetProperty(rozrachunek, "Dokument");
            return new ReceivableDto
            {
                Id = DynamicPropertyHelper.GetId(rozrachunek),
                Type = typ == (int)TypRozrachunku.Naleznosc ? ReceivableType.Receivable : ReceivableType.Payable,
                Status = status,
                DocumentId = dokument != null ? DynamicPropertyHelper.GetNullableInt(dokument, "Id") : null,
                DocumentNumber = DynamicPropertyHelper.GetString(rozrachunek, "DokumentZrodlowy")
                              ?? (dokument != null ? DynamicPropertyHelper.GetString(dokument, "NumerWewnetrzny", "PelnaSygnatura") : null),
                DocumentType = dokument != null ? DynamicPropertyHelper.GetString(dokument, "Symbol") : null,
                DocumentDate = DynamicPropertyHelper.GetDateTime(rozrachunek, "DataDokumentuZrodlowego"),
                ContractorId = DynamicPropertyHelper.GetNullableInt(rozrachunek, "Podmiot", "Id")
                            ?? DynamicPropertyHelper.GetNullableInt(rozrachunek, "Podmiot_Id"),
                ContractorName = DynamicPropertyHelper.GetString(rozrachunek, "Podmiot", "NazwaSkrocona"),
                ContractorNIP = DynamicPropertyHelper.GetString(rozrachunek, "Podmiot", "NIP"),
                OriginalAmount = kwota,
                SettledAmount = kwota - kwotaDoRozliczenia,
                RemainingAmount = kwotaDoRozliczenia,
                UnsettledAmount = DynamicPropertyHelper.GetDecimal(rozrachunek, "KwotaNierozliczona"),
                VatAmount = DynamicPropertyHelper.GetNullableDecimal(rozrachunek, "KwotaVAT"),
                Currency = DynamicPropertyHelper.GetString(rozrachunek, "Waluta", "Symbol") ?? "PLN",
                IssueDate = DynamicPropertyHelper.GetDateTime(rozrachunek, "DataPowstania")
                         ?? DynamicPropertyHelper.GetDateTime(rozrachunek, "DataDokumentuZrodlowego"),
                DueDate = dataPlatnosci,
                LastSettlementDate = DynamicPropertyHelper.GetDateTime(rozrachunek, "DataOstatniegoRozliczenia"),
                DaysOverdue = daysOverdue,
                IsOverdue = daysOverdue > 0,
                IsSettled = status == ReceivableStatus.Settled,
                Title = DynamicPropertyHelper.GetString(rozrachunek, "Tytul"),
                Subtype = DynamicPropertyHelper.GetString(rozrachunek, "Podtyp", "Nazwa"),
                IsCollectible = DynamicPropertyHelper.GetNullableBool(rozrachunek, "Sciagalny"),
                SplitPayment = DynamicPropertyHelper.GetNullableBool(rozrachunek, "PodzielonaPlatnosc")
            };
        }
        catch
        {
            return new ReceivableDto { Id = DynamicPropertyHelper.GetId(rozrachunek) };
        }
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
