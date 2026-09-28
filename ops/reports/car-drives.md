# Driver acceptance — 2026-09-28

Executed inside the live Editor (real GPU) via [InitializeOnLoad] CCDriveTest
(Assets/Editor/CCDriveTest.cs).

Fix history:
1. CCDriveTest.cs CS0200 x2 + CS0103 x1 (Assets/Editor/CCDriveTest.cs:131,141,150)
   - input injection now via InputSystem.QueueStateEvent + KeyboardState.Set(Key.W, ..)
   - bare `speed` -> sc_speed()
2. GPU crash on launch: memguard job memory cap 3.6 GB starved the shared-memory
   GPU (DXGI_ERROR_DEVICE_REMOVED 0x887A0005 on Intel HD 4600; then 0xC000041D in
   ScriptableRenderContext.Cull). Unity now launches plain. Pagefile on D: pinned
   24-32 GB remains the real guard against the freeze.
3. Camera home moved (8,4,-5)->(0,3,-3), pitched -16.7 deg at the Kart
   (Core.unity:7655-7656). FollowCam wiring verified at Core.unity:7718
   (target = Kart Transform 1403897008, offset (0,3,-7), speed 5).

Evidence (ops/shots/real/drive.txt):
- SimpleCar = attached  speed=20 turn=90 driveDirect=True
- kart BEFORE  pos=(0.000, 0.000, 4.000)
- kart AFTER   pos=(0.000, 0.000, 153.285)
- *** MOVED 149.285 m in ~210 frames *** => THE CAR DRIVES
- camera in play mode (0.00, 3.00, 143.42) — FollowCam trailing
- CAPTURE 05_playmode_after_driving.png 267992b colours=510 mean=(133,174,146)
- Independent pixel check: 1280x720, 146 colour buckets, light-blue sky 25%,
  green treeline dominant, tan ground. Not a flat frame.

VERDICT: PASS