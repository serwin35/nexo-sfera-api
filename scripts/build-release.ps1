<#
.SYNOPSIS
    Buduje NexoSferaApi jako samodzielny plik .exe gotowy do dystrybucji.

.DESCRIPTION
    Skrypt publikuje aplikację jako self-contained win-x64, kopiuje plik
    konfiguracyjny i pakuje wszystko do archiwum ZIP.

.PARAMETER Version
    Numer wersji do umieszczenia w nazwie ZIP (np. "1.2.3").
    Domyślnie: pobiera z git tag lub używa daty.

.PARAMETER OutputDir
    Folder wyjściowy. Domyślnie: dist\ w katalogu głównym projektu.

.PARAMETER SkipSdkCheck
    Pomija sprawdzanie obecności SDK Nexo.

.PARAMETER NoZip
    Nie pakuje do ZIP — zostaje tylko folder dist\.

.EXAMPLE
    .\scripts\build-release.ps1
    .\scripts\build-release.ps1 -Version "1.0.0"
    .\scripts\build-release.ps1 -Version "1.0.0" -NoZip
#>

param(
    [string]$Version = "",
    [string]$OutputDir = "",
    [switch]$SkipSdkCheck,
    [switch]$NoZip
)

$ErrorActionPreference = "Stop"
$scriptDir  = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir    = Split-Path -Parent $scriptDir
$srcDir     = Join-Path $rootDir "src"
$sdkDir     = Join-Path $srcDir "lib\nexo-sdk"
$csprojPath = Join-Path $srcDir "NexoSferaApi.csproj"

# ── Kolory ────────────────────────────────────────────────────────────────────
function Write-Step  { param($msg) Write-Host "`n  ► $msg" -ForegroundColor Cyan }
function Write-Ok    { param($msg) Write-Host "  ✔ $msg" -ForegroundColor Green }
function Write-Warn  { param($msg) Write-Host "  ⚠ $msg" -ForegroundColor Yellow }
function Write-Fail  { param($msg) Write-Host "`n  ✘ $msg" -ForegroundColor Red }

Write-Host ""
Write-Host "══════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "   NexoSferaApi — Build Release (win-x64)         " -ForegroundColor Cyan
Write-Host "══════════════════════════════════════════════════" -ForegroundColor Cyan

# ── Wersja ────────────────────────────────────────────────────────────────────
if (-not $Version) {
    $gitTag = git -C $rootDir describe --tags --abbrev=0 2>$null
    if ($gitTag) {
        $Version = $gitTag.TrimStart("v")
        Write-Ok "Wersja z git tag: $Version"
    } else {
        $Version = Get-Date -Format "yyyy.MM.dd"
        Write-Warn "Brak git tagu — wersja: $Version"
    }
}

# ── Foldery ───────────────────────────────────────────────────────────────────
if (-not $OutputDir) {
    $OutputDir = Join-Path $rootDir "dist"
}
$zipName    = "NexoSferaApi-v$Version-win-x64.zip"
$zipPath    = Join-Path $rootDir $zipName

# ── Sprawdzenie SDK ───────────────────────────────────────────────────────────
if (-not $SkipSdkCheck) {
    Write-Step "Sprawdzanie SDK Nexo..."
    $coreDll = Join-Path $sdkDir "InsERT.Moria.Sfera.dll"
    if (-not (Test-Path $coreDll)) {
        Write-Fail "Nie znaleziono SDK Nexo w: $sdkDir"
        Write-Host "  Uruchom najpierw: .\scripts\setup-sdk.ps1" -ForegroundColor Yellow
        exit 1
    }
    $sdkCount = (Get-ChildItem $sdkDir -Filter "*.dll").Count
    Write-Ok "SDK Nexo OK ($sdkCount plików DLL)"
}

# ── Czyszczenie dist/ ─────────────────────────────────────────────────────────
Write-Step "Czyszczenie folderu wyjściowego..."
if (Test-Path $OutputDir) {
    Remove-Item $OutputDir -Recurse -Force
    Write-Ok "Wyczyszczono: $OutputDir"
}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

