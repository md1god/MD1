"""
Agent #1 - connect to the OpenClaw gateway and pin it to a FREE model.

Why: Runware is metered. The dashboard showed $0.11 in one day, 97.7% of it on
qwen3.5-9b. The owner's rule is zero cost, absolute. So the model must be one of
the `opencode/*-free` models, which the OpenCode provider serves at $0.00.

This is a configuration write on a gateway we already own. It does not call a
model, so it does not spend money.

Usage:  python ops/cloud/pin_free.py            # read config, change nothing
        python ops/cloud/pin_free.py --apply    # actually write the model
"""
import json
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from openclaw_ws import Gateway  # noqa: E402

APITXT = pathlib.Path(r"C:\Users\DiDo\Desktop\APi.txt")
HOST = "886841a9.openclaw.runware.run"
OUT = pathlib.Path(__file__).resolve().parent / "pin_free_result.json"

FREE_MODELS = [
    "opencode/ling-3.0-flash-fin-free",
    "opencode/mimo-v2.6-flash-free",
    "opencode/longcat-2.5-preview-free",
    "opencode/big-pickle",
    "opencode/muse-spark-1.3-contributor-free",
    "opencode/nemotron-3-ultra-free",
    "opencode/space-bunny-free",
]
PAY_MODEL_HINTS = ("qwen3.5-9b", "qwen", "gpt-oss", "gemini", "claude", "gpt-4")


def find_token():
    """The gateway control token: the last 64-hex token in APi.txt.

    Read positionally, never printed. Line 44 is the OpenRouter key (prefixed
    sk-or-v1-), line 57 and 93 are bare 64-hex.
    """
    lines = APITXT.read_text(encoding="utf-8", errors="replace").splitlines()
    bare = []
    for i, l in enumerate(lines, 1):
        for m in re.finditer(r"(?<!-)\b([0-9a-fA-F]{64})\b", l):
            bare.append((i, m.group(1)))
    if not bare:
        raise SystemExit("no bare 64-hex token found in APi.txt")
    # agent F measured that the working one is the 3rd; take the last as that
    # is the same file position, and fall back to the 3rd.
    return bare[2][1] if len(bare) >= 3 else bare[-1][1], len(bare)


def redact(text):
    return re.sub(r"\b[0-9a-fA-F]{32,}\b", "<REDACTED>", text)


def main():
    apply = "--apply" in sys.argv
    token, n = find_token()
    print(f"token: found ({n} bare 64-hex candidates), value not printed")

    report = {"host": HOST, "applied": False}
    # The gateway demands a signed device object on every connect. agent F
    # generated and validated the keypair (passes all 3 RFC 8032 vectors) and
    # left it next to this script.
    ident_path = pathlib.Path(__file__).resolve().parent / "device_identity.json"
    if not ident_path.exists():
        raise SystemExit(f"missing device identity: {ident_path}")
    identity = json.loads(ident_path.read_text(encoding="utf-8"))
    for k in ("deviceId", "privateKey", "publicKey"):
        if k not in identity:
            raise SystemExit(f"device_identity.json missing {k}")
    print(f"device identity loaded: {identity['deviceId'][:12]}... (value not printed)")

    gw = Gateway(host=HOST, token=token, timeout=90, identity=identity)
    try:
        hello = gw.connect()
        report["connected"] = True
        report["hello"] = redact(json.dumps(hello)[:800])
        print("CONNECTED to gateway")

        # what does it think it is running?
        for method in ("config.get", "system.info", "channels.status", "models.list"):
            try:
                val = gw.call(method, {}, timeout=60)
                report[method] = redact(json.dumps(val)[:4000])
                print(f"  {method:18} OK  {len(json.dumps(val))} bytes -> report")
            except Exception as e:
                report[method] = f"ERROR {redact(str(e))[:300]}"
                print(f"  {method:18} ERR {redact(str(e))[:160]}")

        # find the current model + the paid one we must replace
        cfg = None
        try:
            cfg = gw.call("config.get", {}, timeout=60)
        except Exception:
            pass
        current = None
        blob = json.dumps(cfg) if cfg else ""
        for h in PAY_MODEL_HINTS:
            for m in re.findall(r"[\w.-]+/[\w.:-]*" + re.escape(h) + r"[\w.:-]*", blob):
                current = m
                break
            if current:
                break
        report["currentModelDetected"] = current
        print(f"  current model in config: {current}")

        if not apply:
            print("\n--apply not given. Read-only. No change made, no cost incurred.")
            OUT.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
            return 0

        if not current:
            print("\nREFUSING to write blind: could not identify the current model field.")
            OUT.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
            return 1

        target = "opencode/ling-3.0-flash-fin-free"  # fastest verified free: 2.4s
        # Try a set of write shapes; report which one the gateway accepts.
        attempts = [
            ("config.set", {"key": "agents.defaults.model", "value": target}),
            ("config.set", {"key": "agent.model", "value": target}),
            ("config.patch", {"agents": {"defaults": {"model": target}}}),
        ]
        for method, params in attempts:
            try:
                res = gw.call(method, params, timeout=60)
                report.setdefault("writes", []).append(
                    {"method": method, "params": params, "ok": True,
                     "result": redact(json.dumps(res)[:400])})
                print(f"  WRITE OK   {method} {params}")
                report["applied"] = True
                break
            except Exception as e:
                report.setdefault("writes", []).append(
                    {"method": method, "params": params, "ok": False,
                     "error": redact(str(e))[:300]})
                print(f"  write fail {method}: {redact(str(e))[:200]}")

        # verify by reading back
        try:
            after = json.dumps(gw.call("config.get", {}, timeout=60))
            report["verify_modelPresent"] = target in after
            report["verify_qwenStillThere"] = "qwen3.5-9b" in after
            print(f"  verify: {target} present = {report['verify_modelPresent']}"
                  f" | qwen3.5-9b still present = {report['verify_qwenStillThere']}")
        except Exception as e:
            report["verify"] = redact(str(e))[:200]
    except Exception as e:
        report["connected"] = False
        report["fatal"] = redact(str(e))[:600]
        print("FAILED: " + redact(str(e))[:400])
    finally:
        gw.close()
        OUT.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
        print(f"\nwritten: {OUT}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
