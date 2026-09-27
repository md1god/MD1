# CC_GAME_1 — Agent Board

Project: `D:\CC_GAME_1` · Unity `6000.0.84f1` · dispatcher: `ops/roster/dispatch.py`

## Roster

| agent | model | cost | role |
|---|---|---|---|
| `space-bunny` | `opencode/space-bunny-free` | **$0** | primary coder |
| `muse-spark` | `opencode-go/muse-spark-1.3-contributor` | $0 (contributor tier) | architect / reviewer |
| `qwen-coder` | `opencode-go/kimi-k2.7-code` | **paid** | hard algorithms — needs approval |
| `grok-build` | `opencode/grok-build-0.1` | **paid** | adversarial review — needs approval |
| `deepseek-flash` | `opencode-go/deepseek-v4-flash` | **paid** | bulk edits — needs approval |

**$0 rule:** only `space-bunny` and `muse-spark` may be dispatched without asking.
The other three bill real money — ask first.

## Ground rules

1. **One agent per file at a time.** `dispatch.py` takes a `DISPATCH.lock` for this reason.
2. **Never touch `Library/`, `Temp/`, `Logs/`.** Unity owns those.
3. **Never edit a `.cs` file without its `.meta`.** Unity pairs them by GUID; breaking the pair breaks every prefab reference to that script.
4. **Verify before believing.** A handoff report is a *claim*. `verify.py` is the *evidence*.
5. **Report format:** what changed, what was verified, what is still broken. No prose.

## Current state (measured 2026-09-27)

| fact | value |
|---|---|
| world scenes | 121 in `Assets/Scenes/World/` |
| build-settings scenes | 12 (Core + 11) — the other 110 are unreachable from a build |
| `SampleScene.unity` | **absent** — the old 371MB freeze source is gone |
| Unity Editor | running, PID 11152 / 11448 |
| last `Assembly-CSharp.dll` | 6:24 AM — predates all of today's work |
| project size | ~6.2 GB under `Assets/` |

## Task board

| id | task | agent | state |
|---|---|---|---|
| T1 | audit the 12-scene build list vs 121 world scenes | `space-bunny` | queued |
| T2 | review T1's output adversarially | `muse-spark` | queued |

## Dispatch

```powershell
cd D:\CC_GAME_1\ops\roster
python dispatch.py --list
python dispatch.py space-bunny tasks/T1.md --timeout 900
python verify.py T1
```

## Handoffs

`ops/roster/handoff/` — one markdown per run. Raw stdout in `ops/roster/logs/`.
