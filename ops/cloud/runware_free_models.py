"""
Task F-1: Is there a zero-cost Runware model?
Discovery, not guessing. Read-only. Never writes outside ops/cloud/.

Steps:
  1. Introspect the ModelList / ModelPricing types from the public GraphQL schema.
  2. Pull the full model list with pricing.
  3. Filter for cost == 0 / free / payg-free signals.
  4. Print a verdict line.
"""
import json
import os
import re
import sys
import time
import urllib.error
import urllib.request

API_KEY_FILE = r"C:\Users\DiDo\Desktop\APi.txt"
ENDPOINT = "https://api.runware.ai/v1/openapi/graphql"
OUT_DIR = os.path.dirname(os.path.abspath(__file__))


def read_key():
    """Pull the Runware key out of the operator's key file, without printing it."""
    with open(API_KEY_FILE, "r", encoding="utf-8", errors="replace") as fh:
        blob = fh.read()
    for m in re.finditer(r"anicw[A-Za-z0-9]{20,}", blob):
        return m.group(0)
    # fallback: any 30+ char alnum token that looks like a runware key
    for m in re.finditer(r"\b[A-Za-z0-9]{32,}\b", blob):
        tok = m.group(0)
        if not re.fullmatch(r"[0-9a-f]{32,}", tok):  # skip hashes
            return tok
    raise SystemExit("FAIL: no runware key found in APi.txt")


KEY = read_key()


def gql(query, variables=None):
    # Runware auth: the key goes in variables.apiKey, NOT an Authorization header.
    # Proof: with a header only, the API answers HTTP 401
    #   code=missingApiKey, parameter=apiKey, type=string
    variables = dict(variables or {})
    variables["apiKey"] = KEY
    # Runware's public GraphQL gateway REQUIRES a batched array payload.
    # (error code: invalidPayloadFormat / "must be an array of objects")
    payload = [{"query": query, "variables": variables}]
    req = urllib.request.Request(
        ENDPOINT,
        method="POST",
        headers={
            "Content-Type": "application/json",
            "Authorization": KEY,
            "User-Agent": "CC_GAME_1-agentF/1.0",
            "Accept": "application/json",
        },
        data=json.dumps(payload).encode(),
    )
    last = None
    for attempt in range(1, 6):
        try:
            with urllib.request.urlopen(req, timeout=90) as resp:
                raw = json.loads(resp.read().decode())
            # batched request -> batched response: unwrap the single-element array
            if isinstance(raw, list):
                raw = raw[0] if raw else {}
            return raw
        except urllib.error.HTTPError as e:
            body = e.read().decode(errors="replace")
            # 4xx = our bug, do not retry
            raise SystemExit(f"HTTP {e.code} from runware:\n{body[:2000]}")
        except Exception as e:  # noqa: BLE001
            last = f"{type(e).__name__}: {e}"
            print(f"[retry {attempt}/5] transport: {last}")
            time.sleep(attempt * 2)
    raise SystemExit(f"TRANSPORT FAIL after 5 attempts: {last}")


def introspect_type(type_name):
    q = """
    { __type(name: "%s") {
        name kind
        fields { name
          type { name kind ofType { name kind ofType { name kind } } } }
      } } }
    """ % type_name
    r = gql(q)
    if "errors" in r:
        return None, r["errors"]
    return r["data"]["__type"], None


