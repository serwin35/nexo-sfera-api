namespace NexoSferaApi.Models.Dto;

/// <summary>
/// VAT rate DTO
/// </summary>
public class VatRateDto
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string? Name { get; set; }
    public decimal Rate { get; set; }
    public bool IsActive { get; set; }
    public VatRateType Type { get; set; }
}

public enum VatRateType
{
    Standard = 0,
    Reduced = 1,
    SuperReduced = 2,
    Zero = 3,
    Exempt = 4
}

/// <summary>
/// Unit of measure DTO
/// </summary>
public class UnitOfMeasureDto
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string? Name { get; set; }
    public int? DecimalPlaces { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Price level DTO (SDK <c>PoziomCen</c>).
/// </summary>
/// <remarks>
/// <c>IsDefault</c>, <c>IsActive</c> and <c>Priority</c> are legacy fields: <c>PoziomCen</c> has no
/// <c>Domyslny</c>/<c>Aktywny</c>/<c>Priorytet</c> members, so they are always <c>false</c>/<c>false</c>/<c>0</c>.
/// They are kept unchanged on purpose (consumers such as WolfFire derive the company default price level from
/// <c>IsDefault</c>; deriving it from another member would silently switch document pricing).
/// </remarks>
public class PriceLevelDto
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public int Priority { get; set; }

    /// <summary>Currency of the price level (<c>PoziomCen.Waluta.Symbol</c>, "for future use" in the SDK).</summary>
    public string? CurrencySymbol { get; set; }

    /// <summary>
    /// Id of the base (main) price list of this level (<c>Cennik.Bazowy = true</c>); <c>null</c> unless the level has
    /// exactly one base price list (see <see cref="BasePriceListCount"/>).
    /// </summary>
    public int? BasePriceListId { get; set; }

    /// <summary>Number of price lists of this level flagged as base (the SDK allows at most one).</summary>
    public int BasePriceListCount { get; set; }

    /// <summary>Ids of all price lists attached to this level (base and additional).</summary>
    public List<int> PriceListIds { get; set; } = new();

    /// <summary>Price list the level computes its base price from (<c>PoziomCen.CennikCenyBazowejId</c>).</summary>
    public int? BasePriceSourcePriceListId { get; set; }

    /// <summary>Default base price function of the level (<c>PoziomCen.FunkcjaWyliczaniaCenyBazowej</c>).</summary>
    public Guid? BasePriceFunctionId { get; set; }

    /// <summary>SDK name of <see cref="BasePriceFunctionId"/> (e.g. <c>WgOstatniejCenyZakupu</c>), <c>null</c> when unknown.</summary>
    public string? BasePriceFunction { get; set; }
}

