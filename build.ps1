<#
    build.ps1 — builds and publishes PGameLib.

    Run from the repository root in PowerShell on Windows:

        powershell -ExecutionPolicy Bypass -File .\build.ps1

    Produces two things under .\dist:
      * portable\            a self-contained single EXE (no .NET install required)
      * framework-dependent\ a smaller build that needs the .NET 9 Desktop Runtime
      * PGameLib-portable.zip   the portable build, zipped and ready to share

    Parameters:
      -Runtime     RID to target (default win-x64; use win-arm64 for ARM PCs)
      -SkipZip     don't create the zip
#>

[CmdletBinding()]
param(
    [string]$Runtime = "win-x64",
    [switch]$SkipZip
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$proj = Join-Path $root "src\PGameLib\PGameLib.csproj"
$dist = Join-Path $root "dist"

Write-Host "== PGameLib build ==" -ForegroundColor Cyan

# Sanity check: is the .NET SDK available?
$sdk = (& dotnet --version) 2>$null
if (-not $sdk) {
    Write-Error "The .NET SDK was not found. Install the .NET 9 SDK from https://dotnet.microsoft.com/download/dotnet/9.0 and try again."
    exit 1
}
Write-Host "Using .NET SDK $sdk" -ForegroundColor DarkGray

if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory -Path $dist | Out-Null

# 1) Restore + build (fails fast with clear errors if anything is wrong)
Write-Host "`n[1/4] Restoring + building (Release)..." -ForegroundColor Cyan
dotnet build $proj -c Release
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE. See the error above." }

# 2) Self-contained, single-file portable build (no runtime install needed)
Write-Host "`n[2/4] Publishing portable (self-contained single file)..." -ForegroundColor Cyan
$portable = Join-Path $dist "portable"
dotnet publish $proj -c Release -r $Runtime --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -o $portable
if ($LASTEXITCODE -ne 0) { throw "Portable publish failed with exit code $LASTEXITCODE. See the error above." }

# 3) Framework-dependent build (smaller; needs the .NET 9 Desktop Runtime)
Write-Host "`n[3/4] Publishing framework-dependent build..." -ForegroundColor Cyan
$fd = Join-Path $dist "framework-dependent"
dotnet publish $proj -c Release -r $Runtime --self-contained false `
    -p:DebugType=none `
    -o $fd
if ($LASTEXITCODE -ne 0) { throw "Framework-dependent publish failed with exit code $LASTEXITCODE. See the error above." }

# 4) Zip the portable build for easy sharing
if (-not $SkipZip) {
    Write-Host "`n[4/4] Zipping portable build..." -ForegroundColor Cyan
    if (-not (Test-Path $portable)) { throw "Portable output was not created; skipping zip." }
    $zip = Join-Path $dist "PGameLib-portable.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path (Join-Path $portable "*") -DestinationPath $zip
    Write-Host "Created $zip" -ForegroundColor Green
}

Write-Host "`nDone." -ForegroundColor Green
Write-Host "Portable EXE:  $(Join-Path $portable 'PGameLib.exe')"
Write-Host "Run it, or double-click from Explorer. No installation required." -ForegroundColor DarkGray
