using System;
using UnityEngine;

namespace DashAndCollect
{
    /// <summary>
    /// Owns the run lifecycle state machine and world-speed escalation.
    ///
    /// State machine: Idle -> Running -> Dead -> Running (repeat).
    /// All state changes route through TransitionTo() — never assign CurrentState directly.
    ///
    /// Not a singleton. Other systems receive a reference via Initialize(GameManager) or
    /// serialized inspector field (TDD §4.1).
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        // ── Inspector references ────────────────────────────────────────────────
        [SerializeField] private CollisionHandler _collisionHandler;
        [SerializeField] private SpawnManager     _spawnManager;
        [SerializeField] private PlayerController _playerController;
        [SerializeField] private SpawnConfig      _spawnConfig;
        [SerializeField] private ScoreConfig      _scoreConfig;

        // Optional — AudioManager is a DontDestroyOnLoad singleton; no Assert required.
        // If null, audio is silently skipped (edit-mode tests, headless builds).
        [SerializeField] private AudioManager     _audioManager;

        // Optional — ModifierSystem wires chain events to spawn bias.
        // If null, modifier loop is skipped (edit-mode tests that don't test modifiers).
        [SerializeField] private ModifierSystem   _modifierSystem;

        // Optional — ChainCounterDisplay drives the 3 dot HUD elements.
        // If null, chain display is silently skipped.
        [SerializeField] private ChainCounterDisplay _chainCounterDisplay;

        // Optional — ChainFlash fires a full-screen colour overlay on chain completion.
        [SerializeField] private ChainFlash _chainFlash;

        // Optional — ModifierLabel shows "SPARSE / SHIELD / SURGE" near the chain dots on bias activation.
        [SerializeField] private ModifierLabel _modifierLabel;

        // ── State ───────────────────────────────────────────────────────────────
        public RunState CurrentState { get; private set; }

        /// <summary>
        /// Current world scroll speed (units/s). Starts at SpawnConfig.initialSpeed;
        /// increases by speedIncrement every 250 m up to maxSpeed.
        /// Owned here; read by SpawnManager each frame.
        /// </summary>
        public float WorldSpeed { get; private set; }

        // ── Events ──────────────────────────────────────────────────────────────

        /// <summary>Fires on every state transition. Preferred subscription point.</summary>
        public event Action<RunState> OnRunStateChanged;

        /// <summary>Fires once: Idle -> Running on the very first run.</summary>
        public event Action OnGameStart;

        /// <summary>Fires on every subsequent Dead -> Running transition.</summary>
        public event Action OnGameRestart;

        /// <summary>Fires on Running -> Dead (player died).</summary>
        public event Action OnGameOver;

        // ── Internal ────────────────────────────────────────────────────────────

        // ScoreManager is plain C# — instantiated here, not serialized (TDD §4.5).
        private ScoreManager _scoreManager;

        // Expose so CollisionHandler and ModifierSystem can wire to it after Initialize.
        public ScoreManager ScoreManager  => _scoreManager;
        public SpawnManager SpawnManager  => _spawnManager;

        private float _distanceAccumulator;
        private float _distanceScoreRemainder; // fractional metres carried between frames
        // Set at field-init time; not reset if Awake() is invoked a second time on the same instance
        // (test harness creates a fresh instance per test, so this is safe in practice).
        private bool  _pendingFirstRun = true;

        // ── Unity lifecycle ─────────────────────────────────────────────────────

        private void Awake()
        {
            // Development-build guards: surface missing inspector assignments immediately (TDD §4.1).
            Debug.Assert(_collisionHandler != null, "GameManager: _collisionHandler not assigned in inspector.");
            Debug.Assert(_spawnManager     != null, "GameManager: _spawnManager not assigned in inspector.");
            Debug.Assert(_playerController != null, "GameManager: _playerController not assigned in inspector.");
            Debug.Assert(_spawnConfig      != null, "GameManager: _spawnConfig not assigned in inspector.");
            Debug.Assert(_scoreConfig      != null, "GameManager: _scoreConfig not assigned in inspector.");

            // Initialization order is fixed (TDD §4.1).
            _scoreManager = new ScoreManager(_scoreConfig);
            _scoreManager.Initialize();

            _spawnManager.Initialize(this);
            _playerController.Initialize(this);
            _collisionHandler.Initialize(this);
            _audioManager?.Initialize(this, _playerController, _collisionHandler);
            _modifierSystem?.Initialize(_scoreManager, _spawnManager, this, _collisionHandler);
            _chainCounterDisplay?.Initialize(this);
            _chainFlash?.Initialize(this);
            _modifierLabel?.Initialize(this);

            _collisionHandler.OnPlayerDied          += EndRun;
            _collisionHandler.OnCollectiblePickedUp += _scoreManager.RegisterPickup;

            TransitionTo(RunState.Idle);
        }

