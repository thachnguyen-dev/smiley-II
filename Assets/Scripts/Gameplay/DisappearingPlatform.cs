using Smiley.Config;
using UnityEngine;

namespace Smiley.Gameplay
{
    /// <summary>
    /// §14.3's Disappearing Platform.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All of the rules live in <see cref="PlatformCountdown"/>, which has no Unity in it and
    /// is tested clause by clause. This component does three things the state machine cannot:
    /// notice whether Smiley is actually touching, turn the collision and the sprite on and
    /// off, and show §14.3's near-expiry cue.
    /// </para>
    /// <para>
    /// ⛔ <b>NO SFX, EVER.</b> §14.3 says so twice — once for near-expiry and once for respawn —
    /// and §27 repeats it. Nothing in this file plays a sound, and nothing should be added that
    /// does.
    /// </para>
    /// <para>
    /// ⛔ <b>AND NO NUMERICAL COUNTDOWN</b> (§14.3, §29). The player reads the remaining time
    /// from the cue, or not at all.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Platform))]
    [RequireComponent(typeof(Collider2D))]
    public sealed class DisappearingPlatform : MonoBehaviour
    {
        [SerializeField] private SmileyTuning _tuning;
        [SerializeField] private SpriteRenderer _renderer;

        private Collider2D _collider;
        private PlatformCountdown _countdown;
        private bool _touching;
        private Color _baseColour;
        private int _settleSteps;
        private Smiley.Save.SaveService _save;

        /// <summary>Whether the platform is solid right now. For tests.</summary>
        public bool IsPresent => _countdown.Phase == PlatformPhase.Present;

        /// <summary>Seconds of countdown left. For tests.</summary>
        public float Remaining => _countdown.Remaining;

        /// <summary>Whether the countdown is ticking this instant. For tests.</summary>
        public bool IsCountingDown => _countdown.IsRunning;

        /// <summary>Accumulated stable-contact time. For tests, and for §21's restore.</summary>
        public float ContactHeld => _countdown.ContactHeld;

