using Smiley.Config;
using UnityEngine;

namespace Smiley.Gameplay
{
    /// <summary>
    /// Drives §11's collision deformation and §12's rolling deformation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>The owner's direction was to "make maximum use of physics effects", so the
    /// coupling is the fullest one §11 allows</b>: the collider genuinely takes the deformed
    /// shape, <b>and</b> the contact properties move with it. A flattened Smiley really does
    /// present a flatter face to the world, grip more and rebound less.
    /// </para>
    /// <para>
    /// ⛔ <b>THE IDLE WOBBLE NEVER REACHES THE COLLIDER.</b> §12 is explicit that idle
    /// deformation <i>"is visual only"</i> and <i>"does not affect physics"</i>, while §11 is
    /// equally explicit that collision deformation is not merely decorative. Those are two
    /// different channels and they are kept apart here: <see cref="_state"/> drives the
    /// collider and the material, and the sprite is given that <b>plus</b> the idle term.
    /// Feeding idle into the collider would have a resting Smiley slowly pushing himself
    /// around.
    /// </para>
    /// <para>
    /// ⚠ <b>Mass must not move (§11).</b> A deforming collider with
    /// <see cref="Rigidbody2D.useAutoMass"/> enabled would silently retune Smiley's mass
    /// every frame — the shape changes, so the computed mass changes. It is forced off in
    /// <see cref="Awake"/> and pinned by a test.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(PolygonCollider2D))]
    public sealed class SmileyDeformer : MonoBehaviour
    {
        [SerializeField] private SmileyTuning _tuning;

        /// <summary>
        /// Carries the squash. Rotated into the deformation axis and scaled along it.
        /// </summary>
        [SerializeField] private Transform _deformFrame;

        /// <summary>
        /// The artwork, inside <see cref="_deformFrame"/> and counter-rotated.
        /// </summary>
        /// <remarks>
        /// ⚠ <b>Two transforms, because one would spin Smiley's face.</b> Squashing along an
        /// arbitrary axis is <c>R · S · R⁻¹</c> — rotate into the axis, scale, rotate back.
        /// Applying only <c>R · S</c> to the sprite would leave the artwork turned to face
        /// the deformation axis, so Smiley's face would swing to point at whatever he last
        /// hit. Invisible on the placeholder circle and glaring the moment real art arrives;
        /// §13 says the face turns with <i>rolling</i>, not with the squash.
        /// </remarks>
        [SerializeField] private Transform _sprite;

        [SerializeField] private PlatformContact _contact;

        /// <summary>Supplies the drag being made right now, for the visual stretch (D-035).</summary>
        [SerializeField] private SmileyController _controller;

        private Rigidbody2D _body;
        private PolygonCollider2D _collider;
        private PhysicsMaterial2D _material;

        private Vector2[] _basePoints;
        private Vector2[] _deformedPoints;

        private DeformationState _state;

        /// <summary>
        /// The decaying memory of the last impact. §11's <i>"gradually returns"</i> applies to
        /// the impact itself, not only to the rendered shape.
        /// </summary>
        private float _impactCharge;
        private float _impactAxisRadians;

        private float _appliedMagnitude = -1f;
        private float _appliedAxis;

        /// <summary>
        /// How far the artwork is currently stretched along the drag. ⛔ VISUAL ONLY.
        /// </summary>
        /// <remarks>
        /// Kept beside <see cref="_state"/> rather than inside it, and that separation is the
        /// whole guarantee: <see cref="_state"/> is what the collider and the material are
        /// built from, and this never enters either. It is the same split §12 forces for the
        /// idle wobble, applied to a second visual-only channel.
        /// </remarks>
        private float _dragStretch;
        private float _dragAxisRadians = Mathf.PI * 0.5f;

        /// <summary>How far the artwork is stretched by the drag right now. For tests.</summary>
        public float DragStretch => _dragStretch;

