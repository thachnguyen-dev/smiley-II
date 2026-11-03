using UnityEngine;
using UnityEngine.UIElements;

namespace Smiley.View
{
    /// <summary>
    /// The few shapes every screen in this game is made of.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠ <b>PLACEHOLDER PRESENTATION, AND IT MUST STAY OBVIOUSLY SO.</b> §32 reserves the visual
    /// direction to the owner. Nothing here is a design: it is the smallest legible arrangement
    /// that lets §19–§25's flows be operated and tested on a phone. Colours, type and spacing are
    /// mine and provisional.
    /// </para>
    /// <para>
    /// ⭐ <b>THIS KIT NOW DOES TWO DIFFERENT JOBS, and the split is D-057's.</b> The <b>meta</b>
    /// screens — Main Menu, Settings, Reset, Story, Completed — are authored by the owner in
    /// UXML, and <see cref="Bind"/> and <see cref="BindVolume"/> attach behaviour to what they
    /// laid out. The <b>in-game overlay</b> — §23's menu and its corner button — is still built
    /// here in code, because it sits over live gameplay and was deliberately left out of the
    /// handover.
    /// </para>
    /// <para>
    /// ⚠ <b>The builders below therefore style only the in-game overlay.</b> Changing them will
    /// not change the Main Menu, and changing the Main Menu will not change them.
    /// </para>
    /// <para>
    /// ⛔ <b>Safe area is honoured, not assumed.</b> §33 and the platform notes require it — a
    /// notch or a gesture bar must eat margin, never a button.
    /// </para>
    /// </remarks>
    public static class UiKit
    {
        /// <summary>Names, so tests and code refer to one spelling.</summary>
        public static class Names
        {
            public const string Primary = "primary-action";
            public const string Settings = "settings";
            public const string Exit = "exit";
            public const string Resume = "resume";
            public const string MainMenu = "main-menu";
            public const string MenuButton = "gameplay-menu-button";
            public const string ResetGame = "reset-game";
            public const string ResetConfirm = "reset-confirm";
            public const string ResetCancel = "reset-cancel";
            public const string Back = "back";
            public const string Acknowledge = "acknowledge";
            public const string TutorialClose = "tutorial-close";

            /// <summary>D-131's guided-opening prompt, floating over live gameplay.</summary>
            public const string TutorialPrompt = "tutorial-prompt";
            public const string StoryNext = "story-next";

            /// <summary>D-137: the story is text now, not a picture.</summary>
            public const string StoryText = "story-text";
            public const string MusicVolume = "music-volume";
            public const string SfxVolume = "sfx-volume";
            public const string ConsentScreen = "consent-screen";
            public const string ConsentQuestion = "consent-question";
            public const string ConsentAnswer = "consent-answer";
            public const string ConsentGrant = "consent-grant";
            public const string ConsentRefuse = "consent-refuse";
            public const string ConsentReview = "consent-review";
            public const string RemoveAds = "remove-ads";

            /// <summary>
            /// Apple's guideline 3.1.1 restore mechanism. Always on screen on Apple platforms,
            /// bought or not — a restore button that hides after a purchase hides exactly when
            /// a reinstalling player needs it.
            /// </summary>
            public const string RestorePurchases = "restore-purchases";
        }

        /// <summary>
        /// Gives a named button in the owner's layout something to do.
        /// </summary>
        /// <remarks>
        /// <para>
        /// ⭐ <b>The binding half of D-057.</b> The owner lays the screens out in Unity; this
        /// finds a control by name and attaches behaviour. ⚠ Re-binding is safe: the previous
        /// callback is removed first, so calling this again after a screen is rebuilt does not
        /// stack up handlers and fire a button twice.
        /// </para>
        /// <para>
        /// ⛔ <b>A missing button is reported, not ignored.</b> <c>Q&lt;Button&gt;</c> returns null
        /// for a name that is not there and everything carries on looking fine — which is exactly
        /// how a dead control ships. <c>UiLayoutGuard</c> fails the build for the same reason;
        /// this is the runtime half, for a layout changed after a build.
        /// </para>
        /// </remarks>
        public static Button Bind(
            VisualElement root, string name, System.Action clicked, string text = null)
        {
            var button = root?.Q<Button>(name);

            if (button == null)
            {
                Debug.LogError(
                    $"[UI] No button named '{name}' in the layout, so whatever it does is now " +
                    "unreachable. ⚠ The name is the contract — move and restyle it freely, but " +
                    "do not rename or delete it.");

                return null;
            }

            if (!button.enabledSelf)
            {
                // The runtime half of the guard's disabled check, for a layout changed after the
                // build. Logged, not fixed: enabling a control the owner disabled would be
                // overriding a decision that might be deliberate.
                Debug.LogError(
                    $"[UI] The '{name}' button is DISABLED in the layout, so it will ignore " +
                    "every tap. Tick 'Enabled' on it in UI Builder.");
            }

            if (button.userData is System.Action previous)
            {
                button.clicked -= previous;
            }

            button.clicked += clicked;
            button.userData = clicked;

            if (text != null)
            {
                button.text = text;
            }

            return button;
        }

