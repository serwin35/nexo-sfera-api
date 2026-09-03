# Changelog

All notable changes to the Nexo Sfera API will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added (2026-09-03)
- **`ProductDto.Units`** (`ProductUnitDto[]`): all units of measure of a product from `Asortyment.JednostkiMiar`
  with `IsBase/IsSale/IsPurchase/IsWarehouse`, precision, package barcode, weight/volume and the conversion to the
  base unit (`ToBaseFactor`, `BaseUnitSymbol`, raw `Conversions` from `PrzelicznikJednostekMiarAsortymentu`).
  Needed by WolfFireApp to interpret stock levels kept in the base unit (embroidery thread: "szt" = 5000 "m").

### Added (2026-06-26)
- **SDK 61.0.0 fields exposed in DTOs** (all dynamic, null-safe, backward-compatible with older SDKs):
  - `AddressDto.GLN` (Global Location Number, `Adres.GLN`) - mapped in `CustomersController` from the address entity with fallback to `Szczegoly`
  - `ElectronicDocumentDto`: `IsCostInvoice` (`DokumentElektroniczny.Kosztowa`), `PaymentStatus` (`StanOplacenia`), `PaymentDueDate` (`TerminPlatnosci`), `IsSynchronized` (`Zsynchronizowany`) - mapped in `KsefController.MapElectronicDocument`
  - `ProductDto.ExternalStocks` + `ExternalWarehouseStockDto` (e-commerce external warehouse stock, `Asortyment.StanyMagazynoweZewnetrzne`) - mapped in the product detail mapper only (list view unaffected). The `StanMagazynowyZewnetrzny` field names (`Ilosc`, `MagazynZewnetrzny.Nazwa`/`IdZewnetrzny`) are best-effort and need confirmation against a live 61.0.0 instance via `debug/item-properties`
- **`scripts/sync-sdk-version.ps1`** - single-source-of-truth SDK version bumper: derives the version from the newest `docs/nexoSDK_<ver>` folder and rewrites it across `README.md` (badge + path), `scripts/setup-sdk.ps1`, and both CI workflows. Supports `-DryRun`, `-Version`, `-Tag` (creates the `sdk-<ver>` bookmark tag)

### Changed (2026-06-26)
- **Upgraded to InsERT Nexo SDK 61.0.0.9362** (from 60.1.1.9292). Verified all 64 required DLLs are present; the API/model changes (186 added / 11 removed / 80 changed API types; 12/1/70 model types) are contained within `InsERT.Moria.API.dll` and `InsERT.Moria.ModelDanych.dll` - no new standalone assemblies, so the required-DLL list and `.csproj` references are unchanged
  - Reviewed every removed / signature-changed / `[Obsolete]` SDK member against the codebase: zero usages - no breaking changes for our endpoints
  - Bumped version paths in `scripts/setup-sdk.ps1`, `.github/workflows/build.yml` + `release.yml` (replaced the stale `nexoSDK_58.0.2.8985` local fallback), and the README SDK badge
- Tagged the aligned commit as `sdk-61.0.0.9362` (bookmark tag for diffing across SDK versions; does not trigger the release pipeline)

### Added (2026-06-12)
- **True multi-company support** - new `SferaConnectionPool` (one SDK connection with its own STA thread per Nexo database) and `SferaServiceRouter` (routes every `ISferaService` call to the tenant's connection based on API key claims). Per-key `Database`, `NexoLogin`/`NexoPassword`, `DefaultWarehouse`, `DefaultBranch` are now enforced on EVERY request across all 61 controllers - previously operator switching worked only in 2 controllers and `Database` was ignored. Keys without an operator override are forced back to the default operator (no tenant leakage). Requires verification on Windows: multiple `Uchwyt` connections in one process.
  - Behavior changes: reconnect endpoints (`/api/health/reconnect`, `/api/settings/connection/reconnect`) now reconnect only the calling key's database connection; failed per-key operator login now surfaces as HTTP 500 with a clear message (previously a 4xx-ish error shaped per endpoint); the duplicated operator-switching helpers were removed from DocumentsController and WarehouseDocumentsController (the router owns switching now)
- **SDK DLLs removed from git entirely** - legacy root `lib/nexo-sdk/` deleted; `src/lib/nexo-sdk` stays the (untracked) build location, filled by `scripts/setup-sdk.ps1` locally or the `NEXO_SDK_URL` download step in CI (`.github/workflows/build.yml`)
- **Client-machine-first SDK binaries** - `Sfera:PreferDeploymentBinaries` (default `true`): when the client's nexo deployment (`%LocalAppData%\InsERT\Deployments\Nexo\*\Binaries`) differs from the build-shipped DLLs, the deployment's full consistent set now OVERWRITES the runtime DLLs (previously only missing DLLs were filled, leaving a version mix); `Sfera:SdkFallbackUrl`/`SdkFallbackToken` - when no nexo exists on the machine and the build shipped no DLLs, the SDK zip is downloaded at startup (same package as CI). `setup-sdk.ps1` also probes deployment Binaries now

### Security (2026-06-12)
- **MCP endpoint now requires authorization** - `/mcp` exposed write tools (invoices, receipts, customers) without any auth; now protected by the same API key scheme as REST
- **Secrets untracked from git** - `src/appsettings.json` (API key + Nexo credentials) removed from tracking; rotate the exposed keys
- **Debug endpoints gated to Development** - `debug/*` endpoints and `DiagnosticsController` return 404 outside the Development environment

### Fixed (2026-06-12)
- **Thread safety completed across all 61 controllers** - wrapped the last 22 unlocked endpoints (DocumentsController ×10 incl. `GET /api/documents`, corrections, advance/VAT-margin invoices, associations; CustomersController debug ×8; DiagnosticsController ×4) in `ExecuteWithLockAsync`
- **Reconnect actually reconnects** - new `ReinitializeAsync()` disposes the stale SDK handle and reconnects on the STA thread; previously `POST /api/health/reconnect` was a no-op and `POST /api/settings/connection/reconnect` disposed the singleton service permanently
- **Operator restore after failed switch** - previous operator is restored with its own tracked password (was: always the default password), with fallback to the default operator
- **Async `ExecuteWithLockAsync` deadlock risk** - now pumps WinForms messages while waiting so continuations posted to the STA synchronization context can run
- **Failed login no longer leaves a half-initialized connection** - `IsConnected` reflects a fully usable connection
- **Manager-unavailable no longer masked as 404** - `GET /api/documents/{id}` and `by-number` return 500 when the SDK manager cannot be obtained

### Removed (2026-06-12)
- Stale work logs (`FIXES_SUMMARY.md`, `IMPLEMENTATION_SUMMARY.md`, `RECEIPT_FIX_SUMMARY.md`, `SERVICE_VALIDATION_FIX.md`, `API-COMPLETION-PLAN.md`, `sdk-structure.md`), ghost dirs (`src/src/`, empty `src/scripts/`)
- Untracked from git: `src/lib/nexo-sdk/` DLL mirror (canonical copy stays in `lib/nexo-sdk/`), `.idea/`, auto-generated `CLAUDE.md` stubs

### Docs (2026-06-12)
- README rewritten: full module map (61 controllers), API conventions (response envelope, pagination, STA concurrency), Laravel integration example, honest multi-tenant status; audit report at `docs/AUDIT-2026-06-12.md` with SDK 60.1.1 coverage roadmap

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