        /// <summary>Where the artwork is drawn, in Smiley's own space. For tests.</summary>
        /// <remarks>
        /// ⓘ Read from the transform rather than recomputed, so a test cannot agree with a
        /// calculation that never reached the screen — the D-018 lesson.
        /// </remarks>
        public Vector2 VisualOffset => _deformFrame != null
            ? (Vector2)_deformFrame.localPosition
            : Vector2.zero;

        /// <summary>The scale the artwork is actually drawn at. For tests.</summary>
        /// <remarks>
        /// ⓘ Read from the transform rather than recomputed, so a test cannot agree with a
        /// calculation that never reached the screen — the D-018 lesson.
        /// </remarks>
        public Vector3 VisualScale => _deformFrame != null ? _deformFrame.localScale : Vector3.one;

        /// <summary>How squashed the physics body currently is. Excludes the idle wobble.</summary>
        public float PhysicalMagnitude => _state.Magnitude;

        /// <summary>World-space axis the squash acts along. Saved by §21.</summary>
        public float PhysicalAxisRadians => _state.AxisRadians;

        /// <summary>
        /// Restores a saved deformation (§21).
        /// </summary>
        /// <remarks>
        /// ⚠ Restores the DECAYING IMPACT too, not just the shape. Setting only the magnitude
        /// would put the squash back and then let the next frame decay it from nothing, so a
        /// game resumed mid-impact would spring flat instantly rather than finishing the
        /// recovery it was in the middle of — §21 asks to resume "as close as practical to the
        /// frame at which the game was saved".
        /// </remarks>
        public void RestoreDeformation(float magnitude, float axisRadians)
        {
            _state.Magnitude = magnitude;
            _state.AxisRadians = axisRadians;

            _impactCharge = magnitude;
            _impactAxisRadians = axisRadians;

            // Force the collider to be rebuilt on the next step rather than skipped as "no
            // material change" against a stale applied value.
            _appliedMagnitude = -1f;
        }

        /// <summary>The outline the collider is currently using. For tests and tooling.</summary>
        public Vector2[] ColliderPoints => _collider != null ? _collider.GetPath(0) : null;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _collider = GetComponent<PolygonCollider2D>();

            if (_contact == null)
            {
                _contact = GetComponent<PlatformContact>();
            }

            if (_controller == null)
            {
                _controller = GetComponent<SmileyController>();
            }

            // §11: "Mass remains constant." Auto-mass would recompute it from the deforming
            // shape on every rebuild.
            _body.useAutoMass = false;

            // ⛔ AND ITS CENTRE MUST NOT WANDER EITHER.
            //
            // Rigidbody2D derives its centre of mass from the collider, so a shape that
            // deforms — especially one squashed about the contact point rather than the
            // middle — moves the centre of mass every rebuild. The solver reads that as the
            // mass having shifted and answers with a small torque, and sixty seconds of small
            // torques walked Smiley two thirds of a unit sideways while he was doing nothing
            // at all.
            //
            // §11 says the mass is constant; pinning where it acts is the companion to that.
            // The body still ROLLS unevenly, because the outline is still asymmetric — what
            // this removes is drift, not character.
            _body.centerOfMass = Vector2.zero;

            if (_tuning != null)
            {
                _body.mass = _tuning.Mass;
            }

            BuildShape();
            BuildMaterial();
        }

        private void BuildShape()
        {
            float radius = _tuning != null ? _tuning.BodyRadius : 0.5f;
            int vertices = _tuning != null ? _tuning.BlobVertices : 16;
            float asymmetry = _tuning != null ? _tuning.BlobAsymmetry : 0.06f;
            int seed = _tuning != null ? _tuning.BlobSeed : 20260910;

            Vector2[] unit = BlobShape.BuildBase(vertices, asymmetry, seed);

            _basePoints = new Vector2[unit.Length];
            _deformedPoints = new Vector2[unit.Length];

            for (int i = 0; i < unit.Length; i++)
            {
                _basePoints[i] = unit[i] * radius;
            }

            _collider.pathCount = 1;
            _collider.SetPath(0, _basePoints);
        }

