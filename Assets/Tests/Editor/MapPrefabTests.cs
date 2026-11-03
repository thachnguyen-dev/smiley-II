using NUnit.Framework;
using Smiley.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Smiley.Tests
{
    /// <summary>
    /// The owner's map assets on disk: the three palette prefabs and the Map (D-076).
    /// </summary>
    /// <remarks>
    /// ⚠ These read <c>Assets/Map/*.prefab</c> — the files the owner edits — so they say what is
    /// true of the map NOW, not of a template. A prefab the owner has not created yet fails here,
    /// which is right: the scene builder writes them on its first run.
    /// </remarks>
    public sealed class MapPrefabTests
    {
        private const string Folder = "Assets/Map";

        private static GameObject Load(string file)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/{file}");
            Assert.That(prefab, Is.Not.Null, $"{Folder}/{file} does not exist — run tools/build-scene.sh once");
            return prefab;
        }

        [TestCase("Platform_Normal.prefab")]
        [TestCase("Platform_Slippery.prefab")]
        [TestCase("Platform_Disappearing.prefab")]
        public void APalettePrefabKeepsGameplayOnTheRootAndThePictureOnAChild(string file)
        {
            // ⭐ Owner, 2026-09-11: "keep the three platform prefabs structured so their visual
            // assets can be replaced without changing their gameplay components or behavior."
            GameObject root = Load(file);

            Assert.That(root.GetComponent<BoxCollider2D>(), Is.Not.Null, "no collider on the root");
            Assert.That(root.GetComponent<Platform>(), Is.Not.Null, "no Platform marker on the root");
            Assert.That(root.GetComponent<PlatformShape>(), Is.Not.Null, "no PlatformShape on the root");
            Assert.That(root.GetComponent<SpriteRenderer>(), Is.Null,
                "a renderer on the ROOT — the art is meant to live on the Visual child");

            Transform visual = root.transform.Find("Visual");
            Assert.That(visual, Is.Not.Null, "no child named Visual");
            Assert.That(visual.GetComponent<SpriteRenderer>(), Is.Not.Null, "Visual has no renderer");
            Assert.That(visual.GetComponent<SpriteRenderer>().sprite, Is.Not.Null, "Visual has no sprite");
            Assert.That(visual.GetComponents<Component>().Length, Is.EqualTo(2),
                "Visual carries more than a Transform and a SpriteRenderer — gameplay has leaked onto the art");

            Assert.That(root.GetComponent<BoxCollider2D>().sharedMaterial, Is.Not.Null,
                "the collider has no physics material, so friction would be the engine default");
        }

        [Test]
        public void ThePaletteTypesCarryTheirOwnComponentAndNoOther()
        {
            GameObject normal = Load("Platform_Normal.prefab");
            GameObject slippery = Load("Platform_Slippery.prefab");
            GameObject vanishing = Load("Platform_Disappearing.prefab");

            Assert.That(normal.GetComponent<SlipperyPlatform>(), Is.Null);
            Assert.That(normal.GetComponent<DisappearingPlatform>(), Is.Null);

            Assert.That(slippery.GetComponent<SlipperyPlatform>(), Is.Not.Null);
            Assert.That(slippery.GetComponent<DisappearingPlatform>(), Is.Null);

            Assert.That(vanishing.GetComponent<DisappearingPlatform>(), Is.Not.Null);
            Assert.That(vanishing.GetComponent<SlipperyPlatform>(), Is.Null);
        }

        [Test]
        public void TheTypedPrefabsHaveTheirTuningAssigned()
        {
            // The Q-008 three seconds live on SmileyTuning; a prefab without it falls back to a
            // literal and nobody notices.
            foreach (string file in new[] { "Platform_Slippery.prefab", "Platform_Disappearing.prefab" })
            {
                GameObject root = Load(file);
                Component type = (Component)root.GetComponent<SlipperyPlatform>() ?? root.GetComponent<DisappearingPlatform>();

                SerializedProperty tuning = new SerializedObject(type).FindProperty("_tuning");
                Assert.That(tuning?.objectReferenceValue, Is.Not.Null, $"{file} has no SmileyTuning");
            }
        }

        [Test]
        public void TheDisappearingPrefabFadesItsVisualChild()
        {
            GameObject root = Load("Platform_Disappearing.prefab");
            SerializedProperty renderer = new SerializedObject(root.GetComponent<DisappearingPlatform>()).FindProperty("_renderer");

            Assert.That(renderer?.objectReferenceValue, Is.SameAs(root.transform.Find("Visual").GetComponent<SpriteRenderer>()),
                "DisappearingPlatform does not point at the Visual child's renderer");
        }

        [Test]
        public void TheShapeDrivesBothTheColliderAndThePicture()
        {
            // One number, two consumers: the box Smiley lands on and the box the player sees.
            GameObject root = Load("Platform_Normal.prefab");
            var shape = root.GetComponent<PlatformShape>();
            var collider = root.GetComponent<BoxCollider2D>();
            var visual = root.transform.Find("Visual").GetComponent<SpriteRenderer>();

            (float top, float bottom, float left, float right) = shape.ColliderInsets;
            Assert.That(collider.size.x, Is.EqualTo(shape.Width - left - right).Within(1e-4f));
            Assert.That(collider.size.y, Is.EqualTo(shape.Height - top - bottom).Within(1e-4f));
            Assert.That(top, Is.GreaterThanOrEqualTo(PlatformShape.PlayerRestGap), "D-084: the collider top sits below the picture top");
            Assert.That(visual.drawMode, Is.EqualTo(SpriteDrawMode.Sliced), "the picture must be drawn at a size, not scaled");
            Assert.That(visual.size.x, Is.EqualTo(shape.Width).Within(1e-4f));
            Assert.That(visual.size.y, Is.EqualTo(shape.Height).Within(1e-4f));
            Assert.That(root.transform.localScale, Is.EqualTo(Vector3.one), "the root is scaled — the art would stretch");
        }

        [Test]
        public void TheMapIsTheOwnersSizeAndHoldsOneOfEverythingItMust()
        {
            GameObject map = Load("Map.prefab");
            var bounds = map.GetComponentInChildren<LevelBounds>();

            Assert.That(bounds, Is.Not.Null, "the Map has no LevelBounds");
            Assert.That(bounds.World.width, Is.EqualTo(MapRule.WorldSize.x).Within(0.01f), "width is not the owner's 6480 px");
            Assert.That(bounds.World.height, Is.EqualTo(MapRule.WorldSize.y).Within(0.01f), "height is not the owner's 46080 px");

            Assert.That(map.GetComponentsInChildren<TopCheckpoint>().Length, Is.EqualTo(1));
            Assert.That(map.GetComponentsInChildren<Platform>().Length, Is.GreaterThan(1));

            Transform start = map.transform.Find("StartArea");
            Assert.That(start, Is.Not.Null, "no StartArea in the Map");
            Assert.That(start.GetComponent<PlatformShape>().Width, Is.EqualTo(bounds.World.width).Within(0.01f),
                "§4: the Start Area spans the entire width");
        }

        [Test]
        public void TheMapHasABackgroundThatIsAPictureAndNothingElse()
        {
            // D-082: replaceable art, part of the map, no gameplay on it.
            GameObject map = Load("Map.prefab");
            Transform background = map.transform.Find("Background");

            Assert.That(background, Is.Not.Null, "no Background child");
            Assert.That(background.GetComponent<MapBackground>(), Is.Not.Null);
            Assert.That(background.GetComponent<SpriteRenderer>()?.sprite, Is.Not.Null, "the Background has no picture");
            Assert.That(background.GetComponent<Collider2D>(), Is.Null);
            Assert.That(background.GetComponent<Platform>(), Is.Null);
            Assert.That(background.GetComponent<SpriteRenderer>().sortingOrder, Is.EqualTo(MapBackground.SortingOrder));
        }

        [Test]
        public void EveryPlatformInTheMapIsAPaletteInstance()
        {
            // ⭐ Edit Platform_Slippery.prefab and every slippery platform in the map changes.
            // A platform that is a loose copy would not.
            GameObject map = Load("Map.prefab");

            foreach (Platform platform in map.GetComponentsInChildren<Platform>())
            {
                GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(platform.gameObject);
                Assert.That(source, Is.Not.Null, $"{platform.name} is not an instance of a palette prefab");
                Assert.That(AssetDatabase.GetAssetPath(source), Does.StartWith(Folder + "/Platform_"),
                    $"{platform.name} comes from {AssetDatabase.GetAssetPath(source)}, not the palette");
            }
        }
    }
}
