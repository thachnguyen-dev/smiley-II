using System.Collections;
using NUnit.Framework;
using Smiley.Gameplay;
using Smiley.Save;
using UnityEngine;
using UnityEngine.TestTools;

namespace Smiley.Tests
{
    /// <summary>
    /// §21's Auto Save and Resume, in the running game.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⛔ <c>SaveFileTests</c> pins the format and would stay green if <see cref="SaveService"/>
    /// were never constructed, never wired, or absent from the scene — the failure this project
    /// has now hit three times (D-018, D-020, D-026). These read the objects the game creates.
    /// </para>
    /// <para>
    /// ⚠ <b>Every test deletes the save BEFORE loading the scene.</b> <see cref="SaveService"/>
    /// restores in <c>Start</c>, so a save left behind by an earlier test would place Smiley
    /// somewhere the next test never put him — and the suite's result would depend on the order
    /// it ran in.
    /// </para>
    /// </remarks>
    public sealed class SaveSceneTests
    {
        [UnitySetUp]
        public IEnumerator LoadTheGameSceneWithNoSave()
        {
            yield return GameSceneFixture.LoadFresh();
        }

        [TearDown]
        public void RemoveWhatTheTestWrote() => SaveFile.DeleteAll();

        private static SaveService Service()
        {
            var service = Object.FindFirstObjectByType<SaveService>();
            Assert.That(service, Is.Not.Null,
                "there is no SaveService in the scene, so §21 is unreachable");
            return service;
        }

        [UnityTest]
        public IEnumerator TheSaveServiceIsInTheSceneAndCanSeeSmiley()
        {
            // The reachability check. A SaveService that exists but was never handed the
            // controller would capture nothing and report success doing it (D-018).
            SaveService service = Service();

            Assert.That(service.SaveNow(), Is.True,
                "SaveNow refused, which it only does when the controller or its body is missing");
            Assert.That(SaveFile.Exists(), Is.True, "SaveNow reported success and wrote no file");

            yield return null;
        }

        [UnityTest]
        public IEnumerator ASaveIsWrittenWhenSmileyComesToRest()
        {
            // §21: saves happen "at important gameplay events", and Ready (§8) is this game's.
            SaveService service = Service();
            var controller = Object.FindFirstObjectByType<SmileyController>();
            var body = controller.GetComponent<Rigidbody2D>();

            int before = service.WriteCount;

            // Throw him, so that becoming Ready is a TRANSITION rather than the state he was
            // already in — the transition is what the rule is about.
            body.WakeUp();
            body.linearVelocity = new Vector2(0f, 6f);

            for (int frame = 0; frame < 30; frame++)
            {
                yield return new WaitForFixedUpdate();
            }

            bool saved = false;
            for (int frame = 0; frame < 600; frame++)
            {
                yield return new WaitForFixedUpdate();
                if (service.WriteCount > before)
                {
                    saved = true;
                    break;
                }
            }

            Assert.That(saved, Is.True, "Smiley came to rest and nothing was saved");
        }

