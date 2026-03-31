using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using DashAndCollect;

namespace DashAndCollect.Tests
{
    /// <summary>
    /// Unit tests for SpawnManager chunk selection and safety pass (TDD §11, M2 exit criteria).
    ///
    /// Strategy:
    ///   SelectChunk() and ApplySafetyPass() are internal (InternalsVisibleTo).
    ///   Tests call them directly — no camera, no player loop, no ObjectPool needed.
    ///   SpawnConfig and ChunkDefinition are created via ScriptableObject.CreateInstance.
    ///   Chunk GameObjects are built in code with HazardMarker / CoinMarker components.
    ///
    /// Naming: MethodUnderTest_Condition_ExpectedResult
    /// </summary>
    [TestFixture]
    public class SpawnManagerTests
    {
        // ── Fixtures ──────────────────────────────────────────────────────────

        private GameObject   _root;
        private GameManager  _gm;
        private SpawnManager _sm;
        private SpawnConfig  _spawnConfig;
        private ScoreConfig  _scoreConfig;
        private LaneConfig   _laneConfig;
        private PlayerConfig _playerConfig;

        // ChunkDefinition assets created per-test
        private readonly List<ChunkDefinition> _createdDefs = new List<ChunkDefinition>();
        private readonly List<GameObject>      _createdGOs  = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("TestRoot");
            _root.SetActive(false);

            var collision = _root.AddComponent<CollisionHandler>();
            var player    = _root.AddComponent<PlayerController>();
            _sm           = _root.AddComponent<SpawnManager>();
            _gm           = _root.AddComponent<GameManager>();

            _spawnConfig = ScriptableObject.CreateInstance<SpawnConfig>();
            _spawnConfig.initialSpeed    = 5f;
            _spawnConfig.speedIncrement  = 0.5f;
            _spawnConfig.maxSpeed        = 20f;
            _spawnConfig.lookAheadDistance = 15f;
            _spawnConfig.recycleBuffer   = 2f;
            _spawnConfig.poolSizePerChunk = 1;

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

            SetField(_gm,     "_collisionHandler", collision);
            SetField(_gm,     "_spawnManager",     _sm);
            SetField(_gm,     "_playerController", player);
            SetField(_gm,     "_spawnConfig",      _spawnConfig);
            SetField(_gm,     "_scoreConfig",      _scoreConfig);
            SetField(player,  "_config",            _playerConfig);

            // SpawnManager needs its own _config reference
            SetField(_sm, "_config", _spawnConfig);

            InvokeMethod(_gm, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _createdGOs)
                if (go != null) Object.DestroyImmediate(go);
            _createdGOs.Clear();

