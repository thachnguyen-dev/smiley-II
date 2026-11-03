using NUnit.Framework;
using Smiley.Audio;

namespace Smiley.Tests
{
    /// <summary>§27's mixing rules, as arithmetic.</summary>
    public sealed class AudioLevelsTests
    {
        private const float Resting = 0.2f;
        private const float Loudest = 14f;

        [Test]
        public void AStrongerImpactIsLouder()
        {
            // §27, in its own words: "light impact → lower volume, strong impact → higher volume".
            float light = AudioLevels.ImpactVolume(1f, Resting, Loudest);
            float strong = AudioLevels.ImpactVolume(10f, Resting, Loudest);

            Assert.That(light, Is.GreaterThan(0f));
            Assert.That(strong, Is.GreaterThan(light));
        }

        [Test]
        public void ARestingSmileyMakesNoSoundAtAll()
        {
            // ⛔ THE CLAUSE THIS PROTECTS IS §27's "NO ROLLING SFX".
            //
            // A Smiley sitting on a platform delivers a contact impulse every single step — his
            // own weight, held up by the surface. Without taking that off the bottom he would
            // rattle continuously while doing nothing, and the rule would be broken by a
            // character standing still.
            Assert.That(AudioLevels.ImpactVolume(Resting, Resting, Loudest), Is.EqualTo(0f));
            Assert.That(AudioLevels.ImpactVolume(Resting * 0.5f, Resting, Loudest), Is.EqualTo(0f));
        }

        [Test]
        public void TheVolumeIsCappedAtFull()
        {
            Assert.That(AudioLevels.ImpactVolume(Loudest, Resting, Loudest),
                Is.LessThanOrEqualTo(1f));
            Assert.That(AudioLevels.ImpactVolume(Loudest * 50f, Resting, Loudest),
                Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void QuietImpactsAreSpreadOutRatherThanBunchedAtTheBottom()
        {
            // ⚠ Loudness is heard roughly logarithmically, so a straight line makes everything
            // above a gentle tap sound the same. §27 asks that light and strong be
            // distinguishable, which is a claim about hearing rather than about arithmetic.
            float quarter = AudioLevels.ImpactVolume(Resting + Loudest * 0.25f, Resting, Loudest);

            Assert.That(quarter, Is.GreaterThan(0.25f),
                $"a quarter-strength impact plays at {quarter:0.00} — on a straight line it " +
                "would be 0.25, and every gentle landing would be nearly inaudible");
        }

        [Test]
        public void TheGuidesToneRisesWithTheDrag()
        {
            // §27: the guide "has sound feedback. Sound changes according to drag force."
            float slack = AudioLevels.GuidePitch(0f, 0.7f, 1.6f);
            float full = AudioLevels.GuidePitch(1f, 0.7f, 1.6f);

            Assert.That(slack, Is.EqualTo(0.7f).Within(1e-4f));
            Assert.That(full, Is.EqualTo(1.6f).Within(1e-4f));
            Assert.That(AudioLevels.GuidePitch(0.5f, 0.7f, 1.6f), Is.GreaterThan(slack));
        }

        [Test]
        public void ADragBeyondTheMaximumDoesNotKeepRising()
        {
            // §6 clamps the drag itself, and the sound must not claim otherwise.
            Assert.That(AudioLevels.GuidePitch(4f, 0.7f, 1.6f), Is.EqualTo(1.6f).Within(1e-4f));
        }

        [Test]
        public void ASettingOfZeroIsSilentWhateverTheCue()
        {
            // ⛔ A player who turns the sound off must hear NOTHING. Multiplying is what
            // guarantees it: no cue, however loud in itself, survives a slider at the bottom.
            Assert.That(AudioLevels.Mix(1f, 0f), Is.EqualTo(0f));
            Assert.That(AudioLevels.Mix(0.5f, 0f), Is.EqualTo(0f));
        }

        [Test]
        public void TheSettingScalesEverythingElse()
        {
            Assert.That(AudioLevels.Mix(1f, 0.5f), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(AudioLevels.Mix(0.5f, 0.5f), Is.EqualTo(0.25f).Within(1e-4f));
            Assert.That(AudioLevels.Mix(2f, 2f), Is.EqualTo(1f), "nothing may exceed full volume");
        }
    }

    /// <summary>§26's settings file.</summary>
    public sealed class SettingsFileTests
    {
        [Test]
        public void SettingsSurviveARoundTrip()
        {
            var settings = new GameSettings { MusicVolume = 0.25f, SfxVolume = 0.6f };

            GameSettings back = SettingsFile.Parse(SettingsFile.Serialise(settings));

            Assert.That(back.MusicVolume, Is.EqualTo(0.25f).Within(1e-4f));
            Assert.That(back.SfxVolume, Is.EqualTo(0.6f).Within(1e-4f));
        }

        [Test]
        public void RubbishGivesTheDefaultsRatherThanAnError()
        {
            // ⓘ A player who has never opened Settings has no file at all, which is the
            // ordinary case rather than a fault. ⚠ Deliberately UNLIKE the save file, which
            // refuses anything it cannot trust: losing a volume costs one gesture, and losing a
            // climb does not.
            foreach (string text in new[] { null, "", "   ", "not json", "{ \"Version\": " })
            {
                GameSettings settings = SettingsFile.Parse(text);

                Assert.That(settings, Is.Not.Null, $"'{text}' produced no settings at all");
                Assert.That(settings.MusicVolume, Is.InRange(0f, 1f));
                Assert.That(settings.SfxVolume, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void ValuesFromOutsideTheRangeAreBroughtBackIn()
        {
            GameSettings settings = SettingsFile.Parse(
                SettingsFile.Serialise(new GameSettings { MusicVolume = 9f, SfxVolume = -4f }));

            Assert.That(settings.MusicVolume, Is.EqualTo(1f));
            Assert.That(settings.SfxVolume, Is.EqualTo(0f));
        }

        [Test]
        public void ASettingsFileFromAFutureVersionFallsBackToTheDefaults()
        {
            var future = new GameSettings { Version = 999, MusicVolume = 0.1f };

            GameSettings settings = SettingsFile.Parse(SettingsFile.Serialise(future));

            Assert.That(settings.MusicVolume, Is.Not.EqualTo(0.1f).Within(1e-4f));
        }

        [Test]
        public void TheSettingsAreNotStoredWithTheSave()
        {
            // ⛔ §25: "Settings are NOT reset." That is guaranteed structurally rather than by
            // remembering — they are a different file, which SaveFile.DeleteAll does not know
            // about and cannot delete. ⓘ Asserted as paths so the guarantee is visible here and
            // not only in a scene test.
            Assert.That(SettingsFile.Path, Is.Not.EqualTo(Smiley.Save.SaveFile.Path));
            Assert.That(SettingsFile.Path, Is.Not.EqualTo(Smiley.Save.SaveFile.BackupPath));
        }
    }
}
