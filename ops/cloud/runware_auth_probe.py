"""
F-1a: which key + which auth transport actually works against Runware?

Three candidate keys live in the operator key file. Test each against both
the legacy GraphQL gateway and the current REST /v1 endpoint. Report, do not guess.
READ-ONLY. No inference task is ever submitted (a submitted task COSTS MONEY).
"""
import json
import os
import re
import time
import urllib.error
import urllib.request

API_KEY_FILE = r"C:\Users\DiDo\Desktop\APi.txt"
OUT_DIR = os.path.dirname(os.path.abspath(__file__))
GRAPHQL = "https://api.runware.ai/v1/openapi/graphql"
REST_V1 = "https://api.runware.ai/v1"


def harvest_keys():
    """Collect every plausible secret from the file, with a label. Never print values."""
    blob = open(API_KEY_FILE, encoding="utf-8", errors="replace").read()
    lines = blob.splitlines()
    cands = []
    # label = the nearest preceding non-empty, non-secret line (Arabic or ascii context)
    for i, line in enumerate(lines):
        for tok in re.findall(r"\b[A-Za-z0-9][A-Za-z0-9_\-]{27,}\b", line):
            hexish = bool(re.fullmatch(r"[0-9a-fA-F]{64}", tok))
            prev = ""
            for j in range(i - 1, max(-1, i - 6), -1):
                s = lines[j].strip()
                if s and not re.fullmatch(r"[A-Za-z0-9_\-\.]{28,}", s):
                    prev = s
                    break
            cands.append({"token": tok, "line": i + 1, "len": len(tok),
                          "hex64": hexish, "context": prev[:60]})
    # dedupe by value
    seen, out = set(), []
    for c in cands:
        if c["token"] not in seen:
            seen.add(c["token"])
            out.append(c)
    return out


def http(url, method="POST", headers=None, body=None, timeout=60):
    req = urllib.request.Request(url, method=method, headers=headers or {},
                                 data=json.dumps(body).encode() if body is not None else None)
    last = None
    for attempt in range(1, 4):
        try:
            with urllib.request.urlopen(req, timeout=timeout) as r:
                return r.status, r.read().decode(errors="replace")
        except urllib.error.HTTPError as e:
            return e.code, e.read().decode(errors="replace")
        except Exception as e:  # noqa: BLE001
            last = f"{type(e).__name__}: {e}"
            time.sleep(2 * attempt)
    return 0, f"TRANSPORT {last}"


def try_graphql(key, style):
    hdr = {"Content-Type": "application/json", "User-Agent": "agentF/1.0"}
    vars_ = {}
    if style == "bearer":
        hdr["Authorization"] = "Bearer " + key
    elif style == "raw":
        hdr["Authorization"] = key
    elif style == "apikey":
        hdr["api-key"] = key
    # style 'var' passes it as a GraphQL variable instead of a header
    if style == "var":
        vars_["apiKey"] = key
    q = "{ modelList { id name type } }"
    body = [{"query": q, "variables": vars_}] if style == "var" else [{"query": q}]
    return http(GRAPHQL, headers=hdr, body=body)


def try_rest(key, style):
    """`getModelList` is a documented-free metadata task; it does not run inference."""
    hdr = {"Content-Type": "application/json", "User-Agent": "agentF/1.0"}
    if style == "bearer":
        hdr["Authorization"] = "Bearer " + key
    elif style == "raw":
        hdr["Authorization"] = key
    elif style == "apikey":
        hdr["api-key"] = key
    body = {"taskType": "getModelList"}
    return http(REST_V1, headers=hdr, body=body)


def brief(txt, n=260):
    return re.sub(r"\s+", " ", txt)[:n]


def main():
    keys = harvest_keys()
    print("=" * 78)
    print("F-1a  RUNWARE AUTH TRANSPORT DISCOVERY")
    print("=" * 78)
    print(f"candidates in {API_KEY_FILE}: {len(keys)}")
    for k in keys:
        print(f"  L{k['line']:<3} len={k['len']:<3} hex64={str(k['hex64']):<5} "
              f"head={k['token'][:4]}..tail={k['token'][-3:]}  ctx={k['context']!r}")
    print()

    winners = []
    for k in keys:
        key = k["token"]
        print("-" * 78)
        print(f"KEY L{k['line']} ({k['len']} chars, head={key[:4]})")
        for style in ("bearer", "raw", "apikey", "var"):
            st, txt = try_graphql(key, style)
            ok = st == 200 and "errors" not in txt
            print(f"   graphql/{style:<7} -> {st} {'OK' if ok else ''} {brief(txt) if not ok else 'no errors'}")
            if ok:
                winners.append(("graphql", key, style, txt))
        for style in ("bearer", "raw", "apikey"):
            st, txt = try_rest(key, style)
            ok = st == 200 and "errors" not in txt.lower() and "missingApiKey" not in txt
            print(f"   rest/v1/{style:<7} -> {st} {'OK' if ok else ''} {brief(txt) if not ok else 'no errors'}")
            if ok:
                winners.append(("rest", key, style, txt))
        time.sleep(1)

    print("=" * 78)
    print(f"WORKING COMBINATIONS: {len(winners)}")
    for w in winners:
        print(f"   {w[0]:<8} keyhead={w[1][:4]} style={w[2]}")
    print("=" * 78)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
