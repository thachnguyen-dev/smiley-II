using Smiley.Config;
using UnityEngine;

namespace Smiley.View
{
    /// <summary>
    /// Mirrors the character's picture to face the way he is moving.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ Owner, 2026-09-12 (D-095). <see cref="FacingRule"/> decides; this applies it to the
    /// <c>Art</c> renderer as <see cref="SpriteRenderer.flipX"/>. The flip is about the sprite's
    /// pivot, which sits on the body's centre column (D-089), so mirroring moves nothing — his
    /// feet stay on the platform (D-092/D-093) and the clips play on unchanged.
    /// </para>
    /// <para>
    /// ⛔ Visual only. Reads the body's velocity; writes one renderer flag. Nothing in gameplay
    /// reads it.
    /// </para>
    /// </remarks>
    public sealed class Facing : MonoBehaviour
    {
        [SerializeField] private SmileyTuning _tuning;
        [SerializeField] private Rigidbody2D _body;
        [SerializeField] private SpriteRenderer _renderer;

        /// <summary>Whether the picture is currently mirrored (facing left). For tests.</summary>
        public bool IsMirrored { get; private set; }

        private void LateUpdate()
        {
            if (_body == null || _renderer == null)
            {
                return;
            }

            float threshold = _tuning != null ? _tuning.FacingSpeedThreshold : 0.3f;
            bool mirror = FacingRule.ShouldMirror(_body.linearVelocity.x, threshold, IsMirrored);

            if (mirror != IsMirrored || _renderer.flipX != mirror)
            {
                IsMirrored = mirror;
                _renderer.flipX = mirror;
            }
        }
    }
}
