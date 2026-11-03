using System.Collections;
using NUnit.Framework;
using Smiley.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;

namespace Smiley.Tests
{
    /// <summary>
    /// §14.2 and §14.3 as they behave in the running game.
    /// </summary>
    /// <remarks>
    /// ⛔ <c>PlatformCountdownTests</c> pins every clause of §14.3 as arithmetic and would stay
    /// green if <c>DisappearingPlatform</c> were never constructed, never wired, or absent from
    /// the scene. These read the objects the game actually creates.
    /// </remarks>
    public sealed class PlatformTypeSceneTests
    {
        [UnitySetUp]
        public IEnumerator LoadTheGameScene()
        {
            yield return GameSceneFixture.LoadFresh();
        }

        [UnityTest]
        public IEnumerator BothNewPlatformTypesAreInTheScene()
        {
            Assert.That(Object.FindFirstObjectByType<SlipperyPlatform>(), Is.Not.Null,
                "no Slippery Platform, so §14.2 is unreachable");
            Assert.That(Object.FindFirstObjectByType<DisappearingPlatform>(), Is.Not.Null,
                "no Disappearing Platform, so §14.3 is unreachable");

            yield return null;
        }

        [UnityTest]
        public IEnumerator BothTypesAreStillPlatforms()
        {
            // §14 makes Slippery and Disappearing TYPES of platform, so §8's Ready rule and
            // §10's collision must apply to them exactly as to a Normal one. ⓘ This is the
            // opposite of the boundaries, which are deliberately NOT platforms (D-025).
            Assert.That(Object.FindFirstObjectByType<SlipperyPlatform>().GetComponent<Platform>(),
                Is.Not.Null, "the Slippery Platform is not a Platform");
            Assert.That(Object.FindFirstObjectByType<DisappearingPlatform>().GetComponent<Platform>(),
                Is.Not.Null, "the Disappearing Platform is not a Platform");

            yield return null;
        }

        [UnityTest]
        public IEnumerator SlipperyIsActuallySlipperierThanNormal()
        {
            // §14.2: "Lower friction. Smiley rolls/slides farther. Smiley decelerates more
            // slowly."
            //
            // ⚠ Measured rather than read off the asset. Unity combines the friction of BOTH
            // colliders, so a low number on the platform does not by itself prove the contact
            // is slippery — the only honest check is how far he actually slides.
            var slippery = Object.FindFirstObjectByType<SlipperyPlatform>();
            Assert.That(slippery, Is.Not.Null);

            var normal = GameObject.Find("Platform_01");
            Assert.That(normal, Is.Not.Null, "the test bed has no Normal platform to compare against");

            float keptOnNormal = 0f;
            yield return SpeedKeptOn(normal, r => keptOnNormal = r);

            float keptOnSlippery = 0f;
            yield return SpeedKeptOn(slippery.gameObject, r => keptOnSlippery = r);

            Assert.That(keptOnNormal, Is.GreaterThan(0f).And.LessThan(1f),
                $"the Normal platform kept {keptOnNormal:0.00} of the shove — either he never " +
                "landed on it, or he never slowed at all, and either way the comparison is empty");

            // ⚠ The margin is deliberately modest, and the reason is physical rather than a
            // concession. Smiley is a BALL: he mostly ROLLS, and rolling loses far less to
            // surface friction than sliding does — so a platform with a fifth of the friction
            // does not produce a fifth of the deceleration. Measured, the retained speed is
            // about 0.82 against 0.73.
            //
            // ⛔ What this must still catch is a Slippery Platform that is not slippery at all
            // — a material that never got applied, or one applied to the wrong collider.
            Assert.That(keptOnSlippery, Is.GreaterThan(keptOnNormal * 1.05f),
                $"kept {keptOnSlippery:0.00} of his speed on Slippery against {keptOnNormal:0.00} " +
                "on Normal — §14.2 requires that he decelerates MORE SLOWLY on Slippery");
        }

