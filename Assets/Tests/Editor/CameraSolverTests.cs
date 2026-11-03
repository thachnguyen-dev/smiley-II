using NUnit.Framework;
using Smiley.View;
using UnityEngine;

namespace Smiley.Tests
{
    /// <summary>§17's follow and D-016's deadzone, as arithmetic.</summary>
    public sealed class CameraSolverTests
    {
        private const float Band = 1f;

        [Test]
        public void TheCameraHoldsStillWhileSmileyStaysInTheBand()
        {
            // D-016's whole reason for existing: §9 has Smiley rolling and sliding after
            // almost every landing, and a camera that answered every wobble would slide the
            // world under him constantly.
            Assert.That(CameraSolver.HorizontalGoal(0f, 0.4f, Band), Is.EqualTo(0f));
            Assert.That(CameraSolver.HorizontalGoal(0f, -0.9f, Band), Is.EqualTo(0f));
        }

        [Test]
        public void TheCameraFollowsOnceHeLeavesTheBand()
        {
            // Follows only as far as the band's edge — recentring him on every touch of the
            // boundary would make the deadzone pointless at any speed above a crawl.
            Assert.That(CameraSolver.HorizontalGoal(0f, 3f, Band), Is.EqualTo(2f).Within(1e-4f));
            Assert.That(CameraSolver.HorizontalGoal(0f, -3f, Band), Is.EqualTo(-2f).Within(1e-4f));
        }

        [Test]
        public void TheBandIsSymmetrical()
        {
            float right = CameraSolver.HorizontalGoal(0f, 5f, Band);
            float left = CameraSolver.HorizontalGoal(0f, -5f, Band);

            Assert.That(right, Is.EqualTo(-left).Within(1e-4f));
        }

        [Test]
        public void ClampingKeepsTheViewInsideTheWorld()
        {
            // D-016: clamping beats centring. The view never shows beyond the authored world.
            var world = new Rect(-10f, -10f, 20f, 20f);
            var half = new Vector2(4f, 5f);

            Vector2 clamped = CameraSolver.ClampToBounds(new Vector2(100f, 100f), world, half);

            Assert.That(clamped.x, Is.EqualTo(6f).Within(1e-4f));
            Assert.That(clamped.y, Is.EqualTo(5f).Within(1e-4f));
        }

        [Test]
        public void ClampingLeavesAnInteriorPositionAlone()
        {
            var world = new Rect(-10f, -10f, 20f, 20f);
            var half = new Vector2(4f, 5f);

            Vector2 clamped = CameraSolver.ClampToBounds(new Vector2(1f, -2f), world, half);

            Assert.That(clamped.x, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(clamped.y, Is.EqualTo(-2f).Within(1e-4f));
        }

        [Test]
        public void AWorldNarrowerThanTheViewIsCentred()
        {
            // Without this case the two edge clamps fight each other and the camera jitters
            // between them. It is reachable in practice: a portrait phone is narrow, and the
            // map's width is still Q-030.
            var world = new Rect(-1f, -10f, 2f, 20f);
            var half = new Vector2(4f, 5f);

            Vector2 clamped = CameraSolver.ClampToBounds(new Vector2(50f, 0f), world, half);

            Assert.That(clamped.x, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void SmoothingApproachesTheGoalWithoutOvershooting()
        {
            float value = 0f;

            for (int i = 0; i < 200; i++)
            {
                value = CameraSolver.Smooth(value, 10f, 0.15f, 1f / 60f);
                Assert.That(value, Is.LessThanOrEqualTo(10f), "the camera overshot its goal");
            }

            Assert.That(value, Is.EqualTo(10f).Within(0.01f));
        }

        [Test]
        public void SmoothingIsFrameRateIndependent()
        {
            // ⚠ §33: gameplay must not change with the display. A per-frame lerp factor would
            // make the camera settle faster on a fast device — the same input producing a
            // different result on different hardware.
            float atSixty = 0f;
            for (int i = 0; i < 60; i++)
            {
                atSixty = CameraSolver.Smooth(atSixty, 10f, 0.2f, 1f / 60f);
            }

            float atThirty = 0f;
            for (int i = 0; i < 30; i++)
            {
                atThirty = CameraSolver.Smooth(atThirty, 10f, 0.2f, 1f / 30f);
            }

            Assert.That(atThirty, Is.EqualTo(atSixty).Within(0.02f),
                "one second of smoothing gave different results at different frame rates");
        }

        [Test]
        public void ZeroSmoothingSnaps()
        {
            // The pan is direct: while a finger is dragging the world, the finger IS the
            // camera, and any easing there reads as the view lagging the hand.
            Assert.That(CameraSolver.Smooth(0f, 7f, 0f, 1f / 60f), Is.EqualTo(7f));
        }
    }
}
