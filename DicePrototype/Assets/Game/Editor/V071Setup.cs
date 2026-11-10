using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DiceCraft.Editor
{
    public static class V071Setup
    {
        public const string Root="Assets/Art/Art071";
        [MenuItem("DiceCraft/Art071/Integrate print art and style lab")]
        public static void Integrate()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
            var current=EditorSceneManager.GetActiveScene();
            if(current.isDirty)throw new InvalidOperationException("Save or review current scene edits before integration.");
            AssetDatabase.Refresh();
            foreach(var raw in Directory.GetFiles(Root,"*.png",SearchOption.AllDirectories))
            {
                string path=raw.Replace('\\','/');var im=(TextureImporter)AssetImporter.GetAtPath(path);
                if(V071TableSetup.IsTableTexture(path)){V071TableSetup.ConfigureImporter(im);continue;}
                im.textureType=TextureImporterType.Sprite;im.spriteImportMode=SpriteImportMode.Single;
                im.spritePixelsPerUnit=path.Contains("/UI/")?200:100;
                im.spriteBorder=path.Contains("/UI/")?new Vector4(32,32,32,32):Vector4.zero;
                var settings=new TextureImporterSettings();im.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;im.SetTextureSettings(settings);
                im.filterMode=FilterMode.Bilinear;im.wrapMode=TextureWrapMode.Clamp;im.mipmapEnabled=false;im.alphaIsTransparency=true;
                im.textureCompression=TextureImporterCompression.Uncompressed;im.maxTextureSize=2048;im.npotScale=TextureImporterNPOTScale.None;im.SaveAndReimport();
            }
            string modelPath=Root+"/Dice/dice_body_bevel.fbx";var importer=(ModelImporter)AssetImporter.GetAtPath(modelPath);
            importer.globalScale=1;importer.useFileScale=true;importer.bakeAxisConversion=true;importer.importCameras=false;importer.importLights=false;importer.addCollider=false;importer.importAnimation=false;importer.SaveAndReimport();
            var scene=EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity");
            var app=UnityEngine.Object.FindAnyObjectByType<GameApp>();if(app==null)throw new InvalidOperationException("Bootstrap GameApp is required.");
            var art=Directory.GetFiles(Root+"/Illustrations","*.png").Select(p=>AssetDatabase.LoadAssetAtPath<Sprite>(p.Replace('\\','/'))).ToArray();
            string[] replaced=art.Select(s=>s.name.Substring("illustration_".Length)).ToArray();
            app.contentIcons=app.contentIcons.Where(s=>s!=null&&!replaced.Any(id=>s.name.Equals("icon_"+id,StringComparison.OrdinalIgnoreCase)||s.name.Equals("illustration_"+id,StringComparison.OrdinalIgnoreCase)||s.name.Equals(id,StringComparison.OrdinalIgnoreCase))).Concat(art).ToArray();
            var surfaces=Directory.GetFiles(Root+"/UI","*.png").Select(p=>AssetDatabase.LoadAssetAtPath<Sprite>(p.Replace('\\','/'))).ToArray();
            app.art07Templates=app.art07Templates.Where(s=>s!=null&&!surfaces.Any(n=>n.name==s.name)).Concat(surfaces).ToArray();
            app.table.bodyMesh=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath).GetComponentsInChildren<MeshFilter>().Select(m=>m.sharedMesh).First(m=>m!=null);
            app.table.bodyMaterial=CopyMaterial(app.table.bodyMaterial,"mat_dice_ceramic");
            app.table.glassMaterial=CopyMaterial(app.table.glassMaterial,"mat_dice_glass");
            app.table.outlineMaterial=CopyMaterial(app.table.outlineMaterial,"dice_contour");
            app.table.bodyMaterial.SetFloat("_Smoothness",.48f);app.table.bodyMaterial.SetFloat("_Specular",.16f);
            app.table.outlineMaterial.SetFloat("_PixelWidth",1f);
            foreach(var m in new[]{app.table.bodyMaterial,app.table.glassMaterial,app.table.outlineMaterial})EditorUtility.SetDirty(m);
            var key=UnityEngine.Object.FindObjectsByType<Light>().FirstOrDefault(l=>l.type==LightType.Directional);
            if(key!=null){key.transform.rotation=Quaternion.Euler(70,145,0);key.shadows=LightShadows.Soft;key.shadowStrength=.7f;key.shadowBias=.015f;key.shadowNormalBias=.035f;}
            V07Setup.WarmFont(app.uiFont);
            PlayerSettings.bundleVersion="0.7.1";
            EditorUtility.SetDirty(app);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            // The lab is a separate scene using the same authored scene/resources and real UI.
            const string lab="Assets/Game/Scenes/ArtStyleLab.unity";
            EditorSceneManager.SaveScene(scene,lab,true);
            scene=EditorSceneManager.OpenScene(lab);
            new GameObject("ArtStyleLab_IsolatedPreview",typeof(Art071StyleLab));
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("ART071_INTEGRATED: "+art.Length+" illustrations; "+app.contentIcons.Length+" content IDs; 300 triangle master.");
        }
        private static Material CopyMaterial(Material source,string name)
        {
            string path=Root+"/Dice/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(source);AssetDatabase.CreateAsset(material,path);}return material;
        }
    }
}

