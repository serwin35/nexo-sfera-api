using InsERT.Moria.Sfera;

namespace NexoSferaApi.Services;

/// <summary>
/// Multi-company implementation of <see cref="ISferaService"/>.
///
/// Resolves the target Nexo database from the authenticated API key's claims and routes
/// every call to the matching pooled <see cref="SferaService"/> connection. Inside
/// <see cref="ExecuteWithLockAsync{T}(Func{T})"/> it additionally enforces the per-key
/// operator (<c>NexoLogin</c>/<c>NexoPassword</c>) and working context
/// (<c>NexoWarehouse</c>/<c>NexoBranch</c>) before running the operation - so ALL
/// controllers are tenant-aware without any per-controller code.
///
/// Calls made from inside an ExecuteWithLockAsync lambda (e.g. GetManager) run on the
/// connection's STA thread where HttpContext does not flow; a ThreadStatic ambient
/// connection set around the operation guarantees they hit the same connection.
/// </summary>
public class SferaServiceRouter : ISferaService
{
    private readonly SferaConnectionPool _pool;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<SferaServiceRouter> _logger;

    // Each pooled connection has its own dedicated STA thread, so ThreadStatic is the
    // correct ambient scope: the value set on connection X's thread is only ever read
    // by work items running on that same thread.
    [ThreadStatic]
    private static SferaService? t_ambientConnection;

    public SferaServiceRouter(
        SferaConnectionPool pool,
        IHttpContextAccessor httpContextAccessor,
        ILogger<SferaServiceRouter> logger)
    {
        _pool = pool;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public bool IsConnected => ResolveConnection().IsConnected;

    /// <summary>Initializes the default-database connection (application startup).</summary>
    public Task InitializeAsync() => _pool.GetConnection(null).InitializeAsync();

    /// <summary>Reconnects the connection of the current tenant (or default outside a request).</summary>
    public Task ReinitializeAsync() => ResolveConnection().ReinitializeAsync();

    public Uchwyt GetSfera() => ResolveConnection().GetSfera();

    public dynamic? GetManager(string managerMethodName) => ResolveConnection().GetManager(managerMethodName);

    public dynamic? GetManagerByType(string assemblyName, string typeName) =>
        ResolveConnection().GetManagerByType(assemblyName, typeName);

    public string? GetConnectionString(bool forLegacySqlClient = false) =>
        ResolveConnection().GetConnectionString(forLegacySqlClient);

    public bool SwitchOperatorIfNeeded(string? nexoLogin, string? nexoPassword) =>
        ResolveConnection().SwitchOperatorIfNeeded(nexoLogin, nexoPassword);

    public string? GetCurrentOperatorLogin() => ResolveConnection().GetCurrentOperatorLogin();

    public async Task<T> ExecuteWithLockAsync<T>(Func<T> operation)
    {
        var tenant = CurrentTenant();
        var connection = _pool.GetConnection(tenant.Database);

        // Idempotent - connects only when there is no live handle yet (lazy first use of a tenant database).
        await connection.InitializeAsync();

        return await connection.ExecuteWithLockAsync(() =>
        {
            var previous = t_ambientConnection;
            t_ambientConnection = connection;
            try
            {
                ApplyTenant(connection, tenant);
                return operation();
            }
            finally
            {
                t_ambientConnection = previous;
            }
        });
    }

    public async Task<T> ExecuteWithLockAsync<T>(Func<Task<T>> operation)
    {
        var tenant = CurrentTenant();
        var connection = _pool.GetConnection(tenant.Database);

        await connection.InitializeAsync();

        return await connection.ExecuteWithLockAsync(async () =>
        {
            var previous = t_ambientConnection;
            t_ambientConnection = connection;
            try
            {
                ApplyTenant(connection, tenant);
                return await operation();
            }
            finally
            {
                t_ambientConnection = previous;
            }
        });
    }

    /// <summary>
    /// Resolution order: ambient connection (we are on a connection's STA thread inside a
    /// lock) -> the current request's tenant database -> the default connection.
    /// </summary>
    private SferaService ResolveConnection()
    {
        if (t_ambientConnection != null)
        {
            return t_ambientConnection;
        }

        return _pool.GetConnection(CurrentTenant().Database);
    }

    private SferaTenantContext CurrentTenant() =>
        SferaTenantContext.FromPrincipal(_httpContextAccessor.HttpContext?.User);

    /// <summary>
    /// Enforces operator and working context for the tenant. Runs on the connection's STA
    /// thread, inside the lock, before the actual operation. With no overrides this restores
    /// the connection defaults, so one tenant's operator never leaks into another's request.
    /// </summary>
    private void ApplyTenant(SferaService connection, SferaTenantContext tenant)
    {
        if (!connection.EnsureOperator(tenant.NexoLogin, tenant.NexoPassword))
        {
            _logger.LogError("Failed to switch to operator {Login} on database {Database}",
                tenant.NexoLogin, connection.Database);
            throw new InvalidOperationException(
                $"Failed to login Nexo operator '{tenant.NexoLogin}' configured for this API key.");
        }

        connection.ApplyRequestContext(tenant.Warehouse, tenant.Branch);
    }
}
