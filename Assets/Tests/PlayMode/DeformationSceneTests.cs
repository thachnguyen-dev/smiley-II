using System.Collections;
using NUnit.Framework;
using Smiley.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;

namespace Smiley.Tests
{
    /// <summary>
    /// Deformation as it behaves in the running game, not as arithmetic.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>The Edit Mode deformation tests would all stay green if the collider were never
    /// rebuilt.</b> They test the shape maths; nothing in them knows whether the physics body
    /// ever changes. These read the collider the engine is actually colliding with.
    /// </remarks>
    public sealed class DeformationSceneTests
    {
        [UnitySetUp]
        public IEnumerator LoadTheGameScene()
        {
            yield return GameSceneFixture.LoadFresh();
        }

        private static SmileyDeformer Deformer()
        {
            var deformer = Object.FindFirstObjectByType<SmileyDeformer>();
            Assert.That(deformer, Is.Not.Null, "the scene has no SmileyDeformer");
            return deformer;
        }

        [UnityTest]
        public IEnumerator TheColliderIsNotACircle()
        {
            // §11, and the owner's ruling that the asymmetry reaches the physics: the body
            // the engine collides with must itself be uneven.
            SmileyDeformer deformer = Deformer();
            yield return null;

            Vector2[] points = deformer.ColliderPoints;
            Assert.That(points, Is.Not.Null.And.Length.GreaterThan(5));

            float min = float.MaxValue;
            float max = float.MinValue;
            foreach (Vector2 p in points)
            {
                min = Mathf.Min(min, p.magnitude);
                max = Mathf.Max(max, p.magnitude);
            }

            Assert.That(max - min, Is.GreaterThan(0.001f), "the collider is a perfect circle");
        }

        [UnityTest]
        public IEnumerator MassDoesNotChangeWhenTheBodyDeforms()
        {
            // ⛔ §11: "Mass remains constant." This is the trap in the whole slice. A
            // PolygonCollider2D that is rebuilt every frame will silently retune the body's
            // mass on every rebuild if useAutoMass is left on -- so Smiley would get heavier
            // and lighter as he squashed, and nothing would report it.
            SmileyDeformer deformer = Deformer();
            var body = deformer.GetComponent<Rigidbody2D>();

            Assert.That(body.useAutoMass, Is.False, "auto-mass would retune mass as the shape changes");

            float massAtRest = body.mass;

            // Drive a real impact.
            body.AddForce(new Vector2(0f, -30f), ForceMode2D.Impulse);

            for (int frame = 0; frame < 120; frame++)
            {
                yield return new WaitForFixedUpdate();
                Assert.That(body.mass, Is.EqualTo(massAtRest).Within(1e-5f),
                    $"mass moved to {body.mass} on frame {frame}");
            }
        }

        [UnityTest]
        public IEnumerator AnImpactActuallyDeformsThePhysicsBody()
        {
            // The whole point of the owner's "maximum use of physics": the collider changes,
            // not just the sprite. If SmileyDeformer never rebuilt the path, every Edit Mode
            // test would still pass and this would fail.
            SmileyDeformer deformer = Deformer();
            var body = deformer.GetComponent<Rigidbody2D>();

            // Let him settle first, so the starting shape is the base one.
            for (int frame = 0; frame < 300; frame++)
            {
                yield return new WaitForFixedUpdate();
                if (deformer.PhysicalMagnitude < 0.001f && body.linearVelocity.magnitude < 0.1f)
                {
                    break;
                }
            }

            Vector2[] before = deformer.ColliderPoints;
            float restMagnitude = deformer.PhysicalMagnitude;

            body.AddForce(new Vector2(0f, -40f), ForceMode2D.Impulse);

            float peak = 0f;
            Vector2[] during = null;
            for (int frame = 0; frame < 90; frame++)
            {
                yield return new WaitForFixedUpdate();
                if (deformer.PhysicalMagnitude > peak)
                {
                    peak = deformer.PhysicalMagnitude;
                    during = deformer.ColliderPoints;
                }
            }

            Assert.That(peak, Is.GreaterThan(restMagnitude + 0.01f),
                "the impact produced no deformation");
            Assert.That(during, Is.Not.Null);

            bool anyPointMoved = false;
            for (int i = 0; i < before.Length; i++)
            {
                if ((before[i] - during[i]).sqrMagnitude > 1e-5f)
                {
                    anyPointMoved = true;
                    break;
                }
            }

            Assert.That(anyPointMoved, Is.True, "the collider path never changed");
        }

