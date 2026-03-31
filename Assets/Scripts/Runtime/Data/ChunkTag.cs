namespace DashAndCollect
{
    /// <summary>
    /// Classifies a chunk's obstacle density for modifier-driven spawn bias (TDD §4.3).
    /// A ChunkDefinition may carry multiple tags.
    /// </summary>
    public enum ChunkTag
    {
        /// <summary>Few or no hazards — used as breathing room.</summary>
        Sparse,

        /// <summary>High hazard density — suppressed during Dash modifier window.</summary>
        Dense,

        /// <summary>Guaranteed safe lane — used for early-game and safety-pass replacement.</summary>
        Safe
    }
}
