using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using DashAndCollect;

namespace DashAndCollect.Tests
{
    /// <summary>
    /// Edit-mode integration tests for CollisionHandler trigger buffer, priority,
    /// shield absorption, and state guards (TDD §4.4, M2 exit criteria).
    ///
    /// No physics loop is available in edit-mode tests.
    /// SimulateCollectible / SimulateHazard (internal helpers) inject directly into the
    /// trigger buffer, honouring the same RunState guard as OnTriggerEnter2D.
    /// InvokeUpdate drives buffer processing without a running player loop.
    ///
    /// SetUp reuses the same scene-in-code pattern as GameManagerTests.
    ///
    /// Naming: MethodUnderTest_Condition_ExpectedResult
    /// </summary>
    [TestFixture]
    public class CollisionHandlerTests
    {
        // ── Fixtures ──────────────────────────────────────────────────────────

        private GameObject       _root;
        private GameManager      _gm;
        private CollisionHandler _collision;
        private SpawnManager     _spawn;
        private PlayerController _player;
        private SpawnConfig      _spawnConfig;
        private ScoreConfig      _scoreConfig;
        private LaneConfig       _laneConfig;
        private PlayerConfig     _playerConfig;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("TestRoot");
            _root.SetActive(false);

            _collision = _root.AddComponent<CollisionHandler>();
            _spawn     = _root.AddComponent<SpawnManager>();
            _player    = _root.AddComponent<PlayerController>();
            _gm        = _root.AddComponent<GameManager>();

            _spawnConfig = ScriptableObject.CreateInstance<SpawnConfig>();
            _spawnConfig.initialSpeed   = 5f;
            _spawnConfig.speedIncrement = 0.5f;
            _spawnConfig.maxSpeed       = 20f;

            _scoreConfig = ScriptableObject.CreateInstance<ScoreConfig>();
            _scoreConfig.basePickupScore = 10;
            _scoreConfig.chainBonusScore = 50;
            _scoreConfig.coinsPerChain   = 1;

            _laneConfig = ScriptableObject.CreateInstance<LaneConfig>();
            _laneConfig.lanePositions = new float[] { -2f, 0f, 2f };

            _playerConfig = ScriptableObject.CreateInstance<PlayerConfig>();
            _playerConfig.dashDuration     = 0.08f;
            _playerConfig.recoveryDuration = 0.05f;
            _playerConfig.laneConfig       = _laneConfig;

            Set(_gm,     "_collisionHandler", _collision);
            Set(_gm,     "_spawnManager",     _spawn);
            Set(_gm,     "_playerController", _player);
            Set(_gm,     "_spawnConfig",      _spawnConfig);
            Set(_gm,     "_scoreConfig",      _scoreConfig);
            Set(_player, "_config",           _playerConfig);
            Set(_spawn,  "_config",           _spawnConfig);

            InvokeMethod(_gm, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_spawnConfig);
            Object.DestroyImmediate(_scoreConfig);
            Object.DestroyImmediate(_laneConfig);
            Object.DestroyImmediate(_playerConfig);
        }

        // ── Collectible dispatch ───────────────────────────────────────────────

        [Test]
        public void CollectibleTrigger_WhenRunning_FiresOnCollectiblePickedUp()
        {
            _gm.StartRun();
            bool fired = false;
            _collision.OnCollectiblePickedUp += _ => fired = true;

            InvokeMethod(_collision, "SimulateCollectible", CollectibleType.Dash);
            InvokeUpdate(_collision);

            Assert.IsTrue(fired, "OnCollectiblePickedUp must fire when a collectible is in the buffer.");
        }

        [Test]
        public void CollectibleTrigger_WhenRunning_FiresWithCorrectType()
        {
            _gm.StartRun();
            CollectibleType received = default;
            _collision.OnCollectiblePickedUp += t => received = t;

            InvokeMethod(_collision, "SimulateCollectible", CollectibleType.Surge);
            InvokeUpdate(_collision);

            Assert.AreEqual(CollectibleType.Surge, received);
        }

