using System.Collections;
using NUnit.Framework;
using Smiley.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;

namespace Smiley.Tests
{
    /// <summary>
    /// Rolling out of a collision (D-034) and the drag stretch (D-035).
    /// </summary>
    /// <remarks>
    /// ⭐ Both are owner instructions of 2026-09-10: <i>"increase the player's ability to roll
    /// after a collision… when the player is being dragged, the visual image should deform in
    /// the direction of the drag. Only the visual image should deform, not the collider or
    /// physics body, and the deformation should be limited to a certain maximum amount."</i>
    /// </remarks>
    public sealed class RollAndDragSceneTests
    {
        [UnitySetUp]
        public IEnumerator Load()
        {
            yield return GameSceneFixture.LoadFresh();
        }

        [TearDown]
        public void GiveTheInputBackToTheDevice() => PointerReader.ClearOverride();

        private static SmileyController Controller() =>
            Object.FindFirstObjectByType<SmileyController>();

        private static SmileyDeformer Deformer() =>
            Object.FindFirstObjectByType<SmileyDeformer>();

        /// <summary>Drops Smiley onto the wide StartArea and launches him at the given velocity.</summary>
        private IEnumerator LandFrom(Vector2 launch, System.Action<float, float> report)
        {
            SmileyController controller = Controller();
            var body = controller.GetComponent<Rigidbody2D>();
            var contact = controller.GetComponent<PlatformContact>();

            // ⚠ The StartArea is 20 units wide, so nothing measured here is really the edge of
            // a platform — an earlier version of this measurement was reading him falling off.
            // ⚠ A CLEAR COLUMN, found in the live map (D-076) — it used to be x = -4.6, and
            // both halves of that had to be checked.
            //
            // Every platform is 3.0-3.2 units wide since D-049 and the leftmost span reaches
            // x = -3.80, so an arc launched from where Smiley happens to rest now flies into
            // one — three tests began failing the moment the platforms were widened, each
            // measuring a collision it did not know it had. ⛔ And the first correction put him
            // at x = -7, which is OUTSIDE the world: the invisible walls are at ±5.5 (D-025),
            // so he was dropped into the void and never landed at all.
            GameSceneFixture.ClearTheSky();
            Bounds floor = GameObject.Find("StartArea").GetComponent<Collider2D>().bounds;

            // ⚠ At the LEFT of the world, not in a found column: the shallow arc travels about
            // seven units to the right, and launched from the centre of a 13.5-unit world he
            // reached the right wall before he had finished rolling (0.06 units, not 1+).
            Rect world = Object.FindFirstObjectByType<LevelBounds>().World;

            body.WakeUp();
            body.position = new Vector2(world.xMin + 1.2f, floor.max.y + 0.55f);
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;

            for (int frame = 0; frame < 200; frame++)
            {
                yield return new WaitForFixedUpdate();
                if (contact.IsTouchingPlatform && body.linearVelocity.magnitude < 0.1f) break;
            }

            body.WakeUp();
            body.linearVelocity = launch;

            bool landed = false;
            float landedAtX = 0f;
            float fastestSpin = 0f;

            for (int frame = 0; frame < 900; frame++)
            {
                yield return new WaitForFixedUpdate();

                if (!landed && frame > 10 && contact.IsTouchingPlatform)
                {
                    landed = true;
                    landedAtX = body.position.x;
                }

                if (!landed) continue;

                fastestSpin = Mathf.Max(fastestSpin, Mathf.Abs(body.angularVelocity));

                if (controller.IsReady) break;
            }

            Assert.That(landed, Is.True, "he never landed, so nothing was measured");
            report(Mathf.Abs(body.position.x - landedAtX), fastestSpin);
        }

