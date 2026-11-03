using System.Collections;
using NUnit.Framework;
using Smiley.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;

namespace Smiley.Tests
{
    /// <summary>
    /// The invisible edges of the gameplay area (owner, 2026-09-10).
    /// </summary>
    public sealed class BoundarySceneTests
    {
        [UnitySetUp]
        public IEnumerator LoadTheGameScene()
        {
            yield return GameSceneFixture.LoadFresh();
        }

        private static Rigidbody2D Body()
        {
            var controller = Object.FindFirstObjectByType<SmileyController>();
            Assert.That(controller, Is.Not.Null);
            return controller.GetComponent<Rigidbody2D>();
        }

        [UnityTest]
        public IEnumerator TheGameplayAreaHasFourEdges()
        {
            var boundaries = Object.FindObjectsByType<Boundary>(FindObjectsSortMode.None);
            Assert.That(boundaries.Length, Is.EqualTo(4),
                "expected two walls and two boundaries");

            yield return null;
        }

        [UnityTest]
        public IEnumerator BoundariesAreNotPlatforms()
        {
            // ⛔ THE DISTINCTION THAT KEEPS §8 INTACT.
            //
            // §8 makes Ready depend on "contact with a Platform". If the walls carried
            // Platform, Smiley pressed against one — sliding down it, in mid-air — would
            // become Ready and could re-launch off nothing. The owner named walls and
            // boundaries as distinct from platforms, and this is where that stays true.
            foreach (Boundary boundary in Object.FindObjectsByType<Boundary>(FindObjectsSortMode.None))
            {
                Assert.That(boundary.GetComponent<Platform>(), Is.Null,
                    $"{boundary.name} is also a Platform, so Smiley could go Ready against it");
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator TheWorldIsSixScreensWideAndNineteenTall()
        {
            // ⭐ The owner's ruling, measured rather than assumed: 6480 × 46080 px on a
            // 1080 × 2400 screen (Q-030, D-081) — 6 wide, 19.2 tall. ⓘ D-028's "2–3× wide" was
            // superseded by the owner's own numbers the same day they doubled the map.
            //
            // ⚠ This test used to pin the HEIGHT to 2–3×, which was never ruled: the 2–3× was
            // horizontal, and the vertical was "large, no number yet" until 2026-09-11.
            //
            // ⓘ The multiples are an OUTCOME of a fixed world seen through a window, so the width
            // is checked on the reference aspect rather than the test window's.
            var bounds = Object.FindFirstObjectByType<LevelBounds>();
            Assert.That(bounds, Is.Not.Null);

            // ⓘ Against the REFERENCE viewport the ruling was made in — 10 units tall — not the
            // live camera, which the owner widened afterwards (D-091) without changing the map.
            const float ReferenceAspect = 1080f / 2400f;
            var viewport = new Vector2(
                MapRule.ReferenceViewportHeightUnits * ReferenceAspect,
                MapRule.ReferenceViewportHeightUnits);

            Vector2 multiple = bounds.ViewportMultiple(viewport);

            Assert.That(multiple.x, Is.EqualTo(6f).Within(0.01f),
                $"the world is {multiple.x:0.00}x the reference viewport wide, not the owner's 6480 / 1080");
            Assert.That(multiple.y, Is.EqualTo(19.2f).Within(0.01f),
                $"the world is {multiple.y:0.00}x the viewport tall, not the owner's 46080 / 2400");

            yield return null;
        }

        [UnityTest]
        public IEnumerator SmileyCannotLeaveThroughASideWall()
        {
            // "so that the player cannot fall or leave the gameplay area". Fired sideways hard
            // enough to clear the world several times over.
            Rigidbody2D body = Body();
            var bounds = Object.FindFirstObjectByType<LevelBounds>();
            Rect world = bounds.World;

            body.WakeUp();
            body.AddForce(new Vector2(200f, 0f), ForceMode2D.Impulse);

            for (int frame = 0; frame < 300; frame++)
            {
                yield return new WaitForFixedUpdate();

                Assert.That(body.position.x,
                    Is.LessThan(world.xMax + 1.5f).And.GreaterThan(world.xMin - 1.5f),
                    $"Smiley left the gameplay area sideways at frame {frame}, x = {body.position.x}");
            }
        }

        [UnityTest]
        public IEnumerator SmileyCannotLeaveThroughTheCeiling()
        {
            Rigidbody2D body = Body();
            var bounds = Object.FindFirstObjectByType<LevelBounds>();
            Rect world = bounds.World;

            body.WakeUp();
            body.AddForce(new Vector2(0f, 200f), ForceMode2D.Impulse);

            for (int frame = 0; frame < 300; frame++)
            {
                yield return new WaitForFixedUpdate();

                Assert.That(body.position.y, Is.LessThan(world.yMax + 1.5f),
                    $"Smiley left through the ceiling at frame {frame}, y = {body.position.y}");
            }
        }

        [UnityTest]
        public IEnumerator SmileyCannotFallOutOfTheWorld()
        {
            // §5 says a fall ends on the Start Area; the floor boundary only catches anything
            // that somehow gets past it. Fired straight down hard to try to find that case.
            Rigidbody2D body = Body();
            var bounds = Object.FindFirstObjectByType<LevelBounds>();
            Rect world = bounds.World;

            body.WakeUp();
            body.AddForce(new Vector2(0f, -200f), ForceMode2D.Impulse);

            for (int frame = 0; frame < 300; frame++)
            {
                yield return new WaitForFixedUpdate();

                Assert.That(body.position.y, Is.GreaterThan(world.yMin - 1.5f),
                    $"Smiley fell out of the world at frame {frame}, y = {body.position.y}");
            }
        }
    }
}
