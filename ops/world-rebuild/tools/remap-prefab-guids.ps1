# remap-prefab-guids.ps1
# =============================================================================
#  يعيد توجيه مراجع prefab داخل مشاهد العالم من الـ prefab الأصلية
#  (التي تحتوي آلاف الأضواء) إلى نسخها المحسّنة _nolight
#
#  آمن: لا يحذف شيئاً، يستبدل الـ guid فقط داخل PrefabInstance blocks
#  يُنفَّذ بعد LightFixer.cs
# =============================================================================
param(
  [string]$Project = "D:\DiDo111_CC-Game",
  [string]$MapFile = "ops\world-rebuild\reports\light-fix-map.txt"
)

$ErrorActionPreference = "Stop"
$mapPath = Join-Path $Project $MapFile
if (-not (Test-Path $mapPath)) { throw "map not found: $mapPath" }

$map = @{}
foreach ($line in (Get-Content $mapPath)) {
  if ($line -match '^([0-9a-f]{32})=([0-9a-f]{32})$') { $map[$Matches[1]] = $Matches[2] }
}
Write-Output ("guid pairs: " + $map.Count)
if ($map.Count -eq 0) { Write-Output "nothing to do"; exit 0 }

$scenes = Get-ChildItem "$Project\Assets\Scenes\World" -Filter *.unity -File
$total = 0

foreach ($s in $scenes) {
  $text = [System.IO.File]::ReadAllText($s.FullName)
  $before = $text.Length
  $hits = 0

  foreach ($old in $map.Keys) {
    $n = ([regex]::Matches($text, [regex]::Escape($old))).Count
    if ($n -gt 0) {
      $text = $text.Replace($old, $map[$old])
      $hits += $n
    }
  }

  if ($hits -gt 0) {
    [System.IO.File]::WriteAllText($s.FullName, $text, (New-Object System.Text.UTF8Encoding($false)))
    $total += $hits
    "{0,-26} remapped {1,7} refs" -f $s.Name, $hits
  } else {
    "{0,-26} (no refs)" -f $s.Name
  }
}
Write-Output ("TOTAL refs remapped: " + $total)
