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
