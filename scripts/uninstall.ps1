#!/usr/bin/env pwsh
#Requires -Version 5.1
param(
    [switch]$CleanTemp
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir

$steamFolder = "Eternal Afternoon"
$dataFolder = "Eternal Afternoon_Data"
$gameExe = "Eternal Afternoon.exe"

$gamePath = $null

if ($env:ETERNALAFTERNOON_PATH -and (Test-Path (Join-Path $env:ETERNALAFTERNOON_PATH $gameExe))) {
    $gamePath = $env:ETERNALAFTERNOON_PATH
}

if (-not $gamePath) {
    $steamPaths = @()
    $regPath = Get-ItemProperty -Path "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam" -Name "InstallPath" -ErrorAction SilentlyContinue
    if (-not $regPath) {
        $regPath = Get-ItemProperty -Path "HKLM:\SOFTWARE\Valve\Steam" -Name "InstallPath" -ErrorAction SilentlyContinue
    }
    if ($regPath) {
        $steamPaths += $regPath.InstallPath
        $vdfFile = Join-Path $regPath.InstallPath "steamapps\libraryfolders.vdf"
        if (Test-Path $vdfFile) {
            $vdfContent = Get-Content $vdfFile -Raw
            $matches = [regex]::Matches($vdfContent, '"path"\s+"([^"]+)"')
            foreach ($m in $matches) {
                $libPath = $m.Groups[1].Value -replace '\\\\', '\'
                if ($libPath -notin $steamPaths) { $steamPaths += $libPath }
            }
        }
    }
    foreach ($sp in $steamPaths) {
        $candidate = Join-Path $sp "steamapps\common\$steamFolder"
        if (Test-Path (Join-Path $candidate $gameExe)) { $gamePath = $candidate; break }
    }
}

if (-not $gamePath) {
    Write-Host "ERROR: Could not find Eternal Afternoon installation." -ForegroundColor Red
    exit 1
}

Write-Host "Found game at: $gamePath" -ForegroundColor Green

$managedPath = Join-Path $gamePath "$dataFolder\Managed"
$assemblyCSharpPath = Join-Path $managedPath "Assembly-CSharp.dll"
$backupPath = Join-Path $managedPath "Assembly-CSharp.dll.original"

# Restore backup
if (Test-Path $backupPath) {
    Copy-Item $backupPath $assemblyCSharpPath -Force
    Remove-Item $backupPath -Force
    Write-Host "Restored original Assembly-CSharp.dll" -ForegroundColor Green
} else {
    Write-Host "No backup found - verify game files via Steam." -ForegroundColor Yellow
}

# Remove mod files
$modFiles = @("EternalAfternoonHeadTracking.dll", "CameraUnlock.Core.dll", "CameraUnlock.Core.Unity.dll",
              "Mono.Cecil.dll", "HeadTracking.cfg", "HeadTracking.log",
              "HeadTracking_BOOT.log", "HeadTracking.manifest.json")

foreach ($f in $modFiles) {
    $p = Join-Path $managedPath $f
    if (Test-Path $p) {
        Remove-Item $p -Force
        Write-Host "  Removed: $f" -ForegroundColor Gray
    }
}

if ($CleanTemp) {
    $tempLog = Join-Path ([System.IO.Path]::GetTempPath()) "HeadTracking_BOOT_ERROR.log"
    if (Test-Path $tempLog) {
        Remove-Item $tempLog -Force
        Write-Host "  Removed temp error log" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "Uninstall complete." -ForegroundColor Green
