# split-world-11-scenes.ps1
# =============================================================================
#  World Splitter
#  Splits SampleScene.unity (371 MB, 124,744 prefab instances) into:
#     Core.unity            -> Kart + City + World + Ground + Camera + Light
#     Scene_01..11_*.unity  -> 11 rows of the 11x11 chunk grid (11 chunks each)
#
#  Guarantees
#   - chunk world positions preserved (11x11 grid, step 5000, -25000..+25000)
#   - every world scene gets its own Camera + Directional Light
#     (fixes the "black camera" problem)
#   - a valid SceneRoots block is generated per scene
#   - WorldManifest.txt is rewritten to match WorldManifestParser.cs
#   - constant memory: one StreamReader -> twelve StreamWriters
# =============================================================================
param(
  [string]$Project   = "D:\DiDo111_CC-Game",
  [string]$SourceRel = "Assets\Scenes\SampleScene.unity",
  [string]$OutCore   = "Assets\Scenes\Core\Core.unity",
  [string]$OutWorld  = "Assets\Scenes\World",
  [string]$Template  = "Assets\Scenes\WebGLMini.unity"
)

$ErrorActionPreference = "Stop"
$cs = @'
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

public static class WorldSplitter
{
    const int CORE = 0;
    const int SCENES = 12;

    static Dictionary<int,string> goName  = new Dictionary<int,string>();
    static Dictionary<int,int>    goTrans = new Dictionary<int,int>();
    static Dictionary<int,int>    trGo    = new Dictionary<int,int>();
    static Dictionary<int,int>    trFath  = new Dictionary<int,int>();
    static Dictionary<int,int>    chunkRow = new Dictionary<int,int>();  // chunkTransformId -> row 0..10
    static Dictionary<int,int>    rowGo    = new Dictionary<int,int>();  // goId -> scene index (1..11)
    static Dictionary<int,int>    prefabParent = new Dictionary<int,int>();
    static HashSet<int>           usedIds  = new HashSet<int>();
    static int worldMapGo = 0, worldMapTrans = 0;
    static List<int>              coreRootGo = new List<int>();

    static string tplCamData = "", tplLightData = "";
    static int    tplCamId = 0, tplLightId = 0;

