using UnityEngine;
using UnityEngine.UI;

namespace DashAndCollect
{
    /// <summary>
    /// Controls the DeathScreen panel: shows on GameOver, hides on new run, wires buttons.
    ///
    /// Lifecycle: the DeathScreen Canvas must be ACTIVE when the scene loads so that Awake
    /// fires and event subscriptions are established. This controller immediately calls
    /// gameObject.SetActive(false) in Awake to hide itself; GameManager events then drive
    /// visibility. Do not set the Canvas inactive in the scene — let this controller own it.
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

        private void Awake()
        {
            Debug.Assert(_gameManager != null, "DeathScreenController: _gameManager not assigned in inspector.");

            _gameManager.OnGameOver    += Show;
            _gameManager.OnGameStart   += Hide;
            _gameManager.OnGameRestart += Hide;

            _retryButton?.onClick.AddListener(OnRetry);
            _mainMenuButton?.onClick.AddListener(OnMainMenu);

            // Start hidden — shown only when OnGameOver fires.
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_gameManager != null)
            {
                _gameManager.OnGameOver    -= Show;
                _gameManager.OnGameStart   -= Hide;
                _gameManager.OnGameRestart -= Hide;
            }
            _retryButton?.onClick.RemoveListener(OnRetry);
            _mainMenuButton?.onClick.RemoveListener(OnMainMenu);
        }

        private void Show()
        {
            if (_finalScoreLabel != null)
                _finalScoreLabel.text = _gameManager.ScoreManager.GetRunSummary().score.ToString();
            gameObject.SetActive(true);
        }

        private void Hide() => gameObject.SetActive(false);

        private void OnRetry() => _gameManager.RestartRun();

        private void OnMainMenu()
        {
            // TODO M3: replace with SceneLoader.LoadMainMenu() when main menu scene is added (TDD §3.1).
            Application.Quit();
        }
    }
}
