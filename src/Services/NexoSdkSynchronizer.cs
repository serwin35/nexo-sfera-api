using System.Diagnostics;
using System.Runtime.InteropServices;
using NexoSferaApi.Models;

namespace NexoSferaApi.Services;

/// <summary>
/// Synchronizes SDK DLLs from the nexo installation directory to the API runtime directory.
/// Must run before EF6Initializer.Initialize() to avoid loading outdated assemblies.
/// </summary>
public static class NexoSdkSynchronizer
{
    private static SdkSyncResult? _lastSyncResult;

    private const string SferaDllName = "InsERT.Moria.Sfera.dll";

    /// <summary>
    /// Result of the last sync operation (from startup).
    /// </summary>
    public static SdkSyncResult? LastSyncResult => _lastSyncResult;

    /// <summary>
    /// Synchronizes SDK DLLs if the nexo installation has a newer version.
    /// </summary>
    public static SdkSyncResult Synchronize(string? configuredInstallPath, string runtimeDir, ILogger? logger, bool alsoSyncSource = false, string? sourceLibDir = null)
    {
        var result = new SdkSyncResult();

        try
        {
            logger?.LogInformation("[SDK Sync] Starting SDK version check...");

            // Resolve nexo installation path
            var installPath = ResolveInstallPath(configuredInstallPath, logger);
            if (installPath == null)
            {
                result.Status = SdkSyncStatus.Skipped;
                result.Message = "Nexo installation directory not found. SDK sync skipped.";
                logger?.LogWarning("[SDK Sync] {Message}", result.Message);
                _lastSyncResult = result;
                return result;
            }

            logger?.LogInformation("[SDK Sync] Nexo installation found at: {Path}", installPath);

            // Find the actual directory containing SDK DLLs (may be in a subdirectory)
            var dllDir = FindDllDirectory(installPath, logger);
            if (dllDir == null)
            {
                result.Status = SdkSyncStatus.Skipped;
                result.Message = $"{SferaDllName} not found in nexo installation directory or subdirectories: {installPath}";
                logger?.LogWarning("[SDK Sync] {Message}", result.Message);
                _lastSyncResult = result;
                return result;
            }

            result.DetectedInstallPath = dllDir;
            logger?.LogInformation("[SDK Sync] SDK DLLs found in: {Path}", dllDir);

            // Compare versions using FileVersionInfo (reads PE header, doesn't load assembly)
            var localDllPath = Path.Combine(runtimeDir, SferaDllName);
            var installedDllPath = Path.Combine(dllDir, SferaDllName);

            var installedVersion = FileVersionInfo.GetVersionInfo(installedDllPath);
            result.InstalledVersion = installedVersion.FileVersion;

            if (File.Exists(localDllPath))
            {
                var localVersion = FileVersionInfo.GetVersionInfo(localDllPath);
                result.LocalVersion = localVersion.FileVersion;
                logger?.LogInformation("[SDK Sync] Local SDK version: {Local}, Installed nexo version: {Installed}",
                    result.LocalVersion, result.InstalledVersion);

                if (result.LocalVersion == result.InstalledVersion)
                {
                    result.Status = SdkSyncStatus.AlreadyCurrent;
                    result.Message = $"SDK is already up to date (version {result.LocalVersion}).";
                    logger?.LogInformation("[SDK Sync] {Message}", result.Message);
                    _lastSyncResult = result;
                    return result;
                }
            }
            else
            {
                result.LocalVersion = null;
                logger?.LogWarning("[SDK Sync] Local {Dll} not found, will copy from installation.", SferaDllName);
            }

            // Versions differ — copy DLLs
            logger?.LogInformation("[SDK Sync] Version mismatch detected. Syncing DLLs from {Source} to {Dest}...",
                dllDir, runtimeDir);

            CopyDlls(dllDir, runtimeDir, result, logger);

            // Optionally sync to lib/nexo-sdk/ source directory
            if (alsoSyncSource && !string.IsNullOrEmpty(sourceLibDir) && Directory.Exists(sourceLibDir))
            {
                logger?.LogInformation("[SDK Sync] Also syncing to source lib directory: {Dir}", sourceLibDir);
                var sourceResult = new SdkSyncResult();
                CopyDlls(dllDir, sourceLibDir, sourceResult, logger);
                result.FilesCopied += sourceResult.FilesCopied;
                result.FilesSkipped += sourceResult.FilesSkipped;
                result.FilesFailed += sourceResult.FilesFailed;
                result.Errors.AddRange(sourceResult.Errors);
            }

            // Determine final status
            if (result.FilesFailed > 0 && result.FilesCopied > 0)
            {
                result.Status = SdkSyncStatus.PartialFailure;
                result.Message = $"Partially synced: {result.FilesCopied} copied, {result.FilesFailed} failed, {result.FilesSkipped} skipped.";
            }
            else if (result.FilesFailed > 0)
            {
                result.Status = SdkSyncStatus.Failed;
                result.Message = $"Sync failed: {result.FilesFailed} files could not be copied.";
            }
            else
            {
                result.Status = SdkSyncStatus.Synchronized;
                result.Message = $"Synced {result.FilesCopied} DLLs from nexo {result.InstalledVersion} (was {result.LocalVersion ?? "missing"}). {result.FilesSkipped} unchanged.";
            }

            logger?.LogInformation("[SDK Sync] {Message}", result.Message);
        }
        catch (Exception ex)
        {
            result.Status = SdkSyncStatus.Failed;
            result.Message = $"SDK sync failed: {ex.Message}";
            result.Errors.Add(ex.ToString());
            logger?.LogError(ex, "[SDK Sync] Unexpected error during SDK synchronization");
        }

        _lastSyncResult = result;
        return result;
    }

