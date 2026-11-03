namespace Smiley.Flow
{
    /// <summary>
    /// The scenes, named once.
    /// </summary>
    /// <remarks>
    /// ⚠ Scene names are strings that fail at <b>runtime</b>, on the device, with nothing more
    /// than a console warning — so they are spelled in one place and the scene builder asserts
    /// that both are actually in the build settings. ⓘ D-1145's shape in the sibling project:
    /// content that exists and cannot be reached.
    /// </remarks>
    public static class Scenes
    {
        /// <summary>§22's Main Menu. The first scene the app opens.</summary>
        public const string Menu = "Menu";

        /// <summary>The climb.</summary>
        public const string Game = "Game";
    }
}
