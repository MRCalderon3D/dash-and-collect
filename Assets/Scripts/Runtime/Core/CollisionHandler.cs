using System;
using System.Collections.Generic;
using UnityEngine;

namespace DashAndCollect
{
    /// <summary>
    /// Detects player overlap with collectibles and hazards; dispatches typed events.
    ///
    /// Detection strategy (TDD §4.4):
    ///   OnTriggerEnter2D buffers events into _triggerBuffer each physics step.
    ///   Update() processes the buffer once per frame: collectibles first, then hazards.
    ///   This guarantees deterministic ordering regardless of physics callback order.
    ///
    /// Shield: ActivateShield() sets a flag; the next hazard is absorbed and the flag cleared.
    /// State guard: triggers are only buffered (and simulate helpers only inject) when Running.
    /// </summary>
    // Positive execution order ensures Update() runs after default-order MonoBehaviours,
    // i.e. after physics callbacks have populated the trigger buffer (TDD §4.4).
    [DefaultExecutionOrder(10)]
    public sealed class CollisionHandler : MonoBehaviour
    {
        // ── Events ──────────────────────────────────────────────────────────────
        public event Action<CollectibleType> OnCollectiblePickedUp;
        public event Action                  OnPlayerDied;

        // ── State ───────────────────────────────────────────────────────────────
        private GameManager _gameManager;
        private bool        _initialized;
        private bool        _shieldActive;

        // ── Trigger buffer ──────────────────────────────────────────────────────
        private readonly List<TriggerInfo> _triggerBuffer = new List<TriggerInfo>();

        private struct TriggerInfo
        {
            public bool            IsHazard;
            public CollectibleType Type;       // only meaningful when IsHazard == false
            public GameObject      SourceObject; // the collectible GameObject to deactivate on pickup
        }

        // ── Initialization ──────────────────────────────────────────────────────

        public void Initialize(GameManager gameManager)
        {
            _gameManager = gameManager;
            _initialized = true;
        }

        // ── Public API ──────────────────────────────────────────────────────────

        /// <summary>
        /// Activates the shield. The next hazard overlap is absorbed without firing OnPlayerDied.
        /// Called by ModifierSystem on 3× Shield chain completion (TDD §5).
        /// </summary>
        public void ActivateShield() => _shieldActive = true;

        /// <summary>
        /// Deactivates the shield without consuming it on a hit.
        /// Called by ModifierSystem when the 15s time-limit expires (TDD §5).
        /// </summary>
        public void DeactivateShield() => _shieldActive = false;

        // ── Unity lifecycle ─────────────────────────────────────────────────────

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_initialized || _gameManager.CurrentState != RunState.Running) return;

            var hazard = other.GetComponent<Hazard>();
            if (hazard != null)
            {
                _triggerBuffer.Add(new TriggerInfo { IsHazard = true });
                return;
            }

            var collectible = other.GetComponent<Collectible>();
            if (collectible != null)
                _triggerBuffer.Add(new TriggerInfo { IsHazard = false, Type = collectible.Type, SourceObject = other.gameObject });
        }

        private void Update()
        {
            // Defence-in-depth: reject buffered items if state has changed since they were queued.
            if (!_initialized || _gameManager.CurrentState != RunState.Running) { _triggerBuffer.Clear(); return; }
            if (_triggerBuffer.Count == 0) return;

            // Pass 1 — collectibles (TDD §4.4 priority rule: collectibles before hazards).
            foreach (var info in _triggerBuffer)
            {
                if (!info.IsHazard)
                {
                    OnCollectiblePickedUp?.Invoke(info.Type);
                    // Deactivate the collectible so it disappears on pickup.
                    // The chunk pool will re-activate it when the chunk is reused.
                    if (info.SourceObject != null)
                        info.SourceObject.SetActive(false);
                }
            }

            // Pass 2 — hazards (only the first hazard per frame matters).
            foreach (var info in _triggerBuffer)
            {
                if (info.IsHazard)
                {
                    if (_shieldActive)
                        _shieldActive = false;  // shield absorbs; consumed immediately
                    else
                        OnPlayerDied?.Invoke();
                    break;  // one hazard outcome per frame
                }
            }

            _triggerBuffer.Clear();
        }

        private void OnDestroy()
        {
            _triggerBuffer.Clear();
        }

        // ── Internal test helpers ────────────────────────────────────────────────
        // Bypass OnTriggerEnter2D for edit-mode tests (no physics loop).
        // Honour the same state guard as the production path.

        internal void SimulateCollectible(CollectibleType type)
        {
            if (!_initialized || _gameManager.CurrentState != RunState.Running) return;
            _triggerBuffer.Add(new TriggerInfo { IsHazard = false, Type = type });
        }

        internal void SimulateHazard()
        {
            if (!_initialized || _gameManager.CurrentState != RunState.Running) return;
            _triggerBuffer.Add(new TriggerInfo { IsHazard = true });
        }
    }
}
