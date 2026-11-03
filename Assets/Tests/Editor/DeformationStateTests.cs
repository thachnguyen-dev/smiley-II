using NUnit.Framework;
using Smiley.Gameplay;
using UnityEngine;

namespace Smiley.Tests
{
    /// <summary>§11 and §12's deformation rules, as arithmetic.</summary>
    public sealed class DeformationStateTests
    {
        private const float Recovery = 0.2f;
        private const float Limit = 0.55f;

        [Test]
        public void ACollisionSquashesImmediately()
        {
            // §11: "A collision causes IMMEDIATE squash & stretch." An eased rise would
            // contradict that word, and would read as mushy at exactly the moment the player
            // is looking for feedback.
            var state = default(DeformationState);
            state.Advance(0.4f, 0f, 1f / 60f, Recovery, Limit);

            Assert.That(state.Magnitude, Is.EqualTo(0.4f).Within(1e-4f));
        }

        [Test]
        public void StrongerForceMeansMoreDeformation()
        {
            // §11: "Strong force -> larger deformation. Weak force -> smaller deformation."
            var weak = default(DeformationState);
            weak.Advance(0.1f, 0f, 1f / 60f, Recovery, Limit);

            var strong = default(DeformationState);
            strong.Advance(0.4f, 0f, 1f / 60f, Recovery, Limit);

            Assert.That(strong.Magnitude, Is.GreaterThan(weak.Magnitude));
        }

        [Test]
        public void DeformationFollowsTheDirectionOfTheForce()
        {
            // §11: "Deformation follows the direction of the collision force."
            var state = default(DeformationState);
            state.Advance(0.3f, Mathf.PI * 0.25f, 1f / 60f, Recovery, Limit);

            Assert.That(state.AxisRadians, Is.EqualTo(Mathf.PI * 0.25f).Within(1e-4f));
        }

        [Test]
        public void AContinuingForceHoldsTheShape()
        {
            // §11: "If the force continues, deformation continues updating." Sustaining the
            // same demand must not let it sag.
            var state = default(DeformationState);

            for (int i = 0; i < 60; i++)
            {
                state.Advance(0.3f, 0f, 1f / 60f, Recovery, Limit);
            }

            Assert.That(state.Magnitude, Is.EqualTo(0.3f).Within(1e-4f));
        }

        [Test]
        public void RemovingTheForceReturnsGraduallyNotInstantly()
        {
            // §11: "When the force ends, deformation GRADUALLY returns toward the natural
            // base shape." One frame later it must have moved, and must not have arrived.
            var state = default(DeformationState);
            state.Advance(0.5f, 0f, 1f / 60f, Recovery, Limit);

            state.Advance(0f, 0f, 1f / 60f, Recovery, Limit);

            Assert.That(state.Magnitude, Is.LessThan(0.5f), "it did not relax at all");
            Assert.That(state.Magnitude, Is.GreaterThan(0.05f), "it snapped back instead of easing");
        }

        [Test]
        public void ItReachesTheBaseShapeEventually()
        {
            // "Gradually" must not mean "never". A decay that asymptotes without ever
            // arriving would leave Smiley permanently, invisibly dented.
            var state = default(DeformationState);
            state.Advance(0.5f, 0f, 1f / 60f, Recovery, Limit);

            for (int i = 0; i < 600; i++)
            {
                state.Advance(0f, 0f, 1f / 60f, Recovery, Limit);
            }

            Assert.That(state.Magnitude, Is.EqualTo(0f));
        }

        [Test]
        public void TheTechnicalLimitCapsAnAbsurdImpact()
        {
            // §11 allows a technical limit "only to prevent physics instability, visual
            // errors, abnormally large values". A squash beyond 1 would invert the collider
            // inside out -- a visual error and an instability at once.
            var state = default(DeformationState);
            state.Advance(50f, 0f, 1f / 60f, Recovery, Limit);

            Assert.That(state.Magnitude, Is.EqualTo(Limit).Within(1e-4f));
            Assert.That(state.Magnitude, Is.LessThan(1f), "the shape would have inverted");
        }

        [Test]
        public void SquashingDoesNotPreserveArea()
        {
            // §11: "Smiley's total size may shrink or grow. Strict volume conservation is NOT
            // required." A stretch ratio below 1 must therefore be allowed to lose area --
            // an implementation that quietly normalised to preserve it would be obeying a
            // rule the spec explicitly waives.
            var state = new DeformationState { Magnitude = 0.5f, AxisRadians = 0f };

            Vector2 scale = state.ScaleInAxisFrame(0.75f);
            float area = scale.x * scale.y;

            Assert.That(scale.x, Is.LessThan(1f), "it did not squash");
            Assert.That(scale.y, Is.GreaterThan(1f), "it did not stretch across");
            Assert.That(area, Is.Not.EqualTo(1f).Within(1e-3f), "area was conserved");
        }

        [Test]
        public void TheBaseShapeIsUndeformedAtZero()
        {
            var state = default(DeformationState);
            Vector2 scale = state.ScaleInAxisFrame(0.75f);

            Assert.That(scale.x, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(scale.y, Is.EqualTo(1f).Within(1e-5f));
        }
    }
}
