using UnityEngine;

namespace Smiley.View
{
    /// <summary>
    /// Where the camera should be, given where Smiley is. Pure maths, no Unity objects.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠ <b>THE TWO AXES BEHAVE DIFFERENTLY ON PURPOSE (D-016).</b> §17 says the camera
    /// <i>"continuously follows Smiley vertically"</i> — that word is the specification's, and
    /// vertical follow is continuous. Horizontal follow is a <b>deadzone</b>: the view holds
    /// still while he moves within a central band and only pans when he nears its edge,
    /// because §9 has him rolling or sliding after almost every landing and tracking X
    /// continuously would slide the world under a character who is never quite still.
    /// </para>
    /// <para>
    /// ⛔ <b>Anyone "harmonising" the two axes would be reversing one of them</b>, either
    /// against §17's own wording or against D-016.
    /// </para>
    /// </remarks>
    public static class CameraSolver
    {
        /// <summary>
        /// The horizontal position the camera wants, applying D-016's deadzone.
        /// </summary>
        /// <remarks>
        /// Returns <paramref name="current"/> unchanged while Smiley is inside the band, so a
        /// caller can smooth toward the result without the view creeping on every frame.
        /// </remarks>
        /// <param name="current">Where the camera is now.</param>
        /// <param name="target">Smiley's x.</param>
        /// <param name="deadzoneHalfWidth">Half the width of the band he may move within.</param>
        public static float HorizontalGoal(float current, float target, float deadzoneHalfWidth)
        {
            float offset = target - current;

            if (Mathf.Abs(offset) <= deadzoneHalfWidth)
            {
                return current;
            }

            // Follows only as far as the band's edge, so he stays at the boundary rather than
            // being recentred every time he touches it — which would make the deadzone
            // pointless at any speed above a crawl.
            return target - Mathf.Sign(offset) * deadzoneHalfWidth;
        }

        /// <summary>
        /// Keeps a camera position inside the authored world, given what it can see.
        /// </summary>
        /// <remarks>
        /// ⛔ <b>Clamping beats centring (D-016).</b> At a map edge the camera cannot keep
        /// Smiley in the band's centre, and the edge wins — the view never shows beyond the
        /// authored world.
        /// <para>
        /// ⚠ When the world is <b>narrower than the view</b>, there is no position that both
        /// fills the screen and stays inside; the world is centred instead. Without that case
        /// the two clamps fight and the camera jitters between them.
        /// </para>
        /// </remarks>
        public static Vector2 ClampToBounds(Vector2 position, Rect world, Vector2 viewHalfExtents)
        {
            float x = ClampAxis(position.x, world.xMin, world.xMax, viewHalfExtents.x);
            float y = ClampAxis(position.y, world.yMin, world.yMax, viewHalfExtents.y);

            return new Vector2(x, y);
        }

        private static float ClampAxis(float value, float min, float max, float halfExtent)
        {
            float low = min + halfExtent;
            float high = max - halfExtent;

            if (low > high)
            {
                // The world is smaller than the window on this axis; centre it.
                return (min + max) * 0.5f;
            }

            return Mathf.Clamp(value, low, high);
        }

        /// <summary>
        /// A frame's worth of smoothing toward a goal, independent of frame rate.
        /// </summary>
        /// <remarks>
        /// ⚠ An exponential approach rather than a fixed step: a per-frame lerp factor makes
        /// the camera faster on a fast device and slower on a slow one, which is exactly the
        /// kind of display-dependent behaviour §33 rules out.
        /// </remarks>
        public static float Smooth(float current, float goal, float seconds, float deltaTime)
        {
            if (seconds <= 0f)
            {
                return goal;
            }

            float t = 1f - Mathf.Exp(-deltaTime / seconds);
            return Mathf.Lerp(current, goal, t);
        }
    }
}
