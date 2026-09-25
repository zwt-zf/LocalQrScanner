[CmdletBinding()]
param(
    [ValidateSet('x64', 'ARM64')]
    [string]$Platform = 'x64',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot 'build.ps1') -Platform $Platform -Configuration Release
}

$sourceDirectory = Join-Path $repoRoot "Community.PowerToys.Run.Plugin.LocalQrScanner\bin\$Platform\Release\net9.0-windows10.0.26100.0"
$pluginRoot = Join-Path $env:LOCALAPPDATA 'Microsoft\PowerToys\PowerToys Run\Plugins'
$destinationDirectory = Join-Path $pluginRoot 'LocalQrScanner'

if (-not (Test-Path -LiteralPath $sourceDirectory)) {
    throw 'Release output is missing. Run scripts\build.ps1 first.'
}

$runningPowerToys = Get-Process -ErrorAction SilentlyContinue | Where-Object {
    $_.ProcessName -eq 'PowerToys' -or $_.ProcessName -eq 'PowerToys.PowerLauncher'
}
if ($runningPowerToys) {
    throw 'Please exit PowerToys from the system tray before installing the plugin.'
}

New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
Copy-Item -Path (Join-Path $sourceDirectory '*') -Destination $destinationDirectory -Recurse -Force
Write-Host "Installed to: $destinationDirectory"
Write-Host 'Start PowerToys, open PowerToys Run, and type: qr'
