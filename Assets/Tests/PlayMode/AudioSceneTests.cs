using System.Collections;
using System.Linq;
using NUnit.Framework;
using Smiley.Audio;
using Smiley.Gameplay;
using Smiley.Save;
using UnityEngine;
using UnityEngine.TestTools;

namespace Smiley.Tests
{
    /// <summary>
    /// §27's audio in the running game, and §26's settings.
    /// </summary>
    /// <remarks>
    /// ⛔ <c>AudioLevelsTests</c> pins the curves and would stay green if nothing ever played a
    /// sound — the failure this project has hit five times. These read the objects the game
    /// builds. ⓘ Audio cannot be HEARD in a headless run, so what is asserted is that the right
    /// sources exist, are playing, and are at the volumes the rules say.
    /// </remarks>
    public sealed class AudioSceneTests
    {
        [UnitySetUp]
        public IEnumerator Load()
        {
            yield return GameSceneFixture.LoadFresh();
        }

        private static GameAudio Audio()
        {
            var audio = Object.FindFirstObjectByType<GameAudio>();
            Assert.That(audio, Is.Not.Null, "no GameAudio in the scene, so §27 is unreachable");
            return audio;
        }

        [UnityTest]
        public IEnumerator TheDragCuePlaysOncePerDragAndNeverLoops()
        {
            // ⭐ Owner, 2026-09-12 (D-100): "SFX should not loop. Each SFX should play once per
            // corresponding event. In particular, Drag.wav should not continuously loop."
            GameAudio audio = Audio();

            // SmileyAudio relays the controller's (idle) drag state every frame; silence it so
            // this test is the only caller and a held drag reads as held.
            Object.FindFirstObjectByType<SmileyAudio>().enabled = false;

            Assert.That(audio.GuideLoops, Is.False, "the drag source is set to loop");

            for (int frame = 0; frame < 20; frame++)
            {
                audio.SetGuide(true, frame / 20f);
                yield return null;
            }

            Assert.That(audio.GuideStarts, Is.EqualTo(1), "a held drag restarted the cue");

            audio.SetGuide(false, 0f);
            yield return null;
            audio.SetGuide(true, 0.3f);
            yield return null;

            Assert.That(audio.GuideStarts, Is.EqualTo(2), "a new drag is a new event and gets its cue");
        }

        [UnityTest]
        public IEnumerator TheCameraShowsTheTunedWidthOnThisScreen()
        {
            // D-099: framed by width; the height follows the aspect. D-112: the owner set 8.5.
            var camera = Object.FindFirstObjectByType<Camera>();
            var framing = camera.GetComponent<Smiley.View.CameraFraming>();

            Assert.That(framing, Is.Not.Null, "no CameraFraming on the camera");
            Assert.That(camera.orthographicSize * camera.aspect * 2f, Is.EqualTo(8.5f).Within(0.01f),
                "the visible width is not the owner's 8.5 units (D-112)");

            yield return null;
        }

        [UnityTest]
        public IEnumerator TheSceneHasExactlyOneEar()
        {
            // ⛔ D-097: no AudioListener existed in either scene, so every source played into
            // silence while isPlaying said true. A scene with no listener is mute; one with two
            // logs an error every frame.
            AudioListener[] ears = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);

            Assert.That(ears.Length, Is.EqualTo(1), "a scene without exactly one AudioListener plays nothing anyone can hear");
            Assert.That(ears[0].enabled, Is.True);
            Assert.That(ears[0].GetComponent<Camera>(), Is.Not.Null, "the ear should be on the camera");

            yield return null;
        }

        [UnityTest]
        public IEnumerator TheSceneCarriesTheOwnersFourSoundsNotThePlaceholders()
        {
            // ⭐ Owner, 2026-09-12 (D-096): four files in D:sound, one per slot. This reads the
            // clips the scene actually wired — a placeholder here means a slot fell back.
            GameAudio audio = Audio();
            string report = audio.ClipReport;

            Assert.That(report, Is.EqualTo("music=Music landing=Landing launch=Launch drag=Drag"), report);
            Assert.That(report, Does.Not.Contain("Placeholder"));

            yield return null;
        }

        [UnityTest]
        public IEnumerator TheMusicPlaysThroughoutGameplay()
        {
            // §27: "Background music plays throughout gameplay."
            yield return null;

            Assert.That(Audio().MusicIsPlaying, Is.True,
                "the music is not playing, so §27's first line is unreachable");
        }

        [UnityTest]
        public IEnumerator SmileyCarriesTheComponentThatTurnsHisCollisionsIntoSound()
        {
            // The reachability half. A GameAudio nothing ever calls is a mixer in an empty room.
            var controller = Object.FindFirstObjectByType<SmileyController>();

            Assert.That(controller.GetComponent<SmileyAudio>(), Is.Not.Null,
                "Smiley has no SmileyAudio, so no collision or launch will ever be heard");

            yield return null;
        }

