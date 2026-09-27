using UnityEngine;

public enum WorldBiome
{
    City,
    Forest,
    Desert,
    Sea,
    Alien
}

public class WorldChunk : MonoBehaviour
{
    public int gridX;
    public int gridZ;
    public WorldBiome biome;
    public Vector3 center;
    public float radius;
    public int objectCount;
}
