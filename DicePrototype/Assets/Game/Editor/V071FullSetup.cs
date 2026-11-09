using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using DiceCraft.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DiceCraft.Editor
{
    /// <summary>Import staged print masters and rebind every serialized consumer, preserving v07 history.</summary>
    public static class V071FullSetup
    {
        private const string Root=V071Setup.Root;
        [MenuItem("DiceCraft/Art071/Integrate complete art")]
        public static void Integrate()=>Integrate(true);
        public static void Integrate(bool requireComplete)
        {
            if(EditorApplication.isPlaying||EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Integration requires stopped play mode and a clean scene.");
            AssetDatabase.Refresh();
            foreach(string raw in Directory.GetFiles(Root,"*.png",SearchOption.AllDirectories))
            {
                string path=raw.Replace('\\','/');var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                bool background=path.Contains("/Backgrounds/"),surface=path.Contains("/Surfaces/")||path.StartsWith(Root+"/UI/",StringComparison.Ordinal);
                importer.textureType=background?TextureImporterType.Default:TextureImporterType.Sprite;
                importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=surface?200:100;
                importer.spriteBorder=surface?(Path.GetFileName(path).StartsWith("ui_progress_",StringComparison.Ordinal)?new Vector4(48,0,48,0):new Vector4(32,32,32,32)):Vector4.zero;
                var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
                // Combo's existing 5–7 physical pixel budget uses real sprite padding.
                settings.spriteMeshType=Path.GetFileName(path)=="fx_square_chip.png"||Path.GetFileName(path)=="fx_short_flame.png"?SpriteMeshType.Tight:SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
                importer.filterMode=FilterMode.Bilinear;importer.wrapMode=background?TextureWrapMode.Repeat:TextureWrapMode.Clamp;
                importer.mipmapEnabled=background;importer.alphaIsTransparency=!background;importer.sRGBTexture=true;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;importer.npotScale=TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }
            var sprites=Directory.GetFiles(Root,"*.png",SearchOption.AllDirectories).Select(p=>AssetDatabase.LoadAssetAtPath<Sprite>(p.Replace('\\','/'))).Where(s=>s!=null).ToDictionary(s=>s.name,StringComparer.OrdinalIgnoreCase);
            var textures=Directory.GetFiles(Root,"*.png",SearchOption.AllDirectories).Select(p=>AssetDatabase.LoadAssetAtPath<Texture2D>(p.Replace('\\','/'))).Where(s=>s!=null).ToDictionary(s=>s.name,StringComparer.OrdinalIgnoreCase);
            var cfg=JsonUtility.FromJson<GameConfig>(GameConfig.PrepareJsonForUnity(File.ReadAllText("Assets/Game/Data/P0Config.json")));cfg.Validate();
            var ids=cfg.controllers.Select(x=>x.id).Concat(cfg.consumables.Select(x=>x.id)).Concat(cfg.services.Select(x=>x.id)).Concat(cfg.diceUpgrades.Select(x=>x.id)).ToArray();
            var missing=ids.Where(id=>!sprites.ContainsKey("illustration_"+id)).ToArray();
            if(requireComplete&&missing.Length>0)throw new InvalidOperationException("Missing full illustrations: "+string.Join(",",missing));
            var scene=EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity");
            var app=Object.FindAnyObjectByType<GameApp>();if(app==null)throw new InvalidOperationException("Missing GameApp.");
            app.contentIcons=ids.Select(id=>sprites.TryGetValue("illustration_"+id,out var sprite)?sprite:app.contentIcons.First(s=>s.name.Equals("icon_"+id,StringComparison.OrdinalIgnoreCase))).ToArray();
            app.enchantIcons=cfg.enchantments.Where(e=>e.id!="none").Select(e=>sprites["badge_enchant_"+e.id]).ToArray();
            app.art07Templates=app.art07Templates.Where(s=>s!=null&&!sprites.ContainsKey(s.name)).Concat(sprites.Values.Where(s=>!s.name.StartsWith("illustration_",StringComparison.OrdinalIgnoreCase))).ToArray();

            Directory.CreateDirectory(Root+"/Full/Materials");Directory.CreateDirectory(Root+"/Full/Prefabs");AssetDatabase.Refresh();
            var materials=new Dictionary<string,Material>(StringComparer.OrdinalIgnoreCase)
            {
                ["mat_dice_ceramic"]=app.table.bodyMaterial,["mat_dice_glass"]=app.table.glassMaterial,["dice_contour"]=app.table.outlineMaterial
            };
            foreach(string raw in Directory.GetFiles(V07Setup.Root,"*.mat",SearchOption.AllDirectories))
            {
                var original=AssetDatabase.LoadAssetAtPath<Material>(raw.Replace('\\','/'));
                if(materials.ContainsKey(original.name))continue;
                string target=Root+"/Full/Materials/"+original.name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(target);
                if(material==null){material=new Material(original);AssetDatabase.CreateAsset(material,target);}
                foreach(string property in material.GetTexturePropertyNames())
                {var texture=material.GetTexture(property);if(texture!=null&&textures.TryGetValue(texture.name,out var replacement))material.SetTexture(property,replacement);}
                materials.Add(original.name,material);EditorUtility.SetDirty(material);
            }
            var views=new List<Art07PrefabView>();int replaced=0;
            foreach(string raw in Directory.GetFiles(V07Setup.Root,"*.prefab",SearchOption.AllDirectories))
            {
                string source=raw.Replace('\\','/'),target=Root+"/Full/Prefabs/"+Path.GetFileName(source);
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
                            if(value is Sprite sprite)
                            {
                                string key=sprite.name.StartsWith("icon_",StringComparison.OrdinalIgnoreCase)?"illustration_"+sprite.name.Substring(5):sprite.name;
                                if(sprites.TryGetValue(key,out var newSprite))replacement=newSprite;
                            }
                            else if(value is Material material&&materials.TryGetValue(material.name,out var newMaterial))replacement=newMaterial;
                            else if(value is Mesh&&AssetDatabase.GetAssetPath(value).Contains("dice_body_bevel"))replacement=app.table.bodyMesh;
                            if(replacement!=null&&replacement!=value){property.objectReferenceValue=replacement;replaced++;}
                        }
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        if(component is UnityEngine.UI.Image image&&image.sprite!=null&&image.sprite.name.StartsWith("ui_progress_",StringComparison.Ordinal))image.preserveAspect=false;
                    }
                    var authored=contents.GetComponent<Art07PrefabView>();
                    if(authored!=null&&authored.assetId=="fx_random_reveal")ConfigureOutcomeWheel(authored,app,sprites);
                    if(authored!=null&&authored.assetId=="fx_combo_burst")
                    {authored.scoreRing071=sprites["fx_ring_soft"];authored.scoreArc071=sprites["fx_arc_short"];}

                    PrefabUtility.SaveAsPrefabAsset(contents,target);
                }
                finally{PrefabUtility.UnloadPrefabContents(contents);}
                var view=AssetDatabase.LoadAssetAtPath<GameObject>(target).GetComponent<Art07PrefabView>();if(view!=null)views.Add(view);
            }
            app.art07Prefabs=views.ToArray();
            var felt=GameObject.Find("Art07Felt");if(felt==null)throw new InvalidOperationException("Missing felt table.");
            felt.GetComponent<Renderer>().sharedMaterial=materials["felt"];
            materials["felt"].SetTexture("_BaseMap",textures["bg_felt_tile"]);EditorUtility.SetDirty(materials["felt"]);
            EditorUtility.SetDirty(app);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            const string lab="Assets/Game/Scenes/ArtStyleLab.unity";
            EditorSceneManager.SaveScene(scene,lab,true);scene=EditorSceneManager.OpenScene(lab);
            new GameObject("ArtStyleLab_IsolatedPreview",typeof(Art071StyleLab));EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log($"ART071_FULL_INTEGRATED illustrations={ids.Length-missing.Length}/{ids.Length} prefabs={views.Count} rebound={replaced}");
        }
        private static void ConfigureOutcomeWheel(Art07PrefabView view,GameApp app,Dictionary<string,Sprite> sprites)
        {
            // The controller's illustration is distinct from the functional nine-outcome wheel.
            var root=view.icon.rectTransform;root.pivot=Vector2.one*.5f;root.anchoredPosition=new Vector2(96,-72);
            view.icon.sprite=sprites["fx_ring_soft"];view.icon.color=UiKit.Paper;
            view.icon.type=UnityEngine.UI.Image.Type.Simple;view.icon.preserveAspect=true;
            var pointer=view.visualRoot.Find("OutcomePointer") as RectTransform;
            if(pointer!=null){pointer.pivot=Vector2.one*.5f;pointer.anchoredPosition=new Vector2(96,-12);pointer.localRotation=Quaternion.Euler(0,0,-90);}
            for(int i=root.childCount-1;i>=0;i--)if(root.GetChild(i).name.StartsWith("Outcome",StringComparison.Ordinal))Object.DestroyImmediate(root.GetChild(i).gameObject);
            var previousFont=UiKit.Font;float previousScale=UiKit.FontScale;
            UiKit.Font=app.uiFont;UiKit.FontScale=1;
            try
            {
                var labels=new List<RectTransform>();string[] values={"1/2","1","2","3","4","5","6","7","8"};
                for(int i=0;i<9;i++)
                {
                    // Counterclockwise sector ordering cancels SetRevealSlice's -index*40 rotation.
                    float angle=(90+i*40)*Mathf.Deg2Rad;
                    Vector2 point=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*43;
                    var label=UiKit.Text(root,"OutcomeLabel"+i,values[i],0,0,28,24,i==0?11:14,UiKit.Paper,TMPro.TextAlignmentOptions.Center);
                    label.rectTransform.anchorMin=label.rectTransform.anchorMax=label.rectTransform.pivot=Vector2.one*.5f;
                    label.rectTransform.anchoredPosition=point;labels.Add(label.rectTransform);
                    float divider=angle+20*Mathf.Deg2Rad;
                    var spoke=UiKit.Box(root,"OutcomeSpoke"+i,0,0,35,1.5f,i%2==0?UiKit.Blue:UiKit.Red);
                    spoke.anchorMin=spoke.anchorMax=spoke.pivot=Vector2.one*.5f;
                    spoke.anchoredPosition=new Vector2(Mathf.Cos(divider),Mathf.Sin(divider))*34;
                    spoke.localRotation=Quaternion.Euler(0,0,divider*Mathf.Rad2Deg);
                }
                view.uprightWheelLabels=labels.ToArray();
            }
            finally{UiKit.Font=previousFont;UiKit.FontScale=previousScale;}
        }
    }
}