def main():
    print("=" * 72)
    print("F-1  RUNWARE FREE-TIER DISCOVERY")
    print("=" * 72)
    print(f"key source : {API_KEY_FILE} (len={len(KEY)}, value not printed)")
    print(f"endpoint   : {ENDPOINT}")
    print()

    # ---- 1. schema introspection -------------------------------------------
    schema_report = {}
    for tname in ("ModelList", "ModelPricing", "ModelType"):
        t, err = introspect_type(tname)
        if t is None:
            schema_report[tname] = f"UNAVAILABLE: {err}"
            print(f"[schema] {tname}: UNAVAILABLE")
        else:
            schema_report[tname] = sorted(f["name"] for f in (t.get("fields") or []))
            print(f"[schema] {tname} fields: {', '.join(schema_report[tname])}")
    print()

    # ---- 2. build the model list query from the real field names ----------
    pricing_fields = schema_report.get("ModelPricing") or []
    want_pricing = [f for f in ("type", "cost", "costPerStep", "unit",
                                "payPerImage", "inputCost", "outputCost",
                                "payPerMegapixelStep", "maxPriceMultiplier")
                    if f in pricing_fields]
    if "ModelPricing" in pricing_fields:
        raise SystemExit("ModelPricing is a field, not a type - adjust query builder")
    base = ["id", "name", "shortName", "description", "type", "architecture", "author"]
    model_list_fields = schema_report.get("ModelList") or []
    base = [f for f in base if f in model_list_fields] or ["id", "name"]
    pricing_sel = "{ " + " ".join(want_pricing) + " }" if want_pricing else ""
    query = "{ modelList { %s pricing %s } }" % (" ".join(base), pricing_sel)
    print(f"[query] {query[:400]}")
    print()

    r = gql(query)
    if "errors" in r and "data" not in r:
        print("[fatal] query errors:")
        print(json.dumps(r["errors"], indent=2)[:3000])
        raise SystemExit(1)
    models = r["data"]["modelList"]
    total = len(models)
    print(f"[ok] modelList returned {total} models")
    if total == 0:
        raise SystemExit("UNVERIFIED: empty modelList, cannot conclude anything")

    with open(os.path.join(OUT_DIR, "runware_model_list.json"), "w",
              encoding="utf-8") as fh:
        json.dump({"total": total, "schema": schema_report, "models": models},
                  fh, indent=2, ensure_ascii=False)
    print(f"[saved] runware_model_list.json  ({total} records)")
    print()

    # ---- 3. classify -------------------------------------------------------
    free, metered, freeword, unknown = [], [], [], []
    for m in models:
        p = m.get("pricing") or {}
        costs = []
        for key in ("cost", "inputCost", "outputCost", "costPerStep",
                    "payPerImage", "payPerMegapixelStep"):
            v = p.get(key)
            if isinstance(v, (int, float)):
                costs.append((key, float(v)))
        blob = json.dumps(p, ensure_ascii=False).lower()
        ptype = str(p.get("type", "")).lower()
        is_freeword = "free" in blob or "free" in ptype
        if costs and all(c == 0.0 for _, c in costs):
            free.append((m, p, costs))
        elif is_freeword:
            freeword.append((m, p, costs))
        elif costs:
            metered.append((m, p, costs))
        else:
            unknown.append((m, p, costs))

    def show(bucket, title):
        print(f"--- {title}: {len(bucket)} ---")
        for m, p, costs in sorted(bucket, key=lambda x: x[0].get("name", "")):
            cd = ", ".join(f"{k}={v}" for k, v in costs) or "no numeric cost field"
            print(f"    {m.get('name', m.get('id'))!r:44} type={p.get('type')!r:12} {cd}")
        print()

    show(free, "EXPLICIT cost == 0")
    show(freeword, "CONTAINS the word 'free'")
    show(unknown, "NO PRICING NUMBERS / UNKNOWN")

    print(f"--- METERED: {len(metered)} (showing 15 cheapest by min known cost) ---")

    def min_cost(item):
        c = item[2]
        return min(v for _, v in c) if c else float("inf")

    for m, p, costs in sorted(metered, key=min_cost)[:15]:
        cd = ", ".join(f"{k}={v}" for k, v in costs)
        print(f"    {m.get('name', m.get('id'))!r:44} {cd}  raw={json.dumps(p)[:120]}")
    print()

    # ---- 4. verdict --------------------------------------------------------
    print("=" * 72)
    if free or freeword:
        print("VERDICT: RUNWARE HAS A FREE TIER")
        for m, p, costs in (free + freeword)[:20]:
            print(f"   {m.get('name', m.get('id'))}  {json.dumps(p)}")
    elif metered:
        print("VERDICT: RUNWARE IS METERED, NO FREE TIER")
        cheapest = sorted(metered, key=min_cost)[0]
        print(f"   cheapest: {cheapest[0].get('name', cheapest[0].get('id'))}")
        print(f"   pricing : {json.dumps(cheapest[1], ensure_ascii=False)}")
    else:
        print("VERDICT: UNVERIFIED - no pricing data of any kind returned")
    print("=" * 72)

    return 0


if __name__ == "__main__":
    sys.exit(main())
