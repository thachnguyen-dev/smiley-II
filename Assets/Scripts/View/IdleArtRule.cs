using UnityEngine;

namespace Smiley.View
{
    /// <summary>
    /// Where a character frame's pivot goes so his feet stay on the platform (D-092).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ Owner, 2026-09-12: <i>"the character consistently stays visually in contact with the
    /// platform across all idle frames, without changing the intended character artwork or
    /// platform collision behavior."</i> The frames are drawn with the body at different heights
    /// on the canvas (rows 7 to 26 of 180), so a pivot at one fixed canvas point reproduced that
    /// drift as floating. The rule here anchors each frame by <b>its own lowest SOLID row</b>.
    /// The <c>Art</c> object that draws him sits at the bottom of the physics circle, so every
    /// frame's lowest visible pixel lands on the collider's bottom edge, whatever height it was
    /// drawn at.
    /// </para>
    /// <para>
    /// ⛔ <b>Solid, not "any alpha" (D-093).</b> The first version anchored on the trimmed rect's
    /// bottom — its lowest pixel with any alpha at all — and the owner still saw floating in some
    /// clips. Measured: the frames carry faint pixels (alpha 1–199, a soft shadow or stray
    /// anti-aliasing) up to <b>21 rows</b> below the visible body, varying frame to frame, so the
    /// rect's bottom was under the platform while the body hovered. The anchor is now the lowest
    /// row with at least <see cref="MinSolidPixels"/> pixels at alpha ≥ <see cref="SolidAlpha"/>.
    /// </para>
    /// <para>
    /// ⓘ x stays at one canvas point — the body's centre, <see cref="BodyCentreX"/> — so the
    /// trimmed rects widening and narrowing (the sprout swaying) do not shift him sideways.
    /// </para>
    /// </remarks>
    public static class IdleArtRule
    {
        /// <summary>The body's centre column in the owner's 160 px canvas (D-089).</summary>
        public const float BodyCentreX = 76.5f;

        /// <summary>The pixels-per-unit that makes the 125 px body one unit across (D-089).</summary>
        public const float PixelsPerUnit = 125f;

        /// <summary>Alpha at or above which a pixel is part of the visible body.</summary>
        public const byte SolidAlpha = 160;

        /// <summary>How many solid pixels a row needs to count as the body's bottom.</summary>
        public const int MinSolidPixels = 6;

        /// <summary>
        /// The lowest texture row inside <paramref name="rect"/> that is solidly painted, or the
        /// rect's bottom row if none is.
        /// </summary>
        /// <param name="pixels">The whole texture, bottom-left first (as <c>GetPixels32</c> returns).</param>
        public static int SolidBottomRow(Color32[] pixels, int textureWidth, RectInt rect)
        {
            for (int y = rect.yMin; y < rect.yMax; y++)
            {
                int solid = 0;

                for (int x = rect.xMin; x < rect.xMax; x++)
                {
                    if (pixels[y * textureWidth + x].a >= SolidAlpha && ++solid >= MinSolidPixels)
                    {
                        return y;
                    }
                }
            }

            return rect.yMin;
        }

        /// <summary>
        /// The normalised pivot for a sprite whose trimmed rect is <paramref name="rect"/> and
        /// whose lowest solid row is <paramref name="solidBottomRow"/>: that row, body-centre column.
        /// </summary>
        public static Vector2 PivotFor(Rect rect, int solidBottomRow)
        {
            float x = rect.width > 0f ? (BodyCentreX - rect.xMin) / rect.width : 0.5f;
            float y = rect.height > 0f ? Mathf.Clamp01((solidBottomRow - rect.yMin) / rect.height) : 0f;
            return new Vector2(x, y);
        }
    }
}
