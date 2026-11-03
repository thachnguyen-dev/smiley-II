using NUnit.Framework;
using Smiley.Ads;
using UnityEditor;

namespace Smiley.Tests
{
    /// <summary>The flags the game sends the ad network, and the owner's asset (D-105).</summary>
    public sealed class AdsPrivacyRuleTests
    {
        [Test]
        public void AChildDirectedAppSendsCoppaAndNeverPersonalises()
        {
            // ⭐ Owner, 2026-09-12: "This app is directed to children."
            AdsPrivacyRule rule = AdsPrivacyRule.For(childDirected: true);

            Assert.That(rule.Coppa, Is.True);
            Assert.That(rule.GdprConsent, Is.False, "a child cannot consent");
            Assert.That(rule.CcpaDoNotSell, Is.True);
            Assert.That(rule.AllowsPersonalisedAds, Is.False);
        }

        [Test]
        public void AGeneralAudienceAppStillDoesNotClaimConsentItNeverAsked()
        {
            AdsPrivacyRule rule = AdsPrivacyRule.For(childDirected: false);

            Assert.That(rule.Coppa, Is.False);
            Assert.That(rule.GdprConsent, Is.False, "consent needs a certified prompt this game does not have");
            Assert.That(rule.AllowsPersonalisedAds, Is.False);
        }

        [Test]
        public void TheOwnersAdSettingsAreOnDiskAndNotChildDirected()
        {
            var settings = AssetDatabase.LoadAssetAtPath<AdSettings>("Assets/Config/AdSettings.asset");

            Assert.That(settings, Is.Not.Null, "no Assets/Config/AdSettings.asset — run tools/build-scene.sh once");
            // ⚠ The ANDROID fields by name, not the platform-resolving properties: these are the
            // credentials the live app on Play is running on, and a rename that silently dropped
            // them would leave IsConfigured false and ship a build with no ads at all (D-127).
            Assert.That(settings.AndroidAppKey, Is.EqualTo("2817aaeed"), "the owner's Android App Key (D-110)");
            Assert.That(settings.AndroidBannerAdUnitId, Is.EqualTo("oxjqpqxpvkq1r8r9"), "the owner's Android banner ad unit (D-110)");
            Assert.That(settings.AndroidInterstitialAdUnitId, Is.EqualTo("udscfsi88kwwxv5h"), "the owner's Android interstitial ad unit (D-122)");

            // ⭐ Q-057 CLOSED 2026-10-10: the owner created the iOS app in LevelPlay and supplied
            // all three. ⓘ The previous assertion here was that they were EMPTY; it was inverted
            // deliberately, which is what §34 asks for — a value arrives from the owner, and the
            // fixture changes on purpose rather than quietly accommodating it.
            // ⛔ They are NOT the Android ones. LevelPlay issues a separate set per platform, and
            // a cross-wired key does not fail loudly — it simply never fills.
            Assert.That(settings.IosAppKey, Is.EqualTo("28a03bf45"), "the owner's iOS App Key (Q-057)");
            Assert.That(settings.IosBannerAdUnitId, Is.EqualTo("bdwcmjdsdo19skt3"), "the owner's iOS banner unit (Q-057)");
            Assert.That(settings.IosInterstitialAdUnitId, Is.EqualTo("4lodjqmnqczfhitq"), "the owner's iOS interstitial unit (Q-057)");
            Assert.That(settings.IosAppKey, Is.Not.EqualTo(settings.AndroidAppKey), "iOS and Android keys must differ");
            Assert.That(settings.InterstitialMinIntervalSeconds, Is.EqualTo(180f), "the owner's 3-minute cap (D-122)");
            Assert.That(settings.ChildDirected, Is.False, "the owner ruled the app not directed at children under 13 (D-110)");
            Assert.That(settings.HasInterstitial, Is.True);
            Assert.That(settings.IsConfigured, Is.True);
        }
    }
}