        private void BuildMaterial()
        {
            // A private instance, because the values are about to be mutated per frame and a
            // shared asset would drag every other collider along with Smiley.
            _material = new PhysicsMaterial2D("Smiley (runtime)")
            {
                friction = _tuning != null ? _tuning.NormalFriction : 0.4f,
                bounciness = _tuning != null ? _tuning.Bounciness : 0.05f,
            };

            _collider.sharedMaterial = _material;
        }

        private void OnCollisionEnter2D(Collision2D collision) => TakeImpact(collision);

        private void OnCollisionStay2D(Collision2D collision) => TakeImpact(collision);

        /// <summary>
        /// Reads a contact and turns it into a deformation demand.
        /// </summary>
        /// <remarks>
        /// §11: deformation <i>"depends on impact force"</i> and <i>"follows the direction of
        /// the collision force"</i>. Both come straight off the contact — the impulse the
        /// solver actually applied, and the contact normal it applied it along.
        /// <para>
        /// ⓘ Using the solver's own impulse rather than the approach speed means a glancing
        /// touch at high speed squashes less than a head-on landing at the same speed, which
        /// is what "force" means and what a balloon would do.
        /// </para>
        /// </remarks>
        private void TakeImpact(Collision2D collision)
        {
            if (_tuning == null || collision.contactCount == 0)
            {
                return;
            }

            float strongest = 0f;
            Vector2 axis = Vector2.up;

            for (int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint2D contact = collision.GetContact(i);
                float impulse = contact.normalImpulse;

                if (impulse > strongest)
                {
                    strongest = impulse;
                    axis = contact.normal;
                }
            }

            // ⛔ RESTING WEIGHT IS NOT AN IMPACT, and this subtraction is what makes §12 true.
            //
            // A Smiley sitting on a platform delivers a contact impulse every single step —
            // his own weight, held up by the surface. Feeding that in as deformation leaves
            // him permanently squashed, which contradicts §12 twice over: "stopped → returns
            // to the natural base shape", and a stationary Smiley's deformation being
            // "visual only". Measuring the impulse ABOVE the resting baseline keeps §11's
            // "if the force continues, deformation continues updating" alive for a real
            // sustained force, while a body merely standing still deforms the physics by
            // nothing at all.
            float restingImpulse =
                _body.mass * Physics2D.gravity.magnitude * Time.fixedDeltaTime;

            float excess = strongest - restingImpulse * _tuning.RestingImpulseTolerance;

            if (excess <= 0f)
            {
                return;
            }

            float demand = excess * _tuning.ImpactToSquash;

            if (demand > _impactCharge)
            {
                _impactCharge = demand;
                _impactAxisRadians = Mathf.Atan2(axis.y, axis.x);
            }
        }

        private void FixedUpdate()
        {
            if (_tuning == null)
            {
                return;
            }

            float dt = Time.fixedDeltaTime;

            // §11: the impact's own contribution fades once the force ends.
            _impactCharge *= Mathf.Exp(-dt / Mathf.Max(0.0001f, _tuning.RecoverySeconds));

            // §12: "higher speed -> greater deformation", and "stopped -> returns to the
            // natural base shape". Only while actually on a platform: this is the shape being
            // pressed against a surface, not something that happens in mid-air.
            float rollingDemand = 0f;
            if (_contact != null && _contact.IsTouchingPlatform)
            {
                rollingDemand = _body.linearVelocity.magnitude * _tuning.RollingSpeedToSquash;
            }

            float demand = Mathf.Max(_impactCharge, rollingDemand);
            float axis = _impactCharge >= rollingDemand ? _impactAxisRadians : RollingAxisRadians();

            _state.Advance(demand, axis, dt, _tuning.RecoverySeconds, _tuning.TechnicalLimit);

            ApplyToPhysics();
        }

