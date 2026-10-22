using InsERT.Moria.Asortymenty;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using NexoSferaApi.Models.Dto;

namespace NexoSferaApi.Helpers;

/// <summary>
/// Typed, read-only access to product (<c>Asortyment</c>) members that the dynamic mapper read under names that do not
/// exist in SDK 61.1 (<c>EAN</c>, <c>Aktywny</c>, <c>PKWIU</c>, <c>Masa</c>, <c>Objetosc</c>, <c>JednostkaSprzedazy.Symbol</c>,
/// <c>StawkaVatSprzedazy</c>, inventory <c>KodEan</c>).
/// </summary>
/// <remarks>
/// <para>
/// Barcodes live on the product units: <c>Asortyment.JednostkiMiar[]</c> (<c>JednostkaMiaryAsortymentu</c>) →
/// <c>PodstawowyKodKreskowy</c> (the unit's primary <c>KodKreskowy</c>) and <c>KodyKreskowe</c> (all codes of the unit).
/// <c>KodKreskowyOpakowania</c> is the barcode of the collective package and is not the EAN.
/// </para>
/// <para>
/// There is no "active" flag on <c>Asortyment</c>. Inactive objects in the SDK are the ones in the recycle bin
/// (<c>IRecyclable.IsInRecycleBin</c>, returned by <c>IDane.Nieaktywne()</c>); <c>Dane.Wszystkie()</c> never returns them.
/// </para>
/// <para>Must run on the SDK thread (inside <c>ISferaService.ExecuteWithLockAsync</c>). Nothing here modifies SDK objects.</para>
/// </remarks>
public static class ProductReader
{
    private static readonly Dictionary<string, decimal> KilogramsPerMassUnit = new(StringComparer.OrdinalIgnoreCase)
    {
        ["kg"] = 1m,
        ["g"] = 0.001m,
        ["dag"] = 0.01m,
        ["mg"] = 0.000001m,
        ["t"] = 1000m,
    };

    /// <summary>The product EAN together with the unit it was taken from.</summary>
    public sealed record EanSource(string Code, JednostkaMiaryAsortymentu Unit);

    /// <summary>A Nexo product kind (<c>RodzajAsortymentu</c>) as the API type, with its Nexo name.</summary>
    public sealed record Kind(ProductType Type, string? Name);

    /// <summary>The columns of one <c>GET /api/products</c> row, projected in SQL.</summary>
    public sealed class ListRow
    {
        public int Id { get; set; }
        public string? Symbol { get; set; }
        public string? Name { get; set; }
        public decimal? Price { get; set; }
        public int? GroupId { get; set; }
        public string? GroupName { get; set; }
        public int? KindId { get; set; }
        public int? ModelId { get; set; }
        public DateTimeOffset? CreatedAt { get; set; }
        public DateTimeOffset? ModifiedAt { get; set; }
    }

