namespace NexoSferaApi.Models.Requests;

/// <summary>
/// Request to refresh the cost of main price list positions and keep the sales price; Nexo recalculates the margin,
/// markup and profit from the new cost (see <see cref="Mode"/>). All positions are saved with one <c>ICennik.Zapisz()</c>: every position or none.
/// </summary>
public class RecalculatePriceListCostsRequest
{
    /// <summary>Nexo product ids (<c>Asortyment.Id</c>) whose main positions are refreshed, each once. Up to 200 together with the kits found through <see cref="ComponentProductIds"/>.</summary>
    public List<int> ProductIds { get; set; } = new();

    /// <summary>
    /// Component product ids: every kit that contains one of them is refreshed too, e.g. all embroidered T-shirts after
    /// the blank T-shirt got a new purchase price. Send <see cref="ProductIds"/>, this, or both.
    /// </summary>
    public List<int> ComponentProductIds { get; set; } = new();

    /// <summary>
    /// Before the refresh, set each kit's registry price (<c>Asortyment.CenaEwidencyjna</c>) to the sum of quantity x
    /// registry price of its components, rounded to 2 decimals. A kit's base price comes from its own registry price, so
    /// this is what makes the cost follow the components. A kit is not computed when a component is counted in a unit
    /// other than its base unit or has no registry price, because the sum would understate the cost.
    /// </summary>
    public bool RecordPriceFromComponents { get; set; }

    /// <summary>
    /// With <see cref="RecordPriceFromComponents"/>: leave kits that cannot be computed out of the refresh and report
    /// them as <c>skipped</c> with the reason, instead of refusing the whole request.
    /// </summary>
    public bool SkipIncompleteKits { get; set; }

    /// <summary>
    /// Also lock the price on documents (<c>ICennikPozycje.UstawBlokadeCen</c>, <c>CenaSztywnaNaDokumencie</c>): offer,
    /// order and invoice lines from this price list can then not change or discount the price. Not needed to keep the
    /// sales price, the refresh refuses any change of it anyway. Default: false. Unlock with
    /// <c>PUT .../items/price-lock</c>.
    /// </summary>
    public bool LockPrices { get; set; }

    /// <summary>Compute and return the new costs and margins without saving anything.</summary>
    public bool DryRun { get; set; }

    /// <summary>
    /// <c>full</c> (default): recalculate the whole position from its base price (<c>AktualizujPozycjeCennika</c>).
    /// <c>estimate</c>: estimate a kit's cost as the sum of its components' costs
    /// (<c>SzacujKosztKompletuZeSkladnikow</c>, <c>PrzeliczSzacunkowyKoszt</c>). <c>calculation</c>: only recalculate the
    /// calculation price from the current base price (<c>AktualizujCeneKalkulacyjna</c>).
    /// </summary>
    public string? Mode { get; set; }
}
