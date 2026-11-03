using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Smiley.Editor.Setup
{
    /// <summary>
    /// Generates the Xcode project for iOS (D-127). ⛔ It does not, and cannot, produce an app.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠ <b>This is half a build, by necessity.</b> Unity on Windows writes an Xcode project;
    /// turning that into a signed .ipa needs macOS, Xcode and CocoaPods, and no flag changes that.
    /// The owner's instruction is that everything else is finished here first, so this exists to
    /// make the Windows half complete and repeatable — and to fail here rather than on a borrowed
    /// Mac, where every mistake costs a round trip.
    /// </para>
    /// <para>
    /// ⛔ <b>No version code is advanced and nothing is signed.</b> Unlike the Android bundle,
    /// which is a shippable artefact the moment it exists, this output is an input to another
    /// tool. Build numbers belong to the step that archives.
    /// </para>
    /// </remarks>
    public static class IosBuildCommand
    {
        private const string Output = "build/ios";
        private const string SimulatorOutput = "build/ios-simulator";

        /// <summary>
        /// Whether to target the Simulator SDK rather than a device.
        /// </summary>
        /// <remarks>
        /// ⭐ <b>This exists for screenshots.</b> App Store listings want 6.9-inch images at
        /// 1320×2868, and the cleanest source is an iPhone 16 Pro Max Simulator — genuinely iOS,
        /// at exactly the right resolution, with no scaling of an Android capture.
        /// ⛔ <b>A Simulator build cannot be archived or uploaded.</b> It is x86_64/arm64-sim and
        /// the App Store takes device builds only, so this writes to a SEPARATE folder: a
        /// simulator build sitting where the device build was is a wasted trip to the Mac.
        /// </remarks>
        private static bool Simulator =>
            System.Environment.GetEnvironmentVariable("SMILEY_IOS_SIMULATOR") == "1";

        public static void BuildXcodeProject()
        {
            string[] scenes = ScenesFromBuildSettings();

            if (scenes.Length == 0)
            {
                Debug.LogError("[IOS] No scenes are enabled in Build Settings; refusing to " +
                               "generate a project that would open to nothing.");
                EditorApplication.Exit(1);
                return;
            }

            foreach (string scene in scenes)
            {
                Debug.Log($"[IOS] scene: {scene}");
            }

            // ⛔ The same guards the Android build runs. The owner's layouts and the map prefab
            // are checked whichever platform is being built — a layout that cannot be played is
            // not a deliverable on iOS either (D-071, D-076, D-087).
            MapAssets.ApplySceneOverrides("Assets/Scenes/Game.unity");

            // ⚠ Set BEFORE the build, and put back afterwards: this is a persisted project
            // setting, and a device build that silently inherited the Simulator SDK would fail
            // on upload with an unhelpful error about the binary's architecture.
            iOSSdkVersion wanted = Simulator ? iOSSdkVersion.SimulatorSDK : iOSSdkVersion.DeviceSDK;
            iOSSdkVersion previous = PlayerSettings.iOS.sdkVersion;
            PlayerSettings.iOS.sdkVersion = wanted;

            Debug.Log($"[IOS] {(Simulator ? "SIMULATOR BUILD (screenshots only, NOT uploadable)" : "DEVICE BUILD")} " +
                      $"bundleId={PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS)} " +
                      $"version={PlayerSettings.bundleVersion} " +
                      $"minIos={PlayerSettings.iOS.targetOSVersionString} " +
                      $"orientation={PlayerSettings.defaultInterfaceOrientation} " +
                      $"arch={PlayerSettings.GetArchitecture(NamedBuildTarget.iOS)}");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                // ⛔ ABSOLUTE. locationPathName is relative to the PROJECT folder, not the
                // repository, so a bare "build/web" lands in src/build/web -- and every
                // script looking beside the project then reports a failed build that had
                // in fact succeeded, which is exactly what happened on 2026-10-10.
                locationPathName = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(Application.dataPath, "..", "..", Simulator ? SimulatorOutput : Output)),
                target = BuildTarget.iOS,
                targetGroup = BuildTargetGroup.iOS,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);

            // ⛔ Always restored, success or failure. Leaving the Simulator SDK set is a trap
            // that only shows up at the end of the NEXT device build.
            PlayerSettings.iOS.sdkVersion = previous;

            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[IOS] FAILED: {report.summary.result}, {report.summary.totalErrors} error(s).");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log($"[IOS] OK  {(Simulator ? SimulatorOutput : Output)}");
            Debug.Log("[IOS] ⚠ This is an Xcode project, NOT an app. Move it to a Mac and run " +
                      "'pod install', then open the .xcworkspace — never the .xcodeproj.");
        }

        private static string[] ScenesFromBuildSettings()
        {
            var scenes = new List<string>();

            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled)
                {
                    scenes.Add(scene.path);
                }
            }

            return scenes.ToArray();
        }
    }
}
