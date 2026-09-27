"""
F-1d: classify the harvested catalogue by cost, and ask Runware for the
account's own billing facts (free metadata tasks only).

No inference is submitted anywhere in this file.
"""
import json
import os
import re
import time
import urllib.error
import urllib.request
import uuid

API_KEY_FILE = r"C:\Users\DiDo\Desktop\APi.txt"
OUT_DIR = os.path.dirname(os.path.abspath(__file__))
REST_V1 = "https://api.runware.ai/v1"
KEY = re.search(r"anicw[A-Za-z0-9]{20,}",
                open(API_KEY_FILE, encoding="utf-8", errors="replace").read()).group(0)
HDR = {"Content-Type": "application/json", "Authorization": "Bearer " + KEY,
       "User-Agent": "agentF/1.0"}


def call(body, timeout=60, retries=4):
    req = urllib.request.Request(REST_V1, method="POST", headers=HDR,
                                 data=json.dumps(body).encode())
    last = None
    for a in range(1, retries + 1):
        try:
            with urllib.request.urlopen(req, timeout=timeout) as r:
                return r.status, json.loads(r.read().decode())
        except urllib.error.HTTPError as e:
            raw = e.read().decode(errors="replace")
            try:
                return e.code, json.loads(raw)
            except Exception:
                return e.code, {"raw": raw}
        except Exception as e:  # noqa: BLE001
            last = f"{type(e).__name__}: {e}"
            time.sleep(2 * a)
    return 0, {"transport_error": last}


def num(v):
    try:
        return float(v)
    except (TypeError, ValueError):
        return None


def main():
    models = json.load(open(os.path.join(OUT_DIR, "harvested_models.json"), encoding="utf-8"))
    print("=" * 78)
    print(f"F-1d  COST CLASSIFICATION over {len(models)} models")
    print("=" * 78)
    print("pricing units = USD per 1M tokens (OpenAI-compatible catalogue)\n")

    free, metered, partial = [], [], []
    for m in models:
        pr = m.get("pricing") or {}
        vals = {k: num(v) for k, v in pr.items()}
        billable = {k: v for k, v in vals.items()
                    if k not in ("image", "request") and v is not None and v > 0}
        any_free = any(v == 0 for v in vals.values() if v is not None)
        if not billable:
            free.append((m, vals))
        elif any_free:
            partial.append((m, vals))
        else:
            metered.append((m, vals))

    def fmt(vals):
        return "  ".join(f"{k}={v:g}" for k, v in sorted(vals.items()) if v is not None)

    print(f"### FULLY ZERO-COST (no billable field at all): {len(free)}")
    for m, v in sorted(free, key=lambda x: x[0]["id"]):
        print(f"    {m['id']:<40} {fmt(v)}")
    print(f"\n### FULLY METERED (every field > 0): {len(metered)}")
    for m, v in sorted(metered, key=lambda x: x[0]["id"]):
        print(f"    {m['id']:<40} {fmt(v)}")
    print(f"\n### MIXED (some fields free, some billed): {len(partial)}")
    for m, v in sorted(partial, key=lambda x: x[0]["id"]):
        print(f"    {m['id']:<40} {fmt(v)}")

    # ---- cheapest per-1k-token text rates -------------------------------
    print("\n### cheapest text models by blended $/1k tokens")
    print("    (blend = 0.3*prompt + 0.7*completion, then /1000)")
    rows = []
    for m in models:
        pr = m.get("pricing") or {}
        p, c = num(pr.get("prompt")), num(pr.get("completion"))
        if p is None:
            continue
        c = 0.0 if c is None else c
        rows.append(((0.3 * p + 0.7 * c) / 1000.0, m["id"], p, c))
    for per1k, mid, p, c in sorted(rows)[:8]:
        print(f"    ${per1k:.8f} / 1k   {mid:<40} prompt={p:g} completion={c:g}")

    # ---- account billing facts (free metadata) --------------------------
    print("\n" + "=" * 78)
    print("F-1d2  ACCOUNT BILLING FACTS (free metadata tasks)")
    print("=" * 78)
    for op in ("getDetails", "getUsageActivity", "getUsagePerformance", "getUsageErrors"):
        body = [{"taskType": "accountManagement", "taskUUID": str(uuid.uuid4()),
                 "operation": op}]
        st, data = call(body)
        print(f"\n--- accountManagement/{op} -> HTTP {st} ---")
        try:
            print(json.dumps(data, indent=2, ensure_ascii=False)[:1500])
        except Exception:
            print(str(data)[:1500])
        with open(os.path.join(OUT_DIR, f"acct_{op}.json"), "w", encoding="utf-8") as f:
            json.dump(data, f, indent=2, ensure_ascii=False)
        time.sleep(0.5)

    # ---- modelSearch with a valid uuid (free) ---------------------------
    print("\n" + "=" * 78)
    print("F-1d3  modelSearch (valid taskUUID) - full catalogue, free")
    print("=" * 78)
    body = [{"taskType": "modelSearch", "taskUUID": str(uuid.uuid4()),
             "search": "", "limit": 2000, "includeCost": True}]
    st, data = call(body)
    print(f"HTTP {st}")
    try:
        print(json.dumps(data, indent=2, ensure_ascii=False)[:2500])
    except Exception:
        print(str(data)[:2500])
    with open(os.path.join(OUT_DIR, "modelsearch_full.json"), "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
