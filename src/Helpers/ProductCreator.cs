using InsERT.Moria.Asortymenty;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Requests;

namespace NexoSferaApi.Helpers;

/// <summary>
/// Typed write paths of <c>POST /api/products</c> and <c>DELETE /api/products/{id}</c>. Creation follows the SDK sample
/// <c>PrzykladyKartoteki/ObslugaKartotek</c>: <c>IAsortymenty.Utworz()</c>, <c>WypelnijNaPodstawieSzablonu</c> (kind, base
/// unit, VAT), <c>AutoSymbol()</c> or the given symbol, the card fields (through <see cref="ProductWriter"/>), one
/// <c>Zapisz()</c>.
/// </summary>
/// <remarks>
/// All-or-nothing: every problem is collected before the save, and a business object that is not saved is disposed, which
/// discards it. Must run on the SDK thread.
/// </remarks>
public static class ProductCreator
{
    public const int MaxComponents = 100;

    /// <summary>Result of <see cref="Create"/>.</summary>
    public sealed class CreateOutcome
    {
        public int? ProductId { get; set; }
        public List<string> Errors { get; } = new();
    }

    /// <summary>Result of <see cref="Delete"/>.</summary>
    public sealed class DeleteOutcome
    {
        public bool Found { get; init; } = true;
        public bool AlreadyInRecycleBin { get; init; }
        public bool InRecycleBin { get; set; }
        public List<string> Errors { get; } = new();
    }

