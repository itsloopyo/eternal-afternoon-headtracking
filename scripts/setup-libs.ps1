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
$uiStubs     = Join-Path $libsPath 'UnityUIStubs.cs'

if (-not (Test-Path $stubSource)) { throw "UnityStubs.cs not found at $libsPath" }
if (-not (Test-Path $uiStubs))    { throw "UnityUIStubs.cs not found at $libsPath" }

New-Item -ItemType Directory -Path $libsPath -Force | Out-Null

# Modules exist only so the csproj references resolve; every stubbed type lives
# in UnityStubs.cs and therefore in UnityEngine.dll.
$modules = @(
    'UnityEngine.CoreModule', 'UnityEngine.IMGUIModule', 'UnityEngine.PhysicsModule',
    'UnityEngine.TextRenderingModule', 'UnityEngine.InputLegacyModule',
    'UnityEngine.UIModule'
)
$expected = @('UnityEngine', 'UnityEngine.UI') + $modules

# Regenerating means emptying libs/, and a build reading libs/ during that
# window fails with unresolvable references. Skip the regeneration entirely
# when what is on disk is already exactly the expected set, newer than every
# input. An unexpected file (a game DLL copied in by hand) fails the set
# comparison and forces a rebuild, which is the drift this script prevents.
$inputs = @($stubSource, $uiStubs, $MyInvocation.MyCommand.Path)
$newestInput = (Get-Item $inputs | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1).LastWriteTimeUtc
$onDisk = @(Get-ChildItem -Path $libsPath -Filter '*.dll' -File)
$namesOnDisk = @($onDisk | ForEach-Object { [System.IO.Path]::GetFileNameWithoutExtension($_.Name) } | Sort-Object)

if (($namesOnDisk -join '|') -eq (($expected | Sort-Object) -join '|') -and
    -not ($onDisk | Where-Object { $_.LastWriteTimeUtc -lt $newestInput })) {
    Write-Host "Build dependencies already current." -ForegroundColor Green
    return
}

Write-Host "Bootstrapping build dependencies (no game install required)..." -ForegroundColor Cyan

# Stage into a scratch directory and move the finished set into libs/ in one
# pass at the end. libs/ then only ever holds a complete set, so a concurrent
# build (or one killed mid-run) never sees half the references.
$stageDir = Join-Path ([System.IO.Path]::GetTempPath()) ("cu-stubs-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stageDir -Force | Out-Null

function Build-Stub {
    param([string]$assemblyName, [string]$compileItem, [string[]]$references = @())

    $refItems = ($references | ForEach-Object {
        $hint = Join-Path $stageDir "$_.dll"
        "    <Reference Include=`"$_`"><HintPath>$hint</HintPath><Private>false</Private></Reference>"
    }) -join "`n"

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
$refItems
  </ItemGroup>
</Project>
"@
    $projPath = Join-Path $stageDir "Stub_$assemblyName.csproj"
    $proj | Out-File -FilePath $projPath -Encoding utf8
    dotnet build $projPath -c Release -o $stageDir --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "Failed to build stub $assemblyName" }
    Write-Host "  Stub: $assemblyName.dll" -ForegroundColor Gray
}

try {
    Build-Stub 'UnityEngine' $stubSource

    # uGUI ships as its own assembly with no forwarder from UnityEngine.dll, so
    # its stubs have to be compiled into UnityEngine.UI.dll or the emitted
    # typerefs point at an assembly that does not declare them.
    Build-Stub 'UnityEngine.UI' $uiStubs @('UnityEngine')

    $emptySource = Join-Path $stageDir 'EmptyStub.cs'
    '// Empty stub assembly' | Out-File -FilePath $emptySource -Encoding utf8
    foreach ($m in $modules) { Build-Stub $m $emptySource }

    foreach ($name in $expected) {
        if (-not (Test-Path (Join-Path $stageDir "$name.dll"))) { throw "Stub $name.dll was not produced" }
    }

    # A stale game DLL left behind would build here and fail on CI, which is
    # exactly the drift this script prevents - so replace, don't merge.
    Get-ChildItem -Path $libsPath -Force |
        Where-Object { $_.Name -notin @('UnityStubs.cs', 'UnityUIStubs.cs') } |
        Remove-Item -Recurse -Force

    Move-Item -Path (Join-Path $stageDir '*.dll') -Destination $libsPath -Force
}
finally {
    Remove-Item $stageDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Build dependencies ready." -ForegroundColor Green
