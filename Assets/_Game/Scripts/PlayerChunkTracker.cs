using System.Collections.Generic;
using UnityEngine;

public class PlayerChunkTracker : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private int chunkSize = 5000;
    [SerializeField] private int loadRadius = 1000;
    [SerializeField] private Vector2 worldOrigin = new Vector2(-5000f, -5000f);

    // يسمح لـ WorldManager بضبط الإعدادات بعد الإنشاء البرمجي — الأصل يُشتق من المانيفست
    public void Configure(Transform t, int cs, int lr, Vector2 origin)
    {
        target = t;
        chunkSize = cs;
        loadRadius = lr;
        worldOrigin = origin;
    }

    public List<Vector2Int> GetActiveChunkCoords()
    {
        if (!target || chunkSize <= 0) return new List<Vector2Int>();

        var pos = target.position;
        var center = new Vector2Int(
            Mathf.FloorToInt((pos.x - worldOrigin.x) / chunkSize),
            Mathf.FloorToInt((pos.z - worldOrigin.y) / chunkSize));

        // Floor (وليس Ceil): loadRadius < chunkSize → التشانك الحالي فقط (حسب معيار النجاح)
        // مثال: loadRadius=1000 → 0 → تشانك واحد | loadRadius=5000 → 1 → 3×3 = 9 تشانكس
        int radiusInChunks = Mathf.FloorToInt(loadRadius / (float)chunkSize);

        var result = new List<Vector2Int>();
        for (int x = -radiusInChunks; x <= radiusInChunks; x++)
            for (int z = -radiusInChunks; z <= radiusInChunks; z++)
                result.Add(center + new Vector2Int(x, z));

        return result;
    }
}