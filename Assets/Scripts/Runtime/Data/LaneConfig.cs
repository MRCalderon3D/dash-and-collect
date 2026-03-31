using UnityEngine;

namespace DashAndCollect
{
    /// <summary>
    /// World-space X positions for the three discrete lanes.
    /// Index 0 = left, 1 = center (spawn), 2 = right (TDD §4.2).
    /// Read-only at runtime — no mutable state (TDD §6.1).
    /// </summary>
    [CreateAssetMenu(fileName = "LaneConfig", menuName = "DashAndCollect/LaneConfig")]
    public sealed class LaneConfig : ScriptableObject
    {
        [Tooltip("World-space X positions for lanes 0 (left), 1 (center), 2 (right).")]
        public float[] lanePositions = { -2f, 0f, 2f };

        private void OnValidate()
        {
            if (lanePositions == null || lanePositions.Length != 3)
                Debug.LogError($"[LaneConfig] lanePositions must have exactly 3 entries.", this);
        }
    }
}
