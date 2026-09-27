"""F-2b: download the gateway UI bundles once, then mine them for the
transport the Control UI actually uses (WebSocket / HTTP API) so we can
drive the cloud without a browser.
"""
import os
import re
import urllib.request
import urllib.error

BASE = "https://886841a9.openclaw.runware.run"
UA = {"User-Agent": "agentF/1.0"}
OUT = os.path.dirname(os.path.abspath(__file__))
BUNDLE_DIR = os.path.join(OUT, "_bundles")


def get(url, timeout=90, retries=3):
    req = urllib.request.Request(url, headers=UA)
    last = None
    for a in range(1, retries + 1):
        try:
            with urllib.request.urlopen(req, timeout=timeout) as r:
                return r.status, r.read()
        except urllib.error.HTTPError as e:
            return e.code, b""
        except Exception as e:  # noqa: BLE001
            last = f"{type(e).__name__}: {e}"
            print(f"   retry {a}/{retries} {url}: {last}")
    return 0, str(last).encode()


def main():
    os.makedirs(BUNDLE_DIR, exist_ok=True)
    s, h = get(BASE + "/")
    html = h.decode(errors="replace")
    refs = re.findall(r'(?:src|href)="([^"]+\.js)"', html)
    print(f"html HTTP {s}; {len(refs)} bundle refs")

    blobs = {}
    for b in refs:
        url = BASE + "/" + b.lstrip("./").lstrip("/")
        st, data = get(url)
        if st == 200:
            fn = os.path.join(BUNDLE_DIR, os.path.basename(b))
            with open(fn, "wb") as f:
                f.write(data)
            blobs[b] = data.decode(errors="replace")
            print(f"  OK {st} {len(data):>8}  {b}")
        else:
            print(f"  -- {st} {'':>8}  {b}")
    total = sum(len(v) for v in blobs.values())
    print(f"\ndownloaded {len(blobs)} bundles, {total} bytes total -> _bundles/")

    joined = "\n".join(blobs.values())
    print("\n" + "=" * 78)
    print("TRANSPORT / ENDPOINT MINING")
    print("=" * 78)

    def find(label, pattern, limit=25, flags=re.I):
        hits = sorted(set(re.findall(pattern, joined, flags)))
        print(f"\n--- {label} ({len(hits)}) ---")
        for h_ in hits[:limit]:
            print("   ", h_)

    find("REST paths", r'["\'`](/api/[A-Za-z0-9_\-/{}$.:]{2,60})["\'`]')
    find("absolute same-origin paths", r'["\'`](/(?:ws|rpc|v1|health|terminal|exec|shell|auth|login)[A-Za-z0-9_\-/{}$.:]{0,50})["\'`]')
    find("gateway methods / RPC names", r'["\'`]((?:gateway|system|exec|terminal|shell|fs|git|agent|chat|session)\.[a-zA-Z][A-Za-z0-9_]{2,30})["\'`]')
    find("WS url construction", r'(?:new WebSocket|WebSocket\()')
    find("auth header names", r'["\'`]([A-Za-z\-]{3,24}-token|authorization|bearer|apikey)["\'`]', flags=re.I)
    find("terminal mentions", r'[^\n]{0,70}terminal[A-Za-z]{0,12}[^\n]{0,70}', limit=15)
    find("feature/capability keys", r'["\'`]([a-z][A-Za-z]{2,28}(?:Enabled|enabled|Available|available))["\'`]')

    # show context around the websocket bootstrap
    print("\n" + "=" * 78)
    print("WEBSOCKET BOOTSTRAP CONTEXT")
    print("=" * 78)
    for m in re.finditer(r'.{300}new WebSocket.{500}', joined):
        print(re.sub(r'\s+', ' ', m.group(0)))
        print("-" * 70)


if __name__ == "__main__":
    main()
