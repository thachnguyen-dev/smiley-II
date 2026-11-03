using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Smiley.Tests
{
    /// <summary>
    /// The published privacy page says what the privacy policy says (D-129).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⛔ <b>This is the bug that made the generator necessary.</b> The page was a hand-written
    /// mirror of the markdown and it drifted: by 2026-10-07 the policy had gained a Remove Ads
    /// section (D-124) and a tracking section (D-127) that the page did not have. Uploading it
    /// would have published a policy failing to mention a purchase the app sells and a prompt it
    /// shows — the one kind of error in a privacy policy that actually matters.
    /// </para>
    /// <para>
    /// ⚠ <b>Nothing here checks prose</b>, only that every section exists in both and that the
    /// owner-only note never reached a public page. Whether a sentence is TRUE is not a thing a
    /// fixture can know — that stays a human's job, and the policy says so itself.
    /// </para>
    /// </remarks>
    public sealed class PrivacyPageTests
    {
        private const string Markdown = "../docs/privacy-policy.md";
        private const string Page = "../docs/privacy-policy.html";

        private static string Read(string relative)
        {
            string path = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), relative));
            Assert.That(File.Exists(path), Is.True, $"{relative} is missing (looked at {path})");
            return File.ReadAllText(path);
        }

        private static List<string> Sections(string text, string pattern)
        {
            var found = new List<string>();

            foreach (Match m in Regex.Matches(text, pattern))
            {
                found.Add(m.Groups[1].Value.Trim());
            }

            return found;
        }

        [Test]
        public void EverySectionOfThePolicyIsOnThePublishedPage()
        {
            List<string> inPolicy = Sections(Read(Markdown), @"(?m)^##\s+(.+)$");
            List<string> onPage = Sections(Read(Page), @"<h2>(.+?)</h2>");

            Assert.That(inPolicy, Is.Not.Empty, "no sections found in the policy — the parse is wrong");

            CollectionAssert.AreEqual(inPolicy, onPage,
                "the page and the policy no longer carry the same sections. Run " +
                "'node tools/build-privacy-page.js' — and do not hand-edit the .html, which is " +
                "how they drifted apart the first time");
        }

        [Test]
        public void TheOwnerOnlyNoteIsNotOnThePublishedPage()
        {
            // ⛔ It tells the owner to set the effective date and says the text is not legal
            // advice. Fine in the repository; not something to publish to players.
            Assert.That(Read(Page), Does.Not.Contain("For the owner"),
                "the owner-only note reached the public page");
        }

        [Test]
        public void ThePageNamesTheThingsTheAppActuallyDoes()
        {
            // ⓘ Named explicitly because each was added to the app AFTER the page was first
            // written, and each is a disclosure a store expects to find.
            string page = Read(Page);

            Assert.That(page, Does.Contain("Remove Ads"), "the purchase (D-124) is not disclosed");
            Assert.That(page, Does.Contain("Tracking"), "the iOS ATT prompt (D-127) is not disclosed");
            Assert.That(page, Does.Contain("Apple"), "the App Store is not named (D-127)");
        }
    }
}
