"""
F-2 step 0c: get the Gateway's *complete* validation complaint, and walk the
connect params up from {} until the schema is satisfied.

The matrix run showed one identical error for every (id, mode, device) combo,
including values lifted verbatim from the deployed bundle's own enum tables.
That means the printed paths are not the whole story, so: print the error
untruncated, then grow the payload one field at a time.
"""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from openclaw_ws import Gateway, GatewayError, SCOPES  # noqa: E402

HOST = sys.argv[1] if len(sys.argv) > 1 else "886841a9.openclaw.runware.run"


def raw_connect(params, token=None, timeout=30):
    """Send `connect` and return (ok, full_error_text)."""
    try:
        with Gateway(host=HOST, token=token) as gw:
            gw.hello = gw.call("connect", params, timeout=timeout)
            return True, None
    except GatewayError as e:
        return False, str(e)
    except Exception as e:  # noqa: BLE001
        return False, f"{type(e).__name__}: {e}"


STEPS = [
    ("empty object", {}),
    ("protocol only", {"minProtocol": 4, "maxProtocol": 4}),
    ("+client.id", {"minProtocol": 4, "maxProtocol": 4, "client": {"id": "cli"}}),
    ("+client.mode", {"minProtocol": 4, "maxProtocol": 4,
                      "client": {"id": "cli", "mode": "cli"}}),
    ("+client.version", {"minProtocol": 4, "maxProtocol": 4,
                         "client": {"id": "cli", "mode": "cli", "version": "1.0.0"}}),
    ("+client.platform", {"minProtocol": 4, "maxProtocol": 4,
                          "client": {"id": "cli", "mode": "cli", "version": "1.0.0",
                                     "platform": "linux"}}),
    ("+client.instanceId", {"minProtocol": 4, "maxProtocol": 4,
                            "client": {"id": "cli", "mode": "cli", "version": "1.0.0",
                                       "platform": "linux", "instanceId": "agentF"}}),
    ("+role", {"minProtocol": 4, "maxProtocol": 4,
               "client": {"id": "cli", "mode": "cli", "version": "1.0.0",
                          "platform": "linux", "instanceId": "agentF"},
               "role": "operator"}),
    ("+scopes", {"minProtocol": 4, "maxProtocol": 4,
                 "client": {"id": "cli", "mode": "cli", "version": "1.0.0",
                            "platform": "linux", "instanceId": "agentF"},
                 "role": "operator", "scopes": SCOPES}),
    ("+device {}", {"minProtocol": 4, "maxProtocol": 4,
                    "client": {"id": "cli", "mode": "cli", "version": "1.0.0",
                               "platform": "linux", "instanceId": "agentF"},
                    "role": "operator", "scopes": SCOPES, "device": {}}),
    ("full, no auth", {"minProtocol": 4, "maxProtocol": 4,
                       "client": {"id": "cli", "mode": "cli", "version": "1.0.0",
                                  "platform": "linux", "instanceId": "agentF"},
                       "role": "operator", "scopes": SCOPES, "device": {},
                       "caps": ["tool-events"], "auth": {},
                       "userAgent": "agentF/1.0", "locale": "en"}),
]

print(f"host = {HOST}\n")
for label, params in STEPS:
    ok, err = raw_connect(params)
    mark = "OK " if ok else "ERR"
    print(f"[{mark}] {label}")
    if err:
        print(f"       {err}")
    print()
    if ok:
        print("  ^ schema satisfied. Stopping here.")
        break
