using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DiceCraft.Presentation.Tests
{
    /// <summary>
    /// V1.1 task W20Xb item A (THEME_SPEC §3.1): every theme's Table/*.png is imported at its native size (npotScale None),
    /// and the table pictures (table_scene, page_scene) with the settings of the default table, bg_table_scene.png, that the
    /// felt shader depends on: it samples them with screen-space derivatives, so mipmaps change what is drawn (the default
    /// table has none), and it reads colour as sRGB through a clamped bilinear sampler. felt_tile stays a tiled texture with
    /// mipmaps. Until W20Xb the pictures were scaled to 2048 x 1024 (ToNearest) with mipmaps.
    /// </summary>
    public sealed class ThemeTableImportW20XbTests
    {
        private const string DefaultTable="Assets/Art/Art071/Backgrounds/bg_table_scene.png";

        private static Vector2Int PngSize(string file)
        {
            var bytes=new byte[24];using(var stream=File.OpenRead(file))Assert.That(stream.Read(bytes,0,24),Is.EqualTo(24),file);
            int Big(int at)=>bytes[at]<<24|bytes[at+1]<<16|bytes[at+2]<<8|bytes[at+3];
            return new Vector2Int(Big(16),Big(20));
        }

        [Test] public void W20XbA_TablePicturesImportAtNativeSizeLikeTheDefaultTable()
        {
            var reference=(TextureImporter)AssetImporter.GetAtPath(DefaultTable);
            Assert.That(reference,Is.Not.Null,DefaultTable);
            // The reference itself: the default table as the felt has drawn it since v0.71.
            Assert.That(reference.npotScale,Is.EqualTo(TextureImporterNPOTScale.None));Assert.That(reference.mipmapEnabled,Is.False);
            int pictures=0,tiles=0;
            foreach(string folder in Directory.GetDirectories(Path.Combine(Application.dataPath,"Resources","Themes")))
            {
                string table=Path.Combine(folder,"Table");if(!Directory.Exists(table))continue;
                string id=Path.GetFileName(folder);
                foreach(string file in Directory.GetFiles(table,"*.png"))
                {
                    string name=Path.GetFileNameWithoutExtension(file),asset="Assets/Resources/Themes/"+id+"/Table/"+name+".png";
                    var importer=(TextureImporter)AssetImporter.GetAtPath(asset);Assert.That(importer,Is.Not.Null,asset);
                    var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(asset);Assert.That(texture,Is.Not.Null,asset);
                    var size=PngSize(file);
                    Assert.That(importer.npotScale,Is.EqualTo(TextureImporterNPOTScale.None),asset+": native size");
                    Assert.That(new Vector2Int(texture.width,texture.height),Is.EqualTo(size),asset+": imported at the PNG's own size");
                    Assert.That(importer.textureType,Is.EqualTo(reference.textureType),asset);
                    Assert.That(importer.sRGBTexture,Is.EqualTo(reference.sRGBTexture),asset);
                    Assert.That(importer.filterMode,Is.EqualTo(reference.filterMode),asset);
                    if(name=="felt_tile")
                    {
                        // The felt's tiled cloth (_BaseMap, 24 x 24 repeats): it keeps its mipmaps and repeats.
                        Assert.That(importer.wrapMode,Is.EqualTo(TextureWrapMode.Repeat),asset);Assert.That(importer.mipmapEnabled,Is.True,asset);
                        tiles++;
                    }
                    else
                    {
                        Assert.That(importer.mipmapEnabled,Is.EqualTo(reference.mipmapEnabled),asset+": mipmaps as the default table");
                        Assert.That(texture.mipmapCount,Is.EqualTo(1),asset);
                        Assert.That(importer.wrapMode,Is.EqualTo(reference.wrapMode),asset);
                        pictures++;
                    }
                }
            }
            Assert.That(pictures,Is.GreaterThanOrEqualTo(1),"At least one theme ships a table picture.");
            Assert.That(tiles,Is.GreaterThanOrEqualTo(1),"At least one theme ships a felt tile.");
        }
    }
}
