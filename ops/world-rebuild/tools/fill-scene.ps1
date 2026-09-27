# fill-scene.ps1 - يبني مشهداً حقيقياً: يقلّل التكرار ويضيف أصولاً مناسبة للـbiome
# =============================================================================
#  مبدأ: المشهد يجب أن يبدو كعالم، لا كDump لأصول.
#   1) يقلّل النسخ المكررة إلى عدد واقعي (يبقى منها عدد محدود)
#   2) يضيف أصولاً مناسبة للـ biome من قوائم Asset Store الحقيقية
#   3) يوزّعها بتوزيع عشوائي منظّم داخل حدود المقطع
#   4) يحدّث SceneRoots
# =============================================================================
param(
  [string]$Project = "D:\DiDo111_CC-Game",
  [string]$Scene   = "Chunk_0_0_Sea",
  [int]$MaxEach     = 14,     # أقصى نسخ لكل نموذج مكرر
  [int]$Seed       = 12345
)
$ErrorActionPreference = "Stop"
$cs = @'
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

public static class SceneFiller
{
    static Dictionary<string,string> idx = new Dictionary<string,string>();   // guid -> asset path
    static Dictionary<string,string> byPath = new Dictionary<string,string>(); // path -> guid
    static Random rng;

    static void BuildIndex(string project)
    {
        foreach (var ext in new[] { ".prefab.meta", ".fbx.meta" })
        {
            foreach (var meta in Directory.GetFiles(Path.Combine(project, "Assets"), "*" + ext, SearchOption.AllDirectories))
            {
                try
                {
                    var lines = File.ReadLines(meta);
                    using (var e = lines.GetEnumerator())
                    {
                        if (!e.MoveNext()) continue;
                        if (!e.MoveNext()) continue;
                        string l = e.Current;
                        if (l == null || !l.StartsWith("guid: ", StringComparison.Ordinal)) continue;
                        string g = l.Substring(6).Trim();
                        if (g.Length != 32) continue;
                        string rel = meta.Substring(project.Length + 1, meta.Length - project.Length - 1 - 5).Replace('\\', '/');
                        idx[g] = rel;
                        byPath[rel] = g;
                    }
                }
                catch { }
            }
        }
    }

    // pick the first asset whose path contains ANY of the keywords
    static string Find(string[] keys)
    {
        foreach (var k in keys)
            foreach (var kv in byPath)
                if (kv.Key.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) return kv.Value;
        return null;
    }

