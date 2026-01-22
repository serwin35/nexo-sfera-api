namespace NexoSferaApi.Models.Dto;

/// <summary>
/// Cash report (Raport kasowy) DTO
/// </summary>
public class CashReportDto
{
    public int Id { get; set; }
    public string? Number { get; set; }
    public string? CashRegisterSymbol { get; set; }
    public string? CashRegisterName { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal TotalDeposits { get; set; }
    public decimal TotalWithdrawals { get; set; }
    public decimal ClosingBalance { get; set; }
    public string? Status { get; set; }
    public bool IsClosed { get; set; }
    public int OperationCount { get; set; }
    public List<CashReportOperationDto>? Operations { get; set; }
}

/// <summary>
/// Cash report operation DTO
/// </summary>
public class CashReportOperationDto
{
    public int Id { get; set; }
    public string? DocumentNumber { get; set; }
    public DateTime? Date { get; set; }
    public string? Description { get; set; }
    public string? OperationType { get; set; }
    public decimal Amount { get; set; }
    public string? ContractorName { get; set; }
    public string? SourceDocumentNumber { get; set; }
}

/// <summary>
/// Bank statement (Wyciąg bankowy) DTO
/// </summary>
public class BankStatementDto
{
    public int Id { get; set; }
    public string? Number { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankAccountName { get; set; }
    public string? BankName { get; set; }
    public DateTime? StatementDate { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal TotalDeposits { get; set; }
    public decimal TotalWithdrawals { get; set; }
    public decimal ClosingBalance { get; set; }
    public string? CurrencySymbol { get; set; }
    public string? Status { get; set; }
    public bool IsClosed { get; set; }
    public int OperationCount { get; set; }
    public List<BankStatementOperationDto>? Operations { get; set; }
}

/// <summary>
/// Bank statement operation DTO
/// </summary>
public class BankStatementOperationDto
{
    public int Id { get; set; }
    public string? DocumentNumber { get; set; }
    public DateTime? Date { get; set; }
    public DateTime? ValueDate { get; set; }
    public string? Description { get; set; }
    public string? OperationType { get; set; }
    public decimal Amount { get; set; }
    public string? ContractorName { get; set; }
    public string? ContractorAccountNumber { get; set; }
    public string? SourceDocumentNumber { get; set; }
    public string? ReferenceNumber { get; set; }
}

/// <summary>
/// Cash register (Stanowisko kasowe) DTO
/// </summary>
public class CashRegisterDto
{
    public int Id { get; set; }
    public string? Symbol { get; set; }
    public string? Name { get; set; }
    public string? CurrencySymbol { get; set; }
    public decimal CurrentBalance { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
}

/// <summary>
/// Bank account DTO
/// </summary>
public class BankAccountDto
{
    public int Id { get; set; }
    public string? Symbol { get; set; }
    public string? Name { get; set; }
    public string? AccountNumber { get; set; }
    public string? BankName { get; set; }
    public string? BankSwift { get; set; }
    public string? CurrencySymbol { get; set; }
    public decimal CurrentBalance { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
}
