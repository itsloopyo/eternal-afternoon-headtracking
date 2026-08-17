#!/usr/bin/env pwsh
#Requires -Version 5.1
# Populates src/EternalAfternoonHeadTracking/libs/ for a game-free build.
# Unity reference stubs are compiled from the checked-in UnityStubs.cs, so a
# clean checkout builds identically on CI and on a machine without the game.
# Nothing here reads a game install.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$scriptDir   = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$libsPath    = Join-Path $projectRoot 'src\EternalAfternoonHeadTracking\libs'
$stubSource  = Join-Path $libsPath 'UnityStubs.cs'

if (-not (Test-Path $stubSource)) { throw "UnityStubs.cs not found at $libsPath" }

New-Item -ItemType Directory -Path $libsPath -Force | Out-Null

Write-Host "Bootstrapping build dependencies (no game install required)..." -ForegroundColor Cyan

# Wipe libs/ except the tracked stub source. A stale game DLL left behind would
# build here and fail on CI, which is exactly the drift this script prevents.
Get-ChildItem -Path $libsPath -Force |
    Where-Object { $_.Name -ne 'UnityStubs.cs' } |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

function Build-Stub {
    param([string]$assemblyName, [string]$compileItem)

    $proj = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <LangVersion>latest</LangVersion>
    <AssemblyName>$assemblyName</AssemblyName>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <NoWarn>CS0169;CS0649;CS0067;CS0660;CS0661</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$compileItem" />
  </ItemGroup>
</Project>
"@
    $projPath = Join-Path $libsPath "Stub_$assemblyName.csproj"
    $proj | Out-File -FilePath $projPath -Encoding utf8
    dotnet build $projPath -c Release -o $libsPath --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "Failed to build stub $assemblyName" }
    Remove-Item $projPath -ErrorAction SilentlyContinue
    Write-Host "  Stub: $assemblyName.dll" -ForegroundColor Gray
}

# Every stubbed type lives in UnityStubs.cs and therefore in UnityEngine.dll.
# The module assemblies exist only so the csproj's references resolve.
Build-Stub 'UnityEngine' 'UnityStubs.cs'

$emptySource = Join-Path $libsPath 'EmptyStub.cs'
'// Empty stub assembly' | Out-File -FilePath $emptySource -Encoding utf8
foreach ($m in @(
    'UnityEngine.CoreModule', 'UnityEngine.IMGUIModule', 'UnityEngine.PhysicsModule',
    'UnityEngine.TextRenderingModule', 'UnityEngine.InputLegacyModule',
    'UnityEngine.UIModule', 'UnityEngine.UI'
)) { Build-Stub $m 'EmptyStub.cs' }

Remove-Item $emptySource -ErrorAction SilentlyContinue
Remove-Item (Join-Path $libsPath '*.deps.json') -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $libsPath '*.pdb')        -Force -ErrorAction SilentlyContinue

Write-Host "Build dependencies ready." -ForegroundColor Green
