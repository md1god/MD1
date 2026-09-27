#!/usr/bin/env python3
"""
Rebuild ProjectSettings/EditorBuildSettings.asset from the scenes that actually
exist on disk.

WHY THIS EXISTS
---------------
The project ships a build list of 12 entries. One of them (Core.unity) resolves.
The other 11 point at files that were deleted - `Scene_01_Sea_South.unity`
through `Scene_11_Sea_North.unity`. They never got replaced by the 121
`Chunk_X_Y_<Biome>.unity` scenes that replaced them.

Consequence: `ChunkSceneManager.cs:26` calls
    SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive)
which only resolves for scenes in the build list. With 0 of 121 chunks in the
build, the world never streams in. The game looks empty no matter what is on
disk. This is the "the game does not work when I run it" bug.

It also drops the 12 dangling GUIDs, which is what makes the list self-healing
on the next rebuild.

    python fix_build_settings.py            # dry run, prints the plan
    python fix_build_settings.py --apply    # writes the file (backs up first)
    python fix_build_settings.py --apply --verify
"""
from __future__ import annotations

import argparse
import os
import re
import shutil
import sys
from pathlib import Path

ROOT = Path(r"D:\CC_GAME_1")
TARGET = ROOT / "ProjectSettings" / "EditorBuildSettings.asset"
BACKUP = ROOT / "ops" / "roster" / "EditorBuildSettings.asset.bak"

GUID_RE = re.compile(r"^guid:\s*([0-9a-fA-F]{32})\s*$", re.M)
CHUNK_RE = re.compile(r"^Chunk_(\d+)_(\d+)_(.+)$")


def enc(s: str) -> str:
    e = sys.stdout.encoding or "utf-8"
    return s.encode(e, "replace").decode(e, "replace")


def core_guid() -> tuple[str, str] | None:
    p = ROOT / "Assets" / "Scenes" / "Core" / "Core.unity"
    if not p.exists():
        return None
    m = GUID_RE.search((p.with_suffix(p.suffix + ".meta")).read_text(
        encoding="utf-8", errors="replace")[:400])
    return (m.group(1).lower(), "Assets/Scenes/Core/Core.unity") if m else None


def chunk_scenes() -> list[tuple[int, int, str, str, str]]:
    """(x, y, biome, guid, project-relative path) for every world chunk."""
    out = []
    world = ROOT / "Assets" / "Scenes" / "World"
    for fn in sorted(os.listdir(world)):
        if not fn.endswith(".unity"):
            continue
        stem = fn[: -len(".unity")]
        m = CHUNK_RE.match(stem)
        if not m:
            continue
        x, y, biome = int(m.group(1)), int(m.group(2)), m.group(3)
        meta = world / (fn + ".meta")
        if not meta.exists():
            continue
        gm = GUID_RE.search(meta.read_text(encoding="utf-8", errors="replace")[:400])
        if not gm:
            continue
        out.append((x, y, biome, gm.group(1).lower(),
                    f"Assets/Scenes/World/{fn}"))
    return out


def keep_config_objects(old: str) -> list[str]:
    """Preserve m_configObjects - it binds the Input System action asset by GUID."""
    keep = []
    m = re.search(r"^(\s*m_configObjects:.*?)(?=^\s*m_UseUCBPForAssetBundles:)",
                  old, re.S | re.M)
    if m:
        keep = [ln.rstrip() for ln in m.group(1).rstrip().splitlines()]
    else:
        keep = ["  m_configObjects: {}"]
    return keep


def build_yaml(entries: list[tuple[str, str]], old: str) -> str:
    lines = [
        "%YAML 1.1",
        "%TAG !u! tag:unity3d.com,2011:",
        "--- !u!1045 &1",
        "EditorBuildSettings:",
        "  m_ObjectHideFlags: 0",
        "  serializedVersion: 2",
        "  m_Scenes:",
    ]
    for guid, path in entries:
        lines += ["  - enabled: 1", f"    path: {path}", f"    guid: {guid}"]
    lines += keep_config_objects(old)
    lines += ["  m_UseUCBPForAssetBundles: 0", ""]
    return "\n".join(lines)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--verify", action="store_true")
    a = ap.parse_args()

    core = core_guid()
    chunks = chunk_scenes()
    if not core:
        print("FATAL: Assets/Scenes/Core/Core.unity or its .meta is missing.")
        return 2
    if not chunks:
        print("FATAL: no Chunk_X_Y_* scenes found in Assets/Scenes/World.")
        return 2

    # WorldManager.cs:36 loads Chunk_0_5 at spawn. Put it at index 1 so the
    # first streamed chunk is already in the build's head.
    spawn = [c for c in chunks if c[0] == 0 and c[1] == 5]
    rest = sorted((c for c in chunks if c not in spawn), key=lambda c: (c[1], c[0]))
    ordered = spawn + rest

    entries = [(core[0], core[1])] + [(c[3], c[4]) for c in ordered]

    print(enc(f"Core.unity            : {core[1]}"))
    print(enc(f"chunks found on disk  : {len(chunks)}"))
    print(enc(f"spawn chunk at index 1: {'Chunk_0_5' if spawn else 'NOT FOUND'}"))
    print(enc(f"total entries to write: {len(entries)}"))
    print()
    print(enc("biome spread:"))
    from collections import Counter
    for biome, n in sorted(Counter(c[2] for c in chunks).items()):
        print(enc(f"  {biome:<8} {n}"))
    print()
    print(enc("first 6 in build order:"))
    for i, (g, p) in enumerate(entries[:6]):
        print(enc(f"  [{i}] {p}"))
    print(enc(f"  ... {len(entries) - 6} more"))

    if not a.apply:
        print()
        print("dry run. re-run with --apply to write.")
        return 0

    old = TARGET.read_text(encoding="utf-8", errors="replace")
    BACKUP.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(TARGET, BACKUP)
    TARGET.write_text(build_yaml(entries, old), encoding="utf-8", newline="\n")
    print()
    print(f"wrote {TARGET}")
    print(f"backup {BACKUP}")

    if a.verify:
        import subprocess
        rc = subprocess.run(
            [sys.executable, str(Path(__file__).with_name("verify_build_scenes.py"))],
            cwd=str(Path(__file__).parent),
        ).returncode
        print(f"verify exit code: {rc}")
        return rc
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
