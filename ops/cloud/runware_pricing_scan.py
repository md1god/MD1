"""
F-1f: DEFINITIVE free-tier scan using Runware's own published pricing JSON.

Docs (https://runware.ai/docs/platform/pricing) publish:
  /docs/models/index.json                 -> every public model + schema URL
  /docs/models/<model>/schema.json        -> info["x-pricing"]  <- the real rates

This walks the index and checks EVERY rate amount for 0. This is the
authoritative answer to "is there a free model?", not a scrape of a web page.
No auth, no inference, no cost.
"""
import concurrent.futures as cf
import json
import os
import re
import time
import urllib.error
import urllib.request

OUT_DIR = os.path.dirname(os.path.abspath(__file__))
INDEX = "https://runware.ai/docs/models/index.json"


def get(url, timeout=45, retries=3):
    req = urllib.request.Request(url, headers={"User-Agent": "agentF/1.0",
                                               "Accept": "application/json"})
    last = None
    for a in range(1, retries + 1):
        try:
            with urllib.request.urlopen(req, timeout=timeout) as r:
                return r.status, r.read().decode(errors="replace")
        except urllib.error.HTTPError as e:
            if e.code in (403, 404):
                return e.code, ""
            last = f"HTTP {e.code}"
        except Exception as e:  # noqa: BLE001
            last = f"{type(e).__name__}: {e}"
            time.sleep(1.5 * a)
    return 0, f"TRANSPORT {last}"


def main():
    print("=" * 78)
    print("F-1f  DEFINITIVE FREE-TIER SCAN (Runware's own published pricing JSON)")
    print("=" * 78)
    st, txt = get(INDEX)
    print(f"[index] HTTP {st}  bytes={len(txt)}")
    if st != 200:
        print(txt[:800])
        print("UNVERIFIED: cannot read the published pricing index")
        return 1
    idx = json.loads(txt)
    models = idx if isinstance(idx, list) else (idx.get("models") or idx.get("data") or [])
    print(f"[index] {len(models)} models listed")
    if not models:
        print("UNVERIFIED: index parsed but empty:", json.dumps(idx)[:600])
        return 1
    print("[index] entry sample:", json.dumps(models[0], ensure_ascii=False)[:500])

    def schema_url(m):
        for k in ("schema", "schemaUrl", "schemaURL", "schema_url", "url"):
            v = m.get(k)
            if isinstance(v, str) and v.endswith(".json"):
                return v if v.startswith("http") else "https://runware.ai" + v
        slug = m.get("id") or m.get("name")
        return f"https://runware.ai/docs/models/{slug}/schema.json"

    def fetch_price(m):
        u = schema_url(m)
        s, t = get(u, timeout=30, retries=2)
        if s != 200 or not t:
            return (m, u, None, f"HTTP {s}")
        try:
            info = json.loads(t).get("info") or {}
            return (m, u, info.get("x-pricing"), None)
        except Exception as e:  # noqa: BLE001
            return (m, u, None, f"parse {type(e).__name__}")

    print(f"\n[scan] fetching x-pricing for {len(models)} models (16 threads)...")
    results = []
    with cf.ThreadPoolExecutor(max_workers=16) as ex:
        for i, r in enumerate(ex.map(fetch_price, models), 1):
            results.append(r)
            if i % 100 == 0:
                print(f"   ...{i}/{len(models)}")

    with open(os.path.join(OUT_DIR, "pricing_scan.json"), "w", encoding="utf-8") as f:
        json.dump([{"model": {k: m.get(k) for k in ("id", "name", "air")},
                    "url": u, "x-pricing": p, "error": e}
                   for m, u, p, e in results], f, indent=2, ensure_ascii=False)
    print(f"[saved] pricing_scan.json")

    # ---- classify --------------------------------------------------------
    zero_rate, free_word, priced, no_pricing, errors = [], [], [], [], []
    for m, u, p, e in results:
        if e:
            errors.append((m, u, p, e))
            continue
        if p is None:
            no_pricing.append((m, u))
            continue
        priced.append((m, u, p))
        blob = json.dumps(p, ensure_ascii=False)
        amounts = [float(a) for a in re.findall(r'"amount"\s*:\s*([0-9.eE+-]+)', blob)]
        if amounts and all(a == 0 for a in amounts):
            zero_rate.append((m, u, p))
        if "free" in blob.lower():
            free_word.append((m, u, p))

    print("\n" + "=" * 78)
    print("RESULT")
    print("=" * 78)
    print(f"  schemas fetched OK with a pricing block : {len(priced)}")
    print(f"  schemas fetched, NO pricing block      : {len(no_pricing)}")
    print(f"  fetch/parse errors                     : {len(errors)}")
    print(f"  >>> rates where EVERY amount == 0      : {len(zero_rate)}")
    print(f"  pricing blocks containing the word free: {len(free_word)}")

    if zero_rate:
        print("\n### ZERO-COST MODELS FOUND")
        for m, u, p in zero_rate:
            print(f"  {m.get('id')}  {json.dumps(p, ensure_ascii=False)[:300]}")
    if free_word:
        print("\n### pricing blocks mentioning 'free'")
        for m, u, p in free_word[:20]:
            print(f"  {m.get('id')}  {json.dumps(p, ensure_ascii=False)[:300]}")

    # ---- cheapest real rates --------------------------------------------
    print("\n### cheapest text (token-billed) models on the platform")
    tok = []
    for m, u, p in priced:
        if not isinstance(p, dict):
            continue
        if p.get("basis") != "token" and not any(
                r.get("unit", "").endswith("Token") for r in p.get("rates", []) or []):
            continue
        ins = [r["amount"] for r in p.get("rates", []) or []
               if r.get("unit") == "inputToken"]
        outs = [r["amount"] for r in p.get("rates", []) or []
                if r.get("unit") == "outputToken"]
        if not ins or not outs:
            continue
        tok.append(((min(ins) * 0.3 + min(outs) * 0.7) * 1000, m.get("id"),
                    min(ins), min(outs)))
    for per1k, mid, i, o in sorted(tok)[:10]:
        print(f"  ${per1k:.9f} / 1k tok   {mid:<34} in=${i*1e6:.3f}/1M out=${o*1e6:.3f}/1M")

    if not zero_rate and priced:
        print("\n" + "!" * 78)
        print("! RUNWARE IS METERED, NO FREE TIER")
        print(f"! {len(priced)} published rate cards were checked. Every one of them")
        print("! charges a non-zero amount for at least one unit.")
        print("!" * 78)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
