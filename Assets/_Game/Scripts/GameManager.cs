using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// GameManager: Central game state, UI management, and system coordination.
/// Works with Chunk Streaming system. WebGL-compatible.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game State")]
    public bool isPaused = false;
    public bool isGameplayActive = true;

    [Header("UI References")]
    public GameObject pauseMenu;
    public GameObject hudCanvas;
    public Text chunkInfoText;
    public Text fpsText;
    public Slider healthSlider;
    public Text healthText;

    [Header("Player")]
    public Transform playerTransform;
    public SimplePlayer playerController;

    [Header("Chunk Streaming")]
    public WorldManager worldManager;
    public PlayerChunkTracker chunkTracker;

    // Internal state
    float _uiUpdateTimer;
    float _fpsAccumulator;
    int _frameCount;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Auto-find refs if not set
        if (!playerTransform) playerTransform = GameObject.FindWithTag("Player")?.transform;
        if (!playerController) playerController = playerTransform?.GetComponent<SimplePlayer>();
        if (!worldManager) worldManager = FindFirstObjectByType<WorldManager>();
        if (!chunkTracker) chunkTracker = FindFirstObjectByType<PlayerChunkTracker>();

        // Setup UI
        if (hudCanvas) hudCanvas.SetActive(true);
        if (pauseMenu) pauseMenu.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        HandleInput();
        UpdateUI();
    }

    void HandleInput()
    {
        if (Keyboard.current == null) return;

        // Pause toggle
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TogglePause();
        }

        // Debug: Print chunk info
        if (Keyboard.current.f1Key.wasPressedThisFrame)
        {
            PrintChunkDebug();
        }
    }

    void UpdateUI()
    {
        _uiUpdateTimer += Time.deltaTime;
        _fpsAccumulator += Time.deltaTime;
        _frameCount++;

        if (_uiUpdateTimer >= 0.5f)
        {
            _uiUpdateTimer = 0f;

            // FPS
            float fps = _frameCount / _fpsAccumulator;
            if (fpsText) fpsText.text = $"FPS: {Mathf.RoundToInt(fps)}";

            // Chunk info
            if (chunkInfoText && chunkTracker)
            {
                var active = chunkTracker.GetActiveChunkCoords();
                if (active != null && active.Count > 0)
                {
                    chunkInfoText.text = $"Chunk: ({active[0].x}, {active[0].y})";
                }
            }

            _fpsAccumulator = 0f;
            _frameCount = 0;
        }
    }

    public void TogglePause()
    {
        isPaused = !isPaused;
        Time.timeScale = isPaused ? 0f : 1f;

        if (pauseMenu) pauseMenu.SetActive(isPaused);
        if (hudCanvas) hudCanvas.SetActive(!isPaused);

        Cursor.lockState = isPaused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isPaused;

        if (playerController) playerController.enabled = !isPaused;
    }

    public void QuitGame()
    {
#if UNITY_WEBGL
        // WebGL: can't quit, just reload
        Application.ExternalCall("window.location.reload");
#else
        Application.Quit();
#endif
    }

    void PrintChunkDebug()
    {
        if (!chunkTracker) return;
        var active = chunkTracker.GetActiveChunkCoords();
        var sceneManager = FindFirstObjectByType<ChunkSceneManager>();
        var loaded = sceneManager?.GetLoadedChunks() ?? new List<Vector2Int>();
        Debug.Log($"[GameManager] Active: {FormatCoords(active)} | Loaded: {FormatCoords(loaded)}");
    }

    string FormatCoords(List<Vector2Int> coords)
    {
        if (coords == null || coords.Count == 0) return "none";
        var sb = new System.Text.StringBuilder();
        foreach (var c in coords) sb.Append($"({c.x},{c.y}) ");
        return sb.ToString().Trim();
    }

    // Static helpers for other scripts
    public static void SetGameplayActive(bool active)
    {
        if (Instance) Instance.isGameplayActive = active;
    }

    public static void ShowMessage(string msg, float duration = 3f)
    {
        if (Instance && Instance.hudCanvas)
        {
            // Simple toast implementation
            var toast = new GameObject("Toast");
            toast.transform.SetParent(Instance.hudCanvas.transform, false);
            var text = toast.AddComponent<Text>();
            text.font = Font.CreateDynamicFontFromOSFont("Arial", 28);
            text.text = msg;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            text.rectTransform.anchoredPosition = Vector2.zero;
            var outline = toast.AddComponent<Outline>();
            outline.effectColor = Color.black;
            Destroy(toast, duration);
        }
    }
}