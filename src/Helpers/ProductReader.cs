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

        EnrichUnits(dto.Units, asortyment);
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
