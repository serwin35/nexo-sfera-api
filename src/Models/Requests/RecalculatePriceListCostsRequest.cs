namespace NexoSferaApi.Models.Requests;

/// <summary>
/// Request to refresh the calculation price (cost) of main price list positions from the price list defaults, keeping
/// the sales price (SDK: <c>ICennikPozycje.AktualizujCeneKalkulacyjna</c>). Nexo recalculates the margin, markup and
/// profit from the new cost. All positions are saved with one <c>ICennik.Zapisz()</c>: every position or none.
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
}
