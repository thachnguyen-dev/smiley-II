using System.Collections.Generic;
using System.IO;
using Smiley.Audio;
using Smiley.Config;
using Smiley.Flow;
using Smiley.Gameplay;
using Smiley.Save;
using Smiley.Device;
using Smiley.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Smiley.Editor.Setup
{
    /// <summary>
    /// Generates the gameplay scene from code.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Built by command rather than clicked together, so the scene is reproducible and its
    /// contents are reviewable as a diff. Re-running replaces it.
    /// </para>
    /// <para>
    /// ⛔ <b>The platforms here are a test bed, not §15's map.</b> §15's layout is
    /// hand-crafted by the designer and is blocked on Q-030; this is the minimum geometry
    /// needed to feel a launch — a Start Area and something to land on. It must never be
    /// promoted into the real map.
    /// </para>
    /// <para>
    /// ⚠ <b>The sprites are flat placeholder shapes and are not art direction.</b> §32 gives
    /// art direction — Smiley's design, platform art, background — to the owner. These exist
    /// so the shapes are visible, and are generated white so nobody mistakes them for a
    /// choice.
    /// </para>
    /// <para>
    /// Invoke with:
    /// <c>Unity.exe -batchmode -quit -nographics -projectPath src
    /// -executeMethod Smiley.Editor.Setup.SceneBuilder.Build</c>
    /// </para>
    /// </remarks>
    public static class SceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";
        private const string TuningPath = "Assets/Config/SmileyTuning.asset";
        private const string PlaceholderDir = "Assets/Art/Placeholder";
        private const string CirclePath = PlaceholderDir + "/PlaceholderCircle.png";
        private const string SquarePath = PlaceholderDir + "/PlaceholderSquare.png";
        private const string PlatformMaterialPath = "Assets/Config/NormalPlatform.physicsMaterial2D";

        /// <summary>Loaded once, after all imports, and reused by every platform.</summary>
        private static PhysicsMaterial2D _platformMaterial;

        /// <summary>
        /// References that came out null. Any at all fails the build.
        /// </summary>
        /// <remarks>
        /// ⛔ <b>Kept by owner instruction, and made fatal rather than merely logged.</b> This
        /// project has already shipped a scene whose gameplay loop was inert because a single
        /// reference serialized as <c>fileID 0</c> — with a green test suite and a build report
        /// that printed the right numbers over the top of it (D-018). A warning nobody reads
        /// is not a guard; a non-zero exit is.
        /// </remarks>
        private static int _wiringFailures;

        /// <summary>
        /// The camera's original half-height, which the map's pixel rulings were made against.
        /// ⛔ Not the camera's size any more — that is derived from <see cref="SmileyTuning.CameraVisibleWidth"/>
        /// (D-091). Kept for the report's "reference viewport" line, and equal to
        /// <see cref="MapRule.ReferenceViewportHeightUnits"/> / 2 by construction.
        /// </summary>
        private const float ReferenceHalfHeight = 5f;

        /// <summary>Builds every scene the game ships, in the order the build settings list them.</summary>
        /// <remarks>
        /// ⚠ The Menu goes first because <see cref="Build"/> writes the build settings and names
        /// the menu scene in them — a game scene built against a menu that does not exist yet
        /// would list a path with no file behind it.
        /// </remarks>
        public static void BuildAll()
        {
            MenuSceneBuilder.Build();
            Build();
        }

        public static void Build()
        {
            // ⛔ ASSETS ARE CREATED FIRST AND LOADED LAST, WITH THE NEW SCENE IN BETWEEN.
            //
            // `EditorSceneManager.NewScene` in Single mode unloads the outgoing scene and
            // takes unreferenced assets with it — which destroys the NATIVE side of an asset
            // loaded beforehand while leaving the managed C# instance alive. Such a reference
            // still returns its field values perfectly well, and still compares == null. So a
            // build report prints the right numbers while every reference it writes into the
            // scene serializes as fileID 0.
            //
            // That is not hypothetical: it is what this builder did, the scene shipped with a
            // null tuning asset, and the loop it configures never ran once.
            _wiringFailures = 0;

            EnsureTuningAsset();
            EnsureSprite(CirclePath, circular: true);
            EnsureSprite(SquarePath, circular: false);
            EnsureMaterial(PlatformMaterialPath);

            if (UiAssets.Ensure() == null)
            {
                EditorApplication.Exit(1);
                return;
            }

            AudioAssets.Ensure();
            UiLayouts.Ensure();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ⭐ The map is the owner's prefab (D-076), written once like the layouts. The
            // references handed in here are used only BEFORE the new scene, and the prefabs
            // reference the assets by GUID, so the caution above does not bite.
            if (!MapAssets.Ensure(
                    AssetDatabase.LoadAssetAtPath<SmileyTuning>(TuningPath),
                    AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath),
                    AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(PlatformMaterialPath)))
            {
                EditorApplication.Exit(1);
                return;
            }

            // ⭐ D-087: edits made on the scene's Map instance become part of the prefab before
            // the scene is thrown away.
            MapAssets.ApplySceneOverrides(ScenePath);

            // ⭐ WHAT THE OWNER ASSIGNED SURVIVES A REGENERATION (D-077, D-088). Story pictures,
            // idle clips — every registered owner-filled field is read from the scene on disk
            // as asset keys and written back after the rebuild.
            OwnerContent owned = CaptureOwnerContent();

            // D-092: every sprite the owner's idle clips use is anchored to its bottom edge.
            // CaptureOwnerContent left the old scene open, so the animator is findable here.
            var idle = Object.FindFirstObjectByType<IdleAnimator>();
            if (idle != null)
            {
                IdleArtFit.FitClips(idle.Clips);
            }

            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Only now, with the scene that will hold them already open.
            SmileyTuning tuning = AssetDatabase.LoadAssetAtPath<SmileyTuning>(TuningPath);
            Sprite circle = AssetDatabase.LoadAssetAtPath<Sprite>(CirclePath);
            Sprite square = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
            _platformMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(PlatformMaterialPath);
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(UiAssets.PanelPath);

            if (tuning == null || circle == null || square == null || _platformMaterial == null ||
                panel == null)
            {
                Debug.LogError("[SCENE] An asset failed to load; aborting rather than " +
                               "writing a scene full of null references.");
                EditorApplication.Exit(1);
                return;
            }

            CheckOwnerAuthoredValues(tuning);

            _platformMaterial.friction = tuning.NormalFriction;
            _platformMaterial.bounciness = 0f;
            EditorUtility.SetDirty(_platformMaterial);

            CreateCamera(tuning);
            CreateGestureGuard();
            CreateSaveService();
            GameObject smiley = CreateSmiley(tuning, circle);
            CreateGuide(smiley, tuning, circle);

            // D-084: colliders sit inside the art by what the art leaves empty, measured here.
            if (!MapArtFit.FitPalette() || !PlaceMap())
            {
                EditorApplication.Exit(1);
                return;
            }

            // ⭐ D-103: he starts standing on the Start Area, wherever its top is — the owner
            // makes the block taller to bring the first platforms within reach, and the spawn
            // follows. (It was a literal -3.2, which stopped being on the ground the moment the
            // block grew.) SaveService reads this position as the spawn on its first frame.
            PlaceSmileyOnTheStartArea(smiley, tuning);

            CreateAudio();
            CreateCompletionFlow();
            CreateScreens(panel);
            RestoreOwnerContent(owned);

            Wire(smiley, tuning);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath) ?? "Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SCENE] Built {ScenePath} with {scene.rootCount} root objects.");
            Report(smiley, tuning);

            int mapFailures = MapGuard.Check();

            if (mapFailures > 0)
            {
                Debug.LogError(
                    $"[SCENE] {mapFailures} problem(s) with {MapAssets.MapPrefab}. The map is " +
                    "the owner's to edit, but a build will not ship one that cannot be played.");

                EditorApplication.Exit(1);
                return;
            }

            int layoutFailures = UiLayoutGuard.Check();

            if (layoutFailures > 0)
            {
                Debug.LogError(
                    $"[SCENE] {layoutFailures} required UI element(s) are missing from the " +
                    "layouts. ⚠ The game finds its controls by name, so each one is a dead " +
                    "button or a blank screen. Rename them back — moving and restyling is free.");

                EditorApplication.Exit(1);
                return;
            }

            if (_wiringFailures > 0)
            {
                Debug.LogError(
                    $"[SCENE] {_wiringFailures} reference(s) failed to wire. The scene on disk is " +
                    "not usable — fix the builder rather than the scene.");
                EditorApplication.Exit(1);
            }
        }

        private static void CreateCamera(SmileyTuning tuning)
        {
            var go = new GameObject("Main Camera", typeof(Camera));
            go.tag = "MainCamera";

            var camera = go.GetComponent<Camera>();
            camera.orthographic = true;
            // D-099: the size is derived from the visible WIDTH at runtime, per phone, by
            // CameraFraming. This is the reference phone's value, so the Editor looks right too.
            camera.orthographicSize = CameraFraming.HalfHeightFor(tuning.CameraVisibleWidth, 1080f / 2400f);

            var framing = go.AddComponent<CameraFraming>();
            SetPrivateField(framing, "_tuning", tuning);
            camera.backgroundColor = new Color(0.16f, 0.18f, 0.22f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.transform.position = new Vector3(0f, 0f, -10f);

            // ⛔ D-097: THERE WAS NO AUDIO LISTENER IN EITHER SCENE, EVER. A camera made by
            // AddComponent<Camera> has none, and without one Unity mixes nothing — every source
            // "plays" (isPlaying is true, which is what the tests checked) into silence. The
            // placeholders were never heard on the phone; the owner's real clips were the first
            // anyone listened for. One per scene, on the camera, where the ear should be.
            go.AddComponent<AudioListener>();

            go.AddComponent<CameraRig>();
        }

        /// <summary>
        /// The extent of the test bed's world.
        /// </summary>
        /// <remarks>
        /// ⏸ <b>Not §15's map, and not Q-030's figures.</b> These bounds exist so the camera has
        /// something to clamp against; the real map's size is an owner decision and is authored
        /// on this component when it arrives. Deliberately taller than the visible window so the
        /// vertical follow has room to actually do something.
        /// </remarks>
        /// <summary>
        /// Stops Android edge gestures from eating the drag (Q-038).
        /// </summary>
        /// <remarks>
        /// Its own object so it survives a scene change and applies once, rather than being
        /// re-applied by whatever happens to load next.
        /// </remarks>
        private static void CreateGestureGuard()
        {
            var go = new GameObject("SystemGestureGuard");
            go.AddComponent<SystemGestureGuard>();
        }

        /// <summary>Section 21 Auto Save. Its own object, like the gesture guard.</summary>
        /// <summary>
        /// Places the owner's Map prefab. The scene holds an instance; the prefab holds the world.
        /// </summary>
        /// <remarks>
        /// ⭐ An instance, not a copy (D-076): the owner edits <c>Assets/Map/Map.prefab</c> and
        /// the scene follows without being regenerated. Everything the rest of the builder
        /// finds by type — <see cref="LevelBounds"/>, <see cref="TopCheckpoint"/> — lives inside it.
        /// </remarks>
        private static bool PlaceMap()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MapAssets.MapPrefab);

            if (prefab == null)
            {
                Debug.LogError($"[SCENE] {MapAssets.MapPrefab} did not load.");
                return false;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "Map";
            instance.transform.position = Vector3.zero;

            return instance.GetComponentInChildren<LevelBounds>() != null
                   && instance.GetComponentInChildren<TopCheckpoint>() != null;
        }

        /// <summary>
        /// Every field in the Game scene the owner fills by hand and a regeneration would empty.
        /// </summary>
        /// <remarks>
        /// ⭐ D-088. The story pictures were preserved first (D-077); the idle clips were not, and
        /// the first scene rebuild after the owner assigned six of them silently emptied the
        /// list — the phone showed the placeholder disc over their new frames. ⛔ Every owner-filled
        /// list on a generated object is registered HERE, and a new one that is not is the D-077
        /// defect again. The rule: if the owner types it into the Inspector of an object this
        /// builder creates, it goes in this table.
        /// </remarks>
        private static readonly (System.Type component, string field)[] OwnerAssignments =
        {
            // ⛔ D-137 removed StorySlides._opening and ._completion. The story is text now and
            // comes from StoryScript, so there is nothing of the owner's in the Inspector to
            // preserve -- and asking for a field that no longer exists made FindProperty return
            // null and this whole builder throw.
            (typeof(IdleAnimator), "_idles"),
        };

        /// <summary>What the owner had assigned, per registered field, as asset keys.</summary>
        private sealed class OwnerContent
        {
            public readonly Dictionary<string, string[]> Keys = new Dictionary<string, string[]>();
        }

        /// <summary>Reads every registered field from the scene on disk, before it is replaced.</summary>
        private static OwnerContent CaptureOwnerContent()
        {
            var content = new OwnerContent();

            if (!File.Exists(ScenePath))
            {
                return content;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            foreach ((System.Type component, string field) in OwnerAssignments)
            {
                var target = Object.FindFirstObjectByType(component) as Component;

                if (target == null)
                {
                    continue;
                }

                string[] keys = ReadAssetKeys(new SerializedObject(target).FindProperty(field));
                content.Keys[component.Name + "." + field] = keys;

                Debug.Log($"[SCENE] Keeping {keys.Length} owner-assigned {component.Name}.{field} across the rebuild.");
            }

            return content;
        }

        /// <summary>"path|name|type" per entry — a key that survives the scene being unloaded.</summary>
        private static string[] ReadAssetKeys(SerializedProperty array)
        {
            if (array == null || !array.isArray)
            {
                return new string[0];
            }

            var keys = new string[array.arraySize];

            for (int i = 0; i < keys.Length; i++)
            {
                Object asset = array.GetArrayElementAtIndex(i).objectReferenceValue;
                keys[i] = asset != null
                    ? AssetDatabase.GetAssetPath(asset) + "|" + asset.name + "|" + asset.GetType().FullName
                    : string.Empty;
            }

            return keys;
        }

        private static void RestoreOwnerContent(OwnerContent content)
        {
            foreach ((System.Type component, string field) in OwnerAssignments)
            {
                if (!content.Keys.TryGetValue(component.Name + "." + field, out string[] keys))
                {
                    continue;
                }

                var target = Object.FindFirstObjectByType(component) as Component;

                if (target == null)
                {
                    Debug.LogError($"[SCENE] The rebuilt scene has no {component.Name} to put the owner's {field} back on.");
                    _wiringFailures++;
                    continue;
                }

                var serialized = new SerializedObject(target);
                SerializedProperty array = serialized.FindProperty(field);

                // ⛔ A registered field that no longer exists on the component. That is a code
                // change outrunning this table, and it threw a bare NullReference from deep in
                // the builder (D-137). Say which field, and carry on with the others.
                if (array == null)
                {
                    Debug.LogError($"[SCENE] {component.Name} has no field \"{field}\" any more — " +
                                   "remove it from OwnerAssignments.");
                    _wiringFailures++;
                    continue;
                }

                WriteAssets(array, keys);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void WriteAssets(SerializedProperty array, string[] keys)
        {
            array.arraySize = keys.Length;

            for (int i = 0; i < keys.Length; i++)
            {
                Object asset = null;
                string[] parts = keys[i].Split('|');

                if (parts.Length == 3 && parts[0].Length > 0)
                {
                    foreach (Object candidate in AssetDatabase.LoadAllAssetsAtPath(parts[0]))
                    {
                        if (candidate != null && candidate.name == parts[1] && candidate.GetType().FullName == parts[2])
                        {
                            asset = candidate;
                            break;
                        }
                    }

                    if (asset == null)
                    {
                        Debug.LogError($"[SCENE] Owner-assigned asset '{keys[i]}' could not be reloaded.");
                        _wiringFailures++;
                    }
                }

                array.GetArrayElementAtIndex(i).objectReferenceValue = asset;
            }
        }

        private static void CreateAudio()
        {
            var go = new GameObject("Audio");
            go.AddComponent<GameAudio>();
        }

        private static void CreateCompletionFlow()
        {
            var go = new GameObject("CompletionFlow");
            go.AddComponent<CompletionFlow>();
        }

        private static void CreateScreens(PanelSettings panel)
        {
            var go = new GameObject("Screens");

            var document = go.AddComponent<UIDocument>();
            document.panelSettings = panel;

            // ⭐ The owner's layout (D-057). ⛔ Loaded, never generated here — UiLayouts writes it
            // once and then leaves it alone forever.
            document.visualTreeAsset =
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiLayouts.StoryUxml);

            go.AddComponent<GameScreens>();

            // D-131: the guided opening sits on the same document, because it draws into the
            // same panel and must hide behind the same covering screens.
            go.AddComponent<Smiley.View.TutorialPrompt>();

            // §20's images live beside the screens that show them (D-068). ⏸ The images
            // themselves are the owner's (Q-050): they assign them on this component in the
            // Inspector, and the lists ship empty.
            var slidesObject = new GameObject("StorySlides");
            slidesObject.transform.SetParent(go.transform, worldPositionStays: false);
            slidesObject.AddComponent<Smiley.Flow.StorySlides>();
        }

        private static void CreateSaveService()
        {
            var go = new GameObject("SaveService");
            go.AddComponent<SaveService>();
        }

        private static void PlaceSmileyOnTheStartArea(GameObject smiley, SmileyTuning tuning)
        {
            GameObject start = GameObject.Find(MapAssets.StartAreaName);
            var collider = start != null ? start.GetComponent<BoxCollider2D>() : null;

            if (collider == null)
            {
                Debug.LogError("[SCENE] No StartArea collider to stand Smiley on.");
                _wiringFailures++;
                return;
            }

            Physics2D.SyncTransforms();
            float top = collider.bounds.max.y;
            smiley.transform.position = new Vector3(0f, top + tuning.BodyRadius + 0.05f, 0f);
            Debug.Log($"[SCENE] Smiley spawns at y = {smiley.transform.position.y:0.00}, on the StartArea top at {top:0.00}.");
        }

        private static GameObject CreateSmiley(SmileyTuning tuning, Sprite sprite)
        {
            var go = new GameObject("Smiley");
            go.transform.position = new Vector3(0f, -3.2f, 0f);

            var body = go.AddComponent<Rigidbody2D>();
            body.mass = tuning.Mass;
            body.linearDamping = tuning.LinearDamping;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            // §11: "Mass remains constant." Auto-mass would recompute it every time the
            // deforming collider rebuilt its shape.
            body.useAutoMass = false;

            // A polygon, not a circle: §11's outline is deliberately not a perfect circle and
            // the owner ruled that the asymmetry reaches the physics. The path is built and
            // rebuilt by SmileyDeformer.
            go.AddComponent<PolygonCollider2D>();

            go.AddComponent<PlatformContact>();
            go.AddComponent<SmileyAudio>();

            // ⚠ Three transforms, and the nesting is load-bearing.
            //   Smiley       - body and collider. Never scaled, so no visual deformation can
            //                  reach the physics (§12: idle deformation is visual only).
            //   DeformFrame  - rotated into the squash axis and scaled along it.
            //   Sprite       - counter-rotated inside it, so the artwork is squashed without
            //                  being turned to face whatever Smiley last hit (§13).
            var frame = new GameObject("DeformFrame");
            frame.transform.SetParent(go.transform, false);

            // ⚠ Four transforms since D-092. 'Sprite' only counter-rotates (the deformer turns it
            // back upright); 'Art' under it holds the renderer at the BOTTOM of the circle, so a
            // bottom-anchored frame's lowest pixel is the collider's lowest point. The offset is
            // below the counter-rotation on purpose: above it, it would swing with the squash axis.
            var holder = new GameObject("Sprite");
            holder.transform.SetParent(frame.transform, false);
            holder.transform.localScale = Vector3.one * (tuning.BodyRadius * 2f);

            var art = new GameObject("Art");
            art.transform.SetParent(holder.transform, false);
            art.transform.localPosition = new Vector3(0f, -0.5f, 0f);

            var renderer = art.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;

            // ⚠ White, not the old yellow tint: the tint was for the placeholder disc, and it
            // multiplied the owner's green idle frames into olive (D-088). Their art draws as
            // drawn; the placeholder is a white disc until the clips replace it.
            renderer.color = Color.white;
            renderer.sortingOrder = 10;

            // ⭐ Owner, 2026-09-12 (D-095): the picture faces the way he is moving — mirrored
            // when moving left. (D-094's static mirror was a misreading of the same request.)
            var facing = art.AddComponent<Facing>();
            SetPrivateField(facing, "_tuning", tuning);
            SetPrivateField(facing, "_body", body);
            SetPrivateField(facing, "_renderer", renderer);

            // D-086: the idle animations play on the Sprite object, inside the deformer's frame.
            // ⏸ The clips are the owner's (Q-053); the list ships empty and he holds still.
            art.AddComponent<Animator>();
            art.AddComponent<IdleAnimator>();

            go.AddComponent<SmileyController>();
            go.AddComponent<SmileyDeformer>();
            go.AddComponent<SmileyRotation>();

            // D-083: the shadow. A flat dark disc under him, on whatever platform is below;
            // no collider, so it is a picture and nothing else. Replace the sprite to restyle.
            var shadow = new GameObject("Shadow");
            shadow.transform.SetParent(go.transform, false);

            var shade = shadow.AddComponent<SpriteRenderer>();
            shade.sprite = sprite;
            shade.color = new Color(0f, 0f, 0f, 0.45f);
            shade.sortingOrder = 5;

            var caster = shadow.AddComponent<SmileyShadow>();
            SetPrivateField(caster, "_tuning", tuning);
            SetPrivateField(caster, "_body", body);
            SetPrivateField(caster, "_renderer", shade);

            return go;
        }

        private static void CreateGuide(GameObject smiley, SmileyTuning tuning, Sprite dot)
        {
            // ⭐ Dots, not a line (owner, 2026-09-10). No LineRenderer and no material: the
            // guide spawns its own SpriteRenderers at runtime from the dot sprite below.
            var go = new GameObject("TrajectoryGuide");

            // ⚠ NOT parented to Smiley. The dots are placed in world space along the predicted
            // arc, and a parent that is squashing and rotating underneath them would drag the
            // whole preview around with the body.
            go.transform.SetParent(smiley.transform.parent, false);

            var guide = go.AddComponent<TrajectoryGuide>();
            SetPrivateField(guide, "_tuning", tuning);
            SetPrivateField(guide, "_dotSprite", dot);
        }

        private static void Wire(GameObject smiley, SmileyTuning tuning)
        {
            var controller = smiley.GetComponent<SmileyController>();
            SetPrivateField(controller, "_tuning", tuning);
            SetPrivateField(controller, "_guide", Object.FindFirstObjectByType<TrajectoryGuide>());
            SetPrivateField(controller, "_camera", Object.FindFirstObjectByType<Camera>());

            var rig = Object.FindFirstObjectByType<CameraRig>();
            SetPrivateField(rig, "_tuning", tuning);
            SetPrivateField(rig, "_target", smiley.transform);
            SetPrivateField(rig, "_bounds", Object.FindFirstObjectByType<LevelBounds>());

            var save = Object.FindFirstObjectByType<SaveService>();
            SetPrivateField(save, "_controller", controller);
            SetPrivateField(save, "_deformer", smiley.GetComponent<SmileyDeformer>());

            var rotation = smiley.GetComponent<SmileyRotation>();
            SetPrivateField(rotation, "_tuning", tuning);

            var deformer = smiley.GetComponent<SmileyDeformer>();
            Transform frame = smiley.transform.Find("DeformFrame");

            SetPrivateField(deformer, "_tuning", tuning);
            SetPrivateField(deformer, "_deformFrame", frame);
            SetPrivateField(deformer, "_sprite", frame != null ? frame.Find("Sprite") : null);
            SetPrivateField(deformer, "_contact", smiley.GetComponent<PlatformContact>());
            SetPrivateField(deformer, "_controller", smiley.GetComponent<SmileyController>());

            var flow = Object.FindFirstObjectByType<CompletionFlow>();
            SetPrivateField(flow, "_checkpoint", Object.FindFirstObjectByType<TopCheckpoint>());
            SetPrivateField(flow, "_save", Object.FindFirstObjectByType<SaveService>());
            SetPrivateField(flow, "_controller", smiley.GetComponent<SmileyController>());

            var audio = Object.FindFirstObjectByType<GameAudio>();
            SetPrivateField(audio, "_tuning", tuning);
            SetPrivateField(audio, "_music",
                AssetDatabase.LoadAssetAtPath<AudioClip>(AudioAssets.MusicPath));
            SetPrivateField(audio, "_impact",
                AssetDatabase.LoadAssetAtPath<AudioClip>(AudioAssets.ImpactPath));
            SetPrivateField(audio, "_launch",
                AssetDatabase.LoadAssetAtPath<AudioClip>(AudioAssets.LaunchPath));
            SetPrivateField(audio, "_guide",
                AssetDatabase.LoadAssetAtPath<AudioClip>(AudioAssets.GuidePath));

            var smileyAudio = smiley.GetComponent<SmileyAudio>();
            SetPrivateField(smileyAudio, "_audio", audio);
            SetPrivateField(smileyAudio, "_controller", smiley.GetComponent<SmileyController>());

            var slides = Object.FindFirstObjectByType<Smiley.Flow.StorySlides>();
            SetPrivateField(flow, "_slides", slides);

            var screens = Object.FindFirstObjectByType<GameScreens>();
            SetPrivateField(screens, "_slides", slides);
            SetPrivateField(screens, "_flow", flow);
            SetPrivateField(screens, "_save", Object.FindFirstObjectByType<SaveService>());
            SetPrivateField(screens, "_controller", smiley.GetComponent<SmileyController>());

            // D-131: the guided opening needs Smiley (to know he is at rest and where he is) and
            // the screens (to know when the instruction image closed, and to hide behind covers).
            var prompt = Object.FindFirstObjectByType<Smiley.View.TutorialPrompt>();
            SetPrivateField(prompt, "_smiley", smiley.GetComponent<SmileyController>());
            SetPrivateField(prompt, "_screens", screens);
            SetPrivateField(prompt, "_camera", Object.FindFirstObjectByType<Camera>());
        }

        /// <summary>
        /// Assigns a serialized private field, the way the inspector would.
        /// </summary>
        /// <remarks>
        /// Uses <see cref="SerializedObject"/> rather than reflection so the value is written
        /// through Unity's own serialization and is actually saved into the scene. Setting the
        /// C# field directly would appear to work and then serialize nothing.
        /// </remarks>
        internal static void SetPrivateField(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);

            if (property == null)
            {
                Debug.LogError($"[SCENE] {target.GetType().Name} has no serialized field '{field}'.");
                return;
            }

            property.objectReferenceValue = value;
            bool applied = so.ApplyModifiedPropertiesWithoutUndo();

            // Read back through a FRESH SerializedObject. Checking the one we just wrote
            // would report our own intention rather than what the component now holds — and
            // an assignment that silently does nothing is exactly the failure this is for.
            var verify = new SerializedObject(target);
            Object stored = verify.FindProperty(field).objectReferenceValue;

            Debug.Log($"[WIRE] {target.GetType().Name}.{field} = " +
                      $"{(stored == null ? "NULL" : stored.name)} " +
                      $"(wanted {(value == null ? "NULL" : value.name)}, applied={applied})");

            if (stored == null)
            {
                _wiringFailures++;
                Debug.LogError(
                    $"[WIRE] {target.GetType().Name}.{field} came out NULL. A component with a " +
                    "missing reference does nothing and says nothing, so the build fails here " +
                    "rather than shipping a scene that looks complete.");
            }
        }

        /// <summary>
        /// Makes sure the tuning asset exists and is serialized against the current script.
        /// </summary>
        /// <remarks>
        /// ⚠ <b>Re-saved even when it already exists.</b> An asset written before a field was
        /// added does not contain that field, so the loaded object silently takes the C#
        /// initialiser instead of anything authored — and the asset on disk keeps looking
        /// complete. Marking it dirty forces the new fields out to disk where they can be
        /// seen and edited.
        /// </remarks>
        private static void EnsureTuningAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<SmileyTuning>(TuningPath);

            if (existing == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(TuningPath) ?? "Assets/Config");
                var created = ScriptableObject.CreateInstance<SmileyTuning>();
                AssetDatabase.CreateAsset(created, TuningPath);
                Debug.Log($"[SCENE] Created {TuningPath}. EVERY VALUE IN IT IS PROVISIONAL.");
                return;
            }

            EditorUtility.SetDirty(existing);
        }

        /// <summary>
        /// Creates the platform surface material if it is missing. Values are applied after
        /// the refresh, never here — see the note at the top of <see cref="Build"/>.
        /// </summary>
        private static void EnsureMaterial(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path) != null)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "Assets/Config");
            var material = new PhysicsMaterial2D(Path.GetFileNameWithoutExtension(path));
            AssetDatabase.CreateAsset(material, path);
        }

        /// <summary>
        /// Generates a flat white placeholder sprite if one is not already on disk.
        /// </summary>
        /// <remarks>
        /// ⚠ Generated white and featureless on purpose. §32 gives the visual direction to
        /// the owner, and a placeholder that looked designed would be the beginning of one.
        /// </remarks>
        private static void EnsureSprite(string path, bool circular)
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(path) != null)
            {
                return;
            }

            Directory.CreateDirectory(PlaceholderDir);

            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float radius = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool inside = !circular ||
                        new Vector2(x - radius + 0.5f, y - radius + 0.5f).magnitude <= radius - 0.5f;
                    texture.SetPixel(x, y, inside ? Color.white : Color.clear);
                }
            }

            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = size;   // one sprite = one world unit
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }


        /// <summary>
        /// Fails the build if an owner-authored value has drifted from its ruling.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Most values in the tuning asset are provisional and mine, and moving one is
        /// ordinary work. A handful are the OWNER'S, and moving one of those is a silent
        /// reversal of a decision.
        /// </para>
        /// <para>
        /// This exists because of a specific near-miss: when Q-008 was ruled at 3 seconds, the
        /// C# initialiser was updated and the ASSET ON DISK kept 2.5. An already-serialized
        /// asset does not take a new initialiser, so the ruling would have been recorded,
        /// documented, committed - and not actually in the game.
        /// </para>
        /// <para>
        /// The duplication below is deliberate: it is a cross-check against the decision log,
        /// not a second source of truth. If the two disagree the build stops rather than
        /// quietly preferring one.
        /// </para>
        /// </remarks>
        private static void CheckOwnerAuthoredValues(SmileyTuning tuning)
        {
            // Q-008, owner 2026-09-10: "The disappearing platform should disappear after
            // 3 seconds of stable contact."
            Expect("DisappearCountdownSeconds (Q-008)", tuning.DisappearCountdownSeconds, 3f);
        }

        private static void Expect(string what, float actual, float authored)
        {
            if (Mathf.Abs(actual - authored) < 1e-4f)
            {
                Debug.Log($"[AUTHORED] {what} = {actual}, as ruled.");
                return;
            }

            _wiringFailures++;
            Debug.LogError(
                $"[AUTHORED] {what} is {actual} but the owner ruled {authored}. " +
                "An owner-authored value has drifted - fix the asset, or get a new ruling. " +
                "The build fails rather than shipping a reversed decision.");
        }

        private static void AddToBuildSettings()
        {
            // ⛔ THE MENU IS FIRST, AND THAT IS §22 RATHER THAN A PREFERENCE. The app opens on
            // the Main Menu, which is where Continue and New Game are chosen; booting straight
            // into the Game scene would skip the only place that choice is made and would
            // silently resume whatever save happened to be on disk.
            var scenes = new[]
            {
                new EditorBuildSettingsScene(MenuSceneBuilder.ScenePath, true),
                new EditorBuildSettingsScene(ScenePath, true),
            };

            EditorBuildSettings.scenes = scenes;
        }

        private static void Report(GameObject smiley, SmileyTuning tuning)
        {
            var body = smiley.GetComponent<Rigidbody2D>();
            Debug.Log($"[SCENE-V] smiley mass         = {body.mass}");
            Debug.Log($"[SCENE-V] linear damping      = {body.linearDamping}");
            Debug.Log($"[SCENE-V] gravity             = {Physics2D.gravity}   (Q-001, engine default)");
            Debug.Log($"[SCENE-V] maxDragFraction     = {tuning.MaxDragScreenFraction}   (Q-006, PROVISIONAL)");
            Debug.Log($"[SCENE-V] maxLaunchForce      = {tuning.MaxLaunchForce}   (Q-007, PROVISIONAL)");
            Debug.Log($"[SCENE-V] stoppingThreshold   = {tuning.StoppingThreshold}   (Q-005, PROVISIONAL)");
            Debug.Log($"[SCENE-V] guideSeconds        = {tuning.GuideSeconds}   (Q-033, PROVISIONAL)");
            Debug.Log($"[SCENE-V] controller wired    = {smiley.GetComponent<SmileyController>() != null}");
            Debug.Log($"[SCENE-V] guide wired         = {Object.FindFirstObjectByType<TrajectoryGuide>() != null}");

            var bounds = Object.FindFirstObjectByType<LevelBounds>();
            if (bounds != null)
            {
                // The reference portrait phone: 1080 x 2400 at orthographic size 5.
                var referenceViewport = new Vector2(2f * ReferenceHalfHeight * (1080f / 2400f), 2f * ReferenceHalfHeight);
                float halfHeight = CameraFraming.HalfHeightFor(tuning.CameraVisibleWidth, 1080f / 2400f);
                Debug.Log($"[SCENE-V] camera viewport    = {tuning.CameraVisibleWidth:0.00} x {2f * halfHeight:0.00} units on the reference phone (D-099, Q-054 PROVISIONAL)");
                Vector2 multiple = bounds.ViewportMultiple(referenceViewport);

                Debug.Log($"[SCENE-V] world              = {bounds.World.width} x {bounds.World.height} units");
                Debug.Log($"[SCENE-V] reference viewport = {referenceViewport.x} x {referenceViewport.y} units (1080x2400)");
                Debug.Log($"[SCENE-V] viewport multiple  = {multiple.x:0.00}x wide, {multiple.y:0.00}x tall   (owner target: 2-3x)");
                Debug.Log($"[SCENE-V] owner map (Q-030)  = {MapRule.OwnerWidthPixels} x {MapRule.OwnerHeightPixels} px = {MapRule.WorldSize.x} x {MapRule.WorldSize.y} units");
                Debug.Log("[SCENE-V] boundaries         = built by LevelBounds at runtime (D-076)");
            }
        }
    }
}
