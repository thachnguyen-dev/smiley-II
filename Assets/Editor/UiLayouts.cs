using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Smiley.Editor.Setup
{
    /// <summary>
    /// Writes §22–§26's screens as UXML and USS, once, for the owner to edit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Owner ruling, 2026-09-11 (D-057):</b> the UI and the image assets are theirs, set up
    /// directly in Unity. These files exist so there is something to open in UI Builder.
    /// </para>
    /// <para>
    /// ⛔ <b>CREATED ONCE, NEVER OVERWRITTEN, and that is the most important line in this
    /// file.</b> Every other asset this project generates is rebuilt on demand, because nobody
    /// edits them. These are the opposite: the moment the owner moves a button, regenerating the
    /// file would throw that away. <see cref="Ensure"/> writes only what is missing.
    /// </para>
    /// <para>
    /// ⚠ <b>The NAMES are load-bearing; everything else is not.</b> The controllers find their
    /// controls by name, so a renamed or deleted element silently disconnects a button. That is
    /// checked at build time by <see cref="UiLayoutGuard"/>, which fails rather than warns — so
    /// the owner can move, restyle, re-parent and re-order freely, and finds out immediately if
    /// they remove something the game needs.
    /// </para>
    /// </remarks>
    public static class UiLayouts
    {
        public const string Folder = "Assets/UI";

        public const string MenuUxml = Folder + "/MainMenu.uxml";
        public const string StoryUxml = Folder + "/StoryScreens.uxml";
        public const string Uss = Folder + "/Meta.uss";

        /// <summary>Writes any of the three files that is not already there.</summary>
        public static void Ensure()
        {
            Directory.CreateDirectory(Folder);

            EnsureFile(Uss, Style);
            EnsureFile(MenuUxml, MenuDocument);
            EnsureFile(StoryUxml, StoryDocument);

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// ⛔ <b>OVERWRITES the layouts with the current templates, DESTROYING any edits.</b>
        /// </summary>
        /// <remarks>
        /// <para>
        /// ⚠ <b>The escape hatch for the one case <see cref="Ensure"/> cannot handle:</b> a
        /// control being <b>added</b> to the game after the owner already has the file. The
        /// guard will refuse the build for a missing name, correctly — and there has to be a way
        /// to get the new element without hand-copying XML.
        /// </para>
        /// <para>
        /// ⛔ <b>Never called by a build.</b> It is run deliberately, by name, and it says what
        /// it destroyed. ⓘ Everything is in git, so the previous file is always recoverable —
        /// which is the only reason a command this blunt is acceptable at all.
        /// </para>
        /// <para>
        /// ⓘ Invoked as
        /// <c>-executeMethod Smiley.Editor.Setup.UiLayouts.RefreshFromCommandLine</c>, or from
        /// the menu below.
        /// </para>
        /// </remarks>
        [MenuItem("Smiley/UI/Refresh layouts (DESTROYS your edits)")]
        public static void Refresh()
        {
            Directory.CreateDirectory(Folder);

            Overwrite(Uss, Style);
            Overwrite(MenuUxml, MenuDocument);
            Overwrite(StoryUxml, StoryDocument);

            AssetDatabase.Refresh();
        }

        /// <summary>Headless entry point for <see cref="Refresh"/>.</summary>
        public static void RefreshFromCommandLine() => Refresh();

        private static void Overwrite(string path, string contents)
        {
            bool existed = File.Exists(path);

            File.WriteAllText(path, contents);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            Debug.LogWarning(existed
                ? $"[UI] OVERWROTE {path} with the current template. ⚠ Any edits to it are gone " +
                  "— git has the previous version."
                : $"[UI] Created {path}.");
        }

        private static void EnsureFile(string path, string contents)
        {
            if (File.Exists(path))
            {
                // ⛔ The owner's file. Never touched again.
                return;
            }

            File.WriteAllText(path, contents);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            Debug.Log($"[UI] Created {path}. ⓘ It is yours now — this never overwrites it.");
        }

        private const string Style = @"/* Smiley — meta screen styling, on the Hyper Casual UI Pack (D-114, owner 2026-09-13).
 *
 * ⭐ THIS FILE IS YOURS. Nothing regenerates it, and nothing in the game reads any of these
 * class names except this stylesheet itself — so rename, restyle and rearrange freely.
 *
 * ⛔ What you must NOT change are the element NAMES in the .uxml files (name=""..."").
 * The game finds its buttons by those, and a build will fail and tell you which one is missing.
 *
 * ⛔ NOTHING HERE IS STRETCHED. Every element that shows a pack sprite has a width and a height
 * in the SPRITE'S OWN RATIO, scaled uniformly; no sprite is 9-sliced and no art is tinted.
 * An Edit Mode test (UiSpriteTests) reads this file and fails the build if a rule's box does
 * not match its sprite's ratio, or if a url() points at nothing. If you resize a sprite-backed
 * element, keep the ratio:
 *
 *   pills (Buttons/empty_buttons)   130 x 50  ->  364 x 140      164 x 50 -> 459 x 140
 *                                   198 x 50  ->  554 x 140
 *   Main Menu pannel                466 x 375 ->  932 x 750
 *   Settings pannel                 741 x 663 ->  926 x 829
 *   Level popup                     446 x 477 ->  825 x 882
 *   CONNECTION ERROR popup          598 x 481 ->  897 x 722
 *   Icons (setting, Cross, 1 lvl)    square   ->  square
 *
 * The URLs are the guid form UI Builder writes, so renaming a sprite does not break them.
 */

/* The Menu scene paints its whole window in this colour too, above the status bar where the
 * owner-laid-out screens do not reach (MainMenuScreen adds .menu-root to its document root). */
.menu-root {
    background-color: rgb(96, 172, 224);
}

.screen {
    position: absolute;
    left: 0;
    right: 0;
    top: 0;
    bottom: 0;
    align-items: center;
    justify-content: center;
    background-color: rgb(96, 172, 224);
}

/* §23's Gameplay Menu sits OVER live gameplay, so it is translucent — Smiley keeps falling
 * behind it (D-008). Put after .screen so this background wins. */
.overlay {
    background-color: rgba(0, 0, 0, 0.55);
}

/* ---- Panels: one pack card each, at its own ratio. ---- */

.panel {
    flex-shrink: 0;
    align-items: center;
    justify-content: center;
    background-color: rgba(0, 0, 0, 0);
    border-width: 0;
    -unity-background-scale-mode: stretch-to-fill;
}

/* Main Menu pannel.png 466 x 375 */
.panel-menu {
    width: 932px;
    height: 750px;
    padding: 80px 60px 40px 60px;
    background-image: url(""project://database/Assets/Hyper_Casual_UI/Sprites/Panel_Sprites/Main%20Menu%20pannel.png?fileID=2800000&guid=9baf89304be800d45a58eaa36ccf44d9&type=3#Main Menu pannel"");
}

/* Settings pannel.png 741 x 663 */
.panel-settings {
    width: 926px;
    height: 829px;
    padding: 50px 60px 40px 60px;
    background-image: url(""project://database/Assets/Hyper_Casual_UI/Sprites/Panel_Sprites/Settings%20pannel.png?fileID=2800000&guid=b0ec607207eca7240babea657d4fe8e7&type=3#Settings pannel"");
}

/* Level popup.png 446 x 477 — the tall card: Completed, the Gameplay Menu, the consent screen. */
.panel-card {
    width: 825px;
    height: 882px;
    padding: 50px 50px 40px 50px;
    background-image: url(""project://database/Assets/Hyper_Casual_UI/Sprites/Panel_Sprites/Level%20popup.png?fileID=2800000&guid=058a1ff09e52a4241b04ff61a23f2f14&type=3#Level popup"");
}

/* CONNECTION ERROR popup.png 598 x 481 — the pink card, for the one destructive confirmation. */
.panel-danger {
    width: 897px;
    height: 722px;
    padding: 50px 60px 40px 60px;
    background-image: url(""project://database/Assets/Hyper_Casual_UI/Sprites/Panel_Sprites/CONNECTION%20ERROR%20popup.png?fileID=2800000&guid=6527f8156af9020418a827a16f5d6794&type=3#CONNECTION ERROR popup"");
}

/* ---- Text: the pack's Baloo 2, white with a dark edge on the teal cards. ---- */

.title {
    font-size: 96px;
    -unity-font-definition: url(""project://database/Assets/Hyper_Casual_UI/Fonts/Baloo2-ExtraBold.ttf?fileID=12800000&guid=18e5179df04e9a54899508068ed373ae&type=3#Baloo2-ExtraBold"");
    color: rgb(255, 255, 255);
    -unity-text-outline-width: 4px;
    -unity-text-outline-color: rgb(28, 78, 90);
    -unity-text-align: middle-center;
    margin-bottom: 20px;
}

.heading {
    font-size: 64px;
    -unity-font-definition: url(""project://database/Assets/Hyper_Casual_UI/Fonts/Baloo2-ExtraBold.ttf?fileID=12800000&guid=18e5179df04e9a54899508068ed373ae&type=3#Baloo2-ExtraBold"");
    color: rgb(255, 255, 255);
    -unity-text-outline-width: 3px;
    -unity-text-outline-color: rgb(28, 78, 90);
    -unity-text-align: middle-center;
    margin-bottom: 16px;
}

.body {
    font-size: 34px;
    -unity-font-definition: url(""project://database/Assets/Hyper_Casual_UI/Fonts/Baloo2-SemiBold.ttf?fileID=12800000&guid=88461247d1eb0a447b94214179096fdc&type=3#Baloo2-SemiBold"");
    color: rgb(255, 255, 255);
    -unity-text-align: middle-center;
    white-space: normal;
    margin-bottom: 16px;
}

.note {
    font-size: 30px;
    -unity-font-definition: url(""project://database/Assets/Hyper_Casual_UI/Fonts/Baloo2-SemiBold.ttf?fileID=12800000&guid=88461247d1eb0a447b94214179096fdc&type=3#Baloo2-SemiBold"");
    color: rgba(255, 255, 255, 0.85);
    -unity-text-align: middle-center;
    white-space: normal;
    margin-bottom: 16px;
}

/* Dark text on the pink card, as the pack draws its EXIT GAME popup. */
.panel-danger .heading,
.panel-danger .body,
.panel-danger .note {
    color: rgb(140, 52, 52);
    -unity-text-outline-width: 0;
}

/* ---- Buttons: the pack's empty pills with the game's own words on them. ---- */

/* The base: Buttons/empty_buttons/Green empty.png 130 x 50 -> 364 x 140. */
.action,
.next-button {
    width: 364px;
    height: 140px;
    font-size: 46px;
    -unity-font-definition: url(""project://database/Assets/Hyper_Casual_UI/Fonts/Baloo2-Bold.ttf?fileID=12800000&guid=4a923f926c182994f8deca8453d1bf64&type=3#Baloo2-Bold"");
    color: rgb(255, 255, 255);
    -unity-text-outline-width: 2px;
    -unity-text-outline-color: rgba(20, 60, 40, 0.9);
    -unity-text-align: middle-center;
    background-image: url(""project://database/Assets/Hyper_Casual_UI/Sprites/Buttons/empty_buttons/Green%20empty.png?fileID=2800000&guid=676e851f440fa7f488e4e0b1b5d24645&type=3#Green empty"");
    -unity-background-scale-mode: stretch-to-fill;
    background-color: rgba(0, 0, 0, 0);
    margin: 8px 0;
    padding: 0 0 6px 0;
    border-width: 0;
    border-radius: 0;
}

/* blue.png 130 x 50 */
.secondary {
    background-image: url(""project://database/Assets/Hyper_Casual_UI/Sprites/Buttons/empty_buttons/blue.png?fileID=2800000&guid=ea29607519568b644b2f6edf6692a370&type=3#blue"");
}

/* GREY.png 130 x 50 */
.neutral {
    background-image: url(""project://database/Assets/Hyper_Casual_UI/Sprites/Buttons/empty_buttons/GREY.png?fileID=2800000&guid=1ee98f5999cfa08469b53d5ae4a19431&type=3#GREY"");
}

/* PURPLE.png 164 x 50 -> 459 x 140 */
.purple {
    width: 459px;
    background-image: url(""project://database/Assets/Hyper_Casual_UI/Sprites/Buttons/empty_buttons/PURPLE.png?fileID=2800000&guid=f0b102eade37b3d469a3e1465d85d4e3&type=3#PURPLE"");
}

/* GOLDEN.png 152 x 72 -> 296 x 140. The one thing on this menu that costs money, so it is the
 * one pill that is not the menu's own blue-green family. */
.golden {
    width: 296px;
    background-image: url(""project://database/Assets/Hyper_Casual_UI/Sprites/Buttons/empty_buttons/GOLDEN.png?fileID=2800000&guid=7b76e5f85e927ee438f2348d785772c1&type=3#GOLDEN"");
}

/* light red.png 198 x 50 -> 554 x 140 */
.danger {
    width: 554px;
    background-image: url(""project://database/Assets/Hyper_Casual_UI/Sprites/Buttons/empty_buttons/light%20red.png?fileID=2800000&guid=ac53a09e048412e428c78cff13d8dc5e&type=3#light red"");
}

/* ⛔ The default runtime theme paints a background COLOUR on a pressed or hovered button, and
 * under a pill it shows in the transparent corners as a grey box. Every state stays clear;
 * a press is shown by darkening the art slightly, which is a tint, not a deformation. */
.unity-button.action:hover,
.unity-button.action:active,
.unity-button.action:focus,
.unity-button.action:hover:active,
.unity-button.action:hover:focus,
.unity-button.next-button:hover,
.unity-button.next-button:active,
.unity-button.next-button:focus,
.unity-button.next-button:hover:active,
.unity-button.close-button:hover,
.unity-button.close-button:active,
.unity-button.close-button:focus,
.unity-button.close-button:hover:active,
.unity-button.menu-button:hover,
.unity-button.menu-button:active,
.unity-button.menu-button:focus,
.unity-button.menu-button:hover:active {
    background-color: rgba(0, 0, 0, 0);
    border-width: 0;
}

.unity-button.action:active,
.unity-button.action:hover:active,
.unity-button.next-button:active,
.unity-button.next-button:hover:active,
.unity-button.close-button:active,
.unity-button.close-button:hover:active,
.unity-button.menu-button:active,
.unity-button.menu-button:hover:active {
    -unity-background-image-tint-color: rgb(200, 200, 200);
}

/* ---- Settings: volume rows. ---- */

.volume {
    width: 760px;
    margin: 8px 0;
}

.volume .body {
    margin-bottom: 0;
    -unity-text-align: middle-left;
}

.volume-slider {
    height: 84px;
    margin: 0;
}

/* The track is drawn, not a sprite: a flat bar is not a picture the pack has to supply. */
.volume-slider .unity-base-slider__tracker {
    height: 22px;
    margin-top: -11px;
    background-color: rgb(24, 88, 100);
    border-width: 0;
    border-radius: 11px;
}

/* The knob is the pack's 1 lvl.png 50 x 50 -> 72 x 72. */
.volume-slider .unity-base-slider__dragger {
    width: 72px;
    height: 72px;
    margin-top: -36px;
    background-image: url(""project://database/Assets/Hyper_Casual_UI/Sprites/Panel_Sprites/1%20lvl.png?fileID=2800000&guid=de9cfbb42b640d14c9033d1469f22a60&type=3#1 lvl"");
    -unity-background-scale-mode: stretch-to-fill;
    background-color: rgba(0, 0, 0, 0);
    border-width: 0;
    border-radius: 0;
}

.volume-slider .unity-base-slider__dragger:hover,
.volume-slider .unity-base-slider__dragger:active,
.volume-slider .unity-base-slider__dragger-border {
    background-color: rgba(0, 0, 0, 0);
    border-width: 0;
}

/* ---- Pictures. ---- */

/* §30's tutorial image. Sized to fit rather than fill, so nothing is cropped. */
.tutorial-image {
    width: 90%;
    height: 70%;
    background-color: rgba(0, 0, 0, 0);
    -unity-background-scale-mode: scale-to-fit;
}

/* The X that closes the tutorial: Icons/Cross.png 54 x 54 -> 150 x 150. Big enough for a thumb. */
.close-button {
    position: absolute;
    top: 24px;
    right: 24px;
    width: 150px;
    height: 150px;
    background-image: url(""project://database/Assets/Hyper_Casual_UI/Sprites/Icons/Cross.png?fileID=2800000&guid=948dcadfe4457a943bda3d27bcb58ba0&type=3#Cross"");
    -unity-background-scale-mode: stretch-to-fill;
    background-color: rgba(0, 0, 0, 0);
    border-width: 0;
    border-radius: 0;
    padding: 0;
    margin: 0;
}

/* ⭐ D-137: §20's story is TEXT now, on black, narrated in a panel at the bottom.
 * ⛔ The whole screen is black on purpose -- the owner asked for it, and it keeps every eye on
 * the words rather than on a letterboxed picture. */
.story-screen {
    background-color: rgb(0, 0, 0);
}

/* ⭐ The narration panel the owner asked for. A VISIBLE surface, not bare text on black --
 * the first attempt had no panel at all, which is not restraint, it is an unfinished ask.
 * ⛔ Deliberately neutral: a faint light wash with a soft border, not a new piece of art. §32
 * keeps the game's look the owner's, and the pack's teal menu panels would fight a black
 * storytelling screen.
 * ⚠ Anchored above the Next button with room to breathe, and its height follows the text, so
 * the longest line -- 180 characters, line 10 of the opening -- grows the panel upward instead
 * of being clipped. */
.story-panel {
    position: absolute;
    left: 48px;
    right: 48px;
    bottom: 300px;
    min-height: 300px;
    padding: 48px 44px;
    align-items: center;
    justify-content: center;
    background-color: rgba(255, 255, 255, 0.07);
    border-width: 2px;
    border-color: rgba(255, 255, 255, 0.22);
    border-radius: 28px;
}

.story-text {
    font-size: 42px;
    -unity-font-definition: url(""project://database/Assets/Hyper_Casual_UI/Fonts/Baloo2-SemiBold.ttf?fileID=12800000&guid=88461247d1eb0a447b94214179096fdc&type=3#Baloo2-SemiBold"");
    color: rgb(255, 255, 255);
    -unity-text-align: middle-center;
    white-space: normal;
}

/* The Next button that steps through a story. Bottom right, big enough for a thumb; the same
 * green pill as .action, sized above. */
.next-button {
    position: absolute;
    bottom: 32px;
    right: 24px;
    margin: 0;
}

/* §23's corner button that opens the Gameplay Menu. D-007: top-right, out of the play space and
 * away from the Android gesture edges. ⭐ Owner, 2026-09-11: three times its first size (D-078).
 * Icons/setting.png 54 x 54 -> 168 x 168 — a gear, deliberately not the pack's pause icon:
 * this menu does not pause (D-008). */
.menu-button {
    position: absolute;
    top: 10px;
    right: 10px;
    width: 168px;
    height: 168px;
    min-width: 168px;
    background-image: url(""project://database/Assets/Hyper_Casual_UI/Sprites/Icons/setting.png?fileID=2800000&guid=212e8bb640af23441b0d4d39f631f88e&type=3#setting"");
    -unity-background-scale-mode: stretch-to-fill;
    background-color: rgba(0, 0, 0, 0);
    border-width: 0;
    border-radius: 0;
    padding: 0;
    margin: 0;
}

/* D-107's parental consent answer box (dormant while the app is not child-directed, D-110). */
.consent-answer {
    width: 300px;
    height: 90px;
    font-size: 44px;
    -unity-font-definition: url(""project://database/Assets/Hyper_Casual_UI/Fonts/Baloo2-Bold.ttf?fileID=12800000&guid=4a923f926c182994f8deca8453d1bf64&type=3#Baloo2-Bold"");
    margin: 8px 0;
}
";

        private const string MenuDocument = @"<?xml version=""1.0"" encoding=""utf-8""?>
<!--
    Smiley — §22 Main Menu, §26 Settings, §25 Reset Game.

    ⭐ THIS FILE IS YOURS. Open it with UI Builder (double-click it in the Project window) and
    lay it out however you like. Nothing regenerates it.

    ⛔ KEEP THE name="""" ATTRIBUTES. The game finds every control by name, and a build fails with
    a message naming anything it cannot find. Everything else — position, order, styling, extra
    decoration, images — is free.
-->
<ui:UXML xmlns:ui=""UnityEngine.UIElements"">
    <Style src=""project://database/Assets/UI/Meta.uss"" />

    <ui:VisualElement name=""main-menu"" class=""screen"">
        <ui:VisualElement class=""panel panel-menu"">
            <ui:Label text=""Smiley II"" class=""title"" />
            <ui:Button name=""primary-action"" text=""New Game"" class=""action"" />
            <ui:Button name=""remove-ads"" text=""Remove Ads"" class=""action golden"" />
            <ui:Button name=""settings"" text=""Settings"" class=""action purple"" />
            <ui:Button name=""exit"" text=""Exit"" class=""action neutral"" />
        </ui:VisualElement>
    </ui:VisualElement>

    <ui:VisualElement name=""settings-screen"" class=""screen"">
        <ui:VisualElement class=""panel panel-settings"">
            <ui:Label text=""Settings"" class=""heading"" />

            <ui:VisualElement name=""music-volume-row"" class=""volume"">
                <ui:Label name=""music-volume-label"" text=""Music"" class=""body"" />
                <ui:Slider name=""music-volume"" low-value=""0"" high-value=""1"" value=""0.7""
                           class=""volume-slider"" />
            </ui:VisualElement>

            <ui:VisualElement name=""sfx-volume-row"" class=""volume"">
                <ui:Label name=""sfx-volume-label"" text=""Sound"" class=""body"" />
                <ui:Slider name=""sfx-volume"" low-value=""0"" high-value=""1"" value=""0.9""
                           class=""volume-slider"" />
            </ui:VisualElement>

            <ui:Button name=""consent-review"" text=""Parental consent"" class=""action secondary"" />
            <ui:Button name=""reset-game"" text=""Reset Game"" class=""action danger"" />
            <ui:Button name=""back"" text=""Back"" class=""action secondary"" />
        </ui:VisualElement>
    </ui:VisualElement>

    <ui:VisualElement name=""confirm-screen"" class=""screen"">
        <ui:VisualElement class=""panel panel-danger"">
            <ui:Label text=""Reset Game"" class=""heading"" />
            <ui:Label text=""All progress will be lost. The climb starts again from nothing, and the tutorial returns. This cannot be undone.""
                      class=""body"" />
            <ui:Label text=""Your settings are kept."" class=""note"" />
            <ui:Button name=""reset-confirm"" text=""Delete my progress"" class=""action danger"" />
            <ui:Button name=""reset-cancel"" text=""Cancel"" class=""action neutral"" />
        </ui:VisualElement>
    </ui:VisualElement>

    <ui:VisualElement name=""consent-screen"" class=""screen"">
        <ui:VisualElement class=""panel panel-card"">
            <ui:Label text=""For parents"" class=""heading"" />
            <ui:Label text=""Smiley can show a small banner advert on this menu. Adverts are not personalised. A parent or guardian must allow this. The game plays the same either way."" class=""body"" />
            <ui:Label name=""consent-question"" text=""Parents: what is 7 × 8?"" class=""body"" />
            <ui:TextField name=""consent-answer"" class=""consent-answer"" />
            <ui:Button name=""consent-grant"" text=""Allow adverts"" class=""action"" />
            <ui:Button name=""consent-refuse"" text=""No adverts"" class=""action neutral"" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
";

        private const string StoryDocument = @"<?xml version=""1.0"" encoding=""utf-8""?>
<!--
    Smiley — everything drawn in the Game scene: §20's Story, §30's Tutorial, the Completed
    screen, and §23's Gameplay Menu with its corner button.

    ⭐ THIS FILE IS YOURS. Nothing regenerates it.

    ⚠ ""gameplay-menu"" sits OVER live gameplay and does not pause anything (§23, D-008). Its two
    buttons are ""resume"" and ""main-menu"". ⛔ Do not add a Restart button (§24) — the game would
    ignore it, and a test fails on the word.

    ⛔ KEEP THE name="""" ATTRIBUTES, and keep ""story-text"": each of the story's lines is
    drawn into that element's background, so removing it leaves the Story blank.

    ⭐ The story is IMAGES stepped with the ""story-next"" button — seven for the opening, two for
    the ending. Put the button wherever you like; the game only needs its name.

    ⓘ The Story CANNOT BE SKIPPED (§20). Next moves exactly one image, and there is no button that
    reaches the end from the middle. Do not add one — the game would ignore it.

    ⭐ The TUTORIAL is different: §30's image is closed with the ""tutorial-close"" X button, and
    that is the moment play begins. Put your instruction image in ""tutorial-image"" — as a
    background image on that element, or replace it with a ui:Image, whichever you prefer.
-->
<ui:UXML xmlns:ui=""UnityEngine.UIElements"">
    <Style src=""project://database/Assets/UI/Meta.uss"" />

    <ui:VisualElement name=""story-screen"" class=""screen story-screen"">
        <ui:VisualElement name=""story-panel"" class=""story-panel"">
            <ui:Label name=""story-text"" text="""" class=""story-text"" />
        </ui:VisualElement>
        <ui:Button name=""story-next"" text=""Next"" class=""next-button"" />
    </ui:VisualElement>

    <ui:Label name=""tutorial-prompt"" text="""" class=""tutorial-prompt"" />
    <ui:VisualElement name=""tutorial-screen"" class=""screen"">
        <ui:VisualElement name=""tutorial-image"" class=""tutorial-image"" />
        <ui:Button name=""tutorial-close"" text="""" class=""close-button"" />
    </ui:VisualElement>

    <ui:VisualElement name=""completed-screen"" class=""screen"">
        <ui:VisualElement class=""panel panel-card"">
            <ui:Label text=""Completed"" class=""heading"" />
            <ui:Label text=""The climb starts again from the Start Area whenever you are ready.""
                      class=""note"" />
            <ui:Button name=""acknowledge"" text=""Main Menu"" class=""action secondary"" />
        </ui:VisualElement>
    </ui:VisualElement>

    <ui:Button name=""gameplay-menu-button"" text="""" class=""menu-button"" />
    <ui:VisualElement name=""gameplay-menu"" class=""screen overlay"">
        <ui:VisualElement class=""panel panel-card"">
            <ui:Label text=""Paused? No."" class=""note"" />
            <ui:Label text=""Smiley is still falling."" class=""body"" />
            <ui:Button name=""resume"" text=""Resume"" class=""action"" />
            <ui:Button name=""main-menu"" text=""Main Menu"" class=""action secondary"" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
";
    }
}
