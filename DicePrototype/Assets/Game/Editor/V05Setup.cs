using System;
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
    public static class V05Setup
    {
        [MenuItem("DiceCraft/Upgrade v0.5 Assets and Profiles")]
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
            app.contentIcons=Directory.GetFiles("Assets/Game/Art/Icons","*.png").OrderBy(x=>x).Select(x=>AssetDatabase.LoadAssetAtPath<Sprite>(x.Replace('\\','/'))).ToArray();
            app.enchantIcons=Directory.GetFiles("Assets/Game/Art/Textures","Enchant_*.png").OrderBy(x=>x).Select(x=>AssetDatabase.LoadAssetAtPath<Sprite>(x.Replace('\\','/'))).ToArray();
            app.sounds=Directory.GetFiles("Assets/Game/Audio","*.wav").OrderBy(x=>x).Select(x=>AssetDatabase.LoadAssetAtPath<AudioClip>(x.Replace('\\','/'))).ToArray();
            string chars=File.ReadAllText("Assets/Game/Data/P0Config.json");
            foreach(var path in Directory.GetFiles("Assets/Game/UI","*.cs"))chars+=File.ReadAllText(path);
            chars+=File.ReadAllText("Assets/Game/Runtime/GameApp.cs");
            foreach(var path in Directory.GetFiles("Assets/Game/Core","*.cs",SearchOption.AllDirectories))chars+=File.ReadAllText(path);
            var requested=chars.Where(c=>!char.IsControl(c)).Distinct().ToArray();
            var absent=new string(requested.Where(c=>!app.uiFont.HasCharacter(c,false,false)).ToArray());
            if(absent.Length>0)app.uiFont.TryAddCharacters(absent,out _,true);
            string missing=new string(requested.Where(c=>!app.uiFont.HasCharacter(c,false,false)).ToArray());
            if(missing.Length>0)throw new InvalidOperationException("Font does not cover UI/config/rule messages: "+missing);
            foreach(var texture in app.uiFont.atlasTextures)if(!AssetDatabase.Contains(texture))AssetDatabase.AddObjectToAsset(texture,app.uiFont);
            EditorUtility.SetDirty(app.uiFont);EditorUtility.SetDirty(app);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            PlayerSettings.bundleVersion="0.5.0";
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneOSX,false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneOSX,new[]{GraphicsDeviceType.Metal});
            CreateProfile("Windows","Windows_x64");CreateProfile("macOS","macOS_Universal");
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("../evidence/v05/unity");
            File.WriteAllText("../evidence/v05/unity/setup.json",JsonUtility.ToJson(new Proof{unity=Application.unityVersion,icons=app.contentIcons.Length,enchantIcons=app.enchantIcons.Length,sounds=app.sounds.Length,missingGlyphs=missing},true));
            Debug.Log("V05_SETUP_COMPLETE");
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
            Directory.CreateDirectory("../evidence/v05/unity");
            File.WriteAllText("../evidence/v05/unity/profile-"+name+".json",EditorJsonUtility.ToJson(profile,true));
        }
        [Serializable]private sealed class Proof{public string unity,missingGlyphs;public int icons,enchantIcons,sounds;}
    }
}
