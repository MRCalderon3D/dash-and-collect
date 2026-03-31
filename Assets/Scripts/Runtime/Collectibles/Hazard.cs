using UnityEngine;

namespace DashAndCollect
{
    /// <summary>
    /// Tags a GameObject as a hazard. Detected by CollisionHandler via GetComponent&lt;Hazard&gt;() (TDD §4.4).
    /// Attach to any obstacle prefab child that owns the BoxCollider2D trigger.
    /// </summary>
    public sealed class Hazard : MonoBehaviour { }
}
