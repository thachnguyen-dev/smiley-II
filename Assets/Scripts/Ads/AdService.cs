using System;
#if !UNITY_WEBGL
using Unity.Services.LevelPlay;
#endif
using UnityEngine;

namespace Smiley.Ads
{
    /// <summary>
    /// The ad SDK's one lifetime: initialised once, and the interstitial that both scenes use.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Why this exists (D-125).</b> The interstitial used to live in <see cref="MenuBanner"/>,
    /// which is in the Menu scene and nowhere else — a fixture asserts the Game scene has no
    /// <see cref="MenuBanner"/> in it, which is how <b>D-004</b> ("ads in menus only") is made
    /// structural rather than remembered. D-124 then asked for an interstitial when §23's gear
    /// menu opens, and that menu is in the <i>Game</i> scene, where nothing could reach the SDK.
    /// So the SDK's lifetime and the interstitial move here, to one object that survives both
    /// scenes, and <see cref="MenuBanner"/> keeps the banner alone — D-004 and its fixture
    /// untouched.
    /// </para>
    /// <para>
    /// ⛔ <b>This and <see cref="MenuBanner"/> are the only files that name a LevelPlay type.</b>
    /// Every call here was read from the installed 9.5.1 package source, not from memory.
    /// </para>
    /// <para>
    /// ⛔ <b>Nothing in the game waits on an ad.</b> No consent, no purchase, no configuration,
    /// no connection, a cap or a failed display all take the same path: the continuation runs at
    /// once. ⓘ In the Editor the SDK is never touched, so every fixture exercises that path.
    /// </para>
    /// <para>
    /// ⭐ <b>D-138: on the web every LevelPlay line is compiled out.</b> The class stays, with
    /// the same public surface, so the scenes and fixtures that name it still build; it simply
    /// always takes the "no ad" path, and <see cref="ShowInterstitialThen"/> runs its
    /// continuation at once.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class AdService : MonoBehaviour
    {
        [SerializeField] private AdSettings _settings;

        private static bool _initialised;
        private static bool _initialising;
        private static double _interstitialLastShown = InterstitialRule.Never;

#if !UNITY_WEBGL
        private LevelPlayInterstitialAd _interstitial;
#endif
        private Action _afterInterstitial;

        /// <summary>The one instance, alive across both scenes, or null before the Menu loads.</summary>
        public static AdService Instance { get; private set; }

        /// <summary>The owner's ad configuration, for whoever needs to read it.</summary>
        public AdSettings Settings => _settings;

        /// <summary>What the interstitial is doing, for tests and the log.</summary>
        public string InterstitialStatus { get; private set; } = "not configured";

        /// <summary>Raised when the SDK is ready, or at once if it already was.</summary>
        private event Action Ready;

        /// <summary>Whether the SDK has finished initialising this process.</summary>
        public bool IsReady => _initialised;

        /// <summary>Forgets the session's last-shown time and the SDK's state. For tests.</summary>
        public static void ResetForTests()
        {
            _interstitialLastShown = InterstitialRule.Never;
            _initialised = false;
            _initialising = false;
        }

        /// <summary>
        /// Whether advertising may run at all: consent (D-107), the purchase (D-124), and
        /// something configured to ask for.
        /// </summary>
        public bool AdsAllowed
        {
            get
            {
                if (_settings == null || !_settings.IsConfigured)
                {
                    return false;
                }

                if (!RemoveAdsRule.AdsMayRun(PurchasedState.AdsRemoved))
                {
                    return false;
                }

                return ConsentRule.AdsMayRun(PurchasedState.Consent, _settings.ChildDirected);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // The Menu scene is loaded again on every return; the first one keeps the SDK.
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start() => Refresh();

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

#if !UNITY_WEBGL
            LevelPlay.OnInitSuccess -= OnInitSuccess;
            LevelPlay.OnInitFailed -= OnInitFailed;
            _interstitial?.DestroyAd();
            _interstitial = null;
#endif
        }

        /// <summary>
        /// Starts the SDK if it may run, or tears the interstitial down if it no longer may.
        /// Called at start, when consent changes, and when Remove Ads is bought.
        /// </summary>
        public void Refresh()
        {
            if (!AdsAllowed)
            {
                InterstitialStatus = RemoveAdsRule.AdsMayRun(PurchasedState.AdsRemoved)
                    ? "not allowed"
                    : "removed: bought";

#if !UNITY_WEBGL
                _interstitial?.DestroyAd();
                _interstitial = null;
#endif
                return;
            }

            if (Application.isEditor)
            {
                InterstitialStatus = "editor: skipped";
                return;
            }

#if UNITY_WEBGL
            InterstitialStatus = "web: no ads";
#else

            if (_initialised)
            {
                LoadInterstitial();
                Ready?.Invoke();
                return;
            }

            LevelPlay.OnInitSuccess += OnInitSuccess;
            LevelPlay.OnInitFailed += OnInitFailed;

            if (_initialising)
            {
                return;
            }

            _initialising = true;

            // ⛔ iOS asks Apple's tracking question BEFORE the SDK starts, because the SDK reads
            // the IDFA while initialising and Apple's rule is that it may not be read until the
            // player has answered — asking afterwards is the same as not asking (D-127).
            //
            // ⓘ On Android, in the Editor, and wherever the native side is absent, this calls
            // straight through on the same frame. ⛔ The Android app on Play must not change
            // because iOS gained a prompt, and a fixture asserts initialisation still completes.
            //
            // ⚠ The answer does NOT decide whether ads run. A player who declines still sees
            // them, just not personalised — which is already this project's posture, since D-110
            // turns personalisation off for everyone regardless.
            Smiley.Device.TrackingConsent.Request(StartTheSdk);
#endif
        }

#if !UNITY_WEBGL
        private void StartTheSdk()
        {
            if (_settings == null)
            {
                return;
            }

            // ⛔ Privacy flags go in BEFORE Init, from the one audience fact (D-105, D-110).
            AdsPrivacyRule privacy = AdsPrivacyRule.For(_settings.ChildDirected);
            LevelPlayPrivacySettings.SetCOPPA(privacy.Coppa);
            LevelPlayPrivacySettings.SetGDPRConsent(privacy.GdprConsent);
            LevelPlayPrivacySettings.SetCCPA(privacy.CcpaDoNotSell);
            Debug.Log($"[ADS] privacy: coppa={privacy.Coppa} gdprConsent={privacy.GdprConsent} ccpaDoNotSell={privacy.CcpaDoNotSell}");

            LevelPlay.Init(_settings.AppKey);
        }
#endif

        /// <summary>Runs <paramref name="then"/> now if the SDK is ready, or when it becomes ready.</summary>
        public void WhenReady(Action then)
        {
            if (then == null)
            {
                return;
            }

            if (_initialised)
            {
                then();
                return;
            }

            Ready += then;
        }

#if !UNITY_WEBGL
        private void OnInitSuccess(LevelPlayConfiguration configuration)
        {
            _initialised = true;
            _initialising = false;
            LevelPlay.OnInitSuccess -= OnInitSuccess;
            LevelPlay.OnInitFailed -= OnInitFailed;

            if (this == null || !isActiveAndEnabled)
            {
                return;
            }

            LoadInterstitial();

            Action ready = Ready;
            Ready = null;
            ready?.Invoke();
        }

        private void OnInitFailed(LevelPlayInitError error)
        {
            _initialising = false;
            LevelPlay.OnInitSuccess -= OnInitSuccess;
            LevelPlay.OnInitFailed -= OnInitFailed;
            InterstitialStatus = "init failed: " + error.ErrorMessage;
            Debug.Log("[ADS] init failed — no ads this session: " + error.ErrorMessage);
        }

        /// <summary>Loads the next interstitial so one is ready at the moments that show it.</summary>
        private void LoadInterstitial()
        {
            if (!_settings.HasInterstitial || _interstitial != null)
            {
                return;
            }

            _interstitial = new LevelPlayInterstitialAd(_settings.InterstitialAdUnitId);
            _interstitial.OnAdLoaded += info => { InterstitialStatus = "ready"; Debug.Log("[ADS] interstitial loaded: " + info.AdUnitName); };
            _interstitial.OnAdLoadFailed += error => { InterstitialStatus = "load failed: " + error.ErrorMessage; Debug.Log("[ADS] interstitial load failed: " + error.ErrorMessage); };
            _interstitial.OnAdDisplayed += info => { InterstitialStatus = "showing"; Debug.Log("[ADS] interstitial shown"); };
            _interstitial.OnAdDisplayFailed += (info, error) => { Debug.Log("[ADS] interstitial display failed: " + error.ErrorMessage); Continue(); };
            _interstitial.OnAdClosed += info => { Debug.Log("[ADS] interstitial closed"); Continue(); LoadNext(); };

            InterstitialStatus = "loading";
            _interstitial.LoadAd();
        }

        /// <summary>A shown interstitial is spent; the next one is loaded for the next moment.</summary>
        private void LoadNext()
        {
            _interstitial?.DestroyAd();
            _interstitial = null;

            if (AdsAllowed && !Application.isEditor && _initialised)
            {
                LoadInterstitial();
            }
        }
#endif

        /// <summary>
        /// Shows an interstitial if one is ready and the cap allows, then runs
        /// <paramref name="then"/>; otherwise runs it at once.
        /// </summary>
        /// <returns>Whether an ad is being shown first.</returns>
        public bool ShowInterstitialThen(Action then)
        {
            double now = Time.realtimeSinceStartup;
#if UNITY_WEBGL
            bool ready = false;
#else
            bool ready = _interstitial != null && _initialised && AdsAllowed && _interstitial.IsAdReady();
#endif
            bool allowed = _settings != null &&
                           InterstitialRule.MayShow(_interstitialLastShown, now, _settings.InterstitialMinIntervalSeconds);

            if (!ready || !allowed)
            {
                Debug.Log($"[ADS] interstitial skipped: ready={ready} allowed={allowed}");
                then?.Invoke();
                return false;
            }

            _afterInterstitial = then;
            _interstitialLastShown = now;
#if !UNITY_WEBGL
            _interstitial.ShowAd();
#endif
            return true;
        }

        private void Continue()
        {
            Action then = _afterInterstitial;
            _afterInterstitial = null;
            then?.Invoke();
        }
    }
}
