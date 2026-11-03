using NUnit.Framework;
using Smiley.Gameplay;

namespace Smiley.Tests
{
    /// <summary>
    /// The Cloud platform is passable from below and solid from everywhere else (D-130).
    /// </summary>
    /// <remarks>
    /// ⛔ <b>This fixture exists because D-130 amends §10</b>, which said "There are: No one-way
    /// platforms". The owner changed that for the Cloud alone. ⚠ What must NOT drift with it is
    /// the rest of §10 — side and corner collisions still happen, and there is still no limit on
    /// collisions per launch. The arc is the whole of the change, so the arc is what is pinned.
    /// </remarks>
    public sealed class PassThroughRuleTests
    {
        private const float Arc = PassThroughRule.CloudSurfaceArcDegrees;

        [Test]
        public void LandingFromDirectlyAboveIsSolid()
        {
            Assert.That(PassThroughRule.CloudIsSolid(0f), Is.True,
                "a cloud you cannot land on is not a platform");
        }

        [Test]
        public void LaunchingUpFromDirectlyBelowPassesThrough()
        {
            // ⭐ The whole point of D-130.
            Assert.That(PassThroughRule.CloudIsSolid(180f), Is.False,
                "the owner asked for pass-through from below and this is below");
        }

        [Test]
        public void AHitOnTheEndIsStillSolid()
        {
            // ⛔ The owner chose "below only; sides stay solid". Unity's usual 180° arc would
            // have made these pass through too, which is why the arc is 270 and not 180.
            Assert.That(PassThroughRule.CloudIsSolid(90f), Is.True, "hit from one side");
            Assert.That(PassThroughRule.CloudIsSolid(-90f), Is.True, "hit from the other side");
        }

        [Test]
        public void TheWindowBelowIsNinetyDegreesWide()
        {
            // 270° solid leaves exactly 90° passable, centred straight down: 135..225.
            Assert.That(PassThroughRule.CloudIsSolid(134f), Is.True, "just outside the window");
            Assert.That(PassThroughRule.CloudIsSolid(136f), Is.False, "just inside it");
            Assert.That(PassThroughRule.CloudIsSolid(-136f), Is.False, "and symmetrically");
        }

        [Test]
        public void ADegenerateArcDoesNotThrowOrInvert()
        {
            // ⓘ The arc is owner-tunable in the Inspector (Q-058), so it can be set to anything.
            Assert.That(PassThroughRule.IsSolid(0f, 0f), Is.True, "zero arc still catches dead-on");
            Assert.That(PassThroughRule.IsSolid(180f, 0f), Is.False);
            Assert.That(PassThroughRule.IsSolid(180f, 360f), Is.True, "a full arc is solid all round");
            Assert.That(PassThroughRule.IsSolid(180f, 999f), Is.True, "clamped, not wrapped");
        }
    }
}
