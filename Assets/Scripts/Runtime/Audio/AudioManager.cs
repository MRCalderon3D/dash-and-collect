using System;
using UnityEngine;

namespace DashAndCollect
{
    /// <summary>
    /// Owns all audio playback. Singleton with DontDestroyOnLoad for cross-scene
    /// persistence (relevant from M3 onward when the main-menu scene is added).
    ///
    /// Singleton scope (per playmode-architecture rules):
    ///   - Exactly one instance exists for the lifetime of the application.
    ///   - A second Awake destroys the duplicate immediately and returns.
    ///   - No Find* APIs are used. All event subscriptions are wired explicitly
    ///     via Initialize() — AudioManager is never a general service locator.
    ///
    /// Testability:
    ///   In edit-mode tests, call InvokeMethod(am, "Awake") then am.Initialize(gm, pc, ch).
    ///   All clips are null in tests — PlaySfx and music methods are null-safe.
    ///   Reset AudioManager.Instance between tests by destroying the GameObject
    ///   (OnDestroy clears the static reference automatically).
    ///
    /// Event wiring:
    ///   GameManager.OnGameStart   → StartMusic (fade in, calm layer)
    ///   GameManager.OnGameRestart → RestartMusic (restart from calm layer)
    ///   GameManager.OnGameOver    → play sfx_death, duck BGM −6 dB
    ///   PlayerController.OnLaneChanged → play sfx_dash
    ///   CollisionHandler.OnCollectiblePickedUp (Coin) → play sfx_coin_collect
    /// </summary>
    [DefaultExecutionOrder(-10)]  // before GameManager(0) so Awake is safe to call first
    public sealed class AudioManager : MonoBehaviour
    {
        // ── Singleton ───────────────────────────────────────────────────────────
        public static AudioManager Instance { get; private set; }

        // ── Inspector — SFX clips ───────────────────────────────────────────────
        [Header("SFX Clips")]
        [SerializeField] private AudioClip _sfxDash;        // quick whoosh (lane change)
        [SerializeField] private AudioClip _sfxCoinCollect; // bright ding
        [SerializeField] private AudioClip _sfxDeath;       // impact crunch

        // ── Inspector — Music ───────────────────────────────────────────────────
        [Header("Music")]
        [SerializeField] private AudioClip _bgmLoop;        // lo-fi chiptune loop

        // ── Inspector — Volume ──────────────────────────────────────────────────
        [Header("Volume")]
        [SerializeField] [Range(0f, 1f)] private float _sfxVolume   = 1.0f;
        [SerializeField] [Range(0f, 1f)] private float _musicVolume = 0.7f;

        // ── Runtime sources ─────────────────────────────────────────────────────
        private AudioSource _sfxSource;
        private AudioSource _musicSource;

        // ── Wired dependencies ──────────────────────────────────────────────────
        private GameManager      _gameManager;
        private PlayerController _playerController;
        private CollisionHandler _collisionHandler;

        // Stored delegates for correct unsubscription (lambdas are not reference-equal
        // across separate invocations, so they must be captured once).
        private Action<int>             _onLaneChangedHandler;
        private Action<CollectibleType> _onCollectiblePickedUpHandler;

        // ── Unity lifecycle ─────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // Destroy is play-mode only; use DestroyImmediate in edit-mode (test runner).
                if (Application.isPlaying)
                    Destroy(gameObject);
                else
                    DestroyImmediate(gameObject);
                return;
            }
            Instance = this;
            // DontDestroyOnLoad is play-mode only; skip in edit-mode (e.g. test runner).
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);

            _sfxSource             = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
            _sfxSource.volume      = _sfxVolume;

            _musicSource             = gameObject.AddComponent<AudioSource>();
            _musicSource.playOnAwake = false;
            _musicSource.loop        = true;
            _musicSource.volume      = _musicVolume;
        }

        private void OnDestroy()
        {
            // Use ReferenceEquals to bypass Unity's operator== overload, which compares
            // native instance IDs and is unreliable after the native object is destroyed.
            if (ReferenceEquals(Instance, this)) Instance = null;

            if (_gameManager != null)
            {
                _gameManager.OnGameStart   -= StartMusic;
                _gameManager.OnGameRestart -= RestartMusic;
                _gameManager.OnGameOver    -= OnGameOver;
            }
            if (_playerController != null && _onLaneChangedHandler != null)
                _playerController.OnLaneChanged -= _onLaneChangedHandler;
            if (_collisionHandler != null && _onCollectiblePickedUpHandler != null)
                _collisionHandler.OnCollectiblePickedUp -= _onCollectiblePickedUpHandler;
        }

        // ── Initialization ──────────────────────────────────────────────────────

        /// <summary>
        /// Wires AudioManager to the gameplay event sources.
        /// Called by GameManager.Awake() after its own systems are ready.
        /// All parameters accept null — pass null to skip the corresponding subscriptions
        /// (e.g. in edit-mode tests where only a subset of systems is under test).
        /// </summary>
        public void Initialize(GameManager gm, PlayerController pc, CollisionHandler ch)
        {
            _gameManager      = gm;
            _playerController = pc;
            _collisionHandler = ch;

            if (gm != null)
            {
                gm.OnGameStart   += StartMusic;
                gm.OnGameRestart += RestartMusic;
                gm.OnGameOver    += OnGameOver;
            }

            if (pc != null)
            {
                _onLaneChangedHandler = _ => PlaySfx(_sfxDash);
                pc.OnLaneChanged += _onLaneChangedHandler;
            }

            if (ch != null)
            {
                _onCollectiblePickedUpHandler = HandleCollectiblePickedUp;
                ch.OnCollectiblePickedUp += _onCollectiblePickedUpHandler;
            }
        }

        // ── Event handlers ──────────────────────────────────────────────────────

        private void StartMusic()
        {
            if (_musicSource == null) return;
            _musicSource.volume = _musicVolume;
            if (_bgmLoop != null && !_musicSource.isPlaying)
            {
                _musicSource.clip = _bgmLoop;
                _musicSource.Play();
            }
        }

        private void RestartMusic()
        {
            if (_musicSource == null) return;
            _musicSource.volume = _musicVolume;  // restore from any duck
            if (_bgmLoop != null)
            {
                _musicSource.clip = _bgmLoop;
                _musicSource.Play();  // always restart from the top (calm layer)
            }
        }

        private void OnGameOver()
        {
            PlaySfx(_sfxDeath);
            // Duck BGM by ~−6 dB (audio bible §6: must-hear category 1 = death SFX).
            if (_musicSource != null)
                _musicSource.volume = _musicVolume * 0.4f;
        }

        private void HandleCollectiblePickedUp(CollectibleType type)
        {
            if (type == CollectibleType.Coin)
                PlaySfx(_sfxCoinCollect);
        }

        // ── Playback helpers ────────────────────────────────────────────────────

        private void PlaySfx(AudioClip clip)
        {
            if (clip == null || _sfxSource == null) return;
            _sfxSource.PlayOneShot(clip, _sfxVolume);
        }
    }
}
