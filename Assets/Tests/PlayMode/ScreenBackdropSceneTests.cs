using System.Collections;
using NUnit.Framework;
using Smiley.Flow;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Smiley.Tests
{
    /// <summary>
    /// D-123: while a screen covers the game, the document root wears that screen's colour so
    /// the strip above the safe area (the status bar's height) shows no world; during play the
    /// root is clear again.
    /// </summary>
    /// <remarks>
    /// ⭐ Owner, 2026-09-17: <i>"a portion of the Stage is still visible at the top behind/above
    /// the menu panels."</i> The colour is read from the owner's stylesheet by way of the
    /// screen's resolved style, so these assert equality with it rather than a literal.
    /// </remarks>
    public sealed class ScreenBackdropSceneTests
    {
        [UnitySetUp]
        public IEnumerator Load()
        {
            yield return GameSceneFixture.LoadFresh();
        }

        private static (Smiley.View.GameScreens screens, VisualElement root) Ui()
        {
            var screens = Object.FindFirstObjectByType<Smiley.View.GameScreens>();
            return (screens, screens.GetComponent<UIDocument>().rootVisualElement);
        }

        [UnityTest]
        public IEnumerator DuringPlayTheRootIsClear()
        {
            (var screens, var root) = Ui();
            yield return null;

            Assert.That(screens.VisibleScreen, Is.Null, "a screen is up; this fixture wants play");
            Assert.That(root.style.backgroundColor.keyword, Is.EqualTo(StyleKeyword.Null),
                "the root is painted during play — the world above the safe area would be hidden");
        }

        [UnityTest]
        public IEnumerator TheGameplayMenuPaintsTheRootInItsOwnColour()
        {
            (var screens, var root) = Ui();
            screens.OpenMenu();
            yield return null;
            yield return null;
            yield return null;

            VisualElement cover = screens.CoveringElement;
            Assert.That(cover, Is.Not.Null, "the menu is open but nothing is reported as covering");
            Assert.That(screens.BackdropColour.a, Is.GreaterThan(0f), "the menu's colour was read as fully transparent");
            Assert.That(root.resolvedStyle.backgroundColor, Is.EqualTo(screens.BackdropColour),
                "the strip above the menu is not the menu's colour");
            // ⚠ One tint, not two: while the root wears the colour, the screen itself is clear.
            Assert.That(cover.resolvedStyle.backgroundColor.a, Is.EqualTo(0f).Within(1e-3f),
                "the overlay still paints its own background as well as the root — the safe area is tinted twice");

            screens.CloseMenu();
            yield return null;
            yield return null;

            Assert.That(root.style.backgroundColor.keyword, Is.EqualTo(StyleKeyword.Null),
                "closing the menu did not clear the root");
            Assert.That(cover.style.backgroundColor.keyword, Is.EqualTo(StyleKeyword.Null),
                "the menu did not get its own background back");
        }

        [UnityTest]
        public IEnumerator TheOpeningStoryPaintsTheRootInThePicturesColour()
        {
            (var screens, var root) = Ui();
            var flow = Object.FindFirstObjectByType<CompletionFlow>();
            Assert.That(flow.ForceOpening(), Is.True, "the opening never started");
            yield return null;
            yield return null;
            yield return null;

            Assert.That(screens.VisibleScreen, Is.EqualTo("story"));
            VisualElement cover = screens.CoveringElement;
            // ⭐ D-137: there is no picture to letterbox any more. The story screen is black
            // from edge to edge, so the backdrop IS the screen.
            Assert.That(cover.name, Is.EqualTo("story-screen"), "the story's backdrop is now the whole black screen");
            Assert.That(screens.BackdropColour.a, Is.GreaterThan(0f), "the picture's letterbox colour was read as transparent");
            Assert.That(root.resolvedStyle.backgroundColor, Is.EqualTo(screens.BackdropColour));
            // Opaque: the picture keeps its own letterbox, or the sky-blue screen behind it would show.
            Assert.That(cover.style.backgroundColor.keyword, Is.EqualTo(StyleKeyword.Null),
                "an opaque cover had its own background cleared");
        }
    }
}
