#!/usr/bin/env pwsh
#Requires -Version 5.1
# Thin wrapper - dev-deploy orchestration lives in
# cameraunlock-core/powershell/DevDeploy.psm1.

param(
    [Parameter(Mandatory=$true, Position=0)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration,
    [Parameter(Mandatory=$false, Position=1)]
    [string]$GivenPath,
    [Parameter(ValueFromRemainingArguments=$true)]
    [string[]]$RemainingArgs
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$ProgressPreference = 'SilentlyContinue'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir

Import-Module (Join-Path $projectRoot "cameraunlock-core\powershell\DevDeploy.psm1") -Force
Import-Module (Join-Path $projectRoot "cameraunlock-core\powershell\ModDeployment.psm1") -Force
$toolsDir = Join-Path $projectRoot "tools"
$cecilPath = & (Join-Path $scriptDir "ensure-cecil.ps1") -ToolsDir $toolsDir

# Compiled once here rather than inside a scriptblock so the patch and unpatch
# callbacks share the type - Add-Type of the same type twice in one session throws.
Add-Type -Path $cecilPath
$patcherCode = Get-Content (Join-Path $scriptDir "patcher\BootstrapPatcher.cs") -Raw
$cp = New-Object System.CodeDom.Compiler.CompilerParameters
[void]$cp.ReferencedAssemblies.Add($cecilPath)
[void]$cp.ReferencedAssemblies.Add("System.dll")
[void]$cp.ReferencedAssemblies.Add("System.Core.dll")
$cp.CompilerOptions = "/nowarn:1668 /warn:0"
$cp.TreatWarningsAsErrors = $false
Add-Type -TypeDefinition $patcherCode -CompilerParameters $cp

$buildOutput = Join-Path $projectRoot "src\EternalAfternoonHeadTracking\bin\$Configuration\net48"
$result = Invoke-DevDeployCecil `
    -GameId 'eternal-afternoon' `
    -GameDisplayName 'Eternal Afternoon' `
    -BuildOutputPath $buildOutput `
    -ModDllName 'EternalAfternoonHeadTracking.dll' `
    -ManagedSubfolder 'Eternal Afternoon_Data\Managed' `
    -ExtraDlls @('CameraUnlock.Core.dll', 'CameraUnlock.Core.Unity.dll') `
    -GivenPath $GivenPath `
    -PatchMarker 'HeadTracking_Patched_EternalAfternoon_v1' `
    -Patcher {
        param($assemblyPath)
        if (-not [BootstrapPatcher]::PatchAssembly($assemblyPath)) {
            throw "BootstrapPatcher::PatchAssembly returned false"
        }
    } `
    -Unpatcher {
        param($assemblyPath)
        if (-not [BootstrapPatcher]::UnpatchAssembly($assemblyPath)) {
            throw "BootstrapPatcher::UnpatchAssembly returned false"
        }
    }

Write-DeploymentSuccess `
    -ModName "Head Tracking mod" `
    -DeployPath $result.DeployedDllPath `
    -Controls @(
        "End       - Toggle head tracking on/off",
        "Page Up   - Cycle tracking mode (both / rotation-only / position-only)",
        "Page Down - Toggle yaw mode (world / local)",
        "Insert    - Toggle aim reticle on/off",
        "",
        "No nav cluster? Chords: Ctrl+Shift+ Y=Toggle G=Mode H=Yaw U=Reticle"
    )