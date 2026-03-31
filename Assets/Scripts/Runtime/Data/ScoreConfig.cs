using UnityEngine;

namespace DashAndCollect
{
    /// <summary>
    /// Authored tuning data for ScoreManager (TDD §4.5, §6.1).
    /// Read-only at runtime — no mutable state.
    /// </summary>
    [CreateAssetMenu(fileName = "ScoreConfig", menuName = "DashAndCollect/ScoreConfig")]
    public sealed class ScoreConfig : ScriptableObject
    {
        [Tooltip("Score added per collectible pickup (excluding Coin chain logic).")]
        [Min(0)] public int basePickupScore = 10;

        [Tooltip("Bonus score added on chain completion (3 of same type).")]
        [Min(0)] public int chainBonusScore = 50;

        [Tooltip("Meta currency coins awarded per chain completion.")]
        [Min(0)] public int coinsPerChain = 1;
    }
}
