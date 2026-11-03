namespace Smiley.Flow
{
    /// <summary>The one action §22's Main Menu offers to start playing.</summary>
    public enum MainMenuAction
    {
        /// <summary>No save exists. A fresh climb from the Start Area (§4).</summary>
        NewGame,

        /// <summary>A save exists. Resume it (§21).</summary>
        Continue,
    }

    /// <summary>
    /// §22's Main Menu, as a rule.
    /// </summary>
    /// <remarks>
    /// <para>
    /// §22 is three sentences: the menu is <b>minimal</b>; <i>"if there is no Save: show New
    /// Game"</i>; <i>"if a Save exists: show Continue"</i>. Plus Settings and Exit / Quit.
    /// </para>
    /// <para>
    /// ⛔ <b>ONE OF THEM, NEVER BOTH.</b> §22 offers a single starting action chosen by whether a
    /// save exists — not two buttons with one greyed out, and not both with New Game asking for
    /// confirmation. That reading is easy to drift away from once a Settings screen can delete
    /// the save (§25), so it is a rule with a test rather than a shape in a layout file.
    /// </para>
    /// <para>
    /// ⚠ <b>After completion there is still a save, so the menu still says Continue.</b> D-012
    /// resets the playable state to the Start Area and keeps the <i>completed</i> flag, which is
    /// exactly §20's <i>"replay the game… start again from the Start Area"</i>. ⓘ New Game comes
    /// back only through §25's Reset Game, which is the one thing that removes the save.
    /// </para>
    /// </remarks>
    public static class MainMenuRule
    {
        /// <summary>Which of the two starting actions §22 shows.</summary>
        public static MainMenuAction PrimaryAction(bool saveExists) =>
            saveExists ? MainMenuAction.Continue : MainMenuAction.NewGame;

        /// <summary>The label that action carries.</summary>
        public static string PrimaryLabel(bool saveExists) =>
            PrimaryAction(saveExists) == MainMenuAction.Continue ? "Continue" : "New Game";

        /// <summary>
        /// How Smiley should enter the game for a given menu action.
        /// </summary>
        /// <remarks>
        /// ⓘ A separate function from <see cref="PrimaryAction"/> because §25's Reset Game
        /// reaches the same New Game state by a different route — deleting the save rather than
        /// never having had one — and both must arrive at the same entry.
        /// </remarks>
        public static GameEntryMode EntryFor(MainMenuAction action) =>
            action == MainMenuAction.Continue ? GameEntryMode.Continue : GameEntryMode.NewGame;
    }
}
