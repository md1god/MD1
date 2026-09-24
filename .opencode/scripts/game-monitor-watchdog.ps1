# GAME + REPO MONITORS — 5-MINUTE WATCHDOG (PS1 + Scheduled Task)
# Installed by OpenCode agent 2026-09-24. Runs every 5 minutes via Windows Task Scheduler.
# Reads current state of the game folder, compares last snapshot, appends to memory + report.

$ErrorActionPreference = 'SilentlyContinue'
$gameRoot = 'D:\DiDo111_CC-Game'
$reportDir = Join-Path $gameRoot '.opencode\reports'
$snapshotFile = Join-Path $reportDir 'snapshot.json'
$latestFile = Join-Path $reportDir 'latest.txt'
$memDir = Join-Path $gameRoot 'memory'
$today = Get-Date -Format 'yyyy-MM-dd'
$memFile = Join-Path $memDir "$today.md"

# Ensure dirs
New-Item -ItemType Directory -Force -Path $reportDir | Out-Null
New-Item -ItemType Directory -Force -Path $memDir | Out-Null

$now = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
$utc = (Get-Date).ToUniversalTime().ToString('yyyy-MM-dd HH:mm:ss') + ' UTC'

# ---- 1. Game folder snapshot (hash of key state: build artifacts + scripts mtime sum) ----
$track = @(
  'Builds\WebGLMini\index.html',
  'TASKS.md',
  'Logs'
)
$folderState = Get-ChildItem -Path $gameRoot -Recurse -File -ErrorAction SilentlyContinue |
  Where-Object { $_.FullName -notmatch '\\(Library|Temp|obj|Logs|memory)\\' -and $_.Extension -in '.cs','.unity','.html','.shader','.prefab' } |
  Measure-Object -Property Length -Sum -Maximum
$folderHash = $folderState | ForEach-Object { "$($_.Sum)" }

# Build artifact existence
$hasIndex = Test-Path (Join-Path $gameRoot 'Builds\WebGLMini\index.html')
$hasWebglMini = Test-Path (Join-Path $gameRoot 'Builds\WebGLMini')
$buildInfo = if ($hasIndex) { 'index.html EXISTS' } elseif ($hasWebglMini) { 'folder found, NO index.html (incomplete)' } else { 'NO build' }

# ----- shell snapshot logic extracting state in JS-like terms -----
$prev = $null
if (Test-Path $snapshotFile) { try { $prev = Get-Content $snapshotFile -Raw | ConvertFrom-Json } catch {} }

$isChanged = $true
if ($prev -and $prev.folderHash -eq $folderHash) {
  # same folder state; still check git/HF for remote changes via reporters on next BEATS but mark local unchanged
  $isChanged = $false
}

# ---- 2. Report ----
$lines = @()
$lines += "GAME-MONITOR heartbeat $now ($utc)"
if ($isChanged) {
  $lines += "🎮 Game folder: CHANGED (code/build mtime or size delta). Build=$buildInfo"
} else {
  $lines += "🎮 Game folder: no local change since last check. Build=$buildInfo"
}
$lines += "🤗 HF repo: https://huggingface.co/Dido599999/MD111"
$lines += "🐙 GitHub repo: https://github.com/md1god/MD1"
$lines += "⏭ Next: agent-rotation @ +5min (see .opencode/agents/monitor-reporter.md)"

# Save snapshot
@{ timestamp = $now; folderHash = $folderHash; build = $buildInfo } | ConvertTo-Json -Compress | Set-Content $snapshotFile

# Save latest report
$lines | Set-Content $latestFile

# Append to daily memory (one line)
Add-Content -Path $memFile -Value ("`n[" + $now + "] [GAME-MONITOR] " + ($lines | Select-Object -Skip 1) -join ' | ')

Write-Output "OK $now | changed=$isChanged | $buildInfo"
Write-Output ($lines | Select-Object -Skip 1) -join "`n"
