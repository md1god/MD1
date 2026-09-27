"""
Dump the Gateway's RAW response frames, so nothing is hidden by our own
unwrapping. Everything above returned `null`, which is suspicious -- either the
server nests its payload somewhere we are not reading, or these methods really
do answer null on this build.
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cloudlink  # noqa: E402

gw = cloudlink.link()
try:
    for method, params in [("system.info", {}),
                           ("node.list", {}),
                           ("sessions.list", {"limit": 3}),
                           ("channels.status", {}),
                           ("config.get", {})]:
        rid = gw._next_id()
        gw.send_text(json.dumps({"type": "req", "id": rid, "method": method,
                                 "params": params}))
        print(f"\n=== {method}  (request id {rid}) ===")
        deadline = time.time() + 30
        while time.time() < deadline:
            gw.sock.settimeout(max(0.5, deadline - time.time()))
            try:
                raw = gw.recv_text()
            except Exception as e:  # noqa: BLE001
                print(f"  <read stopped: {type(e).__name__}: {e}>")
                break
            print(f"  RAW {raw[:1500]}")
            try:
                m = json.loads(raw)
            except Exception:
                continue
            if m.get("type") == "res" and m.get("id") == rid:
                break
    print("\n=== buffered events ===")
    for ev in gw.events[:20]:
        print("  ", json.dumps(ev, default=str)[:300])
finally:
    gw.close()
