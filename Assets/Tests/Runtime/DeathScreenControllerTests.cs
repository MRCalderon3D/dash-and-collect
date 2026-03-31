using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using DashAndCollect;

namespace DashAndCollect.Tests
{
    /// <summary>
    /// Edit-mode tests for DeathScreenController visibility, label population,
    /// and button wiring (M2 exit criteria).
    ///
    /// Strategy:
    ///   DeathScreenController calls gameObject.SetActive(false) in Awake, so it is
    ///   invoked explicitly after all fields are wired (same pattern as other tests).
    ///   Visibility is driven through GameManager state transitions (StartRun/EndRun/RestartRun).
    ///   Button actions are tested by invoking Button.onClick directly.
    ///
    /// Naming: MethodUnderTest_Condition_ExpectedResult
    /// </summary>
    [TestFixture]
    public class DeathScreenControllerTests
    {
        // ── Fixtures ──────────────────────────────────────────────────────────

        private GameObject              _systemRoot;
        private GameObject              _deathRoot;
        private GameManager             _gm;
        private DeathScreenController   _death;
        private Text                    _finalScoreLabel;
        private Button                  _retryButton;
        private Button                  _mainMenuButton;
        private SpawnConfig             _spawnConfig;
        private ScoreConfig             _scoreConfig;
        private LaneConfig              _laneConfig;
        private PlayerConfig            _playerConfig;

        [SetUp]
        public void SetUp()
        {
            // ── System objects ────────────────────────────────────────────────
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

            // ── DeathScreen panel ─────────────────────────────────────────────
            // Start inactive so Awake does not fire before fields are wired.
            _deathRoot = new GameObject("DeathScreen");
            _deathRoot.SetActive(false);
            _death = _deathRoot.AddComponent<DeathScreenController>();

            _finalScoreLabel = new GameObject("FinalScore").AddComponent<Text>();
            _retryButton     = new GameObject("Retry").AddComponent<Button>();
            _mainMenuButton  = new GameObject("MainMenu").AddComponent<Button>();

            Set(_death, "_gameManager",    _gm);
            Set(_death, "_finalScoreLabel", _finalScoreLabel);
            Set(_death, "_retryButton",     _retryButton);
            Set(_death, "_mainMenuButton",  _mainMenuButton);

            // Wire GM first (creates ScoreManager), then DeathScreenController.
            InvokeMethod(_gm,    "Awake");
            InvokeMethod(_death, "Awake");  // subscribes to events and hides panel
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_systemRoot);
            Object.DestroyImmediate(_deathRoot);
            Object.DestroyImmediate(_finalScoreLabel?.gameObject);
            Object.DestroyImmediate(_retryButton?.gameObject);
            Object.DestroyImmediate(_mainMenuButton?.gameObject);
            Object.DestroyImmediate(_spawnConfig);
            Object.DestroyImmediate(_scoreConfig);
            Object.DestroyImmediate(_laneConfig);
            Object.DestroyImmediate(_playerConfig);
        }

        // ── Initial state ─────────────────────────────────────────────────────

        [Test]
        public void Awake_PanelStartsInactive()
        {
            Assert.IsFalse(_deathRoot.activeSelf,
                "DeathScreen must be hidden immediately after Awake.");
        }

        // ── Show on GameOver ──────────────────────────────────────────────────

        [Test]
        public void OnGameOver_ShowsPanel()
        {
            _gm.StartRun();
            _gm.EndRun();

            Assert.IsTrue(_deathRoot.activeSelf,
                "DeathScreen must become active when GameManager.OnGameOver fires.");
        }

        [Test]
        public void OnGameOver_PopulatesFinalScoreLabel()
        {
            _gm.StartRun();
            _gm.ScoreManager.AddDistanceScore(77);
            _gm.EndRun();

            Assert.AreEqual("77", _finalScoreLabel.text,
                "Final score label must show the run score from GetRunSummary().");
        }

        [Test]
        public void OnGameOver_WhenFinalScoreLabelNull_DoesNotThrow()
        {
            Set(_death, "_finalScoreLabel", (Text)null);
            _gm.StartRun();

            Assert.DoesNotThrow(() => _gm.EndRun(),
                "DeathScreenController must handle null final score label gracefully.");
        }

        // ── Hide on run start / restart ───────────────────────────────────────

        [Test]
        public void OnGameStart_HidesPanel()
        {
            // Manually show the panel to simulate an edge case where it was left visible.
            _deathRoot.SetActive(true);

            // StartRun from Idle fires OnGameStart → Hide().
            _gm.StartRun();

            Assert.IsFalse(_deathRoot.activeSelf,
                "DeathScreen must hide when OnGameStart fires.");
        }

        [Test]
        public void OnGameRestart_HidesPanel()
        {
            _gm.StartRun();
            _gm.EndRun();                // panel shows
            Assert.IsTrue(_deathRoot.activeSelf);

            _gm.RestartRun();            // OnGameRestart → Hide()

            Assert.IsFalse(_deathRoot.activeSelf,
                "DeathScreen must hide when OnGameRestart fires.");
        }

        // ── Button wiring ─────────────────────────────────────────────────────

        [Test]
        public void RetryButton_Click_CallsRestartRun()
        {
            _gm.StartRun();
            _gm.EndRun();   // Running → Dead; panel shown

            _retryButton.onClick.Invoke();

            Assert.AreEqual(RunState.Running, _gm.CurrentState,
                "Retry button must call GameManager.RestartRun().");
        }

        [Test]
        public void RetryButton_Click_HidesPanel()
        {
            _gm.StartRun();
            _gm.EndRun();
            Assert.IsTrue(_deathRoot.activeSelf);

            _retryButton.onClick.Invoke();  // RestartRun → OnGameRestart → Hide

            Assert.IsFalse(_deathRoot.activeSelf,
                "Panel must hide after Retry triggers the restart.");
        }

        // ── Unsubscription ────────────────────────────────────────────────────

        [Test]
        public void OnDestroy_UnsubscribesFromGameManagerEvents()
        {
            InvokeMethod(_death, "OnDestroy");  // simulate component destruction

            _gm.StartRun();
            _gm.EndRun();   // OnGameOver fires — controller must not respond

            Assert.IsFalse(_deathRoot.activeSelf,
                "After OnDestroy, DeathScreenController must not respond to GameManager events.");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void Set<T>(T instance, string fieldName, object value)
        {
            typeof(T).GetField(fieldName,
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public)
                ?.SetValue(instance, value);
        }

        private static void InvokeMethod(MonoBehaviour mb, string methodName) =>
            mb.GetType()
                .GetMethod(methodName,
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(mb, null);
    }
}
