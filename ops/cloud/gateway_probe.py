"""
F-2 step 0: can we authenticate to the OpenClaw Gateway on the cloud box,
and what does it say about its own host (OS, cores, disk, tooling)?

Read-only. Never mutates cloud state. Costs $0 -- system.info / config.get /
chat.history are local Gateway RPCs, no model inference.
"""
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from openclaw_ws import Gateway, GatewayError, DEFAULT_HOST  # noqa: E402

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "gateway_probe.json")


def read_candidates(path=r"C:\Users\DiDo\Desktop\APi.txt"):
    """Pull candidate gateway secrets out of the operator key file.

    Two 64-char hex tokens live there; one is the OpenClaw dashboard key. We
    never print a secret, only a 6/4 masked fingerprint so the report is
    reproducible without leaking anything.
    """
    with open(path, "rb") as fh:
        text = fh.read().decode("utf-8", errors="replace")
    toks, seen = [], set()
    for m in re.finditer(r"\b[0-9a-f]{64}\b", text):
        t = m.group(0)
        if t not in seen:
            seen.add(t)
            toks.append(t)
    for m in re.finditer(r"\b[A-Za-z0-9_\-]{32,64}\b", text):
        t = m.group(0)
        if t not in seen and not t.startswith("anicw") and "RUNWARE" not in t:
            seen.add(t)
            toks.append(t)
    return toks


def fp(tok):
    return f"{tok[:6]}..{tok[-4:]}(len={len(tok)})"


def try_token(tok, host):
    out = {"token_fp": fp(tok)}
    try:
        with Gateway(host=host, token=tok) as gw:
            h = gw.hello or {}
            out["ok"] = True
            out["auth"] = {k: v for k, v in (h.get("auth") or {}).items()
                           if k != "deviceToken"}
            snap = h.get("snapshot") or {}
            out["hello_top_keys"] = sorted(h.keys())
            out["snapshot_keys"] = sorted(snap.keys())[:40]
            out["gateway_version"] = (h.get("server") or {}).get("version") or h.get("version")
            out["protocol"] = h.get("protocol") or h.get("minProtocol")
            for name, params in (("system.info", {}),
                                 ("config.get", {"path": "gateway"}),
                                 ("node.list", {}),
                                 ("channels.status", {}),
                                 ("sessions.list", {"limit": 5})):
                try:
                    out[name] = gw.call(name, params, timeout=30)
                except GatewayError as e:
                    out[name] = f"ERR {e}"
                except Exception as e:  # noqa: BLE001
                    out[name] = f"ERR {type(e).__name__}: {e}"
    except GatewayError as e:
        out["ok"] = False
        out["error"] = str(e)[:400]
    except Exception as e:  # noqa: BLE001
        out["ok"] = False
        out["error"] = f"{type(e).__name__}: {e}"[:400]
    return out


def main():
    host = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_HOST
    results = {"host": host, "attempts": []}
    for tok in read_candidates():
        print(f"[try] {fp(tok)} ...", flush=True)
        r = try_token(tok, host)
        results["attempts"].append(r)
        print(f"       ok={r.get('ok')} {r.get('error','')}", flush=True)
        if r.get("ok"):
            break
    with open(OUT, "w", encoding="utf-8") as fh:
        json.dump(results, fh, indent=2, ensure_ascii=False, default=str)
    print(f"\n[saved] gateway_probe.json")
    good = [a for a in results["attempts"] if a.get("ok")]
    if not good:
        print("VERDICT: NO WORKING GATEWAY SECRET IN APi.txt")
        return 1
    g = good[0]
    print(f"VERDICT: CONNECTED with {g['token_fp']}")
    print(f"  gateway version : {g.get('gateway_version')}")
    si = g.get("system.info")
    print(f"  system.info     : {json.dumps(si, default=str)[:1200] if not isinstance(si, str) else si}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
