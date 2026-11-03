using System.Collections;
using NUnit.Framework;
using Smiley.Gameplay;
using Smiley.View;
using UnityEngine;
using UnityEngine.TestTools;

namespace Smiley.Tests
{
    /// <summary>Smiley's idle animation system in the booted scene (D-086).</summary>
    public sealed class IdleAnimatorSceneTests
    {
        [UnitySetUp]
        public IEnumerator Load()
        {
            yield return GameSceneFixture.LoadFresh();
        }

        private static IdleAnimator Find()
        {
            var controller = Object.FindFirstObjectByType<SmileyController>();
            var idle = controller.GetComponentInChildren<IdleAnimator>();
            Assert.That(idle, Is.Not.Null, "Smiley has no IdleAnimator — D-086 is not wired");
            return idle;
        }

        /// <summary>A clip that nudges the object's z a little, so it is a real animation with a length.</summary>
        private static AnimationClip Clip(string name, float seconds)
        {
            var clip = new AnimationClip { name = name };
            clip.SetCurve("", typeof(Transform), "localPosition.z", AnimationCurve.Linear(0f, 0f, seconds, 0.001f));
            return clip;
        }

        [UnityTest]
        public IEnumerator TheIdleAnimatorSitsOnTheArtInsideTheDeformerFrameNotOnTheBody()
        {
            IdleAnimator idle = Find();
            var controller = Object.FindFirstObjectByType<SmileyController>();

            // D-092: Art, at the bottom of the circle, under the counter-rotating Sprite holder.
            Assert.That(idle.gameObject.name, Is.EqualTo("Art"));
            Assert.That(idle.transform.parent.name, Is.EqualTo("Sprite"));
            Assert.That(idle.transform.localPosition.y, Is.LessThan(0f), "Art must sit below the holder's origin — at the feet");
            Assert.That(idle.GetComponent<SpriteRenderer>(), Is.Not.Null, "it must animate the object that draws him");
            Assert.That(idle.GetComponent<Animator>(), Is.Not.Null);
            Assert.That(idle.GetComponent<Rigidbody2D>(), Is.Null, "never on the physics body");
            Assert.That(controller.GetComponent<IdleAnimator>(), Is.Null, "never on the root");
            Assert.That(idle.GetComponent<Animator>().runtimeAnimatorController, Is.Null,
                "a controller beside the playable graph would fight it for the sprite");

            yield return null;
        }

        [UnityTest]
        public IEnumerator WithNoClipsAssignedHeHoldsStillAndNothingBreaks()
        {
            // Q-053: the clips are the owner's; with none he holds still.
            IdleAnimator idle = Find();
            idle.SetClips(new AnimationClip[0]);

            yield return null;
            yield return null;

            Assert.That(idle.IsPlaying, Is.False);
            Assert.That(idle.Current, Is.EqualTo(-1));
        }

        [UnityTest]
        public IEnumerator TheOwnersClipsActuallyChangeThePicture()
        {
            // ⛔ D-088: the first device build after the owner assigned six clips showed the
            // placeholder disc — the list had been emptied by a scene rebuild, and the only test
            // checked the graph was "playing", not that anything reached the renderer. This reads
            // the sprite the game draws. Ignored, not failed, when the owner has assigned nothing.
            IdleAnimator idle = Find();

            if (idle.Count == 0)
            {
                Assert.Ignore("no idle clips assigned yet (Q-053)");
            }

            var renderer = idle.GetComponent<SpriteRenderer>();

            yield return null;
            yield return null;

            // The builder gives the renderer the placeholder disc; a playing sprite clip
            // replaces it on the first evaluated frame. ⓘ Not "the sprite changed between two
            // frames": batch-mode time barely advances, so one frame of a 2.7 s clip is the same picture.
            Assert.That(idle.IsPlaying, Is.True);
            Assert.That(renderer.sprite, Is.Not.Null);
            Assert.That(renderer.sprite.name, Does.Not.StartWith("Placeholder"),
                "the idle clip is playing and the renderer still shows the placeholder — the clip is not reaching it");
        }

