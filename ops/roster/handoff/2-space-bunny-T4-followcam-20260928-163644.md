# Handoff: 2-space-bunny / T4-followcam

- model: `opencode/space-bunny-free`
- seconds: 453.0
- timedOut: False
- stdout log: `D:\CC_GAME_1\ops\roster\logs\2-space-bunny-20260928-162911.out`

## Agent output

I'll start by reading the current `FollowCam.cs` and checking my task box.
Task box says [2.1] diagnose MainMenu, but your direct order is the FollowCam fix. I'll do your order and flag the mismatch in the report. First, verifying the `"Kart"` name actually exists in the scenes.
Searches timed out (large project). Switching to targeted PowerShell.
Now verifying the `"Kart"` GameObject name actually exists in `Core.unity`.
Confirmed: `Kart` object exists (line 32656) and FollowCam is attached (line 7715). Let me check the component's current `target` value and the Kart object's active state.
Key finding before editing: in `Core.unity:7716` the `target` is **already bound** (`{fileID: 1403897008}` = the `Kart` transform), and `Kart` is active (`m_IsActive: 1`, line 32656). So `GameObject.Find` will resolve. Making the minimal additive edit now.
Now verifying the result and confirming no other file was modified.
Only `FollowCam.cs` changed. Let me attempt a real compile check rather than guessing.
Braces/parens balanced. Attempting a true C# compile of just this file against Unity's own assemblies (read-only, output to temp).
Compiler runs now — just needs `netstandard`. Adding it.
Reference set needs the netfx shims too. Using a response file to avoid quoting issues.
Compile succeeded — **exit code 0, no errors**. Confirming the assembly is real and contains the type.
Verified. Now checking the handoff location before writing the report.
Report time. Writing to the fleet's existing handoff folder (`ops/roster/handoff/`, matching the `2-space-bunny-*` prefix used by all prior handoffs).
Report written, but I spotted generation glitches (corrupted words). Let me find and fix them.
Console encoding garbles display, but I'll fix the actual file content.
Final scan for any remaining corrupted characters.
Done. `FollowCam.cs` self-heals, compiles clean, one file touched.

## The change — `FollowCam.cs:12-16`

```csharp
void LateUpdate()
{
    if (!target)
    {
        GameObject kart = GameObject.Find("Kart");
        if (kart) target = kart.transform;
    }
    if (!target) return;          // ← old line 12, unchanged, now at 17
    ...
}
```

5 lines added, **0 deleted, 0 modified**. In `LateUpdate` so it retries every frame; the block only runs when `target` is null, so the `Find` cost is never paid otherwise. The old guard survives, so a failed `Find` gives byte-identical old behavior.

## Verified, not assumed
Real compile with Unity's own Roslyn against `netstandard2.1` + `UnityEngine.CoreModule` (19 refs): **exit code 0**, no errors, `FollowCam` present in the output DLL. I confirmed the harness wasn't vacuous — the same command produced `CS0012`/`CS0518` before I completed the reference set.

Only `FollowCam.cs` changed; `SimpleCar.cs`, `PlayerKart.cs`, and all other scripts untouched (`LastWriteTime` proves it). No `.meta` needed — no new file inside `Assets/`.

## Two things you need to know

**1. Your premise doesn't hold in the main scene.** In `Core.unity` the target is *already wired*:

- `Core.unity:7716` → `target: {fileID: 1403897008}`
- `Core.unity:32643/32656` → that fileID is the Transform of the GameObject named `Kart`
- `Core.unity:32661` → `m_IsActive: 1` (active, so `Find` would resolve it anyway)

So if the player saw only sky, an unbound `target` is **not** the cause there, and my fix will be a no-op in that scene. I did not claim to fix the reported bug — the real cause is undiagnosed. Worth noting too: the scene's serialized offset is `(0,3,-7)` vs the code default `(0,3,-6)` (`FollowCam.cs:7`); I left it alone since the scene overrides it.

**2. `YOUR_TURN` assigns me a different task.** `YOUR_TURN:51-68` gives me [2.1] diagnose `MainMenu.cs` — not the camera. I followed your direct order, so **[2.1] is still unstarted**. Please either update `YOUR_TURN` or re-dispatch it.

I wrote the report to `ops/roster/handoff/2-space-bunny-followcam-20260928.md` (where every other handoff lives, matching the `2-space-bunny-*` prefix) rather than a new root `handoff/` folder. It marks three items `UNVERIFIED` — Play-mode behavior, the root cause, and the other 121 chunks. Only a real run settles those; that's item [4] in `YOUR_TURN:41`, agent 1/5 territory, and I didn't touch it.

