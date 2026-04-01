using System;

namespace DashAndCollect
{
    /// <summary>
    /// Accumulates score, chain counter, meta currency, and personal best (TDD §4.5).
    ///
    /// Plain C# class — not a MonoBehaviour. Owned and instantiated by GameManager.
    ///
    /// Score entry points:
    ///   RegisterPickup(type) — called by CollisionHandler wiring (via GameManager).
    ///   AddDistanceScore(metres) — called by GameManager tick each frame.
    ///
    /// Multiplier:
    ///   SetMultiplier(float) — called by ModifierSystem on Surge start/end.
    ///   Applied to pickup and chain bonus scores; NOT applied to distance score.
    /// </summary>
    public sealed class ScoreManager
    {
        // ── Config ──────────────────────────────────────────────────────────────
        private readonly ScoreConfig _config;

        // ── Events ──────────────────────────────────────────────────────────────
        public event Action<ScoreSnapshot>   OnScoreChanged;
        public event Action<CollectibleType> OnChainCompleted;

        /// <summary>
        /// Fires once per run the first time the current score exceeds the session
        /// personal best. Argument is the new personal best value.
        /// </summary>
        public event Action<int> OnHighScoreBeaten;

        // ── State ───────────────────────────────────────────────────────────────
        private int              _currentScore;
        private int              _chainCount;          // 0–2; fires and resets at 3
        private CollectibleType? _chainType;           // null = no chain in progress
        private int              _coinsEarnedThisRun;
        private int              _personalBest;        // session-only until SaveManager (M4)

        /// <summary>Current session personal best. Read by MainMenuController for high score display.</summary>
        public int PersonalBest => _personalBest;
        private float            _multiplier = 1f;
        private bool             _newPersonalBestFired;

        private bool _initialized;

        // ── Construction ────────────────────────────────────────────────────────

        public ScoreManager(ScoreConfig config = null)
        {
            _config = config;
        }

        // ── Lifecycle ───────────────────────────────────────────────────────────

        public void Initialize()
        {
            _initialized = true;
        }

        public void ResetForNewRun()
        {
            AssertInitialized();
            _currentScore          = 0;
            _chainCount            = 0;
            _chainType             = null;
            _coinsEarnedThisRun    = 0;
            _multiplier            = 1f;
            _newPersonalBestFired  = false;
        }

        // ── Score entry points ──────────────────────────────────────────────────

        /// <summary>
        /// Registers a collectible pickup. Updates score, chain state, and personal best.
        /// Called by CollisionHandler (wired through GameManager) (TDD §4.5).
        /// </summary>
        public void RegisterPickup(CollectibleType type)
        {
            AssertInitialized();

            // Score for the pickup itself.
            AddScore(Scaled(_config?.basePickupScore ?? 10));

            if (type == CollectibleType.Coin)
            {
                // Coins add score but never advance the chain counter (TDD §4.5).
                _coinsEarnedThisRun++;
                NotifyScoreChanged();
                return;
            }

            // Chain logic.
            if (_chainType == type)
            {
                _chainCount++;
            }
            else
            {
                _chainCount = 1;
                _chainType  = type;
            }

            if (_chainCount == 3)
            {
                // Chain complete.
                AddScore(Scaled(_config?.chainBonusScore ?? 50));
                _coinsEarnedThisRun += _config?.coinsPerChain ?? 1;
                _chainCount = 0;
                _chainType  = null;
                NotifyScoreChanged();
                OnChainCompleted?.Invoke(type);
            }
            else
            {
                NotifyScoreChanged();
            }
        }

        /// <summary>
        /// Adds flat distance-based score. Called by GameManager each frame tick.
        /// Distance score is NOT multiplied by the Surge multiplier (TDD §4.5).
        /// </summary>
        public void AddDistanceScore(int metres)
        {
            AssertInitialized();
            if (metres <= 0) return;
            AddScore(metres);
            NotifyScoreChanged();
        }

        // ── Multiplier ──────────────────────────────────────────────────────────

        /// <summary>
        /// Sets the score multiplier for pickup and chain bonus scores.
        /// Called by ModifierSystem on Surge activation (2f) and expiry (1f).
        /// </summary>
        public void SetMultiplier(float multiplier)
        {
            AssertInitialized();
            _multiplier = multiplier > 0f ? multiplier : 1f;
        }

        // ── Snapshot ────────────────────────────────────────────────────────────

        public ScoreSnapshot GetRunSummary()
        {
            AssertInitialized();
            return BuildSnapshot();
        }

        // ── Private helpers ─────────────────────────────────────────────────────

        private void AddScore(int amount)
        {
            if (amount <= 0) return;
            _currentScore += amount;
            CheckPersonalBest();
        }

        private void CheckPersonalBest()
        {
            if (_newPersonalBestFired) return;
            if (_currentScore <= _personalBest) return;

            _personalBest         = _currentScore;
            _newPersonalBestFired = true;
            OnHighScoreBeaten?.Invoke(_personalBest);
        }

        private void NotifyScoreChanged() =>
            OnScoreChanged?.Invoke(BuildSnapshot());

        private ScoreSnapshot BuildSnapshot() => new ScoreSnapshot
        {
            score               = _currentScore,
            chainCount          = _chainCount,
            chainType           = _chainType,
            coinsEarnedThisRun  = _coinsEarnedThisRun,
            personalBest        = _personalBest,
            isNewPersonalBest   = _newPersonalBestFired
        };

        private int Scaled(int base_) => (int)(base_ * _multiplier);

        private void AssertInitialized()
        {
#if UNITY_ASSERTIONS
            if (!_initialized)
                throw new InvalidOperationException(
                    "ScoreManager.Initialize() must be called before use (TDD §4.1).");
#endif
        }
    }
}
