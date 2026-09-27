using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ChunkSceneManager : MonoBehaviour
{
    private Dictionary<Vector2Int, string> coordToPath = new();
    private readonly HashSet<Vector2Int> loading = new();       // ط·ظڈظ„ط¨ طھط­ظ…ظٹظ„ظ‡ط§ ظˆظ„ظ… ظٹظƒطھظ…ظ„ ط¨ط¹ط¯
    private readonly HashSet<Vector2Int> loaded = new();        // ظ…ط­ظ…ظ„ط© ظپط¹ظ„ظٹط§ظ‹ ظپظٹ ط§ظ„ظ…ط´ظ‡ط¯
    private readonly HashSet<Vector2Int> pendingUnload = new(); // ط·ظڈظ„ط¨ طھظپط±ظٹط؛ظ‡ط§ ط£ط«ظ†ط§ط، ط§ظ„طھط­ظ…ظٹظ„

    public void Initialize(List<ChunkData> allChunks)
    {
        coordToPath.Clear();
        if (allChunks != null)
            foreach (var chunk in allChunks)
                coordToPath[chunk.coord] = chunk.scenePath;
    }

    public void LoadChunk(Vector2Int coord)
    {
        if (loaded.Contains(coord) || loading.Contains(coord) || pendingUnload.Contains(coord)) return;
        if (!coordToPath.TryGetValue(coord, out var path)) return;

        var op = SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
        if (op == null)
        {
            Debug.LogError($"[ChunkSceneManager] Load failed: {path}");
            return;
        }

        loading.Add(coord);
        op.completed += _ => OnLoadDone(coord, path);
    }

    private void OnLoadDone(Vector2Int coord, string path)
    {
        loading.Remove(coord);

        if (pendingUnload.Contains(coord))
        {
            // ط·ظڈظ„ط¨ ط§ظ„طھظپط±ظٹط؛ ط£ط«ظ†ط§ط، ط§ظ„طھط­ظ…ظٹظ„ â†’ ط£ظپط±ظ‘ط؛ ظپظˆط± ط§ظƒطھظ…ط§ظ„ظ‡ (ظ„ط§ طھط³ط±ظٹط¨طŒ ظ„ط§ ظ†ط³ط® ظ…ظƒط±ط±ط©)
            pendingUnload.Remove(coord);
            var unload = SceneManager.UnloadSceneAsync(path);
            if (unload == null)
                Debug.LogError($"[ChunkSceneManager] Unload-after-load failed: {path}");
            return;
        }

        loaded.Add(coord);
    }

    public void UnloadChunk(Vector2Int coord)
    {
        if (!coordToPath.TryGetValue(coord, out var path)) return;

        bool inFlight = loading.Contains(coord);
        bool isLoaded = loaded.Contains(coord);
        if (!inFlight && !isLoaded && !pendingUnload.Contains(coord)) return;

        if (inFlight)
        {
            // ظ„ط§ ظ†ظڈط²ظگظ„ظ‡ط§ ظ…ظ† loading ط­طھظ‰ ظٹظƒطھظ…ظ„ ط§ظ„طھط­ظ…ظٹظ„ â€” ظٹظ…ظ†ط¹ طھط­ظ…ظٹظ„ظٹظ’ظ† ظ…ظƒط±ط±ظٹظ† ظ„ظ†ظپط³ ط§ظ„ظ…ط´ظ‡ط¯
            pendingUnload.Add(coord);
            return;
        }

        if (isLoaded)
        {
            loaded.Remove(coord);
            var op = SceneManager.UnloadSceneAsync(path);
            if (op == null)
                Debug.LogError($"[ChunkSceneManager] Unload failed: {path}");
        }
    }

    public bool IsChunkLoaded(Vector2Int coord) => loaded.Contains(coord);
    public List<Vector2Int> GetLoadedChunks() => new List<Vector2Int>(loaded);

    // ط§ط³ظ… ط§ظ„ظ…ط´ظ‡ط¯ (ط¨ط¯ظˆظ† ظ…ط³ط§ط±/ط§ظ…طھط¯ط§ط¯) â€” ظٹطھط·ظ„ط¨ ط¥ط¶ط§ظپط© ط§ظ„ظ…ط´ط§ظ‡ط¯ ط¥ظ„ظ‰ Build Settings
    private static string SceneName(string path) => Path.GetFileNameWithoutExtension(path);
}
