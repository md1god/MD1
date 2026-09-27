"""
Ask the cloud agent a question and stream back the raw result.

Reusable driver for the rest of F-2. Reads the prompt from a file, sends it with
chat.send to a session, then streams events until the run settles. Writes both a
JSON event log and a plain-text transcript so the output is evidence.

Costs inference on Runware (metered -- see runware_free_models.md). Keep prompts
small and never re-clone: the repo is already at /tmp/fleetF on the cloud.

Usage:  python ask_cloud.py <prompt-file> [--session KEY] [--wait SECONDS]
"""
import json
import os
import sys
import time
import uuid

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cloudlink  # noqa: E402
from openclaw_ws import GatewayError  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))


def flatten(content):
    parts = []
    if isinstance(content, str):
        parts.append(content)
    elif isinstance(content, list):
        for b in content:
            if isinstance(b, dict):
                if b.get("type") == "text":
                    parts.append(b.get("text", ""))
                else:
                    parts.append(f"[{b.get('type')}] "
                                 f"{json.dumps(b, default=str)[:1200]}")
    return "\n".join(parts)


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    opts = dict(a[2:].split("=", 1) for a in sys.argv[1:] if a.startswith("--"))
    prompt = open(args[0], "r", encoding="utf-8").read()
    session = opts.get("session", "agent:main:main")
    wait = int(opts.get("wait", "900"))
    tag = os.path.splitext(os.path.basename(args[0]))[0]

    gw = cloudlink.link()
    log = []
    try:
        res = gw.call("chat.send", {"sessionKey": session, "message": prompt,
                                    "deliver": False,
                                    "idempotencyKey": f"{tag}-{uuid.uuid4().hex[:8]}"},
                      timeout=90)
        print("SEND:", json.dumps(res, default=str)[:400], flush=True)
        log.append({"kind": "send", "payload": res})

        deadline = time.time() + wait
        last_ts = 0
        while time.time() < deadline:
            gw.sock.settimeout(25)
            try:
                raw = gw.recv_text()
            except Exception as e:  # noqa: BLE001
                if isinstance(e, OSError) or "timed out" in str(e).lower():
                    continue
                log.append({"kind": "stream_error", "text": f"{type(e).__name__}: {e}"})
                break
            try:
                msg = json.loads(raw)
            except Exception:
                continue
            if msg.get("type") != "event":
                continue
            ev, pl = msg.get("event"), msg.get("payload") or {}
            if ev == "health":
                continue
            log.append({"kind": "event", "event": ev, "payload": pl})
            if ev == "chat.message":
                ts = pl.get("timestamp") or 0
                if ts and ts <= last_ts:
                    continue
                last_ts = ts
                body = flatten(pl.get("content"))
                if body.strip():
                    print(f"\n===== {pl.get('role')} =====", flush=True)
                    print(body[:12000], flush=True)
    finally:
        gw.close()

    with open(os.path.join(HERE, f"{tag}_events.json"), "w", encoding="utf-8") as fh:
        json.dump(log, fh, indent=2, ensure_ascii=False, default=str)
    print(f"\n[saved] {tag}_events.json  ({len(log)} records)", flush=True)


if __name__ == "__main__":
    sys.exit(main())
