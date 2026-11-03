using NUnit.Framework;
using Smiley.Flow;

namespace Smiley.Tests
{
    /// <summary>§19's flow and §20's rules, as arithmetic.</summary>
    /// <remarks>
    /// ⭐ Rewritten 2026-09-11 for D-068: the stories are IMAGES stepped with a Next button, not
    /// videos on a clock. Nothing in these fixtures advances time, because nothing in the rule
    /// reads it.
    /// </remarks>
    public sealed class CompletionSequenceTests
    {
        private const int OpeningImages = 7;
        private const int EndingImages = 2;

        /// <summary>Presses Next until the story ends, counting the presses.</summary>
        private static int NextUntilTheStoryEnds(ref CompletionSequence flow, int guard = 100)
        {
            int presses = 0;

            while (flow.StoryIsPlaying && presses < guard)
            {
                flow.NextSlide();
                presses++;
            }

            return presses;
        }

        [Test]
        public void AFreshFlowIsJustPlaying()
        {
            CompletionSequence flow = CompletionSequence.Fresh;

            Assert.That(flow.Phase, Is.EqualTo(CompletionPhase.Climbing));
            Assert.That(flow.HasStarted, Is.False);
            Assert.That(flow.StoryIsPlaying, Is.False);
        }

        [Test]
        public void TouchingTheCheckpointStartsTheStoryOnItsFirstImage()
        {
            // §19: "Touching the Top Checkpoint starts the completion flow:
            // Top Checkpoint → Story → Completed → Main Menu."
            CompletionSequence flow = CompletionSequence.Fresh;

            Assert.That(flow.ReachCheckpoint(EndingImages), Is.True);
            Assert.That(flow.Phase, Is.EqualTo(CompletionPhase.Story));
            Assert.That(flow.Slide, Is.EqualTo(0), "the story did not begin on its first image");
            Assert.That(flow.SlideCount, Is.EqualTo(EndingImages));
            Assert.That(flow.HasStarted, Is.True);
        }

        [Test]
        public void TouchingItAgainDoesNotRestartAnything()
        {
            // ⚠ Easy to produce, and not hypothetical: the world keeps running behind the story
            // (D-008), so Smiley can roll back across the checkpoint while it is up. A restart
            // would put the player back on the first image of a story they cannot skip.
            CompletionSequence flow = CompletionSequence.Fresh;
            flow.ReachCheckpoint(EndingImages);
            flow.NextSlide();

            Assert.That(flow.ReachCheckpoint(EndingImages), Is.False,
                "a second touch claimed to start the flow");
            Assert.That(flow.Slide, Is.EqualTo(1), "the second touch rewound the story");
        }

        [Test]
        public void TheEndingTakesExactlyAsManyPressesAsItHasImages()
        {
            // ⭐ D-068: two images, a Next on each. The second Next ends it.
            CompletionSequence flow = CompletionSequence.Fresh;
            flow.ReachCheckpoint(EndingImages);

            int presses = NextUntilTheStoryEnds(ref flow);

            Assert.That(presses, Is.EqualTo(EndingImages),
                $"the ending took {presses} presses for {EndingImages} images");
            Assert.That(flow.Phase, Is.EqualTo(CompletionPhase.Completed));
        }

        [Test]
        public void TheOpeningTakesExactlyAsManyPressesAsItHasImages()
        {
            // ⭐ D-068: seven images, a Next on each. The seventh Next hands over to §30.
            CompletionSequence flow = CompletionSequence.Fresh;
            flow.BeginOpening(OpeningImages);

            int presses = NextUntilTheStoryEnds(ref flow);

            Assert.That(presses, Is.EqualTo(OpeningImages),
                $"the opening took {presses} presses for {OpeningImages} images");
            Assert.That(flow.Phase, Is.EqualTo(CompletionPhase.Tutorial));
        }

