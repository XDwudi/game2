using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiceCraft.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DiceCraft.Editor
{
    /// <summary>
    /// v0.72 HD refresh: imports Assets/Art/Art072 and rebinds serialized consumers by sprite name.
    /// Sprites without a v0.72 replacement (illustrations, symbols, FX) keep their v0.71 art; Art071 is not modified.
    /// </summary>
    public static class V072Setup
    {
        public const string Root="Assets/Art/Art072";
        private const string Previous="Assets/Art/Art071";
        private const int Border=40; // 20 reference units at PPU 200: outline, bevel and corner radius stay unstretched.

        public static void ConfigureImporter(string path,TextureImporter importer)
        {
            string file=Path.GetFileName(path);
            bool background=path.Contains("/Backgrounds/"),surface=path.Contains("/Surfaces/");
            importer.textureType=background?TextureImporterType.Default:TextureImporterType.Sprite;
            if(!background)
            {
                importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=surface?200:100;
                importer.spriteBorder=!surface?Vector4.zero:file.StartsWith("ui_progress_",StringComparison.Ordinal)?new Vector4(48,0,48,0):new Vector4(Border,Border,Border,Border);
                var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
                settings.spriteMeshType=file=="fx_square_chip.png"||file=="fx_short_flame.png"?SpriteMeshType.Tight:SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
            }
            importer.filterMode=FilterMode.Bilinear;
            importer.wrapMode=file=="bg_felt_tile.png"?TextureWrapMode.Repeat:TextureWrapMode.Clamp;
            importer.mipmapEnabled=false;importer.alphaIsTransparency=!background;importer.sRGBTexture=true;
            importer.alphaSource=background?TextureImporterAlphaSource.None:TextureImporterAlphaSource.FromInput;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;importer.npotScale=TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }

        [MenuItem("DiceCraft/Art072/Integrate HD surfaces and table")]
        public static void Integrate()
        {
            if(EditorApplication.isPlaying||EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Integration requires stopped play mode and a clean scene.");
            AssetDatabase.Refresh();
            string[] pngs=Directory.GetFiles(Root,"*.png",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')).ToArray();
            foreach(string path in pngs)ConfigureImporter(path,(TextureImporter)AssetImporter.GetAtPath(path));
            var sprites=pngs.Select(AssetDatabase.LoadAssetAtPath<Sprite>).Where(s=>s!=null).ToDictionary(s=>s.name,StringComparer.OrdinalIgnoreCase);
            var textures=pngs.Select(AssetDatabase.LoadAssetAtPath<Texture2D>).Where(t=>t!=null).ToDictionary(t=>t.name,StringComparer.OrdinalIgnoreCase);

            var scene=EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity");
            var app=Object.FindAnyObjectByType<GameApp>();if(app==null)throw new InvalidOperationException("Missing GameApp.");
            Sprite Next(Sprite s)=>s!=null&&sprites.TryGetValue(s.name,out var replacement)?replacement:s;
            app.contentIcons=app.contentIcons.Select(Next).ToArray();
            app.enchantIcons=app.enchantIcons.Select(Next).ToArray();
            app.art07Templates=app.art07Templates.Where(s=>s!=null).Select(Next).ToArray();
            // Surfaces introduced by v0.72 (no v0.71 counterpart) join the shared template list.
            var known=new HashSet<string>(app.art07Templates.Select(s=>s.name),StringComparer.OrdinalIgnoreCase);
            app.art07Templates=app.art07Templates.Concat(sprites.Values.Where(s=>!known.Contains(s.name)&&!s.name.StartsWith("illustration_",StringComparison.OrdinalIgnoreCase)).OrderBy(s=>s.name,StringComparer.Ordinal)).ToArray();

            // Materials: copy the previous set once, then point their textures at the v0.72 versions.
            Directory.CreateDirectory(Root+"/Materials");Directory.CreateDirectory(Root+"/Prefabs");AssetDatabase.Refresh();
            var materials=new Dictionary<string,Material>(StringComparer.OrdinalIgnoreCase);
            foreach(string raw in Directory.GetFiles(Previous,"*.mat",SearchOption.AllDirectories))
            {
                var original=AssetDatabase.LoadAssetAtPath<Material>(raw.Replace('\\','/'));
                if(original==null||materials.ContainsKey(original.name))continue;
                string target=Root+"/Materials/"+original.name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(target);
                if(material==null){material=new Material(original);AssetDatabase.CreateAsset(material,target);}
                foreach(string property in material.GetTexturePropertyNames())
                {var texture=material.GetTexture(property);if(texture!=null&&textures.TryGetValue(texture.name,out var replacement))material.SetTexture(property,replacement);}
                materials.Add(original.name,material);EditorUtility.SetDirty(material);
            }
            Material Swap(Material m)=>m!=null&&materials.TryGetValue(m.name,out var next)?next:m;
            app.table.bodyMaterial=Swap(app.table.bodyMaterial);app.table.glassMaterial=Swap(app.table.glassMaterial);app.table.outlineMaterial=Swap(app.table.outlineMaterial);

            var views=new List<Art07PrefabView>();int rebound=0;
            foreach(string raw in Directory.GetFiles(Previous+"/Full/Prefabs","*.prefab"))
            {
                string source=raw.Replace('\\','/'),target=Root+"/Prefabs/"+Path.GetFileName(source);
                if(AssetDatabase.LoadAssetAtPath<GameObject>(target)==null&&!AssetDatabase.CopyAsset(source,target))throw new IOException("Copy prefab failed: "+source);
                var contents=PrefabUtility.LoadPrefabContents(target);
                try
                {
                    foreach(var component in contents.GetComponentsInChildren<Component>(true))
                    {
                        if(component==null)throw new InvalidOperationException("Missing component: "+target);
                        var serialized=new SerializedObject(component);var property=serialized.GetIterator();
                        while(property.Next(true))
                        {
                            if(property.propertyType!=SerializedPropertyType.ObjectReference)continue;
                            Object value=property.objectReferenceValue,replacement=null;
                            if(value is Sprite sprite&&sprites.TryGetValue(sprite.name,out var newSprite))replacement=newSprite;
                            else if(value is Material material&&materials.TryGetValue(material.name,out var newMaterial))replacement=newMaterial;
                            if(replacement!=null&&replacement!=value){property.objectReferenceValue=replacement;rebound++;}
                        }
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                    }
                    PrefabUtility.SaveAsPrefabAsset(contents,target);
                }
                finally{PrefabUtility.UnloadPrefabContents(contents);}
                var view=AssetDatabase.LoadAssetAtPath<GameObject>(target).GetComponent<Art07PrefabView>();if(view!=null)views.Add(view);
            }
            if(views.Count!=app.art07Prefabs.Length)throw new InvalidOperationException($"Prefab view count changed: {app.art07Prefabs.Length} -> {views.Count}");
            app.art07Prefabs=app.art07Prefabs.Select(old=>views.First(v=>v.assetId==old.assetId)).ToArray();

            var felt=GameObject.Find("Art07Felt");if(felt==null)throw new InvalidOperationException("Missing felt table.");
            var feltMaterial=materials["felt"];
            feltMaterial.SetTexture("_BaseMap",textures["bg_felt_tile"]);feltMaterial.SetTexture("_TableMap",textures["bg_table_scene"]);
            feltMaterial.SetFloat("_UseTableMap",1);EditorUtility.SetDirty(feltMaterial);
            felt.GetComponent<Renderer>().sharedMaterial=feltMaterial;

            PlayerSettings.bundleVersion="0.7.2";
            EditorUtility.SetDirty(app);EditorUtility.SetDirty(app.table);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            const string lab="Assets/Game/Scenes/ArtStyleLab.unity";
            EditorSceneManager.SaveScene(scene,lab,true);scene=EditorSceneManager.OpenScene(lab);
            new GameObject("ArtStyleLab_IsolatedPreview",typeof(Art071StyleLab));EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity");
            Debug.Log($"ART072_INTEGRATED sprites={sprites.Count} icons={app.contentIcons.Length} templates={app.art07Templates.Length} prefabs={views.Count} rebound={rebound}");
        }
    }
}
