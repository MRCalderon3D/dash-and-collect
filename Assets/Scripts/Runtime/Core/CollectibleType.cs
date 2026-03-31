namespace DashAndCollect
{
    /// <summary>
    /// Identifies the type of a collectible pickup (TDD §4.4).
    /// Coin never contributes to chain counter (TDD §4.5).
    /// </summary>
    public enum CollectibleType { Dash, Shield, Surge, Coin }
}
