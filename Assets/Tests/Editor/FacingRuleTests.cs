using NUnit.Framework;
using Smiley.View;

namespace Smiley.Tests
{
    /// <summary>The owner's facing rule as arithmetic (D-095).</summary>
    public sealed class FacingRuleTests
    {
        [Test]
        public void MovingLeftMirrorsAndMovingRightTurnsBack()
        {
            Assert.That(FacingRule.ShouldMirror(-3f, 0.3f, false), Is.True, "moving left: mirrored");
            Assert.That(FacingRule.ShouldMirror(3f, 0.3f, true), Is.False, "moving right: as drawn");
        }

        [Test]
        public void StandingStillKeepsTheLastFacing()
        {
            Assert.That(FacingRule.ShouldMirror(0f, 0.3f, true), Is.True);
            Assert.That(FacingRule.ShouldMirror(0f, 0.3f, false), Is.False);
            Assert.That(FacingRule.ShouldMirror(0.2f, 0.3f, true), Is.True, "a settle twitch does not turn him");
            Assert.That(FacingRule.ShouldMirror(-0.2f, 0.3f, false), Is.False);
        }

        [Test]
        public void ARunOfMovementTurnsHimExactlyOncePerReversal()
        {
            bool mirrored = false;
            int turns = 0;

            // Right, slow down through the dead band, then left, then right again.
            foreach (float vx in new[] { 3f, 2f, 1f, 0.2f, 0f, -0.2f, -1f, -2f, -1f, -0.1f, 0.1f, 1f, 3f })
            {
                bool next = FacingRule.ShouldMirror(vx, 0.3f, mirrored);
                if (next != mirrored) turns++;
                mirrored = next;
            }

            Assert.That(turns, Is.EqualTo(2));
            Assert.That(mirrored, Is.False);
        }
    }
}