    public static void Run(string project, string sceneName, int maxEach, int seed)
    {
        rng = new Random(seed);
        BuildIndex(project);
        Console.WriteLine("indexed: " + idx.Count);

        string path = Path.Combine(project, @"Assets\Scenes\World\" + sceneName + ".unity");
        if (!File.Exists(path)) { Console.WriteLine("scene not found"); return; }

        string[] parts = sceneName.Split('_');
        int cx = int.Parse(parts[1]), cz = int.Parse(parts[2]);
        string biome = parts.Length > 3 ? parts[3] : "City";
        float ox = (cx - 5) * 1000f, oz = (cz - 5) * 1000f;
        float half = 460f;                      // keep inside the 1000x1000 cell
        Console.WriteLine("biome=" + biome + " origin=(" + ox + "," + oz + ")");

        // ---------- read the file, split into blocks ----------
        var lines = File.ReadAllLines(path);
        int headerEnd = 0;
        while (headerEnd < lines.Length && !lines[headerEnd].StartsWith("--- ", StringComparison.Ordinal)) headerEnd++;

        var keep = new List<string>();
        for (int i = 0; i < headerEnd; i++) keep.Add(lines[i]);

        int rootsStart = lines.Length - 1;
        for (int i = 0; i < lines.Length; i++)
            if (lines[i].StartsWith("--- !u!1660057539", StringComparison.Ordinal)) { rootsStart = i; break; }

        // blocks between header and SceneRoots
        var blocks = new List<string[]>();
        int cur = headerEnd;
        for (int i = headerEnd; i < rootsStart; i++)
        {
            if (lines[i].StartsWith("--- ", StringComparison.Ordinal)) { cur = i; }
            int end = i + 1;
            while (end < rootsStart && !lines[end].StartsWith("--- ", StringComparison.Ordinal)) end++;
            if (end > i + 1) { }
        }
        // simpler: linear scan
        var blockList = new List<string>();
        var sb = new StringBuilder();
        for (int i = headerEnd; i < rootsStart; i++)
        {
            sb.AppendLine(lines[i]);
            bool last = (i == rootsStart - 1) || lines[i + 1].StartsWith("--- ", StringComparison.Ordinal);
            if (last) { blockList.Add(sb.ToString()); sb.Length = 0; }
        }

        // ---------- pass 1: thin out duplicates ----------
        var counts = new Dictionary<string, int>();
        var kept = new List<string>();
        int removed = 0, keptInstances = 0;
        var usedIds = new HashSet<int>();
        foreach (var b in blockList)
        {
            int id = IdOf(b);
            if (id != 0) usedIds.Add(id);
            if (b.Contains("!u!1001"))
            {
                string g = GuidOf(b);
                int c; counts.TryGetValue(g, out c);
                counts[g] = c + 1;
                if (c >= maxEach) { removed++; continue; }
                keptInstances++;
            }
            kept.Add(b);
        }
        Console.WriteLine("instances kept=" + keptInstances + " removed=" + removed);

        // ---------- pass 2: add biome assets ----------
        string[][] sets =
        {
            new[]{ "Sea" },                 // unused index 0
            new[]{ "Free Island Collection" },
            new[]{ "HQ Boats" },
            new[]{ "Nature/Rocks" },
            new[]{ "Free Island Collection", "OptiWater" },
            new[]{ "Nature/Trees", "Nature/Rocks" },
            new[]{ "SimplePoly City", "Nature/Rocks" },
            new[]{ "Nature", "InnerverseInteractive" },
            new[]{ "InnerverseInteractive", "Nature/Rocks" },
            new[]{ "InnerverseInteractive", "3D Scifi Kit" },
            new[]{ "Free Island Collection", "Nature/Rocks" },
        };
        int setIdx = biome == "Sea" ? 1 : biome == "Forest" ? 5 : biome == "City" ? 6 : biome == "Desert" ? 7 : 9;

        var addedGuids = new List<string>();
        foreach (var key in sets[setIdx])
        {
            string g = Find(new[] { key });
            if (g != null && !addedGuids.Contains(g)) addedGuids.Add(g);
        }
        Console.WriteLine("biome assets: " + string.Join(", ", addedGuids.ToArray()));

        int nextId = 2000000000;
        Func<int> nid = delegate() { while (usedIds.Contains(nextId)) nextId--; return nextId--; };

        int perAsset = 26;
        var outSb = new StringBuilder();
        foreach (var l in keep) outSb.AppendLine(l);
        foreach (var b in kept) outSb.Append(b);

        int added = 0;
        foreach (var g in addedGuids)
        {
            string assetPath; idx.TryGetValue(g, out assetPath);
            for (int k = 0; k < perAsset; k++)
            {
                float px = ox + (float)((rng.NextDouble() * 2 - 1) * half);
                float pz = oz + (float)((rng.NextDouble() * 2 - 1) * half);
                float ry = (float)(rng.NextDouble() * 360.0);
                float sc = 0.85f + (float)(rng.NextDouble() * 0.5f);

                int instId = nid();
                int trId = nid();

                outSb.Append("--- !u!1001 &" + instId + "\nPrefabInstance:\n  m_ObjectHideFlags: 0\n  serializedVersion: 2\n  m_Modification:\n    serializedVersion: 3\n    m_TransformParent: {fileID: 0}\n    m_Modifications:\n    - target: {fileID: 400000, guid: " + g + ", type: 3}\n      propertyPath: m_LocalPosition.x\n      value: " + F(px) + "\n      objectReference: {fileID: 0}\n    - target: {fileID: 400000, guid: " + g + ", type: 3}\n      propertyPath: m_LocalPosition.z\n      value: " + F(pz) + "\n      objectReference: {fileID: 0}\n    - target: {fileID: 400000, guid: " + g + ", type: 3}\n      propertyPath: m_LocalRotation.y\n      value: " + F(ry) + "\n      objectReference: {fileID: 0}\n    - target: {fileID: 400000, guid: " + g + ", type: 3}\n      propertyPath: m_LocalScale.x\n      value: " + F(sc) + "\n      objectReference: {fileID: 0}\n    - target: {fileID: 400000, guid: " + g + ", type: 3}\n      propertyPath: m_LocalScale.y\n      value: " + F(sc) + "\n      objectReference: {fileID: 0}\n    - target: {fileID: 400000, guid: " + g + ", type: 3}\n      propertyPath: m_LocalScale.z\n      value: " + F(sc) + "\n      objectReference: {fileID: 0}\n    m_RemovedComponents: []\n    m_RemovedGameObjects: []\n    m_AddedGameObjects: []\n    m_AddedComponents: []\n  m_SourcePrefab: {fileID: 100100000, guid: " + g + ", type: 3}\n");
                outSb.Append("--- !u!4 &" + trId + " stripped\nTransform:\n  m_CorrespondingSourceObject: {fileID: 400000, guid: " + g + ", type: 3}\n  m_PrefabInstance: {fileID: " + instId + "}\n  m_PrefabAsset: {fileID: 0}\n");
                added++;
            }
            Console.WriteLine("  + " + perAsset + " x " + assetPath);
        }

        File.WriteAllText(path, outSb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("added instances: " + added);
        Console.WriteLine("NOTE: SceneRoots must be regenerated - run RebuildRoots next");
    }

    static string F(float v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }

    static int IdOf(string block)
    {
        int i = block.IndexOf('&');
        if (i < 0) return 0;
        int a = i + 1, e = a;
        while (e < block.Length && block[e] >= '0' && block[e] <= '9') e++;
        int v; return int.TryParse(block.Substring(a, e - a), out v) ? v : 0;
    }

    static string GuidOf(string block)
    {
        int i = block.IndexOf("m_SourcePrefab:", StringComparison.Ordinal);
        if (i < 0) return "";
        int k = block.IndexOf("guid: ", i, StringComparison.Ordinal);
        if (k < 0) return "";
        return block.Substring(k + 6, 32);
    }
}
'@
Add-Type -TypeDefinition $cs -Language CSharp
[SceneFiller]::Run($Project, $Scene, $MaxEach, $Seed)
Write-Output "DONE"
