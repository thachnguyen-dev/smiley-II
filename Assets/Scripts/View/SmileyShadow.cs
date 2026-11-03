using Smiley.Config;
using Smiley.Gameplay;
using UnityEngine;

namespace Smiley.View
{
    /// <summary>
    /// Draws Smiley's shadow on the platform below him.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ Owner, 2026-09-11 (D-083, D-085). Every frame a ray goes straight down from a point a
    /// little to the right of Smiley's centre; the nearest solid <see cref="Platform"/> it meets
    /// gets the shadow, placed on its top surface at that x, sized and faded by
    /// <see cref="ShadowRule"/> from the height of the drop. No platform within range: no shadow.
    /// </para>
    /// <para>
    /// ⛔ <b>Purely visual.</b> The shadow is a child <see cref="SpriteRenderer"/> with no collider,
    /// on a child that is re-positioned in <c>LateUpdate</c> after physics and the camera have
    /// had their say. It reads the physics world through a raycast and writes nothing back.
    /// The picture is the owner's to replace (§32): swap the sprite on the <c>Shadow</c> child.
    /// </para>
    /// <para>
    /// ⓘ Triggers (the Top Checkpoint) and non-platform colliders (the boundaries) are skipped:
    /// a shadow on an invisible wall would be a shadow on nothing.
    /// </para>
    /// </remarks>
    public sealed class SmileyShadow : MonoBehaviour
    {
        [SerializeField] private SmileyTuning _tuning;
        [SerializeField] private Rigidbody2D _body;
        [SerializeField] private SpriteRenderer _renderer;

        private static readonly RaycastHit2D[] Hits = new RaycastHit2D[16];

        /// <summary>The platform currently shadowed, or null. For tests.</summary>
        public Platform Under { get; private set; }

        /// <summary>Height from Smiley's underside to the shadowed surface. For tests.</summary>
        public float Height { get; private set; }

        private void LateUpdate()
        {
            if (_body == null || _renderer == null)
            {
                return;
            }

            float radius = _tuning != null ? _tuning.BodyRadius : 0.5f;
            float range = _tuning != null ? _tuning.ShadowRange : 6f;

            // ⭐ D-085: the shadow sits a little to the right of him, as if the light came from the
            // upper left. The ray goes down from THAT x, so the shadow lands on whatever is
            // under the shadow, not under him — at a platform's right edge those differ.
            float offset = radius * 2f * (_tuning != null ? _tuning.ShadowOffsetX : 0.18f);
            var origin = new Vector2(_body.position.x + offset, _body.position.y);
            int count = Physics2D.RaycastNonAlloc(origin, Vector2.down, Hits, range + radius);

            Platform nearest = null;
            float nearestY = float.NegativeInfinity;

            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = Hits[i];

                if (hit.collider == null || hit.collider.isTrigger || !hit.collider.enabled)
                {
                    continue;
                }

                var platform = hit.collider.GetComponent<Platform>();

                // The ray starts inside Smiley and would report his own collider first.
                if (platform == null || hit.point.y > origin.y)
                {
                    continue;
                }

                if (hit.point.y > nearestY)
                {
                    nearestY = hit.point.y;
                    nearest = platform;
                }
            }

            Under = nearest;

            if (nearest == null)
            {
                _renderer.enabled = false;
                return;
            }

            Height = Mathf.Max(0f, origin.y - radius - nearestY);

            if (!ShadowRule.InRange(Height, range))
            {
                Under = null;
                _renderer.enabled = false;
                return;
            }

            (float scale, float alpha) = ShadowRule.Evaluate(
                Height, range,
                _tuning != null ? _tuning.ShadowMinScale : 0.35f,
                _tuning != null ? _tuning.ShadowAlphaNear : 0.45f,
                _tuning != null ? _tuning.ShadowAlphaFar : 0.08f);

            float width = radius * 2f * (_tuning != null ? _tuning.ShadowWidth : 0.9f);
            float height = radius * 2f * (_tuning != null ? _tuning.ShadowHeight : 0.28f);

            // ⚠ World position and rotation, set directly: the child would otherwise turn with
            // Smiley as he rolls, and a shadow is flat on the ground whatever he is doing.
            transform.SetPositionAndRotation(
                new Vector3(origin.x, nearestY + 0.005f, 0f), Quaternion.identity);
            transform.localScale = new Vector3(width * scale, height * scale, 1f);

            Color colour = _renderer.color;
            colour.a = alpha;
            _renderer.color = colour;
            _renderer.enabled = true;
        }
    }
}
