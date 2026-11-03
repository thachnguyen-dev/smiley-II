using System.IO;
using Smiley.Config;
using Smiley.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Smiley.Editor.Setup
{
    /// <summary>
    /// Writes the owner's map assets once, and never again.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>THE MAP IS THE OWNER'S, THE WAY THE UXML IS (D-076).</b> §15 says the layout is
    /// hand-crafted by the designer, and until now the platforms were nine lines in the scene
    /// builder that every regeneration rewrote — there was no way to hand-craft anything. Now
    /// there is one prefab, <c>Assets/Map/Map.prefab</c>, holding the whole world: the Start
    /// Area, every platform, the Top Checkpoint and the <see cref="LevelBounds"/>. The scene
    /// places an instance of it and nothing else; the builder can regenerate the scene as
    /// often as it likes and the map does not move.
    /// </para>
    /// <para>
    /// ⭐ <b>Three palette prefabs, one per §14 type</b>, so the owner drags a Slippery platform
    /// in rather than remembering which component makes one. ⛔ <b>Each keeps its picture on a
    /// child named Visual</b> (owner, 2026-09-11: the artwork is replaced later) — the root
    /// carries the collider, the <see cref="Platform"/> marker, the <see cref="PlatformShape"/>
    /// and the type; the child carries a <see cref="SpriteRenderer"/> and nothing else. Swap the
    /// sprite and no gameplay component changes.
    /// </para>
    /// <para>
    /// ⛔ <b>Written only if missing.</b> The same rule as <c>UiLayouts.Ensure</c>: the first
    /// build writes the files, every later build finds them and leaves. The first Map carries
    /// the old test bed's nine platforms so the game stays playable before the owner has drawn
    /// anything; ⚠ they sit in the bottom fifth of a 96-unit world and are scaffolding, not §15.
    /// </para>
    /// </remarks>
    public static class MapAssets
    {
        public const string Folder = "Assets/Map";
        public const string MapPrefab = Folder + "/Map.prefab";
        public const string NormalPrefab = Folder + "/Platform_Normal.prefab";
        public const string SlipperyPrefab = Folder + "/Platform_Slippery.prefab";
        public const string DisappearingPrefab = Folder + "/Platform_Disappearing.prefab";

        /// <summary>The name of the child that draws a platform.</summary>
        public const string VisualName = "Visual";

        /// <summary>The Map child that draws the background (D-082).</summary>
        public const string BackgroundName = "Background";

        /// <summary>The Start Area's name. §4: spans the entire width, physically Normal.</summary>
        public const string StartAreaName = "StartArea";

        /// <summary>Where the world's floor sits. Keeps the Start Area and Smiley where the test bed had them.</summary>
        public const float FloorY = -4.9f;

        private static readonly Color NormalTint = new Color(0.42f, 0.46f, 0.54f);
        private static readonly Color SlipperyTint = new Color(0.45f, 0.68f, 0.82f);
        private static readonly Color DisappearingTint = new Color(0.80f, 0.56f, 0.42f);

        /// <summary>Writes whichever of the four prefabs is missing. Returns false if one could not be written.</summary>
        public static bool Ensure(SmileyTuning tuning, Sprite square, PhysicsMaterial2D material)
        {
            Directory.CreateDirectory(Folder);

            bool ok = true;

            ok &= EnsurePalette(NormalPrefab, tuning, square, material, NormalTint, PlatformKind.Normal);
            ok &= EnsurePalette(SlipperyPrefab, tuning, square, material, SlipperyTint, PlatformKind.Slippery);
            ok &= EnsurePalette(DisappearingPrefab, tuning, square, material, DisappearingTint, PlatformKind.Disappearing);

            // ⚠ EnsurePalette leaves an existing prefab alone, which is right — the owner's map
            // points at it by GUID. So D-130 is applied to the Cloud prefab separately, here,
            // every pass. ⓘ Idempotent: it sets the same values it would create.
            ok &= EnsureCloudIsPassableFromBelow();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            ok &= EnsureMap(square, material);
            ok &= EnsureBackground(square);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return ok;
        }

        /// <summary>
        /// Adds the Background child to the owner's Map if it has none (D-082).
        /// </summary>
        /// <remarks>
        /// ⓘ The one kind of edit a build makes to the owner's map: adding a named child a new
        /// feature needs, the way the guide's §8 adds a control to their UXML. Idempotent — a Map
        /// that has one is left alone, whatever picture the owner has put on it.
        /// </remarks>
        public static bool EnsureBackground(Sprite square)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MapPrefab);

            if (prefab == null || prefab.GetComponentInChildren<MapBackground>() != null)
            {
                return prefab != null;
            }

            using (var scope = new PrefabUtility.EditPrefabContentsScope(MapPrefab))
            {
                BuildBackground(scope.prefabContentsRoot, square);
            }

            Debug.Log($"[MAP] Added '{BackgroundName}' to {MapPrefab}.");
            return true;
        }

        /// <summary>A picture behind the world, sized to it by <see cref="MapBackground"/>.</summary>
        private static void BuildBackground(GameObject map, Sprite square)
        {
            var go = new GameObject(BackgroundName);
            go.transform.SetParent(map.transform, worldPositionStays: false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = square;

            // ⚠ Placeholder: the camera's clear colour, so nothing changes on screen until the
            // owner puts a real picture here. §32 reserves the visual direction to them.
            renderer.color = new Color(0.16f, 0.18f, 0.24f);

            var background = go.AddComponent<MapBackground>();
            background.Set(map.GetComponentInChildren<LevelBounds>(), MapBackground.Fit.Stretch);
        }

        /// <summary>
        /// Resizes the owner's Map to the owner's ruling: LevelBounds to <see cref="MapRule.WorldSize"/>
        /// and the Start Area to the full width. Everything else in the map is left where it is.
        /// </summary>
        /// <remarks>
        /// ⭐ Run once when the ruling changes (D-081: 3240 × 23040 → 6480 × 46080). Not part of a
        /// build — a build <i>checks</i> the size and fails, because a silently resized world is
        /// how a ruling gets lost. ⓘ <c>-executeMethod Smiley.Editor.Setup.MapAssets.ApplyOwnerSizeFromCommandLine</c>.
        /// </remarks>
        public static void ApplyOwnerSize()
        {
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>("Assets/Config/NormalPlatform.physicsMaterial2D");
            Rect world = MapRule.World(FloorY);

            using (var scope = new PrefabUtility.EditPrefabContentsScope(MapPrefab))
            {
                GameObject map = scope.prefabContentsRoot;
                var bounds = map.GetComponentInChildren<LevelBounds>();
                bounds.Set(world.min, world.max, material);

                Transform start = map.transform.Find(StartAreaName);
                var shape = start != null ? start.GetComponent<PlatformShape>() : null;

                if (shape != null)
                {
                    shape.Set(world.width, shape.Height, shape.Visual);
                    start.position = new Vector3(0f, start.position.y, 0f);
                }

                var background = map.GetComponentInChildren<MapBackground>();
                if (background != null)
                {
                    background.Apply();
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[MAP] {MapPrefab} resized to {world.width} x {world.height} units (Q-030).");
        }

        /// <summary>
        /// Folds any edits made to the Map instance in the Game scene back into the prefab.
        /// </summary>
        /// <remarks>
        /// ⭐ D-087. The owner's first big map was drawn partly in Prefab mode and partly on the
        /// scene's instance — sixteen platforms moved and the background assigned as instance
        /// overrides. Those are just as much the map as the prefab is, but a scene rebuild throws
        /// instance overrides away and the guard never sees them. So every build applies them to
        /// the prefab first: what is in the scene becomes what is in the map, and nothing the
        /// owner did in either place is lost. ⓘ Idempotent — a scene with no overrides is left
        /// untouched. ⚠ Runs BEFORE the scene is captured or regenerated.
        /// </remarks>
        public static bool ApplySceneOverrides(string scenePath)
        {
            if (!File.Exists(scenePath))
            {
                return true;
            }

            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

            var bounds = Object.FindFirstObjectByType<LevelBounds>();
            GameObject instance = bounds != null ? PrefabUtility.GetOutermostPrefabInstanceRoot(bounds.gameObject) : null;

            if (instance == null)
            {
                return true;
            }

            if (!PrefabUtility.HasPrefabInstanceAnyOverrides(instance, false))
            {
                return true;
            }

            int before = PrefabUtility.GetObjectOverrides(instance).Count
                         + PrefabUtility.GetAddedGameObjects(instance).Count
                         + PrefabUtility.GetAddedComponents(instance).Count;

            PrefabUtility.ApplyPrefabInstance(instance, InteractionMode.AutomatedAction);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Debug.Log($"[MAP] Applied {before} edit(s) from the Map instance in {scenePath} to {MapPrefab}.");
            return true;
        }

        /// <summary>Deletes one child of the Map by name. Owner instruction, by the owner's name for it.</summary>
        public static bool DeleteChild(string name)
        {
            bool found = false;

            using (var scope = new PrefabUtility.EditPrefabContentsScope(MapPrefab))
            {
                Transform child = scope.prefabContentsRoot.transform.Find(name);

                if (child != null)
                {
                    Object.DestroyImmediate(child.gameObject);
                    found = true;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log(found ? $"[MAP] Deleted '{name}' from {MapPrefab}." : $"[MAP] No child named '{name}' in {MapPrefab}.");
            return found;
        }

        /// <summary>
        /// Makes the Start Area this tall, its bottom on the world's floor. Owner instruction (D-103).
        /// </summary>
        public static void SetStartAreaHeight(float height)
        {
            using (var scope = new PrefabUtility.EditPrefabContentsScope(MapPrefab))
            {
                Transform start = scope.prefabContentsRoot.transform.Find(StartAreaName);
                var shape = start != null ? start.GetComponent<PlatformShape>() : null;

                if (shape == null)
                {
                    Debug.LogError("[MAP] No StartArea with a PlatformShape.");
                    return;
                }

                shape.Set(shape.Width, height, shape.Visual);
                start.position = new Vector3(start.position.x, FloorY + height * 0.5f, 0f);
                Debug.Log($"[MAP] StartArea is {height} tall; its top is at y = {FloorY + height}.");
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>-executeMethod … MapAssets.SetStartAreaHeightFromCommandLine -startHeight 2.9</summary>
        public static void SetStartAreaHeightFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int at = System.Array.IndexOf(args, "-startHeight");
            bool ok = at >= 0 && at + 1 < args.Length
                      && float.TryParse(args[at + 1], System.Globalization.NumberStyles.Float,
                          System.Globalization.CultureInfo.InvariantCulture, out float height);

            if (ok)
            {
                SetStartAreaHeight(float.Parse(args[at + 1], System.Globalization.CultureInfo.InvariantCulture));
            }

            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>-executeMethod … MapAssets.DeleteChildFromCommandLine -mapChild "name"</summary>
        public static void DeleteChildFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int at = System.Array.IndexOf(args, "-mapChild");
            bool ok = at >= 0 && at + 1 < args.Length && DeleteChild(args[at + 1]);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static void ApplyOwnerSizeFromCommandLine()
        {
            ApplyOwnerSize();
            EditorApplication.Exit(0);
        }

        private enum PlatformKind { Normal, Slippery, Disappearing }

        /// <summary>
        /// Makes a Cloud platform passable from below, and nowhere else (D-130).
        /// </summary>
        /// <remarks>
        /// ⚠ <b>Idempotent, and it has to be.</b> The palette prefab already exists and is
        /// referenced by 129 instances in the owner's map, so it can never be deleted and
        /// regenerated — the GUID is load-bearing. This edits in place and is safe to run again.
        /// </remarks>
        public static void ApplyCloudPassThrough(GameObject root)
        {
            var effector = root.GetComponent<PlatformEffector2D>();

            if (effector == null)
            {
                effector = root.AddComponent<PlatformEffector2D>();
            }

            effector.useOneWay = true;
            effector.surfaceArc = PassThroughRule.CloudSurfaceArcDegrees;
            effector.rotationalOffset = 0f;
            effector.useSideFriction = true;
            effector.useSideBounce = true;

            // ⛔ Without this the effector is inert and nothing changes — the collider has to opt
            // in, and a silently inert effector looks exactly like a feature that was never built.
            var collider = root.GetComponent<BoxCollider2D>();

            if (collider != null)
            {
                collider.usedByEffector = true;
            }
        }

        private static bool EnsurePalette(
            string path, SmileyTuning tuning, Sprite square, PhysicsMaterial2D material,
            Color tint, PlatformKind kind)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                return true;
            }

            GameObject root = BuildPlatform(Path.GetFileNameWithoutExtension(path), tuning, square, material, tint, kind);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            Object.DestroyImmediate(root);

            if (!success || saved == null)
            {
                Debug.LogError($"[MAP] Could not write {path}.");
                return false;
            }

            Debug.Log($"[MAP] Wrote {path}.");
            return true;
        }

        /// <summary>A platform of one §14 type: gameplay on the root, the picture on a child.</summary>
        private static GameObject BuildPlatform(
            string name, SmileyTuning tuning, Sprite square, PhysicsMaterial2D material,
            Color tint, PlatformKind kind)
        {
            var root = new GameObject(name);

            var collider = root.AddComponent<BoxCollider2D>();
            collider.sharedMaterial = material;

            root.AddComponent<Platform>();

            var visualObject = new GameObject(VisualName);
            visualObject.transform.SetParent(root.transform, worldPositionStays: false);

            var visual = visualObject.AddComponent<SpriteRenderer>();
            visual.sprite = square;
            visual.color = tint;
            visual.drawMode = SpriteDrawMode.Sliced;

            var shape = root.AddComponent<PlatformShape>();
            shape.Set(3f, 0.4f, visual);

            switch (kind)
            {
                case PlatformKind.Slippery:
                    var slippery = root.AddComponent<SlipperyPlatform>();
                    SetPrivateField(slippery, "_tuning", tuning);
                    break;

                case PlatformKind.Disappearing:
                    var vanishing = root.AddComponent<DisappearingPlatform>();
                    SetPrivateField(vanishing, "_tuning", tuning);
                    SetPrivateField(vanishing, "_renderer", visual);

                    // ⛔ D-130 amends §10 for this type alone: the Cloud is passable from below.
                    ApplyCloudPassThrough(root);
                    break;
            }

            return root;
        }

        /// <summary>
        /// The first Map: the old test bed inside the owner's 13.5 × 96 world.
        /// </summary>
        /// <summary>Applies D-130 to the Cloud palette prefab that already exists on disk.</summary>
        private static bool EnsureCloudIsPassableFromBelow()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DisappearingPrefab);

            if (prefab == null)
            {
                Debug.LogError($"[MAP] {DisappearingPrefab} is missing; cannot apply D-130.");
                return false;
            }

            using (var scope = new PrefabUtility.EditPrefabContentsScope(DisappearingPrefab))
            {
                ApplyCloudPassThrough(scope.prefabContentsRoot);
            }

            Debug.Log($"[MAP] Cloud platform is passable from below (D-130, arc " +
                      $"{PassThroughRule.CloudSurfaceArcDegrees}°).");
            return true;
        }

        private static bool EnsureMap(Sprite square, PhysicsMaterial2D material)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(MapPrefab) != null)
            {
                return true;
            }

            var normal = AssetDatabase.LoadAssetAtPath<GameObject>(NormalPrefab);
            var slippery = AssetDatabase.LoadAssetAtPath<GameObject>(SlipperyPrefab);
            var vanishing = AssetDatabase.LoadAssetAtPath<GameObject>(DisappearingPrefab);

            if (normal == null || slippery == null || vanishing == null)
            {
                Debug.LogError("[MAP] The palette prefabs are missing, so the Map cannot be written.");
                return false;
            }

            var root = new GameObject("Map");

            Rect world = MapRule.World(FloorY);
            var bounds = root.AddComponent<LevelBounds>();
            bounds.Set(world.min, world.max, material);

            // §4: the Start Area spans the entire width and is physically a Normal Platform.
            Place(root, normal, StartAreaName, new Vector2(0f, -4.4f), world.width, 0.8f);

            // ⚠ The test bed's climb (D-049 widths), unchanged so every scene test that names a
            // platform or drops Smiley at a coordinate still means what it meant. Scaffolding:
            // §15's real layout is the owner's to draw over this.
            Place(root, normal, "Platform_01", new Vector2(-1.15f, -1.40f), 3.2f, 0.4f);
            Place(root, slippery, "Platform_02_Slippery", new Vector2(1.20f, 1.10f), 3.0f, 0.4f);
            Place(root, normal, "Platform_03", new Vector2(-1.00f, 3.40f), 3.0f, 0.4f);
            Place(root, normal, "Platform_04", new Vector2(1.60f, 5.80f), 3.0f, 0.4f);
            Place(root, vanishing, "Platform_05_Disappearing", new Vector2(-2.20f, 8.10f), 3.2f, 0.4f);
            Place(root, normal, "Platform_06", new Vector2(0.40f, 10.40f), 3.0f, 0.4f);
            Place(root, normal, "Platform_07", new Vector2(2.80f, 12.60f), 3.0f, 0.4f);
            Place(root, normal, "Platform_08", new Vector2(-1.10f, 14.90f), 3.2f, 0.4f);
            Place(root, normal, "Platform_09", new Vector2(1.40f, 17.30f), 3.2f, 0.4f);

            BuildCheckpoint(root, square, new Vector2(1.60f, 18.90f));
            BuildBackground(root, square);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, MapPrefab, out bool success);
            Object.DestroyImmediate(root);

            if (!success || saved == null)
            {
                Debug.LogError($"[MAP] Could not write {MapPrefab}.");
                return false;
            }

            Debug.Log($"[MAP] Wrote {MapPrefab} — {world.width} x {world.height} units (Q-030).");
            return true;
        }

        /// <summary>A palette prefab instance inside the map, so an edit to the palette reaches every copy.</summary>
        private static void Place(
            GameObject map, GameObject palette, string name, Vector2 position, float width, float height)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(palette);
            instance.name = name;
            instance.transform.SetParent(map.transform, worldPositionStays: false);
            instance.transform.position = position;

            var shape = instance.GetComponent<PlatformShape>();
            shape.Set(width, height, shape.Visual);
        }

        private static void BuildCheckpoint(GameObject map, Sprite sprite, Vector2 position)
        {
            var go = new GameObject("TopCheckpoint");
            go.transform.SetParent(map.transform, worldPositionStays: false);
            go.transform.position = position;
            go.transform.localScale = new Vector3(1.6f, 0.6f, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;

            // ⚠ Placeholder presentation — §32 reserves the visual direction to the owner. It is
            // tinted only so that a human testing on the phone can see where the goal is.
            renderer.color = new Color(0.98f, 0.85f, 0.30f, 0.85f);
            renderer.sortingOrder = -1;

            // ⛔ A TRIGGER, NOT A SOLID (Q-044). A solid checkpoint would be a fourth kind of
            // platform, and §14 enumerates three; §16 fixes the world's contents as Smiley and
            // Platforms. A trigger adds no physics at all.
            var collider = go.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;

            go.AddComponent<TopCheckpoint>();
        }

        private static void SetPrivateField(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);

            if (property == null)
            {
                Debug.LogError($"[MAP] {target.GetType().Name} has no field '{field}'.");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
