using System.Data.Entity;
using System.Data.Entity.SqlServer;

namespace NexoSferaApi.Configuration;

/// <summary>
/// Entity Framework 6 initialization helper for .NET 8 compatibility.
/// Uses official Microsoft.EntityFramework.SqlServer provider for correct BIT type mapping.
/// </summary>
public static class EF6Initializer
{
    private static bool _initialized = false;
    private static readonly object _lock = new();

    /// <summary>
    /// Initializes EF6 with the Microsoft.Data.SqlClient provider.
    /// Must be called early in application startup before any Sfera operations.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;

        lock (_lock)
        {
            if (_initialized) return;

            // Set the official Microsoft EF6 SQL Server configuration
            // This configures EF6 to use Microsoft.Data.SqlClient with correct type mappings
            DbConfiguration.SetConfiguration(new MicrosoftSqlDbConfiguration());

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