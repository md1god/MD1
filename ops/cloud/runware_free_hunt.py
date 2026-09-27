"""
F-1e: definitive free-tier hunt + billing verification.

1. modelSearch paged to the end -> the WHOLE catalogue, with includeCost.
   GET /v1/models only exposed 48 TEXT models; the docs claim 437 total.
   Image/video/audio/3D models are where a "free" model would hide.
2. accountManagement/getUsageActivity over a date range -> hard evidence of
   whether this account is actually being charged, and for what.
Both are free metadata tasks. No inference is submitted.
"""
import json
import os
import re
import time
import urllib.error
import urllib.request
import uuid
from collections import Counter

API_KEY_FILE = r"C:\Users\DiDo\Desktop\APi.txt"
OUT_DIR = os.path.dirname(os.path.abspath(__file__))
REST_V1 = "https://api.runware.ai/v1"
KEY = re.search(r"anicw[A-Za-z0-9]{20,}",
                open(API_KEY_FILE, encoding="utf-8", errors="replace").read()).group(0)
HDR = {"Content-Type": "application/json", "Authorization": "Bearer " + KEY,
       "User-Agent": "agentF/1.0"}


def call(body, timeout=90, retries=4):
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


def main():
    print("=" * 78)
    print("F-1e  FULL CATALOGUE (paged) + BILLING VERIFICATION")
    print("=" * 78)

    # ---------- 1. page the whole modelSearch catalogue --------------------
    page = 1
    everything = []
    while page <= 12:
        body = [{"taskType": "modelSearch", "taskUUID": str(uuid.uuid4()),
                 "search": "", "limit": 500, "page": page, "includeCost": True}]
        st, data = call(body)
        errs = data.get("errors") or []
        items = None
        for d in (data.get("data") or []):
            if isinstance(d, dict):
                items = d.get("models") or d.get("results") or d.get("items") or d
                break
        if isinstance(items, dict):
            everything.append(items)
            items = items.get("models") or items.get("results") or []
        if not isinstance(items, list) or not items:
            print(f"[page {page}] HTTP {st} -> no list; errors="
                  f"{[e.get('code') for e in errs]}")
            if page == 1:
                print(json.dumps(data, indent=2, ensure_ascii=False)[:2000])
            break
        everything += items
        print(f"[page {page}] HTTP {st} -> {len(items)} models (running total {len(everything)})")
        if len(items) < 500:
            break
        page += 1
        time.sleep(0.4)

    # normalise whatever shape came back
    flat = []
    for m in everything:
        if isinstance(m, dict):
            flat.append(m)
    print(f"\nTOTAL CATALOGUE ENTRIES: {len(flat)}")
    if flat:
        print("[entry keys]", sorted(flat[0].keys()))
        with open(os.path.join(OUT_DIR, "catalogue_full.json"), "w", encoding="utf-8") as f:
            json.dump(flat, f, indent=2, ensure_ascii=False)
        print("[saved] catalogue_full.json")

        # hunt for anything that smells free
        print("\n--- free-hunting over the full catalogue ---")
        free_hits, zero_cost = [], []
        for m in flat:
            blob = json.dumps(m, ensure_ascii=False).lower()
            if "free" in blob:
                free_hits.append(m)
            pc = m.get("pricing") or m.get("cost") or {}
            nums = []
            if isinstance(pc, dict):
                for v in pc.values():
                    try:
                        nums.append(float(v))
                    except (TypeError, ValueError):
                        pass
            elif isinstance(pc, (int, float)):
                nums = [float(pc)]
            if nums and all(n == 0 for n in nums):
                zero_cost.append(m)
        print(f"  entries whose JSON contains 'free': {len(free_hits)}")
        for m in free_hits[:25]:
            print("     ", m.get("id") or m.get("name"),
                  json.dumps(m.get("pricing") or m.get("cost"), ensure_ascii=False))
        print(f"  entries where EVERY price field == 0: {len(zero_cost)}")
        for m in zero_cost[:25]:
            print("     ", m.get("id") or m.get("name"),
                  json.dumps(m.get("pricing") or m.get("cost"), ensure_ascii=False))
        if not free_hits and not zero_cost:
            print("  >>> NO ZERO-COST ENTRY ANYWHERE IN THE CATALOGUE <<<")

        # modality spread
        spread = Counter()
        for m in flat:
            t = str(m.get("type") or m.get("modality") or "?")
            spread[t] += 1
        print("\n  by type:", dict(spread))

    # ---------- 2. usage activity: is this account really charged? ---------
    print("\n" + "=" * 78)
    print("F-1e2  USAGE ACTIVITY (real billing evidence, free metadata)")
    print("=" * 78)
    body = [{"taskType": "accountManagement", "taskUUID": str(uuid.uuid4()),
             "operation": "getUsageActivity",
             "startDate": "2026-09-01", "endDate": "2026-09-28"}]
    st, data = call(body)
    print(f"HTTP {st}")
    with open(os.path.join(OUT_DIR, "usage_activity.json"), "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
    s = json.dumps(data, indent=2, ensure_ascii=False)
    print(s[:4000])

    # per-model cost roll-up, if present
    print("\n--- credit roll-up by model, as reported by the API ---")
    def walk(node, acc):
        if isinstance(node, dict):
            mid = node.get("model") or node.get("modelName") or node.get("id")
            cr = node.get("credits") if "credits" in node else node.get("cost")
            if mid is not None and isinstance(cr, (int, float)):
                acc[mid] = acc.get(mid, 0.0) + float(cr)
            for v in node.values():
                walk(v, acc)
        elif isinstance(node, list):
            for v in node:
                walk(v, acc)
    acc = {}
    walk(data, acc)
    total = sum(acc.values())
    for mid, c in sorted(acc.items(), key=lambda x: -x[1])[:20]:
        pct = (c / total * 100) if total else 0
        print(f"   {mid:<34} ${c:.6f}   {pct:5.1f}%")
    if total:
        print(f"   {'TOTAL':<34} ${total:.6f}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
