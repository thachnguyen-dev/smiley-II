using UnityEngine;

namespace Smiley.Gameplay
{
    /// <summary>
    /// How squashed Smiley is, and along which axis.
    /// </summary>
    /// <remarks>
    /// <para>
    /// §11's squash and stretch, held as state rather than played as an animation — which is
    /// what <i>"deformation is gameplay-relevant, not merely decorative animation"</i>
    /// requires. An animation clip would be a fixed shape over a fixed time; this is a
    /// quantity that responds to whatever force is actually acting.
    /// </para>
    /// <para>
    /// ⛔ <b>Two channels, not one, and the split is forced by the spec.</b> §11 says
    /// deformation affects physics; §12 says <i>"idle deformation is visual only… does not
    /// affect physics"</i>. So the collider is driven by <see cref="Magnitude"/> — collision
    /// and rolling — while the sprite is driven by that <b>plus</b> an idle term the physics
    /// never sees. Merging them would either make the idle wobble push Smiley around or
    /// make the collision squash decorative.
    /// </para>
    /// </remarks>
    public struct DeformationState
    {
        /// <summary>
        /// How squashed, 0 (base shape) upward. Compression along <see cref="AxisRadians"/>.
        /// </summary>
        /// <remarks>
        /// ⓘ §11: <i>"There is no fixed gameplay maximum deformation."</i> The clamp applied
        /// in <see cref="Advance"/> exists only for the reasons §11 permits — physics
        /// instability, visual errors, abnormally large values — and is deliberately loose.
        /// </remarks>
        public float Magnitude;

        /// <summary>
        /// World-space direction Smiley is being compressed along, in radians.
        /// </summary>
        /// <remarks>
        /// §11: <i>"Deformation follows the direction of the collision force."</i> Held in
        /// world space because that is the space a contact normal arrives in; the collider
        /// converts to local when it rebuilds, since the body rotates while rolling (§13).
        /// </remarks>
        public float AxisRadians;

        /// <summary>
        /// Advances the state one step toward what the current forces demand.
        /// </summary>
        /// <remarks>
        /// <para>
        /// §11 gives two behaviours that have to coexist. <i>"If the force continues,
        /// deformation continues updating"</i> — so a sustained demand holds the shape. And
        /// <i>"When the force ends, deformation gradually returns toward the natural base
        /// shape"</i> — so with no demand it decays rather than snapping back.
        /// </para>
        /// <para>
        /// ⚠ <b>Rising is immediate and falling is gradual, and that asymmetry is the
        /// spec's.</b> §11 says a collision causes <b>immediate</b> squash. An eased rise
        /// would read as mushy and would contradict that word, so a demand above the current
        /// magnitude is taken at once.
        /// </para>
        /// </remarks>
        /// <param name="demand">
        /// Magnitude the present forces call for — the larger of the decaying impact term
        /// and the rolling term.
        /// </param>
        /// <param name="demandAxisRadians">World-space axis the demand acts along.</param>
        /// <param name="deltaTime">Seconds since the last step.</param>
        /// <param name="recoverySeconds">
        /// Roughly how long an unforced return to base takes. ⏸ Q-034, provisional.
        /// </param>
        /// <param name="technicalLimit">
        /// The stability clamp §11 permits — not a gameplay maximum. ⏸ Q-035, provisional.
        /// </param>
        public void Advance(
            float demand,
            float demandAxisRadians,
            float deltaTime,
            float recoverySeconds,
            float technicalLimit)
        {
            demand = Mathf.Clamp(demand, 0f, technicalLimit);

            if (demand >= Magnitude)
            {
                // §11: a collision causes IMMEDIATE squash, and a continuing force keeps
                // updating it. Both are "take the demand now".
                Magnitude = demand;
                AxisRadians = demandAxisRadians;
                return;
            }

            // §11: "When the force ends, deformation gradually returns toward the natural
            // base shape." Exponential decay, so the return has no fixed duration and never
            // overshoots into a stretch the forces did not ask for.
            if (recoverySeconds <= 0f)
            {
                Magnitude = demand;
                return;
            }

            float t = Mathf.Exp(-deltaTime / recoverySeconds);
            Magnitude = demand + (Magnitude - demand) * t;

            // The axis is kept while decaying. Rotating it toward a weaker demand would
            // swing the squash sideways as it faded, which no force asked for.
            if (Magnitude <= 0.0001f)
            {
                Magnitude = 0f;
                AxisRadians = demandAxisRadians;
            }
        }

        /// <summary>
        /// The non-uniform scale this deformation represents, in the axis's own frame.
        /// </summary>
        /// <remarks>
        /// ⓘ §11: <i>"Smiley's total size may shrink or grow. Strict volume conservation is
        /// not required."</i> So the perpendicular stretch is an independent factor rather
        /// than whatever would preserve area — <paramref name="stretchRatio"/> below 1 lets
        /// a squashed Smiley lose size overall, above 1 lets him gain it.
        /// </remarks>
        /// <returns>x = along the compression axis, y = perpendicular to it.</returns>
        public Vector2 ScaleInAxisFrame(float stretchRatio)
        {
            return new Vector2(1f - Magnitude, 1f + Magnitude * stretchRatio);
        }
    }
}
