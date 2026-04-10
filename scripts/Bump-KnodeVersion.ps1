#Requires -Version 5.1
<#
.SYNOPSIS
  Bumps Knode version in Knode.csproj and Knode.iss (keeps them in sync).

.DESCRIPTION
  Uses three-part versions: Major.Minor.Patch (e.g. 0.2.1, 0.2.2, 0.3.0).

  - minor — patch bump (third number +1): 0.2.1 -> 0.2.2
  - major — "next minor line": second number +1, third becomes 0: 0.2.1 -> 0.3.0

  Two-part versions (e.g. 0.2) are read as 0.2.0 before bumping.

  Usage (repo root):
    powershell -ExecutionPolicy Bypass -File scripts/Bump-KnodeVersion.ps1
    powershell -ExecutionPolicy Bypass -File scripts/Bump-KnodeVersion.ps1 -WhatIf
#>
param(
    [switch] $WhatIf
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$csproj = Join-Path $repoRoot "dotnet\Knode\Knode.csproj"
$iss = Join-Path $repoRoot "dotnet\installer\Knode.iss"

function Get-VersionFromCsproj([string] $path) {
    $raw = Get-Content $path -Raw
    if ($raw -notmatch '<Version>([^<]+)</Version>') {
        throw "Could not find <Version> in $path"
    }
    return $Matches[1].Trim()
}

function Parse-SemVer([string] $s) {
    $p = $s.Trim() -split '\.'
    if ($p.Count -lt 2) {
        throw "Expected Major.Minor or Major.Minor.Patch, got: $s"
    }
    $maj = [int]$p[0]
    $min = [int]$p[1]
    $pat = if ($p.Count -ge 3) { [int]$p[2] } else { 0 }
    return @{ Major = $maj; Minor = $min; Patch = $pat; Original = $s }
}

function Format-SemVer($v) {
    return "$($v.Major).$($v.Minor).$($v.Patch)"
}

function Bump-Patch($v) {
    return @{ Major = $v.Major; Minor = $v.Minor; Patch = $v.Patch + 1 }
}

function Bump-MinorLine($v) {
    return @{ Major = $v.Major; Minor = $v.Minor + 1; Patch = 0 }
}

$current = Get-VersionFromCsproj $csproj
$issRaw = Get-Content $iss -Raw
if ($issRaw -notmatch '#define MyAppVersion "([^"]+)"') {
    throw "Could not parse MyAppVersion in Knode.iss"
}
$issVer = $Matches[1].Trim()
if ($issVer -ne $current) {
    throw "Mismatch: Knode.csproj has Version $current but Knode.iss has $issVer. Fix manually, then re-run."
}

$v = Parse-SemVer $current

Write-Host "Current version: $(Format-SemVer $v)" -ForegroundColor Cyan
Write-Host ""
Write-Host "Choose bump type:"
Write-Host '  minor — patch release (third number +1), e.g. 0.2.1 -> 0.2.2'
Write-Host '  major — next minor line (second number +1, patch 0), e.g. 0.2.1 -> 0.3.0'
Write-Host ""
$choice = Read-Host "Type minor or major"

$next = $null
switch -Regex ($choice.Trim()) {
    '^(?i)minor$' { $next = Bump-Patch $v }
    '^(?i)major$' { $next = Bump-MinorLine $v }
    default { throw "Type exactly 'minor' or 'major'." }
}

$newVer = Format-SemVer $next
Write-Host ""
Write-Host "New version: $newVer" -ForegroundColor Green

if ($WhatIf) {
    Write-Host "WhatIf: would update $csproj and $iss to $newVer"
    exit 0
}

$cs = Get-Content $csproj -Raw
$cs2 = [regex]::Replace($cs, '<Version>[^<]+</Version>', "<Version>$newVer</Version>", 1)
if ($cs -eq $cs2) { throw "Failed to replace <Version> in csproj" }
Set-Content -Path $csproj -Value $cs2 -NoNewline -Encoding utf8

$defineLine = '#define MyAppVersion "' + $newVer + '"'
$iss2 = [regex]::Replace($issRaw, '#define MyAppVersion "[^"]+"', $defineLine, 1)
if ($issRaw -eq $iss2) { throw "Failed to replace MyAppVersion in Knode.iss" }
Set-Content -Path $iss -Value $iss2 -NoNewline -Encoding utf8

Write-Host ""
Write-Host "Updated Knode.csproj and dotnet/installer/Knode.iss to $newVer"
Write-Host "Next: git diff, commit, push main, then scripts/Push-KnodeReleaseTag.ps1 (or push tag manually)."
