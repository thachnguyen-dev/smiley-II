using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Smiley.Editor.Setup
{
    /// <summary>
    /// One-shot project configuration, runnable headlessly so the settings are
    /// reproducible rather than hand-clicked.
    /// <para>
    /// Invoke with:
    /// <c>Unity.exe -batchmode -quit -nographics -projectPath src
    /// -executeMethod Smiley.Editor.Setup.ProjectSetup.Configure</c>
    /// </para>
    /// <para>
    /// ⛔ <b>Every value here traces to a canonical document or a numbered decision, and
    /// nothing is set on inference.</b> Settings the "Universal 2D" template already gets
    /// right are deliberately absent — re-asserting them here would make this file look
    /// like the authority for values it merely copies. In particular the render pipeline,
    /// the 2D Renderer and <c>activeInputHandler: 1</c> (Input System only) come from the
    /// template and are only verified below, never written.
    /// </para>
    /// <para>
    /// This is an editor-only tool and is never included in a player build.
    /// </para>
    /// </summary>
    public static class ProjectSetup
    {
        // Identity. ⚠ The company name and bundle id follow the owner's existing
        // convention (their other project uses "Thach Nguyen" / com.thachnguyen.*), and
        // the git author on this machine is "Thach Nguyen". They are development values:
        // the real store identity depends on Q-024, the publisher account, which only the
        // owner can create. Changing the bundle id later is cheap now and expensive after
        // a store listing exists.
        private const string CompanyName = "Thach Nguyen";
        private const string ProductName = "Smiley II"; // ⭐ owner, 2026-09-13 (D-116): the store name
        private const string BundleId = "com.thachnguyen.smiley";

        // ProjectSettings.asset -> activeInputHandler: 0 = legacy, 1 = new, 2 = both.
        private const int InputHandlerNewOnly = 1;

        private const string PipelineAssetPath = "Assets/Settings/UniversalRP.asset";

        public static void Configure()
        {
            var failures = 0;

            failures += Run("identity", ConfigureIdentity);
            failures += Run("orientation", ConfigureOrientation);
            failures += Run("scripting backend", ConfigureScriptingBackend);
            failures += Run("default render pipeline", ConfigureDefaultRenderPipeline);
            failures += Run("active build target", ConfigureActiveBuildTarget);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Verify();

            if (failures > 0)
            {
                Debug.LogError($"[SETUP] ProjectSetup finished with {failures} failed step(s).");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log("[SETUP] ProjectSetup completed successfully.");
        }

        /// <summary>
        /// Prints what is actually in the settings after configuration, so the log is
        /// evidence rather than a claim. Anything read here that this file does not write
        /// is a template default being checked, not asserted.
        /// </summary>
        public static void Verify()
        {
            Debug.Log($"[SETUP-V] companyName          = {PlayerSettings.companyName}");
            Debug.Log($"[SETUP-V] productName          = {PlayerSettings.productName}");
            Debug.Log($"[SETUP-V] androidAppId         = {PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android)}");
            Debug.Log($"[SETUP-V] defaultOrientation   = {PlayerSettings.defaultInterfaceOrientation}");
            Debug.Log($"[SETUP-V] autorotatePortrait   = {PlayerSettings.allowedAutorotateToPortrait}");
            Debug.Log($"[SETUP-V] autorotateUpsideDown = {PlayerSettings.allowedAutorotateToPortraitUpsideDown}");
            Debug.Log($"[SETUP-V] autorotateLandscapeL = {PlayerSettings.allowedAutorotateToLandscapeLeft}");
            Debug.Log($"[SETUP-V] autorotateLandscapeR = {PlayerSettings.allowedAutorotateToLandscapeRight}");
            Debug.Log($"[SETUP-V] backend(Android)     = {PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android)}");
            Debug.Log($"[SETUP-V] androidArchitectures = {PlayerSettings.Android.targetArchitectures}");
            Debug.Log($"[SETUP-V] colorSpace           = {PlayerSettings.colorSpace}   (template default, not set here)");
            Debug.Log($"[SETUP-V] pipeline (EFFECTIVE) = {Describe(GraphicsSettings.currentRenderPipeline)}");
            Debug.Log($"[SETUP-V]   graphics default   = {Describe(GraphicsSettings.defaultRenderPipeline)}");
            Debug.Log($"[SETUP-V]   active quality lvl = {Describe(QualitySettings.renderPipeline)}   (template assigns this per quality level)");
            Debug.Log($"[SETUP-V] activeInputHandler   = {ReadActiveInputHandler()} (expect {InputHandlerNewOnly} = Input System only; template default)");
            Debug.Log($"[SETUP-V] activeBuildTarget    = {EditorUserBuildSettings.activeBuildTarget}");
            Debug.Log($"[SETUP-V] scene loads          = {DescribeStartScene()}");
            Debug.Log($"[SETUP-V] physics2D gravity    = {UnityEngine.Physics2D.gravity}   ⚠ Q-001 is UNDECIDED — this is the engine default, not an authored value");
        }

        private static int Run(string label, Action step)
        {
            try
            {
                step();
                Debug.Log($"[SETUP] OK   - {label}");
                return 0;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SETUP] FAIL - {label}: {e.GetType().Name}: {e.Message}");
                return 1;
            }
        }

        /// <summary>Product identity and the Android application id.</summary>
        private static void ConfigureIdentity()
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
        }

        /// <summary>
        /// Portrait lock. §1 and §33: the target platform is portrait Android, and
        /// gameplay must not change with the display. The template ships
        /// <c>defaultScreenOrientation: 4</c> (auto-rotation) with every orientation
        /// allowed, so this is a real change and not a re-assertion.
        /// <para>
        /// Upside-down is disabled deliberately: §4 puts the Start Area at the bottom of
        /// the map and §6's drag lives under the player's thumb, so a 180° flip would put
        /// the play space under the hand.
        /// </para>
        /// </summary>
        private static void ConfigureOrientation()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.useAnimatedAutorotation = false;
        }

        /// <summary>
        /// IL2CPP with ARM64 for device builds, per <c>technical-preferences.md</c>. Mono
        /// stays the editor default. These persist whether or not the Android module is
        /// installed on the current machine.
        /// </summary>
        private static void ConfigureScriptingBackend()
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        }

        /// <summary>
        /// Fills the <b>Default Render Pipeline</b> slot in Graphics settings with the
        /// template's own <c>UniversalRP.asset</c>.
        /// <para>
        /// ⚠ <b>The project already rendered through URP without this.</b> The Universal 2D
        /// template assigns the pipeline on each of its six quality levels
        /// (<c>customRenderPipeline</c>) and leaves the graphics-wide default empty, so URP
        /// is active but only <i>because a quality level says so</i>. A quality level added
        /// later would default to null and silently fall back to the Built-in pipeline,
        /// taking 2D lights with it. Assigning the default makes the stack explicit
        /// instead of incidental; the per-quality assignments are left untouched.
        /// </para>
        /// </summary>
        private static void ConfigureDefaultRenderPipeline()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
            if (pipeline == null)
            {
                throw new InvalidOperationException(
                    $"The template's pipeline asset is missing at {PipelineAssetPath}.");
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;

            if (GraphicsSettings.currentRenderPipeline == null)
            {
                throw new InvalidOperationException(
                    "No render pipeline is effective after assignment — the project would render Built-in.");
            }
        }

        /// <summary>
        /// Switches the editor's active platform to Android, so texture import, shader
        /// stripping and a plain <c>Build</c> all target the real device rather than the
        /// desktop this repository happens to sit on.
        /// </summary>
        private static void ConfigureActiveBuildTarget()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
            {
                return;
            }

            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(NamedBuildTarget.Android, BuildTarget.Android))
            {
                throw new InvalidOperationException(
                    "Could not switch the active build target to Android — is the Android module installed?");
            }
        }

        /// <summary>
        /// Opens the template's scene and reports what is in it. ⓘ <b>Compiling is not
        /// loading</b>: a project can build its scripts cleanly and still have a scene that
        /// fails to open, so this reads the scene the editor actually opens.
        /// <para>
        /// ⚠ <c>SampleScene</c> is the <b>template's</b> scene and is scaffolding, not this
        /// game's scene. It is deliberately left un-renamed and un-populated — the real
        /// scene waits on Q-014…Q-019, which decide the shape of what goes in it.
        /// </para>
        /// </summary>
        private static string DescribeStartScene()
        {
            const string scenePath = "Assets/Scenes/SampleScene.unity";
            try
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                    scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
                return scene.IsValid()
                    ? $"OK - {scenePath}, {scene.rootCount} root object(s)"
                    : $"INVALID - {scenePath}";
            }
            catch (Exception e)
            {
                return $"FAILED - {scenePath}: {e.GetType().Name}: {e.Message}";
            }
        }

        private static string ReadActiveInputHandler()
        {
            UnityEngine.Object[] settings =
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");

            if (settings == null || settings.Length == 0)
            {
                return "<unreadable>";
            }

            var so = new SerializedObject(settings[0]);
            SerializedProperty handler = so.FindProperty("activeInputHandler");
            return handler == null ? "<absent>" : handler.intValue.ToString();
        }

        private static string Describe(RenderPipelineAsset asset)
        {
            return asset == null ? "<none - Built-in>" : $"{asset.name} ({asset.GetType().Name})";
        }
    }
}
