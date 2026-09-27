"""F-2a: map the OpenClaw cloud gateway's real API surface from its own JS bundle."""
import re
import urllib.request
import urllib.error

BASE = "https://886841a9.openclaw.runware.run"
UA = {"User-Agent": "agentF/1.0"}


def get(path, timeout=30):
    req = urllib.request.Request(BASE + path, headers=UA)
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return r.status, r.read().decode(errors="replace")
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode(errors="replace")
    except Exception as e:  # noqa: BLE001
        return 0, f"{type(e).__name__}: {e}"


def main():
    st, h = get("/")
    print(f"[html] HTTP {st} len={len(h)}")
    print("\n=== data-* feature flags ===")
    for m in re.finditer(r'(data-[a-z0-9-]+)="([^"]*)"', h):
        print(f"  {m.group(1)} = {m.group(2)}")

    print("\n=== script / link refs ===")
    for pat in (r'src="([^"]+)"', r'href="([^"]+\.js[^"]*)"'):
        for m in re.finditer(pat, h):
            print("  ", m.group(1))

    bundles = re.findall(r'(?:src|href)="([^"]+\.js)"', h)
    print(f"\n=== scanning {len(bundles)} bundle(s) for API routes ===")
    api_routes, flags = set(), set()
    for b in bundles:
        if b.startswith("http"):
            url = b
        else:
            # NB: must strip the leading "./" or BASE+b yields host "runware.run."
            url = BASE + "/" + b.lstrip("./").lstrip("/")
        s, js = get(url, timeout=60)
        print(f"\n--- {b}  HTTP {s}  len={len(js)} ---")
        if s != 200:
            continue
        for m in re.finditer(r'["\'`](/api/[A-Za-z0-9_\-/{}.:]+)["\'`]', js):
            api_routes.add(m.group(1))
        for m in re.finditer(r'["\'`](/(?:ws|rpc|v1|health|terminal|exec|shell|cmd)'
                             r'[A-Za-z0-9_\-/{}.:]*)["\'`]', js):
            api_routes.add(m.group(1))
        for m in re.finditer(r'(?:terminalEnabled|terminal[A-Za-z]*|exec[A-Za-z]*|shell[A-Za-z]*)'
                             r'\s*[:=]\s*(true|false|"[^"]*")', js):
            flags.add(m.group(0)[:90])

    print("\n=== API ROUTES DISCOVERED ===")
    for r_ in sorted(api_routes):
        print("  ", r_)
    print("\n=== terminal/exec related flags in JS ===")
    for f in sorted(flags)[:40]:
        print("  ", f)

    # probe the discovered routes unauthenticated
    print("\n=== live probe of discovered routes ===")
    for r_ in sorted(api_routes):
        if "{" in r_ or "*" in r_:
            continue
        s, b = get(r_, timeout=12)
        flag = ""
        if s == 401:
            flag = "  <-- EXISTS, needs auth"
        elif s == 200 and b.strip().startswith(("{", "[")):
            flag = "  <-- JSON (open?)"
        print(f"  {r_:<42} {s}{flag}  {re.sub(chr(92)+'s+', ' ', b)[:90]}")


if __name__ == "__main__":
    main()
