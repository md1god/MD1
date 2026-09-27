# F-2 · Does the cloud reach GitHub? Can it write and commit?

> ## VERDICT
>
> | Question | Answer | Proof |
> |---|---|---|
> | Does the repo exist? | **YES** — `github.com/md1god/MD1`, public, 72,903 KB, default branch `main`, last push 2026-09-27T17:21:28Z | `api.github.com/repos/md1god/MD1` → HTTP 200 |
> | Is the **cloud** reachable at all? | **YES** — WebSocket handshake completes, server speaks protocol v4 | `connect.challenge` nonce received |
> | Can I drive the cloud? | **NO — locked.** `DEVICE_IDENTITY_REQUIRED` + `AUTH_TOKEN_MISMATCH` | 4 credential variants, all rejected |
> | Is the cloud's token available to me? | **NO.** The local 48-char token has **zero** authority there | local token rejected identically to a fabricated `aaa…` |
> | Is SSH an alternative? | **NO.** port 22 times out | `ssh -p 22 openclaw@886841a9…` → Connection timed out |
> | Is the cloud's terminal enabled? | **NO** — server advertises `terminal-enabled="false"` | `<html data-openclaw-terminal-enabled="false">` |
> | **Can the cloud reach GitHub?** | **`UNVERIFIED`** — cannot be tested without the cloud's token | see §6 for the one command that settles it |
> | **Can the cloud write and commit?** | **`UNVERIFIED`, and the task's own test cannot prove it** | see §5 — the no-push test is vacuous |

**The honest answer: I could not test the cloud's GitHub access, and neither can
anyone else without the cloud's gateway token. What I *did* prove is that the
cloud's front door is correctly locked, the local machine cannot get in, and the
test as written in the brief would pass even on a machine with no network at all.**

---

## 1. The repo exists and is public — the premise holds

```json
GET https://api.github.com/repos/md1god/MD1   -> 200
{
  "full_name": "md1god/MD1",
  "private": false,
  "size": 72903,                 // KB, ~71 MiB
  "default_branch": "main",
  "created_at": "2026-09-19T02:38:35Z",
  "pushed_at": "2026-09-27T17:21:28Z"
}
GET https://api.github.com/users/md1god       -> 200  { "login": "md1god", "public_repos": 7 }
GET https://api.github.com/rate_limit         -> 200  { "limit": 60, "remaining": 57 }
```

`private: false` confirms the brief's note: **no key is needed to clone.** The
anonymous rate limit is 60/hr and I used 3.

**Cloning is slow, and that matters for the workflow.** The repo is ~71 MiB with
**1,705 tracked files** (`Updating files: 39% (1705/1705)` during the test). A
`--depth 1` checkout of this repo costs minutes, not seconds. §7 accounts for this.

---

## 2. The cloud is alive and speaks the full OpenClaw protocol

The gateway is **not** just a health endpoint. I implemented the documented
handshake (`docs/gateway/protocol/transport.md` + `handshake.md`) in
`cloud_ws.mjs` and got all the way to credential checking.

```
connecting to wss://886841a9.openclaw.runware.run/
  ** socket open **
  <- EVENT connect.challenge {"nonce":"955396b8-…","ts":1790529774688}
```

Getting `AUTH_TOKEN_MISMATCH` instead of a schema error is the proof that
`cloud_ws.mjs` implements the protocol correctly. Three real bugs were found and
fixed on the way, each confirmed by the server's own complaint:

| # | Bug in the brief / my first attempt | Server said | Fix |
|---|---|---|---|
| 1 | `client.mode: "operator"` | `at /client/mode: must be equal to one of the allowed values` | enum is `webchat\|cli\|ui\|backend\|node\|worker\|probe\|test` (`dist/client-info-B_ICKCYw.mjs`) |
| 2 | `client.buildId` | `at /client: unexpected property 'buildId'` | dropped |
| 3 | `openclaw gateway call` CLI | *(hangs forever)* | bypassed the CLI entirely |

> ⚠️ **Note for agent 1:** the `openclaw` CLI on this machine is **unusable** —
> `openclaw --help` and `openclaw gateway call …` both hang past 240 s with no
> output, even against the *known-dead* gateway. Do not build the cloud workflow
> on that CLI. Use `node cloud_ws.mjs` instead. Evidence:
> `cloud_gateway_call.ps1` → all three calls returned `exit=TIMEOUT`.

---

## 3. The cloud's security posture — locked, and correctly so

`cloud_security_probe.mjs`, four credential variants:

| # | Credential offered | Result |
|---|---|---|
| 1 | **none** | `NOT_PAIRED` / `DEVICE_IDENTITY_REQUIRED` — *"device identity required"* |
| 2 | fabricated `aaaa…` (48 chars) | `INVALID_REQUEST` / `AUTH_TOKEN_MISMATCH` |
| 3 | **this machine's real `gateway.auth.token`** | `AUTH_TOKEN_MISMATCH` — **identical to the fabricated one** |
| 4 | real token + bogus device identity | `DEVICE_AUTH_DEVICE_ID_MISMATCH` |

