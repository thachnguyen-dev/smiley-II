namespace Smiley.Ads
{
    /// <summary>A parent's answer, as the settings file remembers it.</summary>
    public enum ParentalConsent
    {
        /// <summary>Never asked, or the file predates the question.</summary>
        Unknown = 0,

        /// <summary>A parent passed the gate and consented.</summary>
        Granted = 1,

        /// <summary>A parent declined. Ads stay off; the game is untouched.</summary>
        Refused = 2,
    }

    /// <summary>
    /// What may happen under each state of parental consent (D-107).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ Owner, 2026-09-12, final: <i>"This app is directed to children and requires verifiable
    /// parental consent."</i> The one thing in this game that consent governs is the ad network,
    /// because it is the one thing that sends anything anywhere. So: <b>no consent, no ad SDK</b>
    /// — not even initialised. The game itself is playable in every state; §21's save is local,
    /// there are no accounts, and nothing else leaves the phone.
    /// </para>
    /// <para>
    /// ⛔ The question is asked once, at the first Main Menu, and never again unless a parent
    /// reopens it from Settings. A refusal is an answer, not a retry.
    /// </para>
    /// <para>
    /// ⭐ D-110: the owner re-ruled the app <b>not</b> directed at children under 13. The gate is
    /// then not required and not shown: ads may run without a stored answer, because the SDK is
    /// told there is no consent for personalisation and serves contextual adverts only. The gate
    /// stays in the code and the layout behind `AdSettings.ChildDirected`, so flipping that one
    /// flag brings it back exactly as D-107 built it.
    /// </para>
    /// </remarks>
    public static class ConsentRule
    {
        /// <summary>Whether the ad SDK may be started at all.</summary>
        public static bool AdsMayRun(ParentalConsent consent, bool childDirected) =>
            !childDirected || consent == ParentalConsent.Granted;

        /// <summary>Whether the consent screen must be shown before the menu.</summary>
        public static bool MustAsk(ParentalConsent consent, bool childDirected) =>
            childDirected && consent == ParentalConsent.Unknown;

        /// <summary>A stored value that is not one of the three is Unknown, never Granted.</summary>
        public static ParentalConsent FromStored(int value) =>
            value == (int)ParentalConsent.Granted ? ParentalConsent.Granted
            : value == (int)ParentalConsent.Refused ? ParentalConsent.Refused
            : ParentalConsent.Unknown;
    }
}
