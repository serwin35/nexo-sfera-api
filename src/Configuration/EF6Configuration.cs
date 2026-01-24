namespace NexoSferaApi.Configuration;

/// <summary>
/// Entity Framework 6 initialization helper for .NET 8 compatibility.
/// Required because ASP.NET Core doesn't auto-register DbProviderFactories.
/// NOTE: The Nexo SDK bundles its own EF6 implementation (InsERT.Mox.EntityFramework.Core),
/// so we only register the SqlClient provider factory here - the SDK handles its own DbConfiguration.
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

            // Register Microsoft.Data.SqlClient - matching EasyNexoIntegrator configuration
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

            // Also register under System.Data.SqlClient name for SDK compatibility
            try
            {
                System.Data.Common.DbProviderFactories.RegisterFactory(
                    "System.Data.SqlClient",
                    Microsoft.Data.SqlClient.SqlClientFactory.Instance);
            }
            catch (System.ArgumentException)
            {
                // Provider already registered, ignore
            }

            _initialized = true;
        }
    }
}