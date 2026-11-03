namespace Smiley.Flow
{
    /// <summary>How the Game scene is being entered.</summary>
    public enum GameEntryMode
    {
        /// <summary>§21: resume the most recent Auto Save.</summary>
        Continue,

        /// <summary>§22: a fresh climb. Any existing save is discarded first.</summary>
        NewGame,
    }

    /// <summary>
    /// What the Main Menu asked for, carried across the scene load.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠ <b>Static, and that is the point rather than a shortcut.</b> The Game scene is loaded in
    /// Single mode, so nothing in the Menu scene survives to be read — and the alternative,
    /// a <c>DontDestroyOnLoad</c> object, is a second copy of the same one-field idea that also
    /// has to be prevented from accumulating duplicates. One field, set immediately before the
    /// load and read immediately after it.
    /// </para>
    /// <para>
    /// ⛔ <b>It defaults to Continue, and that matters for more than tidiness.</b> The Game scene
    /// is opened directly all the time — by the editor's play button, and by every Play Mode
    /// test — with no menu having run. Defaulting to New Game would have those entries silently
    /// <b>delete the player's save</b>. Defaulting to Continue makes a direct entry behave the
    /// way the game did before §22 existed.
    /// </para>
    /// </remarks>
    public static class GameSession
    {
        private static GameEntryMode _requested = GameEntryMode.Continue;

        /// <summary>What the menu asked for. Read by the save service as the game starts.</summary>
        public static GameEntryMode Requested
        {
            get => _requested;
            set => _requested = value;
        }

        /// <summary>
        /// Whether the mode was set by a menu this run, rather than being the default.
        /// </summary>
        /// <remarks>
        /// ⓘ Lets the game tell "the player pressed Continue" from "nobody asked, so assume
        /// Continue" — different situations that happen to want the same behaviour today, and
        /// would not if §30's Tutorial ever keyed off a genuine New Game.
        /// </remarks>
        public static bool WasChosen { get; private set; }

        /// <summary>Records the menu's choice.</summary>
        public static void Choose(GameEntryMode mode)
        {
            _requested = mode;
            WasChosen = true;
        }

        /// <summary>Back to the default, as though the app had just started.</summary>
        public static void Reset()
        {
            _requested = GameEntryMode.Continue;
            WasChosen = false;
        }
    }
}
