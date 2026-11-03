using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Smiley.View;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Smiley.Editor.Setup
{
    /// <summary>
    /// Anchors every sprite the idle clips use on its lowest SOLID row, so his feet stay on the platform (D-092, D-093).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Runs in every build, like <see cref="MapArtFit"/>. For each clip in the owner's idle
    /// list it reads the sprite keyframes, and for each texture those sprites come from it sets
    /// every sprite rect's pivot to <see cref="IdleArtRule.PivotFor"/> and the import to
    /// <see cref="IdleArtRule.PixelsPerUnit"/>, through the Sprite Editor's data provider — the
    /// same path the Sprite Editor window takes, so the result is what the owner would get by
    /// hand. ⓘ A texture already carrying the rule is not reimported.
    /// </para>
    /// <para>
    /// ⛔ Only the pivots and the scale move. The pixels, the rects, the clips and the collider
    /// are untouched — the owner's condition.
    /// </para>
    /// </remarks>
    public static class IdleArtFit
    {
        /// <summary>Written into each fitted sprite rect's custom data. Change it to force a re-measure.</summary>
        /// <summary>
        /// The marker a fitted rect carries: the rule's version plus a hash of the PNG's bytes.
        /// </summary>
        /// <remarks>
        /// ⛔ <b>v1 carried no hash, and that let replaced art ship on stale anchors (D-118).</b>
        /// The owner replaced all 72 idle frames on 2026-09-14; every one still carried the v1
        /// marker from the old pixels, so the fitter skipped them all, and the build re-anchored
        /// nothing — 67 frames two rows off and five never-fitted frames 35–47 px off, with the
        /// log silent because it only spoke when it changed something. The marker now names the
        /// bytes it measured, so new pixels under an old marker are simply unmarked.
        /// </remarks>
        public const string MarkerPrefix = "smiley-feet:solid-row:v2:";

        /// <summary>The marker for the PNG currently at <paramref name="path"/>.</summary>
        public static string MarkerFor(string path)
        {
            using (var md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(File.ReadAllBytes(path));
                return MarkerPrefix + System.BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        /// <summary>Fits every sprite referenced by the clips. Returns how many textures were reimported.</summary>
        public static int FitClips(IEnumerable<AnimationClip> clips)
        {
            var textures = new HashSet<Texture2D>();

            foreach (AnimationClip clip in clips)
            {
                if (clip == null)
                {
                    continue;
                }

                foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                {
                    if (binding.type != typeof(SpriteRenderer) || binding.propertyName != "m_Sprite")
                    {
                        continue;
                    }

                    foreach (ObjectReferenceKeyframe key in AnimationUtility.GetObjectReferenceCurve(clip, binding))
                    {
                        if (key.value is Sprite sprite && sprite.texture != null)
                        {
                            textures.Add(sprite.texture);
                        }
                    }
                }
            }

            int reimported = 0;

            foreach (Texture2D texture in textures)
            {
                if (Fit(texture))
                {
                    reimported++;
                }
            }

            if (reimported > 0)
            {
                AssetDatabase.SaveAssets();
            }

            // Always spoken, so a build that re-anchored nothing says so rather than nothing.
            Debug.Log($"[IDLE-FIT] {reimported} of {textures.Count} idle textures re-anchored to their bottom edge (D-092, D-118).");

            return reimported;
        }

        /// <summary>Applies the rule to one texture. Returns true if it had to change.</summary>
        public static bool Fit(Texture2D texture)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer == null)
            {
                Debug.LogError($"[IDLE-FIT] '{path}' has no texture importer.");
                return false;
            }

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);

            if (provider == null)
            {
                Debug.LogError($"[IDLE-FIT] '{path}' gives no sprite data provider.");
                return false;
            }

            provider.InitSpriteEditorDataProvider();
            SpriteRect[] rects = provider.GetSpriteRects();

            // ⓘ Cheap idempotence: a rect this fitter has already measured carries a marker naming
            // the bytes it measured, and a texture whose rects all carry the marker for the bytes
            // on disk now is left alone — reading pixels means two reimports. Replaced art has new
            // bytes, so its old marker no longer matches and it is measured again (D-118).
            string marker = MarkerFor(path);
            bool allMarked = Mathf.Abs(importer.spritePixelsPerUnit - IdleArtRule.PixelsPerUnit) < 1e-3f;

            foreach (SpriteRect rect in rects)
            {
                allMarked &= rect.customData == marker && rect.alignment == SpriteAlignment.Custom;
            }

            if (allMarked)
            {
                return false;
            }

            // D-093: the solid bottom needs pixels, and imported art is not readable by default.
            Color32[] pixels = ReadPixels(texture, importer, out int textureWidth);

            if (pixels == null)
            {
                return false;
            }

            // The reimports above rebuilt the importer's data; start the provider afresh.
            provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            rects = provider.GetSpriteRects();
            bool changed = false;

            foreach (SpriteRect rect in rects)
            {
                var px = new RectInt(
                    Mathf.RoundToInt(rect.rect.xMin), Mathf.RoundToInt(rect.rect.yMin),
                    Mathf.RoundToInt(rect.rect.width), Mathf.RoundToInt(rect.rect.height));
                int solidRow = IdleArtRule.SolidBottomRow(pixels, textureWidth, px);
                Vector2 wanted = IdleArtRule.PivotFor(rect.rect, solidRow);

                if (rect.alignment != SpriteAlignment.Custom || (rect.pivot - wanted).sqrMagnitude > 1e-8f || rect.customData != marker)
                {
                    rect.alignment = SpriteAlignment.Custom;
                    rect.pivot = wanted;
                    rect.customData = marker;
                    changed = true;
                }
            }

            if (Mathf.Abs(importer.spritePixelsPerUnit - IdleArtRule.PixelsPerUnit) > 1e-3f)
            {
                importer.spritePixelsPerUnit = IdleArtRule.PixelsPerUnit;
                changed = true;
            }

            if (!changed)
            {
                return false;
            }

            provider.SetSpriteRects(rects);
            provider.Apply();
            importer.SaveAndReimport();
            return true;
        }

        /// <summary>The texture's pixels, switching the importer to readable for the moment if needed.</summary>
        private static Color32[] ReadPixels(Texture2D texture, TextureImporter importer, out int width)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            bool wasReadable = texture.isReadable;
            width = 0;

            if (!wasReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }

            try
            {
                width = texture.width;
                return texture.GetPixels32();
            }
            finally
            {
                if (!wasReadable)
                {
                    importer.isReadable = false;
                    importer.SaveAndReimport();
                }
            }
        }
    }
}
