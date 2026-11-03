using NUnit.Framework;
using Smiley.Gameplay;
using UnityEngine;

namespace Smiley.Tests
{
    /// <summary>§14.3's countdown, clause by clause.</summary>
    public sealed class PlatformCountdownTests
    {
        private const float Countdown = 2f;
        private const float Respawn = 3f;
        private const float Stable = 0.15f;
        private const float Recover = 2f;
        private const float Step = 1f / 60f;

        private static PlatformCountdown Fresh() => PlatformCountdown.Fresh(Countdown);

        private static void Hold(ref PlatformCountdown c, float seconds, bool touching)
        {
            int steps = Mathf.CeilToInt(seconds / Step);
            for (int i = 0; i < steps; i++)
            {
                c.Advance(touching, Step, Countdown, Respawn, Stable, Recover);
            }
        }

        [Test]
        public void AnUntouchedPlatformNeverCountsDown()
        {
            var c = Fresh();
            Hold(ref c, 10f, touching: false);

            Assert.That(c.Remaining, Is.EqualTo(Countdown).Within(1e-4f));
            Assert.That(c.Phase, Is.EqualTo(PlatformPhase.Present));
        }

        [Test]
        public void ABriefTouchWhileAirborneDoesNotStartIt()
        {
            // §14.3: "If Smiley only briefly touches the platform while airborne, the countdown
            // does not activate." This is the clause that makes a graze different from landing.
            var c = Fresh();

            for (int i = 0; i < 20; i++)
            {
                // A touch far shorter than the stability threshold, then gone again.
                c.Advance(true, Stable * 0.4f, Countdown, Respawn, Stable, Recover);
                c.Advance(false, Step, Countdown, Respawn, Stable, Recover);
            }

            Assert.That(c.Remaining, Is.EqualTo(Countdown).Within(1e-4f),
                "repeated grazes ate into the countdown");
            Assert.That(c.IsRunning, Is.False);
        }

        [Test]
        public void StableContactStartsIt()
        {
            var c = Fresh();
            Hold(ref c, Stable + 0.2f, touching: true);

            Assert.That(c.IsRunning, Is.True, "stable contact did not start the countdown");
            Assert.That(c.Remaining, Is.LessThan(Countdown));
        }

        [Test]
        public void LosingContactPausesItImmediately()
        {
            // §14.3: "If contact is lost, the countdown pauses IMMEDIATELY."
            var c = Fresh();
            Hold(ref c, 0.6f, touching: true);

            float atLoss = c.Remaining;
            Assert.That(atLoss, Is.LessThan(Countdown), "it never started, so this proves nothing");

            // Inside D-117's window: paused, not decaying and not yet recovered.
            Hold(ref c, Recover * 0.75f, touching: false);

            Assert.That(c.Remaining, Is.EqualTo(atLoss).Within(1e-4f),
                "the countdown kept running after contact was lost");
            Assert.That(c.IsRunning, Is.False);
        }

        [Test]
        public void ReturningWithinTheWindowResumesAndDoesNotReset()
        {
            // §14.3: "The countdown resumes from the remaining time. The countdown does not
            // reset." ⭐ Since D-117 that holds for 2 seconds off the platform; a quick hop off
            // and on still costs the time already used.
            var c = Fresh();
            Hold(ref c, 0.9f, touching: true);
            float afterFirstVisit = c.Remaining;

            Hold(ref c, Recover * 0.5f, touching: false);
            Hold(ref c, Stable + Step, touching: true);

            Assert.That(c.Remaining, Is.LessThan(afterFirstVisit),
                "it did not resume");
            Assert.That(c.Remaining, Is.LessThan(Countdown - 0.5f),
                "it reset on return instead of resuming");
        }

        [Test]
        public void AfterTwoSecondsAwayTheDurabilityRecoversInFull()
        {
            // ⭐ D-117 (owner): "After the player leaves a Disappearing platform, its durability
            // should automatically recover after 2 seconds."
            var c = Fresh();
            Hold(ref c, 1.2f, touching: true);
            Assert.That(c.Remaining, Is.LessThan(Countdown - 0.9f), "it never started");

            Hold(ref c, Recover - Step, touching: false);
            Assert.That(c.Remaining, Is.LessThan(Countdown), "it recovered before the window closed");

            Hold(ref c, Step * 2, touching: false);
            Assert.That(c.Remaining, Is.EqualTo(Countdown), "two seconds away did not restore it");
            Assert.That(c.Phase, Is.EqualTo(PlatformPhase.Present));
        }

