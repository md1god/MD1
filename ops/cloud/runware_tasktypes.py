"""
F-1b: enumerate Runware task types, then find a *metadata* (non-inference) task
that returns the model catalogue. Nothing here submits an inference task.
Auth proven: Authorization: Bearer <anicw...> (the only key that passed).
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

blob = open(API_KEY_FILE, encoding="utf-8", errors="replace").read()
KEY = re.search(r"anicw[A-Za-z0-9]{20,}", blob).group(0)


def http(url, body, timeout=60, retries=4):
    req = urllib.request.Request(
        url, method="POST",
        headers={"Content-Type": "application/json",
                 "Authorization": "Bearer " + KEY,
                 "User-Agent": "agentF/1.0"},
        data=json.dumps(body).encode())
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


def http_get(url, timeout=60, retries=3):
    req = urllib.request.Request(url, method="GET",
                                 headers={"User-Agent": "agentF/1.0"})
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


def main():
    print("=" * 78)
    print("F-1b  TASK TYPE ENUMERATION (no inference, no cost)")
    print("=" * 78)

    # 1. force the API to print its own supported taskType list
    st, txt = http(REST_V1, [{"taskType": "__list_these__"}])
    print(f"[probe] bogus taskType -> HTTP {st}")
    types = sorted(set(re.findall(r"'([A-Za-z0-9_]+)'", txt)))
    print(f"[probe] {len(types)} task types advertised by the error message:")
    for t in types:
        print(f"          {t}")
    print()

    # 2. try each plausible metadata / catalogue task
    catalogue_tasks = [
        "getModelList", "modelList", "getModels", "listModels",
        "getModelDetails", "getAccountBalance", "accountBalance",
        "getModelArchitectureList", "getModelLicenses", "getUpscaleModels",
    ]
    print("--- catalogue task probe ---")
    for t in catalogue_tasks:
        for url, wrap in ((REST_V1, lambda x: [x]), (GRAPHQL, lambda x: [x])):
            st, txt = http(url, wrap({"taskType": t}))
            body = re.sub(r"\s+", " ", txt)[:300]
            interesting = st not in (400, 401) or "invalidTaskType" not in txt
            flag = "  <== ACCEPTED" if interesting else ""
            print(f"  {t:<28} {url.split('/v1')[1] or '/v1':<12} {st} {body}{flag}")
        time.sleep(0.4)
    print()

    # 3. public, unauthenticated catalogue endpoints (docs advertise a model search)
    print("--- public catalogue endpoints (no auth) ---")
    for url in (
        "https://api.runware.ai/v1/models",
        "https://api.runware.ai/models",
        "https://runware.ai/api/models",
        "https://runware.ai/models.json",
    ):
        st, txt = http_get(url)
        ct = re.sub(r"\s+", " ", txt)[:180]
        print(f"  {url:<48} {st} {ct}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
