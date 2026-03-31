using UnityEngine;

namespace DashAndCollect
{
    /// <summary>
    /// Scrolls a tilemap-based ground layer at world speed and wraps seamlessly.
    ///
    /// Tiles repeat every 1 world unit (16px at 16 PPU), so the wrap snaps
    /// back by exactly 1 unit — completely invisible. This keeps the Grid
    /// position within 1 unit of its start, guaranteeing camera coverage.
    ///
    /// Requires a reference to GameManager for WorldSpeed.
    /// </summary>
    [DefaultExecutionOrder(2)]
    public sealed class GroundScroller : MonoBehaviour
    {
        [Tooltip("Reference to the GameManager for world speed.")]
        [SerializeField] private GameManager _gameManager;

        private float _startY;

        private void Start()
        {
            _startY = transform.position.y;
        }

        private void Update()
        {
            if (_gameManager == null || _gameManager.CurrentState != RunState.Running) return;

            float dy = _gameManager.WorldSpeed * Time.deltaTime;

            var pos = transform.position;
            pos.y -= dy;

            // Wrap every 1 unit: tiles repeat each unit so the snap is invisible.
            // Keeps pos.y within [_startY - 1, _startY], guaranteeing the tilemap
            // always covers the camera with full margin.
            float scrolled = _startY - pos.y;
            if (scrolled >= 1f)
                pos.y += Mathf.Floor(scrolled);

            transform.position = pos;
        }

        /// <summary>
        /// Resets scroll position. Called on game restart.
        /// </summary>
        public void ResetPosition()
        {
            var pos = transform.position;
            pos.y = _startY;
            transform.position = pos;
        }

        /// <summary>
        /// Called by scene bootstrap to inject the GameManager reference.
        /// </summary>
        public void Initialize(GameManager gm)
        {
            _gameManager = gm;
        }
    }
}
