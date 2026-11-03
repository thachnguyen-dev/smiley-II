using System.Collections;
using NUnit.Framework;
using Smiley.Gameplay;
using Smiley.View;
using UnityEngine;
using UnityEngine.TestTools;

namespace Smiley.Tests
{
    /// <summary>
    /// Proves the loop is <b>reachable</b>, not merely correct.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⛔ <b>Why this exists at all.</b> <see cref="LaunchSolverTests"/> and
    /// <see cref="ReadyRuleTests"/> pin the rules, and every one of them would stay green if
    /// <see cref="SmileyController"/> were never constructed, never wired to a tuning asset,
    /// or absent from the scene entirely. A component's own tests cannot tell you it runs.
    /// </para>
    /// <para>
    /// These load the scene the game actually opens and read the objects the game actually
    /// creates.
    /// </para>
    /// </remarks>
    public sealed class GameSceneTests
    {
        [UnitySetUp]
        public IEnumerator LoadTheGameScene()
        {
            yield return GameSceneFixture.LoadFresh();
        }

        [UnityTest]
        public IEnumerator TheSceneContainsASmiley()
        {
            var controller = Object.FindFirstObjectByType<SmileyController>();
            Assert.That(controller, Is.Not.Null, "the scene has no SmileyController");

            var body = controller.GetComponent<Rigidbody2D>();
            Assert.That(body, Is.Not.Null, "Smiley has no Rigidbody2D");

            var contact = controller.GetComponent<PlatformContact>();
            Assert.That(contact, Is.Not.Null, "Smiley has no PlatformContact");

            // ⚠ The guide is deliberately NOT a child of Smiley. Its dots are positioned in
            // world space along the predicted arc, and a parent that is squashing and rotating
            // underneath them would drag the whole preview around with the body.
            var guide = Object.FindFirstObjectByType<TrajectoryGuide>();
            Assert.That(guide, Is.Not.Null, "the scene has no TrajectoryGuide");
            Assert.That(guide.transform.IsChildOf(controller.transform), Is.False,
                "the guide is parented to Smiley, so its dots will move when he deforms");

            yield return null;
        }

        [UnityTest]
        public IEnumerator TheControllerIsActuallyWiredAndCanRun()
        {
            // ⛔ THIS IS THE TEST WHOSE ABSENCE COST A SLICE.
            //
            // An earlier version of the fixture above was named "...AWiredSmiley" and checked
            // only that the components existed. They did. What did not exist was the tuning
            // asset reference on any of them — the scene builder wrote fileID 0 — so
            // SmileyController.Update() returned on its first line and the drag-and-launch
            // loop never ran once. Every Play Mode test passed, because none of them asked
            // whether the loop COULD run; they asked whether the parts were present.
            //
            // Presence is not wiring, and wiring is not reachability.
            var controller = Object.FindFirstObjectByType<SmileyController>();
            Assert.That(controller, Is.Not.Null);

            Assert.That(controller.IsConfigured, Is.True,
                "the controller is missing its tuning asset or camera, so Update() does nothing");

            var deformer = Object.FindFirstObjectByType<Smiley.Gameplay.SmileyDeformer>();
            Assert.That(deformer, Is.Not.Null, "the scene has no SmileyDeformer");
            Assert.That(deformer.ColliderPoints, Is.Not.Null.And.Length.GreaterThan(5),
                "the deformer never built a collider path, so its tuning is probably unwired");

            yield return null;
        }

        [UnityTest]
        public IEnumerator TheSceneContainsPlatformsToLandOn()
        {
            // §4 requires a Start Area at the bottom spanning the map's width, and it is
            // physically a Normal Platform.
            var platforms = Object.FindObjectsByType<Platform>(FindObjectsSortMode.None);
            Assert.That(platforms.Length, Is.GreaterThan(0), "the scene has no Platform");

            GameObject startArea = GameObject.Find("StartArea");
            Assert.That(startArea, Is.Not.Null, "the scene has no StartArea");
            Assert.That(startArea.GetComponent<Platform>(), Is.Not.Null,
                "the StartArea is not physically a Platform (§4)");

            yield return null;
        }

