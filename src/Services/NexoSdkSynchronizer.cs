using System.Diagnostics;
using System.IO.Compression;
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
    /// Source priority: client machine first (Deployments\Nexo\*\Binaries - always matches
    /// the client's database version - then Program Files), remote fallback zip last.
    /// </summary>
    public static SdkSyncResult Synchronize(string? configuredInstallPath, string runtimeDir, ILogger? logger,
        bool alsoSyncSource = false, string? sourceLibDir = null,
        bool preferDeploymentBinaries = true, string? fallbackUrl = null, string? fallbackToken = null)
    {
        var result = new SdkSyncResult();

        try
        {
            logger?.LogInformation("[SDK Sync] Starting SDK version check...");

            // Find the directory containing the newest SDK DLLs
            var dllDir = FindNewestSdkDirectory(configuredInstallPath, logger);
            if (dllDir == null)
            {
                // No nexo on this machine - if the build didn't ship the DLLs either,
                // try downloading the SDK package from the configured fallback URL.
                if (!File.Exists(Path.Combine(runtimeDir, SferaDllName)) && !string.IsNullOrEmpty(fallbackUrl))
                {
                    if (TryDownloadSdkPackage(fallbackUrl, fallbackToken, runtimeDir, result, logger))
                    {
                        result.Status = SdkSyncStatus.Synchronized;
                        result.Message = $"No local nexo found - downloaded SDK package from fallback URL ({result.FilesCopied} DLLs).";
                        logger?.LogInformation("[SDK Sync] {Message}", result.Message);
                        _lastSyncResult = result;
                        return result;
                    }
                }

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

            // Hash match means SAME VERSION, not "nothing to do": the build copies only the
            // ~60 referenced DLLs, while the SDK needs hundreds of dependencies at runtime
            // (e.g. InsERT.Moria.Narzedzia). On a fresh machine the version matches but most
            // dependencies are missing - the fill steps below must always run.
            var versionsMatch = false;

            if (File.Exists(localDllPath))
            {
                var localVersionInfo = FileVersionInfo.GetVersionInfo(localDllPath);
                result.LocalVersion = localVersionInfo.ProductVersion ?? localVersionInfo.FileVersion;

                // Compare by file hash since FileVersion is often 1.0.0.0 (obfuscated DLLs)
                var localHash = ComputeFileHash(localDllPath);
                var sourceHash = ComputeFileHash(sourceDllPath);

                logger?.LogInformation("[SDK Sync] Local: {Version} (hash: {Hash}), Source: {SrcVersion} (hash: {SrcHash})",
                    result.LocalVersion, localHash[..8], result.InstalledVersion, sourceHash[..8]);

                versionsMatch = localHash == sourceHash;
                if (versionsMatch)
                    logger?.LogInformation("[SDK Sync] Versions match - filling missing dependency DLLs only.");
                else
                    logger?.LogInformation("[SDK Sync] DLL hash mismatch — syncing...");
            }
            else
            {
                result.LocalVersion = null;
                logger?.LogWarning("[SDK Sync] Local {Dll} not found, will copy from deployment.", SferaDllName);
            }

            // Prefer parent "Binaries" directory if it has more DLLs (SelloConnector etc. are subsets)
            var parentDir = Path.GetDirectoryName(dllDir);
            if (parentDir != null &&
                Path.GetFileName(parentDir).Equals("Binaries", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(Path.Combine(parentDir, SferaDllName)))
            {
                var parentDllCount = Directory.GetFiles(parentDir, "*.dll").Length;
                var selectedDllCount = Directory.GetFiles(dllDir, "*.dll").Length;
                if (parentDllCount > selectedDllCount)
                {
                    logger?.LogInformation("[SDK Sync] Using parent Binaries dir ({ParentCount} DLLs) instead of {SubDir} ({SubCount} DLLs)",
                        parentDllCount, Path.GetFileName(dllDir), selectedDllCount);
                    dllDir = parentDir;
                }
            }

            // First try .zip-cache (official SDK packages, fully consistent versions)
            var zipCacheDir = FindMatchingZipCache(dllDir, logger);

            if (versionsMatch)
            {
                // Same version - just complete the dependency set, never overwrite.
                if (zipCacheDir != null)
                    FillMissingDlls(zipCacheDir, runtimeDir, result, logger);

                FillMissingDlls(dllDir, runtimeDir, result, logger);
            }
            else if (preferDeploymentBinaries)
            {
                // The client machine's deployment is the source of truth: its binaries always
                // match the database version. Overwrite differing build-shipped DLLs so the
                // whole set comes consistently from one deployment (mirrors how nexo itself runs).
                logger?.LogInformation("[SDK Sync] Deployment binaries preferred - syncing full set from deployment...");

                if (zipCacheDir != null)
                {
                    logger?.LogInformation("[SDK Sync] Primary source: .zip-cache {Dir}", zipCacheDir);
                    CopyDlls(zipCacheDir, runtimeDir, result, logger);
                }

                CopyDlls(dllDir, runtimeDir, result, logger);
            }
            else
            {
                // Conservative mode: never overwrite what the build shipped - only add missing DLLs.
                logger?.LogInformation("[SDK Sync] Filling missing DLLs from deployment (not overwriting existing)...");

                if (zipCacheDir != null)
                {
                    logger?.LogInformation("[SDK Sync] Primary fill source: .zip-cache {Dir}", zipCacheDir);
                    FillMissingDlls(zipCacheDir, runtimeDir, result, logger);
                }

                FillMissingDlls(dllDir, runtimeDir, result, logger);
            }

            // Also fill from related directories
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
            else if (versionsMatch && result.FilesCopied == 0)
            {
                result.Status = SdkSyncStatus.AlreadyCurrent;
                result.Message = "SDK is already up to date (hash match, no missing dependencies).";
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
    /// From multiple DLL paths, picks the best directory for SDK DLLs.
    /// Groups by same-size DLLs (same version), then prefers the directory with the most DLLs
    /// (complete deployment over subset like SelloConnector). Falls back to newest by write time.
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

        logger?.LogDebug("[SDK Sync] Found {Count} copies, selecting best by version and completeness...", dllPaths.Count);

        // Collect info about each candidate. The real SDK version comes from the DLL's file version -
        // size/write-time heuristics picked a stale 61.0.0 deployment over a 61.1.0 one (both had 625 DLLs).
        var candidates = new List<(string dllPath, string dir, Version version, long size, DateTime writeTime, int dllCount)>();
        foreach (var dllPath in dllPaths)
        {
            try
            {
                var info = new FileInfo(dllPath);
                var dir = Path.GetDirectoryName(dllPath)!;
                var dirName = Path.GetFileName(dir);
                var parentName = Path.GetFileName(Path.GetDirectoryName(dir) ?? "");
                var dllCount = Directory.GetFiles(dir, "*.dll").Length;
                var version = ReadDllVersion(dllPath);

                logger?.LogDebug("[SDK Sync]   {Parent}/{Dir}: version={Version}, size={Size}, modified={Time}, dllCount={Count}",
                    parentName, dirName, version, info.Length, info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"), dllCount);

                candidates.Add((dllPath, dir, version, info.Length, info.LastWriteTimeUtc, dllCount));
            }
            catch { /* skip unreadable */ }
        }

        if (candidates.Count == 0)
            return Path.GetDirectoryName(dllPaths[0])!;

        // Highest SDK version wins (must match the Nexo database version, which is always upgraded to the newest
        // deployment); among equal versions prefer the most complete directory, then the most recently written one.
        var best = candidates
            .OrderByDescending(c => c.version)
            .ThenByDescending(c => c.dllCount)
            .ThenByDescending(c => c.writeTime)
            .First();

        logger?.LogInformation("[SDK Sync] Selected deployment: {Dir} (version {Version}, modified {Time}, {Count} DLLs in dir)",
            best.dir, best.version, best.writeTime.ToString("yyyy-MM-dd HH:mm:ss"), best.dllCount);
        return best.dir;
    }

    /// <summary>
    /// Reads the numeric file version of a DLL (e.g. 61.1.0.9431); returns 0.0.0.0 when unavailable.
    /// </summary>
    private static Version ReadDllVersion(string dllPath)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(dllPath);
            var raw = info.FileVersion ?? info.ProductVersion ?? "";
            var numeric = new string(raw.TakeWhile(ch => char.IsDigit(ch) || ch == '.').ToArray());
            return Version.TryParse(numeric, out var v) ? v : new Version(0, 0, 0, 0);
        }
        catch
        {
            return new Version(0, 0, 0, 0);
        }
    }

    /// <summary>
    /// Downloads a zip archive with SDK DLLs (same package format as the CI NEXO_SDK_URL
    /// secret) and extracts it into the runtime directory. Used as a last resort when the
    /// machine has no nexo deployment/installation and the build did not ship the DLLs.
    /// </summary>
    private static bool TryDownloadSdkPackage(string url, string? bearerToken, string runtimeDir, SdkSyncResult result, ILogger? logger)
    {
        try
        {
            logger?.LogInformation("[SDK Sync] Downloading SDK package from fallback URL...");

            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            if (!string.IsNullOrEmpty(bearerToken))
            {
                http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
            }

            var zipPath = Path.Combine(Path.GetTempPath(), $"nexo-sdk-{Guid.NewGuid():N}.zip");
            try
            {
                using (var response = http.GetAsync(url).GetAwaiter().GetResult())
                {
                    response.EnsureSuccessStatusCode();
                    using var fileStream = File.Create(zipPath);
                    response.Content.CopyToAsync(fileStream).GetAwaiter().GetResult();
                }

                using var archive = ZipFile.OpenRead(zipPath);
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue; // directory entry

                    var destFile = Path.Combine(runtimeDir, entry.Name);
                    if (File.Exists(destFile)) { result.FilesSkipped++; continue; }

                    entry.ExtractToFile(destFile, overwrite: false);
                    result.FilesCopied++;
                }
            }
            finally
            {
                try { File.Delete(zipPath); } catch { /* best effort */ }
            }

            logger?.LogInformation("[SDK Sync] Fallback package extracted: {Copied} DLLs ({Skipped} already present).",
                result.FilesCopied, result.FilesSkipped);
            return File.Exists(Path.Combine(runtimeDir, SferaDllName));
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Fallback download failed: {ex.Message}");
            logger?.LogError(ex, "[SDK Sync] Failed to download SDK package from fallback URL");
            return false;
        }
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

    /// <summary>
    /// Finds a matching Moria version in the .zip-cache directory.
    /// The .zip-cache contains official SDK packages with fully consistent DLL versions.
    /// </summary>
    private static string? FindMatchingZipCache(string deploymentDir, ILogger? logger)
    {
        try
        {
            // Get SDK version from the Sfera DLL in the deployment
            var sferaDll = Path.Combine(deploymentDir, SferaDllName);
            if (!File.Exists(sferaDll)) return null;

            var versionInfo = FileVersionInfo.GetVersionInfo(sferaDll);
            var productVersion = versionInfo.ProductVersion;
            if (string.IsNullOrEmpty(productVersion)) return null;

            // Extract version number (e.g. "60.0.0.9195" from "60.0.0.9195+hash")
            var versionPart = productVersion.Split('+')[0];

            // Look for .zip-cache/Moria-{version} directory
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(localAppData)) return null;

            var zipCacheDir = Path.Combine(localAppData, "InsERT", "Deployments", ".zip-cache", $"Moria-{versionPart}");
            if (Directory.Exists(zipCacheDir))
            {
                var dllCount = Directory.GetFiles(zipCacheDir, "*.dll").Length;
                logger?.LogDebug("[SDK Sync] Found .zip-cache for Moria-{Version}: {Dir} ({Count} DLLs)",
                    versionPart, zipCacheDir, dllCount);
                if (dllCount > 100) // Only use if it has a substantial number of DLLs
                    return zipCacheDir;
            }
        }
        catch (Exception ex)
        {
            logger?.LogDebug("[SDK Sync] Error searching .zip-cache: {Error}", ex.Message);
        }

        return null;
    }

    /// <summary>
    /// Copies only DLLs that are missing in the destination directory.
    /// </summary>
    private static void FillMissingDlls(string sourceDir, string destDir, SdkSyncResult result, ILogger? logger)
    {
        try
        {
            var dllFiles = Directory.GetFiles(sourceDir, "*.dll");
            var filled = 0;
            foreach (var sourceFile in dllFiles)
            {
                var fileName = Path.GetFileName(sourceFile);
                var destFile = Path.Combine(destDir, fileName);
                if (File.Exists(destFile)) continue;
                try
                {
                    File.Copy(sourceFile, destFile, overwrite: false);
                    filled++;
                    result.FilesCopied++;
                }
                catch { /* skip */ }
            }
            if (filled > 0)
                logger?.LogInformation("[SDK Sync] Filled {Count} missing DLLs from deployment.", filled);
        }
        catch { /* ignore */ }
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
            // Build list of search roots: selected deployment, then all other known InsERT locations
            var searchRoots = new List<string>();

            // 1. Walk up from selected dir to deployment root
            var binaries = Path.GetDirectoryName(selectedDir);
            var deploymentDir = binaries != null ? Path.GetDirectoryName(binaries) : null;
            if (deploymentDir != null && Directory.Exists(deploymentDir))
                searchRoots.Add(deploymentDir);

            // 2. All Nexo deployments directory (sibling deployments)
            var deploymentsNexo = deploymentDir != null ? Path.GetDirectoryName(deploymentDir) : null;
            if (deploymentsNexo != null && Directory.Exists(deploymentsNexo))
            {
                try
                {
                    foreach (var siblingDeployment in Directory.GetDirectories(deploymentsNexo))
                    {
                        if (!string.Equals(siblingDeployment, deploymentDir, StringComparison.OrdinalIgnoreCase))
                            searchRoots.Add(siblingDeployment);
                    }
                }
                catch { /* ignore */ }
            }

            // 3. Program Files InsERT locations
            foreach (var pf in new[] { @"C:\Program Files (x86)\InsERT", @"C:\Program Files\InsERT" })
            {
                if (Directory.Exists(pf) && !searchRoots.Contains(pf))
                    searchRoots.Add(pf);
            }

            var copiedExtra = 0;
            foreach (var searchRoot in searchRoots)
            {
                string[] allDlls;
                try { allDlls = Directory.GetFiles(searchRoot, "*.dll", SearchOption.AllDirectories); }
                catch { continue; }

                logger?.LogDebug("[SDK Sync] Searching for missing DLLs in: {Root} ({Count} DLLs found)", searchRoot, allDlls.Length);

                foreach (var sourceFile in allDlls)
                {
                    if (sourceFile.StartsWith(selectedDir, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var fileName = Path.GetFileName(sourceFile);
                    var destFile = Path.Combine(destDir, fileName);

                    if (File.Exists(destFile)) continue;

                    try
                    {
                        File.Copy(sourceFile, destFile, overwrite: false);
                        copiedExtra++;
                        result.FilesCopied++;
                        logger?.LogDebug("[SDK Sync] Copied (from related dir): {File} from {Source}", fileName, Path.GetDirectoryName(sourceFile));
                    }
                    catch { /* skip if locked or failed */ }
                }
            }

            if (copiedExtra > 0)
                logger?.LogInformation("[SDK Sync] Copied {Count} additional DLLs from related directories.", copiedExtra);
            else
                logger?.LogDebug("[SDK Sync] No additional missing DLLs found in {Count} search roots.", searchRoots.Count);
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