        [UnityTest]
        public IEnumerator ASteepLandingKeepsRollingInsteadOfStoppingDead()
        {
            // ⛔ THE OWNER'S REPORT, AS A TEST. Before D-034 this measured EXACTLY 0.000 units
            // of roll: Smiley arrived with vx = 1.5 and left the landing frame with vx =
            // 0.0000, coming to rest 9 frames later.
            //
            // The cause was §13's airborne rotation freeze still being on when the impact was
            // resolved — collision callbacks arrive after the physics step, so on the landing
            // step he was still "airborne" — and a rotation-frozen body has infinite rotational
            // inertia. Friction had nothing to spin up, so all it could do was brake.
            float rolled = 0f;
            float spin = 0f;

            yield return LandFrom(new Vector2(1.2f, 5.5f), (r, s) => { rolled = r; spin = s; });

            Assert.That(rolled, Is.GreaterThan(0.2f),
                $"a steep landing rolled {rolled:0.000} units — the owner's first report was " +
                "that Smiley comes to a complete stop instead of continuing to roll");

            Assert.That(spin, Is.GreaterThan(20f),
                $"he moved on but barely turned ({spin:0} deg/s) — that is sliding, not rolling");
        }

        [UnityTest]
        public IEnumerator AShallowLandingStillRolls()
        {
            // The case that already worked, kept so a fix aimed at steep landings cannot quietly
            // cost the shallow ones.
            float rolled = 0f;
            float spin = 0f;

            yield return LandFrom(new Vector2(7.8f, 4.5f), (r, s) => { rolled = r; spin = s; });

            Assert.That(rolled, Is.GreaterThan(1f), $"a shallow landing rolled only {rolled:0.000} units");
            Assert.That(spin, Is.GreaterThan(20f), $"he slid rather than rolled ({spin:0} deg/s)");
        }

        [UnityTest]
        public IEnumerator NothingTakesSmileysSpinAwayWhileHeIsAirborne()
        {
            // ⛔ THE RULE THE OWNER ASKED FOR, AS A TEST (D-047): the roll must emerge from the
            // physics, not from anything this game does to the body.
            //
            // §13 says Smiley does not ROTATE in the air. It does not say he arrives with no
            // angular momentum — and two earlier versions took it away, which is what made a
            // landing stop him dead and then made how far he rolled depend on the angle he
            // came in at. Nothing may zero, store, restore or damp his spin in flight.
            SmileyController controller = Controller();
            var body = controller.GetComponent<Rigidbody2D>();
            var rotation = Object.FindFirstObjectByType<SmileyRotation>();

            GameSceneFixture.ClearTheSky();
            Bounds floor = GameObject.Find("StartArea").GetComponent<Collider2D>().bounds;

            body.WakeUp();
            body.position = new Vector2(GameSceneFixture.ClearColumn(floor.max.y, floor.max.y + 8f), floor.max.y + 3f);
            body.linearVelocity = new Vector2(0f, 4f);
            body.rotation = 0f;
            body.angularVelocity = 400f;

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.That(rotation.IsHoldingOrientation, Is.True, "he is not airborne, so nothing is proved");

            float held = body.rotation;

            for (int frame = 0; frame < 20; frame++)
            {
                yield return new WaitForFixedUpdate();

                if (!rotation.IsHoldingOrientation) break;

                // ✅ §13 still holds: he does not turn.
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(held, body.rotation)), Is.LessThan(1f),
                    $"Smiley rotated {Mathf.DeltaAngle(held, body.rotation):0.0} degrees in mid-air");

