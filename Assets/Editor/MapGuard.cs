using System.Collections.Generic;
using Smiley.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Smiley.Editor.Setup
{
    /// <summary>
    /// Refuses a build whose map cannot be played.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ The map is the owner's prefab (D-076), so the things a scene builder used to guarantee
    /// by construction — a Start Area across the whole width, one Top Checkpoint, every platform
    /// inside the walls, a world the size the owner ruled — are now things a build must
    /// <i>check</i>. The rules are <see cref="MapRule.Validate"/>, in the runtime assembly where
    /// tests reach them; this reads the prefab and hands them a description.
    /// </para>
    /// <para>
    /// ⓘ Runs in the scene builder and in the APK build, like <c>UiLayoutGuard</c> — "deploy it"
    /// never regenerates the scene, and a map edited after the last regeneration must still be
    /// checked before it reaches the phone (D-071's lesson).
    /// </para>
    /// </remarks>
    public static class MapGuard
    {
        /// <summary>Checks the Map prefab. Returns the number of problems; zero is buildable.</summary>
        public static int Check()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MapAssets.MapPrefab);

            if (prefab == null)
            {
                Debug.LogError($"[MAP-GUARD] {MapAssets.MapPrefab} is missing entirely.");
                return 1;
            }

            var bounds = prefab.GetComponentInChildren<LevelBounds>();

            if (bounds == null)
            {
                Debug.LogError("[MAP-GUARD] The Map has no LevelBounds, so the world has no edges.");
                return 1;
            }

            var platforms = new List<MapRule.PlatformInfo>();

            foreach (Platform platform in prefab.GetComponentsInChildren<Platform>())
            {
                var collider = platform.GetComponent<BoxCollider2D>();
                var shape = platform.GetComponent<PlatformShape>();
                Transform visual = platform.transform.Find(MapAssets.VisualName);

                bool tuningMissing =
                    (platform.TryGetComponent(out SlipperyPlatform slippery) && FieldIsNull(slippery, "_tuning"))
                    || (platform.TryGetComponent(out DisappearingPlatform vanishing) && FieldIsNull(vanishing, "_tuning"));

                platforms.Add(new MapRule.PlatformInfo
                {
                    Name = platform.name,
                    Bounds = WorldRect(platform.transform, collider),
                    IsStartArea = platform.name == MapAssets.StartAreaName,
                    HasShape = shape != null,
                    VisualOnRoot = platform.GetComponent<SpriteRenderer>() != null,
                    HasVisualChild = visual != null && visual.GetComponent<SpriteRenderer>() != null,
                    TuningMissing = tuningMissing,
                });
            }

            var checkpoints = new List<Rect>();

            foreach (TopCheckpoint checkpoint in prefab.GetComponentsInChildren<TopCheckpoint>())
            {
                checkpoints.Add(WorldRect(checkpoint.transform, checkpoint.GetComponent<BoxCollider2D>()));
            }

            var background = prefab.GetComponentInChildren<MapBackground>();
            Rect? covered = background != null ? background.Covered : (Rect?)null;

            List<string> problems = MapRule.Validate(bounds.World, platforms, checkpoints, covered);

            foreach (string problem in problems)
            {
                Debug.LogError($"[MAP-GUARD] {MapAssets.MapPrefab}: {problem}");
            }

            if (problems.Count == 0)
            {
                Debug.Log(
                    $"[MAP-GUARD] {MapAssets.MapPrefab}: {platforms.Count} platforms, " +
                    $"{checkpoints.Count} checkpoint, world {bounds.World.width} x {bounds.World.height} units.");
            }

            return problems.Count;
        }

        /// <summary>A box collider's rectangle in world units, scale included.</summary>
        private static Rect WorldRect(Transform transform, BoxCollider2D collider)
        {
            Vector2 size = collider != null ? collider.size : Vector2.one;
            Vector2 offset = collider != null ? collider.offset : Vector2.zero;
            Vector3 scale = transform.lossyScale;

            var extent = new Vector2(Mathf.Abs(size.x * scale.x), Mathf.Abs(size.y * scale.y));
            Vector2 centre = (Vector2)transform.position + Vector2.Scale(offset, scale);

            return new Rect(centre - extent * 0.5f, extent);
        }

        private static bool FieldIsNull(Object target, string field)
        {
            SerializedProperty property = new SerializedObject(target).FindProperty(field);
            return property == null || property.objectReferenceValue == null;
        }
    }
}
