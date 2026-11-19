using System.Globalization;
using InsERT.Moria.EgzekutorMagazynowy;
using InsERT.Moria.ModelDanych;

namespace NexoSferaApi.Helpers;

/// <summary>
/// Stock of one product on a day through <c>IMagazynier</c> (SDK 62.0.0.9537), bound to the typed overloads at compile
/// time: <c>IloscWolna(DateTime?, Asortyment, Magazyn)</c> and <c>IloscDostepna(DateTime?, Asortyment, Magazyn)</c>.
/// </summary>
/// <remarks>
/// SDK docs: <c>IloscWolna</c> is "maksymalna ilość asortymentu, jaką można wydać z konkretnego magazynu, w danym dniu",
/// <c>IloscDostepna</c> is "remanent na dzień bieżący/dowolny"; the date is the one "do której mają być uwzględniane
/// przychody (i rozchody)", null means "na dowolny dzień", a null warehouse means all warehouses. Overloads for
/// <c>Partia</c> and <c>Przyjecie</c> exist with the same shape, which is why a <c>dynamic</c> call can bind wrongly (or,
/// as the old 2-argument call of <c>free-quantity</c>, not at all).
/// </remarks>
public static class StockAtDate
{
    public const string DateFormat = "yyyy-MM-dd";
    public const string DefaultWarehouse = "MG";

    /// <summary>A calendar day written as yyyy-MM-dd, nothing else.</summary>
    public static bool TryParseDay(string? text, out DateTime day)
    {
        var parsed = DateTime.TryParseExact(text?.Trim(), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out day);
        day = day.Date;
        return parsed;
    }

    /// <summary>The last second of <paramref name="day"/>: receipts and issues of that day included.</summary>
    public static DateTime EndOfDay(DateTime day) => day.Date.AddDays(1).AddSeconds(-1);

    public static decimal Free(IMagazynier magazynier, DateTime? at, Asortyment asortyment, Magazyn? magazyn) =>
        magazynier.IloscWolna(at, asortyment, magazyn);

    public static decimal Available(IMagazynier magazynier, DateTime? at, Asortyment asortyment, Magazyn? magazyn) =>
        magazynier.IloscDostepna(at, asortyment, magazyn);
}
