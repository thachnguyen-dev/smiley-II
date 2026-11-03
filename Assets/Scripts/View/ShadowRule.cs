using UnityEngine;

namespace Smiley.View
{
    /// <summary>
    /// How big and how dark Smiley's shadow is, from how far above the platform he is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ Owner, 2026-09-11 (D-083): <i>"a shadow effect from the player onto the platform … should
    /// follow the player's position relative to the platform and should not interfere with
    /// gameplay collision."</i> This is the arithmetic; <see cref="SmileyShadow"/> finds the
    /// platform and draws. Nothing here touches physics.
    /// </para>
    /// <para>
    /// ⓘ On the ground the shadow is full size and darkest; it shrinks and fades linearly with
    /// height until <c>range</c>, beyond which it is gone. Linear on purpose — it is read at a
    /// glance during a jump, and a curve would be tuning nobody asked for (Q-052, PROVISIONAL).
    /// </para>
    /// </remarks>
    public static class ShadowRule
    {
        /// <summary>The shadow's scale and opacity at a given height above the surface.</summary>
        /// <param name="height">Distance from the bottom of Smiley to the platform's top. Clamped at 0.</param>
        /// <param name="range">Height at which the shadow has faded out entirely.</param>
        /// <param name="minScale">Scale at the top of the range.</param>
        /// <param name="alphaNear">Opacity on the ground.</param>
        /// <param name="alphaFar">Opacity at the top of the range.</param>
        public static (float scale, float alpha) Evaluate(
            float height, float range, float minScale, float alphaNear, float alphaFar)
        {
            if (range <= 0f)
            {
                return (1f, alphaNear);
            }

            float t = Mathf.Clamp01(Mathf.Max(0f, height) / range);

            return (Mathf.Lerp(1f, minScale, t), Mathf.Lerp(alphaNear, alphaFar, t));
        }

        /// <summary>Whether a surface that far down still gets a shadow at all.</summary>
        public static bool InRange(float height, float range) => height <= range;
    }
}
