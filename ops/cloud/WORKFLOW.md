# F-3 · `WORKFLOW.md` — how a cloud agent reads its task and writes its result

> ## The one-sentence design
>
> **Git is the message bus. One file per slot. One writer per file. One merger
> (`main`). Everything else is a lock you do not need because the filesystem
> already is one.**

Everything below is shaped by three facts proven in `github_access.md` and
`runware_free_models.md`, not by assumption:

1. **No free compute exists on Runware** — 354 rate cards checked, 0 free. The
   current cloud relay is *metered* ($0.211965 / 100 requests). So the runner
   must be genuinely free → **GitHub Actions on a public repo**.
2. **The current OpenClaw container cannot do this job** — no shell, no SSH, no
   device-identity credential path, and it cannot see `D:\CC_GAME_1`.
3. **The repo is big** — 71 MiB, 1,705 tracked files. A per-slot `git clone` is
   minutes of work. The workflow must minimise clones.

---

## 0. The mistake in the brief's first draft

The brief proposes:

> المهمة تُكتب كـ**commit** في فرع باسم `tasks/<slot>`
> النتيجة تُكتب كـ**commit** في `reports/<slot>`
> رقم 1 (أو وكيل محلي) يدمج الفروع

This is safe but **expensive and unnecessary**:

| Problem with per-slot branches | Consequence |
|---|---|
| One branch per slot means one clone + one push per slot | 5 agents × 71 MiB = ~355 MiB moved per cycle, for a few KB of markdown |
| `git push` to `tasks/<slot>` needs a credential **per slot** | more secrets to leak, exactly what we are trying to avoid |
| "competing on the same file" becomes possible in a way that is hard to reason about | two agents can each hold a stale `main` and both "win" locally |

**The fix: one integration branch, one file per slot.** Contention is prevented by
*path ownership*, not by branches. Branches are then used only where a genuine
history is needed — the integration PR.

---

## 1. The three-layer contract

```
┌─ LAYER 1  PATHS ────────────────────────────────────────────────┐
│  Every agent owns a directory. Two agents never share one.       │
│  Enforced by a manifest, checked before anything runs.          │
│  Kills 100% of write collisions, statically.                    │
└─────────────────────────────────────────────────────────────────┘
┌─ LAYER 2  FILES ────────────────────────────────────────────────┐
│  tasks/<slot>.md    one file, one writer, one reader            │
│  reports/<slot>.md  one file, one writer                        │
│  Even if two agents collide, they corrupt different files.      │
└─────────────────────────────────────────────────────────────────┘
┌─ LAYER 3  GIT ───────────────────────────────────────────────────┐
│  Agents push to integration/<slot>. Agent 1 is the ONLY writer  │
│  of main. Optimistic concurrency (fetch+rebase) is the lock.    │
└─────────────────────────────────────────────────────────────────┘
```

Layer 1 is the real lock. Layers 2 and 3 are consequences of it.

---

## 2. Layout

```
MD1/
├── ops/
│   ├── cloud/                    # ← agent F owns this, exclusively
│   │   ├── WORKFLOW.md           #   this file
│   │   ├── github_access.md
│   │   ├── runware_free_models.md
│   │   └── cloud_ws.mjs
│   │
│   ├── fleet.json                # THE MANIFEST. path ownership lives here.
│   ├── fleet.py                  # already has the overlap checker — reuse it
│   └── ...
│
├── YOUR_TURN                     # single human-facing task file (unchanged)
│
└── .github/workflows/
    ├── task-dispatch.yml         # runs the agent when tasks/<slot>.md changes
    └── verify.yml                # agent 5's build/test on every PR
```

### Task and report files (Layer 2)

```
ops/tasks/<slot>.md       # e.g. ops/tasks/F.md   — written by agent 1
ops/reports/<slot>.md     # e.g. ops/reports/F.md — written by the agent
```

Rules, all mechanically checkable:

| Rule | Enforced by |
|---|---|
| A slot writes **only** `ops/reports/<its-own-slot>.md` | manifest path check |
| A slot writes **only** inside its owned directory | `fleet.py` overlap check |
| A slot **never** writes `ops/tasks/*` (those belong to agent 1) | manifest |
| A slot **never** writes `main` | branch check, §5 |
| A report is **append-only** within its own file | review, and git history |
| A slot never edits another slot's report | manifest |

