using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Smiley.Editor.Setup
{
    /// <summary>
    /// Checks that the owner's layouts still contain every control the game looks for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>This is what makes D-057 safe.</b> Handing the layouts to the owner means the game
    /// can no longer assume its own UI — a renamed element disconnects a button, and a deleted
    /// one disconnects it silently. Nothing throws: <c>Q&lt;Button&gt;("gone")</c> simply returns
    /// null, the screen still draws, and the button just does not work.
    /// </para>
    /// <para>
    /// ⛔ <b>So this FAILS THE BUILD rather than warning</b>, and names the file and the element.
    /// It is the same discipline as the scene wiring guard, applied to the one part of the
    /// project that is now edited by hand.
    /// </para>
    /// <para>
    /// ⚠ <b>It checks names and nothing else.</b> Position, order, hierarchy, styling, extra
    /// decoration and added artwork are all the owner's and are all invisible to this — which is
    /// the point. The contract between us is a list of names, not a layout.
    /// </para>
    /// </remarks>
    public static class UiLayoutGuard
    {
        /// <summary>What each document must still contain.</summary>
        /// <summary>
        /// Names that must NOT appear in a document, because they belong to the other one.
        /// </summary>
        /// <remarks>
        /// ⛔ D-098: the owner's Main Menu carried an <c>&lt;Instance template="StoryScreens"/&gt;</c>
        /// — the whole Game document dragged into the menu in UI Builder — so the menu showed
        /// the story, the tutorial, the Completed screen, the gameplay menu and its corner button
        /// all at once, and nothing in the Menu scene knows how to hide them. A name from the
        /// other document is the signature of that mistake.
        /// </remarks>
        private static readonly Dictionary<string, string[]> Forbidden =
            new Dictionary<string, string[]>
            {
                [UiLayouts.MenuUxml] = new[] { "story-screen", "tutorial-screen", "completed-screen", "gameplay-menu", "gameplay-menu-button" },
                [UiLayouts.StoryUxml] = new[] { "settings-screen", "confirm-screen", "primary-action", "remove-ads" }, // ⓘ not "main-menu": the gameplay menu's button shares that name
            };

        private static readonly Dictionary<string, string[]> Required =
            new Dictionary<string, string[]>
            {
                [UiLayouts.MenuUxml] = new[]
                {
                    "main-menu", "settings-screen", "confirm-screen",
                    "primary-action", "settings", "exit", "remove-ads",
                    "music-volume", "sfx-volume",
                    "reset-game", "back", "reset-confirm", "reset-cancel",
                    "consent-screen", "consent-question", "consent-answer", "consent-grant", "consent-refuse",
                },

                [UiLayouts.StoryUxml] = new[]
                {
                    "story-screen", "story-panel", "story-text", "story-next",
                    "tutorial-screen", "tutorial-image", "tutorial-close",
                    "tutorial-prompt",
                    "completed-screen", "acknowledge",
                    "gameplay-menu-button", "gameplay-menu", "resume", "main-menu",
                },
            };

        /// <summary>Returns the number of missing elements, and logs each one.</summary>
        public static int Check()
        {
            int missing = 0;

            foreach (KeyValuePair<string, string[]> document in Required)
            {
                var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(document.Key);

                if (tree == null)
                {
                    Debug.LogError($"[UI-GUARD] {document.Key} is missing entirely.");
                    missing++;
                    continue;
                }

                // ⓘ Instantiated rather than parsed as text, so this sees what the game will see
                // — including anything pulled in by a template — instead of what the file says.
                VisualElement root = tree.Instantiate();

                if (Forbidden.TryGetValue(document.Key, out string[] forbidden))
                {
                    foreach (string name in forbidden)
                    {
                        if (root.Q(name) != null)
                        {
                            Debug.LogError(
                                $"[UI-GUARD] {document.Key} contains '{name}', which belongs to the OTHER " +
                                "document. ⚠ The whole other layout was probably dragged in as a template " +
                                "instance in UI Builder — delete that instance from the Hierarchy.");

                            missing++;
                        }
                    }
                }

                foreach (string name in document.Value)
                {
                    VisualElement element = root.Q(name);

                    if (element == null)
                    {
                        Debug.LogError(
                            $"[UI-GUARD] {document.Key} has no element named '{name}'. The game " +
                            "looks it up by that name, so whatever it drives is now dead. " +
                            "⚠ Rename it back — you can move, restyle or re-parent it freely, " +
                            "but the name is the contract.");

                        missing++;
                        continue;
                    }

                    // ⛔ A DISABLED BUTTON IS AS DEAD AS A MISSING ONE, and looks far less
                    // wrong. UI Builder has an "Enabled" checkbox in the Inspector, and unticking
                    // it by accident leaves a button that draws greyed and ignores every tap. The
                    // first tutorial X shipped that way (D-071): on the phone the game could not
                    // be started at all, and nothing had said so.
                    if (!element.enabledSelf)
                    {
                        Debug.LogError(
                            $"[UI-GUARD] {document.Key}: '{name}' is DISABLED (enabled=\"false\"). " +
                            "It will draw and ignore every tap. ⚠ In UI Builder, select it and " +
                            "tick 'Enabled' in the Inspector.");

                        missing++;
                    }
                }

                Debug.Log($"[UI-GUARD] {document.Key}: " +
                          $"{document.Value.Count(n => root.Q(n) != null)}/{document.Value.Length} " +
                          "required elements present.");
            }

            return missing;
        }
    }
}
