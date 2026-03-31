using UnityEngine;

namespace DashAndCollect
{
    /// <summary>
    /// Wires chain-completion events to modifier effects (TDD §5, GDD §6).
    ///
    /// One modifier active at a time. Starting a new chain while one is active
    /// cancels the current modifier immediately before applying the new one.
    ///
    /// Dash  (M3): SetModifierBias(Dash) — reduced obstacle density for 5s.
    /// Shield (M4): CollisionHandler.ActivateShield() — absorbs next hazard; consumed on use or 15s.
    /// Surge  (M4): ScoreManager.SetMultiplier(2) + GameManager.WorldSpeed boost for 8s.
    ///
    /// Testability:
    ///   Tick(float dt) is internal so edit-mode tests can drive timers without Play Mode.
    ///   Initialize() accepts plain C# / MonoBehaviour dependencies — no Find* or singleton use.
    /// </summary>
    public sealed class ModifierSystem : MonoBehaviour
    {
        private const float DashBiasDuration  = 5f;
        private const float ShieldDuration    = 15f;
        private const float SurgeDuration     = 8f;
        private const float SurgeSpeedBonus   = 3f;   // units/s added on top of current WorldSpeed
        private const float SurgeMultiplier   = 2f;

        private ScoreManager     _scoreManager;
        private SpawnManager     _spawnManager;
        private GameManager      _gameManager;
        private CollisionHandler _collisionHandler;

        private ModifierType _activeModifier = ModifierType.None;
        private float        _biasTimer;    // Dash
        private float        _shieldTimer;  // Shield (time-limit fallback)
        private float        _surgeTimer;   // Surge
        private float        _surgeSpeedSnapshot; // WorldSpeed before Surge

        // ── Initialization ──────────────────────────────────────────────────────

        public void Initialize(ScoreManager scoreManager, SpawnManager spawnManager,
                               GameManager gameManager = null, CollisionHandler collisionHandler = null)
        {
            _scoreManager     = scoreManager;
            _spawnManager     = spawnManager;
            _gameManager      = gameManager;
            _collisionHandler = collisionHandler;

            _scoreManager.OnChainCompleted += HandleChainCompleted;

            if (_gameManager != null)
            {
                _gameManager.OnGameOver    += ResetAll;
                _gameManager.OnGameRestart += ResetAll;
            }
        }

        // ── Unity lifecycle ─────────────────────────────────────────────────────

        private void Awake() { }   // lifecycle managed by GameManager

        private void Update()
        {
            if (_activeModifier != ModifierType.None)
                Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            if (_scoreManager != null)
                _scoreManager.OnChainCompleted -= HandleChainCompleted;
            if (_gameManager != null)
            {
                _gameManager.OnGameOver    -= ResetAll;
                _gameManager.OnGameRestart -= ResetAll;
            }
        }

        // ── Internal tick — injectable by edit-mode tests ───────────────────────

        internal void Tick(float dt)
        {
            switch (_activeModifier)
            {
                case ModifierType.Dash:
                    _biasTimer -= dt;
                    if (_biasTimer <= 0f) ExpireDash();
                    break;

                case ModifierType.Shield:
                    _shieldTimer -= dt;
                    if (_shieldTimer <= 0f) ExpireShield();
                    break;

                case ModifierType.Surge:
                    _surgeTimer -= dt;
                    if (_surgeTimer <= 0f) ExpireSurge();
                    break;
            }
        }

        // ── Event handlers ──────────────────────────────────────────────────────

        private void HandleChainCompleted(CollectibleType type)
        {
            if (_gameManager != null && _gameManager.CurrentState != RunState.Running) return;

            // One modifier at a time — cancel current before applying new one.
            if (_activeModifier != ModifierType.None)
                CancelCurrent();

            switch (type)
            {
                case CollectibleType.Dash:   ActivateDash();   break;
                case CollectibleType.Shield: ActivateShield(); break;
                case CollectibleType.Surge:  ActivateSurge();  break;
            }
        }

        // ── Activation helpers ──────────────────────────────────────────────────

        private void ActivateDash()
        {
            _activeModifier = ModifierType.Dash;
            _biasTimer      = DashBiasDuration;
            _spawnManager?.SetModifierBias(ModifierType.Dash);
        }

        private void ActivateShield()
        {
            _activeModifier = ModifierType.Shield;
            _shieldTimer    = ShieldDuration;
            _collisionHandler?.ActivateShield();
        }

        private void ActivateSurge()
        {
            _activeModifier      = ModifierType.Surge;
            _surgeTimer          = SurgeDuration;
            _surgeSpeedSnapshot  = _gameManager?.WorldSpeed ?? 0f;
            _scoreManager?.SetMultiplier(SurgeMultiplier);
            if (_gameManager != null)
                _gameManager.ApplySurgeSpeedBonus(SurgeSpeedBonus);
        }

        // ── Expiry helpers ──────────────────────────────────────────────────────

        private void ExpireDash()
        {
            _biasTimer      = 0f;
            _activeModifier = ModifierType.None;
            _spawnManager?.SetModifierBias(ModifierType.None);
        }

        private void ExpireShield()
        {
            _shieldTimer    = 0f;
            _activeModifier = ModifierType.None;
            // Shield flag on CollisionHandler is self-clearing on use.
            // If still active after time-limit, clear it.
            _collisionHandler?.DeactivateShield();
        }

        private void ExpireSurge()
        {
            _surgeTimer     = 0f;
            _activeModifier = ModifierType.None;
            _scoreManager?.SetMultiplier(1f);
            if (_gameManager != null)
                _gameManager.RemoveSurgeSpeedBonus(SurgeSpeedBonus);
        }

        // ── Cancel current (before applying new modifier) ───────────────────────

        private void CancelCurrent()
        {
            switch (_activeModifier)
            {
                case ModifierType.Dash:   ExpireDash();   break;
                case ModifierType.Shield: ExpireShield(); break;
                case ModifierType.Surge:  ExpireSurge();  break;
            }
        }

        // ── Full reset (game over / restart) ────────────────────────────────────

        private void ResetAll()
        {
            if (_activeModifier != ModifierType.None)
                CancelCurrent();

            _biasTimer    = 0f;
            _shieldTimer  = 0f;
            _surgeTimer   = 0f;
        }

        // ── Internal state accessors (test assertions) ──────────────────────────

        internal ModifierType ActiveModifier => _activeModifier;
        internal float        BiasTimer      => _biasTimer;
        internal float        ShieldTimer    => _shieldTimer;
        internal float        SurgeTimer     => _surgeTimer;
    }
}
