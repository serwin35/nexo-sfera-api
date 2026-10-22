namespace NexoSferaApi.Models.Requests;

/// <summary>
/// Request for a new additional price list that supplements a main price list (SDK: <c>ICenniki.Utworz()</c>,
/// <c>ICennik.UstawJakoDodatkowy</c>). The price list and its positions are saved with one <c>ICennik.Zapisz()</c>.
/// </summary>
public class CreatePriceListRequest
{
    /// <summary>Title (<c>Cennik.Tytul</c>), required.</summary>
    public string? Title { get; set; }

    /// <summary>Optional subtitle (<c>Cennik.Podtytul</c>).</summary>
    public string? Subtitle { get; set; }

    /// <summary>Optional description (<c>Cennik.Opis</c>).</summary>
    public string? Description { get; set; }

    /// <summary>Id of the main price list (<c>Cennik.Bazowy</c>) the new price list supplements.</summary>
    public int MainPriceListId { get; set; }

    /// <summary>
    /// Optional Nexo product ids (0-200, each once). Their positions are added with the calculation parameters of the main
    /// price list (<c>ICennikPozycje.DodajIPrzepiszParametryPozycjiZGlownego</c>).
    /// </summary>
    public List<int> ProductIds { get; set; } = new();

    /// <summary>
    /// Approve the price list on creation. By default it stays defined (<c>StatusCennika.Definiowany</c>), which takes no
    /// part in pricing until it is approved in Subiekt or through <c>PUT .../status</c>.
    /// </summary>
    public bool Approve { get; set; }
}

/// <summary>Request for a status change of an additional price list (SDK: <c>ICennik.UstawStatus</c>).</summary>
public class SetPriceListStatusRequest
{
    /// <summary><c>defined</c>, <c>approved</c> or <c>closed</c> (<c>StatusCennika</c>).</summary>
    public string? Status { get; set; }
}
