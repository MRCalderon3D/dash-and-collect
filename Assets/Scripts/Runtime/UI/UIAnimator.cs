using System;
using System.Collections;
using UnityEngine;

namespace DashAndCollect
{
    /// <summary>
    /// Static coroutine-based UI animation utilities for Dash & Collect.
    /// No external tween library required.
    ///
    /// All methods return IEnumerator — call via StartCoroutine on any MonoBehaviour.
    /// All methods respect the static ReducedMotion flag: when true, the final state
    /// is applied instantly and the coroutine yields once then exits.
    ///
    /// Durations and easing curves follow ART-BIBLE.md §9 / UI Animation Pipeline.
    /// </summary>
    public static class UIAnimator
    {
        // ── Accessibility — ART-BIBLE / ui-animation-pipeline ─────────────
        private const string ReducedMotionPref = "DashAndCollect_ReducedMotion";

        private static bool _reducedMotion;
        private static bool _prefLoaded;

        /// <summary>
        /// When true, all animations skip to their final state instantly.
        /// Persisted in PlayerPrefs alongside other accessibility settings.
        /// </summary>
        public static bool ReducedMotion
        {
            get
            {
                if (!_prefLoaded)
                {
                    _reducedMotion = PlayerPrefs.GetInt(ReducedMotionPref, 0) == 1;
                    _prefLoaded = true;
                }
                return _reducedMotion;
            }
            set
            {
                _reducedMotion = value;
                _prefLoaded = true;
                PlayerPrefs.SetInt(ReducedMotionPref, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // FADE — CanvasGroup alpha
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Alpha 0 → 1 over duration seconds.</summary>
        public static IEnumerator FadeIn(CanvasGroup group, float duration,
            Func<float, float> easing = null)
        {
            if (group == null) yield break;
            if (ReducedMotion || duration <= 0f)
            {
                group.alpha = 1f;
                yield break;
            }

            easing = easing ?? EaseOutQuad;
            group.alpha = 0f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = easing(Mathf.Clamp01(t / duration));
                yield return null;
            }
            group.alpha = 1f;
        }

        /// <summary>Alpha 1 → 0 over duration seconds.</summary>
        public static IEnumerator FadeOut(CanvasGroup group, float duration,
            Func<float, float> easing = null)
        {
            if (group == null) yield break;
            if (ReducedMotion || duration <= 0f)
            {
                group.alpha = 0f;
                yield break;
            }

            easing = easing ?? EaseOutQuad;
            group.alpha = 1f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = 1f - easing(Mathf.Clamp01(t / duration));
                yield return null;
            }
            group.alpha = 0f;
        }

        // ════════════════════════════════════════════════════════════════════
        // SCALE — Transform.localScale
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Scale 0 → 1 with overshoot (EaseOutBack by default).
        /// Good for panels appearing.
        /// </summary>
        public static IEnumerator ScaleIn(Transform target, float duration,
            Func<float, float> easing = null)
        {
            if (target == null) yield break;
            if (ReducedMotion || duration <= 0f)
            {
                target.localScale = Vector3.one;
                yield break;
            }

            easing = easing ?? EaseOutBack;
            target.localScale = Vector3.zero;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float s = easing(Mathf.Clamp01(t / duration));
                target.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            target.localScale = Vector3.one;
        }

        /// <summary>
        /// Scale from <paramref name="fromScale"/> → 1 with easing.
        /// Useful for GameOver panel appearing from 0.8.
        /// </summary>
        public static IEnumerator ScaleFrom(Transform target, float fromScale,
            float duration, Func<float, float> easing = null)
        {
            if (target == null) yield break;
            if (ReducedMotion || duration <= 0f)
            {
                target.localScale = Vector3.one;
                yield break;
            }

            easing = easing ?? EaseOutBack;
            target.localScale = new Vector3(fromScale, fromScale, 1f);
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float s = Mathf.LerpUnclamped(fromScale, 1f, easing(Mathf.Clamp01(t / duration)));
                target.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            target.localScale = Vector3.one;
        }

        // ════════════════════════════════════════════════════════════════════
        // SLIDE — RectTransform anchoredPosition
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Slide from off-screen in the given direction to the target's current position.
        /// </summary>
        public static IEnumerator SlideIn(RectTransform target, SlideDirection direction,
            float duration, Func<float, float> easing = null)
        {
            if (target == null) yield break;

            Vector2 endPos = target.anchoredPosition;

            if (ReducedMotion || duration <= 0f)
            {
                target.anchoredPosition = endPos;
                yield break;
            }

            easing = easing ?? EaseOutQuad;
            Vector2 offset = GetSlideOffset(target, direction);
            Vector2 startPos = endPos + offset;
            target.anchoredPosition = startPos;

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float p = easing(Mathf.Clamp01(t / duration));
                target.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, p);
                yield return null;
            }
            target.anchoredPosition = endPos;
        }

