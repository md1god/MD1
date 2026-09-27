using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public struct ChunkData
{
    public string name;
    public string scenePath;
    public Vector2Int coord;
    public Vector2 worldPos;
    public int gridSize;
    public int chunkSize;
}

public static class WorldManifestParser
{
    public static List<ChunkData> Parse(string path = "Assets/_Game/World/WorldManifest.txt")
    {
        var result = new List<ChunkData>();

        var lines = LoadLines(path);
        if (lines == null) return result;

        int gridSize = 3, chunkSize = 5000;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;

            if (trimmed.StartsWith("gridSize="))
            {
                var eq = trimmed.IndexOf('=');
                if (eq > 0) int.TryParse(trimmed.Substring(eq + 1), out gridSize);
            }
            else if (trimmed.StartsWith("chunkSize="))
            {
                var eq = trimmed.IndexOf('=');
                if (eq > 0) int.TryParse(trimmed.Substring(eq + 1), out chunkSize);
            }
            else if (trimmed.Contains('|'))
            {
                var parts = trimmed.Split('|');
                if (parts.Length < 4) continue;

                var coords = parts[0].Split('_');
                if (coords.Length < 3) continue;
                if (!int.TryParse(coords[1], out int cx)) continue;
                if (!int.TryParse(coords[2], out int cz)) continue;
                if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float wx)) continue;
                if (!float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float wz)) continue;

                result.Add(new ChunkData
                {
                    name = parts[0],
                    scenePath = parts[1],
                    coord = new Vector2Int(cx, cz),
                    worldPos = new Vector2(wx, wz),
                    gridSize = gridSize,
                    chunkSize = chunkSize
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Loads manifest lines in a platform-safe way:
    /// 1) Resources.Load&lt;TextAsset&gt; — works on ALL platforms including WebGL
    ///    (System.IO file access is unavailable in the browser).
    /// 2) File-based fallback kept for Editor/desktop workflows.
    /// </summary>
    private static string[] LoadLines(string path)
    {
        var ta = Resources.Load<TextAsset>("WorldManifest");
        if (ta != null)
            return ta.text.Split('\n');

        // Editor / desktop fallback (WebGL has no real System.IO file access).
        if (!File.Exists(path))
        {
            string streamingPath = Path.Combine(Application.streamingAssetsPath, "WorldManifest.txt");
            if (File.Exists(streamingPath))
                path = streamingPath;
            else
            {
                Debug.LogError($"[WorldManifestParser] Manifest not found (Resources.Load and file '{path}'). Copy WorldManifest.txt into a Resources folder.");
                return null;
            }
        }

        return File.ReadAllLines(path);
    }
}
