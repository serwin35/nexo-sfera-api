using System.ComponentModel.DataAnnotations;
using NexoSferaApi.Models.Dto;

namespace NexoSferaApi.Models.Requests;

/// <summary>
/// New product card (<c>POST /api/products</c>). The kind, base unit and VAT rate come from the template (<c>templateId</c>,
/// or the default template of <c>type</c>); every other field is optional. All-or-nothing: any field that cannot be applied
/// returns 400 and nothing is created.
/// </summary>
public class CreateProductRequest
{
    /// <summary>Symbol (<c>Asortyment.Symbol</c>), unique, no whitespace. Omitted: Nexo numbers it (<c>AutoSymbol</c>).</summary>
    [MaxLength(50)]
    public string? Symbol { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    /// <summary>Primary barcode of the base unit. A code of another product is refused.</summary>
    [MaxLength(20)]
    public string? EAN { get; set; }

    [MaxLength(15)]
    public string? PKWiU { get; set; }

    /// <summary>
    /// Goods, Service or Set (kit): picks the default Nexo template of that kind when no template is given, and must match
    /// the kind of the given template. Omitted: Goods. Material is refused (use <c>templateId</c>).
    /// </summary>
    public ProductType? Type { get; set; }

    /// <summary>Symbol of a unit from the units dictionary to use as the base unit instead of the template's.</summary>
    [MaxLength(10)]
    public string? BaseUnit { get; set; }

    /// <summary>A new product has one unit: when sent, this must be the base unit. Add units after creating the product.</summary>
    [MaxLength(10)]
    public string? SaleUnit { get; set; }

    /// <summary>Like <see cref="SaleUnit"/>.</summary>
    [MaxLength(10)]
    public string? PurchaseUnit { get; set; }

    /// <summary>Refused with 400: Nexo keeps sales prices in price lists.</summary>
    [Range(0, double.MaxValue)]
    public decimal? PriceNet { get; set; }

    /// <summary>
    /// VAT rate symbol as products report it in <c>vatRate</c> (<c>StawkaVat.Symbol</c>), set as both the sales and the
    /// purchase VAT rate. Omitted: the template's.
    /// </summary>
    [MaxLength(5)]
    public string? VatRate { get; set; }

    /// <summary>Gross weight of one base unit in kilograms.</summary>
    [Range(0, double.MaxValue)]
    public decimal? Weight { get; set; }

    /// <summary>Volume of one base unit, in the base unit's volume unit (which the template must set).</summary>
    [Range(0, double.MaxValue)]
    public decimal? Volume { get; set; }

    /// <summary>Product group id (<c>GrupaAsortymentu</c>), as listed by <c>GET /api/product-groups</c>.</summary>
    public int? GroupId { get; set; }

    /// <summary>Product template id (<c>SzablonAsortymentu.Id</c>), as listed by <c>GET /api/product-templates</c>.</summary>
    public int? TemplateId { get; set; }

    /// <summary>Legacy: the default product symbol of a template (<c>SzablonAsortymentu.Symbol</c>); must match one template.</summary>
    public string? TemplateSymbol { get; set; }

    /// <summary>Kit components (only for a kit, at most 100). Quantities are in the component's base unit unless <c>unitSymbol</c>.</summary>
    public List<CreateProductComponentRequest>? Components { get; set; }
}

/// <summary>One component of a new kit (SDK: <c>ISkladnikiKompletu.Dodaj(id, ilosc, jednostka)</c>).</summary>
public class CreateProductComponentRequest
{
    public int ProductId { get; set; }

    public decimal Quantity { get; set; }

    [MaxLength(10)]
    public string? UnitSymbol { get; set; }
}

/// <summary>
/// Partial update of a product. Omitted (null) fields are left unchanged. The update is all-or-nothing: any rejected
/// field returns 400 and nothing is saved. The response lists the outcome of every field sent (fieldResults).
/// </summary>
public class UpdateProductRequest
{
    /// <summary>
    /// Symbol (Asortyment.Symbol). Must not be empty or contain whitespace; a symbol already used by another product is
    /// rejected. The card keeps its id, so documents, stock and price list positions stay attached. Clients that match
    /// products by symbol (shop references, printed labels) must follow the change themselves.
    /// </summary>
    [MaxLength(50)]
    public string? Symbol { get; set; }

    /// <summary>Product name (Asortyment.Nazwa). Must not be empty.</summary>
    [MaxLength(200)]
    public string? Name { get; set; }

    /// <summary>Description (Asortyment.Opis). An empty string clears it.</summary>
    [MaxLength(2000)]
    public string? Description { get; set; }

    /// <summary>
    /// EAN: written as the primary barcode (PodstawowyKodKreskowy) of the unit that currently provides the product EAN
    /// (see ProductDto.eanUnitSymbol), or of <see cref="EanUnitSymbol"/>, or of the base unit when the product has no
    /// barcode yet. A code already owned by another product is rejected. Must not be empty.
    /// </summary>
    [MaxLength(20)]
    public string? EAN { get; set; }

    /// <summary>Optional: symbol of the product unit that receives <see cref="EAN"/>. Only allowed together with ean.</summary>
    [MaxLength(10)]
    public string? EanUnitSymbol { get; set; }

    /// <summary>PKWiU (Asortyment.PKWiU). An empty string clears it.</summary>
    [MaxLength(15)]
    public string? PKWiU { get; set; }

    /// <summary>Rejected with 400: Nexo keeps sales prices in price lists, which this endpoint does not write.</summary>
    [Range(0, double.MaxValue)]
    public decimal? PriceNet { get; set; }

    /// <summary>Rejected with 400: changing the VAT rate is not supported here.</summary>
    [MaxLength(5)]
    public string? VatRate { get; set; }

    /// <summary>
    /// Registry price (<c>Asortyment.CenaEwidencyjna</c>) in the registry price currency. For a kit it is the cost the
    /// price list takes as the base price (function <c>WgCenyEwidencyjnej</c>), so set it to the value of the components
    /// and then refresh the costs with <c>POST /api/dictionary/price-lists/by-id/{id}/items/recalculate-costs</c>.
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? RecordPrice { get; set; }

    /// <summary>
    /// Gross weight of one base unit in kilograms. Stored in the base unit (Masa), converted to its mass unit; when the
    /// base unit has no mass unit yet it is set to kilograms.
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? Weight { get; set; }

    /// <summary>Volume of one base unit, in the base unit's volume unit (ProductDto.volumeUnitSymbol), which must be set.</summary>
    [Range(0, double.MaxValue)]
    public decimal? Volume { get; set; }

    /// <summary>
    /// Product group (Asortyment.Grupa): id of an existing GrupaAsortymentu, as listed by GET /api/product-groups.
    /// </summary>
    public int? GroupId { get; set; }

    /// <summary>Rejected with 400: Nexo has no activity flag on products (inactive = in the recycle bin).</summary>
    public bool? IsActive { get; set; }
}
