using UnityEngine;

namespace Smiley.Device
{
    /// <summary>
    /// Stops Android's edge gestures from eating the player's drag.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⛔ <b>The problem is real and was measured, not assumed (Q-038).</b> On the test handset a
    /// swipe inward from either side edge sent the app straight to the launcher mid-play. A
    /// swipe up from the bottom edge did <b>not</b> — so despite the obvious suspicion, the
    /// culprit is the <b>back</b> gesture, not home.
    /// </para>
    /// <para>
    /// ⓘ <b>That distinction decides whether this is fixable at all.</b> Android lets an app
    /// exclude the back gesture and explicitly does <b>not</b> let it exclude home or
    /// quick-switch. Had the bottom gesture been the cause, no amount of code would have
    /// helped and the answer would have been a layout change instead.
    /// </para>
    /// <para>
    /// ⚠ <b>The exclusion is capped by the system</b> — roughly 200dp per edge — so a full-height
    /// strip is not on offer. The strip is anchored to the <b>bottom</b> of the screen, because
    /// §4 puts the Start Area there and §6 puts the drag under the player's thumb, which is
    /// where the accidental swipes happen.
    /// </para>
    /// <para>
    /// Two defences, because the first is best-effort:
    /// <list type="number">
    /// <item>Exclusion rects, so the gesture is not recognised near the thumb.</item>
    /// <item>A quit guard, so that if back fires anyway it cannot silently end a climb.</item>
    /// </list>
    /// </para>
    /// </remarks>
    public sealed class SystemGestureGuard : MonoBehaviour
    {
        [Tooltip("PROVISIONAL. Width of the excluded strip on each side edge, in dp.")]
        [SerializeField] private float _edgeWidthDp = 44f;

        [Tooltip("PROVISIONAL. Height of the excluded strip, in dp, anchored to the bottom of " +
                 "the screen. ⚠ Android caps the total exclusion per edge at around 200dp, so " +
                 "asking for more than that simply gets less.")]
        [SerializeField] private float _edgeHeightDp = 200f;

        /// <summary>Whether the exclusion was successfully applied. For the device log.</summary>
        public static bool ExclusionApplied { get; private set; }

        private static bool _quitBlocked;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

            BlockAccidentalQuit();
            ApplyGestureExclusion();
        }

        /// <summary>
        /// Stops a stray back gesture from closing the game.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Unity's default Android behaviour is that back quits. ⛔ In a game whose whole
        /// content is one long climb (§1) and whose save is a resume point (§21), losing the
        /// app to a misplaced thumb is the worst possible response to an ambiguous gesture.
        /// </para>
        /// <para>
        /// ⏸ <b>What back SHOULD do is still open.</b> D-007 put the Gameplay Menu on a corner
        /// button and explicitly declined back as its entry point, and §22's Exit/Quit lives on
        /// the Main Menu, which is not built. Until one of those exists, the safe behaviour is
        /// to do nothing rather than to guess — so back is consumed.
        /// </para>
        /// <para>
        /// ⚠ <see cref="AllowQuit"/> exists so §22's Exit can still work when it is built. A
        /// guard with no way out would be a different bug.
        /// </para>
        /// </remarks>
        private static void BlockAccidentalQuit()
        {
            if (_quitBlocked)
            {
                return;
            }

            _quitBlocked = true;
            Application.wantsToQuit += () => AllowQuit;
        }

        /// <summary>Set true by a deliberate Exit before calling <c>Application.Quit</c>.</summary>
        public static bool AllowQuit { get; set; }

        private void ApplyGestureExclusion()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var version = new AndroidJavaClass("android.os.Build$VERSION");

                // setSystemGestureExclusionRects arrived in API 29. Below that the gesture
                // navigation this defends against does not exist either.
                int sdk = version.GetStatic<int>("SDK_INT");
                if (sdk < 29)
                {
                    Debug.Log($"[GESTURE] SDK {sdk} predates gesture navigation; nothing to exclude.");
                    return;
                }

                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity");

                int width = Screen.width;
                int height = Screen.height;
                float density = Screen.dpi > 0f ? Screen.dpi / 160f : 1f;

                int edge = Mathf.RoundToInt(_edgeWidthDp * density);
                int strip = Mathf.RoundToInt(_edgeHeightDp * density);
                int top = Mathf.Max(0, height - strip);

                // ⚠ Rects are in VIEW coordinates: origin top-left, y increasing downward —
                // the opposite of Unity's screen space. Getting this wrong would exclude the
                // top of the screen, where nothing happens, and look like the call silently
                // failing.
                activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    try
                    {
                        using AndroidJavaObject window = activity.Call<AndroidJavaObject>("getWindow");
                        using AndroidJavaObject decor = window.Call<AndroidJavaObject>("getDecorView");
                        using var rects = new AndroidJavaObject("java.util.ArrayList");

                        using (var left = new AndroidJavaObject("android.graphics.Rect", 0, top, edge, height))
                        {
                            rects.Call<bool>("add", left);
                        }

                        using (var right = new AndroidJavaObject("android.graphics.Rect", width - edge, top, width, height))
                        {
                            rects.Call<bool>("add", right);
                        }

                        decor.Call("setSystemGestureExclusionRects", rects);
                        ExclusionApplied = true;

                        Debug.Log($"[GESTURE] Excluded back-gesture strips: {edge}px wide, " +
                                  $"y {top}..{height}, on both edges (SDK {sdk}).");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[GESTURE] Could not set exclusion rects: {e.Message}");
                    }
                }));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GESTURE] Gesture exclusion unavailable: {e.Message}");
            }
#else
            Debug.Log("[GESTURE] Not Android; no gesture exclusion to apply.");
#endif
        }
    }
}
