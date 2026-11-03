using Smiley.Config;
using UnityEngine;

namespace Smiley.Gameplay
{
    /// <summary>
    /// §13's rotation rules.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three states, and the middle one is the surprising one:
    /// <list type="bullet">
    /// <item><b>Rolling</b> — rotation is <i>"determined entirely by physics"</i>, so this
    /// component does nothing at all.</item>
    /// <item><b>Airborne</b> — <i>"Smiley does not rotate according to physics. Smiley maintains
    /// its current orientation."</i> The angle is held.</item>
    /// <item><b>Stopped</b> — <i>"body and face gradually return toward their correct / original
    /// orientation. They must not snap immediately."</i></item>
    /// </list>
    /// </para>
    /// <para>
    /// ⛔ <b>THE AIRBORNE HOLD HOLDS THE ANGLE, AND TOUCHES NOTHING ELSE (D-047).</b> §13 asks
    /// that Smiley not <i>rotate</i> in the air. It does not ask that he arrive with no angular
    /// momentum, and the difference between those two readings is the whole of the owner's
    /// rolling complaint.
    /// </para>
    /// <para>
    /// ⚠ <b>Two earlier versions took angular momentum away, and both distorted the landing.</b>
    /// Freezing the body's rotation gives it <b>infinite rotational inertia</b>, so contact
    /// friction had nothing to spin up and could only brake — a steep landing lost every scrap
    /// of its horizontal velocity. Releasing that freeze a step early with a swept probe fixed
    /// the braking and introduced something worse: whether the probe fired depended on the
    /// <b>direction of approach</b>, so how far Smiley rolled was decided by the angle he landed
    /// at rather than by the forces acting on him. ⛔ Both are gone. The body keeps its own
    /// angular velocity from take-off to landing, untouched, and the roll that follows is
    /// whatever Box2D makes of the impulse, the friction and the shape.
    /// </para>
    /// <para>
    /// ⓘ <b>"Body and face rotate together" needs no code either.</b> The sprite is a child of
    /// the body, so it inherits the rotation; and the deformation frame counter-rotates its
    /// artwork exactly so that the squash does not turn the face (D-022).
    /// </para>
    /// <para>
    /// ⚠ <b>The return to upright uses <see cref="Rigidbody2D.MoveRotation"/>, not a direct
    /// write.</b> Smiley's collider is deliberately asymmetric, so turning him while he rests
    /// against a surface can push geometry into geometry — the same class of problem as D-020.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(PlatformContact))]
    public sealed class SmileyRotation : MonoBehaviour
    {
        [SerializeField] private SmileyTuning _tuning;

        private Rigidbody2D _body;
        private PlatformContact _contact;

        /// <summary>The orientation being held for the current flight (§13).</summary>
        private float _heldAngle;

        /// <summary>Whether §13's airborne hold is in effect this instant. For tests.</summary>
        public bool IsHoldingOrientation { get; private set; }

        /// <summary>Whether the return to upright is running this instant. For tests.</summary>
        public bool IsReturningUpright { get; private set; }

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _contact = GetComponent<PlatformContact>();
        }

        private void FixedUpdate()
        {
            if (_tuning == null)
            {
                return;
            }

            if (!_contact.IsTouchingPlatform)
            {
                // §13: "While airborne or falling, Smiley does not rotate according to physics.
                // Smiley maintains its current orientation."
                //
                // ⛔ THE ANGLE IS HELD; THE ANGULAR VELOCITY IS NOT TOUCHED. Nothing here reads
                // or writes the body's spin, so it carries through the flight the way a thrown
                // ball's does — and the landing is resolved on a body with its real rotational
                // inertia, which is what lets friction turn the impact into a roll instead of
                // into a full stop.
                if (!IsHoldingOrientation)
                {
                    _heldAngle = _body.rotation;
                    IsHoldingOrientation = true;
                }

                IsReturningUpright = false;
                _body.MoveRotation(_heldAngle);
                return;
            }

            IsHoldingOrientation = false;

            if (_body.linearVelocity.magnitude > _tuning.StoppingThreshold)
            {
                // §13: rolling rotation is "determined entirely by physics" — so nothing here
                // touches it. ⛔ Any smoothing, damping or assistance applied in this branch
                // would be this component overruling the sentence it exists to implement, and
                // the owner has ruled that the roll must emerge from the simulation.
                IsReturningUpright = false;
                return;
            }

            float offset = UprightSettle.OffsetFromUpright(_body.rotation);
            if (offset < 0.05f)
            {
                IsReturningUpright = false;
                return;
            }

            IsReturningUpright = true;

            _body.angularVelocity = 0f;
            _body.MoveRotation(UprightSettle.Step(
                _body.rotation, _tuning.UprightSettleSeconds, Time.fixedDeltaTime));
        }
    }
}