        // ════════════════════════════════════════════════════════════════════
        // PUNCH — quick scale impulse for feedback
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Quick scale punch: briefly scales to intensity then returns to 1.0.
        /// intensity &lt; 1 = squeeze, intensity &gt; 1 = expand.
        /// Duration is total round-trip time.
        /// </summary>
        public static IEnumerator PunchScale(Transform target, float intensity,
            float duration)
        {
            if (target == null) yield break;
            if (ReducedMotion || duration <= 0f)
            {
                target.localScale = Vector3.one;
                yield break;
            }

            float half = duration * 0.4f; // 40% to peak, 60% to settle
            float settle = duration - half;

            // Punch out
            float t = 0f;
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                float s = Mathf.Lerp(1f, intensity, EaseOutQuad(Mathf.Clamp01(t / half)));
                target.localScale = new Vector3(s, s, 1f);
                yield return null;
            }

            // Settle back
            t = 0f;
            while (t < settle)
            {
                t += Time.unscaledDeltaTime;
                float s = Mathf.Lerp(intensity, 1f, EaseOutQuad(Mathf.Clamp01(t / settle)));
                target.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            target.localScale = Vector3.one;
        }

        // ════════════════════════════════════════════════════════════════════
        // HELPERS — kept public so callers can compose sequences
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Safely stops a coroutine reference and nulls it.
        /// Returns null (assign back to the field).
        /// </summary>
        public static Coroutine Stop(MonoBehaviour owner, Coroutine routine)
        {
            if (routine != null && owner != null)
                owner.StopCoroutine(routine);
            return null;
        }

        // ════════════════════════════════════════════════════════════════════
        // EASING FUNCTIONS
        // ════════════════════════════════════════════════════════════════════

        // All take t in [0,1] and return a mapped value.

        public static float Linear(float t) => t;

        public static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

        public static float EaseInQuad(float t) => t * t;

        public static float EaseOutCubic(float t)
        {
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        /// <summary>
        /// EaseOutBack — overshoots to ~1.1 then settles to 1.0.
        /// Good for panel pop-in.
        /// </summary>
        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float tm1 = t - 1f;
            return 1f + c3 * tm1 * tm1 * tm1 + c1 * tm1 * tm1;
        }

        // ════════════════════════════════════════════════════════════════════
        // SLIDE DIRECTION
        // ════════════════════════════════════════════════════════════════════

        static Vector2 GetSlideOffset(RectTransform target, SlideDirection direction)
        {
            // Calculate offset large enough to push entirely off-screen
            var canvas = target.GetComponentInParent<Canvas>();
            float screenW = canvas != null
                ? ((RectTransform)canvas.transform).rect.width
                : Screen.width;
            float screenH = canvas != null
                ? ((RectTransform)canvas.transform).rect.height
                : Screen.height;

            switch (direction)
            {
                case SlideDirection.Left:  return new Vector2(-screenW, 0f);
                case SlideDirection.Right: return new Vector2(screenW, 0f);
                case SlideDirection.Up:    return new Vector2(0f, screenH);
                case SlideDirection.Down:  return new Vector2(0f, -screenH);
                default:                   return Vector2.zero;
            }
        }
    }

    /// <summary>Slide direction for UIAnimator.SlideIn.</summary>
    public enum SlideDirection { Left, Right, Up, Down }
}
