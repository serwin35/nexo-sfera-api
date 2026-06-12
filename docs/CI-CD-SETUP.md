# CI/CD Setup Guide for Nexo Sfera API

## Prerequisites

- InsERT Nexo SDK (licensed copy)
- .NET 8.0 SDK
- Windows environment (required for InsERT SDK)

## SDK Path Configuration

The project supports configurable SDK paths via MSBuild properties:

```bash
# Default path (src/lib/nexo-sdk)
dotnet build

# Custom path via command line
dotnet build -p:NexoSdkPath="C:\InsERT\nexo\SDK\Bin"

# Custom path via environment variable
$env:NexoSdkPath = "C:\InsERT\nexo\SDK\Bin"
dotnet build
```

## Local Development Setup

1. **Extract SDK** - Extract your licensed InsERT Nexo SDK to `docs/nexoSDK_60.1.1.9292/` folder

2. **Run setup script**:
   ```powershell
   .\scripts\setup-sdk.ps1
   ```

3. **Build**:
   ```bash
   dotnet build src/NexoSferaApi.csproj
   ```

## GitHub Actions Setup

### Option 1: Private SDK URL (Recommended)

Store your SDK DLLs as a zip file on a private server/storage and configure secrets:

1. **Create SDK package**:
   ```powershell
   # Create minimal SDK package with required DLLs only
   $dlls = Get-ChildItem "docs/nexoSDK_60.1.1.9292\Bin" -Filter "InsERT.*.dll"
   Compress-Archive -Path $dlls.FullName -DestinationPath "nexo-sdk-minimal.zip"
   ```

2. **Upload** to your private storage (Azure Blob, S3, private server)

3. **Configure GitHub Secrets**:
   - `NEXO_SDK_URL` - Direct download URL for the SDK zip
   - `NEXO_SDK_TOKEN` - (Optional) Bearer token for authentication

4. **Use workflow**: `.github/workflows/build.yml`

### Option 2: Self-Hosted Runner (Recommended for Production)

Best for organizations with existing Windows infrastructure:

1. **Setup Windows runner** with InsERT Nexo installed
   - Install InsERT Nexo on the runner machine
   - Configure runner with labels: `self-hosted`, `windows`, `nexo`

2. **Configure SDK path** in workflow:
   ```yaml
   env:
     NEXO_SDK_PATH: 'C:\Program Files\InsERT\nexo\SDK'
   ```

3. **Use workflow**: `.github/workflows/build-selfhosted.yml`

### Option 3: GitHub Artifacts Cache

For smaller teams, cache SDK as encrypted artifact:

1. **Create encrypted SDK archive**:
   ```powershell
   # Requires: Install-Module -Name 7Zip4Powershell
   $password = Read-Host -AsSecureString "Enter password"
   Compress-7Zip -Path "docs/nexoSDK_60.1.1.9292\Bin\InsERT.*.dll" `
                 -ArchiveFileName "nexo-sdk.7z" `
                 -SecurePassword $password
   ```

2. **Store password** in GitHub Secret: `SDK_ARCHIVE_PASSWORD`

3. **Modify workflow** to decrypt during build

## Required SDK DLLs

Minimum DLLs required for compilation:

```
InsERT.Common.Product.dll
InsERT.Moria.API.dll
InsERT.Moria.Asortymenty.dll
InsERT.Moria.Bank.dll
InsERT.Moria.CennikiICeny.dll
InsERT.Moria.DaneDomyslne.dll
InsERT.Moria.Deklaracje.dll
InsERT.Moria.Dokumenty.dll
InsERT.Moria.Finanse.dll
InsERT.Moria.HandelElektroniczny.dll
InsERT.Moria.Intrastat.dll
InsERT.Moria.Inwentaryzacja.dll
InsERT.Moria.Kasa.dll
InsERT.Moria.Klienci.dll
InsERT.Moria.KontrolaSkarbowa.dll
InsERT.Moria.Logistyka.dll
InsERT.Moria.ModelDanych.dll
InsERT.Moria.ModelOrganizacyjny.dll
InsERT.Moria.Naklejki.dll
InsERT.Moria.Promocje.dll
InsERT.Moria.Raporty.dll
InsERT.Moria.Rozrachunki.dll
InsERT.Moria.Security.dll
InsERT.Moria.Security.Core.dll
InsERT.Moria.Sfera.dll
InsERT.Moria.Slowniki.dll
InsERT.Moria.Wydruki.dll
InsERT.Mox.Core.dll
InsERT.Mox.EntityFramework.Core.dll
InsERT.Mox.EntityFrameworkSupport.dll
InsERT.Mox.Security.Sql.dll
```

## Troubleshooting

### Error: SDK DLLs not found
- Verify SDK path is correct
- Run `.\scripts\setup-sdk.ps1` to copy DLLs
- Check if `src/lib/nexo-sdk/` contains the DLL files

### Error: Platform not supported
- InsERT SDK requires Windows
- Use `windows-latest` or self-hosted Windows runner
- Ensure `RuntimeIdentifier` is set to `win-x64`

### Error: License issues
- InsERT SDK requires valid license on the machine running the application
- For build-only, DLL references are sufficient
- For runtime, ensure proper licensing on deployment target
