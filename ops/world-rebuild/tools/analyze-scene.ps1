# SceneStats.ps1 - streaming Unity scene analyzer (no full-file load into RAM)
# Usage: powershell -ExecutionPolicy Bypass -File SceneStats.ps1 -Scene <scene.unity> -Out <report.txt>
param(
  [Parameter(Mandatory=$true)][string]$Scene,
  [Parameter(Mandatory=$true)][string]$Out
)

$cs = @'
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

public class TInfo {
  public int GoId; public int Father; public int Children; public string Pos;
  public TInfo(int go, int fa, int ch, string p) { GoId = go; Father = fa; Children = ch; Pos = p; }
}

public static class SceneStats {
  static string nameTmp = "";

  static int ParseId(string line, string key) {
    int i = line.IndexOf(key, StringComparison.Ordinal);
    if (i < 0) return 0;
    i = line.IndexOf("fileID:", i, StringComparison.Ordinal);
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

  static string ParseGuid(string line) {
    int i = line.IndexOf("guid: ", StringComparison.Ordinal);
    if (i < 0) return null;
    i += 6; int s = i;
    while (i < line.Length && i - s < 32) i++;
    return line.Substring(s, i - s);
  }

  static string AfterColon(string line) {
    int i = line.IndexOf(": ", StringComparison.Ordinal);
    if (i < 0) return "";
    return line.Substring(i + 2).Trim();
  }

  // Resolve the true root of every transform (iterative, cycle safe)
  static void ResolveAll(List<int> ids, Dictionary<int,TInfo> trans, Dictionary<int,int> memo) {
    var path = new List<int>();
    for (int i = 0; i < ids.Count; i++) {
      int cur = ids[i];
      path.Clear();
      int guard = 0;
      while (guard++ < 200000) {
        int cached;
        if (memo.TryGetValue(cur, out cached) && cached != 0) break;
        TInfo t;
        if (!trans.TryGetValue(cur, out t) || t.Father == 0 || !trans.ContainsKey(t.Father)) { memo[cur] = cur; break; }
        path.Add(cur);
        cur = t.Father;
      }
      int rootId;
      if (!memo.TryGetValue(cur, out rootId) || rootId == 0) rootId = cur;
      for (int k = path.Count - 1; k >= 0; k--) memo[path[k]] = rootId;
    }
  }

  public static void Run(string scene, string outPath) {
    var gos = new Dictionary<int,string>();
    var trans = new Dictionary<int,TInfo>();
    var transIds = new List<int>();
    var go2trans = new Dictionary<int,int>();
    var rendPerGo = new List<int>();
    var lights = new List<int>();
    var cams = new List<int>();
    var prefabGuids = new HashSet<string>();
    var prefabParent = new Dictionary<int,int>();
    var scriptGuids = new HashSet<string>();
    var classHist = new Dictionary<int,int>();
    var nameCount = new Dictionary<string,int>();

    int curClass = -1, curId = 0, inChildren = 0;
    int childCount = 0, goId = 0, father = 0;
    string pos = "";
    long lines = 0;

    using (var sr = new StreamReader(scene, Encoding.UTF8, true, 1 << 20)) {
      string line;
      while ((line = sr.ReadLine()) != null) {
        lines++;

        if (line.Length > 6 && line[0] == '-' && line[1] == '-' && line[2] == '-') {
          if (curClass == 4 && curId != 0) { trans[curId] = new TInfo(goId, father, childCount, pos); transIds.Add(curId); }
          if (curClass == 1 && curId != 0) gos[curId] = nameTmp;
          curClass = -1; inChildren = 0; childCount = 0; goId = 0; father = 0; pos = "";
          bool isStripped = line.IndexOf("stripped", StringComparison.Ordinal) >= 0;
          int bang = line.IndexOf("!u!", StringComparison.Ordinal);
          if (bang >= 0) {
            int b = bang + 3, e = b;
            while (e < line.Length && line[e] >= '0' && line[e] <= '9') e++;
            if (e > b) { int cv; if (int.TryParse(line.Substring(b, e - b), out cv)) curClass = cv; }
          }
          if (isStripped) curClass = -2;   // stripped block: no usable data

          int amp = line.IndexOf("&", StringComparison.Ordinal);
          if (amp >= 0) {
            int a = amp + 1, z = a;
            if (z < line.Length && (line[z] == '-' || line[z] == '+')) z++;
            while (z < line.Length && line[z] >= '0' && line[z] <= '9') z++;
            if (z > a) {
              string tok = line.Substring(a, z - a);
              long lv;
              if (long.TryParse(tok, NumberStyles.Integer, CultureInfo.InvariantCulture, out lv)
                  && lv >= int.MinValue && lv <= int.MaxValue) curId = (int)lv;
              else curId = 0;
            }
          }
          if (curClass > 0) { int c; classHist.TryGetValue(curClass, out c); classHist[curClass] = c + 1; }
          continue;
        }

        if (curClass == 1) {
          if (line.StartsWith("  m_Name:", StringComparison.Ordinal)) {
            nameTmp = AfterColon(line);
            int n; nameCount.TryGetValue(nameTmp, out n); nameCount[nameTmp] = n + 1;
          } else if (line.StartsWith("  - component:", StringComparison.Ordinal)) {
            int id = ParseId(line, "component");
            if (id != 0 && !go2trans.ContainsKey(curId)) go2trans[curId] = id;
          }
        } else if (curClass == 4) {
          if (line.StartsWith("  m_GameObject:", StringComparison.Ordinal)) goId = ParseId(line, "m_GameObject");
          else if (line.StartsWith("  m_Father:", StringComparison.Ordinal)) father = ParseId(line, "m_Father");
          else if (line.StartsWith("  m_LocalPosition:", StringComparison.Ordinal)) pos = line.Substring(line.IndexOf(":", StringComparison.Ordinal) + 1).Trim();
          else if (line.StartsWith("  m_Children:", StringComparison.Ordinal)) {
            inChildren = 1;
            if (line.IndexOf("[]", StringComparison.Ordinal) >= 0) inChildren = 0;
          } else if (inChildren == 1) {
            if (line.StartsWith("  - {fileID:", StringComparison.Ordinal)) childCount++;
            else inChildren = 0;
          }
        } else if (curClass == 23) {
          int g = ParseId(line, "m_GameObject");
          if (g != 0) rendPerGo.Add(g);
        } else if (curClass == 108) {
          int g = ParseId(line, "m_GameObject");
          if (g != 0) lights.Add(g);
        } else if (curClass == 20) {
          int g = ParseId(line, "m_GameObject");
          if (g != 0) cams.Add(g);
        } else if (curClass == 1001) {
          if (line.StartsWith("  m_SourcePrefab:", StringComparison.Ordinal)) {
            string g = ParseGuid(line);
            if (g != null) prefabGuids.Add(g);
          }
          if (line.TrimStart().StartsWith("m_TransformParent:", StringComparison.Ordinal)) {
            int pid = ParseId(line, "m_TransformParent");
            if (pid != 0) prefabParent[curId] = pid;
          }
        } else if (curClass == 114) {
          if (line.StartsWith("  m_Script:", StringComparison.Ordinal)) {
            string g = ParseGuid(line);
            if (g != null) scriptGuids.Add(g);
          }
        }
      }
      if (curClass == 4 && curId != 0) { trans[curId] = new TInfo(goId, father, childCount, pos); transIds.Add(curId); }
      if (curClass == 1 && curId != 0) gos[curId] = nameTmp;
    }

    var goRoot = new Dictionary<int,int>();
    foreach (var kv in go2trans) if (trans.ContainsKey(kv.Value)) goRoot[kv.Key] = kv.Value;

    var memo = new Dictionary<int,int>();
    foreach (int id in transIds) memo[id] = 0;
    ResolveAll(transIds, trans, memo);

    var roots = new List<int>();
    foreach (var kv in trans) if (kv.Value.Father == 0) roots.Add(kv.Key);

    var descCount = new Dictionary<int,int>();
    foreach (var kv in trans) {
      int r = memo[kv.Key];
      int c; descCount.TryGetValue(r, out c); descCount[r] = c + 1;
    }
    var rendByRoot = new Dictionary<int,int>();
    foreach (int g in rendPerGo) {
      int tr; if (!goRoot.TryGetValue(g, out tr)) continue;
      int r = memo[tr];
      int c; rendByRoot.TryGetValue(r, out c); rendByRoot[r] = c + 1;
    }
    var lightByRoot = new Dictionary<int,int>();
    foreach (int g in lights) {
      int tr; if (!goRoot.TryGetValue(g, out tr)) continue;
      int r = memo[tr];
      int c; lightByRoot.TryGetValue(r, out c); lightByRoot[r] = c + 1;
    }
    var camByRoot = new Dictionary<int,int>();
    foreach (int g in cams) {
      int tr; if (!goRoot.TryGetValue(g, out tr)) continue;
      int r = memo[tr];
      int c; camByRoot.TryGetValue(r, out c); camByRoot[r] = c + 1;
    }

    // depth-2: direct children of every root
    var kids = new Dictionary<int,List<int>>();
    foreach (var kv in trans) {
      if (kv.Value.Father == 0) continue;
      List<int> l;
      if (!kids.TryGetValue(kv.Value.Father, out l)) { l = new List<int>(); kids[kv.Value.Father] = l; }
      l.Add(kv.Key);
    }
    var subCount = new Dictionary<int,int>();
    foreach (var kv in trans) {
      if (kv.Value.Father == 0) continue;
      int p = kv.Value.Father;
      int c; subCount.TryGetValue(p, out c); subCount[p] = c + 1;
    }
    var rendByChild = new Dictionary<int,int>();
    foreach (int g in rendPerGo) {
      int tr; if (!goRoot.TryGetValue(g, out tr)) continue;
      int p = trans[tr].Father;
      if (p == 0) continue;
      int c; rendByChild.TryGetValue(p, out c); rendByChild[p] = c + 1;
    }

    var prefabByParent = new Dictionary<int,int>();
    foreach (var kv in prefabParent) {
      int p = kv.Value;                 // parent transform id of the instance (the chunk)
      if (p == 0 || !trans.ContainsKey(p)) continue;
      int c; prefabByParent.TryGetValue(p, out c); prefabByParent[p] = c + 1;
    }
    int totalPrefabs = prefabParent.Count;
    var prefabByRoot = new Dictionary<int,int>();
    foreach (var kv in prefabByParent) {
      int r = memo[kv.Key];
      int c; prefabByRoot.TryGetValue(r, out c);
      prefabByRoot[r] = c + kv.Value;
    }

    using (var w = new StreamWriter(outPath, false, Encoding.UTF8)) {
      w.WriteLine("FILE: " + scene);
      w.WriteLine("SIZE_MB: " + (new FileInfo(scene).Length / 1048576.0).ToString("F2"));
      w.WriteLine("LINES: " + lines);
      w.WriteLine("GameObjects: " + gos.Count);
      w.WriteLine("Transforms: " + trans.Count);
      w.WriteLine("Roots: " + roots.Count);
      w.WriteLine("MeshRenderers: " + rendPerGo.Count);
      w.WriteLine("Lights: " + lights.Count);
      w.WriteLine("Cameras: " + cams.Count);
      w.WriteLine("UniquePrefabGuids: " + prefabGuids.Count);
      w.WriteLine("UniqueScriptGuids: " + scriptGuids.Count);
      w.WriteLine("PrefabInstances(total): " + totalPrefabs);
      w.WriteLine();
      w.WriteLine("=== CLASS HISTOGRAM ===");
      var keys = new List<int>(classHist.Keys); keys.Sort();
      foreach (int k in keys) w.WriteLine("  u!" + k + " : " + classHist[k]);
      w.WriteLine();
      w.WriteLine("=== ROOTS (name | objects | renderers | lights | cameras | localPos) ===");
      var rows = new List<string>();
      foreach (int r in roots) {
        TInfo t = trans[r];
        string nm = "?"; string gnm;
        if (t.GoId != 0 && gos.TryGetValue(t.GoId, out gnm)) nm = gnm;
        int dc; descCount.TryGetValue(r, out dc);
        int rc; rendByRoot.TryGetValue(r, out rc);
        int lc; lightByRoot.TryGetValue(r, out lc);
        int cc; camByRoot.TryGetValue(r, out cc);
        int pc; prefabByRoot.TryGetValue(r, out pc);
        rows.Add(string.Format("{0,-30} | objs={1,6} | prefabs={2,7} | rend={3,5} | lights={4,3} | cams={5,3} | {6}", nm, dc, pc, rc, lc, cc, t.Pos));
      }
      rows.Sort(); rows.Reverse();
      foreach (string s in rows) w.WriteLine("  " + s);
      w.WriteLine();
      w.WriteLine("=== ROOT > DIRECT CHILDREN (parent | child | directKids | renderers | localPos) ===");
      var rnames = new Dictionary<int,string>();
      foreach (int r in roots) {
        TInfo t = trans[r];
        string pn = "?"; string g2;
        if (t.GoId != 0 && gos.TryGetValue(t.GoId, out g2)) pn = g2;
        List<int> l;
        if (!kids.TryGetValue(r, out l)) continue;
        w.WriteLine("  [" + pn + "] children=" + l.Count);
        var sub = new List<string>();
        foreach (int k in l) {
          TInfo kt = trans[k];
          string kn = "?"; string g3;
          if (kt.GoId != 0 && gos.TryGetValue(kt.GoId, out g3)) kn = g3;
          int kc; subCount.TryGetValue(k, out kc);
          int kr; rendByChild.TryGetValue(k, out kr);
          int kp; prefabByParent.TryGetValue(k, out kp);
          sub.Add(string.Format("      {0,-24} prefabs={1,6} kids={2,4} rend={3,5} pos={4}", kn, kp, kc, kr, kt.Pos));
        }
        sub.Sort();
        foreach (string s in sub) w.WriteLine(s);
      }
      w.WriteLine();
      w.WriteLine("=== TOP 40 NAMES ===");
      var nk = new List<KeyValuePair<string,int>>(nameCount);
      nk.Sort(delegate(KeyValuePair<string,int> a, KeyValuePair<string,int> b) { return b.Value.CompareTo(a.Value); });
      for (int i = 0; i < nk.Count && i < 40; i++) w.WriteLine("  " + nk[i].Key + " : " + nk[i].Value);
      w.WriteLine();
      w.WriteLine("=== PREFAB GUIDS ===");
      foreach (string g in prefabGuids) w.WriteLine("  " + g);
      w.WriteLine();
      w.WriteLine("=== SCRIPT GUIDS ===");
      foreach (string g in scriptGuids) w.WriteLine("  " + g);
    }
  }
}
'@

Add-Type -TypeDefinition $cs -Language CSharp
[SceneStats]::Run($Scene, $Out)
Write-Output "DONE -> $Out"
