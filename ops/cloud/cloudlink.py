"""
Cloudlink: reach the OpenClaw Gateway on the Runware cloud box.

The gateway secret is NEVER stored in this repo. It is re-discovered at runtime
from the operator's key file by trying each candidate and keeping whichever the
Gateway accepts; only the masked fingerprint of the winner is cached, in
gateway_credential.json, so repeat runs try one secret instead of eleven.

Costs $0. Everything here is a local Gateway RPC -- no model inference.
"""
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from openclaw_ws import Gateway, GatewayError, DEFAULT_HOST  # noqa: E402
from ed25519_pure import make_identity  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
KEY_FILE = r"C:\Users\DiDo\Desktop\APi.txt"
IDENT_FILE = os.path.join(HERE, "device_identity.json")
CACHE_FILE = os.path.join(HERE, "gateway_credential.json")


def fp(tok):
    return f"{tok[:6]}..{tok[-4:]}(len={len(tok)})"


def _candidates():
    with open(KEY_FILE, "rb") as fh:
        text = fh.read().decode("utf-8", errors="replace")
    out, seen = [], set()
    for pat in (r"\b[0-9a-f]{64}\b", r"\b[A-Za-z0-9_\-]{40,64}\b"):
        for m in re.finditer(pat, text):
            t = m.group(0)
            if t not in seen:
                seen.add(t)
                out.append(t)
    return out


def _load_cache():
    try:
        with open(CACHE_FILE, "r", encoding="utf-8") as fh:
            return json.load(fh)
    except Exception:
        return {}


def _save_cache(data):
    with open(CACHE_FILE, "w", encoding="utf-8") as fh:
        json.dump(data, fh, indent=2)


def identity():
    """Stable device identity, created once and reused.

    Reuse matters: a fresh deviceId on every run is a different device to the
    Gateway. The private key stays on this machine and is not committed.
    """
    try:
        with open(IDENT_FILE, "r", encoding="utf-8") as fh:
            return json.load(fh)
    except Exception:
        ident = make_identity()
        with open(IDENT_FILE, "w", encoding="utf-8") as fh:
            json.dump(ident, fh, indent=2)
        return ident


def resolve_token(host=DEFAULT_HOST, force=False):
    """Return the working gateway secret, discovering it if necessary."""
    cands = _candidates()
    cache = _load_cache()
    order = []
    if not force and cache.get("token_fp"):
        order = [t for t in cands if fp(t) == cache["token_fp"]]
    order += [t for t in cands if t not in order]
    ident = identity()
    for tok in order:
        try:
            with Gateway(host=host, token=tok, identity=ident, timeout=45):
                _save_cache({"host": host, "token_fp": fp(tok),
                             "note": "secret lives in APi.txt, never in this repo"})
                return tok
        except Exception:
            continue
    raise GatewayError("no secret in APi.txt opens the gateway")


def link(host=DEFAULT_HOST, **kw):
    """Context-manager-ish helper: returns a connected Gateway."""
    tok = resolve_token(host)
    gw = Gateway(host=host, token=tok, identity=identity(), **kw)
    gw.connect()
    return gw


if __name__ == "__main__":
    tok = resolve_token(force="--force" in sys.argv)
    print("gateway secret resolved:", fp(tok))
    gw = link()
    try:
        print("hello:", json.dumps({k: v for k, v in (gw.hello or {}).items()
                                    if k != "snapshot"}, indent=2)[:800])
    finally:
        gw.close()
