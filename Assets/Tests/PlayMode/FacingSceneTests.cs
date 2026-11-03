using System.Collections;
using NUnit.Framework;
using Smiley.Gameplay;
using Smiley.View;
using UnityEngine;
using UnityEngine.TestTools;

namespace Smiley.Tests
{
    /// <summary>The character faces the way he moves, in the booted scene (D-095).</summary>
    public sealed class FacingSceneTests
    {
        [UnitySetUp]
        public IEnumerator Load()
        {
            yield return GameSceneFixture.LoadFresh();
            GameSceneFixture.ClearTheSky();
        }

        private static (Facing facing, Rigidbody2D body, SpriteRenderer art) Find()
        {
            var controller = Object.FindFirstObjectByType<SmileyController>();
            var facing = controller.GetComponentInChildren<Facing>();
            Assert.That(facing, Is.Not.Null, "Smiley has no Facing — D-095 is not wired");
            return (facing, controller.GetComponent<Rigidbody2D>(), facing.GetComponent<SpriteRenderer>());
        }

        private static IEnumerator Fly(Rigidbody2D body, Vector2 velocity)
        {
            body.gravityScale = 0f;
            body.position = new Vector2(0f, 20f);
            body.linearVelocity = velocity;
            yield return new WaitForFixedUpdate();
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator MovingLeftMirrorsThePictureInPlace()
        {
            (Facing facing, Rigidbody2D body, SpriteRenderer art) = Find();
            float artYBefore = art.transform.localPosition.y;

            yield return Fly(body, new Vector2(-4f, 1f));

            Assert.That(facing.IsMirrored, Is.True);
            Assert.That(art.flipX, Is.True, "moving left and not mirrored");
            Assert.That(art.flipY, Is.False, "left-right only");
            Assert.That(art.transform.localPosition.y, Is.EqualTo(artYBefore).Within(1e-5f), "mirroring must not move him");

            body.gravityScale = 1f;
        }

        [UnityTest]
        public IEnumerator MovingRightShowsHimAsDrawn()
        {
            (Facing facing, Rigidbody2D body, SpriteRenderer art) = Find();

            yield return Fly(body, new Vector2(-4f, 0f));
            Assert.That(facing.IsMirrored, Is.True, "precondition");

            yield return Fly(body, new Vector2(4f, 0f));

            Assert.That(facing.IsMirrored, Is.False);
            Assert.That(art.flipX, Is.False);

            body.gravityScale = 1f;
        }

        [UnityTest]
        public IEnumerator StoppingKeepsHimFacingTheWayHeWent()
        {
            (Facing facing, Rigidbody2D body, _) = Find();

            yield return Fly(body, new Vector2(-4f, 0f));
            yield return Fly(body, Vector2.zero);

            Assert.That(facing.IsMirrored, Is.True, "he stopped and snapped back — the owner asked for the direction of movement, not a default");

            body.gravityScale = 1f;
        }

        [UnityTest]
        public IEnumerator TheFacingTouchesNothingPhysical()
        {
            (Facing facing, Rigidbody2D body, _) = Find();
            var polygon = body.GetComponent<PolygonCollider2D>();

            yield return Fly(body, new Vector2(4f, 0f));
            Vector2[] asDrawn = polygon.GetPath(0);

            yield return Fly(body, new Vector2(-4f, 0f));
            Assert.That(facing.IsMirrored, Is.True);

            Assert.That(polygon.GetPath(0), Is.EqualTo(asDrawn));
            Assert.That(body.transform.localScale, Is.EqualTo(Vector3.one));

            body.gravityScale = 1f;
        }
    }
}