        /// <summary>Gives a named slider in the owner's layout a value and a consequence.</summary>
        /// <remarks>
        /// ⓘ The matching label — the slider's name plus <c>-label</c> — is updated with the
        /// percentage if the owner kept one. ⚠ It is optional: a layout that shows no number is
        /// a design choice, not a defect, so its absence is silent where a missing SLIDER is not.
        /// </remarks>
        public static Slider BindVolume(
            VisualElement root, string name, float value, System.Action<float> changed)
        {
            var slider = root?.Q<Slider>(name);

            if (slider == null)
            {
                Debug.LogError($"[UI] No slider named '{name}' in the layout, so §26's setting " +
                               "cannot be changed.");
                return null;
            }

            var label = root.Q<Label>(name + "-label");
            string caption = label != null ? label.text : null;

            // ⚠ Strip any percentage a previous bind wrote, or re-binding accumulates them.
            if (caption != null)
            {
                int cut = caption.IndexOf("  ", System.StringComparison.Ordinal);
                if (cut > 0)
                {
                    caption = caption.Substring(0, cut);
                }
            }

            if (slider.userData is EventCallback<ChangeEvent<float>> previous)
            {
                slider.UnregisterValueChangedCallback(previous);
            }

            void OnChanged(ChangeEvent<float> evt)
            {
                if (label != null)
                {
                    label.text = $"{caption}  {Mathf.RoundToInt(evt.newValue * 100f)}%";
                }

                changed?.Invoke(evt.newValue);
            }

            slider.RegisterValueChangedCallback(OnChanged);
            slider.userData = (EventCallback<ChangeEvent<float>>)OnChanged;

            slider.SetValueWithoutNotify(value);

            if (label != null)
            {
                label.text = $"{caption}  {Mathf.RoundToInt(value * 100f)}%";
            }

            return slider;
        }

        /// <summary>
        /// Shows or hides an element.
        /// </summary>
        /// <remarks>
        /// ⚠ <b>Through <c>display</c>, not <c>visibility</c> and not a <c>hidden</c> flag.</b>
        /// A merely invisible element still occupies its place in the layout and still takes
        /// pointer events — so a hidden full-screen overlay would keep swallowing every touch
        /// meant for Smiley, and the game would look fine and be uncontrollable.
        /// ⓘ <c>VisualElement.hidden</c> does not exist in this Unity version; the compiler said
        /// so, which is the authority this project trusts over recall (VERSION.md).
        /// </remarks>
        public static void SetVisible(VisualElement element, bool visible)
        {
            if (element == null)
            {
                return;
            }

            // D-113: GameScreens refreshes every frame, so an unconditional write here is a
            // per-frame style write for every screen in the layout. Compare first; a write of
            // the same value is not free in UI Toolkit.
            DisplayStyle wanted = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (element.style.display != wanted)
            {
                element.style.display = wanted;
            }
        }

        /// <summary>Whether an element is currently displayed.</summary>
        public static bool IsVisible(VisualElement element) =>
            element != null && element.style.display != DisplayStyle.None;

        private static IPanel _gamePanel;

        /// <summary>
        /// Tells <see cref="IsOverUi"/> which panel covers the game. Null forgets it.
        /// </summary>
        /// <remarks>
        /// ⭐ <b>D-111 made the whole screen a drag surface</b>, so the one thing a touch must
        /// still be tested against is the UI drawn over the game — §23's corner button. Before
        /// D-111 a touch had to land <i>on Smiley</i> to start a drag, and the button was never
        /// there; now a tap on it would begin a drag under the menu it opens.
        /// </remarks>
        public static void SetGamePanel(IPanel panel) => _gamePanel = panel;

        /// <summary>
        /// Whether a screen-space point (pixels, bottom-left origin) is over a pickable element
        /// of the game's panel.
        /// </summary>
        /// <remarks>
        /// ⓘ Asks the panel itself, so the answer is whatever UI Toolkit would route a click
        /// to: an element with <c>PickingMode.Ignore</c> — the root, the safe-area wrapper —
        /// is transparent, and a hidden screen picks nothing. No list of "blocking" elements
        /// to keep in step with the owner's layout.
        /// </remarks>
        public static bool IsOverUi(Vector2 screenPosition)
        {
            if (_gamePanel == null)
            {
                return false;
            }

            Vector2 panelPosition = RuntimePanelUtils.ScreenToPanel(_gamePanel, screenPosition);
            return _gamePanel.Pick(panelPosition) != null;
        }

