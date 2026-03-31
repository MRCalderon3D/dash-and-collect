using UnityEngine;

namespace DashAndCollect
{
    /// <summary>
    /// Authored tuning data for PlayerController (TDD §4.2).
    /// Read-only at runtime — no mutable state (TDD §6.1).
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerConfig", menuName = "DashAndCollect/PlayerConfig")]
    public sealed class PlayerConfig : ScriptableObject
    {
        [Tooltip("Duration of the lateral lerp to the target lane (seconds).")]
        [Min(0.001f)] public float dashDuration = 0.08f;

        [Tooltip("Input is blocked for this long after a dash completes (seconds).")]
        [Min(0f)] public float recoveryDuration = 0.05f;

        [Tooltip("Lane world-space X positions.")]
        public LaneConfig laneConfig;

        private void OnValidate()
        {
            if (laneConfig == null)
                Debug.LogError("[PlayerConfig] laneConfig must be assigned.", this);
            if (recoveryDuration > dashDuration)
                Debug.LogWarning(
                    "[PlayerConfig] recoveryDuration > dashDuration: a queued dash can fire " +
                    "while the previous lerp is still in progress. This is functional but " +
                    "may look abrupt. Consider recoveryDuration ≤ dashDuration.", this);
        }
    }
}
