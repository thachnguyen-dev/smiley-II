using NUnit.Framework;
using Smiley.Gameplay;
using UnityEngine;

namespace Smiley.Tests
{
    /// <summary>D-084's fringe measurement, on textures made in memory.</summary>
    public sealed class ArtFringeTests
    {
        /// <summary>A texture: solid rows, with a sparse fringe of tufts on top and a fully clear row above that.</summary>
        private static Color32[] Picture(int width, int height, int clearTop, int tuftRows, int clearBottom)
        {
            var pixels = new Color32[width * height];

            for (int y = 0; y < height; y++)
            {
                bool clear = y >= height - clearTop || y < clearBottom;
                bool tuft = !clear && y >= height - clearTop - tuftRows;

                for (int x = 0; x < width; x++)
                {
                    byte alpha = clear ? (byte)0 : tuft ? (x % 3 == 0 ? (byte)255 : (byte)0) : (byte)255;
                    pixels[y * width + x] = new Color32(0, 0, 0, alpha);
                }
            }

            return pixels;
        }

        [Test]
        public void TheFringeIsTheRowsBeforeTheFirstMostlyOpaqueRow()
        {
            // 100 wide, 40 tall: 2 clear rows, then 6 rows of tufts (a third opaque), then solid.
            Color32[] pixels = Picture(100, 40, clearTop: 2, tuftRows: 6, clearBottom: 0);

            (int top, int bottom, int left, int right) = ArtFringe.Measure(pixels, 100, new RectInt(0, 0, 100, 40));

            Assert.That(top, Is.EqualTo(8), "the fringe is the clear rows AND the tufts");
            Assert.That(bottom, Is.EqualTo(0));
            Assert.That(left, Is.EqualTo(0), "the columns are solid from the sides");
            Assert.That(right, Is.EqualTo(0));
        }

        [Test]
        public void TheSpriteRectangleIsRespectedInsideALargerTexture()
        {
            // The sprite sits at (10, 5) in a 120 × 50 texture; outside it is clear.
            const int W = 120, H = 50;
            var pixels = new Color32[W * H];
            Color32[] inner = Picture(100, 40, clearTop: 0, tuftRows: 3, clearBottom: 2);

            for (int y = 0; y < 40; y++)
                for (int x = 0; x < 100; x++)
                    pixels[(y + 5) * W + (x + 10)] = inner[y * 100 + x];

            (int top, int bottom, int left, int right) = ArtFringe.Measure(pixels, W, new RectInt(10, 5, 100, 40));

            Assert.That(top, Is.EqualTo(3));
            Assert.That(bottom, Is.EqualTo(2));
            Assert.That(left, Is.EqualTo(0));
            Assert.That(right, Is.EqualTo(0));
        }

        [Test]
        public void ASolidRectangleHasNoFringe()
        {
            Color32[] pixels = Picture(64, 16, 0, 0, 0);

            Assert.That(ArtFringe.Measure(pixels, 64, new RectInt(0, 0, 64, 16)), Is.EqualTo((0, 0, 0, 0)));
        }

        [Test]
        public void APictureWithNoSolidRowReportsNoFringeRatherThanEatingTheCollider()
        {
            // All tufts: nothing is "the surface", so the collider keeps the full picture.
            Color32[] pixels = Picture(30, 10, 0, 10, 0);

            (int top, int bottom, int left, int right) = ArtFringe.Measure(pixels, 30, new RectInt(0, 0, 30, 10));

            Assert.That(top, Is.EqualTo(0));
            Assert.That(bottom, Is.EqualTo(0));
        }
    }
}