        [Test]
        public void RecoveryIsCountedOnlyWhileAwayAndRestartsOnEachVisit()
        {
            // Off for most of the window, back on briefly, off again: the clock restarts on the
            // return, so the second absence has to be the full two seconds by itself.
            var c = Fresh();
            Hold(ref c, 1.0f, touching: true);
            float used = c.Remaining;

            Hold(ref c, Recover * 0.9f, touching: false);
            Hold(ref c, Stable + Step * 3, touching: true);
            float afterReturn = c.Remaining;
            Assert.That(afterReturn, Is.LessThan(used), "the return did not resume the countdown");

            Hold(ref c, Recover * 0.9f, touching: false);
            Assert.That(c.Remaining, Is.EqualTo(afterReturn).Within(1e-4f),
                "the second absence recovered early — the away clock carried over from the first");

            Hold(ref c, Recover * 0.2f, touching: false);
            Assert.That(c.Remaining, Is.EqualTo(Countdown), "a full window away did not recover it");
        }

        [Test]
        public void AnUntouchedPlatformIsNotRecoveringFromAnything()
        {
            var c = Fresh();
            Hold(ref c, Recover * 3f, touching: false);
            Assert.That(c.AwayHeld, Is.EqualTo(0f));
            Assert.That(c.Remaining, Is.EqualTo(Countdown));
        }

        [Test]
        public void ItKeepsRunningWhileSmileyIsReady()
        {
            // §14.3: the countdown continues while Smiley remains in contact, "INCLUDING while
            // Smiley is in the Ready state". So nothing about his speed may gate it — a player
            // sitting still and thinking is still burning the platform.
            var c = Fresh();
            Hold(ref c, Countdown * 0.5f, touching: true);

            Assert.That(c.IsRunning, Is.True);
            Assert.That(c.Remaining, Is.LessThan(Countdown * 0.6f));
        }

        [Test]
        public void ReachingZeroRemovesThePlatform()
        {
            // §14.3: "The Platform disappears completely. Its collision disappears completely."
            var c = Fresh();
            Hold(ref c, Countdown + Stable + 0.5f, touching: true);

            Assert.That(c.Phase, Is.EqualTo(PlatformPhase.Vanished));
            Assert.That(c.Remaining, Is.EqualTo(0f));
        }

        [Test]
        public void AVanishedPlatformStaysGoneForTheRespawnDelay()
        {
            var c = Fresh();
            Hold(ref c, Countdown + Stable + 0.5f, touching: true);
            Assert.That(c.Phase, Is.EqualTo(PlatformPhase.Vanished));

            Hold(ref c, Respawn * 0.5f, touching: false);

            Assert.That(c.Phase, Is.EqualTo(PlatformPhase.Vanished),
                "it came back early — §14.3 says Smiley must be able to fall through the gap");
        }

        [Test]
        public void ItRespawnsWithAFullCountdown()
        {
            // ⓘ D-011, not §14.3: "does not reset" governs contact loss, not the respawn cycle,
            // and the owner ruled the platform renewable rather than single-use.
            var c = Fresh();
            Hold(ref c, Countdown + Stable + 0.5f, touching: true);
            Hold(ref c, Respawn + 0.2f, touching: false);

            Assert.That(c.Phase, Is.EqualTo(PlatformPhase.Present));
            Assert.That(c.Remaining, Is.EqualTo(Countdown).Within(1e-4f),
                "the respawned platform did not get its full countdown back");
        }

        [Test]
        public void TouchingAVanishedPlatformDoesNothing()
        {
            // It has no collision, so contact should be impossible — but if a stale contact is
            // ever reported, it must not restart a countdown on something that is not there.
            var c = Fresh();
            Hold(ref c, Countdown + Stable + 0.5f, touching: true);

            float respawnAt = c.RespawnRemaining;
            c.Advance(true, Step, Countdown, Respawn, Stable, Recover);

            Assert.That(c.Phase, Is.EqualTo(PlatformPhase.Vanished));
            Assert.That(c.IsRunning, Is.False);
            Assert.That(c.RespawnRemaining, Is.LessThan(respawnAt), "the respawn stalled");
        }

        [Test]
        public void UrgencyRisesTowardDisappearing()
        {
            // Feeds §14.3's near-expiry cue, which must be animation and VFX only — never a
            // numerical countdown and never SFX.
            var c = Fresh();
            Assert.That(c.Urgency(Countdown), Is.EqualTo(0f).Within(1e-4f));

            Hold(ref c, Countdown * 0.5f + Stable, touching: true);
            float mid = c.Urgency(Countdown);

            Assert.That(mid, Is.GreaterThan(0.3f).And.LessThan(0.8f));

            Hold(ref c, Countdown, touching: true);
            Assert.That(c.Urgency(Countdown), Is.EqualTo(1f).Within(1e-4f));
        }
    }
}
