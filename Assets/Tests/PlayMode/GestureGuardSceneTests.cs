using System.Collections;
using NUnit.Framework;
using Smiley.Device;
using UnityEngine;
using UnityEngine.TestTools;

namespace Smiley.Tests
{
    /// <summary>
    /// What can honestly be tested about the gesture guard off-device.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⛔ <b>THE INTEROP ITSELF IS INVISIBLE TO EVERY TEST IN THIS PROJECT.</b> It lives inside
    /// <c>#if UNITY_ANDROID &amp;&amp; !UNITY_EDITOR</c>, so the editor does not even <i>compile</i>
    /// it — which is exactly how a missing <c>com.unity.modules.androidjni</c> got past a green
    /// suite and only surfaced when the player build failed.
    /// </para>
    /// <para>
    /// ⓘ So these fixtures cover the two things that are real off-device: that the guard is in
    /// the scene at all, and that the quit guard starts closed. The gesture exclusion is
    /// verified on hardware, by swiping the edges and watching whether the app survives.
    /// </para>
    /// </remarks>
    public sealed class GestureGuardSceneTests
    {
        [UnitySetUp]
        public IEnumerator LoadTheGameScene()
        {
            yield return GameSceneFixture.LoadFresh();
        }

        [UnityTest]
        public IEnumerator TheGuardIsInTheScene()
        {
            Assert.That(Object.FindFirstObjectByType<SystemGestureGuard>(), Is.Not.Null,
                "the scene has no SystemGestureGuard, so nothing defends the drag from " +
                "Android's back gesture");

            yield return null;
        }

        [UnityTest]
        public IEnumerator QuitIsBlockedUntilSomethingDeliberatelyAllowsIt()
        {
            // ⚠ The default matters more than it looks. Unity's Android back closes the app,
            // and this game is one long climb (§1) whose save is a resume point (§21) — losing
            // it to a misplaced thumb is the worst answer to an ambiguous gesture.
            //
            // ⓘ The escape hatch is asserted too: §22's Exit/Quit has to be able to work when
            // it is built, and a guard with no way out would be a different bug.
            Assert.That(SystemGestureGuard.AllowQuit, Is.False,
                "quitting is permitted by default, so a stray back gesture can end a climb");

            SystemGestureGuard.AllowQuit = true;
            Assert.That(SystemGestureGuard.AllowQuit, Is.True,
                "a deliberate Exit cannot re-enable quitting");

            SystemGestureGuard.AllowQuit = false;
            yield return null;
        }
    }
}
