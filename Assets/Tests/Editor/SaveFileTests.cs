using NUnit.Framework;
using Smiley.Save;

namespace Smiley.Tests
{
    /// <summary>§21's save format, without touching the disk.</summary>
    public sealed class SaveFileTests
    {
        private static SaveData Sample()
        {
            var data = new SaveData
            {
                SmileyX = 1.25f,
                SmileyY = -3.5f,
                VelocityX = 2f,
                VelocityY = -7.5f,
                Rotation = 41f,
                AngularVelocity = -120f,
                DeformMagnitude = 0.32f,
                DeformAxisRadians = 1.57f,
                CompletedAtLeastOnce = true,
            };

            data.Platforms.Add(new PlatformState
            {
                Name = "Platform_05_Disappearing",
                Present = false,
                Remaining = 0f,
                RespawnRemaining = 1.8f,
                Running = false,
                ContactHeld = 0f,
            });

            return data;
        }

        [Test]
        public void ASaveSurvivesARoundTrip()
        {
            SaveData restored = SaveFile.Parse(SaveFile.Serialise(Sample()));

            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.SmileyX, Is.EqualTo(1.25f).Within(1e-4f));
            Assert.That(restored.SmileyY, Is.EqualTo(-3.5f).Within(1e-4f));
            Assert.That(restored.VelocityY, Is.EqualTo(-7.5f).Within(1e-4f));
            Assert.That(restored.Rotation, Is.EqualTo(41f).Within(1e-4f));
            Assert.That(restored.AngularVelocity, Is.EqualTo(-120f).Within(1e-4f));
            Assert.That(restored.CompletedAtLeastOnce, Is.True);
        }

        [Test]
        public void TheDeformationSurvives()
        {
            // §21: "If saving during deformation, save the current deformation state."
            SaveData restored = SaveFile.Parse(SaveFile.Serialise(Sample()));

            Assert.That(restored.DeformMagnitude, Is.EqualTo(0.32f).Within(1e-4f));
            Assert.That(restored.DeformAxisRadians, Is.EqualTo(1.57f).Within(1e-4f));
        }

        [Test]
        public void PlatformStateSurvivesIncludingWhetherItIsPaused()
        {
            // ⛔ §21 lists the remaining time and whether the countdown is running as SEPARATE
            // requirements, and they are separate facts: 1.4s paused and 1.4s ticking are
            // different situations, and restoring only the number converts one into the other.
            SaveData restored = SaveFile.Parse(SaveFile.Serialise(Sample()));

            Assert.That(restored.Platforms, Has.Count.EqualTo(1));

            PlatformState platform = restored.Platforms[0];
            Assert.That(platform.Name, Is.EqualTo("Platform_05_Disappearing"));
            Assert.That(platform.Present, Is.False);
            Assert.That(platform.RespawnRemaining, Is.EqualTo(1.8f).Within(1e-4f));
            Assert.That(platform.Running, Is.False);
        }

        [Test]
        public void APausedCountdownIsDistinguishableFromARunningOne()
        {
            var paused = new SaveData();
            paused.Platforms.Add(new PlatformState { Name = "P", Present = true, Remaining = 1.4f, Running = false });

            var running = new SaveData();
            running.Platforms.Add(new PlatformState { Name = "P", Present = true, Remaining = 1.4f, Running = true });

            Assert.That(SaveFile.Parse(SaveFile.Serialise(paused)).Platforms[0].Running, Is.False);
            Assert.That(SaveFile.Parse(SaveFile.Serialise(running)).Platforms[0].Running, Is.True);
        }

        [Test]
        public void ATruncatedSaveIsRefused()
        {
            // ⛔ THE FAILURE THIS FORMAT IS SHAPED AROUND.
            //
            // §21 saves during play, so a kill can land mid-write. A file cut short may still
            // parse into an object full of plausible zeros — a Smiley at the origin with no
            // platforms — and restoring that is worse than having no save at all. The end
            // marker is written last, so its absence means the write never finished.
            string text = SaveFile.Serialise(Sample());
            string truncated = text.Substring(0, text.Length / 2);

            Assert.That(SaveFile.Parse(truncated), Is.Null);
        }

        [Test]
        public void ASaveMissingItsEndMarkerIsRefused()
        {
            // The same failure in its nastier form: syntactically valid JSON that simply never
            // got the marker. Parsing alone cannot tell this from a good save.
            string text = SaveFile.Serialise(Sample()).Replace(SaveData.Complete, "");

            Assert.That(SaveFile.Parse(text), Is.Null);
        }

        [Test]
        public void RubbishIsRefusedRatherThanThrowing()
        {
            // An unreadable save is an ordinary situation — a first run, a half-written file, a
            // file someone edited — and none of those is an error the player should ever see.
            Assert.That(SaveFile.Parse(null), Is.Null);
            Assert.That(SaveFile.Parse(""), Is.Null);
            Assert.That(SaveFile.Parse("   "), Is.Null);
            Assert.That(SaveFile.Parse("not json at all"), Is.Null);
            Assert.That(SaveFile.Parse("{ \"Version\": "), Is.Null);
        }

        [Test]
        public void ASaveFromAFutureVersionIsRefused()
        {
            // Guessing at fields this build does not know about is worse than starting fresh.
            SaveData future = Sample();
            future.Version = 999;

            Assert.That(SaveFile.Parse(SaveFile.Serialise(future)), Is.Null);
        }
    }
}
