# Setup InsERT Nexo SDK for build
# This script copies required SDK DLLs from the full SDK installation to the lib folder

param(
    [string]$SdkSourcePath = "..\nexoSDK_58.0.2.8985\Bin",
    [string]$TargetPath = "src\lib\nexo-sdk"
)

$ErrorActionPreference = "Stop"

# List of required DLLs (only those referenced in csproj)
$requiredDlls = @(
    "InsERT.Moria.API.dll",
    "InsERT.Moria.Sfera.dll",
    "InsERT.Moria.ModelDanych.dll",
    "InsERT.Moria.Asortymenty.dll",
    "InsERT.Moria.Klienci.dll",
    "InsERT.Moria.ModelOrganizacyjny.dll",
    "InsERT.Moria.Promocje.dll",
    "InsERT.Moria.Logistyka.dll",
    "InsERT.Moria.Finanse.dll",
    "InsERT.Moria.Kasa.dll",
    "InsERT.Moria.Bank.dll",
    "InsERT.Moria.Rozrachunki.dll",
    "InsERT.Mox.Core.dll",
    "InsERT.Common.Product.dll",
    "InsERT.Mox.EntityFrameworkSupport.dll",
    "InsERT.Moria.DaneDomyslne.dll",
    "InsERT.Mox.EntityFramework.Core.dll",
    "InsERT.Moria.Security.Core.dll",
    "InsERT.Moria.Security.dll",
    "InsERT.Mox.Security.Sql.dll",
    # Additional dependencies that may be needed at runtime
    "InsERT.Moria.Dokumenty.dll",
    "InsERT.Moria.Slowniki.dll",
    "InsERT.Moria.CennikiICeny.dll",
    "InsERT.Moria.Inwentaryzacja.dll",
    "InsERT.Moria.Deklaracje.dll",
    "InsERT.Moria.Intrastat.dll",
    "InsERT.Moria.KontrolaSkarbowa.dll",
    "InsERT.Moria.Raporty.dll",
    "InsERT.Moria.Wydruki.dll",
    "InsERT.Moria.HandelElektroniczny.dll",
    "InsERT.Moria.Naklejki.dll"
)

Write-Host "InsERT Nexo SDK Setup Script" -ForegroundColor Cyan
Write-Host "=============================" -ForegroundColor Cyan

# Resolve paths
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir
$sourcePath = Join-Path $rootDir $SdkSourcePath
$targetPath = Join-Path $rootDir $TargetPath

Write-Host "Source: $sourcePath"
Write-Host "Target: $targetPath"

# Check if source exists
if (-not (Test-Path $sourcePath)) {
    Write-Error "SDK source path not found: $sourcePath"
    Write-Host ""
    Write-Host "Please ensure the Nexo SDK is extracted to: $SdkSourcePath" -ForegroundColor Yellow
    Write-Host "Download from InsERT partner portal or use your licensed SDK." -ForegroundColor Yellow
    exit 1
}

# Create target directory
if (-not (Test-Path $targetPath)) {
    New-Item -ItemType Directory -Path $targetPath -Force | Out-Null
    Write-Host "Created target directory: $targetPath" -ForegroundColor Green
}

# Copy DLLs
$copied = 0
$missing = @()

foreach ($dll in $requiredDlls) {
    $sourceFile = Join-Path $sourcePath $dll
    $targetFile = Join-Path $targetPath $dll

    if (Test-Path $sourceFile) {
        Copy-Item $sourceFile $targetFile -Force
        Write-Host "  Copied: $dll" -ForegroundColor Green
        $copied++
    } else {
        $missing += $dll
        Write-Host "  Missing: $dll" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "  Copied: $copied DLLs"
Write-Host "  Missing: $($missing.Count) DLLs"

if ($missing.Count -gt 0) {
    Write-Host ""
    Write-Host "Missing DLLs (may cause runtime issues):" -ForegroundColor Yellow
    foreach ($dll in $missing) {
        Write-Host "  - $dll" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "SDK setup complete!" -ForegroundColor Green
