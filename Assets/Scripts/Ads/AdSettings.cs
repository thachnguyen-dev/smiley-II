using UnityEngine;
using UnityEngine.Serialization;

namespace Smiley.Ads
{
    /// <summary>
    /// What the banner needs from the owner's LevelPlay account, and the one audience fact
    /// that decides how the SDK is allowed to behave.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠ <b>Every id here is per-platform except the product</b> (D-127): LevelPlay issues
    /// separate keys for Android and iOS, while a store product id may be the same string in both
    /// Play Console and App Store Connect. ⏸ The iOS keys are <b>Q-057</b> and are blank until the
    /// owner creates the iOS app in LevelPlay — ⛔ never guessed, never derived from the Android
    /// ones.
    /// </para>
    /// <para>
    /// ⭐ Owner, 2026-09-12 (D-110): App Key <c>2817aaeed</c>, Banner Ad Unit ID
    /// <c>oxjqpqxpvkq1r8r9</c>, worldwide, <i>"not directed at children under 13"</i> — the third
    /// audience ruling of the day (general → children → not children), each recorded (D-105,
    /// D-110). The IDs are seeded by the build the first time the asset is written and are the
    /// owner's to edit afterwards (Assets/Config/AdSettings.asset).
    /// </para>
    /// <para>
    /// ⛔ <b>ChildDirected is not a tuning value.</b> It is a legal declaration that must match
    /// what the owner told LevelPlay's dashboard and Google Play's target-audience form; the
    /// game sends it to the SDK before initialisation and it turns personalised advertising off.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Smiley/Ad Settings")]
    public sealed class AdSettings : ScriptableObject
    {
        // ⛔ DO NOT REMOVE the FormerlySerializedAs attributes below. Assets/Config/AdSettings.asset
        // still carries the OLD field names on disk and will until Unity next rewrites it; these
        // attributes are what maps them. Without them the live App Key and both unit ids load as
        // empty, IsConfigured goes false, and the build ships with no ads at all — silently, since
        // a missing key is indistinguishable from a network that never fills. AdsPrivacyRuleTests
        // asserts the exact strings, so that failure is caught, but only if the fixture is run.
        [Tooltip("⭐ Yours. The ANDROID App Key from LevelPlay → Apps.")]
        [FormerlySerializedAs("AppKey")]
        public string AndroidAppKey = "";

        [Tooltip("⭐ Yours. The ANDROID Banner ad unit's ID from LevelPlay → Monetize → Ad Units.")]
        [FormerlySerializedAs("BannerAdUnitId")]
        public string AndroidBannerAdUnitId = "";

        [Tooltip("⭐ Yours. The ANDROID INTERSTITIAL ad unit's ID from LevelPlay → Monetize → Ad Units " +
                 "(D-122). Empty = no interstitials; the game starts as before.")]
        [FormerlySerializedAs("InterstitialAdUnitId")]
        public string AndroidInterstitialAdUnitId = "";

        [Tooltip("⏸ Q-057. The iOS App Key — a DIFFERENT key from the Android one, issued when you " +
                 "create the iOS app in LevelPlay → Apps. Empty until then, which simply means no ads " +
                 "on iOS.")]
        public string IosAppKey = "";

        [Tooltip("⏸ Q-057. The iOS Banner ad unit's ID. Also different from Android's.")]
        public string IosBannerAdUnitId = "";

        [Tooltip("⏸ Q-057. The iOS INTERSTITIAL ad unit's ID. Also different from Android's.")]
        public string IosInterstitialAdUnitId = "";

        [Tooltip("⭐ Owner, 2026-09-17 (D-122): at most one interstitial per this many seconds. 180 = " +
                 "three minutes.")]
        public float InterstitialMinIntervalSeconds = 180f;

        [Tooltip("⭐ Yours (D-124). The Remove Ads product's ID, exactly as created in Play Console → " +
                 "Monetise → In-app products, and in App Store Connect for iOS (D-127) — the same " +
                 "string may be used in both. Empty = no purchase offered and no button shown.")]
        public string RemoveAdsProductId = "remove_ads";

        [Tooltip("⭐ Yours, and a legal declaration (D-105, D-110). True: COPPA flag sent to the SDK " +
                 "and a parental-consent gate before the first menu. False: no gate; ads are still " +
                 "non-personalised. Must match the LevelPlay dashboard and the Play Console " +
                 "target-audience form.")]
        public bool ChildDirected = false; // D-110: the asset is the source; this default only agrees with it

        [Tooltip("Whether the banner is shown at all. Off is a valid shipping state (D-004: menus " +
                 "only, and never a dependency of gameplay).")]
        public bool Enabled = true;

        /// <summary>The App Key for the platform this build is for.</summary>
        /// <remarks>
        /// ⚠ <b>LevelPlay issues a different App Key and different ad unit ids per platform</b>
        /// (D-127). One set of fields would have meant an iOS build asking the network with
        /// Android's credentials, which does not fail loudly — it simply never fills.
        /// ⓘ Chosen at COMPILE time, not from <c>Application.platform</c>: the right one is the
        /// only one in the build, and in the Editor it follows the active build target, which is
        /// what someone switching platforms expects to see.
        /// ⭐ <b>D-138: the web build has none at all.</b> Every id is empty on WebGL, so
        /// <see cref="IsConfigured"/> is false and no SDK starts — without this, the
        /// <c>#else</c> would hand a browser Android's credentials (D-134 recorded exactly that).
        /// </remarks>
        public string AppKey =>
#if UNITY_IOS
            IosAppKey;
#elif UNITY_WEBGL
            string.Empty; // ⭐ D-138: no advertising on the web.
#else
            AndroidAppKey;
#endif

        /// <summary>The banner unit for the platform this build is for.</summary>
        public string BannerAdUnitId =>
#if UNITY_IOS
            IosBannerAdUnitId;
#elif UNITY_WEBGL
            string.Empty; // ⭐ D-138: no advertising on the web.
#else
            AndroidBannerAdUnitId;
#endif

        /// <summary>The interstitial unit for the platform this build is for.</summary>
        public string InterstitialAdUnitId =>
#if UNITY_IOS
            IosInterstitialAdUnitId;
#elif UNITY_WEBGL
            string.Empty; // ⭐ D-138: no advertising on the web.
#else
            AndroidInterstitialAdUnitId;
#endif

        /// <summary>Whether there is enough here to ask the network for anything.</summary>
        /// <remarks>
        /// ⓘ On a platform whose keys are still blank this is simply false, so no SDK starts and
        /// the game runs without ads — the same normal code path a failed fill already takes
        /// (D-003). ⛔ An unconfigured platform is never an error the player sees.
        /// </remarks>
        public bool IsConfigured =>
            Enabled && !string.IsNullOrWhiteSpace(AppKey) && !string.IsNullOrWhiteSpace(BannerAdUnitId);

        /// <summary>Whether an interstitial unit is named at all (D-122).</summary>
        public bool HasInterstitial => IsConfigured && !string.IsNullOrWhiteSpace(InterstitialAdUnitId);

        /// <summary>Whether there is a Remove Ads product to sell (D-124).</summary>
        /// <remarks>
        /// ⓘ Deliberately NOT gated on <see cref="IsConfigured"/>: the purchase is worth offering
        /// even if the ad units were switched off, and a player who bought it must keep it.
        /// </remarks>
        public bool HasRemoveAds => !string.IsNullOrWhiteSpace(RemoveAdsProductId);
    }
}
