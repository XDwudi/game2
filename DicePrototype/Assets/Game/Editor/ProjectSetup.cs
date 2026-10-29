using System;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;

namespace DiceCraft.Editor
{
    public static class ProjectSetup
    {
        private const string Root="Assets/Game";
        [MenuItem("DiceCraft/Rebuild Bootstrap Scene")]
        public static void Create()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode before setup.");
            Directory.CreateDirectory(Root+"/Scenes");Directory.CreateDirectory(Root+"/Runtime/Materials");
            AssetDatabase.Refresh();
            foreach(string path in Directory.GetFiles(Root+"/Art/Icons","*.png"))
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));
                if(importer.textureType!=TextureImporterType.Sprite||importer.spriteImportMode!=SpriteImportMode.Single){importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.SaveAndReimport();}
            }
            foreach(string path in Directory.GetFiles(Root+"/Art/Models","*.fbx"))
            {
                var importer=(ModelImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));
                importer.globalScale=1;importer.useFileScale=true;importer.bakeAxisConversion=true;
                importer.importCameras=false;importer.importLights=false;importer.importBlendShapes=false;importer.addCollider=false;
                importer.importAnimation=path.Contains("Roll_");importer.animationType=ModelImporterAnimationType.Generic;importer.SaveAndReimport();
            }
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root+"/Fonts/DiceChinese.asset");
            if(font==null)
            {
                var source=AssetDatabase.LoadAssetAtPath<Font>(Root+"/Fonts/NotoSansCJKsc-Regular.otf");
                if(source==null)throw new InvalidOperationException("Missing licensed CJK font.");
                font=TMP_FontAsset.CreateFontAsset(source,48,7,GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);
                font.name="DiceChinese";font.isMultiAtlasTexturesEnabled=true;
                AssetDatabase.CreateAsset(font,Root+"/Fonts/DiceChinese.asset");
                AssetDatabase.AddObjectToAsset(font.material,font);
                foreach(var texture in font.atlasTextures){texture.name="DiceChineseAtlas";AssetDatabase.AddObjectToAsset(texture,font);}
                string chars=File.ReadAllText(Root+"/Data/P0Config.json");
                foreach(var path in Directory.GetFiles(Root+"/UI","*.cs"))chars+=File.ReadAllText(path);
                chars+=File.ReadAllText(Root+"/Runtime/GameApp.cs");
                chars+=string.Concat(Enumerable.Range(32,95).Select(x=>(char)x));
                font.TryAddCharacters(new string(chars.Distinct().ToArray()),out string missing,true);
                foreach(var texture in font.atlasTextures)if(!AssetDatabase.Contains(texture))AssetDatabase.AddObjectToAsset(texture,font);
                EditorUtility.SetDirty(font);
                Debug.Log("Font preload missing glyphs: "+missing);
            }
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Art/Models/DiceBody.fbx");
            var mesh=model.GetComponentsInChildren<MeshFilter>(true).Select(x=>x.sharedMesh).First(m=>m!=null&&m.vertexCount>20);
            if(mesh.triangles.Length/3>2000)throw new InvalidOperationException("Die exceeds triangle budget.");
            var lit=Shader.Find("Universal Render Pipeline/Lit");if(lit==null)throw new InvalidOperationException("URP shader missing.");
            var body=MaterialAsset("Ivory",lit,new Color(.87f,.9f,.83f),.15f,.35f);
            var glass=MaterialAsset("Glass",lit,new Color(.40f,.68f,.71f),.60f,.80f);
            var face=MaterialAsset("Face",Shader.Find("DiceCraft/VertexFace"),Color.white,0,0);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();camera.tag="MainCamera";
            camera.transform.position=new Vector3(0,18,-12);camera.transform.LookAt(Vector3.zero);
            camera.orthographic=true;camera.orthographicSize=6.75f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=UiKit.Ink;camera.nearClipPlane=.1f;camera.farClipPlane=100;
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
            var light=new GameObject("Key Light",typeof(Light)).GetComponent<Light>();light.type=LightType.Directional;light.intensity=1.15f;light.transform.rotation=Quaternion.Euler(45,-25,0);light.shadows=LightShadows.Soft;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.72f,.75f);
            var canvas=new GameObject("HUD",typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster)).GetComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var events=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();events.GetComponent<EventSystem>().sendNavigationEvents=false;
            var table=new GameObject("DiceTable").AddComponent<DiceTable>();table.viewCamera=camera;table.bodyMesh=mesh;table.bodyMaterial=body;table.glassMaterial=glass;table.faceMaterial=face;
            table.rollClips=new[]{"Roll_A","Roll_B"}.Select(n=>AssetDatabase.LoadAllAssetsAtPath(Root+"/Art/Models/"+n+".fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"))).ToArray();
            var app=new GameObject("Game").AddComponent<GameApp>();app.canvas=canvas;app.table=table;app.uiFont=font;app.configAsset=AssetDatabase.LoadAssetAtPath<TextAsset>(Root+"/Data/P0Config.json");
            app.contentIcons=Directory.GetFiles(Root+"/Art/Icons","*.png").OrderBy(x=>x).Select(x=>AssetDatabase.LoadAssetAtPath<Sprite>(x.Replace('\\','/'))).ToArray();
            app.sounds=Directory.GetFiles(Root+"/Audio","*.wav").OrderBy(x=>x).Select(x=>AssetDatabase.LoadAssetAtPath<AudioClip>(x.Replace('\\','/'))).ToArray();
            QualitySettings.vSyncCount=0;PlayerSettings.companyName="XDwudi";PlayerSettings.productName="DiceCraft P0";
            PlayerSettings.defaultScreenWidth=1920;PlayerSettings.defaultScreenHeight=1080;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.runInBackground=true;
            PlayerSettings.enableFrameTimingStats=true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{GraphicsDeviceType.Direct3D11});
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            EditorSettings.serializationMode=SerializationMode.ForceText;
            EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.SaveScene(scene,Root+"/Scenes/Bootstrap.unity");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Root+"/Scenes/Bootstrap.unity",true)};
            AssetDatabase.SaveAssets();
            string proof=JsonUtility.ToJson(new SetupProof{unity=Application.unityVersion,meshVertices=mesh.vertexCount,meshTriangles=mesh.triangles.Length/3,icons=app.contentIcons.Length,sounds=app.sounds.Length,scene=scene.path},true);
            Directory.CreateDirectory("../evidence/unity");File.WriteAllText("../evidence/unity/setup.json",proof);
            Debug.Log("DICECRAFT_SETUP_OK "+proof);
        }
        private static Material MaterialAsset(string name,Shader shader,Color color,float metal,float smooth)
        {
            if(shader==null)throw new InvalidOperationException("Missing shader "+name);
            string path=Root+"/Runtime/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(shader);AssetDatabase.CreateAsset(m,path);}m.SetColor("_BaseColor",color);
            if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",metal);if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",smooth);EditorUtility.SetDirty(m);return m;
        }
        [MenuItem("DiceCraft/Build Windows")]
        public static void BuildWindows()
        {
            Directory.CreateDirectory("../Builds/Windows");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Root+"/Scenes/Bootstrap.unity"},locationPathName="../Builds/Windows/DiceCraft.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            Directory.CreateDirectory("../evidence/unity");
            File.WriteAllText("../evidence/unity/build.json",JsonUtility.ToJson(new BuildProof{result=report.summary.result.ToString(),errors=report.summary.totalErrors,warnings=report.summary.totalWarnings,bytes=report.summary.totalSize,seconds=report.summary.totalTime.TotalSeconds},true));
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new InvalidOperationException("Windows build failed.");
        }
        [Serializable]private sealed class SetupProof{public string unity,scene;public int meshVertices,meshTriangles,icons,sounds;}
        [Serializable]private sealed class BuildProof{public string result;public int errors,warnings;public ulong bytes;public double seconds;}
    }
}
