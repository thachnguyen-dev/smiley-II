using System;

namespace Smiley.View
{
    /// <summary>
    /// Which idle animation plays next.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ Owner, 2026-09-11 (D-086): <i>"the player will have multiple Idle animations, and the game
    /// should randomly alternate between them. At any given time, randomly play one of the
    /// available Idle animations, then randomly choose another Idle animation for the next cycle.
    /// This is an Idle animation system, not an event-based animation system."</i>
    /// </para>
    /// <para>
    /// ⓘ "Another" is read literally: with two or more clips, the next is never the one that just
    /// played. With one clip it is that clip again; with none there is nothing to pick. The
    /// randomness is <see cref="Random"/>, not Unity's — this is a picture, and it must not
    /// disturb anything else that draws random numbers.
    /// </para>
    /// </remarks>
    public static class IdlePicker
    {
        /// <summary>The next index to play, or -1 when there are no clips.</summary>
        public static int Next(int count, int previous, Random random)
        {
            if (count <= 0)
            {
                return -1;
            }

            if (count == 1)
            {
                return 0;
            }

            // Nothing played yet: any clip. ⚠ The first draft drew from count − 1 here too, and
            // the last clip could never open — a test caught it.
            if (previous < 0 || previous >= count)
            {
                return random.Next(count);
            }

            // Draw from the others and skip past the previous one, so every other clip is
            // equally likely and the previous never comes up.
            int pick = random.Next(count - 1);

            return pick >= previous ? pick + 1 : pick;
        }
    }
}
