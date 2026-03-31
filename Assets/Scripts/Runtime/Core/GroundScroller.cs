using UnityEngine;

namespace DashAndCollect
{
    /// <summary>
    /// Scrolls a tilemap-based ground layer at world speed and wraps seamlessly.
    ///
    /// Attach to a Grid GameObject whose child Tilemap is painted with repeating
    /// road tiles. The tilemap must be at least 2x camera height tall.
    /// The script scrolls the Grid downward and snaps it back when half the
    /// tilemap has scrolled past, producing infinite seamless road.
    ///
    /// Requires a reference to GameManager for WorldSpeed.
    /// </summary>
    public sealed class GroundScroller : MonoBehaviour
    {
        [Tooltip("Reference to the GameManager for world speed.")]
        [SerializeField] private GameManager _gameManager;

        [Tooltip("Total painted height of the tilemap in world units.")]
        [SerializeField] private float _tilemapHeight = 20f;

        private Camera _camera;
        private float _wrapThreshold;

        private void Start()
        {
            _camera = Camera.main;
            // Wrap when we've scrolled half the tilemap height — the other half
            // is still covering the camera so the snap is invisible.
            _wrapThreshold = _tilemapHeight * 0.5f;
        }

        private void Update()
        {
            if (_gameManager == null || _gameManager.CurrentState != RunState.Running) return;

            float dy = _gameManager.WorldSpeed * Time.deltaTime;

            var pos = transform.position;
            pos.y -= dy;

            // Wrap: when we've scrolled down by half the tilemap height,
            // snap back up. Because the tile pattern repeats, this is seamless.
            if (pos.y <= -_wrapThreshold)
                pos.y += _wrapThreshold;

            transform.position = pos;
        }

        /// <summary>
        /// Resets scroll position. Called on game restart.
        /// </summary>
        public void ResetPosition()
        {
            transform.position = Vector3.zero;
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
