namespace Smiley.Gameplay
{
    /// <summary>
    /// §8's Ready predicate: when the player is allowed to begin a drag.
    /// </summary>
    /// <remarks>
    /// <para>
    /// §8 gives exactly two conditions — Smiley is <b>in contact with a Platform</b>, and
    /// total velocity is <b>below the Stopping Threshold</b> — and then adds a third rule
    /// that is easy to lose: <i>"There is no additional stabilization delay. Ready appears
    /// immediately once the conditions are satisfied."</i>
    /// </para>
    /// <para>
    /// ⛔ <b>That negative requirement is why this is a pure function of the present
    /// moment.</b> It holds no timer, no counter of consecutive settled frames, and no
    /// hysteresis. A grace period would be invisible in play and would violate §8 exactly.
    /// </para>
    /// <para>
    /// ⚠ <b>Ready is not a lock (§8).</b> Nothing here freezes or anchors Smiley — if he is
    /// near an edge and physics slides him off, he leaves the platform and simply stops
    /// being Ready. Anything that pinned him in place to "hold" the state would be a
    /// different mechanic.
    /// </para>
    /// </remarks>
    public static class ReadyRule
    {
        /// <summary>
        /// Whether a drag may begin this instant.
        /// </summary>
        /// <param name="touchingPlatform">Smiley is in contact with a Platform.</param>
        /// <param name="speed">Total speed, not a single axis — §8 says "total velocity".</param>
        /// <param name="stoppingThreshold">§8's Stopping Threshold. ⏸ Q-005, undecided.</param>
        public static bool IsReady(bool touchingPlatform, float speed, float stoppingThreshold)
        {
            return touchingPlatform && speed <= stoppingThreshold;
        }
    }
}
