using UnityEngine;

namespace Smiley.Gameplay
{
    /// <summary>
    /// The picture behind the whole map. Covers the world, whatever the world's size.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ Owner, 2026-09-11 (D-082): <i>"The background should be part of the map/world and cover
    /// the intended map area … so I can replace the background image later without changing
    /// the gameplay systems."</i> It is a child of the Map prefab named <c>Background</c> with a
    /// <see cref="SpriteRenderer"/> and this component, which sizes and centres the renderer on
    /// the <see cref="LevelBounds"/> every time either changes. Swap the sprite; nothing else
    /// moves.
    /// </para>
    /// <para>
    /// ⓘ Two ways to fill 27 × 192 units with one image: <b>Stretch</b> draws it once across the
    /// whole world (a tall painting), <b>Tile</b> repeats it at its natural size (a pattern).
    /// The owner picks per image in the Inspector. Both are <see cref="SpriteRenderer.drawMode"/>
    /// settings, so no mesh, shader or script is involved.
    /// </para>
    /// <para>
    /// ⛔ No collider, no <see cref="Platform"/>, drawn far behind everything. §16 fixes the
    /// world's contents as Smiley and Platforms; a picture is neither.
    /// </para>
    /// </remarks>
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class MapBackground : MonoBehaviour
    {
        public enum Fit { Stretch, Tile }

        /// <summary>Behind every platform, every checkpoint and Smiley.</summary>
        public const int SortingOrder = -100;

        [Tooltip("The bounds this picture covers. The Map's LevelBounds.")]
        [SerializeField] private LevelBounds _bounds;

        [Tooltip("Stretch draws the image once over the whole world; Tile repeats it at its own size.")]
        [SerializeField] private Fit _fit = Fit.Stretch;

        public Fit Mode => _fit;

        /// <summary>The rectangle the picture covers, in world units.</summary>
        public Rect Covered
        {
            get
            {
                var renderer = GetComponent<SpriteRenderer>();
                Vector2 size = renderer != null ? renderer.size : Vector2.zero;
                return new Rect((Vector2)transform.position - size * 0.5f, size);
            }
        }

        /// <summary>Wires the bounds. Used by the scene builder.</summary>
        public void Set(LevelBounds bounds, Fit fit)
        {
            _bounds = bounds;
            _fit = fit;
            Apply();
        }

        private void OnValidate() => Apply();

        private void Awake() => Apply();

        /// <summary>Sizes and centres the picture on the world.</summary>
        public void Apply()
        {
            var renderer = GetComponent<SpriteRenderer>();

            if (renderer == null)
            {
                return;
            }

            if (_bounds == null)
            {
                _bounds = GetComponentInParent<LevelBounds>();
            }

            if (renderer.sortingOrder != SortingOrder) renderer.sortingOrder = SortingOrder;
            // ⚠ Write only what differs: SpriteRenderer.size and drawMode send a message the
            // renderer refuses inside OnValidate, and a build counts the refusal as an error (D-101).
            SpriteDrawMode mode = _fit == Fit.Tile ? SpriteDrawMode.Tiled : SpriteDrawMode.Sliced;

            if (renderer.drawMode != mode)
            {
                renderer.drawMode = mode;
            }

            if (_bounds == null)
            {
                return;
            }

            Rect world = _bounds.World;

            if ((renderer.size - world.size).sqrMagnitude > 1e-10f)
            {
                renderer.size = world.size;
            }

            var centre = new Vector3(world.center.x, world.center.y, transform.position.z);

            if (transform.position != centre) transform.position = centre;
            if (transform.rotation != Quaternion.identity) transform.rotation = Quaternion.identity;
            if (transform.localScale != Vector3.one) transform.localScale = Vector3.one;
        }
    }
}
