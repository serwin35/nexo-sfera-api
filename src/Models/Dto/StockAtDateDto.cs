namespace NexoSferaApi.Models.Dto;

/// <summary>
/// Stock of one product on a day (GET /api/inventory/stock-at-date): Nexo's own figures for the day next to the
/// figure the bridge's stock pre-check uses (today's <c>StanyMagazynowe.IloscDostepna</c>).
/// </summary>
public class StockAtDateDto
{
    public int ProductId { get; set; }
    public string? ProductSymbol { get; set; }
    public string WarehouseSymbol { get; set; } = string.Empty;
    /// <summary>The day asked for, yyyy-MM-dd.</summary>
    public string Date { get; set; } = string.Empty;
    /// <summary><c>IMagazynier.IloscWolna(D 00:00:00)</c>: what can be issued, receipts up to the start of the day.</summary>
    public decimal IloscWolnaD { get; set; }
    /// <summary><c>IMagazynier.IloscWolna(D 23:59:59)</c>: the same with the day's own receipts.</summary>
    public decimal IloscWolnaEndOfDay { get; set; }
    /// <summary><c>IMagazynier.IloscDostepna(D 00:00:00)</c>: the stock on the day.</summary>
    public decimal IloscDostepnaD { get; set; }
    /// <summary>Today's <c>StanyMagazynowe.IloscDostepna</c>, what <c>StockValidationHelper</c> checks before a save.</summary>
    public decimal IloscDostepnaToday { get; set; }
    public string Unit { get; set; } = "szt.";
}

/// <summary>The number Nexo would give the next document of a type on a day (GET /api/documents/next-number).</summary>
public class NextNumberDto
{
    public string Type { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string? WarehouseSymbol { get; set; }
    /// <summary>The predicted full number, e.g. "KFS 2024/02/2".</summary>
    public string FullNumber { get; set; } = string.Empty;
    /// <summary>The sequence of that number, e.g. 2.</summary>
    public int Sequence { get; set; }
    /// <summary>What the preview stands on; for KFS / ZW it says the number is an approximation.</summary>
    public string Basis { get; set; } = string.Empty;
}