    /// <summary>
    /// Resolves the nexo installation path from config, env var, registry, or default paths.
    /// </summary>
    public static string? ResolveInstallPath(string? configuredPath, ILogger? logger)
    {
        // 1. Explicit configuration
        if (!string.IsNullOrEmpty(configuredPath))
        {
            if (Directory.Exists(configuredPath))
            {
                logger?.LogDebug("[SDK Sync] Using configured install path: {Path}", configuredPath);
                return configuredPath;
            }
            logger?.LogWarning("[SDK Sync] Configured path does not exist: {Path}", configuredPath);
        }

        // 2. Environment variable
        var envPath = Environment.GetEnvironmentVariable("NEXO_INSTALL_PATH");
        if (!string.IsNullOrEmpty(envPath) && Directory.Exists(envPath))
        {
            logger?.LogDebug("[SDK Sync] Using NEXO_INSTALL_PATH env var: {Path}", envPath);
            return envPath;
        }

        // 3. Windows Registry (only on Windows)
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var registryPath = TryGetFromRegistry(logger);
            if (registryPath != null)
                return registryPath;
        }

        // 4. Build search paths list — Program Files + AppData\Local
        var searchPaths = new List<string>
        {
            @"C:\Program Files (x86)\InsERT\nexo",
            @"C:\Program Files\InsERT\nexo",
            @"C:\Program Files (x86)\InsERT\nexo PRO",
            @"C:\Program Files\InsERT\nexo PRO",
            @"C:\Program Files (x86)\InsERT\Subiekt nexo PRO",
            @"C:\Program Files\InsERT\Subiekt nexo PRO",
            @"C:\Program Files (x86)\InsERT",
            @"C:\Program Files\InsERT"
        };

