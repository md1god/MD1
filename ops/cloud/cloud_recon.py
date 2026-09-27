"""
F-2 step 3: free RPC reconnaissance.

Before spending a single token of inference, squeeze out everything the Gateway
will answer for free:
  agents.list / agents.files.get  -> where the cloud's workspace lives
  worktrees.list                  -> is there a git checkout on the cloud
  usage.status / usage.cost       -> what the cloud has already cost

Prints each call verbatim. Costs $0.
"""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cloudlink  # noqa: E402
from openclaw_ws import GatewayError  # noqa: E402

CALLS = [
    ("agents.list", {}),
    ("worktrees.list", {}),
    ("usage.status", {}),
    ("usage.cost", {}),
    ("health", {}),
    ("status", {}),
    ("tasks.list", {}),
    ("commands.list", {}),
    ("models.authStatus", {}),
    ("agents.files.get", {}),
    ("agents.files.get", {"path": "AGENTS.md"}),
    ("agents.files.get", {"agentId": "main", "path": "AGENTS.md"}),
    ("agents.files.get", {"agentId": "main", "path": "/etc/os-release"}),
]

gw = cloudlink.link()
try:
    for method, params in CALLS:
        try:
            r = gw.call(method, params, timeout=40)
            txt = json.dumps(r, default=str, indent=2)
            print(f"\n=== {method} {json.dumps(params) if params else ''} ===")
            print(txt[:1800])
        except GatewayError as e:
            print(f"\n=== {method} {json.dumps(params) if params else ''} ===")
            print(f"  ERR {str(e)[:300]}")
        except Exception as e:  # noqa: BLE001
            print(f"\n=== {method} ===\n  EXC {type(e).__name__}: {str(e)[:200]}")
finally:
    gw.close()
