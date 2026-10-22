using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Helpers;
using NexoSferaApi.Models.Dto;
using NexoSferaApi.Models.Requests;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Variant properties (Właściwości: "Kolor", "RM", …), their allowed values, and the value of a property on a variant
/// </summary>
[ApiController]
[Route("api")]
[Authorize]
[Tags("Product properties")]
public class ProductPropertiesController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<ProductPropertiesController> _logger;

    public ProductPropertiesController(ISferaService sferaService, ILogger<ProductPropertiesController> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Get every variant property with its allowed values
    /// </summary>
    [HttpGet("product-properties")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductPropertyDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProperties()
    {
        try
        {
            var properties = await _sferaService.ExecuteWithLockAsync(() => VariantPropertyWriter.Properties(_sferaService.GetSfera()));
            return Ok(ApiResponse<List<ProductPropertyDto>>.Ok(properties));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading product properties");
            return StatusCode(500, ApiResponse<object>.Error("Error reading product properties"));
        }
    }

    /// <summary>
    /// Add an allowed value to a variant property
    /// </summary>
    /// <remarks>
    /// A value of the same name that already exists in the value dictionary (<c>CechaAsortymentu</c>) is reused, otherwise
    /// it is created. The new value can then be set on variants with <c>PUT /api/products/{id}/variant-properties</c>.
    ///
    /// Example: <c>{"name": "Coyote"}</c>
    /// </remarks>
    /// <param name="id">Property id (<c>Wlasciwosc.Id</c>, from <c>GET /api/product-properties</c>)</param>
    /// <param name="request">Value name</param>
    [HttpPost("product-properties/{id:int}/values")]
    [ProducesResponseType(typeof(ApiResponse<ProductPropertyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddValue(int id, [FromBody] AddProductPropertyValueRequest request)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
            return BadRequest(ApiResponse<object>.Error("Invalid value", new List<string> { "name: must not be empty." }));

        try
        {
            var outcome = await _sferaService.ExecuteWithLockAsync(() => VariantPropertyWriter.AddValue(_sferaService.GetSfera(), id, name));
            if (!outcome.Found) return NotFound(ApiResponse<object>.Error($"Property {id} not found"));
            if (outcome.Errors.Count > 0) return BadRequest(ApiResponse<object>.Error("Value not added", outcome.Errors));

            _logger.LogInformation("Property {Id}: value {Name} {Status}", id, name, outcome.Status);
            return Ok(ApiResponse<ProductPropertyDto>.Ok(outcome.Property!, outcome.Status == VariantPropertyWriter.Updated ? "Value added" : "Value already present"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding a value to property {Id}", id);
            return StatusCode(500, ApiResponse<object>.Error("Error adding the value"));
        }
    }

    /// <summary>
    /// Set the value of a property on a variant (e.g. Kolor: Piaskowy → Beżowy) and regenerate its name
    /// </summary>
    /// <remarks>
    /// Changes <c>Asortyment.WlasciwosciAsortymentu</c> of this product only; other products using the old value keep it.
    /// The value must be an allowed value of the property. With <c>regenerateName</c> (default) the variant name and
    /// description are regenerated from the model, so the name shows the new value.
    ///
    /// Example: <c>{"property": "Kolor", "value": "Beżowy"}</c>
    /// </remarks>
    /// <param name="id">Product id</param>
    /// <param name="request">Property, value, name regeneration</param>
    [HttpPut("products/{id:int}/variant-properties")]
    [ProducesResponseType(typeof(ApiResponse<VariantPropertyChangeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetVariantProperty(int id, [FromBody] SetVariantPropertyRequest request)
    {
        var property = request.Property?.Trim() ?? string.Empty;
        var value = request.Value?.Trim() ?? string.Empty;
        if (property.Length == 0 || value.Length == 0)
            return BadRequest(ApiResponse<object>.Error("Invalid request", new List<string> { "property and value: must not be empty." }));

        try
        {
            var outcome = await _sferaService.ExecuteWithLockAsync(() =>
                VariantPropertyWriter.SetValue(_sferaService.GetSfera(), id, property, value, request.RegenerateName));
            if (!outcome.Found) return NotFound(ApiResponse<object>.Error($"Product with ID {id} not found"));
            if (outcome.Errors.Count > 0) return BadRequest(ApiResponse<object>.Error("Property not changed; nothing was saved", outcome.Errors));

            _logger.LogInformation("Product {Id}: {Property} {Old} -> {New} ({Status})", id, property, outcome.Change!.OldValue, value, outcome.Status);
            return Ok(ApiResponse<VariantPropertyChangeDto>.Ok(outcome.Change!, outcome.Status == VariantPropertyWriter.Updated ? "Property changed" : "Property already had this value"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting {Property} on product {Id}", property, id);
            return StatusCode(500, ApiResponse<object>.Error("Error setting the property"));
        }
    }
}
