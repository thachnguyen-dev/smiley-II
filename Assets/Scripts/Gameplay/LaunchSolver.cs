using UnityEngine;

namespace Smiley.Gameplay
{
    /// <summary>The launch a drag currently describes.</summary>
    public readonly struct LaunchSolution
    {
        /// <summary>Unit vector Smiley will travel along. Opposite the drag (§6).</summary>
        public readonly Vector2 Direction;

        /// <summary>Impulse magnitude, from zero up to the configured maximum.</summary>
        public readonly float Force;

        /// <summary>
        /// How far through the drag range the finger is, 0 to 1, already clamped.
        /// Reaching 1 is §6's Maximum Drag Distance: dragging farther changes nothing.
        /// </summary>
        public readonly float Charge;

        /// <summary>
        /// False when the drag is too short to describe a direction. A zero-length drag
        /// has no opposite, and normalising it would produce a NaN rather than an aim.
        /// </summary>
        public readonly bool HasAim;

        internal LaunchSolution(Vector2 direction, float force, float charge, bool hasAim)
        {
            Direction = direction;
            Force = force;
            Charge = charge;
            HasAim = hasAim;
        }

        /// <summary>A drag that describes no launch yet.</summary>
        public static LaunchSolution None => new LaunchSolution(Vector2.zero, 0f, 0f, false);
    }

    /// <summary>
    /// Turns a drag into a launch. §6, as read by D-009.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⛔ <b>This class never receives time, and that is the point.</b> §6 requires that
    /// <i>"the same final drag distance must produce the same Launch Force regardless of
    /// how quickly the player dragged"</i>. Because the only inputs here are two positions,
    /// drag speed <b>cannot</b> influence the result — the rule holds structurally rather
    /// than by anyone remembering to obey it. Adding a velocity or delta-time parameter to
    /// this method would silently reopen the possibility.
    /// </para>
    /// <para>
    /// ⚠ <b>The origin is the touch-down point, not Smiley (D-009).</b> Force is how far
    /// the finger has travelled since it landed, so where on Smiley the player grabbed makes
    /// no difference, and force always begins at zero. The consequence is that the drawn
    /// guide is <b>not</b> the line from Smiley to the finger — see
    /// <see cref="Smiley.View.TrajectoryGuide"/>.
    /// </para>
    /// <para>
    /// ⏸ The distance → force mapping is <b>Q-007, undecided</b>. It is linear here, which
    /// is provisional and mine, not an owner decision.
    /// </para>
    /// </remarks>
    public static class LaunchSolver
    {
        /// <summary>
        /// Below this many units of drag there is no meaningful direction. Guards the
        /// normalise; not a gameplay dead zone.
        /// </summary>
        private const float MinimumAimDistance = 0.0001f;

        /// <summary>
        /// Solves the launch for a drag in progress.
        /// </summary>
        /// <param name="touchDown">Where the finger landed.</param>
        /// <param name="current">Where the finger is now.</param>
        /// <param name="maxDragDistance">
        /// §6's Maximum Drag Distance, in the same units as the two points. Dragging
        /// beyond it neither moves the aim further nor adds force.
        /// </param>
        /// <param name="maxForce">Impulse produced at full charge.</param>
        public static LaunchSolution Solve(
            Vector2 touchDown, Vector2 current, float maxDragDistance, float maxForce)
        {
            if (maxDragDistance <= 0f)
            {
                return LaunchSolution.None;
            }

            Vector2 drag = current - touchDown;
            float distance = drag.magnitude;

            if (distance < MinimumAimDistance)
            {
                return LaunchSolution.None;
            }

            // §6: the launch is opposite the drag, over a full 360°. No axis is special
            // and no direction is excluded — up, down, left, right and every diagonal.
            Vector2 direction = -drag / distance;

            // §6: the player "cannot drag farther" and force "cannot increase further".
            // Clamping the distance satisfies both at once.
            float charge = Mathf.Clamp01(distance / maxDragDistance);

            return new LaunchSolution(direction, charge * maxForce, charge, true);
        }

        /// <summary>
        /// Where the finger effectively is once §6's clamp is applied — the point the drag
        /// behaves as though it ended at.
        /// </summary>
        /// <remarks>
        /// Presentation needs this to show the player that they have hit the limit. Without
        /// it, a drag past the maximum would keep the visuals moving while the force stayed
        /// still, which reads as the control having stopped responding.
        /// </remarks>
        public static Vector2 ClampedDragPoint(Vector2 touchDown, Vector2 current, float maxDragDistance)
        {
            Vector2 drag = current - touchDown;
            float distance = drag.magnitude;

            if (distance <= maxDragDistance || distance < MinimumAimDistance)
            {
                return current;
            }

            return touchDown + (drag / distance) * maxDragDistance;
        }
    }
}
