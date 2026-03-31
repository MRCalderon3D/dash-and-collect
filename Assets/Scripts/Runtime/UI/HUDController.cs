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
    /// </summary>
    public sealed class HUDController : MonoBehaviour
    {
        [SerializeField] private GameManager _gameManager;

        [Header("Labels")]
        [SerializeField] private Text _scoreLabel;
        [SerializeField] private Text _coinLabel;
        [SerializeField] private Text _personalBestLabel;

        private void Awake()
        {
            Debug.Assert(_gameManager != null, "HUDController: _gameManager not assigned in inspector.");
            _gameManager.ScoreManager.OnScoreChanged += HandleScoreChanged;
        }

        private void OnDestroy()
        {
            if (_gameManager?.ScoreManager != null)
                _gameManager.ScoreManager.OnScoreChanged -= HandleScoreChanged;
        }

        private void HandleScoreChanged(ScoreSnapshot s)
        {
            if (_scoreLabel        != null) _scoreLabel.text        = s.score.ToString();
            if (_coinLabel         != null) _coinLabel.text         = s.coinsEarnedThisRun.ToString();
            if (_personalBestLabel != null) _personalBestLabel.text = s.personalBest.ToString();
        }
    }
}
