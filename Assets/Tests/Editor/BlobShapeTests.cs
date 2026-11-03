using NUnit.Framework;
using Smiley.Gameplay;
using UnityEngine;

namespace Smiley.Tests
{
    /// <summary>§11's outline: nearly round, deliberately not a circle, and deterministic.</summary>
    public sealed class BlobShapeTests
    {
        private const int Vertices = 16;
        private const int Seed = 20260910;

        [Test]
        public void TheOutlineIsNotAPerfectCircle()
        {
            // §11: Smiley is "NOT a mathematically perfect circle" and is "naturally and
            // slightly asymmetrical". The owner ruled this reaches the physics, so the
            // collider path itself must vary.
            Vector2[] points = BlobShape.BuildBase(Vertices, 0.06f, Seed);

            float min = float.MaxValue;
            float max = float.MinValue;

            foreach (Vector2 p in points)
            {
                float r = p.magnitude;
                min = Mathf.Min(min, r);
                max = Mathf.Max(max, r);
            }

            Assert.That(max - min, Is.GreaterThan(0.01f), "the outline is a circle");
        }

        [Test]
        public void ItIsStillNearlyRound()
        {
            // "Slightly" asymmetrical. A wildly lumpy body would catch on platform edges and
            // roll unpredictably in a game §18 already calls difficult.
            Vector2[] points = BlobShape.BuildBase(Vertices, 0.06f, Seed);

            foreach (Vector2 p in points)
            {
                Assert.That(p.magnitude, Is.EqualTo(1f).Within(0.2f));
            }
        }

        [Test]
        public void ZeroAsymmetryGivesATrueCircle()
        {
            // The asymmetry is one value (Q-036), and turning it off must give back the
            // predictable case -- both as an escape hatch and so the value's effect is
            // legible.
            Vector2[] points = BlobShape.BuildBase(Vertices, 0f, Seed);

            foreach (Vector2 p in points)
            {
                Assert.That(p.magnitude, Is.EqualTo(1f).Within(1e-4f));
            }
        }

        [Test]
        public void TheShapeIsDeterministic()
        {
            // ⛔ The same seed must always give the same body. A re-rolled shape would mean
            // two players are rolling physically different characters, and would make a
            // reported feel problem impossible to reproduce.
            Vector2[] first = BlobShape.BuildBase(Vertices, 0.06f, Seed);
            Vector2[] second = BlobShape.BuildBase(Vertices, 0.06f, Seed);

            for (int i = 0; i < first.Length; i++)
            {
                Assert.That(second[i].x, Is.EqualTo(first[i].x).Within(1e-6f));
                Assert.That(second[i].y, Is.EqualTo(first[i].y).Within(1e-6f));
            }
        }

        [Test]
        public void DifferentSeedsGiveDifferentBodies()
        {
            Vector2[] a = BlobShape.BuildBase(Vertices, 0.06f, Seed);
            Vector2[] b = BlobShape.BuildBase(Vertices, 0.06f, Seed + 1);

            bool anyDifferent = false;
            for (int i = 0; i < a.Length; i++)
            {
                if ((a[i] - b[i]).sqrMagnitude > 1e-6f)
                {
                    anyDifferent = true;
                    break;
                }
            }

            Assert.That(anyDifferent, Is.True, "the seed does nothing");
        }

        [Test]
        public void DeformingSquashesAlongTheAxisAndStretchesAcrossIt()
        {
            Vector2[] basePoints = BlobShape.BuildBase(Vertices, 0f, Seed);
            var into = new Vector2[basePoints.Length];

            // Squash along X.
            BlobShape.Deform(basePoints, into, 0f, new Vector2(0.5f, 1.25f), Vector2.zero);

            float widest = 0f;
            float tallest = 0f;
            foreach (Vector2 p in into)
            {
                widest = Mathf.Max(widest, Mathf.Abs(p.x));
                tallest = Mathf.Max(tallest, Mathf.Abs(p.y));
            }

            Assert.That(widest, Is.EqualTo(0.5f).Within(0.02f));
            Assert.That(tallest, Is.EqualTo(1.25f).Within(0.02f));
        }

