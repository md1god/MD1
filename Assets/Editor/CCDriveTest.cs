// Agent #1 - the actual acceptance test for this project: does the car drive?
//
// Everything the project is scoped to is in this one question. Assets are placed
// (report2.txt: World 764 renderers, City 685, Kart 50, Ground 900x900, URP
// bound to PC_RPAsset, real GPU, three renders with real detail). What has never
// been proven is the driving, because every previous attempt used -batchmode,
// which has no graphics device and returned a flat sky frame.
//
// The mechanism that made it work, and that this file relies on: a
// [InitializeOnLoad] editor script in the Editor that is ALREADY open. Editing
// the file forces a recompile (Ctrl+R), and the static constructor then runs
// inside the live Editor, which has an Intel HD 4600 and a real swapchain.
// RenderPipeline.SubmitRenderRequest is the URP-supported way to render a camera
// to a texture; Camera.Render() is the legacy path and is not correct for SRP.
//
// Input is injected directly through the Input System rather than by faking
// keystrokes, so the test does not depend on which panel has focus. SimpleCar
// reads Keyboard.current every Update (SimpleCar.cs:28) and returns early if it
// is null (SimpleCar.cs:29), so a null keyboard is itself a finding worth logging.
//
// Play mode triggers a domain reload, which re-runs every [InitializeOnLoad]
// constructor including this one. Phase is kept in SessionState so the test
// survives that reload instead of restarting.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class CCDriveTest
{
    const string SCENE = "Assets/Scenes/Core/Core.unity";
    const string OUT = @"D:\CC_GAME_1\ops\shots\real";
    const string PHASE = "CCDriveTest.phase";
    const string DONE = "CCDriveTest.done";

    static int _ticks;
    static readonly List<string> Log = new List<string>();
    static void Say(string s) { Log.Add(s); Debug.Log("[DRIVE] " + s); }

    static CCDriveTest()
    {
        // never a session flag check on the way in: the domain reload during play
        // mode has to re-arm the pump or the test dies half way through
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

                case 1:   // waiting for play mode to come up
                    if (!EditorApplication.isPlaying) return;
                    SessionState.SetInt(PHASE, 2);
                    break;

                case 2:
                    if (_ticks > 90)
                    {
                        BeginDrive();
                        SessionState.SetInt(PHASE, 3);
                    }
                    break;

                case 3:
                    if (_ticks > 300)
                    {
                        EndDrive();
                        SessionState.SetInt(PHASE, 4);
                    }
                    break;

                case 4:
                    if (_ticks > 360)
                    {
                        Finish();
                        SessionState.SetInt(PHASE, 5);
                    }
                    break;
            }
        }
        catch (Exception e) { Say("FATAL at phase " + phase + ": " + e.GetType().Name + " " + e.Message); Finish(); SessionState.SetInt(PHASE, 5); }
    }

    static Vector3 _before;
    static GameObject _kart;

    static void Start()
    {
        Say("=== DRIVE TEST ===");
        EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Single);
        Say("scene loaded, entering play mode");
        EditorApplication.EnterPlaymode();
    }

    static void BeginDrive()
    {
        Say("--- play mode running, frame ~90 ---");
        _kart = GameObject.Find("Kart");
        if (_kart == null) { Say("*** no Kart in play mode ***"); return; }
        var sc = _kart.GetComponent<SimpleCar>();
        Say("SimpleCar = " + (sc ? "attached" : "*** MISSING ***") +
            (sc ? "  speed=" + sc.speed + " turn=" + sc.turn + " driveDirect=" + sc.driveDirect : ""));
        _before = _kart.transform.position;
        Say("kart BEFORE  pos=" + _before.ToString("F3") + " euler=" + _kart.transform.eulerAngles.ToString("F1"));

        var kb = Keyboard.current;
        if (kb == null) { Say("*** Keyboard.current is NULL - SimpleCar.cs:29 would return every frame ***"); return; }
        Say("Keyboard.current = " + kb.displayName + "  -> injecting W (QueueStateEvent)");
        // isPressed is read-only on the control; the correct way to inject input is a
        // queued KeyboardState event through the LowLevel API.
        var press = new KeyboardState();
        press.Set(Key.W, true);
        InputSystem.QueueStateEvent(kb, press);

        var cam = Camera.main;
        Say("Camera.main = " + (cam ? cam.name + " at " + cam.transform.position.ToString("F2") : "NONE"));
    }

    static void EndDrive()
    {
        Say("--- frame ~300, releasing W ---");
        var kb = Keyboard.current;
        if (kb != null)
        {
            var press = new KeyboardState();
            press.Set(Key.W, false);
            InputSystem.QueueStateEvent(kb, press);
        }
        if (_kart == null) return;
        var after = _kart.transform.position;
        Say("kart AFTER   pos=" + after.ToString("F3") + " euler=" + _kart.transform.eulerAngles.ToString("F1"));
        float d = Vector3.Distance(_before, after);
        Say("*** MOVED " + d.ToString("F3") + " m in ~210 frames *** " +
            (d > 1f ? "=> THE CAR DRIVES" : "=> THE CAR DID NOT MOVE"));
        if (_kart.GetComponent<SimpleCar>() != null)
            Say("    (SimpleCar moves with transform.Translate at SimpleCar.cs:50, " +
                sc_speed().ToString("F1") + " m/s, so ~" + (sc_speed() * 210f / 60f).ToString("F1") + " m expected) ");

        var cam = Camera.main;
        if (cam != null)
        {
            Say("camera in play mode pos=" + cam.transform.position.ToString("F2") +
                " euler=" + cam.transform.eulerAngles.ToString("F1") +
                "   <-- FollowCam should be trailing the car");
            Shoot(cam, "05_playmode_after_driving.png");
        }
    }

    static float sc_speed()
    {
        var sc = _kart != null ? _kart.GetComponent<SimpleCar>() : null;
        return sc ? sc.speed : 0f;
    }

    static void Finish()
    {
        EditorApplication.update -= Pump;
        try
        {
            Directory.CreateDirectory(OUT);
            File.WriteAllLines(Path.Combine(OUT, "drive.txt"), Log);
        }
        catch { }
        Say("exiting play mode");
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