---

## 3. Reading the task

```bash
git fetch origin
git checkout -B integration/F origin/main      # or: git pull --rebase
cat ops/tasks/F.md                            # exactly one file, exactly your slot
```

`ops/tasks/F.md` is a contract, same idea as `YOUR_TURN` but per-slot:

```markdown
---
slot: F
owns:
  - ops/cloud/
depends_on: []          # other slots that must land first
budget_minutes: 30
model: opencode/big-pickle
---

# Task F
Run the probe. Write evidence to ops/reports/F.md. Touch nothing else.
```

`owns:` is not documentation — it is **input to `fleet.py --check`**, which already
exists and already refuses intersecting paths. Reuse it rather than writing a
second, weaker checker.

```powershell
cd D:\CC_GAME_1\ops\roster
python fleet.py --check          # exits non-zero on overlap, before any agent runs
```

---

## 4. Writing the result

```bash
git checkout -B integration/F
# ... do the work, only inside ops/cloud/ ...
cat > ops/reports/F.md <<'EOF'
# Report F — 2026-09-27
| Question | Answer | Evidence |
|---|---|---|
| Runware free model? | NO — 354 rate cards, 0 free | runware_free_models.md |
Spend caused: $0.00
EOF

git add ops/reports/F.md ops/cloud/
git commit -m "F: report + evidence"
git push origin integration/F
```

Then open a PR, or let agent 1's bot open one:

```bash
gh pr create --base main --head integration/F \
  --title "F: cloud + Runware findings" --body "slot=F, owns=ops/cloud/"
```

### The report contract

Every claim needs evidence, same rule as `AGENTS.md` §"قواعد كل وكيل":

| Field | Required |
|---|---|
| `file:line` for every factual claim | yes |
| the exact command run + its real output | yes |
| cost incurred | yes — must be `$0.00` |
| `UNVERIFIED:` if it could not be checked | yes, and stop rather than guess |

`runware_free_models.md` and `github_access.md` are written to this contract and
are the reference examples.

---

## 5. Merging — the only place contention is real

Agent 1 (or a merge bot) is the **sole writer of `main`**. This is the invariant
that makes the whole thing safe.

```bash
# agent 1 / merge bot, for each slot with a branch
git checkout main && git pull --rebase
git merge --no-ff integration/F -m "merge slot F"
```

If two slots both merged cleanly, great. If they conflict, the conflict is
**information, not an accident** — it means two agents touched the same file,
which violates the manifest. Resolve it by giving the file to one owner and
recording the violation:

```powershell
python fleet.py --check     # confirm the manifest, then fix it
```

### Optimistic concurrency, not locks

There is no lock file, no `flock`, no lease. Git already provides it:

```bash
git fetch origin
git rebase origin/main
# if the rebase conflicts -> someone else moved main -> you lost the race, re-read and retry
```

This is simpler than a lock and cannot deadlock, because it has no lock ordering.
**Do not introduce a distributed lock on top of git.** If you find yourself
writing one, the path ownership is wrong.

---

## 6. The runner — GitHub Actions, and why

The current cloud cannot be the runner (`github_access.md` §7). GitHub Actions can:

| Requirement | GitHub Actions on a public repo |
|---|---|
| Free | **Yes** — free minutes for public repos |
| Survives the PC being off | **Yes** — that is the entire point |
| Credential handling | `secrets.GITHUB_TOKEN` is minted per run; **no long-lived key in the repo** |
| Trigger | `on: push: paths: ['ops/tasks/**']` — the task file *is* the trigger |
| Limitation | **No GPU.** Text and code only. Never image/video. Never Unity rendering. |

