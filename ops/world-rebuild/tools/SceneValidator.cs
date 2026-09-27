using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// SceneValidator - يفتح كل مشهد في العالم/Core ويفحصه:
///   عدد الجذور، التحويلات، الرندرات، الأضواء، الكاميرات، السكربتات المفقودة.
///
/// Menu:  Tools > Validate All Scenes
/// Batch: Unity.exe -batchmode -quit -projectPath .. -executeMethod SceneValidator.Run
/// </summary>
public static class SceneValidator
{
    public static void Run()
    {
        var paths = new List<string> { "Assets/Scenes/Core/Core.unity" };

        foreach (var g in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes/World" }))
            paths.Add(AssetDatabase.GUIDToAssetPath(g));

        paths.Sort();
        Debug.Log("[SceneValidator] scenes to validate: " + paths.Count);

        int totalMissing = 0;

        foreach (var p in paths)
        {
            var sw = Stopwatch.StartNew();

            var scene = EditorSceneManager.OpenScene(p, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();

            int transforms = 0, renderers = 0, lights = 0, cams = 0, missing = 0, objects = 0;

            foreach (var r in roots)
            {
                var all = r.GetComponentsInChildren<Transform>(true);
                transforms += all.Length;
                objects += all.Length;
                renderers += r.GetComponentsInChildren<MeshRenderer>(true).Length;
                lights += r.GetComponentsInChildren<Light>(true).Length;
                cams += r.GetComponentsInChildren<Camera>(true).Length;

                foreach (var c in r.GetComponentsInChildren<Component>(true))
                    if (c == null) missing++;
            }

            totalMissing += missing;
            sw.Stop();

            Debug.Log(string.Format(
                "[SceneValidator] {0} | roots={1} objects={2} renderers={3} lights={4} cameras={5} MISSING_SCRIPTS={6} | {7:F1}s | mem={8}MB",
                p, roots.Length, objects, renderers, lights, cams, missing,
                sw.Elapsed.TotalSeconds,
                (System.GC.GetTotalMemory(false) / (1024 * 1024))));

            // free memory before the next scene
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Resources.UnloadUnusedAssets();
            System.GC.Collect();
        }

        Debug.Log("[SceneValidator] ALL DONE. total missing scripts = " + totalMissing);
    }
}
