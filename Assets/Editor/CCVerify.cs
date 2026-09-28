// Agent #1 - verification probe for the owner's complaints:
//   (1) pressing Play showed only sky, no car, no camera
//   (2) the W key did not drive the car
//
// v2: adds a DRIVE phase (inject W through the Input System exactly like
// CCDriveTest.cs, which compiles and ran) so one run proves BOTH:
//   - what the first play frame actually shows (rays + capture)
//   - whether the car drives with W in the CURRENT compiled state
//
// [InitializeOnLoad] + SessionState pattern, proven in CCDriveTest.cs.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class CCVerify
{
    const string SCENE = "Assets/Scenes/Core/Core.unity";
    const string OUT = @"D:\CC_GAME_1\ops\shots\real";
    const string PHASE = "CCVerify2.phase";
    const string DONE = "CCVerify2.done";

    static int _ticks;
    static readonly List<string> Log = new List<string>();
    static void Say(string s) { Log.Add(s); Debug.Log("[VERIFY] " + s); }

    static CCVerify()
    {
        EditorApplication.update += Pump;
    }

    static void Pump()
    {
        if (SessionState.GetBool(DONE, false)) { EditorApplication.update -= Pump; return; }
        _ticks++;
        if (EditorApplication.isCompiling) return;

        int phase = SessionState.GetInt(PHASE, 0);
        try
        {
            switch (phase)
            {
                case 0:
                    if (EditorApplication.isUpdating) return;
                    if (_ticks < 10) return;
                    EditorApplication.update -= Pump;
                    Start();
                    SessionState.SetInt(PHASE, 1);
                    break;

                case 1:   // first tick inside play mode = the owner's first frame
                    if (!EditorApplication.isPlaying) return;
                    SessionState.SetInt(PHASE, 2);
                    MeasureCamera("EARLY");
                    var c0 = Camera.main;
                    if (c0 != null) Shoot(c0, "06_verify_first_frame.png");
                    break;

                case 2:   // converged state
                    if (_ticks > 90)
                    {
                        MeasureCamera("LATE");
                        var c1 = Camera.main;
                        if (c1 != null) Shoot(c1, "07_verify_playmode.png");
                        SessionState.SetInt(PHASE, 3);
                    }
                    break;

                case 3:   // now inject W and drive for ~210 frames
                    if (_ticks > 110)
                    {
                        BeginDrive();
                        SessionState.SetInt(PHASE, 4);
                    }
                    break;

                case 4:
                    if (_ticks > 330)
                    {
                        EndDrive();
                        Finish();
                        SessionState.SetInt(PHASE, 5);
                    }
                    break;
            }
        }
        catch (Exception e) { Say("FATAL at phase " + phase + ": " + e.GetType().Name + " " + e.Message); Finish(); SessionState.SetInt(PHASE, 5); }
    }

    static void Start()
    {
        Say("=== CC VERIFY ===");
        EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Single);
        Say("scene loaded from disk (camera fix applied), entering play mode");
        EditorApplication.EnterPlaymode();
    }

    static Vector3 _before;
    static GameObject _kart;

    static void MeasureCamera(string tag)
    {
        _kart = GameObject.Find("Kart");
        Say(tag + " Kart = " + (_kart ? _kart.transform.position.ToString("F2") : "*** MISSING ***"));

        var cam = Camera.main;
        if (cam == null) { Say(tag + " Camera.main = NONE (that alone is the sky-image cause: no camera)"); return; }
        Say(tag + " camera pos=" + cam.transform.position.ToString("F2") +
            " euler=" + cam.transform.eulerAngles.ToString("F1") +
            "  fwd=" + cam.transform.forward.ToString("F2"));

        var fc = UnityEngine.Object.FindObjectOfType<FollowCam>();
        Say(tag + " FollowCam = " + (fc ? "present target=" + (fc.target ? fc.target.name : "*** NULL ***") : "*** MISSING ***"));

        var r = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;
        if (Physics.Raycast(r, out hit, 300f))
            Say(tag + " CENTER-RAY -> " + hit.collider.gameObject.name + " dist=" + hit.distance.ToString("F1"));
        else
            Say(tag + " CENTER-RAY -> NOTHING (>=300m)  <= unbroken sky");

        var r2 = cam.ViewportPointToRay(new Vector3(0.5f, 0.42f, 0));
        if (Physics.Raycast(r2, out hit, 300f))
            Say(tag + " CAR-BAND-RAY -> " + hit.collider.gameObject.name + " dist=" + hit.distance.ToString("F1"));
        else
            Say(tag + " CAR-BAND-RAY -> NOTHING");

        var rd = new Ray(cam.transform.position, Vector3.down);
        if (Physics.Raycast(rd, out hit, 200f))
            Say(tag + " DOWN-RAY -> " + hit.collider.gameObject.name + " dist=" + hit.distance.ToString("F1"));
        else
            Say(tag + " DOWN-RAY -> NOTHING (camera above no ground)");
    }

    static void BeginDrive()
    {
        Say("--- DRIVE phase: injecting W ---");
        var kb = Keyboard.current;
        if (kb == null) { Say("*** Keyboard.current is NULL - W would never drive ***"); return; }
        var press = new KeyboardState();
        press.Set(Key.W, true);
        InputSystem.QueueStateEvent(kb, press);
        if (_kart != null) _before = _kart.transform.position;
        Say("kart BEFORE drive pos=" + (_kart ? _kart.transform.position.ToString("F3") : "?") +
            "  (expect ~70 m after ~210 frames at speed 20)");
    }

    static void EndDrive()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            var press = new KeyboardState();
            press.Set(Key.W, false);
            InputSystem.QueueStateEvent(kb, press);
        }
        if (_kart == null) return;
        var after = _kart.transform.position;
        float d = Vector3.Distance(_before, after);
        Say("kart AFTER  pos=" + after.ToString("F3"));
        Say("*** W-DRIVE MOVED " + d.ToString("F3") + " m in ~210 frames *** " +
            (d > 1f ? "=> W DRIVES THE CAR" : "=> W DID NOT MOVE THE CAR"));
        var cam = Camera.main;
        if (cam != null)
        {
            Say("camera after drive pos=" + cam.transform.position.ToString("F2") +
                " euler=" + cam.transform.eulerAngles.ToString("F1") +
                "   <-- FollowCam trailing = true if z moved");
            Shoot(cam, "08_verify_driving.png");
        }
    }

    static void Finish()
    {
        EditorApplication.update -= Pump;
        try
        {
            Directory.CreateDirectory(OUT);
            File.WriteAllLines(Path.Combine(OUT, "verify.txt"), Log);
        }
        catch { }
        Say("verification done, exiting play mode");
        SessionState.SetBool(DONE, true);
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
    }

    static void Shoot(Camera cam, string file)
    {
        try
        {
            const int w = 1280, h = 720;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            var prevT = cam.targetTexture;
            cam.targetTexture = rt;
            var req = new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            RenderPipeline.SubmitRenderRequest(cam, req);
            cam.targetTexture = prevT;
            var t = new Texture2D(w, h, TextureFormat.RGB24, false);
            var prevA = RenderTexture.active; RenderTexture.active = rt;
            t.ReadPixels(new Rect(0, 0, w, h), 0, 0); t.Apply();
            RenderTexture.active = prevA;
            var b = t.EncodeToPNG();
            File.WriteAllBytes(Path.Combine(OUT, file), b);
            var px = t.GetPixels32();
            var u = new HashSet<uint>();
            for (int i = 0; i < px.Length; i += 977) u.Add(((uint)px[i].r << 16) | ((uint)px[i].g << 8) | px[i].b);
            long r = 0, g = 0, bl = 0; foreach (var c in px) { r += c.r; g += c.g; bl += c.b; }
            r /= px.Length; g /= px.Length; bl /= px.Length;
            Say("CAPTURE " + file + " " + b.Length + "b colours=" + u.Count +
                " mean=(" + r + "," + g + "," + bl + ")" + (u.Count < 12 ? "  FLAT" : "  HAS CONTENT"));
            UnityEngine.Object.DestroyImmediate(t); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        }
        catch (Exception e) { Say("CAPTURE FAILED " + e.GetType().Name + ": " + e.Message); }
    }
}