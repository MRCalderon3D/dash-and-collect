using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using DashAndCollect;

namespace DashAndCollect.Tests
{
    /// <summary>
    /// Edit-mode tests for ChainCounterDisplay dot visibility and colour (M3 exit criteria).
    ///
    /// Strategy:
    ///   ChainCounterDisplay reads ScoreSnapshot.chainCount (0-2, resets at chain completion)
    ///   and chainType to drive 3 dot Images. Tests inject snapshots directly via the
    ///   internal Apply(ScoreSnapshot) method — no physics loop required.
    ///
    ///   Dot colour contract (type → colour):
    ///     Dash   → cyan   (0.2, 0.8, 1, 1)
    ///     Shield → blue   (0.2, 0.4, 1, 1)
    ///     Surge  → orange (1, 0.6, 0.1, 1)
    ///     null   → grey   (0.3, 0.3, 0.3, 1)
    ///
    ///   Alpha contract:
    ///     dot[i].color.a == 1  when i < chainCount   (filled)
    ///     dot[i].color.a == 0  when i >= chainCount  (empty)
    ///
    /// Naming: MethodUnderTest_Condition_ExpectedResult
    /// </summary>
    [TestFixture]
    public class ChainCounterDisplayTests
    {
        private GameObject            _root;
        private ChainCounterDisplay   _display;
        private Image[]               _dots;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("ChainCounterDisplay");
            _root.SetActive(false);
            _display = _root.AddComponent<ChainCounterDisplay>();

            // Create 3 dot Image children and inject them.
            _dots = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject($"Dot{i}");
                go.transform.SetParent(_root.transform);
                _dots[i] = go.AddComponent<Image>();
            }

            SetField(_display, "_dot0", _dots[0]);
            SetField(_display, "_dot1", _dots[1]);
            SetField(_display, "_dot2", _dots[2]);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        // ── Initial state ─────────────────────────────────────────────────────

        [Test]
        public void Apply_ChainCountZero_AllDotsTransparent()
        {
            _display.Apply(Snap(0, null));

            Assert.AreEqual(0f, _dots[0].color.a, 0.01f, "Dot 0 must be transparent when chainCount=0.");
            Assert.AreEqual(0f, _dots[1].color.a, 0.01f, "Dot 1 must be transparent when chainCount=0.");
            Assert.AreEqual(0f, _dots[2].color.a, 0.01f, "Dot 2 must be transparent when chainCount=0.");
        }

        // ── Progressive fill ──────────────────────────────────────────────────

        [Test]
        public void Apply_ChainCountOne_OnlyFirstDotVisible()
        {
            _display.Apply(Snap(1, CollectibleType.Dash));

            Assert.AreEqual(1f, _dots[0].color.a, 0.01f, "Dot 0 must be opaque when chainCount=1.");
            Assert.AreEqual(0f, _dots[1].color.a, 0.01f, "Dot 1 must be transparent when chainCount=1.");
            Assert.AreEqual(0f, _dots[2].color.a, 0.01f, "Dot 2 must be transparent when chainCount=1.");
        }

        [Test]
        public void Apply_ChainCountTwo_FirstTwoDotsVisible()
        {
            _display.Apply(Snap(2, CollectibleType.Dash));

            Assert.AreEqual(1f, _dots[0].color.a, 0.01f, "Dot 0 must be opaque when chainCount=2.");
            Assert.AreEqual(1f, _dots[1].color.a, 0.01f, "Dot 1 must be opaque when chainCount=2.");
            Assert.AreEqual(0f, _dots[2].color.a, 0.01f, "Dot 2 must be transparent when chainCount=2.");
        }

        // ── Chain complete resets to 0 ─────────────────────────────────────────

        [Test]
        public void Apply_ChainCountZeroAfterTwo_AllDotsTransparent()
        {
            _display.Apply(Snap(2, CollectibleType.Dash));
            _display.Apply(Snap(0, null));   // chain fired, ScoreManager resets to 0

            Assert.AreEqual(0f, _dots[0].color.a, 0.01f, "All dots must clear after chain completion.");
            Assert.AreEqual(0f, _dots[1].color.a, 0.01f);
            Assert.AreEqual(0f, _dots[2].color.a, 0.01f);
        }

        // ── Colour by type ────────────────────────────────────────────────────

        [Test]
        public void Apply_DashType_DotColourIsCyan()
        {
            _display.Apply(Snap(1, CollectibleType.Dash));

            var c = _dots[0].color;
            Assert.AreEqual(0.2f, c.r, 0.01f, "Dash dot R must be 0.2.");
            Assert.AreEqual(0.8f, c.g, 0.01f, "Dash dot G must be 0.8.");
            Assert.AreEqual(1.0f, c.b, 0.01f, "Dash dot B must be 1.0.");
        }

        [Test]
        public void Apply_ShieldType_DotColourIsBlue()
        {
            _display.Apply(Snap(1, CollectibleType.Shield));

            var c = _dots[0].color;
            Assert.AreEqual(0.2f, c.r, 0.01f, "Shield dot R must be 0.2.");
            Assert.AreEqual(0.4f, c.g, 0.01f, "Shield dot G must be 0.4.");
            Assert.AreEqual(1.0f, c.b, 0.01f, "Shield dot B must be 1.0.");
        }

        [Test]
        public void Apply_SurgeType_DotColourIsOrange()
        {
            _display.Apply(Snap(1, CollectibleType.Surge));

            var c = _dots[0].color;
            Assert.AreEqual(1.0f, c.r, 0.01f, "Surge dot R must be 1.0.");
            Assert.AreEqual(0.6f, c.g, 0.01f, "Surge dot G must be 0.6.");
            Assert.AreEqual(0.1f, c.b, 0.01f, "Surge dot B must be 0.1.");
        }

        [Test]
        public void Apply_NullType_DotColourIsGrey()
        {
            _display.Apply(Snap(1, null));

            var c = _dots[0].color;
            Assert.AreEqual(0.3f, c.r, 0.01f, "Unknown type dot R must be 0.3.");
            Assert.AreEqual(0.3f, c.g, 0.01f, "Unknown type dot G must be 0.3.");
            Assert.AreEqual(0.3f, c.b, 0.01f, "Unknown type dot B must be 0.3.");
        }

        // ── Null dot safety ───────────────────────────────────────────────────

        [Test]
        public void Apply_WhenDotsNotAssigned_DoesNotThrow()
        {
            var go = new GameObject("Bare");
            var bare = go.AddComponent<ChainCounterDisplay>();

            Assert.DoesNotThrow(() => bare.Apply(Snap(2, CollectibleType.Dash)),
                "Apply must not throw when dot references are null.");

            Object.DestroyImmediate(go);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static ScoreSnapshot Snap(int chainCount, CollectibleType? chainType) =>
            new ScoreSnapshot { chainCount = chainCount, chainType = chainType };

        private static void SetField<T>(T instance, string name, object value) =>
            typeof(T).GetField(name,
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(instance, value);
    }
}
