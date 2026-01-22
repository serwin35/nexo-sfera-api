# Nexo Sfera API Configuration Script
# This script helps configure the API connection settings

param(
    [switch]$UseEnvFile,
    [switch]$Interactive,
    [switch]$Test,
    [string]$ConfigPath = "src\appsettings.json"
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Nexo Sfera API Configuration Setup   " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Get script and project paths
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir
$configFullPath = Join-Path $rootDir $ConfigPath

# Default values
$defaults = @{
    Server = "(local)\INSERTNEXO"
    Database = "Nexo_demo_1"
    UseWindowsAuth = "true"
    Product = "Subiekt"
    NexoLogin = "Szef"
    Port = "5000"
}

function Read-ConfigValue {
    param(
        [string]$Prompt,
        [string]$Default,
        [switch]$IsPassword,
        [switch]$Required
    )

    $displayDefault = if ($IsPassword -and $Default) { "****" } else { $Default }
    $fullPrompt = if ($displayDefault) { "$Prompt [$displayDefault]: " } else { "${Prompt}: " }

    if ($IsPassword) {
        $secureValue = Read-Host $fullPrompt -AsSecureString
        $value = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
            [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureValue))
    } else {
        $value = Read-Host $fullPrompt
    }

    if ([string]::IsNullOrWhiteSpace($value)) {
        $value = $Default
    }

    if ($Required -and [string]::IsNullOrWhiteSpace($value)) {
        Write-Host "This field is required!" -ForegroundColor Red
        return Read-ConfigValue -Prompt $Prompt -Default $Default -IsPassword:$IsPassword -Required:$Required
    }

    return $value
}

