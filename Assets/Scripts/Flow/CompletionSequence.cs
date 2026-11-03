using UnityEngine;

namespace Smiley.Flow
{
    /// <summary>Where the player is in §19's completion flow.</summary>
    public enum CompletionPhase
    {
        /// <summary>
        /// §20's OPENING story is showing, before the climb begins.
        /// </summary>
        /// <remarks>
        /// ⭐ Owner, 2026-09-11 (D-058, D-068): the opening plays when the game is opened for
        /// the first time or after §25's Reset Game — as <b>seven images</b> stepped through with
        /// a Next button. ⚠ It comes BEFORE <see cref="Climbing"/>, which is why it is first in
        /// this list — the phases read in the order the player meets them.
        /// </remarks>
        Opening,

        /// <summary>
        /// §30's Tutorial image is up, waiting to be closed.
        /// </summary>
        /// <remarks>
        /// ⭐ Owner, 2026-09-11 (D-064), giving §30 the place in the flow it never had:
        /// <c>New Game → Opening → Tutorial image → X → play</c>.
        /// </remarks>
        Tutorial,

        /// <summary>Normal play. The Top Checkpoint has not been reached.</summary>
        Climbing,

        /// <summary>§20's completion story — <b>two images</b> (D-068). ⛔ It cannot be skipped.</summary>
        Story,

        /// <summary>The Completed screen is up, waiting to be acknowledged.</summary>
        Completed,

        /// <summary>Done. The Main Menu is next.</summary>
        Finished,
    }

    /// <summary>
    /// §19 and §20's flow: <c>Top Checkpoint → Story → Completed → Main Menu</c>, plus the
    /// opening story and §30's tutorial before the climb.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>THE STORIES ARE IMAGES STEPPED WITH A NEXT BUTTON, NOT VIDEOS ON A CLOCK (D-068).</b>
    /// Owner, 2026-09-11: the opening is <b>seven</b> images and the ending <b>two</b>, each with
    /// a Next button in the corner. Nothing in this struct reads time any more — a story ends when
    /// Next has been pressed on its <b>last</b> image, and not before.
    /// </para>
    /// <para>
    /// ⛔ <b>"The player cannot skip the Story" is still enforced here, structurally.</b> The
    /// only way out of a story is <see cref="NextSlide"/>, and it moves exactly one image; there
    /// is no input that reaches the end from the middle. ⚠ A player may tap Next quickly, and
    /// that is <b>not</b> a skip — every image is shown — the same reading as §30's X (Q-051).
    /// </para>
    /// <para>
    /// ⚠ <b>And the checkpoint fires once.</b> §19 has exactly one Top Checkpoint and one flow;
    /// a second touch while the Story is up — easy to produce, since Smiley keeps obeying physics
    /// behind the screen (§5, §8) — must not restart anything.
    /// </para>
    /// <para>
    /// ⓘ No Unity in here beyond <see cref="Mathf"/>, so every clause above is testable directly
    /// rather than inferred from a scene. Same split as <see cref="Gameplay.PlatformCountdown"/>.
    /// </para>
    /// </remarks>
    public struct CompletionSequence
    {
        /// <summary>What the flow is doing right now.</summary>
        public CompletionPhase Phase;

        /// <summary>Which image of the current story is showing, from 0.</summary>
        public int Slide;

        /// <summary>How many images the current story has.</summary>
        /// <remarks>
        /// ⚠ Never below one, whatever was handed in. A story with no images assigned yet
        /// (Q-050) still shows one empty slide with a working Next, so the flow stays testable
        /// before the owner's content exists.
        /// </remarks>
        public int SlideCount;

        /// <summary>
        /// Whether §20's opening story has already run this session.
        /// </summary>
        /// <remarks>
        /// ⛔ <b>A flag, because nothing else can carry this.</b> The opening ends by handing over
        /// to the tutorial and then to <see cref="CompletionPhase.Climbing"/>, which is the state
        /// it starts from — so "have we done this?" is not answerable from the phase. Inferring
        /// it from a counter made the opening replayable once (D-062).
        /// </remarks>
        public bool OpeningPlayed;

        /// <summary>A flow that has not started.</summary>
        public static CompletionSequence Fresh => new CompletionSequence
        {
            Phase = CompletionPhase.Climbing,
            Slide = 0,
            SlideCount = 1,
        };

        /// <summary>Whether either of §20's stories is on screen this instant.</summary>
        public bool StoryIsPlaying =>
            Phase == CompletionPhase.Story || Phase == CompletionPhase.Opening;

        /// <summary>
        /// Whether anything is covering the game, so the player must not be controlling Smiley.
        /// </summary>
        /// <remarks>
        /// ⚠ <b>Opening and Tutorial count.</b> Aiming a launch behind a screen is controlling a
        /// game you cannot see, whichever screen it is.
        /// </remarks>
        public bool HasStarted => Phase != CompletionPhase.Climbing;

