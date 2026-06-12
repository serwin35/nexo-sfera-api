# Setup InsERT Nexo SDK for build
# Copies required SDK DLLs from a full SDK installation to the lib folder.
#
# Default source: docs\nexoSDK_60.1.1.9292\Bin (latest SDK shipped with this repo)
# Default target: src\lib\nexo-sdk (matches NexoSdkPath in NexoSferaApi.csproj)
#
# Usage:
#   .\scripts\setup-sdk.ps1                       # use defaults (60.1.1)
#   .\scripts\setup-sdk.ps1 -SdkSourcePath "C:\Other\Bin"
#   .\scripts\setup-sdk.ps1 -SyncRootLib          # also sync legacy root .\lib\nexo-sdk
#   .\scripts\setup-sdk.ps1 -DryRun               # show what would happen without copying

param(
    [string]$SdkSourcePath = "docs\nexoSDK_60.1.1.9292\Bin",
    [string]$TargetPath    = "src\lib\nexo-sdk",
    [switch]$SyncRootLib,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# DLLs explicitly referenced by NexoSferaApi.csproj (must exist in SDK source).
# Keep this list in sync with <Reference Include="..."> entries in the csproj.
$requiredDlls = @(
    # --- Core SDK / runtime ---
    "InsERT.Moria.API.dll",
    "InsERT.Moria.Sfera.dll",
    "InsERT.Moria.ModelDanych.dll",
    "InsERT.Mox.Core.dll",
    "InsERT.Common.Product.dll",
    "InsERT.Mox.EntityFrameworkSupport.dll",
    "InsERT.Mox.EntityFramework.Core.dll",
    "InsERT.Mox.EntityFramework.Ms.SqlServer.dll",
    "InsERT.Moria.DaneDomyslne.dll",
    "InsERT.Moria.Security.Core.dll",
    "InsERT.Moria.Security.dll",
    "InsERT.Mox.Security.Sql.dll",

    # --- Sales / inventory / logistics ---
    "InsERT.Moria.Asortymenty.dll",
    "InsERT.Moria.Klienci.dll",
    "InsERT.Moria.KlienciBiura.dll",
    "InsERT.Moria.ModelOrganizacyjny.dll",
    "InsERT.Moria.Promocje.dll",
    "InsERT.Moria.Logistyka.dll",
    "InsERT.Moria.Inwentaryzacja.dll",
    "InsERT.Moria.Remanenty.dll",
    "InsERT.Moria.Slowniki.dll",
    "InsERT.Moria.Cenniki.dll",
    "InsERT.Moria.Waluty.dll",
    "InsERT.Moria.Naklejki.dll",

    # --- Documents & e-invoicing ---
    "InsERT.Moria.DokumentyDoKsiegowania.dll",
    "InsERT.Moria.DokumentyDoKsiegowania.Wspomaganie.dll",
    "InsERT.Moria.EwidencjaVAT.dll",
    "InsERT.Moria.DowodyWewnetrzne.dll",

    # --- Finance ---
    "InsERT.Moria.Finanse.dll",
    "InsERT.Moria.Kasa.dll",
    "InsERT.Moria.Bank.dll",
    "InsERT.Moria.Rozrachunki.dll",

    # --- Accounting / fiscal ---
    "InsERT.Moria.Ksiegowosc.dll",
    "InsERT.Moria.ImportKsiegowy.dll",
    "InsERT.Moria.ProcesyKsiegowoKadrowe.dll",
    "InsERT.Moria.Deklaracje.dll",
    "InsERT.Moria.KontrolaSkarbowa.dll",
    "InsERT.Moria.SprawozdaniaFinansowe.dll",
    "InsERT.Moria.Raporty.dll",

    # --- HR & payroll ---
    "InsERT.Moria.Kadry.dll",
    "InsERT.Moria.Place.dll",
    "InsERT.Moria.PPK.dll",

    # --- Assets & fleet ---
    "InsERT.Moria.SrodkiTrwale.dll",
    "InsERT.Moria.Pojazdy.dll",

    # --- Cross-cutting / utilities ---
    "InsERT.Moria.Wspolne.dll",
    "InsERT.Moria.SladRewizyjny.dll",
    "InsERT.Moria.Komentarze.dll",
    "InsERT.Moria.BibliotekaZalacznikow.dll",
    "InsERT.Moria.Parametry.dll",
    "InsERT.Moria.Uprawnienia.dll",
    "InsERT.Moria.PolaWlasne2.dll",
    "InsERT.Moria.GaleriaZdjec.dll",
    "InsERT.Moria.Archiwa.dll",
    "InsERT.Moria.Procesy.dll",
    "InsERT.Moria.Dzialania.dll",
    "InsERT.Moria.Kalendarze.dll",
    "InsERT.Moria.Komunikacja.dll",
    "InsERT.Moria.OperacjeZewnetrzne.dll",
    "InsERT.Moria.Urzadzenia.Core.dll",

    # --- E-commerce / shipping ---
    "InsERT.Moria.HandelElektroniczny.dll",
    "InsERT.Moria.Kurierzy.dll",
    "InsERT.Moria.Wydruki.dll"
)

# Note: some namespaces do NOT exist as standalone assemblies in the SDK Bin folder -
# they are exposed through InsERT.Moria.API.dll and must NOT be listed above:
# InsERT.Moria.Dokumenty, InsERT.Moria.CennikiICeny, InsERT.Moria.Intrastat.

function Sync-SdkFolder {
    param(
        [string]$Source,
        [string]$Target,
        [string]$Label,
        [string[]]$Dlls,
        [switch]$DryRun
    )

    Write-Host ""
    Write-Host "Target: $Label" -ForegroundColor Cyan
    Write-Host "  -> $Target"

    if (-not (Test-Path $Target)) {
        if ($DryRun) {
            Write-Host "  Would create directory: $Target" -ForegroundColor Yellow
        } else {
            New-Item -ItemType Directory -Path $Target -Force | Out-Null
            Write-Host "  Created directory: $Target" -ForegroundColor Green
        }
    }

    $copied = 0
    $missing = @()

    foreach ($dll in $Dlls) {
        $sourceFile = Join-Path $Source $dll
        $targetFile = Join-Path $Target $dll

        if (Test-Path $sourceFile) {
            if ($DryRun) {
                Write-Host "  Would copy: $dll" -ForegroundColor Gray
            } else {
                Copy-Item $sourceFile $targetFile -Force
                Write-Host "  Copied: $dll" -ForegroundColor Green
            }
            $copied++
        } else {
            $missing += $dll
            Write-Host "  Missing: $dll" -ForegroundColor Yellow
        }
    }

    Write-Host "  Summary: $copied copied, $($missing.Count) missing"
    return [pscustomobject]@{ Copied = $copied; Missing = $missing }
}

Write-Host "InsERT Nexo SDK Setup Script" -ForegroundColor Cyan
Write-Host "=============================" -ForegroundColor Cyan

# Resolve paths relative to repo root (script lives in .\scripts)
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir   = Split-Path -Parent $scriptDir
$sourcePath = if ([System.IO.Path]::IsPathRooted($SdkSourcePath)) { $SdkSourcePath } else { Join-Path $rootDir $SdkSourcePath }
$targetPath = if ([System.IO.Path]::IsPathRooted($TargetPath))    { $TargetPath }    else { Join-Path $rootDir $TargetPath }

# The default source (docs\nexoSDK_*\Bin) is gitignored - it exists only on machines where
# the SDK documentation package was extracted. On a fresh clone, fall back to other SDK
# locations: any docs\nexoSDK_* folder, then the installed nexo application.
if (-not $PSBoundParameters.ContainsKey('SdkSourcePath') -and -not (Test-Path $sourcePath)) {
    $fallbacks = @()

    $docsSdk = Get-ChildItem -Path (Join-Path $rootDir "docs") -Directory -Filter "nexoSDK_*" -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending | Select-Object -First 1
    if ($docsSdk) { $fallbacks += (Join-Path $docsSdk.FullName "Bin") }

    # nexo deployment binaries (always match the version the client machine actually runs)
    $deployments = Get-ChildItem -Path "$env:LOCALAPPDATA\InsERT\Deployments\Nexo" -Directory -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending
    foreach ($d in $deployments) { $fallbacks += (Join-Path $d.FullName "Binaries") }

    $fallbacks += "${env:ProgramFiles(x86)}\InsERT\nexo"
    $fallbacks += "$env:ProgramFiles\InsERT\nexo"

    foreach ($candidate in $fallbacks) {
        if ($candidate -and (Test-Path (Join-Path $candidate "InsERT.Moria.API.dll"))) {
            Write-Host "Default SDK source not found - using fallback: $candidate" -ForegroundColor Yellow
            $sourcePath = $candidate
            break
        }
    }
}

Write-Host "Source: $sourcePath"

if (-not (Test-Path $sourcePath)) {
    Write-Error "SDK source path not found: $sourcePath"
    Write-Host ""
    Write-Host "Hint: pass -SdkSourcePath '<path>' pointing at the SDK Bin folder or the nexo install dir," -ForegroundColor Yellow
    Write-Host "e.g. -SdkSourcePath 'C:\Program Files (x86)\InsERT\nexo'" -ForegroundColor Yellow
    Write-Host "or extract the SDK documentation package to docs\nexoSDK_60.1.1.9292\." -ForegroundColor Yellow
    exit 1
}

# Detect SDK version from InsERT.Moria.API.dll for confirmation
$apiDll = Join-Path $sourcePath "InsERT.Moria.API.dll"
if (Test-Path $apiDll) {
    try {
        $ver = (Get-Item $apiDll).VersionInfo.FileVersion
        Write-Host "Detected SDK build: InsERT.Moria.API.dll v$ver" -ForegroundColor Cyan
    } catch {
        Write-Host "Could not read SDK version metadata." -ForegroundColor Yellow
    }
}

$results = @()
$results += Sync-SdkFolder -Source $sourcePath -Target $targetPath -Label "Build target (csproj NexoSdkPath)" -Dlls $requiredDlls -DryRun:$DryRun

if ($SyncRootLib) {
    $rootLibPath = Join-Path $rootDir "lib\nexo-sdk"
    $results += Sync-SdkFolder -Source $sourcePath -Target $rootLibPath -Label "Legacy root lib\nexo-sdk" -Dlls $requiredDlls -DryRun:$DryRun
}

# Second pass: DLLs missing in the chosen source are searched for in ALL other known
# SDK locations (other deployments, .zip-cache, Program Files, docs SDK package).
# Deployments contain only the modules licensed/used by that client - e.g. a deployment
# without Intrastat lacks InsERT.Moria.Intrastat.dll even though the build requires it.
$stillMissing = @($results | ForEach-Object { $_.Missing } | Select-Object -Unique)
if ($stillMissing.Count -gt 0) {
    Write-Host ""
    Write-Host "Searching other SDK locations for $($stillMissing.Count) missing DLL(s)..." -ForegroundColor Cyan

    $extraRoots = @()
    $docsDir = Join-Path $rootDir "docs"
    Get-ChildItem -Path $docsDir -Directory -Filter "nexoSDK_*" -ErrorAction SilentlyContinue |
        ForEach-Object { $extraRoots += (Join-Path $_.FullName "Bin") }
    $extraRoots += "$env:LOCALAPPDATA\InsERT\Deployments"   # all deployments + .zip-cache
    $extraRoots += "${env:ProgramFiles(x86)}\InsERT"
    $extraRoots += "$env:ProgramFiles\InsERT"

    foreach ($dll in $stillMissing) {
        $found = $null
        foreach ($root in $extraRoots) {
            if (-not (Test-Path $root)) { continue }
            $found = Get-ChildItem -Path $root -Recurse -Filter $dll -ErrorAction SilentlyContinue |
                Sort-Object LastWriteTime -Descending | Select-Object -First 1
            if ($found) { break }
        }

        if ($found) {
            if ($DryRun) {
                Write-Host "  Would copy: $dll  <- $($found.DirectoryName)" -ForegroundColor Gray
            } else {
                Copy-Item $found.FullName (Join-Path $targetPath $dll) -Force
                Write-Host "  Recovered: $dll  <- $($found.DirectoryName)" -ForegroundColor Green
            }
            $stillMissing = @($stillMissing | Where-Object { $_ -ne $dll })
        } else {
            Write-Host "  Not found anywhere: $dll" -ForegroundColor Red
        }
    }
}

Write-Host ""
Write-Host "Done." -ForegroundColor Green
if ($stillMissing.Count -gt 0) {
    Write-Host ""
    Write-Host "Still missing after searching all locations: $($stillMissing -join ', ')" -ForegroundColor Red
    Write-Host "These DLLs are REQUIRED by the build (referenced in NexoSferaApi.csproj)." -ForegroundColor Yellow
    Write-Host "Copy them from the SDK documentation package (docs\nexoSDK_*\Bin) on a machine that has it." -ForegroundColor Yellow
}
if ($DryRun) {
    Write-Host ""
    Write-Host "Dry run only - no files were modified." -ForegroundColor Yellow
}
