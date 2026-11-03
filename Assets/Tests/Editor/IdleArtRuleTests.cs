using NUnit.Framework;
using Smiley.View;
using UnityEngine;

namespace Smiley.Tests
{
    /// <summary>D-092/D-093's pivot rule as arithmetic, on pixels made in memory.</summary>
    public sealed class IdleArtRuleTests
    {
        /// <summary>A 160 x 180 canvas: faint rows from <paramref name="faintFrom"/>, a solid body from <paramref name="solidFrom"/> up.</summary>
        private static Color32[] Canvas(int faintFrom, int solidFrom, int top)
        {
            var px = new Color32[160 * 180];

            for (int y = 0; y < 180; y++)
            {
                byte a = y >= solidFrom && y < top ? (byte)255 : y >= faintFrom && y < solidFrom ? (byte)40 : (byte)0;

                for (int x = 14; x < 139; x++)
                {
                    px[y * 160 + x] = new Color32(0, 0, 0, a);
                }
            }

            return px;
        }

        [Test]
        public void TheSolidBottomIgnoresFaintRowsBelowTheBody()
        {
            // ⛔ D-093: the owner's frames carry a soft shadow of alpha 1–199 up to 21 rows under
            // the body. The trimmed rect starts at the faint rows; the anchor must not.
            Color32[] px = Canvas(faintFrom: 0, solidFrom: 15, top: 152);
            var rect = new RectInt(14, 0, 125, 152);

            Assert.That(IdleArtRule.SolidBottomRow(px, 160, rect), Is.EqualTo(15));
        }

        [Test]
        public void AFullyOpaqueBodyAnchorsOnItsFirstRow()
        {
            Color32[] px = Canvas(faintFrom: 7, solidFrom: 7, top: 152);
            var rect = new RectInt(14, 7, 125, 145);

            Assert.That(IdleArtRule.SolidBottomRow(px, 160, rect), Is.EqualTo(7));
        }

        [Test]
        public void AFewStraySolidPixelsDoNotCountAsTheBottom()
        {
            Color32[] px = Canvas(faintFrom: 0, solidFrom: 20, top: 152);

            // Three opaque specks on row 3 — fewer than MinSolidPixels.
            for (int x = 50; x < 53; x++) px[3 * 160 + x] = new Color32(0, 0, 0, 255);

            Assert.That(IdleArtRule.SolidBottomRow(px, 160, new RectInt(14, 0, 125, 152)), Is.EqualTo(20));
        }

        [Test]
        public void ThePivotIsTheSolidRowAtTheBodysCentreColumn()
        {
            // Rect from row 0 (faint shadow), solid body from row 15: the pivot sits 15/152 up.
            var rect = new Rect(14f, 0f, 125f, 152f);
            Vector2 pivot = IdleArtRule.PivotFor(rect, 15);

            Assert.That(pivot.y, Is.EqualTo(15f / 152f).Within(1e-5f));
            Assert.That(pivot.x, Is.EqualTo((76.5f - 14f) / 125f).Within(1e-5f), "x stays on the body's centre column");
        }

        [Test]
        public void AFrameDrawnHigherStillPivotsOnItsOwnSolidBottom()
        {
            // Two frames whose bodies start at rows 7 and 26 both put the pivot exactly on the
            // body's bottom, so both land on the collider bottom.
            Vector2 low = IdleArtRule.PivotFor(new Rect(14f, 7f, 125f, 145f), 7);
            Vector2 high = IdleArtRule.PivotFor(new Rect(14f, 26f, 125f, 145f), 26);

            Assert.That(low.y, Is.Zero);
            Assert.That(high.y, Is.Zero);
        }

        [Test]
        public void ANarrowerFrameKeepsTheSameCanvasColumn()
        {
            Vector2 wide = IdleArtRule.PivotFor(new Rect(14f, 7f, 125f, 145f), 7);
            Vector2 narrow = IdleArtRule.PivotFor(new Rect(30f, 7f, 100f, 145f), 7);

            Assert.That(14f + wide.x * 125f, Is.EqualTo(30f + narrow.x * 100f).Within(1e-3f),
                "both pivots must sit on canvas column 76.5");
        }
    }
}
