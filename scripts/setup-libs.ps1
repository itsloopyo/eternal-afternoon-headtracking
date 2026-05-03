#!/usr/bin/env pwsh
#Requires -Version 5.1
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$libsDir = Join-Path $projectRoot "src\EternalAfternoonHeadTracking\libs"

# --- Game detection (inline, no shared module dependency) ---
$envVar = "ETERNALAFTERNOON_PATH"
$steamFolder = "Eternal Afternoon"
$dataFolder = "Eternal Afternoon_Data"
$gameExe = "Eternal Afternoon.exe"

$gamePath = $null

# Check environment variable
if ($env:ETERNALAFTERNOON_PATH -and (Test-Path (Join-Path $env:ETERNALAFTERNOON_PATH $gameExe))) {
    $gamePath = $env:ETERNALAFTERNOON_PATH
}

# Check default Steam locations
if (-not $gamePath) {
    $steamPaths = @()

    # Registry-based Steam detection
    $regPath = Get-ItemProperty -Path "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam" -Name "InstallPath" -ErrorAction SilentlyContinue
    if (-not $regPath) {
        $regPath = Get-ItemProperty -Path "HKLM:\SOFTWARE\Valve\Steam" -Name "InstallPath" -ErrorAction SilentlyContinue
    }
    if ($regPath) {
        $steamPaths += $regPath.InstallPath
    }

    # Parse libraryfolders.vdf for additional library paths
    foreach ($sp in $steamPaths) {
        $vdfFile = Join-Path $sp "steamapps\libraryfolders.vdf"
        if (Test-Path $vdfFile) {
            $vdfContent = Get-Content $vdfFile -Raw
            $matches = [regex]::Matches($vdfContent, '"path"\s+"([^"]+)"')
            foreach ($m in $matches) {
                $libPath = $m.Groups[1].Value -replace '\\\\', '\'
                if ($libPath -notin $steamPaths) {
                    $steamPaths += $libPath
                }
            }
        }
    }

    foreach ($sp in $steamPaths) {
        $candidate = Join-Path $sp "steamapps\common\$steamFolder"
        if (Test-Path (Join-Path $candidate $gameExe)) {
            $gamePath = $candidate
            break
        }
    }
}

if (-not $gamePath) {
    Write-Host "ERROR: Could not find Eternal Afternoon installation." -ForegroundColor Red
    Write-Host ""
    Write-Host "Please either:" -ForegroundColor Yellow
    Write-Host "  1. Set $envVar environment variable to your game folder"
    Write-Host "  2. Ensure the game is installed via Steam"
    exit 1
}

Write-Host "Found game installation at: $gamePath" -ForegroundColor Green

$managedPath = Join-Path $gamePath "$dataFolder\Managed"

if (-not (Test-Path $managedPath)) {
    Write-Host "ERROR: Managed folder not found at: $managedPath" -ForegroundColor Red
    Write-Host "The game installation may be corrupted. Try verifying game files in Steam."
    exit 1
}

Write-Host "Found Managed folder at: $managedPath" -ForegroundColor Green

# Required DLLs for building the mod
$requiredDlls = @(
    "Assembly-CSharp.dll",
    "UnityEngine.dll",
    "UnityEngine.CoreModule.dll",
    "UnityEngine.IMGUIModule.dll",
    "UnityEngine.PhysicsModule.dll",
    "UnityEngine.InputLegacyModule.dll",
    "UnityEngine.TextRenderingModule.dll",
    "UnityEngine.UIModule.dll",
    "UnityEngine.UI.dll"
)

# Check if all libs already exist and are up-to-date
$stale = @($requiredDlls | Where-Object {
    $dest = Join-Path $libsDir $_
    $src = Join-Path $managedPath $_
    -not (Test-Path $dest) -or (Get-Item $src).LastWriteTime -gt (Get-Item $dest).LastWriteTime
})

if ((Test-Path $libsDir) -and $stale.Count -eq 0) {
    Write-Host "All libs are up-to-date, skipping copy." -ForegroundColor Green
    exit 0
}

# Create libs directory if it doesn't exist
if (-not (Test-Path $libsDir)) {
    New-Item -ItemType Directory -Path $libsDir -Force | Out-Null
    Write-Host "Created libs directory: $libsDir" -ForegroundColor Green
}

# Copy each required DLL
$copyCount = 0
foreach ($dll in $requiredDlls) {
    $sourcePath = Join-Path $managedPath $dll
    $destPath = Join-Path $libsDir $dll

    if (-not (Test-Path $sourcePath)) {
        Write-Host "ERROR: Required DLL not found: $sourcePath" -ForegroundColor Red
        exit 1
    }

    Copy-Item -Path $sourcePath -Destination $destPath -Force
    Write-Host "Copied: $dll" -ForegroundColor Cyan
    $copyCount++
}

Write-Host ""
Write-Host "SUCCESS: Copied $copyCount DLLs to libs/" -ForegroundColor Green
Write-Host "You can now build the project with: pixi run build"
