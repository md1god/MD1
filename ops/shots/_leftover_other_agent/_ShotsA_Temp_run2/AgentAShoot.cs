// AgentAShoot.cs  --  Agent A: take REAL screenshots of the running game.
//
// SOURCE OF TRUTH: ops/shots/Shoot.cs   (my folder)
// Unity only compiles .cs under Assets/, so a TEMPORARY copy is mirrored to
// Assets/Editor/_ShotsA_Temp/AgentAShoot.cs for the batch run and deleted afterwards.
//
// The class is named AgentAShoot, NOT Shoot, on purpose: Assets/Mech/Scripts/Shoot.cs
// already declares an (empty stub) `class Shoot` in Assembly-CSharp, which would make
// `-executeMethod Shoot.Run` ambiguous.
//
// Run:
//   Unity.exe -batchmode -projectPath D:\CC_GAME_1 -executeMethod AgentAShot.Run \
//            -logFile D:\CC_GAME_1\ops\shots\shoot.log
// (NO -quit: the script quits itself with EditorApplication.Exit after Play Mode)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class AgentAShoot
{
    const int W = 1920, H = 1080;
    const string OutDir = "ops/shots";
    const string CoreScene = "Assets/Scenes/Core/Core.unity";

    const string PhaseKey = "ShotsA.Phase";
    const string T0Key = "ShotsA.T0";
    const int P_IDLE = 0, P_PLAYWAIT = 10, P_EDITWAIT = 20, P_DONE = 99;

    static int phase = P_IDLE;
    static double t0;
    static int playFrame = 0;
    static readonly StringBuilder SB = new StringBuilder();

    // ---------------------------------------------------------------- logging
    static void Log(string s)
    {
        Debug.Log("[SHOOT] " + s);
    }

    static string V(Vector3 v)
    {
        return string.Format(CultureInfo.InvariantCulture, "({0:F3}, {1:F3}, {2:F3})", v.x, v.y, v.z);
    }

    static Vector3 ParseV(string s)
    {
        var p = s.Split(',');
        return new Vector3(
            float.Parse(p[0].Trim(), CultureInfo.InvariantCulture),
            float.Parse(p[1].Trim(), CultureInfo.InvariantCulture),
            float.Parse(p[2].Trim(), CultureInfo.InvariantCulture));
    }

    // ------------------------------------------------------------------ entry
    public static void Run()
    {
        Directory.CreateDirectory(OutDir);
        Log("======== SHOOT START ========");
        Log("unity=" + Application.unityVersion + " batchmode=" + Application.isBatchMode);
        Log("gfxDevice=" + SystemInfo.graphicsDeviceType + " | " + SystemInfo.graphicsDeviceName);
        Log("gfxShaderLevel=" + SystemInfo.graphicsShaderLevel + " maxTextureSize=" + SystemInfo.maxTextureSize);
        Log("renderPipeline=" + (GraphicsSettings.currentRenderPipeline != null
            ? GraphicsSettings.currentRenderPipeline.GetType().FullName : "NULL/BUILTIN"));
        Log("scene=" + SceneManager.GetActiveScene().path);

        // ============ 1. STATIC analysis of the boot scene (edit mode) ============
        var core = EditorSceneManager.OpenScene(CoreScene, OpenSceneMode.Single);
        Log("---- opened " + CoreScene + " loaded=" + core.IsValid() + " roots=" + core.rootCount);
        AnalyzeCore();

        // ============ 2. chunk shots (edit mode) ============
        ShootChunk("Chunk_0_5_Sea");
        ShootChunk("Chunk_5_5_City");
        ShootChunk("Chunk_3_6_Forest");

        // ============ 3. back to Core, then real PLAY MODE ============
        core = EditorSceneManager.OpenScene(CoreScene, OpenSceneMode.Single);
        Log("---- reopened " + CoreScene + " for play mode, isDirty=" + core.isDirty);

        phase = P_PLAYWAIT;
        t0 = EditorApplication.timeSinceStartup;
        SessionState.SetInt(PhaseKey, phase);
        SessionState.SetString(T0Key, t0.ToString(CultureInfo.InvariantCulture));
        Register();
        Log("EnterPlaymode() requested at t=" + t0.ToString("F3"));
    }

    [InitializeOnLoadMethod]
    static void Hook()
    {
        int p = SessionState.GetInt(PhaseKey, P_IDLE);
        if (p == P_IDLE) return;
        phase = p;
        t0 = double.Parse(SessionState.GetString(T0Key, "0"), CultureInfo.InvariantCulture);
        Log("---- domain reload: resumed phase=" + phase + " t0=" + t0.ToString("F3"));
        Register();
    }

    static void Register()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        try
        {
            if (phase == P_PLAYWAIT)
            {
                if (!EditorApplication.isPlaying) return;
                playFrame++;
                SamplePlay(now);
                if (now - t0 < 5.0) return;   // wait 5 seconds of play time

                var cam = Camera.main;
                Log("---- PLAY frame=" + playFrame + " elapsed=" + (now - t0).ToString("F2") + "s");
                LogPlayObjects();
                if (cam != null)
                {
                    Log("camera.main name=" + cam.name + " pos=" + V(cam.transform.position) +
                        " rotEuler=" + V(cam.transform.eulerAngles) + " fov=" + cam.fieldOfView +
                        " active=" + cam.gameObject.activeInHierarchy + " enabled=" + cam.enabled +
                        " clearFlags=" + cam.clearFlags + " bg=" + cam.backgroundColor);
                    Log("camera nearClip=" + cam.nearClipPlane + " farClip=" + cam.farClipPlane +
                        " cullingMask=" + cam.cullingMask + " depth=" + cam.depth);
                    Capture(cam, "core_play.png", "PLAY-MODE Camera.main");
                }
                else { Log("FAILED core_play.png : Camera.main == null in play mode"); }

                phase = P_EDITWAIT;
                t0 = now;
                SessionState.SetInt(PhaseKey, phase);
                SessionState.SetString(T0Key, t0.ToString(CultureInfo.InvariantCulture));
                Log("ExitPlaymode() requested");
                EditorApplication.ExitPlaymode();
                return;
            }

            if (phase == P_EDITWAIT)
            {
                if (EditorApplication.isPlaying) return;
                if (now - t0 < 1.0) return;    // wait 1 second after leaving play

                var sc = SceneManager.GetActiveScene();
                Log("---- back in EDIT mode, scene=" + sc.path + " dirty=" + sc.isDirty);
                AnalyzeCore();
                CaptureSceneView("core_scene.png");
                CaptureEditorOverview("core_scene.png");

                Log("======== SHOOT DONE ========");
                phase = P_DONE;
                SessionState.SetInt(PhaseKey, P_DONE);
                EditorApplication.update -= Tick;
                EditorApplication.Exit(0);
            }
        }
        catch (Exception e)
        {
            Log("EXCEPTION in Tick: " + e.GetType().Name + " : " + e.Message + "\n" + e.StackTrace);
            EditorApplication.update -= Tick;
            EditorApplication.Exit(3);
        }
    }

    // =====================================================  STATIC ANALYSIS
    static void AnalyzeCore()
    {
        var scene = SceneManager.GetActiveScene();
        var all = new List<Transform>();
        foreach (var r in scene.GetRootGameObjects()) All(r.transform, all);

        int meshR = 0, rend = 0, rb = 0, cc = 0, coll = 0, anim = 0;
        foreach (var t in all)
        {
            var go = t.gameObject;
            if (go.GetComponent<MeshRenderer>() != null) meshR++;
            if (go.GetComponent<Renderer>() != null) rend++;
            if (go.GetComponent<Rigidbody>() != null) rb++;
            if (go.GetComponent<CharacterController>() != null) cc++;
            if (go.GetComponent<Collider>() != null) coll++;
            if (go.GetComponent<Animator>() != null) anim++;
        }

        Log("CORE COUNTS roots=" + scene.rootCount + " totalGameObjects=" + all.Count +
            " meshRenderers=" + meshR + " renderers=" + rend +
            " rigidbodies=" + rb + " characterControllers=" + cc +
            " colliders=" + coll + " animators=" + anim);

        Log("CORE ROOTS:");
        foreach (var r in scene.GetRootGameObjects())
        {
            int kids = All(r.transform);
            Log("   root '" + r.name + "' active=" + r.activeInHierarchy +
                " pos=" + V(r.transform.position) + " totalTransformsUnderIt=" + kids);
        }

        // --- cameras ---
        Log("CORE CAMERAS:");
        foreach (var t in all)
        {
            var c = t.GetComponent<Camera>();
            if (c == null) continue;
            var fc = t.GetComponent<FollowCam>();
            Log("   cam '" + c.name + "' path=" + ScenePath(scene, t) + " pos=" + V(t.position) +
                " rotEuler=" + V(t.eulerAngles) + " fov=" + c.fieldOfView +
                " isMainCamera=" + c.CompareTag("MainCamera") + " isActive=" + c.enabled +
                " targetTexture=" + (c.targetTexture != null ? c.targetTexture.name : "null"));
            if (fc != null)
                Log("      FollowCam target=" + (fc.target != null ? fc.target.name : "NULL") +
                    " offset=" + V(fc.offset) + " speed=" + fc.speed);
            else
                Log("      FollowCam: NONE on this camera");
        }

        // --- anything that looks like a car ---
        Log("CORE VEHICLE-LIKE OBJECTS (name contains Kart/Car/Vehicle):");
        int found = 0;
        foreach (var t in all)
        {
            string n = t.name;
            if (!(n.IndexOf("Kart", StringComparison.OrdinalIgnoreCase) >= 0 ||
                  n.IndexOf("Car", StringComparison.OrdinalIgnoreCase) >= 0 ||
                  n.IndexOf("Vehicle", StringComparison.OrdinalIgnoreCase) >= 0)) continue;
            found++;
            Log("   '" + n + "' path=" + ScenePath(scene, t) + " pos=" + V(t.position) +
                " rotEuler=" + V(t.eulerAngles) + " scale=" + V(t.lossyScale) +
                " active=" + t.gameObject.activeInHierarchy + " tag=" + t.tag);
            Log("      components: " + CompList(t.gameObject));
            int kids = All(t);
            int kidMesh = 0; var kidList = new List<Transform>(); All(t, kidList);
            foreach (var k in kidList) if (k.GetComponent<MeshRenderer>() != null) kidMesh++;
            Log("      children(Transforms)=" + kids + " meshRenderersInSubtree=" + kidMesh +
                " hasMeshRendererOnRoot=" + (t.GetComponent<MeshRenderer>() != null));
            var rend2 = t.GetComponent<Renderer>();
            if (rend2 != null)
                Log("      root bounds=" + BoundsStr(rend2.bounds) + " mat=" + MatName(rend2));
        }
        if (found == 0) Log("   (none found)");

        // --- water / ground / characters ---
        Log("CORE NOTABLE OBJECTS (Water/Ground/Sea/Character/Player/NPC/Boat):");
        foreach (var t in all)
        {
            string n = t.name;
            bool hit = false;
            foreach (var k in new[] { "Water", "Ground", "Sea", "Character", "Player", "NPC", "Boat", "CharacterController" })
                if (n.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) { hit = true; break; }
            if (!hit) continue;
            var r = t.GetComponent<Renderer>();
            Log("   '" + n + "' path=" + ScenePath(scene, t) + " pos=" + V(t.position) +
                " renderer=" + (r != null) + (r != null ? " bounds=" + BoundsStr(r.bounds) +
                " mat=" + MatName(r) + " enabled=" + r.enabled : ""));
        }

        // --- lights ---
        Log("CORE LIGHTS:");
        foreach (var t in all)
        {
            var l = t.GetComponent<Light>();
            if (l == null) continue;
            Log("   '" + l.name + "' type=" + l.type + " intensity=" + l.intensity +
                " color=" + l.color + " pos=" + V(t.position) + " enabled=" + l.enabled +
                " cullingMask=" + l.cullingMask);
        }

        // --- post processing volume ---
        Log("CORE VOLUMES:");
        foreach (var t in all)
        {
            var v = t.GetComponent<UnityEngine.Rendering.Volume>();
            if (v == null) continue;
            Log("   '" + v.name + "' pos=" + V(t.position) + " global=" + v.isGlobal +
                " enabled=" + v.enabled + " profile=" + (v.profile != null ? v.profile.name : "NULL") +
                " weight=" + v.weight);
        }

        // --- what the camera actually sees ---
        var cam = Camera.main;
        if (cam != null)
        {
            Log("CORE FRUSTUM from Main Camera " + V(cam.transform.position) + " rotEuler=" + V(cam.transform.eulerAngles));
            int visible = 0; var visNames = new List<string>();
            var planes = GeometryUtility.CalculateFrustumPlanes(cam);
            foreach (var t in all)
            {
                var r = t.GetComponent<Renderer>();
                if (r == null || !r.enabled) continue;
                if (GeometryUtility.TestPlanesAABB(planes, r.bounds))
                { visible++; if (visNames.Count < 40) visNames.Add(t.name + "@" + V(r.bounds.center) + " " + BoundsStr(r.bounds)); }
            }
            Log("CORE visibleRenderersInFrustum=" + visible);
            foreach (var s in visNames) Log("   vis: " + s);
        }
        else Log("CORE: Camera.main == null");
    }

    static string CompList(GameObject go)
    {
        var sb = new StringBuilder();
        foreach (var c in go.GetComponents<Component>())
        {
            if (c == null) { sb.Append("<missing-script> "); continue; }
            sb.Append(c.GetType().FullName).Append(" ");
        }
        return sb.ToString().Trim();
    }

    static string MatName(Renderer r)
    {
        var m = r.GetComponent<Material>() ?? r.sharedMaterial;
        return m != null ? m.name : "null";
    }

    static string BoundsStr(Bounds b)
    {
        return string.Format(CultureInfo.InvariantCulture,
            "c=({0:F2},{1:F2},{2:F2}) s=({3:F2},{4:F2},{5:F2})",
            b.center.x, b.center.y, b.center.z, b.size.x, b.size.y, b.size.z);
    }

    static string ScenePath(Scene sc, Transform t)
    {
        var sb = new StringBuilder();
        var cur = t;
        while (cur != null) { sb.Insert(0, cur.name + "/"); cur = cur.parent; }
        return sb.ToString();
    }

    static void All(Transform root, List<Transform> list)
    {
        list.Add(root);
        for (int i = 0; i < root.childCount; i++) All(root.GetChild(i), list);
    }

    static int All(Transform root) { int n = 1; for (int i = 0; i < root.childCount; i++) n += All(root.GetChild(i)); return n; }

    // =====================================================  PLAY-MODE SAMPLING
    static double lastSample = -99;
    static Vector3 camAtPlayStart;
    static Vector3 kartAtPlayStart;
    static bool haveStart = false;

    static void SamplePlay(double now)
    {
        if (now - lastSample < 0.5) return;
        lastSample = now;

        var cam = Camera.main;
        var kart = GameObject.Find("Kart");
        Vector3 cp = cam != null ? cam.transform.position : Vector3.zero;
        Vector3 kp = kart != null ? kart.transform.position : Vector3.zero;

        if (!haveStart && cam != null)
        {
            camAtPlayStart = cp; kartAtPlayStart = kp; haveStart = true;
            Log("PLAY t=0.00 camPos=" + V(cp) + " kartPos=" + V(kp) +
                " kartExists=" + (kart != null) + " camFollowsTarget=" +
                (cam != null && cam.GetComponent<FollowCam>() != null));
        }

        string kb = "n/a";
        try { var k = UnityEngine.InputSystem.Keyboard.current; kb = k != null ? "present" : "NULL(no device)"; }
        catch (Exception e) { kb = "EX:" + e.GetType().Name; }

        Log("PLAY t=" + (now - t0).ToString("F2") +
            " camPos=" + V(cp) +
            " camMovedFromStart=" + (cam != null ? (cp - camAtPlayStart).magnitude.ToString("F4") : "-") +
            " kartPos=" + V(kp) +
            " kartMovedFromStart=" + (kart != null ? (kp - kartAtPlayStart).magnitude.ToString("F4") : "-") +
            " kart=null?" + (kart == null) +
            " keyboard=" + kb +
            " timeScale=" + Time.timeScale.ToString("F2") +
            " frameCount=" + Time.frameCount);
    }

    static void LogPlayObjects()
    {
        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        var all = new List<Transform>();
        foreach (var r in roots) All(r.transform, all);

        int meshR = 0, rb = 0, loaders = 0;
        foreach (var t in all)
        {
            if (t.GetComponent<MeshRenderer>() != null) meshR++;
            if (t.GetComponent<Rigidbody>() != null) rb++;
            var s = t.GetComponent<UnityEngine.Rendering.Volume>() as UnityEngine.Rendering.Volume;
            if (s != null) loaders++;
        }
        Log("PLAY COUNTS roots=" + roots.Length + " totalGameObjects=" + all.Count +
            " meshRenderers=" + meshR + " rigidbodies=" + rb);
        Log("PLAY activeScene=" + SceneManager.GetActiveScene().path +
            " loadedScenes=" + SceneManager.sceneCount);

        foreach (var t in all)
        {
            string n = t.name;
            bool hit = n.IndexOf("Kart", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       n.IndexOf("Car", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       n.IndexOf("Vehicle", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!hit) continue;
            var r = t.GetComponent<Rigidbody>();
            Log("PLAY vehicle '" + n + "' pos=" + V(t.position) + " rotEuler=" + V(t.eulerAngles) +
                " active=" + t.gameObject.activeInHierarchy +
                " rigidbody=" + (r != null) +
                (r != null ? " vel=" + V(r.linearVelocity) + " angularVel=" + V(r.angularVelocity) : "") +
                " components=" + CompList(t.gameObject));
        }
    }

    // ==============================================================  CAPTURE
    static bool RenderInto(Camera cam, RenderTexture rt)
    {
        try
        {
            var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, req))
            {
                RenderPipeline.SubmitRenderRequest(cam, req);
                Log("   render: SubmitRenderRequest OK");
                return true;
            }
            Log("   render: SupportsRenderRequest == false");
        }
        catch (Exception e) { Log("   render: request threw " + e.GetType().Name + ": " + e.Message); }

        try { cam.Render(); Log("   render: legacy Camera.Render() used"); return true; }
        catch (Exception e) { Log("   render: Camera.Render threw " + e.GetType().Name + ": " + e.Message); return false; }
    }

    static void Capture(Camera cam, string file, string what)
    {
        var path = OutDir + "/" + file;
        RenderTexture rt = null; Texture2D tex = null;
        RenderTexture prevActive = RenderTexture.active;
        RenderTexture prevTarget = cam.targetTexture;
        Color prevBg = cam.backgroundColor;
        CameraClearFlags prevFlags = cam.clearFlags;

        try
        {
            rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 1;
            rt.Create();

            if (!RenderInto(cam, rt))
            {
                Log("FAILED " + file + " : camera produced no render");
                return;
            }

            RenderTexture.active = rt;
            tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();

            var png = tex.EncodeToPNG();
            File.WriteAllBytes(path, png);
            Log("SHOT OK " + file + " bytes=" + png.Length + "  [" + what + "]");
            Log("   pixelStats " + PixelStats(tex) + " path=" + Path.GetFullPath(path));
        }
        catch (Exception e)
        {
            Log("FAILED " + file + " : " + e.GetType().Name + " : " + e.Message);
        }
        finally
        {
            cam.targetTexture = prevTarget;
            cam.clearFlags = prevFlags;
            cam.backgroundColor = prevBg;
            RenderTexture.active = prevActive;
            if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
            if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
        }
    }

    static string PixelStats(Texture2D t)
    {
        var px = t.GetPixels();
        double sum = 0, sum2 = 0; int n = 0;
        float minL = 999f, maxL = -999f;
        int dark = 0, bright = 0;
        var hist = new HashSet<int>();
        for (int i = 0; i < px.Length; i += 7)
        {
            var c = px[i];
            float l = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            sum += l; sum2 += l * l; n++;
            if (l < minL) minL = l;
            if (l > maxL) maxL = l;
            if (l < 0.02f) dark++;
            if (l > 0.5f) bright++;
            if (hist.Count < 500) hist.Add(((int)(c.r * 15) << 8) | ((int)(c.g * 15) << 4) | (int)(c.b * 15));
        }
        double mean = sum / n;
        double sd = Math.Sqrt(Math.Max(0, sum2 / n - mean * mean));
        return string.Format(CultureInfo.InvariantCulture,
            "W={0} H={1} sampled={2} meanLum={3:F4} stdLum={4:F4} minLum={5:F3} maxLum={6:F3} nearBlackPx={7} brightPx={8} distinctColors={9} isUniform={10}",
            t.width, t.height, n, mean, sd, minL, maxL, dark, bright, hist.Count, sd < 0.005);
    }

    // ---- scene view capture (edit mode) ----
    static void CaptureSceneView(string file)
    {
        var sv = SceneView.lastActiveSceneView;
        if (sv == null)
        {
            Log("SCENE_VIEW_NULL: SceneView.lastActiveSceneView == null in batchmode -> " +
                "no real Scene view window exists; falling back to an editor camera render.");
            return;
        }
        var cam = sv.camera;
        Log("SceneView found: pivot=" + V(sv.pivot) + " rot=" + V(sv.rotation.eulerAngles) +
            " size=" + sv.size.ToString("F2") + " ortho=" + sv.orthographic);
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 100000f;
        Capture(cam, file, "SCENE VIEW");
    }

    // ---- guaranteed fallback: an editor camera looking at the whole boot scene ----
    static void CaptureEditorOverview(string file)
    {
        var scene = SceneManager.GetActiveScene();
        var all = new List<Transform>();
        foreach (var r in scene.GetRootGameObjects()) All(r.transform, all);

        bool has = File.Exists(OutDir + "/" + file) && new FileInfo(OutDir + "/" + file).Length > 2000;
        if (has) { Log("Skipping editor overview, real Scene view png already written."); return; }

        Bounds b = new Bounds(Vector3.zero, Vector3.zero);
        bool first = true;
        foreach (var t in all)
        {
            var r = t.GetComponent<Renderer>();
            if (r == null || !r.enabled) continue;
            if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
        }
        Log("EDITOR OVERVIEW bounds=" + BoundsStr(b));

        var go = new GameObject("ShotsA_TempCam") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>();
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 200000f;
        cam.fieldOfView = 60f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.19f, 0.30f, 0.47f);

        float rad = Mathf.Max(b.extents.magnitude, 1f);
        Vector3 p = b.center + new Vector3(0f, b.extents.y * 0.6f + rad * 0.25f, -rad * 1.6f);
        go.transform.position = p;
        go.transform.rotation = Quaternion.LookRotation((b.center - p).normalized, Vector3.up);
        Log("EDITOR OVERVIEW camPos=" + V(p) + " looking at " + V(b.center));

        Capture(cam, file, "EDITOR OVERVIEW CAMERA (SceneView was null in batchmode)");

        UnityEngine.Object.DestroyImmediate(go);
    }

    // ============================================================  CHUNK SHOTS
    static void ShootChunk(string name)
    {
        string path = "Assets/Scenes/World/" + name + ".unity";
        Log("======== CHUNK " + name + " ========");
        if (!File.Exists(path)) { Log("FAILED chunk " + name + " : scene file missing"); return; }

        Scene sc;
        try { sc = EditorSceneManager.OpenScene(path, OpenSceneMode.Single); }
        catch (Exception e) { Log("FAILED open " + name + " : " + e.GetType().Name + " : " + e.Message); return; }

        var all = new List<Transform>();
        foreach (var r in sc.GetRootGameObjects()) All(r.transform, all);

        Bounds b = new Bounds(Vector3.zero, Vector3.zero); bool first = true;
        int meshR = 0, lights = 0, anim = 0;
        foreach (var t in all)
        {
            var rr = t.GetComponent<MeshRenderer>();
            if (rr != null)
            {
                meshR++;
                if (first) { b = rr.bounds; first = false; } else b.Encapsulate(rr.bounds);
            }
            if (t.GetComponent<Light>() != null) lights++;
            if (t.GetComponent<Animator>() != null) anim++;
        }
        Log("CHUNK " + name + " roots=" + sc.rootCount + " gameObjects=" + all.Count +
            " meshRenderers=" + meshR + " lights=" + lights + " animators=" + anim +
            " contentBounds=" + BoundsStr(b));

        // pick a camera: existing one first, otherwise a temp one framing the bounds
        Camera cam = null;
        foreach (var t in all) { cam = t.GetComponent<Camera>(); if (cam != null) break; }
        GameObject tmp = null;

        if (cam != null)
        {
            Log("CHUNK " + name + " using existing camera '" + cam.name + "' pos=" + V(cam.transform.position) +
                " rotEuler=" + V(cam.transform.eulerAngles) + " fov=" + cam.fieldOfView);
        }
        else
        {
            tmp = new GameObject("ShotsA_TempCam") { hideFlags = HideFlags.HideAndDontSave };
            cam = tmp.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.19f, 0.30f, 0.47f);
            float rad = Mathf.Max(b.extents.magnitude, 1f);
            Vector3 p = b.center + new Vector3(0f, b.extents.y * 0.5f + rad * 0.3f, -rad * 1.5f);
            tmp.transform.position = p;
            tmp.transform.rotation = Quaternion.LookRotation((b.center - p).normalized, Vector3.up);
            Log("CHUNK " + name + " NO camera in scene -> temp camera pos=" + V(p) + " fov=60");
        }
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 200000f;

        int vis = 0;
        var planes = GeometryUtility.CalculateFrustumPlanes(cam);
        foreach (var t in all)
        {
            var rr = t.GetComponent<Renderer>();
            if (rr != null && rr.enabled && GeometryUtility.TestPlanesAABB(planes, rr.bounds)) vis++;
        }
        Log("CHUNK " + name + " visibleRenderersInFrustum=" + vis);

        Capture(cam, "chunk_" + name + ".png", "CHUNK " + name);

        if (tmp != null) UnityEngine.Object.DestroyImmediate(tmp);
        Resources.UnloadUnusedAssets();
        GC.Collect();
    }
}
