"""
F-1c: pull the real Runware model catalogue and hunt for cost == 0.

Routes tried, in order of preference (all are metadata, none run inference):
  A. GET  /v1/models                       (OpenAI-compatible catalogue)
  B. POST /v1  taskType=modelSearch        (documented metadata task)
  C. POST /v1  taskType=ping               (free liveness + proves the key)
  D. POST /v1  taskType=accountManagement  (free; may expose balance/credits)
"""
import json
import os
import re
import time
import urllib.error
import urllib.request

API_KEY_FILE = r"C:\Users\DiDo\Desktop\APi.txt"
OUT_DIR = os.path.dirname(os.path.abspath(__file__))
REST_V1 = "https://api.runware.ai/v1"

KEY = re.search(r"anicw[A-Za-z0-9]{20,}",
                open(API_KEY_FILE, encoding="utf-8", errors="replace").read()).group(0)
HDR = {"Content-Type": "application/json", "Authorization": "Bearer " + KEY,
       "User-Agent": "agentF/1.0"}


def call(url, body=None, method=None, timeout=60, retries=4):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, method=method or ("POST" if data else "GET"),
                                 headers=HDR, data=data)
    last = None
    for a in range(1, retries + 1):
        try:
            with urllib.request.urlopen(req, timeout=timeout) as r:
                return r.status, r.read().decode(errors="replace")
        except urllib.error.HTTPError as e:
            return e.code, e.read().decode(errors="replace")
        except Exception as e:  # noqa: BLE001
            last = f"{type(e).__name__}: {e}"
            time.sleep(2 * a)
    return 0, f"TRANSPORT {last}"


def show(label, st, txt, n=700):
    print(f"\n### {label}  -> HTTP {st}")
    try:
        print(json.dumps(json.loads(txt), indent=2, ensure_ascii=False)[:n])
    except Exception:
        print(re.sub(r"\s+", " ", txt)[:n])


def harvest(text):
    """Collect any JSON object that looks like a model record."""
    try:
        data = json.loads(text)
    except Exception:
        return []
    found = []

    def walk(node):
        if isinstance(node, dict):
            keys = set(node.keys())
            if keys & {"id", "model", "name"} and (
                    keys & {"pricing", "cost", "price", "type", "architecture",
                            "shortName", "modality"}):
                found.append(node)
            for v in node.values():
                walk(v)
        elif isinstance(node, list):
            for v in node:
                walk(v)

    walk(data)
    return found


def main():
    print("=" * 78)
    print("F-1c  RUNWARE CATALOGUE HARVEST")
    print("=" * 78)

    catalogue = {}
    records = []

    # ---- A. OpenAI-compatible catalogue -----------------------------------
    st, txt = call(REST_V1 + "/models", method="GET")
    show("A  GET /v1/models", st, txt, 1200)
    a = harvest(txt)
    print(f"   -> {len(a)} model records")
    if a:
        catalogue["GET /v1/models"] = {"status": st, "count": len(a), "raw_len": len(txt)}
        records += a
        with open(os.path.join(OUT_DIR, "raw_openai_models.json"), "w", encoding="utf-8") as f:
            json.dump(json.loads(txt), f, indent=2, ensure_ascii=False)

    # ---- B. modelSearch ----------------------------------------------------
    for body in (
        [{"taskType": "modelSearch", "search": "", "limit": 1000}],
        [{"taskType": "modelSearch", "search": "free"}],
        [{"taskType": "modelSearch", "search": "qwen"}],
    ):
        label = "B  modelSearch " + json.dumps(
            {k: v for k, v in body[0].items() if k != "taskType"})
        st, txt = call(REST_V1, body)
        b = harvest(txt)
        show(label, st, txt, 900)
        print(f"   -> {len(b)} model records")
        if b and len(b) > len(records):
            records = b
            with open(os.path.join(OUT_DIR, "raw_modelsearch.json"), "w", encoding="utf-8") as f:
                json.dump(json.loads(txt), f, indent=2, ensure_ascii=False)
        elif b:
            records += b
        time.sleep(0.5)

    # ---- C. ping -----------------------------------------------------------
    st, txt = call(REST_V1, [{"taskType": "ping", "includeCost": True}])
    show("C  ping", st, txt, 600)

    # ---- D. accountManagement (free; look for credits) ---------------------
    st, txt = call(REST_V1, [{"taskType": "accountManagement", "operation": "balance"}])
    show("D  accountManagement/balance", st, txt, 600)
    st, txt = call(REST_V1, [{"taskType": "getTaskDetails", "taskUUID": "00000000-0000-0000-0000-000000000000"}])
    show("D2 getTaskDetails", st, txt, 400)

    # ---- verdict -----------------------------------------------------------
    print("\n" + "=" * 78)
    print(f"TOTAL MODEL RECORDS HARVESTED: {len(records)}")
    if not records:
        print("VERDICT: UNVERIFIED - could not retrieve a catalogue by any route")
        return 0
    with open(os.path.join(OUT_DIR, "harvested_models.json"), "w", encoding="utf-8") as f:
        json.dump(records, f, indent=2, ensure_ascii=False)
    print("[saved] harvested_models.json")
    print("[sample record keys]")
    seen = set()
    for r in records[:50]:
        k = tuple(sorted(r.keys()))
        if k not in seen:
            seen.add(k)
            print("   ", k)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
