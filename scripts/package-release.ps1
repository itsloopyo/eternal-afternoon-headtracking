#!/usr/bin/env pwsh
#Requires -Version 5.1
# Thin wrapper: calls shared packaging script with Eternal Afternoon values.

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Split-Path -Parent $scriptDir

$result = & "$projectDir/cameraunlock-core/scripts/package-bepinex-mod.ps1" `
    -ModName "EternalAfternoonHeadTracking" `
    -CsprojPath "src/EternalAfternoonHeadTracking/EternalAfternoonHeadTracking.csproj" `
    -BuildOutputDir "src/EternalAfternoonHeadTracking/bin/Release/net48" `
    -ModDlls @("EternalAfternoonHeadTracking.dll","CameraUnlock.Core.dll","CameraUnlock.Core.Unity.dll") `
    -ProjectRoot $projectDir

$modName = 'EternalAfternoonHeadTracking'
$buildOutputDir = 'src/EternalAfternoonHeadTracking/bin/Release/net48'
$modDlls = @('EternalAfternoonHeadTracking.dll', 'CameraUnlock.Core.dll', 'CameraUnlock.Core.Unity.dll')

# The Nexus ZIP is built here rather than by the shared packager: the shared
# script lives at whatever cameraunlock-core commit this mod pins, so its
# behaviour is frozen until the pointer moves. The notices below are a licence
# obligation and cannot wait on a submodule bump.
$version = [System.IO.Path]::GetFileNameWithoutExtension($result.GithubZip) -replace '^.*-v', '' -replace '-installer$', ''
$releaseDir = Join-Path $projectDir 'release'

# The installer ZIP is staged by the shared packager, which lives at whatever
# cameraunlock-core commit this mod pins and copies its notice files behind an
# `if (Test-Path)` guard - a missing one only warns, so a compliance failure
# would leave a green build. Assert against the finished archive instead: these
# licences must travel with the binaries the ZIP redistributes.
Add-Type -AssemblyName System.IO.Compression.FileSystem
$requiredInZip = @(
    'LICENSE',                  # our MIT grant
    'THIRD-PARTY-NOTICES.md',   # MIT texts for HarmonyX / Mono.Cecil / MonoMod
    'vendor/bepinex/LICENSE'    # BepInEx's LGPL-2.1, beside the binary it covers
)
$zip = [System.IO.Compression.ZipFile]::OpenRead($result.GithubZip)
try {
    $entries = @($zip.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
} finally {
    $zip.Dispose()
}
foreach ($required in $requiredInZip) {
    if ($entries -notcontains $required) {
        throw "Installer ZIP is missing '$required'. Every published ZIP is a binary distribution and the licences of what it redistributes must travel with it."
    }
}
Write-Host "  Verified notices present in installer ZIP: $($requiredInZip -join ', ')" -ForegroundColor Green

Write-Host ""
Write-Host "=== Creating NexusMods ZIP ===" -ForegroundColor Magenta

$nexusStagingDir = Join-Path $releaseDir 'staging-nexus'
if (Test-Path $nexusStagingDir) { Remove-Item -Recurse -Force $nexusStagingDir }
$nexusPluginsDir = Join-Path $nexusStagingDir 'BepInEx\plugins'
New-Item -ItemType Directory -Path $nexusPluginsDir -Force | Out-Null

foreach ($dll in $modDlls) {
    Copy-Item (Join-Path (Join-Path $projectDir $buildOutputDir) $dll) -Destination $nexusPluginsDir -Force
    Write-Host "  BepInEx/plugins/$dll" -ForegroundColor Green
}

# The Nexus ZIP is a binary distribution too: the licences of everything
# compiled into or bundled with the payload require their notices to travel
# with it, so LICENSE and THIRD-PARTY-NOTICES.md ship at its root.
foreach ($noticeDoc in @('LICENSE', 'THIRD-PARTY-NOTICES.md', 'README.md')) {
    $noticeSrc = Join-Path $projectDir $noticeDoc
    if (-not (Test-Path $noticeSrc)) {
        throw "Required notice file not found: $noticeDoc. Every published ZIP is a binary distribution and must carry it."
    }
    Copy-Item $noticeSrc -Destination $nexusStagingDir -Force
    Write-Host "  $noticeDoc" -ForegroundColor Green
}

$nexusZipPath = Join-Path $releaseDir "$modName-v$version-nexus.zip"
if (Test-Path $nexusZipPath) { Remove-Item $nexusZipPath -Force }
Push-Location $nexusStagingDir
try { Compress-Archive -Path '.\*' -DestinationPath $nexusZipPath -Force }
finally { Pop-Location }
Remove-Item -Recurse -Force $nexusStagingDir

Write-Host ("  $nexusZipPath ({0:N1} KB)" -f ((Get-Item $nexusZipPath).Length / 1KB)) -ForegroundColor Green
