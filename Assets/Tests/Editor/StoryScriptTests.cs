using NUnit.Framework;
using Smiley.Flow;

namespace Smiley.Tests
{
    /// <summary>The story is the owner's words, exactly (D-137).</summary>
    /// <remarks>
    /// ⛔ <b>These assertions exist to stop me editing the owner's prose.</b> Tidying a comma or
    /// shortening a sentence is the kind of change that feels harmless and is not mine to make.
    /// If a line here ever has to change, it changes because the owner said so.
    /// </remarks>
    public sealed class StoryScriptTests
    {
        [Test]
        public void TheOpeningHasThirteenLines()
        {
            // ⚠ THIRTEEN. I asserted fourteen first and this fixture caught it -- which is
            // exactly why the owner's prose is pinned rather than trusted to my counting.
            Assert.That(StoryScript.Opening.Length, Is.EqualTo(13));
        }

        [Test]
        public void TheEndingHasTwo()
        {
            Assert.That(StoryScript.Ending.Length, Is.EqualTo(2));
        }

        [Test]
        public void TheFirstAndLastWordsAreTheOwners()
        {
            Assert.That(StoryScript.Opening[0], Is.EqualTo("Once upon a time, in the land of Smiley..."));
            Assert.That(StoryScript.Opening[12], Does.StartWith("With the help of the village Smiley wizard"));
            Assert.That(StoryScript.Ending[0], Is.EqualTo("At last, Calvin reached the castle and rescued the princess."));
            Assert.That(StoryScript.Ending[1], Does.EndWith("chasing them from behind."));
        }

        [Test]
        public void TheNamesAreSpeltTheWayTheOwnerSpeltThem()
        {
            // ⚠ Calvin, Hime and the Smiley King. A character's name quietly changing spelling
            // between lines is the classic way a story gets corrupted by helpful editing.
            string all = string.Join(" ", StoryScript.Opening) + " " + string.Join(" ", StoryScript.Ending);

            Assert.That(all, Does.Contain("Calvin"));
            Assert.That(all, Does.Contain("Hime"));
            Assert.That(all, Does.Contain("Smiley King"));
            Assert.That(all, Does.Contain("Sky Smileys"));
        }

        [Test]
        public void EveryLineHasWordsInIt()
        {
            foreach (StoryMoment moment in new[] { StoryMoment.Opening, StoryMoment.Completion })
            {
                string[] lines = StoryScript.For(moment);

                for (int i = 0; i < lines.Length; i++)
                {
                    Assert.That(lines[i], Is.Not.Empty, $"{moment} line {i} is blank");
                    Assert.That(lines[i].Trim(), Is.EqualTo(lines[i]), $"{moment} line {i} has stray whitespace");
                }
            }
        }

        [Test]
        public void AskingBeyondTheEndIsQuiet()
        {
            // ⓘ The sequence clamps its own count to at least one, so page 0 of a short story
            // gets asked for. It must answer with silence, not an exception during the opening.
            Assert.That(StoryScript.Line(StoryMoment.Opening, 99), Is.Empty);
            Assert.That(StoryScript.Line(StoryMoment.Opening, -1), Is.Empty);
        }

        [Test]
        public void TheCountsMatchWhatTheSlidesReport()
        {
            Assert.That(StoryScript.CountFor(StoryMoment.Opening), Is.EqualTo(StoryScript.Opening.Length));
            Assert.That(StoryScript.CountFor(StoryMoment.Completion), Is.EqualTo(StoryScript.Ending.Length));
        }
    }
}
