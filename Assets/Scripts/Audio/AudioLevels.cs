using UnityEngine;

namespace Smiley.Audio
{
    /// <summary>
    /// §27's mixing rules: how loud a cue is, given what caused it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// §27 states two of these outright — <i>"SFX volume depends on impact force: light impact →
    /// lower volume, strong impact → higher volume"</i>, and the trajectory guide's
    /// <i>"sound changes according to drag force"</i>. ⏸ The curves themselves are Q-048 and mine.
    /// </para>
    /// <para>
    /// ⓘ No Unity in here beyond <see cref="Mathf"/>, so the rules are testable directly instead
    /// of being inferred from something audible — which is the only way to test audio at all in a
    /// headless run. Same split as <see cref="Gameplay.PlatformCountdown"/>.
    /// </para>
    /// </remarks>
    public static class AudioLevels
    {
        /// <summary>
        /// How loud a collision should be, 0 to 1.
        /// </summary>
        /// <remarks>
        /// <para>
        /// ⛔ <b>RESTING WEIGHT IS NOT AN IMPACT, and this is the same subtraction §11's
        /// deformation makes.</b> A Smiley sitting on a platform delivers a contact impulse every
        /// single step — his own weight, held up by the surface. Without taking that off the
        /// bottom, a motionless Smiley would rattle continuously, and §27's <i>"no rolling
        /// SFX"</i> would be violated by a Smiley doing nothing at all.
        /// </para>
        /// <para>
        /// ⚠ The response is a square root rather than a straight line: loudness is perceived
        /// roughly logarithmically, so a linear map makes every impact above a gentle one sound
        /// the same. §27 asks for light and strong to be distinguishable, which is about hearing
        /// rather than about arithmetic.
        /// </para>
        /// </remarks>
        /// <param name="impulse">The contact impulse the solver actually applied.</param>
        /// <param name="restingImpulse">What Smiley's own weight delivers in one step.</param>
        /// <param name="loudestImpulse">The impulse that reaches full volume. ⏸ Q-048.</param>
        public static float ImpactVolume(float impulse, float restingImpulse, float loudestImpulse)
        {
            float excess = impulse - restingImpulse;

            if (excess <= 0f || loudestImpulse <= 0f)
            {
                return 0f;
            }

            return Mathf.Sqrt(Mathf.Clamp01(excess / loudestImpulse));
        }

        /// <summary>
        /// The guide's tone for a drag of this strength.
        /// </summary>
        /// <remarks>
        /// §27: the trajectory guide <i>"has sound feedback. Sound changes according to drag
        /// force."</i> ⓘ Pitch rather than volume carries the change, because §6's whole control
        /// is about how hard the shot is and a rising note reads as winding up. The volume is
        /// held steady so that a gentle aim is not inaudible.
        /// </remarks>
        /// <param name="charge">How far through §6's Maximum Drag Distance, 0 to 1.</param>
        /// <param name="lowest">Pitch multiplier at no drag. ⏸ Q-048.</param>
        /// <param name="highest">Pitch multiplier at a full drag. ⏸ Q-048.</param>
        public static float GuidePitch(float charge, float lowest, float highest)
        {
            return Mathf.Lerp(lowest, highest, Mathf.Clamp01(charge));
        }

        /// <summary>
        /// Applies a §26 volume setting to a cue's own level.
        /// </summary>
        /// <remarks>
        /// ⛔ <b>A setting of zero must be SILENT, exactly.</b> Multiplying is what guarantees
        /// that: no cue, however loud in itself, can survive a slider at the bottom. A curve that
        /// merely got quiet would leave a player who turned the sound off still hearing the game.
        /// </remarks>
        public static float Mix(float cueLevel, float setting)
        {
            return Mathf.Clamp01(cueLevel) * Mathf.Clamp01(setting);
        }
    }
}