        // Add AppData\Local paths — InsERT uses ClickOnce-style deployments
        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData))
            {
                // Primary: InsERT Deployments directory (contains per-entity deployment folders)
                searchPaths.Add(Path.Combine(localAppData, "InsERT", "Deployments", "Nexo"));
                searchPaths.Add(Path.Combine(localAppData, "InsERT", "Deployments"));
                searchPaths.Add(Path.Combine(localAppData, "InsERT"));
            }

            // Also check ProgramData
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (!string.IsNullOrEmpty(programData))
            {
                searchPaths.Add(Path.Combine(programData, "InsERT"));
            }
        }
        catch { /* ignore */ }

        // Search all paths — recurse into subdirectories looking for the DLL
        var searchRoots = new List<string>();
        foreach (var path in searchPaths)
        {
            if (!Directory.Exists(path)) continue;

            // Direct match
            if (File.Exists(Path.Combine(path, SferaDllName)))
            {
                logger?.LogDebug("[SDK Sync] Found {Dll} at: {Path}", SferaDllName, path);
                return path;
            }

            // Collect roots for recursive search
            var root = path;
            if (!searchRoots.Any(r => root.StartsWith(r, StringComparison.OrdinalIgnoreCase)))
                searchRoots.Add(root);
        }

        // Recursive search through unique roots
        foreach (var root in searchRoots)
        {
            try
            {
                logger?.LogDebug("[SDK Sync] Searching recursively in: {Root}", root);
                var found = Directory.GetFiles(root, SferaDllName, SearchOption.AllDirectories);
                if (found.Length > 0)
                {
                    var dir = Path.GetDirectoryName(found[0])!;
                    logger?.LogInformation("[SDK Sync] Found {Dll} by recursive search: {Path}", SferaDllName, dir);
                    return dir;
                }
            }
            catch (Exception ex)
            {
                logger?.LogDebug("[SDK Sync] Error searching {Root}: {Error}", root, ex.Message);
            }
        }

        // Log what we found for diagnostics
        foreach (var path in searchPaths)
        {
            if (!Directory.Exists(path)) continue;
            try
            {
                var entries = Directory.GetFileSystemEntries(path).Select(Path.GetFileName).Take(20);
                logger?.LogDebug("[SDK Sync] Contents of {Path}: {Entries}", path, string.Join(", ", entries));
            }
            catch { /* ignore */ }
        }

        return null;
    }

    private static string? TryGetFromRegistry(ILogger? logger)
    {
        try
        {
            var registryKeys = new[]
            {
                @"SOFTWARE\WOW6432Node\InsERT\nexo",
                @"SOFTWARE\InsERT\nexo"
            };

            foreach (var keyPath in registryKeys)
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath);
                if (key == null) continue;

                var installDir = key.GetValue("InstallDir") as string
                              ?? key.GetValue("InstalledPath") as string
                              ?? key.GetValue("Path") as string;

                if (!string.IsNullOrEmpty(installDir) && Directory.Exists(installDir))
                {
                    logger?.LogDebug("[SDK Sync] Found nexo via registry key {Key}: {Path}", keyPath, installDir);
                    return installDir;
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogDebug("[SDK Sync] Registry lookup failed: {Error}", ex.Message);
        }

        return null;
    }

    /// <summary>
    /// Finds the directory containing InsERT.Moria.Sfera.dll within the nexo installation.
    /// Checks the root directory first, then searches subdirectories up to 2 levels deep.
    /// </summary>
    private static string? FindDllDirectory(string installPath, ILogger? logger)
    {
        // Check root directory first
        if (File.Exists(Path.Combine(installPath, SferaDllName)))
            return installPath;

        // Search subdirectories — InsERT uses deployment folders per entity
        // (e.g., Deployments/Nexo/DMservice4b8a0986.../InsERT.Moria.Sfera.dll)
        try
        {
            var found = Directory.GetFiles(installPath, SferaDllName, SearchOption.AllDirectories);
            if (found.Length > 0)
            {
                if (found.Length == 1)
                {
                    var dir = Path.GetDirectoryName(found[0])!;
                    logger?.LogDebug("[SDK Sync] Found {Dll} in: {Dir}", SferaDllName, dir);
                    return dir;
                }

                // Multiple copies found — pick the one with the highest file version
                logger?.LogDebug("[SDK Sync] Found {Count} copies of {Dll}, selecting newest version...",
                    found.Length, SferaDllName);

                string? bestDir = null;
                Version? bestVersion = null;

                foreach (var dllPath in found)
                {
                    try
                    {
                        var info = FileVersionInfo.GetVersionInfo(dllPath);
                        var version = Version.TryParse(info.FileVersion, out var v) ? v : null;
                        var dir = Path.GetDirectoryName(dllPath)!;

                        logger?.LogDebug("[SDK Sync]   {Dir} -> version {Version}",
                            Path.GetFileName(Path.GetDirectoryName(dllPath)), info.FileVersion);

                        if (version != null && (bestVersion == null || version > bestVersion))
                        {
                            bestVersion = version;
                            bestDir = dir;
                        }
                    }
                    catch { /* skip unreadable */ }
                }

                if (bestDir != null)
                {
                    logger?.LogInformation("[SDK Sync] Selected deployment with version {Version}: {Dir}",
                        bestVersion, bestDir);
                    return bestDir;
                }

                // Fallback: return directory of first found
                return Path.GetDirectoryName(found[0])!;
            }
        }
        catch (Exception ex)
        {
            logger?.LogDebug("[SDK Sync] Error searching subdirectories: {Error}", ex.Message);
        }

        // Log what IS in the directory for diagnostics
        try
        {
            var subdirs = Directory.GetDirectories(installPath);
            logger?.LogDebug("[SDK Sync] Subdirectories in {Path}: {Dirs}",
                installPath, string.Join(", ", subdirs.Select(Path.GetFileName)));
        }
        catch { /* ignore */ }

        return null;
    }

    private static void CopyDlls(string sourceDir, string destDir, SdkSyncResult result, ILogger? logger)
    {
        var dllFiles = Directory.GetFiles(sourceDir, "*.dll");

        foreach (var sourceFile in dllFiles)
        {
            var fileName = Path.GetFileName(sourceFile);
            var destFile = Path.Combine(destDir, fileName);

            try
            {
                // Skip if identical (same size and last write time)
                if (File.Exists(destFile))
                {
                    var sourceInfo = new FileInfo(sourceFile);
                    var destInfo = new FileInfo(destFile);

                    if (sourceInfo.Length == destInfo.Length &&
                        sourceInfo.LastWriteTimeUtc == destInfo.LastWriteTimeUtc)
                    {
                        result.FilesSkipped++;
                        continue;
                    }
                }

                File.Copy(sourceFile, destFile, overwrite: true);
                result.FilesCopied++;
                logger?.LogDebug("[SDK Sync] Copied: {File}", fileName);
            }
            catch (IOException ex) when (ex.Message.Contains("being used by another process", StringComparison.OrdinalIgnoreCase))
            {
                result.FilesFailed++;
                result.Errors.Add($"Locked: {fileName} - {ex.Message}");
                logger?.LogWarning("[SDK Sync] Skipping locked file: {File}", fileName);
            }
            catch (Exception ex)
            {
                result.FilesFailed++;
                result.Errors.Add($"Failed: {fileName} - {ex.Message}");
                logger?.LogWarning(ex, "[SDK Sync] Failed to copy: {File}", fileName);
            }
        }
    }

    /// <summary>
    /// Performs a live version check comparing the loaded assembly version with the nexo installation.
    /// </summary>
    public static SdkSyncResult CheckVersions(string? configuredInstallPath, string runtimeDir)
    {
        var result = new SdkSyncResult();

        try
        {
            var installPath = ResolveInstallPath(configuredInstallPath, null);
            var dllDir = installPath != null ? FindDllDirectory(installPath, null) : null;
            result.DetectedInstallPath = dllDir ?? installPath;

            var localDllPath = Path.Combine(runtimeDir, SferaDllName);
            var installedDllPath = dllDir != null ? Path.Combine(dllDir, SferaDllName) : null;

            if (File.Exists(localDllPath))
                result.LocalVersion = FileVersionInfo.GetVersionInfo(localDllPath).FileVersion;

            if (installedDllPath != null && File.Exists(installedDllPath))
                result.InstalledVersion = FileVersionInfo.GetVersionInfo(installedDllPath).FileVersion;

            if (result.LocalVersion == null)
            {
                result.Status = SdkSyncStatus.Failed;
                result.Message = "Local SDK DLL not found.";
            }
            else if (result.InstalledVersion == null)
            {
                result.Status = SdkSyncStatus.Skipped;
                result.Message = "Nexo installation not found — cannot compare versions.";
            }
            else if (result.LocalVersion == result.InstalledVersion)
            {
                result.Status = SdkSyncStatus.AlreadyCurrent;
                result.Message = $"Versions match ({result.LocalVersion}). No restart needed.";
            }
            else
            {
                result.Status = SdkSyncStatus.NotRun;
                result.Message = $"Version mismatch: running {result.LocalVersion}, installed {result.InstalledVersion}. Restart recommended.";
            }
        }
        catch (Exception ex)
        {
            result.Status = SdkSyncStatus.Failed;
            result.Message = $"Version check failed: {ex.Message}";
            result.Errors.Add(ex.ToString());
        }

        return result;
    }
}
