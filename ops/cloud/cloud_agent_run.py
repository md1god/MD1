"""
F-2 step 4: ask the cloud agent to run the GitHub test, and stream back exactly
what happened on its box.

Why go through chat.send: the Gateway exposes no direct exec RPC (`terminal.open`
answers "terminal is disabled"), and the cloud agent already has exec tools
configured with host=gateway. This is the minimal inference needed to get real
command output -- see ops/cloud/runware_free_models.md for why it is metered and
what it costs.

Safety rails, taken from the task brief:
  * clone into /tmp only, never into the operator's repo
  * no push, no remote writes
  * any commit happens on a throwaway local branch `fleet/agent-F`
  * no secret is ever written into the cloned repo

Prints the raw event stream so the transcript is evidence, not a summary.
"""
import json
import os
import sys
import time
import uuid

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cloudlink  # noqa: E402
from openclaw_ws import GatewayError  # noqa: E402

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "cloud_agent_run.json")

SESSION = "agent:main:main"

PROMPT = """You are running inside a Linux container. Run the shell commands below and paste the RAW output of each one, each under its own numbered heading. Do not summarise, do not skip any step, and if a command fails print the error text.

1. ENVIRONMENT
   uname -a; echo "---"; cat /etc/os-release | head -4; echo "---"; id; echo "---"; pwd

2. TOOLS PRESENT
   for t in git curl wget python3 node npm bash sh tar unzip; do printf "%s: " "$t"; command -v "$t" || echo MISSING; done
   git --version 2>&1; node --version 2>&1

3. NETWORK TO GITHUB
   curl -sS -m 25 -o /dev/null -w "github.com http=%{http_code} time=%{time_total}\n" https://github.com 2>&1
   curl -sS -m 25 -o /dev/null -w "codeload http=%{http_code}\n" https://codeload.github.com/md1god/MD1/tar.gz/refs/heads/main 2>&1

4. CLONE THE PUBLIC REPO INTO /tmp (throwaway, never push)
   rm -rf /tmp/fleetF && cd /tmp && git clone --depth 1 https://github.com/md1god/MD1.git fleetF 2>&1 && echo CLONE_OK
   ls -a /tmp/fleetF | head -20
   cd /tmp/fleetF && git log --oneline -3 2>&1 && git remote -v

5. WRITE + COMMIT ON A THROWAWAY LOCAL BRANCH (no push, no remote write)
   cd /tmp/fleetF && git checkout -b fleet/agent-F 2>&1 && echo BRANCH_OK
   printf 'agent F cloud probe\\n' > ops_cloud_probe.md
   git add ops_cloud_probe.md && git -c user.email=fleet@local -c user.name=fleet commit -m 'fleet: agent F write+commit probe' 2>&1 && echo COMMIT_OK
   cd /tmp/fleetF && git log --oneline -2 && echo "current branch: $(git branch --show-current)" && git status --short && echo "STATUS_CLEAN_IF_EMPTY_ABOVE"

6. DISK / CPU / MEMORY
   df -h /tmp /home 2>&1 | head -6; nproc; free -m 2>&1 | head -3

7. UNITY
   for u in unity unity-editor Unity unityhub; do printf "%s: " "$u"; command -v "$u" || echo MISSING; done
   ls /opt 2>&1 | head; ls ~/Unity 2>&1 | head -3; find / -maxdepth 4 -iname "Unity" -type f 2>/dev/null | head -3; echo UNITY_CHECK_DONE

8. THE EXISTING AGENT WORKSPACE
   ls -a /home/openclaw/.openclaw/workspace 2>&1 | head -25
   cd /home/openclaw/.openclaw/workspace && git log --oneline -5 2>&1 | head -8; git remote -v 2>&1 | head -4

Finish with a one-line verdict: GIT_CLONE=OK|FAIL WRITE_COMMIT=OK|FAIL UNITY=YES|NO"""


def main():
    gw = cloudlink.link()
    run = {}
    try:
        print("== balance before ==")
        try:
            print(json.dumps(gw.call("accountManagement", {
                "operation": "getDetails"}, timeout=30), indent=2)[:400])
        except GatewayError as e:
            print("  (not a gateway RPC:", str(e)[:120], ")")

        key = f"agentF-{uuid.uuid4().hex[:10]}"
        print(f"\n== chat.send -> {SESSION} ==")
        res = gw.call("chat.send", {"sessionKey": SESSION, "message": PROMPT,
                                    "deliver": False, "idempotencyKey": key},
                      timeout=90)
        print(json.dumps(res, indent=2, default=str)[:800])
        run["send"] = res

        # stream events until the run finishes or we hit the wall
        deadline = time.time() + 900
        seen = []
        print("\n== event stream ==")
        while time.time() < deadline:
            gw.sock.settimeout(20)
            try:
                raw = gw.recv_text()
            except Exception as e:  # noqa: BLE001
                if "timed out" in str(e).lower() or isinstance(e, OSError):
                    continue
                print(f"  <stream stopped: {type(e).__name__}: {e}>")
                break
            try:
                msg = json.loads(raw)
            except Exception:
                continue
            if msg.get("type") != "event":
                continue
            ev, pl = msg.get("event"), msg.get("payload") or {}
            seen.append({"event": ev, "payload": pl})
            if ev in ("chat.message", "session.tool", "chat.status", "health"):
                txt = json.dumps(pl, default=str)
                if ev != "health":
                    print(f"  [{ev}] {txt[:600]}")
            if ev == "chat.message" and pl.get("role") == "assistant":
                body = pl.get("text") or pl.get("content") or ""
                if body and len(body) > 50:
                    print("\n---- ASSISTANT MESSAGE ----")
                    print(body[:6000])
                    print("---- END ----\n")
            # stop when a run finishes
            if ev in ("chat.status",) and pl.get("status") in ("idle", "done", "ok", "error"):
                break
        run["events"] = seen[-400:]
    finally:
        gw.close()

    with open(OUT, "w", encoding="utf-8") as fh:
        json.dump(run, fh, indent=2, ensure_ascii=False, default=str)
    print(f"\n[saved] cloud_agent_run.json ({len(run.get('events', []))} events)")


if __name__ == "__main__":
    sys.exit(main())