        [Test]
        public void TheSquashAxisIsHonouredAtAnyAngle()
        {
            // The axis arrives as a contact normal and can point anywhere. An implementation
            // that only handled the cardinal directions -- squashing vertically whatever it
            // was told -- would pass a vertical-landing test and be wrong on every slope,
            // wall and corner (§10 requires all four).
            Vector2[] basePoints = BlobShape.BuildBase(Vertices, 0f, Seed);
            var into = new Vector2[basePoints.Length];

            float axis = Mathf.PI * 0.25f;   // 45 degrees
            BlobShape.Deform(basePoints, into, axis, new Vector2(0.5f, 1f), Vector2.zero);

            // The most-compressed point should lie along the axis, not along X or Y.
            var axisDirection = new Vector2(Mathf.Cos(axis), Mathf.Sin(axis));
            float extentAlongAxis = 0f;
            float extentAcrossAxis = 0f;

            foreach (Vector2 p in into)
            {
                extentAlongAxis = Mathf.Max(extentAlongAxis, Mathf.Abs(Vector2.Dot(p, axisDirection)));
                extentAcrossAxis = Mathf.Max(extentAcrossAxis,
                    Mathf.Abs(Vector2.Dot(p, new Vector2(-axisDirection.y, axisDirection.x))));
            }

            Assert.That(extentAlongAxis, Is.EqualTo(0.5f).Within(0.02f));
            Assert.That(extentAcrossAxis, Is.EqualTo(1f).Within(0.02f));
        }

        [Test]
        public void LandingKeepsTheContactEdgeAndFlattensDownOntoIt()
        {
            // ⭐ Owner, 2026-09-10: "When the player lands from above, the deformation center
            // should move downward toward the contact point."
            //
            // A platform underfoot pushes UP, so the axis is +Y and the contact is at the
            // BOTTOM of the body. Squashing about the CENTRE lifts that bottom edge off the
            // surface — the ball would flatten by shrinking into itself and then have to fall
            // again. Squashing about the CONTACT keeps the bottom where it is and brings the
            // top down, which is what a landing balloon does.
            Vector2[] basePoints = BlobShape.BuildBase(Vertices, 0f, Seed);
            var aboutCentre = new Vector2[basePoints.Length];
            var aboutContact = new Vector2[basePoints.Length];

            float axis = Mathf.PI * 0.5f;                   // pushed upward
            var squash = new Vector2(0.6f, 1.2f);

            BlobShape.Deform(basePoints, aboutCentre, axis, squash, Vector2.zero);
            BlobShape.Deform(basePoints, aboutContact, axis, squash,
                BlobShape.ContactPivot(axis, 1f, 1f));

            float baseBottom = Lowest(basePoints);

            Assert.That(Lowest(aboutCentre), Is.GreaterThan(baseBottom + 0.1f),
                "squashing about the centre should lift the contact edge — if it does not, " +
                "this test is no longer comparing the two behaviours");

            Assert.That(Lowest(aboutContact), Is.EqualTo(baseBottom).Within(0.02f),
                "the contact edge moved; Smiley would be hovering or sunk while squashed");

            Assert.That(Highest(aboutContact), Is.LessThan(Highest(aboutCentre)),
                "the far side did not come down, so he is not flattening onto the surface");
        }

