namespace Smiley.Ads
{
    /// <summary>
    /// When a full-screen interstitial may be shown (D-122), with no Unity in it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ Owner, 2026-09-17: an interstitial <b>when Continue / New Game is pressed</b>, before the
    /// climb loads — never during play (D-004 stands) — and <b>at most one every three
    /// minutes</b>. The moment is the Main Menu's; this rule only answers the cap.
    /// </para>
    /// <para>
    /// ⓘ "Never shown" is represented by a negative last-shown time, so the first press of a
    /// session is always eligible. The clock is whatever the caller passes — the tests hand it
    /// numbers, the game hands it <c>Time.realtimeSinceStartup</c>, which keeps counting while
    /// the ad itself covers the screen and across scene loads.
    /// </para>
    /// </remarks>
    public static class InterstitialRule
    {
        /// <summary>The value of "an interstitial has never been shown this session".</summary>
        public const double Never = -1;

        /// <summary>Whether the cap allows one now.</summary>
        /// <param name="lastShownSeconds">When the last one was shown, or <see cref="Never"/>.</param>
        /// <param name="nowSeconds">The current time on the same clock.</param>
        /// <param name="minIntervalSeconds">⭐ Owner-chosen: 180.</param>
        public static bool MayShow(double lastShownSeconds, double nowSeconds, double minIntervalSeconds)
        {
            if (lastShownSeconds < 0)
            {
                return true;
            }

            return nowSeconds - lastShownSeconds >= minIntervalSeconds;
        }
    }
}
