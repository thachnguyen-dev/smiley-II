using Smiley.Config;
using UnityEngine;

namespace Smiley.Audio
{
    /// <summary>
    /// §27's audio, and the one place volumes are applied.
    /// </summary>
    /// <remarks>
    /// <para>
    /// §27 in full, and every clause of it is a decision this class either makes or refuses:
    /// <list type="bullet">
    /// <item><b>Background music plays throughout gameplay.</b></item>
    /// <item><b>Collisions have SFX, and the volume depends on impact force</b> — light impact
    /// quieter, strong impact louder.</item>
    /// <item><b>Launch has SFX.</b></item>
    /// <item><b>The trajectory guide has sound feedback, changing with drag force.</b></item>
    /// <item>⛔ <b>NO rolling SFX.</b></item>
    /// <item>⛔ <b>NO SFX for the Disappearing Platform</b> — neither near expiry nor respawn.
    /// §14.3 says it twice and §27 repeats it.</item>
    /// <item>⛔ <b>NO special SFX for the Top Checkpoint.</b></item>
    /// </list>
    /// </para>
    /// <para>
    /// ⚠ <b>THE FOUR "NO" CLAUSES ARE HALF THE SPECIFICATION, and they are kept by this class
    /// having no method that could serve them.</b> There is no <c>PlayRoll</c>, no
    /// <c>PlayVanish</c>, no <c>PlayCheckpoint</c> — a sound that cannot be asked for cannot be
    /// added by accident, and a test asserts the surface stays that small.
    /// </para>
    /// <para>
    /// ⏸ <b>EVERY CLIP IS A GENERATED PLACEHOLDER (Q-049).</b> They are synthesised tones written
    /// by the editor build so that the system is audibly working rather than silently untested —
    /// ⛔ they are <b>not</b> a sound design, and §32 reserves that to the owner. Replacing the
    /// files replaces the audio; nothing else has to change.
    /// </para>
    /// </remarks>
    public sealed class GameAudio : MonoBehaviour
    {
        [SerializeField] private SmileyTuning _tuning;

        [SerializeField] private AudioClip _music;
        [SerializeField] private AudioClip _impact;
        [SerializeField] private AudioClip _launch;
        [SerializeField] private AudioClip _guide;

        private AudioSource _musicSource;
        private AudioSource _sfxSource;
        private AudioSource _guideSource;

        private GameSettings _settings;

        /// <summary>The live settings. §26's sliders write through this.</summary>
        /// <remarks>
        /// ⚠ <b>Read on demand rather than only in <c>Awake</c>.</b> Unity does not order Awake
        /// between components, and §26's screen builds its sliders from these the moment it is
        /// enabled — which threw a NullReferenceException on whichever run happened to enable
        /// the menu first. ⓘ A property that cannot be null is a smaller fix than an execution
        /// order that has to be maintained.
        /// </remarks>
        public GameSettings Settings => _settings ??= SettingsFile.Read();

        /// <summary>The four clip names, for tests: which sounds this scene actually carries.</summary>
        public string ClipReport =>
            "music=" + (_music != null ? _music.name : "none") + " landing=" + (_impact != null ? _impact.name : "none") +
            " launch=" + (_launch != null ? _launch.name : "none") + " drag=" + (_guide != null ? _guide.name : "none");

        /// <summary>Whether the music is playing right now. For tests.</summary>
        public bool MusicIsPlaying => _musicSource != null && _musicSource.isPlaying;

        /// <summary>The volume the last collision was played at. For tests.</summary>
        public float LastImpactVolume { get; private set; } = -1f;

        /// <summary>Whether the guide tone is sounding. For tests.</summary>
        public bool GuideIsSounding => _guideSource != null && _guideSource.isPlaying;

        /// <summary>The pitch the guide tone is at. For tests.</summary>
        public float GuidePitch => _guideSource != null ? _guideSource.pitch : 0f;

        /// <summary>Whether the drag cue repeats while a drag is held. ⛔ Never, since D-100. For tests.</summary>
        public bool GuideLoops => _guideSource != null && _guideSource.loop;

        /// <summary>How many times the drag cue has started. For tests: once per drag.</summary>
        public int GuideStarts { get; private set; }

        private bool _dragging;

        private void Awake()
        {
            _settings ??= SettingsFile.Read();

            _musicSource = CreateSource("Music", loop: true);
            _sfxSource = CreateSource("SFX", loop: false);
            // ⭐ Owner, 2026-09-12 (D-100): "SFX should not loop. Each SFX should play once per
            // corresponding event." The drag cue plays once when a drag begins and is not repeated.
            _guideSource = CreateSource("Guide", loop: false);

            _musicSource.clip = _music;
            _guideSource.clip = _guide;

            ApplyVolumes();
        }

