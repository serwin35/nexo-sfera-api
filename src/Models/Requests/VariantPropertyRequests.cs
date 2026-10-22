using System.ComponentModel.DataAnnotations;

namespace NexoSferaApi.Models.Requests;

/// <summary>New allowed value of a variant property (<c>POST /api/product-properties/{id}/values</c>).</summary>
public class AddProductPropertyValueRequest
{
    /// <summary>Value name as it should appear in variant names, e.g. "Coyote".</summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}

/// <summary>Value of one property of a variant (<c>PUT /api/products/{id}/variant-properties</c>).</summary>
public class SetVariantPropertyRequest
{
    /// <summary>Property name, e.g. "Kolor".</summary>
    [Required]
    [MaxLength(100)]
    public string Property { get; set; } = string.Empty;

    /// <summary>An allowed value of the property, e.g. "Beżowy".</summary>
    [Required]
    [MaxLength(100)]
    public string Value { get; set; } = string.Empty;

    /// <summary>Regenerate the variant name and description from the model (default true).</summary>
    public bool RegenerateName { get; set; } = true;
}

/// <summary>
/// New variants of the model of an existing variant (<c>POST /api/products/{id}/variants</c>). Each variant is filled from
/// that template variant (<c>IAsortyment.WypelnijNaPodstawie</c>) and gets its own property values, symbol and name.
/// </summary>
public class CreateVariantsRequest
{
    /// <summary>Variants to create, at most 50 per request.</summary>
    [Required]
    public List<CreateVariantRequest> Variants { get; set; } = new();

    /// <summary>
    /// Keep the kit components copied from the template (default false: a new variant starts without components, because
    /// the garment of a kit differs per colour and size).
    /// </summary>
    public bool CopyComponents { get; set; }

    /// <summary>Validate and report what would be created without saving anything.</summary>
    public bool DryRun { get; set; }
}

/// <summary>One new variant: a value for every property of the model, and optionally its symbol.</summary>
public class CreateVariantRequest
{
    /// <summary>Value of each model property, e.g. <c>[{"property":"Kolor","value":"Coyote CY"},{"property":"Rozmiar","value":"XS"}]</c>.</summary>
    [Required]
    public List<VariantPropertyValueRequest> Values { get; set; } = new();

    /// <summary>Card symbol, e.g. <c>DMSUDKP02967_CY12</c>. Omitted: the next Nexo variant symbol (<c>AutoSymbol</c>).</summary>
    [MaxLength(40)]
    public string? Symbol { get; set; }
}

/// <summary>Value of one model property for a new variant.</summary>
public class VariantPropertyValueRequest
{
    /// <summary>Property name or symbol, e.g. "Kolor" or "RM" (the symbol variant names show).</summary>
    [Required]
    [MaxLength(100)]
    public string Property { get; set; } = string.Empty;

    /// <summary>An allowed value of the property, e.g. "Coyote CY".</summary>
    [Required]
    [MaxLength(100)]
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Moves an existing card into a model as its variant (<c>PUT /api/products/{id}/model</c>), e.g. an unused card that was
/// generated in the wrong model.
/// </summary>
public class MoveToModelRequest
{
    /// <summary>Target model id (<c>GET /api/product-models</c>).</summary>
    [Range(1, int.MaxValue)]
    public int ModelId { get; set; }

    /// <summary>Value of every property of the target model, by property name or symbol.</summary>
    [Required]
    public List<VariantPropertyValueRequest> Values { get; set; } = new();

    /// <summary>New card symbol, e.g. <c>DMSUDKP00090_0113</c>. Omitted: the symbol stays.</summary>
    [MaxLength(40)]
    public string? Symbol { get; set; }

    /// <summary>Validate and report the change without saving.</summary>
    public bool DryRun { get; set; }
}
