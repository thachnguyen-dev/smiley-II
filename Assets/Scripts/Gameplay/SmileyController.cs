using Smiley.Config;
using Smiley.View;
using UnityEngine;

namespace Smiley.Gameplay
{
    /// <summary>
    /// Drag and release: the whole of the player's control (§6, §7, §8).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What this does not do.</b> It never moves Smiley directly. A launch is a single
    /// impulse handed to <see cref="Rigidbody2D"/>, and everything after it — the arc, the
    /// bounces, the roll, the slide — belongs to the physics engine (D-002). §9's feel is
    /// therefore a property of the body's settings, not of code here.
    /// </para>
    /// <para>
    /// ⭐ <b>The drag begins ANYWHERE on the screen (D-111, owner).</b> §17's "touch on Smiley"
    /// grab is gone: under D-009 the drag vector is measured from wherever the finger landed,
    /// so where it landed never mattered to the launch — only to whether one began. The one
    /// place a touch still cannot start a drag is over the UI drawn on top of the game
    /// (<see cref="Smiley.View.UiKit.IsOverUi"/>): §23's corner button opens the menu, and the
    /// menu must not open with a drag already running underneath it.
    /// </para>
    /// <para>
    /// ⛔ <b>§8's restriction is enforced at the moment the drag begins, and nowhere else.</b>
    /// The player cannot <i>start</i> a drag unless Smiley is Ready. Once started, the drag
    /// is not cancelled if Smiley slips off an edge mid-aim — because §8 is explicit that
    /// Ready is <b>not a lock</b>, and cancelling would be the same thing as anchoring him,
    /// arriving one step later.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(PlatformContact))]
    public sealed class SmileyController : MonoBehaviour
    {
        [SerializeField] private SmileyTuning _tuning;
        [SerializeField] private TrajectoryGuide _guide;
        [SerializeField] private Camera _camera;

        private Rigidbody2D _body;
        private PlatformContact _contact;

        private bool _dragging;
        private int _dragPointerId;
        private Vector2 _dragTouchDown;

        /// <summary>
        /// The aim as of the last frame the owning finger was seen.
        /// </summary>
        /// <remarks>
        /// ⚠ Needed because a release is detected on the frame the finger is <b>already
        /// gone</b>, and that frame carries no position. §6 says the <i>final</i> drag
        /// distance sets the force, so the last seen aim is the one that launches — not a
        /// zero read from an empty pointer.
        /// </remarks>
        private LaunchSolution _lastSolution;

        /// <summary>§8's Ready state: a drag may begin this instant.</summary>
        /// <remarks>
        /// ⓘ §29 forbids a dedicated Ready indicator — the player is meant to read it from
        /// animation and physics behaviour. This property exists for the drag rules and for
        /// tests, not to be drawn.
        /// </remarks>
        public bool IsReady => ReadyRule.IsReady(
            _contact.IsTouchingPlatform,
            _body.linearVelocity.magnitude,
            _tuning != null ? _tuning.StoppingThreshold : 0f);

        /// <summary>
        /// Raised when Smiley is launched, carrying §6's charge.
        /// </summary>
        /// <remarks>
        /// ⓘ An event rather than a call into the audio, because §6's control must not depend on
        /// anything being there to listen — a scene with no audio in it still plays.
        /// </remarks>
        public event System.Action<float> Launched;

        /// <summary>Whether a drag is in progress right now.</summary>
        public bool IsDragging => _dragging;

        /// <summary>
        /// Which way the finger has travelled, as a unit vector, or zero when not dragging.
        /// </summary>
        /// <remarks>
        /// ⚠ <b>This is the DRAG, not the launch, and they are opposites.</b> §6 launches
        /// Smiley <i>away</i> from the drag, so <see cref="LaunchSolution.Direction"/> is
        /// <c>-drag</c>. Presentation that wants to show the pull — the owner's drag
        /// deformation (D-035), and anything like it later — wants this one; using the launch
        /// direction would stretch him away from the finger that is pulling him.
        /// </remarks>
        public Vector2 DragDirection =>
            _dragging && _lastSolution.HasAim ? -_lastSolution.Direction : Vector2.zero;

        /// <summary>
        /// How far through §6's Maximum Drag Distance the current drag is, 0 to 1.
        /// </summary>
        /// <remarks>
        /// ⓘ Already clamped by <see cref="LaunchSolver"/>, so dragging past the maximum
        /// neither aims further nor charges further — and therefore does not deform further.
        /// </remarks>
        public float DragCharge =>
            _dragging && _lastSolution.HasAim ? _lastSolution.Charge : 0f;

