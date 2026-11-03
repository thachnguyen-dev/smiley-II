using NUnit.Framework;
using Smiley.Device;

namespace Smiley.Tests
{
    /// <summary>
    /// App Tracking Transparency, from the side that is not iOS (D-127).
    /// </summary>
    /// <remarks>
    /// ⛔ <b>What this can and cannot prove.</b> The native prompt lives in an Objective-C++ file
    /// that only a Mac can compile, so no test here reaches it. What these DO prove is the part
    /// that could break the shipping Android app: that on every platform which is not iOS the
    /// consent step is a straight pass-through, on the same frame, with no prompt and no wait.
    /// ⚠ The iOS behaviour itself is confirmed on the device and nowhere else.
    /// </remarks>
    public sealed class TrackingConsentTests
    {
        [Test]
        public void OffIosTheCallbackRunsImmediatelyAndOnThisFrame()
        {
            bool ran = false;

            TrackingConsent.Request(() => ran = true);

            Assert.That(ran, Is.True,
                "the consent step did not call through — on Android this would mean the ad SDK " +
                "is never initialised and the live app shows no ads at all");
        }

        [Test]
        public void ANullCallbackIsNotAnError()
        {
            // ⓘ AdService passes a method, but nothing should explode if a caller passes nothing.
            Assert.DoesNotThrow(() => TrackingConsent.Request(null));
        }

        [Test]
        public void OffIosTheStatusIsAuthorisedSoNothingWaitsOnAnAnswer()
        {
            // ⛔ Not "not determined": a caller that waits for an answer would wait forever on a
            // platform that has no prompt to give one.
            Assert.That(TrackingConsent.Current, Is.EqualTo(TrackingConsent.Status.Authorised));
        }
    }
}
