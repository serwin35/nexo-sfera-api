using System.Reflection;
using InsERT.Moria.Asortymenty;
using InsERT.Moria.CennikiICeny;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using NexoSferaApi.Models.Dto;

namespace NexoSferaApi.Helpers;

/// <summary>
/// Typed, read-only access to price lists (<c>Cennik</c>), price levels (<c>PoziomCen</c>) and price list positions.
/// </summary>
/// <remarks>
/// <para>
/// Positions are read through the price list business object (<c>ICenniki.Znajdz(Cennik)</c> → <c>ICennik.Pozycje</c>,
/// i.e. <c>ICennikPozycje</c>), whose items are <c>IUproszczonaPozycjaCennika</c>. The entity collection
/// <c>Cennik.PozycjeCennika</c> is discouraged by the SDK and lacks the VAT rate, supplier ids, minimum margin, etc.
/// </para>
/// <para>
/// Every member below is accessed through the compiled SDK interfaces/entities (no <c>dynamic</c>), so a wrong member
/// name is a build error instead of a silent <c>null</c>. All methods must run on the SDK thread
/// (inside <c>ISferaService.ExecuteWithLockAsync</c>). Nothing here modifies or saves SDK objects.
/// </para>
/// </remarks>
public static class PriceListReader
{
    /// <summary>Maximum number of product ids accepted by a single positions lookup.</summary>
    public const int MaxProductIds = 200;

    /// <summary>Maximum page size of the positions listing.</summary>
    public const int MaxPageSize = 1000;

    private static readonly Lazy<IReadOnlyDictionary<Guid, string>> BasePriceFunctionNames =
        new(() => GuidConstantNames(typeof(FunkcjeWyliczaniaCenyBazowej), "FunkcjaWyliczaniaCenyBazowej", "_ID"));

    private static readonly Lazy<IReadOnlyDictionary<Guid, string>> RoundingFunctionNames =
        new(() => GuidConstantNames(typeof(FunkcjeWyrownywaniaCeny), string.Empty, "_Id"));

    /// <summary>Result of a positions lookup.</summary>
    public sealed class PositionsPage
    {
        public required PriceListDto PriceList { get; init; }
        public required List<PriceListPositionDto> Items { get; init; }
        public required int TotalCount { get; init; }
        public List<int> MissingProductIds { get; init; } = new();
    }

    /// <summary>One price list header by id, or <c>null</c> when it does not exist.</summary>
    public static PriceListDto? GetPriceList(Uchwyt sfera, int priceListId)
    {
        var cennik = FindPriceListEntity(sfera, priceListId);

        return cennik == null ? null : MapHeader(cennik, BuildBaseIndex(sfera, cennik));
    }

