namespace NexoSferaApi.Models.Dto;

public class ProductDto
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? EAN { get; set; }
    public string? PKWiU { get; set; }
    public string? SWW { get; set; }
    public ProductType Type { get; set; }
    public string? SaleUnit { get; set; }
    public string? PurchaseUnit { get; set; }
    public decimal? PriceNet { get; set; }
    public decimal? PriceGross { get; set; }
    public string? VatRate { get; set; }
    public decimal? Weight { get; set; }
    public decimal? Volume { get; set; }
    public bool IsActive { get; set; }
    public StockInfoDto? Stock { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
}

public class StockInfoDto
{
    public decimal Quantity { get; set; }
    public decimal Reserved { get; set; }
    public decimal Available { get; set; }
    public string? WarehouseSymbol { get; set; }
}

public enum ProductType
{
    Goods = 0,        // Towar
    Service = 1,      // Usluga
    Set = 2,          // Komplet
    Material = 3      // Material
}
