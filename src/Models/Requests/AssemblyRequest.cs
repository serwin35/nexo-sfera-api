using System.ComponentModel.DataAnnotations;

namespace NexoSferaApi.Models.Requests;

/// <summary>
/// Request to create an assembly (montaż - ZM)
/// </summary>
public class CreateAssemblyRequest
{
    /// <summary>
    /// Finished product ID to assemble
    /// </summary>
    public int? ProductId { get; set; }

    /// <summary>
    /// Finished product symbol (alternative to ProductId)
    /// </summary>
    public string? ProductSymbol { get; set; }

    /// <summary>
    /// Quantity to assemble
    /// </summary>
    [Required]
    [Range(0.0001, double.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
    public decimal Quantity { get; set; }

    /// <summary>
    /// Warehouse symbol
    /// </summary>
    [Required]
    [StringLength(20)]
    public string WarehouseSymbol { get; set; } = string.Empty;

    /// <summary>
    /// Components (składniki) for assembly
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one component is required")]
    public List<AssemblyComponentRequest> Components { get; set; } = new();

    /// <summary>
    /// Optional notes
    /// </summary>
    [StringLength(500)]
    public string? Notes { get; set; }
}

/// <summary>
/// Component item for assembly
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
    /// Quantity of component to use
    /// </summary>
    [Required]
    [Range(0.0001, double.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
    public decimal Quantity { get; set; }
}

/// <summary>
/// Request to create a disassembly (demontaż)
/// </summary>
public class CreateDisassemblyRequest
{
    /// <summary>
    /// Product ID to disassemble
    /// </summary>
    public int? ProductId { get; set; }

    /// <summary>
    /// Product symbol (alternative to ProductId)
    /// </summary>
    public string? ProductSymbol { get; set; }

    /// <summary>
    /// Quantity to disassemble
    /// </summary>
    [Required]
    [Range(0.0001, double.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
    public decimal Quantity { get; set; }

    /// <summary>
    /// Warehouse symbol
    /// </summary>
    [Required]
    [StringLength(20)]
    public string WarehouseSymbol { get; set; } = string.Empty;

    /// <summary>
    /// Resulting components from disassembly
    /// </summary>
    public List<DisassemblyComponentRequest>? ResultingComponents { get; set; }

    /// <summary>
    /// Optional notes
    /// </summary>
    [StringLength(500)]
    public string? Notes { get; set; }
}

/// <summary>
/// Resulting component from disassembly
/// </summary>
public class DisassemblyComponentRequest
{
    /// <summary>
    /// Resulting product ID
    /// </summary>
    public int? ProductId { get; set; }

    /// <summary>
    /// Resulting product symbol
    /// </summary>
    public string? ProductSymbol { get; set; }

    /// <summary>
    /// Quantity received from disassembly
    /// </summary>
    [Required]
    [Range(0.0001, double.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
    public decimal Quantity { get; set; }
}
