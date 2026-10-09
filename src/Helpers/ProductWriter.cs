using InsERT.Moria.Asortymenty;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Requests;

namespace NexoSferaApi.Helpers;

/// <summary>
/// Typed write path of <c>PUT /api/products/{id}</c>. Every member is compile-checked, so a valid request can no longer
/// fail with a <c>RuntimeBinderException</c> (HTTP 500) on a member that does not exist on <c>Asortyment</c>.
/// </summary>
/// <remarks>
/// The update is all-or-nothing: <see cref="Validate"/> rejects fields that cannot be written here before the SDK is
/// touched, and <see cref="Apply"/> reports per-field errors without saving. The caller saves only when there are no
/// errors. Must run on the SDK thread.
/// </remarks>
public static class ProductWriter
{
    public const string Updated = "updated";
    public const string Unchanged = "unchanged";

    /// <summary>Result of <see cref="Apply"/>.</summary>
    public sealed class Outcome
    {
        public List<ProductFieldResultDto> Fields { get; } = new();
        public List<string> Errors { get; } = new();
        public bool HasChanges => Fields.Any(f => f.Status == Updated);
    }

    /// <summary>
    /// Request validation that needs no SDK access. Returns one message per rejected field, prefixed with the JSON field name.
    /// </summary>
    public static List<string> Validate(UpdateProductRequest request)
    {
        var errors = new List<string>();

        if (request.PriceNet.HasValue)
        {
            errors.Add("priceNet: not writable through PUT /api/products/{id}. Asortyment has no sales price; Nexo keeps " +
                       "prices in price list positions (Cennik), which this bridge does not write yet. Change the price " +
                       "in Subiekt or drop the field.");
        }

        if (request.VatRate != null)
        {
            errors.Add("vatRate: not supported by PUT /api/products/{id}. Change the sales VAT rate in Subiekt or drop the field.");
        }

        if (request.IsActive.HasValue)
        {
            errors.Add("isActive: not writable. Nexo has no activity flag on products; a product is inactive only when it is " +
                       "in the recycle bin (DELETE /api/products/{id}). Drop the field.");
        }

        if (request.Name != null && string.IsNullOrWhiteSpace(request.Name))
        {
            errors.Add("name: must not be empty.");
        }

        if (request.EAN != null)
        {
            if (string.IsNullOrWhiteSpace(request.EAN))
            {
                errors.Add("ean: must not be empty (removing barcodes is not supported here).");
            }
            else if (request.EAN.Trim().Any(char.IsWhiteSpace))
            {
                errors.Add("ean: must not contain whitespace.");
            }
        }

        if (request.EanUnitSymbol != null && request.EAN == null)
        {
            errors.Add("eanUnitSymbol: only allowed together with ean.");
        }

        if (!HasAnyWritableField(request) && errors.Count == 0)
        {
            errors.Add("No fields to update. Writable fields: name, description, ean (+ eanUnitSymbol), pkWiU, weight, volume.");
        }

        return errors;
    }

    /// <summary>
    /// Applies the request to the product business object (<c>IAsortyment.Dane</c>) without saving. Values equal
    /// to the current ones are reported as <see cref="Unchanged"/> and not assigned.
    /// </summary>
    public static Outcome Apply(Uchwyt sfera, IAsortyment product, UpdateProductRequest request)
    {
        var outcome = new Outcome();
        var dane = product.Dane;

        if (request.Name != null)
        {
            var value = request.Name.Trim();
            SetText(outcome, "name", () => dane.Nazwa, v => dane.Nazwa = v, value);
        }

        if (request.Description != null)
        {
            // An empty string clears the description.
            SetText(outcome, "description", () => dane.Opis, v => dane.Opis = v, request.Description);
        }

        if (request.PKWiU != null)
        {
            // An empty string clears PKWiU.
            SetText(outcome, "pkWiU", () => dane.PKWiU, v => dane.PKWiU = v, request.PKWiU.Trim());
        }

        if (request.EAN != null)
        {
            ApplyEan(sfera, dane, request.EAN.Trim(), request.EanUnitSymbol?.Trim(), outcome);
        }

        if (request.Weight.HasValue)
        {
            ApplyWeight(sfera, dane, request.Weight.Value, outcome);
        }

        if (request.Volume.HasValue)
        {
            ApplyVolume(dane, request.Volume.Value, outcome);
        }

        return outcome;
    }

    private static bool HasAnyWritableField(UpdateProductRequest request) =>
        request.Name != null || request.Description != null || request.EAN != null || request.PKWiU != null
        || request.Weight.HasValue || request.Volume.HasValue;

    private static void SetText(Outcome outcome, string field, Func<string?> get, Action<string> set, string value)
    {
        var current = get();
        if (string.Equals(current ?? string.Empty, value, StringComparison.Ordinal))
        {
            outcome.Fields.Add(Result(field, Unchanged, current, value));
            return;
        }

        try
        {
            set(value);
            outcome.Fields.Add(Result(field, Updated, current, value));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            outcome.Errors.Add($"{field}: Nexo rejected the value ({ex.Message}).");
        }
    }