        /// <summary>
        /// The axis rolling deformation acts along.
        /// </summary>
        /// <remarks>
        /// §12 gives rolling a magnitude but not a direction. Derived from §11 rather than
        /// invented: rolling is continuous contact, and §11 says deformation follows the
        /// direction of the collision force — so Smiley is flattened against the surface he
        /// is rolling on. Falls back to world up when no normal is available.
        /// </remarks>
        private float RollingAxisRadians()
        {
            return Mathf.PI * 0.5f;
        }

        private void ApplyToPhysics()
        {
            // Rebuilding a Box2D shape is the expensive part of this whole system, so it is
            // skipped when the change would not be visible or felt.
            bool changed =
                Mathf.Abs(_state.Magnitude - _appliedMagnitude) > 0.002f ||
                Mathf.Abs(Mathf.DeltaAngle(_state.AxisRadians * Mathf.Rad2Deg,
                                           _appliedAxis * Mathf.Rad2Deg)) > 1f;

            if (!changed)
            {
                return;
            }

            _appliedMagnitude = _state.Magnitude;
            _appliedAxis = _state.AxisRadians;

            // ⚠ World -> local. The body rotates while rolling (§13); a world-space axis on a
            // local-space path would spin the squash around Smiley as he rolled.
            float localAxis = _state.AxisRadians - _body.rotation * Mathf.Deg2Rad;

            // ⭐ Squashed about the CONTACT, not the centre (owner, 2026-09-10). Landing from
            // above flattens Smiley down onto the surface instead of shrinking him evenly
            // toward his middle.
            Vector2 pivot = BlobShape.ContactPivot(
                localAxis, _tuning.BodyRadius, _tuning.ContactPivotBias);

            BlobShape.Deform(
                _basePoints, _deformedPoints, localAxis,
                _state.ScaleInAxisFrame(_tuning.StretchRatio), pivot);

            // ⛔ A rebuild may only remove material, never add it — see ClampWithinBase. This
            // is what stops the deforming collider from levitating Smiley (D-020); without it
            // the bulge reaches into a platform, Box2D pushes the body out to resolve the
            // overlap, and repeating that every frame is a drive.
            BlobShape.ClampWithinBase(_basePoints, _deformedPoints);

            // ⛔ Never hand the solver a degenerate outline.
            //
            // A hard squash pushes the vertices toward collinear, and a polygon with no area
            // is not a body — Unity can reject the path outright, which leaves Smiley with no
            // collider for a frame. That is long enough to fall through a platform, and it
            // looks exactly like tunnelling. Keeping the last good shape costs one frame of
            // deformation fidelity and cannot drop the floor out from under him.
            if (!HasUsableArea(_deformedPoints))
            {
                return;
            }

            _collider.SetPath(0, _deformedPoints);

            ApplyMaterial();
        }

        /// <summary>
        /// Whether an outline still encloses enough area to act as a body.
        /// </summary>
        /// <remarks>
        /// The shoelace area, compared against a fraction of the undeformed shape's. The
        /// threshold is deliberately generous: anything approaching a sliver is refused long
        /// before the solver has to reason about it.
        /// </remarks>
        private bool HasUsableArea(Vector2[] points)
        {
            float area = Mathf.Abs(SignedArea(points));
            float baseArea = Mathf.Abs(SignedArea(_basePoints));

            return baseArea > 0f && area > baseArea * 0.02f;
        }

        private static float SignedArea(Vector2[] points)
        {
            float sum = 0f;

            for (int i = 0; i < points.Length; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[(i + 1) % points.Length];
                sum += (a.x * b.y) - (b.x * a.y);
            }

            return sum * 0.5f;
        }

