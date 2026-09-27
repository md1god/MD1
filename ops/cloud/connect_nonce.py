"""
F-2 step 0e: complete the connect.challenge handshake and find out what the
Gateway still wants.

Sequence proven by gateway_is_it_real.py:
  1. client -> {"type":"req","method":"connect", params:{...}}
  2. server -> {"type":"event","event":"connect.challenge","payload":{"nonce":...}}
  3. client -> connect again, with that nonce in device.nonce

This walks the device object from absent -> {} -> nonce only -> nonce + id ->
nonce + id + publicKey -> nonce + id + publicKey + signature, printing the
verbatim error each time, so the required shape is measured rather than assumed.
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from openclaw_ws import Gateway, GatewayError  # noqa: E402

HOST = sys.argv[1] if len(sys.argv) > 1 else "886841a9.openclaw.runware.run"
TOKENS = sys.argv[2:] or [None]

SHAPES = [
    ("device omitted", None),
    ("device {}", {}),
    ("device {nonce}", "{nonce}"),
    ("device {id,nonce}", {"id": "agentF-cloud", "_nonce": 1}),
    ("device {id,publicKey,nonce}", {"id": "agentF-cloud", "publicKey": "x", "_nonce": 1}),
    ("device {id,publicKey,signature,signedAt,nonce}",
     {"id": "agentF-cloud", "publicKey": "x", "signature": "y",
      "signedAt": int(time.time() * 1000), "_nonce": 1}),
]


def build(shape, nonce):
    if shape is None:
        return None
    if shape == "{}":
        return {}
    if shape == "{nonce}":
        return {"nonce": nonce or ""}
    d = {k: v for k, v in shape.items() if not k.startswith("_")}
    d["nonce"] = nonce or ""
    return d


for token in TOKENS:
    for label, shape in SHAPES:
        try:
            with Gateway(host=HOST, token=token) as gw:
                gw.device = build(shape, None)
                # reconnect using the challenge-aware path
                gw.close()
                gw._open()
                gw._handshake()
                rid = gw._next_id()
                gw.send_text(json.dumps({"type": "req", "id": rid, "method": "connect",
                                         "params": gw._connect_params(None)}))
                seen = []

                def grab(m, _s=seen):
                    if m.get("event") == "connect.challenge":
                        _s.append((m.get("payload") or {}).get("nonce"))
                m1 = gw._pump_until(rid, time.time() + 30, collect=grab)
                nonce = seen[-1] if seen else None
                if not nonce:
                    print(f"[tok={str(token)[:8]}] {label:48} -> no challenge; "
                          f"{(m1.get('error') or {}).get('message','')[:160]}")
                    continue
                rid2 = gw._next_id()
                gw.send_text(json.dumps({"type": "req", "id": rid2, "method": "connect",
                                         "params": gw._connect_params(nonce)}))
                m2 = gw._pump_until(rid2, time.time() + 30)
                if m2.get("ok"):
                    print(f"[tok={str(token)[:8]}] {label:48} -> *** CONNECTED ***")
                    print(json.dumps(m2.get("result"), indent=2)[:1500])
                else:
                    e = m2.get("error") or {}
                    print(f"[tok={str(token)[:8]}] {label:48} -> {e.get('code')}: "
                          f"{e.get('message','')[:220]}")
        except GatewayError as e:
            print(f"[tok={str(token)[:8]}] {label:48} -> ERR {str(e)[:220]}")
        except Exception as e:  # noqa: BLE001
            print(f"[tok={str(token)[:8]}] {label:48} -> EXC {type(e).__name__}: {str(e)[:180]}")
    print()