    /// <summary>
    /// Price list positions by price list id. With <paramref name="productIds"/> only these products are looked up
    /// (<c>ICennikPozycje.ZnajdzPozycjeCennika(Asortyment)</c>); otherwise the whole list is read
    /// (<c>Wszystkie</c>, or <c>WszystkieAktywne</c> with <paramref name="activeProductsOnly"/>).
    /// Returns <c>null</c> when the price list does not exist.
    /// </summary>
    public static PositionsPage? GetPositions(
        Uchwyt sfera,
        int priceListId,
        IReadOnlyCollection<int>? productIds,
        bool mainOnly,
        bool activeProductsOnly,
        int page,
        int pageSize)
    {
        var cennik = FindPriceListEntity(sfera, priceListId);
        if (cennik == null) return null;

        var header = MapHeader(cennik, BuildBaseIndex(sfera, cennik));

        using ICennik cennikBo = sfera.Cenniki().Znajdz(cennik);
        ICennikPozycje pozycje = cennikBo.Pozycje;

        List<IUproszczonaPozycjaCennika> positions;
        var missing = new List<int>();

        if (productIds is { Count: > 0 })
        {
            var asortymenty = LoadProducts(sfera, productIds);
            positions = new List<IUproszczonaPozycjaCennika>();

            foreach (var productId in productIds.Distinct())
            {
                if (!asortymenty.TryGetValue(productId, out var asortyment))
                {
                    missing.Add(productId);
                    continue;
                }

                var found = pozycje.ZnajdzPozycjeCennika(asortyment)?.ToList() ?? new List<IUproszczonaPozycjaCennika>();
                if (found.Count == 0) missing.Add(productId);
                positions.AddRange(found);
            }
        }
        else
        {
            positions = (activeProductsOnly ? pozycje.WszystkieAktywne : pozycje.Wszystkie).ToList();
        }

        // Tier counts are computed over every position of the product read above, before the main-only filter,
        // so a main position carries the number of its quantity tiers even when tiers are not returned.
        var tierCounts = positions
            .Where(p => !Read(() => p.Glowna, true))
            .GroupBy(p => Read(() => p.IdAsortymentu, 0))
            .ToDictionary(g => g.Key, g => g.Count());

        IEnumerable<IUproszczonaPozycjaCennika> filtered = positions;
        if (mainOnly) filtered = filtered.Where(p => Read(() => p.Glowna, false));

        var ordered = filtered
            .Select(p => new { Position = p, ProductId = Read(() => p.IdAsortymentu, 0), MinQuantity = Read(() => p.IloscMinAsortymentu, 0m), Id = Read(() => p.Id, 0) })
            .OrderBy(x => x.ProductId)
            .ThenBy(x => x.MinQuantity)
            .ThenBy(x => x.Id)
            .ToList();

        var items = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => MapPosition(x.Position, priceListId, tierCounts))
            .ToList();

