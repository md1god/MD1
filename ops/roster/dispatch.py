#!/usr/bin/env python3
"""
CC_GAME_1 agent dispatcher.

Runs a task file through ONE named agent using the local OpenCode CLI, captures
the transcript, and writes a handoff report. Never runs two agents against the
same file at the same time - Unity .cs/.meta pairs corrupt under parallel writes.

Usage:
    python dispatch.py <agent> <task-file> [--timeout 900] [--dry-run]
    python dispatch.py --list
    python dispatch.py --probe <agent>          # cheap smoke test
"""
from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(r"D:\CC_GAME_1")
ROSTER = ROOT / "ops" / "roster" / "agents.json"
HANDOFF = ROOT / "ops" / "roster" / "handoff"
LOGDIR = ROOT / "ops" / "roster" / "logs"
LOCKFILE = ROOT / "ops" / "roster" / "DISPATCH.lock"

ANSI = re.compile(r"\x1b\[[0-9;]*[A-Za-z]")


def _safe_print(s: str) -> None:
    """Windows consoles default to cp1256/cp1252 and blow up on box-drawing and
    arrow characters. Agent reports are full of them. Encode defensively."""
    enc = sys.stdout.encoding or "utf-8"
    try:
        print(s)
    except UnicodeEncodeError:
        print(s.encode(enc, errors="replace").decode(enc, errors="replace"))


def strip_ansi(s: str) -> str:
    return ANSI.sub("", s)


def load_roster() -> dict:
    return json.loads(ROSTER.read_text(encoding="utf-8"))


def cli_path(roster: dict) -> str:
    p = roster["host"]["opencodeCli"]
    if not Path(p).exists():
        sys.exit(f"opencode CLI not found at {p}")
    return p


class Lock:
    """Cross-process mutex so two agents never write the project at once."""

    def __enter__(self):
        HANDOFF.mkdir(parents=True, exist_ok=True)
        try:
            fd = os.open(str(LOCKFILE), os.O_CREAT | os.O_EXCL | os.O_WRONLY)
            os.write(fd, f"{os.getpid()} {time.strftime('%Y-%m-%d %H:%M:%S')}".encode())
            os.close(fd)
        except FileExistsError:
            age = time.time() - LOCKFILE.stat().st_mtime
            if age > 3600:  # stale lock from a crashed run
                LOCKFILE.unlink(missing_ok=True)
                return self.__enter__()
            sys.exit(
                f"DISPATCH.lock held (age {age:.0f}s). Another agent is running.\n"
                f"Delete {LOCKFILE} if you are sure it is stale."
            )
        return self

    def __exit__(self, *exc):
        LOCKFILE.unlink(missing_ok=True)
        return False


def run_agent(agent: str, prompt: str, timeout: int, roster: dict) -> dict:
    spec = roster["agents"][agent]
    exe = cli_path(roster)
    LOGDIR.mkdir(parents=True, exist_ok=True)
    stamp = time.strftime("%Y%m%d-%H%M%S")
    out_f = LOGDIR / f"{agent}-{stamp}.out"
    err_f = LOGDIR / f"{agent}-{stamp}.err"

    cmd = [exe, "run", "--auto", "--model", spec["model"], prompt]
    t0 = time.time()
    timed_out = False
    with open(out_f, "w", encoding="utf-8", errors="replace") as fo, \
         open(err_f, "w", encoding="utf-8", errors="replace") as fe:
        try:
            subprocess.run(
                cmd, cwd=str(ROOT), stdout=fo, stderr=fe,
                timeout=timeout, stdin=subprocess.DEVNULL,
            )
        except subprocess.TimeoutExpired:
            timed_out = True
    dt = time.time() - t0
    return {
        "agent": agent,
        "model": spec["model"],
        "returncode": None if timed_out else _rc(out_f, err_f),
        "timedOut": timed_out,
        "seconds": round(dt, 1),
        "stdoutFile": str(out_f),
        "stderrFile": str(err_f),
        "stdout": strip_ansi(out_f.read_text(encoding="utf-8", errors="replace")),
        "stderr": strip_ansi(err_f.read_text(encoding="utf-8", errors="replace"))[:4000],
    }


