#!/usr/bin/env bash
# =============================================================================
#  OpenClaw cloud provisioning — DiDo111 CC-Game
#
#  Why this file exists
#  --------------------
#  The cloud gateway kept failing every turn with:
#     Sandbox image not found: ghcr.io/teith/openclaw-sandbox:v0.0.1
#  Its config shipped with:
#     agents.defaults.sandbox = { backend: "docker",
#                                 docker.image: "ghcr.io/teith/...",
#                                 mode: "all" }
#  The container has no Docker daemon, so that image can never be pulled.
#  "mode": "all" means EVERY turn needs it, so the agent never replies.
#
#  Fix: run the agent on-host.  sandbox.mode = "off"
#
#  Second, independent blocker (found 2026-09-27): every model returned
#  HTTP 400 and OpenClaw reported it as the generic
#     "provider rejected the request schema or tool payload"
#  The real Runware body was:
#     "External inference tasks require a paid invoice or at least $5 credit."
#  i.e. the account has no balance, so every *external* model 400s.
#  Only Runware-hosted models answer for free:
#     openai-gpt-oss-120b, qwen3.5-4b, qwen3.5-9b
#  So this script also pins agents.defaults.model.primary to a hosted one.
#  All 1M-context models (gpt-5-5-pro, gemini-3-5-flash, claude, grok, glm-5-2)
#  are external and need a top-up at https://my.runware.ai/wallet
#
#  This script is idempotent — safe to run on every container create.
# =============================================================================
set -uo pipefail

log()  { printf '\033[1;36m[openclaw-provision]\033[0m %s\n' "$*"; }
ok()   { printf '  \033[1;32mOK\033[0m   %s\n' "$*"; }
warn() { printf '  \033[1;33mWARN\033[0m %s\n' "$*"; }
die()  { printf '  \033[1;31mFAIL\033[0m %s\n' "$*"; exit 1; }

command -v openclaw >/dev/null 2>&1 || die "openclaw is not installed or not on PATH"

# Every openclaw call is wrapped in this. `openclaw config get` blocks forever
# on some hosts, and this script runs from postCreateCommand — an unbounded call
# there hangs Codespace creation with no output. Seconds are the unit.
CFG_TIMEOUT="${CFG_TIMEOUT:-30}"
AGENT_TIMEOUT="${AGENT_TIMEOUT:-120}"

if command -v timeout >/dev/null 2>&1; then
  oc() { local t="$1"; shift; timeout "$t" openclaw "$@" 2>&1; }
else
  warn "'timeout' not available - openclaw calls are unbounded"
  oc() { openclaw "$@" 2>&1; }
fi

