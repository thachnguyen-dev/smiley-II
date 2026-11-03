using UnityEngine;

namespace Smiley.View
{
    /// <summary>
    /// What frame rate the game asks the phone for, and why it has to ask at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⛔ <b>Measured on the phone, 2026-09-13 (D-113): 30.0 fps, every frame 33.3 ms, idle and
    /// in play alike.</b> Not load — the display refreshes at 60 Hz and the frame time never
    /// varied — but Unity's Android default: with <see cref="Application.targetFrameRate"/>
    /// left at −1 the player runs at 30, and nothing in this project had set it. The whole
    /// game had been shipping at half the display's rate.
    /// </para>
    /// <para>
    /// ⓘ The target follows the display so a 90 or 120 Hz phone is not held to 60, with 60 as
    /// the floor for a display that reports nothing useful. Gameplay is unaffected either way:
    /// the physics is a fixed step (§33), and the rendering interpolates it.
    /// </para>
    /// </remarks>
    public static class FrameRateRule
    {
        /// <summary>The floor, and the answer when the display's rate is unknown.</summary>
        public const int Minimum = 60;

        /// <summary>The frame rate to target for a display refreshing at <paramref name="refreshHz"/>.</summary>
        public static int Target(double refreshHz)
        {
            if (double.IsNaN(refreshHz) || double.IsInfinity(refreshHz) || refreshHz <= 0)
            {
                return Minimum;
            }

            int rounded = (int)System.Math.Round(refreshHz);
            return rounded < Minimum ? Minimum : rounded;
        }

        /// <summary>Applies the rule once, before the first scene loads — in a player only.</summary>
        /// <remarks>
        /// ⛔ <b>Never in the Editor.</b> A batch-mode test run paces frames as fast as they
        /// come, and every Play Mode fixture in this project is calibrated on that: with a
        /// 60 fps target the Editor throttled each test frame to a real 16 ms, physics
        /// advanced many more steps per <c>yield return null</c>, and two drag fixtures that
        /// measure a resting body failed on the pacing rather than on the game. The phone
        /// runs a fixed physics step either way, so the target changes rendering only.
        /// </remarks>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            if (Application.isEditor)
            {
                return;
            }

            // ⭐ D-138: never in a browser. The page paces frames to the display itself, and
            // Unity's guidance for WebGL is to leave targetFrameRate at −1 — a set value
            // switches it to a timer, which stutters. ⓘ Physics is a fixed step either way (§33).
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                return;
            }

            int target = Target(Screen.currentResolution.refreshRateRatio.value);
            Application.targetFrameRate = target;
            Debug.Log($"[FPS] target {target} (display {Screen.currentResolution.refreshRateRatio.value:0.##} Hz)");
        }
    }
}
