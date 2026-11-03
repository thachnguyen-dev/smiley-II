using System.Collections;
using NUnit.Framework;
using Smiley.Ads;
using Smiley.Flow;
using Smiley.Save;
using Smiley.View;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Smiley.Tests
{
    /// <summary>
    /// D-122: an interstitial at Continue / New Game never stands between the player and the
    /// climb when there is none to show.
    /// </summary>
    /// <remarks>
    /// ⚠ D-125 moved the interstitial from MenuBanner to AdService; these follow it.
    /// ⛔ The SDK never runs in the Editor, so these can only prove the SKIP path — which is the
    /// one that matters for "the game must never block on an ad": no unit configured, no SDK, no
    /// fill and a capped press all take it. The show path is proven on the phone.
    /// </remarks>
    public sealed class InterstitialSceneTests
    {
        [UnitySetUp]
        public IEnumerator Load()
        {
            SaveFile.DeleteAll();
            GameSession.Reset();
            AdService.ResetForTests();
            yield return SceneManager.LoadSceneAsync(Scenes.Menu, LoadSceneMode.Single);
            yield return null;
        }

        [TearDown]
        public void Clean()
        {
            SaveFile.DeleteAll();
            GameSession.Reset();
            AdService.ResetForTests();
        }

        [UnityTest]
        public IEnumerator WithNoInterstitialLoadedTheStartButtonGoesStraightThrough()
        {
            var ads = AdService.Instance;
            Assert.That(ads, Is.Not.Null, "the Menu scene created no AdService");
            // In the Editor the SDK is never started, so nothing is ever loaded — the same state
            // as no fill on a phone, which is what this path is for. ⚠ The words moved with
            // D-125 (AdService now says "editor: skipped" where MenuBanner said "not
            // configured"); what must hold is that no interstitial is loading or ready.
            Assert.That(ads.InterstitialStatus, Is.EqualTo("editor: skipped").Or.EqualTo("not configured").Or.EqualTo("not allowed"),
                $"the Editor reports '{ads.InterstitialStatus}' — the SDK must never run here");

            bool continued = false;
            bool showing = ads.ShowInterstitialThen(() => continued = true);

            Assert.That(showing, Is.False, "claimed to be showing an ad with nothing loaded");
            Assert.That(continued, Is.True, "the continuation did not run — the game would never start");
            yield return null;
        }

        [UnityTest]
        public IEnumerator PressingStartStillChoosesAndLoads()
        {
            // The real button, through MainMenuScreen.StartPlaying, with the ad layer in between.
            var menu = Object.FindFirstObjectByType<MainMenuScreen>();
            var root = menu.GetComponent<UIDocument>().rootVisualElement;
            var button = root.Q<Button>(UiKit.Names.Primary);
            Assert.That(button, Is.Not.Null);

            var action = button.userData as System.Action;
            Assert.That(action, Is.Not.Null, "the start button carries no action");
            action();

            Assert.That(GameSession.WasChosen, Is.True, "the choice was not made");

            // The Game scene load was requested synchronously; give it two frames and confirm it arrived.
            yield return null;
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(Scenes.Game),
                "the start button did not reach the Game scene");
        }
    }
}
