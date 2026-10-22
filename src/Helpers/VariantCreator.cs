using InsERT.Moria.Asortymenty;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Requests;

namespace NexoSferaApi.Helpers;

/// <summary>
/// Write path of <c>POST /api/products/{id}/variants</c>: new variants of the model of an existing variant, the way the
/// Subiekt variant generator adds them, so the owner does not have to generate them by hand.
/// </summary>
/// <remarks>
/// <para>
/// Each variant is a new card (<c>IAsortymenty.Utworz()</c>) filled from the template variant
/// (<c>IAsortyment.WypelnijNaPodstawie</c>: kind, group, units, VAT, suppliers, model), with its own value of every model
/// property (<c>Asortyment.WlasciwosciAsortymentu</c>), the given symbol or
/// <c>AutoSymbol()</c>, and a name generated from the model (<c>WygenerujNazweWariantu</c>). Barcodes are never copied,
/// because a barcode identifies one variant; kit components are copied only on request.
/// </para>
/// <para>
/// Validation of every variant runs before anything is saved; a value set the model already has is reported as
/// <c>exists</c>, so a repeated request creates nothing twice. The save phase stops at the first variant Nexo refuses;
/// the variants saved before it stay and are listed. Must run on the SDK thread.
/// </para>
/// </remarks>
public static class VariantCreator
{
    public const int MaxVariants = 50;
    public const string Created = "created";
    public const string Planned = "planned";
    public const string Exists = "exists";
    public const string Failed = "failed";
    public const string Skipped = "skipped";

    /// <summary>Result of <see cref="Create"/>.</summary>
    public sealed class Outcome
    {
        public bool Found { get; init; } = true;
        public List<string> Errors { get; } = new();
        public CreateVariantsResultDto? Result { get; set; }
        public bool Failed => Errors.Count > 0 || Result?.Variants.Any(v => v.Status == VariantCreator.Failed) == true;
    }

    /// <summary>A model property read once, as plain data, so nothing later navigates the SDK entities again.</summary>
    private sealed record ModelProperty(int Id, string Name, string? Symbol, bool Required, bool Unique, int Order, Dictionary<string, int> ValueIds);

    /// <summary>The model of the template variant, read through a business object (tracked, so lazy loading works).</summary>
    private sealed record ModelSnapshot(int Id, string? Name, List<ModelProperty> Properties, Dictionary<string, (int Id, string? Symbol, string? Name)> Existing);

    private sealed record PlanItem(CreatedVariantDto Result, List<(ModelProperty Property, int ValueId)> Values, string? Symbol);