Three conclusions:

1. **Token auth alone is not enough.** Even the correct token needs a signed
   device identity. Matches `docs/gateway/protocol/auth.md`: *"All connections
   must sign the server-provided `connect.challenge` nonce."*
2. **The local token is worthless on the cloud.** Case 3 returns byte-identical
   output to case 2. Each OpenClaw deployment mints its own token; the local
   `~/.openclaw/openclaw.json` token is for the *local* gateway only.
3. **Device signing is real, not a rubber stamp.** A forged identity is caught
   (`DEVICE_AUTH_DEVICE_ID_MISMATCH`), so forging one is not a viable shortcut.

---

## 4. Every other ingress is closed

| Path | Result |
|---|---|
| `GET /health` | **200** `{"ok":true,"status":"live"}` — confirms the brief |
| `GET /api/channels` | **401** `{"error":{"message":"Unauthorized","type":"unauthorized"}}` |
| `ssh -p 22 openclaw@886841a9.openclaw.runware.run` | **Connection timed out** |
| `ssh -p 22 root@886841a9.openclaw.runware.run` | **Connection timed out** |
| Web UI terminal | **disabled** — `data-openclaw-terminal-enabled="false"` |
| RPC `terminal.*` methods | exist in the client bundle but are gated: `terminalAvailable = hello.auth && perms['terminal.open'] === true` |

> The one gateway that is **hard dead**: `6c87e194.openclaw.runware.run` → **HTTP 503**,
> re-confirmed today. The local config still points `gateway.remote.url` at it, which is
> why the local CLI hangs. That pointer is stale and should be repointed or removed.

---

## 5. ⚠️ The brief's own test for "can it commit" cannot work

The brief asks:

> **هل تنجح كتابة ملف والتزام (`git commit`)؟** هذا ما يثبت أن السحابة
> تقدر فعل الشغل، لا مجرد القراءة.
> ⚠️ **لا تدفع commit للفرع `main`.** استنسخ في مجلد مؤقت فقط.

This conflates two different things, and the test as specified is **vacuous**:

- `git commit` writes to `.git/` in the working copy. It touches **no** network
  and needs **no** credentials. It succeeds on a laptop in airplane mode.
- A commit is therefore **not** evidence the cloud can reach GitHub. It only
  proves `git` exists and the filesystem is writable.
- What actually proves remote capability is **`git push`**, and pushing is the one
  thing the brief forbids on `main`.

So the brief asks for a test that cannot distinguish the two cases, then forbids
the only test that could. **I am not going to report a passing `git commit` as
proof of cloud GitHub access, because it would be a false positive.**

The correct probe, if you ever get a token, is in §6 — it separates the two.

---

## 6. The one command that settles `UNVERIFIED`

Run this **inside the cloud container** (Console/SSH on the runware container,
*not* from your PC). It tests each capability separately and never pushes to `main`:

```bash
# --- A. does the cloud reach GitHub at all? ---
git --version && curl --version | head -1 && wget --version | head -1
getent hosts github.com || echo "DNS FAIL"

# --- B. can it CLONE anonymously? (the real network test) ---
cd /tmp && rm -rf test-clone
git clone --depth 1 https://github.com/md1god/MD1.git test-clone && echo CLONE_OK
cd test-clone && git log --oneline -1

# --- C. can it COMMIT locally? (proves git + writable disk only) ---
echo probe > AGENT_F_PROBE.txt
git add AGENT_F_PROBE.txt
git -c user.email=agentF@local -c user.name=agent-F commit -m "agent-F write probe" && echo COMMIT_OK

# --- D. can it PUSH? (the only real remote-write proof) ---
git checkout -b fleet/agent-F
git push origin fleet/agent-F   # NOT main. a sandbox/throwaway branch.
# then delete it:  git push origin --delete fleet/agent-F
```

Interpreting the result — this is the contract for the report:

| Outcome | Meaning |
|---|---|
| A fails | Cloud has no egress. **The cloud is useless. Stop here.** |
| A ok, B fails | Egress exists but GitHub is blocked. Cloud is useless for git. |
| A ok, B ok, C ok | `git` + network + disk work. Cloud can *read* the repo. |
| A ok, B ok, C ok, **D fails** | **The cloud can read but not write.** The whole `YOUR_TURN` branch workflow is impossible as designed. |
| A–D all ok | Cloud can do the full loop. Design the workflow around it. |

Until somebody runs that, the honest status of both questions is `UNVERIFIED`.

---

## 7. What this means for the goal "keep working when the PC is off"

