using System.Collections;
using NUnit.Framework;
using Smiley.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;

namespace Smiley.Tests
{
    /// <summary>The owner's map, in the scene the game actually boots into (D-075, D-076).</summary>
    public sealed class MapSceneTests
    {
        [UnitySetUp]
        public IEnumerator Load()
        {
            yield return GameSceneFixture.LoadFresh();
        }

        [UnityTest]
        public IEnumerator TheWorldIsTheOwnersTwentySevenByOneNinetyTwo()
        {
            // ⭐ Q-030, owner 2026-09-11 (D-081): 6480 × 46080 px. Read from the LIVE scene, because
            // the prefab test can pass while the scene holds a stale copy.
            var bounds = Object.FindFirstObjectByType<LevelBounds>();

            Assert.That(bounds, Is.Not.Null);
            Assert.That(bounds.World.width, Is.EqualTo(27f).Within(0.01f));
            Assert.That(bounds.World.height, Is.EqualTo(192f).Within(0.01f));

            yield return null;
        }

        [UnityTest]
        public IEnumerator TheCameraShowsMoreThanTheReferenceAndTheMapDoesNotFollowIt()
        {
            // ⭐ D-091: the owner asked for a wider view. The camera shows more than the 10 units
            // the map's pixel size was ruled against — and the conversion stays 240 px/unit, so
            // the world is still 27 x 192. ⛔ A camera change must never resize the map.
            var camera = Object.FindFirstObjectByType<Camera>();
            var bounds = Object.FindFirstObjectByType<LevelBounds>();

            Assert.That(camera.orthographic, Is.True);
            Assert.That(camera.orthographicSize * camera.aspect * 2f, Is.GreaterThan(4.5f),
                "the owner asked for a wider view than the reference 4.5 units across");
            Assert.That(MapRule.PixelsPerUnit, Is.EqualTo(240f));
            Assert.That(bounds.World.size, Is.EqualTo(MapRule.WorldSize).Using<Vector2>((a, b) => (a - b).magnitude < 0.01f ? 0 : 1));

            yield return null;
        }

        [UnityTest]
        public IEnumerator TheFourBoundariesHugTheOwnersWorld()
        {
            // D-076: LevelBounds builds its own walls at runtime, so a world the owner resizes
            // in the prefab gets walls where the world now ends — not where a generator once put them.
            var bounds = Object.FindFirstObjectByType<LevelBounds>();
            Rect w = bounds.World;
            Boundary[] boundaries = Object.FindObjectsByType<Boundary>(FindObjectsSortMode.None);

            Assert.That(boundaries.Length, Is.EqualTo(4));

            foreach (Boundary boundary in boundaries)
            {
                Assert.That(boundary.transform.IsChildOf(bounds.transform), Is.True,
                    $"{boundary.name} is not built by LevelBounds");

                Bounds b = boundary.GetComponent<Collider2D>().bounds;
                bool outside = b.max.x <= w.xMin + 1e-3f || b.min.x >= w.xMax - 1e-3f
                               || b.max.y <= w.yMin + 1e-3f || b.min.y >= w.yMax - 1e-3f;

                Assert.That(outside, Is.True, $"{boundary.name} intrudes into the world at {b}");
                Assert.That(boundary.GetComponent<Platform>(), Is.Null, $"{boundary.name} is a Platform");
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator TheStartAreaSpansTheWholeWidthOfTheOwnersWorld()
        {
            // §4, now against 13.5 units rather than the test bed's 11.
            var bounds = Object.FindFirstObjectByType<LevelBounds>();
            Bounds start = GameObject.Find("StartArea").GetComponent<Collider2D>().bounds;

            Assert.That(start.min.x, Is.LessThanOrEqualTo(bounds.World.xMin + 1e-3f));
            Assert.That(start.max.x, Is.GreaterThanOrEqualTo(bounds.World.xMax - 1e-3f));

            yield return null;
        }

        [UnityTest]
        public IEnumerator TheBackgroundCoversTheWholeWorldAndIsBehindEverything()
        {
            // D-082: part of the map, covering the intended area, replaceable without touching
            // gameplay — so it is a picture with no collider, sized by the bounds.
            var bounds = Object.FindFirstObjectByType<LevelBounds>();
            var background = Object.FindFirstObjectByType<MapBackground>();

            Assert.That(background, Is.Not.Null, "the map has no Background");
            Assert.That(background.transform.IsChildOf(bounds.transform), Is.True, "the Background is not part of the map");
            Assert.That(background.GetComponent<Collider2D>(), Is.Null);
            Assert.That(background.GetComponent<Platform>(), Is.Null);

            var renderer = background.GetComponent<SpriteRenderer>();
            Rect world = bounds.World;

            Assert.That(renderer.bounds.min.x, Is.LessThanOrEqualTo(world.xMin + 1e-3f));
            Assert.That(renderer.bounds.max.x, Is.GreaterThanOrEqualTo(world.xMax - 1e-3f));
            Assert.That(renderer.bounds.min.y, Is.LessThanOrEqualTo(world.yMin + 1e-3f));
            Assert.That(renderer.bounds.max.y, Is.GreaterThanOrEqualTo(world.yMax - 1e-3f));
            Assert.That(renderer.sortingOrder, Is.LessThan(0), "the background must draw behind the platforms");

            yield return null;
        }

        [UnityTest]
        public IEnumerator EveryPlatformsColliderSitsInsideItsPicture()
        {
            // D-084: the collider is the picture minus its fringe minus the rest gap — never
            // larger than the picture, and never taller than the picture minus the rest gap.
            foreach (Platform platform in Object.FindObjectsByType<Platform>(FindObjectsSortMode.None))
            {
                var shape = platform.GetComponent<PlatformShape>();
                var collider = platform.GetComponent<BoxCollider2D>();
                var visual = platform.GetComponentInChildren<SpriteRenderer>();

                Assert.That(shape.FittedSprite, Is.SameAs(visual.sprite),
                    $"{platform.name}'s fringe was measured on a different sprite than it draws — the build did not re-measure");
                Assert.That(collider.size.y, Is.LessThanOrEqualTo(shape.Height - PlatformShape.PlayerRestGap + 1e-4f), platform.name);
                Assert.That(collider.size.x, Is.LessThanOrEqualTo(shape.Width + 1e-4f), platform.name);

                float colliderTop = collider.bounds.max.y;
                float pictureTop = visual.bounds.max.y;
                Assert.That(colliderTop, Is.LessThan(pictureTop), $"{platform.name}'s collider top is not below its picture top");
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator EveryPlatformInTheSceneIsSizedByItsShapeWithThePictureOnAChild()
        {
            // The owner's rule about replaceable art, checked where it matters: the running game.
            foreach (Platform platform in Object.FindObjectsByType<Platform>(FindObjectsSortMode.None))
            {
                var shape = platform.GetComponent<PlatformShape>();
                var collider = platform.GetComponent<BoxCollider2D>();

                Assert.That(shape, Is.Not.Null, $"{platform.name} has no PlatformShape");
                Assert.That(collider.size.x, Is.EqualTo(shape.Width - shape.ColliderInsets.left - shape.ColliderInsets.right).Within(1e-4f),
                    $"{platform.name}'s collider disagrees with its shape");
                Assert.That(platform.GetComponent<SpriteRenderer>(), Is.Null, $"{platform.name} draws on its root");
                Assert.That(platform.GetComponentInChildren<SpriteRenderer>(), Is.Not.Null, $"{platform.name} has no picture");
                Assert.That(platform.transform.localScale, Is.EqualTo(Vector3.one), $"{platform.name} is scaled");
            }

            yield return null;
        }
    }
}