        [UnityTest]
        public IEnumerator TheBodyReturnsToItsBaseShape()
        {
            // §11: "When the force ends, deformation gradually returns toward the natural base
            // shape." Left alone on a platform, the physics body must come back to rest.
            SmileyDeformer deformer = Deformer();
            var body = deformer.GetComponent<Rigidbody2D>();

            body.AddForce(new Vector2(0f, -40f), ForceMode2D.Impulse);
            yield return new WaitForFixedUpdate();

            for (int frame = 0; frame < 600; frame++)
            {
                yield return new WaitForFixedUpdate();
                if (deformer.PhysicalMagnitude < 0.005f)
                {
                    break;
                }
            }

            Assert.That(deformer.PhysicalMagnitude, Is.LessThan(0.005f),
                "the body never returned to its base shape");
        }

        [UnityTest]
        public IEnumerator SmileyStaysPutWhenNothingTouchesHim()
        {
            // ⛔ THIS FIXTURE EXISTS BECAUSE A DEPLOY FOUND WHAT THE SUITE COULD NOT.
            //
            // On hardware, Smiley resting on the Start Area drifted sideways and then rose
            // off it entirely — at 7s he was on the platform, by 70s he was airborne and a
            // long way left. Every other test here settles him and then looks within about
            // twelve seconds, which is well before the drift is visible. A green suite said
            // the deformation was correct while the character slowly levitated.
            //
            // §5 and §9 between them leave no room for this: gravity always acts, and nothing
            // in the game pushes Smiley except a launch the player asked for.
            var deformer = Deformer();
            var body = deformer.GetComponent<Rigidbody2D>();
            var contact = deformer.GetComponent<PlatformContact>();

            for (int frame = 0; frame < 600 && !contact.IsTouchingPlatform; frame++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(contact.IsTouchingPlatform, Is.True, "Smiley never landed at all");

            Vector2 landed = body.position;

            // Long enough for a slow pump to show. At the observed rate this is several
            // times the threshold below.
            for (int frame = 0; frame < 3000; frame++)
            {
                yield return new WaitForFixedUpdate();
            }

            Vector2 drift = body.position - landed;

            Assert.That(contact.IsTouchingPlatform, Is.True,
                $"Smiley left the platform without being launched; he moved {drift}");

            Assert.That(Mathf.Abs(drift.y), Is.LessThan(0.25f),
                $"Smiley rose or sank {drift.y} units while resting — nothing should move him");

            Assert.That(Mathf.Abs(drift.x), Is.LessThan(0.5f),
                $"Smiley drifted {drift.x} units sideways while resting");
        }

        [UnityTest]
        public IEnumerator TheIdleWobbleNeverReachesThePhysics()
        {
            // ⛔ §12: "Idle deformation is visual only. Idle deformation does not affect
            // physics." Once settled, the SPRITE keeps breathing while the collider must sit
            // perfectly still. This is the assertion that keeps the two channels apart -- and
            // it fails the moment someone "simplifies" them into one.
            SmileyDeformer deformer = Deformer();
            var body = deformer.GetComponent<Rigidbody2D>();

            // ⚠ Settled means HELD still, not still for one step (D-069): he now spawns a hair
            // above the Start Area and micro-bounces on the inset surface for a few steps.
            int held = 0;

            for (int frame = 0; frame < 900 && held < 15; frame++)
            {
                yield return new WaitForFixedUpdate();
                held = deformer.PhysicalMagnitude < 0.001f && body.linearVelocity.magnitude < 0.02f ? held + 1 : 0;
            }

            Assert.That(deformer.PhysicalMagnitude, Is.LessThan(0.001f), "Smiley never settled");

            Vector2[] settled = deformer.ColliderPoints;
            Transform frameTransform = deformer.transform.Find("DeformFrame");
            Assert.That(frameTransform, Is.Not.Null, "there is no DeformFrame");

            // Sample across frames rather than comparing two instants. A sine read twice
            // could return the same value by coincidence, which would make this pass or fail
            // on timing rather than on behaviour.
            float widestSeen = 0f;
            for (int i = 0; i < 120; i++)
            {
                yield return null;

                Vector2[] now = deformer.ColliderPoints;
                for (int p = 0; p < settled.Length; p++)
                {
                    // ⚠ The threshold expresses the INTENT, not a numerical epsilon. The idle
                    // wobble would displace a rim point by roughly amplitude × radius —
                    // order 1e-2 units, or 1e-4 squared. Anything below 1e-5 squared is
                    // therefore two orders of magnitude too small to be the wobble, and is
                    // just float noise from rebuilding the path. A tighter bound tests the
                    // arithmetic of Box2D rather than whether §12 is obeyed.
                    Assert.That((settled[p] - now[p]).sqrMagnitude, Is.LessThan(1e-5f),
                        "the collider moved while only the idle wobble was running");
                }

                widestSeen = Mathf.Max(
                    widestSeen, Mathf.Abs(frameTransform.localScale.x - 1f));
            }

            Assert.That(widestSeen, Is.GreaterThan(0.001f),
                "the idle wobble never visibly moved the sprite");
        }
    }
}
