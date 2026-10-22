namespace NexoSferaApi.Models.Requests;

/// <summary>
/// Request for setting sales prices of main price list positions (SDK: <c>ICennik.Pozycje</c> →
/// <c>IUproszczonaPozycjaCennika</c>). All items are saved with one <c>ICennik.Zapisz()</c>: every price is written or none.
/// </summary>
public class SetPriceListPricesRequest
{
    /// <summary>Products and their new prices (1-200 items, each product once).</summary>
    public List<PriceListPositionPriceRequest> Items { get; set; } = new();
}

/// <summary>New price of the main position (<c>Glowna</c>, no quantity tier) of one product.</summary>
public class PriceListPositionPriceRequest
{
    /// <summary>Nexo product id (<c>Asortyment.Id</c>).</summary>
    public int ProductId { get; set; }

    /// <summary>Gross price (<c>CenaBrutto</c>). Exactly one of priceGross / priceNet is required.</summary>
    public decimal? PriceGross { get; set; }

    /// <summary>Net price (<c>CenaNetto</c>). Exactly one of priceGross / priceNet is required.</summary>
    public decimal? PriceNet { get; set; }

    /// <summary>
    /// Kit positions priced as the sum of their components (<c>WyliczCeneKompletuZeSkladnikow</c>) are refused unless
    /// this is <c>true</c>; then the flag is switched off and the given price becomes the fixed kit price.
    /// </summary>
    public bool DisableKitComponentPricing { get; set; }
}