        [UnityTest]
        public IEnumerator AHarderLandingIsLouderThanAGentleOne()
        {
            // §27: "SFX volume depends on impact force."
            GameAudio audio = Audio();
            var controller = Object.FindFirstObjectByType<SmileyController>();
            var body = controller.GetComponent<Rigidbody2D>();

            GameSceneFixture.ClearTheSky();
            Bounds floor = GameObject.Find("StartArea").GetComponent<Collider2D>().bounds;

            float gentle = 0f;
            yield return Drop(body, floor, 0.6f, v => gentle = v);

            float hard = 0f;
            yield return Drop(body, floor, 6f, v => hard = v);

            Assert.That(gentle, Is.GreaterThan(0f), "a gentle landing made no sound at all");
            Assert.That(hard, Is.GreaterThan(gentle),
                $"a 6-unit drop played at {hard:0.000} and a 0.6-unit drop at {gentle:0.000} — " +
                "§27 requires the harder impact to be the louder one");
        }

        private IEnumerator Drop(
            Rigidbody2D body, Bounds floor, float height, System.Action<float> report)
        {
            GameAudio audio = Audio();
            var contact = body.GetComponent<PlatformContact>();

            body.WakeUp();
            body.position = new Vector2(GameSceneFixture.ClearColumn(floor.max.y, floor.max.y + height + 1f), floor.max.y + height);
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.rotation = 0f;

            yield return new WaitForFixedUpdate();

            for (int frame = 0; frame < 300; frame++)
            {
                yield return new WaitForFixedUpdate();
                if (contact.IsTouchingPlatform) break;
            }

            yield return new WaitForFixedUpdate();
            report(audio.LastImpactVolume);
        }

        [UnityTest]
        public IEnumerator ARestingSmileyIsSilent()
        {
            // ⛔ §27: "No rolling SFX." A Smiley in continuous contact delivers an impulse every
            // step, and the resting subtraction is what keeps him quiet — but only if nothing
            // asks for a sound in the first place. SmileyAudio deliberately has no
            // OnCollisionStay2D, where §11's deformation deliberately does.
            GameAudio audio = Audio();
            var controller = Object.FindFirstObjectByType<SmileyController>();

            for (int frame = 0; frame < 200 && !controller.IsReady; frame++)
            {
                yield return new WaitForFixedUpdate();
            }

            float afterLanding = audio.LastImpactVolume;

            for (int frame = 0; frame < 120; frame++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(audio.LastImpactVolume, Is.EqualTo(afterLanding).Within(1e-5f),
                "a motionless Smiley produced a new collision sound, which §27 forbids");
        }

        [UnityTest]
        public IEnumerator TheAudioOffersNoSoundForTheThingsThatMustBeSilent()
        {
            // ⛔ §14.3 and §27 between them forbid FOUR sounds: rolling, the Disappearing
            // Platform near expiry, its respawn, and the Top Checkpoint. The way they are kept
            // is that GameAudio has no method that could serve them — a cue that cannot be asked
            // for cannot be added by accident.
            //
            // ⚠ Asserted against the type's own surface, so adding one fails here rather than
            // passing a review.
            string[] forbidden = { "roll", "vanish", "disappear", "respawn", "checkpoint", "expiry" };

            var methods = typeof(GameAudio)
                .GetMethods(System.Reflection.BindingFlags.Public
                            | System.Reflection.BindingFlags.Instance
                            | System.Reflection.BindingFlags.DeclaredOnly)
                .Select(m => m.Name.ToLowerInvariant())
                .ToArray();

            foreach (string word in forbidden)
            {
                Assert.That(methods.Any(m => m.Contains(word)), Is.False,
                    $"GameAudio has a method mentioning '{word}' — §27 and §14.3 forbid a sound " +
                    "for it, and the way that was kept was by having no way to ask");
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator TurningTheSoundDownToZeroSilencesIt()
        {
            // §26's sliders, and the promise a player makes when they move one to the bottom.
            GameAudio audio = Audio();

            audio.Settings.MusicVolume = 0f;
            audio.ApplyVolumes();
            yield return null;

            AudioSource music = audio.GetComponentsInChildren<AudioSource>()
                .First(s => s.name == "Music");

            Assert.That(music.volume, Is.EqualTo(0f),
                "the music setting is at zero and the source is still audible");
        }

        [UnityTest]
        public IEnumerator TheVolumesAreSavedAndSurviveResetGame()
        {
            // ⛔ §26: both volumes are "saved persistently". §25: "Settings are NOT reset."
            //
            // ⓘ This test could not be written until §27 existed — the Settings screen said so
            // in a comment for a day, because until there was something to store, "settings
            // survive a reset" was a claim about an empty set.
            GameAudio audio = Audio();

            audio.Settings.MusicVolume = 0.13f;
            audio.Settings.SfxVolume = 0.42f;
            audio.SaveSettings();

            Assert.That(SettingsFile.Exists(), Is.True, "nothing was written");

            // §25's Reset Game, exactly as the menu performs it.
            SaveFile.DeleteAll();

            GameSettings after = SettingsFile.Read();

            Assert.That(after.MusicVolume, Is.EqualTo(0.13f).Within(1e-3f),
                "Reset Game took the music setting with it, and §25 says it must not");
            Assert.That(after.SfxVolume, Is.EqualTo(0.42f).Within(1e-3f),
                "Reset Game took the SFX setting with it");

            SettingsFile.DeleteForTests();
            yield return null;
        }
    }
}
