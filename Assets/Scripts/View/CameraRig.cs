using Smiley.Config;
using Smiley.Gameplay;
using UnityEngine;

namespace Smiley.View
{
    /// <summary>
    /// Follows Smiley. Nothing the player touches moves this camera.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implements §17's follow with <b>D-016</b>'s reading of the two axes — continuous
    /// vertically, deadzone horizontally.
    /// </para>
    /// <para>
    /// ⛔ <b>D-010's scouting pan is REMOVED (D-111, owner).</b> The whole screen is now the
    /// drag surface, so there is no "outside the player area" left for a pan to live in; the
    /// owner ruled that the player's drag never controls the camera and that the camera moves
    /// only by this follow. This class reads no input at all — that is what makes the rule
    /// structural rather than a guard.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] private SmileyTuning _tuning;
        [SerializeField] private Transform _target;
        [SerializeField] private LevelBounds _bounds;

        private Camera _camera;

        /// <summary>Whether this rig has everything it needs to do anything.</summary>
        /// <remarks>
        /// The same testable seam as <see cref="SmileyController.IsConfigured"/>, and for the
        /// same reason: an unwired component here is silent rather than broken (D-018).
        /// </remarks>
        public bool IsConfigured =>
            _tuning != null && _target != null && _bounds != null;

        /// <summary>Half the world extent the camera can see, from its own projection.</summary>
        public Vector2 ViewHalfExtents =>
            new Vector2(_camera.orthographicSize * _camera.aspect, _camera.orthographicSize);

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        // ⚠ Start, not Awake (D-103): CameraFraming sizes the camera in ITS Awake, and the two
        // share a GameObject with no ordering between them. A snap taken against the builder's
        // reference size was then clamped to the wrong floor, and the rig spent the first second
        // easing down from there — a fixture that read the camera in that second measured the
        // ease and called it "not following".
        private void Start()
        {
            SnapToTarget();
        }

        /// <summary>Places the camera on the target immediately, with no easing.</summary>
        /// <remarks>
        /// Used at startup so the first frame does not show the view sliding in from wherever
        /// the scene happened to leave it.
        /// </remarks>
        public void SnapToTarget()
        {
            if (!IsConfigured)
            {
                return;
            }

            var goal = new Vector2(_target.position.x, _target.position.y + Framing());
            Apply(CameraSolver.ClampToBounds(goal, _bounds.World, ViewHalfExtents));
        }

        private void LateUpdate()
        {
            if (!IsConfigured)
            {
                return;
            }

            // ⚠ LateUpdate, not Update: the camera must read positions AFTER everything that
            // moves has moved, or the view trails the world by a frame and reads as lag.
            Vector2 position = transform.position;
            Vector2 goal = FollowGoal(position);
            float seconds = _tuning.FollowSmoothing;

            var next = new Vector2(
                CameraSolver.Smooth(position.x, goal.x, seconds, Time.deltaTime),
                CameraSolver.Smooth(position.y, goal.y, seconds, Time.deltaTime));

            Apply(CameraSolver.ClampToBounds(next, _bounds.World, ViewHalfExtents));
        }

        private Vector2 FollowGoal(Vector2 current)
        {
            // §17: continuous vertically. D-016: deadzone horizontally.
            float x = CameraSolver.HorizontalGoal(
                current.x, _target.position.x, _tuning.CameraDeadzoneHalfWidth);

            float y = _target.position.y + Framing();

            return new Vector2(x, y);
        }

        /// <summary>
        /// How far above Smiley the view sits. ⏸ Q-011, provisional.
        /// </summary>
        /// <remarks>
        /// Slightly above centre because the game is a climb (§1): the interesting space is
        /// the one he is heading into, not the one he has left.
        /// </remarks>
        private float Framing() => _tuning.CameraFraming;

        private void Apply(Vector2 position)
        {
            transform.position = new Vector3(position.x, position.y, transform.position.z);
        }
    }
}
