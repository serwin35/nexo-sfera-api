namespace NexoSferaApi.Models.Dto;

/// <summary>
/// Production order (ZPM = assembly / ZPR = disassembly) response DTO
/// </summary>
public class AssemblyDto
{
    public int Id { get; set; }

    /// <summary>
    /// Full document number (NumerWewnetrzny.PelnaSygnatura), e.g. "ZPM 1/2026"
    /// </summary>
    public string? Number { get; set; }

    /// <summary>
    /// Backward-compatible alias of <see cref="Number"/>
    /// </summary>
    public string? DocumentNumber => Number;

    public AssemblyType Type { get; set; }

    /// <summary>
    /// "assemble" or "disassemble"
    /// </summary>
    public string TypeCode => AssemblyTypeCodes.ToCode(Type);

    public string? Status { get; set; }
    public string? StatusSymbol { get; set; }

    /// <summary>
    /// Issue date (DataWprowadzenia)
    /// </summary>
    public DateTime IssueDate { get; set; }

    /// <summary>
    /// Backward-compatible alias of <see cref="IssueDate"/>
    /// </summary>
    public DateTime Date => IssueDate;

    public DateTime? ReceiptDate { get; set; }
    public DateTime? IssueMovementDate { get; set; }

    /// <summary>
    /// Kit warehouse (Dokument.Magazyn)
    /// </summary>
    public string? WarehouseSymbol { get; set; }
    public string? WarehouseName { get; set; }

    /// <summary>
    /// Components warehouse (Dane.MagazynSkladnikow)
    /// </summary>
    public string? ComponentsWarehouseSymbol { get; set; }
    public string? ComponentsWarehouseName { get; set; }

    public int ProductId { get; set; }
    public string? ProductSymbol { get; set; }
    public string? ProductName { get; set; }

    /// <summary>
    /// Kit quantity in <see cref="Unit"/>
    /// </summary>
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public decimal QuantityInBaseUnit { get; set; }

    /// <summary>
    /// Kit line net unit price (PozycjaKomplet.Cena.NettoPoRabacie)
    /// </summary>
    public decimal? UnitCost { get; set; }

    /// <summary>
    /// Kit line net value (PozycjaKomplet.Wartosc.NettoPoRabacie)
    /// </summary>
    public decimal? TotalCost { get; set; }

    /// <summary>
    /// Document net value (Wartosc.NettoPoRabacie)
    /// </summary>
    public decimal? DocumentNetValue { get; set; }
    public string? Currency { get; set; }

    /// <summary>
    /// Automatically generated PW (DokumentPrzychodujacyPW)
    /// </summary>
    public RelatedDocumentRefDto? AutoReceiptDocument { get; set; }

    /// <summary>
    /// Automatically generated RW (DokumentRozchodujacy)
    /// </summary>
    public RelatedDocumentRefDto? AutoIssueDocument { get; set; }

    public List<AssemblyComponentDto> Components { get; set; } = new();
    public string? Notes { get; set; }
    public string? Title { get; set; }
}

/// <summary>
/// Minimal reference to a related document
/// </summary>
public class RelatedDocumentRefDto
{
    public int Id { get; set; }
    public string? Number { get; set; }
}

/// <summary>
/// Production order component line (PozycjaSkladnik)
/// </summary>
public class AssemblyComponentDto
{
    public int LineId { get; set; }
    public int ProductId { get; set; }
    public string? ProductSymbol { get; set; }
    public string? ProductName { get; set; }

    /// <summary>
    /// Quantity per document in <see cref="Unit"/> (Ilosc)
    /// </summary>
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public decimal QuantityInBaseUnit { get; set; }

    /// <summary>
    /// Total quantity for all kits (IloscSumaryczna)
    /// </summary>
    public decimal? TotalQuantity { get; set; }

    /// <summary>
    /// Net unit price (Cena.NettoPoRabacie)
    /// </summary>
    public decimal? UnitCost { get; set; }

    /// <summary>
    /// Net line value (Wartosc.NettoPoRabacie)
    /// </summary>
    public decimal? TotalCost { get; set; }

    /// <summary>
    /// Total value for all kits (WartoscSumaryczna)
    /// </summary>
    public decimal? TotalValue { get; set; }

    /// <summary>
    /// Cost share of the component in the kit (UdzialKosztu)
    /// </summary>
    public decimal? CostShare { get; set; }
}

/// <summary>
/// Production order type
/// </summary>
public enum AssemblyType
{
    /// <summary>
    /// Assembly (montaż, ZPM) - combining components into a kit
    /// </summary>
    Assembly = 0,

    /// <summary>
    /// Disassembly (demontaż / rozkompletowanie, ZPR) - breaking a kit down into components
    /// </summary>
    Disassembly = 1
}

