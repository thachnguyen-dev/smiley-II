using Smiley.Gameplay;
using Smiley.Save;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Smiley.Flow
{
    /// <summary>
    /// Runs §19's flow: <c>Top Checkpoint → Story → Completed → Main Menu</c>, and the opening
    /// story and §30's tutorial before the climb.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rules live in <see cref="CompletionSequence"/>, which has no Unity in it and is
    /// tested clause by clause. This component does what the rule cannot: listen to the
    /// checkpoint, show the images, write D-012's completion save, and load the Main Menu.
    /// </para>
    /// <para>
    /// ⭐ <b>THERE IS NO CLOCK IN HERE ANY MORE (D-068).</b> The stories were videos on a timer for
    /// one day; they are images stepped with a Next button now, and a story ends when Next is
    /// pressed on its last image. The <c>FixedUpdate</c> that advanced them is gone with the timer.
    /// </para>
    /// <para>
    /// ⛔ <b>THE WORLD KEEPS RUNNING BEHIND THE SCREENS, and that is D-008 rather than an
    /// oversight.</b> There is no pause path in this game's physics at all, so Smiley carries on
    /// obeying §5 and §9 while a story is up — he may well roll off the top platform and fall.
    /// That is harmless: §5 says falling can never cause anything, the flow has already started,
    /// and <see cref="CompletionSequence.ReachCheckpoint"/> refuses to restart it if he touches
    /// the checkpoint again on the way past.
    /// </para>
    /// <para>
    /// ⚠ <b>Gameplay input ends when the flow starts</b>, though. Letting the player launch
    /// Smiley from behind a story screen would be controlling a game they cannot see.
    /// </para>
    /// </remarks>
    public sealed class CompletionFlow : MonoBehaviour
    {
        [SerializeField] private TopCheckpoint _checkpoint;
        [SerializeField] private SaveService _save;
        [SerializeField] private SmileyController _controller;
        [SerializeField] private StorySlides _slides;

        private CompletionSequence _sequence = CompletionSequence.Fresh;
        private bool _completionWritten;

        /// <summary>What the flow is doing. For tests and for the screens.</summary>
        public CompletionPhase Phase => _sequence.Phase;

        /// <summary>Which image of the current story is showing, from 0. For tests.</summary>
        public int Slide => _sequence.Slide;

        /// <summary>How many images the current story has. For tests.</summary>
        public int SlideCount => _sequence.SlideCount;

        /// <summary>Whether the image showing is the last one. For presentation.</summary>
        public bool OnLastSlide => _sequence.OnLastSlide;

        /// <summary>Whether the completion flow has begun (§19).</summary>
        public bool HasStarted => _sequence.HasStarted;

        /// <summary>Whether §30's Tutorial image is up. For the screens and for tests.</summary>
        public bool TutorialIsShowing => _sequence.TutorialIsShowing;

        private void Start()
        {
            // ⭐ D-058: the opening story plays when the game is opened for the first time or
            // after §25's Reset Game.
            //
            // ⓘ BOTH of those arrive here as a New Game — a first run has no save, and a reset
            // deletes the one there was, so §22 offers New Game in each case. Reading the entry
            // mode rather than the save file means the two triggers are one condition instead of
            // two that could drift apart.
            if (GameSession.Requested != GameEntryMode.NewGame)
            {
                return;
            }

            // ⭐ D-139: on the web a New Game goes straight to the tutorial image — the opening
            // story is dropped there to meet CrazyGames' one-click rule. ⛔ Phones unchanged.
            if (Smiley.Web.WebPlatform.Active)
            {
                _sequence.BeginTutorial();
                return;
            }

            BeginOpening();
        }

        private void OnEnable()
        {
            if (_checkpoint != null)
            {
                _checkpoint.Touched += OnCheckpointTouched;
            }
        }

        private void OnDisable()
        {
            if (_checkpoint != null)
            {
                _checkpoint.Touched -= OnCheckpointTouched;
            }
        }

        private int CountFor(StoryMoment moment) =>
            _slides != null ? _slides.CountFor(moment) : 0;

        private void BeginOpening()
        {
            if (!_sequence.BeginOpening(CountFor(StoryMoment.Opening)))
            {
                return;
            }

            ShowStory(StoryMoment.Opening);
        }

        private void OnCheckpointTouched()
        {
            if (!_sequence.ReachCheckpoint(CountFor(StoryMoment.Completion)))
            {
                return;
            }

            ShowStory(StoryMoment.Completion);

            // §19 ends the climb. §27: "When reaching the Top Checkpoint, transition to Story.
            // No special SFX / VFX feedback is required."
            if (_controller != null)
            {
                _controller.enabled = false;
            }
        }

        private void ShowStory(StoryMoment moment)
        {
            if (_slides == null)
            {
                return;
            }

            _slides.Load(moment);
            _slides.Show(_sequence.Slide);
        }

        /// <summary>
        /// The Next button (D-068). Advances one image, or ends the story on its last one.
        /// </summary>
        /// <remarks>
        /// ⛔ <b>Completion is written the moment the ending's last image is dismissed</b>, not
        /// when the menu is reached. §20 puts Completed between the story and the Main Menu, and
        /// the player sits on that screen for as long as they like — with the app killable
        /// throughout. A completion recorded only on the way out would be lost by anyone who
        /// closed the game while looking at their own victory screen.
        /// </remarks>
        public void Next()
        {
            CompletionPhase before = _sequence.Phase;

            if (!_sequence.NextSlide())
            {
                _slides?.Show(_sequence.Slide);
                return;
            }

            _slides?.Hide();

            if (before == CompletionPhase.Story && _sequence.Phase == CompletionPhase.Completed)
            {
                WriteCompletion();
            }
        }

        /// <summary>The Completed screen was acknowledged. ⛔ Inert during a story (§20).</summary>
        public void Acknowledge()
        {
            _sequence.Acknowledge();

            if (_sequence.Phase == CompletionPhase.Finished)
            {
                ToMainMenu();
            }
        }

        /// <summary>§30's Tutorial was closed with the X. ⛔ Inert everywhere else.</summary>
        public void CloseTutorial() => _sequence.CloseTutorial();

        /// <summary>Starts the completion flow without a checkpoint. For tests.</summary>
        public void ForceStart() => OnCheckpointTouched();

        /// <summary>Starts the opening story without a New Game. For tests.</summary>
        public bool ForceOpening()
        {
            if (!_sequence.BeginOpening(CountFor(StoryMoment.Opening)))
            {
                return false;
            }

            ShowStory(StoryMoment.Opening);
            return true;
        }

        private void WriteCompletion()
        {
            if (_completionWritten || _save == null)
            {
                return;
            }

            _completionWritten = true;
            _save.CompleteAndResetToStart();
        }

        private void ToMainMenu()
        {
            // Belt and braces: if the story somehow never wrote it, the way out still does.
            WriteCompletion();

            SceneManager.LoadScene(Scenes.Menu, LoadSceneMode.Single);
        }
    }
}
