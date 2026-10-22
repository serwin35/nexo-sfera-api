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
/// property (<c>Asortyment.WlasciwosciAsortymentu</c>), the next free variant number, the given symbol or
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

    private sealed record PlanItem(CreatedVariantDto Result, List<(WlasciwoscModelu Property, CechaAsortymentu Value)> Values, string? Symbol);

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
        var model = template.Model;
        if (model == null)
        {
            outcome.Errors.Add($"product {templateId} ({template.Symbol}) is not a variant; send the id of a variant of the model.");
            return outcome;
        }

        var properties = model.WlasciwosciModelu.Where(w => w.Wlasciwosc != null).OrderBy(w => w.LP).ToList();
        var siblings = model.Asortymenty.Where(a => !a.IsInRecycleBin).ToList();
        var existing = new Dictionary<string, Asortyment>(StringComparer.Ordinal);
        foreach (var sibling in siblings)
        {
            existing.TryAdd(Key(sibling.WlasciwosciAsortymentu
                .Where(w => w.Wlasciwosc != null)
                .Select(w => (w.Wlasciwosc.Nazwa, string.Join(", ", w.CechyAsortymentu.Select(c => c.Nazwa))))), sibling);
        }

        outcome.Result = new CreateVariantsResultDto
        {
            TemplateProductId = template.Id,
            TemplateSymbol = template.Symbol,
            ModelId = model.Id,
            ModelName = model.Nazwa,
            DryRun = request.DryRun,
        };

        var plan = new List<PlanItem>();
        for (var i = 0; i < request.Variants.Count; i++)
        {
            var variant = request.Variants[i];
            var result = new CreatedVariantDto { Symbol = string.IsNullOrWhiteSpace(variant.Symbol) ? null : variant.Symbol.Trim() };
            outcome.Result.Variants.Add(result);

            var values = new List<(WlasciwoscModelu Property, CechaAsortymentu Value)>();
            foreach (var given in variant.Values)
            {
                var name = given.Property.Trim();
                var property = properties.FirstOrDefault(p => string.Equals(p.Wlasciwosc.Nazwa, name, StringComparison.OrdinalIgnoreCase));
                if (property == null)
                {
                    result.Errors.Add($"variants[{i}]: the model has no property '{name}' (it has: {string.Join(", ", properties.Select(p => p.Wlasciwosc.Nazwa))}).");
                    continue;
                }

                var valueName = given.Value.Trim();
                var value = property.Wlasciwosc.CechyAsortymentu.FirstOrDefault(c => c.Nazwa == valueName);
                if (value == null)
                {
                    result.Errors.Add($"variants[{i}]: '{valueName}' is not a value of property '{property.Wlasciwosc.Nazwa}'; add it with POST /api/product-properties/{property.Wlasciwosc.Id}/values.");
                    continue;
                }

                values.Add((property, value));
                result.Values.Add(new ProductVariantPropertyDto { Property = property.Wlasciwosc.Nazwa, Value = value.Nazwa });
            }

            var missing = properties.Where(p => values.All(v => v.Property.Id != p.Id)).Select(p => p.Wlasciwosc.Nazwa).ToList();
            if (missing.Count > 0 && result.Errors.Count == 0)
                result.Errors.Add($"variants[{i}]: no value for {string.Join(", ", missing)}; every model property needs one.");

            if (result.Errors.Count == 0 && existing.TryGetValue(Key(values.Select(v => (v.Property.Wlasciwosc.Nazwa, v.Value.Nazwa))), out var twin))
            {
                result.Status = Exists;
                result.ProductId = twin.Id;
                result.Symbol = twin.Symbol;
                result.Name = twin.Nazwa;
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

        // Variants in the recycle bin keep their numbers, so they count too.
        var nextNumber = model.Asortymenty.Select(s => s.Numer ?? 0).DefaultIfEmpty(0).Max() + 1;
        foreach (var item in plan)
        {
            var result = item.Result;
            try
            {
                if (!Save(asortymenty, template, model, item, request.CopyComponents, nextNumber++)) break;
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

    /// <summary>Creates and saves one planned variant; false (with the errors on the result) when Nexo refuses it.</summary>
    private static bool Save(IAsortymenty asortymenty, Asortyment template, Model model, PlanItem item, bool copyComponents, int number)
    {
        var result = item.Result;
        using IAsortyment product = asortymenty.Utworz();
        product.WypelnijNaPodstawie(template);
        var dane = product.Dane;
        if (dane.Model == null || dane.Model.Id != model.Id) dane.Model = model;

        SetValues(dane, item.Values);
        ClearBarcodes(dane);
        if (!copyComponents) ClearComponents(product);

        dane.Numer = number;
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
    private static void SetValues(Asortyment dane, List<(WlasciwoscModelu Property, CechaAsortymentu Value)> values)
    {
        foreach (var (property, value) in values)
        {
            var link = dane.WlasciwosciAsortymentu.FirstOrDefault(w => w.Wlasciwosc != null && w.Wlasciwosc.Id == property.Wlasciwosc.Id);
            if (link == null)
            {
                link = new WlasciwoscAsortymentu
                {
                    Wlasciwosc = property.Wlasciwosc,
                    Wymagana = property.Wymagana,
                    Unikalna = property.Unikalna,
                    LP = property.LP,
                };
                dane.WlasciwosciAsortymentu.Add(link);
            }

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
    private static string Key(IEnumerable<(string Property, string Value)> values) =>
        string.Join("|", values
            .Select(v => $"{v.Property.Trim().ToLowerInvariant()}={v.Value.Trim()}")
            .OrderBy(v => v, StringComparer.Ordinal));
}
