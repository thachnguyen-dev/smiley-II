using UnityEngine;

namespace Smiley.Gameplay
{
    /// <summary>
    /// §13's return to upright, as arithmetic.
    /// </summary>
    /// <remarks>
    /// <para>
    /// §13: <i>"After rolling stops, body and face gradually return toward their correct /
    /// original orientation. They must not snap immediately."</i>
    /// </para>
    /// <para>
    /// ⛔ <b>"Must not snap" is a negative requirement, which is why this is a function rather
    /// than an assignment.</b> Setting the angle to zero when he stops would satisfy every
    /// other clause of §13 and violate this one, and it would look like a bug rather than like
    /// a rule being broken.
    /// </para>
    /// </remarks>
    public static class UprightSettle
    {
        /// <summary>
        /// One step of the return toward upright.
        /// </summary>
        /// <remarks>
        /// ⚠ <b>Takes the SHORT way round.</b> An angle of 350° is ten degrees from upright, not
        /// three hundred and fifty — easing the raw number toward zero would spin Smiley almost
        /// the whole way round to travel a tenth of a turn.
        /// <para>
        /// Exponential, so the return has no fixed duration and cannot overshoot into a lean
        /// the other way. ⓘ Frame-rate independent for the same reason the camera's is (§33).
        /// </para>
        /// </remarks>
        /// <param name="currentDegrees">The body's rotation now.</param>
        /// <param name="settleSeconds">Roughly how long the return takes. ⏸ Q-040.</param>
        /// <param name="deltaTime">Seconds elapsed.</param>
        public static float Step(float currentDegrees, float settleSeconds, float deltaTime)
        {
            // Signed shortest distance from upright, so 350 reads as -10.
            float offset = Mathf.DeltaAngle(0f, currentDegrees);

            if (settleSeconds <= 0f)
            {
                return 0f;
            }

            float t = 1f - Mathf.Exp(-deltaTime / settleSeconds);
            return currentDegrees - offset * t;
        }

        /// <summary>How far from upright an orientation is, in degrees, ignoring direction.</summary>
        public static float OffsetFromUpright(float degrees) => Mathf.Abs(Mathf.DeltaAngle(0f, degrees));
    }
}
