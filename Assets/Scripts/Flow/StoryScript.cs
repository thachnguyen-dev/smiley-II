namespace Smiley.Flow
{
    /// <summary>
    /// The story, in the owner's own words (D-137).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Owner, 2026-10-12.</b> §20 left the medium open — <i>"Not Yet Decided: whether the
    /// Story uses an image or a video"</i> — and the owner has now decided it: **text**, on a
    /// black screen, narrated in a panel at the bottom. ⓘ That closes an open question rather
    /// than amending the specification.
    /// </para>
    /// <para>
    /// ⛔ <b>Every line below is the owner's, copied exactly.</b> Not shortened, not re-punctuated,
    /// not "improved". <c>StoryScriptTests</c> pins all fifteen, so a later edit of mine is a
    /// deliberate act rather than a drift.
    /// </para>
    /// <para>
    /// ⚠ §20 asks for <i>"approximately several tens of seconds"</i>. Thirteen opening lines read
    /// at a comfortable pace is roughly a minute, and the player sets that pace by pressing Next.
    /// ⛔ There is still no Skip — §20 forbids one and that has not changed.
    /// </para>
    /// </remarks>
    public static class StoryScript
    {
        /// <summary>⭐ The owner's opening, one line per page.</summary>
        public static readonly string[] Opening =
        {
            "Once upon a time, in the land of Smiley...",
            "The Smileys lived peacefully and happily together.",
            "Calvin was one of the Smileys, and his favorite hobby was growing flowers.",
            "As usual, Calvin continued working diligently in his garden.",
            "High above the clouds stood a magnificent castle, home to the Sky Smileys.",
            "Hime was a princess, but spending all her time confined to the castle made her feel bored.",
            "One day, she looked down and saw Calvin tending to his flowers.",
            "She flew down and asked to become friends. Calvin happily agreed and even picked the most beautiful flowers to give her.",
            "As the days passed, their relationship grew closer and closer.",
            "The Smiley King discovered their friendship and became furious. He ordered his soldiers to take the princess back and lock her inside the castle, forbidding her from returning to the ground.",
            "From that moment on, Calvin was always sad, and his flower garden gradually withered.",
            "He finally made the decision to climb into the sky and rescue the princess.",
            "With the help of the village Smiley wizard, who created a series of steps, Calvin began climbing upward one step at a time. Although he sometimes failed and fell back down, he never gave up.",
        };

        /// <summary>⭐ The owner's ending, shown after the Top Checkpoint.</summary>
        public static readonly string[] Ending =
        {
            "At last, Calvin reached the castle and rescued the princess.",
            "The two held hands and ran away together, with the soldiers and the furious Smiley King chasing them from behind.",
        };

        /// <summary>The lines for one moment of the story.</summary>
        public static string[] For(StoryMoment moment) =>
            moment == StoryMoment.Opening ? Opening : Ending;

        /// <summary>How many pages a moment has.</summary>
        public static int CountFor(StoryMoment moment) => For(moment).Length;

        /// <summary>
        /// One page's text, or empty when the index is outside the story.
        /// </summary>
        /// <remarks>
        /// ⓘ Empty rather than an exception, for the same reason the images were blank: the
        /// sequence clamps its own count to at least one, so page 0 of an empty story is asked
        /// for and must answer quietly.
        /// </remarks>
        public static string Line(StoryMoment moment, int index)
        {
            string[] lines = For(moment);
            return index >= 0 && index < lines.Length ? lines[index] : string.Empty;
        }
    }
}