---

## stderr

```
> build · space-bunny-free
→ Read Assets/_Game/Scripts/FollowCam.cs 
→ Read YOUR_TURN 
> build · space-bunny-free
✗ Grep "FollowCam|Kart" failed
Error: Search timed out after 30 seconds. Consider using a more specific path or pattern.
✗ Glob "handoff/**" failed
Error: Search timed out after 30 seconds. Consider using a more specific path or pattern.
✗ Grep "m_Name: Kart" failed
Error: Search timed out after 30 seconds. Consider using a more specific path or pattern.
> build · space-bunny-free
$ Get-ChildItem -Path "D:\CC_GAME_1\Assets\_Game\Scripts" -Filter "*.cs" | Select-Object Name, Length
Name                   Length
----                   ------
CarPhysics.cs           16338
ChunkSceneManager.cs     3591
ChunkStreamer.cs         1130
CombatSystem.cs          4096
FollowCam.cs              572
GameManager.cs           5377
InventorySystem.cs       5462
InventoryUI.cs           8384
MainMenu.cs              4264
PlayerChunkTracker.cs    1595
PlayerKart.cs           10716
SimpleCar.cs             2485
SimplePlayer.cs          2695
StreamingDebugUI.cs      3227
WorldChunk.cs             302
WorldManager.cs          3658
WorldManifestParser.cs   3318
$ Get-ChildItem -Path "D:\CC_GAME_1" -Directory | Select-Object Name; Write-Output "---HANDOFF---"; if (Test-Path "D:\CC_GAME_1\handoff") { Get-ChildItem "D:\CC_GAME_1\handoff" } else { Write-Output "NO handoff dir" }
Name           
----           
.vscode        
Assets         
Library        
Logs           
ops            
Packages       
ProjectSettings
Temp           
test-clone     
UserSettings   
---HANDOFF---
NO handoff dir
$ Get-ChildItem -Path "D:\CC_GAME_1\Assets\Scenes" -Filter "Core.unity" -Recurse | Select-Object FullName, Length
FullName                                    Length
--------                                    ------
D:\CC_GAME_1\Assets\Scenes\Core\Core.unity 2024605
> build · space-bunny-free
$ Select-String -Path "D:\CC_GAME_1\Assets\Scenes\Core\Core.unity" -Pattern "m_Name: (Kart|.*Kart.*|.*Car.*)" | Select-Object LineNumber, Line -First 40
LineNumber Line          
---------- ----          
     32656   m_Name: Kart
$ $g = (Select-String -Path "D:\CC_GAME_1\Assets\_Game\Scripts\FollowCam.cs.meta" -Pattern "guid: (.+)" ).Matches.Groups[1].Value; Write-Output "FollowCam guid: $g"; Select-String -Path "D:\CC_GAME_1\Assets\Scenes\Core\Core.unity" -Pattern $g | Select-Object LineNumber, Line
FollowCam guid: 017574b877195164c8da120dc1818da6

LineNumber Line                                                                           
---------- ----                                                                           
      7715   m_Script: {fileID: 11500000, guid: 017574b877195164c8da120dc1818da6, type: 3}
> build · space-bunny-free
$ Get-Content "D:\CC_GAME_1\Assets\Scenes\Core\Core.unity" | Select-Object -Skip 7705 -First 25
--- !u!114 &330585548
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 330585543}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 017574b877195164c8da120dc1818da6, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  target: {fileID: 1403897008}
  offset: {x: 0, y: 3, z: -7}
  speed: 5
--- !u!1001 &333401549
PrefabInstance:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Modification:
    serializedVersion: 3
    m_TransformParent: {fileID: 878751212}
    m_Modifications:
    - target: {fileID: 100000, guid: 79ef5961b86276943ab5da38ae9f2e4b, type: 3}
      propertyPath: m_Name
$ Get-Content "D:\CC_GAME_1\Assets\Scenes\Core\Core.unity" | Select-Object -Skip 32620 -First 45
m_ScaleInLightmap: 1
  m_ReceiveGI: 1
  m_PreserveUVs: 0
  m_IgnoreNormalsForChartDetection: 0
  m_ImportantGI: 0
  m_StitchLightmapSeams: 1
  m_SelectedEditorRenderState: 3
  m_MinimumChartSize: 4
  m_AutoUVMaxDistance: 0.5
  m_AutoUVMaxAngle: 89
  m_LightmapParameters: {fileID: 0}
  m_SortingLayerID: 0
  m_SortingL
```
