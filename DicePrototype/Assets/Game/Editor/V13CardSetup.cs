using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DiceCraft.Editor
{
    /// <summary>
    /// V1.1 task W13 (docs/specs/v11/CARD_TEMPLATE.md §7): import rule for the baked compact card icons under
    /// Assets/Art/Art11/Icons/&lt;theme&gt;/icon_&lt;ID&gt;.png (160×160, tools/art_v11/crops/make_crops.py). They are drawn at
    /// 27–80 px, so they carry mipmaps (sampled trilinear) — without them a GPU minifying 3–5× speckles. Uncompressed like
    /// the default illustrations, so no block-compression artefacts; at most 256 px.
    /// </summary>
    public sealed class V13IconImport:AssetPostprocessor
    {
        public const string Root="Assets/Art/Art11/Icons/";
        public const int MaxSize=256;
        public static bool Applies(string path)
            =>path!=null&&path.Replace('\\','/').StartsWith(Root,StringComparison.Ordinal)&&path.EndsWith(".png",StringComparison.OrdinalIgnoreCase);

        private void OnPreprocessTexture(){if(Applies(assetPath))Configure((TextureImporter)assetImporter);}

        public static void Configure(TextureImporter importer)
        {
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=100;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.spriteMeshType=SpriteMeshType.FullRect;settings.spriteGenerateFallbackPhysicsShape=false;importer.SetTextureSettings(settings);
            importer.spriteBorder=Vector4.zero;
            importer.mipmapEnabled=true;importer.mipmapFilter=TextureImporterMipFilter.KaiserFilter;importer.borderMipmap=false;importer.streamingMipmaps=false;
            importer.filterMode=FilterMode.Trilinear;importer.wrapMode=TextureWrapMode.Clamp;
            importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=true;importer.sRGBTexture=true;importer.isReadable=false;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=MaxSize;importer.npotScale=TextureImporterNPOTScale.None;
        }
        /// <summary>True when an importer already has the rule above (the bind step reimports a file that does not).</summary>
        public static bool Matches(TextureImporter importer)
            =>importer!=null&&importer.textureType==TextureImporterType.Sprite&&importer.spriteImportMode==SpriteImportMode.Single
              &&importer.mipmapEnabled&&importer.filterMode==FilterMode.Trilinear&&importer.wrapMode==TextureWrapMode.Clamp
              &&importer.alphaIsTransparency&&importer.sRGBTexture&&!importer.isReadable
              &&importer.textureCompression==TextureImporterCompression.Uncompressed&&importer.maxTextureSize==MaxSize
              &&importer.npotScale==TextureImporterNPOTScale.None;
    }

    /// <summary>
    /// V1.1 task W13 batch steps (CARD_TEMPLATE §6–7).
    /// Integrate: the card template's sprites (rarity ring, three gems) join art07Templates through V11Setup, the theme
    /// copies of those sprites are reimported so they take the default sprite's pixels-per-unit and border
    /// (V11ThemeImport), and the compact icons are bound to GameApp.compactIcons13 in the Bootstrap scene.
    /// Batch: Unity -batchmode -quit -projectPath DicePrototype -executeMethod DiceCraft.Editor.V13CardSetup.Integrate
    /// Afterwards run python tools/art_v11/theme_contract.py contract.
    /// </summary>
    public static class V13CardSetup
    {
        public const string IconsRoot="Assets/Art/Art11/Icons";
        public const string ThemesRoot="Assets/Resources/Themes";
        public static readonly string[] CardTemplateSprites={"ui_card_frame_rarity","ui_gem_common","ui_gem_advanced","ui_gem_rare"};

        [MenuItem("DiceCraft/V1.1/Integrate card template art and compact icons (W13)")]
        public static void Integrate()
        {
            V11Setup.Integrate();
            ReimportCardTemplateThemes();
            BindCompactIcons();
        }

        /// <summary>Reimports every theme's copy of the card template sprites, after their default sprites are imported.</summary>
        [MenuItem("DiceCraft/V1.1/Reimport theme card template sprites (W13)")]
        public static void ReimportCardTemplateThemes()
        {
            int n=0;
            if(AssetDatabase.IsValidFolder(ThemesRoot))
                foreach(string dir in Directory.GetDirectories(ThemesRoot).Select(d=>d.Replace('\\','/')).OrderBy(d=>d,StringComparer.Ordinal))
                    foreach(string name in CardTemplateSprites)
                    {
                        string path=dir+"/UI/"+name+".png";
                        if(!File.Exists(path))continue;
                        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);n++;
                        var importer=(TextureImporter)AssetImporter.GetAtPath(path);var reference=V11ThemeImport.DefaultImporter(name);
                        if(reference==null||importer.spriteBorder!=reference.spriteBorder||!Mathf.Approximately(importer.spritePixelsPerUnit,reference.spritePixelsPerUnit))
                            throw new InvalidOperationException(path+": border "+importer.spriteBorder+" / PPU "+importer.spritePixelsPerUnit+" do not match the default sprite"+(reference==null?" (missing)":" ("+reference.spriteBorder+" / "+reference.spritePixelsPerUnit+")")+".");
                    }
            Debug.Log("V13_CARD_TEMPLATE_THEME_REIMPORT files="+n);
        }

        /// <summary>Binds Assets/Art/Art11/Icons/&lt;theme&gt;/icon_*.png to GameApp.compactIcons13, one set per theme folder.</summary>
        [MenuItem("DiceCraft/V1.1/Bind compact card icons (W13)")]
        public static void BindCompactIcons()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Binding requires stopped play mode.");
            AssetDatabase.Refresh();
            if(!AssetDatabase.IsValidFolder(IconsRoot))throw new InvalidOperationException("No "+IconsRoot+" folder.");
            var sets=new List<CompactIconSet13>();int reimported=0;
            foreach(string dir in Directory.GetDirectories(IconsRoot).Select(d=>d.Replace('\\','/')).OrderBy(d=>d,StringComparer.Ordinal))
            {
                string[] paths=Directory.GetFiles(dir,"icon_*.png").Select(p=>p.Replace('\\','/')).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
                if(paths.Length==0)continue;
                foreach(string path in paths)
                {
                    var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                    if(!V13IconImport.Matches(importer)){V13IconImport.Configure(importer);importer.SaveAndReimport();reimported++;}
                }
                var sprites=paths.Select(AssetDatabase.LoadAssetAtPath<Sprite>).ToArray();
                var missing=paths.Where((p,i)=>sprites[i]==null).ToArray();
                if(missing.Length>0)throw new InvalidOperationException("Not imported as sprites: "+string.Join(", ",missing));
                sets.Add(new CompactIconSet13{theme=Path.GetFileName(dir),icons=sprites});
            }
            if(!sets.Any(s=>string.Equals(s.theme,GameTheme.DefaultId,StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("No default icon set under "+IconsRoot+"/"+GameTheme.DefaultId+".");
            var scene=EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity");
            var app=Object.FindAnyObjectByType<GameApp>();if(app==null)throw new InvalidOperationException("Missing GameApp.");
            app.compactIcons13=sets.ToArray();
            EditorUtility.SetDirty(app);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("V13_COMPACT_ICONS_BOUND "+string.Join(" ",sets.Select(s=>s.theme+"="+s.icons.Length))+" reimported="+reimported);
        }
    }
}
