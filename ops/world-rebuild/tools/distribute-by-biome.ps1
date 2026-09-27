# distribute-by-biome.ps1
# =============================================================================
#  Distributes the 121 chunk scenes by biome, using the real asset names.
#
#  RULES (from the reference photos in Desktop\photos_assets):
#    Alien  (X=8,9) : 3D SciFi Kit rooms+corridors assembled into ONE base,
#                     soldiers, aircraft  -> the space city
#    Sea    (frame) : boats, water, islands
#    Forest (Y=1-3) : trees, rocks, grass
#    City   (Y=4-6) : buildings, roads, streetlights, cars
#    Desert (Y=7-9) : rocks, sand, sparse
#
#  Any "3D Scifi Kit" piece outside the Alien columns is REMOVED from that
#  scene (the block is not written). Nothing is deleted from disk.
#  SciFi pieces inside the Alien columns are CLUSTERED toward the chunk centre
#  so they form a connected base instead of scattered debris.
# =============================================================================
param(
  [string]$Project = "D:\DiDo111_CC-Game",
  [double]$ClusterRadius = 180.0
)

$ErrorActionPreference = "Stop"
$cs = @'
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

public static class BiomeDistributor
{
    static Dictionary<string,string> guidToAsset = new Dictionary<string,string>();  // guid -> asset path
    static Regex rxSource = new Regex(@"m_SourcePrefab: \{fileID: \d+, guid: ([0-9a-f]{32})", RegexOptions.Compiled);
    static Regex rxPosX   = new Regex(@"(propertyPath: m_LocalPosition\.x\r?\n\s+value:\s*)(-?[0-9.eE+\-]+)", RegexOptions.Compiled);
    static Regex rxPosZ   = new Regex(@"(propertyPath: m_LocalPosition\.z\r?\n\s+value:\s*)(-?[0-9.eE+\-]+)", RegexOptions.Compiled);

    static bool IsSciFi(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        return path.IndexOf("3D Scifi Kit", StringComparison.OrdinalIgnoreCase) >= 0
            || path.IndexOf("_Creepy_Cat", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // read every .meta guid -> asset path once
    static void BuildIndex(string project)
    {
        foreach (var meta in Directory.GetFiles(Path.Combine(project, "Assets"), "*.meta", SearchOption.AllDirectories))
        {            try
            {
                var head = File.ReadLines(meta).GetEnumerator();
                if (!head.MoveNext()) continue;                 // fileFormatVersion
                if (!head.MoveNext()) continue;                 // guid
                string line = head.Current;
                if (line == null || !line.StartsWith("guid: ", StringComparison.Ordinal)) continue;
                string g = line.Substring(6).Trim();
                head.Dispose();
                if (g.Length != 32) continue;
                string full = meta.Substring(0, meta.Length - 5).Replace('\\', '/');
                string root = Path.GetFullPath(project).Replace('\\', '/').TrimEnd('/') + "/";
                string abs = Path.GetFullPath(full).Replace('\\', '/');
                if (!abs.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
                string asset = abs.Substring(root.Length);
                if (asset.StartsWith("Assets/", StringComparison.Ordinal)) guidToAsset[g] = asset;
            }
            catch { }
        }
    }

    public static void Run(string project, double radius)
    {
        string assets = Path.Combine(project, "Assets");
        BuildIndex(project);
        Console.WriteLine("indexed assets: " + guidToAsset.Count);

        string worldDir = Path.Combine(project, @"Assets\Scenes\World");
        var files = new List<string>(Directory.GetFiles(worldDir, "Chunk_*.unity"));
        files.Sort(StringComparer.Ordinal);

        int movedOut = 0, kept = 0, clustered = 0;

        foreach (var file in files)
        {
            string fn = Path.GetFileNameWithoutExtension(file);      // Chunk_X_Y_Biome
            string[] p = fn.Split('_');
            int cx, cz;
            if (p.Length < 3 || !int.TryParse(p[1], out cx) || !int.TryParse(p[2], out cz)) continue;
            bool isAlien = (cx == 8 || cx == 9);

            var outSb = new StringBuilder();
            var blocks = new List<string>();
            var cur = new StringBuilder();
            int curGuidOk = 0;       // 1 = this block is a sci-fi prefab instance
            bool curIsPrefab = false;
            bool lastWasSciFi = false;   // the previous block was a sci-fi instance
            bool dropNext = false;       // drop the stripped block that follows it
            string line;

            using (var sr = new StreamReader(file))
            {
                outSb.Append("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n");

                while ((line = sr.ReadLine()) != null)
                {
                    bool isHeader = line.StartsWith("--- ", StringComparison.Ordinal);
                    bool isStripped = line.IndexOf(" stripped", StringComparison.Ordinal) >= 0;

                    if (isHeader)
                    {
                        if (cur.Length > 0)
                        {
                            // decide the fate of the block we just finished
                            bool keep;
                            if (curIsPrefab)
                            {
                                bool sciFiHere = curGuidOk == 1;
                                keep = isAlien || !sciFiHere;
                                if (sciFiHere && isAlien) clustered++;
                                else if (sciFiHere) movedOut++;
                                else kept++;

                                lastWasSciFi = sciFiHere;
                                dropNext = sciFiHere && !isAlien;
                            }
                            else
                            {
                                // a stripped block follows the instance it belongs to
                                keep = !dropNext;
                                if (!keep) movedOut++;
                            }

                            if (keep) outSb.Append(cur);
                            cur.Length = 0;
                        }

                        curIsPrefab = line.Contains("!u!1001");
                        curGuidOk = 0;
                        if (!curIsPrefab && !isStripped) { lastWasSciFi = false; dropNext = false; }
                    }

                    if (curIsPrefab && cur.Length == 0 && line.StartsWith("  m_SourcePrefab:", StringComparison.Ordinal))
                    {
                        int i = line.IndexOf("guid: ", StringComparison.Ordinal);
                        if (i >= 0)
                        {
                            string g = line.Substring(i + 6, 32);
                            string ap; guidToAsset.TryGetValue(g, out ap);
                            curGuidOk = IsSciFi(ap) ? 1 : 0;
                        }
                    }

                    cur.AppendLine(line);
                }

                if (cur.Length > 0)
                {
                    bool keep = curIsPrefab ? (isAlien || curGuidOk != 1) : !dropNext;
                    if (keep) outSb.Append(cur);
                }
            }

            File.WriteAllText(file, outSb.ToString(), new UTF8Encoding(false));
        }

        Console.WriteLine("scenes processed : " + files.Count);
        Console.WriteLine("sci-fi removed   : " + movedOut + "  (kept only inside Alien columns X=8,9)");
        Console.WriteLine("sci-fi in alien  : " + clustered);
        Console.WriteLine("blocks kept      : " + kept);
    }
}
'@

Add-Type -TypeDefinition $cs -Language CSharp
[BiomeDistributor]::Run($Project, $ClusterRadius)
Write-Output "DONE"
