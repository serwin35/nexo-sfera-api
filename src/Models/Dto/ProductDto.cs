namespace NexoSferaApi.Models.Dto;

/// <summary>
/// Lightweight product DTO for list views (minimal fields for performance)
/// </summary>
public class ProductListItemDto
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public int? GroupId { get; set; }
    public string? GroupName { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Full product DTO with all available fields (for detail view)
/// </summary>
public class ProductDto
{
    // Basic info
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? FullCharacteristics { get; set; }
    public string? EAN { get; set; }
    public string? PKWiU { get; set; }
    public string? CnCode { get; set; }
    public string? SWW { get; set; }

    // Variant info
    public string? VariantOriginalName { get; set; }
    public string? VariantOriginalDescription { get; set; }
    public int? VariantNumber { get; set; }
    public int? ParentProductId { get; set; }
    public int? ModelId { get; set; }
    public bool IsVariant { get; set; }

    // Type and classification
    public ProductType Type { get; set; }
    public int? GroupId { get; set; }
    public string? GroupName { get; set; }

    // Units
    public string? SaleUnit { get; set; }
    public string? PurchaseUnit { get; set; }
    public decimal? DefaultSalesQuantity { get; set; }
    public decimal? DefaultPurchaseQuantity { get; set; }

    // Pricing
    public decimal? PriceNet { get; set; }
    public decimal? PriceGross { get; set; }
    public decimal? RecordPrice { get; set; }
    public decimal? LaborCost { get; set; }
    public bool AutoCalculatePrice { get; set; }
    public int? CalculationFromValue { get; set; }
    public int? PriceLevelId { get; set; }
    public string? CurrencyId { get; set; }

    // VAT
    public string? VatRate { get; set; }
    public string? VatRateSalesId { get; set; }
    public string? VatRatePurchaseId { get; set; }
    public bool VatMarginEnabled { get; set; }
    public int? ReverseCharge { get; set; }
    public bool FeeSubjectToVat { get; set; }

    // Physical properties
    public decimal? Weight { get; set; }
    public decimal? Volume { get; set; }

    // Status and flags
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsDiscounted { get; set; }
    public bool IsOpenPrice { get; set; }
    public bool RequiresWeighing { get; set; }
    public int? Markers { get; set; }
    public int? CustomFlagId { get; set; }

    // Sales channels
    public bool EcommerceEnabled { get; set; }
    public bool MobileSalesEnabled { get; set; }
    public bool AuctionServiceEnabled { get; set; }

    // Delivery times
    public int? CustomerDeliveryDays { get; set; }
    public int? SupplierDeliveryDays { get; set; }

    // Expiry control
    public bool ExpiryControlEnabled { get; set; }
    public int? ExpiryDays { get; set; }

    // Batch management
    public int? BatchSplitMethod { get; set; }
    public int? RequireBatchNumber { get; set; }
    public int? RequireBatchExpiry { get; set; }
    public int? CheckBatchUniqueness { get; set; }
    public bool BlockOnDuplicateBatch { get; set; }

    // Additional fees and taxes
    public int? AdditionalFeeType { get; set; }
    public int? SplitPayment { get; set; }
    public int? JpkVatGroup { get; set; }
    public bool SugarTax { get; set; }
    public bool CaffeineTax { get; set; }
    public bool ForFee { get; set; }

    // Sugar tax details
    public decimal? BeverageVolume { get; set; }
    public decimal? SugarContent { get; set; }
    public bool VariableSugarFee { get; set; }
    public bool HasOtherSweeteners { get; set; }
    public bool IsElectrolyteDrink { get; set; }

    // Intrastat
    public bool IncludedInIntrastat { get; set; }
    public int? DefaultCountryOfOriginId { get; set; }
    public int? OriginMethod { get; set; }
    public int? IntrastatDescMethod { get; set; }
    public string? IntrastatDescription { get; set; }

    // Messages
    public bool DisplayMessage { get; set; }
    public string? MessageText { get; set; }
    public int? MessageDisplayType { get; set; }

    // External integration
    public int? ExternalId { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? Notes { get; set; }

    // Related entities
    public int? RelatedProductId { get; set; }
    public int? RecServiceId { get; set; }
    public int? FundId { get; set; }
    public int? IntegrationAccountId { get; set; }
    public string? SubstitutesGroup { get; set; }

    // Stock info (populated separately)
    public StockInfoDto? Stock { get; set; }

    // Timestamps
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
