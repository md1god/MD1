using System.Collections.Generic;
using UnityEngine;

// WorldManager: ظٹط±ط¨ط· ط§ظ„ظ…ظƒظˆظ†ط§طھ ط§ظ„ط£ط±ط¨ط¹ط© â€” ط¨ط§ط±ط³ظٹط± ط§ظ„ظ…ط§ظ†ظٹظپط³طھ + ظ…طھطھط¨ط¹ ط§ظ„ظ„ط§ط¹ط¨ + ظ…ط¯ظٹط± ط§ظ„ظ…ط´ط§ظ‡ط¯ + ط§ظ„ظ…ط­ظ…ظ‘ظ„
// ط§ظ„طھط±ظƒظٹط¨: ط¹ظ„ظ‰ GameObject ظˆط§ط­ط¯ ظپظٹ ط§ظ„ظ…ط´ظ‡ط¯ ط§ظ„ط±ط¦ظٹط³ظٹ â€” ط£ظˆ ظٹظڈظ†ط´ط£ طھظ„ظ‚ط§ط¦ظٹط§ظ‹ ط¹ظ†ط¯ طھط´ط؛ظٹظ„ ط£ظٹ ظ…ط´ظ‡ط¯
public class WorldManager : MonoBehaviour
{
    [SerializeField] private Transform playerOrCamera;
    [SerializeField] private int chunkSize = 5000;
    [SerializeField] private int loadRadius = 5000;   // = 1 -> loads the player row +/-1 (seamless)

    private ChunkSceneManager sceneManager;
    private PlayerChunkTracker tracker;
    private ChunkStreamer streamer;

    void Start()
    {
        var chunks = WorldManifestParser.Parse();
        if (chunks.Count == 0)
            Debug.LogError("[WorldManager] ط§ظ„ظ…ط§ظ†ظٹظپط³طھ ظپط§ط±ط؛ ط£ظˆ ظ„ظ… ظٹظڈظ‚ط±ط£");

        sceneManager = Find<ChunkSceneManager>();
        sceneManager.Initialize(chunks);

        if (!playerOrCamera)
        {
            var player = GameObject.FindGameObjectWithTag("Player"); playerOrCamera = player ? player.transform : (Camera.main ? Camera.main.transform : null);
            if (!playerOrCamera)
                Debug.LogWarning("[WorldManager] playerOrCamera ط؛ظٹط± ظ…ط­ط¯ط¯ â€” ط§ط±ط¨ط· ط§ظ„ظƒط§ظ…ظٹط±ط§ ظپظٹ ط§ظ„ظ€ Inspector");
        }

        tracker = Find<PlayerChunkTracker>();
        tracker.Configure(playerOrCamera, chunkSize, loadRadius, ComputeOrigin(chunks));

        streamer = Find<ChunkStreamer>();
        streamer.Configure(tracker, sceneManager); sceneManager.LoadChunk(new Vector2Int(0, 5));  // row of the spawn point (city core)
        // ظ„ط§ ظ†ط³طھط¯ط¹ظٹ Update ظٹط¯ظˆظٹط§ظ‹ â€” ط§ظ„ظ€ MonoBehaviour ظٹط´طھط؛ظ„ طھظ„ظ‚ط§ط¦ظٹط§ظ‹ ظƒظ„ ط¥ط·ط§ط±
    }

    // ط£طµظ„ ط§ظ„ط´ط¨ظƒط© = ظ…ظˆظ‚ط¹ ط§ظ„طھط´ط§ظ†ظƒ (0,0) ظ…ظ† ط§ظ„ظ…ط§ظ†ظٹظپط³طھ (ط­ط§ظ„ظٹط§ظ‹ -5000,-5000)
    private static Vector2 ComputeOrigin(List<ChunkData> chunks)
    {
        foreach (var c in chunks)
            if (c.coord == Vector2Int.zero)
                return c.worldPos;
        return new Vector2(-5000f, -5000f);
    }

    // ظٹط¬ظ„ط¨ ظ…ظƒظˆظ‘ظ†ط§ظ‹ ظ…ظˆط¬ظˆط¯ط§ظ‹ ظپظٹ ط§ظ„ظ…ط´ظ‡ط¯ ط£ظˆ ظٹظ†ط´ط¦ظ‡ ط¹ظ„ظ‰ ظ†ظپط³ ط§ظ„ظ€ GameObject
    T Find<T>() where T : Component
    {
        var existing = FindFirstObjectByType<T>();
        return existing ? existing : gameObject.AddComponent<T>();
    }

    // ظٹط¶ظ…ظ† طھط´ط؛ظٹظ„ ط§ظ„ظ†ط¸ط§ظ… ظپظٹ ط£ظٹ ظ…ط´ظ‡ط¯ ط¨ط¯ظˆظ† ط¥ط¹ط¯ط§ط¯ ظٹط¯ظˆظٹ (ط¥ظ† ظ„ظ… ظٹظˆط¬ط¯ WorldManager ظپظٹ ط§ظ„ظ…ط´ظ‡ط¯)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoBootstrap()
    {
        if (FindFirstObjectByType<WorldManager>() == null)
            new GameObject("[WorldManager]").AddComponent<WorldManager>();
    }
}

