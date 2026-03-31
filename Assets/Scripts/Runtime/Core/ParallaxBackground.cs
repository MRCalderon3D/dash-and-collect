using UnityEngine;

namespace DashAndCollect
{
    /// <summary>
    /// Scrolls and vertically tiles a background sprite layer relative to world speed.
    /// Each layer has a parallax factor (0 = static, 1 = matches world scroll).
    ///
    /// Attach to a GameObject with a SpriteRenderer. The script clones the sprite
    /// into a second tile above the first so the seam is never visible, then wraps
    /// both tiles as they scroll past the camera.
    ///
    /// Requires a reference to GameManager for WorldSpeed.
    /// </summary>
    public sealed class ParallaxBackground : MonoBehaviour
    {
        [Tooltip("0 = static sky, 1 = matches world speed (road surface).")]
        [Range(0f, 1f)]
        [SerializeField] private float _parallaxFactor = 0.3f;

        [Tooltip("Reference to the GameManager for world speed. Wired by scene bootstrap.")]
        [SerializeField] private GameManager _gameManager;

        private Transform _tileA;
        private Transform _tileB;
        private float _spriteHeight;
        private Camera _camera;

        private void Start()
        {
            _camera = Camera.main;

            var sr = GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null) return;

            _spriteHeight = sr.sprite.bounds.size.y;

            // This object is tile A.
            _tileA = transform;

            // Create tile B as a sibling clone, offset one sprite-height above.
            var clone = new GameObject($"{name}_TileB");
            clone.transform.SetParent(transform.parent);
            clone.transform.position = transform.position + Vector3.up * _spriteHeight;
            clone.transform.localScale = transform.localScale;
            var cloneSR = clone.AddComponent<SpriteRenderer>();
            cloneSR.sprite = sr.sprite;
            cloneSR.sortingLayerID = sr.sortingLayerID;
            cloneSR.sortingOrder = sr.sortingOrder;
            cloneSR.color = sr.color;
            _tileB = clone.transform;
        }

        private void Update()
        {
            if (_gameManager == null || _gameManager.CurrentState != RunState.Running) return;

            float dy = _gameManager.WorldSpeed * _parallaxFactor * Time.deltaTime;

            // Scroll both tiles downward.
            var posA = _tileA.position;
            var posB = _tileB.position;
            posA.y -= dy;
            posB.y -= dy;

            // Wrap: if a tile scrolls below camera bottom by one full height, move it above the other.
            float camBottom = _camera.transform.position.y - _camera.orthographicSize;
            if (posA.y + _spriteHeight < camBottom)
                posA.y = posB.y + _spriteHeight;
            if (posB.y + _spriteHeight < camBottom)
                posB.y = posA.y + _spriteHeight;

            _tileA.position = posA;
            _tileB.position = posB;
        }

        /// <summary>
        /// Called by scene bootstrap or GameManager to inject the reference.
        /// </summary>
        public void Initialize(GameManager gm)
        {
            _gameManager = gm;
        }

        /// <summary>
        /// Resets both tiles to their starting positions. Called on restart.
        /// </summary>
        public void ResetPosition()
        {
            if (_tileA == null || _tileB == null) return;
            var basePos = _tileA.position;
            basePos.y = transform.parent != null ? transform.parent.position.y : 0f;
            _tileA.position = basePos;
            _tileB.position = basePos + Vector3.up * _spriteHeight;
        }
    }
}