        [Test]
        public void ADeformedOutlineNeverGrowsBeyondItsBaseShape()
        {
            // ⛔ THE INVARIANT BEHIND D-020: a rebuild may only remove material, never add it.
            //
            // A collider that grows into a platform gets pushed out by the solver, and a shape
            // that grows a little every frame is a slow impulse — on hardware that levitated
            // Smiley off the Start Area. Clamping makes the overlap impossible to create.
            Vector2[] basePoints = BlobShape.BuildBase(Vertices, 0.06f, Seed);
            var into = new Vector2[basePoints.Length];

            // A hard squash with a big cross-axis bulge: the worst case for growth.
            BlobShape.Deform(basePoints, into, Mathf.PI * 0.5f, new Vector2(0.25f, 1.9f),
                BlobShape.ContactPivot(Mathf.PI * 0.5f, 1f, 0.85f));

            bool grewBeforeClamping = false;
            for (int i = 0; i < basePoints.Length; i++)
            {
                if (into[i].magnitude > basePoints[i].magnitude + 1e-4f)
                {
                    grewBeforeClamping = true;
                    break;
                }
            }

            Assert.That(grewBeforeClamping, Is.True,
                "this deformation no longer grows, so the clamp below is not being tested");

            BlobShape.ClampWithinBase(basePoints, into);

            for (int i = 0; i < basePoints.Length; i++)
            {
                Assert.That(into[i].magnitude,
                    Is.LessThanOrEqualTo(basePoints[i].magnitude + 1e-4f),
                    $"vertex {i} still reaches outside the base shape");
            }
        }

        [Test]
        public void ClampingLeavesAShrunkenOutlineAlone()
        {
            // The clamp must only ever pull vertices in. A squash that already fits inside the
            // base shape has to pass through untouched, or the deformation would be quietly
            // eaten by its own safety net.
            Vector2[] basePoints = BlobShape.BuildBase(Vertices, 0.06f, Seed);
            var into = new Vector2[basePoints.Length];

            BlobShape.Deform(basePoints, into, 0f, new Vector2(0.7f, 0.9f), Vector2.zero);

            var before = (Vector2[])into.Clone();
            BlobShape.ClampWithinBase(basePoints, into);

            for (int i = 0; i < basePoints.Length; i++)
            {
                Assert.That(into[i].x, Is.EqualTo(before[i].x).Within(1e-5f));
                Assert.That(into[i].y, Is.EqualTo(before[i].y).Within(1e-5f));
            }
        }

