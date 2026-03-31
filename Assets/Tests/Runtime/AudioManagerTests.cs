using NUnit.Framework;
using UnityEngine;
using DashAndCollect;

namespace DashAndCollect.Tests
{
    /// <summary>
    /// Edit-mode tests for AudioManager singleton lifecycle, event wiring, volume
    /// state transitions, null-clip safety, and unsubscription (M2 exit criteria).
    ///
    /// Strategy:
    ///   AudioSource.PlayOneShot cannot be verified in edit mode (no audio hardware).
    ///   Testable contract:
    ///     - Singleton: Instance is set after Awake; duplicate Awake preserves original.
    ///     - Volume: OnGameOver ducks _musicSource.volume; OnGameRestart restores it.
    ///     - Null safety: all handlers are no-ops when clips are null (default in tests).
    ///     - Unsubscription: volume does not change after OnDestroy.
    ///
    ///   AudioManager.Awake() adds two AudioSource components; their state (volume, loop)
    ///   is readable via reflection on the private fields.
    ///
    /// Naming: MethodUnderTest_Condition_ExpectedResult
    /// </summary>
    [TestFixture]
    public class AudioManagerTests
    {
        // ── Fixtures ──────────────────────────────────────────────────────────

        private GameObject       _amRoot;
        private AudioManager     _am;

        private GameObject       _systemRoot;
        private GameManager      _gm;
        private CollisionHandler _collision;
        private PlayerController _player;
        private SpawnManager     _spawn;
        private SpawnConfig      _spawnConfig;
        private ScoreConfig      _scoreConfig;
        private LaneConfig       _laneConfig;
        private PlayerConfig     _playerConfig;

        [SetUp]
        public void SetUp()
        {
            // AudioManager root — inactive so Awake does not fire automatically.
            _amRoot = new GameObject("AudioManager");
            _amRoot.SetActive(false);
            _am = _amRoot.AddComponent<AudioManager>();
            InvokeMethod(_am, "Awake");  // creates AudioSources, sets Instance

            // System root with full GameManager dependency graph.
            _systemRoot = new GameObject("SystemRoot");
            _systemRoot.SetActive(false);

            _collision = _systemRoot.AddComponent<CollisionHandler>();
            _spawn     = _systemRoot.AddComponent<SpawnManager>();
            _player    = _systemRoot.AddComponent<PlayerController>();
            _gm        = _systemRoot.AddComponent<GameManager>();

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

            // Wire AudioManager to the live game systems.
            _am.Initialize(_gm, _player, _collision);
        }

        [TearDown]
        public void TearDown()
        {
            // DestroyImmediate triggers OnDestroy → Instance = null.
            // Guard against null: OnDestroy_ClearsSingletonInstance sets _amRoot = null.
            if (_amRoot != null) Object.DestroyImmediate(_amRoot);
            Object.DestroyImmediate(_systemRoot);
            Object.DestroyImmediate(_spawnConfig);
            Object.DestroyImmediate(_scoreConfig);
            Object.DestroyImmediate(_laneConfig);
            Object.DestroyImmediate(_playerConfig);
        }

        // ── Singleton lifecycle ───────────────────────────────────────────────

        [Test]
        public void Awake_SetsSingletonInstance()
        {
            Assert.IsNotNull(AudioManager.Instance,
                "AudioManager.Instance must be set after Awake.");
            Assert.AreEqual(_am, AudioManager.Instance,
                "AudioManager.Instance must reference the created component.");
        }

        [Test]
        public void Awake_WhenDuplicate_PreservesOriginalInstance()
        {
            var duplicateGO = new GameObject("Duplicate");
            duplicateGO.SetActive(false);
            var duplicate = duplicateGO.AddComponent<AudioManager>();
            InvokeMethod(duplicate, "Awake");

            Assert.AreEqual(_am, AudioManager.Instance,
                "Original Instance must be preserved when a duplicate Awake fires.");

            // In edit mode, Awake() calls DestroyImmediate(gameObject) on the duplicate,
            // so duplicateGO may already be destroyed — guard before cleanup.
            if (duplicateGO != null) Object.DestroyImmediate(duplicateGO);
        }