    // ------------------------------------------------------------------ utils
    static int ParseFileId(string line)
    {
        int i = line.IndexOf("fileID:", StringComparison.Ordinal);
        if (i < 0) return 0;
        i += 7;
        while (i < line.Length && line[i] == ' ') i++;
        int s = i;
        if (i < line.Length && line[i] == '-') i++;
        while (i < line.Length && line[i] >= '0' && line[i] <= '9') i++;
        int v;
        if (int.TryParse(line.Substring(s, i - s), NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) return v;
        return 0;
    }

    static string AfterColon(string line)
    {
        int i = line.IndexOf(": ", StringComparison.Ordinal);
        return i < 0 ? "" : line.Substring(i + 2).Trim();
    }

    static string F(float v) { return v.ToString("0.####", CultureInfo.InvariantCulture); }

    // parse the "--- !u!N &id [stripped]" header; returns true if header
    static bool Header(string line, out int cls, out int id, out bool stripped)
    {
        cls = -1; id = 0; stripped = false;
        if (line.Length < 7 || line[0] != '-' || line[1] != '-' || line[2] != '-') return false;
        int bang = line.IndexOf("!u!", StringComparison.Ordinal);
        if (bang < 0) return false;
        int b = bang + 3, e = b;
        while (e < line.Length && line[e] >= '0' && line[e] <= '9') e++;
        if (e > b) { int c; if (int.TryParse(line.Substring(b, e - b), out c)) cls = c; }
        int amp = line.IndexOf("&", StringComparison.Ordinal);
        if (amp >= 0)
        {
            int a = amp + 1, z = a;
            if (z < line.Length && (line[z] == '-' || line[z] == '+')) z++;
            while (z < line.Length && line[z] >= '0' && line[z] <= '9') z++;
            if (z > a)
            {
                long lv;
                if (long.TryParse(line.Substring(a, z - a), NumberStyles.Integer, CultureInfo.InvariantCulture, out lv)
                    && lv >= int.MinValue && lv <= int.MaxValue) id = (int)lv;
            }
        }
        stripped = line.IndexOf("stripped", StringComparison.Ordinal) >= 0;
        return true;
    }

    static int ParseRow(string nm)
    {
        if (nm == null || !nm.StartsWith("Chunk_", StringComparison.Ordinal)) return -1;
        string[] p = nm.Split('_');
        if (p.Length < 3) return -1;
        int cx, cy;
        if (!int.TryParse(p[1], out cx)) return -1;
        if (!int.TryParse(p[2], out cy)) return -1;
        if (cy < 0 || cy > 10) return -1;
        return cy;
    }

    // ------------------------------------------------------------------ pass 1
    static void Scan(string src)
    {
        int cls = -1, id = 0;
        string nameTmp = "";
        int goId = 0, father = 0;
        bool inChildren = false;
        string line;
        using (var sr = new StreamReader(src, Encoding.UTF8, true, 1 << 20))
        {
            while ((line = sr.ReadLine()) != null)
            {
                int c2, i2; bool st2;
                if (Header(line, out c2, out i2, out st2))
                {
                    Store(cls, id, nameTmp, goId, father);
                    if (id != 0) usedIds.Add(id);
                    cls = st2 ? -2 : c2; id = i2;
                    nameTmp = ""; goId = 0; father = 0; inChildren = false;
                    continue;
                }
                if (cls == 1)
                {
                    if (line.StartsWith("  m_Name:", StringComparison.Ordinal)) nameTmp = AfterColon(line);
                    else if (line.StartsWith("  - component:", StringComparison.Ordinal))
                    {
                        int cid = ParseFileId(line);
                        if (cid != 0 && !goTrans.ContainsKey(id)) goTrans[id] = cid;
                    }
                }
                else if (cls == 4)
                {
                    if (line.StartsWith("  m_GameObject:", StringComparison.Ordinal)) goId = ParseFileId(line);
                    else if (line.StartsWith("  m_Father:", StringComparison.Ordinal)) father = ParseFileId(line);
                    else if (line.StartsWith("  m_Children:", StringComparison.Ordinal))
                    {
                        inChildren = true;
                        if (line.IndexOf("[]", StringComparison.Ordinal) >= 0) inChildren = false;
                    }
                    else if (inChildren && !line.StartsWith("  - {fileID:", StringComparison.Ordinal)) inChildren = false;
                }
                else if (cls == 1001)
                {
                    if (line.TrimStart().StartsWith("m_TransformParent:", StringComparison.Ordinal))
                    {
                        int pid = ParseFileId(line);
                        if (pid != 0) prefabParent[id] = pid;
                    }
                }
            }
            Store(cls, id, nameTmp, goId, father);
            if (id != 0) usedIds.Add(id);
        }

        foreach (var kv in goName)
            if (kv.Value == "WorldMap")
            {
                worldMapGo = kv.Key;
                if (goTrans.ContainsKey(kv.Key)) worldMapTrans = goTrans[kv.Key];
            }

        // chunk transforms = children of WorldMap
        foreach (var kv in trFath)
        {
            if (worldMapTrans == 0 || kv.Value != worldMapTrans) continue;
            int g; string nm;
            if (!trGo.TryGetValue(kv.Key, out g) || !goName.TryGetValue(g, out nm)) continue;
            int row = ParseRow(nm);
            if (row < 0) continue;
            chunkRow[kv.Key] = row;
            rowGo[g] = row + 1;
        }
        // their children (Ground) join the same scene
        foreach (var kv in trFath)
        {
            int r;
            if (!chunkRow.TryGetValue(kv.Value, out r)) continue;
            int g;
            if (trGo.TryGetValue(kv.Key, out g)) rowGo[g] = r + 1;
        }
        // core roots
        foreach (var kv in trFath)
        {
            if (kv.Value != 0 || kv.Key == worldMapTrans) continue;
            int g;
            if (trGo.TryGetValue(kv.Key, out g)) coreRootGo.Add(g);
        }
    }

    static void Store(int cls, int id, string nameTmp, int goId, int father)
    {
        if (id == 0) return;
        if (cls == 1) goName[id] = nameTmp;
        else if (cls == 4) { trGo[id] = goId; trFath[id] = father; }
    }

    // ------------------------------------------------------------- templates
    static void ReadTemplate(string path)
    {
        if (!File.Exists(path)) return;
        var lines = File.ReadAllLines(path);
        int i = 0;
        while (i < lines.Length)
        {
            if (!lines[i].StartsWith("--- !u!114", StringComparison.Ordinal)) { i++; continue; }
            int start = i; int id; int cls; bool st;
            Header(lines[i], out cls, out id, out st);
            int j = i + 1;
            var sb = new StringBuilder();
            sb.AppendLine(lines[i]);
            while (j < lines.Length && !lines[j].StartsWith("--- ", StringComparison.Ordinal))
            {
                sb.AppendLine(lines[j]);
                if (lines[j].StartsWith("  m_Script:", StringComparison.Ordinal))
                {
                    int k = lines[j].IndexOf("guid: ", StringComparison.Ordinal);
                    if (k >= 0)
                    {
                        string g = lines[j].Substring(k + 6, 32);
                        if (g == "a79441f348de89743a2939f4d699eac1") { tplCamData = sb.ToString(); tplCamId = id; }
                        else if (g == "474bcb49853aa07438625e644c072ee6") { tplLightData = sb.ToString(); tplLightId = id; }
                    }
                }
                j++;
            }
            i = j > start ? j : start + 1;
        }
    }

    static string Retarget(string tpl, int oldId, int newId, int newGo)
    {
        if (tpl.Length == 0 || newId == 0) return "";
        string s = tpl;
        s = s.Replace("--- !u!114 &" + oldId, "--- !u!114 &" + newId);
        int i = s.IndexOf("  m_GameObject: {fileID:", StringComparison.Ordinal);
        if (i >= 0)
        {
            int j = s.IndexOf("}", i, StringComparison.Ordinal);
            s = s.Substring(0, i) + "  m_GameObject: {fileID: " + newGo + s.Substring(j);
        }
        return s;
    }

    // ------------------------------------------------------------ scene table
    class SceneDef { public string name; public float zc; public float lr, lg, lb, li; public string chapter; }

    static SceneDef[] BuildTable()
    {
        var names = new string[]
        {
            "Scene_01_Sea_South", "Scene_02_Forest_South", "Scene_03_Forest_Core", "Scene_04_Forest_North",
            "Scene_05_City_South", "Scene_06_City_Core", "Scene_07_City_North", "Scene_08_Desert_South",
            "Scene_09_Sumer_Temple", "Scene_10_Giza_Desert", "Scene_11_Sea_North"
        };
        var chapters = new string[]
        {
            "Ch3 Boat Heist (south sea)",
            "Ch3 Valley approach (forest)",
            "Ch3 Valley (forest core)",
            "Ch3 Valley north (forest)",
            "Ch3 City south",
            "Ch6 Timeline (city core - start)",
            "Ch2 Facility (city north)",
            "Ch7 Mars (desert south + alien city)",
            "Ch5 Temple + Ch4 Sumer (desert)",
            "Ch1 Giza Villa (desert north)",
            "Finale open sea (north)"
        };
        var t = new SceneDef[11];
        for (int i = 0; i < 11; i++)
        {
            t[i] = new SceneDef();
            t[i].name = names[i];
            t[i].zc = (i - 5) * 5000f;
            t[i].chapter = chapters[i];
            if (i == 0 || i == 10)      { t[i].lr = 0.78f; t[i].lg = 0.88f; t[i].lb = 1.00f; t[i].li = 1.10f; }
            else if (i <= 3)            { t[i].lr = 0.86f; t[i].lg = 0.96f; t[i].lb = 0.82f; t[i].li = 1.15f; }
            else if (i <= 6)            { t[i].lr = 1.00f; t[i].lg = 0.98f; t[i].lb = 0.95f; t[i].li = 1.20f; }
            else                        { t[i].lr = 1.00f; t[i].lg = 0.92f; t[i].lb = 0.78f; t[i].li = 1.30f; }
        }
        return t;
    }

    // ----------------------------------------------------------------- pass 2
    public static void Run(string project, string srcRel, string coreRel, string worldRel, string tplRel)
    {
        string src = Path.Combine(project, srcRel);
        if (!File.Exists(src)) throw new FileNotFoundException("source scene", src);

        Scan(src);
        ReadTemplate(Path.Combine(project, tplRel));
        var table = BuildTable();

        if (chunkRow.Count != 121)
            Console.WriteLine("WARNING: expected 121 chunks, found " + chunkRow.Count);

        var w = new StreamWriter[SCENES];
        var rootGo = new List<int>[SCENES];
        for (int s = 0; s < SCENES; s++) rootGo[s] = new List<int>();

        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(project, coreRel)));
        Directory.CreateDirectory(Path.Combine(project, worldRel));
        for (int s = 0; s < SCENES; s++)
        {
            string p = (s == CORE) ? Path.Combine(project, coreRel)
                                   : Path.Combine(project, worldRel + "\\" + table[s - 1].name + ".unity");
            w[s] = new StreamWriter(p, false, new UTF8Encoding(false));
            w[s].Write("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n");
        }

