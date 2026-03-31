using UnityEngine;
using UnityEngine.UI;

namespace DashAndCollect
{
    /// <summary>
    /// Drives 3 dot Images to show chain progress and type colour.
    ///
    /// Colour contract (type -> colour):
    ///   Dash   -> cyan   (0.2, 0.8, 1, 1)
    ///   Shield -> blue   (0.2, 0.4, 1, 1)
    ///   Surge  -> orange (1, 0.6, 0.1, 1)
    ///   null   -> grey   (0.3, 0.3, 0.3, 1)
    ///
    /// Alpha contract:
    ///   dot[i].color.a == 1  when i < chainCount  (filled)
    ///   dot[i].color.a == 0  when i >= chainCount  (empty)
    /// </summary>
    public sealed class ChainCounterDisplay : MonoBehaviour
    {
        [SerializeField] private Image _dot0;
        [SerializeField] private Image _dot1;
        [SerializeField] private Image _dot2;

        private static readonly Color CyanColor   = new Color(0.2f, 0.8f, 1.0f, 1f);
        private static readonly Color BlueColor   = new Color(0.2f, 0.4f, 1.0f, 1f);
        private static readonly Color OrangeColor = new Color(1.0f, 0.6f, 0.1f, 1f);
        private static readonly Color GreyColor   = new Color(0.3f, 0.3f, 0.3f, 1f);

        private GameManager _gameManager;

        public void Initialize(GameManager gameManager)
        {
            _gameManager = gameManager;
            _gameManager.ScoreManager.OnScoreChanged += Apply;
        }

        private void OnDestroy()
        {
            if (_gameManager != null)
                _gameManager.ScoreManager.OnScoreChanged -= Apply;
        }

        internal void Apply(ScoreSnapshot snap)
        {
            Color dotColor = ColorForType(snap.chainType);

            SetDot(_dot0, 0, snap.chainCount, dotColor);
            SetDot(_dot1, 1, snap.chainCount, dotColor);
            SetDot(_dot2, 2, snap.chainCount, dotColor);
        }

        private static void SetDot(Image dot, int index, int chainCount, Color baseColor)
        {
            if (dot == null) return;

            float alpha = index < chainCount ? 1f : 0f;
            dot.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
        }

        private static Color ColorForType(CollectibleType? type)
        {
            if (type == null) return GreyColor;
            switch (type.Value)
            {
                case CollectibleType.Dash:   return CyanColor;
                case CollectibleType.Shield: return BlueColor;
                case CollectibleType.Surge:  return OrangeColor;
                default:                     return GreyColor;
            }
        }
    }
}
