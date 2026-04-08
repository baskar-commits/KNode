#Requires -Version 5.1
<#
.SYNOPSIS
  Runs dependency vulnerability checks for KindleNotesAgent / Knode.

.DESCRIPTION
  - .NET: dotnet list package --vulnerable (NuGet advisory database)
  - Python: pip-audit against mvp/requirements.txt (locked via requirements.in) and spike requirements (PyPI/OSV)

  Install pip-audit once: python -m pip install pip-audit

  Does not replace: code review, pentest, signing, or antivirus scans of built installers.
#>
$ErrorActionPreference = "Stop"
$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

Set-Location (Join-Path $Root "dotnet")
$exitCode = 0

Write-Host "=== dotnet restore ===" -ForegroundColor Cyan
dotnet restore Knode.sln
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n=== NuGet: vulnerable packages (including transitive) ===" -ForegroundColor Cyan
dotnet list Knode.sln package --vulnerable --include-transitive
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n=== NuGet: outdated top-level packages (informational) ===" -ForegroundColor Cyan
dotnet list Knode.sln package --outdated

Set-Location $Root

$pipAudit = Get-Command pip-audit -ErrorAction SilentlyContinue
if (-not $pipAudit) {
    Write-Host "`n=== pip-audit not on PATH; skipping Python. Install: python -m pip install pip-audit ===" -ForegroundColor Yellow
} else {
    Write-Host "`n=== pip-audit: mvp/requirements.txt ===" -ForegroundColor Cyan
    Write-Host "(Ignoring CVE-2026-1839 on transformers until sentence-transformers supports transformers 5+; see SECURITY-AND-RELEASES.md.)" -ForegroundColor DarkGray
    pip-audit -r (Join-Path $Root "mvp\requirements.txt") --ignore-vuln CVE-2026-1839
    if ($LASTEXITCODE -ne 0) { $exitCode = 1 }
    Write-Host "`n=== pip-audit: spike/webview-notebook/requirements.txt ===" -ForegroundColor Cyan
    pip-audit -r (Join-Path $Root "spike\webview-notebook\requirements.txt")
    if ($LASTEXITCODE -ne 0) { $exitCode = 1 }
}

Write-Host "`nDone. Review docs/SECURITY-AND-RELEASES.md (Dependency and release checks)." -ForegroundColor Green
if ($exitCode -ne 0) {
    Write-Host "pip-audit reported issues (see above). Exit code 1." -ForegroundColor Yellow
}
exit $exitCode
