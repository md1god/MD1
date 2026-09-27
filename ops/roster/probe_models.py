#!/usr/bin/env python3
"""
Probe every candidate model to find out which ones actually answer on THIS
machine, for free, right now.

Names are not evidence. Agent #3 reported a probe that produced empty stdout
and a privacy/policy error - the model was in the catalog and still unusable.
So: run them all, keep the ones that reply.

    python probe_models.py                 # sweep the default candidate list
    python probe_models.py --all           # sweep the whole catalog
    python probe_models.py --only a b c
"""
from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
import time
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

ROOT = Path(r"D:\CC_GAME_1")
ROSTER = Path(__file__).resolve().parent
CLI = json.loads((ROSTER / "agents.json").read_text(encoding="utf-8"))["host"]["opencodeCli"]
LOGDIR = ROSTER / "logs" / "probes"

# Everything whose id suggests no charge. Ordered by how useful we would find it.
CANDIDATES = [
    "opencode/space-bunny-free",
    "opencode/big-pickle",
    "opencode/ling-3.0-flash-fin-free",
    "opencode/longcat-2.5-preview-free",
    "opencode/muse-spark-1.2-contributor",
    "opencode/muse-spark-1.3-contributor",
    "opencode-go/longcat-2.5-preview-free",
    "opencode-go/muse-spark-1.2-contributor",
    "opencode-go/muse-spark-1.3-contributor",
    "opencode-go/hy3",
    "opencode-go/hy4-preview",
    "opencode/muse-spark-1.2-contributor",
    "opencode/muse-spark-1.3-contributor",
]

PROBE = "Reply with exactly: PROBE-OK and nothing else."
ANSI = re.compile(r"\x1b\[[0-9;]*[A-Za-z]")

# Phrases that mean "no, you are not getting a free answer out of this".
DEAD = (
    "insufficient account funds",
    "free tier can only be used",
    "model unavailable",
    "model not found",
    "insufficient credits",
    "payment required",
    "unauthorized",
    "invalid api key",
    "upgrade",
    "subscription",
)


def enc(s: str) -> str:
    e = sys.stdout.encoding or "utf-8"
    return s.encode(e, "replace").decode(e, "replace")


def catalog() -> list[str]:
    try:
        out = subprocess.run([CLI, "models"], cwd=str(ROOT), capture_output=True,
                             text=True, timeout=300).stdout
    except Exception:
        return []
    return [ln.strip() for ln in out.splitlines() if "/" in ln and ln.strip()]


def probe(model: str, timeout: int = 300) -> dict:
    LOGDIR.mkdir(parents=True, exist_ok=True)
    tag = model.replace("/", "_")
    out_f = LOGDIR / f"{tag}.out"
    err_f = LOGDIR / f"{tag}.err"
    t0 = time.time()
    try:
        with open(out_f, "w", encoding="utf-8", errors="replace") as fo, \
             open(err_f, "w", encoding="utf-8", errors="replace") as fe:
            subprocess.run([CLI, "run", "--auto", "--model", model, PROBE],
                           cwd=str(ROOT), stdout=fo, stderr=fe,
                           timeout=timeout, stdin=subprocess.DEVNULL)
        timed_out = False
    except subprocess.TimeoutExpired:
        timed_out = True
    dt = round(time.time() - t0, 1)

    out = ANSI.sub("", out_f.read_text(encoding="utf-8", errors="replace"))
    err = ANSI.sub("", err_f.read_text(encoding="utf-8", errors="replace"))
    blob = (out + " " + err).lower()

    if timed_out:
        verdict, why = "TIMEOUT", f"no answer in {timeout}s"
    elif any(d in blob for d in DEAD):
        verdict, why = "BILLABLE_OR_BLOCKED", next(
            (d for d in DEAD if d in blob), "policy block")
    elif "probe-ok" in blob:
        verdict, why = "WORKS", f"PROBE-OK in {dt}s"
    elif not out.strip() and not err.strip():
        verdict, why = "SILENT", "no stdout and no stderr"
    else:
        verdict, why = "UNCLEAR", (err.strip() or out.strip())[:120]

    return {"model": model, "verdict": verdict, "why": why, "seconds": dt}


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--all", action="store_true")
    ap.add_argument("--only", nargs="*", default=None)
    ap.add_argument("--workers", type=int, default=4)
    ap.add_argument("--timeout", type=int, default=300)
    a = ap.parse_args()

    if a.only:
        models = [m if "/" in m else f"opencode/{m}" for m in a.only]
    elif a.all:
        cat = catalog()
        models = [m for m in cat if re.search(r"free|contributor|opencode/big-pickle", m)]
        print(f"catalog sweep: {len(models)} candidates")
    else:
        models = list(dict.fromkeys(CANDIDATES))

    print(f"probing {len(models)} models, {a.workers} at a time\n")
    results = []
    with ThreadPoolExecutor(max_workers=a.workers) as ex:
        for r in ex.map(lambda m: probe(m, a.timeout), models):
            results.append(r)
            mark = {"WORKS": "PASS", "TIMEOUT": "FAIL", "BILLABLE_OR_BLOCKED": "BILL",
                    "SILENT": "FAIL", "UNCLEAR": "?   "}[r["verdict"]]
            print(f"  [{mark}] {r['model']:<44} {r['seconds']:>6}s  {r['why']}")

    out = ROSTER / "probe_results.json"
    out.write_text(json.dumps(results, indent=2, ensure_ascii=False), encoding="utf-8")
    print()
    good = [r["model"] for r in results if r["verdict"] == "WORKS"]
    print(f"FREE AND WORKING ({len(good)}):")
    for m in good:
        print("   ", m)
    print()
    print(f"wrote {out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
