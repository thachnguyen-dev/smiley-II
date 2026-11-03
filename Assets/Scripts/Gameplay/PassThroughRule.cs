using UnityEngine;

namespace Smiley.Gameplay
{
    /// <summary>
    /// Which directions a Cloud platform is solid from (D-130).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⛔ <b>This amends §10</b>, which says in as many words: <i>"There are: No one-way
    /// platforms."</i> The owner changed that on 2026-10-10, for the Cloud (Disappearing)
    /// platform only, after the conflict was put to them twice. ⭐ <b>Normal and Slippery
    /// platforms are untouched and remain solid from every direction.</b>
    /// </para>
    /// <para>
    /// ⚠ <b>Unity's one-way platform is not quite "one-way".</b> A <c>PlatformEffector2D</c> is
    /// solid across an arc centred on its up vector and passable outside it, and the usual 180°
    /// makes the SIDES passable too — a glancing hit sails straight through. The owner asked for
    /// pass-through from <b>below only</b>, with sides still solid, so the arc is 270°: solid
    /// everywhere except a 90° cone pointing down.
    /// </para>
    /// <para>
    /// ⓘ This keeps §10's other promises intact — side collisions, corner collisions and
    /// unlimited collisions per launch all still hold. Only the underside changed.
    /// </para>
    /// </remarks>
    public static class PassThroughRule
    {
        /// <summary>Degrees of solid surface, centred on the platform's up vector.</summary>
        /// <remarks>
        /// 270° leaves a 90° cone below passable: straight up goes through, 45° from below goes
        /// through, and a horizontal hit on the end does not. ⚠ PROVISIONAL — it is a feel value,
        /// and the owner may want the window wider or narrower once it is on a phone (Q-058).
        /// </remarks>
        public const float CloudSurfaceArcDegrees = 270f;

        /// <summary>
        /// Whether a contact arriving with this surface normal should be solid.
        /// </summary>
        /// <param name="contactNormalDegrees">
        /// The contact normal's angle from the platform's up vector, 0 = hit from directly above.
        /// </param>
        /// <remarks>
        /// ⓘ Mirrors what the effector does, so the arc can be reasoned about and tested without
        /// a physics step. ⛔ It does not drive the physics — the effector does — so a change
        /// here means nothing until the prefab's arc changes with it.
        /// </remarks>
        public static bool IsSolid(float contactNormalDegrees, float surfaceArcDegrees)
        {
            float half = Mathf.Clamp(surfaceArcDegrees, 0f, 360f) * 0.5f;
            float fromUp = Mathf.Abs(Mathf.DeltaAngle(0f, contactNormalDegrees));
            return fromUp <= half;
        }

        /// <summary>Whether a Cloud platform is solid from this direction.</summary>
        public static bool CloudIsSolid(float contactNormalDegrees) =>
            IsSolid(contactNormalDegrees, CloudSurfaceArcDegrees);
    }
}
