using InsERT.Moria.Sfera;

namespace NexoSferaApi.Services;

public interface ISferaService
{
    Task InitializeAsync();
    Uchwyt GetSfera();
    bool IsConnected { get; }

    /// <summary>
    /// Gets a manager by calling the corresponding method on Sfera (e.g., "Asortymenty" calls sfera.Asortymenty())
    /// </summary>
    dynamic? GetManager(string managerMethodName);

    /// <summary>
    /// Gets a typed manager using reflection-based PodajObiektTypu&lt;T&gt;() call
    /// Use this for interfaces/services, not for standard managers
    /// </summary>
    dynamic? GetManagerByType(string assemblyName, string typeName);

    /// <summary>
    /// Executes an SDK operation with thread synchronization.
    /// EF6 is NOT thread-safe, so all write operations must be serialized.
    /// Desktop apps are single-threaded but ASP.NET Core is multi-threaded.
    /// </summary>
    Task<T> ExecuteWithLockAsync<T>(Func<T> operation);

    /// <summary>
    /// Executes an async SDK operation with thread synchronization.
    /// </summary>
    Task<T> ExecuteWithLockAsync<T>(Func<Task<T>> operation);

    /// <summary>
    /// Gets the database connection string used by Sfera.
    /// Returns null if not connected.
    /// </summary>
    /// <param name="forLegacySqlClient">If true, builds connection string compatible with System.Data.SqlClient</param>
    string? GetConnectionString(bool forLegacySqlClient = false);

    /// <summary>
    /// Switches to a different operator if credentials differ from current login.
    /// This enables per-request operator context based on API key credentials.
    /// Must be called on the SDK STA thread (inside ExecuteWithLockAsync).
    /// </summary>
    /// <param name="nexoLogin">The Nexo operator login (from API key claims)</param>
    /// <param name="nexoPassword">The Nexo operator password (from API key claims)</param>
    /// <returns>True if switch was successful or not needed, false if switch failed</returns>
    bool SwitchOperatorIfNeeded(string? nexoLogin, string? nexoPassword);

    /// <summary>
    /// Gets the currently logged-in operator login.
    /// </summary>
    string? GetCurrentOperatorLogin();
}
