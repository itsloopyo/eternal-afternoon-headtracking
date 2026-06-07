#!/usr/bin/env pwsh
#Requires -Version 5.1
<#
.SYNOPSIS
    Packages the Eternal Afternoon Head Tracking mod for release distribution.
.DESCRIPTION
    Produces two release ZIPs in release/:
    - EternalAfternoonHeadTracking-v{version}-installer.zip
      GitHub Release: install.cmd/uninstall.cmd + mod/ + vendor/<slug>/ + docs
    - EternalAfternoonHeadTracking-v{version}-nexus.zip
      Nexus Mods: extract-to-game-folder layout (deploy subtree only)
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$ProgressPreference = 'SilentlyContinue'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir

$csprojPath = Join-Path $projectRoot "src\EternalAfternoonHeadTracking\EternalAfternoonHeadTracking.csproj"
$buildOutput = Join-Path $projectRoot "src\EternalAfternoonHeadTracking\bin\Release\net48"
$toolsDir = Join-Path $projectRoot "tools"
$releaseDir = Join-Path $projectRoot "release"
$vendorRoot = Join-Path $projectRoot "vendor"
$vendorSlug = "mono-cecil"
$vendorDir = Join-Path $vendorRoot $vendorSlug

Import-Module (Join-Path $projectRoot "cameraunlock-core\powershell\ReleaseWorkflow.psm1") -Force

Write-Host ""
Write-Host "=== Eternal Afternoon Head Tracking - Package Release ===" -ForegroundColor Magenta
Write-Host ""

$version = Get-CsprojVersion $csprojPath
Write-Host "Version: $version" -ForegroundColor Cyan
Write-Host ""

# Validate build output exists
$modDlls = @("EternalAfternoonHeadTracking.dll", "CameraUnlock.Core.dll", "CameraUnlock.Core.Unity.dll")
foreach ($dll in $modDlls) {
    $dllPath = Join-Path $buildOutput $dll
    if (-not (Test-Path $dllPath)) {
        throw "Required DLL not found: $dllPath. Run 'pixi run build' first."
    }
}

# Validate installer scripts exist
foreach ($script in @("install.cmd", "uninstall.cmd")) {
    $scriptPath = Join-Path $scriptDir $script
    if (-not (Test-Path $scriptPath)) {
        throw "Required script not found: $scriptPath"
    }
}

# Create release directory
if (-not (Test-Path $releaseDir)) {
    New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
}

# --- GitHub Release ZIP (installer) ---

Write-Host "--- GitHub Release ZIP ---" -ForegroundColor Yellow
Write-Host ""

$ghStagingDir = Join-Path $releaseDir "staging-github"
if (Test-Path $ghStagingDir) { Remove-Item -Recurse -Force $ghStagingDir }
New-Item -ItemType Directory -Path $ghStagingDir -Force | Out-Null

# install/uninstall scripts
foreach ($script in @("install.cmd", "uninstall.cmd")) {
    Copy-Item (Join-Path $scriptDir $script) -Destination $ghStagingDir -Force
    Write-Host "  $script" -ForegroundColor Green
}

# Stamp launcher-manifest.json with the real release version and drop it at the
# installer ZIP root. The launcher reads this file to decide how to stage the
# mod (delivery_mode: manifest -> native deploy).
$manifestSource = Join-Path $projectRoot "launcher-manifest.json"
if (-not (Test-Path $manifestSource)) {
    throw "launcher-manifest.json not found at repo root: $manifestSource"
}
$manifestJson = Get-Content $manifestSource -Raw | ConvertFrom-Json
$manifestJson.mod_info.version = $version
# Set-Content -Encoding UTF8 on Windows PowerShell 5.1 writes a BOM that strict
# JSON parsers reject; write through the .NET API with a no-BOM encoder.
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText(
    (Join-Path $ghStagingDir "launcher-manifest.json"),
    ($manifestJson | ConvertTo-Json -Depth 10),
    $utf8NoBom
)
Write-Host "  launcher-manifest.json (v$version)" -ForegroundColor Green

Copy-SharedBundle -StagingDir $ghStagingDir -CoreRoot (Join-Path $projectRoot 'cameraunlock-core')

# Mod DLLs go into mod/ (Cecil patcher framework convention; install.cmd expects mod/)
$modDestDir = Join-Path $ghStagingDir "mod"
New-Item -ItemType Directory -Path $modDestDir -Force | Out-Null

foreach ($dll in $modDlls) {
    Copy-Item (Join-Path $buildOutput $dll) -Destination $modDestDir -Force
    Write-Host "  mod/$dll" -ForegroundColor Green
}

# Ensure Mono.Cecil is in tools/ then copy into mod/ (runtime dependency of the patcher)
$cecilPath = & (Join-Path $scriptDir "ensure-cecil.ps1") -ToolsDir $toolsDir
Copy-Item $cecilPath -Destination $modDestDir -Force
Write-Host "  mod/Mono.Cecil.dll" -ForegroundColor Green

