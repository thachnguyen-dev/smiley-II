using System.Collections.Generic;
using NUnit.Framework;
using Smiley.Gameplay;
using UnityEngine;

namespace Smiley.Tests
{
    /// <summary>§15's map rules as arithmetic (D-075, D-076).</summary>
    public sealed class MapRuleTests
    {
        [Test]
        public void TheOwnersPixelsBecomeTwentySevenByOneNinetyTwoUnits()
        {
            // ⭐ OWNER, 2026-09-11 (Q-030, D-081): "Increase the current map size to 6480 × 46080
            // pixels." Pixels on the reference phone; the camera shows 10 units over 2400 px, so
            // 240 px/unit. (The first ruling that day was 3240 × 23040 = 13.5 × 96.)
            Assert.That(MapRule.OwnerWidthPixels, Is.EqualTo(6480f));
            Assert.That(MapRule.OwnerHeightPixels, Is.EqualTo(46080f));
            Assert.That(MapRule.PixelsPerUnit, Is.EqualTo(240f));
            Assert.That(MapRule.WorldSize.x, Is.EqualTo(27f).Within(1e-4f));
            Assert.That(MapRule.WorldSize.y, Is.EqualTo(192f).Within(1e-4f));
        }

        [Test]
        public void ThatIsSixScreensWideAndNineteenTall()
        {
            float viewportWidth = MapRule.ReferenceViewportHeightUnits * (1080f / 2400f);

            Assert.That(MapRule.WorldSize.x / viewportWidth, Is.EqualTo(6f).Within(1e-4f));
            Assert.That(MapRule.WorldSize.y / MapRule.ReferenceViewportHeightUnits, Is.EqualTo(19.2f).Within(1e-4f));
        }

        [Test]
        public void AMissingOrShortBackgroundIsRefused()
        {
            // D-082: the background is part of the map and must cover all of it.
            Assert.That(MapRule.Validate(World, AGoodMap(), OneCheckpoint(), null), Has.Some.Contains("Background"));

            var half = new Rect(World.xMin, World.yMin, World.width, World.height * 0.5f);
            Assert.That(MapRule.Validate(World, AGoodMap(), OneCheckpoint(), half), Has.Some.Contains("Background covers"));
        }

        private static MapRule.PlatformInfo Good(string name, Rect bounds, bool start = false) =>
            new MapRule.PlatformInfo
            {
                Name = name, Bounds = bounds, IsStartArea = start,
                HasShape = true, VisualOnRoot = false, HasVisualChild = true, TuningMissing = false,
            };

        private static Rect World => MapRule.World(-4.9f);

        private static Rect? Covering => World;

        private static List<MapRule.PlatformInfo> AGoodMap() => new List<MapRule.PlatformInfo>
        {
            Good("StartArea", new Rect(-13.5f, -4.8f, 27f, 0.8f), start: true),
            Good("Platform_01", new Rect(-2.75f, -1.6f, 3.2f, 0.4f)),
        };

        private static List<Rect> OneCheckpoint() => new List<Rect> { new Rect(0.8f, 18.6f, 1.6f, 0.6f) };

        [Test]
        public void AGoodMapHasNoProblems()
        {
            Assert.That(MapRule.Validate(World, AGoodMap(), OneCheckpoint(), Covering), Is.Empty);
        }

        [Test]
        public void AWorldOfTheWrongSizeIsRefused()
        {
            // ⛔ The owner's number, guarded like Q-008's three seconds: a drag on the bounds
            // handle in the prefab must not silently shrink the map.
            var wrong = new Rect(-5.5f, -4.9f, 11f, 25f);

            List<string> problems = MapRule.Validate(wrong, AGoodMap(), OneCheckpoint(), Covering);

            Assert.That(problems, Has.Some.Contains("Q-030"));
        }

        [Test]
        public void APlatformOutsideTheWorldIsNamed()
        {
            List<MapRule.PlatformInfo> map = AGoodMap();
            map.Add(Good("Platform_Lost", new Rect(12.5f, 3f, 3f, 0.4f)));

            Assert.That(MapRule.Validate(World, map, OneCheckpoint(), Covering), Has.Some.Contains("Platform_Lost"));
        }

        [Test]
        public void APlatformRestingOnTheEdgeIsFine()
        {
            List<MapRule.PlatformInfo> map = AGoodMap();
            map.Add(Good("Platform_Edge", new Rect(10.5f, 187.1f - 0.4f, 3f, 0.4f)));

            Assert.That(MapRule.Validate(World, map, OneCheckpoint(), Covering), Is.Empty);
        }

        [Test]
        public void TheStartAreaMustSpanTheWholeWidth()
        {
            // §4: "spans the entire width of the map."
            var map = new List<MapRule.PlatformInfo>
            {
                Good("StartArea", new Rect(-10f, -4.8f, 20f, 0.8f), start: true),
                Good("Platform_01", new Rect(-2.75f, -1.6f, 3.2f, 0.4f)),
            };

            Assert.That(MapRule.Validate(World, map, OneCheckpoint(), Covering), Has.Some.Contains("entire width"));
        }

        [Test]
        public void TwoPlatformsWithOneNameAreRefused()
        {
            // §21 saves a Disappearing Platform's state by name.
            List<MapRule.PlatformInfo> map = AGoodMap();
            map.Add(Good("Platform_01", new Rect(1f, 3f, 3f, 0.4f)));

            Assert.That(MapRule.Validate(World, map, OneCheckpoint(), Covering), Has.Some.Contains("both named"));
        }

        [Test]
        public void ExactlyOneCheckpointInsideTheWorld()
        {
            Assert.That(MapRule.Validate(World, AGoodMap(), new List<Rect>(), Covering), Has.Some.Contains("0 Top Checkpoints"));
            Assert.That(MapRule.Validate(World, AGoodMap(), new List<Rect> { new Rect(0f, 300f, 1f, 1f) }, Covering),
                Has.Some.Contains("outside the world"));
        }

        [Test]
        public void ThePictureMustBeOnTheVisualChildNotTheRoot()
        {
            // ⭐ Owner, 2026-09-11: the artwork is replaced later; the gameplay components must
            // not change when it is. A renderer on the root is how that separation erodes.
            List<MapRule.PlatformInfo> map = AGoodMap();
            MapRule.PlatformInfo bad = Good("Platform_Art", new Rect(1f, 3f, 3f, 0.4f));
            bad.VisualOnRoot = true;
            map.Add(bad);

            Assert.That(MapRule.Validate(World, map, OneCheckpoint(), Covering), Has.Some.Contains("Visual"));
        }

        [Test]
        public void AMissingTuningIsNamed()
        {
            List<MapRule.PlatformInfo> map = AGoodMap();
            MapRule.PlatformInfo bad = Good("Platform_Slip", new Rect(1f, 3f, 3f, 0.4f));
            bad.TuningMissing = true;
            map.Add(bad);

            Assert.That(MapRule.Validate(World, map, OneCheckpoint(), Covering), Has.Some.Contains("SmileyTuning"));
        }
    }
}
