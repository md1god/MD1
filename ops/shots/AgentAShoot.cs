// AgentAShoot.cs - agent A, batch-mode play-mode screenshot + diagnostics
// harness for CC_GAME_1.
//
// WHY NOT ops/shots/Shoot.cs with "class Shoot":
//   1. Assets/Mech/Scripts/Shoot.cs:5 already declares
//      "public class Shoot : MonoBehaviour" in the global namespace.
//      A second "class Shoot" is error CS0101 and kills the whole build.
//   2. A second concurrent Agent A process is also writing Shoot.cs into
//      ops/shots/ and mirroring it to Assets/Editor/_ShotsA_Temp/.
//      Using a distinct file AND class name keeps both runs compilable.
//
// HOW TO RUN (note: -quit is deliberately omitted; AgentAShoot.Run calls
// EditorApplication.Exit(0) itself, because -quit would kill the editor the
// moment Run() returns and the play-mode phases would never run):
//
//   Unity.exe -batchmode -projectPath D:\CC_GAME_1 \
//            -executeMethod AgentAShoot.Run \
//            -logFile D:\CC_GAME_1\ops\shots\shoot.log
//
// Phase machine, carried across domain reloads in SessionState:
//   0 editor   open Core.unity, dump diagnostics, EnterPlaymode
//   1 playing  snapshot camera + kart at first frame
//   2 playing  after 5s, log delta, capture core_play.png, ExitPlaymode
//   3 editor   wait 1s, capture core_scene.png
//   4 editor   capture the 3 world chunks, write diag, Exit(0)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AgentAShoot
{
    const int ShotW = 1600;
    const int ShotH = 900;
    const string OutDir = @"D:\CC_GAME_1\ops\shots";
    const string DiagPath = @"D:\CC_GAME_1\ops\shots\diagnostics.txt";
    const string CoreScene = "Assets/Scenes/Core/Core.unity";

    // Real scene names, confirmed on disk before this script was written.
    static readonly string[] ChunkNames =
    {
        "Chunk_0_5_Sea",
        "Chunk_5_5_City",
        "Chunk_3_6_Forest"
    };

    const string KPhase = "AgentA.Shoot.Phase";
    const string KFirstSeen = "AgentA.Shoot.FirstSeen";

    // ---------------------------------------------------------------- logging

    static void D(string s)
    {
        Debug.Log("[SHOOT] " + s);
        try
        {
            File.AppendAllText(DiagPath, s + Environment.NewLine);
        }
        catch { /* logging must never break the run */ }
    }

    static void H(string title)
    {
        D("");
        D("================ " + title + " ================");
    }

    static string V(Vector3 v)
    {
        return string.Format(CultureInfo.InvariantCulture,
            "({0:0.###}, {1:0.###}, {2:0.###})", v.x, v.y, v.z);
    }

    // ------------------------------------------------------------ entry point

    [MenuItem("Tools/AgentA/AgentAShoot Run")]
    public static void Run()
    {
        Directory.CreateDirectory(OutDir);
        if (File.Exists(DiagPath)) File.Delete(DiagPath);

        SessionState.SetInt(KPhase, 0);
        SessionState.SetFloat(KFirstSeen, 0f);

        D("AgentAShoot.Run() entered");
        D("Unity version: " + Application.unityVersion);
        D("Batch mode: " + Application.isBatchMode);
        D("Project: " + Application.dataPath);
        D("Output dir: " + OutDir);

        Hook();
        Phase0();
    }

    // Re-arms the update pump after every domain reload.
    [InitializeOnLoadMethod]
    static void Hook()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    static double _lastTick = 0;

    static void Tick()
    {
        // guard against re-entrancy while we are mid-phase
        double now = EditorApplication.timeSinceStartup;
        if (now - _lastTick < 0.05) return;
        _lastTick = now;

        try
        {
            switch (SessionState.GetInt(KPhase, -1))
            {
                case 1: Phase1(); break;
                case 2: Phase2(); break;
                case 3: Phase3(); break;
                case 4: Phase4(); break;
            }
        }
        catch (Exception e)
        {
            D("FATAL in phase " + SessionState.GetInt(KPhase, -1) + ": " + e);
            Finish(1);
        }
    }

    // ============================================================ phase 0/1/2
    // Core.unity, entered play mode, real game camera, 5 second hold.

    static void Phase0()
    {
        H("PHASE 0 - editor: open " + CoreScene);

        if (!File.Exists(CoreScene))
        {
            D("FATAL: Core scene missing on disk");
            Finish(1);
            return;
        }

        var scene = EditorSceneManager.OpenScene(CoreScene, OpenSceneMode.Single);
        D("Opened scene '" + scene.name + "' path=" + scene.path);
        D("scene.isLoaded=" + scene.isLoaded + "  rootObjects=" + scene.rootCount);

        DumpHierarchy(scene);
        DumpCameras(scene);
        DumpVehicles(scene, "EDITOR (scene as authored, not playing)");

        SessionState.SetInt(KPhase, 1);
        D("--> EnterPlaymode()");
        EditorApplication.EnterPlaymode();
    }

    static void Phase1()
    {
        // wait until play mode is actually running
        if (!EditorApplication.isPlaying && !Application.isPlaying) return;

        if (SessionState.GetFloat(KFirstSeen, 0f) == 0f)
        {
            SessionState.SetFloat(KFirstSeen, (float)EditorApplication.timeSinceStartup);
            H("PHASE 1 - play mode entered");
            D("isPlaying=" + Application.isPlaying + "  frameCount=" + Time.frameCount);
            D("timeSinceStartup=" + EditorApplication.timeSinceStartup.ToString("0.000"));

            SessionState.SetInt(KPhase, 2);
            return;
        }
    }

    static void Phase2()
    {
        double started = SessionState.GetFloat(KFirstSeen, (float)EditorApplication.timeSinceStartup);
        double elapsed = EditorApplication.timeSinceStartup - started;
        if (elapsed < 5.0) return;

        H("PHASE 2 - play mode, after 5.0s hold");
        D("elapsedSeconds=" + elapsed.ToString("0.000"));
        D("isPlaying=" + Application.isPlaying + "  frameCount=" + Time.frameCount);

        var playScene = SceneManager.GetActiveScene();
        D("activeScene='" + playScene.name + "' isLoaded=" + playScene.isLoaded);

        DumpVehicles(playScene, "PLAY (t=+5s)");
        DumpCameras(playScene);
        DumpLights(playScene);

        // Does the camera follow the kart?
        var cam = Camera.main;
        if (cam != null)
        {
            D("FOLLOW CHECK: camera pos = " + V(cam.transform.position) +
              " euler=" + V(cam.transform.eulerAngles));
            var kart = FindVehicle(playScene);
            if (kart != null)
            {
                var t = kart.transform;
                D("FOLLOW CHECK: kart pos    = " + V(t.position) +
                  " euler=" + V(t.eulerAngles));
                D("FOLLOW CHECK: camera - kart = " + V(cam.transform.position - t.position));
                D("FOLLOW CHECK: distance cam->kart = " +
                  Vector3.Distance(cam.transform.position, t.position).ToString("0.000"));
            }
            else D("FOLLOW CHECK: no Kart/Car/Vehicle found -> nothing to follow");
        }

        // THE shot the owner asked for: the untouched game camera, as the
        // player actually sees it, with no repositioning by this script.
        var main = Camera.main;
        if (main == null)
        {
            D("FATAL: Camera.main is null in play mode -> no game view to capture");
        }
        else
        {
            string p = OutDir + "/core_play.png";
            if (Capture(main, p, true)) D("WROTE " + p);
            else D("FAILED to write " + p);
            D(AnalysePng(p));
        }

        SessionState.SetInt(KPhase, 3);
        D("--> ExitPlaymode()");
        EditorApplication.ExitPlaymode();
    }

    // ============================================================== phase 3/4
    // Back in the editor: overview of Core, then the three world chunks.

    static void Phase3()
    {
        if (EditorApplication.isPlaying || Application.isPlaying) return;
        if (SessionState.GetInt(KPhase, -1) != 3) return;

        H("PHASE 3 - editor again, after exiting play mode");
        D("isPlaying=" + Application.isPlaying + "  isCompiling=" + EditorApplication.isCompiling);

        // batchmode has no SceneView window, so this is an overview render
        // taken from the scene's own camera, not a literal SceneView grab.
        var scene = SceneManager.GetActiveScene();
        if (scene.path != CoreScene)
        {
            scene = EditorSceneManager.OpenScene(CoreScene, OpenSceneMode.Single);
            D("Reopened " + scene.path);
        }

        var cam = FindCamera(scene);
        if (cam == null)
        {
            D("FAILED: no camera in Core.unity");
        }
        else
        {
            FrameWholeScene(cam, scene);
            D("Overview camera at " + V(cam.transform.position) +
              " fov=" + cam.fieldOfView.ToString("0.0") +
              " far=" + cam.farClipPlane.ToString("0"));
            string p = OutDir + "/core_scene.png";
            if (Capture(cam, p, false)) D("WROTE " + p);
            else D("FAILED to write " + p);
            D(AnalysePng(p));
        }

        SessionState.SetInt(KPhase, 4);
    }

    static void Phase4()
    {
        SessionState.SetInt(KPhase, -1); // one shot only
        H("PHASE 4 - the three world chunks");

        for (int i = 0; i < ChunkNames.Length; i++)
        {
            string name = ChunkNames[i];
            string path = "Assets/Scenes/World/" + name + ".unity";

            D("");
            D("---- chunk " + name + " ----");
            D("path on disk exists = " + File.Exists(path));

            if (!File.Exists(path)) { D("SKIP: not on disk"); continue; }

            try
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                D("opened '" + scene.name + "' roots=" + scene.rootCount);

                int objs = CountObjects(scene);
                int rend = 0;
                Bounds b;
                CountAndBound(scene, out rend, out b);
                D("gameObjects=" + objs + "  renderers=" + rend);
                D("renderer bounds centre=" + V(b.center) + " size=" + V(b.size));

                var cam = FindCamera(scene);
                if (cam == null)
                {
                    D("SKIP: no camera in " + name);
                    continue;
                }

                FrameWholeScene(cam, scene);
                D("camera at " + V(cam.transform.position) + " fov=" + cam.fieldOfView.ToString("0.0") +
                  " far=" + cam.farClipPlane.ToString("0"));

                string outp = OutDir + "/chunk_" + name + ".png";
                if (Capture(cam, outp, false)) D("WROTE " + outp);
                else D("FAILED to write " + outp);

                // pixel truth, so nobody has to guess
                D(AnalysePng(outp));
            }
            catch (Exception e)
            {
                D("EXCEPTION on " + name + ": " + e.Message);
            }
        }

        H("DONE");
        Finish(0);
    }

    static void Finish(int code)
    {
        D("Exiting Unity with code " + code);
        EditorApplication.Exit(code);
    }

    // ============================================================== diagnostics

    static int CountObjects(Scene s)
    {
        int n = 0;
        foreach (var r in s.GetRootGameObjects())
        {
            var all = r.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++) if (all[i].gameObject.scene == s) n++;
        }
        return n;
    }

    static void CountAndBound(Scene s, out int rendererCount, out Bounds bounds)
    {
        rendererCount = 0;
        bool any = false;
        bounds = new Bounds(Vector3.zero, Vector3.zero);
        foreach (var r in s.GetRootGameObjects())
        {
            var rs = r.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++)
            {
                if (rs[i].gameObject.scene != s) continue;
                rendererCount++;
                if (!any) { bounds = rs[i].bounds; any = true; }
                else bounds.Encapsulate(rs[i].bounds);
            }
        }
        return;
    }

    static void DumpHierarchy(Scene s)
    {
        H("HIERARCHY");
        int n = CountObjects(s);
        D("total GameObjects in scene = " + n);

        var names = new List<string>();
        foreach (var r in s.GetRootGameObjects())
        {
            var all = r.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].gameObject.scene != s) continue;
                names.Add(all[i].gameObject.name);
            }
        }
        var counts = new Dictionary<string, int>();
        for (int i = 0; i < names.Count; i++)
        {
            if (!counts.ContainsKey(names[i])) counts[names[i]] = 0;
            counts[names[i]]++;
        }
        var keys = new List<string>(counts.Keys);
        keys.Sort();
        D("distinct names = " + keys.Count);
        for (int i = 0; i < keys.Count && i < 400; i++)
            D("   " + keys[i] + " x" + counts[keys[i]]);
    }

    static void DumpCameras(Scene s)
    {
        H("CAMERAS");
        var roots = s.GetRootGameObjects();
        int total = 0;
        for (int i = 0; i < roots.Length; i++)
        {
            var cs = roots[i].GetComponentsInChildren<Camera>(true);
            for (int j = 0; j < cs.Length; j++)
            {
                if (cs[j].gameObject.scene != s) continue;
                total++;
                var c = cs[j];
                D("CAMERA '" + c.gameObject.name + "' path='" + Path(c.transform) + "'");
                D("   tag=" + c.tag + "  enabled=" + c.enabled +
                  "  gameObject.activeInHierarchy=" + c.gameObject.activeInHierarchy +
                  "  isActiveAndEnabled=" + c.isActiveAndEnabled);
                D("   pos=" + V(c.transform.position));
                D("   euler=" + V(c.transform.eulerAngles) + "  fov=" + c.fieldOfView.ToString("0.0"));
                D("   near=" + c.nearClipPlane.ToString("0.###") +
                  "  far=" + c.farClipPlane.ToString("0.###") +
                  "  clearFlags=" + c.clearFlags + "  bg=" + c.backgroundColor);
                D("   isMainCamera(Camera.main)==this? " + (Camera.main == c));

                var comps = c.GetComponents<Component>();
                for (int k = 0; k < comps.Length; k++)
                {
                    if (comps[k] == null) { D("   component: <MISSING SCRIPT>"); continue; }
                    D("   component: " + comps[k].GetType().FullName);
                    if (comps[k] is FollowCam fc)
                    {
                        D("      >>> FollowCam.target = " +
                          (fc.target == null ? "NULL  <-- camera will NEVER move" : fc.target.name));
                        D("      >>> FollowCam.offset = " + V(fc.offset) + " speed=" + fc.speed.ToString("0.##"));
                    }
                }
            }
        }
        D("total cameras in scene = " + total);
    }

    static void DumpLights(Scene s)
    {
        H("LIGHTS");
        var roots = s.GetRootGameObjects();
        int n = 0;
        for (int i = 0; i < roots.Length; i++)
        {
            var ls = roots[i].GetComponentsInChildren<Light>(true);
            for (int j = 0; j < ls.Length; j++)
            {
                if (ls[j].gameObject.scene != s) continue;
                n++;
                if (n <= 30)
                    D("LIGHT '" + ls[j].gameObject.name + "' type=" + ls[j].type +
                      " intensity=" + ls[j].intensity.ToString("0.###") +
                      " enabled=" + ls[j].enabled);
            }
        }
        D("total lights = " + n);
    }

    static GameObject FindVehicle(Scene s)
    {
        var roots = s.GetRootGameObjects();
        GameObject best = null;
        for (int i = 0; i < roots.Length; i++)
        {
            var ts = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < ts.Length; j++)
            {
                if (ts[j].gameObject.scene != s) continue;
                string n = ts[j].gameObject.name.ToLowerInvariant();
                if (n.Contains("kart") || n.Contains("car") || n.Contains("vehicle"))
                {
                    if (best == null) best = ts[j].gameObject;
                }
            }
        }
        return best;
    }

    static void DumpVehicles(Scene s, string when)
    {
        H("VEHICLES (" + when + ")");
        int found = 0;
        var roots = s.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var ts = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < ts.Length; j++)
            {
                var go = ts[j].gameObject;
                if (go.scene != s) continue;
                string n = go.name.ToLowerInvariant();
                if (!(n.Contains("kart") || n.Contains("car") || n.Contains("vehicle"))) continue;
                found++;

                D("VEHICLE '" + go.name + "' path='" + Path(go.transform) + "'");
                D("   activeInHierarchy=" + go.activeInHierarchy +
                  "  worldPos=" + V(go.transform.position) +
                  "  euler=" + V(go.transform.eulerAngles));

                var comps = go.GetComponents<Component>();
                for (int k = 0; k < comps.Length; k++)
                    D("   component: " + (comps[k] == null ? "<MISSING SCRIPT>" : comps[k].GetType().FullName));

                var mrs = go.GetComponentsInChildren<MeshRenderer>(true);
                var srs = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var fs = go.GetComponentsInChildren<MeshFilter>(true);

                D("   MeshRenderer count (self+children) = " + mrs.Length);
                for (int k = 0; k < mrs.Length; k++)
                {
                    var mr = mrs[k];
                    D("      MR '" + mr.gameObject.name + "' enabled=" + mr.enabled +
                      " activeInHierarchy=" + mr.gameObject.activeInHierarchy +
                      " shadowCasting=" + mr.shadowCastingMode);
                    var mats = mr.sharedMaterials;
                    D("         materials=" + mats.Length);
                    for (int q = 0; q < mats.Length; q++)
                    {
                        var m = mats[q];
                        D("         mat[" + q + "] '" + (m == null ? "NULL" : m.name) + "'" +
                          (m == null ? "" : " shader=" + m.shader.name +
                                       " shaderSupported=" + m.shader.isSupported +
                                       " hasPropertyMainTex=" + m.HasProperty("_MainTex")));
                    }
                    var mf = mr.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        var m2 = mf.sharedMesh;
                        D("         mesh='" + m2.name + "' verts=" + m2.vertexCount +
                          " tris=" + (m2.triangles.Length / 3) +
                          " bounds=" + m2.bounds.ToString());
                    }
                    else D("         mesh=NULL (nothing to draw)");
                    D("         worldBounds=" + mr.bounds.ToString());
                }
                D("   SkinnedMeshRenderer count = " + srs.Length);
                D("   MeshFilter count = " + fs.Length);
            }
        }
        D("vehicles matching Kart/Car/Vehicle = " + found);
        if (found == 0) D(">>> NONE. There is no Kart/Car/Vehicle object in this scene.");
    }

    static string Path(Transform t)
    {
        var sb = new StringBuilder();
        var stack = new List<string>();
        while (t != null) { stack.Add(t.name); t = t.parent; }
        for (int i = stack.Count - 1; i >= 0; i--) sb.Append(stack[i]).Append('/');
        return sb.ToString();
    }

    // ================================================================= capture

    static Camera FindCamera(Scene s)
    {
        var roots = s.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var c = roots[i].GetComponentInChildren<Camera>(true);
            if (c != null) return c;
        }
        return null;
    }

    /// <summary>
    /// Places the camera so the whole scene fits: above and behind the
    /// renderer bounds, looking at their centre. Avoids the stale hardcoded
    /// vantage points the older WorldShots*.cs scripts still carry.
    /// </summary>
    static void FrameWholeScene(Camera cam, Scene s)
    {
        int rc;
        Bounds b;
        CountAndBound(s, out rc, out b);

        if (rc == 0)
        {
            cam.transform.position = new Vector3(0f, 200f, -200f);
            cam.transform.rotation = Quaternion.Euler(30f, 180f, 0f);
            cam.farClipPlane = 5000f;
            return;
        }

        Vector3 c = b.center;
        Vector3 e = b.size;
        float radius = Mathf.Max(e.magnitude * 0.5f, 10f);

        cam.transform.position = c + new Vector3(0f, radius * 0.85f, -radius * 1.5f);
        cam.transform.rotation = Quaternion.LookRotation((c - cam.transform.position).normalized, Vector3.up);
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = Mathf.Max(radius * 6f, 1000f);
        cam.enabled = true;
    }

    static bool Capture(Camera cam, string path, bool reportVisible)
    {
        RenderTexture rt = null;
        Texture2D tex = null;
        RenderTexture prevActive = RenderTexture.active;
        RenderTexture prevTarget = cam.targetTexture;
        try
        {
            rt = new RenderTexture(ShotW, ShotH, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 1;
            rt.Create();

            cam.targetTexture = rt;
            cam.Render();

            if (reportVisible)
            {
                var planes = GeometryUtility.CalculateFrustumPlanes(cam);
                int visible = 0, total = 0;
                var roots = SceneManager.GetActiveScene().GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    var rs = roots[i].GetComponentsInChildren<Renderer>(true);
                    for (int j = 0; j < rs.Length; j++)
                    {
                        if (!rs[j].enabled || !rs[j].gameObject.activeInHierarchy) continue;
                        total++;
                        if (GeometryUtility.TestPlanesAABB(planes, rs[j].bounds)) visible++;
                    }
                }
                D("IN-FRUSTUM RENDERERS: " + visible + " of " + total + " active renderers");
            }

            RenderTexture.active = rt;
            tex = new Texture2D(ShotW, ShotH, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, ShotW, ShotH), 0, 0);
            tex.Apply();

            byte[] png = tex.EncodeToPNG();
            if (png == null || png.Length < 2000)
            {
                D("capture produced a suspiciously small png (" +
                  (png == null ? "null" : png.Length.ToString()) + " bytes)");
                return false;
            }
            File.WriteAllBytes(path, png);
            D("png bytes = " + png.Length);
            return true;
        }
        catch (Exception e)
        {
            D("capture error: " + e.Message);
            return false;
        }
        finally
        {
            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
            if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
        }
    }

    /// <summary>Pixel statistics, so "is it black?" is answered with numbers.</summary>
    static string AnalysePng(string path)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2);
            tex.LoadImage(bytes);
            var px = tex.GetPixels32();
            long black = 0, dark = 0, bright = 0;
            double sum = 0;
            var hist = new Dictionary<int, int>();
            for (int i = 0; i < px.Length; i++)
            {
                int lum = (px[i].r * 299 + px[i].g * 587 + px[i].b * 114) / 1000;
                sum += lum;
                if (lum < 8) black++;
                if (lum < 40) dark++;
                if (lum > 200) bright++;
                int bucket = (px[i].r / 64) * 16 + (px[i].g / 64) * 4 + (px[i].b / 64);
                if (!hist.ContainsKey(bucket)) hist[bucket] = 0;
                hist[bucket]++;
            }
            int n = px.Length;
            int distinct = hist.Count;
            int top = 0;
            foreach (var kv in hist) if (kv.Value > top) top = kv.Value;
            var sb = new StringBuilder();
            sb.Append("PIXELS " + tex.width + "x" + tex.height + " count=" + n);
            sb.Append(" meanLuma=" + (sum / n).ToString("0.00"));
            sb.Append(" nearBlack=" + (100.0 * black / n).ToString("0.00") + "%");
            sb.Append(" dark=" + (100.0 * dark / n).ToString("0.00") + "%");
            sb.Append(" bright=" + (100.0 * bright / n).ToString("0.00") + "%");
            sb.Append(" distinctColourBuckets=" + distinct);
            sb.Append(" dominantBucketShare=" + (100.0 * top / n).ToString("0.00") + "%");
            UnityEngine.Object.DestroyImmediate(tex);
            return sb.ToString();
        }
        catch (Exception e)
        {
            return "PIXEL ANALYSIS FAILED: " + e.Message;
        }
    }
}
