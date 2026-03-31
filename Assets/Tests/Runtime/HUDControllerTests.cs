using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using DashAndCollect;

namespace DashAndCollect.Tests
{
    /// <summary>
    /// Edit-mode tests for HUDController label updates (M2 exit criteria).
    ///
    /// Strategy:
    ///   HUDController is wired to a real GameManager (and thus a real ScoreManager).
    ///   Score is driven directly via ScoreManager API — no CollisionHandler physics needed.
    ///   Text components are created on standalone GameObjects (no Canvas required for
    ///   property access in edit mode).
    ///
    /// SetUp order: wire GM fields → InvokeMethod(gm, "Awake") → InvokeMethod(hud, "Awake").
    /// GameManager.Awake must run first to create the ScoreManager instance.
    ///
    /// Naming: MethodUnderTest_Condition_ExpectedResult
    /// </summary>
    [TestFixture]
    public class HUDControllerTests
    {
        // ── Fixtures ──────────────────────────────────────────────────────────

        private GameObject     _systemRoot;
        private GameObject     _hudRoot;
        private GameManager    _gm;
        private HUDController  _hud;
        private Text           _scoreLabel;
        private Text           _coinLabel;
        private Text           _pbLabel;
        private SpawnConfig    _spawnConfig;
        private ScoreConfig    _scoreConfig;
        private LaneConfig     _laneConfig;
        private PlayerConfig   _playerConfig;

        [SetUp]
        public void SetUp()
        {
            // ── System objects (inactive until Awake is explicitly invoked) ──────
            _systemRoot = new GameObject("SystemRoot");
            _systemRoot.SetActive(false);

            var collision = _systemRoot.AddComponent<CollisionHandler>();
            var spawn     = _systemRoot.AddComponent<SpawnManager>();
            var player    = _systemRoot.AddComponent<PlayerController>();
            _gm           = _systemRoot.AddComponent<GameManager>();

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

            Set(_gm,     "_collisionHandler", collision);
            Set(_gm,     "_spawnManager",     spawn);
            Set(_gm,     "_playerController", player);
            Set(_gm,     "_spawnConfig",      _spawnConfig);
            Set(_gm,     "_scoreConfig",      _scoreConfig);
            Set(player,  "_config",           _playerConfig);
            Set(spawn,   "_config",           _spawnConfig);

            // ── HUD (separate inactive root) ─────────────────────────────────────
            _hudRoot = new GameObject("HUD");
            _hudRoot.SetActive(false);
            _hud = _hudRoot.AddComponent<HUDController>();

            // Labels on child GameObjects (no Canvas needed for .text access).
            _scoreLabel = new GameObject("ScoreLabel").AddComponent<Text>();
            _coinLabel  = new GameObject("CoinLabel").AddComponent<Text>();
            _pbLabel    = new GameObject("PBLabel").AddComponent<Text>();

            Set(_hud, "_gameManager",       _gm);
            Set(_hud, "_scoreLabel",        _scoreLabel);
            Set(_hud, "_coinLabel",         _coinLabel);
            Set(_hud, "_personalBestLabel", _pbLabel);

            // GameManager.Awake must run first (creates ScoreManager instance).
            InvokeMethod(_gm,  "Awake");
            InvokeMethod(_hud, "Awake");

            // Begin a run so ScoreManager is in a valid running state.
            _gm.StartRun();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_systemRoot);
            Object.DestroyImmediate(_hudRoot);
            Object.DestroyImmediate(_scoreLabel?.gameObject);
            Object.DestroyImmediate(_coinLabel?.gameObject);
            Object.DestroyImmediate(_pbLabel?.gameObject);
            Object.DestroyImmediate(_spawnConfig);
            Object.DestroyImmediate(_scoreConfig);
            Object.DestroyImmediate(_laneConfig);
            Object.DestroyImmediate(_playerConfig);
        }

        // ── Score label ───────────────────────────────────────────────────────

        [Test]
        public void ScoreChanged_UpdatesScoreLabel()
        {
            _gm.ScoreManager.AddDistanceScore(42);

            Assert.AreEqual("42", _scoreLabel.text,
                "Score label must reflect the current score from OnScoreChanged.");
        }

        [Test]
        public void ScoreChanged_AccumulatesAcrossMultipleEvents()
        {
            _gm.ScoreManager.AddDistanceScore(10);
            _gm.ScoreManager.AddDistanceScore(20);

            Assert.AreEqual("30", _scoreLabel.text,
                "Score label must show cumulative total, not per-event delta.");
        }

        // ── Coin label ────────────────────────────────────────────────────────

        [Test]
        public void ScoreChanged_CoinPickup_UpdatesCoinLabel()
        {
            _gm.ScoreManager.RegisterPickup(CollectibleType.Coin);

            Assert.AreEqual("1", _coinLabel.text,
                "Coin label must reflect coinsEarnedThisRun.");
        }

        [Test]
        public void ScoreChanged_NonCoinPickup_CoinLabelRemainsZero()
        {
            _gm.ScoreManager.RegisterPickup(CollectibleType.Dash);

            Assert.AreEqual("0", _coinLabel.text,
                "Coin label must not increment for non-Coin pickups.");
        }

        // ── Personal best label ───────────────────────────────────────────────

        [Test]
        public void ScoreChanged_WhenPersonalBestBeaten_UpdatesPBLabel()
        {
            _gm.ScoreManager.AddDistanceScore(100);

            Assert.AreEqual("100", _pbLabel.text,
                "Personal-best label must update when the high score is beaten.");
        }

        [Test]
        public void ScoreChanged_WhenPBNotBeaten_PBLabelReflectsStoredBest()
        {
            // Establish a personal best in run 1.
            _gm.ScoreManager.AddDistanceScore(50);

            // Reset for run 2 — PB persists, score resets.
            _gm.EndRun();
            _gm.RestartRun();
            _gm.ScoreManager.AddDistanceScore(1); // below PB

            // Label shows persisted PB (50), not current score (1).
            Assert.AreEqual("50", _pbLabel.text,
                "PB label must show the persisted personal best across runs.");
        }

        // ── Null safety ───────────────────────────────────────────────────────

        [Test]
        public void ScoreChanged_WhenLabelsNull_DoesNotThrow()
        {
            Set(_hud, "_scoreLabel",        (Text)null);
            Set(_hud, "_coinLabel",         (Text)null);
            Set(_hud, "_personalBestLabel", (Text)null);

            Assert.DoesNotThrow(() => _gm.ScoreManager.AddDistanceScore(10),
                "HUDController must handle null label references gracefully.");
        }

        // ── Unsubscription ────────────────────────────────────────────────────

        [Test]
        public void OnDestroy_UnsubscribesFromScoreManager()
        {
            _gm.ScoreManager.AddDistanceScore(10);
            string labelAfterFirstEvent = _scoreLabel.text;

            InvokeMethod(_hud, "OnDestroy");  // simulate component destruction

            _gm.ScoreManager.AddDistanceScore(99);  // should not reach the controller

            Assert.AreEqual(labelAfterFirstEvent, _scoreLabel.text,
                "After OnDestroy, HUDController must not update labels on further score events.");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void Set<T>(T instance, string fieldName, object value)
        {
            var type = typeof(T);
            var field = type.GetField(fieldName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);
            field?.SetValue(instance, value);
        }

        private static void InvokeMethod(MonoBehaviour mb, string methodName) =>
            mb.GetType()
                .GetMethod(methodName,
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(mb, null);
    }
}
