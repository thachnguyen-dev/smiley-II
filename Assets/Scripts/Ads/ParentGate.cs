using System;

namespace Smiley.Ads
{
    /// <summary>
    /// The question a grown-up answers before the consent buttons work.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠ <b>THIS IS A PARENT GATE, NOT VERIFIABLE PARENTAL CONSENT (D-107, Q-056).</b> A
    /// multiplication a young child cannot do keeps a child from tapping "I consent" by
    /// accident. It does <i>not</i> verify that a parent is present — COPPA's verifiable methods
    /// (a card transaction, a signed form, an ID check, a video call) all need a provider behind
    /// them, and choosing that provider is the owner's. This gate is the seam a provider plugs
    /// into: <see cref="Check"/> is the one call the consent screen makes.
    /// </para>
    /// <para>
    /// ⓘ Deterministic from a seed so a test can pin the arithmetic; the screen seeds it from
    /// the clock so the question differs each time.
    /// </para>
    /// </remarks>
    public readonly struct ParentGate
    {
        public readonly int A;
        public readonly int B;

        private ParentGate(int a, int b)
        {
            A = a;
            B = b;
        }

        /// <summary>A two-factor multiplication with both factors in 3..9.</summary>
        public static ParentGate New(int seed)
        {
            var random = new Random(seed);
            return new ParentGate(random.Next(3, 10), random.Next(3, 10));
        }

        /// <summary>The prompt, in the game's language (§33: English).</summary>
        public string Prompt => $"Parents: what is {A} × {B}?";

        public int Answer => A * B;

        /// <summary>Whether typed text is the answer. Whitespace is forgiven; nothing else is.</summary>
        public bool Check(string typed) =>
            int.TryParse((typed ?? string.Empty).Trim(), out int value) && value == Answer;
    }
}
