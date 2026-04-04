using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
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

            // Find the directory containing the newest SDK DLLs
            var dllDir = FindNewestSdkDirectory(configuredInstallPath, logger);
            if (dllDir == null)
            {
                result.Status = SdkSyncStatus.Skipped;
                result.Message = "Nexo SDK DLLs not found on this system. SDK sync skipped.";
                logger?.LogWarning("[SDK Sync] {Message}", result.Message);
                _lastSyncResult = result;
                return result;
            }

            result.DetectedInstallPath = dllDir;
            logger?.LogInformation("[SDK Sync] SDK DLLs source: {Path}", dllDir);

            // Compare the Sfera DLL between local (runtime) and source (deployment)
            var localDllPath = Path.Combine(runtimeDir, SferaDllName);
            var sourceDllPath = Path.Combine(dllDir, SferaDllName);

            // Get version info for reporting (may be 1.0.0.0 due to obfuscation)
            var sourceVersionInfo = FileVersionInfo.GetVersionInfo(sourceDllPath);
            result.InstalledVersion = sourceVersionInfo.ProductVersion ?? sourceVersionInfo.FileVersion;

            if (File.Exists(localDllPath))
            {
                var localVersionInfo = FileVersionInfo.GetVersionInfo(localDllPath);
                result.LocalVersion = localVersionInfo.ProductVersion ?? localVersionInfo.FileVersion;

                // Compare by file hash since FileVersion is often 1.0.0.0 (obfuscated DLLs)
                var localHash = ComputeFileHash(localDllPath);
                var sourceHash = ComputeFileHash(sourceDllPath);

                logger?.LogInformation("[SDK Sync] Local: {Version} (hash: {Hash}), Source: {SrcVersion} (hash: {SrcHash})",
                    result.LocalVersion, localHash[..8], result.InstalledVersion, sourceHash[..8]);

                if (localHash == sourceHash)
                {
                    result.Status = SdkSyncStatus.AlreadyCurrent;
                    result.Message = $"SDK is already up to date (hash match).";
                    logger?.LogInformation("[SDK Sync] {Message}", result.Message);
                    _lastSyncResult = result;
                    return result;
                }

                logger?.LogInformation("[SDK Sync] DLL hash mismatch — syncing...");
            }
            else
            {
                result.LocalVersion = null;
                logger?.LogWarning("[SDK Sync] Local {Dll} not found, will copy from deployment.", SferaDllName);
            }

            // Copy DLLs
            logger?.LogInformation("[SDK Sync] Syncing DLLs from {Source} to {Dest}...", dllDir, runtimeDir);
            CopyDlls(dllDir, runtimeDir, result, logger);

            // Also copy from parent/sibling directories to catch DLLs not in the selected subdirectory
            CopyMissingFromRelatedDirs(dllDir, runtimeDir, result, logger);

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
                result.Message = $"Synced {result.FilesCopied} DLLs from deployment. {result.FilesSkipped} unchanged.";
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
    /// Finds the deployment directory containing the newest InsERT.Moria.Sfera.dll.
    /// Searches configured path, env vars, registry, Program Files, and AppData deployments.
    /// When multiple deployment folders exist, picks the one with the newest DLL.
    /// </summary>
    private static string? FindNewestSdkDirectory(string? configuredPath, ILogger? logger)
    {
        // 1. Explicit configuration — points directly to a directory with DLLs
        if (!string.IsNullOrEmpty(configuredPath) && Directory.Exists(configuredPath))
        {
            if (File.Exists(Path.Combine(configuredPath, SferaDllName)))
            {
                logger?.LogDebug("[SDK Sync] Using configured path: {Path}", configuredPath);
                return configuredPath;
            }
            // Search subdirectories of configured path
            var found = FindAllSferaDlls(configuredPath, logger);
            if (found.Count > 0)
                return PickNewest(found, logger);
        }

        // 2. Environment variable
        var envPath = Environment.GetEnvironmentVariable("NEXO_INSTALL_PATH");
        if (!string.IsNullOrEmpty(envPath) && Directory.Exists(envPath))
        {
            if (File.Exists(Path.Combine(envPath, SferaDllName)))
                return envPath;
            var found = FindAllSferaDlls(envPath, logger);
            if (found.Count > 0)
                return PickNewest(found, logger);
        }

        // 3. Windows Registry
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var regPath = TryGetFromRegistry(logger);
            if (regPath != null)
            {
                if (File.Exists(Path.Combine(regPath, SferaDllName)))
                    return regPath;
                var found = FindAllSferaDlls(regPath, logger);
                if (found.Count > 0)
                    return PickNewest(found, logger);
            }
        }

        // 4. Search known locations
        var searchRoots = new List<string>();

        // Program Files locations
        foreach (var pf in new[] { @"C:\Program Files (x86)\InsERT", @"C:\Program Files\InsERT" })
        {
            if (Directory.Exists(pf))
                searchRoots.Add(pf);
        }

        // AppData\Local — InsERT Deployments (primary location for updated DLLs)
        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData))
            {
                var deploymentsNexo = Path.Combine(localAppData, "InsERT", "Deployments", "Nexo");
                if (Directory.Exists(deploymentsNexo))
                    searchRoots.Insert(0, deploymentsNexo); // Prioritize deployments
                else
                {
                    var insertLocal = Path.Combine(localAppData, "InsERT");
                    if (Directory.Exists(insertLocal))
                        searchRoots.Insert(0, insertLocal);
                }
            }

            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (!string.IsNullOrEmpty(programData))
            {
                var insertPD = Path.Combine(programData, "InsERT");
                if (Directory.Exists(insertPD))
                    searchRoots.Add(insertPD);
            }
        }
        catch { /* ignore */ }

        // Collect ALL found DLLs across all search roots
        var allFound = new List<string>();
        foreach (var root in searchRoots)
        {
            logger?.LogDebug("[SDK Sync] Searching: {Root}", root);
            var found = FindAllSferaDlls(root, logger);
            allFound.AddRange(found);
        }

        if (allFound.Count > 0)
            return PickNewest(allFound, logger);

        logger?.LogWarning("[SDK Sync] {Dll} not found in any known location.", SferaDllName);
        return null;
    }

    /// <summary>
    /// Finds all InsERT.Moria.Sfera.dll files under a root directory.
    /// </summary>
    private static List<string> FindAllSferaDlls(string root, ILogger? logger)
    {
        try
        {
            return Directory.GetFiles(root, SferaDllName, SearchOption.AllDirectories).ToList();
        }
        catch (Exception ex)
        {
            logger?.LogDebug("[SDK Sync] Error searching {Root}: {Error}", root, ex.Message);
            return new List<string>();
        }
    }

    /// <summary>
    /// From multiple DLL paths, picks the directory containing the newest version.
    /// Uses last write time since InsERT DLLs have obfuscated FileVersion (1.0.0.0).
    /// </summary>
    private static string? PickNewest(List<string> dllPaths, ILogger? logger)
    {
        if (dllPaths.Count == 0) return null;
        if (dllPaths.Count == 1)
        {
            var dir = Path.GetDirectoryName(dllPaths[0])!;
            logger?.LogDebug("[SDK Sync] Single DLL found in: {Dir}", dir);
            return dir;
        }

        logger?.LogDebug("[SDK Sync] Found {Count} copies, selecting newest by write time...", dllPaths.Count);

        string? bestPath = null;
        DateTime bestTime = DateTime.MinValue;

        foreach (var dllPath in dllPaths)
        {
            try
            {
                var info = new FileInfo(dllPath);
                var dir = Path.GetDirectoryName(dllPath)!;
                var dirName = Path.GetFileName(dir);
                var parentName = Path.GetFileName(Path.GetDirectoryName(dir) ?? "");

                logger?.LogDebug("[SDK Sync]   {Parent}/{Dir}: size={Size}, modified={Time}",
                    parentName, dirName, info.Length, info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));

                if (info.LastWriteTimeUtc > bestTime)
                {
                    bestTime = info.LastWriteTimeUtc;
                    bestPath = dllPath;
                }
            }
            catch { /* skip unreadable */ }
        }

        if (bestPath != null)
        {
            var result = Path.GetDirectoryName(bestPath)!;
            logger?.LogInformation("[SDK Sync] Selected newest deployment: {Dir} (modified {Time})",
                result, bestTime.ToString("yyyy-MM-dd HH:mm:ss"));
            return result;
        }

        return Path.GetDirectoryName(dllPaths[0])!;
    }

    private static string ComputeFileHash(string filePath)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
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
    /// Searches parent and sibling directories of the selected SDK directory for DLLs
    /// that are missing in the destination. This handles cases where DLLs like
    /// InsERT.WebServices.Client.Documents.dll exist in a different subdirectory
    /// of the deployment than the one containing InsERT.Moria.Sfera.dll.
    /// </summary>
    private static void CopyMissingFromRelatedDirs(string selectedDir, string destDir, SdkSyncResult result, ILogger? logger)
    {
        try
        {
            var parentDir = Path.GetDirectoryName(selectedDir);
            if (parentDir == null || !Directory.Exists(parentDir)) return;

            // Search parent + all sibling directories for *.dll files
            var searchDirs = new List<string> { parentDir };
            try
            {
                searchDirs.AddRange(Directory.GetDirectories(parentDir));
            }
            catch { /* ignore permission errors */ }

            var copiedExtra = 0;
            foreach (var searchDir in searchDirs)
            {
                if (string.Equals(searchDir, selectedDir, StringComparison.OrdinalIgnoreCase))
                    continue;

                string[] dllFiles;
                try { dllFiles = Directory.GetFiles(searchDir, "*.dll"); }
                catch { continue; }

                foreach (var sourceFile in dllFiles)
                {
                    var fileName = Path.GetFileName(sourceFile);
                    var destFile = Path.Combine(destDir, fileName);

                    // Only copy if missing in destination
                    if (File.Exists(destFile)) continue;

                    try
                    {
                        File.Copy(sourceFile, destFile, overwrite: false);
                        copiedExtra++;
                        result.FilesCopied++;
                        logger?.LogDebug("[SDK Sync] Copied (from related dir): {File}", fileName);
                    }
                    catch { /* skip if locked or failed */ }
                }
            }

            if (copiedExtra > 0)
                logger?.LogInformation("[SDK Sync] Copied {Count} additional DLLs from related directories.", copiedExtra);
        }
        catch (Exception ex)
        {
            logger?.LogDebug("[SDK Sync] Error searching related directories: {Error}", ex.Message);
        }
    }

    /// <summary>
    /// Performs a live version check comparing current SDK DLLs with the deployment.
    /// </summary>
    public static SdkSyncResult CheckVersions(string? configuredInstallPath, string runtimeDir)
    {
        var result = new SdkSyncResult();

        try
        {
            var dllDir = FindNewestSdkDirectory(configuredInstallPath, null);
            result.DetectedInstallPath = dllDir;

            var localDllPath = Path.Combine(runtimeDir, SferaDllName);
            var sourceDllPath = dllDir != null ? Path.Combine(dllDir, SferaDllName) : null;

            if (File.Exists(localDllPath))
            {
                var info = FileVersionInfo.GetVersionInfo(localDllPath);
                result.LocalVersion = info.ProductVersion ?? info.FileVersion;
            }

            if (sourceDllPath != null && File.Exists(sourceDllPath))
            {
                var info = FileVersionInfo.GetVersionInfo(sourceDllPath);
                result.InstalledVersion = info.ProductVersion ?? info.FileVersion;
            }

            if (result.LocalVersion == null)
            {
                result.Status = SdkSyncStatus.Failed;
                result.Message = "Local SDK DLL not found.";
            }
            else if (result.InstalledVersion == null)
            {
                result.Status = SdkSyncStatus.Skipped;
                result.Message = "Nexo deployment not found — cannot compare.";
            }
            else
            {
                // Compare by hash since FileVersion is often 1.0.0.0
                var localHash = ComputeFileHash(localDllPath);
                var sourceHash = ComputeFileHash(sourceDllPath!);

                if (localHash == sourceHash)
                {
                    result.Status = SdkSyncStatus.AlreadyCurrent;
                    result.Message = $"DLLs match (hash identical). No restart needed.";
                }
                else
                {
                    result.Status = SdkSyncStatus.NotRun;
                    result.Message = $"DLL mismatch detected. Restart recommended to sync with deployment.";
                }
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
