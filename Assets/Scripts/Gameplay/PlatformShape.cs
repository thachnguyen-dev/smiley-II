using UnityEngine;

namespace Smiley.Gameplay
{
    /// <summary>
    /// The one place a platform's size is set. Width and height, in world units.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>THE OWNER SIZES A PLATFORM BY TYPING A WIDTH (D-076).</b> §15: horizontal
    /// rectangles, width may vary, no tilt, no slopes. Two numbers cover that, and one
    /// component pushes them to the two things that must agree — the collider Smiley lands on
    /// and the picture the player sees.
    /// </para>
    /// <para>
    /// ⛔ <b>Not the transform's scale.</b> The art is replaced later (owner, 2026-09-11), and a
    /// 9-sliced sprite under a non-uniform scale stretches its corners. The visual is a child
    /// with a <see cref="SpriteRenderer"/> in <see cref="SpriteDrawMode.Sliced"/> mode drawn at
    /// this size, so swapping the sprite — with or without borders — changes nothing here.
    /// </para>
    /// <para>
    /// ⭐ <b>THE COLLIDER IS SMALLER THAN THE PICTURE, BY WHAT THE PICTURE LEAVES EMPTY (D-084).</b>
    /// Owner, 2026-09-11: <i>"there is a visible gap between the player's artwork and the platform
    /// artwork when the player lands … calculate and choose an appropriate reduction value
    /// yourself."</i> Two things made the gap. The grass fringe: the top rows of the platform
    /// sprite are sparse tufts, and Smiley rested on the tips. And Smiley's own overhang: his
    /// polygon outline strays up to <c>BlobAsymmetry</c> outside the circle his art draws, and
    /// Box2D holds resting bodies <c>defaultContactOffset</c> apart. So the build <i>measures</i>
    /// the fringe on each palette sprite — rows from each edge until a row is mostly opaque —
    /// and stores it here as rows; <see cref="Apply"/> turns rows into units for this platform's
    /// size and adds <see cref="PlayerRestGap"/> on top. The picture is not touched.
    /// </para>
    /// <para>
    /// ⛔ <b>THE TOP EDGE ONLY.</b> All four edges are measured and recorded, and only the top
    /// moves the collider. The first fit of the owner's art found 122 and 129 columns of fringe
    /// on the Disappearing platform's sides — a fifth of its width each — and applying that would
    /// have narrowed every landing by two fifths, which is gameplay, and the owner said
    /// <i>"keep the platform gameplay behavior otherwise unchanged."</i> The sides and the
    /// underside are where Smiley bumps, not where he lands; the gap the owner saw is at the top.
    /// </para>
    /// <para>
    /// ⓘ Physics is unchanged from the scaled-transform test bed except for that inset: Box2D
    /// sees the same box whether it is a unit collider scaled ×3 or a 3-unit collider unscaled.
    /// </para>
    /// </remarks>
    [ExecuteAlways]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class PlatformShape : MonoBehaviour
    {
        /// <summary>
        /// How far below the platform's drawn surface its collider top sits, beyond the fringe.
        /// </summary>
        /// <remarks>
        /// PROVISIONAL, mine (D-084). <c>Physics2D.defaultContactOffset</c> (0.01) plus the
        /// furthest Smiley's outline reaches past his art (<c>BodyRadius × BlobAsymmetry</c> =
        /// 0.03). Measured on the Start Area before the change: art 0.024 units above the
        /// picture with no fringe at all. A hair of overlap reads as touching; a hair of gap
        /// reads as floating, so this errs toward overlap.
        /// </remarks>
        public const float PlayerRestGap = 0.04f;

        [Tooltip("How wide the platform is, in world units. ⚠ Below about 3 units Smiley " +
                 "could not stay on a platform in testing (D-049).")]
        [SerializeField] private float _width = 3f;

        [Tooltip("How tall the platform is, in world units.")]
        [SerializeField] private float _height = 0.4f;

        [Tooltip("The child that draws the platform. Replace its sprite to change the art.")]
        [SerializeField] private SpriteRenderer _visual;

        [Header("Measured from the art by the build — do not type these")]
        [Tooltip("Sprite rows above the solid surface (the grass fringe). Measured by the build " +
                 "from the Visual's sprite; re-measured when the sprite changes.")]
        [SerializeField] private int _fringeTop;
        [SerializeField] private int _fringeBottom;
        [SerializeField] private int _fringeLeft;
        [SerializeField] private int _fringeRight;

        [Tooltip("The sprite the fringe was measured on. A different sprite on the Visual means " +
                 "the numbers above are stale, and the next build re-measures.")]
        [SerializeField] private Sprite _fittedSprite;

        public float Width => _width;
        public float Height => _height;

        /// <summary>The picture, so a type can tint or hide it.</summary>
        public SpriteRenderer Visual => _visual;

        /// <summary>The sprite the fringe rows were measured on, or null if never measured.</summary>
        public Sprite FittedSprite => _fittedSprite;

        /// <summary>The measured fringe, in sprite rows/columns: top, bottom, left, right.</summary>
        public (int top, int bottom, int left, int right) FringeRows =>
            (_fringeTop, _fringeBottom, _fringeLeft, _fringeRight);

        /// <summary>
        /// How much smaller the collider is than the picture on each side, in world units.
        /// ⛔ Only the top is ever non-zero (see the class remarks); the others are kept in the
        /// shape of the measurement so a future ruling can switch them on without a new API.
        /// </summary>
        public (float top, float bottom, float left, float right) ColliderInsets
        {
            get
            {
                Sprite sprite = _visual != null ? _visual.sprite : null;

                return (Vertical(sprite, _fringeTop, isTop: true) + PlayerRestGap, 0f, 0f, 0f);
            }
        }

        private void OnValidate()
        {
            _width = Mathf.Max(0.1f, _width);
            _height = Mathf.Max(0.05f, _height);
            Apply();
        }

        private void Awake()
        {
            Apply();
        }

        /// <summary>Sets the size and pushes it to the collider and the picture.</summary>
        public void Set(float width, float height, SpriteRenderer visual)
        {
            _width = width;
            _height = height;
            _visual = visual;
            Apply();
        }

        /// <summary>Records a fringe measurement. Used by the build's art fitter.</summary>
        public void SetFringe(int top, int bottom, int left, int right, Sprite measuredOn)
        {
            _fringeTop = Mathf.Max(0, top);
            _fringeBottom = Mathf.Max(0, bottom);
            _fringeLeft = Mathf.Max(0, left);
            _fringeRight = Mathf.Max(0, right);
            _fittedSprite = measuredOn;
            Apply();
        }

        /// <summary>Pushes the size to the collider and the picture.</summary>
        public void Apply()
        {
            var collider = GetComponent<BoxCollider2D>();

            if (collider != null)
            {
                (float top, float bottom, float left, float right) = ColliderInsets;

                float w = Mathf.Max(0.05f, _width - left - right);
                float h = Mathf.Max(0.05f, _height - top - bottom);

                collider.size = new Vector2(w, h);
                collider.offset = new Vector2((left - right) * 0.5f, (bottom - top) * 0.5f);
            }

            if (_visual != null)
            {
                // ⚠ Write only what differs. Setting SpriteRenderer.size sends a message the
                // renderer refuses inside OnValidate, and a build counts that refusal as an
                // error — the first APK after D-099 failed on it (D-101). A no-op write is not.
                var size = new Vector2(_width, _height);

                if (_visual.drawMode != SpriteDrawMode.Sliced)
                {
                    _visual.drawMode = SpriteDrawMode.Sliced;
                }

                if ((_visual.size - size).sqrMagnitude > 1e-10f)
                {
                    _visual.size = size;
                }

                if (_visual.transform.localPosition != Vector3.zero)
                {
                    _visual.transform.localPosition = Vector3.zero;
                }

                if (_visual.transform.localScale != Vector3.one)
                {
                    _visual.transform.localScale = Vector3.one;
                }
            }
        }

        /// <summary>
        /// Rows of fringe into world units, for this platform's height.
        /// </summary>
        /// <remarks>
        /// In Sliced mode a sprite with no borders stretches uniformly, so a row is a fraction
        /// of the drawn height; with 9-slice borders the edge rows keep their pixel size, so a
        /// row is <c>1 / pixelsPerUnit</c>.
        /// </remarks>
        private float Vertical(Sprite sprite, int rows, bool isTop)
        {
            if (sprite == null || rows <= 0)
            {
                return 0f;
            }

            float border = isTop ? sprite.border.w : sprite.border.y;

            if (border > 0f)
            {
                return rows / sprite.pixelsPerUnit;
            }

            return sprite.rect.height > 0f ? rows / sprite.rect.height * _height : 0f;
        }

        /// <summary>Columns of fringe into units. ⓘ Recorded only — not applied (see the class remarks).</summary>
        public float HorizontalFringeUnits(bool isLeft) =>
            Horizontal(_visual != null ? _visual.sprite : null, isLeft ? _fringeLeft : _fringeRight, isLeft);

        private float Horizontal(Sprite sprite, int columns, bool isLeft)
        {
            if (sprite == null || columns <= 0)
            {
                return 0f;
            }

            float border = isLeft ? sprite.border.x : sprite.border.z;

            if (border > 0f)
            {
                return columns / sprite.pixelsPerUnit;
            }

            return sprite.rect.width > 0f ? columns / sprite.rect.width * _width : 0f;
        }
    }
}
