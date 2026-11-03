using System;
using System.IO;
using UnityEngine;

namespace Smiley.Audio
{
    /// <summary>§26's settings, as data.</summary>
    /// <remarks>
    /// ⛔ <b>Reset Game must not touch these</b> — §25 says so in as many words. That is
    /// guaranteed <b>structurally</b> rather than by remembering: they live in their own file,
    /// which <see cref="Save.SaveFile.DeleteAll"/> does not know about and cannot delete.
    /// </remarks>
    [Serializable]
    public sealed class GameSettings
    {
        /// <summary>Format version, so a future change can migrate rather than guess.</summary>
        public int Version = 1;

        /// <summary>§26: Music Volume, saved persistently.</summary>
        public float MusicVolume = 0.7f;

        /// <summary>§26: SFX Volume, saved persistently.</summary>
        public float SfxVolume = 0.9f;

        /// <summary>
        /// D-107: the parent's answer to the consent question — 0 never asked, 1 granted,
        /// 2 refused. Read through <c>ConsentRule.FromStored</c>, which treats anything else as
        /// never asked. ⓘ A settings file from before the question has no field and reads as 0.
        /// </summary>
        public int ParentalConsent = 0;

        /// <summary>
        /// D-124: whether the Remove Ads purchase has been made — 1 bought, anything else not.
        /// Read through <c>RemoveAdsRule.FromStored</c>. ⚠ The store is the truth; this is the
        /// memory, so the game knows while offline and before the store connects. ⓘ A settings
        /// file from before the purchase existed has no field and reads as 0.
        /// </summary>
        public int AdsRemoved = 0;

        /// <summary>Brings both values back into range, whatever the file said.</summary>
        public void Clamp()
        {
            MusicVolume = Mathf.Clamp01(MusicVolume);
            SfxVolume = Mathf.Clamp01(SfxVolume);
        }
    }

    /// <summary>
    /// Reading and writing §26's settings file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠ <b>Deliberately simpler than <see cref="Save.SaveFile"/>, and the difference is the
    /// point.</b> The save is written during play, so it needs an atomic swap, a backup and an
    /// end marker against a half-finished write. Settings change when a person moves a slider —
    /// there is no mid-play write to be killed in the middle of, and losing a volume setting
    /// costs one gesture rather than a climb.
    /// </para>
    /// <para>
    /// ⓘ An unreadable settings file returns the defaults rather than an error. A player who has
    /// never opened Settings has no file at all, and that is the ordinary case, not a fault.
    /// </para>
    /// </remarks>
    public static class SettingsFile
    {
        private const string FileName = "smiley.settings";

        /// <summary>Where the settings live. ⛔ Not the save file, and not beside its backup.</summary>
        public static string Path =>
            System.IO.Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>Whether anything has ever been saved.</summary>
        public static bool Exists() => File.Exists(Path);

        /// <summary>Serialises to the exact text that would be written. Pure; used by tests.</summary>
        public static string Serialise(GameSettings settings) =>
            JsonUtility.ToJson(settings, prettyPrint: true);

        /// <summary>Parses settings text, falling back to the defaults for anything unusable.</summary>
        public static GameSettings Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new GameSettings();
            }

            try
            {
                var settings = JsonUtility.FromJson<GameSettings>(text);

                if (settings == null || settings.Version > new GameSettings().Version)
                {
                    // A file from a later build. Defaults are safer than guessing at fields this
                    // build does not know about — and unlike a save, nothing is lost by it.
                    return new GameSettings();
                }

                settings.Clamp();
                return settings;
            }
            catch (Exception)
            {
                return new GameSettings();
            }
        }

        /// <summary>Reads the settings, or the defaults when there are none.</summary>
        public static GameSettings Read()
        {
            try
            {
                return File.Exists(Path) ? Parse(File.ReadAllText(Path)) : new GameSettings();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SETTINGS] Could not read {Path}: {e.Message}");
                return new GameSettings();
            }
        }

        /// <summary>Writes the settings. §26: both volumes are saved persistently.</summary>
        public static bool Write(GameSettings settings)
        {
            try
            {
                settings.Clamp();
                Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(Path, Serialise(settings));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SETTINGS] Could not write: {e.Message}");
                return false;
            }
        }

        /// <summary>Removes the settings file. ⛔ For TESTS — §25's Reset Game must never do this.</summary>
        public static void DeleteForTests()
        {
            try
            {
                if (File.Exists(Path))
                {
                    File.Delete(Path);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SETTINGS] Could not delete: {e.Message}");
            }
        }
    }
}
