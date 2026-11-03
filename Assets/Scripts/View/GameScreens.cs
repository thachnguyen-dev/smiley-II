using Smiley.Flow;
using Smiley.Gameplay;
using Smiley.Save;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Smiley.View
{
    /// <summary>
    /// Everything the Game scene draws on top of the world: §20's Story, the Completed screen,
    /// and §23's Gameplay Menu.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⛔ <b>THERE IS NO PAUSE HERE, IN ANY FORM (§23, §24, D-008).</b> §23 removes traditional
    /// pause and §24 removes Restart Level entirely — <i>"under any name or alternative UI
    /// presentation"</i>. Nothing in this file touches <see cref="Time.timeScale"/>, disables
    /// physics, or offers a restart, and nothing should be added that does. The world keeps
    /// running behind every screen below.
    /// </para>
    /// <para>
    /// ⚠ <b>What IS suspended is player input</b>, and only while a screen is up. Aiming a launch
    /// you cannot see is not gameplay, and §8's Ready rule is untouched by it — the drag simply
    /// cannot begin.
    /// </para>
    /// <para>
    /// ⭐ <b>EVERY SCREEN IS THE OWNER'S (D-057, D-074).</b> §20's stories, §30's tutorial, the
    /// Completed screen, and — since D-074 — §23's Gameplay Menu and its corner button all come
    /// from <c>Assets/UI/StoryScreens.uxml</c>, which the owner lays out in UI Builder. This
    /// class builds nothing: it finds each control by name and gives it behaviour.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(UIDocument))]
    public sealed class GameScreens : MonoBehaviour
    {
        [SerializeField] private CompletionFlow _flow;
        [SerializeField] private SaveService _save;
        [SerializeField] private SmileyController _controller;
        [SerializeField] private StorySlides _slides;

        private UIDocument _document;

        private VisualElement _story;
        private VisualElement _completed;
        private VisualElement _menu;
        private VisualElement _menuButton;
        private VisualElement _storyText;
        private VisualElement _tutorial;

        private bool _menuOpen;

        /// <summary>Whether §23's Gameplay Menu is open. For tests.</summary>
        public bool MenuIsOpen => _menuOpen;

        /// <summary>The screen currently covering the game, or null. For tests.</summary>
        public string VisibleScreen
        {
            get
            {
                if (_story != null && UiKit.IsVisible(_story)) return "story";
                if (_tutorial != null && UiKit.IsVisible(_tutorial)) return "tutorial";
                if (_completed != null && UiKit.IsVisible(_completed)) return "completed";
                if (_menuOpen) return "menu";
                return null;
            }
        }

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            Build();
        }

        private void OnDisable()
        {
            UiKit.SetGamePanel(null);
        }

        private void Build()
        {
            VisualElement root = _document.rootVisualElement;

            // ⚠ The root must not swallow touches meant for Smiley. A UI Toolkit panel picks up
            // every pointer inside it, and a full-screen root would make the whole game
            // undraggable — which is exactly what §6 needs the screen for.
            root.pickingMode = PickingMode.Ignore;

            // D-111: the drag surface is the whole screen, so the controller asks this panel
            // whether a touch landed on the corner button instead.
            UiKit.SetGamePanel(root.panel);

            // ⭐ The owner's layout lives inside the safe area (D-072), so what they see in UI
            // Builder is what the phone shows — minus the status bar, which is the system's.
            UiKit.WrapInSafeArea(root);

            // The owner's half: found, never built. ⛔ Nothing here clears the root, because the
            // root IS their document.
            _story = root.Q("story-screen");
            _tutorial = root.Q("tutorial-screen");
            _completed = root.Q("completed-screen");
            _storyText = root.Q<Label>(UiKit.Names.StoryText);

            if (_story == null || _completed == null)
            {
                Debug.LogError(
                    "[UI] The Story or Completed screen is missing from the layout. §19's " +
                    "completion flow will still run and the player will see nothing happen.");
            }

            UiKit.Bind(root, UiKit.Names.Acknowledge, Acknowledge);
            UiKit.Bind(root, UiKit.Names.TutorialClose, CloseTutorial);
            UiKit.Bind(root, UiKit.Names.StoryNext, Next);

            // ⭐ D-137: the lines land in the owner's story-text Label.
            _slides?.AttachTo(_storyText);

            // §23's menu and its corner button, over live gameplay — the owner's since D-074.
            // ⛔ Two buttons and nothing else: there is no Restart (§24), and a test reads every
            // button's text to make sure one has not been added.
            _menu = root.Q("gameplay-menu");
            _menuButton = UiKit.Bind(root, UiKit.Names.MenuButton, OpenMenu);
            UiKit.Bind(root, UiKit.Names.Resume, CloseMenu);
            UiKit.Bind(root, UiKit.Names.MainMenu, ToMainMenu);

            if (_menu != null)
            {
                UiKit.SetVisible(_menu, _menuOpen);
            }

            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (_flow == null)
            {
                return;
            }

            // ⭐ D-058: EITHER of §20's two stories puts the Story screen up — the opening one
            // before the climb, and the completion one after the checkpoint. The screen does not
            // care which; only the images differ.
            bool story = _flow.Phase == CompletionPhase.Story
                         || _flow.Phase == CompletionPhase.Opening;

            bool completed = _flow.Phase == CompletionPhase.Completed;

            if (_story != null)
            {
                UiKit.SetVisible(_story, story);
            }

            if (_tutorial != null)
            {
                // §30: the image sits between the opening video and the first launch.
                UiKit.SetVisible(_tutorial, _flow.TutorialIsShowing);
            }

            if (_completed != null)
            {
                UiKit.SetVisible(_completed, completed);
            }

            if (_menuButton != null)
            {
                // The corner button has nothing to open once the climb is over.
                UiKit.SetVisible(_menuButton, !_flow.HasStarted);
            }

            if (_flow.HasStarted && _menuOpen)
            {
                CloseMenu();
            }

            SetGameplayInput(!_flow.HasStarted && !_menuOpen);
            PaintBehindTheCoveringScreen();
        }

        /// <summary>
        /// The element whose background the player is looking at when a screen is up, or null
        /// during play. The story's picture element, not its screen: the screen is sky and the
        /// picture's letterbox is black, and the strip must match what sits beside it.
        /// </summary>
        public VisualElement CoveringElement
        {
            get
            {
                // ⭐ D-137: the story screen itself. It used to be the PICTURE, because a
                // letterboxed image carried its own colour and the screen behind it did not.
                // There is no picture now -- the screen is black edge to edge -- so the screen
                // is the cover.
                if (_story != null && UiKit.IsVisible(_story)) return _story;
                if (_tutorial != null && UiKit.IsVisible(_tutorial)) return _tutorial;
                if (_completed != null && UiKit.IsVisible(_completed)) return _completed;
                if (_menuOpen && _menu != null) return _menu;
                return null;
            }
        }

        /// <summary>
        /// Paints the document root in the covering screen's colour, and clears it during play.
        /// </summary>
        /// <remarks>
        /// ⭐ <b>D-123 (owner, 2026-09-17): "a portion of the Stage is still visible at the top
        /// behind/above the menu panels."</b> D-072 put the owner's layout inside a safe-area
        /// wrapper so their <c>top: 0</c> lands below the status bar — and so does every screen's
        /// background, leaving the strip above it showing the world. The root is the one element
        /// that spans the whole window, and during play it must stay clear (this is the Game
        /// scene; the Menu scene's root wears <c>.menu-root</c> instead, D-114). So it takes the
        /// covering screen's <i>resolved</i> colour — the owner's stylesheet stays the single
        /// source — and gives it back when nothing covers. Written only when the covering element
        /// changes (D-113): this runs every frame.
        /// </remarks>
        private VisualElement _covered;
        private VisualElement _pendingCover;

        /// <summary>The colour the root currently wears for a covering screen, or clear.</summary>
        public Color BackdropColour { get; private set; } = Color.clear;

        private void PaintBehindTheCoveringScreen()
        {
            VisualElement root = _document.rootVisualElement;
            VisualElement cover = CoveringElement;

            if (cover == _covered)
            {
                _pendingCover = null;
                return;
            }

            // ⚠ A screen's resolved style is not trustworthy on the frame it is first shown —
            // the opening story read as transparent that way on the phone. Wait one frame.
            if (cover != null && _pendingCover != cover)
            {
                _pendingCover = cover;
                return;
            }

            _pendingCover = null;

            // The previous cover gets its own background back.
            if (_covered != null)
            {
                _covered.style.backgroundColor = StyleKeyword.Null;
            }

            _covered = cover;

            if (cover == null)
            {
                root.style.backgroundColor = StyleKeyword.Null;
                BackdropColour = Color.clear;
                return;
            }

            BackdropColour = cover.resolvedStyle.backgroundColor;
            root.style.backgroundColor = BackdropColour;

            // ⚠ ONE tint, not two. A TRANSLUCENT cover would be applied twice inside the safe area
            // and once above it, so it goes clear and the root carries it alone. An OPAQUE cover
            // keeps its own: painting it twice changes nothing, and clearing it would expose
            // whatever sits between it and the root (the story's letterbox went sky-blue).
            if (BackdropColour.a < 0.999f)
            {
                cover.style.backgroundColor = Color.clear;
            }
        }

        /// <summary>
        /// Turns the player's control on and off.
        /// </summary>
        /// <remarks>
        /// ⛔ <b>The CONTROLLER, never the world.</b> Disabling the component stops §6's drag
        /// from starting; it does not stop Smiley's body, the platforms' countdowns, or anything
        /// else. That is D-008 exactly: the menu does not stop the world.
        /// </remarks>
        private void SetGameplayInput(bool enabled)
        {
            if (_controller != null && _controller.enabled != enabled)
            {
                _controller.enabled = enabled;
            }
        }

        /// <summary>Opens §23's menu.</summary>
        /// <remarks>
        /// ⭐ <b>D-124 (owner): a full-screen ad may show here</b>, capped with every other
        /// interstitial at one per three minutes. ⚠ <b>The world keeps running behind it</b> —
        /// §23 has no pause and D-008 forbids one, so a player who opens the menu mid-flight
        /// comes back to a Smiley who has landed somewhere. The owner was shown that and chose
        /// this moment. ⛔ The menu opens whether or not an ad is shown: nothing waits.
        /// </remarks>
        public void OpenMenu()
        {
            _menuOpen = true;
            Smiley.Ads.AdService.Instance?.ShowInterstitialThen(null);

            if (_menu != null)
            {
                UiKit.SetVisible(_menu, true);
            }

            Refresh();
        }

        /// <summary>§23's Resume: "closes the menu and continues gameplay".</summary>
        public void CloseMenu()
        {
            _menuOpen = false;

            if (_menu != null)
            {
                UiKit.SetVisible(_menu, false);
            }

            Refresh();
        }

        /// <summary>The Completed screen's single button.</summary>
        public void Acknowledge() => _flow?.Acknowledge();

        /// <summary>The story's Next button (D-068). One image forward; the last one ends it.</summary>
        public void Next() => _flow?.Next();

        /// <summary>§30's tutorial X. ⭐ The moment play begins (D-064).</summary>
        public void CloseTutorial() => _flow?.CloseTutorial();

        /// <summary>
        /// §23's Main Menu: <i>"Before returning to the Main Menu, perform Auto Save."</i>
        /// </summary>
        /// <remarks>
        /// ⛔ <b>The save happens FIRST and its result is not consulted.</b> §21 names returning
        /// to the Main Menu as one of its two Auto Save moments, and a failed write is not a
        /// reason to trap the player in the game — §21 already treats an unusable save as an
        /// ordinary situation rather than an error the player sees.
        /// </remarks>
        public void ToMainMenu()
        {
            if (_save != null)
            {
                _save.SaveNow();
            }

            SceneManager.LoadScene(Scenes.Menu, LoadSceneMode.Single);
        }
    }
}
