#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace Smiley.Editor.Setup
{
    /// <summary>
    /// The two things Apple requires of the generated Xcode project that Unity does not add and
    /// LevelPlay does not add either (D-127).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⛔ <b>Both are review-blocking.</b> A build carrying an ad SDK is rejected without
    /// <c>NSUserTrackingUsageDescription</c>, and an app without a privacy manifest is rejected
    /// for the required-reason APIs it uses.
    /// </para>
    /// <para>
    /// ⓘ <b>SKAdNetwork is NOT handled here.</b> LevelPlay's own post-process fetches the
    /// identifiers for every adapter and writes them into Info.plist — verified by reading
    /// <c>Editor/IosAdsSupport/PostProcessBuildPlist.cs</c> in the installed package, not assumed.
    /// ⚠ It fetches them over the network at build time, so the Mac doing the Xcode step needs a
    /// connection or the list silently comes back short.
    /// </para>
    /// <para>
    /// ⚠ <b>This runs only on a Mac</b>, because that is the only place an iOS build happens. It
    /// is written on Windows and is unverified until then — which is exactly why it does as little
    /// as possible and says so loudly in the build log either way.
    /// </para>
    /// </remarks>
    public static class IosBuildPostProcess
    {
        /// <summary>
        /// ⭐ Owner-facing text. Shown by iOS inside Apple's own prompt, so it must say what the
        /// player gets, not what the developer gets. ⏸ The owner may reword this; it is the only
        /// sentence here they would ever want to change.
        /// </summary>
        private const string TrackingReason =
            "Smiley II shows adverts. Allowing this lets them be more relevant to you. " +
            "The game plays exactly the same either way.";

        [PostProcessBuild(100)]
        public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS)
            {
                return;
            }

            AddTrackingUsageDescription(pathToBuiltProject);
            AddPrivacyManifest(pathToBuiltProject);
            DropTheGitSpecRepo(pathToBuiltProject);
            ForcePodDeploymentTarget(pathToBuiltProject);
        }

        /// <summary>
        /// Makes every pod build against the same minimum iOS version as the app.
        /// </summary>
        /// <remarks>
        /// <para>
        /// ⚠ <b>IronSourceSDK declares iOS 13.0.</b> Xcode 26 supports 15.0 and above, so every
        /// pod target raises <i>"the range of supported deployment target versions is 15.0 to
        /// 27.0.x"</i>. Today that is a warning; newer Xcode turns it into an error, and either
        /// way it buries the warnings that matter under dozens that do not.
        /// </para>
        /// <para>
        /// ⓘ The app itself is already 15.0 — <c>PlayerSettings.iOS.targetOSVersionString</c>
        /// and the Podfile's own <c>platform</c> line both say so. This only drags the pods up to
        /// meet it, which is the standard CocoaPods remedy and changes nothing about which
        /// devices are supported.
        /// </para>
        /// <para>
        /// ⛔ Skipped when a <c>post_install</c> hook already exists, rather than appending a
        /// second one: two hooks in a Podfile is a syntax error, and a Podfile that will not
        /// parse is a worse problem than a warning.
        /// </para>
        /// </remarks>
        private static void ForcePodDeploymentTarget(string path)
        {
            string podfile = Path.Combine(path, "Podfile");

            if (!File.Exists(podfile))
            {
                return;
            }

            string text = File.ReadAllText(podfile);

            if (text.Contains("post_install"))
            {
                Debug.Log("[IOS] The Podfile already has a post_install hook; leaving it alone.");
                return;
            }

            string minimum = PlayerSettings.iOS.targetOSVersionString;

            text += "\n" +
                    "# Added by Smiley's iOS post-process. IronSourceSDK declares iOS 13.0 and\n" +
                    "# Xcode supports 15.0 and above, so every pod target warns. This drags them\n" +
                    "# up to the app's own minimum.\n" +
                    "post_install do |installer|\n" +
                    "  # The PROJECT, not only its targets. Xcode reports the warning\n" +
                    "  # against Pods.xcodeproj itself, and a targets-only loop never\n" +
                    "  # touches it -- which is why the first attempt did not clear it.\n" +
                    "  installer.pods_project.build_configurations.each do |config|\n" +
                    "    config.build_settings['IPHONEOS_DEPLOYMENT_TARGET'] = '" + minimum + "'\n" +
                    "  end\n" +
                    "  installer.pods_project.targets.each do |target|\n" +
                    "    target.build_configurations.each do |config|\n" +
                    "      config.build_settings['IPHONEOS_DEPLOYMENT_TARGET'] = '" + minimum + "'\n" +
                    "    end\n" +
                    "  end\n" +
                    "end\n";

            File.WriteAllText(podfile, text);
            Debug.Log($"[IOS] Podfile post_install pins every pod to iOS {minimum}.");
        }
        /// <summary>
        /// Removes the git spec repo from the Podfile, leaving the CDN.
        /// </summary>
        /// <remarks>
        /// <para>
        /// ⛔ <b>This cost a real hour on a borrowed Mac.</b> LevelPlay's dependency file names
        /// <c>https://github.com/CocoaPods/Specs</c> as a source, the resolver copies it into the
        /// Podfile, and CocoaPods then tries to <i>clone that entire repository</i> — several
        /// gigabytes — before it will resolve a single pod. It looks exactly like a hang.
        /// </para>
        /// <para>
        /// ⭐ <b>The CDN line below it already serves every pod in that repo</b>, without a clone.
        /// Dropping the git source changes nothing about which versions resolve; it changes only
        /// how CocoaPods finds them.
        /// </para>
        /// <para>
        /// ⚠ Written as a post-process rather than by editing LevelPlay's XML, because that file
        /// is regenerated by their Network Manager and any edit to it would be silently undone.
        /// </para>
        /// </remarks>
        private static void DropTheGitSpecRepo(string path)
        {
            string podfile = Path.Combine(path, "Podfile");

            if (!File.Exists(podfile))
            {
                return;
            }

            string[] lines = File.ReadAllLines(podfile);
            var kept = new System.Collections.Generic.List<string>(lines.Length);
            bool dropped = false;

            foreach (string line in lines)
            {
                if (line.Contains("github.com/CocoaPods/Specs"))
                {
                    dropped = true;
                    continue;
                }

                kept.Add(line);
            }

            if (!dropped)
            {
                return;
            }

            // ⛔ Only safe because the CDN source is present. Removing the only source would
            // leave CocoaPods unable to find anything at all.
            if (!System.Array.Exists(lines, l => l.Contains("cdn.cocoapods.org")))
            {
                Debug.LogWarning("[IOS] The Podfile names the git spec repo and NO CDN. Leaving it " +
                                 "alone — removing the only source would break pod install.");
                return;
            }

            File.WriteAllLines(podfile, kept);
            Debug.Log("[IOS] Dropped the git spec repo from the Podfile; the CDN serves the same " +
                      "pods without cloning several gigabytes.");
        }

        private static void AddTrackingUsageDescription(string path)
        {
            string plistPath = Path.Combine(path, "Info.plist");

            if (!File.Exists(plistPath))
            {
                Debug.LogError("[IOS] No Info.plist at " + plistPath + " — the ATT prompt will be " +
                               "rejected by App Store review.");
                return;
            }

            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetString("NSUserTrackingUsageDescription", TrackingReason);

            // ⭐ Export compliance, answered once here instead of by hand on every upload.
            //
            // ⛔ FALSE is the accurate answer, not a convenience. Verified 2026-10-13: no
            // cryptography anywhere in this project (no System.Security.Cryptography, no cipher
            // of any kind), the save is plain JSON through JsonUtility, and the game code makes
            // no network calls at all. The only encrypted traffic is HTTPS performed by Apple's
            // own operating system for the ad SDK and StoreKit, which is exactly what the
            // question excludes.
            //
            // ⚠ If this app ever encrypts a save, bundles its own cipher, or pins a certificate,
            // this line becomes a false declaration and must change with it.
            plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);

            plist.WriteToFile(plistPath);

            Debug.Log("[IOS] NSUserTrackingUsageDescription written.");
        }

        /// <summary>
        /// The app's own privacy manifest.
        /// </summary>
        /// <remarks>
        /// ⓘ LevelPlay ships one for ITS framework; this is the app's, which Apple requires
        /// separately. It declares the required-reason APIs this game actually uses: UserDefaults,
        /// because settings and the Remove Ads entitlement are remembered, and file timestamps,
        /// which Unity's own save path touches.
        /// ⛔ <b>NSPrivacyTracking is false and the collected-data list is empty</b>, which is the
        /// truth for this game: it has no server, no account, no analytics, and keeps nothing about
        /// the player anywhere but their own device (D-003).
        /// </remarks>
        private static void AddPrivacyManifest(string path)
        {
            string target = Path.Combine(path, "PrivacyInfo.xcprivacy");

            File.WriteAllText(target, Manifest);
            Debug.Log("[IOS] PrivacyInfo.xcprivacy written. ⚠ Add it to the Xcode target if the " +
                      "build is archived without it being picked up automatically.");
        }

        private const string Manifest =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
	<key>NSPrivacyTracking</key>
	<false/>
	<key>NSPrivacyTrackingDomains</key>
	<array/>
	<key>NSPrivacyCollectedDataTypes</key>
	<array/>
	<key>NSPrivacyAccessedAPITypes</key>
	<array>
		<dict>
			<key>NSPrivacyAccessedAPIType</key>
			<string>NSPrivacyAccessedAPICategoryUserDefaults</string>
			<key>NSPrivacyAccessedAPITypeReasons</key>
			<array>
				<string>CA92.1</string>
			</array>
		</dict>
		<dict>
			<key>NSPrivacyAccessedAPIType</key>
			<string>NSPrivacyAccessedAPICategoryFileTimestamp</string>
			<key>NSPrivacyAccessedAPITypeReasons</key>
			<array>
				<string>C617.1</string>
			</array>
		</dict>
	</array>
</dict>
</plist>
";
    }
}
#endif
