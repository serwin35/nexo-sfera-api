namespace NexoSferaApi.Models.Requests;

/// <summary>
/// Request for a price change of price list positions, Subiekt's "Przecena" (SDK: <c>ICennik.Przecen</c>). All
/// positions are saved with one <c>ICennik.Zapisz()</c>: every position is repriced or none.
/// </summary>
public class RepricePriceListRequest
{
    /// <summary>Nexo product ids (<c>Asortyment.Id</c>) whose positions are repriced, 1-200, each once.</summary>
    public List<int> ProductIds { get; set; } = new();

    /// <summary>
    /// <c>decreasePercent</c>, <c>increasePercent</c>, <c>decreaseAmount</c> or <c>increaseAmount</c>
    /// (<c>RodzajPrzeceny</c>).
    /// </summary>
    public string? Kind { get; set; }

    /// <summary>
    /// Percent kinds: a fraction, 0.10 = 10% (a decrease below 1, an increase up to 1). Amount kinds: an amount in the
    /// price list currency. Must be greater than zero.
    /// </summary>
    public decimal? Value { get; set; }

    /// <summary>
    /// Optional rounding function (<c>FunkcjeWyrownywaniaCeny</c>) by the name the positions report in
    /// <c>roundingFunction</c>, e.g. <c>WyrownywanieDoJednostek</c>. Omitted: none is passed to Nexo.
    /// </summary>
    public string? RoundingFunction { get; set; }

    /// <summary>Optional signed correction after rounding (<c>KorektaCeny</c>), e.g. -0.01 for prices ending in .99.</summary>
    public decimal? PriceCorrection { get; set; }

    /// <summary>Reset the default discount of the repriced positions to zero.</summary>
    public bool ResetDefaultDiscount { get; set; }

    /// <summary>Reprice only the main positions. By default the quantity tiers of the products are repriced too.</summary>
    public bool MainOnly { get; set; }
}
