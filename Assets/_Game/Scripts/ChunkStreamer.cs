using System.Collections.Generic;
using UnityEngine;

public class ChunkStreamer : MonoBehaviour
{
    [SerializeField] private PlayerChunkTracker tracker;
    [SerializeField] private ChunkSceneManager sceneManager;

    private readonly HashSet<Vector2Int> loadedChunks = new HashSet<Vector2Int>();
    private readonly List<Vector2Int> unloadBuffer = new List<Vector2Int>();

    public void Configure(PlayerChunkTracker t, ChunkSceneManager s)
    {
        tracker = t;
        sceneManager = s;
    }

    void Update()
    {
        if (tracker == null || sceneManager == null) return;

        var active = tracker.GetActiveChunkCoords();
        if (active == null) return;

        foreach (var coord in active)
            if (loadedChunks.Add(coord))
                sceneManager.LoadChunk(coord);

        unloadBuffer.Clear();

        foreach (var coord in loadedChunks)
            if (!active.Contains(coord))
                unloadBuffer.Add(coord);

        foreach (var coord in unloadBuffer)
        {
            sceneManager.UnloadChunk(coord);
            loadedChunks.Remove(coord);
        }
    }
}