        [Test]
        public void OnDestroy_ClearsSingletonInstance()
        {
            // DestroyImmediate on an inactive GameObject (Awake called only via reflection)
            // does not guarantee Unity calls OnDestroy. Invoke it directly — this is the
            // same pattern used by all other OnDestroy tests in this suite.
            InvokeMethod(_am, "OnDestroy");

            Assert.IsNull(AudioManager.Instance,
                "AudioManager.Instance must be null after OnDestroy runs.");
        }

        // ── Music volume state ────────────────────────────────────────────────

        [Test]
        public void OnGameStart_SetsMusicVolumeToNominal()
        {
            // Directly duck volume so StartMusic has something to restore against.
            MusicSource().volume = 0f;

            _gm.StartRun();  // fires OnGameStart → StartMusic

            Assert.Greater(MusicSource().volume, 0f,
                "StartMusic must restore music volume to the nominal level.");
        }

        [Test]
        public void OnGameOver_DucksMusicVolume()
        {
            _gm.StartRun();
            float volumeAfterStart = MusicSource().volume;

            _gm.EndRun();   // fires OnGameOver → duck

            Assert.Less(MusicSource().volume, volumeAfterStart,
                "Music volume must be ducked (~−6 dB) when OnGameOver fires.");
        }

        [Test]
        public void OnGameRestart_RestoresMusicVolume()
        {
            _gm.StartRun();
            _gm.EndRun();   // duck
            float duckedVolume = MusicSource().volume;

            _gm.RestartRun();   // fires OnGameRestart → RestartMusic

            Assert.Greater(MusicSource().volume, duckedVolume,
                "Music volume must be restored when OnGameRestart fires.");
        }

        // ── Null-clip safety ──────────────────────────────────────────────────

        [Test]
        public void OnGameOver_WhenDeathClipNull_DoesNotThrow()
        {
            _gm.StartRun();

            Assert.DoesNotThrow(() => _gm.EndRun(),
                "AudioManager must handle a null death SFX clip without throwing.");
        }

        [Test]
        public void OnLaneChanged_WhenDashClipNull_DoesNotThrow()
        {
            _gm.StartRun();
            _player.ResetToCenter();   // ensure we are not at an edge (lane 1)

            Assert.DoesNotThrow(() => _player.ProcessDash(left: false, right: true),
                "AudioManager must handle a null dash SFX clip without throwing.");
        }

        [Test]
        public void OnCollectiblePickedUp_Coin_WhenClipNull_DoesNotThrow()
        {
            _gm.StartRun();
            _collision.SimulateCollectible(CollectibleType.Coin);

            Assert.DoesNotThrow(() => InvokeMethod(_collision, "Update"),
                "AudioManager must handle a null coin SFX clip without throwing.");
        }

        [Test]
        public void OnCollectiblePickedUp_NonCoin_DoesNotThrow()
        {
            _gm.StartRun();
            _collision.SimulateCollectible(CollectibleType.Dash);

            Assert.DoesNotThrow(() => InvokeMethod(_collision, "Update"),
                "AudioManager must not throw for non-Coin collectible types.");
        }

        // ── Unsubscription ────────────────────────────────────────────────────

        [Test]
        public void OnDestroy_UnsubscribesFromGameManagerEvents()
        {
            _gm.StartRun();
            float volumeAfterStart = MusicSource().volume;

            InvokeMethod(_am, "OnDestroy");   // unsubscribe without destroying GO

            _gm.EndRun();   // OnGameOver fires — AudioManager must not respond

            Assert.AreEqual(volumeAfterStart, MusicSource().volume,
                "After OnDestroy, AudioManager must not duck music on GameOver.");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private AudioSource MusicSource() =>
            (AudioSource)typeof(AudioManager)
                .GetField("_musicSource",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(_am);

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
