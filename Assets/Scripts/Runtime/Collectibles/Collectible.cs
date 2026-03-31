using UnityEngine;

namespace DashAndCollect
{
    /// <summary>
    /// Tags a GameObject as a collectible and carries its type.
    /// Detected by CollisionHandler via GetComponent&lt;Collectible&gt;() (TDD §4.4).
    /// Attach to any collectible prefab child that owns the BoxCollider2D trigger.
    /// </summary>
    public sealed class Collectible : MonoBehaviour
    {
        public CollectibleType Type;
    }
}
