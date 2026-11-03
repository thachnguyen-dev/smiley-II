using NUnit.Framework;
using Smiley.Gameplay;
using UnityEngine;

namespace Smiley.Tests
{
    /// <summary>How a fringe measurement becomes a smaller collider (D-084).</summary>
    public sealed class PlatformShapeTests
    {
        private GameObject _root;

        [TearDown]
        public void Clear()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        private static Sprite MakeSprite(int width, int height, float ppu, Vector4 border)
        {
            var texture = new Texture2D(width, height);
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect, border);
        }

        private (PlatformShape shape, BoxCollider2D collider, SpriteRenderer visual) Build(Sprite sprite)
        {
            _root = new GameObject("Platform");
            var collider = _root.AddComponent<BoxCollider2D>();
            var shape = _root.AddComponent<PlatformShape>();

            var child = new GameObject("Visual");
            child.transform.SetParent(_root.transform, false);
            var visual = child.AddComponent<SpriteRenderer>();
            visual.sprite = sprite;

            shape.Set(3f, 0.4f, visual);
            return (shape, collider, visual);
        }

        [Test]
        public void WithNoFringeTheColliderIsThePictureMinusTheRestGap()
        {
            (PlatformShape shape, BoxCollider2D collider, SpriteRenderer visual) = Build(MakeSprite(100, 40, 100f, Vector4.zero));

            Assert.That(visual.size, Is.EqualTo(new Vector2(3f, 0.4f)));
            Assert.That(collider.size.x, Is.EqualTo(3f).Within(1e-5f));
            Assert.That(collider.size.y, Is.EqualTo(0.4f - PlatformShape.PlayerRestGap).Within(1e-5f));
            Assert.That(collider.offset.y, Is.EqualTo(-PlatformShape.PlayerRestGap * 0.5f).Within(1e-5f),
                "the collider shrinks from the TOP only, so its centre moves down by half the gap");
        }

        [Test]
        public void AStretchedSpriteTurnsFringeRowsIntoAFractionOfTheHeight()
        {
            // No 9-slice borders: the sprite stretches to 0.4 units, so 8 of 40 rows is a fifth.
            Sprite sprite = MakeSprite(100, 40, 100f, Vector4.zero);
            (PlatformShape shape, BoxCollider2D collider, _) = Build(sprite);

            shape.SetFringe(top: 8, bottom: 0, left: 0, right: 0, measuredOn: sprite);

            float expectedTop = 0.4f * 8f / 40f + PlatformShape.PlayerRestGap;

            Assert.That(shape.ColliderInsets.top, Is.EqualTo(expectedTop).Within(1e-5f));
            Assert.That(collider.size.y, Is.EqualTo(0.4f - expectedTop).Within(1e-5f));
            Assert.That(collider.offset.y, Is.EqualTo(-expectedTop * 0.5f).Within(1e-5f));
            Assert.That(shape.FittedSprite, Is.SameAs(sprite));
        }

        [Test]
        public void ANineSlicedSpriteKeepsItsFringeAtPixelSize()
        {
            // With a top border the edge rows are drawn at their own pixel size whatever the
            // platform's height, so 8 rows at 200 px/unit is 0.04 units — not a fifth of 0.4.
            Sprite sprite = MakeSprite(100, 40, 200f, new Vector4(0f, 0f, 0f, 10f));
            (PlatformShape shape, BoxCollider2D collider, _) = Build(sprite);

            shape.SetFringe(top: 8, bottom: 0, left: 0, right: 0, measuredOn: sprite);

            float expectedTop = 8f / 200f + PlatformShape.PlayerRestGap;

            Assert.That(shape.ColliderInsets.top, Is.EqualTo(expectedTop).Within(1e-5f));
            Assert.That(collider.size.y, Is.EqualTo(0.4f - expectedTop).Within(1e-5f));
        }

        [Test]
        public void SideAndBottomFringeAreRecordedButNeverNarrowTheCollider()
        {
            // ⛔ Owner: "keep the platform gameplay behavior otherwise unchanged." The first fit of
            // their art found a fifth of the Disappearing platform's width to be side fringe;
            // applying it would have shrunk every landing. Only the top moves.
            Sprite sprite = MakeSprite(100, 40, 100f, Vector4.zero);
            (PlatformShape shape, BoxCollider2D collider, SpriteRenderer visual) = Build(sprite);

            shape.SetFringe(top: 0, bottom: 9, left: 10, right: 20, measuredOn: sprite);

            Assert.That(shape.FringeRows, Is.EqualTo((0, 9, 10, 20)), "the measurement is kept");
            Assert.That(shape.HorizontalFringeUnits(isLeft: true), Is.EqualTo(0.3f).Within(1e-5f));
            Assert.That(collider.size.x, Is.EqualTo(3f).Within(1e-5f), "the sides did not move");
            Assert.That(collider.offset.x, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(collider.size.y, Is.EqualTo(0.4f - PlatformShape.PlayerRestGap).Within(1e-5f), "the bottom did not move");
            Assert.That(visual.size, Is.EqualTo(new Vector2(3f, 0.4f)), "the picture must not change");
            Assert.That(visual.transform.localPosition, Is.EqualTo(Vector3.zero));
        }
    }
}
