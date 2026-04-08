<#
.SYNOPSIS
  Copies Cursor agent transcript .jsonl files that mention this repo into scripts/chat-backups/.

.DESCRIPTION
  Scans %USERPROFILE%\.cursor\projects\**\agent-transcripts\**\*.jsonl and copies files whose
  content matches -MatchPattern (default: KindleNotesAgent). Filenames are prefixed with the
  Cursor project slug and chat UUID so copies from different workspaces do not collide.
#>
[CmdletBinding()]
param(
    [string]$CursorProjectsRoot = "",
    [string]$MatchPattern = "KindleNotesAgent",
    [string]$OutputRoot = "",
    [switch]$WhatIf
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $scriptDir "chat-backups"
}
if ([string]::IsNullOrWhiteSpace($CursorProjectsRoot)) {
    $CursorProjectsRoot = Join-Path $env:USERPROFILE ".cursor\projects"
}

function Get-ProjectSlugFromPath {
    param([string]$FullPath)
    $normalized = $FullPath -replace '/', '\'
    if ($normalized -match '\\projects\\([^\\]+)\\agent-transcripts\\') {
        return $Matches[1]
    }
    return "unknown-project"
}

$runStamp = Get-Date -Format "yyyy-MM-dd_HHmmss"
$destDir = Join-Path $OutputRoot $runStamp

$allJsonl = Get-ChildItem -Path $CursorProjectsRoot -Recurse -File -Filter "*.jsonl" -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '[\\/]agent-transcripts[\\/]' }

$copied = @()

foreach ($file in $allJsonl) {
    $hit = $null
    try {
        $hit = Select-String -Path $file.FullName -SimpleMatch -Pattern $MatchPattern -Quiet -ErrorAction Stop
    }
    catch {
        continue
    }
    if (-not $hit) {
        continue
    }

    $slug = Get-ProjectSlugFromPath $file.FullName
    $chatId = $file.Directory.Name
    $destName = "{0}__{1}.jsonl" -f $slug, $chatId
    $destPath = Join-Path $destDir $destName

    $item = [pscustomobject]@{
        Source           = $file.FullName
        Destination      = $destPath
        Length           = $file.Length
        LastWriteUtc     = $file.LastWriteTimeUtc.ToString("o")
        CursorProjectDir = $slug
        ChatId           = $chatId
    }

    if ($WhatIf) {
        $copied += $item
        continue
    }

    if (-not (Test-Path $destDir)) {
        New-Item -ItemType Directory -Path $destDir -Force | Out-Null
    }
    Copy-Item -LiteralPath $file.FullName -Destination $destPath -Force
    $copied += $item
}

$manifestPath = Join-Path $destDir "manifest.json"
if (-not $WhatIf -and $copied.Count -gt 0) {
    $copied | ConvertTo-Json -Depth 3 | Set-Content -Path $manifestPath -Encoding UTF8
}

[pscustomobject]@{
    CursorProjectsRoot = $CursorProjectsRoot
    Pattern            = $MatchPattern
    BackupFolder       = $destDir
    FilesCopied        = $copied.Count
    FilesInspected     = $allJsonl.Count
}

if ($copied.Count -eq 0) {
    Write-Warning "No transcripts matched '$MatchPattern' under $CursorProjectsRoot"
}
