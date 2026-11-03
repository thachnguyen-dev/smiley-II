using NUnit.Framework;
using Smiley.Gameplay;
using P = Smiley.Gameplay.TutorialPromptRule.Prompt;

namespace Smiley.Tests
{
    /// <summary>The guided opening appears exactly twice, and never in the way (D-131).</summary>
    public sealed class TutorialPromptRuleTests
    {
        private static P At(bool first = true, int launches = 0, bool atRest = true, bool covered = false) =>
            TutorialPromptRule.Current(first, launches, atRest, covered);

        [Test]
        public void TheFirstThingAPlayerSeesIsHowToLaunch()
        {
            Assert.That(At(), Is.EqualTo(P.Launch));
        }

        [Test]
        public void AfterLandingFromTheFirstLaunchItSaysToDoItAgain()
        {
            Assert.That(At(launches: 1), Is.EqualTo(P.Again));
        }

        [Test]
        public void AfterTwoLaunchesTheGuidedOpeningIsOver()
        {
            // ⛔ §30: only basic controls are taught; everything else is discovered.
            Assert.That(At(launches: 2), Is.EqualTo(P.None));
            Assert.That(At(launches: 37), Is.EqualTo(P.None));
        }

        [Test]
        public void NothingIsSaidWhileSmileyIsInTheAir()
        {
            // ⚠ He cannot drag until he is at rest (§8), so a prompt mid-flight is noise.
            Assert.That(At(atRest: false), Is.EqualTo(P.None));
            Assert.That(At(launches: 1, atRest: false), Is.EqualTo(P.None));
        }

        [Test]
        public void NothingIsSaidBehindACoveringScreen()
        {
            // The story, the instruction image, the gear menu. ⛔ A prompt showing through a gap
            // in a covering screen is the bug D-123 spent a whole decision on.
            Assert.That(At(covered: true), Is.EqualTo(P.None));
            Assert.That(At(launches: 1, covered: true), Is.EqualTo(P.None));
        }

        [Test]
        public void AReplayAfterCompletionIsNotTaughtAgain()
        {
            // ⛔ §30, unchanged by D-131: the tutorial does not reappear on a replay.
            Assert.That(At(first: false), Is.EqualTo(P.None));
            Assert.That(At(first: false, launches: 1), Is.EqualTo(P.None));
        }

        [Test]
        public void TheWordsAreTheOwnersAndTheyAreNotEmpty()
        {
            // ⭐ Approved 2026-10-10. ⛔ A fixture so a later edit of mine is a deliberate one.
            Assert.That(TutorialPromptRule.TextFor(P.Launch), Is.EqualTo("Drag and release to launch"));
            Assert.That(TutorialPromptRule.TextFor(P.Again), Is.EqualTo("Drag again to keep climbing"));
            Assert.That(TutorialPromptRule.TextFor(P.None), Is.Empty);
        }
    }
}