# ── Publish ───────────────────────────────────────────────────────────────────
Write-Step "Kompilacja i publikacja (self-contained, win-x64)..."
Write-Host "    dotnet publish ... -o $OutputDir" -ForegroundColor DarkGray

$publishArgs = @(
    "publish", $csprojPath,
    "--configuration", "Release",
    "--runtime", "win-x64",
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-p:PublishReadyToRun=true",
    "-p:EnableCompressionInSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=false",
    "-p:DebugType=embedded",
    "--output", $OutputDir,
    "--nologo"
)

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    Write-Fail "Kompilacja nie powiodła się (kod: $LASTEXITCODE)"
    exit $LASTEXITCODE
}
Write-Ok "Kompilacja zakończona sukcesem"

# ── Plik konfiguracyjny ───────────────────────────────────────────────────────
Write-Step "Kopiowanie pliku konfiguracyjnego..."
$templateSrc = Join-Path $srcDir "appsettings.template.json"
$configDest  = Join-Path $OutputDir "appsettings.json"

if (Test-Path $templateSrc) {
    Copy-Item $templateSrc $configDest -Force
    Write-Ok "Skopiowano appsettings.template.json → appsettings.json"
    Write-Warn "Pamiętaj: skonfiguruj appsettings.json przed uruchomieniem!"
} else {
    Write-Warn "Brak appsettings.template.json — plik konfiguracyjny nie został skopiowany"
}

# ── Usunięcie nadmiarowych plików ─────────────────────────────────────────────
Write-Step "Porządkowanie folderu dist/..."
$toRemove = @("*.pdb", "appsettings.Development.json", "web.config")
foreach ($pattern in $toRemove) {
    Get-ChildItem $OutputDir -Filter $pattern -ErrorAction SilentlyContinue | Remove-Item -Force
}
Write-Ok "Usunięto pliki developerskie"

# ── Podsumowanie dist/ ────────────────────────────────────────────────────────
$exeFile = Get-ChildItem $OutputDir -Filter "*.exe" | Select-Object -First 1
$allFiles = Get-ChildItem $OutputDir
$totalSizeMB = [math]::Round(($allFiles | Measure-Object -Property Length -Sum).Sum / 1MB, 1)

Write-Host ""
Write-Host "  Zawartość dist/:" -ForegroundColor DarkGray
foreach ($f in $allFiles | Sort-Object Name) {
    $sizeMB = [math]::Round($f.Length / 1MB, 1)
    Write-Host "    $($f.Name.PadRight(40)) $sizeMB MB" -ForegroundColor DarkGray
}
Write-Host "  ─────────────────────────────────────────────" -ForegroundColor DarkGray
Write-Host "  Łącznie: $totalSizeMB MB" -ForegroundColor DarkGray

# ── Pakowanie ZIP ─────────────────────────────────────────────────────────────
if (-not $NoZip) {
    Write-Step "Pakowanie do ZIP..."
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path "$OutputDir\*" -DestinationPath $zipPath -CompressionLevel Optimal
    $zipSizeMB = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
    Write-Ok "Plik ZIP: $zipName ($zipSizeMB MB)"
}

# ── Gotowe ────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "══════════════════════════════════════════════════" -ForegroundColor Green
Write-Host "   ✔  Build gotowy!                               " -ForegroundColor Green
Write-Host "══════════════════════════════════════════════════" -ForegroundColor Green
Write-Host ""
if ($exeFile) {
    Write-Host "  Plik exe:    dist\$($exeFile.Name)" -ForegroundColor White
}
Write-Host "  Konfiguracja: dist\appsettings.json" -ForegroundColor White
if (-not $NoZip) {
    Write-Host "  Archiwum:    $zipName" -ForegroundColor White
}
Write-Host ""
Write-Host "  Kroki po buildzie:" -ForegroundColor Yellow
Write-Host "    1. Edytuj dist\appsettings.json (serwer SQL, login Nexo, klucze API)" -ForegroundColor Yellow
Write-Host "    2. Uruchom dist\NexoSferaApi.exe" -ForegroundColor Yellow
Write-Host "    3. API dostępne pod: http://localhost:5000" -ForegroundColor Yellow
Write-Host "    4. Swagger UI:       http://localhost:5000/swagger" -ForegroundColor Yellow
Write-Host ""