        int nextId = 1000000000;
        Func<int> newId = delegate()
        {
            while (usedIds.Contains(nextId)) nextId++;
            usedIds.Add(nextId);
            return nextId++;
        };

        long written = 0, skipped = 0;
        var buf = new StringBuilder(8192);
        int curCls = -1, curId = 0, mGo = 0;
        int pendingScene = -99;
        string line;

        using (var sr = new StreamReader(src, Encoding.UTF8, true, 1 << 20))
        {
            while ((line = sr.ReadLine()) != null)
            {
                int c2, i2; bool st2;
                if (Header(line, out c2, out i2, out st2))
                {
                    // ---- emit the block that just ended ----
                    if (curCls != -1 && curId != 0)
                    {
                        if (curCls == 1660057539) { skipped++; }
                        else if (curCls == 29 || curCls == 104 || curCls == 157 || curCls == 196)
                        {
                            for (int s = 0; s < SCENES; s++) { w[s].Write(buf.ToString()); written++; }
                        }
                        else
                        {
                            int scene;
                            if (curCls == -2) scene = pendingScene;
                            else if (curCls == 1)
                            {
                                int r;
                                if (curId == worldMapGo) scene = -1;
                                else if (rowGo.TryGetValue(curId, out r)) scene = r;
                                else scene = CORE;
                            }
                            else if (curCls == 4)
                            {
                                int r;
                                if (curId == worldMapTrans) scene = -1;
                                else if (chunkRow.ContainsKey(curId))
                                {
                                    scene = chunkRow[curId] + 1;
                                    DetachFather(buf);
                                    rootGo[scene].Add(mGo);
                                }
                                else if (rowGo.TryGetValue(mGo, out r)) scene = r;
                                else scene = CORE;
                            }
                            else if (curCls == 1001)
                            {
                                int p, r;
                                if (prefabParent.TryGetValue(curId, out p) && chunkRow.TryGetValue(p, out r)) scene = r + 1;
                                else scene = CORE;
                                pendingScene = scene;
                            }
                            else
                            {
                                // every other component block belongs to its GameObject's scene
                                int r;
                                if (mGo != 0 && mGo == worldMapGo) scene = -1;
                                else if (mGo != 0 && rowGo.TryGetValue(mGo, out r)) scene = r;
                                else scene = CORE;
                            }

                            if (scene == -1 || scene == -99) skipped++;
                            else { w[scene].Write(buf.ToString()); written++; }
                        }
                    }

                    buf.Length = 0; mGo = 0;
                    curCls = st2 ? -2 : c2; curId = i2;
                    if (!st2) pendingScene = -99;
                    buf.AppendLine(line);
                    continue;
                }

                if (line.StartsWith("  m_GameObject:", StringComparison.Ordinal))
                    mGo = ParseFileId(line);
                buf.AppendLine(line);
            }
        }

