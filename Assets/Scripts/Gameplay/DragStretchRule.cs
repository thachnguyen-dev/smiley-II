using UnityEngine;

namespace Smiley.Gameplay
{
    /// <summary>
    /// How far the artwork stretches along a drag in progress (D-035).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Owner instruction, 2026-09-10:</b> while the player is dragging, the visual image
    /// deforms in the direction of the drag; <b>only</b> the image; and the deformation is
    /// limited to a maximum amount.
    /// </para>
    /// <para>
    /// ⛔ <b>There is no Unity in here, which is the point.</b> The ceiling the owner asked for
    /// is a rule, so it lives where a rule can be tested directly rather than inferred from a
    /// transform in a running scene — the same split as <see cref="LaunchSolver"/>,
    /// <see cref="ReadyRule"/> and <see cref="PlatformCountdown"/>.
    /// </para>
    /// </remarks>
    public static class DragStretchRule
    {
        /// <summary>
        /// How far a drag of this strength should stretch the artwork.
        /// </summary>
        /// <param name="charge">
        /// How far through §6's Maximum Drag Distance the drag is, 0 to 1. ⓘ Already clamped by
        /// <see cref="LaunchSolver"/>: past the maximum the player cannot drag further and the
        /// force cannot rise, so the stretch stops growing at exactly the same point.
        /// </param>
        /// <param name="atFullDrag">Stretch produced by a full-strength drag. ⏸ Q-043.</param>
        /// <param name="maximum">
        /// ⭐ The ceiling the owner asked for. Applied as its own clamp rather than left to
        /// fall out of <paramref name="atFullDrag"/>, so it still holds if that is retuned —
        /// and so that "limited to a maximum amount" is a stated rule rather than a consequence.
        /// </param>
        public static float Target(float charge, float atFullDrag, float maximum)
        {
            float wanted = Mathf.Max(0f, charge) * Mathf.Max(0f, atFullDrag);

            return Mathf.Min(wanted, Mathf.Max(0f, maximum));
        }

        /// <summary>
        /// Moves the current stretch one step toward the target.
        /// </summary>
        /// <remarks>
        /// ⚠ An exponential approach rather than a per-frame lerp, so the stretch behaves the
        /// same on a 120 Hz phone as on a 60 Hz one — §33 requires that the display does not
        /// change the game. ⓘ Both directions ease: a drag that begins or ends abruptly would
        /// otherwise snap the artwork.
        /// </remarks>
        /// <param name="seconds">Roughly how long the stretch takes to follow the finger.</param>
        public static float Advance(float current, float target, float seconds, float deltaTime)
        {
            if (seconds <= 0f || deltaTime <= 0f)
            {
                return target;
            }

            float t = Mathf.Exp(-deltaTime / seconds);
            float next = target + (current - target) * t;

            // Settles exactly rather than approaching zero forever, so "not dragging" really is
            // the base shape and not an ever-smaller stretch.
            return next < 0.0002f && target <= 0f ? 0f : next;
        }
    }
}
