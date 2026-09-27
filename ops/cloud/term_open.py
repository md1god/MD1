"""
F-2 step 2: get a shell on the cloud box.

`tools.exec.host = "gateway"` in the cloud's own config, and the Gateway exposes
a `terminal.open` RPC that needs `cols`. This opens one and prints exactly what
comes back, so the next step (the binary hub protocol) is built from the real
answer rather than from the minified client.
"""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cloudlink  # noqa: E402
from openclaw_ws import GatewayError  # noqa: E402

gw = cloudlink.link()
try:
    print("== system.info ==")
    print(json.dumps(gw.call("system.info", {}, timeout=30), indent=2))

    print("\n== exec.approvals.get ==")
    try:
        print(json.dumps(gw.call("exec.approvals.get", {}, timeout=30), indent=2)[:2500])
    except GatewayError as e:
        print("ERR", e)

    print("\n== terminal.open {cols,rows} ==")
    try:
        r = gw.call("terminal.open", {"cols": 120, "rows": 40}, timeout=60)
        print(json.dumps(r, indent=2, default=str)[:3000])
    except GatewayError as e:
        print("ERR", e)

    print("\n== terminal.list ==")
    try:
        print(json.dumps(gw.call("terminal.list", {}, timeout=30), indent=2, default=str)[:2000])
    except GatewayError as e:
        print("ERR", e)
finally:
    gw.close()
