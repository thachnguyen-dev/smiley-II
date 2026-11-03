using System;
using NUnit.Framework;
using Smiley.View;

namespace Smiley.Tests
{
    /// <summary>The owner's idle rule as arithmetic (D-086): random, and never the same twice running.</summary>
    public sealed class IdlePickerTests
    {
        [Test]
        public void TheNextIdleIsNeverTheOneThatJustPlayed()
        {
            var random = new Random(20260911);
            int previous = -1;

            for (int i = 0; i < 2000; i++)
            {
                int next = IdlePicker.Next(4, previous, random);

                Assert.That(next, Is.InRange(0, 3));
                Assert.That(next, Is.Not.EqualTo(previous), $"cycle {i} repeated clip {previous}");
                previous = next;
            }
        }

        [Test]
        public void EveryOtherIdleIsEquallyLikely()
        {
            // "Randomly choose another" — not a fixed order, and no clip starved. From clip 0 of
            // four, each of 1, 2, 3 should come up about a third of the time.
            var random = new Random(7);
            var counts = new int[4];

            for (int i = 0; i < 6000; i++)
            {
                counts[IdlePicker.Next(4, 0, random)]++;
            }

            Assert.That(counts[0], Is.Zero);

            for (int clip = 1; clip < 4; clip++)
            {
                Assert.That(counts[clip], Is.InRange(1700, 2300), $"clip {clip} came up {counts[clip]} of 6000");
            }
        }

        [Test]
        public void OneIdlePlaysAgainAndNoneIsNothing()
        {
            var random = new Random(1);

            Assert.That(IdlePicker.Next(1, 0, random), Is.EqualTo(0));
            Assert.That(IdlePicker.Next(1, -1, random), Is.EqualTo(0));
            Assert.That(IdlePicker.Next(0, -1, random), Is.EqualTo(-1));
        }

        [Test]
        public void TheFirstPickWithNoPreviousCanBeAnyClip()
        {
            var random = new Random(3);
            var seen = new bool[3];

            for (int i = 0; i < 300; i++)
            {
                seen[IdlePicker.Next(3, -1, random)] = true;
            }

            Assert.That(seen, Is.All.True);
        }
    }
}
