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
    /// v0.73: imports Assets/Art/Art073 (stepped-corner surfaces, shop pieces, dice tags, high-score FX),
    /// rebinds serialized consumers by sprite name and binds the v0.73 dice materials.
    /// Art072 is not modified; backgrounds, word mark and card back keep their v0.72 files.
    /// </summary>
    public static class V073Setup
    {
        public const string Root="Assets/Art/Art073";
        private const string Previous="Assets/Art/Art072";
        private const int Border=40;
        // Mirrors BORDERS in tools/art_v073/build_art.py: (left, bottom, right, top) in pixels.
        private static readonly Dictionary<string,Vector4> Borders=new Dictionary<string,Vector4>
        {
            {"ui_section_header.png",new Vector4(40,0,72,0)},{"ui_tray_front.png",new Vector4(40,0,40,0)},
            {"fx_score_plate.png",new Vector4(104,104,104,104)},{"fx_formula_chip.png",new Vector4(48,0,48,0)}
        };

        public static void ConfigureImporter(string path,TextureImporter importer)
        {
            string file=Path.GetFileName(path);bool surface=path.Contains("/Surfaces/");
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=surface?200:100;
            importer.spriteBorder=!surface?Vector4.zero:Borders.TryGetValue(file,out var custom)?custom:new Vector4(Border,Border,Border,Border);
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
            importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;
            importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.sRGBTexture=true;
            importer.alphaSource=TextureImporterAlphaSource.FromInput;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;importer.npotScale=TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }

        private static Material Asset(string path,string shaderName)
        {
            var shader=Shader.Find(shaderName);if(shader==null)throw new InvalidOperationException("Missing shader "+shaderName);
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
            else material.shader=shader;
            EditorUtility.SetDirty(material);return material;
        }

        [MenuItem("DiceCraft/Art073/Integrate stepped surfaces and dice styles")]
        public static void Integrate()
        {
            if(EditorApplication.isPlaying||EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Integration requires stopped play mode and a clean scene.");
            AssetDatabase.Refresh();
            string[] pngs=Directory.GetFiles(Root,"*.png",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')).ToArray();
            foreach(string path in pngs)ConfigureImporter(path,(TextureImporter)AssetImporter.GetAtPath(path));
            var sprites=pngs.Select(AssetDatabase.LoadAssetAtPath<Sprite>).Where(s=>s!=null).ToDictionary(s=>s.name,StringComparer.OrdinalIgnoreCase);
            var textures=pngs.Select(AssetDatabase.LoadAssetAtPath<Texture2D>).Where(t=>t!=null).ToDictionary(t=>t.name,StringComparer.OrdinalIgnoreCase);
            if(sprites.Count!=pngs.Length)throw new InvalidOperationException($"Imported {sprites.Count} sprites from {pngs.Length} files.");

            var scene=EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity");
            var app=Object.FindAnyObjectByType<GameApp>();if(app==null)throw new InvalidOperationException("Missing GameApp.");
            Sprite Next(Sprite s)=>s!=null&&sprites.TryGetValue(s.name,out var replacement)?replacement:s;
            app.contentIcons=app.contentIcons.Select(Next).ToArray();
            app.enchantIcons=app.enchantIcons.Select(Next).ToArray();
            app.art07Templates=app.art07Templates.Where(s=>s!=null).Select(Next).ToArray();
            var known=new HashSet<string>(app.art07Templates.Select(s=>s.name),StringComparer.OrdinalIgnoreCase);
            app.art07Templates=app.art07Templates.Concat(sprites.Values.Where(s=>!known.Contains(s.name)).OrderBy(s=>s.name,StringComparer.Ordinal)).ToArray();

            // Materials: copy the previous set once, then point same-named textures at v0.73 files (none today).
            Directory.CreateDirectory(Root+"/Materials");Directory.CreateDirectory(Root+"/Prefabs");AssetDatabase.Refresh();
            var materials=new Dictionary<string,Material>(StringComparer.OrdinalIgnoreCase);
            foreach(string raw in Directory.GetFiles(Previous+"/Materials","*.mat"))
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

            // v0.73 dice styles: clear blue glass, the per-face finish layer, and the interface dice image.
            var glass=app.table.glassMaterial;
            glass.SetColor("_BaseColor",new Color(.31f,.66f,.90f,1));glass.SetFloat("_GlassOpacity",.46f);glass.SetFloat("_Glass073",1);glass.SetFloat("_Glass",1);
            EditorUtility.SetDirty(glass);
            app.table.styleMaterial=Asset(Root+"/Materials/dice_face_style073.mat","DiceCraft/Art073/FaceStyle");
            app.uiDiceImageMaterial073=Asset(Root+"/Materials/ui_dice_premultiplied073.mat","DiceCraft/Art073/UiPremultiplied");

            var views=new List<Art07PrefabView>();int rebound=0;
            foreach(string raw in Directory.GetFiles(Previous+"/Prefabs","*.prefab"))
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
            felt.GetComponent<Renderer>().sharedMaterial=materials["felt"];

            PlayerSettings.bundleVersion="0.7.3";
            EditorUtility.SetDirty(app);EditorUtility.SetDirty(app.table);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            const string lab="Assets/Game/Scenes/ArtStyleLab.unity";
            EditorSceneManager.SaveScene(scene,lab,true);scene=EditorSceneManager.OpenScene(lab);
            new GameObject("ArtStyleLab_IsolatedPreview",typeof(Art071StyleLab));EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity");
            Debug.Log($"ART073_INTEGRATED sprites={sprites.Count} icons={app.contentIcons.Length} templates={app.art07Templates.Length} prefabs={views.Count} rebound={rebound}");
        }
    }
}
