using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using NexoSferaApi.Configuration;

namespace NexoSferaApi.Services;

/// <summary>
/// Maintains one <see cref="SferaService"/> connection per Nexo database.
/// Each connection owns its dedicated STA thread, so different companies (databases)
/// never share SDK state. Connections are created lazily on first use and live for
/// the lifetime of the application.
/// </summary>
public class SferaConnectionPool : IDisposable
{
    private readonly SferaSettings _defaultSettings;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<SferaConnectionPool> _logger;

    // Lazy ensures exactly one SferaService is constructed per database even under
    // concurrent first requests.
    private readonly ConcurrentDictionary<string, Lazy<SferaService>> _connections = new(StringComparer.OrdinalIgnoreCase);

    public SferaConnectionPool(IOptions<SferaSettings> defaultSettings, ILoggerFactory loggerFactory)
    {
        _defaultSettings = defaultSettings.Value;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<SferaConnectionPool>();
    }

    /// <summary>
    /// Gets (creating lazily if needed) the connection for the given database.
    /// Null/empty database means the default database from Sfera settings.
    /// The returned connection may not be initialized yet - await
    /// <see cref="SferaService.InitializeAsync"/> before executing SDK work.
    /// </summary>
    public SferaService GetConnection(string? database)
    {
        var databaseName = string.IsNullOrWhiteSpace(database) ? _defaultSettings.Database : database.Trim();

        var lazy = _connections.GetOrAdd(databaseName, name => new Lazy<SferaService>(() =>
        {
            _logger.LogInformation("Creating Sfera connection for database {Database} (pool size: {Count})",
                name, _connections.Count);

            var settings = _defaultSettings.CloneForDatabase(name);
            return new SferaService(settings, _loggerFactory.CreateLogger<SferaService>());
        }));

        return lazy.Value;
    }

    /// <summary>Databases with an existing (created) connection.</summary>
    public IReadOnlyCollection<string> ActiveDatabases =>
        _connections.Where(kv => kv.Value.IsValueCreated).Select(kv => kv.Key).ToList();

    public void Dispose()
    {
        foreach (var entry in _connections.Values)
        {
            if (!entry.IsValueCreated) continue;

            try
            {
                entry.Value.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing pooled Sfera connection");
            }
        }

        _connections.Clear();
    }
}
