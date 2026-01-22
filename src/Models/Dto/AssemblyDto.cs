namespace NexoSferaApi.Models.Dto;

/// <summary>
/// Assembly (montaż - ZM) response DTO
/// </summary>
public class AssemblyDto
{
    public int Id { get; set; }
    public string? DocumentNumber { get; set; }
    public AssemblyType Type { get; set; }
    public int ProductId { get; set; }
    public string? ProductSymbol { get; set; }
    public string? ProductName { get; set; }
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public string? WarehouseSymbol { get; set; }
    public string? WarehouseName { get; set; }
    public DateTime Date { get; set; }
    public string? Status { get; set; }
    public List<AssemblyComponentDto> Components { get; set; } = new();
    public string? Notes { get; set; }
}

/// <summary>
/// Assembly component DTO
/// </summary>
public class AssemblyComponentDto
{
    public int ProductId { get; set; }
    public string? ProductSymbol { get; set; }
    public string? ProductName { get; set; }
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public decimal? UnitCost { get; set; }
    public decimal? TotalCost { get; set; }
}

/// <summary>
/// Assembly type
/// </summary>
public enum AssemblyType
{
    /// <summary>
    /// Assembly (montaż) - combining components into finished product
    /// </summary>
    Assembly = 0,

    /// <summary>
    /// Disassembly (demontaż) - breaking down product into components
    /// </summary>
    Disassembly = 1
}

/// <summary>
/// Assembly list item for listing operations
/// </summary>
public class AssemblyListItemDto
{
    public int Id { get; set; }
    public string? DocumentNumber { get; set; }
    public AssemblyType Type { get; set; }
    public string TypeName => Type == AssemblyType.Assembly ? "Montaż" : "Demontaż";
    public int ProductId { get; set; }
    public string? ProductSymbol { get; set; }
    public string? ProductName { get; set; }
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public string? WarehouseSymbol { get; set; }
    public DateTime Date { get; set; }
    public string? Status { get; set; }
    public int ComponentCount { get; set; }
}
