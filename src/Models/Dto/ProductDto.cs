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
    /// <summary>False only for products in the recycle bin (the SDK has no other activity flag on Asortyment).</summary>
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
    /// <summary>
    /// Product EAN: primary barcode (<c>PodstawowyKodKreskowy</c>) of the base unit, falling back to any barcode of the
    /// base unit, then of the sale unit, then of the first unit that has one. See <see cref="EanUnitSymbol"/>.
    /// </summary>
    public string? EAN { get; set; }
    /// <summary>Symbol of the unit <see cref="EAN"/> was taken from (null when the product has no barcode).</summary>
    public string? EanUnitSymbol { get; set; }
    /// <summary>All barcodes of all units (SDK: JednostkiMiar[].PodstawowyKodKreskowy / KodyKreskowe).</summary>
    public List<ProductBarcodeDto> Barcodes { get; set; } = new();
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
    /// <summary>Symbol of the base (stock) unit (PodstawowaJednostkaMiaryAsortymentu.JednostkaMiary.Symbol).</summary>
    public string? BaseUnit { get; set; }
    public string? SaleUnit { get; set; }
    public string? PurchaseUnit { get; set; }
    /// <summary>All units of measure of the product with conversions to the base unit (SDK: Asortyment.JednostkiMiar).</summary>
    public List<ProductUnitDto> Units { get; set; } = new();
    public decimal? DefaultSalesQuantity { get; set; }
    public decimal? DefaultPurchaseQuantity { get; set; }

    // Pricing
    /// <summary>
    /// Always null: Asortyment has no net/gross sales price (prices live in price lists, see
    /// GET /api/dictionary/price-lists/by-id/{id}/items).
    /// </summary>
    public decimal? PriceNet { get; set; }
    /// <summary>Always null, see <see cref="PriceNet"/>.</summary>
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
    /// <summary>
    /// Gross weight of one base unit in kilograms (base unit Masa converted from <see cref="WeightUnitSymbol"/>);
    /// null when not set or the mass unit is not a known mass unit.
    /// </summary>
    public decimal? Weight { get; set; }
    /// <summary>Mass unit of the base unit weight as stored in Nexo (JednostkaMiaryMasy.Symbol).</summary>
    public string? WeightUnitSymbol { get; set; }
    /// <summary>Volume of one base unit, in <see cref="VolumeUnitSymbol"/> (not converted).</summary>
    public decimal? Volume { get; set; }
    /// <summary>Volume unit of the base unit as stored in Nexo (JednostkaMiaryObjetosci.Symbol).</summary>
    public string? VolumeUnitSymbol { get; set; }

    // Status and flags
    /// <summary>False only for products in the recycle bin (same as !IsDeleted; the SDK has no other activity flag).</summary>
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

    // Kit (komplet) composition
    /// <summary>True when the product kind (Asortyment.Rodzaj) is a kit (komplet).</summary>
    public bool IsKit { get; set; }
    /// <summary>Kit components (SDK: Asortyment.SkladnikiKompletu). Empty when the product is not a kit.</summary>
    public List<ProductComponentDto> Components { get; set; } = new();

    // Stock info (populated separately)
    public StockInfoDto? Stock { get; set; }

    /// <summary>
    /// External (e-commerce) warehouse stock levels
    /// (SDK 61.0.0: Asortyment.StanyMagazynoweZewnetrzne). Empty on older SDKs.
    /// </summary>
    public List<ExternalWarehouseStockDto> ExternalStocks { get; set; } = new();

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

/// <summary>
/// Stock level on an external (e-commerce) warehouse (SDK 61.0.0: StanMagazynowyZewnetrzny).
/// </summary>
public class ExternalWarehouseStockDto
{
    public decimal? Quantity { get; set; }
    public string? ExternalWarehouseName { get; set; }
    public string? ExternalWarehouseId { get; set; }
}