        // ---- camera + light for each world scene ---------------------------
        for (int s = 1; s < SCENES; s++)
        {
            var d = table[s - 1];
            int camGo = newId(), camTr = newId(), camC = newId(), camData = newId();
            int litGo = newId(), litTr = newId(), litC = newId(), litData = newId();

            string camComp = "  - component: {fileID: " + camTr + "}\n  - component: {fileID: " + camC + "}\n";
            if (tplCamData.Length > 0) camComp += "  - component: {fileID: " + camData + "}\n";
            string litComp = "  - component: {fileID: " + litTr + "}\n  - component: {fileID: " + litC + "}\n";
            if (tplLightData.Length > 0) litComp += "  - component: {fileID: " + litData + "}\n";

            float camZ = d.zc + 2800f;
            double half = -28.0 * Math.PI / 180.0 / 2.0;
            float cqx = (float)Math.Sin(half), cqw = (float)Math.Cos(half);

            w[s].Write("--- !u!1 &" + camGo + "\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  serializedVersion: 6\n  m_Component:\n" + camComp + "  m_Layer: 0\n  m_Name: SceneCamera\n  m_TagString: MainCamera\n  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n");
            w[s].Write("--- !u!4 &" + camTr + "\nTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: " + camGo + "}\n  serializedVersion: 2\n  m_LocalRotation: {x: " + F(cqx) + ", y: 0, z: 0, w: " + F(cqw) + "}\n  m_LocalPosition: {x: 0, y: 1500, z: " + F(camZ) + "}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n  m_Children: []\n  m_Father: {fileID: 0}\n  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n");
            w[s].Write("--- !u!20 &" + camC + "\nCamera:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: " + camGo + "}\n  m_Enabled: 1\n  serializedVersion: 2\n  m_ClearFlags: 1\n  m_BackGroundColor: {r: 0.192, g: 0.302, b: 0.431, a: 0}\n  m_projectionMatrixMode: 1\n  m_GateFitMode: 2\n  m_FOVAxisMode: 0\n  m_SensorSize: {x: 36, y: 24}\n  m_LensShift: {x: 0, y: 0}\n  m_FocalLength: 50\n  m_NormalizedViewPortRect: {serializedVersion: 2, x: 0, y: 0, width: 1, height: 1}\n  near clip plane: 0.3\n  far clip plane: 2000\n  field of view: 60\n  orthographic: 0\n  orthographic size: 5\n  m_Depth: -1\n  m_CullingMask: {serializedVersion: 2, m_Bits: 4294967295}\n  m_RenderingPath: -1\n  m_TargetTexture: {fileID: 0}\n  m_TargetDisplay: 0\n  m_TargetEye: 3\n  m_HDR: 1\n  m_AllowMSAA: 1\n  m_AllowDynamicResolution: 0\n  m_ForceIntoRT: 0\n  m_OcclusionCulling: 1\n  m_StereoConvergence: 10\n  m_StereoSeparation: 0.022\n");
            if (tplCamData.Length > 0) w[s].Write(Retarget(tplCamData, tplCamId, camData, camGo));

            float ax = 25f * (float)Math.PI / 180f, ay = -15f * (float)Math.PI / 180f;
            float lx = (float)(Math.Cos(ay) * Math.Sin(ax));
            float ly = (float)Math.Sin(ay);
            float lz = (float)(-Math.Sin(ay) * Math.Sin(ax));
            float lw = (float)(Math.Cos(ay) * Math.Cos(ax));
            w[s].Write("--- !u!1 &" + litGo + "\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  serializedVersion: 6\n  m_Component:\n" + litComp + "  m_Layer: 0\n  m_Name: SceneLight\n  m_TagString: Untagged\n  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n");
            w[s].Write("--- !u!4 &" + litTr + "\nTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: " + litGo + "}\n  serializedVersion: 2\n  m_LocalRotation: {x: " + F(lx) + ", y: " + F(ly) + ", z: " + F(lz) + ", w: " + F(lw) + "}\n  m_LocalPosition: {x: 0, y: 300, z: " + F(d.zc) + "}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n  m_Children: []\n  m_Father: {fileID: 0}\n  m_LocalEulerAnglesHint: {x: 50, y: -30, z: 0}\n");
            w[s].Write("--- !u!108 &" + litC + "\nLight:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: " + litGo + "}\n  m_Enabled: 1\n  serializedVersion: 11\n  m_Type: 1\n  m_Shape: 0\n  m_Color: {r: " + F(d.lr) + ", g: " + F(d.lg) + ", b: " + F(d.lb) + ", a: 1}\n  m_Intensity: " + F(d.li) + "\n  m_Range: 10\n  m_SpotAngle: 30\n  m_InnerSpotAngle: 21.80208\n  m_CookieSize: 10\n  m_Shadows:\n    m_Type: 2\n    m_Resolution: -1\n    m_CustomResolution: -1\n    m_Strength: 0.85\n    m_Bias: 0.05\n    m_NormalBias: 0.4\n    m_NearPlane: 0.2\n    m_CullingMatrixOverride:\n      e00: 1, e01: 0, e02: 0, e03: 0\n      e10: 0, e11: 1, e12: 0, e13: 0\n      e20: 0, e21: 0, e22: 1, e23: 0\n      e30: 0, e31: 0, e32: 0, e33: 1\n  m_RenderMode: 0\n  m_CullingMask:\n    serializedVersion: 2\n    m_Bits: 4294967295\n  m_RenderingLayerMask: 1\n  m_Lightmapping: 4\n  m_LightShadowCasterMode: 0\n  m_AreaSize: {x: 1, y: 1}\n  m_BounceIntensity: 1\n  m_ColorTemperature: 6570\n  m_UseColorTemperature: 0\n  m_BoundingSphereOverride: {x: 0, y: 0, z: 0, w: 0}\n  m_UseBoundingSphereOverride: 0\n  m_UseViewFrustumForShadowCasterCull: 1\n  m_ShadowRadius: 0\n  m_ShadowAngle: 0\n");
            if (tplLightData.Length > 0) w[s].Write(Retarget(tplLightData, tplLightId, litData, litGo));

            rootGo[s].Add(camGo);
            rootGo[s].Add(litGo);
        }

