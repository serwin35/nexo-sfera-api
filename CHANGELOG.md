# Changelog

All notable changes to the Nexo Sfera API will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **Service-type product detection in stock validation** - Added `IsService()` method to `StockValidationHelper` that identifies service products by checking `Rodzaj_Id = 1`
- **Automatic stock validation skip for services** - Services no longer trigger "insufficient stock" errors since they don't require physical inventory
- **StatusSymbol in document DTOs** - Added `StatusSymbol` field to all document mapping methods for better status tracking
- **Enhanced error diagnostics for receipts** - Improved `GetBusinessObjectErrors()` to properly extract validation errors from SDK's `InvalidData` collection
- **Comprehensive receipt item logging** - Added detailed logging to `AddReceiptItemsById()` to track item addition and diagnose issues

### Changed
- **Stock validation logic** - Modified `ValidateStock()` in `StockValidationHelper` to skip stock checks for service-type items (Rodzaj_Id = 1)
- Stock validation now logs debug messages when skipping services: `"Skipping stock validation for service: {Symbol} (Rodzaj_Id=1)"`
- **IsService() method improvements** - Now prioritizes reliable `Rodzaj_Id` FK check over navigation property access, with multiple fallback mechanisms
- **Receipt creation simplified** - Removed problematic status manipulation that was causing validation failures; now follows minimal SDK pattern

### Fixed
- **Stock validation errors for services** - Services no longer fail with "insufficient stock" errors when creating sales documents (invoices, receipts)
- **Document status in API responses** - Status information now properly included in all document DTOs (StatusId, Status, StatusSymbol)
- **Receipt validation failures** - Fixed MoznaZapisac=false issue by removing direct StatusDokumentuId manipulation that put receipts in invalid state
- **IsService() null reference exceptions** - Fixed exceptions when Rodzaj navigation property is null; now handles null values gracefully
- **Receipt creation for historical dates** - Simplified historical document handling to use minimal flags instead of status manipulation

### Verified
- **Import remarks handling** - Confirmed that invoices and receipts skip "Import ILUO" type notes via `ShouldSkipImportRemarks()` and `ShouldSkipImportRemarksForContext()` methods
- **Multi-tenant authentication** - Verified that per-API-key Nexo credentials (NexoLogin/NexoPassword) work correctly via claims-based authentication

## Technical Details

### Stock Validation for Services
Previously, service items (like "DMSUSGI00081") would fail stock validation with errors like:
```
Sales invoice creation failed - insufficient stock: Insufficient stock for 'DMSUSGI00081': 
requested 1,0, available 0 (shortage: 1,0)
```

Now, the system detects services by checking:
1. `Rodzaj_Id = 1` (primary check - Service type in Nexo)
2. `Rodzaj.Symbol = "Usluga"` or `"U"` (fallback check)

Services are marked as available without checking stock levels:
- `HasSufficientStock = true`
- `AvailableQuantity = 0` (services don't track inventory)

### Document Status Enhancement
All document mapping methods now include:
- `StatusId` - Numeric status identifier
- `Status` - Status name (e.g., "W realizacji", "Zrealizowano")
- `StatusSymbol` - Status symbol for programmatic checks

This applies to:
- `MapSalesDocumentToDto()` - Sales invoices, receipts
- `MapPurchaseDocumentToDto()` - Purchase invoices
- `MapOrderToDto()` - Customer orders

### Authentication Architecture
The API supports multi-tenant scenarios where different API keys can use different Nexo operators:

1. **API Key Configuration** (`appsettings.json`):
   ```json
   {
     "ApiKeys": {
       "Keys": [
         {
           "Key": "api-key-1",
           "Name": "Company A",
           "NexoLogin": "operator1",
           "NexoPassword": "password1"
         },
         {
           "Key": "api-key-2",
           "Name": "Company B",
           "NexoLogin": "operator2",
           "NexoPassword": "password2"
         }
       ]
     }
   }
   ```

2. **Authentication Flow**:
   - API key is validated by `ApiKeyAuthenticationHandler`
   - NexoLogin/NexoPassword are added as claims if configured
   - Controllers call `GetOperatorCredentialsFromClaims()` to retrieve credentials
   - `SwitchToRequestOperator()` switches Nexo operator context before document operations

3. **Usage**:
   ```bash
   curl -X POST http://localhost:5000/api/documents/sales-invoice \
     -H "Authorization: Bearer api-key-1" \
     -H "Content-Type: application/json" \
     -d '{ ... }'
   ```

This allows the same API instance to serve multiple companies/operators with proper data isolation.

---

## Previous Releases

See git history for changes prior to this changelog.
