using System.Collections;
using NUnit.Framework;
using Smiley.Gameplay;
using Smiley.View;
using UnityEngine;
using UnityEngine.TestTools;

namespace Smiley.Tests
{
    /// <summary>
    /// The camera as it behaves in the running game.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>`CameraSolverTests` would stay green if the rig were never constructed, never
    /// wired, or absent from the scene.</b> They test arithmetic; nothing in them knows
    /// whether a camera ever moves. That mistake has already cost this project a whole slice
    /// (D-018), so these read the camera the game actually renders through.
    /// </remarks>
    public sealed class CameraSceneTests
    {
        [UnitySetUp]
        public IEnumerator LoadTheGameScene()
        {
            yield return GameSceneFixture.LoadFresh();
        }

        private static CameraRig Rig()
        {
            var rig = Object.FindFirstObjectByType<CameraRig>();
            Assert.That(rig, Is.Not.Null, "the scene has no CameraRig");
            return rig;
        }

        [UnityTest]
        public IEnumerator TheRigIsWiredAndCanRun()
        {
            Assert.That(Rig().IsConfigured, Is.True,
                "the camera rig is missing a reference, so LateUpdate does nothing");

            Assert.That(Object.FindFirstObjectByType<LevelBounds>(), Is.Not.Null,
                "the scene has no LevelBounds, so the camera has nothing to clamp against");

            yield return null;
        }

        [UnityTest]
        public IEnumerator TheCameraFollowsSmileyUpward()
        {
            // §17: "The camera continuously follows Smiley vertically." Without this the game
            // cannot be played at all — it is a climb (§1), and a fixed view loses him on the
            // first good launch.
            CameraRig rig = Rig();
            var controller = Object.FindFirstObjectByType<SmileyController>();
            var body = controller.GetComponent<Rigidbody2D>();

            for (int frame = 0; frame < 600 && !controller.IsReady; frame++)
            {
                yield return new WaitForFixedUpdate();
            }

            float cameraBefore = rig.transform.position.y;

            // ⚠ MOVED DELIBERATELY, NOT LAUNCHED.
            //
            // An earlier version fired an impulse and hoped. Two things made that fragile: how
            // high a given impulse carries him varies run to run, and the camera is CLAMPED at
            // the world's lower edge (D-016), so it cannot move at all until he climbs past
            // that floor. A launch that fell slightly short reported "the camera did not
            // follow" about a camera that was behaving exactly as ruled.
            //
            // §17's claim is that the camera follows him vertically. How he got there is not
            // part of it.
            var bounds = Object.FindFirstObjectByType<LevelBounds>();
            float clampFloor = bounds.World.yMin + rig.ViewHalfExtents.y;

            body.WakeUp();
            body.position = new Vector2(body.position.x, clampFloor + 3f);
            body.linearVelocity = Vector2.zero;

            float cameraHighest = rig.transform.position.y;

            for (int frame = 0; frame < 120; frame++)
            {
                yield return new WaitForFixedUpdate();

                // The camera's PEAK, not where it ends up: he falls back down, and a correct
                // camera follows him both ways — so a final reading would find it where it
                // started and call that a failure.
                cameraHighest = Mathf.Max(cameraHighest, rig.transform.position.y);
            }

            Assert.That(cameraHighest, Is.GreaterThan(cameraBefore + 0.5f),
                "the camera did not follow Smiley upward");
        }

        [UnityTest]
        public IEnumerator SmileyStaysOnScreenWhileClimbing()
        {
            // The point of the follow, stated as the property that matters rather than as a
            // camera position: the player must be able to see the character they control.
            CameraRig rig = Rig();
            var controller = Object.FindFirstObjectByType<SmileyController>();
            var body = controller.GetComponent<Rigidbody2D>();
            var camera = rig.GetComponent<Camera>();

            for (int frame = 0; frame < 600 && !controller.IsReady; frame++)
            {
                yield return new WaitForFixedUpdate();
            }

            body.WakeUp();
            body.AddForce(new Vector2(3f, 24f), ForceMode2D.Impulse);

            for (int frame = 0; frame < 200; frame++)
            {
                yield return new WaitForFixedUpdate();

                Vector3 viewport = camera.WorldToViewportPoint(body.position);

                Assert.That(viewport.y, Is.GreaterThan(-0.25f).And.LessThan(1.25f),
                    $"Smiley left the view vertically at frame {frame} (viewport y {viewport.y})");
            }
        }

        [UnityTest]
        public IEnumerator TheCameraNeverShowsBeyondTheAuthoredWorld()
        {
            // ⛔ D-016: clamping beats centring. Even chasing Smiley hard, the view stays
            // inside LevelBounds — otherwise the player sees the edge of the world, which in a
            // hand-crafted map (§15) is scenery nobody authored.
            CameraRig rig = Rig();
            var bounds = Object.FindFirstObjectByType<LevelBounds>();
            var controller = Object.FindFirstObjectByType<SmileyController>();
            var body = controller.GetComponent<Rigidbody2D>();

            body.WakeUp();
            body.AddForce(new Vector2(30f, 40f), ForceMode2D.Impulse);

            Rect world = bounds.World;

            for (int frame = 0; frame < 240; frame++)
            {
                yield return new WaitForFixedUpdate();

                Vector2 half = rig.ViewHalfExtents;
                Vector2 p = rig.transform.position;

                // Only meaningful on an axis where the world is larger than the view; a world
                // narrower than the window is centred instead, and has no inside to stay in.
                if (world.width > half.x * 2f)
                {
                    Assert.That(p.x, Is.GreaterThanOrEqualTo(world.xMin + half.x - 0.01f));
                    Assert.That(p.x, Is.LessThanOrEqualTo(world.xMax - half.x + 0.01f));
                }

                if (world.height > half.y * 2f)
                {
                    Assert.That(p.y, Is.GreaterThanOrEqualTo(world.yMin + half.y - 0.01f));
                    Assert.That(p.y, Is.LessThanOrEqualTo(world.yMax - half.y + 0.01f));
                }
            }
        }

        [UnityTest]
        public IEnumerator TheCameraHoldsStillForSmallMovements()
        {
            // D-016's deadzone, observed rather than calculated: nudging Smiley within the
            // band must not move the view. A continuous-X implementation passes every other
            // test here and fails this one.
            CameraRig rig = Rig();
            var controller = Object.FindFirstObjectByType<SmileyController>();
            var body = controller.GetComponent<Rigidbody2D>();

            for (int frame = 0; frame < 600 && !controller.IsReady; frame++)
            {
                yield return new WaitForFixedUpdate();
            }

            // Let the follow settle first, so we measure the deadzone and not the approach.
            for (int frame = 0; frame < 120; frame++)
            {
                yield return new WaitForFixedUpdate();
            }

            float before = rig.transform.position.x;

            // A small sideways shove — well inside the band.
            body.WakeUp();
            body.AddForce(new Vector2(0.6f, 0f), ForceMode2D.Impulse);

            for (int frame = 0; frame < 60; frame++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(Mathf.Abs(rig.transform.position.x - before), Is.LessThan(0.2f),
                "the camera tracked a small sideways movement — the horizontal deadzone is not working");
        }
    }
}