    /// <summary>Overwrites the fields of a <see cref="ProductDto"/> built by the dynamic mapper with the real SDK members.</summary>
    public static void Enrich(ProductDto dto, object entity)
    {
        if (entity is not Asortyment asortyment) return;

        var baseUnit = SdkMember.Read(() => asortyment.PodstawowaJednostkaMiaryAsortymentu, null);
        var units = ProductUnits(asortyment);

        dto.IsActive = IsActive(asortyment);
        dto.PKWiU = SdkMember.Text(() => asortyment.PKWiU);
        dto.BaseUnit = UnitSymbol(baseUnit);
        dto.SaleUnit = UnitSymbol(SdkMember.Read(() => asortyment.JednostkaSprzedazy, null));
        dto.PurchaseUnit = UnitSymbol(SdkMember.Read(() => asortyment.JednostkaZakupu, null));

        var ean = ResolveEan(asortyment);
        dto.EAN = ean?.Code;
        dto.EanUnitSymbol = ean == null ? null : UnitSymbol(ean.Unit);
        dto.Barcodes = units
            .SelectMany(unit => Barcodes(unit).Select(code => new ProductBarcodeDto
            {
                Code = code.Code,
                UnitSymbol = UnitSymbol(unit),
                IsPrimary = code.IsPrimary,
            }))
            .ToList();

        if (baseUnit != null)
        {
            var massUnit = SdkMember.Text(() => baseUnit.JednostkaMiaryMasy == null ? null : baseUnit.JednostkaMiaryMasy.Symbol);
            dto.Weight = ToKilograms(SdkMember.Read(() => baseUnit.Masa, null), massUnit);
            dto.WeightUnitSymbol = massUnit;
            dto.Volume = SdkMember.Read(() => baseUnit.Objetosc, null);
            dto.VolumeUnitSymbol = SdkMember.Text(() => baseUnit.JednostkaMiaryObjetosci == null ? null : baseUnit.JednostkaMiaryObjetosci.Symbol);
        }

        var salesVat = SdkMember.Read(() => asortyment.StawkaVatSprzedaz, null);
        if (salesVat != null)
        {
            dto.VatRate = SdkMember.Text(() => salesVat.Symbol);
            dto.VatRateSalesId = SdkMember.Read<Guid?>(() => salesVat.Id, null)?.ToString();
        }

        var rodzaj = SdkMember.Read(() => asortyment.Rodzaj, null);
        if (rodzaj != null)
        {
            var kind = KindOf(rodzaj);
            dto.Type = kind.Type;
            dto.KindName = kind.Name;
        }

        var model = SdkMember.Read(() => asortyment.Model, null);
        dto.ModelId = model == null ? null : SdkMember.Read<int?>(() => model.Id, null);
        dto.IsVariant = model != null;
        dto.VariantProperties = VariantProperties(asortyment);
        dto.Suppliers = Suppliers(asortyment);

        var header = SdkMember.Read(() => asortyment.Naglowek, null);
        if (header != null)
        {
            dto.CreatedAt = SdkMember.Read<DateTimeOffset?>(() => header.Utworzono, null)?.LocalDateTime ?? dto.CreatedAt;
            dto.ModifiedAt = SdkMember.Read<DateTimeOffset?>(() => header.Zmieniono, null)?.LocalDateTime ?? dto.ModifiedAt;
        }

        EnrichUnits(dto.Units, asortyment);
    }

    /// <summary>Value of each property of a variant (<c>Asortyment.WlasciwosciAsortymentu</c>), in model order.</summary>
    public static List<ProductVariantPropertyDto> VariantProperties(Asortyment asortyment) =>
        SdkMember.Read(() => asortyment.WlasciwosciAsortymentu
            .Where(w => w.Wlasciwosc != null)
            .OrderBy(w => w.LP ?? int.MaxValue)
            .Select(w => new ProductVariantPropertyDto
            {
                Property = w.Wlasciwosc.Nazwa,
                Value = string.Join(", ", w.CechyAsortymentu.Select(c => c.Nazwa)),
            })
            .ToList(), new List<ProductVariantPropertyDto>());

    /// <summary>Suppliers of the product (<c>AsortymentExtensions.Dostawcy</c>), primary first.</summary>
    public static List<ProductSupplierDto> Suppliers(Asortyment asortyment)
    {
        var primary = SdkMember.Read(() => asortyment.DostawcaPodstawowy(), null);
        return SdkMember.Read(() => asortyment.Dostawcy()
            .Where(d => d.Podmiot != null)
            .Select(d => new ProductSupplierDto
            {
                ContractorId = d.Podmiot.Id,
                ContractorSymbol = SdkMember.Text(() => d.Podmiot.Sygnatura == null ? null : d.Podmiot.Sygnatura.PelnaSygnatura),
                ContractorName = SdkMember.Text(() => d.Podmiot.NazwaSkrocona),
                IsPrimary = primary != null && ReferenceEquals(d, primary),
                SupplierProductSymbol = string.IsNullOrWhiteSpace(d.Symbol) ? null : d.Symbol,
                DeclaredPrice = d.CenaDeklarowana,
                DeclaredPriceIsGross = d.CenaBrutto,
            })
            .OrderByDescending(d => d.IsPrimary)
            .ThenBy(d => d.ContractorName)
            .ToList(), new List<ProductSupplierDto>());
    }

    /// <summary>Every product kind of the database by id (a small dictionary, read once per request).</summary>
    public static Dictionary<int, Kind> Kinds(Uchwyt sfera) =>
        sfera.RodzajeAsortymentu().Dane.Wszystkie()
            .ToList()
            .ToDictionary(r => r.Id, KindOf);

