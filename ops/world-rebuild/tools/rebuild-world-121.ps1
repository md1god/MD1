# rebuild-world-121.ps1
# =============================================================================
#  World Rebuilder --scale-correct
#
#  Rebuilds the open world from SampleScene.unity with:
#     * every one of the 121 chunks as its OWN scene
#     * world scale corrected: chunk spacing 5000 -> 1000 units
#       (world 50km -> 11km, density x25, no empty land)
#     * a camera + a directional light inside every scene
#       (fixes the black-screen problem)
#     * a manifest that matches WorldManifestParser.cs exactly
#     * no asset is ever deleted
#
#  Usage:
#     powershell -ExecutionPolicy Bypass -File ops\world-rebuild\tools\rebuild-world-121.ps1
# =============================================================================
param(
  [string]$Project   = "D:\DiDo111_CC-Game",
  [string]$SourceRel = "Assets\Scenes\SampleScene.unity",
  [string]$OutCore   = "Assets\Scenes\Core\Core.unity",
  [string]$OutWorld  = "Assets\Scenes\World",
  [string]$Template  = "Assets\Scenes\WebGLMini.unity",
  [double]$Scale     = 0.2
)

$ErrorActionPreference = "Stop"
$cs = @'
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

public static class WorldRebuilder
{
    const int CORE = 0;
    public static double S = 0.2;

    static Dictionary<int,string> goName = new Dictionary<int,string>();
    static Dictionary<int,int> goTrans = new Dictionary<int,int>();
    static Dictionary<int,int> trGo    = new Dictionary<int,int>();
    static Dictionary<int,int> trFath  = new Dictionary<int,int>();
    static Dictionary<int,float> trX   = new Dictionary<int,float>();
    static Dictionary<int,float> trZ   = new Dictionary<int,float>();
    static Dictionary<int,int> chunkScene = new Dictionary<int,int>();  // chunk transform -> scene idx
    static Dictionary<int,int> goScene    = new Dictionary<int,int>();  // goId -> scene idx
    static Dictionary<int,int> groundScale= new Dictionary<int,int>();  // transform -> scale its localScale
    static Dictionary<int,int> prefabParent= new Dictionary<int,int>();
    static HashSet<int> usedIds = new HashSet<int>();
    static int worldMapGo = 0, worldMapTrans = 0;
    static List<int> coreRootGo = new List<int>();

    static List<string> sceneName = new List<string>();
    static List<float> sceneCX = new List<float>();
    static List<float> sceneCZ = new List<float>();
    static List<string> sceneBiome = new List<string>();

    static string tplCamData = "", tplLightData = "";
    static int tplCamId = 0, tplLightId = 0;

    // ---------------------------------------------------------------- utils
    static int Pid(string line)
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
    static string AC(string l) { int i = l.IndexOf(": ", StringComparison.Ordinal); return i < 0 ? "" : l.Substring(i + 2).Trim(); }
    static string F(float v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }

    static bool Hdr(string line, out int cls, out int id, out bool stripped)
    {
        cls = -1; id = 0; stripped = false;
        if (line.Length < 7 || line[0] != '-' || line[1] != '-' || line[2] != '-') return false;
        int b = line.IndexOf("!u!", StringComparison.Ordinal);
        if (b >= 0)
        {
            int p = b + 3, e = p;
            while (e < line.Length && line[e] >= '0' && line[e] <= '9') e++;
            if (e > p) { int c; if (int.TryParse(line.Substring(p, e - p), out c)) cls = c; }
        }
        int a = line.IndexOf("&", StringComparison.Ordinal);
        if (a >= 0)
        {
            int p = a + 1, e = p;
            if (e < line.Length && (line[e] == '-' || line[e] == '+')) e++;
            while (e < line.Length && line[e] >= '0' && line[e] <= '9') e++;
            if (e > p)
            {
                long lv;
                if (long.TryParse(line.Substring(p, e - p), NumberStyles.Integer, CultureInfo.InvariantCulture, out lv)
                    && lv >= int.MinValue && lv <= int.MaxValue) id = (int)lv;
            }
        }
        stripped = line.IndexOf("stripped", StringComparison.Ordinal) >= 0;
        return true;
    }

