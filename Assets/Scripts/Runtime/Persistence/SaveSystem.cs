using UnityEngine;

namespace DashAndCollect
{
    /// <summary>
    /// Persists and retrieves all player-owned save data via PlayerPrefs.
    ///
    /// Scope (GDD §5.7):
    ///   - Personal best (high score)
    ///   - Onboarding completion flag
    ///
    /// Security (rules/common/security.md):
    ///   PlayerPrefs stores plain text. This class intentionally stores ONLY
    ///   non-sensitive gameplay data (score integers, boolean flags). No PII,
    ///   no auth tokens, no device identifiers.
    ///
    /// Key contract:
    ///   All PlayerPrefs keys are public constants prefixed with "DashAndCollect."
    ///   to avoid collisions with Unity packages and third-party SDKs.
    ///   ClearAll() deletes only owned keys — it does not call PlayerPrefs.DeleteAll().
    ///
    /// Migration note (serialization-data rules):
    ///   If a key is renamed in a future version, add a migration step in
    ///   LoadHighScore() / IsOnboardingComplete() before removing the old key.
    ///
    /// Testing:
    ///   PlayerPrefs writes to disk/registry in all Unity test modes.
    ///   Tests call ClearAll() in both SetUp and TearDown for isolation.
    /// </summary>
    public static class SaveSystem
    {
        // ── Owned keys ──────────────────────────────────────────────────────────
        public const string KeyHighScore          = "DashAndCollect.HighScore";
        public const string KeyOnboardingComplete = "DashAndCollect.OnboardingComplete";

        // ── Defaults ────────────────────────────────────────────────────────────
        private const int DefaultHighScore   = 0;
        private const int OnboardingNotDone  = 0;
        private const int OnboardingDone     = 1;

        // ── High score ──────────────────────────────────────────────────────────

        /// <summary>
        /// Persists <paramref name="score"/> as the player's high score.
        /// Overwrites any previously stored value. No clamping — caller is responsible
        /// for ensuring the value is non-negative.
        /// </summary>
        public static void SaveHighScore(int score)
        {
            PlayerPrefs.SetInt(KeyHighScore, score);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Returns the stored high score, or 0 if no value has been saved.
        /// </summary>
        public static int LoadHighScore() =>
            PlayerPrefs.GetInt(KeyHighScore, DefaultHighScore);

        // ── Onboarding ──────────────────────────────────────────────────────────

        /// <summary>
        /// Returns true if the player has completed the onboarding sequence
        /// (GDD §7.1: first 10 seconds, no hazards).
        /// </summary>
        public static bool IsOnboardingComplete() =>
            PlayerPrefs.GetInt(KeyOnboardingComplete, OnboardingNotDone) == OnboardingDone;

        /// <summary>
        /// Marks onboarding as complete. Idempotent — safe to call multiple times.
        /// </summary>
        public static void CompleteOnboarding()
        {
            PlayerPrefs.SetInt(KeyOnboardingComplete, OnboardingDone);
            PlayerPrefs.Save();
        }

        // ── Maintenance ─────────────────────────────────────────────────────────

        /// <summary>
        /// Deletes all SaveSystem-owned keys from PlayerPrefs.
        /// Does NOT call PlayerPrefs.DeleteAll() — unowned keys from Unity packages
        /// or third-party SDKs are not affected.
        /// </summary>
        public static void ClearAll()
        {
            PlayerPrefs.DeleteKey(KeyHighScore);
            PlayerPrefs.DeleteKey(KeyOnboardingComplete);
            PlayerPrefs.Save();
        }
    }
}
