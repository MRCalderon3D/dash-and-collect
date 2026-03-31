using UnityEngine;

namespace DashAndCollect
{
    /// <summary>
    /// Authored data for a single spawnable chunk (TDD §4.3).
    /// A chunk is a prefab containing a pre-authored pattern of hazards and collectibles
    /// in a fixed-height strip. Read-only at runtime (TDD §6.1).
    /// </summary>
    [CreateAssetMenu(fileName = "ChunkDefinition", menuName = "DashAndCollect/ChunkDefinition")]
    public sealed class ChunkDefinition : ScriptableObject
    {
        [Tooltip("The chunk prefab. Must contain lane-indexed hazard/collectible children.")]
        public GameObject prefab;

        [Tooltip("Height of this chunk in world units. Spawn cursor advances by this amount.")]
        [Min(0.1f)] public float height = 5f;

        [Tooltip("This chunk is not eligible until the player has travelled this many metres.")]
        [Min(0)] public int minDistanceMilestone = 0;

        [Tooltip("Tags controlling modifier-driven selection bias.")]
        public ChunkTag[] tags = System.Array.Empty<ChunkTag>();

        /// <summary>Returns true if this chunk carries <paramref name="tag"/>.</summary>
        public bool HasTag(ChunkTag tag)
        {
            foreach (var t in tags)
                if (t == tag) return true;
            return false;
        }

        private void OnValidate()
        {
            if (prefab == null)
                Debug.LogWarning($"[ChunkDefinition] '{name}' has no prefab assigned.", this);
            if (height <= 0f)
                Debug.LogError($"[ChunkDefinition] '{name}' height must be > 0.", this);
        }
    }
}
