using Smiley.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace Smiley.View
{
    /// <summary>
    /// Shows D-131's guided-opening prompts near Smiley, and gets out of the way.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⛔ <b>It must never eat a drag.</b> §6 puts the player's thumb on Smiley and this floats
    /// right above him, so the label is <c>pickingMode = Ignore</c> and the USS marks it
    /// absolute. A tutorial that blocks the control it is teaching is worse than no tutorial.
    /// </para>
    /// <para>
    /// ⚠ <b>Counted here, not saved.</b> The launches are a count for this session only: §21's
    /// save restores a climb in progress, and a player resuming mid-climb has already been
    /// taught. ⓘ <see cref="TutorialPromptRule"/> keys off the first-playthrough flag for the
    /// case §30 actually cares about — a replay after completion.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(UIDocument))]
    public sealed class TutorialPrompt : MonoBehaviour
    {
        [Tooltip("How far above Smiley the prompt floats, in world units.")]
        [SerializeField] private float _heightAboveSmiley = 1.4f;

        [SerializeField] private SmileyController _smiley;
        [SerializeField] private GameScreens _screens;
        [SerializeField] private Camera _camera;

        private Label _label;
        private int _launches;
        private bool _tutorialWasUp;
        private bool _armed;

        /// <summary>Whether the guided opening has begun. For tests.</summary>
        public bool Armed => _armed;

        /// <summary>What is on screen right now. For tests.</summary>
        public TutorialPromptRule.Prompt Showing { get; private set; }

        /// <summary>Launches counted this session. For tests.</summary>
        public int Launches => _launches;

        private void Awake()
        {
            var document = GetComponent<UIDocument>();
            _label = document != null ? document.rootVisualElement?.Q<Label>(UiKit.Names.TutorialPrompt) : null;

            if (_label != null)
            {
                // ⛔ The whole reason this is safe to float over gameplay.
                _label.pickingMode = PickingMode.Ignore;
            }

            if (_camera == null)
            {
                _camera = Camera.main;
            }
        }

        private void OnEnable()
        {
            if (_smiley != null)
            {
                _smiley.Launched += OnLaunched;
            }
        }

        private void OnDisable()
        {
            if (_smiley != null)
            {
                _smiley.Launched -= OnLaunched;
            }
        }

        private void OnLaunched(float _) => _launches++;

        /// <summary>Resets the count, for a genuinely new climb. For tests and New Game.</summary>
        public void Restart()
        {
            _launches = 0;
            _armed = false;
            _tutorialWasUp = false;
        }

        private void LateUpdate()
        {
            if (_label == null)
            {
                return;
            }

            // ⭐ The guided opening begins the moment §30s instruction image is closed, and only
            // then. ⓘ That image appears on a first playthrough and nowhere else, so this needs no
            // save flag of its own: a replay never shows the image, so it never arms the prompts,
            // which is exactly what §30 asks for.
            bool tutorialUp = _screens != null && _screens.VisibleScreen == "tutorial";

            if (_tutorialWasUp && !tutorialUp)
            {
                _armed = true;
            }

            _tutorialWasUp = tutorialUp;

            // ⛔ Any full screen hides the prompt -- the story, the image, the gear menu. A label
            // showing through the gap above a covering screen is the bug D-123 was written for.
            bool covered = _screens == null || _screens.CoveringElement != null;
            bool atRest = _smiley != null && _smiley.IsReady;

            Showing = TutorialPromptRule.Current(_armed, _launches, atRest, covered);

            if (Showing == TutorialPromptRule.Prompt.None)
            {
                UiKit.SetVisible(_label, false);
                return;
            }

            _label.text = TutorialPromptRule.TextFor(Showing);
            UiKit.SetVisible(_label, true);
            PlaceAboveSmiley();
        }

        /// <summary>
        /// Puts the label over Smiley's head, clamped so it never leaves the screen.
        /// </summary>
        /// <remarks>
        /// ⚠ Panel space, not pixels. The UI is laid out at a reference resolution and scaled per
        /// device (§33 keeps the world the same everywhere), so a screen-pixel position would put
        /// the prompt somewhere different on every phone.
        /// </remarks>
        private void PlaceAboveSmiley()
        {
            if (_smiley == null || _camera == null || _label.panel == null)
            {
                return;
            }

            Vector3 head = _smiley.transform.position + Vector3.up * _heightAboveSmiley;
            Vector2 screen = _camera.WorldToScreenPoint(head);

            Vector2 panelPoint = RuntimePanelUtils.ScreenToPanel(
                _label.panel, new Vector2(screen.x, Screen.height - screen.y));

            float height = _label.resolvedStyle.height;
            float panelHeight = _label.panel.visualTree.layout.height;

            if (float.IsNaN(height) || float.IsNaN(panelHeight))
            {
                return;
            }

            // ⓘ Left/right are pinned by the USS, so only the vertical is placed here — the text
            // stays centred and a long line wraps rather than running off the side.
            _label.style.top = Mathf.Clamp(panelPoint.y - height, 0f, Mathf.Max(0f, panelHeight - height));
        }
    }
}
