using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using NexoSferaApi.Models.Dto;

namespace NexoSferaApi.Helpers;

/// <summary>
/// Typed access to variant properties: the property dictionary (<c>Wlasciwosc</c>, e.g. "Kolor", "RM") with its allowed
/// values (<c>Wlasciwosc.CechyAsortymentu</c>), and the value a variant has for a property
/// (<c>Asortyment.WlasciwosciAsortymentu</c>), which is what the generated variant name shows ("Kolor: Piaskowy").
/// </summary>
/// <remarks>
/// A value is a <c>CechaAsortymentu</c> shared by every property and product that uses it, so renaming one renames it
/// everywhere; changing the value of one variant here touches only that variant. Must run on the SDK thread.
/// </remarks>
public static class VariantPropertyWriter
{
    public const string Updated = "updated";
    public const string Unchanged = "unchanged";

    /// <summary>Result of <see cref="AddValue"/> and <see cref="SetValue"/>.</summary>
    public sealed class Outcome
    {
        public bool Found { get; init; } = true;
        public string Status { get; set; } = Unchanged;
        public List<string> Errors { get; } = new();
        public ProductPropertyDto? Property { get; set; }
        public VariantPropertyChangeDto? Change { get; set; }
    }

    /// <summary>Every property with its allowed values, by name.</summary>
    public static List<ProductPropertyDto> Properties(Uchwyt sfera) =>
        sfera.Wlasciwosci().Dane.Wszystkie()
            .OrderBy(w => w.Nazwa)
            .Select(w => new { w.Id, w.Nazwa, w.Symbol, Values = w.CechyAsortymentu.Select(c => new { c.Id, c.Nazwa }) })
            .ToList()
            .Select(w => new ProductPropertyDto
            {
                Id = w.Id,
                Name = w.Nazwa,
                Symbol = string.IsNullOrWhiteSpace(w.Symbol) ? null : w.Symbol,
                Values = w.Values.OrderBy(v => v.Nazwa).Select(v => new ProductPropertyValueDto { Id = v.Id, Name = v.Nazwa }).ToList(),
            })
            .ToList();

    /// <summary>
    /// Adds the value <paramref name="name"/> to the property. An existing value of the same name (anywhere in the
    /// dictionary) is reused; otherwise a new one is created. Adding a value the property already has is unchanged.
    /// </summary>
    public static Outcome AddValue(Uchwyt sfera, int propertyId, string name)
    {
        var property = sfera.Wlasciwosci().Dane.Wszystkie().FirstOrDefault(w => w.Id == propertyId);
        if (property == null) return new Outcome { Found = false };

        var outcome = new Outcome();
        if (property.CechyAsortymentu.Any(c => c.Nazwa == name))
        {
            outcome.Property = Properties(sfera).First(p => p.Id == propertyId);
            return outcome;
        }

        var value = sfera.CechyAsortymentu().Dane.Wszystkie().FirstOrDefault(c => c.Nazwa == name);
        if (value == null)
        {
            using var created = sfera.CechyAsortymentu().Utworz();
            created.Dane.Nazwa = name;
            if (!created.Zapisz())
            {
                outcome.Errors.AddRange(SdkErrors.Of(created));
                if (outcome.Errors.Count == 0) outcome.Errors.Add($"Nexo refused to create the value '{name}'.");
                return outcome;
            }

            value = created.Dane;
        }

        using (var bo = sfera.Wlasciwosci().Znajdz(property))
        {
            bo.Dane.CechyAsortymentu.Add(value);
            if (!bo.Zapisz())
            {
                outcome.Errors.AddRange(SdkErrors.Of(bo));
                if (outcome.Errors.Count == 0) outcome.Errors.Add($"Nexo refused to add '{name}' to property '{property.Nazwa}'.");
                return outcome;
            }
        }

        outcome.Status = Updated;
        outcome.Property = Properties(sfera).First(p => p.Id == propertyId);
        return outcome;
    }

    /// <summary>
    /// Sets the value of <paramref name="propertyName"/> on a variant to <paramref name="valueName"/>, which must be an
    /// allowed value of that property, and with <paramref name="regenerateName"/> regenerates the variant name and
    /// description from the model (<c>WygenerujNazweWariantu</c>, <c>WygenerujOpisWariantu</c>).
    /// </summary>
    public static Outcome SetValue(Uchwyt sfera, int productId, string propertyName, string valueName, bool regenerateName)
    {
        var asortymenty = sfera.Asortymenty();
        var entity = asortymenty.Dane.Wszystkie().FirstOrDefault(a => a.Id == productId);
        if (entity == null) return new Outcome { Found = false };

        var outcome = new Outcome();
        using var product = asortymenty.Znajdz(entity);
        var dane = product.Dane;
        var link = dane.WlasciwosciAsortymentu.FirstOrDefault(w => w.Wlasciwosc != null && w.Wlasciwosc.Nazwa == propertyName);
        if (link == null)
        {
            var names = dane.WlasciwosciAsortymentu.Where(w => w.Wlasciwosc != null).Select(w => w.Wlasciwosc.Nazwa).ToList();
            outcome.Errors.Add($"property: product {productId} has no property '{propertyName}' (it has: {string.Join(", ", names)}).");
            return outcome;
        }

        var value = link.Wlasciwosc.CechyAsortymentu.FirstOrDefault(c => c.Nazwa == valueName);
        if (value == null)
        {
            outcome.Errors.Add($"value: '{valueName}' is not a value of property '{propertyName}'; add it with POST /api/product-properties/{link.Wlasciwosc.Id}/values.");
            return outcome;
        }

        var change = new VariantPropertyChangeDto
        {
            ProductId = productId,
            Symbol = dane.Symbol,
            Property = propertyName,
            OldValue = string.Join(", ", link.CechyAsortymentu.Select(c => c.Nazwa)),
            Value = valueName,
            OldName = dane.Nazwa,
        };

        if (link.CechyAsortymentu.Count == 1 && link.CechyAsortymentu.Contains(value))
        {
            change.Name = dane.Nazwa;
            outcome.Change = change;
            return outcome;
        }

        foreach (var old in link.CechyAsortymentu.ToList())
        {
            link.CechyAsortymentu.Remove(old);
        }

        link.CechyAsortymentu.Add(value);
        if (regenerateName)
        {
            product.WygenerujNazweWariantu();
            product.WygenerujOpisWariantu();
        }

        if (!product.Zapisz())
        {
            outcome.Errors.AddRange(SdkErrors.Of(product));
            if (outcome.Errors.Count == 0) outcome.Errors.Add("Nexo refused to save the product.");
            return outcome;
        }

        change.Name = product.Dane.Nazwa;
        outcome.Status = Updated;
        outcome.Change = change;
        return outcome;
    }
}