        foreach (int g in coreRootGo) rootGo[CORE].Add(g);

        for (int s = 0; s < SCENES; s++)
        {
            var sb = new StringBuilder();
            sb.Append("--- !u!1660057539 &9223372036854775807\nSceneRoots:\n  m_ObjectHideFlags: 0\n  m_Roots:\n");
            foreach (int g in rootGo[s]) sb.Append("  - {fileID: " + g + "}\n");
            w[s].Write(sb.ToString());
            w[s].Flush();
            w[s].Dispose();
        }

        // ---- manifest (single column x=0, coord (0,row)) -------------------
        var msb = new StringBuilder();
        msb.AppendLine("# Auto-generated by ops/split-world-11-scenes.ps1 - do not edit by hand");
        msb.AppendLine("# world: 11 rows (z = -25000..+25000), each row scene spans the full 50000 X width");
        msb.AppendLine("gridSize=11");
        msb.AppendLine("chunkSize=5000");
        for (int s = 1; s < SCENES; s++)
        {
            var d = table[s - 1];
            msb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "Row_0_{0}|Assets/Scenes/World/{1}.unity|-25000|{2}", (s - 1), d.name, ((int)d.zc).ToString(CultureInfo.InvariantCulture)));
        }
        string m1 = Path.Combine(project, @"Assets\_Game\Resources\WorldManifest.txt");
        string m2 = Path.Combine(project, @"Assets\_Game\World\WorldManifest.txt");
        File.WriteAllText(m1, msb.ToString(), new UTF8Encoding(false));
        File.WriteAllText(m2, msb.ToString(), new UTF8Encoding(false));

        Console.WriteLine("chunks found        : " + chunkRow.Count);
        Console.WriteLine("prefab instances    : " + prefabParent.Count);
        Console.WriteLine("blocks written      : " + written);
        Console.WriteLine("blocks skipped      : " + skipped);
        Console.WriteLine("core root objects   : " + coreRootGo.Count);
        Console.WriteLine("URP camera template : " + (tplCamData.Length > 0 ? "yes" : "no"));
        Console.WriteLine("URP light template  : " + (tplLightData.Length > 0 ? "yes" : "no"));
        Console.WriteLine("manifest            : " + m1);
    }

    static void DetachFather(StringBuilder buf)
    {
        string s = buf.ToString();
        int i = s.IndexOf("  m_Father: {fileID:", StringComparison.Ordinal);
        if (i < 0) return;
        int j = s.IndexOf("}", i, StringComparison.Ordinal);
        if (j < 0) return;
        buf.Length = 0;
        // keep the original closing brace: s.Substring(j + 1)
        buf.Append(s.Substring(0, i)).Append("  m_Father: {fileID: 0}").Append(s.Substring(j + 1));
    }
}
'@

Add-Type -TypeDefinition $cs -Language CSharp
[WorldSplitter]::Run($Project, $SourceRel, $OutCore, $OutWorld, $Template)
Write-Output "DONE"
