using System.Collections;
using NUnit.Framework;
using Smiley.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;

namespace Smiley.Tests
{
    /// <summary>§13's rotation rules in the running game.</summary>
    /// <remarks>
    /// ⛔ <c>UprightSettleTests</c> pins the return maths and would stay green if
    /// <c>SmileyRotation</c> were never constructed or never wired. These read the body the
    /// game actually simulates.
    /// </remarks>
    public sealed class RotationSceneTests
    {
        [UnitySetUp]
        public IEnumerator LoadTheGameScene()
        {
            yield return GameSceneFixture.LoadFresh();
        }

        private static SmileyRotation Rotation()
        {
            var rotation = Object.FindFirstObjectByType<SmileyRotation>();
            Assert.That(rotation, Is.Not.Null, "the scene has no SmileyRotation");
            return rotation;
        }

        private static IEnumerator Settle(SmileyController controller)
        {
            for (int frame = 0; frame < 600 && !controller.IsReady; frame++)
            {
                yield return new WaitForFixedUpdate();
            }
        }

        [UnityTest]
        public IEnumerator SmileyDoesNotRotateWhileAirborne()
        {
            // §13: "While airborne or falling, Smiley does not rotate according to physics.
            // Smiley maintains its current orientation."
            //
            // ⚠ Asserted as the ANGLE holding, not just the angular velocity being zeroed —
            // zeroing the velocity every step and still drifting would pass a weaker check.
            Rotation();
            var controller = Object.FindFirstObjectByType<SmileyController>();
            var body = controller.GetComponent<Rigidbody2D>();
            var contact = controller.GetComponent<PlatformContact>();

            yield return Settle(controller);

            // ⛔ THE COLUMN MATTERS, AND THE FIRST VERSION OF THIS TEST DID NOT CHOOSE ONE.
            //
            // Launched from where he happens to rest, Smiley's "flight" passes within a radius
            // of Platform_01 and Platform_03 — the test bed is dense — so he GRAZES them on the
            // way up. Those frames are contacts, where §13 hands rotation to physics on purpose,
            // and a graze at speed spun him to 3700 deg/s. The assertion below was reading real
            // collisions and calling them mid-air rotation.
            //
            // ⚠ The column moved from x = 4.7 to x = -4.6 when D-049 widened every platform:
            // Platform_07 now reaches x = 4.30, and 4.7 left barely a tenth of a unit.
            // ⛔ Not -7: the invisible walls are at ±5.5, and outside them there is no world.
            //
            // ⓘ It only began failing when D-034 stopped throwing the spin away every airborne
            // frame, which is what made the grazes visible. The test was measuring flight by
            // HEIGHT, and height is not the same question as contact.
            // ⭐ Since D-076 the map is the owner's and dense; this is a test about Smiley, so
            // the map is switched off and the column found rather than known.
            GameSceneFixture.ClearTheSky();
            body.position = new Vector2(GameSceneFixture.ClearColumn(body.position.y, body.position.y + 10f), body.position.y);
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;

            for (int settling = 0; settling < 120 && !controller.IsReady; settling++)
            {
                yield return new WaitForFixedUpdate();
            }

            float groundY = body.position.y;

            // ⚠ Hard enough to tumble a physics-driven body, and NOT hard enough to reach the
            // ceiling: the top boundary (D-025) is a surface too, and bouncing off it would put
            // contacts back into the middle of the measurement.
            body.WakeUp();
            body.AddForce(new Vector2(0f, 12f), ForceMode2D.Impulse);
            body.angularVelocity = 720f;

            // Flight is detected by HEIGHT, not by the contact flag. The collision callbacks
            // flicker for a step or two as he separates, so waiting on IsTouchingPlatform
            // exits on a transient and then finds "contact" again on the very next frame -
            // which reported a one-frame flight for a launch that clearly left the ground.
            const float Clear = 1.2f;

            for (int frame = 0; frame < 240 && body.position.y < groundY + Clear; frame++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(body.position.y, Is.GreaterThan(groundY + Clear),
                "he never got clear of the ground");

            float held = body.rotation;
            int airborneFrames = 0;
            int grazes = 0;

            for (int frame = 0; frame < 120; frame++)
            {
                yield return new WaitForFixedUpdate();

                if (body.position.y < groundY + Clear * 0.5f)
                {
                    break;
                }

                // ⛔ A contact is not mid-air. §13 gives rotation to physics the moment he
                // touches something, so a frame with a contact in it is outside this rule
                // rather than a violation of it — and with the clear column above there should
                // not be one at all, which is why this counts them.
                if (contact.IsTouchingPlatform)
                {
                    grazes++;
                    continue;
                }

                airborneFrames++;

                Assert.That(Mathf.Abs(Mathf.DeltaAngle(held, body.rotation)), Is.LessThan(1f),
                    $"Smiley rotated {Mathf.DeltaAngle(held, body.rotation)} degrees in mid-air " +
                    $"at frame {frame}");
            }

            Assert.That(airborneFrames, Is.GreaterThan(10),
                $"only {airborneFrames} frames of flight, which proves little");

            Assert.That(grazes, Is.EqualTo(0),
                $"{grazes} of the measured frames were contacts — the launch column is not " +
                "clear, so this is no longer a test about being airborne");
        }

        [UnityTest]
        public IEnumerator SmileyReturnsUprightAfterRollingStops()
        {
            // §13: "After rolling stops, body and face gradually return toward their correct /
            // original orientation."
            SmileyRotation rotation = Rotation();
            var controller = Object.FindFirstObjectByType<SmileyController>();
            var body = controller.GetComponent<Rigidbody2D>();

            yield return Settle(controller);

            // Put him plainly askew while resting.
            body.MoveRotation(70f);
            body.angularVelocity = 0f;

            for (int frame = 0; frame < 5; frame++)
            {
                yield return new WaitForFixedUpdate();
            }

            float leaning = UprightSettle.OffsetFromUpright(body.rotation);
            Assert.That(leaning, Is.GreaterThan(20f), "he did not stay leaning long enough to test");

            for (int frame = 0; frame < 600; frame++)
            {
                yield return new WaitForFixedUpdate();
                if (UprightSettle.OffsetFromUpright(body.rotation) < 1f)
                {
                    break;
                }
            }

            Assert.That(UprightSettle.OffsetFromUpright(body.rotation), Is.LessThan(2f),
                "Smiley never returned to upright after coming to rest");
        }

        [UnityTest]
        public IEnumerator TheReturnIsGradualAndNotASnap()
        {
            // ⛔ §13: "They must not snap immediately." Observed in the running game, not just
            // in the arithmetic: one physics step must not take him from askew to upright.
            var controller = Object.FindFirstObjectByType<SmileyController>();
            var body = controller.GetComponent<Rigidbody2D>();

            yield return Settle(controller);

            body.MoveRotation(70f);
            body.angularVelocity = 0f;
            yield return new WaitForFixedUpdate();

            float before = body.rotation;
            yield return new WaitForFixedUpdate();
            float after = body.rotation;

            Assert.That(UprightSettle.OffsetFromUpright(after), Is.GreaterThan(20f),
                $"one step took him from {before} to {after} — that is a snap, not a return");
        }

        [UnityTest]
        public IEnumerator PhysicsStillDrivesRotationWhileRolling()
        {
            // §13: "Rolling rotation speed is determined ENTIRELY by physics." So while he is
            // moving on a platform, this component must keep its hands off — a test that only
            // checked the airborne hold and the upright return would pass an implementation
            // that froze him solid on the ground.
            var controller = Object.FindFirstObjectByType<SmileyController>();
            var body = controller.GetComponent<Rigidbody2D>();
            var contact = controller.GetComponent<PlatformContact>();

            yield return Settle(controller);

            body.WakeUp();
            body.AddForce(new Vector2(5f, 0f), ForceMode2D.Impulse);

            float mostSpin = 0f;
            for (int frame = 0; frame < 60; frame++)
            {
                yield return new WaitForFixedUpdate();
                if (contact.IsTouchingPlatform)
                {
                    mostSpin = Mathf.Max(mostSpin, Mathf.Abs(body.angularVelocity));
                }
            }

            Assert.That(mostSpin, Is.GreaterThan(10f),
                "Smiley barely rotated while rolling along a platform — something is holding " +
                "him still that §13 says should be left entirely to physics");
        }
    }
}
