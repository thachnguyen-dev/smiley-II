using NUnit.Framework;
using Smiley.Ads;

namespace Smiley.Tests
{
    /// <summary>D-122's cap: at most one interstitial every three minutes (owner, 2026-09-17).</summary>
    public sealed class InterstitialRuleTests
    {
        private const double Cap = 180;

        [Test]
        public void TheFirstOneOfASessionIsAlwaysAllowed()
        {
            Assert.That(InterstitialRule.MayShow(InterstitialRule.Never, 0, Cap), Is.True);
            Assert.That(InterstitialRule.MayShow(InterstitialRule.Never, 5000, Cap), Is.True);
        }

        [Test]
        public void InsideTheWindowNothingShows()
        {
            Assert.That(InterstitialRule.MayShow(100, 100 + 179.9, Cap), Is.False);
            Assert.That(InterstitialRule.MayShow(100, 100, Cap), Is.False, "two presses in the same second");
        }

        [Test]
        public void AtTheWindowsEdgeAndBeyondOneMay()
        {
            Assert.That(InterstitialRule.MayShow(100, 280, Cap), Is.True);
            Assert.That(InterstitialRule.MayShow(100, 1000, Cap), Is.True);
        }
    }
}
