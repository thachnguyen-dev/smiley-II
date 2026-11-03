using UnityEngine;

namespace Smiley.Gameplay
{
    /// <summary>
    /// An invisible edge of the gameplay area. Smiley cannot pass it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Owner instruction, 2026-09-10:</b> <i>"There should be two invisible walls and two
    /// boundaries defining the gameplay area, so that the player cannot fall or leave the
    /// gameplay area."</i>
    /// </para>
    /// <para>
    /// ⛔ <b>A BOUNDARY IS NOT A PLATFORM, AND THE DISTINCTION IS LOAD-BEARING.</b> §8 makes
    /// Ready depend on <i>"contact with a Platform"</i>, so if the walls carried
    /// <see cref="Platform"/> then Smiley pressed against one — sliding down it, in mid-air —
    /// would become Ready and could re-launch off nothing. The owner named walls and
    /// boundaries as distinct from platforms, and keeping them distinct is what leaves §8
    /// exactly as written.
    /// </para>
    /// <para>
    /// ⚠ <b>This does amend §10</b>, which says Smiley has gameplay physics interaction
    /// <i>"only with Platforms"</i>. He now also collides with the edge of the world. That is
    /// the owner's instruction; it is recorded here rather than glossed.
    /// </para>
    /// <para>
    /// ⓘ Invisible by having no renderer at all, rather than a transparent one — there is
    /// nothing to draw and nothing to accidentally make visible later.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Collider2D))]
    public sealed class Boundary : MonoBehaviour
    {
    }
}
