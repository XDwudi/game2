using System;
using DiceCraft.Core;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace DiceCraft.Editor
{
    public static class V06Setup
    {
        [MenuItem("DiceCraft/Upgrade v0.6 Assets and Profiles")]
        public static void Upgrade()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode first.");
            AssetDatabase.Refresh();
            foreach(var path in Directory.GetFiles("Assets/Game/Art","*.png",SearchOption.AllDirectories).Where(p=>p.Contains("Icons")||p.Contains("Enchant")))
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.SaveAndReimport();
            }
            var scene=EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity");
            var app=UnityEngine.Object.FindFirstObjectByType<GameApp>();
            if(app==null)throw new InvalidOperationException("Existing Bootstrap is missing GameApp.");
            // TMP otherwise clears the warmed atlas on Editor quit as well as build,
            // invalidating frozen source hashes and forcing first-use glyph work.
            var fontSettings=new SerializedObject(app.uiFont);
            fontSettings.FindProperty("m_ClearDynamicDataOnBuild").boolValue=false;
            fontSettings.ApplyModifiedPropertiesWithoutUndo();
            app.uiFont.atlasPopulationMode=AtlasPopulationMode.Dynamic;
            var cfg=JsonUtility.FromJson<GameConfig>(GameConfig.PrepareJsonForUnity(File.ReadAllText("Assets/Game/Data/P0Config.json")));cfg.Validate();
            var active=cfg.controllers.Select(x=>x.id).Concat(cfg.consumables.Select(x=>x.id)).Concat(cfg.services.Select(x=>x.id)).Concat(cfg.diceUpgrades.Select(x=>x.id)).ToArray();
            app.contentIcons=Directory.GetFiles("Assets/Game/Art/Icons","*.png").Where(x=>active.Contains(Path.GetFileNameWithoutExtension(x))).OrderBy(x=>x).Select(x=>AssetDatabase.LoadAssetAtPath<Sprite>(x.Replace('\\','/'))).ToArray();
            app.enchantIcons=Directory.GetFiles("Assets/Game/Art/Textures","Enchant_*.png").Where(x=>cfg.enchantments.Any(e=>e.id!="none"&&"Enchant_"+e.id==Path.GetFileNameWithoutExtension(x))).OrderBy(x=>x).Select(x=>AssetDatabase.LoadAssetAtPath<Sprite>(x.Replace('\\','/'))).ToArray();
            app.sounds=Directory.GetFiles("Assets/Game/Audio","*.wav").Where(x=>Path.GetFileNameWithoutExtension(x)!="enchant_base5"&&Path.GetFileNameWithoutExtension(x)!="enchant_mult120").OrderBy(x=>x).Select(x=>AssetDatabase.LoadAssetAtPath<AudioClip>(x.Replace('\\','/'))).ToArray();
            string chars=File.ReadAllText("Assets/Game/Data/P0Config.json");
            foreach(var path in Directory.GetFiles("Assets/Game/UI","*.cs"))chars+=File.ReadAllText(path);
            chars+=File.ReadAllText("Assets/Game/Runtime/GameApp.cs");
            foreach(var path in Directory.GetFiles("Assets/Game/Core","*.cs",SearchOption.AllDirectories))chars+=File.ReadAllText(path);
            var requested=chars.Where(c=>!char.IsControl(c)).Distinct().ToArray();
            var absent=new string(requested.Where(c=>!app.uiFont.HasCharacter(c,false,false)).ToArray());
            if(absent.Length>0)app.uiFont.TryAddCharacters(absent,out _,true);
            string missing=new string(requested.Where(c=>!app.uiFont.HasCharacter(c,false,false)).ToArray());
            if(missing.Length>0)throw new InvalidOperationException("Font does not cover UI/config/rule messages: "+missing);
            app.uiFont.atlasPopulationMode=AtlasPopulationMode.Static;
            const string fallbackPath="Assets/Game/Fonts/DiceChineseFallback.asset";
            var fallback=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fallbackPath);
            if(fallback==null)
            {
                var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/Fonts/NotoSansCJKsc-Regular.otf");
                fallback=TMP_FontAsset.CreateFontAsset(source,48,7,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,true);
                fallback.name="DiceChineseFallback";fallback.isMultiAtlasTexturesEnabled=true;
                fallback.ClearFontAssetData(true);AssetDatabase.CreateAsset(fallback,fallbackPath);
                AssetDatabase.AddObjectToAsset(fallback.material,fallback);
                foreach(var texture in fallback.atlasTextures)AssetDatabase.AddObjectToAsset(texture,fallback);
            }
            app.uiFont.fallbackFontAssetTable=new System.Collections.Generic.List<TMP_FontAsset>{fallback};
            foreach(var texture in app.uiFont.atlasTextures)if(!AssetDatabase.Contains(texture))AssetDatabase.AddObjectToAsset(texture,app.uiFont);
            EditorUtility.SetDirty(app.uiFont);EditorUtility.SetDirty(app);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            PlayerSettings.bundleVersion="0.6.0";
            PlayerSettings.fullScreenMode=FullScreenMode.FullScreenWindow;PlayerSettings.resizableWindow=true;PlayerSettings.macRetinaSupport=true;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneOSX,false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneOSX,new[]{GraphicsDeviceType.Metal});
            CreateProfile("Windows","Windows_x64");CreateProfile("macOS","macOS_Universal");
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("../evidence/v06/unity");
            File.WriteAllText("../evidence/v06/unity/setup.json",JsonUtility.ToJson(new Proof{unity=Application.unityVersion,icons=app.contentIcons.Length,enchantIcons=app.enchantIcons.Length,sounds=app.sounds.Length,missingGlyphs=missing},true));
            Debug.Log("V06_SETUP_COMPLETE");
        }
        private static void CreateProfile(string platformName,string name)
        {
            const string folder="Assets/Settings/BuildProfiles";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            var profile=AssetDatabase.LoadAssetAtPath<BuildProfile>(folder+"/"+name+".asset");
            if(profile==null)
            {
                var installed=BuildProfile.GetInstalledPlatformModules().Where(p=>p.displayName.IndexOf(platformName,StringComparison.OrdinalIgnoreCase)>=0).ToArray();
                if(installed.Length!=1)throw new InvalidOperationException("Need one platform "+platformName+": "+string.Join(",",installed.Select(p=>p.displayName)));
                profile=BuildProfile.CreateBuildProfile(installed[0].platformGuid,name);
                var error=AssetDatabase.MoveAsset(AssetDatabase.GetAssetPath(profile),folder+"/"+name+".asset");
                if(!string.IsNullOrEmpty(error))throw new InvalidOperationException(error);
            }
            profile.overrideGlobalScenes=true;profile.scenes=new[]{new EditorBuildSettingsScene("Assets/Game/Scenes/Bootstrap.unity",true)};
            var serialized=new SerializedObject(profile);var settings=serialized.FindProperty("m_PlatformBuildProfile");
            if(settings==null)throw new InvalidOperationException("Build profile settings missing.");
            serialized.FindProperty("m_Subtarget").intValue=(int)StandaloneBuildSubtarget.Player;
            var architecture=settings.FindPropertyRelative("m_Architecture");
            if(architecture==null)throw new InvalidOperationException("Architecture option missing.");
            // Verified against the installed 6000.6.7f1 modules: Windows x64=0;
            // MacOSArchitecture x64=0, ARM64=1, x64ARM64=2.
            architecture.intValue=platformName=="macOS"?2:0;
            var development=settings.FindPropertyRelative("m_Development");if(development==null)throw new InvalidOperationException("Development option missing.");development.boolValue=true;
            var compression=settings.FindPropertyRelative("m_CompressionType");if(compression!=null)
            {
                var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEditor.Compression")).FirstOrDefault(t=>t!=null);
                if(type==null)throw new InvalidOperationException("Compression enum unavailable.");
                string lz4=Enum.GetNames(type).Single(n=>n.Equals("Lz4",StringComparison.OrdinalIgnoreCase));
                compression.intValue=Convert.ToInt32(Enum.Parse(type,lz4));
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(profile);
            Directory.CreateDirectory("../evidence/v06/unity");
            File.WriteAllText("../evidence/v06/unity/profile-"+name+".json",EditorJsonUtility.ToJson(profile,true));
        }
        [Serializable]private sealed class Proof{public string unity,missingGlyphs;public int icons,enchantIcons,sounds;}
    }
}
