using NUnit.Framework;
using UnityEngine;
using DashAndCollect;

namespace DashAndCollect.Tests
{
    /// <summary>
    /// Unit tests for ScoreManager chain logic, score accumulation, multiplier,
    /// personal best, and reset (TDD §11, M2 exit criteria).
    ///
    /// ScoreManager is plain C# — no scene, no MonoBehaviour, no reflection needed.
    /// A fresh ScoreConfig and ScoreManager are created in SetUp for each test.
    ///
    /// Naming: MethodUnderTest_Condition_ExpectedResult
    /// </summary>
    [TestFixture]
    public class ScoreManagerTests
    {
        private ScoreConfig  _config;
        private ScoreManager _sm;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<ScoreConfig>();
            _config.basePickupScore = 10;
            _config.chainBonusScore = 50;
            _config.coinsPerChain   = 1;

            _sm = new ScoreManager(_config);
            _sm.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
        }

        // ── Pickup score ──────────────────────────────────────────────────────

        [Test]
        public void RegisterPickup_Dash_AddsBasePickupScore()
        {
            int received = -1;
            _sm.OnScoreChanged += s => received = s.score;

            _sm.RegisterPickup(CollectibleType.Dash);

            Assert.AreEqual(_config.basePickupScore, received);
        }

        [Test]
        public void RegisterPickup_Coin_AddsBasePickupScore()
        {
            int received = -1;
            _sm.OnScoreChanged += s => received = s.score;

            _sm.RegisterPickup(CollectibleType.Coin);

            Assert.AreEqual(_config.basePickupScore, received);
        }

        [Test]
        public void RegisterPickup_Coin_IncrementsCoinCount()
        {
            int coins = -1;
            _sm.OnScoreChanged += s => coins = s.coinsEarnedThisRun;

            _sm.RegisterPickup(CollectibleType.Coin);

            Assert.AreEqual(1, coins, "Coin pickup must increment coinsEarnedThisRun.");
        }

        // ── Chain logic ───────────────────────────────────────────────────────

        [Test]
        public void RegisterPickup_Coin_DoesNotAdvanceChainCounter()
        {
            _sm.RegisterPickup(CollectibleType.Dash);  // chainCount = 1
            int chainCount = -1;
            _sm.OnScoreChanged += s => chainCount = s.chainCount;

            _sm.RegisterPickup(CollectibleType.Coin);  // must not touch chain

            Assert.AreEqual(1, chainCount,
                "Coin pickup must not advance the chain counter.");
        }

        [Test]
        public void RegisterPickup_SameType3Times_FiresOnChainCompleted()
        {
            CollectibleType? fired = null;
            _sm.OnChainCompleted += t => fired = t;

            _sm.RegisterPickup(CollectibleType.Dash);
            _sm.RegisterPickup(CollectibleType.Dash);
            _sm.RegisterPickup(CollectibleType.Dash);

            Assert.AreEqual(CollectibleType.Dash, fired,
                "OnChainCompleted must fire after 3 pickups of the same type.");
        }

        [Test]
        public void RegisterPickup_SameType3Times_AddsChainBonusScore()
        {
            int score = 0;
            _sm.OnScoreChanged += s => score = s.score;

            _sm.RegisterPickup(CollectibleType.Dash);
            _sm.RegisterPickup(CollectibleType.Dash);
            _sm.RegisterPickup(CollectibleType.Dash);

            int expected = _config.basePickupScore * 3 + _config.chainBonusScore;
            Assert.AreEqual(expected, score,
                "Chain completion must add chainBonusScore on top of 3 pickup scores.");
        }

        [Test]
        public void RegisterPickup_SameType3Times_ResetsChainCounter()
        {
            _sm.RegisterPickup(CollectibleType.Dash);
            _sm.RegisterPickup(CollectibleType.Dash);
            _sm.RegisterPickup(CollectibleType.Dash);  // chain fires, resets

            int chainCount = -1;
            _sm.OnScoreChanged += s => chainCount = s.chainCount;
            _sm.RegisterPickup(CollectibleType.Dash);  // starts new chain at 1

            Assert.AreEqual(1, chainCount,
                "Chain counter must reset to 0 after completion, then increment to 1 on next pickup.");
        }

        [Test]
        public void RegisterPickup_DifferentType_ResetsChainToOne()
        {
            _sm.RegisterPickup(CollectibleType.Dash);
            _sm.RegisterPickup(CollectibleType.Dash);  // chainCount = 2

            int chainCount = -1;
            CollectibleType? chainType = null;
            _sm.OnScoreChanged += s => { chainCount = s.chainCount; chainType = s.chainType; };

            _sm.RegisterPickup(CollectibleType.Shield);  // different type

            Assert.AreEqual(1, chainCount,
                "Chain counter must reset to 1 when pickup type changes.");
            Assert.AreEqual(CollectibleType.Shield, chainType,
                "Chain type must update to the new pickup type.");
        }

        [Test]
        public void RegisterPickup_ChainCompletion_AwardsCoins()
        {
            int coins = 0;
            _sm.OnScoreChanged += s => coins = s.coinsEarnedThisRun;

            _sm.RegisterPickup(CollectibleType.Dash);
            _sm.RegisterPickup(CollectibleType.Dash);
            _sm.RegisterPickup(CollectibleType.Dash);

            Assert.AreEqual(_config.coinsPerChain, coins,
                "Chain completion must award coinsPerChain meta currency.");
        }

        // ── Multiplier ────────────────────────────────────────────────────────

