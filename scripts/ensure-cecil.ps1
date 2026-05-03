#Requires -Version 5.1
param(
    [Parameter(Mandatory=$true)]
    [string]$ToolsDir
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$cecilPath = Join-Path $ToolsDir "Mono.Cecil.dll"

if (Test-Path $cecilPath) {
    return $cecilPath
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$vendorDir = Join-Path $projectRoot "vendor\mono-cecil"

$nupkg = Get-ChildItem -Path $vendorDir -Filter "Mono.Cecil.*.nupkg" -ErrorAction SilentlyContinue |
    Select-Object -First 1
if (-not $nupkg) {
    throw "Vendored Mono.Cecil nupkg not found in $vendorDir. Run 'pixi run update-deps' to refresh, then commit the result."
}

if (-not (Test-Path $ToolsDir)) {
    New-Item -ItemType Directory -Path $ToolsDir -Force | Out-Null
}

$extractPath = Join-Path $ToolsDir "mono.cecil"
if (Test-Path $extractPath) { Remove-Item -Recurse -Force $extractPath }

Write-Host "Extracting vendored Mono.Cecil ($($nupkg.Name)) to tools/..." -ForegroundColor Yellow
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory($nupkg.FullName, $extractPath)
Copy-Item (Join-Path $extractPath "lib\net40\Mono.Cecil.dll") $cecilPath -Force
Remove-Item $extractPath -Recurse -Force

Write-Host "  Mono.Cecil.dll ready at tools/" -ForegroundColor Green

return $cecilPath