        [Test]
        public void CollectibleTrigger_MultipleInOneFrame_AllFire()
        {
            _gm.StartRun();
            int count = 0;
            _collision.OnCollectiblePickedUp += _ => count++;

            InvokeMethod(_collision, "SimulateCollectible", CollectibleType.Dash);
            InvokeMethod(_collision, "SimulateCollectible", CollectibleType.Coin);
            InvokeUpdate(_collision);

            Assert.AreEqual(2, count, "Every collectible in the same-frame buffer must dispatch.");
        }

        // ── Hazard dispatch ───────────────────────────────────────────────────

        [Test]
        public void HazardTrigger_WhenRunning_FiresOnPlayerDied()
        {
            _gm.StartRun();
            bool fired = false;
            _collision.OnPlayerDied += () => fired = true;

            InvokeMethod(_collision, "SimulateHazard");
            InvokeUpdate(_collision);

            Assert.IsTrue(fired, "OnPlayerDied must fire when a hazard is in the buffer.");
        }

        [Test]
        public void HazardTrigger_MultipleInOneFrame_FiresOnlyOnce()
        {
            _gm.StartRun();
            int count = 0;
            _collision.OnPlayerDied += () => count++;

            InvokeMethod(_collision, "SimulateHazard");
            InvokeMethod(_collision, "SimulateHazard");
            InvokeUpdate(_collision);

            Assert.AreEqual(1, count, "Only the first hazard per frame must trigger death.");
        }

        // ── Collectibles-before-hazards priority ──────────────────────────────

        [Test]
        public void SimultaneousTriggers_CollectibleFiresBeforeHazard()
        {
            _gm.StartRun();
            var order = new List<string>();
            _collision.OnCollectiblePickedUp += _ => order.Add("collectible");
            _collision.OnPlayerDied          += () => order.Add("hazard");

            // Inject hazard first to confirm ordering is enforced by processing logic, not injection order.
            InvokeMethod(_collision, "SimulateHazard");
            InvokeMethod(_collision, "SimulateCollectible", CollectibleType.Dash);
            InvokeUpdate(_collision);

            Assert.AreEqual(2,            order.Count);
            Assert.AreEqual("collectible", order[0], "Collectible must be processed before hazard (TDD §4.4).");
            Assert.AreEqual("hazard",      order[1]);
        }

        // ── Shield absorption ─────────────────────────────────────────────────

        [Test]
        public void HazardTrigger_WhenShieldActive_SuppressesOnPlayerDied()
        {
            _gm.StartRun();
            _collision.ActivateShield();
            bool fired = false;
            _collision.OnPlayerDied += () => fired = true;

            InvokeMethod(_collision, "SimulateHazard");
            InvokeUpdate(_collision);

            Assert.IsFalse(fired, "Active shield must absorb hazard and suppress OnPlayerDied.");
        }

        [Test]
        public void HazardTrigger_WhenShieldActive_ConsumesShield()
        {
            _gm.StartRun();
            _collision.ActivateShield();

            // First hazard — absorbed.
            InvokeMethod(_collision, "SimulateHazard");
            InvokeUpdate(_collision);

            // Second hazard — shield should be gone.
            bool died = false;
            _collision.OnPlayerDied += () => died = true;
            InvokeMethod(_collision, "SimulateHazard");
            InvokeUpdate(_collision);

            Assert.IsTrue(died, "Shield must be consumed after absorbing one hazard.");
        }

        [Test]
        public void SimultaneousTriggers_ShieldActive_CollectibleStillFires()
        {
            _gm.StartRun();
            _collision.ActivateShield();
            bool collectibleFired = false;
            bool diedFired        = false;
            _collision.OnCollectiblePickedUp += _ => collectibleFired = true;
            _collision.OnPlayerDied          += () => diedFired = true;

            InvokeMethod(_collision, "SimulateCollectible", CollectibleType.Dash);
            InvokeMethod(_collision, "SimulateHazard");
            InvokeUpdate(_collision);

            Assert.IsTrue(collectibleFired,  "Collectible must still fire when shield absorbs hazard.");
            Assert.IsFalse(diedFired,        "OnPlayerDied must be suppressed by shield.");
        }

