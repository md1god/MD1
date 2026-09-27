"""Agent #1 - dump the gateway config in full and find the model setting.

Read-only. Calls no model. Costs nothing.
"""
import json
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from openclaw_ws import Gateway  # noqa: E402
from pin_free import find_token  # noqa: E402

HERE = pathlib.Path(__file__).resolve().parent


def redact(t):
    return re.sub(r"\b[0-9a-fA-F]{32,}\b", "<REDACTED>", t)


def main():
    token, _ = find_token()
    identity = json.loads((HERE / "device_identity.json").read_text(encoding="utf-8"))
    gw = Gateway(host="886841a9.openclaw.runware.run", token=token, timeout=120, identity=identity)
    gw.connect()
    try:
        for name, method, params in [
            ("config_full.json", "config.get", {}),
            ("models_full.json", "models.list", {}),
            ("channels_full.json", "channels.status", {}),
            ("system_full.json", "system.info", {}),
        ]:
            try:
                val = gw.call(method, params, timeout=90)
                txt = redact(json.dumps(val, indent=2, ensure_ascii=False))
                (HERE / name).write_text(txt, encoding="utf-8")
                print(f"  {method:16} -> ops/cloud/{name}  ({len(txt)} bytes)")
            except Exception as e:
                print(f"  {method:16} ERR {redact(str(e))[:200]}")
    finally:
        gw.close()


if __name__ == "__main__":
    main()
