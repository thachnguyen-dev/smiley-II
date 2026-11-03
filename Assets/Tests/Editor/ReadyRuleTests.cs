using NUnit.Framework;
using Smiley.Gameplay;

namespace Smiley.Tests
{
    /// <summary>§8's Ready predicate.</summary>
    public sealed class ReadyRuleTests
    {
        private const float Threshold = 0.25f;

        [Test]
        public void ContactAndStillnessTogetherMeanReady()
        {
            Assert.That(ReadyRule.IsReady(true, 0f, Threshold), Is.True);
        }

        [Test]
        public void StillnessAloneIsNotEnough()
        {
            // At the top of an arc, vertical speed passes through zero while Smiley is
            // nowhere near a platform. §8 requires contact as well, and this is the case
            // that catches a speed-only implementation.
            Assert.That(ReadyRule.IsReady(false, 0f, Threshold), Is.False);
        }

        [Test]
        public void ContactAloneIsNotEnough()
        {
            // Rolling along a platform is contact without stillness (§9: Smiley rolls and
            // slides after impact), and §8 forbids dragging while he is still moving.
            Assert.That(ReadyRule.IsReady(true, Threshold * 4f, Threshold), Is.False);
        }

        [Test]
        public void TheThresholdItselfCounts()
        {
            // §8 says "reduced below the Stopping Threshold". Read inclusively here, so a
            // body resting at exactly the threshold is Ready rather than stuck forever a
            // hair above it.
            Assert.That(ReadyRule.IsReady(true, Threshold, Threshold), Is.True);
        }

        [Test]
        public void ReadyIsInstantaneousAndHoldsNoHistory()
        {
            // §8: "There is no additional stabilization delay. Ready appears immediately
            // once the conditions are satisfied."
            //
            // A settle timer or an N-consecutive-frames counter would be invisible in play
            // and would violate that sentence exactly. Calling the rule repeatedly with a
            // moving body and then once with a still one must give Ready on the FIRST still
            // call -- no warm-up.
            for (int i = 0; i < 10; i++)
            {
                Assert.That(ReadyRule.IsReady(true, 5f, Threshold), Is.False);
            }

            Assert.That(ReadyRule.IsReady(true, 0f, Threshold), Is.True);
        }

        [Test]
        public void LosingContactStopsBeingReadyImmediately()
        {
            // §8: "Ready does not freeze or anchor Smiley to the platform." If he slides off
            // an edge he is simply no longer Ready -- there is no grace window in which the
            // state lingers.
            Assert.That(ReadyRule.IsReady(true, 0f, Threshold), Is.True);
            Assert.That(ReadyRule.IsReady(false, 0f, Threshold), Is.False);
        }
    }
}