        /// <summary>
        /// The second half of the owner's "maximum use of physics": contact properties move
        /// with the squash.
        /// </summary>
        /// <remarks>
        /// ⏸ The mapping is <b>provisional and mine</b> (Q-037). A flatter body presents more
        /// contact and less spring, so friction rises and bounciness falls with the squash.
        /// ⚠ §11 never mentions friction — this is the chosen expression of "gameplay-relevant",
        /// not a rule the spec states.
        /// </remarks>
        private void ApplyMaterial()
        {
            if (_material == null)
            {
                return;
            }

            float t = Mathf.Clamp01(_state.Magnitude / Mathf.Max(0.0001f, _tuning.TechnicalLimit));

            _material.friction = Mathf.Lerp(
                _tuning.NormalFriction, _tuning.NormalFriction * _tuning.SquashFrictionGain, t);
            _material.bounciness = Mathf.Lerp(_tuning.Bounciness, 0f, t);

            // Re-assigning is what makes the change take: mutating the material alone does not
            // reliably mark the collider dirty.
            _collider.sharedMaterial = _material;
        }

        private void Update()
        {
            if (_deformFrame == null || _tuning == null)
            {
                return;
            }

            // §12: "While stationary / Ready, Smiley has a subtle idle deformation." A slow
            // breathing wobble, added on top of the physical squash.
            //
            // ⛔ It is applied HERE and nowhere else. Nothing in this method touches the
            // collider or the material, which is the whole of §12's "visual only".
            float idle = Mathf.Sin(Time.time * _tuning.IdleFrequency * Mathf.PI * 2f)
                         * _tuning.IdleAmplitude;

            bool isDragging = _controller != null && _controller.IsDragging;

            AdvanceDragStretch(
                isDragging ? _controller.DragDirection : Vector2.zero,
                isDragging ? _controller.DragCharge : 0f,
                Time.deltaTime);

            // ⚠ Deliberately NOT clamped at zero. The wobble breathes both ways — a negative
            // magnitude is a stretch along the axis rather than a squash, which is what makes
            // it read as a soft body settling rather than a shape twitching in one direction.
            // Clamping left Smiley at exactly his base shape for half of every cycle.
            //
            // ⭐ AND THE DRAG IS A SQUEEZE (D-053, owner 2026-09-10): "he should become shorter
            // along the drag direction and expand wider perpendicular to the drag direction,
            // like a water balloon being squeezed."
            //
            // ⓘ That needs no new mechanism at all — it is what a POSITIVE magnitude already
            // means. ScaleInAxisFrame returns (1 − M) along the axis and (1 + M · ratio) across
            // it, so the sign alone chooses between a squeeze and an elongation, and the drag
            // simply takes the same sign a collision does.
            bool dragging = _dragStretch > 0.0005f;

            var visual = new DeformationState
            {
                Magnitude = dragging ? idle + _dragStretch : _state.Magnitude + idle,
                AxisRadians = dragging
                    ? _dragAxisRadians
                    : (_state.Magnitude > 0.001f ? _state.AxisRadians : Mathf.PI * 0.5f),
            };

            Vector2 scale = visual.ScaleInAxisFrame(_tuning.StretchRatio);

            // R · S on the frame, R⁻¹ on the artwork inside it. The transform hierarchy
            // composes those into the squash, and the sprite comes out unrotated.
            float localAxisDegrees = (visual.AxisRadians * Mathf.Rad2Deg) - _body.rotation;
            float localAxis = localAxisDegrees * Mathf.Deg2Rad;

            _deformFrame.localRotation = Quaternion.Euler(0f, 0f, localAxisDegrees);
            _deformFrame.localScale = new Vector3(scale.x, scale.y, 1f);

            // ⭐ WHERE THE SQUASH IS ANCHORED, AND THE DRAG NEEDED A DIFFERENT ANSWER (D-066).
            //
            // §11's impact pivots on the CONTACT — landing flattens Smiley onto the surface he
            // hit, so the far side comes down to meet it, and the pivot lies along the squash
            // axis because the contact normal IS that axis.
            //
            // ⛔ A DRAG IS NOT LIKE THAT, AND CENTRING IT WAS WRONG. Smiley is resting on a
            // platform while the player aims. Squeezing him about his middle pulls his bottom
            // edge up off the ground, and the owner saw exactly that: "the deformation pivot is
            // always centred, which causes the player's visual to separate from the ground."
            //
            // ✅ So a drag pivots on the GROUND — the compression happens away from the surface,
            // and the part of him that is touching stays touching. ⓘ Straight down is exact
            // rather than approximate: §15 has only horizontal platforms, no slopes and no
            // tilts, so the surface normal is always world up.
            Vector2 pivot;

            if (dragging && _contact != null && _contact.IsTouchingPlatform)
            {
                // World down, brought into the body's own space — he may be resting at any
                // angle, and the ground does not rotate with him.
                float toLocal = -_body.rotation * Mathf.Deg2Rad;
                var down = new Vector2(
                    Mathf.Sin(toLocal), -Mathf.Cos(toLocal));

                pivot = down * (_tuning.BodyRadius * _tuning.ContactPivotBias);
            }
            else
            {
                pivot = BlobShape.ContactPivot(
                    localAxis, _tuning.BodyRadius, _tuning.ContactPivotBias);
            }

            // ⚠ The general offset, not the shortcut. Scaling about a pivot means moving by
            // (p − M·p); that collapses to p · (1 − scaleX) only when p lies ALONG the squash
            // axis, which is true of a contact normal and false of the ground during a sideways
            // drag. ⛔ Using the shortcut there computes the offset for a pivot nobody is using.
            Vector2 offset = BlobShape.PivotOffset(pivot, localAxis, scale);

            _deformFrame.localPosition = new Vector3(offset.x, offset.y, 0f);

            if (_sprite != null)
            {
                _sprite.localRotation = Quaternion.Euler(0f, 0f, -localAxisDegrees);
            }
        }

