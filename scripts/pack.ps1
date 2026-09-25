[CmdletBinding()]
param(
    [ValidateSet('x64', 'ARM64')]
    [string]$Platform = 'x64'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$pluginMetadata = Get-Content -Raw (Join-Path $repoRoot 'Community.PowerToys.Run.Plugin.LocalQrScanner\plugin.json') | ConvertFrom-Json
$version = $pluginMetadata.Version
& (Join-Path $PSScriptRoot 'build.ps1') -Platform $Platform -Configuration Release

$outputDirectory = Join-Path $repoRoot "Community.PowerToys.Run.Plugin.LocalQrScanner\bin\$Platform\Release\net9.0-windows10.0.26100.0"
$artifactDirectory = Join-Path $repoRoot 'artifacts'
$packagePath = Join-Path $artifactDirectory "LocalQrScanner-$version-$Platform.zip"
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null

Compress-Archive -Path (Join-Path $outputDirectory '*') -DestinationPath $packagePath -Force
Write-Host "Package: $packagePath"
