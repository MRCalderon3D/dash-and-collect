using NUnit.Framework;
using UnityEngine;
using DashAndCollect;

namespace DashAndCollect.Tests
{
    /// <summary>
    /// Edit-mode tests for SaveSystem persistence, defaults, idempotency,
    /// and key isolation (M2 exit criteria).
    ///
    /// Strategy:
    ///   PlayerPrefs writes to disk/registry in all Unity test modes. Tests call
    ///   SaveSystem.ClearAll() in both SetUp and TearDown to guarantee a clean slate
    ///   before and after every test regardless of prior run state.
    ///
    ///   ClearAll_OnlyDeletesOwnedKeys verifies that unowned keys survive a ClearAll()
    ///   call; it cleans up its own extra key explicitly.
    ///
    /// Naming: MethodUnderTest_Condition_ExpectedResult
    /// </summary>
    [TestFixture]
    public class SaveSystemTests
    {
        [SetUp]
        public void SetUp() => SaveSystem.ClearAll();

        [TearDown]
        public void TearDown() => SaveSystem.ClearAll();

        // ── LoadHighScore defaults ────────────────────────────────────────────

        [Test]
        public void LoadHighScore_WhenNoDataStored_ReturnsZero()
        {
            Assert.AreEqual(0, SaveSystem.LoadHighScore(),
                "LoadHighScore must return 0 when no value has been saved.");
        }

        // ── SaveHighScore round-trip ──────────────────────────────────────────

        [Test]
        public void SaveHighScore_ThenLoad_ReturnsStoredValue()
        {
            SaveSystem.SaveHighScore(1234);

            Assert.AreEqual(1234, SaveSystem.LoadHighScore(),
                "LoadHighScore must return the value that was saved.");
        }

        [Test]
        public void SaveHighScore_Zero_IsStoredAndLoaded()
        {
            SaveSystem.SaveHighScore(999);
            SaveSystem.SaveHighScore(0);

            Assert.AreEqual(0, SaveSystem.LoadHighScore(),
                "Saving 0 must overwrite a prior value and be returned correctly.");
        }

        [Test]
        public void SaveHighScore_CalledTwice_ReturnsLatestValue()
        {
            SaveSystem.SaveHighScore(100);
            SaveSystem.SaveHighScore(500);

            Assert.AreEqual(500, SaveSystem.LoadHighScore(),
                "A second SaveHighScore must overwrite the first.");
        }

        [Test]
        public void SaveHighScore_LargeValue_RoundTripsCorrectly()
        {
            SaveSystem.SaveHighScore(int.MaxValue);

            Assert.AreEqual(int.MaxValue, SaveSystem.LoadHighScore(),
                "SaveHighScore must handle large integer values without truncation.");
        }

        // ── IsOnboardingComplete defaults ─────────────────────────────────────

        [Test]
        public void IsOnboardingComplete_WhenNotSet_ReturnsFalse()
        {
            Assert.IsFalse(SaveSystem.IsOnboardingComplete(),
                "IsOnboardingComplete must return false when CompleteOnboarding has never been called.");
        }

        // ── CompleteOnboarding round-trip ─────────────────────────────────────

        [Test]
        public void CompleteOnboarding_ThenCheck_ReturnsTrue()
        {
            SaveSystem.CompleteOnboarding();

            Assert.IsTrue(SaveSystem.IsOnboardingComplete(),
                "IsOnboardingComplete must return true after CompleteOnboarding is called.");
        }

        [Test]
        public void CompleteOnboarding_CalledTwice_RemainsTrue()
        {
            SaveSystem.CompleteOnboarding();
            SaveSystem.CompleteOnboarding();

            Assert.IsTrue(SaveSystem.IsOnboardingComplete(),
                "CompleteOnboarding must be idempotent — calling it twice must not corrupt state.");
        }

        // ── ClearAll ──────────────────────────────────────────────────────────

        [Test]
        public void ClearAll_AfterSaveHighScore_ResetsToZero()
        {
            SaveSystem.SaveHighScore(777);
            SaveSystem.ClearAll();

            Assert.AreEqual(0, SaveSystem.LoadHighScore(),
                "ClearAll must reset the high score to the default (0).");
        }

        [Test]
        public void ClearAll_AfterCompleteOnboarding_ResetsToFalse()
        {
            SaveSystem.CompleteOnboarding();
            SaveSystem.ClearAll();

            Assert.IsFalse(SaveSystem.IsOnboardingComplete(),
                "ClearAll must reset the onboarding flag to false.");
        }

        [Test]
        public void ClearAll_IsIdempotent()
        {
            SaveSystem.ClearAll();
            SaveSystem.ClearAll();

            Assert.AreEqual(0,     SaveSystem.LoadHighScore(),        "ClearAll called twice must leave defaults intact.");
            Assert.IsFalse(SaveSystem.IsOnboardingComplete(), "ClearAll called twice must leave defaults intact.");
        }

        [Test]
        public void ClearAll_OnlyDeletesOwnedKeys()
        {
            const string foreignKey = "SomeThirdParty.Setting";
            PlayerPrefs.SetInt(foreignKey, 42);

            SaveSystem.SaveHighScore(100);
            SaveSystem.ClearAll();

            int foreignValue = PlayerPrefs.GetInt(foreignKey, -1);
            PlayerPrefs.DeleteKey(foreignKey);   // explicit cleanup for this test

            Assert.AreEqual(42, foreignValue,
                "ClearAll must not delete PlayerPrefs keys it does not own.");
        }

        // ── Key constant contract ─────────────────────────────────────────────

        [Test]
        public void KeyConstants_AreNonEmpty()
        {
            Assert.IsFalse(string.IsNullOrEmpty(SaveSystem.KeyHighScore),
                "KeyHighScore must be a non-empty string constant.");
            Assert.IsFalse(string.IsNullOrEmpty(SaveSystem.KeyOnboardingComplete),
                "KeyOnboardingComplete must be a non-empty string constant.");
        }

        [Test]
        public void KeyConstants_AreDistinct()
        {
            Assert.AreNotEqual(SaveSystem.KeyHighScore, SaveSystem.KeyOnboardingComplete,
                "Each SaveSystem key must be unique to prevent PlayerPrefs collisions.");
        }

        [Test]
        public void KeyConstants_HaveNamespacePrefix()
        {
            StringAssert.StartsWith("DashAndCollect.", SaveSystem.KeyHighScore,
                "Keys must be prefixed with 'DashAndCollect.' to avoid collisions.");
            StringAssert.StartsWith("DashAndCollect.", SaveSystem.KeyOnboardingComplete,
                "Keys must be prefixed with 'DashAndCollect.' to avoid collisions.");
        }
    }
}