        /// <summary>
        /// Whether this controller has everything it needs to actually do anything.
        /// </summary>
        /// <remarks>
        /// ⛔ <b>A testable seam, and it exists because its absence cost a whole slice.</b>
        /// <see cref="Update"/> returns immediately when the tuning asset or the camera is
        /// missing, so an unwired controller is silent rather than broken — no exception, no
        /// warning, nothing in a log. The scene shipped in exactly that state and five Play
        /// Mode tests passed over it, because every one of them asserted that <i>components
        /// existed</i> rather than that the loop <i>could run</i>.
        /// </remarks>
        public bool IsConfigured => _tuning != null && _camera != null;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _contact = GetComponent<PlatformContact>();

            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_tuning != null)
            {
                _body.mass = _tuning.Mass;
                _body.linearDamping = _tuning.LinearDamping;

                // ⭐ Rolling resistance (Q-047). Box2D models none, and a soft body has a great
                // deal of it — without this Smiley coasts across every platform and off the far
                // side. ⛔ A body property, not a roll-distance rule: the distance he covers is
                // still whatever the forces produce.
                _body.angularDamping = _tuning.RollingResistance;
            }
        }

        private void Update()
        {
            ProcessPointer(PointerReader.Read());
        }

        /// <summary>
        /// One frame of §6's drag loop, for a pointer that has already been read.
        /// </summary>
        /// <remarks>
        /// ⛔ <b>Split out from <see cref="Update"/> because the drag was otherwise untestable,
        /// and untested.</b> <see cref="PointerReader"/> asks the input system for a finger, and
        /// a batch-mode run has none — so every existing test of §6 tested the solver
        /// (<see cref="LaunchSolver"/>) and none of them tested that a drag reaches it.
        /// ⓘ The tests drive <see cref="PointerReader.SetOverride"/> rather than calling this,
        /// so the real <c>Update</c> still runs and the reader is still consulted; the split is
        /// what makes the frame's work readable, and the seam is one layer further out.
        /// </remarks>
        private void ProcessPointer(PointerSample pointer)
        {
            if (_tuning == null || _camera == null)
            {
                return;
            }

            if (_dragging)
            {
                // A different finger cannot take over a drag in progress (§6, single finger).
                if (!pointer.IsDown || pointer.Id != _dragPointerId)
                {
                    Release(pointer);
                    return;
                }

                Aim(pointer);
                return;
            }

            if (pointer.IsDown && IsReady && !UiKit.IsOverUi(pointer.Position))
            {
                Begin(pointer);
            }
        }

        /// <summary>
        /// A drag interrupted by the controller being switched off is abandoned, never launched.
        /// </summary>
        /// <remarks>
        /// ⛔ <b>Only reachable since D-111.</b> <see cref="Smiley.View.GameScreens"/> disables
        /// this component while a screen is up. Before, a screen could only open from a touch
        /// that was not on Smiley — so never mid-drag. Now every touch is a potential drag, and
        /// without this the aim frozen at disable time would fire as a launch the moment the
        /// menu closed.
        /// </remarks>
        private void OnDisable()
        {
            if (!_dragging)
            {
                return;
            }

            _dragging = false;
            _dragPointerId = -1;
            _lastSolution = default;

            if (_guide != null)
            {
                _guide.Hide();
            }
        }

        private void Begin(PointerSample pointer)
        {
            _dragging = true;
            _dragPointerId = pointer.Id;

            // D-009: the origin is where the finger landed, so force starts at zero and it
            // does not matter where on Smiley the player grabbed.
            _dragTouchDown = pointer.Position;

            Aim(pointer);
        }

        private void Aim(PointerSample pointer)
        {
            LaunchSolution solution = Solve(pointer.Position);

            if (_guide != null)
            {
                // §7: while dragging, the guide is shown and updates continuously.
                _guide.Show(solution, _body.position, _body.mass);
            }
        }

        private void Release(PointerSample pointer)
        {
            // The last position the owning finger was seen at is the final drag distance.
            // When the finger has already lifted, the pointer carries no position, so the
            // aim computed on the previous frame is the one that counts.
            LaunchSolution solution = pointer.IsDown && pointer.Id == _dragPointerId
                ? Solve(pointer.Position)
                : _lastSolution;

            _dragging = false;
            _dragPointerId = -1;

            // §7: the guide disappears immediately on release.
            if (_guide != null)
            {
                _guide.Hide();
            }

            if (solution.HasAim)
            {
                _body.AddForce(solution.Direction * solution.Force, ForceMode2D.Impulse);

                // §27: "Launch has SFX." Announced after the impulse, so a listener that reads
                // the body sees the launch that actually happened.
                Launched?.Invoke(solution.Charge);
            }
        }

        private LaunchSolution Solve(Vector2 pointerPosition)
        {
            _lastSolution = LaunchSolver.Solve(
                _dragTouchDown,
                pointerPosition,
                _tuning.MaxDragPixels(Screen.height),
                _tuning.MaxLaunchForce);

            return _lastSolution;
        }
    }
}