    static bool ParseChunk(string nm, out int cx, out int cz, out string biome)
    {
        cx = cz = 0; biome = "";
        if (nm == null || !nm.StartsWith("Chunk_", StringComparison.Ordinal)) return false;
        string[] p = nm.Split('_');
        if (p.Length < 3) return false;
        if (!int.TryParse(p[1], out cx)) return false;
        if (!int.TryParse(p[2], out cz)) return false;
        if (p.Length > 3) biome = p[3];
        return cx >= 0 && cx <= 10 && cz >= 0 && cz <= 10;
    }

    // ---------------------------------------------------------------- pass 1
    static void Scan(string src)
    {
        int cls = -1, id = 0, goId = 0, father = 0;
        float px = 0f, pz = 0f;
        bool inChildren = false;
        string nm = "", line;

        using (var sr = new StreamReader(src, Encoding.UTF8, true, 1 << 20))
        {
            while ((line = sr.ReadLine()) != null)
            {
                int c2, i2; bool st;
                if (Hdr(line, out c2, out i2, out st))
                {
                    Store(cls, id, nm, goId, father, px, pz);
                    if (id != 0) usedIds.Add(id);
                    cls = st ? -2 : c2; id = i2; nm = ""; goId = 0; father = 0; px = pz = 0f; inChildren = false;
                    continue;
                }
                if (cls == 1)
                {
                    if (line.StartsWith("  m_Name:", StringComparison.Ordinal)) nm = AC(line);
                    else if (line.StartsWith("  - component:", StringComparison.Ordinal))
                    {
                        int cid = Pid(line);
                        if (cid != 0 && !goTrans.ContainsKey(id)) goTrans[id] = cid;
                    }
                }
                else if (cls == 4)
                {
                    if (line.StartsWith("  m_GameObject:", StringComparison.Ordinal)) goId = Pid(line);
                    else if (line.StartsWith("  m_Father:", StringComparison.Ordinal)) father = Pid(line);
                    else if (line.StartsWith("  m_LocalPosition:", StringComparison.Ordinal))
                    {
                        string p = AC(line);
                        int ix = p.IndexOf("x:", StringComparison.Ordinal);
                        int iz = p.IndexOf("z:", StringComparison.Ordinal);
                        if (ix >= 0) { float t; if (float.TryParse(p.Substring(ix + 2, (iz > ix ? iz - ix : p.Length - ix - 2)).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out t)) px = t; }
                        if (iz >= 0) { float t; if (float.TryParse(p.Substring(iz + 2).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out t)) pz = t; }
                    }
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
                        int q = Pid(line);
                        if (q != 0) prefabParent[id] = q;
                    }
                }
            }
            Store(cls, id, nm, goId, father, px, pz);
            if (id != 0) usedIds.Add(id);
        }

        foreach (var kv in goName)
            if (kv.Value == "WorldMap")
            {
                worldMapGo = kv.Key;
                if (goTrans.ContainsKey(kv.Key)) worldMapTrans = goTrans[kv.Key];
            }

        // chunks = children of WorldMap -> one scene each
        foreach (var kv in trFath)
        {
            if (worldMapTrans == 0 || kv.Value != worldMapTrans) continue;
            int g; string name;
            if (!trGo.TryGetValue(kv.Key, out g) || !goName.TryGetValue(g, out name)) continue;
            int cx, cz; string biome;
            if (!ParseChunk(name, out cx, out cz, out biome)) continue;

            sceneName.Add(string.Format(CultureInfo.InvariantCulture, "Chunk_{0}_{1}_{2}", cx, cz, biome));
            sceneCX.Add((float)((cx - 5) * 1000 * S));
            sceneCZ.Add((float)((cz - 5) * 1000 * S));
            sceneBiome.Add(biome);
            int si = sceneName.Count;   // 1..121

            chunkScene[kv.Key] = si;
            goScene[g] = si;
        }

        // ground children follow their chunk, and their scale is corrected
        foreach (var kv in trFath)
        {
            int si;
            if (!chunkScene.TryGetValue(kv.Value, out si)) continue;
            int g;
            if (trGo.TryGetValue(kv.Key, out g)) goScene[g] = si;
            groundScale[kv.Key] = 1;
        }

