using Smiley.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Smiley.Editor.Setup
{
    /// <summary>
    /// Measures each palette sprite's fringe and writes it into its <see cref="PlatformShape"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ D-084. Runs in every build, before the map guard. A palette prefab whose Visual sprite
    /// is the one it was last measured on is left alone; one whose sprite changed — the owner
    /// dropped in new art — is measured again and saved. ⓘ The only field a build ever writes
    /// into the owner's prefabs, and it is a derived value, like the physics material's friction.
    /// </para>
    /// <para>
    /// ⚠ Reading pixels needs a readable texture, and imported art is not readable by default.
    /// The importer is switched to readable for the measurement and switched back, so the
    /// shipping texture keeps its memory footprint. Three sprites, two reimports each, once
    /// per art change — not per build.
    /// </para>
    /// </remarks>
    public static class MapArtFit
    {
        /// <summary>Measures every palette prefab whose art changed. Returns false if one could not be read.</summary>
        public static bool FitPalette()
        {
            bool ok = true;

            foreach (string path in new[] { MapAssets.NormalPrefab, MapAssets.SlipperyPrefab, MapAssets.DisappearingPrefab })
            {
                ok &= Fit(path);
            }

            AssetDatabase.SaveAssets();
            return ok;
        }

        private static bool Fit(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
            {
                return true;
            }

            var shape = prefab.GetComponent<PlatformShape>();
            SpriteRenderer visual = shape != null ? shape.Visual : null;
            Sprite sprite = visual != null ? visual.sprite : null;

            if (shape == null || sprite == null)
            {
                Debug.LogError($"[ART-FIT] {path} has no PlatformShape with a Visual sprite.");
                return false;
            }

            if (shape.FittedSprite == sprite)
            {
                return true;
            }

            (int top, int bottom, int left, int right)? fringe = Measure(sprite);

            if (fringe == null)
            {
                return false;
            }

            using (var scope = new PrefabUtility.EditPrefabContentsScope(path))
            {
                scope.prefabContentsRoot.GetComponent<PlatformShape>()
                    .SetFringe(fringe.Value.top, fringe.Value.bottom, fringe.Value.left, fringe.Value.right, sprite);
            }

            Debug.Log(
                $"[ART-FIT] {path}: '{sprite.name}' fringe top {fringe.Value.top} / bottom " +
                $"{fringe.Value.bottom} / left {fringe.Value.left} / right {fringe.Value.right} rows " +
                $"of {sprite.rect.height}x{sprite.rect.width}. The collider sits that far inside the picture.");

            return true;
        }

        /// <summary>Reads the sprite's pixels, making the texture readable for the moment if it is not.</summary>
        public static (int top, int bottom, int left, int right)? Measure(Sprite sprite)
        {
            // Read before any reimport: the sprite object is re-created by it.
            Rect r = sprite.rect;
            var rect = new RectInt(
                Mathf.RoundToInt(r.xMin), Mathf.RoundToInt(r.yMin),
                Mathf.RoundToInt(r.width), Mathf.RoundToInt(r.height));

            Texture2D texture = sprite.texture;
            string texturePath = AssetDatabase.GetAssetPath(texture);
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            bool wasReadable = texture.isReadable;

            if (!wasReadable)
            {
                if (importer == null)
                {
                    Debug.LogError($"[ART-FIT] '{sprite.name}' is not readable and has no importer to change.");
                    return null;
                }

                importer.isReadable = true;
                importer.SaveAndReimport();
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            }

            try
            {
                Color32[] pixels = texture.GetPixels32();
                return ArtFringe.Measure(pixels, texture.width, rect);
            }
            finally
            {
                if (!wasReadable && importer != null)
                {
                    importer.isReadable = false;
                    importer.SaveAndReimport();
                }
            }
        }
    }
}
