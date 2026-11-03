using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Smiley.Editor.Setup
{
    /// <summary>
    /// Writes §27's placeholder sounds, as WAV files.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⏸ <b>PLACEHOLDERS, AND THEY MUST STAY OBVIOUSLY SO (Q-049).</b> These are synthesised
    /// tones and noise bursts, not a sound design — §32 reserves that to the owner. They exist so
    /// the audio system is <b>audibly working</b> rather than silently untested: a mixer with no
    /// clips in it is the same defect as a volume slider with no audio behind it, one layer down.
    /// </para>
    /// <para>
    /// ⓘ <b>Generated rather than committed as binaries</b>, for the same reason the placeholder
    /// sprites are: a build produces them, so nobody has to have run the right menu item once,
    /// and replacing a file with real audio is the whole of the handover.
    /// </para>
    /// <para>
    /// ⚠ <b>WAV by hand, because Unity imports it without a codec.</b> A 44-byte header and
    /// 16-bit PCM samples is a format that can be written from C# with no dependencies at all.
    /// </para>
    /// </remarks>
    public static class AudioAssets
    {
        /// <summary>Where the placeholder clips live.</summary>
        public const string Folder = "Assets/Audio";

        private const string PlaceholderMusic = Folder + "/PlaceholderMusic.wav";
        private const string PlaceholderImpact = Folder + "/PlaceholderImpact.wav";
        private const string PlaceholderLaunch = Folder + "/PlaceholderLaunch.wav";
        private const string PlaceholderGuide = Folder + "/PlaceholderGuide.wav";

        /// <summary>
        /// ⭐ The owner's clips (D-096), by slot: drop a file with this name and any of the
        /// extensions below into <c>Assets/Audio</c> and the next build wires it. The placeholder
        /// is used only while the real one is missing.
        /// </summary>
        public const string MusicName = "Music";
        public const string LandingName = "Landing";
        public const string LaunchName = "Launch";
        public const string DragName = "Drag";

        private static readonly string[] Extensions = { ".wav", ".flac", ".ogg", ".mp3", ".aiff" };

        public static string MusicPath => Resolve(MusicName, PlaceholderMusic);
        public static string ImpactPath => Resolve(LandingName, PlaceholderImpact);
        public static string LaunchPath => Resolve(LaunchName, PlaceholderLaunch);
        public static string GuidePath => Resolve(DragName, PlaceholderGuide);

        /// <summary>The owner's file for a slot if one exists, else the placeholder.</summary>
        private static string Resolve(string name, string placeholder)
        {
            foreach (string extension in Extensions)
            {
                string path = Folder + "/" + name + extension;

                if (File.Exists(path))
                {
                    return path;
                }
            }

            return placeholder;
        }

        /// <summary>Which slots are still on placeholders. For the build report.</summary>
        public static string Report() =>
            $"music={System.IO.Path.GetFileName(MusicPath)} landing={System.IO.Path.GetFileName(ImpactPath)} " +
            $"launch={System.IO.Path.GetFileName(LaunchPath)} drag={System.IO.Path.GetFileName(GuidePath)}";

        private const int SampleRate = 22050;

        /// <summary>Creates every placeholder that is not already there, and imports what is.</summary>
        public static void Ensure()
        {
            Directory.CreateDirectory(Folder);
            EnsurePlaceholders();
            AssetDatabase.Refresh();
            ConfigureImports();
            Debug.Log($"[AUDIO] {Report()}");
        }

        private static void EnsurePlaceholders()
        {

            // ⚠ A LOOP, and it has to end where it begins or the music clicks once a bar. Whole
            // numbers of cycles at every frequency is what makes the seam silent.
            EnsureClip(PlaceholderMusic, 4f, (t, length) =>
            {
                float bar = t / length;
                float root = 110f;
                float third = 138.6f;
                float fifth = 164.8f;

                float swell = 0.5f + 0.5f * Mathf.Sin(bar * Mathf.PI * 2f);

                return 0.22f * (
                    Mathf.Sin(t * root * Mathf.PI * 2f) +
                    0.6f * swell * Mathf.Sin(t * third * Mathf.PI * 2f) +
                    0.4f * Mathf.Sin(t * fifth * Mathf.PI * 2f)) / 2f;
            });

            // A soft thud: low, and gone quickly. ⓘ Its LOUDNESS is not baked in — §27 makes that
            // depend on the impact force, and GameAudio applies it per collision.
            EnsureClip(PlaceholderImpact, 0.18f, (t, length) =>
            {
                float decay = Mathf.Exp(-t / 0.045f);
                float body = Mathf.Sin(t * 150f * Mathf.PI * 2f);
                float grit = (Mathf.PerlinNoise(t * 900f, 0f) - 0.5f) * 2f;

                return decay * (0.75f * body + 0.25f * grit);
            });

            // A short upward blip, so a launch reads as a release rather than a hit.
            EnsureClip(PlaceholderLaunch, 0.22f, (t, length) =>
            {
                float decay = Mathf.Exp(-t / 0.07f);
                float rise = Mathf.Lerp(240f, 620f, Mathf.Clamp01(t / length));

                return decay * 0.8f * Mathf.Sin(t * rise * Mathf.PI * 2f);
            });

            // ⚠ The guide tone LOOPS under the whole of an aim, so it is a plain steady note:
            // anything with a shape to it would repeat every fraction of a second and nag.
            // ⓘ §27 has its pitch change with drag force, and GameAudio does that to the source.
            EnsureClip(PlaceholderGuide, 0.5f, (t, length) =>
            {
                float note = 440f;
                return 0.5f * (Mathf.Sin(t * note * Mathf.PI * 2f)
                               + 0.25f * Mathf.Sin(t * note * 2f * Mathf.PI * 2f));
            });

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Imports the owner's music as a compressed stream, and the cues as short decompressed clips.
        /// </summary>
        /// <remarks>
        /// ⚠ The owner's background track is a 33 MB WAV (D-096). Left at Unity's default it would
        /// be decompressed into memory whole on the phone; streamed as Vorbis it costs a few
        /// hundred kilobytes of buffer. The short cues stay decompressed so they start instantly.
        /// Idempotent — only changed when a setting differs.
        /// </remarks>
        public static void ConfigureImports()
        {
            Configure(MusicPath, AudioClipLoadType.Streaming, AudioCompressionFormat.Vorbis, 0.6f);
            Configure(ImpactPath, AudioClipLoadType.DecompressOnLoad, AudioCompressionFormat.PCM, 1f);
            Configure(LaunchPath, AudioClipLoadType.DecompressOnLoad, AudioCompressionFormat.PCM, 1f);
            Configure(GuidePath, AudioClipLoadType.DecompressOnLoad, AudioCompressionFormat.PCM, 1f);
        }

        private static void Configure(string path, AudioClipLoadType load, AudioCompressionFormat format, float quality)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;

            if (importer == null)
            {
                return;
            }

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;

            if (settings.loadType == load && settings.compressionFormat == format
                && Mathf.Abs(settings.quality - quality) < 0.01f && importer.forceToMono == false)
            {
                return;
            }

            settings.loadType = load;
            settings.compressionFormat = format;
            settings.quality = quality;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = false;
            importer.SaveAndReimport();
            Debug.Log($"[AUDIO] {path}: {load}, {format}.");
        }

        private static void EnsureClip(string path, float seconds, Func<float, float, float> shape)
        {
            if (File.Exists(path))
            {
                return;
            }

            int samples = Mathf.CeilToInt(seconds * SampleRate);
            var pcm = new short[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float value = Mathf.Clamp(shape(t, seconds), -1f, 1f);

                pcm[i] = (short)(value * short.MaxValue * 0.9f);
            }

            File.WriteAllBytes(path, Wav(pcm));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            Debug.Log($"[AUDIO] Created {path} ({seconds:0.00}s placeholder).");
        }

        /// <summary>Wraps 16-bit mono PCM in the smallest valid WAV container.</summary>
        private static byte[] Wav(short[] pcm)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            int dataBytes = pcm.Length * 2;

            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataBytes);
            writer.Write(new[] { 'W', 'A', 'V', 'E' });

            writer.Write(new[] { 'f', 'm', 't', ' ' });
            writer.Write(16);                       // PCM header length
            writer.Write((short)1);                 // uncompressed
            writer.Write((short)1);                 // mono
            writer.Write(SampleRate);
            writer.Write(SampleRate * 2);           // bytes per second
            writer.Write((short)2);                 // block align
            writer.Write((short)16);                // bits per sample

            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(dataBytes);

            foreach (short sample in pcm)
            {
                writer.Write(sample);
            }

            writer.Flush();
            return stream.ToArray();
        }
    }
}