    /// <summary>
    /// The API type of a Nexo kind through the SDK kind checks (<c>CzyKomplet</c>, <c>CzyUsluga</c>, <c>CzyTowar</c>).
    /// Kinds that are none of them (returnable packaging, additional fees) are reported as Goods, as before; the Nexo name
    /// tells them apart.
    /// </summary>
    public static Kind KindOf(RodzajAsortymentu rodzaj)
    {
        var type = SdkMember.Read(() => rodzaj.CzyKomplet(), false) ? ProductType.Set
            : SdkMember.Read(() => rodzaj.CzyUsluga(), false) ? ProductType.Service
            : ProductType.Goods;
        return new Kind(type, SdkMember.Text(() => rodzaj.NaString()) ?? SdkMember.Text(() => rodzaj.Nazwa));
    }

    /// <summary>
    /// The filters of <c>GET /api/products</c> as one SQL query. Search matches the symbol, the name or a barcode of any
    /// unit (database collation, case-insensitive). <c>type</c> goes through the product kinds; Material matches no kind.
    /// Products in the recycle bin are never returned (<c>Dane.Wszystkie()</c>).
    /// </summary>
    public static IQueryable<Asortyment> Query(Uchwyt sfera, string? search, ProductType? type, bool activeOnly,
        DateTimeOffset? modifiedSince, IReadOnlyDictionary<int, Kind> kinds)
    {
        var dane = sfera.Asortymenty().Dane;
        IQueryable<Asortyment> products = activeOnly ? dane.WszystkieDostepne() : dane.Wszystkie();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var fragment = search.Trim();
            products = products.Where(a =>
                a.Symbol.Contains(fragment) ||
                a.Nazwa.Contains(fragment) ||
                a.JednostkiMiar.Any(j => j.KodyKreskowe.Any(k => k.Kod.Contains(fragment))));
        }

        if (modifiedSince.HasValue)
        {
            var since = modifiedSince.Value;
            products = products.Where(a => a.Naglowek != null && a.Naglowek.Zmieniono >= since);
        }

        if (type.HasValue)
        {
            var kindIds = kinds.Where(k => k.Value.Type == type.Value).Select(k => k.Key).ToList();
            products = type.Value == ProductType.Material || kindIds.Count == 0
                ? products.Where(a => false)
                : products.Where(a => a.Rodzaj != null && kindIds.Contains(a.Rodzaj.Id));
        }

