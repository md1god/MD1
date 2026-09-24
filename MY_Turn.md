# 🔄 MY_Turn.md - Central Coordination File

> **Central coordination hub for all agent teams. Single agent (Muse Spark) executes requests in turns. All teams coordinate here.**

---

## 🤖 **Primary Agent: Muse Spark (Fast, Free, 1M Context)**
- **Model:** `opencode/muse-spark-1.3-contributor-free`
- **Role:** Single execution agent on local device
- **Responsibility:** Execute ONE request at a time from the queue below
- **Rotation:** If rate limited → `opencode/mimo-v2.6-flash-free` → `openrouter/nex-agi/nex-n2.5-pro:free`

---

## 📋 **REQUEST QUEUE (Single Thread - FIFO)**

| Turn | Team | Request | Status | Assigned To | Started | Completed |
|------|------|---------|--------|-------------|---------|-----------|
| 1 | **Cloud Deploy** | Create HF Space `cc-game-gateway` with Dockerfile | ❌ BLOCKED | Muse Spark | 2026-09-24 11:30 | Waiting for user to create HF Space manually |
| 2 | **Cloud Deploy** | Set HF Space secrets (API keys) | ⏳ PENDING | Muse Spark | - | - |
| 3 | **Cloud Deploy** | Connect HF Space to GitHub `md1god/MD1` | ⏳ PENDING | Muse Spark | - | - |
| 4 | **Mobile Bridge** | Verify mobile pairing QR code works | ⏳ PENDING | Muse Spark | - | - |
| 5 | **Chat Agent** | Add Fast Chat Agent (runware/big-pickle) to gateway | ⏳ PENDING | Muse Spark | - | - |
| 4 | **Unity Build** | Build Production WebGL (GameScene + MainMenu + 9 chunks) | ⏳ PENDING | Muse Spark | - | - |
| 5 | **Unity Play** | Upload Production build to Unity Play | ⏳ PENDING | Muse Spark | - | - |
| 6 | **HF Sync** | Pull assets from `Dido599999/MD111` (AllStarModels) | ⏳ PENDING | Muse Spark | - | - |
| 7 | **GitHub Sync** | Pull latest from `md1god/MD1` | ⏳ PENDING | Muse Spark | - | - |
| 8 | **Blender Bridge** | Process D drive assets in Blender → glTF | ⏳ PENDING | Muse Spark | - | - |
| 9 | **Asset Distribution** | Distribute assets across 11 chunks (7 regions) | ⏳ PENDING | Muse Spark | - | - |
| 10 | **Unity Play** | Upload final build to play.unity.com | ⏳ PENDING | Muse Spark | - | - |

---

## 👥 **TEAM ROLES & LEADERS**

| Team | Leader | Members | Channel |
|------|--------|---------|---------|
| **Cloud Deploy** | `cloud-lead` | hf-sync, github-sync, unity-cloud-bridge | `#cloud-deploy` |
| **Mobile/Chat** | `mobile-lead` | mobile-bridge, fast-chat, blender-bridge | `#mobile-chat` |
| **Unity Build** | `unity-lead` | unity-builder, unity-cloud-bridge, error-reviewer | `#unity-build` |
| **Asset Pipeline** | `asset-lead` | hf-sync, github-sync, blender-bridge, asset-scout | `#asset-pipeline` |
| **Monitoring** | `monitor-lead` | monitor, game-folder-monitor, hf-repo-monitor, github-repo-monitor | `#monitoring` |

---

## 🔄 **EXECUTION PROTOCOL**

### **For Muse Spark (Single Agent):**
```
1. READ this file → check first PENDING request
2. EXECUTE that request completely
4. UPDATE status to COMPLETED with timestamp
5. WRITE result to memory/ folder
6. REPEAT for next PENDING
```

### **For Team Leaders (Monitor Only):**
```
- READ this file every 5 minutes
- DO NOT execute - only monitor queue
- If Muse Spark stuck > 10 min → escalate in this file
- Report status in team channel
```

---

## 📊 **STATUS DEFINITIONS**
- `⏳ PENDING` - Waiting for turn
- `🔄 IN_PROGRESS` - Muse Spark executing
- `✅ COMPLETED` - Done with result
- `❌ BLOCKED` - Needs external input
- `❌ FAILED` - Error, needs retry

---

## 📝 **EXECUTION LOG**

| Time | Turn | Action | Result |
|------|------|--------|--------|
| 2026-09-24 10:15 | - | File created | MY_Turn.md initialized |
| 2026-09-24 11:30 | 1 | Create HF Space `cc-game-gateway` | ❌ BLOCKED - User must create HF Space manually at https://huggingface.co/new-space |

---

## 🚨 **ESCALATION RULES**
1. If Muse Spark fails 3 times → Switch to fallback model
2. If blocked > 15 min → Team Leader adds note here
3. If critical failure → All teams notified via this file

---

## 📁 **ASSOCIATED FILES**
- `memory/` - Execution results stored here
- `.opencode/reports/latest.md` - Monitor reports
- `README.md` - Public status (verified facts only)
- `readme111.md` - Private secrets (never committed)

---

## 🎯 **CURRENT PRIORITY**
**Turn 1: Create HF Space `cc-game-gateway` with Dockerfile**

---

*Last Updated: 2026-09-24 | Next Review: Every 5 minutes by Monitor*