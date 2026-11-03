using System.Collections;
using NUnit.Framework;
using Smiley.Gameplay;
using UnityEngine;
using Smiley.Save;
using UnityEngine.SceneManagement;

namespace Smiley.Tests
{
    /// <summary>How every Play Mode fixture gets into the game.</summary>
    /// <remarks>
    /// <para>
    /// ⛔ <b>THE SAVE MUST GO FIRST, AND THIS IS NOT HOUSEKEEPING.</b> Since §21,
    /// <see cref="SaveService"/> restores in <c>Start</c>, so a save left on disk places Smiley
    /// wherever the last test left him — with his rotation, his velocity and the platforms'
    /// countdowns — before any test body runs.
    /// </para>
    /// <para>
    /// ⚠ <b>It was caught by two rotation tests going red for reasons that had nothing to do
    /// with rotation.</b> §13's fixture launches Smiley and asserts he holds his orientation; a
    /// restored 9.4° tilt made him settle in mid-flight instead. And because the FIRST test of
    /// a fixture writes the save the SECOND one then inherits, the suite's result depended on
    /// how many times it had been run — the failure moved when the file was deleted.
    /// </para>
    /// <para>
    /// ⓘ <b>The game is behaving correctly; the tests were.</b> Resuming where you left off is
    /// exactly what §21 asks for. It is only a fixture that means to start from nothing that has
    /// to say so.
    /// </para>
    /// </remarks>
    public static class GameSceneFixture
    {
        /// <summary>The one scene, named once.</summary>
        public const string SceneName = "Game";

        /// <summary>Loads the game with no save to resume from.</summary>
        public static IEnumerator LoadFresh()
        {
            SaveFile.DeleteAll();

            SceneManager.LoadScene(SceneName, LoadSceneMode.Single);

            // Two frames: the first runs Awake and Start — which is where the restore would
            // happen — and the second lets the scene's first FixedUpdate land.
            yield return null;
            yield return null;
        }

        /// <summary>
        /// Removes every platform but the Start Area, leaving a floor under an empty sky.
        /// </summary>
        /// <remarks>
        /// ⭐ <b>For PHYSICS fixtures only.</b> Rolling distance, mid-air rotation, landing
        /// loudness and the drag squeeze are properties of Smiley, not of the map — and the map is
        /// the owner's now (D-076), dense enough that no column above the Start Area is clear for
        /// more than a few units. A fixture measuring a roll across a runway it does not have, or
        /// a flight through a platform, measures the map instead of Smiley. This turns the map
        /// off for the duration of one test; the next LoadFresh brings it back.
        /// ⛔ Never call it from a test about the map, the checkpoint, or §21's platform state.
        /// </remarks>
        public static void ClearTheSky()
        {
            foreach (Platform platform in Object.FindObjectsByType<Platform>(FindObjectsSortMode.None))
            {
                if (platform.name != "StartArea")
                {
                    platform.gameObject.SetActive(false);
                }
            }

            Physics2D.SyncTransforms();
        }

        /// <summary>
        /// An x inside the world where no platform sits between <paramref name="yMin"/> and
        /// <paramref name="yMax"/>, with room for Smiley either side.
        /// </summary>
        /// <remarks>
        /// ⭐ <b>The map is the owner's now (D-076), so a fixture cannot know where the gaps are.</b>
        /// Five tests used to launch or drop Smiley at x = -4.6 because that column was clear in
        /// the test bed; the owner's first map put a platform there and two of them measured a
        /// collision as mid-air rotation. This scans the live scene instead: the Start Area is
        /// ignored (it is the floor), every other platform overlapping the band pushes candidates
        /// away, and the candidate with the most clearance wins. It fails loudly if no column has
        /// room, which is a fact about the map worth hearing rather than a flaky test.
        /// </remarks>
        public static float ClearColumn(float yMin, float yMax)
        {
            var bounds = Object.FindFirstObjectByType<LevelBounds>();
            Assert.That(bounds, Is.Not.Null, "no LevelBounds in the scene");
            Rect world = bounds.World;

            var obstacles = new System.Collections.Generic.List<Bounds>();

            foreach (Platform platform in Object.FindObjectsByType<Platform>(FindObjectsSortMode.None))
            {
                if (platform.name == "StartArea")
                {
                    continue;
                }

                Bounds b = platform.GetComponent<Collider2D>().bounds;

                if (b.max.y >= yMin && b.min.y <= yMax)
                {
                    obstacles.Add(b);
                }
            }

            // Smiley is one unit across; a margin on top so a wobble is not a graze.
            const float Room = 0.9f;
            float bestX = 0f;
            float bestClearance = -1f;

            for (float x = world.xMin + Room; x <= world.xMax - Room; x += 0.05f)
            {
                float clearance = Mathf.Min(x - world.xMin, world.xMax - x);

                foreach (Bounds b in obstacles)
                {
                    float gap = x < b.center.x ? b.min.x - x : x - b.max.x;
                    clearance = Mathf.Min(clearance, gap);
                }

                if (clearance > bestClearance)
                {
                    bestClearance = clearance;
                    bestX = x;
                }
            }

            Assert.That(bestClearance, Is.GreaterThanOrEqualTo(Room),
                $"no column between y = {yMin} and {yMax} is clear of platforms — the best has " +
                $"{bestClearance:0.00} units of room at x = {bestX:0.00}");

            return bestX;
        }
    }
}