        private void Start()
        {
            // M2 grey-box: auto-start the run. M3 will replace this with a title-screen Start button.
            StartRun();
        }

        private void Update()
        {
            if (CurrentState != RunState.Running) return;

            float delta = WorldSpeed * Time.deltaTime;

            // Speed escalation every 250 m.
            _distanceAccumulator += delta;
            if (_distanceAccumulator >= 250f)
            {
                _distanceAccumulator -= 250f;
                WorldSpeed = Mathf.Min(
                    WorldSpeed + _spawnConfig.speedIncrement,
                    _spawnConfig.maxSpeed);
            }

            // Distance score: +1 per metre, accumulated to avoid per-frame float noise.
            _distanceScoreRemainder += delta;
            int metres = (int)_distanceScoreRemainder;
            if (metres > 0)
            {
                _distanceScoreRemainder -= metres;
                _scoreManager.AddDistanceScore(metres);
            }
        }

        private void OnDestroy()
        {
            if (_collisionHandler != null)
            {
                _collisionHandler.OnPlayerDied          -= EndRun;
                _collisionHandler.OnCollectiblePickedUp -= _scoreManager.RegisterPickup;
            }
        }

        // ── Public API ──────────────────────────────────────────────────────────

        /// <summary>Idle -> Running. No-op if not in Idle.</summary>
        public void StartRun()
        {
            if (CurrentState != RunState.Idle) return;
            ResetAllSystems();
            TransitionTo(RunState.Running);
        }

        /// <summary>Running -> Dead. Called by CollisionHandler event; safe to call directly.</summary>
        public void EndRun()
        {
            if (CurrentState != RunState.Running) return;
            TransitionTo(RunState.Dead);
        }

        /// <summary>
        /// Adds a flat speed bonus for Surge modifier. Capped at SpawnConfig.maxSpeed.
        /// Called by ModifierSystem on Surge activation.
        /// </summary>
        public void ApplySurgeSpeedBonus(float bonus)
        {
            WorldSpeed = Mathf.Min(WorldSpeed + bonus, _spawnConfig.maxSpeed);
        }

        /// <summary>
        /// Removes the flat speed bonus added by Surge. Clamped to initialSpeed floor.
        /// Called by ModifierSystem on Surge expiry.
        /// </summary>
        public void RemoveSurgeSpeedBonus(float bonus)
        {
            WorldSpeed = Mathf.Max(WorldSpeed - bonus, _spawnConfig.initialSpeed);
        }

        /// <summary>Dead -> Running. No-op if not in Dead.</summary>
        public void RestartRun()
        {
            if (CurrentState != RunState.Dead) return;
            ResetAllSystems();
            TransitionTo(RunState.Running);
        }

        // ── Private helpers ─────────────────────────────────────────────────────

        /// <summary>
        /// Single choke point for all state transitions.
        /// Fires OnRunStateChanged first, then the convenience event for the specific transition.
        /// </summary>
        private void TransitionTo(RunState next)
        {
            CurrentState = next;
            OnRunStateChanged?.Invoke(next);

            switch (next)
            {
                case RunState.Running when _pendingFirstRun:
                    _pendingFirstRun = false;
                    OnGameStart?.Invoke();
                    break;
                case RunState.Running:
                    OnGameRestart?.Invoke();
                    break;
                case RunState.Dead:
                    OnGameOver?.Invoke();
                    break;
            }
        }

        /// <summary>
        /// Resets all systems to a clean run state.
        /// Must be called before every transition to Running.
        /// </summary>
        private void ResetAllSystems()
        {
            _scoreManager.ResetForNewRun();
            _spawnManager.ResetPool();
            _playerController.ResetToCenter();
            WorldSpeed = _spawnConfig.initialSpeed;
            _distanceAccumulator    = 0f;
            _distanceScoreRemainder = 0f;
        }
    }
}
