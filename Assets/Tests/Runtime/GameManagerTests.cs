using NUnit.Framework;
using UnityEngine;
using DashAndCollect;

namespace DashAndCollect.Tests
{
    /// <summary>
    /// Play-mode integration tests for GameManager state machine (TDD §11, M2 exit criteria).
    ///
    /// Test strategy:
    ///   Each test builds a minimal scene in code — no asset dependency.
    ///   GameManager's four MonoBehaviour dependencies (CollisionHandler, SpawnManager,
    ///   PlayerController) are real stubs wired via reflection helpers below.
    ///   ScoreManager is plain C# and needs no special handling.
    ///   SpawnConfig is created via ScriptableObject.CreateInstance.
    ///
    /// Naming convention: MethodUnderTest_Condition_ExpectedResult
    /// </summary>
    [TestFixture]
    public class GameManagerTests
    {
        // ── Fixtures ──────────────────────────────────────────────────────────

        private GameObject        _root;
        private GameManager       _gm;
        private CollisionHandler  _collision;
        private SpawnManager      _spawn;
        private PlayerController  _player;
        private SpawnConfig       _spawnConfig;
        private ScoreConfig       _scoreConfig;
        private LaneConfig        _laneConfig;
        private PlayerConfig      _playerConfig;

        [SetUp]
        public void SetUp()
        {
            // Create root inactive so Awake does not fire until we have wired all references.
            _root = new GameObject("TestRoot");
            _root.SetActive(false);

            _collision   = _root.AddComponent<CollisionHandler>();
            _spawn       = _root.AddComponent<SpawnManager>();
            _player      = _root.AddComponent<PlayerController>();
            _gm          = _root.AddComponent<GameManager>();

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

            // Wire serialized fields before Awake runs.
            Set(_gm,     "_collisionHandler", _collision);
            Set(_gm,     "_spawnManager",     _spawn);
            Set(_gm,     "_playerController", _player);
            Set(_gm,     "_spawnConfig",      _spawnConfig);
            Set(_gm,     "_scoreConfig",      _scoreConfig);
            Set(_player, "_config",           _playerConfig);
            // SpawnManager.Initialize() now calls BuildPools() which reads _config.
            Set(_spawn,  "_config",           _spawnConfig);

            // SetActive(true) does not reliably fire Awake in edit-mode tests (no player loop).
            // Invoke Awake directly so _scoreManager and event subscriptions are in place.
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

        // ── State transition: Idle -> Running ─────────────────────────────────

        [Test]
        public void StartRun_WhenIdle_TransitionsToRunning()
        {
            // Awake fires automatically when the component is added.
            Assert.AreEqual(RunState.Idle, _gm.CurrentState);

            _gm.StartRun();

            Assert.AreEqual(RunState.Running, _gm.CurrentState);
        }

        [Test]
        public void StartRun_WhenIdle_FiresOnGameStart()
        {
            bool fired = false;
            _gm.OnGameStart += () => fired = true;

            _gm.StartRun();

            Assert.IsTrue(fired, "OnGameStart must fire on first Idle -> Running transition.");
        }

        [Test]
        public void StartRun_WhenIdle_FiresOnRunStateChanged_WithRunning()
        {
            RunState received = RunState.Idle;
            _gm.OnRunStateChanged += s => received = s;

            _gm.StartRun();

            Assert.AreEqual(RunState.Running, received);
        }

        [Test]
        public void StartRun_WhenNotIdle_IsNoOp()
        {
            _gm.StartRun();                          // Idle -> Running
            int callCount = 0;
            _gm.OnGameStart += () => callCount++;

            _gm.StartRun();                          // should be no-op

            Assert.AreEqual(RunState.Running, _gm.CurrentState);
            Assert.AreEqual(0, callCount, "StartRun must not fire again when already Running.");
        }

        // ── State transition: Running -> Dead ─────────────────────────────────

        [Test]
        public void EndRun_WhenRunning_TransitionsToDead()
        {
            _gm.StartRun();
            _gm.EndRun();

            Assert.AreEqual(RunState.Dead, _gm.CurrentState);
        }

        [Test]
        public void EndRun_WhenRunning_FiresOnGameOver()
        {
            _gm.StartRun();
            bool fired = false;
            _gm.OnGameOver += () => fired = true;

            _gm.EndRun();

            Assert.IsTrue(fired, "OnGameOver must fire on Running -> Dead transition.");
        }

        [Test]
        public void EndRun_WhenDead_IsNoOp()
        {
            _gm.StartRun();
            _gm.EndRun();                            // Running -> Dead
            int callCount = 0;
            _gm.OnGameOver += () => callCount++;

            _gm.EndRun();                            // should be no-op

            Assert.AreEqual(RunState.Dead, _gm.CurrentState);
            Assert.AreEqual(0, callCount, "EndRun must not fire again when already Dead.");
        }

        // ── State transition: Dead -> Running ─────────────────────────────────

        [Test]
        public void RestartRun_WhenDead_TransitionsToRunning()
        {
            _gm.StartRun();
            _gm.EndRun();
            _gm.RestartRun();

            Assert.AreEqual(RunState.Running, _gm.CurrentState);
        }

        [Test]
        public void RestartRun_WhenDead_FiresOnGameRestart_NotOnGameStart()
        {
            _gm.StartRun();
            _gm.EndRun();

            bool restartFired = false;
            bool startFired   = false;
            _gm.OnGameRestart += () => restartFired = true;
            _gm.OnGameStart   += () => startFired   = true;

            _gm.RestartRun();

            Assert.IsTrue(restartFired,  "OnGameRestart must fire on Dead -> Running.");
            Assert.IsFalse(startFired,   "OnGameStart must NOT fire on restart — only on first run.");
        }

        [Test]
        public void RestartRun_WhenNotDead_IsNoOp()
        {
            // Still in Idle — RestartRun should do nothing.
            int callCount = 0;
            _gm.OnGameRestart += () => callCount++;

            _gm.RestartRun();

            Assert.AreEqual(RunState.Idle, _gm.CurrentState);
            Assert.AreEqual(0, callCount);
        }

        // ── CollisionHandler.OnPlayerDied wires to EndRun ────────────────────

        // Event wiring is set up in Awake — no game loop needed; convert to plain [Test].
        [Test]
        public void CollisionHandlerOnPlayerDied_WhenRunning_TransitionsToDead()
        {
            _gm.StartRun();
            InvokeEvent(_collision, "OnPlayerDied");
            Assert.AreEqual(RunState.Dead, _gm.CurrentState);
        }

        // ── WorldSpeed escalation ─────────────────────────────────────────────

        [Test]
        public void StartRun_ResetsWorldSpeedToInitial()
        {
            _gm.StartRun();

            Assert.AreEqual(_spawnConfig.initialSpeed, _gm.WorldSpeed, 0.001f,
                "WorldSpeed must equal SpawnConfig.initialSpeed at run start.");
        }

        // Time.deltaTime == 0 when Update is invoked via reflection in edit mode.
        // Advance _distanceAccumulator directly to trigger the threshold logic.
        [Test]
        public void WorldSpeed_IncreasesAfter250mAccumulated()
        {
            _spawnConfig.initialSpeed   = 10f;
            _spawnConfig.speedIncrement = 1f;
            _spawnConfig.maxSpeed       = 20f;

            _gm.StartRun();
            float startSpeed = _gm.WorldSpeed;

            SetField(_gm, "_distanceAccumulator", 251f);
            InvokeUpdate(_gm);

            Assert.Greater(_gm.WorldSpeed, startSpeed,
                "WorldSpeed must increase after the 250m threshold is passed.");
        }

        [Test]
        public void WorldSpeed_NeverExceedsMaxSpeed()
        {
            _spawnConfig.initialSpeed   = 19.9f;
            _spawnConfig.speedIncrement = 5f;   // would reach 24.9 without clamping
            _spawnConfig.maxSpeed       = 20f;

            _gm.StartRun();

            // Repeatedly cross the threshold to drive multiple increments.
            for (int i = 0; i < 10; i++)
            {
                SetField(_gm, "_distanceAccumulator", 251f);
                InvokeUpdate(_gm);
            }

            Assert.LessOrEqual(_gm.WorldSpeed, _spawnConfig.maxSpeed,
                "WorldSpeed must never exceed SpawnConfig.maxSpeed.");
        }

        // ── ResetAllSystems resets WorldSpeed on restart ──────────────────────

        [Test]
        public void RestartRun_ResetsWorldSpeedToInitial()
        {
            _spawnConfig.initialSpeed   = 5f;
            _spawnConfig.maxSpeed       = 20f;

            _gm.StartRun();
            // Artificially bump speed to simulate mid-run escalation.
            SetField(_gm, "_distanceAccumulator", 0f);
            // Set WorldSpeed to a high value directly (private setter — use reflection).
            SetProperty(_gm, "WorldSpeed", 15f);

            _gm.EndRun();
            _gm.RestartRun();

            Assert.AreEqual(_spawnConfig.initialSpeed, _gm.WorldSpeed, 0.001f,
                "WorldSpeed must reset to initialSpeed on restart.");
        }

        // ── PlayerController reset on run start ───────────────────────────────

        [Test]
        public void StartRun_ResetsPlayerToCenter()
        {
            _gm.StartRun();

            Assert.AreEqual(1, _player.CurrentLane,
                "PlayerController.ResetToCenter() must be called on StartRun (lane index 1).");
        }

        [Test]
        public void RestartRun_ResetsPlayerToCenter()
        {
            _gm.StartRun();
            _gm.EndRun();
            _gm.RestartRun();

            Assert.AreEqual(1, _player.CurrentLane);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void Set<T>(T instance, string fieldName, object value)
        {
            typeof(T).GetField(fieldName,
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(instance, value);
        }

        private static void SetField<T>(T instance, string fieldName, object value) =>
            typeof(T).GetField(fieldName,
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(instance, value);

        private static void SetProperty<T>(T instance, string propName, object value)
        {
            typeof(T).GetProperty(propName,
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(instance, value);
        }

        private static void InvokeMethod(MonoBehaviour mb, string methodName) =>
            mb.GetType()
                .GetMethod(methodName,
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(mb, null);

        private static void InvokeUpdate(MonoBehaviour mb) => InvokeMethod(mb, "Update");

        private static void InvokeEvent(object target, string eventName)
        {
            var field = target.GetType().GetField(eventName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            var del = field?.GetValue(target) as System.Delegate;
            del?.DynamicInvoke();
        }
    }
}
