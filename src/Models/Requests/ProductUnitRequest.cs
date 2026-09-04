using System.ComponentModel.DataAnnotations;

namespace NexoSferaApi.Models.Requests;

/// <summary>
/// Request for adding a unit of measure to a product (SDK: IJednostkiMiarAsortymentu.DodajJednostkeMiary)
/// </summary>
public class AddProductUnitRequest
{
    /// <summary>
    /// Symbol of the dictionary unit of measure to add (e.g. "szt", "kg", "op"). Aliases are accepted.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string UnitSymbol { get; set; } = string.Empty;

    /// <summary>
    /// Symbol of the unit the conversion is expressed against. Defaults to the product's base unit.
    /// </summary>
    [MaxLength(50)]
    public string? BaseUnitSymbol { get; set; }

    /// <summary>
    /// Conversion: NewUnitCount × new unit = BaseUnitCount × base unit (e.g. 1 "op" = 12 "szt" → 1 / 12).
    /// Must be provided together with <see cref="BaseUnitCount"/>; when both are omitted the converter is copied
    /// from the unit dictionary (if defined there).
    /// </summary>
    [Range(typeof(decimal), "0.000001", "79228162514264337593543950335")]
    public decimal? NewUnitCount { get; set; }

    /// <summary>
    /// Conversion: how many base units correspond to <see cref="NewUnitCount"/> new units.
    /// </summary>
    [Range(typeof(decimal), "0.000001", "79228162514264337593543950335")]
    public decimal? BaseUnitCount { get; set; }

    /// <summary>
    /// Set the new unit as the product's default sale unit (Asortyment.JednostkaSprzedazy)
    /// </summary>
    public bool SetAsSaleUnit { get; set; }

    /// <summary>
    /// Set the new unit as the product's default purchase unit (Asortyment.JednostkaZakupu)
    /// </summary>
    public bool SetAsPurchaseUnit { get; set; }
}

/// <summary>
/// Request for changing the product's base unit of measure (SDK: IJednostkiMiarAsortymentu.UstawPodstawowaJednostkeMiary)
/// </summary>
public class SetProductBaseUnitRequest
{
    /// <summary>
    /// Symbol of the dictionary unit of measure that should become the base unit
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string UnitSymbol { get; set; } = string.Empty;
}

/// <summary>
/// Request for changing the product's default sale / purchase units. Only provided fields are changed.
/// </summary>
public class SetProductDefaultUnitsRequest
{
    /// <summary>
    /// Symbol of a unit already assigned to the product to use as the default sale unit
    /// </summary>
    [MaxLength(50)]
    public string? SaleUnitSymbol { get; set; }

    /// <summary>
    /// Symbol of a unit already assigned to the product to use as the default purchase unit
    /// </summary>
    [MaxLength(50)]
    public string? PurchaseUnitSymbol { get; set; }
}

/// <summary>
/// Request for adding a component to a kit (komplet) product (SDK: ISkladnikiKompletu.Dodaj)
/// </summary>
public class AddProductComponentRequest
{
    /// <summary>
    /// ID of the component product. Either this or <see cref="ComponentSymbol"/> is required.
    /// </summary>
    public int? ComponentProductId { get; set; }

    /// <summary>
    /// Symbol of the component product. Either this or <see cref="ComponentProductId"/> is required.
    /// </summary>
    [MaxLength(100)]
    public string? ComponentSymbol { get; set; }

    /// <summary>
    /// Quantity of the component in one kit (must be greater than zero)
    /// </summary>
    [Range(typeof(decimal), "0.000001", "79228162514264337593543950335")]
    public decimal Quantity { get; set; }

    /// <summary>
    /// Symbol of the component's unit of measure. Defaults to the component product's base unit.
    /// </summary>
    [MaxLength(50)]
    public string? UnitSymbol { get; set; }
}
