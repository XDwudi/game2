using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DiceCraft.Editor
{
    /// <summary>Reproducible import of the Image2 background; keeps the shared world shadow receiver.</summary>
    public static class V071TableSetup
    {
        public const string TexturePath="Assets/Art/Art071/Backgrounds/bg_table_scene.png";
        public const string MaterialPath="Assets/Art/Art071/Full/Materials/felt.mat";
        public static bool IsTableTexture(string path)=>path==TexturePath;

        public static void ConfigureImporter(TextureImporter importer)
        {
            importer.textureType=TextureImporterType.Default;
            importer.sRGBTexture=true;importer.alphaSource=TextureImporterAlphaSource.None;
            importer.alphaIsTransparency=false;importer.isReadable=false;
            importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;
            importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;
            importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        public static void BindMaterial(Material material)
        {
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if(texture==null)throw new InvalidOperationException("Missing authored table background: "+TexturePath);
            if(material==null||!material.HasProperty("_TableMap"))throw new InvalidOperationException("Missing compatible world felt material.");
            material.SetTexture("_TableMap",texture);material.SetFloat("_UseTableMap",1);
            EditorUtility.SetDirty(material);
        }

        [MenuItem("DiceCraft/Art071/Apply Image2 table background")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying||EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Table integration requires stopped Play mode and a clean scene.");
            AssetDatabase.ImportAsset(TexturePath,ImportAssetOptions.ForceSynchronousImport);
            ConfigureImporter((TextureImporter)AssetImporter.GetAtPath(TexturePath));
            BindMaterial(AssetDatabase.LoadAssetAtPath<Material>(MaterialPath));
            AssetDatabase.SaveAssets();
            Debug.Log("ART071_TABLE_APPLIED: shared world receiver, Clamp texture, live UI and dice retained.");
        }
    }
}
