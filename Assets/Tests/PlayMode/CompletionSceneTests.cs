using System.Collections;
using NUnit.Framework;
using Smiley.Flow;
using Smiley.Gameplay;
using Smiley.Save;
using Smiley.View;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Smiley.Tests
{
    /// <summary>
    /// §19's Top Checkpoint and §20's Story in the running game.
    /// </summary>
    /// <remarks>
    /// ⛔ <c>CompletionSequenceTests</c> pins every clause of the flow and would stay green if
    /// the checkpoint were absent from the scene, unreachable, or wired to nothing — the failure
    /// this project has hit four times (D-018, D-026, D-031, D-033). These read the objects the
    /// game builds.
    /// </remarks>
    public sealed class CompletionSceneTests
    {
        [UnitySetUp]
        public IEnumerator Load()
        {
            yield return GameSceneFixture.LoadFresh();
        }

        [TearDown]
        public void Clear()
        {
            GameSession.Reset();
            SaveFile.DeleteAll();
        }

        private static CompletionFlow Flow()
        {
            var flow = Object.FindFirstObjectByType<CompletionFlow>();
            Assert.That(flow, Is.Not.Null, "no CompletionFlow, so §19 is unreachable");
            return flow;
        }

        [UnityTest]
        public IEnumerator TheSceneHasExactlyOneTopCheckpointAndItIsATrigger()
        {
            // §19: "There is exactly one Top Checkpoint. There are no intermediate checkpoints."
            TopCheckpoint[] checkpoints = Object.FindObjectsByType<TopCheckpoint>(
                FindObjectsSortMode.None);

            Assert.That(checkpoints.Length, Is.EqualTo(1),
                $"the scene has {checkpoints.Length} Top Checkpoints, and §19 allows exactly one");

            // ⛔ Q-044's reading, asserted rather than left to a comment. A SOLID checkpoint
            // would be a fourth kind of platform — §14 lists three, and §16 fixes the world's
            // contents as Smiley and Platforms. It would also be a ledge Smiley lands on, which
            // would look like a working checkpoint he happens to be standing on.
            Assert.That(checkpoints[0].GetComponent<Collider2D>().isTrigger, Is.True,
                "the Top Checkpoint is solid, so it is a platform §14 does not have");

            Assert.That(checkpoints[0].GetComponent<Platform>(), Is.Null,
                "the Top Checkpoint is a Platform, so §8's Ready rule now depends on it");

            yield return null;
        }

        [UnityTest]
        public IEnumerator TouchingTheCheckpointStartsTheStory()
        {
            // The reachability test: Smiley is moved to the checkpoint and the flow must notice.
            CompletionFlow flow = Flow();
            var checkpoint = Object.FindFirstObjectByType<TopCheckpoint>();
            var body = Object.FindFirstObjectByType<SmileyController>().GetComponent<Rigidbody2D>();

            Assert.That(flow.Phase, Is.EqualTo(CompletionPhase.Climbing));

            body.WakeUp();
            body.position = checkpoint.transform.position;
            body.linearVelocity = Vector2.zero;

            for (int frame = 0; frame < 30 && !checkpoint.HasBeenTouched; frame++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(checkpoint.HasBeenTouched, Is.True,
                "Smiley was placed on the Top Checkpoint and it never noticed");

            yield return null;

            Assert.That(flow.Phase, Is.EqualTo(CompletionPhase.Story),
                "the checkpoint fired and the flow did not start — it is wired to nothing");
        }

        [UnityTest]
        public IEnumerator ThePlayerCannotLaunchSmileyDuringTheStory()
        {
            // ⚠ The world keeps running behind the Story (D-008 — there is no pause path in this
            // game's physics at all), but aiming a launch you cannot see is not gameplay.
            CompletionFlow flow = Flow();
            var controller = Object.FindFirstObjectByType<SmileyController>();

            flow.ForceStart();
            yield return null;
            yield return null;

            Assert.That(flow.Phase, Is.EqualTo(CompletionPhase.Story));
            Assert.That(controller.enabled, Is.False,
                "the player can still drag Smiley from behind the Story screen");
        }

        [UnityTest]
        public IEnumerator TheWorldKeepsRunningBehindTheStory()
        {
            // ⛔ D-008 AND §24, ASSERTED. The obvious way to build a full-screen story is to
            // stop time; this game has no pause path anywhere in its physics, and a test that
            // only checked the screen appeared would not notice one being added.
            CompletionFlow flow = Flow();
            var body = Object.FindFirstObjectByType<SmileyController>().GetComponent<Rigidbody2D>();

            // Put him in the air so gravity has something to prove.
            //
            // ⚠ In a CLEAR column: at x = 0 this test once dropped him onto Platform_04 and
            // then reported that the world was frozen. The map is the owner's (D-076), so the
            // column is found, not assumed.
            GameSceneFixture.ClearTheSky();
            body.WakeUp();
            body.position = new Vector2(GameSceneFixture.ClearColumn(-4f, 7f), 6f);
            body.linearVelocity = Vector2.zero;

            flow.ForceStart();
            yield return null;

            float startY = body.position.y;

            for (int frame = 0; frame < 20; frame++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(Time.timeScale, Is.EqualTo(1f).Within(1e-4f),
                "something paused the game, and §23 removes traditional pause");
            Assert.That(body.position.y, Is.LessThan(startY - 0.1f),
                "Smiley stopped falling while the Story played — the world was frozen");
        }

        /// <summary>Presses the story's Next through the real screens until it ends.</summary>
        private static IEnumerator PressNextUntilTheStoryEnds(Smiley.View.GameScreens screens, CompletionFlow flow)
        {
            var document = screens.GetComponent<UIDocument>();

            for (int press = 0; press < 20 && (flow.Phase == CompletionPhase.Story
                                              || flow.Phase == CompletionPhase.Opening); press++)
            {
                var next = document.rootVisualElement.Q<Button>("story-next");
                Assert.That(next, Is.Not.Null, "the layout has no story-next button");

                var action = next.userData as System.Action;
                Assert.That(action, Is.Not.Null, "the Next button carries no action");

                action();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator TheStoryCannotBeTappedAwayByAnythingButNext()
        {
            // §20: "The player cannot skip the Story." The Completed button and the tutorial X
            // both exist as inputs, and neither may serve as a way out.
            CompletionFlow flow = Flow();
            flow.ForceStart();

            for (int frame = 0; frame < 30; frame++)
            {
                flow.Acknowledge();
                flow.CloseTutorial();
                yield return new WaitForFixedUpdate();
            }

            Assert.That(flow.Phase, Is.EqualTo(CompletionPhase.Story),
                "the Story was ended by something other than Next, which §20 forbids");
            Assert.That(flow.Slide, Is.EqualTo(0), "something other than Next moved it on");
        }

        [UnityTest]
        public IEnumerator TheEndingTakesOneNextPerImageAndNoTimeAtAll()
        {
            // ⭐ D-068: two images, a Next on each. ⚠ And NO clock — the earlier video version
            // ended on a timer, and a fixture that waited would now wait forever.
            CompletionFlow flow = Flow();
            var screens = Object.FindFirstObjectByType<Smiley.View.GameScreens>();

            flow.ForceStart();
            yield return null;

            int images = flow.SlideCount;
            Assert.That(images, Is.GreaterThanOrEqualTo(1));

            // Time alone must not move it.
            for (int frame = 0; frame < 120; frame++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(flow.Phase, Is.EqualTo(CompletionPhase.Story), "the story advanced on its own");
            Assert.That(flow.Slide, Is.EqualTo(0));

            yield return PressNextUntilTheStoryEnds(screens, flow);

            Assert.That(flow.Phase, Is.EqualTo(CompletionPhase.Completed),
                "pressing Next through every image did not end the story");
        }

        [UnityTest]
        public IEnumerator FinishingTheStoryRecordsCompletionAndResetsToTheStartArea()
        {
            // D-012: the playable state returns to the Start Area and the FACT of completion
            // outlives it — which is what lets §20's "watch the Story again after subsequent
            // completions" and §22's Continue both be true at once.
            CompletionFlow flow = Flow();
            var screens = Object.FindFirstObjectByType<Smiley.View.GameScreens>();
            SaveService save = Object.FindFirstObjectByType<SaveService>();
            var body = Object.FindFirstObjectByType<SmileyController>().GetComponent<Rigidbody2D>();

            // Somewhere he could only be at the end of a climb.
            body.WakeUp();
            body.position = new Vector2(1.6f, 18.5f);
            yield return new WaitForFixedUpdate();

            flow.ForceStart();
            yield return null;

            yield return PressNextUntilTheStoryEnds(screens, flow);

            Assert.That(flow.Phase, Is.EqualTo(CompletionPhase.Completed), "the Story never ended");

            Assert.That(save.CompletedAtLeastOnce, Is.True,
                "the game was completed and nothing recorded it");

            Assert.That(body.position.y, Is.LessThan(0f),
                $"Smiley is still at y={body.position.y:0.0} — D-012 returns the playable state " +
                "to the Start Area on completion");

            SaveData onDisk = SaveFile.Read();
            Assert.That(onDisk, Is.Not.Null,
                "no save survived completion, so §22 would offer New Game rather than Continue");
            Assert.That(onDisk.CompletedAtLeastOnce, Is.True);
        }

        [UnityTest]
        public IEnumerator TheTutorialAppearsAfterTheOpeningAndTheXStartsTheGame()
        {
            // ⭐ The owner's flow (D-064, amended by D-068), read off the screens the game
            // actually draws: opening images → Next through them → tutorial image → X → play.
            CompletionFlow flow = Flow();
            var screens = Object.FindFirstObjectByType<Smiley.View.GameScreens>();
            var controller = Object.FindFirstObjectByType<SmileyController>();

            Assert.That(flow.ForceOpening(), Is.True, "the opening never started");
            yield return null;

            Assert.That(screens.VisibleScreen, Is.EqualTo("story"),
                $"the opening is showing and the screen is '{screens.VisibleScreen ?? "nothing"}'");

            yield return PressNextUntilTheStoryEnds(screens, flow);

            Assert.That(flow.TutorialIsShowing, Is.True, "the tutorial never appeared");
            yield return null;

            Assert.That(screens.VisibleScreen, Is.EqualTo("tutorial"),
                $"the tutorial is showing and the screen is '{screens.VisibleScreen ?? "nothing"}'");
            Assert.That(controller.enabled, Is.False,
                "Smiley can be dragged from behind the tutorial image");

            // The X.
            screens.CloseTutorial();
            yield return null;

            Assert.That(flow.Phase, Is.EqualTo(CompletionPhase.Climbing), "play did not begin");
            Assert.That(screens.VisibleScreen, Is.Null, "a screen is still covering the game");
            Assert.That(controller.enabled, Is.True, "the player still has no control");
        }

        [UnityTest]
        public IEnumerator TheXButtonInTheLayoutIsTheOneThatClosesTheTutorial()
        {
            // ⛔ The reachability half. The flow can be perfectly correct while the button the
            // owner laid out is wired to nothing — which is exactly the defect the layout
            // handover makes possible, and why UiLayoutGuard exists.
            CompletionFlow flow = Flow();
            var screens = Object.FindFirstObjectByType<Smiley.View.GameScreens>();
            var document = screens.GetComponent<UIDocument>();

            flow.ForceOpening();
            yield return null;
            yield return PressNextUntilTheStoryEnds(screens, flow);

            Assert.That(flow.TutorialIsShowing, Is.True);

            var close = document.rootVisualElement.Q<Button>("tutorial-close");
            Assert.That(close, Is.Not.Null, "the layout has no tutorial-close button");

            var action = close.userData as System.Action;
            Assert.That(action, Is.Not.Null,
                "the X carries no action, so pressing it would do nothing at all");

            action();
            yield return null;

            Assert.That(flow.Phase, Is.EqualTo(CompletionPhase.Climbing),
                "the X was pressed and the game did not begin");
        }

        [UnityTest]
        public IEnumerator TheStoryIsOnScreenWhileItPlays()
        {
            // ⛔ The flow can be perfectly correct and draw nothing. This reads the panel.
            CompletionFlow flow = Flow();
            var screens = Object.FindFirstObjectByType<Smiley.View.GameScreens>();
            Assert.That(screens, Is.Not.Null, "no GameScreens, so nothing is drawn at all");

            Assert.That(screens.VisibleScreen, Is.Null, "a screen is up before anything happened");

            flow.ForceStart();
            yield return null;
            yield return null;

            Assert.That(screens.VisibleScreen, Is.EqualTo("story"),
                "the Story is playing and the screen showing is " +
                $"'{screens.VisibleScreen ?? "nothing"}'");
        }

        [UnityTest]
        public IEnumerator TheGameplayMenuOpensAndResumesWithoutStoppingTheWorld()
        {
            // §23: the menu contains Resume and Main Menu. ⛔ And nothing else — §24 removes
            // Restart Level "under any name or alternative UI presentation".
            var screens = Object.FindFirstObjectByType<Smiley.View.GameScreens>();
            var controller = Object.FindFirstObjectByType<SmileyController>();

            screens.OpenMenu();
            yield return null;

            Assert.That(screens.MenuIsOpen, Is.True);
            Assert.That(controller.enabled, Is.False, "Smiley is still draggable behind the menu");
            Assert.That(Time.timeScale, Is.EqualTo(1f).Within(1e-4f),
                "opening the menu paused the game, and D-008 says it must not");

            screens.CloseMenu();
            yield return null;

            Assert.That(screens.MenuIsOpen, Is.False);
            Assert.That(controller.enabled, Is.True, "Resume did not give control back");
        }

        [UnityTest]
        public IEnumerator TheGameplayMenuOffersNoRestart()
        {
            // ⛔ §24: "No Restart Level button… do not implement Restart Level under any name or
            // alternative UI presentation." Asserted against the panel's actual contents, so a
            // button added later fails this rather than passing a review.
            var screens = Object.FindFirstObjectByType<Smiley.View.GameScreens>();
            var document = screens.GetComponent<UIDocument>();

            screens.OpenMenu();
            yield return null;

            foreach (Button button in document.rootVisualElement.Query<Button>().ToList())
            {
                string text = (button.text ?? string.Empty).ToLowerInvariant();

                Assert.That(text.Contains("restart"), Is.False,
                    $"a button reads '{button.text}', and §24 removes Restart Level entirely");
                Assert.That(text.Contains("retry"), Is.False,
                    $"a button reads '{button.text}' — §24 covers alternative presentations too");
            }
        }

        [UnityTest]
        public IEnumerator EveryScreenInTheGameSitsInsideTheSafeArea()
        {
            // ⭐ D-072, for the Game scene — and since D-074 that includes §23's menu and its
            // corner button, which used to be built in code with their own padding.
            var screens = Object.FindFirstObjectByType<Smiley.View.GameScreens>();
            VisualElement root = screens.GetComponent<UIDocument>().rootVisualElement;
            VisualElement safe = root.Q("safe-area");

            Assert.That(safe, Is.Not.Null, "the story document was never wrapped in the safe area");

            foreach (string name in new[]
            {
                "story-screen", "story-panel", UiKit.Names.StoryText, UiKit.Names.StoryNext,
                "tutorial-screen", "tutorial-image", UiKit.Names.TutorialClose,
                "completed-screen", UiKit.Names.Acknowledge,
                "gameplay-menu", UiKit.Names.MenuButton, UiKit.Names.Resume, UiKit.Names.MainMenu,
            })
            {
                Assert.That(safe.Q(name), Is.Not.Null, $"'{name}' is not inside the safe area");
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator TheOwnersCornerButtonOpensTheMenuAndTheirResumeClosesIt()
        {
            // ⭐ D-074: §23's menu is the owner's layout now, so the thing to prove is that THEIR
            // buttons are wired — not that OpenMenu() works when called from C#. The action is
            // read back from userData, where UiKit.Bind leaves it for exactly this purpose.
            var screens = Object.FindFirstObjectByType<Smiley.View.GameScreens>();
            VisualElement root = screens.GetComponent<UIDocument>().rootVisualElement;

            var corner = root.Q<Button>(UiKit.Names.MenuButton);
            var resume = root.Q<Button>(UiKit.Names.Resume);
            var toMenu = root.Q<Button>(UiKit.Names.MainMenu);
            VisualElement menu = root.Q("gameplay-menu");

            Assert.That(corner?.userData, Is.InstanceOf<System.Action>(), "the corner button is not bound");
            Assert.That(resume?.userData, Is.InstanceOf<System.Action>(), "Resume is not bound");
            Assert.That(toMenu?.userData, Is.InstanceOf<System.Action>(), "Main Menu is not bound");
            Assert.That(UiKit.IsVisible(menu), Is.False, "the menu is up before anyone opened it");

            ((System.Action)corner.userData).Invoke();
            yield return null;

            Assert.That(screens.MenuIsOpen, Is.True);
            Assert.That(UiKit.IsVisible(menu), Is.True, "the owner's menu element did not appear");

            ((System.Action)resume.userData).Invoke();
            yield return null;

            Assert.That(screens.MenuIsOpen, Is.False);
            Assert.That(UiKit.IsVisible(menu), Is.False, "the owner's menu element did not hide");
        }

        [UnityTest]
        public IEnumerator WrappingTwiceDoesNotNest()
        {
            // A second Bind — a scene reload, a re-enable — must find the wrapper and reuse it,
            // not put a wrapper inside the wrapper and inset everything twice.
            var screens = Object.FindFirstObjectByType<Smiley.View.GameScreens>();
            VisualElement root = screens.GetComponent<UIDocument>().rootVisualElement;

            VisualElement first = root.Q("safe-area");
            VisualElement second = UiKit.WrapInSafeArea(root);

            Assert.That(second, Is.SameAs(first));
            Assert.That(root.Query("safe-area").ToList().Count, Is.EqualTo(1));

            yield return null;
        }
    }
}
