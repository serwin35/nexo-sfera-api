using System.Reflection;

namespace NexoSferaApi.Configuration;

/// <summary>
/// Entity Framework 6 initialization helper for .NET 8 compatibility.
/// The Nexo SDK has its own EF6 implementation (InsERT.Mox.EntityFramework.Core) with
/// MicrosoftSqlDbConfiguration that should handle type mappings correctly.
/// </summary>
public static class EF6Initializer
{
    private static bool _initialized = false;
    private static readonly object _lock = new();
    private static string? _initializationLog;
    private static bool _configurationSet = false;

    /// <summary>
    /// Gets the initialization log for diagnostic purposes.
    /// </summary>
    public static string? InitializationLog => _initializationLog;

    /// <summary>
    /// Gets whether the SDK's DbConfiguration was successfully set.
    /// </summary>
    public static bool ConfigurationSet => _configurationSet;

    /// <summary>
    /// Initializes the SQL Server DbProviderFactory for use with Nexo SDK's internal EF6.
    /// Must be called early in application startup before any Sfera operations.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;

        lock (_lock)
        {
            if (_initialized) return;

            var log = new System.Text.StringBuilder();
            log.AppendLine($"EF6Initializer started at {DateTime.Now:O}");

            // IMPORTANT: Register System.Data.SqlClient FIRST as the primary provider
            // The SDK was designed for .NET Framework 4.7.2 which used System.Data.SqlClient
            // Registration order matters for provider resolution
            try
            {
                System.Data.Common.DbProviderFactories.RegisterFactory(
                    "System.Data.SqlClient",
                    System.Data.SqlClient.SqlClientFactory.Instance);
                log.AppendLine("Registered System.Data.SqlClient provider factory (primary)");
            }
            catch (System.ArgumentException)
            {
                log.AppendLine("System.Data.SqlClient provider already registered");
            }

            // Also register Microsoft.Data.SqlClient for compatibility with newer code
            // But register it AFTER System.Data.SqlClient
            try
            {
                System.Data.Common.DbProviderFactories.RegisterFactory(
                    "Microsoft.Data.SqlClient",
                    Microsoft.Data.SqlClient.SqlClientFactory.Instance);
                log.AppendLine("Registered Microsoft.Data.SqlClient provider factory (secondary)");
            }
            catch (System.ArgumentException)
            {
                log.AppendLine("Microsoft.Data.SqlClient provider already registered");
            }

            // Try to activate SDK's MicrosoftSqlDbConfiguration via reflection
            try
            {
                log.AppendLine("Attempting to load SDK EF assemblies...");
                var sdkEfAssembly = Assembly.Load("InsERT.Mox.EntityFramework.Ms.SqlServer");
                log.AppendLine($"Loaded: {sdkEfAssembly?.FullName}");

                var configType = sdkEfAssembly?.GetType("System.Data.Entity.SqlServer.MicrosoftSqlDbConfiguration");
                log.AppendLine($"MicrosoftSqlDbConfiguration type: {configType?.FullName ?? "NOT FOUND"}");

                if (configType != null)
                {
                    var coreAssembly = Assembly.Load("InsERT.Mox.EntityFramework.Core");
                    log.AppendLine($"Loaded core: {coreAssembly?.FullName}");

                    var dbConfigType = coreAssembly?.GetType("System.Data.Entity.DbConfiguration");
                    log.AppendLine($"DbConfiguration type: {dbConfigType?.FullName ?? "NOT FOUND"}");

                    if (dbConfigType != null)
                    {
                        // List all static methods
                        var staticMethods = dbConfigType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                        log.AppendLine($"DbConfiguration static methods: {string.Join(", ", staticMethods.Select(m => m.Name))}");

                        var setConfigMethod = dbConfigType.GetMethod("SetConfiguration", BindingFlags.Public | BindingFlags.Static);
                        if (setConfigMethod == null)
                        {
                            // Try with different binding flags
                            setConfigMethod = dbConfigType.GetMethod("SetConfiguration", BindingFlags.NonPublic | BindingFlags.Static);
                        }
                        log.AppendLine($"SetConfiguration method: {(setConfigMethod != null ? "FOUND" : "NOT FOUND")}");

                        if (setConfigMethod != null)
                        {
                            var configInstance = Activator.CreateInstance(configType);
                            log.AppendLine($"Created config instance: {configInstance?.GetType().FullName}");

                            setConfigMethod.Invoke(null, new[] { configInstance });
                            log.AppendLine("SetConfiguration invoked successfully!");
                            _configurationSet = true;
                        }
                        else
                        {
                            // Alternative: Try to find and use LoadConfiguration method
                            var loadMethod = dbConfigType.GetMethod("LoadConfiguration", BindingFlags.NonPublic | BindingFlags.Static);
                            if (loadMethod != null)
                            {
                                log.AppendLine("Found LoadConfiguration - attempting...");
                                loadMethod.Invoke(null, new object[] { configType });
                                log.AppendLine("LoadConfiguration invoked!");
                                _configurationSet = true;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.AppendLine($"Configuration error: {ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    log.AppendLine($"Inner: {ex.InnerException.Message}");
                }
            }

            log.AppendLine($"EF6Initializer completed. ConfigurationSet={_configurationSet}");
            _initializationLog = log.ToString();
            _initialized = true;
        }
    }

    /// <summary>
    /// Gets diagnostic information about the current EF6 configuration state.
    /// </summary>
    public static Dictionary<string, object?> GetDiagnostics()
    {
        var diagnostics = new Dictionary<string, object?>
        {
            ["Initialized"] = _initialized,
            ["ConfigurationSet"] = _configurationSet,
            ["InitializationLog"] = _initializationLog
        };

        // Check registered providers
        try
        {
            var providers = System.Data.Common.DbProviderFactories.GetProviderInvariantNames();
            diagnostics["RegisteredProviders"] = providers.ToList();
        }
        catch (Exception ex)
        {
            diagnostics["RegisteredProviders"] = $"Error: {ex.Message}";
        }

        // Check loaded EF assemblies
        var efAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.Contains("EntityFramework") == true ||
                        a.GetName().Name?.Contains("Mox.EntityFramework") == true)
            .Select(a => a.FullName)
            .ToList();
        diagnostics["LoadedEFAssemblies"] = efAssemblies;

        return diagnostics;
    }
}