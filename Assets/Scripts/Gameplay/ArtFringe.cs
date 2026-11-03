using UnityEngine;

namespace Smiley.Gameplay
{
    /// <summary>
    /// Finds how many rows and columns of a sprite are fringe — grass tufts, a glow, a soft
    /// edge — before the solid body of the picture begins.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ D-084's measurement, as arithmetic over pixels so it can be tested on a texture made
    /// in memory. A row is <b>solid</b> when at least <see cref="SolidCoverage"/> of its pixels
    /// are opaque; the fringe on an edge is the count of rows before the first solid one.
    /// </para>
    /// <para>
    /// ⓘ Why coverage and not "any opaque pixel": the sprite's rectangle is already trimmed to
    /// the opaque bounding box, so the first row always has <i>some</i> paint in it — the tips
    /// of the tufts. The question is where the surface is, and a surface is a row that is
    /// mostly there.
    /// </para>
    /// </remarks>
    public static class ArtFringe
    {
        /// <summary>Fraction of a row or column that must be opaque to count as the solid edge.</summary>
        public const float SolidCoverage = 0.85f;

        /// <summary>Alpha at or above which a pixel counts as opaque.</summary>
        public const byte OpaqueAlpha = 128;

        /// <summary>
        /// Fringe rows/columns on each edge of <paramref name="rect"/> within a texture's pixels.
        /// </summary>
        /// <param name="pixels">The whole texture, bottom-left first, row-major (as <c>GetPixels32</c> returns).</param>
        /// <param name="textureWidth">The texture's width.</param>
        /// <param name="rect">The sprite's rectangle inside the texture, in pixels.</param>
        public static (int top, int bottom, int left, int right) Measure(
            Color32[] pixels, int textureWidth, RectInt rect)
        {
            int top = 0, bottom = 0, left = 0, right = 0;

            // Top: rows from the highest downward. Texture rows count up from the bottom.
            for (int y = rect.yMax - 1; y >= rect.yMin; y--, top++)
            {
                if (RowIsSolid(pixels, textureWidth, rect, y)) break;
            }

            for (int y = rect.yMin; y < rect.yMax; y++, bottom++)
            {
                if (RowIsSolid(pixels, textureWidth, rect, y)) break;
            }

            for (int x = rect.xMin; x < rect.xMax; x++, left++)
            {
                if (ColumnIsSolid(pixels, textureWidth, rect, x)) break;
            }

            for (int x = rect.xMax - 1; x >= rect.xMin; x--, right++)
            {
                if (ColumnIsSolid(pixels, textureWidth, rect, x)) break;
            }

            // A picture with no solid row at all is all fringe; report nothing rather than
            // shrinking the collider to nothing.
            if (top >= rect.height || bottom >= rect.height) { top = 0; bottom = 0; }
            if (left >= rect.width || right >= rect.width) { left = 0; right = 0; }

            return (top, bottom, left, right);
        }

        private static bool RowIsSolid(Color32[] pixels, int textureWidth, RectInt rect, int y)
        {
            int opaque = 0;

            for (int x = rect.xMin; x < rect.xMax; x++)
            {
                if (pixels[y * textureWidth + x].a >= OpaqueAlpha) opaque++;
            }

            return opaque >= rect.width * SolidCoverage;
        }

        private static bool ColumnIsSolid(Color32[] pixels, int textureWidth, RectInt rect, int x)
        {
            int opaque = 0;

            for (int y = rect.yMin; y < rect.yMax; y++)
            {
                if (pixels[y * textureWidth + x].a >= OpaqueAlpha) opaque++;
            }

            return opaque >= rect.height * SolidCoverage;
        }
    }
}
