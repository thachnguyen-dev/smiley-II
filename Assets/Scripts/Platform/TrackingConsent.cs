using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Smiley.Device
{
    /// <summary>
    /// Apple's App Tracking Transparency prompt, and the one question the game asks of it:
    /// may we start the ad SDK yet? (D-127)
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⛔ <b>Only iOS has this.</b> On Android, in the Editor, and on any platform without the
    /// framework, <see cref="Request"/> completes immediately and the game proceeds exactly as it
    /// does today — ⛔ the Android app on Play must not change behaviour because iOS gained a
    /// prompt.
    /// </para>
    /// <para>
    /// ⚠ <b>The answer does not gate advertising.</b> A player who declines still sees ads; they
    /// are simply not personalised, which is already this project's posture — D-110 set the
    /// audience to "not directed at children" and the SDK is told not to personalise regardless.
    /// So nothing here can refuse the player the game, and a denied prompt is a normal path, not
    /// an error (D-003).
    /// </para>
    /// <para>
    /// ⛔ <b>Unverified until a Mac compiles it.</b> The native side cannot be built on Windows,
    /// so everything below is written to survive its absence: a missing symbol is caught and
    /// treated as "no answer needed" rather than thrown at the player.
    /// </para>
    /// </remarks>
    public static class TrackingConsent
    {
        /// <summary>Apple's ATTrackingManagerAuthorizationStatus.</summary>
        public enum Status
        {
            NotDetermined = 0,
            Restricted = 1,
            Denied = 2,
            Authorised = 3,
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int SmileyTrackingStatus();

        [DllImport("__Internal")]
        private static extern void SmileyRequestTracking(TrackingAnswer done);

        private delegate void TrackingAnswer(int status);

        private static Action _then;

        [AOT.MonoPInvokeCallback(typeof(TrackingAnswer))]
        private static void OnAnswered(int status)
        {
            Debug.Log("[ATT] " + (Status)status);
            Action then = _then;
            _then = null;
            then?.Invoke();
        }
#endif

        /// <summary>What iOS currently says, or <see cref="Status.Authorised"/> everywhere else.</summary>
        public static Status Current
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                try
                {
                    return (Status)SmileyTrackingStatus();
                }
                catch (Exception e)
                {
                    // ⛔ A missing native symbol must not stop the game starting.
                    Debug.Log("[ATT] unavailable: " + e.Message);
                    return Status.Authorised;
                }
#else
                return Status.Authorised;
#endif
            }
        }

        /// <summary>
        /// Asks once if iOS has not yet been asked, then runs <paramref name="then"/>. Runs it
        /// immediately on every other platform, and whenever the prompt cannot be shown.
        /// </summary>
        /// <remarks>
        /// ⚠ <b>Call this BEFORE the ad SDK starts.</b> Apple's rule is that the IDFA may not be
        /// read until the player has answered, and the SDK reads it while initialising — so
        /// asking afterwards is the same as not asking.
        /// </remarks>
        public static void Request(Action then)
        {
#if UNITY_IOS && !UNITY_EDITOR
            try
            {
                if (SmileyTrackingStatus() != (int)Status.NotDetermined)
                {
                    // Already answered, in this install or a previous launch. Apple ignores a
                    // second request anyway, so asking again would just delay the game.
                    then?.Invoke();
                    return;
                }

                _then = then;
                SmileyRequestTracking(OnAnswered);
                return;
            }
            catch (Exception e)
            {
                Debug.Log("[ATT] could not ask: " + e.Message);
            }
#endif
            then?.Invoke();
        }
    }
}