        [Test]
        public void EachPressMovesExactlyOneImage()
        {
            // ⛔ §20 IN ONE SENTENCE: "The player cannot skip the Story." With images the way that
            // stays true is that Next moves ONE image and there is no input that reaches the end
            // from the middle — so every image is shown, however quickly the player taps.
            CompletionSequence flow = CompletionSequence.Fresh;
            flow.BeginOpening(OpeningImages);

            for (int expected = 0; expected < OpeningImages; expected++)
            {
                Assert.That(flow.Slide, Is.EqualTo(expected),
                    $"after {expected} presses the story was on image {flow.Slide}");
                Assert.That(flow.StoryIsPlaying, Is.True, $"the story ended after {expected} presses");

                flow.NextSlide();
            }

            Assert.That(flow.StoryIsPlaying, Is.False);
        }

        [Test]
        public void NothingButNextCanEndAStory()
        {
            // ⛔ The other inputs exist — the Completed screen's button and the tutorial's X —
            // and neither may serve as a way out of a story.
            CompletionSequence flow = CompletionSequence.Fresh;
            flow.ReachCheckpoint(EndingImages);

            for (int tap = 0; tap < 50; tap++)
            {
                flow.Acknowledge();
                flow.CloseTutorial();
            }

            Assert.That(flow.Phase, Is.EqualTo(CompletionPhase.Story),
                "something other than Next ended the story, which §20 forbids");
            Assert.That(flow.Slide, Is.EqualTo(0), "something other than Next moved the story on");
        }

        [Test]
        public void AStoryWithNoImagesYetStillShowsOneSlideWithAWorkingNext()
        {
            // ⏸ Q-050: the images are the owner's and may not exist yet. The game must still be
            // playable and testable — one blank slide, one Next.
            CompletionSequence flow = CompletionSequence.Fresh;
            flow.ReachCheckpoint(0);

            Assert.That(flow.SlideCount, Is.EqualTo(1));
            Assert.That(flow.OnLastSlide, Is.True);

            flow.NextSlide();

            Assert.That(flow.Phase, Is.EqualTo(CompletionPhase.Completed));
        }

        [Test]
        public void OnLastSlideIsTrueOnlyOnTheLastOne()
        {
            CompletionSequence flow = CompletionSequence.Fresh;
            flow.BeginOpening(3);

            Assert.That(flow.OnLastSlide, Is.False);
            flow.NextSlide();
            Assert.That(flow.OnLastSlide, Is.False);
            flow.NextSlide();
            Assert.That(flow.OnLastSlide, Is.True);
        }

        [Test]
        public void TheCompletedScreenWaitsToBeAcknowledged()
        {
            CompletionSequence flow = CompletionSequence.Fresh;
            flow.ReachCheckpoint(EndingImages);
            NextUntilTheStoryEnds(ref flow);

            // Next must not carry the player past their own completion screen.
            for (int tap = 0; tap < 10; tap++)
            {
                flow.NextSlide();
            }

            Assert.That(flow.Phase, Is.EqualTo(CompletionPhase.Completed));

            flow.Acknowledge();
            Assert.That(flow.Phase, Is.EqualTo(CompletionPhase.Finished));
        }

        [Test]
        public void NothingAdvancesOnceItIsFinished()
        {
            CompletionSequence flow = CompletionSequence.Fresh;
            flow.ReachCheckpoint(EndingImages);
            NextUntilTheStoryEnds(ref flow);
            flow.Acknowledge();

            flow.NextSlide();
            flow.Acknowledge();
            flow.CloseTutorial();

            Assert.That(flow.Phase, Is.EqualTo(CompletionPhase.Finished));
        }

        [Test]
        public void TheIdleInputsDoNothingDuringPlay()
        {
            CompletionSequence flow = CompletionSequence.Fresh;

            flow.Acknowledge();
            flow.NextSlide();
            flow.CloseTutorial();

            Assert.That(flow.Phase, Is.EqualTo(CompletionPhase.Climbing));
        }

