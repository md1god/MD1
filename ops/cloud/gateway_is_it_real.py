"""
F-2 step 0d: is this endpoint actually an OpenClaw Gateway, or a static shim?

Evidence so far: `connect` returns a byte-identical error for {} and for a fully
populated payload, so the message is canned rather than schema-derived.

This sends deliberately invalid method names, invalid params, and garbage
frames, and prints the raw server text for each. A real router answers
"unknown method" per-method and echoes the request id; a stub answers the same
thing to everything.
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from openclaw_ws import Gateway  # noqa: E402

HOST = sys.argv[1] if len(sys.argv) > 1 else "886841a9.openclaw.runware.run"

print(f"host = {HOST}\n")

# --- 1. open ONE socket, fire many methods, dump whatever comes back -------
gw = Gateway(host=HOST)
gw._open()
head = gw._handshake()
print("== upgrade response ==")
for line in head.split("\r\n"):
    if line:
        print("   ", line[:160])
print()

METHODS = [
    ("connect", {"minProtocol": 4, "maxProtocol": 4,
                 "client": {"id": "openclaw-control-ui", "version": "control-ui",
                            "platform": "linux", "mode": "ui", "instanceId": "x"},
                 "role": "operator",
                 "scopes": ["operator.admin", "operator.read", "operator.write",
                            "operator.approvals", "operator.pairing"],
                 "device": {"id": "d", "publicKey": "p", "signature": "s",
                            "signedAt": 0, "nonce": ""},
                 "caps": ["tool-events"], "auth": {"token": "x"},
                 "userAgent": "x", "locale": "en"}),
    ("this.method.does.not.exist", {}),
    ("system.info", {}),
    ("ping", {}),
    ("", {}),
    ("connect", "not-an-object"),
    ("exec.run", {"command": "id"}),
]

for method, params in METHODS:
    rid = gw._next_id()
    frame = {"type": "req", "id": rid, "method": method, "params": params}
    try:
        gw.send_text(json.dumps(frame))
        deadline = time.time() + 20
        while time.time() < deadline:
            gw.sock.settimeout(max(0.5, deadline - time.time()))
            try:
                raw = gw.recv_text()
            except Exception as e:  # noqa: BLE001
                raw = f"<{type(e).__name__}: {e}>"
                break
            print(f"--> method={method!r} params={type(params).__name__}")
            print(f"<-- {raw[:700]}")
            try:
                m = json.loads(raw)
                print(f"    id match: {m.get('id') == rid}  type={m.get('type')!r}")
            except Exception:
                pass
            break
    except Exception as e:  # noqa: BLE001
        print(f"--> method={method!r} raised {type(e).__name__}: {e}")
    print()

gw.close()

# --- 2. does a non-WS HTTP POST to the same path behave differently? --------
import ssl  # noqa: E402
import urllib.request  # noqa: E402
for path, body in (("/", '{"type":"req","id":"1","method":"system.info","params":{}}'),
                   ("/rpc", '{"method":"system.info"}'),
                   ("/api/rpc", '{"method":"system.info"}')):
    req = urllib.request.Request(f"https://{HOST}{path}", data=body.encode(),
                                 headers={"Content-Type": "application/json"},
                                 method="POST")
    try:
        with urllib.request.urlopen(req, timeout=20) as r:
            print(f"POST {path:10} -> {r.status} {r.read()[:200]!r}")
    except Exception as e:  # noqa: BLE001
        code = getattr(getattr(e, "response", None), "status", None) or getattr(e, "code", "?")
        print(f"POST {path:10} -> {code} {str(e)[:120]}")
