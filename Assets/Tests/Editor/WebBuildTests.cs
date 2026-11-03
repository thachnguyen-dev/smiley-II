using System;
using System.Collections.Generic;
using NUnit.Framework;
using Smiley.Flow;
using Smiley.Save;
using Smiley.Web;

namespace Smiley.Tests
{
    /// <summary>D-138 and D-139: the browser build's rules, without a browser.</summary>
    /// <remarks>
    /// ⚠ The <c>#if UNITY_WEBGL</c> branches in the ad and store files compile only for a WebGL
    /// build target and are not exercised here; a browser run is the only check of those.
    /// </remarks>
    public sealed class WebBuildTests
    {
        /// <summary>A portal store that is a dictionary, standing in for CrazyGames' Data module.</summary>
        private sealed class FakePortal : IPortalBackend
        {
            public readonly Dictionary<string, string> Data = new Dictionary<string, string>();
            public readonly List<string> Events = new List<string>();

            public string Name => "fake";
            public bool IsAvailable => true;
            public void Init(Action ready) => ready();
            public void GameplayStart() => Events.Add("start");
            public void GameplayStop() => Events.Add("stop");
            public bool HasKey(string key) => Data.ContainsKey(key);
            public string GetString(string key) => Data.TryGetValue(key, out string v) ? v : string.Empty;
            public void SetString(string key, string value) => Data[key] = value;
            public void DeleteKey(string key) => Data.Remove(key);
        }

        [SetUp]
        public void SetUp()
        {
            Portal.ResetForTests();
            WebPlatform.OverrideForTests = null;
        }

        [TearDown]
        public void TearDown()
        {
            Portal.ResetForTests();
            WebPlatform.OverrideForTests = null;
        }

        [Test]
        public void TheEditorIsNeverTheWebBuild()
        {
            // ⛔ Every other fixture assumes the phone flow; switching the Editor's target must not change that.
            Assert.That(WebPlatform.Active, Is.False);
        }

        [Test]
        public void OnTheWebANewGameGoesStraightToTheTutorialImage()
        {
            CompletionSequence flow = CompletionSequence.Fresh;

            Assert.That(flow.BeginTutorial(), Is.True);
            Assert.That(flow.Phase, Is.EqualTo(CompletionPhase.Tutorial));
            Assert.That(flow.StoryIsPlaying, Is.False, "an opening story played on the web");
            Assert.That(flow.HasStarted, Is.True, "the climb could be controlled behind the tutorial image");
        }

        [Test]
        public void ClosingTheTutorialImageIsTheOneClickBeforePlay()
        {
            CompletionSequence flow = CompletionSequence.Fresh;
            flow.BeginTutorial();

            flow.CloseTutorial();

            Assert.That(flow.Phase, Is.EqualTo(CompletionPhase.Climbing));
        }

        [Test]
        public void TheOpeningCannotStartAfterTheTutorialOnlyStart()
        {
            CompletionSequence flow = CompletionSequence.Fresh;
            flow.BeginTutorial();
            flow.CloseTutorial();

            Assert.That(flow.BeginOpening(13), Is.False);
            Assert.That(flow.BeginTutorial(), Is.False);
            Assert.That(flow.Phase, Is.EqualTo(CompletionPhase.Climbing));
        }

        [Test]
        public void ATutorialOnlyStartIsRefusedOnceTheClimbIsUnderWay()
        {
            CompletionSequence flow = CompletionSequence.Fresh;
            flow.ReachCheckpoint(2);

            Assert.That(flow.BeginTutorial(), Is.False);
            Assert.That(flow.Phase, Is.EqualTo(CompletionPhase.Story));
        }

        [Test]
        public void PlayIsOnlyTheUncoveredClimbWithTheGearMenuShut()
        {
            Assert.That(GameplayRule.IsPlaying(true, CompletionPhase.Climbing, false), Is.True);
            Assert.That(GameplayRule.IsPlaying(true, CompletionPhase.Climbing, true), Is.False, "gear menu");
            Assert.That(GameplayRule.IsPlaying(true, CompletionPhase.Tutorial, false), Is.False, "tutorial image");
            Assert.That(GameplayRule.IsPlaying(true, CompletionPhase.Story, false), Is.False, "ending story");
            Assert.That(GameplayRule.IsPlaying(true, CompletionPhase.Completed, false), Is.False, "Completed screen");
            Assert.That(GameplayRule.IsPlaying(false, CompletionPhase.Climbing, false), Is.False, "Main Menu");
        }

        [Test]
        public void ThePortalHearsOnlyChanges()
        {
            var portal = new FakePortal();
            Portal.ForceReadyForTests(portal);

            Portal.SetPlaying(true);
            Portal.SetPlaying(true);
            Portal.SetPlaying(false);
            Portal.SetPlaying(false);
            Portal.SetPlaying(true);

            Assert.That(portal.Events, Is.EqualTo(new[] { "start", "stop", "start" }));
        }

        [Test]
        public void NoPortalMeansWhenReadyRunsAtOnce()
        {
            bool ran = false;

            Portal.WhenReady(() => ran = true);

            Assert.That(ran, Is.True, "the menu would wait for a portal that is not there");
            Assert.That(Portal.Storage, Is.Null);
        }

        [Test]
        public void WithAPortalTheSaveGoesToItsStoreAndSurvivesARoundTrip()
        {
            var portal = new FakePortal();
            Portal.ForceReadyForTests(portal);

            Assert.That(SaveFile.Exists(), Is.False);

            Assert.That(SaveFile.Write(new SaveData { SmileyY = 12.5f }), Is.True);

            Assert.That(SaveFile.Exists(), Is.True);
            Assert.That(SaveFile.Read().SmileyY, Is.EqualTo(12.5f).Within(1e-4f));
        }

        [Test]
        public void ThePortalStoreKeepsThePreviousSaveAsTheBackup()
        {
            var portal = new FakePortal();
            Portal.ForceReadyForTests(portal);

            SaveFile.Write(new SaveData { SmileyY = 1f });
            SaveFile.Write(new SaveData { SmileyY = 2f });

            // A live save that did not finish being written is refused, and the backup is used.
            portal.Data["smiley.save"] = "{ \"SmileyY\": 99";

            Assert.That(SaveFile.Read().SmileyY, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void ResetGameEmptiesThePortalStoreBackupAndAll()
        {
            var portal = new FakePortal();
            Portal.ForceReadyForTests(portal);
            SaveFile.Write(new SaveData());
            SaveFile.Write(new SaveData());

            SaveFile.DeleteAll();

            Assert.That(SaveFile.Exists(), Is.False);
            Assert.That(portal.Data, Is.Empty);
        }
    }
}
