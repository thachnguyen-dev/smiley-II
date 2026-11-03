using NUnit.Framework;
using Smiley.View;

namespace Smiley.Tests
{
    /// <summary>The frame-rate target (D-113): the display's rate, never below 60.</summary>
    public sealed class FrameRateRuleTests
    {
        [TestCase(60.0, 60)]
        [TestCase(59.94, 60)]
        [TestCase(90.0, 90)]
        [TestCase(120.0, 120)]
        public void FollowsTheDisplay(double refreshHz, int expected)
        {
            Assert.That(FrameRateRule.Target(refreshHz), Is.EqualTo(expected));
        }

        [TestCase(0.0)]
        [TestCase(-1.0)]
        [TestCase(30.0)]
        [TestCase(double.NaN)]
        public void NeverBelowSixtyAndNeverTheAndroidDefault(double refreshHz)
        {
            // ⛔ 30 is what the phone ran at for every build before D-113: Unity's Android
            // default when nothing sets a target. No display reading may hand it back.
            Assert.That(FrameRateRule.Target(refreshHz), Is.EqualTo(60));
        }
    }
}
