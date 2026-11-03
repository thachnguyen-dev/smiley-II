using Smiley.Config;
using UnityEngine;

namespace Smiley.Gameplay
{
    /// <summary>
    /// §14.2's Slippery Platform: lower friction, so Smiley slides farther.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>§14.2's "applies immediately upon contact" needs no code at all.</b> Friction is a
    /// property of the contact, and the solver reads both colliders' materials at the moment
    /// they touch — so the example in the spec, <i>"if Smiley is rolling on a Normal Platform
    /// and touches a Slippery Platform, slippery physics applies immediately at the moment of
    /// contact"</i>, is what a per-collider material already does. A component that watched for
    /// the transition and swapped something would be slower, later, and wrong.
    /// </para>
    /// <para>
    /// ⛔ <b>It does NOT modify Launch Force</b> (§14.2). Nothing here touches the launch, and
    /// nothing should: the drag is measured in screen space (D-009) and never consults what
    /// Smiley is standing on.
    /// </para>
    /// <para>
    /// ⓘ It is still a <see cref="Platform"/> — §14 makes Slippery a <i>type</i> of platform,
    /// so §8's Ready rule and §10's collision apply to it exactly as they do to a Normal one.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Platform))]
    [RequireComponent(typeof(Collider2D))]
    public sealed class SlipperyPlatform : MonoBehaviour
    {
        [SerializeField] private SmileyTuning _tuning;

        private void Awake()
        {
            if (_tuning == null)
            {
                return;
            }

            // A private material instance: sharing one would drag every other platform's
            // friction along with any future change to this.
            var collider = GetComponent<Collider2D>();
            collider.sharedMaterial = new PhysicsMaterial2D("Slippery (runtime)")
            {
                friction = _tuning.SlipperyFriction,
                bounciness = 0f,
            };
        }
    }
}
