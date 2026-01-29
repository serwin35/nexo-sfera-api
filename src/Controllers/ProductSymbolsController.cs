using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Product symbol management - sequence analysis, validation, and generation.
///
/// Symbol structure: {SupplierCode}{CategoryCode}{SequenceNumber}[_{VariantCode}]
/// Example: DMSUDKB04425_0114
/// </summary>
[ApiController]
[Route("api/product-symbols")]
[Authorize]
[Tags("Product Symbols")]
public class ProductSymbolsController : ControllerBase
{
    private readonly ProductSymbolService _symbolService;
    private readonly ILogger<ProductSymbolsController> _logger;

    public ProductSymbolsController(ProductSymbolService symbolService, ILogger<ProductSymbolsController> logger)
    {
        _symbolService = symbolService;
        _logger = logger;
    }

    /// <summary>
    /// Parse and validate a product symbol
    /// </summary>
    /// <param name="symbol">The symbol to validate (e.g., DMSUDKB04425 or DMSUDKB04425_0114)</param>
    /// <returns>Validation result with parsed components</returns>
    [HttpGet("validate/{symbol}")]
    public ActionResult<ApiResponse<SymbolValidationResult>> ValidateSymbol(string symbol)
    {
        try
        {
            var result = _symbolService.ValidateSymbol(symbol);
            return Ok(ApiResponse<SymbolValidationResult>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating symbol {Symbol}", symbol);
            return StatusCode(500, ApiResponse<SymbolValidationResult>.Error("Error validating symbol", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Analyze sequence numbers for a supplier+category prefix
    /// </summary>
    /// <param name="supplierCode">3-letter supplier code (e.g., DMS)</param>
    /// <param name="categoryCode">4-letter category code (e.g., UDKB)</param>
    /// <returns>Analysis including used numbers, gaps, and next available</returns>
    [HttpGet("analyze/{supplierCode}/{categoryCode}")]
    public async Task<ActionResult<ApiResponse<SequenceAnalysisResult>>> AnalyzeSequence(string supplierCode, string categoryCode)
    {
        try
        {
            if (supplierCode.Length != 3)
            {
                return BadRequest(ApiResponse<SequenceAnalysisResult>.Error("Supplier code must be exactly 3 characters"));
            }

            if (categoryCode.Length != 4)
            {
                return BadRequest(ApiResponse<SequenceAnalysisResult>.Error("Category code must be exactly 4 characters"));
            }

            var result = await _symbolService.AnalyzeSequenceAsync(supplierCode, categoryCode);

            if (!string.IsNullOrEmpty(result.Error))
            {
                return StatusCode(500, ApiResponse<SequenceAnalysisResult>.Error("Error analyzing sequence", new List<string> { result.Error }));
            }

            return Ok(ApiResponse<SequenceAnalysisResult>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing sequence for {SupplierCode}{CategoryCode}", supplierCode, categoryCode);
            return StatusCode(500, ApiResponse<SequenceAnalysisResult>.Error("Error analyzing sequence", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Generate the next available symbol for a supplier+category
    /// </summary>
    /// <param name="supplierCode">3-letter supplier code (e.g., DMS)</param>
    /// <param name="categoryCode">4-letter category code (e.g., UDKB)</param>
    /// <param name="fillGaps">If true, fills gaps in sequence; if false, always uses next after max</param>
    /// <returns>Next available symbol</returns>
    [HttpGet("next/{supplierCode}/{categoryCode}")]
    public async Task<ActionResult<ApiResponse<NextSymbolResult>>> GetNextSymbol(
        string supplierCode,
        string categoryCode,
        [FromQuery] bool fillGaps = true)
    {
        try
        {
            if (supplierCode.Length != 3)
            {
                return BadRequest(ApiResponse<NextSymbolResult>.Error("Supplier code must be exactly 3 characters"));
            }

            if (categoryCode.Length != 4)
            {
                return BadRequest(ApiResponse<NextSymbolResult>.Error("Category code must be exactly 4 characters"));
            }

            var symbol = await _symbolService.GenerateNextSymbolAsync(supplierCode, categoryCode, fillGaps);

            var result = new NextSymbolResult
            {
                Symbol = symbol,
                SupplierCode = supplierCode.ToUpperInvariant(),
                CategoryCode = categoryCode.ToUpperInvariant(),
                FilledGap = fillGaps
            };

            return Ok(ApiResponse<NextSymbolResult>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating next symbol for {SupplierCode}{CategoryCode}", supplierCode, categoryCode);
            return StatusCode(500, ApiResponse<NextSymbolResult>.Error("Error generating next symbol", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Check if a symbol is already in use
    /// </summary>
    /// <param name="symbol">The symbol to check</param>
    /// <returns>Whether the symbol is in use</returns>
    [HttpGet("check/{symbol}")]
    public async Task<ActionResult<ApiResponse<SymbolCheckResult>>> CheckSymbol(string symbol)
    {
        try
        {
            var validation = _symbolService.ValidateSymbol(symbol);
            var isInUse = await _symbolService.IsSymbolInUseAsync(symbol);

            var result = new SymbolCheckResult
            {
                Symbol = symbol,
                IsValidFormat = validation.IsValid,
                IsInUse = isInUse,
                CanBeUsed = validation.IsValid && !isInUse,
                ParsedInfo = validation.ParsedInfo
            };

            return Ok(ApiResponse<SymbolCheckResult>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking symbol {Symbol}", symbol);
            return StatusCode(500, ApiResponse<SymbolCheckResult>.Error("Error checking symbol", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Generate a variant symbol from a base symbol
    /// </summary>
    /// <param name="baseSymbol">Base symbol (e.g., DMSUDKB04425)</param>
    /// <param name="variantCode">Variant code (e.g., 0114 for color 01 + size 14)</param>
    /// <returns>Generated variant symbol</returns>
    [HttpGet("variant/{baseSymbol}/{variantCode}")]
    public ActionResult<ApiResponse<VariantSymbolResult>> GenerateVariantSymbol(string baseSymbol, string variantCode)
    {
        try
        {
            var validation = _symbolService.ValidateSymbol(baseSymbol);
            if (!validation.IsValid)
            {
                return BadRequest(ApiResponse<VariantSymbolResult>.Error("Invalid base symbol", validation.Errors));
            }

            var variantSymbol = _symbolService.GenerateVariantSymbol(baseSymbol, variantCode);

            var result = new VariantSymbolResult
            {
                BaseSymbol = validation.ParsedInfo!.BaseSymbol,
                VariantCode = variantCode.ToUpperInvariant(),
                VariantSymbol = variantSymbol
            };

            return Ok(ApiResponse<VariantSymbolResult>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating variant symbol for {BaseSymbol}_{VariantCode}", baseSymbol, variantCode);
            return StatusCode(500, ApiResponse<VariantSymbolResult>.Error("Error generating variant symbol", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get all supplier codes currently in use
    /// </summary>
    /// <returns>List of unique supplier codes</returns>
    [HttpGet("suppliers")]
    public async Task<ActionResult<ApiResponse<List<string>>>> GetSupplierCodes()
    {
        try
        {
            var codes = await _symbolService.GetUsedSupplierCodesAsync();
            return Ok(ApiResponse<List<string>>.Ok(codes, $"Found {codes.Count} supplier codes"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting supplier codes");
            return StatusCode(500, ApiResponse<List<string>>.Error("Error getting supplier codes", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get all category codes in use, optionally filtered by supplier
    /// </summary>
    /// <param name="supplierCode">Optional supplier code to filter by</param>
    /// <returns>List of unique category codes</returns>
    [HttpGet("categories")]
    public async Task<ActionResult<ApiResponse<List<string>>>> GetCategoryCodes([FromQuery] string? supplierCode = null)
    {
        try
        {
            var codes = await _symbolService.GetUsedCategoryCodesAsync(supplierCode);
            return Ok(ApiResponse<List<string>>.Ok(codes, $"Found {codes.Count} category codes"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting category codes");
            return StatusCode(500, ApiResponse<List<string>>.Error("Error getting category codes", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get statistics about product symbols
    /// </summary>
    /// <returns>Statistics including counts by prefix and supplier</returns>
    [HttpGet("statistics")]
    public async Task<ActionResult<ApiResponse<SymbolStatistics>>> GetStatistics()
    {
        try
        {
            var stats = await _symbolService.GetStatisticsAsync();
            return Ok(ApiResponse<SymbolStatistics>.Ok(stats));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting symbol statistics");
            return StatusCode(500, ApiResponse<SymbolStatistics>.Error("Error getting statistics", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Batch generate symbols for multiple products
    /// </summary>
    [HttpPost("batch-generate")]
    public async Task<ActionResult<ApiResponse<List<string>>>> BatchGenerateSymbols([FromBody] BatchGenerateRequest request)
    {
        try
        {
            if (request.SupplierCode.Length != 3)
            {
                return BadRequest(ApiResponse<List<string>>.Error("Supplier code must be exactly 3 characters"));
            }

            if (request.CategoryCode.Length != 4)
            {
                return BadRequest(ApiResponse<List<string>>.Error("Category code must be exactly 4 characters"));
            }

            if (request.Count < 1 || request.Count > 1000)
            {
                return BadRequest(ApiResponse<List<string>>.Error("Count must be between 1 and 1000"));
            }

            var symbols = new List<string>();
            var analysis = await _symbolService.AnalyzeSequenceAsync(request.SupplierCode, request.CategoryCode);

            var availableNumbers = new Queue<int>();

            // If filling gaps, add gaps first
            if (request.FillGaps && analysis.Gaps.Any())
            {
                foreach (var gap in analysis.Gaps.Take(request.Count))
                {
                    availableNumbers.Enqueue(gap);
                }
            }

            // Add sequential numbers after max
            int nextNumber = analysis.NextAfterMax ?? 1;
            while (availableNumbers.Count < request.Count)
            {
                availableNumbers.Enqueue(nextNumber++);
            }

            // Generate symbols
            var prefix = $"{request.SupplierCode.ToUpperInvariant()}{request.CategoryCode.ToUpperInvariant()}";
            while (symbols.Count < request.Count && availableNumbers.Count > 0)
            {
                var number = availableNumbers.Dequeue();
                symbols.Add($"{prefix}{number:D5}");
            }

            return Ok(ApiResponse<List<string>>.Ok(symbols, $"Generated {symbols.Count} symbols"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error batch generating symbols");
            return StatusCode(500, ApiResponse<List<string>>.Error("Error batch generating symbols", new List<string> { ex.Message }));
        }
    }
}

#region Request/Response Models

public class NextSymbolResult
{
    public string Symbol { get; set; } = "";
    public string SupplierCode { get; set; } = "";
    public string CategoryCode { get; set; } = "";
    public bool FilledGap { get; set; }
}

public class SymbolCheckResult
{
    public string Symbol { get; set; } = "";
    public bool IsValidFormat { get; set; }
    public bool IsInUse { get; set; }
    public bool CanBeUsed { get; set; }
    public ProductSymbolInfo? ParsedInfo { get; set; }
}

public class VariantSymbolResult
{
    public string BaseSymbol { get; set; } = "";
    public string VariantCode { get; set; } = "";
    public string VariantSymbol { get; set; } = "";
}

public class BatchGenerateRequest
{
    public string SupplierCode { get; set; } = "";
    public string CategoryCode { get; set; } = "";
    public int Count { get; set; } = 1;
    public bool FillGaps { get; set; } = true;
}

#endregion