function Test-SqlConnection {
    param(
        [string]$Server,
        [string]$Database,
        [bool]$UseWindowsAuth,
        [string]$SqlLogin,
        [string]$SqlPassword
    )

    Write-Host "Testing SQL connection..." -ForegroundColor Yellow

    try {
        $connectionString = if ($UseWindowsAuth) {
            "Server=$Server;Database=$Database;Integrated Security=True;TrustServerCertificate=True"
        } else {
            "Server=$Server;Database=$Database;User Id=$SqlLogin;Password=$SqlPassword;TrustServerCertificate=True"
        }

        $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
        $connection.Open()
        $connection.Close()

        Write-Host "SQL connection successful!" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "SQL connection failed: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

if ($Interactive) {
    Write-Host "Please provide connection settings:" -ForegroundColor Yellow
    Write-Host ""

    # SQL Server settings
    Write-Host "--- SQL Server Connection ---" -ForegroundColor Cyan
    $server = Read-ConfigValue -Prompt "SQL Server" -Default $defaults.Server
    $database = Read-ConfigValue -Prompt "Database name" -Default $defaults.Database

    $useWinAuth = Read-ConfigValue -Prompt "Use Windows Authentication (true/false)" -Default $defaults.UseWindowsAuth
    $useWindowsAuth = $useWinAuth -eq "true"

    $sqlLogin = ""
    $sqlPassword = ""
    if (-not $useWindowsAuth) {
        $sqlLogin = Read-ConfigValue -Prompt "SQL Login" -Required
        $sqlPassword = Read-ConfigValue -Prompt "SQL Password" -IsPassword -Required
    }

    Write-Host ""
    Write-Host "--- Nexo Application Settings ---" -ForegroundColor Cyan
    $product = Read-ConfigValue -Prompt "Product (Subiekt/Rachmistrz/Rewizor/Gratyfikant)" -Default $defaults.Product
    $nexoLogin = Read-ConfigValue -Prompt "Nexo operator login" -Default $defaults.NexoLogin
    $nexoPassword = Read-ConfigValue -Prompt "Nexo operator password" -IsPassword -Required

    Write-Host ""
    Write-Host "--- API Settings ---" -ForegroundColor Cyan
    $port = Read-ConfigValue -Prompt "API Port" -Default $defaults.Port
    $apiKey = Read-ConfigValue -Prompt "API Key (leave empty to generate)" -Default ""

    if ([string]::IsNullOrWhiteSpace($apiKey)) {
        $apiKey = [System.Guid]::NewGuid().ToString("N")
        Write-Host "Generated API Key: $apiKey" -ForegroundColor Green
    }

    # Test connection if requested
    if ($Test) {
        Write-Host ""
        $testResult = Test-SqlConnection -Server $server -Database $database `
            -UseWindowsAuth $useWindowsAuth -SqlLogin $sqlLogin -SqlPassword $sqlPassword

        if (-not $testResult) {
            $continue = Read-Host "Connection test failed. Continue anyway? (y/n)"
            if ($continue -ne "y") {
                exit 1
            }
        }
    }

    # Create configuration
    $config = @{
        Logging = @{
            LogLevel = @{
                Default = "Information"
                "Microsoft.AspNetCore" = "Warning"
                NexoSferaApi = "Information"
            }
        }
        AllowedHosts = "*"
        Kestrel = @{
            Endpoints = @{
                Http = @{
                    Url = "http://0.0.0.0:$port"
                }
            }
        }
        Sfera = @{
            Server = $server
            Database = $database
            UseWindowsAuth = $useWindowsAuth
            SqlLogin = if ($useWindowsAuth) { $null } else { $sqlLogin }
            SqlPassword = if ($useWindowsAuth) { $null } else { $sqlPassword }
            NexoLogin = $nexoLogin
            NexoPassword = $nexoPassword
            Product = $product
        }
        ApiKeys = @{
            Keys = @(
                @{
                    Key = $apiKey
                    Name = "Primary API Key"
                    IsActive = $true
                }
            )
        }
    }

    # Save configuration
    $json = $config | ConvertTo-Json -Depth 10
    $json | Out-File -FilePath $configFullPath -Encoding UTF8

    Write-Host ""
    Write-Host "Configuration saved to: $configFullPath" -ForegroundColor Green
    Write-Host ""
    Write-Host "Summary:" -ForegroundColor Cyan
    Write-Host "  Server:     $server"
    Write-Host "  Database:   $database"
    Write-Host "  Auth:       $(if($useWindowsAuth){'Windows'}else{'SQL'})"
    Write-Host "  Product:    $product"
    Write-Host "  Operator:   $nexoLogin"
    Write-Host "  API Port:   $port"
    Write-Host "  API Key:    $apiKey"
}
elseif ($UseEnvFile) {
    # Generate .env file for Docker/environment-based configuration
    $envPath = Join-Path $rootDir ".env"

    Write-Host "Generating .env file..." -ForegroundColor Yellow

    @"
# Nexo Sfera API Environment Configuration
# Copy this file and customize for your environment

# SQL Server Connection
SFERA_SERVER=(local)\INSERTNEXO
SFERA_DATABASE=Nexo_demo_1
SFERA_USE_WINDOWS_AUTH=true
SFERA_SQL_LOGIN=
SFERA_SQL_PASSWORD=

# Nexo Application
SFERA_NEXO_LOGIN=Szef
SFERA_NEXO_PASSWORD=robocze
SFERA_PRODUCT=Subiekt

# API Settings
API_KEY=$(([System.Guid]::NewGuid().ToString("N")))
API_PORT=5000

# Logging
LOGGING__LOGLEVEL__DEFAULT=Information
"@ | Out-File -FilePath $envPath -Encoding UTF8

    Write-Host ".env file created: $envPath" -ForegroundColor Green
    Write-Host "Edit this file with your settings, then restart the application." -ForegroundColor Yellow
}
else {
    Write-Host "Usage:" -ForegroundColor Yellow
    Write-Host "  .\configure.ps1 -Interactive      # Interactive configuration wizard"
    Write-Host "  .\configure.ps1 -Interactive -Test # With SQL connection test"
    Write-Host "  .\configure.ps1 -UseEnvFile       # Generate .env template file"
    Write-Host ""
    Write-Host "Environment Variables (alternative to appsettings.json):" -ForegroundColor Yellow
    Write-Host "  SFERA_SERVER          - SQL Server instance"
    Write-Host "  SFERA_DATABASE        - Database name"
    Write-Host "  SFERA_USE_WINDOWS_AUTH - true/false"
    Write-Host "  SFERA_SQL_LOGIN       - SQL login (if not Windows auth)"
    Write-Host "  SFERA_SQL_PASSWORD    - SQL password (if not Windows auth)"
    Write-Host "  SFERA_NEXO_LOGIN      - Nexo operator login"
    Write-Host "  SFERA_NEXO_PASSWORD   - Nexo operator password"
    Write-Host "  SFERA_PRODUCT         - Product type (Subiekt/Rachmistrz/etc)"
    Write-Host "  API_KEY               - API authentication key"
}