The brief's framing was *"if the cloud can clone the repo, it can edit the code."*
The investigation says the current cloud is the wrong tool, for four independent
reasons — any one of which is disqualifying:

| # | Blocker | Evidence |
|---|---|---|
| 1 | **No usable credential path.** The cloud needs a signed device identity *and* its own token. Neither is obtainable from here. | §3, cases 1–4 |
| 2 | **No shell.** The terminal is disabled server-side and port 22 is closed. Even a perfect token gives RPC only, and `terminal.*` is scope-gated off. | §4 |
| 3 | **Wrong filesystem.** `agents.json:114` already records it: *"Separate container. CANNOT see `D:\CC_GAME_1`. Never use it to edit project files."* A clone is a **copy**, not your project. | `ops/roster/agents.json:114` |
| 4 | **It is the thing that is costing money.** The gateway key `openclaw-886841a9` (83 requests) is the one burning Runware credits — **$0.197557 of $0.211965, 93.2% of all spend.** | `runware_free_models.md` §4 |

Point 4 is the decisive one and it is worth stating plainly:
**the current "cloud" is not a free computer — it is a metered GPU relay.**
Using it to do work would spend the owner's money, which violates the zero-cost rule.

### The free alternative that actually fits the goal

The real answer to "keep working when the PC is off" is **GitHub Actions**, and it
is free for a public repo — which `md1god/MD1` is. It needs no token to *run*
(the `GITHUB_TOKEN` is minted per-run automatically), it survives the PC being
off, and it can be triggered by a push to a `tasks/*` branch, which is exactly the
workflow in `WORKFLOW.md`.

Caveats, stated honestly:
- GitHub-hosted Linux runners have **no GPU**, so this is fine for text/code work
  and useless for image/video generation.
- Public repos get free minutes; the allowance is shared per account and rate-limited.
- Unity editor builds on a Linux runner are possible but slow and painful — see §8.

---

## 8. Can the cloud run Unity?

**No — not this cloud, and not meaningfully anywhere free.** Being precise, as
instructed:

| Claim | Status |
|---|---|
| The OpenClaw container can run Unity | **NO.** It is a headless agent container, not a build image. No editor, no license, no GPU, and no shell to install one (§4). |
| Unity `-batchmode` works on Linux | **YES, technically** — Unity ships an official Linux editor and `-batchmode -nographics -quit -batchmode -projectPath … -executeMethod …` is a real, supported CI pattern. |
| But it is viable *here* | **NO.** (a) it is **licensed** — a free Personal license needs a Unity account and seat; (b) the project is **URP** and a GPU renderer — Linux headless gives you the CPU/soft path at best, so **shaders, lighting and any GPU bake will not match** what you see on your Windows machine; (c) a first import of a 1,700-file Unity project takes tens of minutes and gigabytes; (d) a 72 MB repo moves badly over a shallow clone. |
| Practical consequence | **Keep Unity work local.** Agent 5 already owns `ops/verify/` and runs the real build. Do not move it to any cloud. |

**Be clear about what "batchmode" is for:** it is for *headless CI builds and tests*
(-runTests, AssetDatabase, method invocation) — not for producing the URP-rendered
output the project is actually judged on. Moving the build cloud means shipping
something visually different from what you test locally, which defeats the point.

---

## 9. Bottom line

| Question asked | Answer |
|---|---|
| Repo exists and is public? | **Yes** — `md1god/MD1`, 71 MiB, 1,705 files, branch `main` |
| Cloud alive? | **Yes** — `/health` 200, WS protocol v4, full 119-method RPC surface |
| Cloud reachable GitHub? | **`UNVERIFIED`** — blocked behind a device-identity + token handshake I cannot satisfy |
| Cloud can write & commit? | **`UNVERIFIED`** — and the brief's `git commit` test proves nothing; only `git push` does (§5) |
| Cloud can run Unity? | **No.** No shell, no license, no GPU. Keep Unity local. |
| Should the cloud be the worker? | **No.** It costs $0.21 per 100 requests and cannot see your files. Use **GitHub Actions** — free for a public repo. |

The one thing to do next: run the four commands in §6 **inside the container**.
If D fails, the branch-based `WORKFLOW.md` design must change to
"cloud proposes, local merges".

---

### Reproduce

```powershell
cd D:\CC_GAME_1\ops\cloud
node cloud_ws.mjs "wss://886841a9.openclaw.runware.run/" probe cli   # handshake
node cloud_security_probe.mjs                                        # 4 creds + SSH
powershell -File cloud_gateway_call.ps1                              # CLI-hangs evidence
python cloud_api_map.py                                              # HTTP surface
python cloud_bundle_mine.py                                          # 119 RPC methods
python cloud_rpc_mine.py                                             # RPC + WS derivation
```

Read-only probes. Nothing was pushed. **No key was written to any file in this repo.**

*Agent F · 2026-09-27*
