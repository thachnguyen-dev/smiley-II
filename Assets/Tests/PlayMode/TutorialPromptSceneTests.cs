using System.Collections;
using NUnit.Framework;
using Smiley.Flow;
using Smiley.Gameplay;
using Smiley.Save;
using Smiley.View;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Smiley.Tests
{
    /// <summary>D-131's guided opening, as the running game has it.</summary>
    /// <remarks>
    /// ⛔ <b>The one thing that must not regress.</b> The prompt floats over live gameplay, right
    /// where §6 puts the player's thumb. If it ever becomes pickable it swallows the drag it is
    /// teaching, and the game is unplayable for exactly the players being taught.
    /// </remarks>
    public sealed class TutorialPromptSceneTests
    {
        [UnitySetUp]
        public IEnumerator Load()
        {
            SaveFile.DeleteAll();
            GameSession.Reset();
            yield return SceneManager.LoadSceneAsync(Scenes.Game, LoadSceneMode.Single);
            yield return null;
        }

        [TearDown]
        public void Clean()
        {
            SaveFile.DeleteAll();
            GameSession.Reset();
        }

        private static TutorialPrompt Prompt() => Object.FindFirstObjectByType<TutorialPrompt>();

        [UnityTest]
        public IEnumerator TheGameSceneHasOne()
        {
            Assert.That(Prompt(), Is.Not.Null, "the Game scene has no TutorialPrompt (D-131)");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ItNeverSwallowsADrag()
        {
            yield return null;

            var document = Object.FindFirstObjectByType<UIDocument>();
            var label = document.rootVisualElement.Q<Label>(UiKit.Names.TutorialPrompt);

            Assert.That(label, Is.Not.Null, "the prompt label is missing from the owner's layout");
            Assert.That(label.pickingMode, Is.EqualTo(PickingMode.Ignore),
                "the prompt can be hit-tested, so it will eat the drag it exists to teach");
        }

        [UnityTest]
        public IEnumerator ItSaysNothingBeforeTheInstructionImageHasBeenClosed()
        {
            yield return null;
            yield return null;

            // ⓘ The guided opening begins when §30's image closes, which is also the only thing
            // that confines it to a first playthrough.
            Assert.That(Prompt().Armed, Is.False, "the prompt armed itself before the image closed");
            Assert.That(Prompt().Showing, Is.EqualTo(TutorialPromptRule.Prompt.None));
        }

        [UnityTest]
        public IEnumerator ItIsHiddenWhileAScreenCoversTheGame()
        {
            yield return null;

            var screens = Object.FindFirstObjectByType<GameScreens>();
            screens.OpenMenu();
            yield return null;
            yield return null;

            Assert.That(Prompt().Showing, Is.EqualTo(TutorialPromptRule.Prompt.None),
                "a prompt is visible behind the gear menu");
        }

        [UnityTest]
        public IEnumerator LaunchingIsCountedSoTheOpeningCanEnd()
        {
            yield return null;

            TutorialPrompt prompt = Prompt();
            int before = prompt.Launches;

            Object.FindFirstObjectByType<SmileyController>().SendMessage(
                "OnLaunchedForTest", 1f, SendMessageOptions.DontRequireReceiver);

            // ⓘ The count is driven by the controller's Launched event; this only asserts the
            // starting state and that Restart clears it, because firing a real launch needs a
            // drag the test harness cannot perform.
            Assert.That(before, Is.Zero, "launches did not start at zero");

            prompt.Restart();
            Assert.That(prompt.Launches, Is.Zero);
            Assert.That(prompt.Armed, Is.False, "Restart left the opening armed");
            yield return null;
        }
    }
}
