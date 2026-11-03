using Smiley.Audio;
using UnityEngine;

namespace Smiley.Ads
{
    /// <summary>
    /// The two stored answers advertising depends on: the parent's consent (D-107) and whether
    /// ads have been bought away (D-124).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠ <b>The live settings object wins over the file.</b> While the Menu scene is loaded,
    /// <see cref="GameAudio"/> holds the settings the player is editing, and the file is only
    /// what was last written; reading the file there would miss a change made this second. In
    /// the Game scene there is no <see cref="GameAudio"/>, so the file is the answer.
    /// </para>
    /// <para>
    /// ⓘ Deliberately not cached. Both reads happen when a screen opens or an ad is about to be
    /// shown — never per frame — and a cache here would be one more thing to invalidate when the
    /// player buys, consents, or resets.
    /// </para>
    /// </remarks>
    public static class PurchasedState
    {
        /// <summary>The settings as they are right now.</summary>
        public static GameSettings Settings
        {
            get
            {
                var audio = Object.FindFirstObjectByType<GameAudio>();
                return audio != null ? audio.Settings : SettingsFile.Read();
            }
        }

        /// <summary>Whether Remove Ads has been bought (D-124).</summary>
        public static bool AdsRemoved => RemoveAdsRule.FromStored(Settings.AdsRemoved);

        /// <summary>The parent's stored answer (D-107).</summary>
        public static ParentalConsent Consent => ConsentRule.FromStored(Settings.ParentalConsent);

        /// <summary>
        /// Records the purchase, through the live settings when there are any so the menu sees
        /// it immediately, and always to the file so the next launch does.
        /// </summary>
        public static void RememberThePurchase()
        {
            var audio = Object.FindFirstObjectByType<GameAudio>();

            if (audio != null)
            {
                audio.Settings.AdsRemoved = RemoveAdsRule.Owned;
                audio.SaveSettings();
                return;
            }

            GameSettings settings = SettingsFile.Read();
            settings.AdsRemoved = RemoveAdsRule.Owned;
            SettingsFile.Write(settings);
        }
    }
}