        return new PositionsPage
        {
            PriceList = header,
            Items = items,
            TotalCount = ordered.Count,
            MissingProductIds = missing,
        };
    }

    /// <summary>
    /// Enriches legacy <see cref="PriceLevelDto"/> rows with the real <c>PoziomCen</c> members. Legacy fields
    /// (<c>IsDefault</c>, <c>IsActive</c>, <c>Priority</c>) are left untouched.
    /// </summary>
    public static void EnrichPriceLevels(Uchwyt sfera, IEnumerable<(PriceLevelDto Dto, object Entity)> levels)
    {
        var cenniki = sfera.Cenniki().Dane.Wszystkie().ToList();
        var listsByLevel = cenniki
            .Select(c => new { Cennik = c, LevelId = Read(() => c.PoziomCen?.Id, null) })
            .Where(x => x.LevelId.HasValue)
            .GroupBy(x => x.LevelId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Cennik).ToList());

        foreach (var (dto, entity) in levels)
        {
            if (entity is not PoziomCen poziom) continue;

            dto.CurrencySymbol = Read(() => poziom.Waluta?.Symbol, null);
            dto.BasePriceSourcePriceListId = Read(() => poziom.CennikCenyBazowejId, null);
            dto.BasePriceFunctionId = Read(() => poziom.FunkcjaWyliczaniaCenyBazowej, null);
            dto.BasePriceFunction = BasePriceFunctionName(dto.BasePriceFunctionId);

            if (listsByLevel.TryGetValue(poziom.Id, out var lists))
            {
                var baseIds = lists.Where(c => Read(() => c.Bazowy, false)).Select(c => c.Id).ToList();
                dto.PriceListIds = lists.Select(c => c.Id).OrderBy(id => id).ToList();
                dto.BasePriceListCount = baseIds.Count;
                dto.BasePriceListId = baseIds.Count == 1 ? baseIds[0] : null;
            }
        }
    }

    /// <summary>
    /// Enriches a legacy <see cref="PriceListDto"/> built from a <c>Cennik</c> entity with the real header fields.
    /// </summary>
    public static void EnrichPriceList(PriceListDto dto, object entity, IReadOnlyDictionary<int, List<int>> baseIndex)
    {
        if (entity is not Cennik cennik) return;

        FillHeader(dto, cennik, baseIndex);
    }

    /// <summary>Index price level id → ids of its base price lists, for <see cref="EnrichPriceList"/>.</summary>
    public static IReadOnlyDictionary<int, List<int>> BuildBaseIndex(Uchwyt sfera)
    {
        return BuildBaseIndex(sfera.Cenniki().Dane.Wszystkie().ToList());
    }

    private static Cennik? FindPriceListEntity(Uchwyt sfera, int priceListId)
    {
        return sfera.Cenniki().Dane.Wszystkie().FirstOrDefault(c => c.Id == priceListId);
    }

    private static IReadOnlyDictionary<int, List<int>> BuildBaseIndex(IEnumerable<Cennik> cenniki)
    {
        return cenniki
            .Where(c => Read(() => c.Bazowy, false))
            .Select(c => new { c.Id, LevelId = Read(() => c.PoziomCen?.Id, null) })
            .Where(x => x.LevelId.HasValue)
            .GroupBy(x => x.LevelId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());
    }

    private static IReadOnlyDictionary<int, List<int>> BuildBaseIndex(Uchwyt sfera, Cennik cennik)
    {
        var levelId = Read(() => cennik.PoziomCen?.Id, null);
        if (levelId == null) return new Dictionary<int, List<int>>();

        var baseIds = sfera.Cenniki().Dane.Wszystkie()
            .Where(c => c.Bazowy && c.PoziomCen != null && c.PoziomCen.Id == levelId.Value)
            .Select(c => c.Id)
            .ToList();

        return new Dictionary<int, List<int>> { [levelId.Value] = baseIds };
    }

    private static Dictionary<int, Asortyment> LoadProducts(Uchwyt sfera, IReadOnlyCollection<int> productIds)
    {
        var ids = productIds.Distinct().ToList();
        IAsortymentyDane dane = sfera.Asortymenty().Dane;

        // Wszystkie() skips deactivated products; their positions still exist in the price list.
        var products = dane.Wszystkie().Where(a => ids.Contains(a.Id)).ToList();
        var found = products.Select(a => a.Id).ToHashSet();
        if (found.Count < ids.Count)
        {
            var rest = ids.Where(id => !found.Contains(id)).ToList();
            products.AddRange(dane.Nieaktywne().Where(a => rest.Contains(a.Id)).ToList());
        }

        return products.GroupBy(a => a.Id).ToDictionary(g => g.Key, g => g.First());
    }

    private static PriceListDto MapHeader(Cennik cennik, IReadOnlyDictionary<int, List<int>> baseIndex)
    {
        // Legacy fields keep their historical values (members that do not exist on Cennik read as empty/false/0).
        var dto = new PriceListDto
        {
            Id = cennik.Id,
            Symbol = DynamicPropertyHelper.GetString(cennik, "Symbol") ?? string.Empty,
            Name = DynamicPropertyHelper.GetString(cennik, "Nazwa"),
            Description = Read(() => cennik.Opis, null),
            ValidFrom = DynamicPropertyHelper.GetDateTime(cennik, "DataOd"),
            ValidTo = DynamicPropertyHelper.GetDateTime(cennik, "DataDo"),
            IsActive = DynamicPropertyHelper.GetBool(cennik, "Aktywny"),
            CurrencySymbol = Read(() => cennik.Waluta?.Symbol, null),
            ItemCount = 0,
        };

        FillHeader(dto, cennik, baseIndex);

        return dto;
    }

    private static void FillHeader(PriceListDto dto, Cennik cennik, IReadOnlyDictionary<int, List<int>> baseIndex)
    {
        var statusCode = Read(() => cennik.Status, -1);
        var isBase = Read(() => cennik.Bazowy, false);
        var poziom = Read(() => cennik.PoziomCen, null);
        var parametry = Read(() => cennik.ParametryPozycji?.FirstOrDefault(p => p.Domyslne), null);

        dto.Title = Read(() => cennik.Tytul, null);
        dto.Subtitle = Read(() => cennik.Podtytul, null);
        dto.StatusCode = statusCode;
        dto.Status = statusCode switch
        {
            (int)StatusCennika.Definiowany => "defined",
            (int)StatusCennika.Zatwierdzony => "approved",
            (int)StatusCennika.Zamkniety => "closed",
            _ => null,
        };
        dto.IsApproved = statusCode == (int)StatusCennika.Zatwierdzony;
        dto.IsBase = isBase;
        dto.PriceLevelId = poziom?.Id;
        dto.PriceLevelSymbol = poziom == null ? null : Read(() => poziom.Symbol, null);
        dto.PriceLevelName = poziom == null ? null : Read(() => poziom.Nazwa, null);
        dto.MainPriceListId = !isBase && poziom != null && baseIndex.TryGetValue(poziom.Id, out var baseIds) && baseIds.Count == 1
            ? baseIds[0]
            : null;
        dto.CurrencySymbol ??= Read(() => cennik.Waluta?.Symbol, null);
        dto.CurrencyPricePrecision = Read<int?>(() => cennik.Waluta == null ? null : cennik.Waluta.PrecyzjaCeny, null);
        dto.CalculatedPriceKind = Read(() => cennik.RodzajKalkulowanejCeny, -1) switch
        {
            (int)RodzajCeny.Netto => "net",
            (int)RodzajCeny.Brutto => "gross",
            _ => null,
        };
        dto.DynamicPricing = Read(() => cennik.DynamicznaKalkulacjaCen, false);

        if (parametry != null)
        {
            dto.DefaultCalculationMethod = CalculationMethodName(Read<int?>(() => parametry.WyliczajPozycjeWedlug, null));
            dto.DefaultMargin = Read(() => parametry.Marza, null);
            dto.DefaultMarkup = Read(() => parametry.Narzut, null);
            dto.DefaultProfit = Read(() => parametry.Zysk, null);
            dto.DefaultBasePriceFunctionId = Read(() => parametry.FunkcjaWyliczaniaCenyBazowej, null);
            dto.DefaultBasePriceFunction = BasePriceFunctionName(dto.DefaultBasePriceFunctionId);
            dto.DefaultBasePriceSourcePriceListId = Read<int?>(() => parametry.CennikCenyBazowej == null ? null : parametry.CennikCenyBazowej.Id, null);
            dto.DefaultRoundingFunctionId = Read(() => parametry.FunkcjaWyrownywaniaCen, null);
            dto.DefaultRoundingFunction = RoundingFunctionName(dto.DefaultRoundingFunctionId);
        }

        dto.ZeroPricePolicy = Read(() => cennik.CenaZerowa, -1) switch
        {
            (int)KontrolaCenyZerowej.Ostrzegaj => "warn",
            (int)KontrolaCenyZerowej.Zezwalaj => "allow",
            _ => null,
        };
        dto.BelowMarginPolicy = Read(() => cennik.CenaPonizejMarzy, -1) switch
        {
            (int)KontrolaCenyPonizejMarzy.Ostrzegaj => "warn",
            (int)KontrolaCenyPonizejMarzy.ZezwalajDlaCenyZRabatem => "allowWithDiscount",
            (int)KontrolaCenyPonizejMarzy.ZezwalajZawsze => "allowAlways",
            _ => null,
        };
        dto.CreatedAt = Read<DateTime?>(() => cennik.DataUtworzenia, null);
        dto.ApprovedAt = Read(() => cennik.DataZatwierdzenia, null);
        dto.ClosedAt = Read(() => cennik.DataZamkniecia, null);
        dto.LastPriceUpdateAt = Read(() => cennik.DataOstatniejAktualizacji, null);
        dto.LastEditedAt = Read(() => cennik.DataOstatniejEdycji, null);
        dto.Schedule = MapSchedule(Read(() => cennik.HarmonogramWaznosci, null));
    }

    private static PriceListScheduleDto? MapSchedule(Harmonogram? harmonogram)
    {
        if (harmonogram == null) return null;

        var start = Read(() => harmonogram.DataPoczatkowa, null);
        var end = Read(() => harmonogram.DataKoncowa, null);
        var daysOfWeek = Read(() => harmonogram.DniTygodnia, 0L);
        var daysOfMonth = Read(() => harmonogram.DniMiesiaca, 0L);
        var months = Read(() => harmonogram.Miesiace, 0L);

        // An unset complex type comes back with default values: no dates and empty masks.
        if (start == null && end == null && daysOfWeek == 0 && daysOfMonth == 0 && months == 0) return null;

        var kindCode = Read(() => harmonogram.RodzajHarmonogramu, -1);

        return new PriceListScheduleDto
        {
            KindCode = kindCode,
            Kind = kindCode switch
            {
                (int)RodzajHarmonogramu.Codzienny => "daily",
                (int)RodzajHarmonogramu.Cotygodniowy => "weekly",
                (int)RodzajHarmonogramu.Comiesięczny => "monthly",
                _ => null,
            },
            StartDate = start,
            EndDate = end,
            DaysOfWeekMask = daysOfWeek,
            DaysOfMonthMask = daysOfMonth,
            MonthsMask = months,
        };
    }

    private static PriceListPositionDto MapPosition(
        IUproszczonaPozycjaCennika p,
        int priceListId,
        IReadOnlyDictionary<int, int> tierCounts)
    {
        var productId = Read(() => p.IdAsortymentu, 0);
        var isMain = Read(() => p.Glowna, false);
        var vatSymbol = Read(() => p.StawkaVATSprzedaz, null);
        decimal? vatRate = string.IsNullOrWhiteSpace(vatSymbol) ? null : Read<decimal?>(() => p.StawkaVATSprzedaz_Stawka, null);
        var calculationMethodCode = Read(() => p.DomyslneWyliczajPozycjeWedlug, null);
        var basePriceFunctionId = Read(() => p.FunkcjaWyliczaniaCenyBazowej, null);
        var roundingFunctionId = Read(() => p.FunkcjaWyrownywaniaCeny, null);
        var correction = Read(() => p.KorektaCeny, 0m);

        return new PriceListPositionDto
        {
            Id = Read(() => p.Id, 0),
            PriceListId = priceListId,
            ProductId = productId,
            ProductSymbol = Read(() => p.SymbolAsortymentu, null),
            ProductName = Read(() => p.NazwaAsortymentu, null),
            ProductKind = Read(() => p.RodzajAsortymentu, null),
            IsService = Read(() => p.CzyUsluga, false),
            IsKit = Read(() => p.CzyKomplet, false),
            IsMain = isMain,
            MinQuantity = Read(() => p.IloscMinAsortymentu, 0m),
            QuantityTierCount = isMain && tierCounts.TryGetValue(productId, out var tiers) ? tiers : 0,
            UnitId = Read(() => p.IdJednostkiMiaryAsortymentu, 0),
            UnitSymbol = Read(() => p.SymbolJednostkiMiary, null),
            PriceNet = Read(() => p.CenaNetto, 0m),
            PriceGross = Read(() => p.CenaBrutto, 0m),
            CurrencySymbol = Read(() => p.SymbolWaluty, null),
            CurrencyPrecision = Read(() => p.PrecyzjaWaluty, 2),
            VatRateSymbol = string.IsNullOrWhiteSpace(vatSymbol) ? null : vatSymbol,
            VatRate = vatRate,
            VatRatePercent = vatRate == null ? null : (vatRate.Value <= 1m ? vatRate.Value * 100m : vatRate.Value),
            BasePrice = Read(() => p.CenaBazowa, 0m),
            BasePriceCurrencySymbol = Read(() => p.SymbolWalutyCenyBazowej, null),
            BasePriceExchangeRate = Read(() => p.Kurs, 0m),
            CalculationPrice = Read(() => p.CenaKalkulacyjna, 0m),
            CalculationParameter = Read(() => p.ParametrKalkulacyjny, 0m),
            CalculationMethodCode = calculationMethodCode,
            CalculationMethod = CalculationMethodName(calculationMethodCode),
            BasePriceFunctionId = basePriceFunctionId,
            BasePriceFunction = BasePriceFunctionName(basePriceFunctionId),
            BasePriceSourcePriceListId = Read(() => p.IdCennikaCenyBazowej, null),
            RoundingFunctionId = roundingFunctionId,
            RoundingFunction = RoundingFunctionName(roundingFunctionId),
            PriceCorrection = Read(() => p.ZnakKorektyCeny, true) ? correction : -correction,
            PriceAfterCalculation = Read(() => p.CenaPoWyliczeniu, 0m),
            PriceAfterRounding = Read(() => p.CenaPoZaokragleniu, 0m),
            EstimatedCost = Read(() => p.SzacowanyKoszt, 0m),
            RegistryPrice = Read(() => p.CenaEwidencyjnaAsortymentu, 0m),
            MinimumMargin = Read(() => p.MinimalnaMarza, 0m),
            Margin = Read(() => p.Marza, null),
            Markup = Read(() => p.Narzut, null),
            Profit = Read(() => p.Zysk, 0m),
            DefaultDiscount = Read(() => p.RabatDomyslny, 0m),
            MaxDiscount = Read(() => p.RabatDopuszczalny, 0m),
            IsPriceLocked = Read(() => p.CenaSztywnaNaDokumencie, false),
            UpdatedAt = Read(() => p.DataAktualizacji, null),
            SupplierIds = Read(() => p.IdDostawcowAsortymentu?.ToList(), null) ?? new List<int>(),
            PrimarySupplierId = Read(() => p.IdPodstawowegoDostawcyAsortymentu, null),
            ManufacturerId = Read(() => p.IdProducentaAsortymentu, null),
        };
    }

    private static string? CalculationMethodName(int? code) => code switch
    {
        (int)MetodaWyliczaniaPozycjiCennika.WedlugMarzy => "margin",
        (int)MetodaWyliczaniaPozycjiCennika.WedlugNarzutu => "markup",
        (int)MetodaWyliczaniaPozycjiCennika.WedlugZysku => "profit",
        _ => null,
    };

    private static string? BasePriceFunctionName(Guid? id) =>
        id.HasValue && BasePriceFunctionNames.Value.TryGetValue(id.Value, out var name) ? name : null;

    private static string? RoundingFunctionName(Guid? id) =>
        id.HasValue && RoundingFunctionNames.Value.TryGetValue(id.Value, out var name) ? name : null;

    /// <summary>
    /// Maps the <c>public static readonly Guid</c> identifiers of an SDK constants class to their names
    /// (prefix/suffix stripped), e.g. <c>FunkcjaWyliczaniaCenyBazowejWgCenyEwidencyjnej_ID</c> → <c>WgCenyEwidencyjnej</c>.
    /// </summary>
    private static IReadOnlyDictionary<Guid, string> GuidConstantNames(Type type, string prefix, string suffix)
    {
        var result = new Dictionary<Guid, string>();
        try
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType != typeof(Guid) || field.GetValue(null) is not Guid id) continue;

                var name = field.Name;
                if (prefix.Length > 0 && name.StartsWith(prefix, StringComparison.Ordinal)) name = name[prefix.Length..];
                if (name.EndsWith(suffix, StringComparison.Ordinal)) name = name[..^suffix.Length];
                result.TryAdd(id, name);
            }
        }
        catch
        {
            // Names are informative only; the ids are always returned.
        }

        return result;
    }

    /// <summary>
    /// Reads one SDK member; an SDK exception on a single member yields <paramref name="fallback"/> instead of failing the
    /// whole response. Member names themselves are compile-time checked.
    /// </summary>
    private static T Read<T>(Func<T> getter, T fallback)
    {
        try
        {
            return getter();
        }
        catch
        {
            return fallback;
        }
    }
}