        [Test]
        public void TheWholeOpeningFlowRunsInTheOrderTheOwnerSpecified()
        {
            // ⭐ Owner, 2026-09-11 (D-064, amended by D-068):
            //
            //   "First time opening the game or after Reset Game → press New Game →
            //    Opening [seven images, Next on each] → enter the game → Tutorial image →
            //    press the X button to close the image → start playing the game."
            //
            // ⛔ Asserted as the ORDER, not as separate facts, because the order is what was
            // specified and each step is only correct in its place.
            CompletionSequence flow = CompletionSequence.Fresh;

            Assert.That(flow.BeginOpening(OpeningImages), Is.True);
            Assert.That(flow.Phase, Is.EqualTo(CompletionPhase.Opening), "the opening did not start");

            NextUntilTheStoryEnds(ref flow);
            Assert.That(flow.Phase, Is.EqualTo(CompletionPhase.Tutorial),
                "the opening handed over to something other than §30's tutorial");

            // ⚠ Neither Next nor time may carry the player past the image — it waits for the X.
            flow.NextSlide();
            Assert.That(flow.Phase, Is.EqualTo(CompletionPhase.Tutorial),
                "Next closed the tutorial, and §30's image waits for its X");

            flow.CloseTutorial();
            Assert.That(flow.Phase, Is.EqualTo(CompletionPhase.Climbing), "play did not begin");
            Assert.That(flow.HasStarted, Is.False, "the player still cannot control Smiley");
        }

        [Test]
        public void ThePlayerCannotDragWhileTheTutorialIsUp()
        {
            CompletionSequence flow = CompletionSequence.Fresh;
            flow.BeginOpening(OpeningImages);
            NextUntilTheStoryEnds(ref flow);

            Assert.That(flow.Phase, Is.EqualTo(CompletionPhase.Tutorial));
            Assert.That(flow.HasStarted, Is.True, "the player can drag Smiley behind the tutorial");
            Assert.That(flow.TutorialIsShowing, Is.True);
        }

        [Test]
        public void TheTutorialXDoesNothingDuringOrdinaryPlay()
        {
            CompletionSequence flow = CompletionSequence.Fresh;

            flow.CloseTutorial();

            Assert.That(flow.Phase, Is.EqualTo(CompletionPhase.Climbing));
        }

        [Test]
        public void TheCompletionStoryDoesNotShowTheTutorialAgain()
        {
            // §30: the tutorial "does not appear when replaying after completion". ⚠ Here that
            // is narrower and sharper: finishing the game must not put the image up between the
            // story and the Completed screen, which the two stories sharing a phase machine makes
            // easy to get wrong.
            CompletionSequence flow = CompletionSequence.Fresh;
            flow.ReachCheckpoint(EndingImages);
            NextUntilTheStoryEnds(ref flow);

            Assert.That(flow.Phase, Is.EqualTo(CompletionPhase.Completed),
                "the completion story led to the tutorial");
        }

        [Test]
        public void TheOpeningPlaysOnceAndNotAgain()
        {
            // ⛔ THE BUG THIS WAS WRITTEN FOR (D-062), and it was mine. The opening ends by
            // handing over to a state the flow starts from, so "have we done this?" cannot be
            // read from the phase, and inferring it from a counter made it replayable.
            CompletionSequence flow = CompletionSequence.Fresh;

            flow.BeginOpening(OpeningImages);
            NextUntilTheStoryEnds(ref flow);
            flow.CloseTutorial();

            Assert.That(flow.Phase, Is.EqualTo(CompletionPhase.Climbing));
            Assert.That(flow.BeginOpening(OpeningImages), Is.False,
                "the opening started a second time");
            Assert.That(flow.Phase, Is.EqualTo(CompletionPhase.Climbing));
        }

