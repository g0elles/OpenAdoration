<#
.SYNOPSIS
    Publishes OpenAdoration as a self-contained single-file exe and builds the MSI installer.

.DESCRIPTION
    1. dotnet publish (Release, win-x64, single-file, self-contained) using the win-x64 profile.
    2. wix build → installer\out\OpenAdoration-<version>-win-x64.msi

    Requires the WiX CLI:  dotnet tool install --global wix

.EXAMPLE
    pwsh installer\build.ps1
    pwsh installer\build.ps1 -Version 1.1.0
#>
[CmdletBinding()]
param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$repoRoot   = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $repoRoot "OpenAdoration.WPF\bin\Release\net10.0-windows\win-x64\publish"
$outDir     = Join-Path $PSScriptRoot "out"
$msiPath    = Join-Path $outDir "OpenAdoration-$Version-win-x64.msi"

Write-Host "==> Publishing self-contained single-file exe..." -ForegroundColor Cyan
dotnet publish (Join-Path $repoRoot "OpenAdoration.WPF") -c Release -p:PublishProfile=win-x64 | Out-Host

if (-not (Test-Path (Join-Path $publishDir "OpenAdoration.exe"))) {
    throw "Publish did not produce OpenAdoration.exe at $publishDir"
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

Write-Host "==> Building MSI installer..." -ForegroundColor Cyan
$iconPath = Join-Path $repoRoot "OpenAdoration.WPF\Assets\openadoration.ico"
wix build (Join-Path $PSScriptRoot "OpenAdoration.wxs") `
    -d "PublishDir=$publishDir" `
    -d "IconPath=$iconPath" `
    -d "Version=$Version.0" `
    -o $msiPath | Out-Host

Write-Host "==> Done: $msiPath" -ForegroundColor Green
