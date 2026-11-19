using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DiceCraft.Editor
{
    /// <summary>
    /// V1.1 task W22i (docs/specs/v11/W22I_REPORT.md): the batch step after tools/art_v11/install_art.py and
    /// tools/art_v11/crops/make_crops.py have written the files.
    ///  1. Imports them. The five styles' Resources/Themes/&lt;style&gt;/Art/*.png take the theme Art rule of V11ThemeImport
    ///     on import (the Swiss trial images' settings), the icons under Art/Art11/Icons the W13 icon rule (V13IconImport).
    ///  2. The default style's emblems (Art/Art11/Emblems/emblem_&lt;family&gt;.png, beside Art11's other default art, outside
    ///     Resources like the default illustrations in Art071/Illustrations) get the same theme Art rule written into their
    ///     .meta here. No new AssetPostprocessor is added for them: a new texture postprocessor makes Unity reimport every
    ///     texture of the project (W13 met this).
    ///  3. Every installed picture is checked against the rule (one that differs is reimported with it).
    ///  4. The compact icon sets are bound to GameApp.compactIcons13 in the Bootstrap scene (V13CardSetup.BindCompactIcons).
    /// The emblems are not bound to any screen or array here: W15 wires them (SETTLEMENT_LAYOUT §8.6, §13 item 1).
    /// Batch: Unity -batchmode -quit -projectPath DicePrototype -executeMethod DiceCraft.Editor.V22iArtSetup.Integrate
    /// </summary>
    public static class V22iArtSetup
    {
        public const string DefaultEmblemsRoot="Assets/Art/Art11/Emblems";
        public static readonly string[] Styles={"swiss","sancai","comic","deco","glass"};

        /// <summary>The theme Art rule of V11ThemeImport (its "Art" branch), for a picture outside Resources/Themes.</summary>
        public static void ArtRule(TextureImporter importer)
        {
            importer.sRGBTexture=true;importer.alphaIsTransparency=true;importer.filterMode=FilterMode.Bilinear;
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;
            importer.spritePixelsPerUnit=100;importer.maxTextureSize=1024;
            importer.textureCompression=TextureImporterCompression.CompressedHQ;
        }
        /// <summary>True when an importer has the theme Art rule.</summary>
        public static bool HasArtRule(TextureImporter importer)
            =>importer!=null&&importer.textureType==TextureImporterType.Sprite&&importer.spriteImportMode==SpriteImportMode.Single
              &&importer.sRGBTexture&&importer.alphaIsTransparency&&importer.filterMode==FilterMode.Bilinear
              &&!importer.mipmapEnabled&&importer.wrapMode==TextureWrapMode.Clamp&&Mathf.Approximately(importer.spritePixelsPerUnit,100)
              &&importer.maxTextureSize==1024&&importer.textureCompression==TextureImporterCompression.CompressedHQ;

        private static string[] Pngs(string folder)=>Directory.Exists(folder)?Directory.GetFiles(folder,"*.png").Select(p=>p.Replace('\\','/')).OrderBy(p=>p,StringComparer.Ordinal).ToArray():new string[0];

        [MenuItem("DiceCraft/V1.1/Import the installed style art and bind the compact icons (W22i)")]
        public static void Integrate()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Integration requires stopped play mode.");
            AssetDatabase.Refresh();
            int emblems=0,themed=0,reimported=0;
            foreach(string path in Pngs(DefaultEmblemsRoot))
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                if(importer==null)throw new InvalidOperationException("Not imported: "+path);
                if(!HasArtRule(importer)){ArtRule(importer);importer.SaveAndReimport();reimported++;}
                emblems++;
            }
            foreach(string style in Styles)
                foreach(string path in Pngs(V11ThemeImport.Root+style+"/Art"))
                {
                    var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                    if(importer==null)throw new InvalidOperationException("Not imported: "+path);
                    // V11ThemeImport applies the rule on every import of a file under Resources/Themes.
                    if(!HasArtRule(importer)){AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);reimported++;importer=(TextureImporter)AssetImporter.GetAtPath(path);}
                    if(!HasArtRule(importer))throw new InvalidOperationException(path+" does not take the theme Art rule.");
                    if(AssetDatabase.LoadAssetAtPath<Sprite>(path)==null)throw new InvalidOperationException(path+" is not a sprite.");
                    themed++;
                }
            AssetDatabase.SaveAssets();
            V13CardSetup.BindCompactIcons();
            Debug.Log("V22I_ART_INTEGRATED themed="+themed+" defaultEmblems="+emblems+" reimported="+reimported);
        }
    }
}