/// <summary>
/// String codes used on the API surface for <see cref="AssemblyType"/>
/// </summary>
public static class AssemblyTypeCodes
{
    public const string Assemble = "assemble";
    public const string Disassemble = "disassemble";
    public const string All = "all";

    public static string ToCode(AssemblyType type) => type == AssemblyType.Assembly ? Assemble : Disassemble;

    /// <summary>
    /// Parses a type filter. Returns null for "all" / empty. Throws ArgumentException for unknown values.
    /// Accepts English codes, Polish routes and enum names/values for backward compatibility.
    /// </summary>
    public static AssemblyType? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        switch (value.Trim().ToLowerInvariant())
        {
            case All:
                return null;
            case Assemble:
            case "assembly":
            case "montaz":
            case "montaż":
            case "zpm":
            case "0":
                return AssemblyType.Assembly;
            case Disassemble:
            case "disassembly":
            case "demontaz":
            case "demontaż":
            case "zpr":
            case "1":
                return AssemblyType.Disassembly;
            default:
                throw new ArgumentException($"Unknown assembly type '{value}'. Use 'assemble', 'disassemble' or 'all'.");
        }
    }
}

/// <summary>
/// Production order list item
/// </summary>
public class AssemblyListItemDto
{
    public int Id { get; set; }
    public string? Number { get; set; }
    public string? DocumentNumber => Number;
    public AssemblyType Type { get; set; }
    public string TypeCode => AssemblyTypeCodes.ToCode(Type);
    public string TypeName => Type == AssemblyType.Assembly ? "Montaż" : "Demontaż";
    public int ProductId { get; set; }
    public string? ProductSymbol { get; set; }
    public string? ProductName { get; set; }
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public decimal QuantityInBaseUnit { get; set; }
    public string? WarehouseSymbol { get; set; }
    public string? ComponentsWarehouseSymbol { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime Date => IssueDate;
    public string? Status { get; set; }
    public decimal? TotalCost { get; set; }
    public int ComponentCount { get; set; }
}

/// <summary>
/// Result of the max-assemblable-quantity calculation (PodajMaksymalnaIloscKompletu)
/// </summary>
public class AssemblyMaxQuantityDto
{
    public int ProductId { get; set; }
    public string? ProductSymbol { get; set; }
    public string? ProductName { get; set; }
    public string? WarehouseSymbol { get; set; }
    public string? ComponentsWarehouseSymbol { get; set; }

    /// <summary>
    /// Maximum number of kits that can be assembled from the current component stock
    /// </summary>
    public decimal MaxQuantity { get; set; }
    public string? Unit { get; set; }
    public List<AssemblyMaxQuantityComponentDto> Components { get; set; } = new();
}

public class AssemblyMaxQuantityComponentDto
{
    public int ProductId { get; set; }
    public string? Symbol { get; set; }
    public string? Name { get; set; }

    /// <summary>
    /// Component quantity required for one kit (in <see cref="Unit"/>)
    /// </summary>
    public decimal RequiredPerKit { get; set; }
    public string? Unit { get; set; }

    /// <summary>
    /// Available stock (IloscDostepna) in the components warehouse; null when it could not be determined
    /// </summary>
    public decimal? AvailableStock { get; set; }

    /// <summary>
    /// Kits assemblable from this component alone (AvailableStock / RequiredPerKit); null when unknown
    /// </summary>
    public decimal? MaxKitsFromComponent { get; set; }
}

/// <summary>
/// Component shortages of a production order
/// </summary>
public class AssemblyShortageDto
{
    public int AssemblyId { get; set; }
    public string? Number { get; set; }
    public AssemblyType Type { get; set; }
    public string TypeCode => AssemblyTypeCodes.ToCode(Type);
    public string? ComponentsWarehouseSymbol { get; set; }
    public bool HasShortages { get; set; }

    /// <summary>
    /// "sdk" when taken from IBraki (failed warehouse movements), "stock-comparison" when computed from current stock levels
    /// </summary>
    public string Source { get; set; } = "sdk";
    public List<AssemblyShortageItemDto> Items { get; set; } = new();
}

public class AssemblyShortageItemDto
{
    public int LineId { get; set; }
    public int ProductId { get; set; }
    public string? Symbol { get; set; }
    public string? Name { get; set; }
    public string? Unit { get; set; }

    /// <summary>
    /// Quantity required by the order line
    /// </summary>
    public decimal RequiredQuantity { get; set; }

    /// <summary>
    /// Quantity allocated / available (IloscZadysponowana or current available stock)
    /// </summary>
    public decimal AllocatedQuantity { get; set; }

    /// <summary>
    /// Missing quantity (IloscBrakujaca or Required - Available)
    /// </summary>
    public decimal MissingQuantity { get; set; }
    public decimal? MissingQuantityInBaseUnit { get; set; }
    public List<string> Errors { get; set; } = new();
}