        /// <summary>
        /// Moves everything in a document under a container that covers only the safe area.
        /// </summary>
        /// <remarks>
        /// <para>
        /// ⭐ <b>What makes UI Builder and the phone agree (D-072).</b> The game renders under the
        /// Android status bar, so the top 78 px of this phone belong to the system and a touch
        /// there never reaches Unity — but UI Builder's canvas shows all 2400 px as usable, and
        /// the owner placed an X at <c>top: 8px</c> on the strength of that. Wrapping their tree
        /// in a container inset by the safe area makes <c>top: 0</c> in the Builder mean "just
        /// below the status bar" on the phone.
        /// </para>
        /// <para>
        /// ⚠ <b>A wrapper, not padding on the root.</b> The owner's buttons are absolutely
        /// positioned, and an absolutely positioned child ignores its parent's padding — it
        /// offsets from the padding edge. A container with its own inset bounds works for flow
        /// and absolute children alike.
        /// </para>
        /// <para>
        /// ⓘ Their layout is not edited: the same elements are re-parented at runtime, and
        /// queries by name still find them, because <c>Q</c> searches descendants.
        /// </para>
        /// </remarks>
        public static VisualElement WrapInSafeArea(VisualElement root)
        {
            const string Name = "safe-area";

            VisualElement existing = root?.Q(Name);
            if (existing != null)
            {
                return existing;
            }

            var safe = new VisualElement { name = Name };
            safe.style.position = Position.Absolute;
            safe.pickingMode = PickingMode.Ignore;

            SafeInsets insets = SafeInsets.From(
                UnityEngine.Screen.safeArea, UnityEngine.Screen.width, UnityEngine.Screen.height);

            safe.style.left = Percent(insets.Left);
            safe.style.right = Percent(insets.Right);
            safe.style.top = Percent(insets.Top);
            safe.style.bottom = Percent(insets.Bottom);

            var children = new System.Collections.Generic.List<VisualElement>(root.Children());

            foreach (VisualElement child in children)
            {
                child.RemoveFromHierarchy();
                safe.Add(child);
            }

            root.Add(safe);
            return safe;
        }

        /// <summary>
        /// Insets an element by the device's safe area.
        /// </summary>
        /// <remarks>
        /// ⓘ In pixels converted to the panel's own scale. <see cref="UnityEngine.Screen.safeArea"/>
        /// is in screen pixels and a runtime panel may be scaled, so the reference resolution the
        /// panel was given is what makes the two comparable.
        /// </remarks>
        public static void ApplySafeArea(VisualElement element)
        {
            SafeInsets insets = SafeInsets.From(
                UnityEngine.Screen.safeArea, UnityEngine.Screen.width, UnityEngine.Screen.height);

            element.style.paddingLeft = Percent(insets.Left);
            element.style.paddingRight = Percent(insets.Right);
            element.style.paddingTop = Percent(insets.Top);
            element.style.paddingBottom = Percent(insets.Bottom);
        }

        private static StyleLength Percent(float fraction) =>
            new StyleLength(new Length(fraction * 100f, LengthUnit.Percent));

        /// <summary>
        /// How far each screen edge is from the safe area, as a fraction of the screen.
        /// </summary>
        /// <remarks>
        /// ⓘ The arithmetic on its own, so it can be tested with this phone's numbers — a status
        /// bar of 78 px over 2400 — without a device. <see cref="UnityEngine.Screen.safeArea"/>
        /// has its origin at the bottom-left, so a bar at the top shows up as
        /// <c>yMax &lt; height</c>. Fractions, never pixels: a runtime panel may be scaled, and
        /// a percentage of the panel is the same slice of the screen whatever the scale.
        /// </remarks>
        public readonly struct SafeInsets
        {
            public readonly float Left;
            public readonly float Right;
            public readonly float Top;
            public readonly float Bottom;

            private SafeInsets(float left, float right, float top, float bottom)
            {
                Left = left;
                Right = right;
                Top = top;
                Bottom = bottom;
            }

            public static SafeInsets None => new SafeInsets(0f, 0f, 0f, 0f);

            public static SafeInsets From(Rect safe, float width, float height)
            {
                if (width <= 0f || height <= 0f)
                {
                    return None;
                }

                return new SafeInsets(
                    Mathf.Max(0f, safe.xMin / width),
                    Mathf.Max(0f, (width - safe.xMax) / width),
                    Mathf.Max(0f, (height - safe.yMax) / height),
                    Mathf.Max(0f, safe.yMin / height));
            }
        }
    }
}