# --- 1. what is the state right now? ---------------------------------------
log "current sandbox config:"
CFG="$(oc "$CFG_TIMEOUT" config file | tail -1)"
case "$CFG" in
  /*|./*|~*) ;;
  *) CFG="${OPENCLAW_CONFIG:-$HOME/.openclaw/openclaw.json}" ;;
esac

if [ -f "$CFG" ]; then
  sed -n '/"sandbox"/,/}/p' "$CFG" | sed 's/^/    /' || true
else
  warn "config file not found at $CFG"
fi

# --- 2. turn the sandbox off ------------------------------------------------
log "setting agents.defaults.sandbox.mode = off"
oc "$CFG_TIMEOUT" config set agents.defaults.sandbox.mode off | sed 's/^/    /' || true

# --- 2b. pin a model that actually answers without a paid balance ------------
# Every external model 400s on a zero-balance account. Hosted ones are free.
HOSTED_MODEL="${HOSTED_MODEL:-runware/openai-gpt-oss-120b}"
log "setting agents.defaults.model.primary = $HOSTED_MODEL"
oc "$CFG_TIMEOUT" config set agents.defaults.model.primary "$HOSTED_MODEL" | sed 's/^/    /' || true
oc "$CFG_TIMEOUT" config set agents.defaults.models "{\"runware/*\":{}}" >/dev/null 2>&1 || true

# --- 3. verify what actually landed on disk --------------------------------
# The file is the truth that survives a restart, so verify against the file and
# not `config get`. Scope the match to the sandbox block: the config also holds
# unrelated `"mode": "off"` keys (e.g. gateway.tailscale), so a plain grep over
# the whole file passes even when the sandbox is still on.
GOT=""
GOTMODEL=""
if command -v node >/dev/null 2>&1; then
  GOT="$(node -e '
    const fs = require("fs");
    try {
      const c = JSON.parse(fs.readFileSync(process.argv[1], "utf8"));
      const s = c?.agents?.defaults?.sandbox;
      process.stdout.write(s?.mode ?? "unset");
    } catch { process.stdout.write("unreadable"); }
  ' "$CFG" 2>/dev/null)"
  GOTMODEL="$(node -e '
    const fs = require("fs");
    try {
      const c = JSON.parse(fs.readFileSync(process.argv[1], "utf8"));
      process.stdout.write(c?.agents?.defaults?.model?.primary ?? "unset");
    } catch { process.stdout.write("unreadable"); }
  ' "$CFG" 2>/dev/null)"
fi
[ -n "$GOT" ] || GOT="unreadable"
[ -n "$GOTMODEL" ] || GOTMODEL="unreadable"

if [ "$GOT" = "off" ]; then
  ok "agents.defaults.sandbox.mode = off in $CFG"
elif [ "$GOT" = "unset" ] || [ "$GOT" = "unreadable" ]; then
  warn "could not read agents.defaults.sandbox.mode from $CFG"
  warn "if the gateway is still running, it has the old config in memory:"
  warn "    openclaw gateway restart   (or re-create the container)"
else
  die "sandbox.mode is '$GOT', expected 'off' in $CFG"
fi

case "$GOTMODEL" in
  openai-gpt-oss-120b|qwen3.5-4b|qwen3.5-9b|runware/*)
    ok "agents.defaults.model.primary = $GOTMODEL (hosted, no credit needed)" ;;
  *)
    warn "model.primary is '$GOTMODEL'"
    warn "external models 400 on a zero-balance Runware account:"
    warn "  'External inference tasks require a paid invoice or at least \$5 credit'"
    warn "use openai-gpt-oss-120b or qwen3.5-9b, or top up https://my.runware.ai/wallet" ;;
esac

# --- 3b. OpenCode CLI inside the container ---------------------------------
# The container is ephemeral: a rebuild wipes /usr/lib/node_modules and
# /home/openclaw/.openclaw. Without this the coding agent is gone again.
#
# No apiKey goes in this file on purpose. The container already carries a
# working OpenCode credential, and that credential is accepted by models the
# public API rejects with:
#     "OpenCode's free tier can only be used from within OpenCode"
# So big-pickle answers from in here and 401s from outside. Writing a key
# here would only break it.
if ! command -v opencode >/dev/null 2>&1; then
  log "installing OpenCode CLI"
  if command -v npm >/dev/null 2>&1; then
    npm install -g opencode-ai >/dev/null 2>&1 || warn "npm install -g opencode-ai failed"
  else
    warn "no npm on this host - skipping OpenCode CLI install"
  fi
fi

if command -v opencode >/dev/null 2>&1; then
  ok "opencode $(opencode --version 2>/dev/null | head -1)"
  OC_CFG_DIR="${OPENCLAW_CONFIG:-$HOME/.openclaw}"
  if [ ! -f "$OC_CFG_DIR/opencode.json" ]; then
    cat > "$OC_CFG_DIR/opencode.json" <<'JSON'
{
  "$schema": "https://opencode.ai/config.json",
  "model": "opencode/big-pickle",
  "provider": {
    "opencode": {
      "npm": "@ai-sdk/openai-compatible",
      "options": { "baseURL": "https://opencode.ai/zen/v1" }
    }
  },
  "permission": { "edit": "allow", "bash": "allow" }
}
JSON
    ok "wrote $OC_CFG_DIR/opencode.json"
  else
    ok "$OC_CFG_DIR/opencode.json already present"
  fi
else
  warn "opencode CLI unavailable - the coding sub-agent will not exist"
fi

# --- 4. prove the agent actually answers -----------------------------------
# This is the only check that matters. Config can look right and still fail.
log "live agent test (timeout ${AGENT_TIMEOUT}s - this is the real proof)"
OUT="$(oc "$AGENT_TIMEOUT" agent --agent main \
         --message 'Reply with exactly: PROVISION_OK')"
RC=$?

if [ "$RC" -eq 124 ]; then
  die "agent did not answer within ${AGENT_TIMEOUT}s"
fi

if printf '%s' "$OUT" | grep -q 'PROVISION_OK'; then
  ok "agent replied — the sandbox error is gone"
  echo
  log "cloud gateway is working. Nothing further to do."
  exit 0
fi

warn "agent still failing. First real error lines:"
printf '%s\n' "$OUT" \
  | grep -viE 'sqlite|tool-search|memory\]|trace:|gateway target|source:|bind:' \
  | tail -6 | sed 's/^/      /'
echo
warn "if the error is 'provider rejected the request schema or tool payload',"
warn "the model 400'd. Ask Runware what it actually said:"
warn "    curl -s -X POST https://api.runware.ai/v1/chat/completions \\"
warn "      -H \"Authorization: Bearer \$RUNWARE_API_KEY\" -H 'Content-Type: application/json' \\"
warn "      -d '{\"model\":\"<model>\",\"messages\":[{\"role\":\"user\",\"content\":\"hi\"}]}'"
warn "a zero-balance account answers every external model with"
warn "  'External inference tasks require a paid invoice or at least \$5 credit'"
exit 1
