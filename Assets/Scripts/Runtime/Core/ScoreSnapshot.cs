namespace DashAndCollect
{
    /// <summary>
    /// Point-in-time snapshot of scoring state.
    /// Passed via OnScoreChanged and returned by GetRunSummary() (TDD §4.5).
    ///
    /// Class (not struct) so that Action&lt;ScoreSnapshot&gt; delegate invocations do not
    /// box the value in Mono (editor / development builds). Treat as read-only at call sites.
    /// </summary>
    public class ScoreSnapshot
    {
        public int score;
        public int chainCount;
        public CollectibleType? chainType;
        public int coinsEarnedThisRun;
        public int personalBest;
        public bool isNewPersonalBest;
    }
}
