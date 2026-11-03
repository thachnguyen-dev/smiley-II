using NUnit.Framework;
using Smiley.Gameplay;
using UnityEngine;

namespace Smiley.Tests
{
    /// <summary>
    /// §6's launch rules, and D-009's reading of where the drag is measured from.
    /// </summary>
    public sealed class LaunchSolverTests
    {
        private const float MaxDrag = 200f;
        private const float MaxForce = 10f;

        [Test]
        public void TheLaunchIsOppositeTheDrag()
        {
            // Drag down-left; §6 says Smiley leaves up-right.
            LaunchSolution s = LaunchSolver.Solve(
                new Vector2(500f, 500f), new Vector2(400f, 400f), MaxDrag, MaxForce);

            Assert.That(s.HasAim, Is.True);
            Assert.That(s.Direction.x, Is.GreaterThan(0f));
            Assert.That(s.Direction.y, Is.GreaterThan(0f));
            Assert.That(s.Direction.magnitude, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void EveryDirectionIsReachable()
        {
            // §6 grants a full 360 degrees. Sampling the circle proves no axis is special
            // and no arc is excluded -- an implementation that clamped upward, say, would
            // pass a single up-left case and fail here.
            for (int degrees = 0; degrees < 360; degrees += 15)
            {
                float radians = degrees * Mathf.Deg2Rad;
                var drag = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * 100f;

                LaunchSolution s = LaunchSolver.Solve(Vector2.zero, drag, MaxDrag, MaxForce);

                Assert.That(s.HasAim, Is.True, $"no aim at {degrees} degrees");
                Assert.That(Vector2.Dot(s.Direction, drag.normalized),
                    Is.EqualTo(-1f).Within(1e-3f), $"not opposite at {degrees} degrees");
            }
        }

        [Test]
        public void ForceGrowsWithDistanceUpToTheMaximum()
        {
            LaunchSolution quarter = LaunchSolver.Solve(
                Vector2.zero, new Vector2(MaxDrag * 0.25f, 0f), MaxDrag, MaxForce);
            LaunchSolution half = LaunchSolver.Solve(
                Vector2.zero, new Vector2(MaxDrag * 0.5f, 0f), MaxDrag, MaxForce);

            Assert.That(half.Force, Is.GreaterThan(quarter.Force));
            Assert.That(half.Charge, Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void DraggingBeyondTheMaximumAddsNoForce()
        {
            // §6: "the player cannot drag farther" and "Launch Force cannot increase further".
            LaunchSolution atLimit = LaunchSolver.Solve(
                Vector2.zero, new Vector2(MaxDrag, 0f), MaxDrag, MaxForce);
            LaunchSolution farBeyond = LaunchSolver.Solve(
                Vector2.zero, new Vector2(MaxDrag * 8f, 0f), MaxDrag, MaxForce);

            Assert.That(farBeyond.Force, Is.EqualTo(atLimit.Force).Within(1e-4f));
            Assert.That(farBeyond.Charge, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(farBeyond.Direction, Is.EqualTo(atLimit.Direction));
        }

        [Test]
        public void AWanderingDragIsWorthNoMoreThanADirectOne()
        {
            // §6: "the FINAL drag distance determines Launch Force", and drag speed is
            // explicitly irrelevant.
            //
            // The bug this is pointed at: measuring the drag as the total path the finger
            // travelled rather than its displacement from the touch-down point. That
            // implementation is easy to reach for -- accumulate a length per frame as the
            // pointer moves -- and it reads as correct until someone wiggles. Under it, a
            // player could scrub back and forth to charge up without ever moving away from
            // where they started, and drag speed would become force, because a faster drag
            // covers more path in the same gesture.
            var start = Vector2.zero;
            var end = new Vector2(120f, 0f);

            // A drag that rambles far and wide before finishing at `end`.
            Vector2[] path =
            {
                new Vector2(60f, 0f),
                new Vector2(60f, 180f),
                new Vector2(-140f, 180f),
                new Vector2(-140f, -90f),
                new Vector2(200f, -90f),
                end,
            };

            LaunchSolution wandered = LaunchSolution.None;
            foreach (Vector2 point in path)
            {
                wandered = LaunchSolver.Solve(start, point, MaxDrag, MaxForce);
            }

            LaunchSolution direct = LaunchSolver.Solve(start, end, MaxDrag, MaxForce);

            Assert.That(wandered.Force, Is.EqualTo(direct.Force).Within(1e-6f));
            Assert.That(wandered.Direction, Is.EqualTo(direct.Direction));
            Assert.That(wandered.Charge, Is.EqualTo(direct.Charge).Within(1e-6f));
        }

        [Test]
        public void AZeroLengthDragHasNoAim()
        {
            // Normalising a zero vector yields NaN; an unguarded solver would launch Smiley
            // to nowhere at all, or not at all, depending on the platform's float handling.
            LaunchSolution s = LaunchSolver.Solve(
                new Vector2(10f, 10f), new Vector2(10f, 10f), MaxDrag, MaxForce);

            Assert.That(s.HasAim, Is.False);
            Assert.That(s.Force, Is.EqualTo(0f));
        }

        [Test]
        public void ForceStartsAtZeroOnTouchDown()
        {
            // D-009's reason for choosing the touch-down origin: there is a natural dead
            // zone, and a tap that does not move cannot fire Smiley across the map.
            LaunchSolution s = LaunchSolver.Solve(
                new Vector2(300f, 300f), new Vector2(300f, 300f), MaxDrag, MaxForce);

            Assert.That(s.Charge, Is.EqualTo(0f));
        }

        [Test]
        public void TheClampedDragPointStopsAtTheLimit()
        {
            Vector2 clamped = LaunchSolver.ClampedDragPoint(
                Vector2.zero, new Vector2(MaxDrag * 3f, 0f), MaxDrag);

            Assert.That(clamped.x, Is.EqualTo(MaxDrag).Within(1e-3f));
            Assert.That(clamped.y, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void TheClampedDragPointIsUntouchedInsideTheLimit()
        {
            var inside = new Vector2(MaxDrag * 0.5f, 0f);
            Vector2 clamped = LaunchSolver.ClampedDragPoint(Vector2.zero, inside, MaxDrag);

            Assert.That(clamped, Is.EqualTo(inside));
        }
    }
}