# Patcher source (consumed by install.cmd at install time)
$patcherSource = Join-Path $scriptDir "patcher\BootstrapPatcher.cs"
if (-not (Test-Path $patcherSource)) {
    throw "Patcher not found: $patcherSource"
}
$patcherMain = Join-Path $scriptDir "patcher\PatcherMain.cs"
if (-not (Test-Path $patcherMain)) {
    throw "Patcher wrapper not found: $patcherMain"
}
Copy-Item $patcherSource -Destination $modDestDir -Force
Write-Host "  mod/BootstrapPatcher.cs" -ForegroundColor Green

$nativeToolsDir = Join-Path $ghStagingDir "tools"
New-Item -ItemType Directory -Path $nativeToolsDir -Force | Out-Null
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    throw "csc.exe not found at $csc"
}
$patcherExe = Join-Path $nativeToolsDir "BootstrapPatcher.exe"
& $csc /nologo /target:exe /out:$patcherExe /reference:$cecilPath $patcherSource $patcherMain
if ($LASTEXITCODE -ne 0) {
    throw "Failed to compile BootstrapPatcher.exe"
}
Copy-Item $cecilPath -Destination $nativeToolsDir -Force
Write-Host "  tools/BootstrapPatcher.exe" -ForegroundColor Green
Write-Host "  tools/Mono.Cecil.dll" -ForegroundColor Green

# Vendor tree (committed fallback for offline installs). Copy if present.
if (Test-Path $vendorDir) {
    $vendorStageDir = Join-Path $ghStagingDir "vendor\$vendorSlug"
    New-Item -ItemType Directory -Path $vendorStageDir -Force | Out-Null
    Copy-Item "$vendorDir\*" -Destination $vendorStageDir -Recurse -Force
    Write-Host "  vendor/$vendorSlug/" -ForegroundColor Green
} else {
    Write-Host "  (vendor/$vendorSlug/ not present - omitted)" -ForegroundColor DarkYellow
}

# Documentation
$docFiles = @("README.md", "CHANGELOG.md", "THIRD-PARTY-NOTICES.md", "THIRD-PARTY-NOTICES.txt")
foreach ($doc in $docFiles) {
    $docPath = Join-Path $projectRoot $doc
    if (Test-Path $docPath) {
        Copy-Item $docPath -Destination $ghStagingDir -Force
        Write-Host "  $doc" -ForegroundColor Green
    }
}

$ghZipName = "EternalAfternoonHeadTracking-v$version-installer.zip"
$ghZipPath = Join-Path $releaseDir $ghZipName
if (Test-Path $ghZipPath) { Remove-Item $ghZipPath -Force }

Write-Host ""
Write-Host "Creating GitHub ZIP..." -ForegroundColor Cyan

Push-Location $ghStagingDir
try {
    Compress-Archive -Path ".\*" -DestinationPath $ghZipPath -Force
} finally {
    Pop-Location
}
Remove-Item -Recurse -Force $ghStagingDir

$ghZipSize = (Get-Item $ghZipPath).Length / 1KB
Write-Host ("  $ghZipPath ({0:N1} KB)" -f $ghZipSize) -ForegroundColor Green

# --- Nexus Mods ZIP (extract-to-game-folder, deploy subtree only) ---

Write-Host ""
Write-Host "--- Nexus Mods ZIP ---" -ForegroundColor Yellow
Write-Host ""

$nexusStagingDir = Join-Path $releaseDir "staging-nexus"
if (Test-Path $nexusStagingDir) { Remove-Item -Recurse -Force $nexusStagingDir }

# Mirror game directory structure: Eternal Afternoon_Data/Managed/
$nexusManagedDir = Join-Path $nexusStagingDir "Eternal Afternoon_Data\Managed"
New-Item -ItemType Directory -Path $nexusManagedDir -Force | Out-Null

foreach ($dll in $modDlls) {
    Copy-Item (Join-Path $buildOutput $dll) -Destination $nexusManagedDir -Force
    Write-Host "  Eternal Afternoon_Data/Managed/$dll" -ForegroundColor Green
}

$nexusZipName = "EternalAfternoonHeadTracking-v$version-nexus.zip"
$nexusZipPath = Join-Path $releaseDir $nexusZipName
if (Test-Path $nexusZipPath) { Remove-Item $nexusZipPath -Force }

Write-Host ""
Write-Host "Creating Nexus ZIP..." -ForegroundColor Cyan

Push-Location $nexusStagingDir
try {
    Compress-Archive -Path ".\*" -DestinationPath $nexusZipPath -Force
} finally {
    Pop-Location
}
Remove-Item -Recurse -Force $nexusStagingDir

$nexusZipSize = (Get-Item $nexusZipPath).Length / 1KB
Write-Host ("  $nexusZipPath ({0:N1} KB)" -f $nexusZipSize) -ForegroundColor Green

# --- Summary ---

Write-Host ""
Write-Host "=== Package Complete ===" -ForegroundColor Magenta
Write-Host ""
Write-Host ("GitHub Release: $ghZipPath ({0:N1} KB)" -f $ghZipSize) -ForegroundColor Green
Write-Host ("Nexus Mods:     $nexusZipPath ({0:N1} KB)" -f $nexusZipSize) -ForegroundColor Green

# Output zip paths for CI capture (one per line)
Write-Output $ghZipPath
Write-Output $nexusZipPath
