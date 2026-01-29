using System.Text.RegularExpressions;

namespace NexoSferaApi.Services;

/// <summary>
/// Service for managing product symbol sequences and validation.
///
/// Symbol structure: {SupplierCode}{CategoryCode}{SequenceNumber}[_{VariantCode}]
/// Example: DMSUDKB04425_0114
///   - DMS = Supplier code (3 chars)
///   - UDKB = Category code (4 chars)
///   - 04425 = Sequence number (5 digits)
///   - _0114 = Variant suffix (optional): 01=color, 14=size
/// </summary>
public class ProductSymbolService
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<ProductSymbolService> _logger;

    // Default pattern - can be made configurable
    private const int SUPPLIER_CODE_LENGTH = 3;
    private const int CATEGORY_CODE_LENGTH = 4;
    private const int SEQUENCE_NUMBER_LENGTH = 5;

    public ProductSymbolService(ISferaService sferaService, ILogger<ProductSymbolService> logger)
    {
        _sferaService = sferaService;
        _logger = logger;
    }

    /// <summary>
    /// Parse a product symbol into its components
    /// </summary>
    public ProductSymbolInfo? ParseSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return null;

        // Pattern: 3 chars supplier + 4 chars category + 5 digits sequence + optional variant
        var pattern = @"^([A-Z]{3})([A-Z]{4})(\d{5})(?:_(.+))?$";
        var match = Regex.Match(symbol.ToUpperInvariant(), pattern);

        if (!match.Success)
            return null;

        return new ProductSymbolInfo
        {
            FullSymbol = symbol.ToUpperInvariant(),
            SupplierCode = match.Groups[1].Value,
            CategoryCode = match.Groups[2].Value,
            SequenceNumber = int.Parse(match.Groups[3].Value),
            VariantSuffix = match.Groups[4].Success ? match.Groups[4].Value : null,
            IsVariant = match.Groups[4].Success,
            BaseSymbol = $"{match.Groups[1].Value}{match.Groups[2].Value}{match.Groups[3].Value}"
        };
    }

    /// <summary>
    /// Validate a product symbol format
    /// </summary>
    public SymbolValidationResult ValidateSymbol(string symbol)
    {
        var result = new SymbolValidationResult { Symbol = symbol };

        if (string.IsNullOrWhiteSpace(symbol))
        {
            result.IsValid = false;
            result.Errors.Add("Symbol cannot be empty");
            return result;
        }

        var parsed = ParseSymbol(symbol);
        if (parsed == null)
        {
            result.IsValid = false;
            result.Errors.Add($"Symbol does not match expected pattern: [A-Z]{{3}}[A-Z]{{4}}[0-9]{{5}}[_variant]");
            return result;
        }

        result.IsValid = true;
        result.ParsedInfo = parsed;
        return result;
    }

    /// <summary>
    /// Analyze the sequence for a given supplier+category prefix to find gaps and next number
    /// </summary>
    public async Task<SequenceAnalysisResult> AnalyzeSequenceAsync(string supplierCode, string categoryCode)
    {
        var result = new SequenceAnalysisResult
        {
            SupplierCode = supplierCode.ToUpperInvariant(),
            CategoryCode = categoryCode.ToUpperInvariant(),
            Prefix = $"{supplierCode.ToUpperInvariant()}{categoryCode.ToUpperInvariant()}"
        };

        var prefix = result.Prefix;

        try
        {
            var usedNumbers = await _sferaService.ExecuteWithLockAsync(() =>
            {
                var numbers = new HashSet<int>();
                var asortymenty = _sferaService.GetManager("Asortymenty");
                if (asortymenty == null)
                    return numbers;

                foreach (var asortyment in asortymenty.Dane.Wszystkie())
                {
                    string? symbol = Helpers.DynamicPropertyHelper.GetString(asortyment, "Symbol");
                    if (string.IsNullOrEmpty(symbol))
                        continue;

                    var parsed = ParseSymbol(symbol);
                    if (parsed != null &&
                        parsed.SupplierCode == result.SupplierCode &&
                        parsed.CategoryCode == result.CategoryCode)
                    {
                        numbers.Add(parsed.SequenceNumber);
                    }
                }

                return numbers;
            });

            result.UsedNumbers = usedNumbers.OrderBy(n => n).ToList();
            result.TotalUsed = usedNumbers.Count;

            if (usedNumbers.Count == 0)
            {
                result.NextAvailable = 1;
                result.Gaps = new List<int>();
            }
            else
            {
                int maxNumber = usedNumbers.Max();
                result.MaxUsed = maxNumber;
                result.NextAfterMax = maxNumber + 1;

                // Find gaps
                var allPossible = Enumerable.Range(1, maxNumber).ToHashSet();
                result.Gaps = allPossible.Except(usedNumbers).OrderBy(n => n).ToList();

                // Next available is first gap or next after max
                result.NextAvailable = result.Gaps.Count > 0 ? result.Gaps.First() : result.NextAfterMax;
            }

            _logger.LogInformation(
                "Sequence analysis for {Prefix}: {TotalUsed} used, {GapsCount} gaps, next available: {NextAvailable}",
                prefix, result.TotalUsed, result.Gaps.Count, result.NextAvailable);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing sequence for prefix {Prefix}", prefix);
            result.Error = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Generate the next available symbol for a supplier+category
    /// </summary>
    public async Task<string> GenerateNextSymbolAsync(string supplierCode, string categoryCode, bool fillGaps = true)
    {
        var analysis = await AnalyzeSequenceAsync(supplierCode, categoryCode);

        int nextNumber = fillGaps ? analysis.NextAvailable : (analysis.NextAfterMax ?? 1);

        return $"{supplierCode.ToUpperInvariant()}{categoryCode.ToUpperInvariant()}{nextNumber:D5}";
    }

    /// <summary>
    /// Generate a variant symbol from a base symbol
    /// </summary>
    public string GenerateVariantSymbol(string baseSymbol, string variantCode)
    {
        var parsed = ParseSymbol(baseSymbol);
        if (parsed == null)
            throw new ArgumentException($"Invalid base symbol: {baseSymbol}");

        // Use the base symbol without any existing variant
        return $"{parsed.BaseSymbol}_{variantCode.ToUpperInvariant()}";
    }

    /// <summary>
    /// Check if a symbol is already in use in Nexo
    /// </summary>
    public async Task<bool> IsSymbolInUseAsync(string symbol)
    {
        return await _sferaService.ExecuteWithLockAsync(() =>
        {
            var asortymenty = _sferaService.GetManager("Asortymenty");
            if (asortymenty == null)
                return false;

            foreach (var asortyment in asortymenty.Dane.Wszystkie())
            {
                string? existingSymbol = Helpers.DynamicPropertyHelper.GetString(asortyment, "Symbol");
                if (string.Equals(existingSymbol, symbol, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        });
    }

    /// <summary>
    /// Get all unique supplier codes currently in use
    /// </summary>
    public async Task<List<string>> GetUsedSupplierCodesAsync()
    {
        return await _sferaService.ExecuteWithLockAsync(() =>
        {
            var codes = new HashSet<string>();
            var asortymenty = _sferaService.GetManager("Asortymenty");
            if (asortymenty == null)
                return codes.ToList();

            foreach (var asortyment in asortymenty.Dane.Wszystkie())
            {
                string? symbol = Helpers.DynamicPropertyHelper.GetString(asortyment, "Symbol");
                if (string.IsNullOrEmpty(symbol))
                    continue;

                var parsed = ParseSymbol(symbol);
                if (parsed != null)
                {
                    codes.Add(parsed.SupplierCode);
                }
            }

            return codes.OrderBy(c => c).ToList();
        });
    }

    /// <summary>
    /// Get all unique category codes for a supplier
    /// </summary>
    public async Task<List<string>> GetUsedCategoryCodesAsync(string? supplierCode = null)
    {
        return await _sferaService.ExecuteWithLockAsync(() =>
        {
            var codes = new HashSet<string>();
            var asortymenty = _sferaService.GetManager("Asortymenty");
            if (asortymenty == null)
                return codes.ToList();

            foreach (var asortyment in asortymenty.Dane.Wszystkie())
            {
                string? symbol = Helpers.DynamicPropertyHelper.GetString(asortyment, "Symbol");
                if (string.IsNullOrEmpty(symbol))
                    continue;

                var parsed = ParseSymbol(symbol);
                if (parsed != null)
                {
                    if (supplierCode == null || parsed.SupplierCode == supplierCode.ToUpperInvariant())
                    {
                        codes.Add(parsed.CategoryCode);
                    }
                }
            }

            return codes.OrderBy(c => c).ToList();
        });
    }

    /// <summary>
    /// Get summary statistics for all symbol sequences
    /// </summary>
    public async Task<SymbolStatistics> GetStatisticsAsync()
    {
        return await _sferaService.ExecuteWithLockAsync(() =>
        {
            var stats = new SymbolStatistics();
            var asortymenty = _sferaService.GetManager("Asortymenty");
            if (asortymenty == null)
                return stats;

            var prefixCounts = new Dictionary<string, int>();
            var supplierCounts = new Dictionary<string, int>();

            foreach (var asortyment in asortymenty.Dane.Wszystkie())
            {
                stats.TotalProducts++;

                string? symbol = Helpers.DynamicPropertyHelper.GetString(asortyment, "Symbol");
                if (string.IsNullOrEmpty(symbol))
                {
                    stats.ProductsWithoutSymbol++;
                    continue;
                }

                var parsed = ParseSymbol(symbol);
                if (parsed == null)
                {
                    stats.ProductsWithNonStandardSymbol++;
                    stats.NonStandardSymbols.Add(symbol);
                    continue;
                }

                stats.ProductsWithStandardSymbol++;
                if (parsed.IsVariant)
                    stats.VariantProducts++;

                var prefix = $"{parsed.SupplierCode}{parsed.CategoryCode}";
                prefixCounts[prefix] = prefixCounts.GetValueOrDefault(prefix, 0) + 1;
                supplierCounts[parsed.SupplierCode] = supplierCounts.GetValueOrDefault(parsed.SupplierCode, 0) + 1;
            }

            stats.PrefixCounts = prefixCounts.OrderByDescending(kv => kv.Value)
                .Select(kv => new PrefixCount { Prefix = kv.Key, Count = kv.Value })
                .ToList();

            stats.SupplierCounts = supplierCounts.OrderByDescending(kv => kv.Value)
                .Select(kv => new SupplierCount { SupplierCode = kv.Key, Count = kv.Value })
                .ToList();

            return stats;
        });
    }
}

#region DTOs

public class ProductSymbolInfo
{
    public string FullSymbol { get; set; } = "";
    public string SupplierCode { get; set; } = "";
    public string CategoryCode { get; set; } = "";
    public int SequenceNumber { get; set; }
    public string? VariantSuffix { get; set; }
    public bool IsVariant { get; set; }
    public string BaseSymbol { get; set; } = "";
}

public class SymbolValidationResult
{
    public string Symbol { get; set; } = "";
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public ProductSymbolInfo? ParsedInfo { get; set; }
}

public class SequenceAnalysisResult
{
    public string SupplierCode { get; set; } = "";
    public string CategoryCode { get; set; } = "";
    public string Prefix { get; set; } = "";
    public List<int> UsedNumbers { get; set; } = new();
    public List<int> Gaps { get; set; } = new();
    public int TotalUsed { get; set; }
    public int? MaxUsed { get; set; }
    public int? NextAfterMax { get; set; }
    public int NextAvailable { get; set; }
    public string? Error { get; set; }
}

public class SymbolStatistics
{
    public int TotalProducts { get; set; }
    public int ProductsWithStandardSymbol { get; set; }
    public int ProductsWithNonStandardSymbol { get; set; }
    public int ProductsWithoutSymbol { get; set; }
    public int VariantProducts { get; set; }
    public List<string> NonStandardSymbols { get; set; } = new();
    public List<PrefixCount> PrefixCounts { get; set; } = new();
    public List<SupplierCount> SupplierCounts { get; set; } = new();
}

public class PrefixCount
{
    public string Prefix { get; set; } = "";
    public int Count { get; set; }
}

public class SupplierCount
{
    public string SupplierCode { get; set; } = "";
    public int Count { get; set; }
}

#endregion
