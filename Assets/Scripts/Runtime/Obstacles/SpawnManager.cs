using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace DashAndCollect
{
    /// <summary>
    /// Pools and spawns obstacle/collectible chunks; advances the spawn cursor as the
    /// world scrolls; recycles chunks that pass below the camera (TDD §4.3).
    ///
    /// Spawn trigger: distance-based (cursor vs camera top + lookAhead), not timer-based.
    /// World speed: owned by GameManager, read each frame.
    /// Pool: one ObjectPool&lt;GameObject&gt; per ChunkDefinition, pre-warmed at run start.
    /// </summary>
    // Execution order: after GameManager (0) so WorldSpeed is already updated this frame,
    // before CollisionHandler (10) so chunks are scrolled before overlap queries fire.
    [DefaultExecutionOrder(5)]
    public sealed class SpawnManager : MonoBehaviour
    {
        // ── Inspector ───────────────────────────────────────────────────────────
        [SerializeField] private SpawnConfig _config;
        [SerializeField] private Camera      _camera;   // main camera; null → Camera.main

        // ── Runtime state ───────────────────────────────────────────────────────
        private GameManager   _gameManager;
        private ModifierType  _activeBias = ModifierType.None;

        /// <summary>Current active spawn bias. Internal — readable by ModifierSystemTests.</summary>
        internal ModifierType ActiveBias => _activeBias;

        // Per-definition pools
        private Dictionary<ChunkDefinition, ObjectPool<GameObject>> _pools;

        // Reusable scratch lists for SelectChunk — allocated once, cleared per call (Finding 3.1).
        private readonly List<ChunkDefinition> _eligibleScratch = new List<ChunkDefinition>(16);
        private readonly List<ChunkDefinition> _biasedScratch   = new List<ChunkDefinition>(16);

        // Active chunk tracking: GameObject → its definition (for recycle dispatch)
        private readonly List<ActiveChunk> _activeChunks = new List<ActiveChunk>(32);

        // Spawn cursor: Y position where the next chunk top edge will be placed
        private float _spawnCursorY;

        private bool _initialized;

        // ── Internal bookkeeping ────────────────────────────────────────────────

        private readonly struct ActiveChunk
        {
            public readonly GameObject       Instance;
            public readonly ChunkDefinition  Definition;

            public ActiveChunk(GameObject instance, ChunkDefinition definition)
            {
                Instance   = instance;
                Definition = definition;
            }
        }

        // ── Initialization ──────────────────────────────────────────────────────

        public void Initialize(GameManager gameManager)
        {
            Debug.Assert(_config != null, "SpawnManager: _config not assigned in inspector.");

            _gameManager = gameManager;
            if (_camera == null) _camera = Camera.main;

            BuildPools();
            _initialized = true;
        }

        // ── Public API ──────────────────────────────────────────────────────────

        /// <summary>
        /// Returns all active chunks to their pools and resets the spawn cursor to just
        /// above the camera top edge so the first spawn happens immediately on run start.
        /// </summary>
        public void ResetPool()
        {
            AssertInitialized();

            foreach (var ac in _activeChunks)
                ReturnToPool(ac);
            _activeChunks.Clear();

            _spawnCursorY = CameraTopY() + 0.1f;
            _activeBias   = ModifierType.None;
        }

        /// <summary>
        /// Biases chunk selection based on the active modifier (TDD §4.3, §5).
        /// <see cref="ModifierType.Dash"/> suppresses Dense-tagged chunks.
        /// <see cref="ModifierType.None"/> removes all bias.
        /// </summary>
        public void SetModifierBias(ModifierType modifier)
        {
            AssertInitialized();
            if (_activeBias == modifier) return;
            _activeBias = modifier;
            OnBiasChanged?.Invoke(modifier);
        }

        /// <summary>
        /// Fires when the active spawn bias changes. Subscribed to by UI feedback components.
        /// </summary>
        public event Action<ModifierType> OnBiasChanged;

        // ── Unity lifecycle ─────────────────────────────────────────────────────

        private void Update()
        {
            if (!_initialized) return;
            if (_gameManager.CurrentState != RunState.Running) return;

            ScrollActiveChunks();
            RecycleExpiredChunks();
            TrySpawnNextChunk();
        }

        // ── Scrolling ───────────────────────────────────────────────────────────

        private void ScrollActiveChunks()
        {
            float dy = _gameManager.WorldSpeed * Time.deltaTime;
            foreach (var ac in _activeChunks)
            {
                if (ac.Instance == null) continue;
                var pos = ac.Instance.transform.position;
                pos.y -= dy;
                ac.Instance.transform.position = pos;
            }
            _spawnCursorY -= dy;
        }

        // ── Recycling ───────────────────────────────────────────────────────────

        private void RecycleExpiredChunks()
        {
            float recycleY = CameraBottomY() - _config.recycleBuffer;
            for (int i = _activeChunks.Count - 1; i >= 0; i--)
            {
                var ac = _activeChunks[i];
                if (ac.Instance == null || ac.Instance.transform.position.y < recycleY)
                {
                    ReturnToPool(ac);
                    _activeChunks.RemoveAt(i);
                }
            }
        }

        // ── Spawn ───────────────────────────────────────────────────────────────

        private void TrySpawnNextChunk()
        {
            float spawnThreshold = CameraTopY() + _config.lookAheadDistance;
            if (_spawnCursorY >= spawnThreshold) return;

            ChunkDefinition def = SelectChunk();
            if (def == null) return;

            SpawnChunk(def);
        }

        /// <summary>
        /// Selects the next chunk definition:
        /// 1. Filter by minDistanceMilestone.
        /// 2. Apply modifier bias (Dash suppresses Dense).
        /// 3. Pseudo-random pick.
        /// Falls back to any eligible chunk if the bias filter yields nothing.
        /// </summary>
        internal ChunkDefinition SelectChunk()
        {
            int distance = Mathf.FloorToInt(_gameManager != null
                ? _gameManager.WorldSpeed   // distance proxy — full distance tracking is M4
                : 0f);

            // Step 1: milestone filter — reuse scratch list to avoid per-call heap allocation.
            _eligibleScratch.Clear();
            foreach (var def in _config.chunkPool)
            {
                if (def != null && def.minDistanceMilestone <= distance)
                    _eligibleScratch.Add(def);
            }
            if (_eligibleScratch.Count == 0) return null;

            // Step 2: modifier bias — reuse second scratch list.
            List<ChunkDefinition> candidates = _eligibleScratch;
            if (_activeBias == ModifierType.Dash)
            {
                _biasedScratch.Clear();
                foreach (var def in _eligibleScratch)
                    if (!def.HasTag(ChunkTag.Dense))
                        _biasedScratch.Add(def);
                if (_biasedScratch.Count > 0)
                    candidates = _biasedScratch;
                // else fall back to full eligible set
            }

            // Step 3: pseudo-random pick
            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        private void SpawnChunk(ChunkDefinition def)
        {
            if (!_pools.TryGetValue(def, out var pool)) return;

            var instance = pool.Get();
            instance.transform.position = new Vector3(0f, _spawnCursorY, 0f);
            instance.SetActive(true);

            // Re-activate any children that were deactivated on pickup (collectibles).
            // The pool only reactivates the root; children need explicit reset.
            foreach (Transform child in instance.transform)
                child.gameObject.SetActive(true);

            ApplySafetyPass(instance);

            _activeChunks.Add(new ActiveChunk(instance, def));
            _spawnCursorY += def.height;
        }

        // ── Safety pass (TDD §4.3) ──────────────────────────────────────────────

        /// <summary>
        /// If a chunk blocks all three lanes with hazards, replaces the centre-lane
        /// hazard with a coin collectible so that an unblocked path always exists.
        ///
        /// Hazards are identified by the <see cref="HazardMarker"/> component.
        /// Coin replacement is performed by the <see cref="CoinMarker"/> component.
        ///
        /// Authoring contract: <see cref="HazardMarker"/> and <see cref="CoinMarker"/>
        /// components must be on <em>direct children</em> of the chunk prefab root.
        /// Deeper nesting is not scanned.
        /// </summary>
        internal void ApplySafetyPass(GameObject chunkInstance)
        {
            var hazardsByLane = new bool[3];
            var centreHazard  = (GameObject)null;

            foreach (Transform child in chunkInstance.transform)
            {
                var hazard = child.GetComponent<HazardMarker>();
                if (hazard == null) continue;

                int lane = hazard.LaneIndex;
                if (lane >= 0 && lane <= 2)
                {
                    hazardsByLane[lane] = true;
                    if (lane == 1)
                        centreHazard = child.gameObject;
                }
            }

            bool allBlocked = hazardsByLane[0] && hazardsByLane[1] && hazardsByLane[2];
            if (!allBlocked || centreHazard == null) return;

            // Disable the hazard marker, enable (or swap to) the coin marker.
            centreHazard.GetComponent<HazardMarker>().enabled = false;
            var coin = centreHazard.GetComponent<CoinMarker>();
            if (coin != null) coin.enabled = true;
        }

        // ── Pool management ─────────────────────────────────────────────────────

        private void BuildPools()
        {
            _pools = new Dictionary<ChunkDefinition, ObjectPool<GameObject>>(
                _config.chunkPool.Length);

            foreach (var def in _config.chunkPool)
            {
                if (def == null || def.prefab == null) continue;
                var captured = def;

                var pool = new ObjectPool<GameObject>(
                    createFunc:    () => Instantiate(captured.prefab),
                    actionOnGet:   go => go.SetActive(true),
                    actionOnRelease: go => go.SetActive(false),
                    actionOnDestroy: go => Destroy(go),
                    collectionCheck: false,
                    defaultCapacity: _config.poolSizePerChunk,
                    maxSize: _config.poolSizePerChunk * 4);

                // Pre-warm
                var prewarm = new GameObject[_config.poolSizePerChunk];
                for (int i = 0; i < _config.poolSizePerChunk; i++)
                    prewarm[i] = pool.Get();
                for (int i = 0; i < _config.poolSizePerChunk; i++)
                    pool.Release(prewarm[i]);

                _pools[def] = pool;
            }
        }

        private void ReturnToPool(ActiveChunk ac)
        {
            if (ac.Instance == null) return;
            if (_pools.TryGetValue(ac.Definition, out var pool))
                pool.Release(ac.Instance);
            else
                ac.Instance.SetActive(false);
        }

        // ── Camera helpers ──────────────────────────────────────────────────────

        private float CameraTopY() =>
            _camera != null
                ? _camera.transform.position.y + _camera.orthographicSize
                : 0f;

        private float CameraBottomY() =>
            _camera != null
                ? _camera.transform.position.y - _camera.orthographicSize
                : 0f;

        // ── Guard ───────────────────────────────────────────────────────────────

        private void AssertInitialized()
        {
#if UNITY_ASSERTIONS
            if (!_initialized)
                throw new InvalidOperationException(
                    "SpawnManager.Initialize() must be called before use (TDD §4.1).");
#endif
        }
    }
}
