using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.SqlServer;

namespace NexoSferaApi.Configuration;

/// <summary>
/// Entity Framework 6 configuration for .NET 8 compatibility.
/// Required because ASP.NET Core doesn't use app.config/web.config.
/// This class configures the SQL Server provider services for EF6.
/// </summary>
public class EF6Configuration : DbConfiguration
{
    public EF6Configuration()
    {
        // Set the SQL Server provider services
        SetProviderServices("System.Data.SqlClient", SqlProviderServices.Instance);

        // Set the default connection factory for SQL Server
        SetDefaultConnectionFactory(new SqlConnectionFactory());

        // Enable execution strategy for transient fault handling
        SetExecutionStrategy("System.Data.SqlClient", () => new DefaultExecutionStrategy());
    }
}

/// <summary>
/// Static helper to initialize EF6 configuration before any EF operations.
/// Must be called early in application startup.
/// </summary>
public static class EF6Initializer
{
    private static bool _initialized = false;
    private static readonly object _lock = new();

    /// <summary>
    /// Initializes Entity Framework 6 for use in .NET 8 without app.config.
    /// Registers the SQL Server provider and sets up the DbConfiguration.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;

        lock (_lock)
        {
            if (_initialized) return;

            try
            {
                // Register the SQL Server DbProviderFactory
                // This is required in .NET Core/.NET 8 where providers aren't auto-registered
                System.Data.Common.DbProviderFactories.RegisterFactory(
                    "System.Data.SqlClient",
                    System.Data.SqlClient.SqlClientFactory.Instance);
            }
            catch (ArgumentException)
            {
                // Provider already registered, ignore
            }

            try
            {
                // Set the EF6 configuration
                DbConfiguration.SetConfiguration(new EF6Configuration());
            }
            catch (InvalidOperationException)
            {
                // Configuration already set, ignore
            }

            _initialized = true;
        }
    }
}