        [UnityTest]
        public IEnumerator EveryFrameOfEveryIdleClipKeepsHisFeetOnTheColliderBottom()
        {
            // ⭐ D-092/D-093, the owner's requirement measured directly: for every clip, at every
            // twelfth of a second, the sprite's pivot — its lowest SOLID row, placed there by the
            // build — sits at the bottom of the physics circle, and nothing drawn hangs more
            // than a shadow's worth below it. A frame drawn higher on its canvas used to float
            // here by up to 0.21 units; the faint rows under the body then hid another 0.17.
            IdleAnimator idle = Find();

            if (idle.Count == 0)
            {
                Assert.Ignore("no idle clips assigned yet (Q-053)");
            }

            var body = Object.FindFirstObjectByType<SmileyController>().GetComponent<Rigidbody2D>();
            var deformer = body.GetComponent<SmileyDeformer>();
            var renderer = idle.GetComponent<SpriteRenderer>();
            var worst = new System.Collections.Generic.List<string>();

            // Hold him still: no gravity drop, no squash between readings.
            body.gravityScale = 0f;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            deformer.RestoreDeformation(0f, 0f);

            const float Tolerance = 0.06f;

            for (int i = 0; i < idle.Count; i++)
            {
                if (idle.Clips[i] == null) continue;

                idle.Play(i);
                float length = idle.Clips[i].length;

                for (float t = 0f; t <= length + 1e-3f; t += 1f / 12f)
                {
                    idle.Scrub(t == 0f ? 0f : 1f / 12f);
                    yield return null;

                    float feet = body.position.y - 0.5f;
                    float pivot = renderer.transform.position.y;
                    float drawn = renderer.bounds.min.y;
                    Sprite sprite = renderer.sprite;

                    // The pivot must be ON the feet, and it must be a measured solid row — not
                    // the rect bottom (y = 0 in pixels) unless the build found the body there.
                    bool pivotOnFeet = Mathf.Abs(pivot - feet) <= Tolerance;
                    bool nothingFarBelow = drawn >= feet - 0.25f;
                    bool anchoredByBuild = sprite != null && sprite.pivot.y >= -0.01f && sprite.pivot.y < sprite.rect.height;

                    if (!pivotOnFeet || !nothingFarBelow || !anchoredByBuild)
                    {
                        worst.Add(idle.Clips[i].name + " @ " + t.ToString("0.00") + "s: pivot " + pivot.ToString("0.000")
                                  + " drawn bottom " + drawn.ToString("0.000") + " vs feet " + feet.ToString("0.000")
                                  + " (sprite " + (sprite != null ? sprite.name + " pivot px " + sprite.pivot : "none") + ")");
                    }
                }
            }

            body.gravityScale = 1f;

            Assert.That(worst, Is.Empty, "frames whose lowest pixel is not on the collider bottom:\n" + string.Join("\n", worst));
        }

        [UnityTest]
        public IEnumerator WithClipsAssignedOnePlaysAndTheNextIsAlwaysDifferent()
        {
            IdleAnimator idle = Find();
            idle.SetClips(new[] { Clip("IdleA", 0.5f), Clip("IdleB", 0.5f), Clip("IdleC", 0.5f) });

            yield return null;
            yield return null;

            Assert.That(idle.IsPlaying, Is.True, "clips were assigned and nothing plays");
            Assert.That(idle.Current, Is.InRange(0, 2));

            // ⚠ Driven by hand rather than by the clock: batch-mode frames barely advance time
            // (D-062's lesson), so the cycle is stepped with the same call Update makes.
            int previous = idle.Current;

            for (int cycle = 0; cycle < 40; cycle++)
            {
                idle.PlayNext();
                Assert.That(idle.Current, Is.Not.EqualTo(previous), $"cycle {cycle} replayed clip {previous}");
                Assert.That(idle.IsPlaying, Is.True);
                previous = idle.Current;
            }
        }

        [UnityTest]
        public IEnumerator NoGameplayEventReachesTheIdleAnimator()
        {
            // ⛔ Owner: "should not be triggered by collisions, jumping, landing, or other gameplay
            // events." Asserted structurally: the component references nothing that could tell it
            // one happened — no controller, no contact, no body, no flow.
            IdleAnimator idle = Find();

            foreach (var field in typeof(IdleAnimator).GetFields(
                         System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public))
            {
                System.Type t = field.FieldType;
                bool gameplay = t == typeof(SmileyController) || t == typeof(PlatformContact)
                                || t == typeof(Rigidbody2D) || t == typeof(Collider2D)
                                || t.Namespace == "Smiley.Gameplay" || t.Namespace == "Smiley.Flow";

                Assert.That(gameplay, Is.False, $"IdleAnimator holds a {t.Name} ('{field.Name}') — a way for gameplay to drive it");
            }

            yield return null;
        }
    }
}
