using System;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using DiceCraft.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DiceCraft.Editor
{
    public static class V07Setup
    {
        public const string Root="Assets/Art/Art07";
        [MenuItem("DiceCraft/Art07/Integrate Art")]
        public static void Integrate()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit play mode before asset integration.");
            AssetDatabase.Refresh();
            foreach(var raw in Directory.GetFiles(Root,"*.png",SearchOption.AllDirectories))
            {
                var path=raw.Replace('\\','/');var im=(TextureImporter)AssetImporter.GetAtPath(path);
                bool sprite=!path.Contains("Backgrounds")&&!path.Contains("/Dice/Textures");
                im.textureType=sprite?TextureImporterType.Sprite:TextureImporterType.Default;im.spriteImportMode=SpriteImportMode.Single;
                im.filterMode=path.Contains("bg_felt")?FilterMode.Bilinear:FilterMode.Point;im.mipmapEnabled=false;im.alphaIsTransparency=true;
                im.textureCompression=TextureImporterCompression.Uncompressed;im.npotScale=TextureImporterNPOTScale.None;im.maxTextureSize=2048;
                im.wrapMode=path.Contains("bg_felt")?TextureWrapMode.Repeat:TextureWrapMode.Clamp;
                if(path.Contains("/Templates/"))im.spriteBorder=new Vector4(16,16,16,16);
                im.SaveAndReimport();
            }
            foreach(var raw in Directory.GetFiles(Root,"*.fbx",SearchOption.AllDirectories))
            {
                var path=raw.Replace('\\','/');var im=(ModelImporter)AssetImporter.GetAtPath(path);
                im.globalScale=1;im.useFileScale=true;im.bakeAxisConversion=true;im.importCameras=false;im.importLights=false;im.addCollider=false;
                im.importAnimation=path.Contains("Animation");im.animationType=ModelImporterAnimationType.Generic;im.SaveAndReimport();
            }
            Art07AssetLibrary.Build();
            var scene=EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity");
            var app=UnityEngine.Object.FindFirstObjectByType<GameApp>();if(app==null)throw new InvalidOperationException("Missing GameApp.");
            var icons=Directory.GetFiles(Root+"/ContentIcons","*.png",SearchOption.AllDirectories).Select(p=>AssetDatabase.LoadAssetAtPath<Sprite>(p.Replace('\\','/'))).ToArray();
            if(icons.Length!=71)throw new InvalidOperationException("All 71 current content icons must be present.");
            app.contentIcons=icons;
            app.enchantIcons=Directory.GetFiles(Root+"/UI/Badges","badge_enchant_*.png").Select(p=>AssetDatabase.LoadAssetAtPath<Sprite>(p.Replace('\\','/'))).ToArray();
            app.art07Templates=Directory.GetFiles(Root+"/UI","*.png",SearchOption.AllDirectories).Select(p=>AssetDatabase.LoadAssetAtPath<Sprite>(p.Replace('\\','/'))).ToArray();
            app.art07Sounds=Directory.GetFiles(Root+"/Audio","*.wav").Select(p=>AssetDatabase.LoadAssetAtPath<AudioClip>(p.Replace('\\','/'))).ToArray();
            app.art07Prefabs=Directory.GetFiles(Root,"*.prefab",SearchOption.AllDirectories).Select(p=>AssetDatabase.LoadAssetAtPath<GameObject>(p.Replace('\\','/')).GetComponent<Art07PrefabView>()).Where(v=>v!=null).ToArray();
            if(app.art07Prefabs.Length!=26)throw new InvalidOperationException("Expected 26 authored view prefabs.");
            app.art07NumberFont=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root+"/Fonts/font_numbers_tmp.asset");
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Dice/Models/dice_body_bevel.fbx");
            if(model!=null)app.table.bodyMesh=model.GetComponentsInChildren<MeshFilter>().Select(m=>m.sharedMesh).First(m=>m!=null);
            app.table.bodyMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Dice/Materials/mat_dice_ceramic.mat");
            app.table.glassMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Dice/Materials/mat_dice_glass.mat");
            app.table.faceMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Dice/Materials/mat_dice_pips.mat");
            app.table.rollClips=new[]{"dice_roll_a","dice_roll_b"}.Select(id=>AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/FX/Animations/"+id+".anim")).ToArray();
            if(app.table.rollClips.Any(c=>c==null))throw new InvalidOperationException("Authored roll clips are required.");
            string contourPath=Root+"/Dice/Materials/dice_contour.mat";var contour=AssetDatabase.LoadAssetAtPath<Material>(contourPath);
            if(contour==null){contour=new Material(Shader.Find("DiceCraft/Art07/Contour"));AssetDatabase.CreateAsset(contour,contourPath);}contour.SetColor("_BaseColor",UiKit.Ink);app.table.outlineMaterial=contour;
            var camera=app.table.viewCamera;camera.backgroundColor=UiKit.Background;camera.transform.position=new Vector3(0,20,-11.547f);camera.transform.LookAt(Vector3.zero);
            var key=UnityEngine.Object.FindFirstObjectByType<Light>();if(key!=null){key.transform.rotation=Quaternion.Euler(55,155,0);key.shadowStrength=.35f;key.shadowBias=.04f;key.shadowNormalBias=.1f;}
            var felt=GameObject.Find("Art07Felt");if(felt==null)felt=GameObject.CreatePrimitive(PrimitiveType.Plane);felt.name="Art07Felt";
            var collider=felt.GetComponent<Collider>();if(collider!=null)UnityEngine.Object.DestroyImmediate(collider);
            felt.transform.position=new Vector3(0,-.58f,0);felt.transform.localScale=new Vector3(10,1,10);
            string materialPath=Root+"/Backgrounds/felt.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(material==null){material=new Material(Shader.Find("DiceCraft/Art07/Felt"));AssetDatabase.CreateAsset(material,materialPath);}material.shader=Shader.Find("DiceCraft/Art07/Felt");
            material.SetColor("_BaseColor",UiKit.Felt);material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Backgrounds/bg_felt_tile.png"));material.SetTextureScale("_BaseMap",new Vector2(24,24));
            felt.GetComponent<MeshRenderer>().sharedMaterial=material;
            app.canvas.GetComponent<UnityEngine.UI.CanvasScaler>().referenceResolution=new Vector2(1920,1080);
            WarmFont(app.uiFont);
            PlayerSettings.bundleVersion="0.7.0";
            EditorUtility.SetDirty(app);EditorUtility.SetDirty(material);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Directory.CreateDirectory("../evidence/v07/unity");File.WriteAllText("../evidence/v07/unity/art-setup.json",JsonUtility.ToJson(new Proof{unity=Application.unityVersion,art=GameApp.ArtVersion,gameplay=app.Config?.configVersion??"p0.6.0",newIcons=icons.Length,bodyTriangles=app.table.bodyMesh.triangles.Length/3},true));
            Debug.Log("ART07_INTEGRATED");
        }
        private static Material Mat(string name,Color color,float glass)
        {
            string dir=Root+"/Dice/Materials";Directory.CreateDirectory(dir);string path=dir+"/"+name+".mat";
            var shader=Shader.Find("DiceCraft/Art07/Ceramic");if(shader==null)throw new InvalidOperationException("Missing ceramic shader.");
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}mat.shader=shader;mat.SetColor("_BaseColor",color);mat.SetColor("_EdgeColor",UiKit.Ink);mat.SetFloat("_Glass",glass);EditorUtility.SetDirty(mat);return mat;
        }
        public static void WarmFont(TMP_FontAsset font)
        {
            var paths=Directory.GetFiles("Assets/Game","*.cs",SearchOption.AllDirectories).Where(p=>!p.Split(Path.DirectorySeparatorChar).Any(x=>x.EndsWith("~")));
            string chars=string.Concat(paths.Select(File.ReadAllText))+File.ReadAllText("Assets/Game/Data/P0Config.json");
            font.atlasPopulationMode=AtlasPopulationMode.Dynamic;var requested=new string(chars.Where(c=>!char.IsControl(c)).Distinct().Where(c=>!font.HasCharacter(c,false,false)).ToArray());
            if(requested.Length>0)font.TryAddCharacters(requested,out _,true);
            font.atlasPopulationMode=AtlasPopulationMode.Static;
            foreach(var t in font.atlasTextures)if(!AssetDatabase.Contains(t))AssetDatabase.AddObjectToAsset(t,font);
            EditorUtility.SetDirty(font);
        }
        [Serializable]private sealed class Proof{public string unity,art,gameplay;public int newIcons,bodyTriangles;}
    }
}
