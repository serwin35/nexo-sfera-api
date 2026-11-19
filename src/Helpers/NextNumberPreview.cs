using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Numeracja;
using InsERT.Moria.Sfera;

namespace NexoSferaApi.Helpers;

/// <summary>
/// The number Nexo would give the next document of a type on a day, read without reserving anything:
/// <c>IZrodloNumerow.PodajPrzewidywanyNumer(encja)</c> ("Podaje podgląd przewidywanego numeru dokumentu, bez rezerwowania
/// go", SDK 62.0.0.9537) on an in-memory document that is disposed unsaved.
/// </summary>
/// <remarks>
/// <para>
/// Nexo offers no numbering preview by type and date alone: the source of numbers (<c>IZrodloNumerow</c>) belongs to
/// an entity (<c>IObiektNumerowany.ZrodloNumerowRezerwujace</c>; <c>INumeracja.ZnajdzZrodloNumerowRezerwujace</c>
/// also needs one). So the document is created in memory through the same creator the bridge's create endpoint uses,
/// gets the issue date (and warehouse) the way that endpoint sets them, and is asked for the predicted number.
/// </para>
/// <para>
/// Nothing is reserved or saved: this class never calls <c>ZarezerwujNumer</c>, <c>Zarezerwuj</c>,
/// <c>ZatwierdzRezerwacje</c> or <c>Zapisz</c> (a unit test reads this file for them), a new document has no reserved
/// number until <c>ZarezerwujNumer</c> or <c>Zapisz</c> (its preview shows "Auto" until then, per
/// <c>IObiektNumerowany.PodajPodgladNumeru</c>), and the business object is disposed at the end.
/// </para>
/// <para>Must run on the SDK thread.</para>
/// </remarks>
public static class NextNumberPreview
{
    public static readonly IReadOnlyList<string> Types = new[] { "FS", "PA", "KFS", "ZW", "RW", "PW" };

    /// <summary>Types numbered per warehouse: their create endpoints set <c>Dokument.Magazyn</c> before the number.</summary>
    private static readonly HashSet<string> WarehouseTypes = new(StringComparer.Ordinal) { "FS", "PA", "RW", "PW" };

    /// <summary>Types whose create endpoints also set <c>DataWprowadzenia</c>; the typed correction path does not.</summary>
    private static readonly HashSet<string> EntryDateTypes = new(StringComparer.Ordinal) { "FS", "PA", "RW", "PW" };

    public sealed record Outcome(string? FullNumber, int? Sequence, string Basis, string? Error);

    /// <summary>The type in upper case, or the error for one this endpoint does not preview.</summary>
    public static string? CheckType(string? type, out string normalized)
    {
        normalized = (type ?? string.Empty).Trim().ToUpperInvariant();
        return Types.Contains(normalized)
            ? null
            : $"type must be one of {string.Join(", ", Types)}; got '{type}'";
    }

    public static bool IsCorrection(string type) => type is "KFS" or "ZW";

    /// <summary>The symbol of the document a correction type corrects.</summary>
    public static string CorrectedSymbol(string type) => type == "ZW" ? "PA" : "FS";

    /// <summary>Reads the predicted number; the document is created in memory and disposed, never reserved or saved.</summary>
    public static Outcome Read(Uchwyt sfera, string type, DateTime day, Magazyn? magazyn, DokumentDS? original)
    {
        var basis = IsCorrection(type)
            ? original != null ? $"korekta dokumentu {original.Id}" : "korekta bez dokumentu korygowanego"
            : "nowy dokument z konfiguracji domyślnej";
        using var document = Create(sfera, type, original);
        return Predict(document, type, day, magazyn, basis);
    }

    /// <summary>
    /// The prediction for a document created in memory: the issue date (and the entry date and warehouse where the
    /// create endpoint sets them) go on its data entity, then the numbering source predicts. Reads only; the caller
    /// disposes the document.
    /// </summary>
    public static Outcome Predict(IDisposable document, string type, DateTime day, Magazyn? magazyn, string basis)
    {
        if (document is not IObiektNumerowany numbered)
        {
            return new Outcome(null, null, basis, $"{type}: this document takes no number");
        }

        var data = DataOf(document);
        data.DataWydaniaWystawienia = day;
        if (EntryDateTypes.Contains(type))
        {
            data.DataWprowadzenia = day;
        }

        if (magazyn != null && WarehouseTypes.Contains(type))
        {
            data.Magazyn = magazyn;
        }

        var source = numbered.ZrodloNumerowRezerwujace;
        if (source == null)
        {
            return new Outcome(null, null, basis, $"{type}: the document has no numbering source");
        }

        var preview = source.PodajPrzewidywanyNumer(data);
        var full = preview?.ToString()?.Trim();
        var sequence = ExplicitDocumentNumber.ParseNumber(preview?.Numer);
        if (sequence == null)
        {
            return new Outcome(full, null, basis, $"{type}: Nexo predicted no number ({full ?? "no preview"})");
        }

        var period = ExplicitDocumentNumber.PeriodMismatch(full, day);
        return period == null ? new Outcome(full, sequence, basis, null) : new Outcome(full, sequence, basis, $"{type}: {period}");
    }

    private static IDisposable Create(Uchwyt sfera, string type, DokumentDS? original)
    {
        switch (type)
        {
            case "FS":
                return sfera.DokumentySprzedazy().UtworzFaktureSprzedazy();
            case "PA":
                return sfera.DokumentySprzedazy().UtworzParagon();
            case "KFS":
                return original != null
                    ? sfera.KorektyDokumentowSprzedazy().UtworzKorekteFakturySprzedazy(original)
                    : sfera.KorektyDokumentowSprzedazy().UtworzKorekteFakturySprzedazy();
            case "ZW":
                return original != null
                    ? sfera.KorektyDokumentowSprzedazy().UtworzZwrotDoParagonu(original)
                    : sfera.KorektyDokumentowSprzedazy().UtworzZwrotDoParagonu();
            case "RW":
            {
                var konfiguracja = sfera.Konfiguracje().DaneDomyslne.RozchodWewnetrzny;
                return konfiguracja != null ? sfera.RozchodyWewnetrzne().Utworz(konfiguracja) : sfera.RozchodyWewnetrzne().Utworz();
            }
            case "PW":
            {
                var konfiguracja = sfera.Konfiguracje().DaneDomyslne.PrzychodWewnetrzny;
                return konfiguracja != null ? sfera.PrzychodyWewnetrzne().Utworz(konfiguracja) : sfera.PrzychodyWewnetrzne().Utworz();
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, "not a previewed type");
        }
    }

    private static Dokument DataOf(IDisposable document) => document switch
    {
        IDokumentSprzedazy sales => sales.Dane,
        IKorektaDokumentuSprzedazy correction => correction.Dane,
        IRozchodWewnetrzny rw => rw.Dane,
        IPrzychodWewnetrzny pw => pw.Dane,
        _ => throw new InvalidOperationException($"No data entity for {document.GetType().Name}"),
    };
}
