using UnityEngine;

namespace DashAndCollect
{
    /// <summary>
    /// Marks a child GameObject within a chunk prefab as a coin collectible.
    /// Enabled by SpawnManager's safety pass when a hazard is replaced (TDD §4.3).
    /// Also used by CollisionHandler to dispatch OnCollectiblePickedUp(Coin) (TDD §4.4).
    /// </summary>
    public sealed class CoinMarker : MonoBehaviour { }
}
