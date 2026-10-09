# Security review rules for nexo-sfera-api

The bridge serves several Nexo companies from one process. `SferaServiceRouter` resolves the tenant (database,
operator, warehouse, branch) from the API key claims on every request. Treat these as findings.

## Tenant isolation

- Data read from the SDK (products, stock, customers, documents, dictionaries) is cached only through
  `ISferaService.GetTenantState<T>`. That store belongs to the pooled connection and is partitioned by `TenantCacheKey`.
  A `static` field, a singleton dictionary or `IMemoryCache` holding SDK data is a cross-tenant leak, even when the key contains
  request filters.
- Cache keys and log lines never contain `NexoPassword`, API keys or connection strings.
- Every SQL query that goes past the SDK uses the connection of the current tenant, never a shared or default one.

## Endpoints

- Endpoints require API key authentication. `[AllowAnonymous]` is allowed only for health and the existing settings
  bootstrap. Diagnostic and `debug/*` endpoints carry `[DevelopmentOnly]`.
- Production error responses do not include stack traces, SQL or SDK exception details.

## Writes

- A write sets only SDK members that exist; check them with typed readers and writers like `ProductWriter`, not with `dynamic`.
- A request field the endpoint cannot apply returns 400 with the field name. It is never accepted and ignored. Example:
  `documentIdsToSettle` on payments.
- Fiscal and accounting writes (FS, PA, FZ, KP/KW/BP/BW, KSeF import) stay all-or-nothing, run in a single SDK transaction, and
  do not retry on their own.
