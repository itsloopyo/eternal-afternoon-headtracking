#!/usr/bin/env pwsh
#Requires -Version 5.1
# Generates reference-only assemblies from Unity DLLs using JetBrains.Refasmer.
# Reference assemblies contain only type/method signatures (no IL bodies),
# making them legal to commit and sufficient for compilation.
#
# Prerequisites:
#   dotnet tool install -g JetBrains.Refasmer.CliTool
#
# Input:  src/EternalAfternoonHeadTracking/libs/ (real Unity DLLs from game)
# Output: src/EternalAfternoonHeadTracking/ref-libs/ (metadata-only assemblies)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$libsDir = Join-Path $projectRoot "src\EternalAfternoonHeadTracking\libs"
$refLibsDir = Join-Path $projectRoot "src\EternalAfternoonHeadTracking\ref-libs"

Write-Host ""
Write-Host "=== Generate Reference Assemblies ===" -ForegroundColor Cyan
Write-Host ""

# Validate source libs exist
if (-not (Test-Path $libsDir)) {
    Write-Host "ERROR: libs/ not found. Run 'pixi run setup-libs' first to extract Unity DLLs from the game." -ForegroundColor Red
    exit 1
}

# Validate refasmer is installed
$refasmer = Get-Command refasmer -ErrorAction SilentlyContinue
if (-not $refasmer) {
    Write-Host "ERROR: refasmer not found. Install it with:" -ForegroundColor Red
    Write-Host "  dotnet tool install -g JetBrains.Refasmer.CliTool" -ForegroundColor Yellow
    exit 1
}

# Unity DLLs that the project references (must match .csproj HintPath entries)
$unityDlls = @(
    "UnityEngine.dll",
    "UnityEngine.CoreModule.dll",
    "UnityEngine.IMGUIModule.dll",
    "UnityEngine.PhysicsModule.dll",
    "UnityEngine.InputLegacyModule.dll",
    "UnityEngine.TextRenderingModule.dll",
    "UnityEngine.UIModule.dll",
    "UnityEngine.UI.dll"
)

# Validate all source DLLs exist
foreach ($dll in $unityDlls) {
    $srcPath = Join-Path $libsDir $dll
    if (-not (Test-Path $srcPath)) {
        Write-Host "ERROR: Required DLL not found: $srcPath" -ForegroundColor Red
        exit 1
    }
}

# Create/clean output directory
if (Test-Path $refLibsDir) {
    Remove-Item -Recurse -Force $refLibsDir
}
New-Item -ItemType Directory -Path $refLibsDir -Force | Out-Null

# Generate reference assemblies
foreach ($dll in $unityDlls) {
    $srcPath = Join-Path $libsDir $dll
    $dstPath = Join-Path $refLibsDir $dll

    Write-Host "  $dll" -NoNewline
    $refOutput = & refasmer --all -O $refLibsDir $srcPath 2>&1
    $refExitCode = $LASTEXITCODE

    if ($refExitCode -ne 0) {
        Write-Host " FAILED" -ForegroundColor Red
        Write-Host $refOutput -ForegroundColor Red
        exit 1
    }

    if (-not (Test-Path $dstPath)) {
        Write-Host " FAILED (output missing)" -ForegroundColor Red
        exit 1
    }

    $srcSize = (Get-Item $srcPath).Length / 1KB
    $dstSize = (Get-Item $dstPath).Length / 1KB
    Write-Host (" ({0:N0} KB -> {1:N0} KB)" -f $srcSize, $dstSize) -ForegroundColor Green
}

Write-Host ""
Write-Host "Reference assemblies written to: $refLibsDir" -ForegroundColor Green
Write-Host "These are safe to commit - they contain only type signatures, no implementation." -ForegroundColor Gray
Write-Host ""
