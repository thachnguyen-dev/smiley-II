using Smiley.Ads;
using UnityEditor;
using UnityEngine;

namespace Smiley.Editor.Setup
{
    /// <summary>
    /// Writes the owner's ad settings once, seeded with what they supplied (D-105).
    /// </summary>
    /// <remarks>
    /// ⛔ Written only if missing, like the layouts and the map: the asset is the owner's to edit
    /// afterwards. The IDs are NOT secrets — an App Key and an ad-unit ID identify the app to the
    /// network and ship inside every APK — so seeding them here is safe; the keystore password is
    /// the thing that must never be written down, and it is not (D-102).
    /// </remarks>
    public static class AdAssets
    {
        public const string Path = "Assets/Config/AdSettings.asset";

        public static AdSettings Ensure()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AdSettings>(Path);

            if (existing != null)
            {
                return existing;
            }

            var settings = ScriptableObject.CreateInstance<AdSettings>();
            settings.AndroidAppKey = "2817aaeed";               // ⭐ owner, 2026-09-12 (D-110)
            settings.AndroidBannerAdUnitId = "oxjqpqxpvkq1r8r9"; // ⭐ owner, 2026-09-12 (D-110)
            settings.AndroidInterstitialAdUnitId = "udscfsi88kwwxv5h"; // ⭐ owner, 2026-09-17 (D-122)

            settings.IosAppKey = "28a03bf45";                      // ⭐ owner, 2026-10-10 (Q-057)
            settings.IosBannerAdUnitId = "bdwcmjdsdo19skt3";        // ⭐ owner, 2026-10-10 (Q-057)
            settings.IosInterstitialAdUnitId = "4lodjqmnqczfhitq";  // ⭐ owner, 2026-10-10 (Q-057)
            settings.InterstitialMinIntervalSeconds = 180f; // ⭐ owner, 2026-09-17 (D-122)
            settings.RemoveAdsProductId = "remove_ads";     // ⭐ owner, 2026-10-01 (D-124)
            settings.ChildDirected = false;               // ⭐ owner, 2026-09-12 (D-110): not directed at children under 13
            settings.Enabled = true;

            AssetDatabase.CreateAsset(settings, Path);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ADS] Wrote {Path}.");
            return settings;
        }
    }
}