        // ── State guards ──────────────────────────────────────────────────────

        [Test]
        public void CollectibleTrigger_WhenIdle_IsIgnored()
        {
            // State is Idle — triggers must not be buffered.
            bool fired = false;
            _collision.OnCollectiblePickedUp += _ => fired = true;

            InvokeMethod(_collision, "SimulateCollectible", CollectibleType.Dash);
            InvokeUpdate(_collision);

            Assert.IsFalse(fired, "Triggers must be ignored in Idle state.");
        }

        [Test]
        public void CollectibleTrigger_WhenDead_IsIgnored()
        {
            _gm.StartRun();
            _gm.EndRun();   // Running -> Dead
            bool fired = false;
            _collision.OnCollectiblePickedUp += _ => fired = true;

            InvokeMethod(_collision, "SimulateCollectible", CollectibleType.Dash);
            InvokeUpdate(_collision);

            Assert.IsFalse(fired, "Collectible triggers must be ignored in Dead state.");
        }

        [Test]
        public void HazardTrigger_WhenDead_IsIgnored()
        {
            _gm.StartRun();
            _gm.EndRun();   // Running -> Dead
            bool fired = false;
            _collision.OnPlayerDied += () => fired = true;

            InvokeMethod(_collision, "SimulateHazard");
            InvokeUpdate(_collision);

            Assert.IsFalse(fired, "Hazard triggers must be ignored in Dead state.");
        }

        // ── GameManager wiring (Option A integration) ─────────────────────────

        [Test]
        public void CollectiblePickup_WhenRunning_ScoreIncrements()
        {
            // Verifies the full wiring: CollisionHandler.OnCollectiblePickedUp
            // → ScoreManager.RegisterPickup → score accumulates (TDD §4.4 + §4.5).
            _gm.StartRun();
            int score = 0;
            _gm.ScoreManager.OnScoreChanged += s => score = s.score;

            InvokeMethod(_collision, "SimulateCollectible", CollectibleType.Dash);
            InvokeUpdate(_collision);

            Assert.Greater(score, 0, "Pickup via CollisionHandler must route to ScoreManager.");
        }

        [Test]
        public void HazardPickup_WhenRunning_TransitionsToDead()
        {
            // Verifies: CollisionHandler.OnPlayerDied → GameManager.EndRun (already wired
            // before this session; test kept here for completeness of the integration suite).
            _gm.StartRun();

            InvokeMethod(_collision, "SimulateHazard");
            InvokeUpdate(_collision);

            Assert.AreEqual(RunState.Dead, _gm.CurrentState,
                "Hazard must route through OnPlayerDied → GameManager.EndRun.");
        }

        [Test]
        public void CoinPickup_WhenRunning_IncrementsCoinCount()
        {
            // Coin type: score increments and coin counter increments (TDD §4.5).
            _gm.StartRun();
            int coins = 0;
            _gm.ScoreManager.OnScoreChanged += s => coins = s.coinsEarnedThisRun;

            InvokeMethod(_collision, "SimulateCollectible", CollectibleType.Coin);
            InvokeUpdate(_collision);

            Assert.AreEqual(1, coins, "Coin pickup must increment coinsEarnedThisRun via ScoreManager.");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void Set<T>(T instance, string fieldName, object value)
        {
            typeof(T).GetField(fieldName,
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(instance, value);
        }

        private static void InvokeMethod(MonoBehaviour mb, string methodName) =>
            mb.GetType()
                .GetMethod(methodName,
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public)
                ?.Invoke(mb, null);

        private static void InvokeMethod<T>(MonoBehaviour mb, string methodName, T arg) =>
            mb.GetType()
                .GetMethod(methodName,
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public)
                ?.Invoke(mb, new object[] { arg });

        private static void InvokeUpdate(MonoBehaviour mb) => InvokeMethod(mb, "Update");
    }
}
