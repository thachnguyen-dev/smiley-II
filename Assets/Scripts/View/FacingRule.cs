namespace Smiley.View
{
    /// <summary>
    /// Which way the character's picture faces: the way he is moving (D-095).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ Owner, 2026-09-12: <i>"The character simply needs to flip left or right to match the
    /// direction of movement."</i> Moving right: the art as drawn. Moving left: mirrored. Not
    /// moving sideways — resting, or a straight-up hop — keeps whichever way he last faced, so
    /// he does not snap back on landing. ⛔ Visual only.
    /// </para>
    /// <para>
    /// ⓘ A dead band, not a sign test: sideways speed hovers around zero while he settles on a
    /// platform, and a sign test would twitch him left and right. He turns once he is moving
    /// faster than <c>threshold</c>.
    /// </para>
    /// </remarks>
    public static class FacingRule
    {
        /// <summary>Whether the picture should be mirrored (facing left) this frame.</summary>
        /// <param name="horizontalSpeed">Rigidbody horizontal velocity; negative is leftward.</param>
        /// <param name="threshold">Speed below which the current facing is kept.</param>
        /// <param name="currentlyMirrored">Last frame's answer.</param>
        public static bool ShouldMirror(float horizontalSpeed, float threshold, bool currentlyMirrored)
        {
            if (horizontalSpeed < -threshold)
            {
                return true;
            }

            if (horizontalSpeed > threshold)
            {
                return false;
            }

            return currentlyMirrored;
        }
    }
}
