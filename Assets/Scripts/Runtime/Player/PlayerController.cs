using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DashAndCollect
{
    /// <summary>
    /// Reads DashLeft / DashRight input, manages 3-lane snap position, and executes
    /// the lateral lerp with a recovery-frame block (TDD §4.2).
    ///
    /// Input approach: manual InputAction.Enable/Disable (not PlayerInput component)
    /// for testability and explicit lifetime control (TDD §8).
    ///
    /// Lane model: 3 discrete lanes, index 0 (left) / 1 (center) / 2 (right).
    /// World-space X positions authored on LaneConfig SO.
    /// </summary>
    // Default execution order (0) — intentionally before CollisionHandler (+10) so input
    // is processed before death dispatch on the same frame (TDD §4.2).
    [DefaultExecutionOrder(0)]
    public sealed class PlayerController : MonoBehaviour
    {
        // ── Inspector ───────────────────────────────────────────────────────────
        [SerializeField] private PlayerConfig      _config;
        [SerializeField] private InputActionAsset  _inputActionAsset;

        // ── Public state ────────────────────────────────────────────────────────
        public int  CurrentLane   { get; private set; } = 1;
        public bool IsInRecovery  { get; private set; }

        public event Action<int> OnLaneChanged;

        // ── Private state ───────────────────────────────────────────────────────
        private GameManager   _gameManager;
        private IInputProvider _inputProvider;
        private InputAction   _dashLeft;
        private InputAction   _dashRight;

        private float _dashTimer;       // counts down; > 0 means lerp in progress
        private float _recoveryTimer;   // counts down; > 0 means input blocked
        private float _dashStartX;
        private float _dashTargetX;
        private int   _queuedDash;      // -1 left, 0 none, +1 right

        private bool  _initialized;

        // ── Initialization ──────────────────────────────────────────────────────

        /// <param name="inputProvider">
        /// Optional IInputProvider for single-button input (tap / click / space).
        /// Pass null to rely solely on the InputActionAsset bindings for directional input.
        /// Pass a TestInputProvider in edit-mode tests.
        /// </param>
        public void Initialize(GameManager gameManager, IInputProvider inputProvider = null)
        {
            Debug.Assert(_config            != null, "PlayerController: _config not assigned in inspector.");
            Debug.Assert(_config?.laneConfig != null, "PlayerController: _config.laneConfig not assigned.");
            _gameManager   = gameManager;
            _inputProvider = inputProvider;
            _initialized   = true;
        }

        /// <summary>
        /// Snaps the player to center lane (index 1), clears all movement and recovery
        /// state, and repositions the Transform immediately (TDD §4.1 reset contract).
        /// </summary>
        public void ResetToCenter()
        {
            AssertInitialized();
            CurrentLane    = 1;
            IsInRecovery   = false;
            _dashTimer     = 0f;
            _recoveryTimer = 0f;
            _queuedDash    = 0;

            if (_config?.laneConfig?.lanePositions != null)
            {
                var pos   = transform.position;
                pos.x     = _config.laneConfig.lanePositions[1];
                transform.position = pos;
                _dashTargetX = pos.x;
                _dashStartX  = pos.x;
            }
        }

        // ── Unity lifecycle ─────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (_inputActionAsset == null) return;
            _dashLeft  = _inputActionAsset.FindAction("Gameplay/DashLeft",  throwIfNotFound: true);
            _dashRight = _inputActionAsset.FindAction("Gameplay/DashRight", throwIfNotFound: true);
            _dashLeft.Enable();
            _dashRight.Enable();
        }

        private void OnDisable()
        {
            _dashLeft?.Disable();
            _dashRight?.Disable();
        }

        private void Update()
        {
            if (!_initialized) return;
            if (_gameManager.CurrentState != RunState.Running) return;

            Tick(Time.deltaTime);

            // ── IInputProvider path (tap / click / touch — single-button devices) ──
            // JumpPressed behavior is mapped here when the mechanic is defined (TDD §8).
            if (_inputProvider != null && _inputProvider.JumpPressed)
            {
                // TODO M3: map JumpPressed to the appropriate game action
                // (e.g. vertical jump, auto-dash) when the mechanic is confirmed.
            }

            // ── InputAction path (keyboard / controller — directional input) ──────
            if (_dashLeft != null && _dashRight != null)
            {
                bool left  = _dashLeft.WasPressedThisFrame();
                bool right = _dashRight.WasPressedThisFrame();
                ProcessDash(left, right);
            }
        }

        /// <summary>
        /// Advances timer state by <paramref name="dt"/> seconds.
        /// Internal so edit-mode unit tests can inject explicit delta time without
        /// requiring the Unity player loop (Time.deltaTime == 0 in edit mode).
        /// </summary>
        internal void Tick(float dt)
        {
            // ── Recovery timer ──────────────────────────────────────────────────
            if (_recoveryTimer > 0f)
            {
                _recoveryTimer -= dt;
                if (_recoveryTimer <= 0f)
                {
                    IsInRecovery = false;
                    if (_queuedDash != 0)
                    {
                        int queued = _queuedDash;
                        _queuedDash = 0;
                        ExecuteDash(queued);
                    }
                }
            }

            // ── Dash lerp ───────────────────────────────────────────────────────
            if (_dashTimer > 0f)
            {
                _dashTimer -= dt;
                float t   = 1f - Mathf.Clamp01(_dashTimer / _config.dashDuration);
                var   pos = transform.position;
                pos.x     = Mathf.Lerp(_dashStartX, _dashTargetX, t);
                transform.position = pos;
            }
        }

        // ── Core dash logic (internal for unit testing via InternalsVisibleTo) ──

        /// <summary>
        /// Processes a pair of directional inputs for the current frame.
        /// Called by Update() with real input; called directly by unit tests.
        ///
        /// Rules (TDD §4.2, GDD §5.1):
        ///   - left &amp;&amp; right simultaneously → neutral, no movement
        ///   - input during recovery → queued for one frame (overwrites previous queue)
        ///   - otherwise → dash in direction if not already at edge
        /// </summary>
        internal void ProcessDash(bool left, bool right)
        {
            // Simultaneous = neutral (GDD §5.1).
            if (left && right) return;

            int direction = left ? -1 : right ? 1 : 0;
            if (direction == 0) return;

            if (IsInRecovery)
            {
                // Queue for the frame recovery expires; overwrite any existing queue.
                _queuedDash = direction;
                return;
            }

            ExecuteDash(direction);
        }

        // ── Private helpers ─────────────────────────────────────────────────────

        private void ExecuteDash(int direction)
        {
            int target = Mathf.Clamp(CurrentLane + direction, 0, 2);
            if (target == CurrentLane) return;  // already at edge — no movement

            _dashStartX  = transform.position.x;
            _dashTargetX = _config.laneConfig.lanePositions[target];
            _dashTimer   = _config.dashDuration;

            CurrentLane    = target;
            IsInRecovery   = true;
            _recoveryTimer = _config.recoveryDuration;
            _queuedDash    = 0;

            OnLaneChanged?.Invoke(CurrentLane);
        }

        private void AssertInitialized()
        {
#if UNITY_ASSERTIONS
            if (!_initialized)
                throw new InvalidOperationException(
                    "PlayerController.Initialize() must be called before use (TDD §4.1).");
#endif
        }
    }
}
