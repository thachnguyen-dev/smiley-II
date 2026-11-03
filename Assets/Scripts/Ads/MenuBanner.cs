#if !UNITY_WEBGL
using Unity.Services.LevelPlay;
#endif
using UnityEngine;

namespace Smiley.Ads
{
    /// <summary>
    /// The one banner: at the bottom of the Main Menu, and nowhere else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ D-003: banner advertising was the sole monetisation, amended by D-122 (interstitials)
    /// and D-124 (the Remove Ads purchase). ⛔ <b>D-004 is untouched: the banner is in menus only,
    /// never during gameplay</b> — this component lives in the Menu scene, and a fixture asserts
    /// the Game scene has none. Leaving the menu destroys the banner; coming back makes a new one.
    /// </para>
    /// <para>
    /// ⚠ <b>The SDK's lifetime is NOT here any more (D-125).</b> <see cref="AdService"/> owns
    /// initialisation and the interstitial, because the interstitial is needed in both scenes and
    /// this component may only exist in one. This file is now the banner and nothing else.
    /// </para>
    /// <para>
    /// ⛔ <b>Nothing waits on an ad.</b> No load, no init and no failure changes what the menu
    /// does; the phone in aeroplane mode simply has no banner, and so does a player who bought
    /// Remove Ads (D-124). ⓘ In the Editor the SDK is not touched at all.
    /// </para>
    /// <para>
    /// ⭐ <b>D-138: on the web the banner's LevelPlay lines are compiled out</b>, and it always
    /// reports why there is none. The component stays so the Menu scene builds unchanged.
    /// </para>
    /// </remarks>
    public sealed class MenuBanner : MonoBehaviour
    {
        [SerializeField] private AdSettings _settings;

#if !UNITY_WEBGL
        private LevelPlayBannerAd _banner;
#endif

        /// <summary>What the banner is doing, for tests and the log.</summary>
        public string Status { get; private set; } = "idle";

        /// <summary>The audience declaration, read by the menu to decide whether to ask (D-110).</summary>
        public bool ChildDirected => _settings != null && _settings.ChildDirected;

        /// <summary>Whether this component would talk to the SDK at all here.</summary>
        public bool WouldRun => _settings != null && _settings.IsConfigured && !Application.isEditor;

        private void Start() => Refresh();

        private void OnDestroy()
        {
#if !UNITY_WEBGL
            _banner?.DestroyAd();
            _banner = null;
#endif
        }

        /// <summary>
        /// Shows the banner if everything allows it, or tears it down if something no longer
        /// does. Called at start, when the consent answer changes (D-107), and when Remove Ads
        /// is bought (D-124).
        /// </summary>
        public void Refresh()
        {
            AdService service = AdService.Instance;

            if (service == null || !service.AdsAllowed)
            {
                Status = StatusWhenRefused();
#if !UNITY_WEBGL
                _banner?.DestroyAd();
                _banner = null;
#endif
                return;
            }

            if (Application.isEditor)
            {
                Status = "editor: skipped";
                return;
            }

            Status = "initialising";
            service.WhenReady(CreateBanner);
        }

        /// <summary>Why there is no banner, in the words the fixtures read.</summary>
        private string StatusWhenRefused()
        {
            if (_settings == null || !_settings.IsConfigured)
            {
                return "not configured";
            }

            if (PurchasedState.AdsRemoved)
            {
                return "removed: bought";
            }

            ParentalConsent consent = PurchasedState.Consent;

            if (!ConsentRule.AdsMayRun(consent, ChildDirected))
            {
                return consent == ParentalConsent.Refused ? "refused: no ads" : "awaiting parental consent";
            }

            return "not configured";
        }

        private void CreateBanner()
        {
#if !UNITY_WEBGL
            // ⚠ Re-checked here, not only at Refresh: WhenReady may run a second or two later,
            // and the player can have bought Remove Ads in between.
            if (this == null || !isActiveAndEnabled || _banner != null || AdService.Instance == null || !AdService.Instance.AdsAllowed)
            {
                return;
            }

            LevelPlayBannerAd.Config config = new LevelPlayBannerAd.Config.Builder()
                .SetSize(LevelPlayAdSize.BANNER)
                .SetPosition(LevelPlayBannerPosition.BottomCenter)
                .SetRespectSafeArea(true)
                .SetDisplayOnLoad(true)
                .Build();

            _banner = new LevelPlayBannerAd(_settings.BannerAdUnitId, config);
            _banner.OnAdLoaded += info => { Status = "showing"; Debug.Log("[ADS] banner loaded: " + info.AdUnitName); };
            _banner.OnAdLoadFailed += error => { Status = "load failed: " + error.ErrorMessage; Debug.Log("[ADS] banner load failed: " + error.ErrorMessage); };

            Status = "loading";
            _banner.LoadAd();
#endif
        }
    }
}