/// <summary>
/// Unit of measure assigned to a product (Asortyment.JednostkiMiar → JednostkaMiaryAsortymentu) with its
/// conversion to the base unit. Example: thread spool "szt" = 5000 "m" → Symbol "szt", BaseUnitSymbol "m",
/// ToBaseFactor 5000. Stock levels are kept in the base unit.
/// </summary>
public class ProductUnitDto
{
    public int Id { get; set; }
    public string? Symbol { get; set; }
    public string? Name { get; set; }
    public bool IsBase { get; set; }
    public bool IsSale { get; set; }
    public bool IsPurchase { get; set; }
    public bool IsWarehouse { get; set; }
    public int? Precision { get; set; }
    /// <summary>
    /// Barcode of the collective package bound to the unit (KodKreskowyOpakowania). This is NOT the unit's EAN; see
    /// <see cref="PrimaryBarcode"/> and <see cref="Barcodes"/>.
    /// </summary>
    public string? Barcode { get; set; }
    /// <summary>Primary barcode of the unit (PodstawowyKodKreskowy.Kod).</summary>
    public string? PrimaryBarcode { get; set; }
    /// <summary>All barcodes of the unit, primary first (KodyKreskowe[].Kod).</summary>
    public List<string> Barcodes { get; set; } = new();
    /// <summary>Gross weight of one unit in <see cref="WeightUnitSymbol"/> (not converted).</summary>
    public decimal? Weight { get; set; }
    public string? WeightUnitSymbol { get; set; }
    public decimal? Volume { get; set; }
    public string? VolumeUnitSymbol { get; set; }
    /// <summary>How many base units make one of this unit (1 for the base unit itself, null when unknown).</summary>
    public decimal? ToBaseFactor { get; set; }
    public string? BaseUnitSymbol { get; set; }
    /// <summary>Raw converters as stored by nexo (parent/child quantities), for anything the factor above cannot express.</summary>
    public List<ProductUnitConversionDto> Conversions { get; set; } = new();
}

/// <summary>Result of PUT /api/products/{id}: the saved product plus the outcome of every field sent.</summary>
public class UpdateProductResultDto : ProductDto
{
    public List<ProductFieldResultDto> FieldResults { get; set; } = new();
}

/// <summary>Outcome of one field of a product update.</summary>
public class ProductFieldResultDto
{
    /// <summary>JSON name of the request field (name, description, ean, pkWiU, weight, volume).</summary>
    public string Field { get; set; } = string.Empty;
    /// <summary>"updated" or "unchanged" (the value already matched).</summary>
    public string Status { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    /// <summary>Unit of the value: the barcode's unit symbol for ean, "kg" for weight, the volume unit for volume.</summary>
    public string? Unit { get; set; }
}

/// <summary>A barcode of one product unit (SDK: KodKreskowy).</summary>
public class ProductBarcodeDto
{
    public string Code { get; set; } = string.Empty;
    /// <summary>Symbol of the unit the barcode belongs to.</summary>
    public string? UnitSymbol { get; set; }
    /// <summary>True for the unit's primary barcode (PodstawowyKodKreskowy).</summary>
    public bool IsPrimary { get; set; }
}

/// <summary>PrzelicznikJednostekMiarAsortymentu: ParentQuantity × parent unit = ChildQuantity × child unit.</summary>
public class ProductUnitConversionDto
{
    public string? ParentUnitSymbol { get; set; }
    public decimal? ParentQuantity { get; set; }
    public string? ChildUnitSymbol { get; set; }
    public decimal? ChildQuantity { get; set; }
}

/// <summary>
/// Component of a kit (komplet) product (SDK: SkladnikKompletu). Quantity is expressed in the component's
/// unit of measure (<see cref="UnitSymbol"/>), which is one of the component product's own units.
/// </summary>
public class ProductComponentDto
{
    /// <summary>ID of the component product (SkladnikKompletu.Skladnik.Id) — use it for DELETE.</summary>
    public int ComponentProductId { get; set; }
    public string? ComponentSymbol { get; set; }
    public string? ComponentName { get; set; }
    public decimal Quantity { get; set; }
    public string? UnitSymbol { get; set; }
    /// <summary>ID of the JednostkaMiaryAsortymentu (product-unit binding) used for the quantity.</summary>
    public int? UnitId { get; set; }
    public decimal? Price { get; set; }
    public decimal? Value { get; set; }
    public int? LineNumber { get; set; }
    /// <summary>SkladnikKompletu.BlokujIlosc — quantity cannot be changed on documents.</summary>
    public bool LockQuantity { get; set; }
}

public enum ProductType
{
    Goods = 0,        // Towar
    Service = 1,      // Usluga
    Set = 2,          // Komplet
    Material = 3      // Material
}
