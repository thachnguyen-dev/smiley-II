using System.IO;
using Smiley.Audio;
using Smiley.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Smiley.Editor.Setup
{
    /// <summary>
    /// Generates §22's Main Menu scene.
    /// </summary>
    /// <remarks>
    /// ⛔ Built by command, never by hand, for the reason D-018 recorded: a scene assembled in
    /// the editor is a file nobody can verify, and this project has already shipped one whose
    /// references all serialized as null while every report about it read correctly.
    /// </remarks>
    public static class MenuSceneBuilder
    {
        /// <summary>Where the Main Menu scene lives.</summary>
        public const string ScenePath = "Assets/Scenes/Menu.unity";

        /// <summary>Builds the Main Menu scene from nothing.</summary>
        public static void Build()
        {
            AudioAssets.Ensure();
            AdAssets.Ensure();
            UiLayouts.Ensure();

            PanelSettings panel = UiAssets.Ensure();

            if (panel == null)
            {
                EditorApplication.Exit(1);
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ⚠ Loaded only now that the scene which will reference it is open — the same
            // ordering D-018 forced on the game scene, for the same reason.
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(UiAssets.PanelPath);

            if (settings == null)
            {
                Debug.LogError("[MENU] The panel settings asset failed to load; aborting.");
                EditorApplication.Exit(1);
                return;
            }

            // A camera, purely so the scene is not black behind a panel that does not fill it
            // and so Unity does not warn about a scene with no camera.
            var cameraObject = new GameObject("Main Camera");
            var camera = cameraObject.AddComponent<Camera>();

            // D-097: the ear. See SceneBuilder.CreateCamera.
            cameraObject.AddComponent<AudioListener>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.05f, 0.08f);
            cameraObject.tag = "MainCamera";

            // ⛔ §26's sliders must control something REAL, even in the menu — the sibling
            // project shipped volume controls over a game with no audio system and recorded it
            // as a defect (its D-835). The menu gets its own GameAudio so a slider moved here
            // is audibly a slider, not a number written to a file.
            var audioObject = new GameObject("Audio");
            var audio = audioObject.AddComponent<GameAudio>();

            var uiObject = new GameObject("MainMenu");
            var document = uiObject.AddComponent<UIDocument>();
            document.panelSettings = settings;

            // ⭐ The owner's layout (D-057).
            document.visualTreeAsset =
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiLayouts.MenuUxml);

            var menu = uiObject.AddComponent<MainMenuScreen>();

            var tuning = AssetDatabase.LoadAssetAtPath<Smiley.Config.SmileyTuning>(
                "Assets/Config/SmileyTuning.asset");

            SceneBuilder.SetPrivateField(audio, "_tuning", tuning);
            SceneBuilder.SetPrivateField(audio, "_music",
                AssetDatabase.LoadAssetAtPath<AudioClip>(AudioAssets.MusicPath));
            SceneBuilder.SetPrivateField(menu, "_audio", audio);

            var adSettings = AssetDatabase.LoadAssetAtPath<Smiley.Ads.AdSettings>(AdAssets.Path);

            // ⭐ D-125: the SDK lifetime and the interstitial live on one object that survives
            // both scenes, because §23 gear menu is in the GAME scene (D-124) and the banner
            // may not be (D-004). Created here because the Menu is scene 0 (D-042).
            var serviceObject = new GameObject("AdService");
            SceneBuilder.SetPrivateField(serviceObject.AddComponent<Smiley.Ads.AdService>(), "_settings", adSettings);

            // ⭐ D-124: the Remove Ads purchase, also persistent — the store is asked once per
            // launch and the answer must outlive the menu.
            var storeObject = new GameObject("Store");
            SceneBuilder.SetPrivateField(storeObject.AddComponent<Smiley.Ads.StorePurchases>(), "_settings", adSettings);

            // ⭐ D-003/D-004: the banner, in the Menu scene and nowhere else (D-105).
            var bannerObject = new GameObject("Banner");
            var banner = bannerObject.AddComponent<Smiley.Ads.MenuBanner>();
            SceneBuilder.SetPrivateField(banner, "_settings", adSettings);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath) ?? "Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ⛔ Verified by reading the saved scene back, not by trusting the assignment. A
            // UIDocument whose panelSettings serialized as null draws nothing at all, and looks
            // exactly like a menu that was never opened.
            bool wired = document.panelSettings != null && document.visualTreeAsset != null;

            Debug.Log($"[MENU] Built {ScenePath} with {scene.rootCount} root objects. " +
                      $"panelSettings wired = {wired}");

            if (!wired)
            {
                Debug.LogError("[MENU] The UIDocument has no panel settings or no layout; the " +
                               "menu would be invisible. Fix the builder rather than the scene.");
                EditorApplication.Exit(1);
            }
        }
    }
}
