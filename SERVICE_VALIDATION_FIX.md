# Service Items Stock Validation Fix

## Problem Description

Service items (e.g., "Koszt przesyłki + obsługi zamówienia") were incorrectly failing stock validation, preventing the creation of sales invoices and receipts when those documents contained service items.

### Error Symptoms
```
warn: NexoSferaApi.Helpers.StockValidationHelper[0]
      Insufficient stock detected: Insufficient stock for 'DMSUSGI00081': requested 1,0, available 0 (shortage: 1,0)
warn: NexoSferaApi.Controllers.DocumentsController[0]
      Sales invoice creation failed - insufficient stock: Insufficient stock for 'DMSUSGI00081': requested 1,0, available 0 (shortage: 1,0)
```

## Root Cause

The `IsService()` method in `src/Helpers/StockValidationHelper.cs` was using direct dynamic property access instead of the `DynamicPropertyHelper` class:

**Problematic code (original version before fix):**
```csharp
// 2. GrupaTowaru
if (product.GrupaTowaru != null && product.GrupaTowaru == "US")
    return true;

// 3. TypTowaru
if (product.TypTowaru != null && (int)product.TypTowaru == 2)
    return true;
```

This direct property access doesn't work consistently with Nexo SDK's dynamic `EntityObject` types, causing the checks to fail even when the properties had the correct values.

## Solution

### 1. Fixed Property Access
Changed all direct property access to use `DynamicPropertyHelper`:

```csharp
// 2. GrupaTowaru - alternative check for service type
// Some systems use "US" to mark services
var grupaTowaru = DynamicPropertyHelper.GetString(product, "GrupaTowaru");
if (!string.IsNullOrEmpty(grupaTowaru) && grupaTowaru.Equals("US", StringComparison.OrdinalIgnoreCase))
{
    return true;
}

// 3. TypTowaru - another alternative marker
// TypTowaru == 2 indicates a service in some configurations
var typTowaru = DynamicPropertyHelper.GetNullableInt(product, "TypTowaru");
if (typTowaru.HasValue && typTowaru.Value == 2)
{
    return true;
}
```

### 2. Enhanced Logging
Added comprehensive debug logging to track service detection:

```csharp
public static bool IsService(dynamic product, ILogger? logger = null)
{
    // ... detection logic ...
    
    if (isService && logger != null)
    {
        logger.LogDebug("Product '{Symbol}' (ID: {Id}) detected as service via GrupaTowaru='{GrupaTowaru}'", 
            (object)productSymbol, (object)productId, (object)grupaTowaru);
    }
}
```

This helps diagnose which detection method successfully identified a service:
- Rodzaj_Id = 1
- GrupaTowaru = "US"
- TypTowaru = 2
- Name/symbol pattern matching

### 3. Code Cleanup
Removed the obsolete `IsService__old_methods()` method that was no longer being used.

## How Service Detection Works

The `IsService()` method checks products in the following priority order:

1. **Rodzaj_Id** (most reliable)
   - `Rodzaj_Id = 1` → Service (Usługa)
   - `Rodzaj_Id = 2` → Product (Towar)
   - `Rodzaj_Id = 3` → Set/Bundle (Komplet)

2. **GrupaTowaru** (alternative marker)
   - `GrupaTowaru = "US"` → Service

3. **TypTowaru** (another alternative)
   - `TypTowaru = 2` → Service

4. **Name/Symbol Pattern Matching** (fallback heuristic)
   - Checks for patterns like:
     - "usługa", "service"
     - "koszt przesyłki", "shipping cost"
     - "obsługa", "handling"
     - "dostawa", "delivery"
     - "transport", "montaż", "instalacja"
     - "opłata", "fee", "charge"

## Impact

### Before Fix
- Documents with service items failed with "Insufficient stock" errors
- Service items like shipping costs blocked invoice/receipt creation
- Users had to manually exclude service items or work around the validation

### After Fix
- Service items are properly detected using multiple methods
- Stock validation is automatically skipped for service items
- Documents with mixed product types (physical goods + services) can be created successfully
- Detailed logging helps diagnose any edge cases

## Testing Recommendations

To verify the fix works correctly:

1. **Test with Service Items**
   ```
   POST /api/documents/sales-invoices
   {
     "items": [
       { "productSymbol": "PRODUCT123", "quantity": 5 },      // Physical product
       { "productSymbol": "DMSUSGI00081", "quantity": 1 }     // Service (shipping)
     ]
   }
   ```

2. **Check Logs**
   Look for debug messages like:
   ```
   dbug: Product 'DMSUSGI00081' (ID: 100347) detected as service via GrupaTowaru='US'
   info: Skipping stock validation for service product 'DMSUSGI00081' (ID: 100347, Name: 'Koszt przesyłki + obsługi zamówienia')
   ```

3. **Verify No Stock Errors**
   Service items should NOT generate "Insufficient stock" warnings

## Related Files

- `src/Helpers/StockValidationHelper.cs` - Contains the fixed `IsService()` method
- `src/Helpers/DynamicPropertyHelper.cs` - Helper for safe property access on dynamic SDK objects
- `src/Controllers/DocumentsController.cs` - Uses stock validation before creating documents

## Technical Notes

### Why DynamicPropertyHelper?

The Nexo Sfera SDK uses Entity Framework 6 with `EntityObject` base types. These objects don't expose properties in the expected way at runtime, causing direct property access on `dynamic` types to fail or return null even when values exist.

`DynamicPropertyHelper` uses reflection to safely access properties:
```csharp
public static string? GetString(dynamic obj, string propertyName)
{
    try
    {
        var type = obj.GetType();
        var prop = type.GetProperty(propertyName);
        return prop?.GetValue(obj)?.ToString();
    }
    catch
    {
        return null;
    }
}
```

### Logger Parameter Casting

When using the optional logger parameter, all arguments must be cast to `object` to avoid C# compiler error CS1973:
```csharp
logger.LogDebug("Message {Symbol}", (object)productSymbol, (object)productId);
```

This is required because:
1. Extension methods (like `LogDebug`) cannot be called on nullable types with the null-conditional operator
2. The compiler needs explicit types to resolve the extension method

## Future Enhancements

Potential improvements to consider:

1. **Add Unit Tests**
   - Test service detection with various Rodzaj_Id, GrupaTowaru, and TypTowaru values
   - Test pattern matching with different service names
   - Test mixed documents with products and services

2. **Support for Sets/Bundles**
   - Consider special handling for `Rodzaj_Id = 3` (Komplet)
   - May need custom stock calculation for bundled items

3. **Configuration**
   - Make service name patterns configurable via appsettings.json
   - Allow custom service detection rules per tenant

4. **Performance**
   - Cache service detection results during a single request
   - Avoid repeated reflection calls for the same product

## References

- Nexo SDK Documentation: `docs/nexoSDK_59.0.0.9026/`
- Issue: "Problemy z walidacją stanów magazynowych i rozpoznawaniem usług"
- Commits:
  - Fix IsService method to use DynamicPropertyHelper for all property access (9fa09c7)
  - Remove old unused IsService__old_methods, clean up code (595242c)
