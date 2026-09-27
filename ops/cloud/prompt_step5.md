The repo is ALREADY cloned at /tmp/fleetF — do NOT clone again. Do not use the `process poll` loop. Run each command separately, keep output short, and paste the raw output under each numbered heading. No commentary.

1. TOOLS
   for t in git curl wget python3 node npm bash; do printf "%s: " "$t"; command -v "$t" || echo MISSING; done
   git --version 2>&1 | head -1; node --version 2>&1

2. WRITE + COMMIT ON A THROWAWAY LOCAL BRANCH inside /tmp/fleetF (no push, no remote write, no secret in the file)
   cd /tmp/fleetF && git checkout -b fleet/agent-F 2>&1 && echo BRANCH_OK
   cd /tmp/fleetF && printf 'agent F cloud probe\n' > ops_cloud_probe.md && git add ops_cloud_probe.md && git -c user.email=fleet@local -c user.name=fleet commit -m 'fleet: agent F write+commit probe' 2>&1 | tail -3 && echo COMMIT_DONE
   cd /tmp/fleetF && git log --oneline -2 && echo "branch=$(git branch --show-current)" && echo "status:[$(git status --short)]" && echo "ahead_of_main=$(git rev-list --count main..HEAD 2>/dev/null || git rev-list --count master..HEAD 2>/dev/null)"

3. PUSH TEST (expect failure, no credentials - just prove it is refused)
   cd /tmp/fleetF && timeout 25 git push origin fleet/agent-F 2>&1 | tail -4; echo "push_exit=$?"

4. DISK / CPU / MEMORY
   df -h /tmp /home 2>&1 | head -5; nproc; free -m 2>&1 | head -2

5. UNITY
   for u in unity unity-editor Unity unityhub; do printf "%s: " "$u"; command -v "$u" || echo MISSING; done
   ls /opt 2>&1 | head -5; find / -maxdepth 4 -iname "Unity" -type f 2>/dev/null | head -3; echo UNITY_CHECK_DONE

6. THE AGENT WORKSPACE + the Dockerfile we just cloned
   ls -a /home/openclaw/.openclaw/workspace 2>&1 | head -20
   cd /home/openclaw/.openclaw/workspace && git log --oneline -3 2>&1 | head -5; git remote -v 2>&1 | head -3
   echo "--- /tmp/fleetF/Dockerfile.openclaw (first 25 lines) ---"; head -25 /tmp/fleetF/Dockerfile.openclaw 2>&1

Last line, exactly this format:
GIT_CLONE=OK WRITE_COMMIT=OK PUSH=REFUSED UNITY=NO
