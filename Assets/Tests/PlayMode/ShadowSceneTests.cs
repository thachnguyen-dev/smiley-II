using System.Collections;
using NUnit.Framework;
using Smiley.Gameplay;
using Smiley.View;
using UnityEngine;
using UnityEngine.TestTools;

namespace Smiley.Tests
{
    /// <summary>Smiley's shadow in the booted scene (D-083).</summary>
    public sealed class ShadowSceneTests
    {
        [UnitySetUp]
        public IEnumerator Load()
        {
            yield return GameSceneFixture.LoadFresh();
            GameSceneFixture.ClearTheSky();
        }

        private static (SmileyShadow shadow, Rigidbody2D body, SpriteRenderer renderer) Find()
        {
            var controller = Object.FindFirstObjectByType<SmileyController>();
            var shadow = controller.GetComponentInChildren<SmileyShadow>();
            Assert.That(shadow, Is.Not.Null, "Smiley has no shadow child — D-083 is not wired");
            return (shadow, controller.GetComponent<Rigidbody2D>(), shadow.GetComponent<SpriteRenderer>());
        }

        [UnityTest]
        public IEnumerator TheShadowIsAPictureAndNothingElse()
        {
            // ⛔ Owner: "should not interfere with gameplay collision." No collider anywhere on it.
            (SmileyShadow shadow, _, SpriteRenderer renderer) = Find();

            Assert.That(shadow.GetComponent<Collider2D>(), Is.Null);
            Assert.That(shadow.GetComponentsInChildren<Collider2D>(), Is.Empty);
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.sprite, Is.Not.Null, "the shadow has no picture to draw");

            yield return null;
        }

        [UnityTest]
        public IEnumerator AboveTheStartAreaTheShadowLiesOnItsSurfaceUnderSmiley()
        {
            (SmileyShadow shadow, Rigidbody2D body, SpriteRenderer renderer) = Find();
            Bounds floor = GameObject.Find("StartArea").GetComponent<Collider2D>().bounds;

            body.WakeUp();
            body.position = new Vector2(2f, floor.max.y + 2f);
            body.linearVelocity = Vector2.zero;
            body.gravityScale = 0f;

            yield return null;
            yield return null;

            Assert.That(renderer.enabled, Is.True, "no shadow with a platform two units below");
            Assert.That(shadow.Under, Is.Not.Null);
            Assert.That(shadow.Under.name, Is.EqualTo("StartArea"));
            // ⭐ D-085: a little to the RIGHT of his centre, by ShadowOffsetX of his diameter.
            Assert.That(shadow.transform.position.x, Is.GreaterThan(2f + 0.05f), "the shadow is centred under him; the owner asked for it to sit to the right");
            Assert.That(shadow.transform.position.x, Is.LessThan(2f + 0.5f), "the shadow is too far right to read as his");
            Assert.That(shadow.transform.position.y, Is.EqualTo(floor.max.y).Within(0.02f), "the shadow is not on the surface");
            Assert.That(shadow.transform.rotation, Is.EqualTo(Quaternion.identity), "a shadow lies flat");

            body.gravityScale = 1f;
        }

        [UnityTest]
        public IEnumerator TheShadowShrinksAndFadesWithHeight()
        {
            (SmileyShadow shadow, Rigidbody2D body, SpriteRenderer renderer) = Find();
            Bounds floor = GameObject.Find("StartArea").GetComponent<Collider2D>().bounds;
            body.gravityScale = 0f;
            body.linearVelocity = Vector2.zero;

            body.position = new Vector2(0f, floor.max.y + 0.6f);
            yield return null;
            yield return null;
            float lowScale = shadow.transform.localScale.x;
            float lowAlpha = renderer.color.a;

            body.position = new Vector2(0f, floor.max.y + 4f);
            yield return null;
            yield return null;
            float highScale = shadow.transform.localScale.x;
            float highAlpha = renderer.color.a;

            Assert.That(renderer.enabled, Is.True);
            Assert.That(highScale, Is.LessThan(lowScale), "higher up, the shadow should be smaller");
            Assert.That(highAlpha, Is.LessThan(lowAlpha), "higher up, the shadow should be fainter");

            body.gravityScale = 1f;
        }

        [UnityTest]
        public IEnumerator FarAboveAnyPlatformThereIsNoShadow()
        {
            (SmileyShadow shadow, Rigidbody2D body, SpriteRenderer renderer) = Find();
            Bounds floor = GameObject.Find("StartArea").GetComponent<Collider2D>().bounds;
            body.gravityScale = 0f;
            body.linearVelocity = Vector2.zero;
            body.position = new Vector2(0f, floor.max.y + 40f);

            yield return null;
            yield return null;

            Assert.That(renderer.enabled, Is.False, "a shadow forty units above the floor is a shadow on nothing");
            Assert.That(shadow.Under, Is.Null);

            body.gravityScale = 1f;
        }
    }
}
