using UnityEngine;

namespace Smiley.Gameplay
{
    /// <summary>
    /// Smiley's outline: a nearly-round blob that is deliberately not a circle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// §11: Smiley is <i>"NOT a mathematically perfect circle"</i> and is <i>"naturally and
    /// slightly asymmetrical"</i>. ⭐ <b>The owner ruled that this asymmetry reaches the
    /// physics, not only the sprite</b> — so the collider is built from this outline too,
    /// and Smiley rolls very slightly unevenly and prefers to settle in some orientations.
    /// </para>
    /// <para>
    /// ⛔ <b>The asymmetry is deterministic, from a fixed seed.</b> A shape re-rolled at
    /// runtime would mean two players — or the same player after a Resume (§21) — are rolling
    /// physically different characters, which nothing in the spec asks for and which would
    /// make a reported feel problem impossible to reproduce.
    /// </para>
    /// <para>
    /// ⚠ This is <b>geometry, not art</b>. §32 reserves Smiley's visual design to the owner;
    /// what this class fixes is the physical outline the body rolls on.
    /// </para>
    /// </remarks>
    public static class BlobShape
    {
        /// <summary>
        /// Builds the base outline, in local space, radius 1 before scaling.
        /// </summary>
        /// <param name="vertexCount">Points around the rim.</param>
        /// <param name="asymmetry">
        /// How far each rim point strays from a perfect circle, as a fraction of the radius.
        /// Zero gives a true circle. ⏸ Q-036, provisional.
        /// </param>
        /// <param name="seed">Fixes the shape. Changing it changes how Smiley rolls.</param>
        public static Vector2[] BuildBase(int vertexCount, float asymmetry, int seed)
        {
            vertexCount = Mathf.Max(6, vertexCount);
            var points = new Vector2[vertexCount];

            // Deliberately not UnityEngine.Random: this must not depend on, or disturb, any
            // other consumer of the global random state.
            var random = new System.Random(seed);

            // A low-frequency wobble rather than per-vertex noise. Independent jitter on each
            // point would give a spiky outline that catches on edges; two slow harmonics give
            // the gently-uneven blob §11 describes.
            float phaseA = (float)random.NextDouble() * Mathf.PI * 2f;
            float phaseB = (float)random.NextDouble() * Mathf.PI * 2f;

            for (int i = 0; i < vertexCount; i++)
            {
                float angle = (i / (float)vertexCount) * Mathf.PI * 2f;

                float wobble =
                    Mathf.Sin(angle * 2f + phaseA) * 0.6f +
                    Mathf.Sin(angle * 3f + phaseB) * 0.4f;

                float radius = 1f + wobble * asymmetry;
                points[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
            }

            return points;
        }

        /// <summary>
        /// Applies a deformation to a base outline.
        /// </summary>
        /// <remarks>
        /// Squashes along <paramref name="axisRadians"/> and stretches across it, by rotating
        /// each point into the axis's frame, scaling, and rotating back.
        /// <para>
        /// ⚠ <b><paramref name="axisRadians"/> must already be in the body's LOCAL frame.</b>
        /// A contact normal arrives in world space, and the body rotates while rolling (§13),
        /// so a world-space angle applied to a local-space path would make the squash spin
        /// around Smiley as he rolled.
        /// </para>
        /// </remarks>
        /// <param name="basePoints">The undeformed outline. Not modified.</param>
        /// <param name="into">Destination, same length as <paramref name="basePoints"/>.</param>
        /// <param name="pivot">
        /// The point the squash happens <b>around</b>, in the same local space as the outline.
        /// <see cref="Vector2.zero"/> squashes about the body's centre; a point on the rim
        /// squashes the body <i>into</i> that point.
        /// </param>
        public static void Deform(
            Vector2[] basePoints,
            Vector2[] into,
            float axisRadians,
            Vector2 scaleInAxisFrame,
            Vector2 pivot)
        {
            float cos = Mathf.Cos(axisRadians);
            float sin = Mathf.Sin(axisRadians);

            for (int i = 0; i < basePoints.Length; i++)
            {
                // ⚠ Relative to the PIVOT, not the centre. Scaling about the centre lifts the
                // contact edge off the surface — a landing ball would flatten by rising into
                // itself, which is not what a soft body does and not what it looks like.
                Vector2 p = basePoints[i] - pivot;

                // Into the axis frame: x along the compression axis, y across it.
                float along = p.x * cos + p.y * sin;
                float across = -p.x * sin + p.y * cos;

                along *= scaleInAxisFrame.x;
                across *= scaleInAxisFrame.y;

                // Back out, and back into local space.
                into[i] = new Vector2(
                    along * cos - across * sin,
                    along * sin + across * cos) + pivot;
            }
        }

        /// <summary>
        /// Pulls a deformed outline back inside its base shape, vertex by vertex.
        /// </summary>
        /// <remarks>
        /// <para>
        /// ⛔ <b>THE INVARIANT: A COLLIDER REBUILD MAY ONLY REMOVE MATERIAL, NEVER ADD IT.</b>
        /// </para>
        /// <para>
        /// Deforming grows the body across the squash axis. If that growth reaches into a
        /// platform, Box2D resolves the overlap the only way it can — by pushing the body out
        /// — and a shape that grows a little every frame becomes a slow, silent impulse. On
        /// hardware that showed up as Smiley drifting sideways at rest and then rising off the
        /// Start Area entirely (D-020).
        /// </para>
        /// <para>
        /// Clamping each vertex to its base radius makes an overlap impossible to *create* by
        /// rebuilding: the physical outline only ever shrinks toward the shape it already
        /// occupied, so the solver has nothing new to push against.
        /// </para>
        /// <para>
        /// ⓘ <b>The visual is untouched.</b> The sprite still stretches, so Smiley still reads
        /// as a soft balloon; it is only the collider that declines to claim space it did not
        /// already hold. §11 explicitly waives strict volume conservation, so nothing in the
        /// specification requires the physical body to bulge.
        /// </para>
        /// </remarks>
        public static void ClampWithinBase(Vector2[] basePoints, Vector2[] points)
        {
            for (int i = 0; i < points.Length; i++)
            {
                float baseRadius = basePoints[i].magnitude;
                float radius = points[i].magnitude;

                if (radius > baseRadius && radius > 0f)
                {
                    points[i] *= baseRadius / radius;
                }
            }
        }

        /// <summary>
        /// Where the squash should be centred for a contact arriving along
        /// <paramref name="axisRadians"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// ⭐ <b>Owner instruction, 2026-09-10:</b> <i>"When the player lands from above, the
        /// deformation center should move downward toward the contact point. Likewise, for
        /// contacts from other directions, the deformation center should be positioned close
        /// to the actual collision/contact location."</i>
        /// </para>
        /// <para>
        /// The contact sits on the rim, opposite the direction the surface pushes: a platform
        /// underfoot pushes <b>up</b>, and the contact is at the <b>bottom</b> of the body. So
        /// the pivot runs from the centre toward that rim point as
        /// <paramref name="bias"/> goes 0 → 1.
        /// </para>
        /// </remarks>
        /// <param name="axisRadians">Direction the contact force pushes, in local space.</param>
        /// <param name="radius">Body radius.</param>
        /// <param name="bias">0 = centre (the old behaviour), 1 = the contact point itself.</param>
        /// <summary>
        /// How far the artwork must move so that a scale happens about <paramref name="pivot"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A transform has no pivot, so scaling about one is expressed as a scale about the
        /// origin plus a translation: <c>x ↦ M(x − p) + p = Mx + (p − Mp)</c>. This returns that
        /// <c>p − Mp</c>, where <c>M = R·S·R⁻¹</c> is the map the deformation frame and its
        /// counter-rotated child compose to.
        /// </para>
        /// <para>
        /// ⛔ <b>GENERAL, because the special case was load-bearing and stopped being true.</b>
        /// When the pivot lies ALONG the squash axis the whole thing collapses to
        /// <c>p · (1 − scaleX)</c>, and that shortcut was used everywhere — correctly, while
        /// every pivot was a contact point on the deformation axis. ⚠ The drag squeeze put the
        /// pivot on the GROUND while squashing along the FINGER's axis, which is a different
        /// direction, and the shortcut then computed an offset for a pivot nobody was using:
        /// Smiley's artwork lifted off the platform he was resting on.
        /// </para>
        /// </remarks>
        /// <param name="pivot">The point to scale about, in the body's own space.</param>
        /// <param name="axisRadians">The squash axis, in the body's own space.</param>
        /// <param name="scale">x along the axis, y across it.</param>
        public static Vector2 PivotOffset(Vector2 pivot, float axisRadians, Vector2 scale)
        {
            float cos = Mathf.Cos(axisRadians);
            float sin = Mathf.Sin(axisRadians);

            // Into the axis's own frame.
            var local = new Vector2(
                pivot.x * cos + pivot.y * sin,
                -pivot.x * sin + pivot.y * cos);

            local.x *= scale.x;
            local.y *= scale.y;

            // And back out of it.
            var mapped = new Vector2(
                local.x * cos - local.y * sin,
                local.x * sin + local.y * cos);

            return pivot - mapped;
        }

        public static Vector2 ContactPivot(float axisRadians, float radius, float bias)
        {
            float reach = radius * Mathf.Clamp01(bias);
            return new Vector2(-Mathf.Cos(axisRadians) * reach, -Mathf.Sin(axisRadians) * reach);
        }
    }
}