        /// <summary>
        /// Shoves Smiley along a platform and reports what fraction of that shove survives.
        /// </summary>
        /// <remarks>
        /// ⚠ <b>Speed retained, not distance travelled.</b> The first version of this measured
        /// how far he ended up from where he started, which on a platform less than two units
        /// wide mostly measured how he fell off the end — it reported the Normal platform as
        /// the slipperier of the two. §14.2's actual claim is that he <i>"decelerates more
        /// slowly"</i>, and that is what this reads, while he is still in contact.
        /// </remarks>
        private IEnumerator SpeedKeptOn(GameObject platform, System.Action<float> report)
        {
            var controller = Object.FindFirstObjectByType<SmileyController>();
            var body = controller.GetComponent<Rigidbody2D>();
            var contact = controller.GetComponent<PlatformContact>();

            var collider = platform.GetComponent<Collider2D>();
            Bounds bounds = collider.bounds;

            body.position = new Vector2(bounds.min.x + 0.35f, bounds.max.y + 0.55f);
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.WakeUp();

            for (int frame = 0; frame < 240; frame++)
            {
                yield return new WaitForFixedUpdate();
                if (contact.IsTouchingPlatform && body.linearVelocity.magnitude < 0.1f)
                {
                    break;
                }
            }

            // Gentle enough that he stays on a platform under two units wide.
            const float Shove = 1.6f;
            body.WakeUp();
            body.linearVelocity = Vector2.zero;
            body.AddForce(new Vector2(Shove, 0f), ForceMode2D.Impulse);

            // A short window, so the reading is about friction rather than about where the
            // platform ends.
            for (int frame = 0; frame < 15; frame++)
            {
                yield return new WaitForFixedUpdate();
            }

            float kept = contact.IsTouchingPlatform
                ? Mathf.Abs(body.linearVelocity.x) / (Shove / body.mass)
                : -1f;

            report(kept);
        }

        [UnityTest]
        public IEnumerator ADisappearingPlatformVanishesUnderSustainedContactAndComesBack()
        {
            // §14.3 end to end, through the real component: land, wait, fall through the gap,
            // and see it return.
            var vanishing = Object.FindFirstObjectByType<DisappearingPlatform>();
            var controller = Object.FindFirstObjectByType<SmileyController>();
            var body = controller.GetComponent<Rigidbody2D>();
            var collider = vanishing.GetComponent<Collider2D>();

            Assert.That(vanishing.IsPresent, Is.True, "it started already gone");

            Bounds bounds = collider.bounds;
            body.position = new Vector2(bounds.center.x, bounds.max.y + 0.6f);
            body.linearVelocity = Vector2.zero;
            body.WakeUp();

            bool vanished = false;
            for (int frame = 0; frame < 1200; frame++)
            {
                yield return new WaitForFixedUpdate();
                if (!vanishing.IsPresent)
                {
                    vanished = true;
                    break;
                }
            }

            Assert.That(vanished, Is.True,
                "the platform never disappeared under sustained contact");

            // §14.3: "Its collision disappears completely" — and if Smiley falls through the
            // former position before it respawns, "there must be no collision at that location".
            Assert.That(collider.enabled, Is.False,
                "the platform vanished but its collision is still there");

            bool returned = false;
            for (int frame = 0; frame < 1200; frame++)
            {
                yield return new WaitForFixedUpdate();
                if (vanishing.IsPresent)
                {
                    returned = true;
                    break;
                }
            }

            Assert.That(returned, Is.True, "the platform never respawned");
            Assert.That(collider.enabled, Is.True,
                "§14.3: collision must become active immediately on respawn");
        }

        [UnityTest]
        public IEnumerator ADisappearingPlatformIgnoresSmileyWhileHeIsElsewhere()
        {
            // §14.3: only contact starts it. Smiley resting on the Start Area, far away, must
            // leave it untouched — a countdown that ran regardless would be a timer, not a
            // reaction to the player.
            var vanishing = Object.FindFirstObjectByType<DisappearingPlatform>();
            float before = vanishing.Remaining;

            for (int frame = 0; frame < 300; frame++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(vanishing.IsCountingDown, Is.False,
                "the countdown is running with nobody on the platform");
            Assert.That(vanishing.Remaining, Is.EqualTo(before).Within(1e-3f),
                "the countdown advanced with nobody on the platform");
        }
    }
}
