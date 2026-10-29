namespace NexoSferaApi.Models.Requests;

/// <summary>
/// Request to switch the price lock on documents (<c>CenaSztywnaNaDokumencie</c>) of main price list positions
/// (SDK: <c>ICennikPozycje.UstawBlokadeCen</c>). All positions are saved with one <c>ICennik.Zapisz()</c>: every position or none.
/// </summary>
public class SetPriceListPriceLockRequest
{
    /// <summary>Nexo product ids (<c>Asortyment.Id</c>) whose main positions are switched, each once (1-200).</summary>
    public List<int> ProductIds { get; set; } = new();

    /// <summary>
    /// Required. <c>true</c> locks the price on documents: the price of an offer, order or invoice line taken from this
    /// price list cannot be changed or discounted. <c>false</c> unlocks it. The sales price itself does not change.
    /// </summary>
    public bool? Locked { get; set; }

    /// <summary>Report the changes without saving anything.</summary>
    public bool DryRun { get; set; }
}
