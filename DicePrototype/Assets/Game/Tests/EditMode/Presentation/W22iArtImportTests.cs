using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace DiceCraft.Presentation.Tests
{
    /// <summary>
    /// V1.1 task W22i items 2-4 (THEME_SPEC §3, SETTLEMENT_LAYOUT §8.6): every installed picture is imported like the Swiss
    /// trial illustrations (the theme Art rule of Editor/V11ThemeImport.cs: sprite, 100 pixels per unit, at most 1024 on the
    /// default platform, compressed, no mipmaps, clamped bilinear sRGB with transparency): the five styles' 184
    /// illustrations and 7 emblems under Resources/Themes/&lt;style&gt;/Art, and the default style's 7 emblems under
    /// Art/Art11/Emblems. Every style has a full set of baked compact icons (184) imported like the default style's (W13).
    /// </summary>
    public sealed class W22iArtImportTests
    {
        private static readonly string[] Styles={"swiss","sancai","comic","deco","glass"};
        private static readonly string[] Families={"seal","constraint","multiplier","cards","economy","target","final"};
        /// <summary>A Swiss trial illustration (W12) whose .meta the trial committed: the reference for every other picture.</summary>
        private const string Reference="Assets/Resources/Themes/swiss/Art/illustration_C08.png";
        private const string DefaultEmblems="Assets/Art/Art11/Emblems";
        private const string Icons="Assets/Art/Art11/Icons";

        private static string[] Pngs(string folder)=>Directory.GetFiles(folder,"*.png").Select(p=>p.Replace('\\','/')).OrderBy(p=>p,System.StringComparer.Ordinal).ToArray();

        private static void AssertArtImport(string asset,TextureImporter reference)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(asset);Assert.That(importer,Is.Not.Null,asset);
            Assert.That(importer.textureType,Is.EqualTo(reference.textureType),asset);
            Assert.That(importer.spriteImportMode,Is.EqualTo(reference.spriteImportMode),asset);
            Assert.That(importer.spritePixelsPerUnit,Is.EqualTo(reference.spritePixelsPerUnit),asset);
            Assert.That(importer.spriteBorder,Is.EqualTo(reference.spriteBorder),asset);
            Assert.That(importer.maxTextureSize,Is.EqualTo(reference.maxTextureSize),asset);
            Assert.That(importer.textureCompression,Is.EqualTo(reference.textureCompression),asset);
            Assert.That(importer.mipmapEnabled,Is.EqualTo(reference.mipmapEnabled),asset);
            Assert.That(importer.sRGBTexture,Is.EqualTo(reference.sRGBTexture),asset);
            Assert.That(importer.alphaIsTransparency,Is.EqualTo(reference.alphaIsTransparency),asset);
            Assert.That(importer.filterMode,Is.EqualTo(reference.filterMode),asset);
            Assert.That(importer.wrapMode,Is.EqualTo(reference.wrapMode),asset);
            Assert.That(importer.npotScale,Is.EqualTo(reference.npotScale),asset);
            Assert.That(importer.isReadable,Is.EqualTo(reference.isReadable),asset);
            var standalone=importer.GetPlatformTextureSettings("Standalone");
            Assert.That(standalone.overridden,Is.EqualTo(reference.GetPlatformTextureSettings("Standalone").overridden),asset+": the Standalone override as the reference");
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(asset);Assert.That(texture,Is.Not.Null,asset);
            Assert.That(Mathf.Max(texture.width,texture.height),Is.LessThanOrEqualTo(1024),asset+": import cap 1024");
            Assert.That(texture.mipmapCount,Is.EqualTo(1),asset);
            // Block compression needs both sides a multiple of 4. A non-square picture scaled to the 1024 cap can miss that
            // (1536 x 1024 becomes 1024 x 683, like the Swiss trial C08): Unity then keeps it uncompressed (RGBA32).
            bool blocks=texture.width%4==0&&texture.height%4==0;
            if(blocks)Assert.That(GraphicsFormatUtility.IsCompressedFormat(texture.graphicsFormat),Is.True,asset+": "+texture.graphicsFormat);
            else
            {
                Assert.That(texture.graphicsFormat,Is.EqualTo(GraphicsFormat.R8G8B8A8_SRGB),asset+": "+texture.width+"x"+texture.height+" is not a multiple of 4");
                uncompressed.Add(asset.Substring(asset.LastIndexOf('/')+1)+" "+texture.width+"x"+texture.height);
            }
            Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>(asset),Is.Not.Null,asset+": a sprite");
        }
        private static readonly System.Collections.Generic.List<string> uncompressed=new System.Collections.Generic.List<string>();

        [Test] public void W22i_EveryInstalledPictureIsImportedLikeTheSwissTrialImages()
        {
            var reference=(TextureImporter)AssetImporter.GetAtPath(Reference);Assert.That(reference,Is.Not.Null,Reference);
            // The reference is the theme Art rule's own result (the trial .meta was written by V11ThemeImport).
            Assert.That(reference.textureType,Is.EqualTo(TextureImporterType.Sprite));Assert.That(reference.spritePixelsPerUnit,Is.EqualTo(100));
            Assert.That(reference.maxTextureSize,Is.EqualTo(1024));Assert.That(reference.textureCompression,Is.EqualTo(TextureImporterCompression.CompressedHQ));
            Assert.That(reference.mipmapEnabled,Is.False);Assert.That(reference.wrapMode,Is.EqualTo(TextureWrapMode.Clamp));Assert.That(reference.filterMode,Is.EqualTo(FilterMode.Bilinear));
            Assert.That(reference.GetPlatformTextureSettings("Standalone").overridden,Is.False);
            int checkedFiles=0;uncompressed.Clear();
            foreach(string style in Styles)
            {
                var pngs=Pngs("Assets/Resources/Themes/"+style+"/Art");
                Assert.That(pngs.Count(p=>Path.GetFileName(p).StartsWith("illustration_")),Is.EqualTo(184),style+": illustrations");
                Assert.That(pngs.Where(p=>Path.GetFileName(p).StartsWith("emblem_")).Select(Path.GetFileNameWithoutExtension),
                    Is.EquivalentTo(Families.Select(f=>"emblem_"+f)),style+": emblems");
                Assert.That(pngs.Length,Is.EqualTo(184+7),style+": nothing else");
                foreach(string asset in pngs){AssertArtImport(asset,reference);checkedFiles++;}
            }
            var emblems=Pngs(DefaultEmblems);
            Assert.That(emblems.Select(Path.GetFileNameWithoutExtension),Is.EquivalentTo(Families.Select(f=>"emblem_"+f)),"the default style's emblems");
            foreach(string asset in emblems){AssertArtImport(asset,reference);checkedFiles++;}
            Assert.That(checkedFiles,Is.EqualTo(5*191+7));
            TestContext.WriteLine("W22i: kept uncompressed (a side not a multiple of 4 after the 1024 cap): "+uncompressed.Count+": "+string.Join(", ",uncompressed));
        }

        [Test] public void W22i_EveryStyleHasItsBakedCompactIcons()
        {
            var reference=(TextureImporter)AssetImporter.GetAtPath(Icons+"/default/icon_C01.png");Assert.That(reference,Is.Not.Null);
            Assert.That(reference.mipmapEnabled,Is.True);Assert.That(reference.filterMode,Is.EqualTo(FilterMode.Trilinear));
            foreach(string style in new[]{"default"}.Concat(Styles))
            {
                var pngs=Pngs(Icons+"/"+style);
                Assert.That(pngs.Length,Is.EqualTo(184),style+": one icon per content id");
                foreach(string asset in pngs)
                {
                    var importer=(TextureImporter)AssetImporter.GetAtPath(asset);Assert.That(importer,Is.Not.Null,asset);
                    Assert.That(importer.textureType,Is.EqualTo(reference.textureType),asset);Assert.That(importer.spriteImportMode,Is.EqualTo(reference.spriteImportMode),asset);
                    Assert.That(importer.mipmapEnabled,Is.EqualTo(reference.mipmapEnabled),asset);Assert.That(importer.filterMode,Is.EqualTo(reference.filterMode),asset);
                    Assert.That(importer.wrapMode,Is.EqualTo(reference.wrapMode),asset);Assert.That(importer.textureCompression,Is.EqualTo(reference.textureCompression),asset);
                    Assert.That(importer.maxTextureSize,Is.EqualTo(reference.maxTextureSize),asset);Assert.That(importer.isReadable,Is.EqualTo(reference.isReadable),asset);
                    Assert.That(importer.sRGBTexture,Is.EqualTo(reference.sRGBTexture),asset);Assert.That(importer.alphaIsTransparency,Is.EqualTo(reference.alphaIsTransparency),asset);
                }
            }
        }
    }
}