def _rc(out_f: Path, err_f: Path) -> int:
    for f in (err_f, out_f):
        t = f.read_text(encoding="utf-8", errors="replace")
        m = re.findall(r"exit(?:ed)?(?:\s+with)?\s*(?:code)?\s*=?\s*(\d+)", t, re.I)
        if m:
            return int(m[-1])
    return 0 if out_f.stat().st_size > 0 else 1


def write_handoff(res: dict, task_name: str) -> Path:
    HANDOFF.mkdir(parents=True, exist_ok=True)
    stamp = time.strftime("%Y%m%d-%H%M%S")
    p = HANDOFF / f"{res['agent']}-{task_name}-{stamp}.md"
    body = res["stdout"].strip() or "_(agent produced no stdout)_"
    if res["stderr"].strip():
        body += "\n\n---\n\n## stderr\n\n```\n" + res["stderr"].strip() + "\n```"
    p.write_text(
        f"# Handoff: {res['agent']} / {task_name}\n\n"
        f"- model: `{res['model']}`\n"
        f"- seconds: {res['seconds']}\n"
        f"- timedOut: {res['timedOut']}\n"
        f"- stdout log: `{res['stdoutFile']}`\n\n"
        f"## Agent output\n\n{body}\n",
        encoding="utf-8",
    )
    return p


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("agent", nargs="?")
    ap.add_argument("task_file", nargs="?")
    ap.add_argument("--timeout", type=int, default=900)
    ap.add_argument("--probe", action="store_true")
    ap.add_argument("--list", action="store_true")
    ap.add_argument("--dry-run", action="store_true")
    a = ap.parse_args()

    roster = load_roster()

    if a.list or (not a.agent):
        for n, s in roster["agents"].items():
            print(f"{n:<14} {s['model']:<44} cost={s.get('costPerRun')}")
        print()
        print("BANNED (never dispatch):")
        for m in roster.get("banned", {}).get("models", []):
            print(f"  {m}")
        return 0

    if a.agent not in roster["agents"]:
        sys.exit(f"unknown agent '{a.agent}'. try --list")

    if a.probe:
        prompt = "Reply with exactly: PROBE-OK and nothing else."
        task = "probe"
    elif a.task_file:
        p = Path(a.task_file)
        if not p.is_absolute():
            # relative to the roster dir first, then the project root
            for base in (Path(__file__).resolve().parent, ROOT):
                cand = base / p
                if cand.exists():
                    p = cand
                    break
        if not p.exists():
            sys.exit(f"task file not found: {p}")
        prompt = p.read_text(encoding="utf-8", errors="replace")
        task = p.stem
    else:
        sys.exit("need a task file or --probe")

    if a.dry_run:
        print(f"would run {a.agent} ({roster['agents'][a.agent]['model']}) on {task}")
        print(prompt[:2000])
        return 0

    spec = roster["agents"][a.agent]
    if spec.get("costPerRun") != 0:
        sys.exit(
            f"REFUSED: agent '{a.agent}' has costPerRun={spec.get('costPerRun')!r}.\n"
            f"Only zero-cost agents may be dispatched."
        )
    banned = roster.get("banned", {}).get("models", [])
    if spec["model"] in banned:
        sys.exit(f"REFUSED: model '{spec['model']}' is on the banned list.")

    with Lock():
        res = run_agent(a.agent, prompt, a.timeout, roster)
    hp = write_handoff(res, task)
    print(f"[dispatch] {a.agent} -> {hp}")
    print(f"[dispatch] {res['seconds']}s timedOut={res['timedOut']}")
    print("-" * 60)
    _safe_print(res["stdout"].strip()[:6000] or "(no stdout)")
    if res["stderr"].strip():
        print("-" * 60)
        _safe_print("STDERR: " + res["stderr"][:1500])
    return 1 if res["timedOut"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
