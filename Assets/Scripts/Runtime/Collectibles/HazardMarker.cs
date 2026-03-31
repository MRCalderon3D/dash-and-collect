using UnityEngine;

namespace DashAndCollect
{
    /// <summary>
    /// Marks a child GameObject within a chunk prefab as a hazard in a specific lane.
    /// Used by SpawnManager's safety pass (TDD §4.3) and by CollisionHandler (TDD §4.4).
    /// LaneIndex: 0 = left, 1 = centre, 2 = right.
    /// </summary>
    public sealed class HazardMarker : MonoBehaviour
    {
        [Range(0, 2)] public int LaneIndex = 1;
    }
}