```yaml
# .github/workflows/task-dispatch.yml
name: task-dispatch
on:
  push:
    branches: [main]
    paths: ['ops/tasks/**']

permissions:
  contents: write          # narrow on purpose - not admin, not packages

jobs:
  dispatch:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: list slots with a task
        id: slots
        run: |
          for f in ops/tasks/*.md; do
            [ -e "$f" ] || continue
            s=$(basename "$f" .md)
            echo "add::slot=$s" >> "$GITHUB_OUTPUT"
          done

      - name: refuse to run on an overlapping manifest
        run: python ops/roster/fleet.py --check

      - name: run each slot
        env:
          OPENCODE_MODEL: opencode/big-pickle   # free, per probe_results.json
        run: |
          for s in ${{ steps.slots.outputs.slot }}; do
            echo "::group::slot $s"
            # only free models; dispatch.py already refuses paid + blacklisted
            python ops/roster/dispatch.py "$s" --task "ops/tasks/$s.md" --timeout 1800
            echo "::endgroup::"
          done

      - name: open one PR per slot
        run: |
          for s in ${{ steps.slots.outputs.slot }}; do
            git checkout -B "integration/$s"
            git commit --allow-empty -m "slot $s: no changes"
            git push -f origin "integration/$s"
            gh pr create --base main --head "integration/$s" \
              --title "slot $s" --body "auto-dispatch" || true
          done
```

Two guards worth keeping even though they look redundant:

- **`fleet.py --check` before any agent runs** — a static overlap check. Catches
  the collision before it costs anything, not after.
- **`permissions: contents: write` only** — not `write-all`. If a prompt-injected
  agent asks for more scope, it cannot get it. This is the cheapest real defence
  against an autonomous agent with a write token.

---

## 7. Failure modes, and what to do

| Failure | Symptom | Response |
|---|---|---|
| Two agents edit one file | merge conflict on `main` | fix the manifest; give the file to one owner |
| Agent dies mid-run | stale branch, no PR | branch has no PR → nothing merged. Safe. Re-dispatch. |
| Agent runs forever | job exceeds limit | `--timeout 1800` in dispatch; GitHub's 6 h job cap is the backstop |
| Agent tries to touch `main` | branch protection | set `main` to require PR; make agent 1 the only approver |
| Agent wants to spend money | non-free model | `dispatch.py` refuses paid + blacklisted models — **keep that check, do not disable it** |
| Agent wants a secret | any request for a key | refuse. Free models and `GITHUB_TOKEN` are the whole budget. |
| Repo is too big to clone per slot | slow, 71 MiB | one clone per **job**, not per slot — loop inside a single runner, as above |

---

## 8. What this does and does not buy you

| Goal | Achieved? |
|---|---|
| Work continues when the PC is off | **Yes** — Actions runs on GitHub's hardware |
| Zero cost | **Yes** — public-repo Actions + the 4 free OpenCode models from `probe_results.json` |
| Less load on your machine | **Yes** — nothing runs locally |
| Unity build/test in the cloud | **No** — keep agent 5 local (§8 of `github_access.md`) |
| Asset generation in the cloud | **No** — no free GPU, and Runware is metered |

The scope in `AGENTS.md` is *asset distribution → open world → drivable car →
test → watch*. Of that, the cloud can take **the code and the testing of text
artifacts**. It cannot take the Unity build, and it cannot take image generation.
That split is the honest limit, and it is a real one.

---

## 9. Decision needed from agent 1

| # | Question | Recommendation |
|---|---|---|
| 1 | Per-slot branches (brief) or one file per slot (this doc)? | **One file per slot.** 5× less traffic on a 71 MiB repo, and contention is prevented by path ownership instead. |
| 2 | Runner? | **GitHub Actions**, not the OpenClaw container. It is free, it survives power-off, and the container is a metered relay that cannot see your files. |
| 3 | `ops/roster/fleet.json` — extend it to cloud slots, or a new manifest? | **Extend it.** The overlap checker already exists and already works. Do not write a second one. |
| 4 | Should `main` be branch-protected? | **Yes.** It makes "agent 1 is the only merger" an enforced rule instead of a convention. |
| 5 | `gateway.remote.url` still points at the dead `6c87e194`? | **Repoint or delete it.** It is why the local `openclaw` CLI hangs. |

---

*Agent F · 2026-09-27 · designed against measured constraints, not assumptions.
No key written to the repo. No commit pushed to `main`. Total spend: $0.00.*
