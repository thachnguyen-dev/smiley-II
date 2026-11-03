using NUnit.Framework;
using Smiley.Gameplay;
using UnityEngine;

namespace Smiley.Tests
{
    /// <summary>§13's return to upright.</summary>
    public sealed class UprightSettleTests
    {
        private const float Settle = 0.4f;
        private const float Step = 1f / 60f;

        [Test]
        public void ItMovesTowardUpright()
        {
            float angle = UprightSettle.Step(40f, Settle, Step);

            Assert.That(angle, Is.LessThan(40f));
            Assert.That(angle, Is.GreaterThan(0f));
        }

        [Test]
        public void ItDoesNotSnap()
        {
            // ⛔ §13: "They must not snap immediately." A single step from a large lean must
            // still leave him leaning — an implementation that assigned zero on stopping would
            // satisfy every other clause of §13 and break this one.
            float after = UprightSettle.Step(80f, Settle, Step);

            Assert.That(after, Is.GreaterThan(60f),
                $"one frame took him from 80 degrees to {after} — that is a snap, not a return");
        }

        [Test]
        public void ItArrivesEventually()
        {
            // "Gradually" must not mean "never": a return that only ever asymptotes would leave
            // Smiley permanently, slightly crooked.
            float angle = 120f;
            for (int i = 0; i < 600; i++)
            {
                angle = UprightSettle.Step(angle, Settle, Step);
            }

            Assert.That(UprightSettle.OffsetFromUpright(angle), Is.LessThan(0.5f));
        }

        [Test]
        public void ItTakesTheShortWayRound()
        {
            // ⚠ 350 degrees is TEN degrees from upright, not three hundred and fifty. Easing
            // the raw number toward zero would spin Smiley almost the whole way round to
            // travel a tenth of a turn — correct by the arithmetic, absurd on screen.
            float after = UprightSettle.Step(350f, Settle, Step);

            Assert.That(after, Is.GreaterThan(350f),
                $"went from 350 to {after} — it took the long way round");
        }

        [Test]
        public void ItNeverOvershootsIntoALeanTheOtherWay()
        {
            float angle = 25f;
            for (int i = 0; i < 600; i++)
            {
                angle = UprightSettle.Step(angle, Settle, Step);
                Assert.That(angle, Is.GreaterThanOrEqualTo(-0.01f),
                    "the return overshot past upright and leaned the other way");
            }
        }

        [Test]
        public void ItIsFrameRateIndependent()
        {
            // §33: the same input must not give a different result on different hardware.
            float atSixty = 90f;
            for (int i = 0; i < 60; i++)
            {
                atSixty = UprightSettle.Step(atSixty, Settle, 1f / 60f);
            }

            float atThirty = 90f;
            for (int i = 0; i < 30; i++)
            {
                atThirty = UprightSettle.Step(atThirty, Settle, 1f / 30f);
            }

            Assert.That(atThirty, Is.EqualTo(atSixty).Within(0.5f),
                "one second of settling gave different results at different frame rates");
        }

        [Test]
        public void AnUprightSmileyIsLeftAlone()
        {
            Assert.That(UprightSettle.Step(0f, Settle, Step), Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void OffsetIgnoresWhichWayHeLeans()
        {
            Assert.That(UprightSettle.OffsetFromUpright(10f), Is.EqualTo(10f).Within(1e-3f));
            Assert.That(UprightSettle.OffsetFromUpright(-10f), Is.EqualTo(10f).Within(1e-3f));
            Assert.That(UprightSettle.OffsetFromUpright(350f), Is.EqualTo(10f).Within(1e-3f));
            Assert.That(UprightSettle.OffsetFromUpright(180f), Is.EqualTo(180f).Within(1e-3f));
        }
    }
}
