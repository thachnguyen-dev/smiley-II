using System;
using Smiley.Flow;
using UnityEngine;

namespace Smiley.Web
{
    /// <summary>
    /// Whether this is the browser build (D-138). The one place the game asks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⓘ <b>A runtime property over a compile-time fact, on purpose.</b> The flow files that behave
    /// differently on the web (<see cref="CompletionFlow"/>, the Main Menu, the pointer) read this
    /// instead of carrying their own <c>#if UNITY_WEBGL</c>, so they compile identically on every
    /// platform and a fixture can turn the web branch on in the Editor.
    /// </para>
    /// <para>
    /// ⛔ <b>False in the Editor, whatever the build target.</b> Every existing fixture was written
    /// for the phone flow, and switching the Editor to WebGL must not quietly change what they test.
    /// </para>
    /// </remarks>
    public static class WebPlatform
    {
        /// <summary>What this player was built as.</summary>
        public const bool Compiled =
#if UNITY_WEBGL && !UNITY_EDITOR
            true;
#else
            false;
#endif

        /// <summary>Lets a fixture exercise the web branch. Null means "what was compiled".</summary>
        public static bool? OverrideForTests { get; set; }

        /// <summary>Whether the browser rules apply right now.</summary>
        public static bool Active => OverrideForTests ?? Compiled;
    }

    /// <summary>A key/value store shaped like <c>PlayerPrefs</c>, which is what portal SDKs offer.</summary>
    public interface IKeyValueStore
    {
        bool HasKey(string key);
        string GetString(string key);
        void SetString(string key, string value);
        void DeleteKey(string key);
    }

    /// <summary>
    /// What the game needs from a web portal's SDK, and nothing more.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>An interface, because <c>Smiley.Runtime</c> cannot name the SDK.</b> The portal's
    /// package is imported by hand and may live in <c>Assembly-CSharp</c>, which an assembly
    /// definition cannot reference. The adapter lives outside the asmdef
    /// (<c>Assets/Web/CrazyGamesBackend.cs</c>) and registers itself here.
    /// </remarks>
    public interface IPortalBackend : IKeyValueStore
    {
        /// <summary>For the log.</summary>
        string Name { get; }

        /// <summary>Whether the SDK is present and allowed on this page.</summary>
        bool IsAvailable { get; }

        /// <summary>Starts the SDK; <paramref name="ready"/> runs when it has finished.</summary>
        void Init(Action ready);

        void GameplayStart();
        void GameplayStop();
    }

    /// <summary>
    /// The web portal's one lifetime: initialised once, then gameplay events and the save store.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>D-138.</b> CrazyGames asks for three things this game uses: <c>Gameplay start</c> /
    /// <c>stop</c> around real play, the Data module for progress, and nothing called before
    /// initialisation finishes. This class is all three, and is inert when no backend registered
    /// — which is every platform except a web build with the SDK imported.
    /// </para>
    /// <para>
    /// ⛔ <b>Nothing waits on the portal for ever.</b> An SDK that never answers is given
    /// <see cref="InitTimeoutSeconds"/>, after which the game carries on with local saves and no
    /// events, the same as a page the SDK does not run on. ⚠ A late answer after that is ignored,
    /// so a session never switches save stores halfway through.
    /// </para>
    /// </remarks>
    public static class Portal
    {
        /// <summary>How long the menu waits for the SDK before going on without it.</summary>
        public const float InitTimeoutSeconds = 10f;

        private enum State { NotStarted, Initialising, Ready, Failed }

        private static IPortalBackend _backend;
        private static State _state = State.NotStarted;
        private static Action _waiting;
        private static bool _playing;

        /// <summary>Called by the adapter, once, before the first scene.</summary>
        public static void Register(IPortalBackend backend) => _backend = backend;

