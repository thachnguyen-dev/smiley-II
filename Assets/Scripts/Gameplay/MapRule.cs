using System.Collections.Generic;
using UnityEngine;

namespace Smiley.Gameplay
{
    /// <summary>
    /// §15's map: the owner's size, and what a hand-crafted map must satisfy.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>OWNER-AUTHORED, 2026-09-11 (Q-030, D-075, re-ruled D-081): the map is 6480 × 46080.</b>
    /// Those are pixels on the reference phone — six screens wide and 19.2 tall on 1080 × 2400
    /// — and the world is authored in units, so the conversion is pinned here: the camera shows
    /// <see cref="ReferenceViewportHeightUnits"/> units over <see cref="ReferenceScreenHeightPixels"/>
    /// pixels, which makes <b>240 px per unit</b> and a world of <b>27 × 192 units</b>. ⓘ The
    /// first ruling was 3240 × 23040 (13.5 × 96); the owner doubled both the same day.
    /// ⛔ Do not retune the two owner numbers. The conversion is mine and is what the camera
    /// already does; change the camera and this changes with it, which a test pins.
    /// </para>
    /// <para>
    /// ⓘ The rest is arithmetic over a description of the map, kept out of the Editor so it can
    /// be tested without a prefab: every platform inside the world, one Top Checkpoint, a Start
    /// Area spanning the full width (§4), unique names (§21 saves platform state by name), and
    /// a background that covers the world (D-082).
    /// The Editor's <c>MapGuard</c> reads the prefab and feeds this; a build fails on any line
    /// it returns.
    /// </para>
    /// </remarks>
    public static class MapRule
    {
        /// <summary>⭐ Owner, 2026-09-11: the map's width, in reference-phone pixels.</summary>
        public const float OwnerWidthPixels = 6480f;

        /// <summary>⭐ Owner, 2026-09-11: the map's height, in reference-phone pixels.</summary>
        public const float OwnerHeightPixels = 46080f;

        /// <summary>The reference phone's height in pixels (1080 × 2400).</summary>
        public const float ReferenceScreenHeightPixels = 2400f;

        /// <summary>
        /// World units the camera showed vertically when the map's pixel size was ruled: 10.
        /// ⛔ An AUTHORING constant, not the live camera (D-091): the owner widened the view
        /// afterwards and the map did not change, so this must not follow the camera.
        /// </summary>
        public const float ReferenceViewportHeightUnits = 10f;

        /// <summary>240 on the reference phone.</summary>
        public const float PixelsPerUnit = ReferenceScreenHeightPixels / ReferenceViewportHeightUnits;

        /// <summary>The map in world units: 27 × 192.</summary>
        public static Vector2 WorldSize =>
            new Vector2(OwnerWidthPixels / PixelsPerUnit, OwnerHeightPixels / PixelsPerUnit);

        /// <summary>How far off the owner's size a map may be before the build refuses it.</summary>
        public const float SizeTolerance = 0.01f;

        /// <summary>Slack for a platform resting exactly on the world's edge.</summary>
        public const float EdgeTolerance = 0.001f;

        /// <summary>The world rectangle, centred horizontally, with its floor at the given height.</summary>
        public static Rect World(float floorY) =>
            new Rect(-WorldSize.x * 0.5f, floorY, WorldSize.x, WorldSize.y);

        /// <summary>Whether <paramref name="item"/> lies entirely inside <paramref name="world"/>.</summary>
        public static bool Contains(Rect world, Rect item)
        {
            return item.xMin >= world.xMin - EdgeTolerance
                   && item.xMax <= world.xMax + EdgeTolerance
                   && item.yMin >= world.yMin - EdgeTolerance
                   && item.yMax <= world.yMax + EdgeTolerance;
        }

        /// <summary>What the guard knows about one platform in the map.</summary>
        public struct PlatformInfo
        {
            public string Name;
            public Rect Bounds;
            public bool IsStartArea;
            public bool HasShape;
            public bool VisualOnRoot;
            public bool HasVisualChild;
            public bool TuningMissing;
        }

        /// <summary>
        /// Every way the map is wrong, as messages. Empty means the map is buildable.
        /// </summary>
        public static List<string> Validate(
            Rect world, IReadOnlyList<PlatformInfo> platforms, IReadOnlyList<Rect> checkpoints,
            Rect? background)
        {
            var problems = new List<string>();

            if (background == null)
            {
                problems.Add(
                    "the map has no Background child. The build adds one; if it was deleted, " +
                    "run tools/build-scene.sh.");
            }
            else if (!Contains(background.Value, world))
            {
                problems.Add(
                    $"the Background covers {background.Value} but the world is {world} — part " +
                    "of the map would show nothing behind it.");
            }

            if (Mathf.Abs(world.width - WorldSize.x) > SizeTolerance
                || Mathf.Abs(world.height - WorldSize.y) > SizeTolerance)
            {
                problems.Add(
                    $"the world is {world.width} x {world.height} units, and the owner ruled " +
                    $"{OwnerWidthPixels} x {OwnerHeightPixels} px = {WorldSize.x} x {WorldSize.y} " +
                    "units (Q-030). Set LevelBounds back, or ask the owner.");
            }

            if (platforms.Count == 0)
            {
                problems.Add("the map has no platforms at all.");
            }

            var seen = new HashSet<string>();
            int startAreas = 0;

            foreach (PlatformInfo platform in platforms)
            {
                if (!seen.Add(platform.Name))
                {
                    problems.Add(
                        $"two platforms are both named '{platform.Name}'. §21 saves a " +
                        "Disappearing Platform's state by name, so they must be unique.");
                }

                if (!Contains(world, platform.Bounds))
                {
                    problems.Add(
                        $"'{platform.Name}' at {platform.Bounds} is outside the world {world}.");
                }

                if (platform.IsStartArea)
                {
                    startAreas++;

                    if (platform.Bounds.width < world.width - EdgeTolerance)
                    {
                        problems.Add(
                            $"the Start Area is {platform.Bounds.width} wide; §4 says it spans " +
                            $"the entire width of the map ({world.width}).");
                    }
                }

                if (!platform.HasShape)
                {
                    problems.Add($"'{platform.Name}' has no PlatformShape — size it with one.");
                }

                if (platform.VisualOnRoot || !platform.HasVisualChild)
                {
                    problems.Add(
                        $"'{platform.Name}' does not keep its picture on a child named Visual. " +
                        "The art is replaced there; the gameplay components stay on the root.");
                }

                if (platform.TuningMissing)
                {
                    problems.Add(
                        $"'{platform.Name}' has no SmileyTuning assigned, so its type would " +
                        "fall back to defaults.");
                }
            }

            if (startAreas != 1)
            {
                problems.Add($"the map has {startAreas} Start Areas; §4 needs exactly one.");
            }

            if (checkpoints.Count != 1)
            {
                problems.Add(
                    $"the map has {checkpoints.Count} Top Checkpoints; §19 needs exactly one.");
            }

            foreach (Rect checkpoint in checkpoints)
            {
                if (!Contains(world, checkpoint))
                {
                    problems.Add($"the Top Checkpoint at {checkpoint} is outside the world.");
                }
            }

            return problems;
        }
    }
}
