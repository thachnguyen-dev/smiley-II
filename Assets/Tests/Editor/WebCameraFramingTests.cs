using NUnit.Framework;
using Smiley.View;

namespace Smiley.Tests
{
    /// <summary>D-140: the browser's camera, framed by height and capped by the map.</summary>
    public sealed class WebCameraFramingTests
    {
        private const float PhoneWidth = 8.5f;     // D-112, owner-set
        private const float WebHeight = 18.9f;     // Q-060, derived from the reference phone
        private const float MapWidth = 27f;        // Q-030 / D-081, owner-set

        private static float VisibleWidth(float half, float aspect) => half * 2f * aspect;

        [Test]
        public void AWideWindowShowsTheWholeMapWidthAndNoMore()
        {
            float aspect = 16f / 9f;
            float half = CameraFraming.WebHalfHeightFor(PhoneWidth, WebHeight, MapWidth, aspect);

            Assert.That(VisibleWidth(half, aspect), Is.EqualTo(MapWidth).Within(1e-3f), "past the map's sides");
            Assert.That(half * 2f, Is.LessThan(WebHeight), "the height should give way to the map's width");
        }

        [Test]
        public void ANarrowerWindowGetsAPhonesHeight()
        {
            float aspect = 4f / 3f;
            float half = CameraFraming.WebHalfHeightFor(PhoneWidth, WebHeight, MapWidth, aspect);

            Assert.That(half * 2f, Is.EqualTo(WebHeight).Within(1e-3f));
            Assert.That(VisibleWidth(half, aspect), Is.LessThanOrEqualTo(MapWidth));
        }

        [Test]
        public void APhoneShapedWindowLooksLikeThePhone()
        {
            float aspect = 1080f / 2400f;
            float half = CameraFraming.WebHalfHeightFor(PhoneWidth, WebHeight, MapWidth, aspect);

            Assert.That(VisibleWidth(half, aspect), Is.EqualTo(PhoneWidth).Within(0.01f));
        }

        [Test]
        public void TheViewIsNeverNarrowerThanAPhone()
        {
            float aspect = 0.3f; // a very tall, thin window
            float half = CameraFraming.WebHalfHeightFor(PhoneWidth, WebHeight, MapWidth, aspect);

            Assert.That(VisibleWidth(half, aspect), Is.GreaterThanOrEqualTo(PhoneWidth - 1e-3f));
        }

        [Test]
        public void WithNoMapFoundThereIsNoCap()
        {
            float half = CameraFraming.WebHalfHeightFor(PhoneWidth, WebHeight, 0f, 16f / 9f);

            Assert.That(half * 2f, Is.EqualTo(WebHeight).Within(1e-3f));
        }

        [Test]
        public void PhonesKeepTheWidthRule()
        {
            // ⛔ The phone formula is unchanged: 8.5 across at any aspect.
            float aspect = 1080f / 2400f;
            Assert.That(VisibleWidth(CameraFraming.HalfHeightFor(PhoneWidth, aspect), aspect),
                Is.EqualTo(PhoneWidth).Within(1e-4f));
        }
    }
}
