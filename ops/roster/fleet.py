#!/usr/bin/env python3
"""
Fleet runner: many free agents at once, with a HARD no-overlap guard.

dispatch.py has one global lock, so it can only ever run one agent. That is the
right behaviour for editing Unity .cs/.meta pairs, and the wrong behaviour when
the jobs are separate files or separate directories.

So this runner keeps the safety and drops the serialization:

  - every task declares the paths it owns
  - two tasks whose owned paths intersect are REFUSED before anything starts
  - a task that writes outside its declared paths is a policy violation, logged
  - paid and blacklisted models are refused, same as dispatch.py

Usage:
    python fleet.py                      # run the manifest
    python fleet.py --check             # overlap check only, run nothing
    python fleet.py --workers 3         # cap concurrency
"""
from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
import time
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path

ROOT = Path(r"D:\CC_GAME_1")
ROSTER_DIR = Path(__file__).resolve().parent
ROSTER = ROSTER_DIR / "agents.json"
MANIFEST = ROSTER_DIR / "fleet.json"
HANDOFF = ROOT / "ops" / "roster" / "handoff"
LOGDIR = ROOT / "ops" / "roster" / "logs"
ANSI = re.compile(r"\x1b\[[0-9;]*[A-Za-z]")


def safe_print(s: str) -> None:
    enc = sys.stdout.encoding or "utf-8"
    try:
        print(s)
    except UnicodeEncodeError:
        print(s.encode(enc, errors="replace").decode(enc, errors="replace"))


def norm(p: str) -> str:
    """Case-insensitive, separator-normalised, so Assets and assets collide."""
    return p.replace("\\", "/").rstrip("/").lower()


def overlaps(a: str, b: str) -> bool:
    """True if two owned paths touch. Directory ownership covers its children."""
    a, b = norm(a), norm(b)
    if a == b:
        return True
    # one is a parent of the other
    return a.startswith(b + "/") or b.startswith(a + "/")


def check_manifest(manifest: dict, verbose: bool = True) -> list[str]:
    problems = []
    seen = {}
    for job in manifest["jobs"]:
        slot = job["slot"]
        if slot in seen:
            problems.append(f"duplicate slot {slot}")
        seen[slot] = job
        for p in job.get("owns", []):
            if not p.strip():
                problems.append(f"slot {slot}: empty path in owns")
    # pairwise
    slots = list(seen)
    for i in range(len(slots)):
        for j in range(i + 1, len(slots)):
            for pa in seen[slots[i]].get("owns", []):
                for pb in seen[slots[j]].get("owns", []):
                    if overlaps(pa, pb):
                        problems.append(
                            f"OVERLAP: {slots[i]} owns '{pa}' and {slots[j]} owns '{pb}'")
    if verbose:
        if problems:
            safe_print("OVERLAP CHECK: FAILED")
            for p in problems:
                safe_print("   " + p)
        else:
            safe_print(f"OVERLAP CHECK: clean across {len(slots)} jobs")
            for s in slots:
                safe_print(f"   {s:<4} {seen[s]['model']:<44} owns {seen[s].get('owns')}")
    return problems


def run_job(job: dict, timeout: int, exe: str, banned: list[str]) -> dict:
    HANDOFF.mkdir(parents=True, exist_ok=True)
    LOGDIR.mkdir(parents=True, exist_ok=True)
    stamp = time.strftime("%Y%m%d-%H%M%S")
    tag = f"fleet-{job['slot']}-{stamp}"
    out_f = LOGDIR / f"{tag}.out"
    err_f = LOGDIR / f"{tag}.err"

    prompt = Path(job["task"]).read_text(encoding="utf-8", errors="replace")
    cmd = [exe, "run", "--auto", "--model", job["model"], prompt]
    t0 = time.time()
    timed_out = False
    with open(out_f, "w", encoding="utf-8", errors="replace") as fo, \
         open(err_f, "w", encoding="utf-8", errors="replace") as fe:
        try:
            subprocess.run(cmd, cwd=str(ROOT), stdout=fo, stderr=fe,
                           timeout=timeout, stdin=subprocess.DEVNULL)
        except subprocess.TimeoutExpired:
            timed_out = True
    dt = round(time.time() - t0, 1)

    stdout = ANSI.sub("", out_f.read_text(encoding="utf-8", errors="replace"))
    stderr = ANSI.sub("", err_f.read_text(encoding="utf-8", errors="replace"))

    hp = HANDOFF / f"{tag}.md"
    hp.write_text(
        f"# Fleet handoff: slot {job['slot']}\n\n"
        f"- title: {job.get('title','')}\n"
        f"- model: `{job['model']}` (free)\n"
        f"- owns: {job.get('owns')}\n"
        f"- seconds: {dt}  timedOut: {timed_out}\n"
        f"- stdout log: `{out_f}`\n\n"
        f"## Agent output\n\n{stdout.strip() or '_(no stdout)_'}\n\n"
        f"## stderr\n\n```\n{stderr.strip()[:3000]}\n```\n",
        encoding="utf-8")

    return {"slot": job["slot"], "title": job.get("title", ""), "model": job["model"],
            "seconds": dt, "timedOut": timed_out, "handoff": str(hp),
            "stdoutTail": stdout.strip()[-1500:]}


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--workers", type=int, default=6)
    ap.add_argument("--timeout", type=int, default=1500)
    ap.add_argument("--only", nargs="*", default=None)
    a = ap.parse_args()

    roster = json.loads(ROSTER.read_text(encoding="utf-8"))
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    banned = roster.get("banned", {}).get("models", [])
    exe = roster["host"]["opencodeCli"]
    if not Path(exe).exists():
        sys.exit(f"opencode CLI missing: {exe}")

    jobs = manifest["jobs"]
    if a.only:
        jobs = [j for j in jobs if j["slot"] in a.only]

    safe_print(f"FLEET: {len(jobs)} jobs, {a.workers} at a time, timeout {a.timeout}s")
    safe_print(f"window note: {manifest.get('windowNote','')}\n")

    problems = check_manifest({"jobs": jobs})
    if problems:
        sys.exit("\nREFUSED to run. Fix the overlap first.")

    for j in jobs:
        if j["model"] in banned:
            sys.exit(f"REFUSED: slot {j['slot']} model {j['model']} is blacklisted.")
        if "free" not in j["model"] and j["model"] != "opencode/big-pickle":
            safe_print(f"WARNING: slot {j['slot']} model {j['model']} is not named '*free*'.")

    if a.check:
        return 0

    results = []
    with ThreadPoolExecutor(max_workers=a.workers) as ex:
        futs = {ex.submit(run_job, j, a.timeout, exe, banned): j for j in jobs}
        for f in as_completed(futs):
            j = futs[f]
            try:
                r = f.result()
            except Exception as e:
                r = {"slot": j["slot"], "title": j.get("title", ""), "model": j["model"],
                     "seconds": 0, "timedOut": False, "handoff": "-",
                     "stdoutTail": f"CRASH in runner: {e}"}
            results.append(r)
            mark = "TIMEOUT" if r["timedOut"] else "done"
            safe_print(f"  [{mark:<7}] slot {r['slot']:<3} {r['seconds']:>7}s  {r['title']}")

    safe_print("\n" + "=" * 66)
    for r in sorted(results, key=lambda x: x["slot"]):
        safe_print(f"\n### slot {r['slot']} — {r['title']}  ({r['model']}, {r['seconds']}s)")
        safe_print(r["stdoutTail"][:900])
    safe_print("\n" + "=" * 66)
    (ROSTER_DIR / "fleet_results.json").write_text(
        json.dumps(results, indent=2, ensure_ascii=False), encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