        private AudioSource CreateSource(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, worldPositionStays: false);

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;

            // ⚠ 2D, flatly. A positional source would make Smiley quieter at the edges of the
            // screen, and §33 forbids the display changing the game — the same rule that keeps
            // the map in world units rather than screens.
            source.spatialBlend = 0f;

            return source;
        }

        private void Start()
        {
            // §27: "Background music plays throughout gameplay."
            if (_music != null)
            {
                _musicSource.Play();
            }
        }

        /// <summary>Re-reads both §26 volumes onto the live sources.</summary>
        public void ApplyVolumes()
        {
            GameSettings settings = Settings;
            settings.Clamp();

            if (_musicSource != null)
            {
                _musicSource.volume = settings.MusicVolume;
            }

            if (_guideSource != null)
            {
                _guideSource.volume = AudioLevels.Mix(
                    _tuning != null ? _tuning.GuideCueVolume : 0.4f, settings.SfxVolume);
            }
        }

        /// <summary>Stores §26's settings. Called when a slider moves.</summary>
        public void SaveSettings()
        {
            ApplyVolumes();
            SettingsFile.Write(Settings);
        }

        /// <summary>
        /// §27: a collision, at a volume that depends on the impact force.
        /// </summary>
        /// <param name="impulse">The contact impulse the solver applied.</param>
        public void PlayImpact(float impulse)
        {
            if (_tuning == null || _sfxSource == null || _impact == null)
            {
                return;
            }

            float resting = _tuning.Mass * Physics2D.gravity.magnitude * Time.fixedDeltaTime
                            * _tuning.RestingImpulseTolerance;

            float level = AudioLevels.ImpactVolume(impulse, resting, _tuning.LoudestImpulse);

            if (level < _tuning.QuietestAudibleImpact)
            {
                // ⛔ TWO OF §27's RULES LIVE IN THIS RETURN, and the second one was found by
                // testing rather than by reading.
                //
                // A resting Smiley delivers a contact impulse every step, which the resting
                // subtraction already removes. But a body as lumpy as §11's does not settle
                // cleanly: it micro-bounces on its own bumps for a second or two after landing,
                // and each of those is a genuine OnCollisionEnter2D. Played, they are a rattle
                // from a character standing still — which is what §27's "no rolling SFX" is
                // protecting against, arriving by a route the rule does not name.
                //
                // ⓘ Below this level a soft body makes no audible sound at all, so the floor is
                // physical rather than a mute switch on a real event.
                return;
            }

            LastImpactVolume = AudioLevels.Mix(level, Settings.SfxVolume);
            _sfxSource.PlayOneShot(_impact, LastImpactVolume);
        }

        /// <summary>§27: "Launch has SFX."</summary>
        public void PlayLaunch(float charge)
        {
            if (_sfxSource == null || _launch == null || _tuning == null)
            {
                return;
            }

            float level = Mathf.Lerp(_tuning.LaunchQuietest, 1f, Mathf.Clamp01(charge));
            _sfxSource.PlayOneShot(_launch, AudioLevels.Mix(level, Settings.SfxVolume));
        }

        /// <summary>
        /// §27: the guide's sound feedback, which changes with drag force.
        /// </summary>
        /// <remarks>
        /// ⭐ D-100: the cue plays <b>once</b>, at the moment a drag begins, and runs to its own
        /// end — never looped, never restarted while the finger is down. §27's pitch still
        /// follows the drag force for as long as the clip lasts. Releasing stops whatever is
        /// left of it, so the launch cue is not heard over the tail of the drag cue.
        /// </remarks>
        /// <param name="dragging">Whether a drag is in progress.</param>
        /// <param name="charge">How far through §6's Maximum Drag Distance.</param>
        public void SetGuide(bool dragging, float charge)
        {
            if (_guideSource == null || _guide == null || _tuning == null)
            {
                return;
            }

            if (!dragging)
            {
                if (_dragging && _guideSource.isPlaying)
                {
                    _guideSource.Stop();
                }

                _dragging = false;
                return;
            }

            _guideSource.pitch = AudioLevels.GuidePitch(
                charge, _tuning.GuidePitchLowest, _tuning.GuidePitchHighest);

            if (!_dragging)
            {
                _dragging = true;
                _guideSource.Play();
                GuideStarts++;
            }
        }
    }
}
