using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DashAndCollect
{
    /// <summary>
    /// Full-screen overlay flash that fires when a chain is completed.
    /// Colour matches the chain type (same contract as ChainCounterDisplay).
    /// Attach to a high-sort-order Canvas GO; assign _flashImage in Inspector.
    /// </summary>
    public sealed class ChainFlash : MonoBehaviour
    {
        [SerializeField] private Image _flashImage;

        [SerializeField] private float _peakAlpha    = 0.35f;
        [SerializeField] private float _fadeInTime   = 0.05f;
        [SerializeField] private float _fadeOutTime  = 0.30f;

        private static readonly Color CyanColor   = new Color(0.2f, 0.8f, 1.0f);
        private static readonly Color BlueColor   = new Color(0.2f, 0.4f, 1.0f);
        private static readonly Color OrangeColor = new Color(1.0f, 0.6f, 0.1f);
        private static readonly Color GreyColor   = new Color(0.8f, 0.8f, 0.8f);

        private GameManager _gameManager;
        private Coroutine   _activeFlash;

        public void Initialize(GameManager gameManager)
        {
            _gameManager = gameManager;
            _gameManager.ScoreManager.OnChainCompleted += HandleChainCompleted;

            if (_flashImage != null)
            {
                _flashImage.color = new Color(0f, 0f, 0f, 0f);
                _flashImage.raycastTarget = false;
            }
        }

        private void OnDestroy()
        {
            if (_gameManager != null)
                _gameManager.ScoreManager.OnChainCompleted -= HandleChainCompleted;
        }

        private void HandleChainCompleted(CollectibleType type)
        {
            if (_flashImage == null) return;

            if (_activeFlash != null)
                StopCoroutine(_activeFlash);

            _activeFlash = StartCoroutine(Flash(ColorForType(type)));
        }

        private IEnumerator Flash(Color baseColor)
        {
            // Fade in
            float t = 0f;
            while (t < _fadeInTime)
            {
                t += Time.deltaTime;
                float a = Mathf.Lerp(0f, _peakAlpha, t / _fadeInTime);
                _flashImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);
                yield return null;
            }

            // Fade out
            t = 0f;
            while (t < _fadeOutTime)
            {
                t += Time.deltaTime;
                float a = Mathf.Lerp(_peakAlpha, 0f, t / _fadeOutTime);
                _flashImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);
                yield return null;
            }

            _flashImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
            _activeFlash = null;
        }

        private static Color ColorForType(CollectibleType type)
        {
            switch (type)
            {
                case CollectibleType.Dash:   return CyanColor;
                case CollectibleType.Shield: return BlueColor;
                case CollectibleType.Surge:  return OrangeColor;
                default:                     return GreyColor;
            }
        }
    }
}