                // ⛔ AND HIS ANGULAR MOMENTUM SURVIVES IT.
                Assert.That(Mathf.Abs(body.angularVelocity), Is.GreaterThan(350f),
                    $"his spin fell to {body.angularVelocity:0} deg/s in flight — something is " +
                    "taking it away, and the landing will be poorer for it");
            }
        }

        [UnityTest]
        public IEnumerator TheBodyCarriesTheRollingResistanceItIsConfiguredWith()
        {
            // ⭐ Rolling resistance is a property of the BODY (Q-047), not a rule applied to it.
            // ⛔ This is the reachability half: a tuning value nothing assigns is a number in a
            // file, and this project has shipped several (D-018, D-031).
            var body = Controller().GetComponent<Rigidbody2D>();
            var tuning = Resources.FindObjectsOfTypeAll<Smiley.Config.SmileyTuning>()[0];

            yield return null;

            Assert.That(body.angularDamping, Is.EqualTo(tuning.RollingResistance).Within(1e-3f),
                $"the body rolls with {body.angularDamping:0.00} of resistance against the " +
                $"{tuning.RollingResistance:0.00} that was authored");

            Assert.That(tuning.RollingResistance, Is.GreaterThan(0f),
                "with no rolling resistance at all Smiley coasts across every platform and off " +
                "the far side — Box2D models none of its own");
        }

        [UnityTest]
        public IEnumerator HeCanLandOnAPlatformAndComeToRestOnIt()
        {
            // ⛔ THE OWNER'S REPORT, AS A TEST: "the current rolling behavior makes the game too
            // difficult to progress through." Landing on a platform and staying there long
            // enough to aim again is the whole loop (§8), and before D-047 and D-049 a brisk
            // arrival stayed on in ZERO of six landings.
            //
            // ⚠ Six starting rotations, because §11's outline is deliberately lumpy and a single
            // drop measures which bump he happened to land on rather than the rule.
            var platform = GameObject.Find("Platform_01");
            Bounds b = platform.GetComponent<Collider2D>().bounds;

            int stayed = 0;

            for (int i = 0; i < 6; i++)
            {
                yield return LandOn(b, 1.2f, 1.5f, i * 60f, ok => { if (ok) stayed++; });
            }

            Assert.That(stayed, Is.GreaterThanOrEqualTo(5),
                $"he stayed on the platform in {stayed} of 6 gentle landings");
        }

        /// <summary>Drops Smiley onto a platform and reports whether he came to rest on it.</summary>
        private IEnumerator LandOn(
            Bounds platform, float height, float sideways, float startAngle,
            System.Action<bool> report)
        {
            SmileyController controller = Controller();
            var body = controller.GetComponent<Rigidbody2D>();
            var contact = controller.GetComponent<PlatformContact>();

            body.WakeUp();
            body.position = new Vector2(platform.center.x, platform.max.y + height);
            body.linearVelocity = new Vector2(sideways, 0f);
            body.angularVelocity = 0f;

            // ⚠ The ROTATION is reset too. Without it each drop inherits the angle the last one
            // left, he lands on a different part of his outline every time, and the result
            // swings wildly with everything else held constant — which is exactly what the
            // first version of this measurement did, and it reported eight failures in nine
            // that were mostly its own.
            body.rotation = startAngle;

            yield return new WaitForFixedUpdate();

            bool landed = false;

            for (int frame = 0; frame < 400; frame++)
            {
                yield return new WaitForFixedUpdate();

                if (!landed && contact.IsTouchingPlatform)
                {
                    landed = true;
                }

                if (landed && controller.IsReady) break;
                if (body.position.y < platform.max.y - 2f) break;
            }

            report(body.position.x > platform.min.x && body.position.x < platform.max.x
                   && body.position.y > platform.max.y - 0.5f);
        }

        /// <summary>Settles Smiley so a drag may begin (§8).</summary>
        private IEnumerator Settle()
        {
            // ⚠ READY IS NOT THE SAME AS STILL, and a fixture that confused them was flaky.
            //
            // §8's Ready is a speed threshold with no stabilisation delay — deliberately — so it
            // is true while §11's landing squash is still RECOVERING, and while he is still
            // micro-bouncing on his lumpy outline. A baseline collider captured in that state
            // then "moves" during the drag for reasons that have nothing to do with the drag,
            // and a touch-down on the one frame a bounce un-readies him is refused by §8.
            //
            // So this waits for Ready to HOLD, with the physical squash gone, for ten straight
            // fixed steps. ⓘ That is a fixture's need, not the game's: §8 stays exactly as it is.
            SmileyController controller = Controller();
            SmileyDeformer deformer = Deformer();
            int stillFor = 0;

            for (int frame = 0; frame < 600 && stillFor < 10; frame++)
            {
                yield return new WaitForFixedUpdate();

                bool still = controller.IsReady && deformer.PhysicalMagnitude < 0.01f;
                stillFor = still ? stillFor + 1 : 0;
            }

            Assert.That(stillFor, Is.GreaterThanOrEqualTo(10),
                "he never came genuinely to rest, so no drag can be measured against a baseline");
        }

        /// <summary>
        /// Drags from Smiley toward a screen offset, through the real §6 loop.
        /// </summary>
        /// <remarks>
        /// ⛔ Driven with synthetic <see cref="PointerSample"/>s rather than by poking the
        /// deformer, because a stretch that appears only when a test calls it directly is not
        /// evidence that dragging produces one. This is the same Begin → Aim path the finger
        /// takes; <see cref="Update"/> is left running and is handed the same pointer, so
        /// nothing here is fighting the component for the value it is about to read.
        /// </remarks>
        private IEnumerator DragBy(Vector2 screenOffset, int frames)
        {
            SmileyController controller = Controller();
            var body = controller.GetComponent<Rigidbody2D>();

            // ⛔ THE INPUT IS OVERRIDDEN, NOT THE COMPONENT.
            //
            // An earlier version disabled the controller so its own Update could not treat the
            // absent finger as a release and launch Smiley. That broke the moment GameScreens
            // arrived and began re-enabling the controller every frame — a test standing on a
            // component's enabled flag is a test one unrelated feature away from failing.
            //
            // Feeding PointerReader instead leaves the whole real path running: Update reads,
            // Begin and Aim run in their own order, and every other component behaves normally.
            Camera camera = Camera.main;
            Vector2 onSmiley = camera.WorldToScreenPoint(body.position);

            PointerReader.SetOverride(new PointerSample(true, onSmiley, 7));
            yield return null;

            Assert.That(controller.IsDragging, Is.True,
                "the drag never started — §8's Ready gate, or the grab test, refused it");

            for (int frame = 0; frame < frames; frame++)
            {
                PointerReader.SetOverride(new PointerSample(true, onSmiley + screenOffset, 7));
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator DraggingStretchesTheArtworkAndNothingElse()
        {
            // ⭐ The owner's second instruction. Its clauses are asserted separately: the image
            // deforms, and the physics does not.
            SmileyDeformer deformer = Deformer();
            SmileyController controller = Controller();
            var collider = controller.GetComponent<PolygonCollider2D>();
            var body = controller.GetComponent<Rigidbody2D>();

            yield return Settle();
            yield return null;

            Vector2[] restingOutline = (Vector2[])collider.GetPath(0).Clone();
            float restingFriction = collider.sharedMaterial.friction;
            Vector3 restingScale = deformer.VisualScale;
            Vector2 restingPosition = body.position;

            Assert.That(deformer.DragStretch, Is.EqualTo(0f),
                "the artwork is already stretched with nobody dragging");

            // A long drag — past §6's maximum, so this is also the capped case.
            yield return DragBy(new Vector2(0f, Screen.height * 0.6f), 40);

            Assert.That(deformer.DragStretch, Is.GreaterThan(0.01f),
                "a full drag produced no stretch at all");

            // ✅ THE IMAGE MOVED.
            Assert.That(Vector3.Distance(deformer.VisualScale, restingScale), Is.GreaterThan(0.01f),
                $"the artwork is still drawn at {restingScale} — the stretch never reached it");

            // ⛔ AND NOTHING ELSE DID.
            Vector2[] outlineNow = collider.GetPath(0);
            Assert.That(outlineNow.Length, Is.EqualTo(restingOutline.Length));

            for (int i = 0; i < outlineNow.Length; i++)
            {
                Assert.That(Vector2.Distance(outlineNow[i], restingOutline[i]), Is.LessThan(1e-4f),
                    $"vertex {i} of the COLLIDER moved, and only the image should have");
            }

            Assert.That(collider.sharedMaterial.friction, Is.EqualTo(restingFriction).Within(1e-4f),
                "the drag changed the contact friction, so it reached the physics after all");

            Assert.That(Vector2.Distance(body.position, restingPosition), Is.LessThan(0.05f),
                "the drag moved the body — a visual stretch must not push Smiley around (D-020)");
        }

        [UnityTest]
        public IEnumerator TheStretchFollowsTheDirectionOfTheDrag()
        {
            // ⭐ "The visual image should deform IN THE DIRECTION OF THE DRAG." A stretch along
            // the drag and one across it are different shapes and only one was asked for, so the
            // AXIS is read rather than just the amount.
            //
            // ⚠ Read as the angle the deform frame is turned to, because the scale is expressed
            // in the axis's own frame: a vertical drag and a horizontal one produce identical
            // scale numbers, and only the frame's rotation tells them apart. Asserting on the
            // scale alone would pass on a stretch pointing anywhere at all.
            SmileyDeformer deformer = Deformer();
            Transform frame = deformer.transform.Find("DeformFrame");
            Assert.That(frame, Is.Not.Null, "no DeformFrame, so nothing can be squashed");

            yield return Settle();

            // Dragging DOWN the screen: the stretch axis should be vertical.
            yield return DragBy(new Vector2(0f, -Screen.height * 0.3f), 40);

            float vertical = frame.localEulerAngles.z;
            float bodyAngle = deformer.GetComponent<Rigidbody2D>().rotation;

            // The frame is in Smiley's local space, so his own rotation is added back before the
            // axis is compared against the world.
            float verticalWorld = Mathf.DeltaAngle(0f, vertical + bodyAngle);

            Assert.That(Mathf.Abs(Mathf.Abs(verticalWorld) - 90f), Is.LessThan(25f),
                $"a vertical drag put the stretch axis at {verticalWorld:0} degrees from " +
                "horizontal, and 90 is what following the drag means");
        }

        [UnityTest]
        public IEnumerator DraggingSqueezesHimShorterAlongTheDragAndWiderAcrossIt()
        {
            // ⭐ THE OWNER'S RULING, 2026-09-10 (D-053): "he should become shorter along the drag
            // direction and expand wider perpendicular to the drag direction, like a water
            // balloon being squeezed."
            //
            // ⛔ THE AMOUNT OF DEFORMATION CANNOT TELL THE TWO READINGS APART, and two earlier
            // versions of this test were fooled by exactly that: a squeeze and an elongation of
            // the same magnitude differ only in SIGN, and both report the same DragStretch. What
            // separates them is the scale the artwork is actually drawn at — which axis got
            // shorter.
            SmileyDeformer deformer = Deformer();
            var body = Controller().GetComponent<Rigidbody2D>();

            yield return Settle();
            yield return null;

            Vector3 resting = deformer.VisualScale;

            // Straight up the screen, so the drag axis is unambiguous.
            yield return DragBy(new Vector2(0f, Screen.height * 0.4f), 40);

            Assert.That(deformer.DragStretch, Is.GreaterThan(0.01f), "nothing deformed at all");

            Vector3 squeezed = deformer.VisualScale;

            // ⓘ x is ALONG the deformation axis and y is across it — the frame is rotated to
            // the axis, so these are read in the drag's own frame rather than in the world's.
            //
            // ⚠ The SIZE of the squeeze is not asserted here and cannot be. The follow runs on
            // Time.deltaTime, which a headless run barely advances, so the value never reaches
            // the target it would reach in a frame of real play — on the phone this same drag
            // is a plain 28 % compression. What this fixture is for is that the deformation
            // reaches the artwork at all and points the right way; DragStretchRuleTests pins the
            // arithmetic exactly, and it is the SIGN that the owner corrected twice.
            Assert.That(squeezed.x, Is.LessThan(1f),
                $"he is {squeezed.x:0.000} as long along the drag — a squeeze makes him SHORTER " +
                "that way, and an elongation is what the owner corrected");

            Assert.That(squeezed.y, Is.GreaterThan(1f),
                $"he is {squeezed.y:0.000} as wide across the drag — a balloon squeezed one way " +
                "expands the other");

            // The compression really is the drag's, not the idle wobble's: it tracks the amount
            // the drag channel is carrying.
            Assert.That(1f - squeezed.x,
                Is.EqualTo(deformer.DragStretch).Within(_tuningIdleAmplitude()),
                $"the artwork is compressed by {1f - squeezed.x:0.000} while the drag channel " +
                $"carries {deformer.DragStretch:0.000} — they should be the same deformation");

            Assert.That(squeezed.x, Is.LessThan(squeezed.y),
                "the two axes came out the same, so nothing distinguishes the squeeze");

            // ⭐ THE SQUEEZE IS ANCHORED ON THE GROUND, NOT CENTRED (D-066).
            //
            // ⚠ This assertion said the opposite until the owner reported the consequence: a
            // squeeze about his middle shortens him from both ends, so the part of him resting
            // on the platform lifts off it. Anchored at the ground, the compression happens
            // away from the surface — and the artwork's centre therefore moves DOWN, toward
            // the contact, rather than staying put.
            Assert.That(deformer.VisualOffset.y, Is.LessThan(-0.001f),
                $"the artwork shifted by {deformer.VisualOffset} — squashing onto the ground " +
                "moves it toward the ground, and zero means it is anchored at his middle again");

            Assert.That(resting.x, Is.EqualTo(resting.y).Within(0.15f),
                "he was already deformed before the drag, so the comparison above is muddied");
        }

        [UnityTest]
        public IEnumerator DraggingDoesNotLiftSmileyOffTheGround()
        {
            // ⭐ THE OWNER'S REPORT, 2026-09-11 (D-066): "the deformation pivot is always
            // centred, which causes the player's visual to separate from the ground."
            //
            // ⚠ Measured as the DRAWN bottom edge, which is how they saw it — a squeeze about
            // his middle shortens him from both ends, so the part of him touching the platform
            // rises off it while the collider stays exactly where it was.
            SmileyDeformer deformer = Deformer();
            var art = deformer.GetComponentInChildren<SpriteRenderer>();
            Assert.That(art, Is.Not.Null, "there is no artwork to measure");

            // ⚠ Hold one picture. Since D-086 the idle clips swap the sprite as they play, and
            // two of the owner's frames differ in height by more than this test's tolerance —
            // a frame change between the two readings measured the animation, not the drag.
            art.GetComponent<Smiley.View.IdleAnimator>()?.SetClips(new AnimationClip[0]);
            yield return null;

            yield return Settle();
            yield return null;

            float restingBottom = art.bounds.min.y;

            yield return DragBy(new Vector2(0f, Screen.height * 0.4f), 40);

            Assert.That(deformer.DragStretch, Is.GreaterThan(0.01f), "nothing deformed at all");

            float draggedBottom = art.bounds.min.y;

            // ⓘ A tolerance of a hair rather than zero: the body is still settling on a lumpy
            // outline (§11) and the platform is not perfectly still under him.
            //
            // ⚠ THIS ASSERTION DOES NOT DISCRIMINATE IN A HEADLESS RUN, and saying so is worth
            // more than letting it look strong. The drag follow runs on Time.deltaTime, which a
            // batch-mode frame barely advances, so the squeeze reaches a hundredth of what it
            // reaches in real play — and the rise a centred pivot would cause is then smaller
            // than the noise. ⛔ Proved: with the pivot forced back to the middle, this test
            // still passed and only the offset assertion above went red.
            //
            // ⓘ It is kept because it is the owner's own view of the defect — the drawn bottom
            // edge — and on a phone, where deltaTime is real, it does discriminate.
            Assert.That(draggedBottom, Is.LessThanOrEqualTo(restingBottom + 0.02f),
                $"his bottom edge rose from {restingBottom:0.000} to {draggedBottom:0.000} — " +
                "the squeeze is anchored somewhere other than the ground, so he floats");
        }

        [UnityTest]
        public IEnumerator TheStretchIsCappedHoweverFarTheFingerTravels()
        {
            // ⭐ "The deformation should be limited to a certain maximum amount." Pinned as
            // arithmetic in DragStretchRuleTests; this is the same claim about the live object,
            // dragged far past §6's Maximum Drag Distance.
            SmileyDeformer deformer = Deformer();
            float maximum = TuningMaximum();

            yield return Settle();
            yield return DragBy(new Vector2(0f, Screen.height * 4f), 60);

            Assert.That(deformer.DragStretch, Is.LessThanOrEqualTo(maximum + 1e-4f),
                $"stretched to {deformer.DragStretch:0.000} against a stated maximum of {maximum:0.000}");

            Assert.That(deformer.DragStretch, Is.GreaterThan(0f),
                "nothing stretched at all, so the cap above is asserting about nothing");
        }

        /// <summary>§12's idle wobble rides on top of everything, so it is the tolerance.</summary>
        private static float _tuningIdleAmplitude()
        {
            var tunings = Resources.FindObjectsOfTypeAll<Smiley.Config.SmileyTuning>();
            Assert.That(tunings, Is.Not.Empty, "the tuning asset is not loaded");
            return tunings[0].IdleAmplitude + 1e-3f;
        }

        /// <summary>The maximum the scene's own tuning asset states.</summary>
        private static float TuningMaximum()
        {
            var tunings = Resources.FindObjectsOfTypeAll<Smiley.Config.SmileyTuning>();
            Assert.That(tunings, Is.Not.Empty, "the tuning asset is not loaded");
            return tunings[0].DragStretchMaximum;
        }
    }
}
