#Requires -Version 5.1
<#
  Publishes Knode for Windows x64, then runs Inno Setup when available.

  Usage (from repo):
    powershell -ExecutionPolicy Bypass -File dotnet/build-installer.ps1

  Prerequisites:
    - .NET 8 SDK
    - Inno Setup 6 (optional): https://jrsoftware.org/isdl.php
      Install so "ISCC.exe" is on PATH, or pass -InnoPath "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
#>
param(
    [string] $InnoPath = "ISCC.exe"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$csproj = Join-Path $PSScriptRoot "Knode\Knode.csproj"
$publishDir = Join-Path $PSScriptRoot "publish"
$iss = Join-Path $PSScriptRoot "installer\Knode.iss"

Write-Host "Publishing Knode -> $publishDir"
dotnet publish $csproj -c Release -r win-x64 --self-contained false -o $publishDir

if (-not (Test-Path $iss)) {
    Write-Warning "Missing $iss; skipping installer compile."
    exit 0
}

$inno = Get-Command $InnoPath -ErrorAction SilentlyContinue
if (-not $inno) {
    $msg = "Inno Setup compiler not found ($InnoPath). Published folder is ready at:`n  $publishDir`nInstall Inno Setup and re-run to build Setup.exe."
    Write-Warning $msg
    exit 0
}

Write-Host "Compiling installer with Inno Setup..."
& $InnoPath $iss
Write-Host "Done. Look under dotnet/dist-installer/"
