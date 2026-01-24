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

            // Register Microsoft.Data.SqlClient provider factory
            try
            {
                System.Data.Common.DbProviderFactories.RegisterFactory(
                    "Microsoft.Data.SqlClient",
                    Microsoft.Data.SqlClient.SqlClientFactory.Instance);
            }
            catch (System.ArgumentException)
            {
                // Provider already registered, ignore
            }

            // Also register System.Data.SqlClient for compatibility
            try
            {
                System.Data.Common.DbProviderFactories.RegisterFactory(
                    "System.Data.SqlClient",
                    System.Data.SqlClient.SqlClientFactory.Instance);
            }
            catch (System.ArgumentException)
            {
                // Provider already registered, ignore
            }

            _initialized = true;
        }
    }
}