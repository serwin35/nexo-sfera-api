# Sync the Nexo SDK version string across the repo to a single source of truth.
#
# Source of truth: the newest docs\nexoSDK_<version>\ folder (or -Version override).
# Rewrites the version everywhere it is hard-coded:
#   - README.md            (badge "nexo X.Y.Z.B" + docs\nexoSDK_X.Y.Z.B path)
#   - scripts\setup-sdk.ps1 (default -SdkSourcePath + comments/hints)
#   - .github\workflows\build.yml, release.yml (local-SDK fallback paths)
#
# Usage:
#   .\scripts\sync-sdk-version.ps1            # detect from docs\nexoSDK_* and apply
#   .\scripts\sync-sdk-version.ps1 -DryRun    # show what would change
#   .\scripts\sync-sdk-version.ps1 -Version 61.0.0.9362
#   .\scripts\sync-sdk-version.ps1 -Tag       # also create annotated tag sdk-<version>

param(
    [string]$Version,
    [switch]$DryRun,
    [switch]$Tag
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir   = Split-Path -Parent $scriptDir

# 1. Resolve the target version (explicit param wins, else newest docs\nexoSDK_* folder)
if (-not $Version) {
    $docsDir = Join-Path $rootDir "docs"
    $sdkFolder = Get-ChildItem -Path $docsDir -Directory -Filter "nexoSDK_*" -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending | Select-Object -First 1
    if (-not $sdkFolder) {
        Write-Error "No docs\nexoSDK_* folder found and no -Version given."
        exit 1
    }
    $Version = $sdkFolder.Name -replace '^nexoSDK_', ''
}

if ($Version -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    Write-Error "Version '$Version' is not in expected form X.Y.Z.BUILD."
    exit 1
}

Write-Host "Target SDK version: $Version" -ForegroundColor Cyan

# 2. Files and the version-bearing patterns to rewrite within them
$verPattern = '\d+\.\d+\.\d+\.\d+'
$targets = @(
    @{ Path = "README.md"; Patterns = @(
        @{ Find = "nexoSDK_$verPattern"; Replace = "nexoSDK_$Version" }
        @{ Find = "nexo $verPattern";    Replace = "nexo $Version" }
    )}
    @{ Path = "scripts\setup-sdk.ps1"; Patterns = @(
        @{ Find = "nexoSDK_$verPattern"; Replace = "nexoSDK_$Version" }
    )}
    @{ Path = ".github\workflows\build.yml"; Patterns = @(
        @{ Find = "nexoSDK_$verPattern"; Replace = "nexoSDK_$Version" }
    )}
    @{ Path = ".github\workflows\release.yml"; Patterns = @(
        @{ Find = "nexoSDK_$verPattern"; Replace = "nexoSDK_$Version" }
    )}
)

$totalChanged = 0

foreach ($target in $targets) {
    $filePath = Join-Path $rootDir $target.Path
    if (-not (Test-Path $filePath)) {
        Write-Host "  Skip (not found): $($target.Path)" -ForegroundColor Yellow
        continue
    }

    $content = Get-Content -Path $filePath -Raw
    $original = $content

    foreach ($p in $target.Patterns) {
        $content = [regex]::Replace($content, $p.Find, $p.Replace)
    }

    if ($content -ne $original) {
        $totalChanged++
        if ($DryRun) {
            Write-Host "  Would update: $($target.Path)" -ForegroundColor Gray
        } else {
            Set-Content -Path $filePath -Value $content -NoNewline
            Write-Host "  Updated: $($target.Path)" -ForegroundColor Green
        }
    } else {
        Write-Host "  Already current: $($target.Path)" -ForegroundColor DarkGray
    }
}

Write-Host ""
Write-Host "Files changed: $totalChanged" -ForegroundColor Cyan

# 3. Optional bookmark tag (sdk-<version>) - see memory sdk-version-tags
if ($Tag) {
    $tagName = "sdk-$Version"
    if (git tag --list $tagName) {
        Write-Host "Tag $tagName already exists - skipping." -ForegroundColor Yellow
    } elseif ($DryRun) {
        Write-Host "Would create annotated tag: $tagName" -ForegroundColor Gray
    } else {
        git tag -a $tagName -m "Codebase aligned with InsERT Nexo SDK $Version"
        Write-Host "Created tag: $tagName (push with: git push origin $tagName)" -ForegroundColor Green
    }
}

if ($DryRun) {
    Write-Host ""
    Write-Host "Dry run only - no files were modified." -ForegroundColor Yellow
}
