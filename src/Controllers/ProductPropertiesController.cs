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
            return StatusCode(500, ApiResponse<object>.Error("Error reading product properties", ExceptionErrors(ex)));
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
            return StatusCode(500, ApiResponse<object>.Error("Error adding the value", ExceptionErrors(ex)));
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
            return StatusCode(500, ApiResponse<object>.Error("Error setting the property", ExceptionErrors(ex)));
        }
    }

    /// <summary>
    /// Create new variants of the model of an existing variant (the Subiekt variant generator)
    /// </summary>
    /// <remarks>
    /// Each new card is filled from the template variant <c>{id}</c> (kind, group, units, VAT, suppliers, model), gets a
    /// value for every model property, the given symbol (or the next Nexo variant symbol)
    /// and a name generated from the model. Barcodes are not copied; kit components only with <c>copyComponents</c>.
    /// Sales prices stay as Nexo sets them for a new card: set them with
    /// <c>PUT /api/dictionary/price-lists/by-id/{id}/items</c>.
    ///
    /// A value set the model already has is reported as <c>exists</c> and not created again. Every variant is validated
    /// before anything is saved (400, nothing saved); the save phase stops at the first variant Nexo refuses, and the
    /// variants saved before it stay (listed with <c>created</c>). <c>dryRun</c> validates only.
    ///
    /// Example: <c>{"variants":[{"values":[{"property":"Kolor","value":"Coyote CY"},{"property":"Rozmiar","value":"XS"}],"symbol":"DMSUDKP02967_CY12"}],"dryRun":true}</c>
    /// </remarks>
    /// <param name="id">Id of an existing variant of the model; the new variants are filled from it</param>
    /// <param name="request">Variants, component copy, dry run</param>
    [HttpPost("products/{id:int}/variants")]
    [ProducesResponseType(typeof(ApiResponse<CreateVariantsResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateVariants(int id, [FromBody] CreateVariantsRequest request)
    {
        var errors = VariantCreator.Validate(request);
        if (errors.Count > 0) return BadRequest(ApiResponse<object>.Error("Invalid variants; nothing was created", errors));

        try
        {
            var outcome = await _sferaService.ExecuteWithLockAsync(() => VariantCreator.Create(_sferaService.GetSfera(), id, request));
            if (!outcome.Found) return NotFound(ApiResponse<object>.Error($"Product with ID {id} not found"));
            if (outcome.Errors.Count > 0) return BadRequest(ApiResponse<object>.Error("Variants not created", outcome.Errors));

            var result = outcome.Result!;
            var created = result.Variants.Count(v => v.Status == VariantCreator.Created);
            _logger.LogInformation("Model {Model} from product {Id}: {Created} variant(s) created, dry run {DryRun}", result.ModelId, id, created, request.DryRun);
            if (outcome.Failed)
            {
                var message = created > 0 ? $"Stopped after {created} created variant(s); see the failed one" : "Variants not created; nothing was saved";
                return BadRequest(new ApiResponse<CreateVariantsResultDto> { Success = false, Message = message, Data = result, Errors = result.Variants.SelectMany(v => v.Errors).ToList() });
            }

            var exists = result.Variants.Count(v => v.Status == VariantCreator.Exists);
            var summary = request.DryRun
                ? $"Dry run: {result.Variants.Count(v => v.Status == VariantCreator.Planned)} to create, {exists} already exist"
                : $"{created} created, {exists} already exist";
            return Ok(ApiResponse<CreateVariantsResultDto>.Ok(result, summary));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating variants from product {Id}", id);
            return StatusCode(500, ApiResponse<object>.Error("Error creating the variants", ExceptionErrors(ex)));
        }
    }

    /// <summary>
    /// List product models (the Subiekt cards that group variants) with their properties and variant counts
    /// </summary>
    /// <param name="search">Fragment of the model name or signature, e.g. "ZOSP" or "DMSUDKP03820"</param>
    [HttpGet("product-models")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductModelDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetModels([FromQuery] string? search = null)
    {
        try
        {
            var models = await _sferaService.ExecuteWithLockAsync(() => ProductModelReader.List(_sferaService.GetSfera(), search));
            return Ok(ApiResponse<List<ProductModelDto>>.Ok(models));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading product models");
            return StatusCode(500, ApiResponse<object>.Error("Error reading product models", ExceptionErrors(ex)));
        }
    }

    /// <summary>
    /// Get one product model with its properties and every variant (symbol, name, number, property values)
    /// </summary>
    /// <param name="id">Model id</param>
    [HttpGet("product-models/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<ProductModelDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetModel(int id)
    {
        try
        {
            var model = await _sferaService.ExecuteWithLockAsync(() => ProductModelReader.Get(_sferaService.GetSfera(), id));
            if (model == null) return NotFound(ApiResponse<object>.Error($"Model {id} not found"));
            return Ok(ApiResponse<ProductModelDto>.Ok(model));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading product model {Id}", id);
            return StatusCode(500, ApiResponse<object>.Error("Error reading the product model", ExceptionErrors(ex)));
        }
    }

    /// <summary>
    /// Move an existing card into a model as its variant (or give it other values in its own model)
    /// </summary>
    /// <remarks>
    /// Sets the model (Nexo gives the next variant number), the value of every property of the target
    /// model, the optional new symbol, and regenerates the name and description from the model. Kind, group, components
    /// and prices stay; change them with the other endpoints. Refused (400, nothing saved): an unknown property or value,
    /// a missing value, a value set the model already has, a symbol of another card, or a property of the card the target
    /// model does not have.
    ///
    /// Example: <c>{"modelId": 100150, "values": [{"property":"Kolor","value":"Czarny"},{"property":"RM","value":"S"}], "symbol": "DMSUDKP00090_0113", "dryRun": true}</c>
    /// </remarks>
    /// <param name="id">Product id</param>
    /// <param name="request">Target model, values, symbol, dry run</param>
    [HttpPut("products/{id:int}/model")]
    [ProducesResponseType(typeof(ApiResponse<MovedVariantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MoveToModel(int id, [FromBody] MoveToModelRequest request)
    {
        var errors = VariantMover.Validate(request);
        if (errors.Count > 0) return BadRequest(ApiResponse<object>.Error("Invalid request; nothing was saved", errors));

        try
        {
            var outcome = await _sferaService.ExecuteWithLockAsync(() => VariantMover.Move(_sferaService.GetSfera(), id, request));
            if (!outcome.Found) return NotFound(ApiResponse<object>.Error($"Product with ID {id} not found"));
            if (outcome.Errors.Count > 0)
                return BadRequest(new ApiResponse<MovedVariantDto> { Success = false, Message = "Not moved; nothing was saved", Data = outcome.Result, Errors = outcome.Errors });

            var result = outcome.Result!;
            _logger.LogInformation("Product {Id}: model {Old} -> {New}, {OldSymbol} -> {Symbol} ({Status})", id, result.OldModelId, result.ModelId, result.OldSymbol, result.Symbol, result.Status);
            var message = result.Status switch
            {
                VariantMover.Moved => "Moved",
                VariantMover.Planned => "Dry run: would be moved",
                _ => "Already a variant of this model with these values",
            };
            return Ok(ApiResponse<MovedVariantDto>.Ok(result, message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error moving product {Id} to model {Model}", id, request.ModelId);
            return StatusCode(500, ApiResponse<object>.Error("Error moving the product", ExceptionErrors(ex)));
        }
    }

    /// <summary>The exception and its root cause (EF wraps SDK errors), as the other controllers report a 500.</summary>
    private static List<string> ExceptionErrors(Exception ex)
    {
        var errors = new List<string> { $"{ex.GetType().Name}: {ex.Message}" };
        var root = ex.GetBaseException();
        if (!ReferenceEquals(root, ex)) errors.Add($"{root.GetType().Name}: {root.Message}");
        return errors;
    }
}
