namespace Smiley.Ads
{
    /// <summary>
    /// What the game tells the ad SDK about the player before it asks for an ad (D-105).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three flags, derived from one owner fact — whether the app is directed to children —
    /// and one policy the owner did not have to decide:
    /// </para>
    /// <list type="bullet">
    /// <item><b>COPPA</b> = child-directed. Tells the network the audience is children, which
    /// turns personalised advertising off at the source.</item>
    /// <item><b>GDPR consent</b> = never granted by this game. A child cannot give it, and for
    /// a general audience it would need a certified consent prompt first; either way the honest
    /// default is "no", which yields non-personalised ads rather than none.</item>
    /// <item><b>CCPA do-not-sell</b> = always on. Costs nothing to the player, and it is the safe
    /// answer for California without a prompt.</item>
    /// </list>
    /// <para>
    /// ⓘ Kept as arithmetic so a test can pin the child-directed answer without an SDK in the
    /// room, and so the flags can never be set in the wrong order or half-set.
    /// </para>
    /// </remarks>
    public readonly struct AdsPrivacyRule
    {
        public readonly bool Coppa;
        public readonly bool GdprConsent;
        public readonly bool CcpaDoNotSell;

        private AdsPrivacyRule(bool coppa, bool gdprConsent, bool ccpaDoNotSell)
        {
            Coppa = coppa;
            GdprConsent = gdprConsent;
            CcpaDoNotSell = ccpaDoNotSell;
        }

        /// <summary>The flags for an app that is, or is not, directed to children.</summary>
        public static AdsPrivacyRule For(bool childDirected) =>
            new AdsPrivacyRule(coppa: childDirected, gdprConsent: false, ccpaDoNotSell: true);

        /// <summary>Whether the network may ever serve a personalised ad under these flags.</summary>
        public bool AllowsPersonalisedAds => !Coppa && GdprConsent;
    }
}
