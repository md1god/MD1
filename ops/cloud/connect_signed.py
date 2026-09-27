"""
F-2 the actual gate: connect to the cloud Gateway with a properly signed device
identity, trying every candidate secret from the operator key file.

Each attempt prints the Gateway's verbatim verdict, so the outcome is measured
rather than assumed. Expect one of:
  CONNECTED                     -> we are in; F-2 can proceed
  AUTH_TOKEN_NOT_CONFIGURED     -> gateway runs without a shared secret
  AUTH_TOKEN_MISMATCH           -> wrong secret
  PAIRING_REQUIRED              -> signature valid, device unknown to gateway
  DEVICE_AUTH_*                 -> signature/device rejected

Costs $0: no model inference, connect is a local handshake.
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from openclaw_ws import Gateway, GatewayError  # noqa: E402
from ed25519_pure import make_identity  # noqa: E402
from gateway_probe import read_candidates, fp  # noqa: E402

HOST = sys.argv[1] if len(sys.argv) > 1 else "886841a9.openclaw.runware.run"
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "connect_result.json")

# one stable identity for this run, so a PAIRING_REQUIRED requestId can be
# approved later and then reused
IDENT = make_identity()
IDENT_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "device_identity.json")


def attempt(token):
    t0 = time.time()
    try:
        with Gateway(host=HOST, token=token, identity=IDENT) as gw:
            h = gw.hello or {}
            return {"ok": True, "ms": int((time.time() - t0) * 1000),
                    "hello": {k: v for k, v in h.items() if k != "snapshot"},
                    "snapshot_keys": sorted((h.get("snapshot") or {}).keys())}
    except GatewayError as e:
        return {"ok": False, "ms": int((time.time() - t0) * 1000), "error": str(e)[:500]}
    except Exception as e:  # noqa: BLE001
        return {"ok": False, "ms": int((time.time() - t0) * 1000),
                "error": f"{type(e).__name__}: {e}"[:500]}


def main():
    print(f"host    : {HOST}")
    print(f"deviceId: {IDENT['deviceId']}")
    print(f"identity: persisted to {os.path.basename(IDENT_PATH)}\n")

    rows = []
    # first: no secret at all, to learn whether the gateway is even secured
    for label, token in [("(no secret)", None)] + \
                       [(fp(t), t) for t in read_candidates()]:
        r = attempt(token)
        rows.append({"label": label, **r})
        status = "CONNECTED" if r["ok"] else "rejected"
        print(f"[{status:9}] {label:24} {r['ms']:>6}ms  "
              f"{r.get('error', '') or json.dumps(r.get('hello', {}))[:150]}")
        if r["ok"]:
            break

    with open(IDENT_PATH, "w", encoding="utf-8") as fh:
        json.dump(IDENT, fh, indent=2)
    with open(OUT, "w", encoding="utf-8") as fh:
        json.dump({"host": HOST, "deviceId": IDENT["deviceId"], "attempts": rows},
                  fh, indent=2, ensure_ascii=False)

    good = [r for r in rows if r["ok"]]
    print()
    if good:
        print("VERDICT: CONNECTED")
        print(json.dumps(good[0].get("hello", {}), indent=2)[:1200])
    else:
        print("VERDICT: NOT CONNECTED -- no secret in APi.txt opens the Gateway")
        seen = {r.get("error", "")[:90] for r in rows}
        for s in sorted(seen):
            print(f"   gateway said: {s}")
    return 0 if good else 1


if __name__ == "__main__":
    sys.exit(main())
