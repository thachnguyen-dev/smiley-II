using UnityEngine;

namespace Smiley.Gameplay
{
    /// <summary>
    /// Marks a collider as a Platform — the only thing Smiley has gameplay physics
    /// interaction with (§10).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A typed marker rather than a layer, so <see cref="PlatformContact"/> can answer
    /// <i>which</i> platform Smiley is on and not merely <i>whether</i> he is on one. §14.2
    /// needs that — slippery friction applies "immediately upon contact", which means
    /// knowing what was touched at the moment of touching.
    /// </para>
    /// <para>
    /// ⛔ <b>This carries no platform type.</b> §14 has three — Normal, Slippery and
    /// Disappearing — and only Normal is in scope for this slice. Adding an unused enum now
    /// would be scaffolding for mechanics nobody has built, which is how a placeholder turns
    /// into content by accident.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Collider2D))]
    public sealed class Platform : MonoBehaviour
    {
    }
}