        /// <summary>
        /// Follows the drag with the visual-only stretch (D-035).
        /// </summary>
        /// <remarks>
        /// <para>
        /// ⭐ <b>Owner instruction, 2026-09-10:</b> while the player is dragging, the visual
        /// image deforms in the direction of the drag; <b>only</b> the image, not the collider
        /// or the physics body; and the deformation is limited to a maximum amount.
        /// </para>
        /// <para>
        /// ⛔ <b>"Visual only" is structural here rather than promised.</b> This is called from
        /// <see cref="Update"/> and writes two private floats that nothing but the transform
        /// hierarchy reads. The collider and the contact material are both built from
        /// <see cref="_state"/> in <see cref="ApplyToPhysics"/>, and neither is touched — the
        /// same separation §12 forces on the idle wobble, applied to a second visual channel.
        /// </para>
        /// <para>
        /// ⓘ <b>Public because it is the seam the tests drive.</b> A batch-mode run has no
        /// finger for <c>PointerReader</c> to find, so driving the real input would test
        /// nothing; handing the drag in tests the rule and the wiring separately, which is what
        /// let the collider assertion below be written at all.
        /// </para>
        /// </remarks>
        /// <param name="dragDirection">Unit vector along the finger's travel, or zero.</param>
        /// <param name="charge">How far through §6's Maximum Drag Distance the drag is.</param>
        public void AdvanceDragStretch(Vector2 dragDirection, float charge, float deltaTime)
        {
            if (_tuning == null)
            {
                return;
            }

            float target = 0f;

            if (dragDirection.sqrMagnitude > 0.0001f)
            {
                target = DragStretchRule.Target(
                    charge, _tuning.DragStretchAtFullDrag, _tuning.DragStretchMaximum);

                _dragAxisRadians = Mathf.Atan2(dragDirection.y, dragDirection.x);
            }

            _dragStretch = DragStretchRule.Advance(
                _dragStretch, target, _tuning.DragStretchSeconds, deltaTime);
        }
    }
}