        /// <summary>Whether §30's Tutorial image is up. For presentation.</summary>
        public bool TutorialIsShowing => Phase == CompletionPhase.Tutorial;

        /// <summary>Whether the image showing is the story's last one. For presentation.</summary>
        public bool OnLastSlide => StoryIsPlaying && Slide >= SlideCount - 1;

        /// <summary>Starts the opening story (D-058). Returns false if the climb has begun.</summary>
        /// <param name="slideCount">How many images the opening has — seven, per the owner.</param>
        public bool BeginOpening(int slideCount)
        {
            if (Phase != CompletionPhase.Climbing || OpeningPlayed)
            {
                return false;
            }

            OpeningPlayed = true;
            Phase = CompletionPhase.Opening;
            Slide = 0;
            SlideCount = Mathf.Max(1, slideCount);
            return true;
        }

        /// <summary>
        /// Goes straight to §30's Tutorial image, with no opening story first. Returns false if
        /// the climb has begun.
        /// </summary>
        /// <remarks>
        /// ⭐ <b>D-139 (owner, 2026-10-19): the web build only.</b> CrazyGames allows at most one
        /// click before gameplay, and the opening is thirteen Next presses with no Skip (D-137).
        /// The owner dropped the opening on the web and kept the tutorial image, whose X is the
        /// one click. ⛔ The phones are untouched — they still call <see cref="BeginOpening"/>.
        /// ⚠ The opening counts as played, so nothing can start it later in the same session.
        /// </remarks>
        public bool BeginTutorial()
        {
            if (Phase != CompletionPhase.Climbing || OpeningPlayed)
            {
                return false;
            }

            OpeningPlayed = true;
            Phase = CompletionPhase.Tutorial;
            Slide = 0;
            SlideCount = 1;
            return true;
        }

        /// <summary>
        /// Smiley touched the Top Checkpoint.
        /// </summary>
        /// <param name="slideCount">How many images the ending has — two, per the owner.</param>
        /// <returns>True when this is the touch that started the flow.</returns>
        public bool ReachCheckpoint(int slideCount)
        {
            if (Phase != CompletionPhase.Climbing)
            {
                // ⛔ Already running — including during the opening story, where Smiley still
                // obeys physics behind the screen (D-008) and on a small map could touch the
                // checkpoint before the player has seen the game. §19 describes one flow, not
                // one per touch.
                return false;
            }

            Phase = CompletionPhase.Story;
            Slide = 0;
            SlideCount = Mathf.Max(1, slideCount);
            return true;
        }

        /// <summary>
        /// The player pressed Next.
        /// </summary>
        /// <remarks>
        /// <para>
        /// ⭐ <b>Owner, 2026-09-11 (D-068):</b> a Next button in the corner advances to the next
        /// image. On the last image it ends the story instead.
        /// </para>
        /// <para>
        /// ⚠ <b>The two stories end in different places, and that is the whole difference between
        /// them:</b> the opening hands the player §30's tutorial and then the game; the completion
        /// hands them the Completed screen and then the menu.
        /// </para>
        /// <para>
        /// ⛔ <b>Inert everywhere else.</b> Next cannot close the tutorial, acknowledge the
        /// Completed screen, or do anything during play — each phase has exactly one input.
        /// </para>
        /// </remarks>
        /// <returns>True when the press ended the story.</returns>
        public bool NextSlide()
        {
            if (!StoryIsPlaying)
            {
                return false;
            }

            if (Slide < SlideCount - 1)
            {
                Slide++;
                return false;
            }

            Phase = Phase == CompletionPhase.Opening
                ? CompletionPhase.Tutorial
                : CompletionPhase.Completed;

            return true;
        }

        /// <summary>
        /// The player acknowledged the Completed screen.
        /// </summary>
        /// <remarks>
        /// ⛔ <b>Deliberately inert during a story.</b> This is the whole of §20's "cannot skip":
        /// the input exists, the story ignores it. ⓘ Written as a rule rather than as a hidden
        /// button, because a hidden button is one careless edit away from being shown.
        /// </remarks>
        public void Acknowledge()
        {
            if (Phase == CompletionPhase.Completed)
            {
                Phase = CompletionPhase.Finished;
            }
        }

        /// <summary>
        /// The player closed §30's Tutorial image.
        /// </summary>
        /// <remarks>
        /// ⭐ Owner, 2026-09-11 (D-064): the image is dismissed with an X button — the only input
        /// the tutorial takes, and the moment play begins. ⚠ §30 says "no skip button"; the
        /// tension is recorded as Q-051 rather than argued away. ⛔ Inert everywhere else, which is
        /// what keeps §20's separate rule true.
        /// </remarks>
        public void CloseTutorial()
        {
            if (Phase == CompletionPhase.Tutorial)
            {
                Phase = CompletionPhase.Climbing;
            }
        }

        /// <summary>How far through the story, 0 to 1. For presentation only.</summary>
        public float StoryProgress =>
            SlideCount <= 1 ? 1f : Mathf.Clamp01((float)Slide / (SlideCount - 1));
    }
}