        [Test]
        public void ReachingTheCheckpointDuringTheOpeningIsIgnored()
        {
            // ⚠ Not hypothetical: the world runs behind the story (D-008), and on a map whose
            // checkpoint is reachable from the Start Area, Smiley could touch it before the
            // player has seen the game at all.
            CompletionSequence flow = CompletionSequence.Fresh;
            flow.BeginOpening(OpeningImages);

            Assert.That(flow.ReachCheckpoint(EndingImages), Is.False);
            Assert.That(flow.Phase, Is.EqualTo(CompletionPhase.Opening));
        }

        [Test]
        public void ProgressRunsFromZeroToOneAcrossTheImages()
        {
            CompletionSequence flow = CompletionSequence.Fresh;
            flow.BeginOpening(5);

            Assert.That(flow.StoryProgress, Is.EqualTo(0f).Within(1e-4f));

            flow.NextSlide();
            flow.NextSlide();
            Assert.That(flow.StoryProgress, Is.EqualTo(0.5f).Within(1e-4f));

            flow.NextSlide();
            flow.NextSlide();
            Assert.That(flow.StoryProgress, Is.EqualTo(1f).Within(1e-4f));
        }
    }

    /// <summary>§22's Main Menu, as a rule.</summary>
    public sealed class MainMenuRuleTests
    {
        [Test]
        public void NoSaveOffersANewGame()
        {
            Assert.That(MainMenuRule.PrimaryAction(false), Is.EqualTo(MainMenuAction.NewGame));
            Assert.That(MainMenuRule.PrimaryLabel(false), Is.EqualTo("New Game"));
        }

        [Test]
        public void ASaveOffersContinue()
        {
            Assert.That(MainMenuRule.PrimaryAction(true), Is.EqualTo(MainMenuAction.Continue));
            Assert.That(MainMenuRule.PrimaryLabel(true), Is.EqualTo("Continue"));
        }

        [Test]
        public void TheTwoActionsEnterTheGameDifferently()
        {
            // ⛔ The whole point of the distinction. Continue resumes §21's save; New Game must
            // not, or "New Game" would drop the player back into the climb they abandoned.
            Assert.That(MainMenuRule.EntryFor(MainMenuAction.Continue),
                Is.EqualTo(GameEntryMode.Continue));
            Assert.That(MainMenuRule.EntryFor(MainMenuAction.NewGame),
                Is.EqualTo(GameEntryMode.NewGame));
        }

        [Test]
        public void TheChoiceIsExactlyOneOfTheTwo()
        {
            // §22 shows New Game OR Continue — never both, and never neither. Written as a rule
            // over both inputs so a menu cannot drift into offering two buttons.
            foreach (bool saveExists in new[] { false, true })
            {
                MainMenuAction action = MainMenuRule.PrimaryAction(saveExists);

                Assert.That(action, Is.EqualTo(MainMenuAction.NewGame)
                    .Or.EqualTo(MainMenuAction.Continue));
                Assert.That(MainMenuRule.PrimaryLabel(saveExists), Is.Not.Empty);
            }
        }
    }

    /// <summary>What the Game scene assumes when no menu has run.</summary>
    public sealed class GameSessionTests
    {
        [TearDown]
        public void Clear() => GameSession.Reset();

        [Test]
        public void ItDefaultsToContinue()
        {
            // ⛔ THE DEFAULT IS LOAD-BEARING, not tidiness. The Game scene is opened directly by
            // the editor's play button and by every Play Mode test, with no menu having run.
            // Defaulting to New Game would have each of those silently DELETE the save.
            GameSession.Reset();

            Assert.That(GameSession.Requested, Is.EqualTo(GameEntryMode.Continue));
            Assert.That(GameSession.WasChosen, Is.False);
        }

        [Test]
        public void AMenuChoiceIsRememberedAndMarked()
        {
            GameSession.Choose(GameEntryMode.NewGame);

            Assert.That(GameSession.Requested, Is.EqualTo(GameEntryMode.NewGame));
            Assert.That(GameSession.WasChosen, Is.True,
                "nothing distinguishes a chosen Continue from the default one");
        }
    }
}
