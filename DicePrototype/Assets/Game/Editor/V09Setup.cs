using System;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DiceCraft.Editor
{
    /// <summary>
    /// p0.9.0 content art: imports the staged illustrations of the 50 new controllers and the O08 service with the
    /// same settings as the v0.71 illustrations, then rebuilds <c>GameApp.contentIcons</c> in configuration order in
    /// Bootstrap and ArtStyleLab. Nothing else in either scene, no prefab and no surface binding is touched, so the
    /// v0.72 / v0.73 integrations stay as they are.
    /// </summary>
    public static class V09Setup
    {
        private const string Folder="Assets/Art/Art071/Illustrations";
        [MenuItem("DiceCraft/Art09/Integrate new content illustrations")]
        public static void Integrate()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Integration requires stopped play mode.");
            AssetDatabase.Refresh();
            var cfg=JsonUtility.FromJson<GameConfig>(GameConfig.PrepareJsonForUnity(File.ReadAllText("Assets/Game/Data/P0Config.json")));cfg.Validate();
            var ids=cfg.controllers.Select(x=>x.id).Concat(cfg.consumables.Select(x=>x.id)).Concat(cfg.services.Select(x=>x.id)).Concat(cfg.diceUpgrades.Select(x=>x.id)).ToArray();
            var sprites=new Sprite[ids.Length];int imported=0;
            for(int i=0;i<ids.Length;i++)
            {
                string path=Folder+"/illustration_"+ids[i].ToLowerInvariant()+".png";
                var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                if(importer==null)throw new InvalidOperationException("Missing illustration: "+path);
                if(Configure(importer)){importer.SaveAndReimport();imported++;}
                sprites[i]=AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if(sprites[i]==null)throw new InvalidOperationException("Illustration did not import as a sprite: "+path);
            }
            foreach(string scenePath in new[]{"Assets/Game/Scenes/Bootstrap.unity","Assets/Game/Scenes/ArtStyleLab.unity"})
            {
                var scene=EditorSceneManager.OpenScene(scenePath);
                var app=Object.FindAnyObjectByType<GameApp>();if(app==null)throw new InvalidOperationException("Missing GameApp in "+scenePath);
                app.contentIcons=sprites.ToArray();
                EditorUtility.SetDirty(app);EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save "+scenePath);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"ART09_CONTENT_INTEGRATED illustrations={sprites.Length} reimported={imported}");
        }
        /// <summary>The v0.71 illustration import settings (V071FullSetup); returns whether anything had to change.</summary>
        private static bool Configure(TextureImporter importer)
        {
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            bool same=importer.textureType==TextureImporterType.Sprite&&importer.spriteImportMode==SpriteImportMode.Single&&Mathf.Approximately(importer.spritePixelsPerUnit,100)
                &&importer.spriteBorder==Vector4.zero&&settings.spriteMeshType==SpriteMeshType.FullRect&&importer.filterMode==FilterMode.Bilinear&&importer.wrapMode==TextureWrapMode.Clamp
                &&!importer.mipmapEnabled&&importer.alphaIsTransparency&&importer.sRGBTexture&&importer.textureCompression==TextureImporterCompression.Uncompressed
                &&importer.maxTextureSize==2048&&importer.npotScale==TextureImporterNPOTScale.None;
            if(same)return false;
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=100;importer.spriteBorder=Vector4.zero;
            importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
            importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.sRGBTexture=true;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;importer.npotScale=TextureImporterNPOTScale.None;
            return true;
        }
    }
}
