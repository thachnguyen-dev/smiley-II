using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Smiley.Editor.Setup
{
    /// <summary>
    /// Creates the one <see cref="PanelSettings"/> asset every screen draws through.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A runtime UI Toolkit panel needs settings and a theme, and both are assets. They are
    /// generated here for the same reason the scenes are: so that a build produces them rather
    /// than depending on someone having clicked the right menu item once.
    /// </para>
    /// <para>
    /// ⛔ <b>THE THEME IS FOUND, NOT RECALLED.</b> The default runtime theme lives inside a
    /// package, and its path has moved between Unity versions — <c>VERSION.md</c>'s knowledge-gap
    /// warning covers exactly this. A hardcoded path that has drifted resolves to null, and a
    /// <see cref="PanelSettings"/> with no theme draws unstyled or not at all, at runtime, on the
    /// device. So the known paths are tried and then the project is searched, and a build that
    /// finds nothing <b>fails</b> rather than shipping a panel that cannot draw.
    /// </para>
    /// </remarks>
    public static class UiAssets
    {
        /// <summary>Where the generated settings asset lives.</summary>
        public const string PanelPath = "Assets/UI/SmileyPanelSettings.asset";

        /// <summary>
        /// Portrait reference height. Matches the phone the layout was sized against, so a
        /// button that fits on one device fits on all of them (§33).
        /// </summary>
        private static readonly Vector2Int Reference = new Vector2Int(1080, 2400);

        /// <summary>Where the project's own copy of the runtime theme lives.</summary>
        private const string ThemePath = "Assets/UI/UnityDefaultRuntimeTheme.tss";

        /// <summary>
        /// The entire contents of a default runtime theme.
        /// </summary>
        /// <remarks>
        /// ⓘ <b>One import of a built-in URL, and that really is the whole file.</b> Taken from a
        /// working Unity project on this machine rather than from recall — the same discipline
        /// the package versions were fixed with at founding, and the reason the theme is
        /// GENERATED here instead of being hunted for inside a package whose path moves between
        /// versions.
        /// </remarks>
        private const string ThemeSource = "@import url(\"unity-theme://default\");";

        private static readonly string[] KnownThemePaths =
        {
            ThemePath,
            "Packages/com.unity.ui/PackageResources/StyleSheets/Generated/DefaultRuntimeTheme.tss",
            "Packages/com.unity.modules.uielements/PackageResources/StyleSheets/Generated/DefaultRuntimeTheme.tss",
        };

        /// <summary>Creates or updates the panel settings. Returns null when it cannot.</summary>
        public static PanelSettings Ensure()
        {
            Directory.CreateDirectory("Assets/UI");

            EnsureTheme();

            ThemeStyleSheet theme = FindTheme();

            if (theme == null)
            {
                Debug.LogError(
                    "[UI] No runtime ThemeStyleSheet could be found, by path or by search. A " +
                    "PanelSettings without one cannot draw at runtime, so the build stops here " +
                    "rather than shipping screens that are invisible on the device.");
                return null;
            }

            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);

            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(settings, PanelPath);
            }

            settings.themeStyleSheet = theme;
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = Reference;

            // ⚠ Match on HEIGHT. Portrait phones vary far more in aspect than in height, and
            // matching width would shrink every control on a narrow device — §33 requires that
            // the display does not change the game, and an unreachable button would.
            settings.match = 1f;

            EditorUtility.SetDirty(settings);
            return settings;
        }

        /// <summary>Writes the project's own theme file if it is not there already.</summary>
        private static void EnsureTheme()
        {
            if (File.Exists(ThemePath))
            {
                return;
            }

            File.WriteAllText(ThemePath, ThemeSource);
            AssetDatabase.ImportAsset(ThemePath, ImportAssetOptions.ForceUpdate);

            Debug.Log($"[UI] Created {ThemePath}.");
        }

        private static ThemeStyleSheet FindTheme()
        {
            foreach (string path in KnownThemePaths)
            {
                var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(path);

                if (theme != null)
                {
                    Debug.Log($"[UI] Runtime theme: {path}");
                    return theme;
                }
            }

            foreach (string guid in AssetDatabase.FindAssets("t:ThemeStyleSheet"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(path);

                if (theme != null)
                {
                    Debug.Log($"[UI] Runtime theme found by search: {path}");
                    return theme;
                }
            }

            return null;
        }
    }
}