        foreach (var kv in trFath)
        {
            if (kv.Value != 0 || kv.Key == worldMapTrans) continue;
            int g;
            if (trGo.TryGetValue(kv.Key, out g)) coreRootGo.Add(g);
        }
    }

    static void Store(int cls, int id, string nm, int goId, int father, float px, float pz)
    {
        if (id == 0) return;
        if (cls == 1) goName[id] = nm;
        else if (cls == 4) { trGo[id] = goId; trFath[id] = father; trX[id] = px; trZ[id] = pz; }
    }

    static void ReadTemplate(string path)
    {
        if (!File.Exists(path)) return;
        var lines = File.ReadAllLines(path);
        int i = 0;
        while (i < lines.Length)
        {
            if (!lines[i].StartsWith("--- !u!114", StringComparison.Ordinal)) { i++; continue; }
            int id, cls; bool st;
            Hdr(lines[i], out cls, out id, out st);
            var sb = new StringBuilder();
            sb.AppendLine(lines[i]);
            int j = i + 1;
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
            i = j > i ? j : i + 1;
        }
    }

    static string Retarget(string tpl, int oldId, int newId, int newGo)
    {
        if (tpl.Length == 0 || newId == 0) return "";
        string s = tpl.Replace("--- !u!114 &" + oldId, "--- !u!114 &" + newId);
        int i = s.IndexOf("  m_GameObject: {fileID:", StringComparison.Ordinal);
        if (i >= 0)
        {
            int j = s.IndexOf("}", i, StringComparison.Ordinal);
            s = s.Substring(0, i) + "  m_GameObject: {fileID: " + newGo + s.Substring(j);
        }
        return s;
    }

    // ---------------------------------------------------------------- scaling
    static readonly Regex ReLocalXZ = new Regex(@"(propertyPath: m_LocalPosition\.[xz]\r?\n\s+value:\s*)(-?[0-9.eE+\-]+)", RegexOptions.Compiled);

    static string ScaleInstancePositions(string s)
    {
        return ReLocalXZ.Replace(s, delegate(Match m)
        {
            double v;
            if (!double.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return m.Value;
            return m.Groups[1].Value + (v * S).ToString("0.###", CultureInfo.InvariantCulture);
        });
    }

    static string SetLine(string s, string key, string newVal)
    {
        int i = s.IndexOf(key, StringComparison.Ordinal);
        if (i < 0) return s;
        int eol = s.IndexOf('\n', i);
        if (eol < 0) eol = s.Length;
        return s.Substring(0, i) + key + newVal + s.Substring(eol);
    }

    static string ScaleLocalPosition(string s, float x, float z)
    {
        string rep = "{x: " + F(x) + ", y: 0, z: " + F(z) + "}";
        return SetLine(s, "  m_LocalPosition: ", rep);
    }

    static string ScaleLocalScaleXZ(string s, float k)
    {
        int i = s.IndexOf("  m_LocalScale: ", StringComparison.Ordinal);
        if (i < 0) return s;
        int eol = s.IndexOf('\n', i);
        if (eol < 0) eol = s.Length;
        string line = s.Substring(i, eol - i);
        int c1 = line.IndexOf("x:", StringComparison.Ordinal);
        int c2 = line.IndexOf("y:", StringComparison.Ordinal);
        int c3 = line.IndexOf("z:", StringComparison.Ordinal);
        if (c1 < 0 || c2 < 0 || c3 < 0) return s;
        float sx, sy, sz;
        if (!float.TryParse(line.Substring(c1 + 2, c2 - c1 - 2).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out sx)) return s;
        if (!float.TryParse(line.Substring(c2 + 2, c3 - c2 - 2).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out sy)) return s;
        if (!float.TryParse(line.Substring(c3 + 2).Trim().TrimEnd(',', '}'), NumberStyles.Float, CultureInfo.InvariantCulture, out sz)) return s;
        string rep = "  m_LocalScale: {x: " + F(sx * k) + ", y: " + F(sy) + ", z: " + F(sz * k) + "}";
        return s.Substring(0, i) + rep + s.Substring(eol);
    }

    // ---------------------------------------------------------------- biome light
    static void LightFor(string biome, out float r, out float g, out float b, out float inten)
    {
        switch (biome)
        {
            case "Sea":    r = 0.80f; g = 0.89f; b = 1.00f; inten = 1.15f; break;
            case "Forest": r = 0.86f; g = 0.96f; b = 0.80f; inten = 1.15f; break;
            case "City":   r = 1.00f; g = 0.98f; b = 0.94f; inten = 1.25f; break;
            case "Desert": r = 1.00f; g = 0.92f; b = 0.76f; inten = 1.35f; break;
            case "Alien":  r = 0.72f; g = 0.80f; b = 1.00f; inten = 1.05f; break;
            default:       r = 1.00f; g = 0.98f; b = 0.95f; inten = 1.20f; break;
        }
    }

    // ---------------------------------------------------------------- main
    public static void Run(string project, string srcRel, string coreRel, string worldRel, string tplRel, double scale)
    {
        S = scale;
        string src = Path.Combine(project, srcRel);
        if (!File.Exists(src)) throw new FileNotFoundException("source", src);

        Scan(src);
        ReadTemplate(Path.Combine(project, tplRel));

        int nScenes = sceneName.Count + 1;   // 1 core + N chunks
        Console.WriteLine("chunks found : " + sceneName.Count);
        if (sceneName.Count != 121) Console.WriteLine("WARNING: expected 121");

        // clean the old world folder
        if (Directory.Exists(Path.Combine(project, worldRel)))
        {
            foreach (var f in Directory.GetFiles(Path.Combine(project, worldRel), "*.unity*")) File.Delete(f);
        }
        else Directory.CreateDirectory(Path.Combine(project, worldRel));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(project, coreRel)));

        var w = new StreamWriter[nScenes];
        var roots = new List<int>[nScenes];
        for (int s = 0; s < nScenes; s++) roots[s] = new List<int>();

        for (int s = 0; s < nScenes; s++)
        {
            string pth = (s == CORE) ? Path.Combine(project, coreRel)
                                     : Path.Combine(project, worldRel + "\\" + sceneName[s - 1] + ".unity");
            w[s] = new StreamWriter(pth, false, new UTF8Encoding(false));
            w[s].Write("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n");
        }

        int nextId = 1000000000;
        Func<int> nid = delegate() { while (usedIds.Contains(nextId)) nextId++; usedIds.Add(nextId); return nextId++; };

        var buf = new StringBuilder(8192);
        int curCls = -1, curId = 0, mGo = 0, pending = -99;
        long written = 0;
        string line;

        using (var sr = new StreamReader(src, Encoding.UTF8, true, 1 << 20))
        {
            while ((line = sr.ReadLine()) != null)
            {
                int c2, i2; bool st;
                if (Hdr(line, out c2, out i2, out st))
                {
                    if (curCls != -1 && curId != 0 && curCls != 1660057539)
                    {
                        if (curCls == 29 || curCls == 104 || curCls == 157 || curCls == 196)
                        {
                            for (int s = 0; s < nScenes; s++) { w[s].Write(buf.ToString()); written++; }
                        }
                        else
                        {
                            int scene;
                            string text = buf.ToString();
                            if (curCls == -2) scene = pending;
                            else if (curCls == 1)
                            {
                                int r;
                                if (curId == worldMapGo) scene = -1;
                                else if (goScene.TryGetValue(curId, out r)) scene = r;
                                else scene = CORE;
                            }
                            else if (curCls == 4)
                            {
                                int r;
                                if (curId == worldMapTrans) scene = -1;
                                else if (chunkScene.ContainsKey(curId))
                                {
                                    scene = chunkScene[curId];
                                    text = ScaleLocalPosition(text, trX[curId] * (float)S, trZ[curId] * (float)S);
                                    roots[scene].Add(mGo);
                                }
                                else if (groundScale.ContainsKey(curId))
                                {
                                    scene = chunkScene[trFath[curId]];
                                    text = ScaleLocalScaleXZ(text, (float)S);
                                }
                                else if (goScene.TryGetValue(mGo, out r)) scene = r;
                                else scene = CORE;
                            }
                            else if (curCls == 1001)
                            {
                                int p, r;
                                if (prefabParent.TryGetValue(curId, out p) && chunkScene.TryGetValue(p, out r))
                                {
                                    scene = r;
                                    text = ScaleInstancePositions(text);
                                }
                                else scene = CORE;
                                pending = scene;
                            }
                            else
                            {
                                int r;
                                if (mGo != 0 && mGo == worldMapGo) scene = -1;
                                else if (mGo != 0 && goScene.TryGetValue(mGo, out r)) scene = r;
                                else scene = CORE;
                            }

                            if (scene == -1 || scene == -99) { }
                            else { w[scene].Write(text); written++; }
                        }
                    }

                    buf.Length = 0; mGo = 0;
                    curCls = st ? -2 : c2; curId = i2;
                    if (!st) pending = -99;
                    buf.AppendLine(line);
                    continue;
                }
                if (line.StartsWith("  m_GameObject:", StringComparison.Ordinal)) mGo = Pid(line);
                buf.AppendLine(line);
            }
        }

        // ---- camera + light per chunk scene ----
        for (int s = 1; s < nScenes; s++)
        {
            float cx = sceneCX[s - 1], cz = sceneCZ[s - 1];
            float lr, lg, lb, li;
            LightFor(sceneBiome[s - 1], out lr, out lg, out lb, out li);

            int camGo = nid(), camTr = nid(), camC = nid(), camD = nid();
            int litGo = nid(), litTr = nid(), litC = nid(), litD = nid();

            string camComp = "  - component: {fileID: " + camTr + "}\n  - component: {fileID: " + camC + "}\n"
                           + (tplCamData.Length > 0 ? "  - component: {fileID: " + camD + "}\n" : "");
            string litComp = "  - component: {fileID: " + litTr + "}\n  - component: {fileID: " + litC + "}\n"
                           + (tplLightData.Length > 0 ? "  - component: {fileID: " + litD + "}\n" : "");

            double half = -32.0 * Math.PI / 180.0 / 2.0;
            float cqx = (float)Math.Sin(half), cqw = (float)Math.Cos(half);

            w[s].Write("--- !u!1 &" + camGo + "\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  serializedVersion: 6\n  m_Component:\n" + camComp + "  m_Layer: 0\n  m_Name: SceneCamera\n  m_TagString: MainCamera\n  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n");
            w[s].Write("--- !u!4 &" + camTr + "\nTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: " + camGo + "}\n  serializedVersion: 2\n  m_LocalRotation: {x: " + F(cqx) + ", y: 0, z: 0, w: " + F(cqw) + "}\n  m_LocalPosition: {x: " + F(cx) + ", y: 260, z: " + F(cz + 380) + "}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n  m_Children: []\n  m_Father: {fileID: 0}\n  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n");
            w[s].Write("--- !u!20 &" + camC + "\nCamera:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: " + camGo + "}\n  m_Enabled: 1\n  serializedVersion: 2\n  m_ClearFlags: 1\n  m_BackGroundColor: {r: 0.32, g: 0.47, b: 0.63, a: 0}\n  near clip plane: 0.3\n  far clip plane: 4000\n  field of view: 60\n  orthographic: 0\n  orthographic size: 5\n  m_Depth: -1\n  m_CullingMask: {serializedVersion: 2, m_Bits: 4294967295}\n  m_TargetTexture: {fileID: 0}\n  m_TargetDisplay: 0\n  m_TargetEye: 3\n  m_HDR: 1\n  m_AllowMSAA: 1\n  m_OcclusionCulling: 1\n");
            if (tplCamData.Length > 0) w[s].Write(Retarget(tplCamData, tplCamId, camD, camGo));

            float ax = 25f * (float)Math.PI / 180f, ay = -15f * (float)Math.PI / 180f;
            float lx = (float)(Math.Cos(ay) * Math.Sin(ax));
            float ly = (float)Math.Sin(ay);
            float lz = (float)(-Math.Sin(ay) * Math.Sin(ax));
            float lw = (float)(Math.Cos(ay) * Math.Cos(ax));
            w[s].Write("--- !u!1 &" + litGo + "\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  serializedVersion: 6\n  m_Component:\n" + litComp + "  m_Layer: 0\n  m_Name: SceneLight\n  m_TagString: Untagged\n  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n");
            w[s].Write("--- !u!4 &" + litTr + "\nTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: " + litGo + "}\n  serializedVersion: 2\n  m_LocalRotation: {x: " + F(lx) + ", y: " + F(ly) + ", z: " + F(lz) + ", w: " + F(lw) + "}\n  m_LocalPosition: {x: " + F(cx) + ", y: 120, z: " + F(cz) + "}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n  m_Children: []\n  m_Father: {fileID: 0}\n  m_LocalEulerAnglesHint: {x: 50, y: -30, z: 0}\n");
            w[s].Write("--- !u!108 &" + litC + "\nLight:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: " + litGo + "}\n  m_Enabled: 1\n  serializedVersion: 11\n  m_Type: 1\n  m_Shape: 0\n  m_Color: {r: " + F(lr) + ", g: " + F(lg) + ", b: " + F(lb) + ", a: 1}\n  m_Intensity: " + F(li) + "\n  m_Range: 10\n  m_SpotAngle: 30\n  m_Shadows:\n    m_Type: 2\n    m_Resolution: -1\n    m_Strength: 0.85\n    m_Bias: 0.05\n    m_NormalBias: 0.4\n    m_NearPlane: 0.2\n  m_RenderMode: 0\n  m_CullingMask:\n    serializedVersion: 2\n    m_Bits: 4294967295\n  m_RenderingLayerMask: 1\n  m_Lightmapping: 4\n  m_BounceIntensity: 1\n  m_UseColorTemperature: 0\n");
            if (tplLightData.Length > 0) w[s].Write(Retarget(tplLightData, tplLightId, litD, litGo));

            roots[s].Add(camGo);
            roots[s].Add(litGo);
        }

        foreach (int g in coreRootGo) roots[CORE].Add(g);

        for (int s = 0; s < nScenes; s++)
        {
            var sb = new StringBuilder();
            sb.Append("--- !u!1660057539 &9223372036854775807\nSceneRoots:\n  m_ObjectHideFlags: 0\n  m_Roots:\n");
            foreach (int g in roots[s]) sb.Append("  - {fileID: " + g + "}\n");
            w[s].Write(sb.ToString());
            w[s].Flush();
            w[s].Dispose();
        }

        // ---- manifest ----
        var msb = new StringBuilder();
        msb.AppendLine("# Auto-generated by ops/world-rebuild/tools/rebuild-world-121.ps1");
        msb.AppendLine("# world 11000 x 11000 units, 11x11 grid, chunkSize=1000, origin -5000,-5000");
        msb.AppendLine("# streaming: PlayerChunkTracker loads a 3x3 neighbourhood (9 scenes)");
        msb.AppendLine("gridSize=11");
        msb.AppendLine("chunkSize=1000");
        for (int s = 1; s < nScenes; s++)
        {
            msb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "{0}|Assets/Scenes/World/{1}.unity|{2}|{3}",
                sceneName[s - 1].Substring(0, sceneName[s - 1].IndexOf('_', 6) + 1) + ((s - 1) % 11).ToString(CultureInfo.InvariantCulture),
                sceneName[s - 1],
                ((int)sceneCX[s - 1]).ToString(CultureInfo.InvariantCulture),
                ((int)sceneCZ[s - 1]).ToString(CultureInfo.InvariantCulture)));
        }
        // rebuild the names properly: Chunk_X_Y
        msb.Length = 0;
        msb.AppendLine("# Auto-generated by ops/world-rebuild/tools/rebuild-world-121.ps1");
        msb.AppendLine("# world 11000 x 11000 units, 11x11 grid, chunkSize=1000");
        msb.AppendLine("gridSize=11");
        msb.AppendLine("chunkSize=1000");
        for (int i = 0; i < sceneName.Count; i++)
        {
            string full = sceneName[i];
            string key = full.Substring(0, full.LastIndexOf('_'));   // Chunk_X_Y
            msb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "{0}|Assets/Scenes/World/{1}.unity|{2}|{3}", key, full,
                ((int)sceneCX[i]).ToString(CultureInfo.InvariantCulture),
                ((int)sceneCZ[i]).ToString(CultureInfo.InvariantCulture)));
        }
        string m1 = Path.Combine(project, @"Assets\_Game\Resources\WorldManifest.txt");
        string m2 = Path.Combine(project, @"Assets\_Game\World\WorldManifest.txt");
        File.WriteAllText(m1, msb.ToString(), new UTF8Encoding(false));
        File.WriteAllText(m2, msb.ToString(), new UTF8Encoding(false));

        Console.WriteLine("scenes written : " + (nScenes - 1));
        Console.WriteLine("blocks written : " + written);
        Console.WriteLine("core roots     : " + coreRootGo.Count);
        Console.WriteLine("scale          : " + S);
        Console.WriteLine("manifest       : " + m1);
    }
}
'@

Add-Type -TypeDefinition $cs -Language CSharp
[WorldRebuilder]::Run($Project, $SourceRel, $OutCore, $OutWorld, $Template, $Scale)
Write-Output "DONE"
