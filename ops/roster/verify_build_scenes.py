#!/usr/bin/env python3
"""
Verify the Unity build scene list against what is actually on disk.

PowerShell's Get-ChildItem -Recurse over a 12 GB Assets tree silently drops
files, which made it report every GUID as dangling. Python's os.walk does not
lie. This is the tool that decides whether the build list is healthy.

    python verify_build_scenes.py
    python verify_build_scenes.py --json
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

ROOT = Path(r"D:\CC_GAME_1")
BUILD_SETTINGS = ROOT / "ProjectSettings" / "EditorBuildSettings.asset"

GUID_RE = re.compile(r"^guid:\s*([0-9a-fA-F]{32})\s*$", re.M)


def scan_scene_guids() -> dict[str, Path]:
    """Map guid -> scene path for every *.unity under Assets/."""
    out: dict[str, Path] = {}
    assets = ROOT / "Assets"
    for dirpath, dirnames, filenames in os.walk(assets):
        dirnames[:] = [d for d in dirnames if d not in {"Library", "Temp", "obj"}]
        for fn in filenames:
            if not fn.endswith(".unity.meta"):
                continue
            mp = Path(dirpath) / fn
            try:
                m = GUID_RE.search(mp.read_text(encoding="utf-8", errors="replace")[:400])
            except OSError:
                continue
            if m:
                out[m.group(1).lower()] = Path(dirpath) / fn[: -len(".meta")]
    return out


def parse_build_entries() -> list[dict]:
    raw = BUILD_SETTINGS.read_text(encoding="utf-8", errors="replace")
    entries = []
    for block in re.findall(r"-\s*enabled:\s*(\d+)(.*?)(?=\n\s*-\s*enabled:|\Z)", raw, re.S):
        enabled, body = block
        g = re.search(r"guid:\s*([0-9a-fA-F]{32})", body)
        entries.append({
            "enabled": enabled == "1",
            "guid": g.group(1).lower() if g else None,
        })
    return entries


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--json", action="store_true")
    a = ap.parse_args()

    if not BUILD_SETTINGS.exists():
        print(f"MISSING: {BUILD_SETTINGS}")
        return 2

    guids = scan_scene_guids()
    entries = parse_build_entries()

    rows = []
    for i, e in enumerate(entries, 1):
        if not e["guid"]:
            rows.append({**e, "index": i, "state": "NO_GUID", "path": None})
            continue
        p = guids.get(e["guid"])
        if p:
            # Unity stores paths with forward slashes regardless of platform.
            # str(relative_to) gives backslashes on Windows, so normalise both
            # sides or every scene reads as "not in build".
            rows.append({**e, "index": i, "state": "OK",
                         "path": p.relative_to(ROOT).as_posix()})
        else:
            rows.append({**e, "index": i, "state": "DANGLING", "path": None})

    world = sorted((ROOT / "Assets" / "Scenes" / "World").glob("*.unity"))
    in_build = {r["path"] for r in rows if r["path"]}
    unbuilt = [w for w in world if w.relative_to(ROOT).as_posix() not in in_build]

    report = {
        "scenesOnDisk": len(guids),
        "worldScenes": len(world),
        "buildEntries": len(entries),
        "ok": sum(1 for r in rows if r["state"] == "OK"),
        "dangling": sum(1 for r in rows if r["state"] == "DANGLING"),
        "worldScenesInBuild": len(world) - len(unbuilt),
        "worldScenesUnbuilt": len(unbuilt),
        "rows": rows,
    }

    if a.json:
        print(json.dumps(report, indent=2))
        return 0 if report["dangling"] == 0 else 1

    def enc(s: str) -> str:
        e = sys.stdout.encoding or "utf-8"
        return s.encode(e, "replace").decode(e, "replace")

    print(enc(f"scenes on disk (Assets/**.unity) : {report['scenesOnDisk']}"))
    print(enc(f"world scenes                     : {report['worldScenes']}"))
    print(enc(f"build entries                    : {report['buildEntries']}"))
    print(enc(f"  resolved                       : {report['ok']}"))
    print(enc(f"  DANGLING                       : {report['dangling']}"))
    print(enc(f"world scenes in the build        : {report['worldScenesInBuild']}"))
    print(enc(f"world scenes NOT in the build    : {report['worldScenesUnbuilt']}"))
    print()
    for r in rows:
        mark = "OK      " if r["state"] == "OK" else "DANGLING"
        label = r["path"] or r["guid"]
        print(enc(f"  [{r['index']:>2}] {mark} {'(disabled)' if not r['enabled'] else '         '} {label}"))

    return 0 if report["dangling"] == 0 else 1


if __name__ == "__main__":
    import os  # noqa: E402  (used in scan_scene_guids)
    raise SystemExit(main())
