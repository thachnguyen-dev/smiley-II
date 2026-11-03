using System;
using System.IO;
using Smiley.Web;
using UnityEngine;

namespace Smiley.Save
{
    /// <summary>
    /// Reading and writing the one save file (D-013).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠ <b>Written atomically, with one backup.</b> A game that saves at gameplay events (§21)
    /// is writing while the player is playing, and an app killed mid-write must not come back
    /// to a half-file. The new save is written beside the real one and moved into place only
    /// once it is complete, and the previous good save is kept.
    /// </para>
    /// <para>
    /// ⛔ <b>A truncated file that still parses is the failure that matters here.</b> JSON gives
    /// a parse error for most truncations, but not all — a cut in the right place can still
    /// deserialize into an object full of plausible zeros. That is why
    /// <see cref="SaveData.EndMarker"/> exists and is checked: a save that did not finish being
    /// written is refused, and the backup is used instead.
    /// </para>
    /// <para>
    /// ⭐ <b>D-138: on CrazyGames the save lives in the portal's Data module, not in a file.</b>
    /// Whenever <see cref="Portal.Storage"/> is present, the same text goes under the same two
    /// names — live and backup — so a logged-in player's climb follows them between devices, and a
    /// guest's survives in the portal's own storage. ⓘ The format, the end marker and the backup
    /// rule are unchanged; only where the text is kept moves. ⚠ The Data module debounces writes by
    /// about a second, so a tab closed in that second can lose the very last save.
    /// ⛔ Everywhere else — every phone and every fixture — <see cref="Portal.Storage"/> is null and
    /// this class reads and writes files exactly as before.
    /// </para>
    /// </remarks>
    public static class SaveFile
    {
        private const string FileName = "smiley.save";
        private const string BackupName = "smiley.save.bak";
        private const string TempName = "smiley.save.tmp";

        private static string Dir => Application.persistentDataPath;

        /// <summary>Full path of the live save.</summary>
        public static string Path => System.IO.Path.Combine(Dir, FileName);

        /// <summary>Full path of the previous good save.</summary>
        public static string BackupPath => System.IO.Path.Combine(Dir, BackupName);

        /// <summary>Whether a save exists at all — what §22's Continue depends on.</summary>
        public static bool Exists()
        {
            IKeyValueStore store = Portal.Storage;

            if (store != null)
            {
                return store.HasKey(FileName) || store.HasKey(BackupName);
            }

            return File.Exists(Path) || File.Exists(BackupPath);
        }

        /// <summary>Serialises to the exact text that would be written. Pure; used by tests.</summary>
        public static string Serialise(SaveData data)
        {
            data.EndMarker = SaveData.Complete;
            return JsonUtility.ToJson(data, prettyPrint: true);
        }

        /// <summary>
        /// Parses save text, returning null if it is unusable.
        /// </summary>
        /// <remarks>
        /// ⓘ Null rather than an exception, because an unreadable save is an ordinary
        /// situation — a first run, a half-written file, a save from a future version — and
        /// none of those is an error the player should ever see.
        /// </remarks>
        public static SaveData Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            try
            {
                var data = JsonUtility.FromJson<SaveData>(text);

                if (data == null || !data.IsComplete())
                {
                    return null;
                }

                if (data.Version > new SaveData().Version)
                {
                    // A save written by a later build. Refusing it is safer than guessing at
                    // fields this build does not know about.
                    return null;
                }

                return data;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Writes a save, atomically, keeping the previous one as a backup.</summary>
        public static bool Write(SaveData data)
        {
            IKeyValueStore store = Portal.Storage;

            if (store != null)
            {
                return WriteTo(store, data);
            }

            try
            {
                Directory.CreateDirectory(Dir);

                string temp = System.IO.Path.Combine(Dir, TempName);
                File.WriteAllText(temp, Serialise(data));

                // Only once the temp file is whole does the live save get replaced.
                if (File.Exists(Path))
                {
                    if (File.Exists(BackupPath))
                    {
                        File.Delete(BackupPath);
                    }

                    File.Move(Path, BackupPath);
                }

                File.Move(temp, Path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SAVE] Could not write: {e.Message}");
                return false;
            }
        }

        /// <summary>The same write, into the portal's store: the previous save becomes the backup.</summary>
        private static bool WriteTo(IKeyValueStore store, SaveData data)
        {
            try
            {
                string text = Serialise(data);

                if (store.HasKey(FileName))
                {
                    store.SetString(BackupName, store.GetString(FileName));
                }

                store.SetString(FileName, text);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SAVE] Could not write to the portal: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Reads the most recent usable save, falling back to the backup.
        /// </summary>
        /// <returns>Null when there is nothing usable, which is a normal first run.</returns>
        public static SaveData Read()
        {
            IKeyValueStore store = Portal.Storage;

            if (store != null)
            {
                return ReadFrom(store);
            }

            SaveData live = ReadFrom(Path);
            if (live != null)
            {
                return live;
            }

            SaveData backup = ReadFrom(BackupPath);
            if (backup != null)
            {
                Debug.LogWarning("[SAVE] The live save was unusable; the backup was loaded.");
            }

            return backup;
        }

        private static SaveData ReadFrom(IKeyValueStore store)
        {
            try
            {
                SaveData live = store.HasKey(FileName) ? Parse(store.GetString(FileName)) : null;
                if (live != null)
                {
                    return live;
                }

                SaveData backup = store.HasKey(BackupName) ? Parse(store.GetString(BackupName)) : null;
                if (backup != null)
                {
                    Debug.LogWarning("[SAVE] The live save was unusable; the backup was loaded.");
                }

                return backup;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SAVE] Could not read from the portal: {e.Message}");
                return null;
            }
        }

        private static SaveData ReadFrom(string path)
        {
            try
            {
                return File.Exists(path) ? Parse(File.ReadAllText(path)) : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SAVE] Could not read {path}: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Deletes everything, for §25's Reset Game.
        /// </summary>
        /// <remarks>
        /// ⚠ <b>The backup goes too.</b> §25 says Reset Game <i>"deletes all gameplay Save /
        /// Progress"</i>, and a reset that left a resurrectable copy behind would not be one.
        /// ⓘ §25 also says settings are NOT reset — they are not stored here.
        /// </remarks>
        public static void DeleteAll()
        {
            IKeyValueStore store = Portal.Storage;

            if (store != null)
            {
                try
                {
                    store.DeleteKey(FileName);
                    store.DeleteKey(BackupName);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SAVE] Could not delete from the portal: {e.Message}");
                }

                return;
            }

            foreach (string path in new[] { Path, BackupPath, System.IO.Path.Combine(Dir, TempName) })
            {
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SAVE] Could not delete {path}: {e.Message}");
                }
            }
        }
    }
}
