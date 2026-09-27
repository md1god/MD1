using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.IO;

public class SceneScreenshotTaker
{
    [MenuItem("Tools/Screenshot Taker/Capture All")]
    static void CaptureAll()
    {
        string[] scenePaths = new string[] {
            "Assets/Scenes/SampleScene.unity",
"Assets/Scenes/Core/Core.unity",
"Assets/Scenes/World/Scene_01_Sea_South.unity",
"Assets/Scenes/World/Scene_02_Forest_South.unity",
"Assets/Scenes/World/Scene_03_Forest_Core.unity",
"Assets/Scenes/World/Scene_04_Forest_North.unity",
"Assets/Scenes/World/Scene_05_City_South.unity",
"Assets/Scenes/World/Scene_06_City_Core.unity",
"Assets/Scenes/World/Scene_07_City_North.unity",
"Assets/Scenes/World/Scene_08_Desert_South.unity",
"Assets/Scenes/World/Scene_09_Sumer_Temple.unity",
"Assets/Scenes/World/Scene_10_Giza_Desert.unity",
"Assets/Scenes/World/Scene_11_Sea_North.unity"
        };

        for (int i = 0; i < scenePaths.Length; i++)
        {
            EditorSceneManager.OpenScene(scenePaths[i], OpenSceneMode.Single);
            SceneManager.SetActiveScene(SceneManager.GetActiveScene());
            
            // Create a camera for screenshot
            GameObject camObj = new GameObject("ScreenshotCamera");
            Camera cam = camObj.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.transform.position = new Vector3(0, 500, 0);
            cam.transform.rotation = Quaternion.Euler(90, 0, 0);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 2000f;
            cam.fieldOfView = 60f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.gray;
            
            // Render to texture
            RenderTexture rt = new RenderTexture(1024, 1024, 24);
            cam.targetTexture = rt;
            cam.Render();
            
            // Read pixels
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            
            byte[] pngData = tex.EncodeToPNG();
            string path = "D:/DiDo111_CC-Game/.opencode/reports/scene_" + (i + 1) + "_done.png";
            File.WriteAllBytes(path, pngData);
            Debug.Log($"Screenshot saved: {path} ({pngData.Length} bytes)");
            
            Object.DestroyImmediate(camObj);
            RenderTexture.DestroyImmediate(rt);
        }
        
        Debug.Log("ALL SCREENSHOTS COMPLETE!");
        EditorApplication.Exit(0);
    }
}