    /// <summary>Validation that needs no SDK access, one message per rejected field.</summary>
    public static List<string> Validate(CreateProductRequest request)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.Name)) errors.Add("name: required.");
        if (request.Symbol?.Trim().Any(char.IsWhiteSpace) == true) errors.Add("symbol: must not contain whitespace.");
        if (request.EAN != null && (string.IsNullOrWhiteSpace(request.EAN) || request.EAN.Trim().Any(char.IsWhiteSpace)))
            errors.Add("ean: must not be empty or contain whitespace.");
        if (request.PriceNet.HasValue)
            errors.Add("priceNet: not writable here. Nexo keeps sales prices in price lists: create the product, then use " +
                       "PUT /api/dictionary/price-lists/by-id/{id}/items.");
        if (request.Type == ProductType.Material) errors.Add("type: Material has no default Nexo template; send templateId and omit type.");
        if (request.TemplateId.HasValue && request.TemplateSymbol != null) errors.Add("templateId: send templateId or templateSymbol, not both.");
        if (request.TemplateId is <= 0) errors.Add("templateId: must be a positive product template id.");
        if (request.GroupId is <= 0) errors.Add("groupId: must be a positive product group id.");

        var components = request.Components ?? new List<CreateProductComponentRequest>();
        if (components.Count > MaxComponents) errors.Add($"components: at most {MaxComponents}, got {components.Count}.");
        for (var i = 0; i < components.Count; i++)
        {
            if (components[i].ProductId <= 0) errors.Add($"components[{i}].productId: must be a positive Nexo product id.");
            if (components[i].Quantity <= 0m) errors.Add($"components[{i}].quantity: must be greater than zero.");
        }

        if (components.Select(c => c.ProductId).Distinct().Count() != components.Count)
            errors.Add("components: every component product must be listed once.");

        return errors;
    }

    /// <summary>Creates and saves the product card. Call <see cref="Validate"/> first.</summary>
    public static CreateOutcome Create(Uchwyt sfera, CreateProductRequest request)
    {
        var outcome = new CreateOutcome();
        var template = ResolveTemplate(sfera, request, outcome.Errors);
        JednostkaMiary? baseUnit = null;
        if (request.BaseUnit != null)
        {
            var symbol = request.BaseUnit.Trim();
            baseUnit = sfera.JednostkiMiar().Dane.Wszystkie().FirstOrDefault(j => j.Symbol == symbol);
            if (baseUnit == null) outcome.Errors.Add($"baseUnit: unit '{symbol}' not found in the units dictionary.");
        }

        StawkaVat? vat = null;
        if (request.VatRate != null)
        {
            var symbol = request.VatRate.Trim();
            vat = sfera.StawkiVat().Dane.Wszystkie().FirstOrDefault(s => s.Symbol == symbol);
            if (vat == null) outcome.Errors.Add($"vatRate: VAT rate '{symbol}' not found.");
        }

        if (outcome.Errors.Count > 0 || template == null) return outcome;

        using IAsortyment product = sfera.Asortymenty().Utworz();
        product.WypelnijNaPodstawieSzablonu(template);
        var dane = product.Dane;

        var kind = request.Type ?? (request.TemplateId.HasValue || request.TemplateSymbol != null ? (ProductType?)null : ProductType.Goods);
        if (kind.HasValue && !IsKind(dane.Rodzaj, kind.Value))
        {
            outcome.Errors.Add($"type: the template creates a product of kind '{RodzajeAsortymentowExtensions.NaString(dane.Rodzaj)}', not {kind.Value}.");
        }

        if (baseUnit != null) product.JednostkiMiary.UstawPodstawowaJednostkeMiary(baseUnit);
        var baseSymbol = ProductReader.UnitSymbol(dane.PodstawowaJednostkaMiaryAsortymentu);
        if (baseUnit != null && !string.Equals(baseSymbol, baseUnit.Symbol?.Trim(), StringComparison.OrdinalIgnoreCase))
            outcome.Errors.Add($"baseUnit: Nexo did not set '{baseUnit.Symbol}' as the base unit (it is '{baseSymbol}').");
        if (request.SaleUnit != null && !string.Equals(request.SaleUnit.Trim(), baseSymbol, StringComparison.OrdinalIgnoreCase))
            outcome.Errors.Add($"saleUnit: a new product has only its base unit '{baseSymbol}'; add '{request.SaleUnit}' with POST /api/products/{{id}}/units after creating it.");
        if (request.PurchaseUnit != null && !string.Equals(request.PurchaseUnit.Trim(), baseSymbol, StringComparison.OrdinalIgnoreCase))
            outcome.Errors.Add($"purchaseUnit: a new product has only its base unit '{baseSymbol}'; add '{request.PurchaseUnit}' with POST /api/products/{{id}}/units after creating it.");

        if (vat != null)
        {
            dane.StawkaVatSprzedaz = vat;
            dane.StawkaVatKupno = vat;
        }

        if (string.IsNullOrWhiteSpace(request.Symbol)) product.AutoSymbol();

        var fields = ProductWriter.Apply(sfera, product, new UpdateProductRequest
        {
            Symbol = string.IsNullOrWhiteSpace(request.Symbol) ? null : request.Symbol.Trim(),
            Name = request.Name.Trim(),
            Description = request.Description,
            EAN = request.EAN?.Trim(),
            PKWiU = request.PKWiU?.Trim(),
            Weight = request.Weight,
            Volume = request.Volume,
            GroupId = request.GroupId,
        });
        outcome.Errors.AddRange(fields.Errors);

        AddComponents(product, request.Components, outcome.Errors);
        if (outcome.Errors.Count > 0) return outcome;

        if (!product.Zapisz())
        {
            outcome.Errors.AddRange(SdkErrors.Of(product));
            if (outcome.Errors.Count == 0) outcome.Errors.Add("Nexo refused to save the product.");
            return outcome;
        }

        outcome.ProductId = dane.Id;
        return outcome;
    }

    /// <summary>
    /// Deletes the product through its business object (<c>IObiektBiznesowy.Usun()</c>) and reports whether Nexo kept it in
    /// the recycle bin (<c>Dane.Nieaktywne()</c>). A product already in the recycle bin is reported as such, not as missing.
    /// </summary>
    public static DeleteOutcome Delete(Uchwyt sfera, int productId)
    {
        var asortymenty = sfera.Asortymenty();
        var entity = asortymenty.Dane.Wszystkie().FirstOrDefault(a => a.Id == productId);
        if (entity == null)
        {
            var inRecycleBin = asortymenty.Dane.Nieaktywne().Any(a => a.Id == productId);
            return inRecycleBin
                ? new DeleteOutcome { AlreadyInRecycleBin = true, InRecycleBin = true }
                : new DeleteOutcome { Found = false };
        }

        var outcome = new DeleteOutcome();
        using (IAsortyment product = asortymenty.Znajdz(entity))
        {
            if (!product.Usun())
            {
                outcome.Errors.AddRange(SdkErrors.Of(product));
                if (outcome.Errors.Count == 0) outcome.Errors.Add("Nexo refused to delete the product.");
                return outcome;
            }
        }

        outcome.InRecycleBin = asortymenty.Dane.Nieaktywne().Any(a => a.Id == productId);
        return outcome;
    }

    private static SzablonAsortymentu? ResolveTemplate(Uchwyt sfera, CreateProductRequest request, List<string> errors)
    {
        var szablony = sfera.SzablonyAsortymentu();
        if (request.TemplateId is int templateId)
        {
            var byId = szablony.Dane.Wszystkie().FirstOrDefault(s => s.Id == templateId);
            if (byId == null) errors.Add($"templateId: product template {templateId} not found.");
            return byId;
        }

        if (request.TemplateSymbol != null)
        {
            var symbol = request.TemplateSymbol.Trim();
            var matches = szablony.Dane.Wszystkie().Where(s => s.Symbol == symbol).Take(2).ToList();
            if (matches.Count != 1) errors.Add($"templateSymbol: {matches.Count} templates have the default symbol '{symbol}'; send templateId.");
            return matches.Count == 1 ? matches[0] : null;
        }

        var defaults = szablony.DaneDomyslne;
        var type = request.Type ?? ProductType.Goods;
        var template = type switch
        {
            ProductType.Service => defaults.Usluga,
            ProductType.Set => defaults.Komplet,
            _ => defaults.Towar,
        };
        if (template == null) errors.Add($"type: Nexo has no default {type} template; send templateId.");
        return template;
    }

    private static bool IsKind(RodzajAsortymentu rodzaj, ProductType type) => type switch
    {
        ProductType.Goods => RodzajeAsortymentowExtensions.CzyTowar(rodzaj),
        ProductType.Service => RodzajeAsortymentowExtensions.CzyUsluga(rodzaj),
        ProductType.Set => RodzajeAsortymentowExtensions.CzyKomplet(rodzaj),
        _ => false,
    };

    /// <summary>Kit components through the typed <c>IAsortyment.Skladniki</c> (implemented explicitly by the business object).</summary>
    private static void AddComponents(IAsortyment product, List<CreateProductComponentRequest>? components, List<string> errors)
    {
        if (components == null || components.Count == 0) return;

        if (!RodzajeAsortymentowExtensions.CzyKomplet(product.Dane.Rodzaj))
        {
            errors.Add("components: only a kit has components (type Set or a kit template).");
            return;
        }

        ISkladnikiKompletu skladniki = product.Skladniki;
        for (var i = 0; i < components.Count; i++)
        {
            var component = components[i];
            try
            {
                skladniki.Dodaj(component.ProductId, component.Quantity, component.UnitSymbol?.Trim() ?? string.Empty);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                errors.Add($"components[{i}]: Nexo rejected product {component.ProductId} ({ex.Message}).");
            }
        }
    }
}
