using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

// StreamingDebugUI: On-screen diagnostics for WebGL testing.
// Shows FPS, player position, active chunk, loaded chunks.
// Auto-creates Canvas if missing. Works in WebGL (no Resources font).
public class StreamingDebugUI : MonoBehaviour
{
    Text label;
    float timer;
    readonly StringBuilder sb = new StringBuilder(512);

    void Awake()
    {
        var canvas = FindFirstObjectByType<Canvas>();
        if (!canvas)
        {
            var go = new GameObject("DebugCanvas");
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            go.AddComponent<GraphicRaycaster>();
        }

        var textGO = new GameObject("DebugText");
        textGO.transform.SetParent(canvas.transform, false);
        label = textGO.AddComponent<Text>();
        // WebGL-safe: use default font (assigned at runtime)
        label.font = Font.CreateDynamicFontFromOSFont("Arial", 26);
        label.fontSize = 26;
        label.color = Color.white;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;

        var rt = label.rectTransform;
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(16, -16);

        var outline = textGO.AddComponent<Outline>();
        outline.effectColor = new Color(0, 0, 0, 0.8f);
        outline.effectDistance = new Vector2(2, -2);
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer < 0.25f) return;
        timer = 0f;

        var tracker = FindFirstObjectByType<PlayerChunkTracker>();
        var sceneManager = FindFirstObjectByType<ChunkSceneManager>();

        sb.Clear();
        sb.AppendLine($"FPS: {Mathf.RoundToInt(1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f))}");
        if (Camera.main)
        {
            var p = Camera.main.transform.position;
            sb.AppendLine($"Player: ({p.x:F0}, {p.z:F0})");
        }

        if (tracker)
        {
            var active = tracker.GetActiveChunkCoords();
            sb.Append("Active: ");
            AppendCoords(active);
            sb.AppendLine();
        }

        if (sceneManager)
        {
            var loaded = sceneManager.GetLoadedChunks();
            sb.Append($"Loaded ({loaded.Count}): ");
            AppendCoords(loaded);
        }
        else
        {
            sb.Append("Loading world…");
        }

        label.text = sb.ToString();
    }

    void AppendCoords(List<Vector2Int> coords)
    {
        if (coords == null || coords.Count == 0) { sb.Append("—"); return; }
        for (int i = 0; i < coords.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append('(').Append(coords[i].x).Append(',').Append(coords[i].y).Append(')');
        }
    }
}