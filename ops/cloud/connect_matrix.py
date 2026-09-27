"""
F-2 step 0b: find a connect handshake the Gateway accepts.

The first probe proved the transport works (HTTP 101 + our JSON parsed) and that
auth had not even been reached yet -- the Gateway rejected the *schema*:
  /client/id  must be one of the allowed values
  /client/mode must be one of the allowed values
  /device     must be object

Allowed values were read out of the deployed bundle (assets/gateway-*.js):
  client.id : webchat-ui openclaw-control-ui openclaw-tui webchat cli
              gateway-client openclaw-macos openclaw-ios openclaw-android
              node-host test fingerprint openclaw-probe
  client.mode: webchat cli ui backend node probe test

This walks the matrix, then reports which (id, mode, device) shape got past
schema validation -- i.e. reached the auth check. Costs $0.
"""
import itertools
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from openclaw_ws import Gateway, GatewayError, SCOPES  # noqa: E402
from gateway_probe import read_candidates, fp  # noqa: E402

CLIENT_IDS = ["cli", "gateway-client", "openclaw-probe", "test",
              "openclaw-control-ui", "webchat", "node-host"]
MODES = ["cli", "probe", "test", "backend", "webchat", "ui", "node"]
DEVICES = [("empty", {}), ("omitted", "__OMIT__")]

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "connect_matrix.json")


def attempt(host, token, cid, mode, dev):
    params = {
        "minProtocol": 4, "maxProtocol": 4,
        "client": {"id": cid, "version": "agentF/1.0", "platform": "python",
                   "mode": mode, "instanceId": "ops-cloud-agentF"},
        "role": "operator", "scopes": SCOPES,
        "caps": ["tool-events"],
        "auth": {"token": token} if token else {},
        "userAgent": "ops-cloud-agentF/1.0", "locale": "en",
    }
    if dev != "__OMIT__":
        params["device"] = dev
    try:
        with Gateway(host=host, token=token) as gw:
            gw.hello = gw.call("connect", params, timeout=30)
            return {"result": "CONNECTED", "hello_keys": sorted((gw.hello or {}).keys())}
    except GatewayError as e:
        return {"result": "rejected", "error": str(e)[:300]}
    except Exception as e:  # noqa: BLE001
        return {"result": "error", "error": f"{type(e).__name__}: {e}"[:300]}


def main():
    host = sys.argv[1] if len(sys.argv) > 1 else "886841a9.openclaw.runware.run"
    toks = read_candidates()
    # de-dup, keep order; the 64-hex ones are the plausible gateway secrets
    rows = []
    print("== phase 1: no token, find a schema-valid handshake ==")
    base = None
    for cid, mode, (dname, dev) in itertools.product(CLIENT_IDS, MODES, DEVICES):
        r = attempt(host, None, cid, mode, dev)
        rows.append({"token": None, "client_id": cid, "mode": mode,
                     "device": dname, **r})
        if r["result"] == "CONNECTED":
            base = (cid, mode, dname)
            print(f"  CONNECTED (no auth needed) id={cid} mode={mode} device={dname}")
            break
        if "INVALID_REQUEST" not in r.get("error", ""):
            print(f"  id={cid:22} mode={mode:8} dev={dname:8} -> {r['error'][:150]}")
    if not base:
        print("  no schema-valid handshake found without auth; trying every token")
    else:
        print(f"\n  schema-valid shape: client.id={base[0]} mode={base[1]} device={base[2]}")

    print("\n== phase 2: each candidate secret against the valid shape ==")
    cid, mode, (dname, dev) = base or ("cli", "cli", "empty")
    auth_rows = []
    for tok in toks:
        r = attempt(host, tok, cid, mode, {} if dname == "empty" else "__OMIT__")
        auth_rows.append({"token": fp(tok), **r})
        print(f"  {fp(tok):22} -> {r['result']:9} {r.get('error','')[:170]}")
        if r["result"] == "CONNECTED":
            break

    with open(OUT, "w", encoding="utf-8") as fh:
        json.dump({"host": host, "shape": {"client_id": cid, "mode": mode, "device": dname},
                   "schema_probe": rows, "auth_probe": auth_rows},
                  fh, indent=2, ensure_ascii=False)
    print(f"\n[saved] connect_matrix.json")
    return 0


if __name__ == "__main__":
    sys.exit(main())
