using NUnit.Framework;
using Smiley.Ads;
using Smiley.Audio;

namespace Smiley.Tests
{
    /// <summary>D-124's purchase: what it suppresses, and what it does to the menu.</summary>
    public sealed class RemoveAdsRuleTests
    {
        [Test]
        public void BeforeThePurchaseAdsRunAndTheButtonIsOffered()
        {
            Assert.That(RemoveAdsRule.AdsMayRun(purchased: false), Is.True);
            Assert.That(RemoveAdsRule.OfferTheButton(purchased: false), Is.True);
        }

        [Test]
        public void AfterItNoAdvertisingRunsAndNothingIsLeftToSell()
        {
            // ⭐ The owner chose "All ads": banner and every interstitial.
            Assert.That(RemoveAdsRule.AdsMayRun(purchased: true), Is.False);
            Assert.That(RemoveAdsRule.OfferTheButton(purchased: true), Is.False);
        }

        [TestCase(0, false)]
        [TestCase(1, true)]
        [TestCase(2, false)]
        [TestCase(-1, false)]
        [TestCase(99, false)]
        public void OnlyTheOneStoredValueMeansBought(int stored, bool expected)
        {
            // A settings file from before the purchase existed, or a corrupted one, is "not
            // bought" — never accidentally "bought", which would silently cost the sale.
            Assert.That(RemoveAdsRule.FromStored(stored), Is.EqualTo(expected));
        }

        [Test]
        public void ASettingsFileFromBeforeThePurchaseReadsAsNotBought()
        {
            GameSettings settings = SettingsFile.Parse("{\"Version\":1,\"MusicVolume\":0.5,\"SfxVolume\":0.5}");
            Assert.That(RemoveAdsRule.FromStored(settings.AdsRemoved), Is.False);
        }

        [Test]
        public void ThePurchaseSurvivesASettingsRoundTrip()
        {
            var settings = new GameSettings { AdsRemoved = RemoveAdsRule.Owned };
            GameSettings back = SettingsFile.Parse(SettingsFile.Serialise(settings));

            Assert.That(RemoveAdsRule.FromStored(back.AdsRemoved), Is.True,
                "the purchase did not survive being written and read — a relaunch would sell it again");
        }

        [Test]
        public void TheOwnersProductIdIsOnDisk()
        {
            var settings = UnityEditor.AssetDatabase.LoadAssetAtPath<AdSettings>("Assets/Config/AdSettings.asset");
            Assert.That(settings, Is.Not.Null, "no Assets/Config/AdSettings.asset");
            Assert.That(settings.RemoveAdsProductId, Is.EqualTo("remove_ads"),
                "the product ID must match the one created in Play Console (D-124)");
            Assert.That(settings.HasRemoveAds, Is.True);
        }

        [Test]
        public void TheButtonIsHiddenWhenTheStoreHasNothingToSell()
        {
            // ⛔ THE 2.1.0 REJECTION, 2026-10-14. A configured product id proves only that
            // someone typed a string. The button was shown on that alone, a reviewer pressed it,
            // and nothing happened -- which Apple calls App Completeness, correctly.
            Assert.That(RemoveAdsRule.ShowTheButton(productIsNamed: true, storeHasIt: false, purchased: false),
                Is.False, "a button that cannot sell anything must not be shown");
        }

        [Test]
        public void TheButtonShowsOnlyWhenAllThreeHold()
        {
            Assert.That(RemoveAdsRule.ShowTheButton(true, true, false), Is.True, "named, stocked, unbought");
            Assert.That(RemoveAdsRule.ShowTheButton(false, true, false), Is.False, "no product id");
            Assert.That(RemoveAdsRule.ShowTheButton(true, true, true), Is.False, "already bought");
            Assert.That(RemoveAdsRule.ShowTheButton(false, false, false), Is.False, "nothing at all");
        }

        [Test]
        public void OnAppleTheButtonDoesNotWaitForTheStore()
        {
            // ⛔ THE 5.6 REJECTION, 2026-10-24. Hidden until StoreKit answered, the button never
            // appeared on the reviewer's device while the notes and recording showed it — "a
            // feature intentionally hidden during review".
            Assert.That(RemoveAdsRule.ShowTheButton(productIsNamed: true, storeHasIt: false, purchased: false, applePlatform: true),
                Is.True, "on iOS a named, unbought product must be visible whether or not the store has answered");
        }

        [Test]
        public void OnAppleTheButtonStillGoesOnceBoughtOrWhenNothingIsNamed()
        {
            Assert.That(RemoveAdsRule.ShowTheButton(true, true, true, applePlatform: true), Is.False, "already bought");
            Assert.That(RemoveAdsRule.ShowTheButton(true, false, true, applePlatform: true), Is.False, "already bought, store silent");
            Assert.That(RemoveAdsRule.ShowTheButton(false, false, false, applePlatform: true), Is.False, "no product id");
        }

        [Test]
        public void OffAppleTheFourArgumentRuleIsTheThreeArgumentOne()
        {
            // ⓘ Android on Play must not change because iOS did.
            foreach (bool named in new[] { false, true })
            foreach (bool stocked in new[] { false, true })
            foreach (bool bought in new[] { false, true })
            {
                Assert.That(RemoveAdsRule.ShowTheButton(named, stocked, bought, applePlatform: false),
                    Is.EqualTo(RemoveAdsRule.ShowTheButton(named, stocked, bought)),
                    $"named={named} stocked={stocked} bought={bought}");
            }
        }

        [Test]
        public void OnAppleTheRestoreButtonIsAlwaysShown()
        {
            // ⭐ Apple 3.1.1. The player who needs Restore is the one who HAS bought and is on a
            // new phone or a fresh install, so purchase state must not gate it. The rule takes
            // no purchase parameter at all, which is the point being pinned here.
            Assert.That(RemoveAdsRule.ShowRestoreButton(applePlatform: true), Is.True,
                "Apple asks for a restore mechanism and there would be none");
        }

        [Test]
        public void OnAndroidTheRestoreButtonIsNotShown()
        {
            // ⓘ Google Play re-queries entitlements itself, so restoration is silent and a
            // button would do nothing a player could observe.
            Assert.That(RemoveAdsRule.ShowRestoreButton(applePlatform: false), Is.False,
                "Android does not need a Restore button");
        }
    }
}
