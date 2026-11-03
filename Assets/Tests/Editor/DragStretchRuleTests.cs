using NUnit.Framework;
using Smiley.Gameplay;
using UnityEngine;

namespace Smiley.Tests
{
    /// <summary>The drag stretch as arithmetic (D-035).</summary>
    public sealed class DragStretchRuleTests
    {
        [Test]
        public void ADragSqueezesHimShorterAlongItAndWiderAcrossIt()
        {
            // ⭐ THE OWNER'S RULING, 2026-09-10 (D-053): "he should become shorter along the drag
            // direction and expand wider perpendicular to the drag direction, like a water
            // balloon being squeezed."
            //
            // ⛔ PINNED HERE, AS ARITHMETIC, BECAUSE THE SIGN IS THE WHOLE OF IT. A squeeze and
            // an elongation of the same size are the same number with opposite signs and report
            // the same magnitude — so a test that reads "how deformed is he" cannot tell the two
            // apart, and two earlier readings of this instruction were built and shipped before
            // the owner corrected them.
            var squeezed = new DeformationState { Magnitude = 0.3f, AxisRadians = 0f };

            Vector2 scale = squeezed.ScaleInAxisFrame(0.85f);

            Assert.That(scale.x, Is.LessThan(1f),
                $"he is {scale.x:0.000} as long ALONG the drag — a squeeze makes him shorter");
            Assert.That(scale.y, Is.GreaterThan(1f),
                $"he is {scale.y:0.000} as wide ACROSS the drag — a balloon squeezed one way " +
                "expands the other");
            Assert.That(scale.x, Is.LessThan(scale.y));
        }

        [Test]
        public void TheSameNumberNegatedWouldElongateHimInstead()
        {
            // ⚠ The failure this guards, stated as its own test: the mechanism is symmetric, and
            // nothing but the sign chooses which way Smiley deforms.
            var elongated = new DeformationState { Magnitude = -0.3f, AxisRadians = 0f };

            Vector2 scale = elongated.ScaleInAxisFrame(0.85f);

            Assert.That(scale.x, Is.GreaterThan(1f),
                "a negative magnitude no longer elongates, so the sign has stopped being the " +
                "thing that distinguishes a squeeze from a stretch");
            Assert.That(scale.y, Is.LessThan(1f));
        }

        [Test]
        public void NoDragMeansNoStretch()
        {
            Assert.That(DragStretchRule.Target(0f, 0.3f, 0.4f), Is.EqualTo(0f));
        }

        [Test]
        public void TheStretchGrowsWithTheDrag()
        {
            float half = DragStretchRule.Target(0.5f, 0.3f, 1f);
            float full = DragStretchRule.Target(1f, 0.3f, 1f);

            Assert.That(half, Is.GreaterThan(0f));
            Assert.That(full, Is.GreaterThan(half));
        }

        [Test]
        public void TheStretchIsCappedAtTheStatedMaximum()
        {
            // ⭐ The owner's own clause: "the deformation should be limited to a certain maximum
            // amount." ⛔ Asserted against a mapping that WOULD exceed it, because a cap tested
            // only where it never binds is not tested at all.
            Assert.That(DragStretchRule.Target(1f, 0.9f, 0.35f), Is.EqualTo(0.35f).Within(1e-5f));
            Assert.That(DragStretchRule.Target(4f, 0.9f, 0.35f), Is.EqualTo(0.35f).Within(1e-5f));
        }

        [Test]
        public void TheCapHoldsIndependentlyOfTheMapping()
        {
            // ⚠ The two numbers are separate on purpose. If the ceiling were merely whatever a
            // full drag happens to produce, retuning the mapping would silently move it — and
            // the owner asked for a maximum, not for a coincidence.
            for (float atFull = 0f; atFull <= 1f; atFull += 0.1f)
            {
                Assert.That(DragStretchRule.Target(1f, atFull, 0.2f),
                    Is.LessThanOrEqualTo(0.2f + 1e-5f), $"atFullDrag {atFull:0.0} broke the cap");
            }
        }

        [Test]
        public void ANonsenseChargeCannotProduceASquash()
        {
            // A negative charge would flip the sign, and a negative stretch is a SQUASH along
            // the drag — Smiley pinching himself as the player pulls.
            Assert.That(DragStretchRule.Target(-2f, 0.3f, 0.4f), Is.EqualTo(0f));
        }

        [Test]
        public void TheStretchApproachesItsTargetRatherThanSnapping()
        {
            float value = DragStretchRule.Advance(0f, 0.3f, 0.06f, 1f / 60f);

            Assert.That(value, Is.GreaterThan(0f), "it did not move toward the target at all");
            Assert.That(value, Is.LessThan(0.3f), "it snapped straight to the target");
        }

        [Test]
        public void TheApproachDoesNotDependOnTheFrameRate()
        {
            // §33: the display must not change the game. Twelve steps at 120 Hz must land in the
            // same place as six at 60 Hz — a per-frame lerp would not.
            float slow = 0f;
            for (int i = 0; i < 6; i++) slow = DragStretchRule.Advance(slow, 0.3f, 0.06f, 1f / 60f);

            float fast = 0f;
            for (int i = 0; i < 12; i++) fast = DragStretchRule.Advance(fast, 0.3f, 0.06f, 1f / 120f);

            Assert.That(fast, Is.EqualTo(slow).Within(1e-4f));
        }

        [Test]
        public void ReleasingTheDragSettlesExactlyAtTheBaseShape()
        {
            float value = 0.3f;

            for (int i = 0; i < 200; i++)
            {
                value = DragStretchRule.Advance(value, 0f, 0.06f, 1f / 60f);
            }

            Assert.That(value, Is.EqualTo(0f),
                "the stretch decays toward zero forever instead of reaching it, so Smiley is " +
                "never quite back to his base shape");
        }

        [Test]
        public void AZeroFollowTimeIsTakenImmediately()
        {
            Assert.That(DragStretchRule.Advance(0f, 0.25f, 0f, 1f / 60f), Is.EqualTo(0.25f));
        }
    }
}
