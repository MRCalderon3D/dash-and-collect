using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DashAndCollect
{
    /// <summary>
    /// Displays a brief label near the chain dots when a modifier bias activates.
    /// Addresses P4 playtest finding: players observe the density change but don't
    /// connect it to their collection action.
    ///
    /// Assign _label (Text) in Inspector. Wire via GameManager.Initialize.
    /// </summary>
    public sealed class ModifierLabel : MonoBehaviour
    {
        [SerializeField] private Text  _label;
        [SerializeField] private float _holdTime   = 1.0f;
        [SerializeField] private float _fadeTime   = 0.4f;

        private GameManager _gameManager;
        private Coroutine   _active;

        public void Initialize(GameManager gameManager)
        {
            _gameManager = gameManager;
            _gameManager.SpawnManager.OnBiasChanged += HandleBiasChanged;

            if (_label != null)
                _label.color = new Color(_label.color.r, _label.color.g, _label.color.b, 0f);
        }

        private void OnDestroy()
        {
            if (_gameManager != null)
                _gameManager.SpawnManager.OnBiasChanged -= HandleBiasChanged;
        }

        private void HandleBiasChanged(ModifierType modifier)
        {
            if (_label == null) return;

            if (modifier == ModifierType.None)
            {
                if (_active != null) StopCoroutine(_active);
                _label.color = new Color(_label.color.r, _label.color.g, _label.color.b, 0f);
                return;
            }

            _label.text = LabelForModifier(modifier);
            _label.color = new Color(ColorForModifier(modifier).r,
                                     ColorForModifier(modifier).g,
                                     ColorForModifier(modifier).b, 1f);

            if (_active != null) StopCoroutine(_active);
            _active = StartCoroutine(FadeOut());
        }

        private IEnumerator FadeOut()
        {
            yield return new WaitForSeconds(_holdTime);

            float t = 0f;
            Color c = _label.color;
            while (t < _fadeTime)
            {
                t += Time.deltaTime;
                float a = Mathf.Lerp(1f, 0f, t / _fadeTime);
                _label.color = new Color(c.r, c.g, c.b, a);
                yield return null;
            }

            _label.color = new Color(c.r, c.g, c.b, 0f);
            _active = null;
        }

        private static string LabelForModifier(ModifierType modifier)
        {
            switch (modifier)
            {
                case ModifierType.Dash:   return "SPARSE";
                case ModifierType.Shield: return "SHIELD";
                case ModifierType.Surge:  return "SURGE";
                default:                  return "";
            }
        }

        private static Color ColorForModifier(ModifierType modifier)
        {
            switch (modifier)
            {
                case ModifierType.Dash:   return new Color(0.2f, 0.8f, 1.0f);
                case ModifierType.Shield: return new Color(0.2f, 0.4f, 1.0f);
                case ModifierType.Surge:  return new Color(1.0f, 0.6f, 0.1f);
                default:                  return Color.white;
            }
        }
    }
}
