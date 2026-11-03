using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Smiley.Editor.Setup
{
    /// <summary>
    /// Assigns the application icon (D-136).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ Owner, 2026-10-10: the icon is <c>Art/Story/bg_8</c>, cropped so Smiley fills the frame.
    /// ⛔ The art is theirs under §32; the only thing chosen here is the crop, and that was shown
    /// at home-screen size before it was used.
    /// </para>
    /// <para>
    /// ⚠ <b>Before this, iOS had no icon at all</b> and every platform fell back to a legacy
    /// default pointing at <c>Art/animation/ani5/5.4.png</c> — an animation frame, almost
    /// certainly assigned by accident. A build with no icon is rejected by App Store review.
    /// </para>
    /// <para>
    /// ⓘ <b>Why a script rather than the Inspector.</b> Unity stores an icon per size slot, and
    /// there are a dozen of them on iOS. Setting them by hand is a dozen chances to miss one, and
    /// a missed slot is invisible until a device shows a blank square.
    /// </para>
    /// </remarks>
    public static class IconAssets
    {
        public const string IconPath = "Assets/Art/Icon/AppIcon.png";

        [MenuItem("Smiley/Assign App Icons")]
        public static void Assign() => Apply(alsoAndroid: true);

        /// <summary>Assigns the icon to every iOS size slot.</summary>
        /// <param name="alsoAndroid">
        /// ⭐ True since 2026-10-12. The owner asked for "the app icon" without naming a
        /// platform; I set iOS only and left Android for a separate decision, which was my call
        /// to make and the wrong one. Android had been shipping an ANIMATION FRAME as its
        /// launcher icon the whole time.
        /// </param>
        public static void Apply(bool alsoAndroid)
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);

            if (icon == null)
            {
                Debug.LogError($"[ICON] No texture at {IconPath}.");
                EditorApplication.Exit(1);
                return;
            }

            Fill(NamedBuildTarget.iOS, icon);

            if (alsoAndroid)
            {
                Fill(NamedBuildTarget.Android, icon);
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>Puts the one texture in every slot the platform asks for.</summary>
        private static void Fill(NamedBuildTarget target, Texture2D icon)
        {
            foreach (PlatformIconKind kind in PlayerSettings.GetSupportedIconKinds(target))
            {
                PlatformIcon[] slots = PlayerSettings.GetPlatformIcons(target, kind);

                if (slots == null || slots.Length == 0)
                {
                    continue;
                }

                foreach (PlatformIcon slot in slots)
                {
                    // ⚠ EVERY layer, not just the first. Android Adaptive icons are built from a
                    // background and a foreground, and filling only layer 0 leaves the launcher
                    // drawing half an icon. ⓘ The same picture on both reads correctly: the
                    // system masks and parallaxes them together, and a single flat illustration
                    // is what it expects when there is no separate foreground art.
                    for (int layer = 0; layer < Mathf.Max(1, slot.maxLayerCount); layer++)
                    {
                        slot.SetTexture(icon, layer);
                    }
                }

                PlayerSettings.SetPlatformIcons(target, kind, slots);
                Debug.Log($"[ICON] {target.TargetName} {kind}: {slots.Length} slot(s) filled.");
            }
        }
    }
}