        /// <summary>Whether a portal SDK is here and willing.</summary>
        public static bool Present
        {
            get
            {
                if (_backend == null)
                {
                    return false;
                }

                try
                {
                    return _backend.IsAvailable;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[PORTAL] availability check failed: " + e.Message);
                    return false;
                }
            }
        }

        /// <summary>Whether the SDK finished initialising this session.</summary>
        public static bool IsReady => _state == State.Ready;

        /// <summary>
        /// The portal's save store, or null — in which case saves go to local files as before.
        /// </summary>
        public static IKeyValueStore Storage => _state == State.Ready ? _backend : null;

        /// <summary>Whether the portal has been told play is under way. For tests and the log.</summary>
        public static bool IsPlaying => _playing;

        /// <summary>
        /// Runs <paramref name="then"/> once the portal is ready, failed, or simply absent.
        /// </summary>
        /// <remarks>ⓘ With no portal this runs on the same frame, so callers need no second path.</remarks>
        public static void WhenReady(Action then)
        {
            if (then == null)
            {
                return;
            }

            if (_state == State.Ready || _state == State.Failed || !Present)
            {
                then();
                return;
            }

            _waiting += then;

            if (_state == State.Initialising)
            {
                return;
            }

            _state = State.Initialising;
            PortalRunner.Ensure().StartTimeout(InitTimeoutSeconds);

            try
            {
                Debug.Log("[PORTAL] initialising " + _backend.Name);
                _backend.Init(OnReady);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PORTAL] init threw — continuing without it: " + e.Message);
                Fail();
            }
        }

        private static void OnReady()
        {
            if (_state != State.Initialising)
            {
                Debug.LogWarning("[PORTAL] answered after the timeout — ignored for this session");
                return;
            }

            _state = State.Ready;
            Debug.Log("[PORTAL] ready");
            Flush();
        }

        /// <summary>The runner's clock ran out.</summary>
        internal static void TimedOut()
        {
            if (_state == State.Initialising)
            {
                Debug.LogWarning("[PORTAL] no answer in " + InitTimeoutSeconds + " s — continuing without it");
                Fail();
            }
        }

        private static void Fail()
        {
            _state = State.Failed;
            Flush();
        }

        private static void Flush()
        {
            Action waiting = _waiting;
            _waiting = null;
            waiting?.Invoke();
        }

        /// <summary>Tells the portal play started or stopped. Sends only on a change.</summary>
        public static void SetPlaying(bool playing)
        {
            if (_state != State.Ready || playing == _playing)
            {
                return;
            }

            _playing = playing;

            try
            {
                if (playing)
                {
                    _backend.GameplayStart();
                }
                else
                {
                    _backend.GameplayStop();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PORTAL] gameplay event failed: " + e.Message);
            }
        }

        /// <summary>Installs a backend that is already initialised. For tests.</summary>
        public static void ForceReadyForTests(IPortalBackend backend)
        {
            ResetForTests();
            _backend = backend;
            _state = State.Ready;
        }

        /// <summary>Back to "no portal". For tests.</summary>
        public static void ResetForTests()
        {
            _backend = null;
            _state = State.NotStarted;
            _waiting = null;
            _playing = false;
        }
    }

    /// <summary>
    /// When the portal should hear that play is under way. A rule, so it is arithmetic.
    /// </summary>
    /// <remarks>
    /// ⭐ CrazyGames: <i>start</i> whenever the player starts or resumes playing, <i>stop</i> on
    /// every break — menus, level ends, pausing. ⓘ So play is exactly: the climb is on screen, no
    /// story, tutorial or Completed screen covers it, and §23's gear menu is shut. ⛔ Not stopped
    /// when the tab loses focus — their docs say not to.
    /// </remarks>
    public static class GameplayRule
    {
        public static bool IsPlaying(bool inTheClimb, CompletionPhase phase, bool gearMenuOpen) =>
            inTheClimb && phase == CompletionPhase.Climbing && !gearMenuOpen;
    }
}
