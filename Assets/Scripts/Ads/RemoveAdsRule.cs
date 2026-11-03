namespace Smiley.Ads
{
    /// <summary>
    /// What the Remove Ads purchase does, with no Unity and no store in it (D-124).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ Owner, 2026-10-01: a <b>Remove Ads</b> purchase at <b>$1.99</b> that removes
    /// <b>all</b> advertising — the Main Menu banner and every interstitial. ⚠ An amendment to
    /// <b>D-003</b>, which made banner advertising the sole monetisation and listed a shop among
    /// the forbidden patterns; the owner amended it knowingly, as they did for D-122.
    /// </para>
    /// <para>
    /// ⛔ <b>The purchase is a non-consumable and the store is the truth.</b> The settings file
    /// remembers it so the game knows offline and before the store connects; the store's own
    /// record restores it on a reinstall or a second device. ⚠ Defending against someone editing
    /// that file is deliberately not attempted — it is their own device, and the game has no
    /// server.
    /// </para>
    /// </remarks>
    public static class RemoveAdsRule
    {
        /// <summary>The stored value that means "bought". Anything else means not bought.</summary>
        public const int Owned = 1;

        /// <summary>Reads the settings file's number without trusting it to be in range.</summary>
        public static bool FromStored(int value) => value == Owned;

        /// <summary>Whether any advertising may run at all.</summary>
        /// <remarks>
        /// ⛔ <b>All of it, not some.</b> The owner chose "All ads" over the cheaper readings,
        /// because a purchase named Remove Ads that leaves ads running is a refund request.
        /// </remarks>
        public static bool AdsMayRun(bool purchased) => !purchased;

        /// <summary>Whether the Main Menu still shows the button.</summary>
        /// <remarks>
        /// ⓘ A bought product has nothing left to sell, so the row disappears rather than going
        /// grey — §22's menu is a short list and a dead row in it is clutter.
        /// </remarks>
        public static bool OfferTheButton(bool purchased) => !purchased;

        /// <summary>
        /// Whether the Main Menu may show the button at all.
        /// </summary>
        /// <param name="productIsNamed">A product id is set in the settings asset.</param>
        /// <param name="storeHasIt">The store returned that product when asked.</param>
        /// <param name="purchased">It has already been bought.</param>
        /// <remarks>
        /// ⛔ <b>All three, and the middle one is the one that was missing.</b> Apple rejected the
        /// app under 2.1.0 App Completeness because the button appeared on the strength of a
        /// configured id while the store had nothing to sell under it (2026-10-14).
        /// </remarks>
        public static bool ShowTheButton(bool productIsNamed, bool storeHasIt, bool purchased) =>
            productIsNamed && storeHasIt && OfferTheButton(purchased);

        /// <summary>
        /// Whether the Main Menu may show the button, on a platform where hiding it is itself a fault.
        /// </summary>
        /// <param name="productIsNamed">A product id is set in the settings asset.</param>
        /// <param name="storeHasIt">The store returned that product when asked.</param>
        /// <param name="purchased">It has already been bought.</param>
        /// <param name="applePlatform">Running on an iPhone or iPad.</param>
        /// <remarks>
        /// <para>
        /// ⛔ <b>Apple rejected the app under 5.6 Developer Code of Conduct for the 2.1.0 fix
        /// (2026-10-24):</b> "features that appear to have been intentionally hidden during the
        /// review process". The review notes and the screen recording both showed a Remove Ads
        /// button; on the reviewer's device the store had not returned the product yet, so the
        /// button never appeared. A paid feature visible on the developer's phone and absent on the
        /// reviewer's is exactly what 5.6 looks for.
        /// </para>
        /// <para>
        /// ⭐ <b>So on Apple the button does not wait for the store.</b> It is shown whenever a
        /// product is named and not yet bought; pressing it with no product says so plainly (the
        /// Main Menu's "store unavailable" label) instead of doing nothing, which is what 2.1.0
        /// objected to. ⓘ Android keeps the three-argument rule unchanged.
        /// </para>
        /// </remarks>
        public static bool ShowTheButton(bool productIsNamed, bool storeHasIt, bool purchased, bool applePlatform) =>
            applePlatform
                ? productIsNamed && OfferTheButton(purchased)
                : ShowTheButton(productIsNamed, storeHasIt, purchased);

        /// <summary>
        /// Whether the Restore Purchases button belongs on screen.
        /// </summary>
        /// <param name="applePlatform">Running on an Apple platform.</param>
        /// <remarks>
        /// <para>
        /// ⭐ <b>On Apple it is always shown — the purchase state is deliberately not a parameter.</b>
        /// Apple's guideline 3.1.1 asks for a restore mechanism for a non-consumable, and the
        /// player who needs it is precisely the one who <i>has</i> bought: new phone, or a
        /// reinstall, where this device knows nothing of the purchase yet. A button that
        /// disappears once bought is gone at the only moment it was for.
        /// </para>
        /// <para>
        /// ⓘ Not shown on Android, where Google Play re-queries entitlements itself and asks for
        /// no button. Smiley restores silently there, and an extra button would do nothing a
        /// player could observe.
        /// </para>
        /// </remarks>
        public static bool ShowRestoreButton(bool applePlatform) => applePlatform;
    }
}
