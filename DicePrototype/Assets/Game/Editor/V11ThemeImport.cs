using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DiceCraft.Editor
{
    /// <summary>
    /// V1.1 import settings for theme files under Assets/Resources/Themes/&lt;id&gt;/ (docs/specs/v11/DEV_PLAN.md
    /// §4.6, THEME_SPEC). A UI sprite copies the pixels-per-unit and nine-slice border of the default sprite
    /// with the same name, so it drops into every UiKit surface without layout changes; the theme generator
    /// must therefore draw it at the default sprite's pixel size and border geometry. Illustrations match
    /// the default illustration import except for a smaller maximum size (six themes share one build).
    /// </summary>
    public sealed class V11ThemeImport:AssetPostprocessor
    {
        public const string Root="Assets/Resources/Themes/";
        // Newest art set first: the scene's template array lets the newest set win for each name.
        private static readonly string[] DefaultFolders={"Assets/Art/Art073","Assets/Art/Art072","Assets/Art/Art071","Assets/Art/Art07","Assets/Game/Art"};

        private void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith(Root))return;
            var importer=(TextureImporter)assetImporter;
            string[] parts=assetPath.Substring(Root.Length).Split('/');
            if(parts.Length<2)return;
            string kind=parts.Length>=3?parts[1]:"";
            string name=Path.GetFileNameWithoutExtension(assetPath);
            importer.sRGBTexture=true;importer.alphaIsTransparency=true;importer.filterMode=FilterMode.Bilinear;
            if(kind=="Table")
            {
                importer.textureType=TextureImporterType.Default;importer.mipmapEnabled=true;
                importer.wrapMode=name=="felt_tile"?TextureWrapMode.Repeat:TextureWrapMode.Clamp;
                importer.maxTextureSize=4096;importer.textureCompression=TextureImporterCompression.CompressedHQ;
                return;
            }
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;
            if(kind=="UI")
            {
                // Surfaces stay uncompressed: thin ink lines and stepped corners band under block compression.
                importer.textureCompression=TextureImporterCompression.Uncompressed;
                var reference=DefaultImporter(name);
                importer.spritePixelsPerUnit=reference!=null?reference.spritePixelsPerUnit:200;
                importer.spriteBorder=reference!=null?reference.spriteBorder:Vector4.zero;
            }
            else if(kind=="Art")
            {
                importer.spritePixelsPerUnit=100;importer.maxTextureSize=1024;
                importer.textureCompression=TextureImporterCompression.CompressedHQ;
            }
            else
            {
                // preview.png and anything else at the theme root.
                importer.spritePixelsPerUnit=100;importer.maxTextureSize=1024;
                importer.textureCompression=TextureImporterCompression.CompressedHQ;
            }
        }

        /// <summary>The importer of the default sprite with this file name, newest art set first.</summary>
        public static TextureImporter DefaultImporter(string name)
        {
            foreach(string folder in DefaultFolders)
            {
                if(!AssetDatabase.IsValidFolder(folder))continue;
                foreach(string guid in AssetDatabase.FindAssets(name+" t:Texture2D",new[]{folder}))
                {
                    string path=AssetDatabase.GUIDToAssetPath(guid);
                    if(Path.GetFileNameWithoutExtension(path)!=name)continue;
                    if(AssetImporter.GetAtPath(path) is TextureImporter importer)return importer;
                }
            }
            return null;
        }

        /// <summary>Batch entry point: reimports every theme file with the rules above.</summary>
        public static void ReimportAll()
        {
            var paths=AssetDatabase.FindAssets("t:Texture2D",new[]{Root.TrimEnd('/')}).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            foreach(string path in paths)AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
            Debug.Log("V11_THEME_REIMPORT textures="+paths.Length);
        }
    }
}