/// <summary>
/// Price list DTO (SDK <c>Cennik</c>).
/// </summary>
/// <remarks>
/// <c>Symbol</c>, <c>Name</c>, <c>ValidFrom</c>, <c>ValidTo</c>, <c>IsActive</c> and <c>ItemCount</c> are legacy fields read
/// from members that do not exist on <c>Cennik</c> (always empty/false/0). They are kept for backward compatibility;
/// use <c>Title</c>, <c>Status</c>, <c>IsBase</c>, <c>PriceLevelId</c> and <c>Schedule</c> instead. Price lists have no
/// symbol in Nexo, so they are addressed by <c>Id</c> (<c>/api/dictionary/price-lists/by-id/{id}</c>).
/// </remarks>
public class PriceListDto
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Description { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public bool IsActive { get; set; }
    public string? CurrencySymbol { get; set; }
    public int ItemCount { get; set; }

    /// <summary>Title of the price list (<c>Cennik.Tytul</c>).</summary>
    public string? Title { get; set; }

    /// <summary>Subtitle (<c>Cennik.Podtytul</c>).</summary>
    public string? Subtitle { get; set; }

    /// <summary><c>defined</c>, <c>approved</c> or <c>closed</c> (<c>Cennik.Status</c>, enum <c>StatusCennika</c>); <c>null</c> for an unknown value.</summary>
    public string? Status { get; set; }

    /// <summary>Raw <c>Cennik.Status</c> (0 Definiowany, 1 Zatwierdzony, 2 Zamkniety).</summary>
    public int StatusCode { get; set; }

    /// <summary>Only approved price lists take part in pricing (<c>Status = Zatwierdzony</c>).</summary>
    public bool IsApproved { get; set; }

    /// <summary>Base (main) price list of its price level (<c>Cennik.Bazowy</c>); at most one per level.</summary>
    public bool IsBase { get; set; }

    /// <summary>Price level of the price list (<c>Cennik.PoziomCen</c>); <c>null</c> for an additional list not linked to a level.</summary>
    public int? PriceLevelId { get; set; }

    public string? PriceLevelSymbol { get; set; }

    public string? PriceLevelName { get; set; }

    /// <summary>
    /// For an additional price list linked to a main one (<c>ICennik.UstawJakoDodatkowy</c>: same level, <c>Bazowy = false</c>)
    /// the id of the level's base price list; <c>null</c> for base lists, unlinked lists, and levels without exactly one base list.
    /// </summary>
    public int? MainPriceListId { get; set; }

    /// <summary>Price precision of the currency (<c>Cennik.Waluta.PrecyzjaCeny</c>).</summary>
    public int? CurrencyPricePrecision { get; set; }

    /// <summary><c>net</c> or <c>gross</c>: which price the list calculates (<c>Cennik.RodzajKalkulowanejCeny</c>, enum <c>RodzajCeny</c>).</summary>
    public string? CalculatedPriceKind { get; set; }

    /// <summary>Dynamic price list (<c>Cennik.DynamicznaKalkulacjaCen</c>): prices recalculated on the fly from the base price.</summary>
    public bool DynamicPricing { get; set; }

    /// <summary>Default position calculation method (<c>ParametryPozycjiDomyslne().WyliczajPozycjeWedlug</c>): <c>margin</c>, <c>markup</c> or <c>profit</c>.</summary>
    public string? DefaultCalculationMethod { get; set; }

    /// <summary>Default margin (<c>ParametrGrupyPozycjiCennika.Marza</c>), as stored by the SDK.</summary>
    public decimal? DefaultMargin { get; set; }

    /// <summary>Default markup (<c>ParametrGrupyPozycjiCennika.Narzut</c>), as stored by the SDK.</summary>
    public decimal? DefaultMarkup { get; set; }

    /// <summary>Default profit (<c>ParametrGrupyPozycjiCennika.Zysk</c>), as stored by the SDK.</summary>
    public decimal? DefaultProfit { get; set; }

    /// <summary>Default base price function of the positions (<c>ParametrGrupyPozycjiCennika.FunkcjaWyliczaniaCenyBazowej</c>).</summary>
    public Guid? DefaultBasePriceFunctionId { get; set; }

    /// <summary>SDK name of <see cref="DefaultBasePriceFunctionId"/> (e.g. <c>WgCenyEwidencyjnej</c>, <c>WgOstatniejCenyZakupu</c>).</summary>
    public string? DefaultBasePriceFunction { get; set; }

    /// <summary>Price list the positions take their base price from by default (<c>ParametrGrupyPozycjiCennika.CennikCenyBazowej</c>).</summary>
    public int? DefaultBasePriceSourcePriceListId { get; set; }

    /// <summary>Default rounding function (<c>ParametrGrupyPozycjiCennika.FunkcjaWyrownywaniaCen</c>).</summary>
    public Guid? DefaultRoundingFunctionId { get; set; }

    /// <summary>SDK name of <see cref="DefaultRoundingFunctionId"/> (e.g. <c>WyrownywanieDoJednostek</c>).</summary>
    public string? DefaultRoundingFunction { get; set; }

    /// <summary>Zero price policy (<c>Cennik.CenaZerowa</c>): <c>warn</c> or <c>allow</c>.</summary>
    public string? ZeroPricePolicy { get; set; }

    /// <summary>Below-margin policy (<c>Cennik.CenaPonizejMarzy</c>): <c>warn</c>, <c>allowWithDiscount</c> or <c>allowAlways</c>.</summary>
    public string? BelowMarginPolicy { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    /// <summary>Last update of position prices (<c>Cennik.DataOstatniejAktualizacji</c>).</summary>
    public DateTime? LastPriceUpdateAt { get; set; }

    /// <summary>Last modification of the price list (<c>Cennik.DataOstatniejEdycji</c>).</summary>
    public DateTime? LastEditedAt { get; set; }

    /// <summary>Validity schedule of an additional price list (<c>Cennik.HarmonogramWaznosci</c>); <c>null</c> when not set.</summary>
    public PriceListScheduleDto? Schedule { get; set; }
}

/// <summary>
/// Validity schedule of an additional price list (SDK complex type <c>Harmonogram</c>).
/// </summary>
public class PriceListScheduleDto
{
    /// <summary><c>daily</c>, <c>weekly</c> or <c>monthly</c> (enum <c>RodzajHarmonogramu</c>).</summary>
    public string? Kind { get; set; }

    public int KindCode { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    /// <summary>Bit mask of active days of week (<c>Harmonogram.DniTygodnia</c>).</summary>
    public long DaysOfWeekMask { get; set; }

    /// <summary>Bit mask of active days of month (<c>Harmonogram.DniMiesiaca</c>).</summary>
    public long DaysOfMonthMask { get; set; }

    /// <summary>Bit mask of active months (<c>Harmonogram.Miesiace</c>).</summary>
    public long MonthsMask { get; set; }
}

/// <summary>
/// Price list position read through the price list business object
/// (<c>ICennik.Pozycje</c> = <c>ICennikPozycje</c>, items are <c>IUproszczonaPozycjaCennika</c>).
/// </summary>
/// <remarks>
/// A product has one main position (<c>IsMain</c>, minimum quantity 0) and optional quantity tiers
/// (<c>IsMain = false</c>, <c>MinQuantity</c> &gt; 0). Amounts are in the price list currency (<c>CurrencySymbol</c>).
/// </remarks>
public class PriceListPositionDto
{
    public int Id { get; set; }
    public int PriceListId { get; set; }

    /// <summary>Nexo product id (<c>IdAsortymentu</c>).</summary>
    public int ProductId { get; set; }

    public string? ProductSymbol { get; set; }
    public string? ProductName { get; set; }

    /// <summary>Product kind name (<c>RodzajAsortymentu</c>).</summary>
    public string? ProductKind { get; set; }

    public bool IsService { get; set; }
    public bool IsKit { get; set; }

    /// <summary>Main position of the product in this price list (<c>Glowna</c>).</summary>
    public bool IsMain { get; set; }

    /// <summary>Quantity threshold (<c>IloscMinAsortymentu</c>); 0 for the main position.</summary>
    public decimal MinQuantity { get; set; }

    /// <summary>For a main position: number of quantity tier positions of the same product in this price list.</summary>
    public int QuantityTierCount { get; set; }

    public int UnitId { get; set; }
    public string? UnitSymbol { get; set; }

    public decimal PriceNet { get; set; }
    public decimal PriceGross { get; set; }
    public string? CurrencySymbol { get; set; }
    public int CurrencyPrecision { get; set; }

    /// <summary>Sales VAT rate symbol (<c>StawkaVATSprzedaz</c>, e.g. <c>23</c>, <c>8</c>, <c>zw</c>); <c>null</c> when the product has none.</summary>
    public string? VatRateSymbol { get; set; }

    /// <summary>Sales VAT rate as returned by the SDK (<c>StawkaVATSprzedaz_Stawka</c>; <c>StawkaVat.Stawka</c> is documented as 0–1); <c>null</c> without a symbol.</summary>
    public decimal? VatRate { get; set; }

    /// <summary><see cref="VatRate"/> as a percentage (23 for 23%), <c>null</c> without a symbol.</summary>
    public decimal? VatRatePercent { get; set; }

    /// <summary>Base price the position is calculated from (<c>CenaBazowa</c>).</summary>
    public decimal BasePrice { get; set; }

    public string? BasePriceCurrencySymbol { get; set; }

    /// <summary>Exchange rate of the base price (<c>Kurs</c>).</summary>
    public decimal BasePriceExchangeRate { get; set; }

    /// <summary>Calculation price (<c>CenaKalkulacyjna</c>).</summary>
    public decimal CalculationPrice { get; set; }

    /// <summary>Margin / markup / profit parameter of the position (<c>ParametrKalkulacyjny</c>), as stored by the SDK.</summary>
    public decimal CalculationParameter { get; set; }

    /// <summary><c>margin</c>, <c>markup</c> or <c>profit</c> (<c>DomyslneWyliczajPozycjeWedlug</c>, enum <c>MetodaWyliczaniaPozycjiCennika</c>).</summary>
    public string? CalculationMethod { get; set; }

    public int? CalculationMethodCode { get; set; }

    /// <summary>Base price function (<c>FunkcjaWyliczaniaCenyBazowej</c>).</summary>
    public Guid? BasePriceFunctionId { get; set; }

    /// <summary>SDK name of <see cref="BasePriceFunctionId"/> (e.g. <c>WgOstatniejCenyZakupu</c>).</summary>
    public string? BasePriceFunction { get; set; }

    /// <summary>Price list the base price comes from (<c>IdCennikaCenyBazowej</c>).</summary>
    public int? BasePriceSourcePriceListId { get; set; }

    /// <summary>Rounding function (<c>FunkcjaWyrownywaniaCeny</c>).</summary>
    public Guid? RoundingFunctionId { get; set; }

    /// <summary>SDK name of <see cref="RoundingFunctionId"/> (e.g. <c>WyrownywanieDoJednostek</c>).</summary>
    public string? RoundingFunction { get; set; }

    /// <summary>Signed price correction after rounding (<c>ZnakKorektyCeny</c> × <c>KorektaCeny</c>).</summary>
    public decimal PriceCorrection { get; set; }

    /// <summary>Sales price calculated from the purchase price before rounding (<c>CenaPoWyliczeniu</c>).</summary>
    public decimal PriceAfterCalculation { get; set; }

    /// <summary>Sales price calculated from the purchase price after rounding (<c>CenaPoZaokragleniu</c>).</summary>
    public decimal PriceAfterRounding { get; set; }

    /// <summary>Estimated purchase cost (<c>SzacowanyKoszt</c>).</summary>
    public decimal EstimatedCost { get; set; }

    /// <summary>Registry price of the product (<c>CenaEwidencyjnaAsortymentu</c>).</summary>
    public decimal RegistryPrice { get; set; }

    /// <summary>Minimum margin of the product (<c>MinimalnaMarza</c>), as stored by the SDK.</summary>
    public decimal MinimumMargin { get; set; }

    public decimal? Margin { get; set; }
    public decimal? Markup { get; set; }
    public decimal Profit { get; set; }

    public decimal DefaultDiscount { get; set; }
    public decimal MaxDiscount { get; set; }

    /// <summary>Price locked on documents (<c>CenaSztywnaNaDokumencie</c>).</summary>
    public bool IsPriceLocked { get; set; }

    /// <summary>Last update of the position price (<c>DataAktualizacji</c>).</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Supplier (contractor) ids of the product (<c>IdDostawcowAsortymentu</c>).</summary>
    public List<int> SupplierIds { get; set; } = new();

    /// <summary>Primary supplier id (<c>IdPodstawowegoDostawcyAsortymentu</c>).</summary>
    public int? PrimarySupplierId { get; set; }

    /// <summary>Manufacturer id (<c>IdProducentaAsortymentu</c>).</summary>
    public int? ManufacturerId { get; set; }
}

/// <summary>
/// Price list item DTO
/// </summary>
public class PriceListItemDto
{
    public int Id { get; set; }
    public int PriceListId { get; set; }
    public int ProductId { get; set; }
    public string? ProductSymbol { get; set; }
    public string? ProductName { get; set; }
    public decimal PriceNet { get; set; }
    public decimal PriceGross { get; set; }
    public string? VatRate { get; set; }
    public decimal? MinQuantity { get; set; }
    public decimal? MaxQuantity { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
}

/// <summary>
/// Currency DTO
/// </summary>
public class CurrencyDto
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? IsoCode { get; set; }
    public decimal? ExchangeRate { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Exchange rate DTO
/// </summary>
public class ExchangeRateDto
{
    public int Id { get; set; }
    public string CurrencySymbol { get; set; } = string.Empty;
    public DateTime? Date { get; set; }
    public decimal Rate { get; set; }
    public int Multiplier { get; set; } = 1;
    public string? Source { get; set; }
    public string? TableNumber { get; set; }
}

/// <summary>
/// Payment method DTO
/// </summary>
public class PaymentMethodDto
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string? Name { get; set; }
    public PaymentMethodType Type { get; set; }
    public int? DefaultDueDays { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
}

public enum PaymentMethodType
{
    Cash = 0,
    BankTransfer = 1,
    Card = 2,
    DirectDebit = 3,
    Compensation = 4,
    ElectronicPayment = 5,
    Other = 99
}
