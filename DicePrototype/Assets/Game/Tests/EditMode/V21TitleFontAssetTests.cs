using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace DiceCraft.Tests
{
    public class V21TitleFontAssetTests
    {
        private const string Root = "Assets/Game/Fonts/";
        private const string TitlePath = "Assets/Resources/Themes/sancai/Fonts/title.asset";
        private const string SancaiFallback = "²·‹›←↑→↓≈≤≥◆◇●✓";

        private static TMP_FontAsset Load(string path)
        {
            var face = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            Assert.That(face, Is.Not.Null, path);
            return face;
        }

        [Test]
        public void FW01_SancaiUsesOneStaticAtlasAndTheApprovedFaceMetrics()
        {
            var title = Load(TitlePath);
            Assert.That(title.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static));
            Assert.That(title.sourceFontFile, Is.Null, "A static title must not ship its TTF");
            Assert.That(title.atlasTextureCount, Is.EqualTo(1));
            Assert.That(title.atlasRenderMode, Is.EqualTo(GlyphRenderMode.SDFAA));
            Assert.That(title.atlasPadding, Is.EqualTo(8));
            var atlas = title.atlasTexture;
            Assert.That(atlas.width, Is.EqualTo(2048));
            Assert.That(atlas.height, Is.EqualTo(2048));
            Assert.That(atlas.isReadable, Is.False);
            Assert.That(atlas.format, Is.EqualTo(TextureFormat.Alpha8));
            Assert.That(title.material.mainTexture, Is.EqualTo(atlas));
            var face = title.faceInfo;
            Assert.That(face.pointSize, Is.EqualTo(56));
            Assert.That(face.scale, Is.EqualTo(0.96f).Within(0.001f));
            float scale = face.scale / face.pointSize;
            Assert.That(face.lineHeight * scale, Is.EqualTo(1.448f).Within(0.001f));
            Assert.That(face.ascentLine * scale, Is.EqualTo(1.16f).Within(0.001f));
            Assert.That(face.descentLine * scale, Is.EqualTo(-0.288f).Within(0.001f));
            CollectionAssert.AreEqual(new[] { Load(Root + "DiceChineseBold.asset"), Load(Root + "DiceChinese.asset") },
                                      title.fallbackFontAssetTable);
        }

        [Test]
        public void FW02_AllTitleCharactersResolveInTheIntendedStaticFace()
        {
            var title = Load(TitlePath);
            var bold = Load(Root + "DiceChineseBold.asset");
            foreach (char c in File.ReadAllText(Root + "Themes/title_chars.txt").TrimEnd('\n','\r'))
            {
                var glyph = TMP_FontAssetUtilities.GetCharacterFromFontAsset(c, title, true,
                    FontStyles.Normal, FontWeight.Regular, out _);
                Assert.That(glyph, Is.Not.Null, "U+" + ((int)c).ToString("X4"));
                Assert.That(glyph.textAsset, Is.EqualTo(SancaiFallback.Contains(c) ? bold : title), c.ToString());
                Assert.That(glyph.glyph, Is.Not.Null, c.ToString());
            }
        }

        [Test]
        public void Q5_RegularWeightMappingResolvesAllGameCharactersToRealBold()
        {
            var regular = Load(Root + "DiceChinese.asset");
            var bold = Load(Root + "DiceChineseBold.asset");
            Assert.That(TMP_Settings.defaultFontAsset, Is.EqualTo(regular));
            Assert.That(bold.sourceFontFile, Is.Null);
            Assert.That(regular.fontWeightTable[7].regularTypeface, Is.EqualTo(bold));
            foreach (char c in File.ReadAllText(Root + "Themes/bold_chars.txt").TrimEnd('\n','\r'))
            {
                var glyph = TMP_FontAssetUtilities.GetCharacterFromFontAsset(c, regular, true,
                    FontStyles.Bold, FontWeight.Bold, out bool alternative);
                Assert.That(glyph, Is.Not.Null, c.ToString());
                Assert.That(glyph.textAsset, Is.EqualTo(bold), c.ToString());
                Assert.That(alternative, Is.True, c.ToString());
            }
        }
    }
}
