using System.Collections;
using NUnit.Framework;
using Smiley.Gameplay;
using Smiley.View;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Smiley.Tests
{
    /// <summary>
    /// D-111: the whole screen is the drag surface, and nothing the finger does moves the camera.
    /// </summary>
    /// <remarks>
    /// ⭐ Owner, 2026-09-13: <i>"Allow the player to be dragged from anywhere across the entire
    /// screen. Remove the ability for the player drag interaction to control or move the camera.
    /// Camera movement should remain controlled only by the existing gameplay camera logic."</i>
    /// ⓘ Every fixture here drives <see cref="PointerReader.SetOverride"/> so the real
    /// Update → Begin → Aim → Release path runs (D-036); nothing pokes the controller.
    /// </remarks>
    public sealed class DragAnywhereSceneTests
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

        private IEnumerator Settle()
        {
            SmileyController controller = Controller();
            var deformer = Object.FindFirstObjectByType<SmileyDeformer>();
            int stillFor = 0;

            for (int frame = 0; frame < 600 && stillFor < 10; frame++)
            {
                yield return new WaitForFixedUpdate();
                bool still = controller.IsReady && deformer.PhysicalMagnitude < 0.01f;
                stillFor = still ? stillFor + 1 : 0;
            }

            Assert.That(stillFor, Is.GreaterThanOrEqualTo(10), "he never came to rest");
        }

        /// <summary>A screen point as far from Smiley as this screen allows.</summary>
        private static Vector2 FarFromSmiley()
        {
            Vector2 onSmiley = Camera.main.WorldToScreenPoint(Controller().GetComponent<Rigidbody2D>().position);
            float x = onSmiley.x < Screen.width * 0.5f ? Screen.width * 0.92f : Screen.width * 0.08f;
            float y = onSmiley.y < Screen.height * 0.5f ? Screen.height * 0.9f : Screen.height * 0.1f;
            return new Vector2(x, y);
        }

        [UnityTest]
        public IEnumerator ADragBeginsFromAnywhereOnTheScreen()
        {
            yield return Settle();

            SmileyController controller = Controller();
            Vector2 far = FarFromSmiley();
            Vector2 onSmiley = Camera.main.WorldToScreenPoint(controller.GetComponent<Rigidbody2D>().position);

            Assert.That((far - onSmiley).magnitude, Is.GreaterThan(Screen.height * 0.3f),
                "the fixture's 'far' point is not far from Smiley; the test would prove nothing");

            PointerReader.SetOverride(new PointerSample(true, far, 3));
            yield return null;

            Assert.That(controller.IsDragging, Is.True,
                "a touch nowhere near Smiley did not begin a drag — D-111 says the whole screen does");
        }

        [UnityTest]
        public IEnumerator ATouchOnTheCornerButtonDoesNotBeginADrag()
        {
            yield return Settle();

            var document = Object.FindFirstObjectByType<GameScreens>().GetComponent<UIDocument>();
            VisualElement button = document.rootVisualElement.Q(UiKit.Names.MenuButton);
            Assert.That(button, Is.Not.Null, "the layout has no corner button");
            Assert.That(UiKit.IsVisible(button), Is.True, "the corner button is hidden before the climb");

            Vector2 screen = ScreenPointOf(document.rootVisualElement.panel, button.worldBound.center);

            Assert.That(UiKit.IsOverUi(screen), Is.True,
                "the hit test does not see the corner button at its own centre, so the guard is inert");

            PointerReader.SetOverride(new PointerSample(true, screen, 4));
            yield return null;

            Assert.That(Controller().IsDragging, Is.False,
                "tapping the menu button began a drag underneath the menu it opens");
        }

        [UnityTest]
        public IEnumerator DisablingTheControllerMidDragAbandonsItWithoutALaunch()
        {
            yield return Settle();

            SmileyController controller = Controller();
            var body = controller.GetComponent<Rigidbody2D>();
            Vector2 start = FarFromSmiley();

            PointerReader.SetOverride(new PointerSample(true, start, 5));
            yield return null;
            PointerReader.SetOverride(new PointerSample(true, start + new Vector2(0f, -Screen.height * 0.25f), 5));
            yield return null;
            Assert.That(controller.IsDragging, Is.True, "no drag to interrupt");

            // What GameScreens does when a screen goes up — with a full aim frozen in the drag.
            controller.enabled = false;
            yield return null;

            Assert.That(controller.IsDragging, Is.False, "the drag survived the controller being disabled");

            // The finger lifts while the screen is up; then the screen comes down.
            PointerReader.SetOverride(PointerSample.None);
            controller.enabled = true;
            yield return null;
            yield return new WaitForFixedUpdate();

            Assert.That(body.linearVelocity.magnitude, Is.LessThan(0.05f),
                "the abandoned aim fired as a launch when the controller came back");
        }

        [UnityTest]
        public IEnumerator DraggingAcrossTheScreenNeverMovesTheCamera()
        {
            yield return Settle();

            SmileyController controller = Controller();
            Transform camera = Camera.main.transform;

            // Let the follow finish settling on the resting Smiley before the baseline is read.
            for (int i = 0; i < 30; i++) yield return null;
            Vector3 before = camera.position;

            Vector2 start = FarFromSmiley();
            PointerReader.SetOverride(new PointerSample(true, start, 6));
            yield return null;
            Assert.That(controller.IsDragging, Is.True, "no drag began, so nothing here tests the camera");

            // A finger travelling most of the screen — under D-010 the world would have moved with it.
            for (int frame = 1; frame <= 20; frame++)
            {
                Vector2 finger = start + new Vector2(-Screen.width * 0.6f, Screen.height * 0.5f) * (frame / 20f);
                PointerReader.SetOverride(new PointerSample(true, finger, 6));
                yield return null;
            }

            Assert.That((camera.position - before).magnitude, Is.LessThan(0.02f),
                "the camera moved with the finger — the drag is controlling the view (D-111 forbids it)");
        }

        /// <summary>
        /// The screen-space point (bottom-left origin) under a panel-space point, found by
        /// inverting UI Toolkit's own mapping rather than assuming the panel's scale.
        /// </summary>
        private static Vector2 ScreenPointOf(IPanel panel, Vector2 panelPoint)
        {
            Vector2 a = RuntimePanelUtils.ScreenToPanel(panel, Vector2.zero);
            Vector2 b = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(Screen.width, Screen.height));
            float tx = (panelPoint.x - a.x) / (b.x - a.x);
            float ty = (panelPoint.y - a.y) / (b.y - a.y);
            return new Vector2(tx * Screen.width, ty * Screen.height);
        }
    }
}