        [Test]
        public void TheContactPivotFollowsTheDirectionOfTheForce()
        {
            // "Likewise, for contacts from other directions, the deformation center should be
            // positioned close to the actual collision/contact location." A wall on the right
            // pushes LEFT, so the contact is on Smiley's right-hand side.
            Vector2 fromBelow = BlobShape.ContactPivot(Mathf.PI * 0.5f, 1f, 1f);
            Assert.That(fromBelow.y, Is.EqualTo(-1f).Within(1e-3f), "contact should be underneath");
            Assert.That(fromBelow.x, Is.EqualTo(0f).Within(1e-3f));

            Vector2 fromTheRight = BlobShape.ContactPivot(Mathf.PI, 1f, 1f);
            Assert.That(fromTheRight.x, Is.EqualTo(1f).Within(1e-3f), "contact should be on the right");
            Assert.That(fromTheRight.y, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void ZeroBiasIsTheOldCentredBehaviour()
        {
            // The bias is one value, and turning it to zero must give back squashing about the
            // centre — both as an escape hatch and so its effect stays legible.
            Assert.That(BlobShape.ContactPivot(Mathf.PI * 0.5f, 1f, 0f),
                Is.EqualTo(Vector2.zero));
        }

        private static float Lowest(Vector2[] points)
        {
            float lowest = float.MaxValue;
            foreach (Vector2 p in points)
            {
                lowest = Mathf.Min(lowest, p.y);
            }

            return lowest;
        }

        private static float Highest(Vector2[] points)
        {
            float highest = float.MinValue;
            foreach (Vector2 p in points)
            {
                highest = Mathf.Max(highest, p.y);
            }

            return highest;
        }

        [Test]
        public void AnUndeformedCallLeavesTheOutlineAlone()
        {
            Vector2[] basePoints = BlobShape.BuildBase(Vertices, 0.06f, Seed);
            var into = new Vector2[basePoints.Length];

            BlobShape.Deform(basePoints, into, 1.234f, Vector2.one, Vector2.zero);

            for (int i = 0; i < basePoints.Length; i++)
            {
                Assert.That(into[i].x, Is.EqualTo(basePoints[i].x).Within(1e-4f));
                Assert.That(into[i].y, Is.EqualTo(basePoints[i].y).Within(1e-4f));
            }
        }

        /// <summary>Applies the deformation map to a point, the way the transforms compose.</summary>
        private static Vector2 Map(Vector2 point, float axisRadians, Vector2 scale)
        {
            float cos = Mathf.Cos(axisRadians);
            float sin = Mathf.Sin(axisRadians);

            var local = new Vector2(
                point.x * cos + point.y * sin,
                -point.x * sin + point.y * cos);

            local.x *= scale.x;
            local.y *= scale.y;

            return new Vector2(
                local.x * cos - local.y * sin,
                local.x * sin + local.y * cos);
        }

        [Test]
        public void ThePivotIsTheOnePointThatDoesNotMove()
        {
            // ⭐ THE WHOLE CONTENT OF THE FIX (D-066), stated as the property it guarantees.
            //
            // Scaling about a pivot means every other point moves and that one does not. The
            // owner's report was that Smiley's visual "separates from the ground" — which is
            // this property failing for the point resting on the platform.
            //
            // ⚠ Swept over pivots that are NOT along the squash axis, because that is the case
            // the old shortcut got wrong and the only case that matters here.
            foreach (float axis in new[] { 0f, 0.4f, Mathf.PI * 0.5f, 2.3f, Mathf.PI })
            {
                foreach (var scale in new[]
                    { new Vector2(0.7f, 1.2f), new Vector2(1.3f, 0.8f), new Vector2(0.5f, 1.5f) })
                {
                    foreach (var pivot in new[]
                        { new Vector2(0f, -0.45f), new Vector2(0.3f, -0.4f), new Vector2(-0.5f, 0f) })
                    {
                        Vector2 offset = BlobShape.PivotOffset(pivot, axis, scale);
                        Vector2 landed = Map(pivot, axis, scale) + offset;

                        Assert.That(Vector2.Distance(landed, pivot), Is.LessThan(1e-4f),
                            $"axis {axis:0.00}, scale {scale}, pivot {pivot}: the pivot moved to " +
                            $"{landed} — every other point may move, that one may not");
                    }
                }
            }
        }

        [Test]
        public void ItStillAgreesWithTheShortcutWhenThePivotIsOnTheAxis()
        {
            // ⛔ The impact deformation has always pivoted on the contact NORMAL, which IS the
            // squash axis — so the old closed form was right for it, and this fix must not have
            // changed §11's landing squash by a hair.
            foreach (float axis in new[] { 0f, 0.9f, Mathf.PI * 0.5f, 3.0f })
            {
                var scale = new Vector2(0.6f, 1.34f);
                Vector2 pivot = BlobShape.ContactPivot(axis, 0.5f, 0.85f);

                Vector2 general = BlobShape.PivotOffset(pivot, axis, scale);
                Vector2 shortcut = pivot * (1f - scale.x);

                Assert.That(Vector2.Distance(general, shortcut), Is.LessThan(1e-4f),
                    $"axis {axis:0.00}: the general form gives {general} where the shortcut " +
                    $"gives {shortcut}, so §11's landing squash has moved");
            }
        }

        [Test]
        public void NoDeformationMeansNoOffset()
        {
            Vector2 offset = BlobShape.PivotOffset(
                new Vector2(0f, -0.45f), 1.1f, Vector2.one);

            Assert.That(offset.magnitude, Is.LessThan(1e-5f),
                "an undeformed Smiley is being moved on the screen");
        }
    }
}