[CmdletBinding()]
param(
    [ValidateSet('x64', 'ARM64')]
    [string]$Platform = 'x64',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$dotnet = Get-Command dotnet -ErrorAction Stop
$installedSdks = & $dotnet.Source --list-sdks
if (-not ($installedSdks | Where-Object { $_ -match '^9\.' })) {
    throw '.NET 9 SDK is required. Install it with: winget install --id Microsoft.DotNet.SDK.9 -e'
}

$pluginProject = Join-Path $repoRoot 'Community.PowerToys.Run.Plugin.LocalQrScanner\Community.PowerToys.Run.Plugin.LocalQrScanner.csproj'
& $dotnet.Source clean $pluginProject `
    --configuration $Configuration `
    --property:Platform=$Platform `
    --verbosity quiet `
    --nologo

if ($LASTEXITCODE -ne 0) {
    throw "Clean failed with exit code $LASTEXITCODE."
}

& $dotnet.Source build (Join-Path $repoRoot 'LocalQrScanner.slnx') `
    --configuration $Configuration `
    --property:Platform=$Platform `
    --nologo

if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

$outputDirectory = Join-Path $repoRoot "Community.PowerToys.Run.Plugin.LocalQrScanner\bin\$Platform\$Configuration\net9.0-windows10.0.26100.0"
$manifestPath = Join-Path $outputDirectory 'plugin.json'
$zxingPath = Join-Path $outputDirectory 'zxing.dll'
$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
if ($manifest.DynamicLoading -ne $false) {
    throw 'plugin.json must use the host load context; the plugin resolves only its private ZXing dependency.'
}

if (-not (Test-Path -LiteralPath $zxingPath)) {
    throw "Required runtime dependency is missing: $zxingPath"
}

Write-Host "Plugin output: $outputDirectory"
