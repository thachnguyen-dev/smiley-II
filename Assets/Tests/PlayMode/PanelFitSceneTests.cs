using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Smiley.Flow;
using Smiley.View;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Smiley.Tests
{
    /// <summary>
    /// Every button sits inside the panel that frames it, on every menu screen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠ <b>This is the bug D-124 introduced.</b> The Main Menu panel is a fixed-size sprite with
    /// room for three buttons; Remove Ads made four, and the fourth pushed Exit out through the
    /// bottom of the frame. Nothing failed — the build was green and the suite was green — because
    /// the only thing that knew was a screenshot nobody was comparing.
    /// </para>
    /// <para>
    /// ⛔ <b>The panels cannot simply be made bigger.</b> They are pack sprites stretched to a
    /// fixed size, and §32 keeps their proportions the owner's: growing one axis distorts the art.
    /// The room has to come from the contents, so this fixture measures the contents.
    /// </para>
    /// <para>
    /// ⓘ Measured in panel space, which is the reference resolution rather than any one device's
    /// pixels — the same reason §33 forbids deriving the world from screen size.
    /// </para>
    /// </remarks>
    public sealed class PanelFitSceneTests
    {
        private static readonly string[] Screens =
        {
            "main-menu", "settings-screen", "confirm-screen", "consent-screen",
        };

        private UIDocument _document;

        [UnitySetUp]
        public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync(Scenes.Menu, LoadSceneMode.Single);
            yield return null;

            _document = Object.FindFirstObjectByType<UIDocument>();
            Assert.That(_document, Is.Not.Null, "the Menu scene has no UIDocument to measure");
        }

        [UnityTest]
        public IEnumerator EveryButtonFitsInsideItsPanelOnEveryScreen()
        {
            var failures = new List<string>();

            foreach (string screenName in Screens)
            {
                VisualElement screen = _document.rootVisualElement.Q<VisualElement>(screenName);
                Assert.That(screen, Is.Not.Null, $"no screen named '{screenName}' in the layout");

                // ⓘ Shown one at a time: a display:none element resolves to a zero rect, which
                // would pass this test by measuring nothing at all.
                DisplayStyle was = screen.style.display.value;
                screen.style.display = DisplayStyle.Flex;
                yield return null;
                yield return null;

                VisualElement panel = screen.Q(className: "panel");
                Assert.That(panel, Is.Not.Null, $"screen '{screenName}' has no .panel to fit into");

                Rect frame = panel.worldBound;

                foreach (Button button in screen.Query<Button>().ToList())
                {
                    if (!UiKit.IsVisible(button))
                    {
                        continue;
                    }

                    Rect b = button.worldBound;

                    if (b.yMax > frame.yMax + 0.5f || b.yMin < frame.yMin - 0.5f ||
                        b.xMax > frame.xMax + 0.5f || b.xMin < frame.xMin - 0.5f)
                    {
                        failures.Add(
                            $"{screenName}/{button.name}: button {b} escapes panel {frame} " +
                            $"(over the bottom by {b.yMax - frame.yMax:F0}px)");
                    }
                }

                screen.style.display = was;
                yield return null;
            }

            Assert.That(failures, Is.Empty,
                "a button is drawn outside the panel that frames it:\n  " +
                string.Join("\n  ", failures));
        }
    }
}
