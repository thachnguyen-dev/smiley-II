using Smiley.Gameplay;
using UnityEngine;

namespace Smiley.Audio
{
    /// <summary>
    /// Turns what Smiley does into §27's cues.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠ <b>Separate from <see cref="GameAudio"/> on purpose.</b> That class knows how loud
    /// things are; this one knows what happened. Merging them would put collision callbacks and
    /// volume curves in the same file and make either harder to test alone.
    /// </para>
    /// <para>
    /// ⛔ <b>NOTHING HERE ANSWERS A ROLL.</b> §27 says <i>"no rolling SFX"</i>, and the way that
    /// is kept is that the only collision this component reports is <c>OnCollisionEnter2D</c> —
    /// the moment of arrival. A rolling Smiley is in continuous contact and generates
    /// <c>OnCollisionStay2D</c> forever; that callback is deliberately absent, where §11's
    /// deformation deliberately has it.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(SmileyController))]
    public sealed class SmileyAudio : MonoBehaviour
    {
        [SerializeField] private GameAudio _audio;
        [SerializeField] private SmileyController _controller;

        private void Awake()
        {
            if (_controller == null)
            {
                _controller = GetComponent<SmileyController>();
            }

            if (_audio == null)
            {
                _audio = Object.FindFirstObjectByType<GameAudio>();
            }
        }

        private void OnEnable()
        {
            if (_controller != null)
            {
                _controller.Launched += OnLaunched;
            }
        }

        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.Launched -= OnLaunched;
            }
        }

        private void OnLaunched(float charge)
        {
            // §27: "Launch has SFX."
            _audio?.PlayLaunch(charge);
        }

        /// <summary>
        /// §27: collisions have SFX, and the volume depends on the impact force.
        /// </summary>
        /// <remarks>
        /// ⓘ The solver's own impulse, not the approach speed — the same reading §11 takes, and
        /// for the same reason: a glancing touch at speed is a quieter event than a head-on
        /// landing, which is what "force" means.
        /// </remarks>
        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (_audio == null || collision.contactCount == 0)
            {
                return;
            }

            float strongest = 0f;

            for (int i = 0; i < collision.contactCount; i++)
            {
                strongest = Mathf.Max(strongest, collision.GetContact(i).normalImpulse);
            }

            _audio.PlayImpact(strongest);
        }

        private void Update()
        {
            if (_audio == null || _controller == null)
            {
                return;
            }

            // §27: the trajectory guide "has sound feedback. Sound changes according to drag
            // force." ⓘ Driven from the same two values §7's dots are drawn from, so the sound
            // and the picture can never disagree about how hard the shot is.
            _audio.SetGuide(_controller.IsDragging, _controller.DragCharge);
        }
    }
}
