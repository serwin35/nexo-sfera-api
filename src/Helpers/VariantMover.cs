using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Requests;

namespace NexoSferaApi.Helpers;

/// <summary>
/// Write path of <c>PUT /api/products/{id}/model</c>: an existing card becomes a variant of another model (or gets other
/// values in its own), with the target model's property values, the next free variant number, an optional new symbol and
/// a name generated from the model.
/// </summary>
/// <remarks>
/// Meant for unused cards generated in the wrong model (the empty Coyote CY kits of FLRA 340), so they can be reused instead
/// of deleted. Every check runs before the card is opened; a card that has a property the target model does not have is
/// refused, because removing a property link is not supported here. The card is opened as a business object
/// (<c>IAsortymenty.Znajdz</c>) like <see cref="VariantPropertyWriter"/>. Must run on the SDK thread.
/// </remarks>
public static class VariantMover
{
    public const string Moved = "moved";
    public const string Planned = "planned";
    public const string Unchanged = "unchanged";

    /// <summary>Result of <see cref="Move"/>.</summary>
    public sealed class Outcome
    {
        public bool Found { get; init; } = true;
        public List<string> Errors { get; } = new();
        public MovedVariantDto? Result { get; set; }
    }

    private sealed record TargetProperty(int Id, string Name, string? Symbol, bool Required, bool Unique, int Order, Dictionary<string, int> ValueIds);

