using InsERT.Moria.Sfera;
using NexoSferaApi.Models.Dto;

namespace NexoSferaApi.Helpers;

/// <summary>
/// Read-only access to product models (<c>Model</c>, the Subiekt card that groups variants: "Koszulka T-Shirt haftowana
/// OSP ZOSP" with its properties Kolor and RM) and their variants.
/// </summary>
/// <remarks>
/// Everything is a SQL projection over <c>Dane.Wszystkie()</c>. Navigating collections of those untracked entities can
/// throw ("NoTracking merge option"), a projection never lazy loads. Must run on the SDK thread.
/// </remarks>
public static class ProductModelReader
{
    /// <summary>Models whose name or signature contains <paramref name="search"/> (all when empty), by name.</summary>
    public static List<ProductModelDto> List(Uchwyt sfera, string? search)
    {
        var term = search?.Trim();
        var query = sfera.Modele().Dane.Wszystkie();
        if (!string.IsNullOrEmpty(term))
            query = query.Where(m => m.Nazwa.Contains(term) || m.Sygnatura.PelnaSygnatura.Contains(term));

        var models = query
            .OrderBy(m => m.Nazwa)
            .Select(m => new
            {
                m.Id,
                m.Nazwa,
                Signature = m.Sygnatura.PelnaSygnatura,
                Properties = m.WlasciwosciModelu
                    .OrderBy(w => w.LP)
                    .Select(w => new { w.Wlasciwosc.Id, w.Wlasciwosc.Nazwa, w.Wlasciwosc.Symbol, w.Wymagana, Default = w.DomyslnaCechaAsortymentu.Nazwa }),
            })
            .ToList();

        var ids = models.Select(m => m.Id).ToList();
        var counts = sfera.Asortymenty().Dane.Wszystkie()
            .Where(a => a.Model != null && ids.Contains(a.Model.Id))
            .GroupBy(a => a.Model.Id)
            .Select(g => new { ModelId = g.Key, Count = g.Count() })
            .ToList()
            .ToDictionary(x => x.ModelId, x => x.Count);

        return models.Select(m => new ProductModelDto
        {
            Id = m.Id,
            Name = m.Nazwa,
            Signature = m.Signature,
            VariantCount = counts.TryGetValue(m.Id, out var count) ? count : 0,
            Properties = m.Properties.Select(p => new ProductModelPropertyDto
            {
                Id = p.Id,
                Name = p.Nazwa,
                Symbol = string.IsNullOrWhiteSpace(p.Symbol) ? null : p.Symbol,
                Required = p.Wymagana,
                DefaultValue = p.Default,
            }).ToList(),
        }).ToList();
    }

    /// <summary>One model with its variants (values of every property); null when it does not exist.</summary>
    public static ProductModelDto? Get(Uchwyt sfera, int modelId)
    {
        var model = List(sfera, null).FirstOrDefault(m => m.Id == modelId);
        if (model == null) return null;

        model.Variants = sfera.Asortymenty().Dane.Wszystkie()
            .Where(a => a.Model != null && a.Model.Id == modelId)
            .OrderBy(a => a.Numer)
            .Select(a => new
            {
                a.Id,
                a.Symbol,
                a.Nazwa,
                a.Numer,
                Values = a.WlasciwosciAsortymentu
                    .OrderBy(w => w.LP)
                    .Select(w => new { w.Wlasciwosc.Nazwa, Values = w.CechyAsortymentu.Select(c => c.Nazwa) }),
            })
            .ToList()
            .Select(a => new ProductModelVariantDto
            {
                ProductId = a.Id,
                Symbol = a.Symbol,
                Name = a.Nazwa,
                Number = a.Numer,
                Values = a.Values.Select(v => new ProductVariantPropertyDto { Property = v.Nazwa, Value = string.Join(", ", v.Values) }).ToList(),
            })
            .ToList();
        return model;
    }
}
