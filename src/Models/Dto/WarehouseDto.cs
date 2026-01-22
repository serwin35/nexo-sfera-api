namespace NexoSferaApi.Models.Dto;

/// <summary>
/// Warehouse (Magazyn) DTO
/// </summary>
public class WarehouseDto
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Stock level DTO
/// </summary>
public class StockDto
{
    public int ProductId { get; set; }
    public string ProductSymbol { get; set; } = string.Empty;
    public int WarehouseId { get; set; }
    public string WarehouseSymbol { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Reserved { get; set; }
    public decimal Available { get; set; }
}
