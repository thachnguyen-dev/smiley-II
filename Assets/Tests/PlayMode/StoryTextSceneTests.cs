using System.Collections;
using NUnit.Framework;
using Smiley.Flow;
using Smiley.Save;
using Smiley.View;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Smiley.Tests
{
    /// <summary>The story reads as text, in the running game (D-137).</summary>
    /// <remarks>
    /// ⛔ <b>What this does and does not prove.</b> It proves the owner's words reach the screen
    /// and that stepping forward advances them. It cannot judge whether 42px white on black is
    /// comfortable to read on a phone — that is a look, and `coding-standards.md` says not to
    /// automate those. ⚠ It has not been seen on a device.
    /// </remarks>
    public sealed class StoryTextSceneTests
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

        private static Label Text() =>
            Object.FindFirstObjectByType<UIDocument>().rootVisualElement.Q<Label>(UiKit.Names.StoryText);

        [UnityTest]
        public IEnumerator TheOpeningShowsTheOwnersFirstLine()
        {
            var flow = Object.FindFirstObjectByType<CompletionFlow>();
            Assert.That(flow.ForceOpening(), Is.True, "the opening never started");
            yield return null;
            yield return null;

            Assert.That(Text(), Is.Not.Null, "the owner's layout has no story-text Label");
            Assert.That(Text().text, Is.EqualTo(StoryScript.Opening[0]),
                "the first page is not the owner's first line");
        }

        [UnityTest]
        public IEnumerator PressingNextAdvancesThroughTheOwnersLines()
        {
            var flow = Object.FindFirstObjectByType<CompletionFlow>();
            flow.ForceOpening();
            yield return null;

            flow.Next();
            yield return null;
            yield return null;

            Assert.That(Text().text, Is.EqualTo(StoryScript.Opening[1]),
                "Next did not move to the owner's second line");
        }

        [UnityTest]
        public IEnumerator ThereIsNoPictureLeftToShow()
        {
            // ⛔ D-137 removed the illustrated cutscenes. A stray background image on the story
            // screen would mean an old asset is still being drawn behind the words.
            var flow = Object.FindFirstObjectByType<CompletionFlow>();
            flow.ForceOpening();
            yield return null;
            yield return null;

            var screens = Object.FindFirstObjectByType<GameScreens>();
            Assert.That(screens.CoveringElement.name, Is.EqualTo("story-screen"),
                "the cover should be the black screen, not a picture");
            yield return null;
        }
    }
}
