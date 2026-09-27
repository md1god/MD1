"""
F-2 step 1: what IS this cloud box, and what can it do?

Dumps the Gateway's own view of the host (system.info), its config, its nodes,
channels and sessions, then probes the RPC surface for a way to run a command.
Read-only. Costs $0.
"""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cloudlink  # noqa: E402
from openclaw_ws import GatewayError  # noqa: E402

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "cloud_probe.json")

PROBES = [
    ("system.info", {}),
    ("config.get", {"path": "gateway"}),
    ("config.get", {}),
    ("node.list", {}),
    ("channels.status", {}),
    ("sessions.list", {"limit": 5}),
    ("device.pair.list", {}),
    ("models.list", {}),
    ("skills.list", {}),
    ("plugins.list", {}),
    ("usage.status", {}),
    # ways one might run a command
    ("exec.approvals.get", {}),
    ("terminal.open", {}),
    ("terminal.list", {}),
    ("shell.exec", {"command": "id"}),
    ("exec.run", {"command": "id"}),
    ("fs.read", {"path": "/etc/os-release"}),
    ("debug.rpc", {"method": "system.info", "params": {}}),
]


def main():
    gw = cloudlink.link()
    res = {}
    try:
        h = gw.hello or {}
        snap = h.get("snapshot") or {}
        res["hello"] = {"keys": sorted(h.keys()),
                        "auth": {k: v for k, v in (h.get("auth") or {}).items()
                                 if k != "deviceToken"},
                        "protocol": h.get("protocol"),
                        "server": h.get("server")}
        res["snapshot_keys"] = sorted(snap.keys())
        res["snapshot"] = {k: snap[k] for k in sorted(snap.keys())
                           if k not in ("channels", "sessions", "agents")}

        for method, params in PROBES:
            key = method + (json.dumps(params) if params else "")
            try:
                r = gw.call(method, params, timeout=40)
                txt = json.dumps(r, default=str)
                res[key] = r if len(txt) < 6000 else txt[:6000] + "...[truncated]"
                print(f"[OK   ] {method:22} {txt[:220]}")
            except GatewayError as e:
                res[key] = f"ERR {e}"
                print(f"[ERR  ] {method:22} {str(e)[:200]}")
            except Exception as e:  # noqa: BLE001
                res[key] = f"EXC {type(e).__name__}: {e}"
                print(f"[EXC  ] {method:22} {type(e).__name__}: {str(e)[:160]}")
    finally:
        gw.close()

    with open(OUT, "w", encoding="utf-8") as fh:
        json.dump(res, fh, indent=2, ensure_ascii=False, default=str)
    print(f"\n[saved] cloud_probe.json")


if __name__ == "__main__":
    sys.exit(main())
