namespace NexoSferaApi.Configuration;

public class SferaSettings
{
    public string Server { get; set; } = "(local)\\INSERTNEXO";
    public string Database { get; set; } = "Nexo_demo_1";
    public string? SqlLogin { get; set; }
    public string? SqlPassword { get; set; }
    public bool UseWindowsAuth { get; set; } = true;
    public string NexoLogin { get; set; } = "Szef";
    public string NexoPassword { get; set; } = "robocze";
    public string Product { get; set; } = "Subiekt"; // Subiekt, Rachmistrz, Rewizor, Gratyfikant

    // Context settings - required for document operations
    public string? DefaultWarehouse { get; set; } // e.g., "MAG"
    public string? DefaultBranch { get; set; } // e.g., "CENTRALA"
    public string? DefaultCashRegister { get; set; } // e.g., "CENTR"

    // SDK synchronization settings
    public string? NexoInstallPath { get; set; }     // Path to nexo installation directory
    public bool AutoSyncSdk { get; set; } = true;    // Auto-sync SDK DLLs on startup

    /// <summary>
    /// When the client machine has a nexo deployment with a DIFFERENT SDK version than the
    /// build shipped with, overwrite the runtime DLLs with the deployment's binaries
    /// (keeps the SDK consistent with the client's database version). When false, only
    /// missing DLLs are filled in and the build's binaries always win.
    /// </summary>
    public bool PreferDeploymentBinaries { get; set; } = true;

    /// <summary>
    /// Optional URL of a zip archive with SDK DLLs, downloaded at startup when no nexo
    /// deployment/installation is found on the machine and the build did not ship the DLLs.
    /// Same package format as the CI NEXO_SDK_URL secret.
    /// </summary>
    public string? SdkFallbackUrl { get; set; }

    /// <summary>Optional bearer token for SdkFallbackUrl.</summary>
    public string? SdkFallbackToken { get; set; }

    public bool SyncSdkSource { get; set; } = false;  // Also update lib/nexo-sdk/ source dir

    /// <summary>
    /// Creates a copy of these settings pointing at a different database (and optionally
    /// a different default warehouse/branch). Used by the multi-company connection pool -
    /// everything else (server, SQL auth, product, default operator) is inherited.
    /// </summary>
    public SferaSettings CloneForDatabase(string database, string? defaultWarehouse = null, string? defaultBranch = null)
    {
        return new SferaSettings
        {
            Server = Server,
            Database = database,
            SqlLogin = SqlLogin,
            SqlPassword = SqlPassword,
            UseWindowsAuth = UseWindowsAuth,
            NexoLogin = NexoLogin,
            NexoPassword = NexoPassword,
            Product = Product,
            DefaultWarehouse = defaultWarehouse ?? DefaultWarehouse,
            DefaultBranch = defaultBranch ?? DefaultBranch,
            DefaultCashRegister = DefaultCashRegister,
            NexoInstallPath = NexoInstallPath,
            AutoSyncSdk = AutoSyncSdk,
            PreferDeploymentBinaries = PreferDeploymentBinaries,
            SdkFallbackUrl = SdkFallbackUrl,
            SdkFallbackToken = SdkFallbackToken,
            SyncSdkSource = SyncSdkSource
        };
    }
}
