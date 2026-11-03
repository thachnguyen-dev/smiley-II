using System;
using System.Collections.Generic;
using UnityEngine;

namespace Smiley.Save
{
    /// <summary>One Disappearing Platform's state (§14.3, §21).</summary>
    [Serializable]
    public sealed class PlatformState
    {
        /// <summary>Which platform this is. The scene is generated, so names are stable.</summary>
        public string Name;

        /// <summary>True while it is solid; false while it is gone.</summary>
        public bool Present;

        /// <summary>§21: "Remaining Disappearing Platform countdown time."</summary>
        public float Remaining;

        /// <summary>Seconds left before a vanished platform returns.</summary>
        public float RespawnRemaining;

        /// <summary>
        /// §21: "Whether the countdown is currently running or paused."
        /// </summary>
        /// <remarks>
        /// ⚠ <b>Listed separately in §21 from the remaining time, and it has to be.</b> A
        /// platform with 1.4 s left that is paused and one with 1.4 s left that is ticking are
        /// different situations, and restoring only the number would silently convert one into
        /// the other.
        /// </remarks>
        public bool Running;

        /// <summary>How long contact has been held, for §14.3's stability test.</summary>
        public float ContactHeld;
    }

    /// <summary>
    /// Everything §21 requires to resume accurately.
    /// </summary>
    /// <remarks>
    /// <para>
    /// §21 enumerates what must survive: <i>"Smiley position. Required physics state. Smiley
    /// deformation. Platform states. Remaining Disappearing Platform countdown time. Whether
    /// the countdown is currently running or paused. Any other gameplay state required for
    /// accurate restoration."</i>
    /// </para>
    /// <para>
    /// ⚠ <b>"Required physics state" is where D-002 costs something, and the spec knew it.</b>
    /// Under a float solver there is no way to restore a body bit-exactly, which is why §21
    /// asks for a resume <i>"as close as practical to the frame / state at which the game was
    /// saved"</i> rather than an identical one. Position, velocity, rotation and spin is what
    /// "as close as practical" means here.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class SaveData
    {
        /// <summary>Format version, so a future change can migrate rather than guess.</summary>
        public int Version = 1;

        // §21: "Smiley position" and "required physics state".
        public float SmileyX;
        public float SmileyY;
        public float VelocityX;
        public float VelocityY;
        public float Rotation;
        public float AngularVelocity;

        /// <summary>
        /// §21: "If saving during deformation, save the current deformation state."
        /// </summary>
        /// <remarks>
        /// ⓘ The <b>physical</b> deformation only. §12's idle wobble is visual and never
        /// reaches the collider (D-019), so it is not state — it is a function of the clock and
        /// regenerates itself on the next frame.
        /// </remarks>
        public float DeformMagnitude;

        /// <summary>World-space axis the squash acts along.</summary>
        public float DeformAxisRadians;

        /// <summary>§3, §21: platform states persist.</summary>
        public List<PlatformState> Platforms = new List<PlatformState>();

        /// <summary>
        /// D-012: the fact of completion outlives the state of a playthrough.
        /// </summary>
        /// <remarks>
        /// Three rules depend on it: §30's tutorial must not reappear on a replay, §20 lets the
        /// Story be watched again after later completions, and §22's Continue means a fresh
        /// climb rather than standing on the checkpoint.
        /// </remarks>
        public bool CompletedAtLeastOnce;

        /// <summary>
        /// Written last, and checked on load.
        /// </summary>
        /// <remarks>
        /// ⛔ <b>A truncated save can still be valid-looking, which is the failure this
        /// guards.</b> A file cut short mid-write may still parse into a
        /// <see cref="SaveData"/> with plausible zeros in it — a Smiley at the origin with no
        /// platforms — and restoring that is worse than having no save at all. The marker is
        /// the last field written, so its absence means the write did not finish.
        /// </remarks>
        public string EndMarker = Complete;

        /// <summary>The value <see cref="EndMarker"/> must hold for a save to be trusted.</summary>
        public const string Complete = "smiley-save-end";

        /// <summary>Whether this looks like a save that finished being written.</summary>
        public bool IsComplete() => EndMarker == Complete;
    }
}
