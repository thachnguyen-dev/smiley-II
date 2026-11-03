namespace Smiley.Gameplay
{
    /// <summary>
    /// Which guided-opening prompt is on screen, if any (D-131).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ Owner, 2026-10-10: a guided opening inside the map, taught with short text prompts near
    /// Smiley that fade once the action is performed. ⛔ <b>This amends §30</b>, which allows
    /// "exactly one instruction image" and no separate tutorial level; the prompts are a second
    /// teaching surface and the owner added them knowingly, after the conflict was put to them.
    /// </para>
    /// <para>
    /// ⛔ <b>What §30 still forbids, and this does not do:</b> no Skip button, no hints after
    /// repeated failure, no separate tutorial level — the prompts sit in the real map, over the
    /// real first climb. ⓘ And §30's principle survives: only the basic control is taught. Two
    /// prompts, then silence, and every other mechanic is still left to be discovered.
    /// </para>
    /// <para>
    /// ⚠ <b>Nothing here is shown while Smiley is moving.</b> A prompt that appears mid-flight is
    /// read as a reaction to the flight; §8 already says a drag may only begin when he is at
    /// rest, so the prompt and the permission to act appear together.
    /// </para>
    /// </remarks>
    public static class TutorialPromptRule
    {
        /// <summary>The prompts, in the order a first-time player meets them.</summary>
        public enum Prompt
        {
            /// <summary>Nothing on screen.</summary>
            None = 0,

            /// <summary>⭐ Owner's words: how to move at all.</summary>
            Launch = 1,

            /// <summary>⭐ Owner's words: that it repeats.</summary>
            Again = 2,
        }

        /// <summary>⭐ The owner approved this wording on 2026-10-10. ⛔ Not mine to reword.</summary>
        public const string LaunchText = "Drag and release to launch";

        /// <summary>⭐ The owner approved this wording on 2026-10-10. ⛔ Not mine to reword.</summary>
        public const string AgainText = "Drag again to keep climbing";

        /// <summary>How many launches end the guided opening.</summary>
        /// <remarks>
        /// ⓘ Two, because the second prompt teaches that the control repeats — once the player
        /// has done it twice there is nothing left in §30's "basic controls" to say.
        /// </remarks>
        public const int LaunchesUntilDone = 2;

        /// <summary>
        /// The prompt to show right now.
        /// </summary>
        /// <param name="firstPlaythrough">
        /// False on a replay after completion. §30: the tutorial does not reappear then, and this
        /// is part of the tutorial.
        /// </param>
        /// <param name="launches">How many times Smiley has been launched this session.</param>
        /// <param name="atRest">
        /// Whether Smiley is settled and could be dragged — <c>SmileyController.IsReady</c>.
        /// </param>
        /// <param name="aScreenIsCovering">
        /// Whether a full screen (the story, the instruction image, the gear menu) is up. A
        /// prompt behind a covering screen is either invisible or, worse, visible through a gap.
        /// </param>
        public static Prompt Current(bool firstPlaythrough, int launches, bool atRest, bool aScreenIsCovering)
        {
            if (!firstPlaythrough || aScreenIsCovering || launches >= LaunchesUntilDone)
            {
                return Prompt.None;
            }

            if (!atRest)
            {
                // ⛔ Mid-flight. Say nothing: he cannot act on it until he lands anyway (§8).
                return Prompt.None;
            }

            return launches == 0 ? Prompt.Launch : Prompt.Again;
        }

        /// <summary>The words for a prompt, or empty for <see cref="Prompt.None"/>.</summary>
        public static string TextFor(Prompt prompt) => prompt switch
        {
            Prompt.Launch => LaunchText,
            Prompt.Again => AgainText,
            _ => string.Empty,
        };
    }
}