        [UnityTest]
        public IEnumerator SmileySettlesOntoAPlatformAndBecomesReady()
        {
            // The whole loop in one assertion: gravity acts, Smiley lands on a platform,
            // the contact is registered, his speed falls under the threshold, and §8's
            // predicate flips. If any link in that chain is missing this fails, and none of
            // the Edit Mode tests would have noticed.
            var controller = Object.FindFirstObjectByType<SmileyController>();
            Assert.That(controller, Is.Not.Null);

            // Generous: this is about whether it happens at all, not how fast.
            for (int frame = 0; frame < 600 && !controller.IsReady; frame++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(controller.IsReady, Is.True,
                "Smiley never became Ready — he did not land, or contact was never registered");
        }

        [UnityTest]
        public IEnumerator TheGuideIsHiddenWhenNobodyIsDragging()
        {
            // §7: the guide is shown *while dragging*. With no finger down not one dot may be
            // on screen — a renderer left enabled would sit there permanently.
            var guide = Object.FindFirstObjectByType<TrajectoryGuide>();
            Assert.That(guide, Is.Not.Null);

            yield return null;

            Assert.That(guide.VisibleDotCount, Is.EqualTo(0),
                "the trajectory guide is drawn with no drag in progress");
        }

        [UnityTest]
        public IEnumerator TheGuideDrawsFadingDots()
        {
            // ⭐ Owner, 2026-09-10: the guide is a sequence of circular dots that fade out
            // along the predicted trajectory.
            var guide = Object.FindFirstObjectByType<TrajectoryGuide>();
            Assert.That(guide, Is.Not.Null);

            yield return null;

            var solution = LaunchSolver.Solve(
                Vector2.zero, new Vector2(0f, -150f), 200f, 10f);
            Assert.That(solution.HasAim, Is.True);

            guide.Show(solution, Vector2.zero, 1f);
            yield return null;

            Assert.That(guide.VisibleDotCount, Is.GreaterThan(3),
                "the guide drew too few dots to read as a trajectory");

            // ⚠ Only the dots actually DRAWN. The guide keeps a fixed pool and lights as many
            // as the arc is long enough to reach, so an unplaced dot still carries its default
            // white — comparing against one of those tests the pool, not the fade.
            var drawn = new System.Collections.Generic.List<SpriteRenderer>();
            foreach (SpriteRenderer dot in guide.GetComponentsInChildren<SpriteRenderer>())
            {
                if (dot.enabled)
                {
                    drawn.Add(dot);
                }
            }

            Assert.That(drawn.Count, Is.GreaterThan(3), "too few dots to read as a trajectory");

            foreach (SpriteRenderer dot in drawn)
            {
                Assert.That(dot.sprite, Is.Not.Null, "a guide dot has no sprite");
            }

            // The fade is the point: the far end of the trail must be dimmer than the near
            // end, or the guide reads as a row of markers rather than a direction.
            Assert.That(drawn[drawn.Count - 1].color.a, Is.LessThan(drawn[0].color.a),
                "the dots do not fade along the trajectory");

            // And they must be discrete. Dots spaced closer than their own size merge into a
            // smear, which is what spacing by time produced and what spacing by arc length
            // exists to prevent.
            float gap = Vector2.Distance(drawn[0].transform.position, drawn[1].transform.position);
            Assert.That(gap, Is.GreaterThan(drawn[0].transform.localScale.x * 0.9f),
                $"dots are {gap} apart and {drawn[0].transform.localScale.x} wide — they overlap");

            guide.Hide();
            yield return null;

            Assert.That(guide.VisibleDotCount, Is.EqualTo(0),
                "§7: the guide must disappear immediately on release");
        }

        [UnityTest]
        public IEnumerator AnImpulseActuallyMovesSmiley()
        {
            // D-002: the controller hands a single impulse to the physics engine and the
            // engine owns everything after. This checks that the body is dynamic and
            // responds — a kinematic or frozen Rigidbody2D would leave every other test
            // here passing while nothing could ever be launched.
            var controller = Object.FindFirstObjectByType<SmileyController>();
            var body = controller.GetComponent<Rigidbody2D>();

            for (int frame = 0; frame < 600 && !controller.IsReady; frame++)
            {
                yield return new WaitForFixedUpdate();
            }

            Vector2 before = body.position;
            body.AddForce(new Vector2(2f, 8f), ForceMode2D.Impulse);

            for (int frame = 0; frame < 10; frame++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(Vector2.Distance(body.position, before), Is.GreaterThan(0.1f),
                "an impulse did not move Smiley");
        }

        [UnityTest]
        public IEnumerator TheGameSceneCarriesNoAdCodeAtAll()
        {
            // ⛔ D-004: banners in menus only, never during gameplay. Asserted on the live scene.
            Assert.That(Object.FindFirstObjectByType<Smiley.Ads.MenuBanner>(), Is.Null, "a banner component is in the Game scene");
            yield return null;
        }
    }
}
