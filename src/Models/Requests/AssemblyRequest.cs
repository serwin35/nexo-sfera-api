using System.ComponentModel.DataAnnotations;

namespace NexoSferaApi.Models.Requests;

/// <summary>
/// Common header fields shared by production-order (ZPM / ZPR) create requests.
/// </summary>
public abstract class ProductionOrderRequestBase
{
    /// <summary>
    /// Kit (komplet) product ID.
    /// </summary>
    public int? ProductId { get; set; }

    /// <summary>
    /// Kit (komplet) product symbol (alternative to ProductId).
    /// </summary>
    public string? ProductSymbol { get; set; }

    /// <summary>
    /// Kit quantity (in <see cref="Unit"/> if given, otherwise in the kit's base unit).
    /// </summary>
    [Required]
    [Range(0.0001, double.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
    public decimal Quantity { get; set; }

    /// <summary>
    /// Optional unit symbol for the kit line (must be one of the product's units of measure, e.g. "szt.", "opak.").
    /// When omitted the SDK default unit for the kit is used.
    /// </summary>
    [StringLength(20)]
    public string? Unit { get; set; }

    /// <summary>
    /// Warehouse symbol of the kit (Dokument.Magazyn — where the assembled kit is received / the disassembled kit is issued from).
    /// </summary>
    [Required]
    [StringLength(20)]
    public string WarehouseSymbol { get; set; } = string.Empty;

    /// <summary>
    /// Warehouse symbol for the components (Dane.MagazynSkladnikow). Defaults to <see cref="WarehouseSymbol"/>.
    /// </summary>
    [StringLength(20)]
    public string? ComponentsWarehouseSymbol { get; set; }

    /// <summary>
    /// Issue date (DataWprowadzenia). Defaults to today when omitted.
    /// </summary>
    public DateTime? IssueDate { get; set; }

    /// <summary>
    /// Reserve document number (ZarezerwujNumer) before saving. Default false — number is assigned during Zapisz().
    /// </summary>
    public bool ReserveNumber { get; set; } = false;

    /// <summary>
    /// When true (SDK default) component quantities are recalculated after the kit quantity changes.
    /// Maps (inverted) to NiePrzeliczajSkladnikowPoZmianieIlosciKompletu. Null = leave SDK default.
    /// </summary>
    public bool? RecalculateComponentsOnQuantityChange { get; set; }

    /// <summary>
    /// Optional notes (Uwagi).
    /// </summary>
    [StringLength(500)]
    public string? Notes { get; set; }
}

/// <summary>
/// Request to create an assembly order (montaż - ZPM)
/// </summary>
public class CreateAssemblyRequest : ProductionOrderRequestBase
{
    /// <summary>
    /// Components (składniki) for assembly.
    /// Used only when <see cref="UseKompletDefinition"/> is false; otherwise components come from the kit definition in Nexo.
    /// </summary>
    public List<AssemblyComponentRequest>? Components { get; set; }

    /// <summary>
    /// Use the kit (komplet) definition from Nexo to auto-populate components.
    /// If true (default), <see cref="Components"/> is ignored.
    /// </summary>
    public bool UseKompletDefinition { get; set; } = true;
}

/// <summary>
/// Component line for a production order
/// </summary>
public class AssemblyComponentRequest
{
    /// <summary>
    /// Component product ID
    /// </summary>
    public int? ProductId { get; set; }

    /// <summary>
    /// Component product symbol (alternative to ProductId)
    /// </summary>
    public string? ProductSymbol { get; set; }

    /// <summary>
    /// Quantity of the component (in <see cref="Unit"/> if given, otherwise in the product's base unit)
    /// </summary>
    [Required]
    [Range(0.0001, double.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
    public decimal Quantity { get; set; }

    /// <summary>
    /// Optional unit symbol (must be one of the component's units of measure).
    /// </summary>
    [StringLength(20)]
    public string? Unit { get; set; }
}

/// <summary>
/// Request to create a disassembly order (demontaż / rozkompletowanie - ZPR)
/// </summary>
public class CreateDisassemblyRequest : ProductionOrderRequestBase
{
    /// <summary>
    /// Resulting components from disassembly.
    /// When omitted, components are populated from the kit definition (Rozkompletuj).
    /// </summary>
    public List<DisassemblyComponentRequest>? ResultingComponents { get; set; }
}

/// <summary>
/// Resulting component from disassembly
/// </summary>
public class DisassemblyComponentRequest : AssemblyComponentRequest
{
}

/// <summary>
/// Request to create an assembly order (ZPM) from a customer order (ZK) line.
/// </summary>
public class CreateAssemblyFromOrderLineRequest
{
    /// <summary>
    /// Customer order (ZK) ID
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int OrderId { get; set; }

    /// <summary>
    /// Customer order line (PozycjaDokumentu) ID
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int LineId { get; set; }

    /// <summary>
    /// Kit quantity to assemble. When omitted, the SDK uses the outstanding quantity of the order line.
    /// </summary>
    [Range(0.0001, double.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
    public decimal? Quantity { get; set; }

    /// <summary>
    /// Warehouse symbol of the kit (where the assembled kit is received)
    /// </summary>
    [Required]
    [StringLength(20)]
    public string WarehouseSymbol { get; set; } = string.Empty;

    /// <summary>
    /// Warehouse symbol for the components. Defaults to <see cref="WarehouseSymbol"/>.
    /// </summary>
    [StringLength(20)]
    public string? ComponentsWarehouseSymbol { get; set; }

    /// <summary>
    /// Issue date (DataWprowadzenia). Defaults to today when omitted.
    /// </summary>
    public DateTime? IssueDate { get; set; }

    /// <summary>
    /// Reserve document number (ZarezerwujNumer) before saving.
    /// </summary>
    public bool ReserveNumber { get; set; } = false;

    /// <summary>
    /// Optional notes (Uwagi).
    /// </summary>
    [StringLength(500)]
    public string? Notes { get; set; }
}
