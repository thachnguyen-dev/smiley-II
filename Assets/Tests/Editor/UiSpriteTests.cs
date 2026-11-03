using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Smiley.Tests
{
    /// <summary>
    /// The stylesheet's sprites: every one exists, is imported at its own size, and is drawn in
    /// a box of its own ratio (D-114).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ Owner, 2026-09-13: <i>"Do not stretch, squash, distort, or otherwise deform the UI
    /// assets. Preserve their original proportions/aspect ratios."</i> A stylesheet is data —
    /// nothing compiles it, and a rule that stretches a pill into a square looks exactly like
    /// one that does not until a phone draws it. This reads <c>Meta.uss</c> and checks the
    /// arithmetic instead.
    /// </para>
    /// <para>
    /// ⚠ Three ways a sprite gets deformed, each caught separately: a <c>url()</c> pointing at
    /// nothing (UI Toolkit then drops every rule after it in the file); an importer that
    /// resamples a non-power-of-two texture (two of the pack's panels arrived set that way —
    /// 466 × 375 would have shipped as 512 × 256); and a box whose ratio is not the sprite's.
    /// </para>
    /// </remarks>
    public sealed class UiSpriteTests
    {
        private const string Uss = "Assets/UI/Meta.uss";

        private static readonly Regex Rule = new Regex(@"([^{}]+)\{([^{}]*)\}", RegexOptions.Singleline);
        private static readonly Regex Url = new Regex(@"url\(""[^""]*guid=([0-9a-f]{32})[^""]*""\)");
        private static readonly Regex Width = new Regex(@"(?<![-\w])width:\s*(\d+(?:\.\d+)?)px");
        private static readonly Regex Height = new Regex(@"(?<![-\w])height:\s*(\d+(?:\.\d+)?)px");

        /// <summary>The path of the texture a rule draws, or null — a rule can also name a font.</summary>
        private static string TexturePath(string body)
        {
            foreach (Match m in Url.Matches(body))
            {
                string path = AssetDatabase.GUIDToAssetPath(m.Groups[1].Value);
                if (path.EndsWith(".png"))
                {
                    return path;
                }
            }

            return null;
        }

        private static string WithoutComments(string css) =>
            Regex.Replace(css, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        private static IEnumerable<(string selector, string body)> Rules()
        {
            string css = WithoutComments(File.ReadAllText(Uss));
            foreach (Match m in Rule.Matches(css))
            {
                yield return (m.Groups[1].Value.Trim(), m.Groups[2].Value);
            }
        }

        [Test]
        public void EveryUrlInTheStylesheetResolvesToAnAsset()
        {
            var guids = new HashSet<string>();
            foreach (Match m in Url.Matches(WithoutComments(File.ReadAllText(Uss))))
            {
                guids.Add(m.Groups[1].Value);
            }

            Assert.That(guids, Is.Not.Empty, "the stylesheet references no assets — is this the pack build?");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Assert.That(path, Is.Not.Empty, $"guid {guid} in {Uss} resolves to no asset");
                Assert.That(AssetDatabase.LoadMainAssetAtPath(path), Is.Not.Null, $"{path} cannot be loaded");
            }
        }

        [Test]
        public void EveryReferencedTextureIsImportedAtItsOwnSize()
        {
            foreach (Match m in Url.Matches(WithoutComments(File.ReadAllText(Uss))))
            {
                string path = AssetDatabase.GUIDToAssetPath(m.Groups[1].Value);
                if (!path.EndsWith(".png"))
                {
                    continue;
                }

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(importer, Is.Not.Null, $"{path} has no texture importer");

                importer.GetSourceTextureWidthAndHeight(out int sourceWidth, out int sourceHeight);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

                Assert.That((texture.width, texture.height), Is.EqualTo((sourceWidth, sourceHeight)),
                    $"{path} is {sourceWidth}x{sourceHeight} on disk but imports as " +
                    $"{texture.width}x{texture.height} — the importer is resampling it " +
                    $"(textureType={importer.textureType}, npotScale={importer.npotScale})");
            }
        }

        [Test]
        public void EverySpriteBackedBoxHasItsSpritesRatio()
        {
            int checkedRules = 0;
            var seen = new List<string>();

            foreach ((string selector, string body) in Rules())
            {
                Match w = Width.Match(body);
                Match h = Height.Match(body);
                // ⚠ The first url() in a rule may be its FONT: .action names Baloo 2 before its pill,
                // and a version of this test that took the first url skipped .action entirely.
                // The count assertion below is what caught it.
                string path = TexturePath(body);

                if (path == null || !w.Success || !h.Success)
                {
                    continue;
                }

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                float boxRatio = float.Parse(w.Groups[1].Value) / float.Parse(h.Groups[1].Value);
                float spriteRatio = (float)texture.width / texture.height;

                Assert.That(boxRatio, Is.EqualTo(spriteRatio).Within(0.015f * spriteRatio),
                    $"{selector} draws {Path.GetFileName(path)} ({texture.width}x{texture.height}, ratio " +
                    $"{spriteRatio:0.000}) in a {w.Groups[1].Value}x{h.Groups[1].Value} box (ratio {boxRatio:0.000})");
                checkedRules++;
                seen.Add(selector);
            }

            // Every panel, pill and icon rule sizes its own box; a stylesheet that sized none
            // of them would pass the loop above by checking nothing.
            Assert.That(checkedRules, Is.GreaterThanOrEqualTo(8),
                $"only {checkedRules} sprite-backed rules carry both a width and a height: {string.Join(" | ", seen)}");
        }

        [Test]
        public void VariantsThatChangeOnlyTheSpriteKeepTheBaseRatio()
        {
            // .secondary / .neutral / .purple / .danger / .golden swap the pill and may set a width, but
            // inherit .action's 140px height. Check each against that height.
            const float baseHeight = 140f;
            var variants = new Dictionary<string, float>();

            foreach ((string selector, string body) in Rules())
            {
                if (selector != ".secondary" && selector != ".neutral" && selector != ".purple" && selector != ".danger" && selector != ".golden")
                {
                    continue;
                }

                string path = TexturePath(body);
                Assert.That(path, Is.Not.Null, $"{selector} names no sprite");
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Match w = Width.Match(body);
                float width = w.Success ? float.Parse(w.Groups[1].Value) : 364f;

                Assert.That(width / baseHeight, Is.EqualTo((float)texture.width / texture.height).Within(0.02f),
                    $"{selector}: {texture.width}x{texture.height} drawn at {width}x{baseHeight}");
                variants[selector] = width;
            }

            Assert.That(variants.Count, Is.EqualTo(5), "one of the five pill variants is missing");
        }

        [Test]
        public void NoSpriteIsNineSliced()
        {
            string css = WithoutComments(File.ReadAllText(Uss));
            Assert.That(css, Does.Not.Contain("-unity-slice-"),
                "slicing hides a wrong box behind stretched middles; the pack is drawn whole, at ratio");

            foreach (Match m in Url.Matches(css))
            {
                string path = AssetDatabase.GUIDToAssetPath(m.Groups[1].Value);
                if (AssetImporter.GetAtPath(path) is TextureImporter importer)
                {
                    Assert.That(importer.spriteBorder, Is.EqualTo(Vector4.zero),
                        $"{path} carries a sprite border, which UI Toolkit would slice by");
                }
            }
        }
    }
}