    /// <summary>Validation that needs no SDK access, one message per rejected field.</summary>
    public static List<string> Validate(CreateVariantsRequest request)
    {
        var errors = new List<string>();
        var variants = request.Variants ?? new List<CreateVariantRequest>();
        if (variants.Count == 0) errors.Add("variants: at least one variant is required.");
        if (variants.Count > MaxVariants) errors.Add($"variants: at most {MaxVariants} per request, got {variants.Count}.");

        var symbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < variants.Count; i++)
        {
            var variant = variants[i];
            var values = variant.Values ?? new List<VariantPropertyValueRequest>();
            if (values.Count == 0) errors.Add($"variants[{i}].values: a value of every model property is required.");
            if (values.Any(v => string.IsNullOrWhiteSpace(v.Property) || string.IsNullOrWhiteSpace(v.Value)))
                errors.Add($"variants[{i}].values: property and value must not be empty.");
            if (values.GroupBy(v => v.Property?.Trim(), StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
                errors.Add($"variants[{i}].values: a property is given more than once.");
            if (!keys.Add(Key(values.Select(v => (v.Property ?? string.Empty, v.Value ?? string.Empty)))))
                errors.Add($"variants[{i}].values: the same values as an earlier variant of this request.");

            var symbol = variant.Symbol?.Trim();
            if (variant.Symbol != null && string.IsNullOrEmpty(symbol)) errors.Add($"variants[{i}].symbol: must not be empty when given.");
            if (symbol?.Any(char.IsWhiteSpace) == true) errors.Add($"variants[{i}].symbol: must not contain whitespace.");
            if (!string.IsNullOrEmpty(symbol) && !symbols.Add(symbol)) errors.Add($"variants[{i}].symbol: {symbol} is given more than once.");
        }

        return errors;
    }

    /// <summary>Creates (or with <c>dryRun</c> plans) the variants of the model of <paramref name="templateId"/>.</summary>
    public static Outcome Create(Uchwyt sfera, int templateId, CreateVariantsRequest request)
    {
        var asortymenty = sfera.Asortymenty();
        var template = asortymenty.Dane.Wszystkie().FirstOrDefault(a => a.Id == templateId);
        if (template == null) return new Outcome { Found = false };

        var outcome = new Outcome();
        var model = ReadModel(asortymenty, template);
        if (model == null)
        {
            outcome.Errors.Add($"product {templateId} ({template.Symbol}) is not a variant; send the id of a variant of the model.");
            return outcome;
        }

        var properties = model.Properties;

        outcome.Result = new CreateVariantsResultDto
        {
            TemplateProductId = template.Id,
            TemplateSymbol = template.Symbol,
            ModelId = model.Id,
            ModelName = model.Name,
            DryRun = request.DryRun,
        };

        var plan = new List<PlanItem>();
        for (var i = 0; i < request.Variants.Count; i++)
        {
            var variant = request.Variants[i];
            var result = new CreatedVariantDto { Symbol = string.IsNullOrWhiteSpace(variant.Symbol) ? null : variant.Symbol.Trim() };
            outcome.Result.Variants.Add(result);

            var values = new List<(ModelProperty Property, int ValueId)>();
            foreach (var given in variant.Values)
            {
                var name = given.Property.Trim();
                var property = properties.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                               ?? properties.FirstOrDefault(p => string.Equals(p.Symbol, name, StringComparison.OrdinalIgnoreCase));
                if (property == null)
                {
                    result.Errors.Add($"variants[{i}]: the model has no property '{name}' (it has: {string.Join(", ", properties.Select(p => p.Name))}).");
                    continue;
                }

                var valueName = given.Value.Trim();
                if (!property.ValueIds.TryGetValue(valueName, out var valueId))
                {
                    result.Errors.Add($"variants[{i}]: '{valueName}' is not a value of property '{property.Name}'; add it with POST /api/product-properties/{property.Id}/values.");
                    continue;
                }

                values.Add((property, valueId));
                result.Values.Add(new ProductVariantPropertyDto { Property = property.Name, Value = valueName });
            }

            var missing = properties.Where(p => values.All(v => v.Property.Id != p.Id)).Select(p => p.Name).ToList();
            if (missing.Count > 0 && result.Errors.Count == 0)
                result.Errors.Add($"variants[{i}]: no value for {string.Join(", ", missing)}; every model property needs one.");

            if (result.Errors.Count == 0 && model.Existing.TryGetValue(Key(result.Values.Select(v => (v.Property, v.Value ?? string.Empty))), out var twin))
            {
                result.Status = Exists;
                result.ProductId = twin.Id;
                result.Symbol = twin.Symbol;
                result.Name = twin.Name;
                continue;
            }

            if (result.Symbol != null)
            {
                var owners = ProductReader.FindProductIdsBySymbol(sfera, result.Symbol);
                if (owners.Count > 0) result.Errors.Add($"variants[{i}].symbol: {result.Symbol} already belongs to product(s) {string.Join(", ", owners)}.");
            }

            result.Status = result.Errors.Count > 0 ? Failed : Planned;
            if (result.Errors.Count == 0) plan.Add(new PlanItem(result, values, result.Symbol));
        }

        if (outcome.Failed || request.DryRun) return outcome;

        foreach (var item in plan)
        {
            var result = item.Result;
            try
            {
                if (!Save(sfera, templateId, model.Id, item, request.CopyComponents)) break;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
            {
                result.Status = Failed;
                result.Errors.Add($"Nexo rejected the variant ({ex.Message}).");
                break;
            }
        }

        // Variants after a refused one are not attempted.
        foreach (var item in plan.Where(p => p.Result.Status == Planned)) item.Result.Status = Skipped;
        return outcome;
    }

    /// <summary>
    /// Reads the model of <paramref name="template"/> through a business object of the template that is never saved.
    /// Entities from <c>Dane.Wszystkie()</c> are not tracked (NoTracking), and lazy loading a collection that the
    /// relationship fix-up has already partly filled (the model's variants contain the template) throws
    /// "When an object is returned with a NoTracking merge option, Load can only be called when the EntityCollection or
    /// EntityReference does not contain objects". Null when the template is not a variant.
    /// </summary>
    private static ModelSnapshot? ReadModel(IAsortymenty asortymenty, Asortyment template)
    {
        using var bo = asortymenty.Znajdz(template);
        var model = bo.Dane.Model;
        if (model == null) return null;

        var properties = model.WlasciwosciModelu
            .Where(w => w.Wlasciwosc != null)
            .OrderBy(w => w.LP)
            .Select(w => new ModelProperty(
                w.Wlasciwosc.Id,
                w.Wlasciwosc.Nazwa,
                string.IsNullOrWhiteSpace(w.Wlasciwosc.Symbol) ? null : w.Wlasciwosc.Symbol,
                w.Wymagana,
                w.Unikalna,
                w.LP,
                w.Wlasciwosc.CechyAsortymentu
                    .Where(c => c.Nazwa != null)
                    .GroupBy(c => c.Nazwa)
                    .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.Ordinal)))
            .ToList();

        var existing = new Dictionary<string, (int Id, string? Symbol, string? Name)>(StringComparer.Ordinal);
        foreach (var variant in model.Asortymenty.ToList())
        {
            if (!ProductReader.IsActive(variant)) continue;
            existing.TryAdd(Key(ProductReader.VariantProperties(variant).Select(v => (v.Property, v.Value ?? string.Empty))),
                (variant.Id, variant.Symbol, variant.Nazwa));
        }

        return new ModelSnapshot(model.Id, model.Nazwa, properties, existing);
    }

    /// <summary>
    /// Creates and saves one planned variant; false (with the errors on the result) when Nexo refuses it. Everything put on
    /// the new card is a fresh untracked entity (the template, the model, property values), the way the other writers
    /// assign groups and kinds.
    /// </summary>
    private static bool Save(Uchwyt sfera, int templateId, int modelId, PlanItem item, bool copyComponents)
    {
        var result = item.Result;
        var asortymenty = sfera.Asortymenty();
        var template = asortymenty.Dane.Wszystkie().First(a => a.Id == templateId);
        using IAsortyment product = asortymenty.Utworz();
        product.WypelnijNaPodstawie(template);
        var dane = product.Dane;
        if (dane.Model == null || dane.Model.Id != modelId) dane.Model = sfera.Modele().Dane.Wszystkie().First(m => m.Id == modelId);

        SetValues(sfera, dane, item.Values);
        ClearBarcodes(dane);
        if (!copyComponents) ClearComponents(product);

        if (item.Symbol != null) dane.Symbol = item.Symbol;
        else product.AutoSymbol();
        product.WygenerujNazweWariantu();
        product.WygenerujOpisWariantu();

        if (!product.Zapisz())
        {
            result.Status = Failed;
            result.Errors.AddRange(SdkErrors.Of(product));
            if (result.Errors.Count == 0) result.Errors.Add("Nexo refused to save the variant.");
            return false;
        }

        result.Status = Created;
        result.ProductId = dane.Id;
        result.Symbol = dane.Symbol;
        result.Name = dane.Nazwa;
        result.ComponentCount = dane.SkladnikiKompletu.Count;
        return true;
    }

    /// <summary>
    /// One value per model property. A property link copied from the template gets the new value; a missing one is added
    /// with the model's flags (required, unique, order).
    /// </summary>
    private static void SetValues(Uchwyt sfera, Asortyment dane, List<(ModelProperty Property, int ValueId)> values)
    {
        foreach (var (property, valueId) in values)
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

            var value = sfera.CechyAsortymentu().Dane.Wszystkie().First(c => c.Id == valueId);

            foreach (var old in link.CechyAsortymentu.ToList())
            {
                link.CechyAsortymentu.Remove(old);
            }

            link.CechyAsortymentu.Add(value);
        }
    }

    private static void ClearBarcodes(Asortyment dane)
    {
        foreach (var unit in dane.JednostkiMiar)
        {
            unit.PodstawowyKodKreskowy = null;
            foreach (var code in unit.KodyKreskowe.ToList())
            {
                unit.KodyKreskowe.Remove(code);
            }
        }
    }

    private static void ClearComponents(IAsortyment product)
    {
        foreach (var component in product.Dane.SkladnikiKompletu.ToList())
        {
            if (component.Skladnik != null) product.Skladniki.Usun(component.Skladnik);
        }
    }

    /// <summary>Order-independent key of a value set: "property=value" pairs sorted by property, case-insensitive names.</summary>
    internal static string Key(IEnumerable<(string Property, string Value)> values) =>
        string.Join("|", values
            .Select(v => $"{v.Property.Trim().ToLowerInvariant()}={v.Value.Trim()}")
            .OrderBy(v => v, StringComparer.Ordinal));
}
