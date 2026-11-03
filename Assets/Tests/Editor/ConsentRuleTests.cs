using NUnit.Framework;
using Smiley.Ads;
using Smiley.Audio;

namespace Smiley.Tests
{
    /// <summary>Parental consent as arithmetic (D-107): what may run, when to ask, and the gate.</summary>
    public sealed class ConsentRuleTests
    {
        [Test]
        public void ForAChildDirectedAppOnlyGrantedConsentLetsTheAdSdkRun()
        {
            Assert.That(ConsentRule.AdsMayRun(ParentalConsent.Granted, childDirected: true), Is.True);
            Assert.That(ConsentRule.AdsMayRun(ParentalConsent.Refused, childDirected: true), Is.False);
            Assert.That(ConsentRule.AdsMayRun(ParentalConsent.Unknown, childDirected: true), Is.False, "never asked is not consent");
        }

        [Test]
        public void ForAGeneralAudienceAdsRunWithoutTheGate()
        {
            // ⭐ D-110: not directed at children under 13 — no gate, contextual adverts only.
            Assert.That(ConsentRule.AdsMayRun(ParentalConsent.Unknown, childDirected: false), Is.True);
            Assert.That(ConsentRule.MustAsk(ParentalConsent.Unknown, childDirected: false), Is.False);
        }

        [Test]
        public void ForAChildDirectedAppTheQuestionIsAskedOnlyWhenNeverAnswered()
        {
            Assert.That(ConsentRule.MustAsk(ParentalConsent.Unknown, childDirected: true), Is.True);
            Assert.That(ConsentRule.MustAsk(ParentalConsent.Granted, childDirected: true), Is.False);
            Assert.That(ConsentRule.MustAsk(ParentalConsent.Refused, childDirected: true), Is.False, "a refusal is an answer, not a retry");
        }

        [Test]
        public void AGarbledStoredValueIsNeverReadAsGranted()
        {
            Assert.That(ConsentRule.FromStored(1), Is.EqualTo(ParentalConsent.Granted));
            Assert.That(ConsentRule.FromStored(2), Is.EqualTo(ParentalConsent.Refused));
            Assert.That(ConsentRule.FromStored(0), Is.EqualTo(ParentalConsent.Unknown));
            Assert.That(ConsentRule.FromStored(7), Is.EqualTo(ParentalConsent.Unknown));
            Assert.That(ConsentRule.FromStored(-1), Is.EqualTo(ParentalConsent.Unknown));
        }

        [Test]
        public void ASettingsFileFromBeforeTheQuestionReadsAsNeverAsked()
        {
            // The field did not exist; JSON without it must not invent consent.
            GameSettings old = SettingsFile.Parse("{\"Version\":1,\"MusicVolume\":0.5,\"SfxVolume\":0.5}");

            Assert.That(ConsentRule.FromStored(old.ParentalConsent), Is.EqualTo(ParentalConsent.Unknown));
        }

        [Test]
        public void TheAnswerRoundTripsThroughTheSettingsFile()
        {
            var settings = new GameSettings { ParentalConsent = (int)ParentalConsent.Refused };

            GameSettings back = SettingsFile.Parse(SettingsFile.Serialise(settings));

            Assert.That(ConsentRule.FromStored(back.ParentalConsent), Is.EqualTo(ParentalConsent.Refused));
        }

        [Test]
        public void TheGateAcceptsOnlyItsOwnProduct()
        {
            ParentGate gate = ParentGate.New(20260912);

            Assert.That(gate.A, Is.InRange(3, 9));
            Assert.That(gate.B, Is.InRange(3, 9));
            Assert.That(gate.Check(gate.Answer.ToString()), Is.True);
            Assert.That(gate.Check(" " + gate.Answer + " "), Is.True, "whitespace is forgiven");
            Assert.That(gate.Check((gate.Answer + 1).ToString()), Is.False);
            Assert.That(gate.Check(""), Is.False);
            Assert.That(gate.Check(null), Is.False);
            Assert.That(gate.Check("abc"), Is.False);
            Assert.That(gate.Prompt, Does.Contain(gate.A + " × " + gate.B));
        }
    }
}
