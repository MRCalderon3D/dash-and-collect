using NUnit.Framework;
using UnityEngine;
using DashAndCollect;

namespace DashAndCollect.Tests
{
    /// <summary>
    /// Edit-mode tests for the IInputProvider abstraction layer (TDD §8).
    ///
    /// Test scope:
    ///   TestInputProvider  — default state and mutation contract.
    ///   UnityInputProvider — interface implementation contract (polling behaviour
    ///                        cannot be verified in edit mode; lifecycle is covered).
    ///   PlayerController   — Initialize() accepts IInputProvider; Update() does not
    ///                        throw or break run state when JumpPressed is true.
    ///
    /// SetUp uses the same scene-in-code pattern as other test fixtures.
    ///
    /// Naming: MethodUnderTest_Condition_ExpectedResult
    /// </summary>
    [TestFixture]
    public class InputProviderTests
    {
        // ── Fixtures ──────────────────────────────────────────────────────────

        private GameObject       _root;
        private GameManager      _gm;
        private PlayerController _pc;
        private PlayerConfig     _playerConfig;
        private LaneConfig       _laneConfig;
        private SpawnConfig      _spawnConfig;
        private ScoreConfig      _scoreConfig;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("TestRoot");
            _root.SetActive(false);

            var collision = _root.AddComponent<CollisionHandler>();
            var spawn     = _root.AddComponent<SpawnManager>();
            _pc           = _root.AddComponent<PlayerController>();
            _gm           = _root.AddComponent<GameManager>();

            _laneConfig = ScriptableObject.CreateInstance<LaneConfig>();
            _laneConfig.lanePositions = new float[] { -2f, 0f, 2f };

            _playerConfig = ScriptableObject.CreateInstance<PlayerConfig>();
            _playerConfig.dashDuration     = 0.08f;
            _playerConfig.recoveryDuration = 0.05f;
            _playerConfig.laneConfig       = _laneConfig;

            _spawnConfig = ScriptableObject.CreateInstance<SpawnConfig>();
            _spawnConfig.initialSpeed   = 5f;
            _spawnConfig.speedIncrement = 0.5f;
            _spawnConfig.maxSpeed       = 20f;

            _scoreConfig = ScriptableObject.CreateInstance<ScoreConfig>();
            _scoreConfig.basePickupScore = 10;
            _scoreConfig.chainBonusScore = 50;
            _scoreConfig.coinsPerChain   = 1;

            SetField(_gm,    "_collisionHandler", collision);
            SetField(_gm,    "_spawnManager",     spawn);
            SetField(_gm,    "_playerController", _pc);
            SetField(_gm,    "_spawnConfig",      _spawnConfig);
            SetField(_gm,    "_scoreConfig",      _scoreConfig);
            SetField(_pc,    "_config",           _playerConfig);
            SetField(spawn,  "_config",           _spawnConfig);

            InvokeMethod(_gm, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_laneConfig);
            Object.DestroyImmediate(_playerConfig);
            Object.DestroyImmediate(_spawnConfig);
            Object.DestroyImmediate(_scoreConfig);
        }

        // ── TestInputProvider state ───────────────────────────────────────────

        [Test]
        public void TestInputProvider_JumpPressed_DefaultFalse()
        {
            var provider = new TestInputProvider();

            Assert.IsFalse(provider.JumpPressed,
                "TestInputProvider.JumpPressed must default to false.");
        }

        [Test]
        public void TestInputProvider_SetJumpPressedTrue_ReturnsTrue()
        {
            var provider = new TestInputProvider { JumpPressed = true };

            Assert.IsTrue(provider.JumpPressed,
                "TestInputProvider must return the value that was set.");
        }

        [Test]
        public void TestInputProvider_SetJumpPressedFalse_AfterTrue_ReturnsFalse()
        {
            var provider = new TestInputProvider { JumpPressed = true };
            provider.JumpPressed = false;

            Assert.IsFalse(provider.JumpPressed,
                "TestInputProvider must return false after being reset to false.");
        }

        // ── Interface contracts ───────────────────────────────────────────────

        [Test]
        public void UnityInputProvider_Implements_IInputProvider()
        {
            Assert.IsTrue(
                typeof(IInputProvider).IsAssignableFrom(typeof(UnityInputProvider)),
                "UnityInputProvider must implement IInputProvider.");
        }

        [Test]
        public void TestInputProvider_Implements_IInputProvider()
        {
            Assert.IsTrue(
                typeof(IInputProvider).IsAssignableFrom(typeof(TestInputProvider)),
                "TestInputProvider must implement IInputProvider.");
        }

        // ── PlayerController integration ──────────────────────────────────────

        [Test]
        public void PlayerController_Initialize_WithInputProvider_DoesNotThrow()
        {
            var provider = new TestInputProvider();

            Assert.DoesNotThrow(() => _pc.Initialize(_gm, provider),
                "PlayerController.Initialize must accept an IInputProvider without throwing.");
        }

        [Test]
        public void PlayerController_Initialize_WithNullInputProvider_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _pc.Initialize(_gm, null),
                "PlayerController.Initialize must accept null IInputProvider without throwing.");
        }

        [Test]
        public void PlayerController_WhenRunning_JumpPressed_DoesNotCrashOrEndRun()
        {
            var provider = new TestInputProvider { JumpPressed = true };
            _pc.Initialize(_gm, provider);

            _gm.StartRun();
            // Drive Update() without a player loop — Time.deltaTime is 0 in edit mode.
            InvokeMethod(_pc, "Update");

            Assert.AreEqual(RunState.Running, _gm.CurrentState,
                "JumpPressed must not crash or terminate the run.");
        }

        [Test]
        public void PlayerController_WhenNotRunning_JumpPressed_IsIgnored()
        {
            var provider = new TestInputProvider { JumpPressed = true };
            _pc.Initialize(_gm, provider);

            // State is Idle — Update() must early-exit before reading input.
            InvokeMethod(_pc, "Update");

            Assert.AreEqual(RunState.Idle, _gm.CurrentState,
                "JumpPressed must be ignored when the run state is not Running.");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void SetField<T>(T instance, string fieldName, object value)
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
