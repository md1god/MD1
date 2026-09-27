# F-2d: drive the OpenClaw cloud gateway over its documented WS protocol.
# Uses the locally installed `openclaw` CLI (v2026.9.6) via `gateway call`.
# Every call is wrapped in a hard timeout - the CLI hangs on --help in this env.
$ErrorActionPreference = 'Continue'
$cfgPath = "$env:USERPROFILE\.openclaw\openclaw.json"
$cfg = Get-Content $cfgPath -Raw | ConvertFrom-Json
$token = $cfg.gateway.auth.token
$pass  = $cfg.gateway.auth.password
$cloud = 'wss://886841a9.openclaw.runware.run/'
$dead  = 'wss://6c87e194.openclaw.runware.run/'

function Invoke-Claw {
    param([string[]]$ClawArgs, [int]$TimeoutSec = 60)
    $out = [System.IO.Path]::GetTempFileName()
    $err = [System.IO.Path]::GetTempFileName()
    $p = Start-Process -FilePath 'node' `
        -ArgumentList (@("$env:APPDATA\npm\node_modules\openclaw\openclaw.mjs") + $ClawArgs) `
        -RedirectStandardOutput $out -RedirectStandardError $err `
        -NoNewWindow -PassThru
    if (-not $p.WaitForExit($TimeoutSec * 1000)) {
        try { $p.Kill() } catch {}
        return @{ code = 'TIMEOUT'; out = (Get-Content $out -Raw); err = (Get-Content $err -Raw) }
    }
    return @{ code = $p.ExitCode; out = (Get-Content $out -Raw); err = (Get-Content $err -Raw) }
}

Write-Output "token present: $([bool]$token)  len=$($token.Length)"
Write-Output "password present: $([bool]$pass)"
Write-Output ("=" * 78)

# --- 0. is the dead one still dead? -------------------------------------
Write-Output "`n### 0. DEAD GATEWAY 6c87e194 (expect 503)"
try {
    $r = Invoke-WebRequest -Uri 'https://6c87e194.openclaw.runware.run/health' -TimeoutSec 20 -UseBasicParsing
    Write-Output "  health HTTP $($r.StatusCode) $($r.Content)"
} catch {
    Write-Output "  health FAILED: $($_.Exception.Message)"
}

# --- 1. live gateway health ---------------------------------------------
Write-Output "`n### 1. LIVE GATEWAY 886841a9 /health"
try {
    $r = Invoke-WebRequest -Uri 'https://886841a9.openclaw.runware.run/health' -TimeoutSec 20 -UseBasicParsing
    Write-Output "  health HTTP $($r.StatusCode) $($r.Content)"
} catch {
    Write-Output "  health FAILED: $($_.Exception.Message)"
}

# --- 2. system.info over the WS protocol --------------------------------
Write-Output "`n### 2. gateway call system.info  (cloud, with token)"
$r = Invoke-Claw -ClawArgs @('gateway','call','system.info','--url',$cloud,'--token',$token,'--json','--timeout','30') -TimeoutSec 90
Write-Output "  exit=$($r.code)"
if ($r.out) { Write-Output "  STDOUT: $($r.out.Trim())" }
if ($r.err) { Write-Output "  STDERR: $($r.err.Trim())" }

# --- 3. without a token, to see the failure mode -----------------------
Write-Output "`n### 3. gateway call system.info  (cloud, NO token - expect AUTH_* rejection)"
$r = Invoke-Claw -ClawArgs @('gateway','call','system.info','--url',$cloud,'--json','--timeout','30') -TimeoutSec 90
Write-Output "  exit=$($r.code)"
if ($r.out) { Write-Output "  STDOUT: $($r.out.Trim())" }
if ($r.err) { Write-Output "  STDERR: $($r.err.Trim())" }

# --- 4. dead gateway with the same token --------------------------------
Write-Output "`n### 4. gateway call system.info  (DEAD 6c87e194, same token)"
$r = Invoke-Claw -ClawArgs @('gateway','call','system.info','--url',$dead,'--token',$token,'--json','--timeout','30') -TimeoutSec 90
Write-Output "  exit=$($r.code)"
if ($r.out) { Write-Output "  STDOUT: $($r.out.Trim())" }
if ($r.err) { Write-Output "  STDERR: $($r.err.Trim())" }
