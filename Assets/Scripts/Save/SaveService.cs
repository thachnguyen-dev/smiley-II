using Smiley.Flow;
using Smiley.Gameplay;
using UnityEngine;

namespace Smiley.Save
{
    /// <summary>
    /// §21's Auto Save: captures the world at gameplay events, and restores it on resume.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⛔ <b>NOT ON A TIMER.</b> §21 says so explicitly: <i>"Do NOT use a fixed timer-based save
    /// system."</i> Nothing here counts seconds.
    /// </para>
    /// <para>
    /// <b>What counts as an "important gameplay event" here</b>, since §21 gives the principle
    /// and names only one of them:
    /// <list type="bullet">
    /// <item><b>Smiley comes to rest and becomes Ready (§8).</b> The natural checkpoint in this
    /// game — it is the moment the world is stable and the player is deciding what to do next,
    /// and it happens exactly once per launch rather than continuously.</item>
    /// <item><b>A Disappearing Platform vanishes or returns.</b> A change to persistent world
    /// state (§3) that no other event would capture.</item>
    /// <item><b>The app is backgrounded.</b> §21 requires resuming from the most recent Auto
    /// Save after a <i>"Crash / App Kill"</i>, and on Android a kill almost always follows a
    /// backgrounding — so this is the event that makes that promise true.</item>
    /// <item><b>Returning to the Main Menu</b> — §21 names this one, and §23 repeats it. The
    /// menu is not built yet, so <see cref="SaveNow"/> is the seam it will call.</item>
    /// </list>
    /// </para>
    /// <para>
    /// ⚠ <b>Saving on Ready rather than on landing is deliberate.</b> Landing fires on every
    /// bounce of a single flight; Ready fires once, when it is over.
    /// </para>
    /// </remarks>
    public sealed class SaveService : MonoBehaviour
    {
        [SerializeField] private SmileyController _controller;
        [SerializeField] private SmileyDeformer _deformer;

        private Rigidbody2D _body;
        private bool _wasReady;
        private bool _restored;

        /// <summary>Where the scene puts Smiley before any save is applied.</summary>
        /// <remarks>
        /// ⚠ Read in <c>Awake</c>, which is the only moment it is knowable: <c>Start</c> is
        /// where a save is restored, so by then the body is wherever the save said. D-012 needs
        /// this — completing the game returns the playable state to the Start Area, and this is
        /// the Start Area as the scene itself authored it rather than a number copied here.
        /// </remarks>
        private Vector2 _spawn;

        /// <summary>How many times this session has written a save. For tests.</summary>
        public int WriteCount { get; private set; }

        /// <summary>Whether a save was found and applied at startup. For tests.</summary>
        public bool RestoredOnStart { get; private set; }

        private void Awake()
        {
            if (_controller != null)
            {
                _body = _controller.GetComponent<Rigidbody2D>();
            }

            if (_body != null)
            {
                _spawn = _body.position;
            }
        }

        private void Start()
        {
            // §22 chooses, and this is where its choice lands.
            //
            // ⛔ NEW GAME DELETES THE SAVE BEFORE ANYTHING READS IT. §22 offers New Game only
            // when no save exists, so arriving here in that mode with a file on disk means
            // §25's Reset Game put it there — and a Reset that left the old climb restorable
            // would not be one.
            //
            // ⚠ The default is Continue (GameSession), because the Game scene is opened
            // directly by the editor and by every Play Mode test with no menu having run.
            if (GameSession.Requested == GameEntryMode.NewGame)
            {
                BeginNewGame();
                RestoredOnStart = false;
            }
            else
            {
                // §21: "If the game closes unexpectedly, resume from the most recent Auto Save."
                RestoredOnStart = RestoreNow();
            }

            _restored = true;
        }

        private void FixedUpdate()
        {
            if (_controller == null || !_restored)
            {
                return;
            }

            bool ready = _controller.IsReady;

            if (ready && !_wasReady)
            {
                SaveNow();
            }

            _wasReady = ready;
        }

