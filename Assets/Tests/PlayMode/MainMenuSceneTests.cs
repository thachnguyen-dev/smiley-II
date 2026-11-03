using System.Collections;
using NUnit.Framework;
using Smiley.Audio;
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
    /// §22's Main Menu and §25's Reset Game, in the scene the game actually boots into.
    /// </summary>
    /// <remarks>
    /// ⚠ Every test deletes the save <b>before</b> loading, because the menu reads it while
    /// building itself — a file left by another fixture would decide which button this one sees.
    /// </remarks>
    public sealed class MainMenuSceneTests
    {
        private IEnumerator LoadMenu()
        {
            GameSession.Reset();

            // D-107: the consent question comes before the menu when nothing is stored. Every
            // fixture about the MENU stores an answer first; the consent fixtures delete it again.
            if (!SettingsFile.Exists())
            {
                var answered = new GameSettings { ParentalConsent = (int)Smiley.Ads.ParentalConsent.Refused };
                SettingsFile.Write(answered);
            }

            SceneManager.LoadScene(Scenes.Menu, LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        /// <summary>The menu as a brand-new install sees it: no answer stored.</summary>
        private IEnumerator LoadMenuUnanswered()
        {
            GameSession.Reset();
            SettingsFile.DeleteForTests();
            SceneManager.LoadScene(Scenes.Menu, LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        [TearDown]
        public void Clear()
        {
            SaveFile.DeleteAll();
            SettingsFile.DeleteForTests();
            GameSession.Reset();
        }

        private static MainMenuScreen Menu()
        {
            var menu = Object.FindFirstObjectByType<MainMenuScreen>();
            Assert.That(menu, Is.Not.Null, "the Menu scene has no MainMenuScreen");
            return menu;
        }

        private static Button ButtonNamed(string name)
        {
            var document = Menu().GetComponent<UIDocument>();
            var button = document.rootVisualElement.Q<Button>(name);

            Assert.That(button, Is.Not.Null, $"the menu has no '{name}' button");
            return button;
        }

        [UnityTest]
        public IEnumerator TheMenuSceneIsInTheBuildAndDraws()
        {
            // ⛔ THE REACHABILITY TEST, and it is the one that matters most here. A menu scene
            // that is not in the build settings loads in the editor and does not exist on the
            // device — and an app that boots into the game instead would look almost right.
            SaveFile.DeleteAll();

            yield return LoadMenu();

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(Scenes.Menu));

            var document = Menu().GetComponent<UIDocument>();
            Assert.That(document.panelSettings, Is.Not.Null,
                "the UIDocument has no panel settings, so the menu draws nothing at all");

            Assert.That(document.rootVisualElement.Query<Button>().ToList(), Is.Not.Empty,
                "the menu built no buttons");
        }

        [UnityTest]
        public IEnumerator WithNoSaveTheMenuOffersANewGame()
        {
            // §22: "If there is no Save: show New Game."
            SaveFile.DeleteAll();

            yield return LoadMenu();

            Assert.That(Menu().Primary, Is.EqualTo(MainMenuAction.NewGame));
            Assert.That(ButtonNamed(UiKit.Names.Primary).text, Is.EqualTo("New Game"));
        }

        [UnityTest]
        public IEnumerator WithASaveTheMenuOffersContinue()
        {
            // §22: "If a Save exists: show Continue."
            SaveFile.DeleteAll();
            SaveFile.Write(new SaveData { SmileyX = 1f, SmileyY = 2f });

            yield return LoadMenu();

            Assert.That(Menu().Primary, Is.EqualTo(MainMenuAction.Continue));
            Assert.That(ButtonNamed(UiKit.Names.Primary).text, Is.EqualTo("Continue"));
        }

        [UnityTest]
        public IEnumerator ThereIsOnlyEverOneStartingButton()
        {
            // §22 shows New Game OR Continue. ⛔ Not both, and not one greyed out — a menu with
            // two entry points is a different design from the one that was approved.
            SaveFile.DeleteAll();
            SaveFile.Write(new SaveData());

            yield return LoadMenu();

            var document = Menu().GetComponent<UIDocument>();
            int starters = 0;

            foreach (Button button in document.rootVisualElement.Query<Button>().ToList())
            {
                string text = (button.text ?? string.Empty).ToLowerInvariant();

                if (text.Contains("continue") || text.Contains("new game"))
                {
                    starters++;
                }
            }

            Assert.That(starters, Is.EqualTo(1),
                $"the menu offers {starters} ways to start, and §22 offers one");
        }

        [UnityTest]
        public IEnumerator TheMenuCarriesSettingsAndExit()
        {
            // §22: "The Main Menu also contains Settings, Exit / Quit."
            SaveFile.DeleteAll();

            yield return LoadMenu();

            Assert.That(ButtonNamed(UiKit.Names.Settings), Is.Not.Null);
            Assert.That(ButtonNamed(UiKit.Names.Exit), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator ChoosingContinueAsksToResumeAndNewGameDoesNot()
        {
            // ⛔ THE WHOLE POINT OF THE TWO ACTIONS. Continue resumes §21's save; New Game must
            // not, or the button would drop the player back into the climb they left.
            SaveFile.DeleteAll();
            SaveFile.Write(new SaveData { SmileyX = 3f });

            yield return LoadMenu();

            Click(Menu().GetComponent<UIDocument>(), UiKit.Names.Primary);

            Assert.That(GameSession.Requested, Is.EqualTo(GameEntryMode.Continue));
            Assert.That(GameSession.WasChosen, Is.True);

            // ⚠ Loading the Game scene here would run the real entry; the choice is what this
            // test is about, and CompletionSceneTests plus SaveSceneTests cover the arrival.
            SaveFile.DeleteAll();
            GameSession.Reset();

            yield return LoadMenu();

            Click(Menu().GetComponent<UIDocument>(), UiKit.Names.Primary);

            Assert.That(GameSession.Requested, Is.EqualTo(GameEntryMode.NewGame));
        }

        [UnityTest]
        public IEnumerator ResetGameNeedsConfirmingAndWarnsBeforeItDeletesAnything()
        {
            // §25: Reset Game "requires confirmation" and "must clearly warn that all progress
            // will be lost".
            SaveFile.DeleteAll();
            SaveFile.Write(new SaveData { SmileyX = 5f });

            yield return LoadMenu();

            MainMenuScreen menu = Menu();
            var document = menu.GetComponent<UIDocument>();

            Assert.That(menu.VisibleScreen, Is.EqualTo("menu"));

            Click(document, UiKit.Names.Settings);
            yield return null;

            // ⓘ Driven through the buttons' own callbacks rather than by calling the methods,
            // so a button wired to nothing fails this test.
            Assert.That(menu.VisibleScreen, Is.EqualTo("settings"),
                "the Settings button did not open Settings");

            Click(document, UiKit.Names.ResetGame);
            yield return null;

            Assert.That(menu.VisibleScreen, Is.EqualTo("confirm"),
                "Reset Game deleted the save without asking, and §25 requires confirmation");

            Assert.That(SaveFile.Exists(), Is.True,
                "⛔ THE SAVE WAS ALREADY GONE BEFORE THE PLAYER CONFIRMED ANYTHING");

            // The warning has to say what is lost.
            string warning = string.Empty;

            foreach (Label label in document.rootVisualElement.Query<Label>().ToList())
            {
                warning += " " + (label.text ?? string.Empty).ToLowerInvariant();
            }

            Assert.That(warning.Contains("lost") || warning.Contains("delete"), Is.True,
                "the confirmation does not warn that progress will be lost");

            Click(document, UiKit.Names.ResetConfirm);
            yield return null;

            Assert.That(SaveFile.Exists(), Is.False, "confirming Reset Game left the save behind");
            Assert.That(menu.VisibleScreen, Is.EqualTo("menu"),
                "§25 returns the player to the Main Menu");
            Assert.That(menu.Primary, Is.EqualTo(MainMenuAction.NewGame),
                "after Reset Game the menu still offers Continue");
        }

        [UnityTest]
        public IEnumerator CancellingAResetKeepsTheSave()
        {
            SaveFile.DeleteAll();
            SaveFile.Write(new SaveData { SmileyX = 7f });

            yield return LoadMenu();

            MainMenuScreen menu = Menu();
            var document = menu.GetComponent<UIDocument>();

            Click(document, UiKit.Names.Settings);
            yield return null;
            Click(document, UiKit.Names.ResetGame);
            yield return null;
            Click(document, UiKit.Names.ResetCancel);
            yield return null;

            Assert.That(SaveFile.Exists(), Is.True, "cancelling a reset still deleted the save");
            Assert.That(menu.VisibleScreen, Is.EqualTo("settings"));
        }

        [UnityTest]
        public IEnumerator TheMenuOffersNoRestartLevel()
        {
            // ⛔ §24 again, at the other end of the game.
            SaveFile.DeleteAll();

            yield return LoadMenu();

            var document = Menu().GetComponent<UIDocument>();

            foreach (Button button in document.rootVisualElement.Query<Button>().ToList())
            {
                string text = (button.text ?? string.Empty).ToLowerInvariant();

                Assert.That(text.Contains("restart"), Is.False,
                    $"a menu button reads '{button.text}', and §24 removes Restart Level");
            }
        }

        /// <summary>
        /// Presses a button the way the panel would.
        /// </summary>
        /// <remarks>
        /// ⚠ <b>Through the action the button carries, not through a synthesized event.</b> A
        /// <c>ClickEvent</c> sent straight at a <c>Button</c> does not run its <c>clicked</c>
        /// callback — <c>Clickable</c> listens for pointer events — so the first version of this
        /// helper pressed every button in the menu and changed nothing, and the tests failed
        /// saying the Settings button did not open Settings. It was right; the helper was wrong.
        /// ⓘ <c>UiKit</c> stores each button's action on the element, so this still proves the
        /// button is wired to the right thing — the failure that actually matters.
        /// </remarks>
        private static void Click(UIDocument document, string name)
        {
            var button = document.rootVisualElement.Q<Button>(name);
            Assert.That(button, Is.Not.Null, $"no '{name}' button to click");

            var action = button.userData as System.Action;
            Assert.That(action, Is.Not.Null,
                $"the '{name}' button carries no action, so pressing it would do nothing");

            action();
        }

        [UnityTest]
        public IEnumerator TheOwnersLayoutSitsInsideTheSafeArea()
        {
            // ⭐ D-072: the owner lays the menu out on a 1080 × 2400 canvas, and the phone gives
            // the top 78 px of that to its status bar. The game re-parents their whole document
            // under one container inset by the safe area, so "top: 0" in UI Builder lands just
            // below the bar. Asserted structurally — the editor's safe area is the whole screen,
            // so the inset itself is zero here and is tested as arithmetic in SafeInsetsTests.
            SaveFile.DeleteAll();

            yield return LoadMenu();

            VisualElement root = Menu().GetComponent<UIDocument>().rootVisualElement;
            VisualElement safe = root.Q("safe-area");

            Assert.That(safe, Is.Not.Null, "the menu document was never wrapped in the safe area");
            Assert.That(root.childCount, Is.EqualTo(1),
                "something sits beside the wrapper, outside the safe area");

            foreach (string name in new[]
            {
                "main-menu", "settings-screen", "confirm-screen",
                UiKit.Names.Primary, UiKit.Names.Settings, UiKit.Names.Exit,
                UiKit.Names.ResetGame, UiKit.Names.Back,
                UiKit.Names.ResetConfirm, UiKit.Names.ResetCancel,
            })
            {
                Assert.That(safe.Q(name), Is.Not.Null, $"'{name}' is not inside the safe area");
            }
        }

        [UnityTest]
        public IEnumerator TheMenuSceneHasAnEar()
        {
            // D-097: the menu plays the music too, and had no AudioListener either.
            SaveFile.DeleteAll();
            yield return LoadMenu();

            Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator OnlyTheMainMenuIsVisibleAndNothingFromTheGameDocumentIsThere()
        {
            // ⛔ D-098: the owner's menu carried an instance of the whole Game document, so the
            // story, tutorial and ≡ button drew on top of the menu. Asserted on what is on
            // screen: exactly one of the three menu screens, and none of the Game scene's names.
            SaveFile.DeleteAll();
            yield return LoadMenu();

            VisualElement root = Menu().GetComponent<UIDocument>().rootVisualElement;

            int visibleScreens = 0;
            foreach (string name in new[] { "main-menu", "settings-screen", "confirm-screen" })
            {
                if (UiKit.IsVisible(root.Q(name))) visibleScreens++;
            }

            Assert.That(visibleScreens, Is.EqualTo(1), "the menu shows more than one of its screens at once");

            foreach (string name in new[] { "story-screen", "tutorial-screen", "completed-screen", "gameplay-menu", UiKit.Names.MenuButton })
            {
                Assert.That(root.Q(name), Is.Null, $"'{name}' belongs to the Game document and is in the menu");
            }
        }

        [UnityTest]
        public IEnumerator TheBannerLivesInTheMenuAndIsConfiguredButStaysOutOfTheEditor()
        {
            // D-003/D-004/D-105: one banner, in the menu, with the owner's IDs; the SDK is never
            // touched in the Editor, so nothing here can hang on a network.
            SaveFile.DeleteAll();
            yield return LoadMenu();

            var banner = Object.FindFirstObjectByType<Smiley.Ads.MenuBanner>();
            Assert.That(banner, Is.Not.Null, "the Menu scene has no MenuBanner");

            yield return null;

            // D-110: not child-directed, so nothing gates the SDK; in the Editor it is skipped.
            Assert.That(banner.Status, Is.EqualTo("editor: skipped"), banner.Status);
        }

        [UnityTest]
        public IEnumerator TheGateStillWorksWhenOpenedAndAWrongAnswerDoesNotConsent()
        {
            // D-107's gate, kept behind AdSettings.ChildDirected (D-110). The app is not
            // child-directed now, so the menu comes first — and the gate, opened by hand, still
            // refuses a wrong answer and records a right one.
            SaveFile.DeleteAll();
            yield return LoadMenuUnanswered();

            MainMenuScreen menu = Menu();
            Assert.That(menu.ChildDirected, Is.False, "the owner ruled the app not directed at children (D-110)");
            Assert.That(menu.VisibleScreen, Is.EqualTo("menu"), "a general-audience app shows no gate");
            Assert.That(menu.Consent, Is.EqualTo(Smiley.Ads.ParentalConsent.Unknown));

            menu.AskForConsent();
            yield return null;
            Assert.That(menu.VisibleScreen, Is.EqualTo("consent"));

            var answer = menu.GetComponent<UIDocument>().rootVisualElement.Q<TextField>("consent-answer");
            answer.value = "1";
            menu.GrantConsent();
            yield return null;

            Assert.That(menu.VisibleScreen, Is.EqualTo("consent"), "a wrong answer must not pass the gate");
            Assert.That(menu.Consent, Is.EqualTo(Smiley.Ads.ParentalConsent.Unknown));

            var question = menu.GetComponent<UIDocument>().rootVisualElement.Q<Label>("consent-question");
            var parts = question.text.Replace("Parents: what is ", "").Replace("?", "").Split('×');
            answer.value = (int.Parse(parts[0].Trim()) * int.Parse(parts[1].Trim())).ToString();
            menu.GrantConsent();
            yield return null;

            Assert.That(menu.VisibleScreen, Is.EqualTo("menu"));
            Assert.That(menu.Consent, Is.EqualTo(Smiley.Ads.ParentalConsent.Granted));
            Assert.That(Smiley.Ads.ConsentRule.FromStored(SettingsFile.Read().ParentalConsent), Is.EqualTo(Smiley.Ads.ParentalConsent.Granted), "consent was not saved");
        }

        [UnityTest]
        public IEnumerator RefusingIsRememberedAndForAGeneralAudienceTheBannerIsNotGated()
        {
            SaveFile.DeleteAll();
            yield return LoadMenuUnanswered();

            MainMenuScreen menu = Menu();
            menu.AskForConsent();
            menu.RefuseConsent();
            yield return null;

            Assert.That(menu.VisibleScreen, Is.EqualTo("menu"));
            Assert.That(menu.Consent, Is.EqualTo(Smiley.Ads.ParentalConsent.Refused));

            // D-110: not child-directed, so the stored answer does not gate the SDK — in the
            // Editor the SDK is skipped, which is the status that proves the gate was passed.
            var banner = Object.FindFirstObjectByType<Smiley.Ads.MenuBanner>();
            Assert.That(banner.Status, Is.EqualTo("editor: skipped"), banner.Status);

            yield return LoadMenu();
            Assert.That(Menu().VisibleScreen, Is.EqualTo("menu"));
        }
    }
}
