using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using NexoSferaApi.Models.Dto;

namespace NexoSferaApi.Helpers;

/// <summary>
/// The form of a sales invoice, <c>Dokument.FormaFaktury</c>: paper (not sent to KSeF) or meant for KSeF, stated by
/// the client in <c>invoiceForm</c> and applied before <c>Zapisz()</c>, so a historical invoice imported from another
/// system does not land in Subiekt's KSeF send queue.
/// </summary>
/// <remarks>
/// SDK 62.0.0.9537: <c>Dokument.FormaFaktury</c> is a <c>byte?</c> holding <see cref="FormaFaktury"/>
/// (<c>Papierowa</c> = 0 "faktura papierowa, niepodlegająca wysyłce do KSeF", <c>KSEF</c> = 1, <c>KSEFOffline</c> = 2,
/// <c>KSEFAwaryjna</c> = 3). <c>KSeFExtensions.OkreslaFormeFaktury(Dokument)</c> says whether a document can take a
/// form; <c>KSeFExtensions.StatusKSeF(Dokument)</c> reads its KSeF status. Fails loudly: a form that cannot be set, or
/// that Nexo does not keep, refuses the save instead of being ignored.
/// </remarks>
public static class InvoiceForm
{
    public const string Field = "invoiceForm";

    /// <summary>Endpoints (relative to <c>/api/documents</c>) that apply <see cref="Field"/>.</summary>
    public static readonly IReadOnlyList<string> SupportedEndpoints = new[] { "sales-invoice" };

    /// <summary>Error for an endpoint that cannot apply <see cref="Field"/>.</summary>
    public static readonly string Unsupported = $"{Field} is supported only by POST /api/documents/sales-invoice";

    public const string CannotTakeForm = Field + ": this invoice cannot take a form (OkreslaFormeFaktury is false)";

    /// <summary>The forms a client may ask for.</summary>
    private static readonly IReadOnlyDictionary<string, FormaFaktury> Requestable =
        new Dictionary<string, FormaFaktury>(StringComparer.OrdinalIgnoreCase)
        {
            ["Papierowa"] = FormaFaktury.Papierowa,
            ["KSEF"] = FormaFaktury.KSEF,
        };

    /// <summary>Null when <paramref name="value"/> is absent or a form a client may ask for; otherwise the 400 text.</summary>
    public static string? CheckRequest(string? value) =>
        value == null || Requestable.ContainsKey(value.Trim())
            ? null
            : $"{Field}: \"{value}\" is not a form this bridge applies; use Papierowa or KSEF";

    /// <summary>
    /// Applies the requested form to the document before it is saved. Returns the refusal, or null when the form took.
    /// Must run on the SDK thread.
    /// </summary>
    public static string? Apply(Dokument dokument, string requested) =>
        Apply(requested, () => KSeFExtensions.OkreslaFormeFaktury(dokument),
            () => dokument.FormaFaktury, value => dokument.FormaFaktury = value);

    /// <summary>The rule of <see cref="Apply(Dokument, string)"/>, with the SDK calls passed in (tests).</summary>
    public static string? Apply(string requested, Func<bool> canTakeForm, Func<byte?> read, Action<byte?> write)
    {
        string? requestError = CheckRequest(requested);
        if (requestError != null)
        {
            return requestError;
        }

        if (!canTakeForm())
        {
            return CannotTakeForm;
        }

        byte wanted = (byte)Requestable[requested.Trim()];
        write(wanted);
        byte? kept = read();
        return kept == wanted ? null : $"{Field}: Nexo kept {Name(kept) ?? "no form"}";
    }

    /// <summary>
    /// The form a correction (KFS) must take from the invoice it corrects: <c>Papierowa</c> when that invoice is paper,
    /// so the correction stays out of the KSeF send queue too; otherwise null (Nexo's default). The SDK 62.0.0.9537 docs
    /// do not say that <c>UtworzKorekteFakturySprzedazy(DokumentDS)</c> carries <c>FormaFaktury</c> over.
    /// </summary>
    public static string? CarriedToCorrection(byte? correctedForm) =>
        correctedForm == (byte)FormaFaktury.Papierowa ? FormaFaktury.Papierowa.ToString() : null;

    /// <summary>The name of a stored form ("Papierowa", "KSEF", "KSEFOffline", "KSEFAwaryjna"), or null.</summary>
    public static string? Name(byte? stored) =>
        stored.HasValue && Enum.IsDefined(typeof(FormaFaktury), stored.Value)
            ? ((FormaFaktury)stored.Value).ToString()
            : stored?.ToString();

    /// <summary>Read-back for any document: its form and its KSeF status name, each null when unavailable.</summary>
    public static (string? Form, string? KsefStatus) Read(object? document)
    {
        if (document is not Dokument dokument)
        {
            return (null, null);
        }

        string? form = null;
        string? status = null;
        try
        {
            form = Name(dokument.FormaFaktury);
        }
        catch
        {
            // a document type without the column: no form
        }

        try
        {
            status = KSeFExtensions.StatusKSeF(dokument).ToString();
        }
        catch
        {
            // not readable outside a full SDK context, or not a KSeF document type
        }

        return (form, status);
    }

    /// <summary>
    /// Puts the read-back on a document DTO: <c>invoiceForm</c>, and the KSeF status name in <c>ksefStatus.status</c>
    /// (the existing KSeF block, kept so other clients of <c>ksefStatus</c> do not break).
    /// </summary>
    public static void Fill(DocumentDto dto, object? document)
    {
        var (form, status) = Read(document);
        dto.InvoiceForm = form;
        if (status != null)
        {
            dto.KsefStatus ??= new KsefStatusDto();
            dto.KsefStatus.Status = status;
        }
    }
}
