namespace NexoSferaApi.Models.Requests;

/// <summary>
/// Request to refresh the cost of main price list positions and keep the sales price; Nexo recalculates the margin,
/// markup and profit from the new cost (see <see cref="Mode"/>). All positions are saved with one <c>ICennik.Zapisz()</c>: every position or none.
/// </summary>
public class RecalculatePriceListCostsRequest
{
    /// <summary>Nexo product ids (<c>Asortyment.Id</c>) whose main positions are refreshed, 1-200, each once.</summary>
    public List<int> ProductIds { get; set; } = new();

    /// <summary>
    /// Lock the sales price of the positions (<c>ICennikPozycje.UstawBlokadeCen</c>), so a later cost change moves the
    /// margin and not the price. Default: true.
    /// </summary>
    public bool LockPrices { get; set; } = true;

    /// <summary>Compute and return the new costs and margins without saving anything.</summary>
    public bool DryRun { get; set; }

    /// <summary>
    /// <c>full</c> (default): recalculate the whole position from its base price (<c>AktualizujPozycjeCennika</c>);
    /// needs <c>lockPrices</c>. <c>estimate</c>: estimate a kit's cost as the sum of its components' costs
    /// (<c>SzacujKosztKompletuZeSkladnikow</c>, <c>PrzeliczSzacunkowyKoszt</c>). <c>calculation</c>: only recalculate the
    /// calculation price from the current base price (<c>AktualizujCeneKalkulacyjna</c>).
    /// </summary>
    public string? Mode { get; set; }
}