        [Test]
        public void SetMultiplier_2x_DoublesPickupScore()
        {
            _sm.SetMultiplier(2f);
            int score = 0;
            _sm.OnScoreChanged += s => score = s.score;

            _sm.RegisterPickup(CollectibleType.Dash);

            Assert.AreEqual(_config.basePickupScore * 2, score,
                "2× multiplier must double the pickup score.");
        }

        [Test]
        public void SetMultiplier_2x_DoublesChainBonus()
        {
            _sm.SetMultiplier(2f);
            int score = 0;
            _sm.OnScoreChanged += s => score = s.score;

            _sm.RegisterPickup(CollectibleType.Dash);
            _sm.RegisterPickup(CollectibleType.Dash);
            _sm.RegisterPickup(CollectibleType.Dash);

            int expected = (_config.basePickupScore * 3 + _config.chainBonusScore) * 2;
            Assert.AreEqual(expected, score,
                "2× multiplier must double both pickup and chain bonus scores.");
        }

        [Test]
        public void AddDistanceScore_IsNotAffectedByMultiplier()
        {
            _sm.SetMultiplier(2f);
            int score = 0;
            _sm.OnScoreChanged += s => score = s.score;

            _sm.AddDistanceScore(100);

            Assert.AreEqual(100, score,
                "Distance score must be flat — not scaled by the Surge multiplier.");
        }

        // ── Distance score ────────────────────────────────────────────────────

        [Test]
        public void AddDistanceScore_AddsCorrectAmount()
        {
            int score = 0;
            _sm.OnScoreChanged += s => score = s.score;

            _sm.AddDistanceScore(75);

            Assert.AreEqual(75, score);
        }

        [Test]
        public void AddDistanceScore_Zero_DoesNotFireOnScoreChanged()
        {
            int callCount = 0;
            _sm.OnScoreChanged += _ => callCount++;

            _sm.AddDistanceScore(0);

            Assert.AreEqual(0, callCount,
                "AddDistanceScore(0) must not fire OnScoreChanged.");
        }

        // ── Personal best ─────────────────────────────────────────────────────

        [Test]
        public void PersonalBest_BeatenFirstTime_FiresOnHighScoreBeaten()
        {
            int fired = -1;
            _sm.OnHighScoreBeaten += v => fired = v;

            _sm.AddDistanceScore(1);  // 0 PB → beaten on first point

            Assert.Greater(fired, 0,
                "OnHighScoreBeaten must fire when score first exceeds personal best.");
        }

        [Test]
        public void PersonalBest_BeatenTwiceInRun_EventFiresOnlyOnce()
        {
            int callCount = 0;
            _sm.OnHighScoreBeaten += _ => callCount++;

            _sm.AddDistanceScore(50);
            _sm.AddDistanceScore(50);  // second crossing — guard should block

            Assert.AreEqual(1, callCount,
                "OnHighScoreBeaten must fire at most once per run.");
        }

        [Test]
        public void PersonalBest_IsNewPersonalBest_SetInSnapshot()
        {
            _sm.AddDistanceScore(1);

            var summary = _sm.GetRunSummary();
            Assert.IsTrue(summary.isNewPersonalBest,
                "isNewPersonalBest must be true after personal best is beaten.");
        }

        [Test]
        public void PersonalBest_PersistedAcrossRuns_WithinSession()
        {
            _sm.AddDistanceScore(100);
            int pbAfterRun1 = _sm.GetRunSummary().personalBest;

            _sm.ResetForNewRun();
            // Score is 0 again — PB should remain 100.
            var summary = _sm.GetRunSummary();

            Assert.AreEqual(pbAfterRun1, summary.personalBest,
                "Personal best must persist across ResetForNewRun (session-scoped until SaveManager).");
        }

        // ── ResetForNewRun ────────────────────────────────────────────────────

        [Test]
        public void ResetForNewRun_ClearsScoreAndChainAndCoins()
        {
            _sm.RegisterPickup(CollectibleType.Dash);
            _sm.RegisterPickup(CollectibleType.Dash);
            _sm.AddDistanceScore(50);

            _sm.ResetForNewRun();
            var s = _sm.GetRunSummary();

            Assert.AreEqual(0, s.score,               "score must reset.");
            Assert.AreEqual(0, s.chainCount,           "chainCount must reset.");
            Assert.IsNull(s.chainType,                 "chainType must reset.");
            Assert.AreEqual(0, s.coinsEarnedThisRun,   "coinsEarnedThisRun must reset.");
            Assert.IsFalse(s.isNewPersonalBest,        "isNewPersonalBest must reset.");
        }

        [Test]
        public void ResetForNewRun_ResetsMultiplierToOne()
        {
            _sm.SetMultiplier(2f);
            _sm.ResetForNewRun();

            int score = 0;
            _sm.OnScoreChanged += s => score = s.score;
            _sm.RegisterPickup(CollectibleType.Dash);

            Assert.AreEqual(_config.basePickupScore, score,
                "Multiplier must reset to 1× after ResetForNewRun.");
        }

        // ── GetRunSummary snapshot ────────────────────────────────────────────

        [Test]
        public void GetRunSummary_ReturnsCorrectSnapshot()
        {
            _sm.RegisterPickup(CollectibleType.Surge);
            _sm.RegisterPickup(CollectibleType.Surge);
            _sm.AddDistanceScore(30);

            var s = _sm.GetRunSummary();

            Assert.AreEqual(_config.basePickupScore * 2 + 30, s.score);
            Assert.AreEqual(2, s.chainCount);
            Assert.AreEqual(CollectibleType.Surge, s.chainType);
            Assert.AreEqual(0, s.coinsEarnedThisRun);  // chain not complete yet
        }
    }
}