        return products;
    }

    /// <summary>
    /// Stable order for paging: by id, or by card change time then id (<c>modified_asc</c> / <c>modified_desc</c>).
    /// </summary>
    public static IOrderedQueryable<Asortyment> Order(IQueryable<Asortyment> products, string? sortBy) => sortBy switch
    {
        "modified_asc" => products.OrderBy(a => a.Naglowek.Zmieniono).ThenBy(a => a.Id),
        "modified_desc" => products.OrderByDescending(a => a.Naglowek.Zmieniono).ThenByDescending(a => a.Id),
        _ => products.OrderBy(a => a.Id),
    };

    /// <summary>One page of list rows, projected in SQL (no product card is materialized).</summary>
    public static List<ListRow> ListRows(IOrderedQueryable<Asortyment> ordered, int skip, int take) =>
        ordered
            .Skip(skip)
            .Take(take)
            .Select(a => new ListRow
            {
                Id = a.Id,
                Symbol = a.Symbol,
                Name = a.Nazwa,
                Price = a.CenaEwidencyjna,
                GroupId = a.Grupa == null ? (int?)null : a.Grupa.Id,
                GroupName = a.Grupa == null ? null : a.Grupa.Nazwa,
                KindId = a.Rodzaj == null ? (int?)null : a.Rodzaj.Id,
                ModelId = a.Model == null ? (int?)null : a.Model.Id,
                CreatedAt = a.Naglowek == null ? (DateTimeOffset?)null : a.Naglowek.Utworzono,
                ModifiedAt = a.Naglowek == null ? (DateTimeOffset?)null : a.Naglowek.Zmieniono,
            })
            .ToList();

    /// <summary>The API list item of a row. Every listed product is active: the recycle bin is never listed.</summary>
    public static ProductListItemDto ToListItem(ListRow row, IReadOnlyDictionary<int, Kind> kinds)
    {
        var kind = row.KindId.HasValue && kinds.TryGetValue(row.KindId.Value, out var k) ? k : null;
        return new ProductListItemDto
        {
            Id = row.Id,
            Symbol = row.Symbol ?? string.Empty,
            Name = row.Name ?? string.Empty,
            Price = row.Price,
            GroupId = row.GroupId,
            GroupName = row.GroupName,
            IsActive = true,
            Type = kind?.Type ?? ProductType.Goods,
            KindName = kind?.Name,
            ModelId = row.ModelId,
            IsVariant = row.ModelId.HasValue,
            CreatedAt = row.CreatedAt,
            ModifiedAt = row.ModifiedAt,
        };
    }

    /// <summary>Adds the unit barcodes and mass/volume unit symbols to <see cref="ProductUnitDto"/> rows (matched by unit id).</summary>
    public static void EnrichUnits(List<ProductUnitDto> unitDtos, object entity)
    {
        if (entity is not Asortyment asortyment || unitDtos.Count == 0) return;

        var byId = ProductUnits(asortyment).ToDictionary(u => SdkMember.Read(() => u.Id, 0));
        foreach (var unitDto in unitDtos)
        {
            if (!byId.TryGetValue(unitDto.Id, out var unit)) continue;

            var codes = Barcodes(unit);
            unitDto.PrimaryBarcode = codes.FirstOrDefault(c => c.IsPrimary)?.Code;
            unitDto.Barcodes = codes.Select(c => c.Code).ToList();
            unitDto.WeightUnitSymbol = SdkMember.Text(() => unit.JednostkaMiaryMasy == null ? null : unit.JednostkaMiaryMasy.Symbol);
            unitDto.VolumeUnitSymbol = SdkMember.Text(() => unit.JednostkaMiaryObjetosci == null ? null : unit.JednostkaMiaryObjetosci.Symbol);
        }
    }

    /// <summary>A product is active unless it is in the recycle bin (the SDK has no other activity flag on products).</summary>
    public static bool IsActive(object entity)
    {
        return entity is not Asortyment asortyment || !SdkMember.Read(() => asortyment.IsInRecycleBin, false);
    }

    /// <summary>
    /// The product EAN: the primary barcode of the base unit, then any barcode of the base unit, then the same for the
    /// sale unit, then the first unit (by id) that has a barcode. <c>null</c> when no unit has a barcode.
    /// </summary>
    public static EanSource? ResolveEan(Asortyment asortyment)
    {
        var candidates = new List<JednostkaMiaryAsortymentu>();
        var baseUnit = SdkMember.Read(() => asortyment.PodstawowaJednostkaMiaryAsortymentu, null);
        var saleUnit = SdkMember.Read(() => asortyment.JednostkaSprzedazy, null);
        if (baseUnit != null) candidates.Add(baseUnit);
        if (saleUnit != null && !candidates.Contains(saleUnit)) candidates.Add(saleUnit);
        candidates.AddRange(ProductUnits(asortyment).Where(u => !candidates.Contains(u)).OrderBy(u => SdkMember.Read(() => u.Id, 0)));

        foreach (var unit in candidates)
        {
            var codes = Barcodes(unit);
            var code = codes.FirstOrDefault(c => c.IsPrimary) ?? codes.FirstOrDefault();
            if (code != null) return new EanSource(code.Code, unit);
        }

        return null;
    }

    /// <summary>Barcodes of one unit: the primary one first, then the remaining codes by id; empty codes skipped, no duplicates.</summary>
    public static List<(string Code, bool IsPrimary)> BarcodesOf(JednostkaMiaryAsortymentu unit) =>
        Barcodes(unit).Select(c => (c.Code, c.IsPrimary)).ToList();

    /// <summary>Ids of products whose symbol equals <paramref name="symbol"/>, one SQL query (recycle bin not included).</summary>
    public static List<int> FindProductIdsBySymbol(Uchwyt sfera, string symbol)
    {
        var trimmed = symbol.Trim();
        return sfera.Asortymenty().Dane.Wszystkie()
            .Where(a => a.Symbol == trimmed)
            .Select(a => a.Id)
            .OrderBy(id => id)
            .ToList();
    }

    /// <summary>Ids of products (any unit) having a barcode equal to <paramref name="code"/>, one SQL query.</summary>
    public static List<int> FindProductIdsByBarcode(Uchwyt sfera, string code)
    {
        var trimmed = code.Trim();
        return sfera.Asortymenty().Dane.Wszystkie()
            .Where(a => a.JednostkiMiar.Any(j =>
                j.KodyKreskowe.Any(k => k.Kod == trimmed) ||
                (j.PodstawowyKodKreskowy != null && j.PodstawowyKodKreskowy.Kod == trimmed)))
            .Select(a => a.Id)
            .OrderBy(id => id)
            .ToList();
    }

    /// <summary>Ids of products (any unit) having a barcode containing <paramref name="fragment"/>, one SQL query.</summary>
    public static HashSet<int> FindProductIdsByBarcodeFragment(Uchwyt sfera, string fragment)
    {
        var trimmed = fragment.Trim();
        return sfera.Asortymenty().Dane.Wszystkie()
            .Where(a => a.JednostkiMiar.Any(j => j.KodyKreskowe.Any(k => k.Kod.Contains(trimmed))))
            .Select(a => a.Id)
            .ToList()
            .ToHashSet();
    }

    /// <summary>
    /// The EAN of many products at once with the same precedence as <see cref="ResolveEan"/>, in one SQL query instead of
    /// lazy loads per product. Products without a barcode are absent from the map. <c>KodyKreskowe</c> holds every code of
    /// a unit, the primary one included (SDK FAQ: the primary code is added to <c>KodyKreskowe</c> and then referenced).
    /// </summary>
    public static Dictionary<int, string> ProductEans(Uchwyt sfera, IReadOnlyCollection<int>? productIds = null)
    {
        IQueryable<Asortyment> products = sfera.Asortymenty().Dane.Wszystkie();
        if (productIds != null)
        {
            var ids = productIds.Distinct().ToList();
            products = products.Where(a => ids.Contains(a.Id));
        }

        var rows = products
            .SelectMany(a => a.JednostkiMiar.SelectMany(j => j.KodyKreskowe.Select(k => new
            {
                ProductId = a.Id,
                UnitId = j.Id,
                CodeId = k.Id,
                k.Kod,
                IsPrimary = j.PodstawowyKodKreskowy != null && j.PodstawowyKodKreskowy.Id == k.Id,
                IsBase = a.PodstawowaJednostkaMiaryAsortymentu != null && a.PodstawowaJednostkaMiaryAsortymentu.Id == j.Id,
                IsSale = a.JednostkaSprzedazy != null && a.JednostkaSprzedazy.Id == j.Id,
            })))
            .ToList();

        return rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Kod))
            .GroupBy(r => r.ProductId)
            .ToDictionary(
                g => g.Key,
                g => g
                    .OrderBy(r => r.IsBase ? 0 : r.IsSale ? 1 : 2)
                    .ThenBy(r => r.UnitId)
                    .ThenBy(r => r.IsPrimary ? 0 : 1)
                    .ThenBy(r => r.CodeId)
                    .First().Kod.Trim());
    }

    /// <summary>Symbol of a product unit (<c>JednostkaMiaryAsortymentu.JednostkaMiary.Symbol</c>).</summary>
    public static string? UnitSymbol(JednostkaMiaryAsortymentu? unit)
    {
        if (unit == null) return null;
        return SdkMember.Text(() => unit.JednostkaMiary == null ? null : unit.JednostkaMiary.Symbol);
    }

    /// <summary>Kilograms per one unit of the given mass unit symbol, or <c>null</c> when the symbol is not a known mass unit.</summary>
    public static decimal? KilogramsPer(string? massUnitSymbol) =>
        massUnitSymbol != null && KilogramsPerMassUnit.TryGetValue(massUnitSymbol.Trim(), out var factor) ? factor : null;

    private static decimal? ToKilograms(decimal? mass, string? massUnitSymbol)
    {
        if (mass == null) return null;
        var factor = KilogramsPer(massUnitSymbol);
        return factor == null ? null : mass.Value * factor.Value;
    }

    private static List<JednostkaMiaryAsortymentu> ProductUnits(Asortyment asortyment) =>
        SdkMember.Read(() => asortyment.JednostkiMiar?.Where(u => u != null).ToList(), null) ?? new List<JednostkaMiaryAsortymentu>();

    private sealed record UnitBarcode(string Code, bool IsPrimary);

    private static List<UnitBarcode> Barcodes(JednostkaMiaryAsortymentu unit)
    {
        var result = new List<UnitBarcode>();
        var primary = SdkMember.Read(() => unit.PodstawowyKodKreskowy, null);
        var primaryCode = primary == null ? null : SdkMember.Text(() => primary.Kod);
        if (primaryCode != null) result.Add(new UnitBarcode(primaryCode, true));

        var others = SdkMember.Read(() => unit.KodyKreskowe?.Where(k => k != null).ToList(), null) ?? new List<KodKreskowy>();
        foreach (var kod in others.OrderBy(k => SdkMember.Read(() => k.Id, 0)))
        {
            var value = SdkMember.Text(() => kod.Kod);
            if (value == null || result.Any(c => c.Code == value)) continue;
            result.Add(new UnitBarcode(value, false));
        }

        return result;
    }
}
