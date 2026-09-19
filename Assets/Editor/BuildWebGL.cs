using UnityEditor;
using UnityEditor.Build.Reporting;
using System.Linq;

// Build WebGL from CLI / GameCI:
// Unity -batchmode -nographics -quit -projectPath . -executeMethod WebGLBuilder.Build -logFile -
public static class WebGLBuilder
{
    [MenuItem("DODA/Build WebGL")]
    public static void Build()
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();
        if (scenes.Length == 0)
            scenes = new string[] { "Assets/Scenes/SampleScene.unity" };

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Builds/WebGL",
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new System.Exception("WebGL build failed: " + report.summary.result);
    }
}
