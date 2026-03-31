using UnityEngine;

namespace DashAndCollect
{
    /// <summary>
    /// Authored configuration for the spawn system and world speed escalation (TDD §4.3, §6.1).
    /// Read-only at runtime — no mutable state.
    /// </summary>
    [CreateAssetMenu(fileName = "SpawnConfig", menuName = "DashAndCollect/SpawnConfig")]
    public sealed class SpawnConfig : ScriptableObject
    {
        [Header("World Speed")]
        [Tooltip("Starting world scroll speed in units/second.")]
        [Min(0.01f)] public float initialSpeed = 5f;

        [Tooltip("Speed added every 250 m of distance.")]
        [Min(0f)] public float speedIncrement = 0.5f;

        [Tooltip("Hard upper limit on world speed.")]
        [Min(0.01f)] public float maxSpeed = 20f;

        [Header("Chunk Pool")]
        [Tooltip("All chunk definitions available to the spawner. Eligibility filtered at runtime.")]
        public ChunkDefinition[] chunkPool = System.Array.Empty<ChunkDefinition>();

        [Tooltip("How far ahead of the camera top edge the spawner looks to decide whether to spawn.")]
        [Min(1f)] public float lookAheadDistance = 15f;

        [Tooltip("How far below the camera bottom edge before a chunk is recycled.")]
        [Min(0f)] public float recycleBuffer = 2f;

        [Tooltip("Number of instances pre-warmed per unique ChunkDefinition at run start.")]
        [Min(1)] public int poolSizePerChunk = 3;
    }
}