    /// <summary>
    /// Sets the primary barcode (<c>PodstawowyKodKreskowy</c>) of the unit that currently provides the product EAN
    /// (or <paramref name="unitSymbol"/>, or the base unit when the product has no barcode). An existing code of that unit
    /// becomes primary; otherwise the current primary code is replaced; otherwise a new code is added the way the SDK FAQ
    /// shows (<c>KodyKreskowe.Add</c> then <c>PodstawowyKodKreskowy = kod</c>).
    /// </summary>
    private static void ApplyEan(Uchwyt sfera, Asortyment dane, string ean, string? unitSymbol, Outcome outcome)
    {
        JednostkaMiaryAsortymentu? unit;
        if (!string.IsNullOrEmpty(unitSymbol))
        {
            unit = dane.JednostkiMiar.FirstOrDefault(u =>
                string.Equals(ProductReader.UnitSymbol(u), unitSymbol, StringComparison.OrdinalIgnoreCase));
            if (unit == null)
            {
                outcome.Errors.Add($"eanUnitSymbol: unit '{unitSymbol}' is not assigned to this product.");
                return;
            }
        }
        else
        {
            unit = ProductReader.ResolveEan(dane)?.Unit ?? dane.PodstawowaJednostkaMiaryAsortymentu;
            if (unit == null)
            {
                outcome.Errors.Add("ean: the product has no base unit to hold the barcode.");
                return;
            }
        }

        string? current = ProductReader.BarcodesOf(unit).Where(c => c.IsPrimary).Select(c => c.Code).FirstOrDefault();
        if (current == ean)
        {
            outcome.Fields.Add(Result("ean", Unchanged, current, ean, ProductReader.UnitSymbol(unit)));
            return;
        }

        // Nexo treats a duplicate barcode on another product as a warning in some configurations; refuse it explicitly.
        var owners = ProductReader.FindProductIdsByBarcode(sfera, ean).Where(id => id != dane.Id).ToList();
        if (owners.Count > 0)
        {
            outcome.Errors.Add($"ean: barcode {ean} already belongs to product(s) {string.Join(", ", owners)}.");
            return;
        }

        try
        {
            var existing = unit.KodyKreskowe.FirstOrDefault(k => k != null && k.Kod != null && k.Kod.Trim() == ean);
            if (existing != null)
            {
                unit.PodstawowyKodKreskowy = existing;
            }
            else if (unit.PodstawowyKodKreskowy != null)
            {
                unit.PodstawowyKodKreskowy.Kod = ean;
            }
            else
            {
                var kod = new KodKreskowy();
                unit.KodyKreskowe.Add(kod);
                kod.Kod = ean;
                unit.PodstawowyKodKreskowy = kod;
            }

            outcome.Fields.Add(Result("ean", Updated, current, ean, ProductReader.UnitSymbol(unit)));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            outcome.Errors.Add($"ean: Nexo rejected the barcode ({ex.Message}).");
        }
    }

    /// <summary>Weight in kilograms → base unit <c>Masa</c>, converted to the unit's mass unit (kilograms when none is set).</summary>
    private static void ApplyWeight(Uchwyt sfera, Asortyment dane, decimal weightKg, Outcome outcome)
    {
        var unit = dane.PodstawowaJednostkaMiaryAsortymentu;
        if (unit == null)
        {
            outcome.Errors.Add("weight: the product has no base unit to hold the weight.");
            return;
        }

        var massUnit = unit.JednostkaMiaryMasy;
        var massUnitSymbol = massUnit?.Symbol?.Trim();
        decimal factor;
        if (massUnit == null)
        {
            factor = 1m;
        }
        else if (ProductReader.KilogramsPer(massUnitSymbol) is { } known)
        {
            factor = known;
        }
        else
        {
            outcome.Errors.Add($"weight: the base unit weight is kept in '{massUnitSymbol}', which is not a mass unit this bridge can convert kilograms to.");
            return;
        }

        var value = weightKg / factor;
        var currentKg = unit.Masa.HasValue ? unit.Masa.Value * factor : (decimal?)null;
        if (unit.Masa == value && massUnit != null)
        {
            outcome.Fields.Add(Result("weight", Unchanged, Format(currentKg), Format(weightKg), "kg"));
            return;
        }

        try
        {
            if (massUnit == null)
            {
                unit.JednostkaMiaryMasy = sfera.JednostkiMiar().DaneDomyslne.Kilogram;
            }

            unit.Masa = value;
            outcome.Fields.Add(Result("weight", Updated, Format(currentKg), Format(weightKg), "kg"));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            outcome.Errors.Add($"weight: Nexo rejected the value ({ex.Message}).");
        }
    }

    /// <summary>Volume → base unit <c>Objetosc</c>, in the unit's volume unit (which must already be set in Nexo).</summary>
    private static void ApplyVolume(Asortyment dane, decimal volume, Outcome outcome)
    {
        var unit = dane.PodstawowaJednostkaMiaryAsortymentu;
        if (unit == null)
        {
            outcome.Errors.Add("volume: the product has no base unit to hold the volume.");
            return;
        }

        var volumeUnit = unit.JednostkaMiaryObjetosci?.Symbol?.Trim();
        if (string.IsNullOrEmpty(volumeUnit))
        {
            outcome.Errors.Add("volume: the base unit has no volume unit in Nexo; set it in Subiekt first.");
            return;
        }

        if (unit.Objetosc == volume)
        {
            outcome.Fields.Add(Result("volume", Unchanged, Format(unit.Objetosc), Format(volume), volumeUnit));
            return;
        }

        try
        {
            var current = unit.Objetosc;
            unit.Objetosc = volume;
            outcome.Fields.Add(Result("volume", Updated, Format(current), Format(volume), volumeUnit));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            outcome.Errors.Add($"volume: Nexo rejected the value ({ex.Message}).");
        }
    }

    private static string? Format(decimal? value) =>
        value?.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static ProductFieldResultDto Result(string field, string status, string? oldValue, string? newValue, string? unit = null) =>
        new()
        {
            Field = field,
            Status = status,
            OldValue = oldValue,
            NewValue = newValue,
            Unit = unit,
        };
}
