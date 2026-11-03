using UnityEngine;

namespace Smiley.Gameplay
{
    /// <summary>What a Disappearing Platform is doing right now (§14.3).</summary>
    public enum PlatformPhase
    {
        /// <summary>Solid, with time left. The countdown may or may not be running.</summary>
        Present,

        /// <summary>Gone: no collision at all, waiting to respawn.</summary>
        Vanished,
    }

    /// <summary>
    /// §14.3's countdown, as a state machine with no Unity in it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The most intricate rule in the specification, so it lives where every clause can be
    /// tested directly rather than inferred from a platform's behaviour in a scene.
    /// </para>
    /// <para>
    /// §14.3's clauses, each of which is a test:
    /// <list type="bullet">
    /// <item><b>Only stable contact starts it.</b> A brief touch while airborne does not.</item>
    /// <item><b>It continues while Smiley remains in contact</b>, including while he is Ready.</item>
    /// <item><b>Losing contact pauses it immediately.</b></item>
    /// <item><b>Returning within the recovery window resumes from the remaining time.</b></item>
    /// <item>⭐ <b>D-117 (owner, 2026-09-13): after 2 seconds off the platform its durability
    /// recovers in full.</b> §14.3's "does not reset" is now bounded by that window — the
    /// pause is a pause for two seconds, and a reset after.</item>
    /// <item><b>Reaching zero removes the platform and its collision completely.</b></item>
    /// <item><b>After a delay it respawns</b>, with collision active immediately.</item>
    /// </list>
    /// </para>
    /// <para>
    /// ⓘ And one clause that is <b>D-011</b> rather than §14.3: a respawned platform gets its
    /// <b>full</b> countdown back. §14.3's <i>"does not reset"</i> is about contact being lost,
    /// not about the disappear → respawn cycle, and the owner ruled the platform renewable.
    /// </para>
    /// </remarks>
    public struct PlatformCountdown
    {
        /// <summary>Whether the platform is currently solid.</summary>
        public PlatformPhase Phase;

        /// <summary>Seconds of countdown left before it disappears.</summary>
        public float Remaining;

        /// <summary>Seconds left before a vanished platform comes back.</summary>
        public float RespawnRemaining;

        /// <summary>
        /// How long contact has been held without a break, for the stability test.
        /// </summary>
        /// <remarks>
        /// §14.3: <i>"If Smiley only briefly touches the platform while airborne, the countdown
        /// does not activate."</i> Distinguishing a graze from standing on something needs a
        /// notion of duration, and this is it.
        /// </remarks>
        public float ContactHeld;

        /// <summary>
        /// How long Smiley has been off the platform since the countdown last ran, for D-117's
        /// recovery. Zero while touching, and while nothing has been used up.
        /// </summary>
        public float AwayHeld;

        /// <summary>Whether the countdown is actually ticking down this instant.</summary>
        public bool IsRunning { get; private set; }

        /// <summary>Starts a platform in its untouched state.</summary>
        public static PlatformCountdown Fresh(float countdownSeconds)
        {
            return new PlatformCountdown
            {
                Phase = PlatformPhase.Present,
                Remaining = countdownSeconds,
                RespawnRemaining = 0f,
                ContactHeld = 0f,
                AwayHeld = 0f,
            };
        }

        /// <summary>
        /// Advances one step.
        /// </summary>
        /// <param name="touching">Whether Smiley is in contact with this platform.</param>
        /// <param name="deltaTime">Seconds elapsed.</param>
        /// <param name="countdownSeconds">§14.3's countdown duration. ⏸ Q-008.</param>
        /// <param name="respawnSeconds">§14.3's respawn delay. ⏸ Q-009.</param>
        /// <param name="stableContactSeconds">
        /// How long contact must hold before it counts as stable. ⏸ Q-039.
        /// </param>
        /// <param name="recoverSeconds">
        /// ⭐ D-117: how long Smiley must be off the platform before its durability recovers in
        /// full. Owner-authored, 2 s.
        /// </param>
        public void Advance(
            bool touching,
            float deltaTime,
            float countdownSeconds,
            float respawnSeconds,
            float stableContactSeconds,
            float recoverSeconds)
        {
            if (Phase == PlatformPhase.Vanished)
            {
                IsRunning = false;
                ContactHeld = 0f;
                AwayHeld = 0f;

                RespawnRemaining -= deltaTime;
                if (RespawnRemaining <= 0f)
                {
                    // ⓘ D-011: a respawned platform gets the FULL countdown back. §14.3's
                    // "does not reset" governs contact being lost, not the respawn cycle.
                    Phase = PlatformPhase.Present;
                    Remaining = countdownSeconds;
                    RespawnRemaining = 0f;
                }

                return;
            }

            if (!touching)
            {
                // §14.3: "If contact is lost, the countdown pauses IMMEDIATELY." Not decays —
                // stops where it is, and Remaining is left untouched so returning continues
                // from here...
                IsRunning = false;
                ContactHeld = 0f;

                // ⭐ ...for two seconds (D-117). Once Smiley has been away that long the
                // platform is as good as new. Counted only while something has been used up, so
                // a platform he never stood on is not "recovering" from anything.
                if (Remaining < countdownSeconds)
                {
                    AwayHeld += deltaTime;
                    if (AwayHeld >= recoverSeconds)
                    {
                        Remaining = countdownSeconds;
                        AwayHeld = 0f;
                    }
                }

                return;
            }

            AwayHeld = 0f;
            ContactHeld += deltaTime;

            if (ContactHeld < stableContactSeconds)
            {
                // Touching, but not yet stably. A graze in mid-flight never gets this far.
                IsRunning = false;
                return;
            }

            // §14.3: the countdown continues while Smiley remains in contact, "including while
            // Smiley is in the Ready state" — so nothing here consults whether he is moving.
            IsRunning = true;
            Remaining -= deltaTime;

            if (Remaining <= 0f)
            {
                // §14.3: "The Platform disappears completely. Its collision disappears
                // completely. Smiley falls normally. There is no hold behavior."
                Remaining = 0f;
                Phase = PlatformPhase.Vanished;
                RespawnRemaining = respawnSeconds;
                IsRunning = false;
                ContactHeld = 0f;
            }
        }

        /// <summary>
        /// How close to disappearing this platform is, 0 (untouched) to 1 (gone).
        /// </summary>
        /// <remarks>
        /// For §14.3's near-expiry cue, which must be animation and VFX only — ⛔ <b>never a
        /// numerical countdown</b> (§14.3, §29) and ⛔ <b>never SFX</b> (§14.3, §27).
        /// </remarks>
        public float Urgency(float countdownSeconds)
        {
            if (countdownSeconds <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(1f - (Remaining / countdownSeconds));
        }
    }
}
