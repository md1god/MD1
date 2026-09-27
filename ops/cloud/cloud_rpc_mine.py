"""F-2c: find the gateway WebSocket endpoint and learn the RPC envelope.

Reads every lazily-referenced page bundle too (they are listed in the main
bundle's __vite__mapDeps), because the RPC names live in those.
"""
import os
import re
import urllib.request
import urllib.error

BASE = "https://886841a9.openclaw.runware.run"
UA = {"User-Agent": "agentF/1.0"}
OUT = os.path.dirname(os.path.abspath(__file__))
BD = os.path.join(OUT, "_bundles")


def get(url, timeout=60, retries=3):
    req = urllib.request.Request(url, headers=UA)
    last = None
    for a in range(1, retries + 1):
        try:
            with urllib.request.urlopen(req, timeout=timeout) as r:
                return r.status, r.read().decode(errors="replace")
        except urllib.error.HTTPError as e:
            return e.code, ""
        except Exception as e:  # noqa: BLE001
            last = f"{type(e).__name__}: {e}"
    return 0, str(last)


def main():
    # 1. every asset name mentioned anywhere in what we already have
    known = ""
    for fn in os.listdir(BD):
        known += open(os.path.join(BD, fn), encoding="utf-8", errors="replace").read()
    refs = sorted(set(re.findall(r'["\'`](?:\./)?assets/([A-Za-z0-9_\-.]+\.js)["\'`]', known))
                  | set(re.findall(r'["\'`]\./([A-Za-z0-9_\-.]+\.js)["\'`]', known)))
    print(f"discovered {len(refs)} asset filenames inside the bundles")
    have = {f for f in os.listdir(BD)}
    todo = [r for r in refs if r not in have]
    print(f"  {len(todo)} not downloaded yet")

    for a in range(4):
        if not todo:
            break
        nxt = []
        for name in todo:
            st, js = get(f"{BASE}/assets/{name}")
            if st == 200:
                open(os.path.join(BD, name), "w", encoding="utf-8", errors="replace").write(js)
            else:
                nxt.append(name)
        todo = nxt
    print("after recursive expansion, still missing:", len(todo), todo[:10])

    blob = ""
    for fn in sorted(os.listdir(BD)):
        blob += open(os.path.join(BD, fn), encoding="utf-8", errors="replace").read() + "\n"
    print(f"total corpus: {len(blob)} bytes across {len(os.listdir(BD))} files")

    # 2. RPC method names: e.request(`a.b`) or "method":"a.b"
    print("\n" + "=" * 78)
    print("RPC METHOD NAMES (dotted)")
    print("=" * 78)
    methods = set()
    for m in re.finditer(r'\.request\(\s*[`"\']([a-z][A-Za-z0-9_]*(?:\.[A-Za-z0-9_]+)+)[`"\']', blob):
        methods.add(m.group(1))
    for m in re.finditer(r'["\']((?:method|type)\s*[:=]\s*[`"\'])([a-z][A-Za-z0-9_]*(?:\.[A-Za-z0-9_]+)+)', blob):
        methods.add(m.group(2))
    for m in sorted(methods):
        print("  ", m)
    print(f"total: {len(methods)}")

    # 3. websocket url derivation
    print("\n" + "=" * 78)
    print("WEBSOCKET URL DERIVATION")
    print("=" * 78)
    for pat in (r'.{260}18789.{260}', r'.{200}wss?:.{200}', r'.{160}`/ws`.{200}'):
        for m in re.finditer(pat, blob):
            print("  ...", re.sub(r'\s+', ' ', m.group(0))[:520], "\n")

    # 4. terminal permission + capability gating
    print("=" * 78)
    print("TERMINAL / EXEC GATING")
    print("=" * 78)
    for pat in (r'.{140}terminal\.open.{200}', r'.{100}terminalAvailable.{160}',
                r'.{100}exec\.run.{160}', r'.{80}approval.{0,40}exec.{160}'):
        for m in list(re.finditer(pat, blob))[:4]:
            print("  ...", re.sub(r'\s+', ' ', m.group(0))[:400], "\n")

    # 5. anything git / clone / shell related in the agent tool surface
    print("=" * 78)
    print("GIT / SHELL TOOL REFERENCES")
    print("=" * 78)
    for pat in (r'["\'`]([a-z_]*(?:exec|shell|bash|command|git|clone)[a-z_]*(?:\.[a-zA-Z_]+)?)["\'`]'):
        hits = sorted(set(re.findall(pat, blob, re.I)))
        for h in hits[:60]:
            print("  ", h)


if __name__ == "__main__":
    main()
