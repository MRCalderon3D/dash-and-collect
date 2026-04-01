using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DashAndCollect
{
    /// <summary>
    /// Controls the DeathScreen panel: shows on GameOver, hides on new run, wires buttons.
    ///
    /// Lifecycle: the DeathScreen Canvas must be ACTIVE when the scene loads so that Awake
    /// fires and event subscriptions are established. This controller hides itself in Awake
    /// by zeroing the CanvasGroup alpha and disabling interaction; GameManager events then
    /// drive visibility with animated transitions.
    ///
    /// Transitions (UI Animation Pipeline):
    ///   Appear:  FadeIn overlay (200ms) + ScaleFrom result panel 0.8→1.0 (250ms, EaseOutBack)
    ///   Dismiss: FadeOut overlay (150ms) → restart / quit
    ///
    /// Main Menu: calls Application.Quit() (no-op in editor). Replace with
    /// SceneLoader.LoadMainMenu() in M3 when the main menu scene is added (TDD §3.1).
    /// </summary>
    public sealed class DeathScreenController : MonoBehaviour
    {
        [SerializeField] private GameManager _gameManager;

        [Header("Labels")]
        [SerializeField] private Text _finalScoreLabel;

        [Header("Buttons")]
        [SerializeField] private Button _retryButton;
        [SerializeField] private Button _mainMenuButton;

        [Header("Animation")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Transform   _resultPanel;

        private Coroutine _activeTransition;

        private void Awake()
        {
            Debug.Assert(_gameManager != null, "DeathScreenController: _gameManager not assigned in inspector.");

            _gameManager.OnGameOver      += Show;
            _gameManager.OnGameStart     += Hide;
            _gameManager.OnGameRestart   += Hide;
            _gameManager.OnReturnToMenu  += Hide;

            _retryButton?.onClick.AddListener(OnRetry);
            _mainMenuButton?.onClick.AddListener(OnMainMenu);

            // Ensure CanvasGroup exists for fade transitions
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // Start hidden — alpha 0, non-interactive, but GameObject stays active
            // so Awake/event subscriptions work without re-activation gymnastics.
            SetHidden();
        }

        private void OnDestroy()
        {
            if (_gameManager != null)
            {
                _gameManager.OnGameOver      -= Show;
                _gameManager.OnGameStart     -= Hide;
                _gameManager.OnGameRestart   -= Hide;
                _gameManager.OnReturnToMenu  -= Hide;
            }
            _retryButton?.onClick.RemoveListener(OnRetry);
            _mainMenuButton?.onClick.RemoveListener(OnMainMenu);
        }

        private void Show()
        {
            if (_finalScoreLabel != null)
                _finalScoreLabel.text = _gameManager.ScoreManager.GetRunSummary().score.ToString();

            CancelTransition();
            _activeTransition = StartCoroutine(ShowSequence());
        }

        private void Hide()
        {
            CancelTransition();
            SetHidden();
        }

        private void OnRetry()
        {
            CancelTransition();
            _activeTransition = StartCoroutine(DismissThen(() => _gameManager.RestartRun()));
        }

        private void OnMainMenu()
        {
            CancelTransition();
            _activeTransition = StartCoroutine(DismissThen(() => _gameManager.ReturnToMenu()));
        }

        // ── Animated transitions ──────────────────────────────────────────

        private IEnumerator ShowSequence()
        {
            // Make visible but transparent
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = true;

            if (_resultPanel != null)
                _resultPanel.localScale = new Vector3(0.8f, 0.8f, 1f);

            // Fade in overlay (200ms) and scale result panel (250ms) in parallel
            // Drive both manually so they run on the same coroutine
            float fadeDur  = 0.2f;
            float scaleDur = 0.25f;
            float totalDur = Mathf.Max(fadeDur, scaleDur);

            if (UIAnimator.ReducedMotion)
            {
                _canvasGroup.alpha = 1f;
                if (_resultPanel != null)
                    _resultPanel.localScale = Vector3.one;
            }
            else
            {
                float t = 0f;
                while (t < totalDur)
                {
                    t += Time.unscaledDeltaTime;

                    // Fade
                    if (t <= fadeDur)
                        _canvasGroup.alpha = UIAnimator.EaseOutQuad(Mathf.Clamp01(t / fadeDur));
                    else
                        _canvasGroup.alpha = 1f;

                    // Scale
                    if (_resultPanel != null)
                    {
                        if (t <= scaleDur)
                        {
                            float s = Mathf.LerpUnclamped(0.8f, 1f,
                                UIAnimator.EaseOutBack(Mathf.Clamp01(t / scaleDur)));
                            _resultPanel.localScale = new Vector3(s, s, 1f);
                        }
                        else
                        {
                            _resultPanel.localScale = Vector3.one;
                        }
                    }

                    yield return null;
                }

                _canvasGroup.alpha = 1f;
                if (_resultPanel != null)
                    _resultPanel.localScale = Vector3.one;
            }

            _canvasGroup.interactable = true;
            _activeTransition = null;
        }

        private IEnumerator DismissThen(System.Action onComplete)
        {
            _canvasGroup.interactable = false;

            // Fade out (150ms)
            yield return UIAnimator.FadeOut(_canvasGroup, 0.15f, UIAnimator.EaseOutQuad);

            SetHidden();
            _activeTransition = null;

            onComplete?.Invoke();
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private void SetHidden()
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        private void CancelTransition()
        {
            _activeTransition = UIAnimator.Stop(this, _activeTransition);
        }
    }
}
