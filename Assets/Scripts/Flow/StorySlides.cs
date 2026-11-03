using UnityEngine;
using UnityEngine.UIElements;

namespace Smiley.Flow
{
    /// <summary>Which of §20's two stories is wanted.</summary>
    /// <remarks>
    /// ⭐ <b>Owner ruling, 2026-09-11 (D-058): there are TWO stories, not one.</b> §20 as written
    /// says <i>"there is one story for the entire game"</i>, so this is an amendment to an
    /// approved section rather than an implementation detail.
    /// </remarks>
    public enum StoryMoment
    {
        /// <summary>Shown when the game is opened for the first time, or after §25's Reset Game.</summary>
        Opening,

        /// <summary>§20's original: shown after reaching the Top Checkpoint (§19).</summary>
        Completion,
    }

    /// <summary>
    /// Holds §20's images and puts the current one on the Story screen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Owner, 2026-09-11 (D-068):</b> the opening is <b>seven images</b> and the ending
    /// <b>two</b>, each advanced with a Next button. This replaced a video player the same day it
    /// was built; nothing here reads a clock.
    /// </para>
    /// <para>
    /// ⛔ <b>THE IMAGES ARE THE OWNER'S (Q-050), AND NONE IS NOT AN ERROR.</b> The game must run
    /// before they exist, or every feature downstream becomes untestable until an artist has
    /// finished. An empty list shows one blank slide with a working Next.
    /// </para>
    /// <para>
    /// ⚠ <b>Count-agnostic on purpose.</b> The owner said seven and two, and the lists below take
    /// whatever is assigned — a build that refused an eighth image, or a first draft with three,
    /// would be enforcing a number the owner can change by dragging a file. The counts are
    /// recorded in the guide, not in a guard.
    /// </para>
    /// <para>
    /// ⓘ The line lands in the Label named <c>story-text</c>, which is in the owner's layout
    /// (D-057) — they place and style it; this only fills it.
    /// </para>
    /// </remarks>
    public sealed class StorySlides : MonoBehaviour
    {
        // ⛔ D-137: the pictures are gone. The owner replaced the illustrated cutscenes with
        // text, so the lines come from StoryScript and nothing is assigned in the Inspector any
        // more. ⓘ The art files are NOT deleted — bg_8 is the app icon's source — they are
        // simply no longer shown here.
        private Label _surface;

        /// <summary>Which story is loaded right now. For tests.</summary>
        public StoryMoment Moment { get; private set; } = StoryMoment.Completion;

        /// <summary>The line currently on screen, or empty. For tests.</summary>
        public string Showing { get; private set; } = string.Empty;

        /// <summary>How many pages the given story has.</summary>
        public int CountFor(StoryMoment moment) => StoryScript.CountFor(moment);

        /// <summary>Points at one of the two stories, without showing anything yet.</summary>
        public void Load(StoryMoment moment)
        {
            Moment = moment;
        }

        /// <summary>Puts the given page of the loaded story on the screen.</summary>
        /// <remarks>
        /// ⓘ Out of range is empty, not an exception: the sequence clamps its own count to at
        /// least one, so page 0 of an empty story is asked for and must answer quietly.
        /// </remarks>
        public void Show(int index)
        {
            Showing = StoryScript.Line(Moment, index);

            if (_surface != null)
            {
                _surface.text = Showing;
            }
        }

        /// <summary>Clears the screen.</summary>
        public void Hide()
        {
            Showing = string.Empty;

            if (_surface != null)
            {
                _surface.text = string.Empty;
            }
        }

        /// <summary>Draws into the element the owner's layout provides.</summary>
        /// <remarks>
        /// ⓘ Looked up by name each time the screens rebuild rather than once, because the owner
        /// may replace the layout and the element with it (D-057).
        /// </remarks>
        public void AttachTo(VisualElement surface)
        {
            // ⓘ The owner's layout provides a Label now rather than a picture frame. A non-Label
            // is treated as absent rather than throwing — the story then simply shows nothing,
            // which is survivable, where an exception during the opening is not.
            _surface = surface as Label;
        }
    }
}
