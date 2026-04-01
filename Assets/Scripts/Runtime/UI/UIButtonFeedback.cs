using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DashAndCollect
{
    /// <summary>
    /// Adds scale-based hover/press/release feedback to any UI button.
    /// Attach to the same GameObject as a Button component.
    ///
    /// Timings from UI Animation Pipeline:
    ///   Hover:   1.05x over 80ms  (EaseOutQuad)
    ///   Press:   0.95x over 60ms  (punch)
    ///   Release: 1.0x  over 100ms (EaseOutQuad)
    ///
    /// Respects UIAnimator.ReducedMotion — when true, no scale animation.
    /// </summary>
    public sealed class UIButtonFeedback : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler
    {
        [SerializeField] private float _hoverScale    = 1.05f;
        [SerializeField] private float _pressScale    = 0.95f;
        [SerializeField] private float _hoverDuration = 0.08f;  // 80ms
        [SerializeField] private float _pressDuration = 0.06f;  // 60ms
        [SerializeField] private float _releaseDuration = 0.10f; // 100ms

        private Coroutine _active;
        private bool _hovered;
        private bool _pressed;

        // ── Pointer events ────────────────────────────────────────────────

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            if (!_pressed)
                AnimateTo(_hoverScale, _hoverDuration);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            if (!_pressed)
                AnimateTo(1f, _releaseDuration);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressed = true;
            AnimateTo(_pressScale, _pressDuration);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;
            float target = _hovered ? _hoverScale : 1f;
            float dur = _hovered ? _hoverDuration : _releaseDuration;
            AnimateTo(target, dur);
        }

        // ── Keyboard/gamepad focus ────────────────────────────────────────

        public void OnSelect(BaseEventData eventData)
        {
            _hovered = true;
            if (!_pressed)
                AnimateTo(_hoverScale, _hoverDuration);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _hovered = false;
            if (!_pressed)
                AnimateTo(1f, _releaseDuration);
        }

        // ── Animation ─────────────────────────────────────────────────────

        private void AnimateTo(float targetScale, float duration)
        {
            _active = UIAnimator.Stop(this, _active);
            _active = StartCoroutine(ScaleTo(targetScale, duration));
        }

        private IEnumerator ScaleTo(float targetScale, float duration)
        {
            if (UIAnimator.ReducedMotion || duration <= 0f)
            {
                transform.localScale = new Vector3(targetScale, targetScale, 1f);
                _active = null;
                yield break;
            }

            Vector3 start = transform.localScale;
            Vector3 end = new Vector3(targetScale, targetScale, 1f);
            float t = 0f;

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float p = UIAnimator.EaseOutQuad(Mathf.Clamp01(t / duration));
                transform.localScale = Vector3.LerpUnclamped(start, end, p);
                yield return null;
            }

            transform.localScale = end;
            _active = null;
        }

        private void OnDisable()
        {
            // Reset scale when disabled to avoid stuck state
            _active = UIAnimator.Stop(this, _active);
            transform.localScale = Vector3.one;
            _hovered = false;
            _pressed = false;
        }
    }
}