    /// <summary>Validation that needs no SDK access, one message per rejected field.</summary>
    public static List<string> Validate(MoveToModelRequest request)
    {
        var errors = new List<string>();
        if (request.ModelId <= 0) errors.Add("modelId: must be a positive model id.");
        var values = request.Values ?? new List<VariantPropertyValueRequest>();
        if (values.Count == 0) errors.Add("values: a value of every property of the target model is required.");
        if (values.Any(v => string.IsNullOrWhiteSpace(v.Property) || string.IsNullOrWhiteSpace(v.Value)))
            errors.Add("values: property and value must not be empty.");
        if (values.GroupBy(v => v.Property?.Trim(), StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            errors.Add("values: a property is given more than once.");
        var symbol = request.Symbol?.Trim();
        if (request.Symbol != null && string.IsNullOrEmpty(symbol)) errors.Add("symbol: must not be empty when given.");
        if (symbol?.Any(char.IsWhiteSpace) == true) errors.Add("symbol: must not contain whitespace.");
        return errors;
    }

    /// <summary>Moves (or with <c>dryRun</c> plans moving) product <paramref name="productId"/> into the target model.</summary>
    public static Outcome Move(Uchwyt sfera, int productId, MoveToModelRequest request)
    {
        var asortymenty = sfera.Asortymenty();
        var card = asortymenty.Dane.Wszystkie()
            .Where(a => a.Id == productId)
            .Select(a => new
            {
                a.Id,
                a.Symbol,
                a.Nazwa,
                ModelId = a.Model == null ? (int?)null : a.Model.Id,
                Properties = a.WlasciwosciAsortymentu.Select(w => new { w.Wlasciwosc.Id, w.Wlasciwosc.Nazwa }),
            })
            .FirstOrDefault();
        if (card == null) return new Outcome { Found = false };

        var outcome = new Outcome();
        var target = ProductModelReader.Get(sfera, request.ModelId);
        if (target == null)
        {
            outcome.Errors.Add($"modelId: model {request.ModelId} not found (GET /api/product-models).");
            return outcome;
        }

        var properties = sfera.Modele().Dane.Wszystkie()
            .Where(m => m.Id == request.ModelId)
            .SelectMany(m => m.WlasciwosciModelu)
            .OrderBy(w => w.LP)
            .Select(w => new
            {
                w.Wlasciwosc.Id,
                w.Wlasciwosc.Nazwa,
                w.Wlasciwosc.Symbol,
                w.Wymagana,
                w.Unikalna,
                w.LP,
                Values = w.Wlasciwosc.CechyAsortymentu.Select(c => new { c.Id, c.Nazwa }),
            })
            .ToList()
            .Select(w => new TargetProperty(w.Id, w.Nazwa, string.IsNullOrWhiteSpace(w.Symbol) ? null : w.Symbol, w.Wymagana, w.Unikalna, w.LP,
                w.Values.Where(v => v.Nazwa != null).GroupBy(v => v.Nazwa).ToDictionary(g => g.Key, g => g.First().Id, StringComparer.Ordinal)))
            .ToList();

        var extra = card.Properties.Where(p => properties.All(t => t.Id != p.Id)).Select(p => p.Nazwa).ToList();
        if (extra.Count > 0)
            outcome.Errors.Add($"product {productId} has properties the target model does not have ({string.Join(", ", extra)}); removing them is not supported.");

        var values = new List<(TargetProperty Property, int ValueId, string Value)>();
        foreach (var given in request.Values)
        {
            var name = given.Property.Trim();
            var property = properties.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                           ?? properties.FirstOrDefault(p => string.Equals(p.Symbol, name, StringComparison.OrdinalIgnoreCase));
            if (property == null)
            {
                outcome.Errors.Add($"values: the model has no property '{name}' (it has: {string.Join(", ", properties.Select(p => p.Name))}).");
                continue;
            }

            var valueName = given.Value.Trim();
            if (!property.ValueIds.TryGetValue(valueName, out var valueId))
            {
                outcome.Errors.Add($"values: '{valueName}' is not a value of property '{property.Name}'; add it with POST /api/product-properties/{property.Id}/values.");
                continue;
            }

            values.Add((property, valueId, valueName));
        }

        var missing = properties.Where(p => values.All(v => v.Property.Id != p.Id)).Select(p => p.Name).ToList();
        if (missing.Count > 0) outcome.Errors.Add($"values: no value for {string.Join(", ", missing)}; every model property needs one.");

        var symbol = string.IsNullOrWhiteSpace(request.Symbol) ? card.Symbol : request.Symbol.Trim();
        var result = new MovedVariantDto
        {
            ProductId = productId,
            OldModelId = card.ModelId,
            ModelId = target.Id,
            OldSymbol = card.Symbol,
            Symbol = symbol,
            OldName = card.Nazwa,
            Values = values.Select(v => new ProductVariantPropertyDto { Property = v.Property.Name, Value = v.Value }).ToList(),
        };
        outcome.Result = result;

        var key = VariantCreator.Key(result.Values.Select(v => (v.Property, v.Value ?? string.Empty)));
        var twin = (target.Variants ?? new List<ProductModelVariantDto>())
            .FirstOrDefault(v => v.ProductId != productId && VariantCreator.Key(v.Values.Select(x => (x.Property, x.Value ?? string.Empty))) == key);
        if (twin != null && missing.Count == 0) outcome.Errors.Add($"values: the model already has a variant with these values ({twin.Symbol}, id {twin.ProductId}).");

        if (!string.Equals(symbol, card.Symbol, StringComparison.Ordinal))
        {
            var owners = ProductReader.FindProductIdsBySymbol(sfera, symbol).Where(id => id != productId).ToList();
            if (owners.Count > 0) outcome.Errors.Add($"symbol: {symbol} already belongs to product(s) {string.Join(", ", owners)}.");
        }

        if (outcome.Errors.Count > 0) return outcome;

        var self = (target.Variants ?? new List<ProductModelVariantDto>()).FirstOrDefault(v => v.ProductId == productId);
        if (self != null && VariantCreator.Key(self.Values.Select(x => (x.Property, x.Value ?? string.Empty))) == key
            && string.Equals(symbol, card.Symbol, StringComparison.Ordinal))
        {
            result.Status = Unchanged;
            result.Name = card.Nazwa;
            return outcome;
        }

        if (request.DryRun)
        {
            result.Status = Planned;
            return outcome;
        }

        // Variants in the recycle bin keep their numbers, so they count too.
        var numbers = asortymenty.Dane.Wszystkie().Where(a => a.Model != null && a.Model.Id == target.Id).Select(a => a.Numer)
            .ToList()
            .Concat(asortymenty.Dane.Nieaktywne().Where(a => a.Model != null && a.Model.Id == target.Id).Select(a => a.Numer).ToList());
        var nextNumber = numbers.Select(n => n ?? 0).DefaultIfEmpty(0).Max() + 1;

        var entity = asortymenty.Dane.Wszystkie().First(a => a.Id == productId);
        using var product = asortymenty.Znajdz(entity);
        var dane = product.Dane;
        if (dane.Model == null || dane.Model.Id != target.Id)
        {
            dane.Model = sfera.Modele().Dane.Wszystkie().First(m => m.Id == target.Id);
            dane.Numer = nextNumber;
        }

        foreach (var (property, valueId, _) in values)
        {
            var link = dane.WlasciwosciAsortymentu.FirstOrDefault(w => w.Wlasciwosc != null && w.Wlasciwosc.Id == property.Id);
            if (link == null)
            {
                link = new WlasciwoscAsortymentu
                {
                    Wlasciwosc = sfera.Wlasciwosci().Dane.Wszystkie().First(w => w.Id == property.Id),
                    Wymagana = property.Required,
                    Unikalna = property.Unique,
                    LP = property.Order,
                };
                dane.WlasciwosciAsortymentu.Add(link);
            }

            // The card is tracked, so the value is taken from its own graph (as VariantPropertyWriter does).
            var value = link.Wlasciwosc.CechyAsortymentu.FirstOrDefault(c => c.Id == valueId)
                        ?? sfera.CechyAsortymentu().Dane.Wszystkie().First(c => c.Id == valueId);
            foreach (var old in link.CechyAsortymentu.ToList())
            {
                link.CechyAsortymentu.Remove(old);
            }

            link.CechyAsortymentu.Add(value);
        }

        if (!string.Equals(symbol, dane.Symbol, StringComparison.Ordinal)) dane.Symbol = symbol;
        product.WygenerujNazweWariantu();
        product.WygenerujOpisWariantu();

        if (!product.Zapisz())
        {
            outcome.Errors.AddRange(SdkErrors.Of(product));
            if (outcome.Errors.Count == 0) outcome.Errors.Add("Nexo refused to save the product.");
            return outcome;
        }

        result.Status = Moved;
        result.Symbol = product.Dane.Symbol;
        result.Name = product.Dane.Nazwa;
        return outcome;
    }
}
