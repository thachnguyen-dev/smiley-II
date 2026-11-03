using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Smiley.Editor.Setup
{
    /// <summary>
    /// Builds the Android player headlessly — the development APK for the phone, or the release
    /// bundle for the store.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Unity.exe -batchmode -quit -nographics -projectPath src -executeMethod
    /// Smiley.Editor.Setup.AndroidBuildCommand.Build</c> — the <b>development APK</b>: debug-allowed,
    /// debug-signed unless <c>SMILEY_KEYSTORE_PASS</c> is set. It exists so the owner can feel the
    /// game on hardware; it is not a release artefact.
    /// </para>
    /// <para>
    /// <c>… -executeMethod Smiley.Editor.Setup.AndroidBuildCommand.BuildBundle</c> — ⭐ the
    /// <b>release AAB</b> (D-119): Development off, no debugging, signed with the owner's release
    /// keystore or refused, the version code advanced and persisted, native symbols zipped
    /// beside it. Both paths run the same guards first; the only differences are the flags.
    /// </para>
    /// </remarks>
    public static class AndroidBuildCommand
    {
        private const string ApkPath = "../build/smiley.apk";
        private const string BundlePath = "../build/smiley.aab";

        /// <summary>The development APK for the phone.</summary>
        public static void Build() => Run(release: false);

        /// <summary>The release App Bundle for Google Play (D-119).</summary>
        public static void BuildBundle() => Run(release: true);

        /// <summary>
        /// Whether to build an APK without Unity's development watermark, for capturing media.
        /// </summary>
        /// <remarks>
        /// ⛔ <b>Not a release.</b> It is still debug-signed and Play will reject it. It exists
        /// because a store cover or a trailer with "Development Build" stamped across the corner
        /// is worthless, and the alternative was waiting for a release to reach the store before
        /// a single screenshot could be taken.
        /// </remarks>
        private static bool CleanCapture =>
            Environment.GetEnvironmentVariable("SMILEY_CLEAN_APK") == "1";

        private static void Run(bool release)
        {
            string[] scenes = ScenesFromBuildSettings();

            if (scenes.Length == 0)
            {
                Debug.LogError("[BUILD] No scenes are enabled in Build Settings; refusing to " +
                               "build an APK that would open to nothing.");
                EditorApplication.Exit(1);
                return;
            }

            foreach (string scene in scenes)
            {
                Debug.Log($"[BUILD] scene: {scene}");
            }

            // ⛔ THE LAYOUT GUARD RUNS HERE TOO, and it did not always. It ran only in the scene
            // builder, and "deploy it" never runs the scene builder — the owner's layouts are
            // theirs, and regenerating the scene would wipe what they assigned in the Inspector.
            // So a layout with a disabled X reached the phone with nothing having checked it
            // (D-071). An APK that cannot be played is not a deliverable, whichever path built it.
            // D-076: the map is the owner's prefab, edited after any regeneration, so the APK
            // build checks it the way it checks the layouts.
            // D-087: the guard checks the prefab, so what the owner did on the scene instance
            // must be in the prefab first.
            MapAssets.ApplySceneOverrides("Assets/Scenes/Game.unity");

            // D-092: the idle frames are bottom-anchored before they ship.
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
            var idle = UnityEngine.Object.FindFirstObjectByType<Smiley.View.IdleAnimator>();
            if (idle != null)
            {
                IdleArtFit.FitClips(idle.Clips);
            }

            if (!MapArtFit.FitPalette())
            {
                Debug.LogError("[BUILD] Could not measure the platform art (D-084).");
                EditorApplication.Exit(1);
                return;
            }

            int mapFailures = MapGuard.Check();

            if (mapFailures > 0)
            {
                Debug.LogError($"[BUILD] {mapFailures} problem(s) with the map. Refusing to build an APK the player could not climb.");
                EditorApplication.Exit(1);
                return;
            }

            int layoutFailures = UiLayoutGuard.Check();

            if (layoutFailures > 0)
            {
                Debug.LogError(
                    $"[BUILD] {layoutFailures} UI control(s) are missing or disabled in the " +
                    "layouts. Refusing to build an APK the player could not get through.");

                EditorApplication.Exit(1);
                return;
            }

            string full = Path.GetFullPath(release ? BundlePath : ApkPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full) ?? ".");

            // ⭐ D-102: the owner configured a release keystore in Player Settings (D:/keystore,
            // alias "smiley"). A batch build has no dialog to ask for its passwords, so: with
            // SMILEY_KEYSTORE_PASS (and optionally SMILEY_KEYALIAS_PASS) in the environment the
            // player is signed with it; without them the DEVELOPMENT build falls back to the
            // debug key for its own run only — the setting is put back afterwards, never
            // persisted. ⛔ Passwords never reach a file in this repository.
            // ⛔ D-119: the RELEASE bundle is never debug-signed. Play rejects an upload not
            // signed by the registered key, so a bundle without the password is refused here,
            // before eight minutes of IL2CPP — unless SMILEY_DEBUG_BUNDLE=1 asks for a pipeline
            // dry run, which lands as smiley-debug.aab so it cannot be mistaken for an upload.
            bool customKeystore = PlayerSettings.Android.useCustomKeystore;
            string keystorePass = Environment.GetEnvironmentVariable("SMILEY_KEYSTORE_PASS");
            string aliasPass = Environment.GetEnvironmentVariable("SMILEY_KEYALIAS_PASS");
            bool debugBundle = release && Environment.GetEnvironmentVariable("SMILEY_DEBUG_BUNDLE") == "1";

            if (release && !debugBundle && (!customKeystore || string.IsNullOrEmpty(keystorePass)))
            {
                Debug.LogError("[BUILD] REFUSED: a release bundle must be signed with the release keystore. " +
                               (customKeystore
                                   ? "Set SMILEY_KEYSTORE_PASS (and SMILEY_KEYALIAS_PASS if it differs) in the environment."
                                   : "Player Settings has no custom keystore configured."));
                EditorApplication.Exit(1);
                return;
            }

            if (debugBundle)
            {
                full = Path.Combine(Path.GetDirectoryName(full) ?? ".", "smiley-debug.aab");
                Debug.LogWarning("[BUILD] SMILEY_DEBUG_BUNDLE=1: a DEBUG-SIGNED bundle for a pipeline dry run — NOT uploadable.");
            }

            // ⭐ D-119: every store upload needs a higher version code than the last. The bundle
            // build advances it and persists it, so the number lives in ProjectSettings and in
            // git beside the code it names; SMILEY_VERSION_CODE overrides for a deliberate jump.
            int previousVersionCode = PlayerSettings.Android.bundleVersionCode;
            if (release && !debugBundle)
            {
                string wanted = Environment.GetEnvironmentVariable("SMILEY_VERSION_CODE");
                int versionCode = int.TryParse(wanted, out int parsed) && parsed > 0 ? parsed : previousVersionCode + 1;

                if (versionCode <= previousVersionCode)
                {
                    Debug.LogError($"[BUILD] REFUSED: version code {versionCode} is not above the last one ({previousVersionCode}).");
                    EditorApplication.Exit(1);
                    return;
                }

                PlayerSettings.Android.bundleVersionCode = versionCode;
            }

            EditorUserBuildSettings.buildAppBundle = release;

            if (release)
            {
                // Native symbols beside the bundle, so a Play crash report names a function.
                UnityEditor.Android.UserBuildSettings.DebugSymbols.level = Unity.Android.Types.DebugSymbolLevel.SymbolTable;
                UnityEditor.Android.UserBuildSettings.DebugSymbols.format = Unity.Android.Types.DebugSymbolFormat.Zip;
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = full,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,

                // Development so the owner can profile what they are feeling, and so a crash
                // gives a usable log rather than a silent close. ⛔ Neither flag on a release.
                //
                // ⚠ SMILEY_CLEAN_APK=1 drops them from an APK, for one reason only: Unity stamps
                // a "Development Build" watermark over the corner of every frame, and a store
                // cover or trailer carrying that watermark is unusable. ⛔ It is NOT a release
                // build and must never be uploaded -- it is debug-signed, and D-119 keeps the
                // release path the only thing that produces an uploadable artefact.
                options = release || CleanCapture
                    ? BuildOptions.None
                    : BuildOptions.Development | BuildOptions.AllowDebugging,
            };

            Debug.Log($"[BUILD] {(release ? "RELEASE BUNDLE" : CleanCapture ? "CLEAN APK (no watermark, NOT uploadable)" : "development APK")} " +
                      $"version {PlayerSettings.bundleVersion} ({PlayerSettings.Android.bundleVersionCode}) " +
                      $"backend={PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android)} " +
                      $"arch={PlayerSettings.Android.targetArchitectures} " +
                      $"minSdk={PlayerSettings.Android.minSdkVersion} targetSdk={PlayerSettings.Android.targetSdkVersion} " +
                      $"orientation={PlayerSettings.defaultInterfaceOrientation}");

            if (customKeystore && (string.IsNullOrEmpty(keystorePass) || debugBundle))
            {
                Debug.Log("[BUILD] signing: DEBUG key for this build — " +
                          $"the release keystore '{PlayerSettings.Android.keystoreName}' is left alone.");
                PlayerSettings.Android.useCustomKeystore = false;
            }
            else if (customKeystore)
            {
                PlayerSettings.Android.keystorePass = keystorePass;
                PlayerSettings.Android.keyaliasPass = string.IsNullOrEmpty(aliasPass) ? keystorePass : aliasPass;
                Debug.Log($"[BUILD] signing: release keystore '{PlayerSettings.Android.keystoreName}', alias '{PlayerSettings.Android.keyaliasName}'.");
            }

            BuildReport report;

            try
            {
                report = BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                PlayerSettings.Android.useCustomKeystore = customKeystore;
                PlayerSettings.Android.keystorePass = string.Empty;
                PlayerSettings.Android.keyaliasPass = string.Empty;
                EditorUserBuildSettings.buildAppBundle = false;

                // ⚠ Restored in memory is not restored on disk: the build had already serialised
                // ProjectSettings.asset with the flag off, and -quit did not write it back, so
                // three debug-signed builds on 2026-09-13 left the owner's keystore switched
                // off in the working tree (caught at commit, never committed).
                AssetDatabase.SaveAssets();
            }

            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                if (release && !debugBundle)
                {
                    // A failed bundle did not reach the store; the number is not spent.
                    PlayerSettings.Android.bundleVersionCode = previousVersionCode;
                    AssetDatabase.SaveAssets();
                }

                Debug.LogError($"[BUILD] FAILED: {summary.result}, {summary.totalErrors} error(s).");
                EditorApplication.Exit(1);
                return;
            }

            long bytes = new FileInfo(full).Length;
            Debug.Log($"[BUILD] OK  {full}");
            Debug.Log($"[BUILD] size {bytes / (1024 * 1024)} MB");
            Debug.Log($"[BUILD] time {summary.totalTime}");

            if (release && !debugBundle)
            {
                Debug.Log($"[BUILD] versionCode {PlayerSettings.Android.bundleVersionCode} (was {previousVersionCode}) — commit ProjectSettings.asset with this upload.");
            }
        }

        private static string[] ScenesFromBuildSettings()
        {
            var enabled = new System.Collections.Generic.List<string>();

            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled)
                {
                    enabled.Add(scene.path);
                }
            }

            return enabled.ToArray();
        }
    }
}