        private void Awake()
        {
            _collider = GetComponent<Collider2D>();

            if (_renderer == null)
            {
                // D-076: the picture is a child named Visual, so the art can be replaced
                // without touching this component. The root has no renderer of its own.
                _renderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (_renderer != null)
            {
                _baseColour = _renderer.color;
            }

            _countdown = PlatformCountdown.Fresh(
                _tuning != null ? _tuning.DisappearCountdownSeconds : 3f);

            // §21 counts a platform vanishing or returning as an important gameplay event: it is
            // a change to persistent world state (§3) that no other event would capture. Found
            // rather than serialised because the platforms are generated (SceneBuilder) and a
            // scene without a SaveService must still play.
            _save = Object.FindFirstObjectByType<Smiley.Save.SaveService>();
        }

        /// <summary>
        /// Snapshots this platform for section 21.
        /// </summary>
        /// <remarks>
        /// Section 21 asks for "platform states", "remaining Disappearing Platform countdown
        /// time", and "whether the countdown is currently running or paused" - three separate
        /// requirements, so three separate fields. A platform with 1.4s left that is paused and
        /// one with 1.4s left that is ticking are different situations.
        /// </remarks>
        public Smiley.Save.PlatformState CaptureState()
        {
            return new Smiley.Save.PlatformState
            {
                Name = name,
                Present = _countdown.Phase == PlatformPhase.Present,
                Remaining = _countdown.Remaining,
                RespawnRemaining = _countdown.RespawnRemaining,
                Running = _countdown.IsRunning,
                ContactHeld = _countdown.ContactHeld,
            };
        }

        /// <summary>Restores a snapshot (section 21).</summary>
        /// <remarks>
        /// The collider and renderer are applied immediately rather than waiting for the next
        /// phase change - a restored platform that is meant to be GONE must have no collision
        /// the instant the game resumes, or Smiley lands on something that is not there.
        /// </remarks>
        public void RestoreState(Smiley.Save.PlatformState state)
        {
            _countdown.Phase = state.Present ? PlatformPhase.Present : PlatformPhase.Vanished;
            _countdown.Remaining = state.Remaining;
            _countdown.RespawnRemaining = state.RespawnRemaining;
            _countdown.ContactHeld = state.ContactHeld;

            _touching = false;

            // ⛔ WITHOUT THIS THE RESTORED ContactHeld IS DEAD ON ARRIVAL, and the line above is
            // why. Unity reports contacts AFTER it steps, so on the first FixedUpdate following
            // a restore nothing is touching anything yet — even a Smiley placed squarely on this
            // platform. Advance would take its "contact lost" branch, zero ContactHeld, and the
            // countdown would then have to re-earn §14.3's stability from scratch. Skipping a
            // couple of steps lets the physics engine answer the question before it is asked.
            _settleSteps = 2;

            ApplyPhase();
        }

        private void OnCollisionEnter2D(Collision2D collision) => Note(collision, true);

        private void OnCollisionExit2D(Collision2D collision) => Note(collision, false);

        /// <summary>
        /// Records whether the thing touching us is Smiley.
        /// </summary>
        /// <remarks>
        /// ⚠ Only Smiley counts. §14.3 is written entirely about him, and a platform that
        /// counted down because something else brushed it would be answering a question nobody
        /// asked — there is nothing else in the game today (§16), but the rule should not
        /// depend on that staying true.
        /// </remarks>
        private void Note(Collision2D collision, bool touching)
        {
            if (collision.collider.GetComponentInParent<SmileyController>() == null)
            {
                return;
            }

            _touching = touching;
        }

        private void FixedUpdate()
        {
            if (_tuning == null)
            {
                return;
            }

            if (_settleSteps > 0)
            {
                _settleSteps--;
                return;
            }

            PlatformPhase before = _countdown.Phase;

            _countdown.Advance(
                _touching,
                Time.fixedDeltaTime,
                _tuning.DisappearCountdownSeconds,
                _tuning.RespawnDelaySeconds,
                _tuning.StableContactSeconds,
                _tuning.DisappearRecoverSeconds);

            if (_countdown.Phase != before)
            {
                ApplyPhase();

                // ⛔ Deliberately NOT inside ApplyPhase, which RestoreState also calls — saving
                // from inside a restore would write the world back out before it had finished
                // being read in.
                if (_save != null)
                {
                    _save.NotifyWorldChanged();
                }
            }
        }

        private void ApplyPhase()
        {
            bool present = _countdown.Phase == PlatformPhase.Present;

            // §14.3: "The Platform disappears completely. Its collision disappears completely."
            // ⛔ And: "If Smiley falls through the former platform position before the platform
            // respawns, there must be NO COLLISION at that location." Disabling the collider —
            // not merely hiding the sprite — is what makes that true.
            _collider.enabled = present;

            if (_renderer != null)
            {
                _renderer.enabled = present;
                _renderer.color = _baseColour;
            }

            if (present)
            {
                // §14.3: on respawn, "collision becomes active immediately" — which the line
                // above already did — and "play VFX". ⚠ Placeholder: §32 reserves the visual
                // direction to the owner, so this is a shape, not a design.
                _touching = false;
            }
        }

        private void Update()
        {
            if (_tuning == null || _renderer == null || !_renderer.enabled)
            {
                return;
            }

            // §14.3's near-expiry cue: "Use only Animation. VFX." — and explicitly NOT a
            // numerical countdown, and NOT SFX.
            //
            // ⚠ Placeholder presentation. A pulse whose rate rises with urgency reads as
            // "running out" without saying a number; the real look is the owner's (§32).
            float urgency = _countdown.Urgency(_tuning.DisappearCountdownSeconds);

            if (urgency < _tuning.NearExpiryFrom)
            {
                _renderer.color = _baseColour;
                return;
            }

            float into = Mathf.InverseLerp(_tuning.NearExpiryFrom, 1f, urgency);
            float rate = Mathf.Lerp(2f, 12f, into);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * rate * Mathf.PI * 2f);

            Color colour = _baseColour;
            colour.a = Mathf.Lerp(_baseColour.a, _baseColour.a * 0.25f, pulse * into);
            _renderer.color = colour;
        }
    }
}
