using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Smiley.View
{
    /// <summary>
    /// Plays Smiley's idle animations one after another, in random order, forever.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ Owner, 2026-09-11 (D-086): an <b>idle</b> animation system — not driven by collisions,
    /// jumps, landings or any gameplay event. Nothing here listens to physics, the controller or
    /// the flow. A clip plays to its end, <see cref="IdlePicker"/> chooses a different one, and
    /// that plays. That is the whole behaviour.
    /// </para>
    /// <para>
    /// ⭐ <b>The clips are the owner's</b> (Q-053): make them in Unity's Animation window with the
    /// <c>Sprite</c> object selected, then drag them into <c>Idles</c> here. Any
    /// <see cref="AnimationClip"/> works — sprite-swap frames, a breathing scale, a blink —
    /// and none needs the Legacy flag. ⚠ The Animation window also creates an Animator
    /// Controller and assigns it; this component clears that at start, because it plays the
    /// clips itself through a <see cref="PlayableGraph"/> and a controller running beside it
    /// would fight for the same properties.
    /// </para>
    /// <para>
    /// ⓘ Lives on <c>Art</c> — the object with the renderer — under <c>Sprite</c>, inside the
    /// deformer's frame. The squash (§11, §12) scales the frame above it and never this object,
    /// so a clip that animates this object's scale, position or sprite composes with the
    /// deformation rather than fighting it. <c>Art</c> sits at the bottom of the physics circle
    /// and every frame's pivot is its own bottom edge (D-092), so his feet stay on the platform
    /// whatever a frame draws. ⛔ Never on Smiley's root: the physics body is there.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Animator))]
    public sealed class IdleAnimator : MonoBehaviour
    {
        [Tooltip("⭐ Yours. Every idle animation Smiley has; the game plays them in random order, " +
                 "never the same one twice in a row. Empty means he holds still.")]
        [SerializeField] private AnimationClip[] _idles = new AnimationClip[0];

        private Animator _animator;
        private PlayableGraph _graph;
        private System.Random _random;
        private int _current = -1;
        private float _remaining;

        /// <summary>The index in <c>Idles</c> of the clip playing now, or -1. For tests.</summary>
        public int Current => _current;

        /// <summary>How many clips the owner has assigned.</summary>
        public int Count => _idles != null ? _idles.Length : 0;

        /// <summary>The owner's clips. For the build's art fitter and for tests.</summary>
        public AnimationClip[] Clips => _idles ?? new AnimationClip[0];

        /// <summary>Whether a clip is playing right now.</summary>
        public bool IsPlaying => _graph.IsValid() && _graph.IsPlaying();

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _random = new System.Random();

            // See the remarks: the Animation window leaves a controller here, and two things
            // animating one sprite is one too many.
            if (_animator.runtimeAnimatorController != null)
            {
                _animator.runtimeAnimatorController = null;
            }
        }

        private void Update()
        {
            if (Count == 0)
            {
                return;
            }

            if (!_graph.IsValid())
            {
                PlayNext();
                return;
            }

            _remaining -= Time.deltaTime;

            if (_remaining <= 0f)
            {
                PlayNext();
            }
        }

        /// <summary>Stops the current clip and starts a different one. Public for tests.</summary>
        public void PlayNext()
        {
            int next = IdlePicker.Next(Count, _current, _random);

            if (next < 0)
            {
                Stop();
                return;
            }

            AnimationClip clip = _idles[next];

            if (clip == null)
            {
                // A hole in the owner's list is skipped, not a crash; the next Update tries again.
                _current = next;
                _remaining = 0f;
                return;
            }

            Stop();
            AnimationPlayableUtilities.PlayClip(_animator, clip, out _graph);

            _current = next;
            _remaining = Mathf.Max(clip.length, 1f / 60f);
        }

        /// <summary>Plays one particular clip. For tests; play picks its own.</summary>
        public void Play(int index)
        {
            if (index < 0 || index >= Count || _idles[index] == null)
            {
                return;
            }

            Stop();
            AnimationPlayableUtilities.PlayClip(_animator, _idles[index], out _graph);
            _current = index;
            _remaining = Mathf.Max(_idles[index].length, 1f / 60f);
        }

        /// <summary>
        /// Advances the playing clip by hand and evaluates it now. For tests — batch-mode frames
        /// barely advance time (D-062), and a test about every frame of a clip needs to reach them.
        /// </summary>
        public void Scrub(float seconds)
        {
            if (!_graph.IsValid())
            {
                return;
            }

            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            _graph.Evaluate(seconds);
        }

        /// <summary>Replaces the clip list. For tests and tooling; the owner uses the Inspector.</summary>
        public void SetClips(AnimationClip[] clips)
        {
            _idles = clips ?? new AnimationClip[0];
            _current = -1;
            Stop();
        }

        private void Stop()
        {
            if (_graph.IsValid())
            {
                _graph.Destroy();
            }
        }

        private void OnDisable() => Stop();

        private void OnDestroy() => Stop();
    }
}
