using System.Collections;
using NUnit.Framework;
using Smiley.Ads;
using Smiley.Audio;
using Smiley.Flow;
using Smiley.Save;
using Smiley.View;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Smiley.Tests
{
    /// <summary>
    /// D-124's purchase and D-125's split, as the running game has them.
    /// </summary>
    /// <remarks>
    /// ⛔ Neither store is touched in the Editor — the ad SDK is skipped and Unity's editor
    /// billing is a dialog a batch run cannot answer. So these prove the paths that matter most:
    /// <b>nothing waits on a store</b>, the purchase silences every ad, and D-004's rule that no
    /// banner exists in the Game scene survives the split.
    /// </remarks>
    public sealed class RemoveAdsSceneTests
    {
        private static void WriteAdsRemoved(bool removed)
        {
            GameSettings settings = SettingsFile.Read();
            settings.AdsRemoved = removed ? RemoveAdsRule.Owned : 0;
            SettingsFile.Write(settings);
        }

        [UnitySetUp]
        public IEnumerator Load()
        {
            SaveFile.DeleteAll();
            GameSession.Reset();
            WriteAdsRemoved(false);
            AdService.ResetForTests();
            yield return SceneManager.LoadSceneAsync(Scenes.Menu, LoadSceneMode.Single);
            yield return null;

            // ⓘ Play Mode has no store, so nothing would ever be for sale and every fixture
            // below would be asking about a button that cannot exist. This grants the premise
            // they were all written under: a store that stocks the product.
            StorePurchases.Instance.SetProductAvailableForTests(true);
            yield return null;
        }

        [TearDown]
        public void Clean()
        {
            StorePurchases.ApplePlatformForTests = null;
            WriteAdsRemoved(false);
            SaveFile.DeleteAll();
            GameSession.Reset();
            AdService.ResetForTests();
        }

        [UnityTest]
        public IEnumerator TheButtonIsOfferedWhateverOrderTheSceneWokeUpIn()
        {
            // ⛔ The regression this exists for: MainMenuScreen read StorePurchases.Instance from
            // Bind(), which runs during Awake, and Unity does not define Awake order between two
            // scene objects. On a build where the Store happened to wake second, the button was
            // hidden AND the subscription that could have un-hidden it was skipped. Every test
            // passed and the phone showed three buttons where there should have been four.
            yield return null;

            var menu = Object.FindFirstObjectByType<MainMenuScreen>();
            Assert.That(menu, Is.Not.Null, "the Menu scene has no MainMenuScreen");

            Assert.That(menu.RemoveAdsOffered, Is.True,
                "nothing has been bought, a product id is configured, and the button is still " +
                "not being offered - the menu read the store before the store existed");
        }

        [UnityTest]
        public IEnumerator TheMenuSceneHasTheServicesAndTheySurviveIt()
        {
            Assert.That(AdService.Instance, Is.Not.Null, "the Menu scene created no AdService");
            Assert.That(StorePurchases.Instance, Is.Not.Null, "the Menu scene created no StorePurchases");

            // D-125: they outlive the scene, because the gear menu is in the other one.
            Assert.That(AdService.Instance.gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"),
                "AdService does not survive a scene load, so the Game scene could not show an ad");
            yield return null;
        }

        [UnityTest]
        public IEnumerator WithNothingBoughtTheMenuOffersTheButton()
        {
            var menu = Object.FindFirstObjectByType<MainMenuScreen>();
            Assert.That(StorePurchases.Instance.ShouldOffer, Is.True, "nothing is for sale");
            Assert.That(menu.RemoveAdsOffered, Is.True, "the Remove Ads button is not on the menu");
            yield return null;
        }

        [UnityTest]
        public IEnumerator OnceBoughtTheButtonGoesAndNoAdvertisingMayRun()
        {
            WriteAdsRemoved(true);

            // The menu is rebuilt the way a relaunch would build it.
            yield return SceneManager.LoadSceneAsync(Scenes.Menu, LoadSceneMode.Single);
            yield return null;

            var menu = Object.FindFirstObjectByType<MainMenuScreen>();
            var banner = Object.FindFirstObjectByType<MenuBanner>();

            Assert.That(PurchasedState.AdsRemoved, Is.True, "the purchase was not read back");
            Assert.That(StorePurchases.Instance.ShouldOffer, Is.False, "a bought product is still for sale");
            Assert.That(menu.RemoveAdsOffered, Is.False, "the Remove Ads button is still on the menu");

            Assert.That(AdService.Instance.AdsAllowed, Is.False, "advertising may still run after Remove Ads");
            Assert.That(banner.Status, Is.EqualTo("removed: bought"),
                $"the banner says '{banner.Status}' rather than knowing it was bought away");
        }

        [UnityTest]
        public IEnumerator BuyingWithNoStoreChangesNothingAndBreaksNothing()
        {
            // ⛔ D-003's connection rule: the store is unreachable here, and pressing the button
            // must simply do nothing — no exception, no stuck menu, no accidental grant.
            var menu = Object.FindFirstObjectByType<MainMenuScreen>();
            StorePurchases.Instance.Buy();
            yield return null;

            Assert.That(PurchasedState.AdsRemoved, Is.False, "a purchase was granted with no store");
            Assert.That(menu.RemoveAdsOffered, Is.True, "the button vanished without a purchase");
            Assert.That(menu.VisibleScreen, Is.EqualTo("menu"), "the menu moved somewhere");
        }

        [UnityTest]
        public IEnumerator AnInterstitialNeverStandsBetweenThePlayerAndTheGame()
        {
            bool continued = false;
            bool showing = AdService.Instance.ShowInterstitialThen(() => continued = true);

            Assert.That(showing, Is.False, "claimed to show an ad with no SDK running");
            Assert.That(continued, Is.True, "the continuation never ran — the climb would never start");
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheGameSceneStillHasNoBannerButCanReachAnInterstitial()
        {
            yield return SceneManager.LoadSceneAsync(Scenes.Game, LoadSceneMode.Single);
            yield return null;

            // ⛔ D-004, unchanged by the split: no banner component in the Game scene at all.
            Assert.That(Object.FindFirstObjectByType<MenuBanner>(), Is.Null,
                "a banner component is in the Game scene");

            // ⭐ D-124: but the gear menu can reach an interstitial, because AdService persisted.
            Assert.That(AdService.Instance, Is.Not.Null,
                "the Game scene cannot reach the ad service, so the gear menu could never show one");
        }

        [UnityTest]
        public IEnumerator OpeningTheGearMenuAsksForAnAdAndOpensRegardless()
        {
            yield return SceneManager.LoadSceneAsync(Scenes.Game, LoadSceneMode.Single);
            yield return null;

            var screens = Object.FindFirstObjectByType<GameScreens>();
            screens.OpenMenu();
            yield return null;

            // The ad is asked for and skipped (no SDK here); what matters is that the menu opened.
            Assert.That(screens.MenuIsOpen, Is.True, "the gear menu did not open");
            Assert.That(screens.VisibleScreen, Is.EqualTo("menu"));

            screens.CloseMenu();
            yield return null;
        }

        [UnityTest]
        public IEnumerator WithNoProductInTheStoreTheMenuDoesNotOfferTheButton()
        {
            // ⛔ APPLE REJECTED THE APP FOR THIS, 2.1.0 App Completeness, 2026-10-14. The button
            // was shown because a product id was configured, which proves only that someone
            // typed a string. remove_ads did not exist in App Store Connect -- the Paid
            // Applications Agreement was never activated, so it could not be created -- and a
            // reviewer pressed a button that logged "the store has no remove_ads" and did
            // nothing whatever.
            StorePurchases.Instance.SetProductAvailableForTests(false);
            yield return null;

            var menu = Object.FindFirstObjectByType<MainMenuScreen>();

            Assert.That(StorePurchases.Instance.ShouldOffer, Is.False,
                "the store has nothing to sell and the game still means to sell it");
            Assert.That(menu.RemoveAdsOffered, Is.False,
                "the Remove Ads button is on the menu with no product behind it — this is the " +
                "exact state Apple rejected");
        }

        [UnityTest]
        public IEnumerator WhenTheStoreAnswersLateTheButtonStillArrives()
        {
            // ⚠ The flip side: availability is asynchronous, so the menu is built before the
            // answer comes. The button must appear when it does, not stay hidden for the
            // session -- otherwise the fix for the rejection would hide a working product.
            StorePurchases.Instance.SetProductAvailableForTests(false);
            yield return null;

            var menu = Object.FindFirstObjectByType<MainMenuScreen>();
            Assert.That(menu.RemoveAdsOffered, Is.False, "precondition: hidden with no product");

            StorePurchases.Instance.SetProductAvailableForTests(true);
            yield return null;

            Assert.That(menu.RemoveAdsOffered, Is.True,
                "the store answered and the menu never un-hid the button");
        }

        [UnityTest]
        public IEnumerator OnIosTheButtonIsShownBeforeTheStoreAnswers()
        {
            // ⛔ THE 5.6 REJECTION, 2026-10-24: hidden until StoreKit answered, the button was
            // never on the reviewer's screen, though the notes and recording showed it.
            StorePurchases.ApplePlatformForTests = true;
            StorePurchases.Instance.SetProductAvailableForTests(false);
            yield return null;

            var menu = Object.FindFirstObjectByType<MainMenuScreen>();

            Assert.That(StorePurchases.Instance.ShouldOffer, Is.True,
                "on iOS the purchase must not depend on the store having answered");
            Assert.That(menu.RemoveAdsOffered, Is.True,
                "the Remove Ads button is hidden on iOS — this is the exact state Apple rejected under 5.6");
        }

        [UnityTest]
        public IEnumerator OnIosAPressWithNoProductSaysTheStoreIsUnavailable()
        {
            // ⛔ The 2.1.0 half: a visible button that does nothing at all is what was rejected
            // first. Play Mode has no store, so this press cannot open a purchase sheet.
            StorePurchases.ApplePlatformForTests = true;
            StorePurchases.Instance.SetProductAvailableForTests(false);
            yield return null;

            var menu = Object.FindFirstObjectByType<MainMenuScreen>();
            string label = menu.RemoveAdsText;

            menu.PressRemoveAdsForTests();
            yield return null;

            Assert.That(menu.RemoveAdsText, Is.EqualTo(MainMenuScreen.StoreUnavailableText),
                "a press that could not reach the store gave the player no answer");
            Assert.That(menu.RemoveAdsOffered, Is.True, "the button vanished after a failed press");

            yield return new WaitForSecondsRealtime(MainMenuScreen.UnavailableSeconds + 0.5f);

            Assert.That(menu.RemoveAdsText, Is.EqualTo(label),
                "the 'store unavailable' message never went away");
        }

        [UnityTest]
        public IEnumerator TheMenuAlwaysBuildsARestoreButton()
        {
            var menu = Object.FindFirstObjectByType<MainMenuScreen>();

            Assert.That(menu.RestoreExists, Is.True,
                "the Main Menu has no Restore Purchases element — Apple 3.1.1 asks for one, and " +
                "StorePurchases.Restore() would have no caller again");

            Assert.That(menu.RestoreOffered,
                Is.EqualTo(RemoveAdsRule.ShowRestoreButton(
                    Application.platform == RuntimePlatform.IPhonePlayer ||
                    Application.platform == RuntimePlatform.OSXPlayer ||
                    Application.platform == RuntimePlatform.OSXEditor)),
                "the button's visibility disagrees with the rule for this platform");
            yield return null;
        }

        [UnityTest]
        public IEnumerator APurchaseTakesRemoveAdsAwayAndLeavesRestoreAlone()
        {
            // ⛔ THE BUG THIS EXISTS FOR, reported on 2026-10-14 from a sibling project: Restore
            // sat beside Remove Ads and was hidden by the same purchase that hid Remove Ads. A
            // reviewer who had already bought could then find no way to restore at all.
            var menu = Object.FindFirstObjectByType<MainMenuScreen>();
            bool restoreBefore = menu.RestoreOffered;

            WriteAdsRemoved(true);
            yield return SceneManager.LoadSceneAsync(Scenes.Menu, LoadSceneMode.Single);
            yield return null;

            menu = Object.FindFirstObjectByType<MainMenuScreen>();

            Assert.That(menu.RemoveAdsOffered, Is.False, "Remove Ads survived its own purchase");
            Assert.That(menu.RestoreOffered, Is.EqualTo(restoreBefore),
                "the purchase moved the Restore button — it must be untouched by buying, because " +
                "a player who has bought is exactly who needs it");
        }
    }
}
