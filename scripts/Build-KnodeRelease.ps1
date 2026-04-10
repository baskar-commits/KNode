#Requires -Version 5.1
<#
.SYNOPSIS
  Optional version bump, then Release build + publish + Inno installer (one flow).

.DESCRIPTION
  Does NOT hook into plain "dotnet build" (that would break CI and non-interactive builds).

  1. Optionally runs Bump-KnodeVersion.ps1 (minor/major) so csproj + Knode.iss stay in sync.
  2. dotnet build Knode.sln -c Release
  3. dotnet/build-installer.ps1 (publish + Inno)

  Usage (repository root):
    powershell -ExecutionPolicy Bypass -File .\scripts\Build-KnodeRelease.ps1

  Skip bump (e.g. quick rebuild):
    powershell -ExecutionPolicy Bypass -File .\scripts\Build-KnodeRelease.ps1 -SkipVersionBump

  Always bump without asking:
    powershell -ExecutionPolicy Bypass -File .\scripts\Build-KnodeRelease.ps1 -BumpVersion

  Inno not on PATH:
    powershell -ExecutionPolicy Bypass -File .\scripts\Build-KnodeRelease.ps1 -InnoPath "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
#>
param(
    [switch] $BumpVersion,
    [switch] $SkipVersionBump,
    [string] $InnoPath = "ISCC.exe"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$bumpScript = Join-Path $repoRoot "scripts\Bump-KnodeVersion.ps1"
$dotnetBuild = Join-Path $repoRoot "dotnet\Knode.sln"
$buildInstaller = Join-Path $repoRoot "dotnet\build-installer.ps1"

Set-Location $repoRoot

if (-not $SkipVersionBump) {
    if ($BumpVersion) {
        Write-Host "=== Version bump (Bump-KnodeVersion.ps1) ===" -ForegroundColor Cyan
        & $bumpScript
    }
    else {
        $a = Read-Host "Run version bump before build? (updates Knode.csproj + Knode.iss) [y/N]"
        if ($a -match '^[yY]') {
            Write-Host "=== Version bump (Bump-KnodeVersion.ps1) ===" -ForegroundColor Cyan
            & $bumpScript
        }
    }
}

Write-Host "`n=== dotnet build (Release) ===" -ForegroundColor Cyan
dotnet build $dotnetBuild -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n=== Publish + Inno (build-installer.ps1) ===" -ForegroundColor Cyan
& $buildInstaller -InnoPath $InnoPath
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`nDone. Installer (if Inno found): dotnet\dist-installer\KnodeSetup-*.exe" -ForegroundColor Green