            foreach (var def in _createdDefs)
                if (def != null) Object.DestroyImmediate(def);
            _createdDefs.Clear();

            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_spawnConfig);
            Object.DestroyImmediate(_scoreConfig);
            Object.DestroyImmediate(_laneConfig);
            Object.DestroyImmediate(_playerConfig);
        }

        // ── SelectChunk: milestone filter ────────────────────────────────────

        [Test]
        public void SelectChunk_WhenOnlyHighMilestoneChunkExists_ReturnsNull()
        {
            var def = MakeDef("High", minMilestone: 9999);
            SetPool(def);
            InitSM();

            var result = _sm.SelectChunk();

            Assert.IsNull(result,
                "No chunk should be selected when none meet the milestone requirement.");
        }

        [Test]
        public void SelectChunk_WhenMilestoneIsMet_ReturnsChunk()
        {
            var def = MakeDef("Zero", minMilestone: 0);
            SetPool(def);
            InitSM();

            var result = _sm.SelectChunk();

            Assert.AreEqual(def, result,
                "A chunk with minDistanceMilestone == 0 must always be eligible.");
        }

        [Test]
        public void SelectChunk_FiltersOutChunksAboveMilestone()
        {
            var eligible   = MakeDef("Eligible",   minMilestone: 0);
            var ineligible = MakeDef("Ineligible", minMilestone: 9999);
            SetPool(eligible, ineligible);
            InitSM();

            // Run 20 random picks — ineligible chunk must never appear.
            for (int i = 0; i < 20; i++)
            {
                var result = _sm.SelectChunk();
                Assert.AreNotEqual(ineligible, result,
                    "Chunk above minDistanceMilestone must not be selected.");
            }
        }

        // ── SelectChunk: modifier bias ────────────────────────────────────────

        [Test]
        public void SelectChunk_DashBias_NeverSelectsDenseChunk_WhenAlternativeExists()
        {
            var sparse = MakeDef("Sparse", minMilestone: 0, ChunkTag.Sparse);
            var dense  = MakeDef("Dense",  minMilestone: 0, ChunkTag.Dense);
            SetPool(sparse, dense);
            InitSM();

            _sm.SetModifierBias(ModifierType.Dash);

            for (int i = 0; i < 30; i++)
            {
                var result = _sm.SelectChunk();
                Assert.AreNotEqual(dense, result,
                    "Dash modifier must suppress Dense-tagged chunks.");
            }
        }

        [Test]
        public void SelectChunk_DashBias_FallsBackToDenseWhenNoAlternative()
        {
            var dense = MakeDef("Dense", minMilestone: 0, ChunkTag.Dense);
            SetPool(dense);
            InitSM();

            _sm.SetModifierBias(ModifierType.Dash);

            var result = _sm.SelectChunk();
            Assert.AreEqual(dense, result,
                "When only Dense chunks are eligible, Dash bias must fall back to them.");
        }

        [Test]
        public void SelectChunk_NoBias_CanSelectDenseChunk()
        {
            var dense = MakeDef("Dense", minMilestone: 0, ChunkTag.Dense);
            SetPool(dense);
            InitSM();

            _sm.SetModifierBias(ModifierType.None);

            var result = _sm.SelectChunk();
            Assert.AreEqual(dense, result,
                "Without modifier bias, Dense chunks must be eligible.");
        }

        // ── Safety pass ───────────────────────────────────────────────────────

        [Test]
        public void ApplySafetyPass_WhenAllLanesBlocked_DisablesCentreHazard()
        {
            InitSM();
            var chunk = MakeAllLaneHazardChunk();

            _sm.ApplySafetyPass(chunk);

            var centreHazard = FindChildWithLane(chunk, 1).GetComponent<HazardMarker>();
            Assert.IsFalse(centreHazard.enabled,
                "Safety pass must disable the centre-lane HazardMarker when all lanes are blocked.");
        }

        [Test]
        public void ApplySafetyPass_WhenAllLanesBlocked_EnablesCoinOnCentreHazardObject()
        {
            InitSM();
            var chunk = MakeAllLaneHazardChunk(addCoinMarkers: true);

            _sm.ApplySafetyPass(chunk);

            var coinMarker = FindChildWithLane(chunk, 1).GetComponent<CoinMarker>();
            Assert.IsTrue(coinMarker.enabled,
                "Safety pass must enable CoinMarker on the centre object when all lanes are blocked.");
        }

        [Test]
        public void ApplySafetyPass_WhenOneTwoLanesBlocked_DoesNotModifyChunk()
        {
            InitSM();
            var chunk = MakeTwoLaneHazardChunk();

            // Capture initial state
            var hazard0 = FindChildWithLane(chunk, 0).GetComponent<HazardMarker>();
            var hazard1 = FindChildWithLane(chunk, 1).GetComponent<HazardMarker>();

            _sm.ApplySafetyPass(chunk);

            Assert.IsTrue(hazard0.enabled, "Lane 0 hazard must remain enabled when not all lanes blocked.");
            Assert.IsTrue(hazard1.enabled, "Lane 1 hazard must remain enabled when not all lanes blocked.");
        }

        [Test]
        public void ApplySafetyPass_WhenNoHazards_DoesNotThrow()
        {
            InitSM();
            var chunk = new GameObject("EmptyChunk");
            _createdGOs.Add(chunk);

            Assert.DoesNotThrow(() => _sm.ApplySafetyPass(chunk),
                "Safety pass must not throw on a chunk with no hazard children.");
        }

        // ── ResetPool ─────────────────────────────────────────────────────────

        [Test]
        public void SetModifierBias_AfterReset_IsNone()
        {
            InitSM();
            _sm.SetModifierBias(ModifierType.Dash);
            _sm.ResetPool();

            // Bias is reset inside ResetPool (TDD §4.3 — reset returns to clean state).
            // Verify by checking SelectChunk still picks Dense after reset with no bias.
            var dense = MakeDef("Dense", minMilestone: 0, ChunkTag.Dense);
            SetPool(dense);

            var result = _sm.SelectChunk();
            Assert.AreEqual(dense, result,
                "After ResetPool, modifier bias must be cleared (Dense selectable again).");
        }

        // ── Helpers: SpawnManager wiring ──────────────────────────────────────

        /// <summary>
        /// Calls Initialize on SpawnManager with a minimal GameManager.
        /// Bypasses ObjectPool construction (no prefabs in these unit tests) by
        /// setting _pools to an empty dictionary via reflection.
        /// </summary>
        private void InitSM()
        {
            // Set _pools to an empty dictionary so BuildPools does not try to Instantiate prefabs.
            var emptyPools = new System.Collections.Generic.Dictionary<
                ChunkDefinition,
                UnityEngine.Pool.ObjectPool<GameObject>>();
            SetField(_sm, "_pools", emptyPools);
            SetField(_sm, "_initialized", true);
            SetField(_sm, "_gameManager", _gm);
        }

        private void SetPool(params ChunkDefinition[] defs)
        {
            _spawnConfig.chunkPool = defs;
        }

        // ── Helpers: chunk / def construction ────────────────────────────────

        private ChunkDefinition MakeDef(string name, int minMilestone = 0,
            params ChunkTag[] tags)
        {
            var def = ScriptableObject.CreateInstance<ChunkDefinition>();
            def.name                   = name;
            def.height                 = 5f;
            def.minDistanceMilestone   = minMilestone;
            def.tags                   = tags.Length > 0 ? tags : System.Array.Empty<ChunkTag>();
            _createdDefs.Add(def);
            return def;
        }

        /// <summary>Chunk with hazards in all 3 lanes.</summary>
        private GameObject MakeAllLaneHazardChunk(bool addCoinMarkers = false)
        {
            var chunk = new GameObject("AllLaneChunk");
            _createdGOs.Add(chunk);
            for (int lane = 0; lane < 3; lane++)
            {
                var child = new GameObject($"Hazard_L{lane}");
                child.transform.SetParent(chunk.transform);
                var h = child.AddComponent<HazardMarker>();
                h.LaneIndex = lane;
                if (addCoinMarkers)
                {
                    var c = child.AddComponent<CoinMarker>();
                    c.enabled = false;
                }
            }
            return chunk;
        }

        /// <summary>Chunk with hazards in lanes 0 and 1 only (lane 2 clear).</summary>
        private GameObject MakeTwoLaneHazardChunk()
        {
            var chunk = new GameObject("TwoLaneChunk");
            _createdGOs.Add(chunk);
            for (int lane = 0; lane < 2; lane++)
            {
                var child = new GameObject($"Hazard_L{lane}");
                child.transform.SetParent(chunk.transform);
                var h = child.AddComponent<HazardMarker>();
                h.LaneIndex = lane;
            }
            return chunk;
        }

        private static Transform FindChildWithLane(GameObject chunk, int lane)
        {
            foreach (Transform child in chunk.transform)
            {
                var h = child.GetComponent<HazardMarker>();
                if (h != null && h.LaneIndex == lane)
                    return child;
            }
            return null;
        }

        // ── Reflection helpers ────────────────────────────────────────────────

        private static void SetField<T>(T instance, string name, object value) =>
            typeof(T).GetField(name,
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public)
                ?.SetValue(instance, value);

        private static void InvokeMethod(MonoBehaviour mb, string methodName) =>
            mb.GetType()
                .GetMethod(methodName,
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(mb, null);
    }
}