        [UnityTest]
        public IEnumerator NothingIsSavedWhileNothingHappens()
        {
            // ⛔ §21: "Do NOT use a fixed timer-based save system." The honest way to check that
            // is to let a lot of time pass with the world untouched and see no save appear.
            SaveService service = Service();

            // Let him settle first, so the Ready transition is behind us.
            for (int frame = 0; frame < 240; frame++)
            {
                yield return new WaitForFixedUpdate();
            }

            int settled = service.WriteCount;

            for (int frame = 0; frame < 600; frame++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(service.WriteCount, Is.EqualTo(settled),
                $"{service.WriteCount - settled} saves happened over twelve seconds in which " +
                "nothing occurred — that is a timer, which §21 forbids");
        }

        [UnityTest]
        public IEnumerator SmileyComesBackWhereHeWasAndMovingHowHeWas()
        {
            // §21: "Smiley position. Required physics state."
            SaveService service = Service();
            var body = Object.FindFirstObjectByType<SmileyController>().GetComponent<Rigidbody2D>();

            body.WakeUp();
            body.position = new Vector2(1.75f, 4.25f);
            body.linearVelocity = new Vector2(-2.5f, 3.5f);
            body.angularVelocity = 90f;
            yield return new WaitForFixedUpdate();

            SaveData saved = service.Capture();

            // Somewhere else entirely, so a restore that did nothing cannot pass.
            body.position = new Vector2(-3f, -3f);
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            yield return new WaitForFixedUpdate();

            service.Apply(saved);
            yield return new WaitForFixedUpdate();

            // ⚠ The tolerances are a frame of physics, not a numerical epsilon. §21 asks for a
            // resume "as close as practical to the frame at which the game was saved", and under
            // a float solver (D-002) that is the honest standard — gravity has acted once by the
            // time this reads the body back.
            Assert.That(body.position.x, Is.EqualTo(saved.SmileyX).Within(0.1f));
            Assert.That(body.position.y, Is.EqualTo(saved.SmileyY).Within(0.1f));
            Assert.That(body.linearVelocity.x, Is.EqualTo(saved.VelocityX).Within(0.3f));
            Assert.That(body.linearVelocity.y, Is.EqualTo(saved.VelocityY).Within(0.5f));
            Assert.That(body.angularVelocity, Is.EqualTo(saved.AngularVelocity).Within(30f));
        }

        [UnityTest]
        public IEnumerator TheDeformationComesBackToo()
        {
            // §21: "If saving during deformation, save the current deformation state."
            SaveService service = Service();
            var deformer = Object.FindFirstObjectByType<SmileyDeformer>();
            Assert.That(deformer, Is.Not.Null, "no SmileyDeformer, so §11 is unreachable");

            // ⚠ Let him come to rest first. Since D-084 the Start Area's collider top sits a
            // little below its picture, so he drops a fraction at scene start — and a landing
            // squash arriving between the restore and the capture changed the number (0.3956).
            var controller = deformer.GetComponent<SmileyController>();
            var body = deformer.GetComponent<Rigidbody2D>();

            for (int i = 0; i < 300; i++)
            {
                yield return new WaitForFixedUpdate();
                if (i > 30 && controller.IsReady && body.linearVelocity.magnitude < 0.01f
                    && deformer.PhysicalMagnitude < 0.01f) break;
            }

            deformer.RestoreDeformation(0.4f, 1.1f);
            yield return null;

            SaveData saved = service.Capture();
            Assert.That(saved.DeformMagnitude, Is.EqualTo(0.4f).Within(1e-2f),
                "the deformation was not captured at all");

            deformer.RestoreDeformation(0f, 0f);
            yield return null;

            service.Apply(saved);

            Assert.That(deformer.PhysicalMagnitude, Is.EqualTo(0.4f).Within(1e-2f));
            Assert.That(deformer.PhysicalAxisRadians, Is.EqualTo(1.1f).Within(1e-2f));

            yield return null;
        }

        [UnityTest]
        public IEnumerator AVanishedPlatformComesBackVanishedWithNoCollision()
        {
            // §14.3 through §21: a platform that was gone when the app died must still be gone,
            // and must have NO COLLISION, on the frame the game resumes. Waiting for the next
            // phase change would give Smiley a frame of standing on something that is not there.
            SaveService service = Service();
            var platform = Object.FindFirstObjectByType<DisappearingPlatform>();
            Assert.That(platform, Is.Not.Null, "no Disappearing Platform, so §14.3 is unreachable");

            var collider = platform.GetComponent<Collider2D>();
            Assert.That(platform.IsPresent, Is.True, "it started already gone");

            var gone = new SaveData();
            gone.Platforms.Add(new PlatformState
            {
                Name = platform.name,
                Present = false,
                Remaining = 0f,
                RespawnRemaining = 2.5f,
                Running = false,
                ContactHeld = 0f,
            });

            service.Apply(gone);

            Assert.That(platform.IsPresent, Is.False,
                "the platform is solid after restoring a vanished one");
            Assert.That(collider.enabled, Is.False,
                "it restored as gone but its collision is still there — Smiley would land on nothing");

            yield return null;
        }

        [UnityTest]
        public IEnumerator ARestoredCountdownContinuesFromWhereItWas()
        {
            // §14.3: returning "resumes from the remaining time — it does not reset", and §21
            // extends that across an app kill.
            SaveService service = Service();
            var platform = Object.FindFirstObjectByType<DisappearingPlatform>();

            var partway = new SaveData();
            partway.Platforms.Add(new PlatformState
            {
                Name = platform.name,
                Present = true,
                Remaining = 1.4f,
                RespawnRemaining = 0f,
                Running = false,
                ContactHeld = 0f,
            });

            service.Apply(partway);
            yield return new WaitForFixedUpdate();

            Assert.That(platform.Remaining, Is.EqualTo(1.4f).Within(0.05f),
                $"restored to {platform.Remaining:0.00}s rather than the 1.4s that was saved — " +
                "that is a reset, not a resume");
        }

        [UnityTest]
        public IEnumerator TheStableContactAccumulationSurvivesTheRestore()
        {
            // ⛔ THE DEFECT THIS TEST WAS WRITTEN FOR, and it is not visible from the save format.
            //
            // Whether §14.3's countdown is "running or paused" — which §21 names as its own
            // requirement — is not a flag the platform holds. It is derived every step from
            // ContactHeld, the accumulated unbroken contact time. So restoring the running/paused
            // distinction means restoring THAT.
            //
            // Unity reports contacts only after it steps, so on the first FixedUpdate following a
            // restore nothing is touching anything yet — not even a Smiley placed squarely on the
            // platform. Without the settle window in DisappearingPlatform, Advance takes its
            // "contact lost" branch on that very frame and zeroes the restored accumulation: a
            // platform mid-countdown resumes paused, always, whatever was saved.
            SaveService service = Service();
            var platform = Object.FindFirstObjectByType<DisappearingPlatform>();

            var running = new SaveData();
            running.Platforms.Add(new PlatformState
            {
                Name = platform.name,
                Present = true,
                Remaining = 2.0f,
                RespawnRemaining = 0f,
                Running = true,
                ContactHeld = 1.9f,
            });

            service.Apply(running);
            yield return new WaitForFixedUpdate();

            Assert.That(platform.ContactHeld, Is.EqualTo(1.9f).Within(0.05f),
                $"the restored contact accumulation is {platform.ContactHeld:0.00}s, not 1.9s — " +
                "a platform that was mid-countdown resumes as though nobody had ever stood on it");
        }

        [UnityTest]
        public IEnumerator APlatformComingBackIsItselfASaveEvent()
        {
            // §21 saves at important gameplay events, and a Disappearing Platform changing phase
            // is one: it is persistent world state (§3) that no other event would capture.
            //
            // ⛔ THE POINT OF THIS TEST IS REACHABILITY. SaveService.NotifyWorldChanged shipped
            // with its rationale written out and NO CALLER — the shape that has cost this project
            // three separate defects (D-018, D-025, D-026). A save event nothing raises is a
            // paragraph, not a feature.
            SaveService service = Service();
            var platform = Object.FindFirstObjectByType<DisappearingPlatform>();

            // Restored as gone, with almost no respawn delay left. ⓘ The respawn leg needs no
            // contact at all — it runs on time alone — so this reaches the phase change without
            // having to stand Smiley anywhere.
            var nearlyBack = new SaveData();
            nearlyBack.Platforms.Add(new PlatformState
            {
                Name = platform.name,
                Present = false,
                Remaining = 0f,
                RespawnRemaining = 0.1f,
                Running = false,
                ContactHeld = 0f,
            });

            service.Apply(nearlyBack);
            int before = service.WriteCount;

            bool returned = false;
            for (int frame = 0; frame < 60; frame++)
            {
                yield return new WaitForFixedUpdate();
                if (platform.IsPresent)
                {
                    returned = true;
                    break;
                }
            }

            Assert.That(returned, Is.True, "the platform never respawned, so there was no event to save on");
            yield return new WaitForFixedUpdate();

            Assert.That(service.WriteCount, Is.GreaterThan(before),
                "a platform came back and nothing was saved — §21's world-state event is unreachable");
        }

        [UnityTest]
        public IEnumerator ASaveSurvivesTheRoundTripThroughTheRealFile()
        {
            // Everything above works on objects in memory. This is the only one that proves the
            // bytes on disk carry it — the path an app kill actually takes.
            SaveService service = Service();
            var body = Object.FindFirstObjectByType<SmileyController>().GetComponent<Rigidbody2D>();

            body.WakeUp();
            body.position = new Vector2(-1.25f, 2.75f);
            yield return new WaitForFixedUpdate();

            Assert.That(service.SaveNow(), Is.True);

            SaveData fromDisk = SaveFile.Read();
            Assert.That(fromDisk, Is.Not.Null, "the file was written and cannot be read back");
            Assert.That(fromDisk.SmileyX, Is.EqualTo(-1.25f).Within(0.1f));
            Assert.That(fromDisk.SmileyY, Is.EqualTo(2.75f).Within(0.1f));
            Assert.That(fromDisk.Platforms, Is.Not.Empty,
                "no platform states reached the file, so §21's world state is not being saved");
        }

        [UnityTest]
        public IEnumerator ResetGameLeavesNothingToResumeFrom()
        {
            // §25: Reset Game "deletes all gameplay Save / Progress". A backup left behind would
            // make that untrue, so both files go.
            SaveService service = Service();

            Assert.That(service.SaveNow(), Is.True);
            Assert.That(service.SaveNow(), Is.True,
                "a second save, so that there is a backup to delete as well");
            Assert.That(SaveFile.Exists(), Is.True);

            service.BeginNewGame();

            Assert.That(SaveFile.Exists(), Is.False,
                "a save survived Reset Game — the backup counts, since a resurrectable copy is not a reset");
            Assert.That(SaveFile.Read(), Is.Null);

            yield return null;
        }

        [UnityTest]
        public IEnumerator ASavedPositionInsideAPlatformResumesOnTheStartAreaInstead()
        {
            // ⛔ D-104: the owner raised the Start Area and every old save's position was inside
            // the new block. A save from a map that has changed under it must not bury him.
            SaveService service = Service();
            var body = Object.FindFirstObjectByType<SmileyController>().GetComponent<Rigidbody2D>();
            Bounds start = GameObject.Find("StartArea").GetComponent<Collider2D>().bounds;

            var stale = new SaveData { SmileyX = start.center.x, SmileyY = start.center.y - 0.2f };

            service.Apply(stale);
            yield return null;

            Assert.That(body.position.y, Is.GreaterThan(start.max.y), "he was restored inside the Start Area block");
            foreach (Collider2D hit in Physics2D.OverlapPointAll(body.position))
            {
                Assert.That(hit.GetComponent<Platform>(), Is.Null, $"he was restored inside {hit.name}");
            }
        }
    }
}
