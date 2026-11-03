using NUnit.Framework;
using Smiley.View;

namespace Smiley.Tests
{
    /// <summary>The shadow's size and darkness against height (D-083).</summary>
    public sealed class ShadowRuleTests
    {
        [Test]
        public void OnTheGroundTheShadowIsFullSizeAndDarkest()
        {
            (float scale, float alpha) = ShadowRule.Evaluate(0f, 6f, 0.35f, 0.45f, 0.08f);

            Assert.That(scale, Is.EqualTo(1f));
            Assert.That(alpha, Is.EqualTo(0.45f));
        }

        [Test]
        public void AtTheTopOfTheRangeItIsSmallestAndFaintest()
        {
            (float scale, float alpha) = ShadowRule.Evaluate(6f, 6f, 0.35f, 0.45f, 0.08f);

            Assert.That(scale, Is.EqualTo(0.35f).Within(1e-6f));
            Assert.That(alpha, Is.EqualTo(0.08f).Within(1e-6f));
        }

        [Test]
        public void HalfwayIsHalfway()
        {
            (float scale, float alpha) = ShadowRule.Evaluate(3f, 6f, 0.35f, 0.45f, 0.08f);

            Assert.That(scale, Is.EqualTo(0.675f).Within(1e-6f));
            Assert.That(alpha, Is.EqualTo(0.265f).Within(1e-6f));
        }

        [Test]
        public void BeyondTheRangeThereIsNoShadowAndBelowZeroCountsAsGround()
        {
            Assert.That(ShadowRule.InRange(6.01f, 6f), Is.False);
            Assert.That(ShadowRule.InRange(6f, 6f), Is.True);

            // A negative height — the collider resting a hair below the surface line — is ground.
            (float scale, float alpha) = ShadowRule.Evaluate(-0.02f, 6f, 0.35f, 0.45f, 0.08f);
            Assert.That(scale, Is.EqualTo(1f));
            Assert.That(alpha, Is.EqualTo(0.45f));
        }
    }
}
