[CmdletBinding()]
param(
    [string]$InnoCompiler
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$pluginMetadata = Get-Content -Raw (Join-Path $repoRoot 'Community.PowerToys.Run.Plugin.LocalQrScanner\plugin.json') | ConvertFrom-Json
$version = $pluginMetadata.Version
$payloadDirectory = Join-Path $repoRoot 'Community.PowerToys.Run.Plugin.LocalQrScanner\bin\x64\Release\net9.0-windows10.0.26100.0'
$installerScript = Join-Path $repoRoot 'installer\LocalQrScanner.iss'
$outputDirectory = Join-Path $repoRoot 'artifacts'
$outputPath = Join-Path $outputDirectory "LocalQrScanner-Setup-$version-x64.exe"

& (Join-Path $PSScriptRoot 'build.ps1') -Platform x64 -Configuration Release

if (-not $InnoCompiler) {
    $compilerCandidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 7\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe')
    )
    $InnoCompiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}

if (-not $InnoCompiler -or -not (Test-Path -LiteralPath $InnoCompiler)) {
    throw 'Inno Setup compiler was not found. Install it with: winget install --id JRSoftware.InnoSetup -e'
}

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
& $InnoCompiler `
    "/DPluginSource=$payloadDirectory" `
    "/DOutputDirectory=$outputDirectory" `
    "/DAppVersion=$version" `
    "/DVersionInfoVersion=$version.0" `
    $installerScript

if ($LASTEXITCODE -ne 0) {
    throw "Installer compilation failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path -LiteralPath $outputPath)) {
    throw "Installer compilation finished but the expected output is missing: $outputPath"
}

Write-Host "Installer: $outputPath"
