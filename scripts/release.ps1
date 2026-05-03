#!/usr/bin/env pwsh
#Requires -Version 5.1
<#
.SYNOPSIS
    Automated release workflow for Eternal Afternoon Head Tracking.

.DESCRIPTION
    Performs the canonical 8-step release workflow:
      1. Parse <version> arg, validate semver.
      2. Verify on main, clean working tree, tag not already existing.
      3. Update version in csproj and HeadTrackingMod.cs.
      4. pixi run build (Release config).
      5. Generate CHANGELOG.md from commits since the last tag.
      6. Commit the version bump + changelog as "Release v<version>".
      7. Create annotated tag v<version>.
      8. Push commits + tag (triggers .github/workflows/release.yml).

    Non-destructive by default: fails fast on dirty tree, existing tag, or
    non-main branch. Never force-pushes, never amends.

.PARAMETER Version
    Semantic version to release (e.g., "1.0.0").
#>
param(
    [Parameter(Position=0)]
    [string]$Version = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Split-Path -Parent $scriptDir
$csprojPath = Join-Path $projectDir "src\EternalAfternoonHeadTracking\EternalAfternoonHeadTracking.csproj"
$modSourcePath = Join-Path $projectDir "src\EternalAfternoonHeadTracking\Core\HeadTrackingMod.cs"
$changelogPath = Join-Path $projectDir "CHANGELOG.md"

Import-Module (Join-Path $projectDir "cameraunlock-core\powershell\ReleaseWorkflow.psm1") -Force

Write-Host "=== Eternal Afternoon Head Tracking Release ===" -ForegroundColor Cyan
Write-Host ""

$currentVersion = Get-CsprojVersion $csprojPath

# No version provided -> show current and exit
if ([string]::IsNullOrWhiteSpace($Version)) {
    Write-Host "Current version: " -NoNewline -ForegroundColor Yellow
    Write-Host $currentVersion -ForegroundColor White
    Write-Host ""
    Write-Host "Usage: " -NoNewline -ForegroundColor Yellow
    Write-Host "pixi run release <major|minor|patch|X.Y.Z>" -ForegroundColor White
    Write-Host "Example: " -NoNewline -ForegroundColor Yellow
    Write-Host "pixi run release patch" -ForegroundColor White
    exit 0
}

# Step 1: resolve major/minor/patch into a concrete version (or accept literal X.Y.Z)
try {
    $Version = Resolve-ReleaseVersion -Argument $Version -CurrentVersion $currentVersion
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

$tagName = "v$Version"

# Step 2: preflight checks (branch, dirty tree, tag)
$currentBranch = (git rev-parse --abbrev-ref HEAD).Trim()
if ($currentBranch -ne "main") {
    Write-Host "Error: Must be on 'main' branch to release (currently on '$currentBranch')" -ForegroundColor Red
    exit 1
}

if (-not (Test-CleanGitStatus)) {
    Write-Host "Error: Working directory has uncommitted changes. Commit or stash before releasing." -ForegroundColor Red
    exit 1
}

if (Test-GitTagExists $tagName) {
    Write-Host "Error: Tag '$tagName' already exists" -ForegroundColor Red
    exit 1
}

Write-Host "Current version: $currentVersion" -ForegroundColor Gray
Write-Host "New version:     $Version" -ForegroundColor Green
Write-Host ""

# Step 3: update canonical version in csproj, then mirror into HeadTrackingMod.cs ModVersion constant
Write-Host "Updating version in csproj..." -ForegroundColor Cyan
Set-CsprojVersion $csprojPath $Version

if (Test-Path $modSourcePath) {
    Write-Host "Updating ModVersion constant in HeadTrackingMod.cs..." -ForegroundColor Cyan
    $modContent = Get-Content $modSourcePath -Raw
    $modContent = $modContent -replace 'ModVersion = "[^"]+"', "ModVersion = `"$Version`""
    $modContent | Set-Content $modSourcePath -NoNewline
}

# Step 4: build via pixi (ensures vendor refresh + restore + build chain runs)
Write-Host "Building release (pixi run build)..." -ForegroundColor Cyan
Push-Location $projectDir
try {
    & pixi run build
    if ($LASTEXITCODE -ne 0) {
        throw "pixi run build failed with exit code $LASTEXITCODE"
    }
} finally {
    Pop-Location
}

# Step 5: generate CHANGELOG from commits since last tag
Write-Host "Generating CHANGELOG..." -ForegroundColor Cyan
$hasExistingTags = git tag -l 2>$null
if (-not $hasExistingTags) {
    # First release - ensure a baseline CHANGELOG exists
    if (-not (Test-Path $changelogPath)) {
        $date = Get-Date -Format 'yyyy-MM-dd'
        "# Changelog`n`n## [$Version] - $date`n`nFirst release.`n" | Set-Content $changelogPath
        Write-Host "  Wrote initial CHANGELOG.md" -ForegroundColor Gray
    }
} else {
    $changelogArgs = @{
        ChangelogPath = $changelogPath
        Version = $Version
        ArtifactPaths = @(
            "src/",
            "cameraunlock-core",
            "scripts/install.cmd",
            "scripts/uninstall.cmd"
        )
    }
    New-ChangelogFromCommits @changelogArgs | Out-Null
}

# Step 6: commit version bump + changelog
Write-Host "Committing Release v$Version..." -ForegroundColor Cyan
git add $csprojPath
if (Test-Path $modSourcePath) { git add $modSourcePath }
if (Test-Path $changelogPath) { git add $changelogPath }
git commit -m "Release v$Version"
if ($LASTEXITCODE -ne 0) {
    throw "git commit failed"
}

# Step 7: create annotated tag
Write-Host "Creating tag $tagName..." -ForegroundColor Cyan
git tag -a $tagName -m "Release $tagName"
if ($LASTEXITCODE -ne 0) {
    throw "git tag failed"
}

# Step 8: push commits + tag (never force)
Write-Host "Pushing to origin main..." -ForegroundColor Cyan
git push origin main
if ($LASTEXITCODE -ne 0) {
    throw "git push origin main failed - tag created locally. Resolve and run: git push origin main $tagName"
}

Write-Host "Pushing tag $tagName..." -ForegroundColor Cyan
git push origin $tagName
if ($LASTEXITCODE -ne 0) {
    throw "git push tag failed - commit is on remote, tag is local. Run: git push origin $tagName"
}

Write-Host ""
Write-Host "Release $tagName complete. GitHub Actions release workflow will build the release artifacts." -ForegroundColor Green
