using NUnit.Framework;
using Smiley.View;
using UnityEngine;

namespace Smiley.Tests
{
    /// <summary>The safe-area arithmetic, with this phone's numbers (D-072).</summary>
    public sealed class SafeInsetsTests
    {
        [Test]
        public void TheStatusBarBecomesATopInsetOfItsOwnHeight()
        {
            // ⭐ The HUAWEI DVC-AN20: 1080 × 2400, Android status bar over the top 78 px, and
            // Unity's safeArea reported from the BOTTOM-LEFT — so the bar shows as yMax 2322,
            // not as yMin 78. Getting that backwards would inset the bottom and leave the X
            // under the bar exactly as before.
            var safe = new Rect(0f, 0f, 1080f, 2322f);

            UiKit.SafeInsets insets = UiKit.SafeInsets.From(safe, 1080f, 2400f);

            Assert.That(insets.Top, Is.EqualTo(78f / 2400f).Within(1e-6f), "top = the bar");
            Assert.That(insets.Bottom, Is.Zero, "nothing sits at the bottom of this phone");
            Assert.That(insets.Left, Is.Zero);
            Assert.That(insets.Right, Is.Zero);
        }

        [Test]
        public void AFullScreenSafeAreaInsetsNothing()
        {
            // The editor and the Game view: safeArea == the whole screen, so the wrapper is a
            // no-op and what the owner sees in UI Builder is what they get in Play.
            UiKit.SafeInsets insets = UiKit.SafeInsets.From(new Rect(0f, 0f, 1080f, 2400f), 1080f, 2400f);

            Assert.That(insets.Top, Is.Zero);
            Assert.That(insets.Bottom, Is.Zero);
            Assert.That(insets.Left, Is.Zero);
            Assert.That(insets.Right, Is.Zero);
        }

        [Test]
        public void ADegenerateScreenInsetsNothingRatherThanDividingByZero()
        {
            UiKit.SafeInsets insets = UiKit.SafeInsets.From(new Rect(0f, 0f, 0f, 0f), 0f, 0f);

            Assert.That(insets.Top, Is.Zero);
            Assert.That(float.IsNaN(insets.Left), Is.False);
        }

        [Test]
        public void ASafeAreaLargerThanTheScreenNeverGoesNegative()
        {
            // A negative inset would GROW the wrapper past the screen edge.
            UiKit.SafeInsets insets = UiKit.SafeInsets.From(new Rect(-10f, -10f, 1100f, 2420f), 1080f, 2400f);

            Assert.That(insets.Left, Is.Zero);
            Assert.That(insets.Right, Is.Zero);
            Assert.That(insets.Top, Is.Zero);
            Assert.That(insets.Bottom, Is.Zero);
        }
    }
}
