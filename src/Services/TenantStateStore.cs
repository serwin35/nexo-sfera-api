using System.Collections.Concurrent;

namespace NexoSferaApi.Services;

/// <summary>
/// State kept between requests for one pooled connection (one database), partitioned by <see cref="TenantCacheKey"/>.
/// Owned by <see cref="SferaService"/> and cleared when the connection reconnects or is disposed, so cached SDK data never
/// outlives the connection it was read from and never crosses databases.
/// </summary>
/// <remarks>Use through <see cref="ISferaService.GetTenantState{T}"/>, which supplies the key of the current tenant.</remarks>
public sealed class TenantStateStore
{
    private readonly ConcurrentDictionary<(string TenantKey, string Name, Type Type), object> _entries = new();

    /// <summary>The state object of <paramref name="name"/> for <paramref name="tenantKey"/>, created on first use.</summary>
    public T GetOrAdd<T>(string tenantKey, string name, Func<T> factory) where T : class
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantKey);
        ArgumentException.ThrowIfNullOrEmpty(name);

        return (T)_entries.GetOrAdd((tenantKey, name, typeof(T)), _ => factory());
    }

    /// <summary>Drops all state (reconnect, dispose).</summary>
    public void Clear() => _entries.Clear();
}
