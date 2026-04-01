using UnityEngine;
using UnityEngine.UI;

namespace DashAndCollect
{
    /// <summary>
    /// Drives the HUD score labels from ScoreManager events. Purely reactive — no polling.
    ///
    /// Lifecycle: this MonoBehaviour must be on an active GameObject when the scene loads
    /// so that Awake fires and subscribes to ScoreManager.OnScoreChanged.
    /// The GameManager reference must be assigned in the inspector before Awake runs.
    ///
    /// Labels are optional — any null reference is silently skipped.
    ///
    /// Feedback (UI Animation Pipeline):
    ///   Score change: PunchScale score text (100ms)
    ///   Coin collect: PunchScale coin icon + brief yellow flash on label (150ms)
    /// </summary>
    public sealed class HUDController : MonoBehaviour
    {
        [SerializeField] private GameManager _gameManager;

        [Header("Labels")]
        [SerializeField] private Text _scoreLabel;
        [SerializeField] private Text _coinLabel;
        [SerializeField] private Text _personalBestLabel;

        [Header("Feedback")]
        [SerializeField] private Transform _coinIcon;

        private CanvasGroup _canvasGroup;
        private int  _lastScore;
        private int  _lastCoins;
        private Coroutine _scorePunch;
        private Coroutine _coinPunch;
        private Coroutine _coinFlash;

        // ART-BIBLE §9.1 — Accent Coin yellow for flash
        private static readonly Color CoinFlashColor = new Color(1f, 0.93f, 0.35f); // #FFEE58
        private Color _coinLabelBaseColor;

        private void Awake()
        {
            Debug.Assert(_gameManager != null, "HUDController: _gameManager not assigned in inspector.");
            _gameManager.ScoreManager.OnScoreChanged += HandleScoreChanged;
            _gameManager.OnGameStart   += ShowHUD;
            _gameManager.OnGameRestart += ShowHUD;
            _gameManager.OnGameOver    += HideHUD;
            _gameManager.OnReturnToMenu += HideHUD;

            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // Start hidden — shown when gameplay begins
            HideHUD();

            if (_coinLabel != null)
                _coinLabelBaseColor = _coinLabel.color;
        }

        private void OnDestroy()
        {
            if (_gameManager?.ScoreManager != null)
                _gameManager.ScoreManager.OnScoreChanged -= HandleScoreChanged;
            if (_gameManager != null)
            {
                _gameManager.OnGameStart    -= ShowHUD;
                _gameManager.OnGameRestart  -= ShowHUD;
                _gameManager.OnGameOver     -= HideHUD;
                _gameManager.OnReturnToMenu -= HideHUD;
            }
        }

        private void ShowHUD()
        {
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = false; // HUD is display-only, never blocks game input
        }

        private void HideHUD()
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
        }

        private void HandleScoreChanged(ScoreSnapshot s)
        {
            // Update text
            if (_scoreLabel        != null) _scoreLabel.text        = s.score.ToString();
            if (_coinLabel         != null) _coinLabel.text         = s.coinsEarnedThisRun.ToString();
            if (_personalBestLabel != null) _personalBestLabel.text = s.personalBest.ToString();

            // Score change feedback — PunchScale 100ms
            if (s.score != _lastScore && _scoreLabel != null)
            {
                _scorePunch = UIAnimator.Stop(this, _scorePunch);
                _scorePunch = StartCoroutine(UIAnimator.PunchScale(
                    _scoreLabel.transform, 1.15f, 0.1f));
            }

            // Coin collect feedback — PunchScale icon (150ms) + yellow flash on label
            if (s.coinsEarnedThisRun != _lastCoins)
            {
                if (_coinIcon != null)
                {
                    _coinPunch = UIAnimator.Stop(this, _coinPunch);
                    _coinPunch = StartCoroutine(UIAnimator.PunchScale(
                        _coinIcon, 1.25f, 0.15f));
                }

                if (_coinLabel != null)
                {
                    _coinFlash = UIAnimator.Stop(this, _coinFlash);
                    _coinFlash = StartCoroutine(CoinLabelFlash());
                }
            }

            _lastScore = s.score;
            _lastCoins = s.coinsEarnedThisRun;
        }

        private System.Collections.IEnumerator CoinLabelFlash()
        {
            if (_coinLabel == null) yield break;

            if (UIAnimator.ReducedMotion)
            {
                _coinLabel.color = _coinLabelBaseColor;
                _coinFlash = null;
                yield break;
            }

            // Flash to coin yellow
            _coinLabel.color = CoinFlashColor;

            // Fade back over 150ms
            float duration = 0.15f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float p = UIAnimator.EaseOutQuad(Mathf.Clamp01(t / duration));
                _coinLabel.color = Color.Lerp(CoinFlashColor, _coinLabelBaseColor, p);
                yield return null;
            }

            _coinLabel.color = _coinLabelBaseColor;
            _coinFlash = null;
        }
    }
}