        /// <summary>
        /// Called by a platform whose persistent state changed (§3, §14.3).
        /// </summary>
        public void NotifyWorldChanged() => SaveNow();

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                SaveNow();
            }
        }

        private void OnApplicationQuit() => SaveNow();

        /// <summary>Captures and writes the world now. The seam §23's Main Menu will call.</summary>
        public bool SaveNow()
        {
            if (_controller == null || _body == null)
            {
                return false;
            }

            SaveData data = Capture();

            if (!SaveFile.Write(data))
            {
                return false;
            }

            WriteCount++;
            return true;
        }

        /// <summary>Builds a snapshot of everything §21 requires.</summary>
        public SaveData Capture()
        {
            var data = new SaveData
            {
                SmileyX = _body.position.x,
                SmileyY = _body.position.y,
                VelocityX = _body.linearVelocity.x,
                VelocityY = _body.linearVelocity.y,
                Rotation = _body.rotation,
                AngularVelocity = _body.angularVelocity,
                CompletedAtLeastOnce = CompletedAtLeastOnce,
            };

            if (_deformer != null)
            {
                // §21: "If saving during deformation, save the current deformation state."
                data.DeformMagnitude = _deformer.PhysicalMagnitude;
                data.DeformAxisRadians = _deformer.PhysicalAxisRadians;
            }

            foreach (DisappearingPlatform platform in
                     Object.FindObjectsByType<DisappearingPlatform>(FindObjectsSortMode.None))
            {
                data.Platforms.Add(platform.CaptureState());
            }

            return data;
        }

        /// <summary>
        /// Whether the player has ever finished the climb (D-012).
        /// </summary>
        /// <remarks>
        /// Held here rather than in the scene because it must outlive a playthrough being reset.
        /// ⏸ Nothing sets it yet — §19's Top Checkpoint is not built.
        /// </remarks>
        public bool CompletedAtLeastOnce { get; set; }

        /// <summary>Reads the save and applies it. Returns false when there is nothing to apply.</summary>
        public bool RestoreNow()
        {
            SaveData data = SaveFile.Read();

            if (data == null)
            {
                return false;
            }

            Apply(data);
            return true;
        }

        /// <summary>Applies a snapshot to the live world.</summary>
        public void Apply(SaveData data)
        {
            if (_body != null)
            {
                var saved = new Vector2(data.SmileyX, data.SmileyY);

                // ⛔ D-104: a save is from the map as it WAS. When the owner raised the Start
                // Area, every existing save's position was inside the new block, and the
                // restore put Smiley in the ground. A position now inside a platform is not
                // resumable; he stands on the Start Area instead, with the rest of the save kept.
                if (IsInsideAPlatform(saved))
                {
                    Debug.Log($"[SAVE] Saved position {saved} is inside a platform now (the map changed); resuming on the Start Area.");
                    saved = _spawn;
                }

                _body.position = saved;
                _body.linearVelocity = new Vector2(data.VelocityX, data.VelocityY);

                // ⚠ Rotation is restored through MoveRotation for the same reason §13's return
                // to upright uses it: the collider is asymmetric, and writing an angle directly
                // can drop a shape into a platform (D-020's family of problem).
                _body.MoveRotation(data.Rotation);
                _body.angularVelocity = data.AngularVelocity;
            }

            if (_deformer != null)
            {
                _deformer.RestoreDeformation(data.DeformMagnitude, data.DeformAxisRadians);
            }

            foreach (DisappearingPlatform platform in
                     Object.FindObjectsByType<DisappearingPlatform>(FindObjectsSortMode.None))
            {
                PlatformState state = data.Platforms.Find(p => p.Name == platform.name);
                if (state != null)
                {
                    platform.RestoreState(state);
                }
            }

            CompletedAtLeastOnce = data.CompletedAtLeastOnce;
        }

        /// <summary>
        /// §25's Reset Game, and §22's New Game.
        /// </summary>
        /// <remarks>
        /// ⚠ Deletes the save but does <b>not</b> touch settings — §25 is explicit that settings
        /// are not reset.
        /// </remarks>
        public void BeginNewGame()
        {
            SaveFile.DeleteAll();
            CompletedAtLeastOnce = false;
        }

        /// <summary>
        /// D-012: the climb is finished, so the playable state goes back to the Start Area and
        /// the fact of completion outlives it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// §20: <i>"After completion, the player can replay the game… start again from the Start
        /// Area… watch the Story again after subsequent completions."</i> So completion is not a
        /// New Game: a save still exists afterwards, which is why §22's menu still offers
        /// <b>Continue</b> — and why the Story can be reached a second time.
        /// </para>
        /// <para>
        /// ⛔ <b>The world is reset, not just Smiley.</b> A save holding a mid-cycle Disappearing
        /// Platform (§14.3) would have the replay begin with a hole in the map where the last
        /// climb happened to leave one. Writing a snapshot with <b>no</b> platform states puts
        /// every platform back to how the scene authored it, because a restore only touches
        /// platforms it has a record for.
        /// </para>
        /// </remarks>
        /// <summary>Whether a point sits inside any solid Platform collider.</summary>
        private static bool IsInsideAPlatform(Vector2 point)
        {
            foreach (Collider2D hit in Physics2D.OverlapPointAll(point))
            {
                if (!hit.isTrigger && hit.GetComponent<Platform>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        public void CompleteAndResetToStart()
        {
            CompletedAtLeastOnce = true;

            var fresh = new SaveData
            {
                SmileyX = _spawn.x,
                SmileyY = _spawn.y,
                CompletedAtLeastOnce = true,
            };

            Apply(fresh);
            SaveFile.Write(fresh);
            WriteCount++;
        }
    }
}
