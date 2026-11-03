using Smiley.Audio;
using Smiley.Flow;
using Smiley.Save;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Smiley.View
{
    /// <summary>
    /// §22's Main Menu, §26's Settings and §25's Reset Game — behaviour only.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>THE LAYOUT IS THE OWNER'S (D-057).</b> This class builds nothing and positions
    /// nothing: it finds controls in <c>Assets/UI/MainMenu.uxml</c> <b>by name</b> and gives them
    /// behaviour. Move them, restyle them, re-parent them, add artwork around them — none of that
    /// reaches this file. ⛔ Only the names matter, and a build fails naming any that go missing
    /// (<c>UiLayoutGuard</c>).
    /// </para>
    /// <para>
    /// §22: the menu is <b>minimal</b> — one starting action chosen by whether a save exists, plus
    /// Settings and Exit / Quit. The choice itself is <see cref="MainMenuRule"/>, which has no
    /// Unity in it and is tested directly.
    /// </para>
    /// <para>
    /// ✅ <b>§26 is complete</b>: Music Volume, SFX Volume and Reset Game, with both volumes saved
    /// persistently. ⚠ §25 keeps settings, and that holds <b>structurally</b> — they live in
    /// their own file, which <see cref="SaveFile.DeleteAll"/> does not know about.
    /// </para>
    /// <para>
    /// ⛔ <b>AND THERE IS NO RESTART LEVEL ANYWHERE (§24)</b>, under any name or presentation.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MainMenuScreen : MonoBehaviour
    {
        [SerializeField] private GameAudio _audio;

        private UIDocument _document;

        private VisualElement _menu;
        private VisualElement _settings;
        private VisualElement _consent;
        private Label _consentQuestion;
        private TextField _consentAnswer;
        private Smiley.Ads.ParentGate _gate;
        private VisualElement _confirm;

        /// <summary>Which screen is showing. For tests.</summary>
        public string VisibleScreen
        {
            get
            {
                if (UiKit.IsVisible(_confirm)) return "confirm";
                if (UiKit.IsVisible(_settings)) return "settings";
                if (UiKit.IsVisible(_consent)) return "consent";
                return "menu";
            }
        }

        /// <summary>What §22 offers right now. For tests.</summary>
        public MainMenuAction Primary => MainMenuRule.PrimaryAction(SaveFile.Exists());

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            // ⚠ A menu the player reaches by finishing or by leaving a game must not carry the
            // last run's intent into the next one.
            GameSession.Reset();

            if (!Smiley.Web.WebPlatform.Active)
            {
                Bind();
                return;
            }

            // ⭐ D-138: on the web, nothing is read until the portal has answered — the save
            // lives in its Data module, so "is there a save?" has no answer before then. The
            // menu stays hidden meanwhile, so the player never sees New Game turn into Continue.
            SetMenuShown(false);
            Smiley.Web.Portal.WhenReady(OnPortalReady);
        }

        /// <summary>Whether this session's first menu has already decided to start at once.</summary>
        private static bool _bootHandled;

        /// <summary>Forgets that this session has booted. For tests.</summary>
        public static void ResetBootForTests() => _bootHandled = false;

        private void OnPortalReady()
        {
            if (this == null || !isActiveAndEnabled)
            {
                return;
            }

            Bind();

            if (StartAtOnceOnWeb())
            {
                return;
            }

            SetMenuShown(true);
        }

        /// <summary>
        /// D-139: a first-time web player goes straight into the climb, without the menu.
        /// </summary>
        /// <remarks>
        /// ⭐ Owner, 2026-10-19: CrazyGames lands new users in gameplay at once. ⓘ A player with a
        /// save still gets §22's menu and its Continue. ⛔ <b>Once per session, at boot only.</b>
        /// §25's Reset Game returns to this menu with no save, and must stay there — §25 says it
        /// returns to the Main Menu, it does not start a game.
        /// </remarks>
        private bool StartAtOnceOnWeb()
        {
            if (_bootHandled)
            {
                return false;
            }

            _bootHandled = true;

            if (SaveFile.Exists())
            {
                return false;
            }

            StartPlaying();
            return true;
        }

        private void SetMenuShown(bool shown)
        {
            VisualElement root = _document != null ? _document.rootVisualElement : null;

            if (root != null)
            {
                root.style.visibility = shown ? Visibility.Visible : Visibility.Hidden;
            }
        }

        /// <summary>Finds the owner's controls and gives them behaviour.</summary>
        private void Bind()
        {
            VisualElement root = _document.rootVisualElement;

            // ⭐ The owner's layout lives inside the safe area (D-072).
            UiKit.WrapInSafeArea(root);

            // D-114: the wrapper starts below the status bar, so the strip above it is the
            // document root's to paint. The colour is the owner's, in Meta.uss under .menu-root;
            // the Game scene's root must stay clear, so this class is added here only.
            root.AddToClassList("menu-root");

            _menu = root.Q("main-menu");
            _settings = root.Q("settings-screen");
            _consent = root.Q(UiKit.Names.ConsentScreen);
            _consentQuestion = root.Q<Label>(UiKit.Names.ConsentQuestion);
            _consentAnswer = root.Q<TextField>(UiKit.Names.ConsentAnswer);
            _confirm = root.Q("confirm-screen");

            bool saveExists = SaveFile.Exists();

            // ⛔ ONE starting action, not two with one disabled. §22 says show New Game OR
            // Continue, and MainMenuRule is where that is decided so it cannot drift. ⓘ Only the
            // LABEL is written here — the button itself is wherever the owner put it.
            UiKit.Bind(root, UiKit.Names.Primary, StartPlaying,
                MainMenuRule.PrimaryLabel(saveExists));

            UiKit.Bind(root, UiKit.Names.Settings, () => Show("settings"));
            Button exit = UiKit.Bind(root, UiKit.Names.Exit, Quit);

            // ⭐ D-139: a browser tab cannot be quit — Application.Quit does nothing there — so
            // the web build does not offer a button that does nothing. ⛔ Phones keep §22's Exit.
            if (Smiley.Web.WebPlatform.Active)
            {
                UiKit.SetVisible(exit, false);
            }

            // ⭐ D-124: the Remove Ads purchase, on the Main Menu by the owner's choice.
            _removeAds = UiKit.Bind(root, UiKit.Names.RemoveAds, BuyRemoveAds);
            string label = (_removeAds as Button)?.text;
            if (label != StoreUnavailableText)
            {
                _removeAdsLabel = label;
            }

            // ⭐ Apple 3.1.1's restore mechanism. Bound and placed once, then left alone:
            // RefreshRemoveAds never touches it, so no purchase can take it off the screen.
            _restore = UiKit.Bind(root, UiKit.Names.RestorePurchases, RestorePurchases);
            UiKit.SetVisible(_restore, Smiley.Ads.RemoveAdsRule.ShowRestoreButton(ApplePlatform));

            HookTheStore();

            UiKit.Bind(root, UiKit.Names.ResetGame, () => Show("confirm"));
            UiKit.Bind(root, UiKit.Names.Back, () => Show("menu"));
            UiKit.Bind(root, UiKit.Names.ResetConfirm, ResetGame);
            UiKit.Bind(root, UiKit.Names.ResetCancel, () => Show("settings"));

            GameSettings settings = _audio != null ? _audio.Settings : new GameSettings();

            UiKit.BindVolume(root, UiKit.Names.MusicVolume, settings.MusicVolume, SetMusicVolume);
            UiKit.BindVolume(root, UiKit.Names.SfxVolume, settings.SfxVolume, SetSfxVolume);

            // ⭐ D-107: the parental consent question, once, before the menu. The grant button
            // only works with the gate's answer typed in; refusal needs nothing.
            UiKit.Bind(root, UiKit.Names.ConsentGrant, GrantConsent);
            UiKit.Bind(root, UiKit.Names.ConsentRefuse, RefuseConsent);

            // Optional: a Settings button that reopens the question for a parent. Only shown
            // when the audience declaration makes the question meaningful (D-110).
            Button review = root.Q<Button>(UiKit.Names.ConsentReview);
            if (review != null)
            {
                UiKit.Bind(root, UiKit.Names.ConsentReview, AskForConsent);
                UiKit.SetVisible(review, ChildDirected && !Smiley.Web.WebPlatform.Active);
            }

            // ⭐ D-138: D-107's question gates ADVERTISING, and the web build has none — so it is
            // never asked there. A question about something that cannot happen is not consent.
            if (!Smiley.Web.WebPlatform.Active && Smiley.Ads.ConsentRule.MustAsk(Consent, ChildDirected))
            {
                AskForConsent();
            }
            else
            {
                Show("menu");
            }
        }

        /// <summary>The owner's audience declaration, from the banner's settings. For tests too.</summary>
        public bool ChildDirected
        {
            get
            {
                var banner = Object.FindFirstObjectByType<Smiley.Ads.MenuBanner>();
                return banner != null && banner.ChildDirected;
            }
        }

        /// <summary>The parent's stored answer. For the banner and for tests.</summary>
        public Smiley.Ads.ParentalConsent Consent =>
            Smiley.Ads.ConsentRule.FromStored(_audio != null ? _audio.Settings.ParentalConsent : 0);

        /// <summary>Shows the consent screen with a fresh gate question.</summary>
        public void AskForConsent()
        {
            _gate = Smiley.Ads.ParentGate.New(System.Environment.TickCount);

            if (_consentQuestion != null)
            {
                _consentQuestion.text = _gate.Prompt;
            }

            if (_consentAnswer != null)
            {
                _consentAnswer.value = string.Empty;
            }

            Show("consent");
        }

        /// <summary>The grant button: only a correct gate answer records consent.</summary>
        public void GrantConsent()
        {
            string typed = _consentAnswer != null ? _consentAnswer.value : string.Empty;

            if (!_gate.Check(typed))
            {
                // Wrong or empty: a new question, and the screen stays. Not an error — a child
                // pressing the button is exactly what the gate is for.
                AskForConsent();
                return;
            }

            StoreConsent(Smiley.Ads.ParentalConsent.Granted);
            Show("menu");
        }

        /// <summary>The refuse button. Ads stay off; the game is unchanged.</summary>
        public void RefuseConsent()
        {
            StoreConsent(Smiley.Ads.ParentalConsent.Refused);
            Show("menu");
        }

        private void StoreConsent(Smiley.Ads.ParentalConsent consent)
        {
            if (_audio != null)
            {
                _audio.Settings.ParentalConsent = (int)consent;
                _audio.SaveSettings();
            }

            var banner = Object.FindFirstObjectByType<Smiley.Ads.MenuBanner>();
            if (banner != null)
            {
                banner.Refresh();
            }
        }

        private VisualElement _removeAds;
        private VisualElement _restore;

        /// <summary>
        /// Starts the Remove Ads purchase (D-124).
        /// </summary>
        /// <remarks>
        /// <para>
        /// ⛔ Nothing here waits: a cancelled purchase leaves the menu as it is, and the row
        /// disappears when the purchase lands, through <see cref="Smiley.Ads.StorePurchases.Changed"/>.
        /// </para>
        /// <para>
        /// ⭐ <b>A press that cannot open the purchase sheet says so.</b> On iOS the button is
        /// shown before the store answers (5.6, 2026-10-24), so a press with no product must not be
        /// the silent no-op that 2.1.0 rejected (2026-10-14). The label says the store is
        /// unavailable for <see cref="UnavailableSeconds"/>, then goes back to what it was.
        /// </para>
        /// </remarks>
        private void BuyRemoveAds()
        {
            var store = Smiley.Ads.StorePurchases.Instance;

            if (store != null && (store.Purchased || store.Buy()))
            {
                return;
            }

            ShowStoreUnavailable();
        }

        /// <summary>What the Remove Ads button says when the store cannot sell right now.</summary>
        public const string StoreUnavailableText = "Store unavailable - try again later";

        /// <summary>How long that message stays before the button's own label returns.</summary>
        public const float UnavailableSeconds = 3f;

        /// <summary>The owner's label for the button, as laid out in the UXML.</summary>
        private string _removeAdsLabel;

        private void ShowStoreUnavailable()
        {
            if (_removeAds is Button button)
            {
                button.text = StoreUnavailableText;
            }

            // ⓘ Real time, not scaled: the message must clear even if time is ever paused here.
            if (_unavailable != null)
            {
                StopCoroutine(_unavailable);
            }

            _unavailable = StartCoroutine(EndStoreUnavailableLater());
        }

        private Coroutine _unavailable;

        private System.Collections.IEnumerator EndStoreUnavailableLater()
        {
            yield return new WaitForSecondsRealtime(UnavailableSeconds);
            _unavailable = null;
            EndStoreUnavailable();
        }

        private void EndStoreUnavailable()
        {
            if (_removeAds is Button button && _removeAdsLabel != null)
            {
                button.text = _removeAdsLabel;
            }

            // ⓘ The press asked the store again; if the product has arrived meanwhile, this puts
            // its price on the button.
            RefreshRemoveAds();
        }

        /// <summary>Presses Remove Ads as a player would. For tests.</summary>
        public void PressRemoveAdsForTests() => BuyRemoveAds();

        /// <summary>The Remove Ads button's current text, or null. For tests.</summary>
        public string RemoveAdsText => (_removeAds as Button)?.text;

        /// <summary>Asks the store to hand back a purchase this device has not heard about.</summary>
        private void RestorePurchases() => Smiley.Ads.StorePurchases.Instance?.Restore();

        /// <summary>
        /// Whether this is an Apple platform, which is the only thing the Restore button depends on.
        /// </summary>
        /// <remarks>
        /// ⓘ Read at runtime rather than compiled in, so the Editor reports the platform it is
        /// actually playing as and the rule is exercised rather than assumed.
        /// </remarks>
        private static bool ApplePlatform =>
            Application.platform == RuntimePlatform.IPhonePlayer ||
            Application.platform == RuntimePlatform.OSXPlayer ||
            Application.platform == RuntimePlatform.OSXEditor;

        /// <summary>Runs after every Awake, which is the first moment the store is reachable.</summary>
        private void Start() => HookTheStore();

        /// <summary>
        /// Reads the store and subscribes to it, from a place where it is certain to exist.
        /// </summary>
        /// <remarks>
        /// ⚠ <b>This used to run only from Bind(), and Bind() runs during Awake.</b> Unity does not
        /// define the order of Awake between two scene objects, so StorePurchases.Instance was
        /// sometimes still null here — and a null did two things, not one: it hid the button, and
        /// it skipped the subscription that was the only way the button could ever come back. The
        /// button then stayed hidden for the whole session. ⛔ It cost nothing to build, nothing to
        /// test and nothing to ship: the scene was regenerated, the object order flipped, and
        /// Remove Ads simply stopped appearing. Start() runs after every Awake has finished, so
        /// calling it from both places makes the result the same whichever order Unity chose.
        /// </remarks>
        private void HookTheStore()
        {
            RefreshRemoveAds();

            var store = Smiley.Ads.StorePurchases.Instance;

            if (store != null)
            {
                store.Changed -= RefreshRemoveAds;
                store.Changed += RefreshRemoveAds;
            }
        }

        /// <summary>Shows or hides the button, which has nothing to sell once it is bought.</summary>
        private void RefreshRemoveAds()
        {
            var store = Smiley.Ads.StorePurchases.Instance;
            bool offer = store != null && store.ShouldOffer;
            UiKit.SetVisible(_removeAds, offer);

            // ⓘ The store's own localised price once it knows one — the owner's word stays on the
            // button until then, so an unreachable store shows a plain label rather than a gap.
            if (offer && _removeAds is UnityEngine.UIElements.Button button && !string.IsNullOrEmpty(store.LocalisedPrice))
            {
                button.text = "Remove Ads  " + store.LocalisedPrice;
            }
        }

        /// <summary>Whether the Remove Ads button is on screen. For tests.</summary>
        public bool RemoveAdsOffered => _removeAds != null && UiKit.IsVisible(_removeAds);

        /// <summary>Whether the Restore Purchases button is on screen. For tests.</summary>
        public bool RestoreOffered => _restore != null && UiKit.IsVisible(_restore);

        /// <summary>Whether the menu built a Restore button at all, visible or not. For tests.</summary>
        public bool RestoreExists => _restore != null;

        private void Show(string screen)
        {
            UiKit.SetVisible(_menu, screen == "menu");
            UiKit.SetVisible(_settings, screen == "settings");
            UiKit.SetVisible(_confirm, screen == "confirm");
            UiKit.SetVisible(_consent, screen == "consent");
        }

        /// <summary>§22's one starting action.</summary>
        /// <remarks>
        /// ⭐ D-122 (owner): an interstitial may show here, before the climb loads — the one
        /// moment the owner chose, capped by <see cref="InterstitialRule"/>. The scene loads
        /// when the ad closes, or at once when there is none; the choice of Continue / New Game
        /// is made before either, so an ad can never change it.
        /// </remarks>
        public void StartPlaying()
        {
            MainMenuAction action = MainMenuRule.PrimaryAction(SaveFile.Exists());
            GameSession.Choose(MainMenuRule.EntryFor(action));

            // ⭐ D-122's interstitial, now owned by AdService (D-125). Shows first and loads the
            // scene on close, or loads it at once — its contract, either way.
            var ads = Smiley.Ads.AdService.Instance;
            if (ads != null)
            {
                ads.ShowInterstitialThen(LoadGame);
                return;
            }

            LoadGame();
        }

        private static void LoadGame() => SceneManager.LoadScene(Scenes.Game, LoadSceneMode.Single);

        /// <summary>
        /// §25's Reset Game.
        /// </summary>
        /// <remarks>
        /// ⛔ <b>It returns to the Main Menu, it does not start a game.</b> §25 says so in those
        /// words. ⓘ The change the player actually reads is the button: it said Continue, and now
        /// it says New Game.
        /// </remarks>
        public void ResetGame()
        {
            SaveFile.DeleteAll();
            GameSession.Reset();

            // ⚠ §25 keeps settings. They are in their own file, so deleting the save cannot
            // touch them — and a Play Mode test now asserts that, which it could not do until
            // §27 gave the settings something to hold.
            Bind();
        }

        /// <summary>§26: Music Volume, saved persistently.</summary>
        public void SetMusicVolume(float value)
        {
            if (_audio == null)
            {
                return;
            }

            _audio.Settings.MusicVolume = value;
            _audio.SaveSettings();
        }

        /// <summary>§26: SFX Volume, saved persistently.</summary>
        public void SetSfxVolume(float value)
        {
            if (_audio == null)
            {
                return;
            }

            _audio.Settings.SfxVolume = value;
            _audio.SaveSettings();
        }

        /// <summary>§22's Exit / Quit.</summary>
        public void Quit()
        {
            // ⓘ Does nothing in the editor, which is Unity's behaviour and not a defect.
            Application.Quit();
        }
    }
}
