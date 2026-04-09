#Requires -Version 5.1
<#
  Single helper after you bump <Version> in Knode.csproj and MyAppVersion in Knode.iss and commit to main.

  Usage (repo root):
    powershell -ExecutionPolicy Bypass -File scripts/Push-KnodeReleaseTag.ps1

  Creates and pushes annotated tag vX.Y.Z matching the csproj. That triggers the "Knode installer" workflow
  which builds KnodeSetup-X.Y.Z.exe and attaches it to a GitHub Release.

  Prerequisites: git, remote origin, no existing tag vX.Y.Z on remote.
#>
param(
    [switch] $WhatIf
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$csproj = Join-Path $repoRoot "dotnet\Knode\Knode.csproj"
$iss = Join-Path $repoRoot "dotnet\installer\Knode.iss"
$rawCs = Get-Content $csproj -Raw
if ($rawCs -notmatch '<Version>([^<]+)</Version>') { throw "No <Version> in $csproj" }
$ver = $Matches[1].Trim()
$rawIss = Get-Content $iss -Raw
if ($rawIss -notmatch '#define MyAppVersion "([^"]+)"') { throw "No MyAppVersion in $iss" }
$issVer = $Matches[1].Trim()
if ($ver -ne $issVer) {
    throw "Version mismatch: csproj=$ver iss=$issVer. Align both files, commit, then re-run."
}

$tag = "v$ver"
$exists = git tag -l $tag
if ($exists) {
    throw "Tag $tag already exists locally. Delete it (git tag -d $tag) after fixing, or bump version."
}

Write-Host "Tag to create: $tag (Knode $ver)"
if ($WhatIf) {
    Write-Host "WhatIf: would run: git tag -a $tag -m ""Knode $ver""; git push origin $tag"
    exit 0
}

git tag -a $tag -m "Knode $ver"
git push origin $tag
Write-Host ""
Write-Host "Pushed $tag. Open Actions, workflow Knode installer, to watch progress. Release appears when the job finishes."
