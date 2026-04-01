using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DashAndCollect
{
    /// <summary>
    /// Controls the MainMenu canvas: visible on scene load (Idle state), hidden during gameplay.
    ///
    /// Lifecycle: the MainMenu Canvas must be ACTIVE when the scene loads so that Awake
    /// fires and event subscriptions are established. The menu stays visible until the
    /// player presses Play, then fades out (200ms) and starts the run.
    ///
    /// On GameOver the menu does NOT reappear — the DeathScreen handles retry/quit.
    /// The menu only shows again on a fresh scene load (Idle state).
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private GameManager _gameManager;

        [Header("UI")]
        [SerializeField] private Button _playButton;
        [SerializeField] private Text   _highScoreLabel;

        [Header("Animation")]
        [SerializeField] private CanvasGroup _canvasGroup;

        private Coroutine _activeTransition;

        private void Awake()
        {
            Debug.Assert(_gameManager != null,
                "MainMenuController: _gameManager not assigned in inspector.");

            _playButton?.onClick.AddListener(OnPlay);
            _gameManager.OnGameStart    += HandleGameStart;
            _gameManager.OnReturnToMenu += HandleReturnToMenu;

            // Ensure CanvasGroup exists for fade
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // Show menu, block gameplay input behind it
            SetVisible();
            UpdateHighScore();
        }

        private void OnDestroy()
        {
            _playButton?.onClick.RemoveListener(OnPlay);
            if (_gameManager != null)
            {
                _gameManager.OnGameStart    -= HandleGameStart;
                _gameManager.OnReturnToMenu -= HandleReturnToMenu;
            }
        }

        private void OnPlay()
        {
            _playButton.interactable = false; // prevent double-tap
            _activeTransition = UIAnimator.Stop(this, _activeTransition);
            _activeTransition = StartCoroutine(PlaySequence());
        }

        private IEnumerator PlaySequence()
        {
            // Fade out menu (200ms)
            yield return UIAnimator.FadeOut(_canvasGroup, 0.2f, UIAnimator.EaseOutQuad);

            SetHidden();
            _activeTransition = null;

            // Start the run — transitions Idle → Running, fires OnGameStart
            _gameManager.StartRun();
        }

        private void HandleGameStart()
        {
            // Defensive: ensure menu is hidden if StartRun was called from elsewhere
            if (_canvasGroup.alpha > 0f)
            {
                _activeTransition = UIAnimator.Stop(this, _activeTransition);
                SetHidden();
            }
        }

        private void HandleReturnToMenu()
        {
            UpdateHighScore();
            _playButton.interactable = true;
            _activeTransition = UIAnimator.Stop(this, _activeTransition);
            _activeTransition = StartCoroutine(ShowSequence());
        }

        private IEnumerator ShowSequence()
        {
            _canvasGroup.blocksRaycasts = true;
            yield return UIAnimator.FadeIn(_canvasGroup, 0.2f, UIAnimator.EaseOutQuad);
            _canvasGroup.interactable = true;
            _activeTransition = null;
        }

        private void UpdateHighScore()
        {
            if (_highScoreLabel == null || _gameManager.ScoreManager == null) return;
            int best = _gameManager.ScoreManager.PersonalBest;
            _highScoreLabel.text = best > 0 ? $"HIGH SCORE: {best}" : "HIGH SCORE: 0";
        }

        private void SetVisible()
        {
            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
        }

        private void SetHidden()
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }
    }
}
