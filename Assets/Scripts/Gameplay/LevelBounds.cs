using UnityEngine;

namespace Smiley.Gameplay
{
    /// <summary>
    /// The extent of the authored world, in world units.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two jobs: what the camera may never show beyond (D-016 — clamping beats centring), and
    /// where the four invisible boundaries sit (owner, 2026-09-10).
    /// </para>
    /// <para>
    /// ⭐ <b>THE WORLD IS FIXED; THE WINDOW ONTO IT VARIES.</b> The owner asked for a gameplay
    /// area <i>"larger than the camera viewport by approximately 2–3 times, depending on the
    /// screen size of each phone"</i>. That multiple is an <b>outcome</b>, not a setting: the
    /// world is authored once in world units, and because a narrow phone sees less of it than
    /// a squarer one, the ratio lands somewhere in that range by itself.
    /// </para>
    /// <para>
    /// ⛔ <b>Deriving the world from the viewport instead would make gameplay
    /// device-dependent</b> — a wide phone would get more room, so the same launch would reach
    /// different places on different hardware. §33 forbids exactly that, and D-014 already
    /// ruled that the map's width is authored in world units and never as a number of screens.
    /// </para>
    /// <para>
    /// ⭐ <b>The size is the owner's (Q-030, D-075): 3240 × 23040 px = 13.5 × 96 units</b>, and
    /// <see cref="MapRule"/> holds the numbers. This component lives on the Map prefab's root,
    /// where the owner can see and drag it; a build fails if it drifts from the ruling.
    /// </para>
    /// <para>
    /// ⭐ <b>The four invisible boundaries are built HERE, at runtime, from these numbers</b>
    /// (D-076) — not by the scene builder. The map is the owner's to edit, and walls placed by
    /// a generator would sit where the world <i>used</i> to end the moment the bounds moved.
    /// Each sits just outside the world line and is deliberately thick, so nothing moving at a
    /// plausible speed can pass through one between two physics steps. They carry
    /// <see cref="Boundary"/> and NOT <see cref="Platform"/>: §8 makes Ready depend on contact
    /// with a Platform, so a wall that was one would let Smiley go Ready against it.
    /// </para>
    /// </remarks>
    public sealed class LevelBounds : MonoBehaviour
    {
        [SerializeField] private Vector2 _min = new Vector2(-5.5f, -4.9f);
        [SerializeField] private Vector2 _max = new Vector2(5.5f, 20.1f);

        [Tooltip("How thick the invisible boundaries are. Thick enough that nothing moving at " +
                 "a plausible speed can pass through one between two physics steps.")]
        [SerializeField] private float _boundaryThickness = 2f;

        [Tooltip("The physics material the boundaries get — the Normal platform's, so a wall " +
                 "feels like the floor.")]
        [SerializeField] private PhysicsMaterial2D _material;

        /// <summary>The authored world rectangle.</summary>
        public Rect World => new Rect(_min, _max - _min);

        /// <summary>How thick each invisible boundary is built.</summary>
        public float BoundaryThickness => _boundaryThickness;

        /// <summary>Replaces the bounds. Used by the scene builder.</summary>
        public void Set(Vector2 min, Vector2 max, PhysicsMaterial2D material)
        {
            _min = min;
            _max = max;
            _material = material;
        }

        private void Awake()
        {
            BuildBoundaries();
        }

        /// <summary>
        /// Builds the four invisible edges of the gameplay area as children.
        /// </summary>
        /// <remarks>
        /// Owner, 2026-09-10: two invisible walls and two boundaries, so the player cannot fall
        /// or leave the gameplay area. The floor is a backstop BENEATH §4's Start Area, not a
        /// replacement for it: §5 says a fall ends on the Start Area, and this only catches
        /// anything that somehow gets past it.
        /// </remarks>
        public void BuildBoundaries()
        {
            foreach (Boundary old in GetComponentsInChildren<Boundary>())
            {
                Destroy(old.gameObject);
            }

            Rect w = World;
            float t = _boundaryThickness;
            float half = t * 0.5f;

            Create("Wall_Left", new Vector2(w.xMin - half, w.center.y), new Vector2(t, w.height + t * 2f));
            Create("Wall_Right", new Vector2(w.xMax + half, w.center.y), new Vector2(t, w.height + t * 2f));
            Create("Boundary_Floor", new Vector2(w.center.x, w.yMin - half), new Vector2(w.width + t * 2f, t));
            Create("Boundary_Ceiling", new Vector2(w.center.x, w.yMax + half), new Vector2(w.width + t * 2f, t));
        }

        private void Create(string name, Vector2 centre, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.position = centre;

            // No renderer at all: invisible by having nothing to draw, rather than by a
            // transparent one somebody could later switch on.
            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = size;
            collider.sharedMaterial = _material;

            go.AddComponent<Boundary>();
        }

        /// <summary>
        /// How many times the world exceeds a viewport of the given size.
        /// </summary>
        /// <remarks>
        /// The owner's 2–3× target, reported rather than assumed — a build can print what the
        /// figure actually is on a given device instead of anyone trusting that it is right.
        /// </remarks>
        public Vector2 ViewportMultiple(Vector2 viewportSize)
        {
            return new Vector2(
                viewportSize.x > 0f ? World.width / viewportSize.x : 0f,
                viewportSize.y > 0f ? World.height / viewportSize.y : 0f);
        }

        private void OnDrawGizmosSelected()
        {
            Rect r = World;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(r.center, new Vector3(r.width, r.height, 0f));
        }
    }
}
