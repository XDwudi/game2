// [UNITY-SKILL:SPRITEATLAS]
// V2 authoring + built-in delivery. Root alone invokes this builder in Unity.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using DiceCraft.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

namespace DiceCraft.Editor
{
    /// <summary>Creates actual reusable view assets from the approved art source. Does not alter scenes or rules.</summary>
    public static class Art07AssetLibrary
    {
        public const string Art = "Assets/Art/Art07";
        private const string PlanPath = "../docs/specs/v07/dice_art_v0_7_manifest.json";
        private static TMP_FontAsset cjk, numbers;
        private static Scene buildScene;
        private static readonly List<string> written = new List<string>();
        [Serializable] private sealed class Plan { public Shared sharedResources; public Content[] contentResources; }
        [Serializable] private sealed class Shared { public Asset[] assets; }
        [Serializable] private sealed class Asset { public string assetId, assetType, category, runtimePath, sourcePath, importProfile; public int durationMs; public int[] dimensionsPx; }
        [Serializable] private sealed class Content { public string id, icon_id, iconExport, iconSource; }
        [Serializable] private sealed class MappingFile { public Mapping[] records; }
        [Serializable] private sealed class Mapping { public string assetId, sourcePath, runtimePath; }
        [Serializable] private sealed class MapOutput { public string artVersion = "art0.7.0"; public string gameplayConfigVersion = "p0.6.0"; public MapEntry[] assets; }
        [Serializable] private sealed class MapEntry { public string assetId, runtimePath, sourcePath, guid; public bool exists; }
        [Serializable] private sealed class BuildProof { public string artVersion = "art0.7.0", unityVersion, spritePackerMode, generatorSha256, viewSha256, shaderSha256, planSha256; public bool runtimeVerified; public string[] generatedAssets; public int prefabs, materials, clips, atlases; }

        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before building assets.");
            if (!File.Exists(PlanPath)) throw new FileNotFoundException("The immutable art specification must accompany this checkout.", PlanPath);
            var plan = JsonUtility.FromJson<Plan>(File.ReadAllText(PlanPath));
            if (plan?.sharedResources?.assets == null || plan.contentResources?.Length != 71) throw new InvalidDataException("Expected full art0.7.0 source plan.");
            written.Clear(); AssetDatabase.Refresh();
            ImportTextures(plan); BuildFonts(); BuildMaterials(); BuildClips(plan);
            var oldFont = UiKit.Font; var oldTemplates = UiKit.Templates; float oldScale = UiKit.FontScale;
            try
            {
                UiKit.Font = cjk; UiKit.FontScale = 1;
                UiKit.Templates = Directory.GetFiles(Art + "/UI/Templates", "*.png").Select(p => Load<Sprite>(p.Replace('\\', '/'))).ToArray();
                foreach (var a in plan.sharedResources.assets.Where(a => a.assetType == "prefab")) BuildPrefab(a);
            }
            finally { UiKit.Font = oldFont; UiKit.Templates = oldTemplates; UiKit.FontScale = oldScale; }
            BuildAtlases(); BuildConfig(plan); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            var proof = new BuildProof { unityVersion = Application.unityVersion, spritePackerMode = EditorSettings.spritePackerMode.ToString(), generatedAssets = written.Distinct().OrderBy(p => p, StringComparer.Ordinal).ToArray(), prefabs = written.Count(p => p.EndsWith(".prefab")), materials = written.Count(p => p.EndsWith(".mat")), clips = written.Count(p => p.EndsWith(".anim")), atlases = written.Count(p => p.EndsWith(".spriteatlasv2")), runtimeVerified = false };
            proof.generatorSha256 = Hash("Assets/Game/Editor/Art07AssetLibrary.cs"); proof.viewSha256 = Hash("Assets/Game/Runtime/Presentation/Art07PrefabView.cs"); proof.shaderSha256 = Hash("Assets/Game/Runtime/Shaders/Art07LocalFx.shader"); proof.planSha256 = Hash(PlanPath);
            if (proof.prefabs != 26 || proof.materials != 7 || proof.clips != 10 || proof.atlases != 3) throw new InvalidDataException("Library output cardinality mismatch.");
            Directory.CreateDirectory("../ArtSource/Art07/Review");
            File.WriteAllText("../ArtSource/Art07/Review/library-build-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".json", JsonUtility.ToJson(proof, true));
            Debug.Log("ART07_LIBRARY_BUILT " + JsonUtility.ToJson(proof));
        }
        private static T Load<T>(string path) where T : Object
        { var a = AssetDatabase.LoadAssetAtPath<T>(path); if (a == null) throw new InvalidOperationException("Missing " + typeof(T).Name + ": " + path); return a; }
        private static string Hash(string path) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }
        private static void Folder(string path) { string dir = Path.GetDirectoryName(path); if (!Directory.Exists(dir)) { Directory.CreateDirectory(dir); AssetDatabase.Refresh(); } }
        private static Sprite Sprite(string path) => Load<Sprite>(Art + "/" + path + ".png");
        private static void ImportTextures(Plan plan)
        {
            foreach (var a in plan.sharedResources.assets.Where(a => a.runtimePath.EndsWith(".png")))
            {
                var importer = AssetImporter.GetAtPath(a.runtimePath) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Missing TextureImporter " + a.runtimePath);
                bool data = a.assetId.StartsWith("dice_") && a.assetId != "dice_pips_red_blue";
                bool soft = a.assetId.Contains("soft") || a.assetId == "bg_vignette" || a.assetId == "dice_contact_shadow";
                bool sprite = !a.assetId.StartsWith("bg_") && !a.assetId.StartsWith("dice_");
                importer.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
                importer.spriteImportMode = SpriteImportMode.Single; importer.spritePixelsPerUnit = 100;
                importer.sRGBTexture = !data; importer.mipmapEnabled = false; importer.isReadable = false;
                importer.filterMode = soft || a.assetId == "bg_felt_tile" ? FilterMode.Bilinear : FilterMode.Point;
                importer.wrapMode = a.assetId == "bg_felt_tile" || a.assetId == "bg_print_grain" || a.assetId == "dice_smooth_mask" || a.assetId == "dice_rough_mask" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.alphaIsTransparency = !data; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 2048;
                if (a.category == "ui_template")
                {
                    int b = a.assetId.Contains("panel") || a.assetId.Contains("tooltip") ? 24 : 16;
                    importer.spriteBorder = a.assetId.Contains("progress") ? new Vector4(12, 0, 12, 0) : new Vector4(b, b, b, b);
                }
                importer.SaveAndReimport();
            }
            foreach (var c in plan.contentResources)
            {
                var i = AssetImporter.GetAtPath(c.iconExport) as TextureImporter;
                if (i == null) throw new InvalidOperationException("Missing content icon " + c.id);
                i.textureType = TextureImporterType.Sprite; i.spriteImportMode = SpriteImportMode.Single; i.spritePixelsPerUnit = 100;
                var settings = new TextureImporterSettings(); i.ReadTextureSettings(settings); settings.spriteMeshType = SpriteMeshType.FullRect; i.SetTextureSettings(settings);
                i.filterMode = FilterMode.Point; i.wrapMode = TextureWrapMode.Clamp; i.mipmapEnabled = false; i.isReadable = false; i.alphaIsTransparency = true; i.sRGBTexture = true; i.textureCompression = TextureImporterCompression.Uncompressed; i.SaveAndReimport();
            }
        }
        private static void BuildFonts()
        {
            cjk = Load<TMP_FontAsset>("Assets/Game/Fonts/DiceChinese.asset");
            string path = Art + "/Fonts/font_numbers_tmp.asset"; Folder(path);
            numbers = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (numbers == null)
            {
                numbers = TMP_FontAsset.CreateFontAsset(Load<Font>("Assets/Game/Fonts/NotoSansCJKsc-Regular.otf"), 72, 8, GlyphRenderMode.SDFAA, 512, 512, AtlasPopulationMode.Dynamic, false);
                numbers.name = "Art07 Numbers"; AssetDatabase.CreateAsset(numbers, path);
                if (!numbers.TryAddCharacters("0123456789.,+-×÷/%(): ≤≥—", out string missing)) throw new InvalidOperationException("Numeric glyphs missing: " + missing);
                foreach (var texture in numbers.atlasTextures) if (!AssetDatabase.Contains(texture)) AssetDatabase.AddObjectToAsset(texture, numbers);
                if (numbers.material != null && !AssetDatabase.Contains(numbers.material)) AssetDatabase.AddObjectToAsset(numbers.material, numbers);
                numbers.atlasPopulationMode = AtlasPopulationMode.Static; numbers.fallbackFontAssetTable = new List<TMP_FontAsset> { cjk }; EditorUtility.SetDirty(numbers);
            }
            written.Add(path);
        }
        private static Material Material(string id, string shaderName, Color color, string texture = null)
        {
            string folder = id == "mat_ui_decor" || id == "mat_fx_soft" ? "/FX/Materials/" : "/Dice/Materials/";
            string path = Art + folder + id + ".mat"; Folder(path);
            var shader = Shader.Find(shaderName); if (shader == null) throw new InvalidOperationException("Shader not compiled: " + shaderName);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); } else m.shader = shader;
            if (m.HasProperty("_Color")) m.SetColor("_Color", color); if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (texture != null) m.mainTexture = Load<Texture2D>(Art + texture);
            EditorUtility.SetDirty(m); written.Add(path); return m;
        }
        private static void BuildMaterials()
        {
            var ceramic = Material("mat_dice_ceramic", "DiceCraft/Art07/Ceramic", UiKit.Paper); ceramic.SetColor("_EdgeColor", UiKit.Ink); ceramic.SetFloat("_Glass", 0);
            var glass = Material("mat_dice_glass", "DiceCraft/Art07/Ceramic", UiKit.Hex("B1DBD7")); glass.SetColor("_EdgeColor", UiKit.Ink); glass.SetFloat("_Glass", 1);
            Material("mat_dice_pips", "DiceCraft/VertexFace", UiKit.Paper);
            Material("mat_dice_gold_edge", "DiceCraft/Art07/LocalFx", UiKit.Gold, "/Dice/Textures/dice_gold_edge_mask.png");
            Material("mat_dice_selection", "DiceCraft/Art07/LocalFx", UiKit.Paper, "/UI/Templates/ui_focus_frame.png");
            Material("mat_ui_decor", "DiceCraft/Art07/LocalFx", Color.white);
            Material("mat_fx_soft", "DiceCraft/Art07/LocalFx", Color.white);
        }
        private static void PersistClip(AnimationClip result, string path)
        {
            Folder(path); var old = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (old == null) AssetDatabase.CreateAsset(result, path);
            else { EditorUtility.CopySerialized(result, old); Object.DestroyImmediate(result); EditorUtility.SetDirty(old); }
            written.Add(path);
        }
        private static void BuildClips(Plan plan)
        {
            foreach (var a in plan.sharedResources.assets.Where(a => a.assetType == "animationclip"))
            {
                float targetSeconds = a.durationMs / 1000f;
                var result = new AnimationClip { name = a.assetId, frameRate = 30, wrapMode = WrapMode.Once };
                if (a.assetId.StartsWith("dice_"))
                {
                    string fbx = Art + "/Dice/Animation/" + a.assetId + ".fbx";
                    var source = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                    if (source == null || source.length <= 0) throw new InvalidOperationException("Actual animation data missing: " + fbx);
                    var bindings = AnimationUtility.GetCurveBindings(source); if (bindings.Length == 0) throw new InvalidOperationException("FBX has zero animation curves: " + a.assetId);
                    float ratio = targetSeconds / source.length;
                    foreach (var binding in bindings)
                    {
                        var curve = AnimationUtility.GetEditorCurve(source, binding); var keys = curve.keys;
                        for (int k = 0; k < keys.Length; k++) { keys[k].time *= ratio; keys[k].inTangent /= ratio; keys[k].outTangent /= ratio; }
                        curve.keys = keys; AnimationUtility.SetEditorCurve(result, binding, curve);
                    }
                }
                else
                {
                    if (a.assetId == "card_trigger_punch") SetScale(result, "Visual", new[] { 0f, .06f, .14f, .22f }, new[] { 1f, .98f, 1.03f, 1f });
                    else if (a.assetId == "card_acquire") SetScale(result, "Visual", new[] { 0f, targetSeconds * .7f, targetSeconds }, new[] { .85f, 1.03f, 1f });
                    else if (a.assetId == "card_remove") { SetScale(result, "Visual", new[] { 0f, targetSeconds }, new[] { 1f, .85f }); SetCurve(result, "", typeof(CanvasGroup), "m_Alpha", new[] { 0f, targetSeconds }, new[] { 1f, 0f }); }
                    else if (a.assetId == "panel_enter") { SetCurve(result, "Visual", typeof(Transform), "m_LocalPosition.y", new[] { 0f, targetSeconds }, new[] { -16f, 0f }); SetCurve(result, "", typeof(CanvasGroup), "m_Alpha", new[] { 0f, targetSeconds }, new[] { 0f, 1f }); }
                    else if (a.assetId == "panel_exit") SetCurve(result, "", typeof(CanvasGroup), "m_Alpha", new[] { 0f, targetSeconds }, new[] { 1f, 0f });
                    else throw new InvalidDataException("Missing explicit UI curve design " + a.assetId);
                }
                if (AnimationUtility.GetCurveBindings(result).Length == 0) throw new InvalidOperationException("Empty clip rejected: " + a.assetId);
                PersistClip(result, a.runtimePath);
            }
            Directory.CreateDirectory("../ArtSource/Art07/Animation");
            File.WriteAllText("../ArtSource/Art07/Animation/ui_curves.json", "{\"artVersion\":\"art0.7.0\",\"implementation\":\"Assets/Game/Editor/Art07AssetLibrary.cs:BuildClips\",\"transformPath\":\"Visual\",\"card_trigger_punch\":{\"seconds\":[0,0.06,0.14,0.22],\"scale\":[1,0.98,1.03,1]},\"card_acquireMs\":320,\"card_removeMs\":320,\"panel_enterMs\":160,\"panel_exitMs\":120,\"rulesSideEffects\":false}");
        }
        private static void SetScale(AnimationClip clip, string path, float[] times, float[] values)
        { foreach (string axis in new[] { "x", "y", "z" }) SetCurve(clip, path, typeof(Transform), "m_LocalScale." + axis, times, values); }
        private static void SetCurve(AnimationClip clip, string path, Type type, string property, float[] times, float[] values)
        {
            var curve = new AnimationCurve(times.Select((time, i) => new Keyframe(time, values[i])).ToArray());
            for (int i = 0; i < curve.length; i++) { AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto); AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto); }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, property), curve);
        }
        private static Art07PrefabView UIObject(string name, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup), typeof(Art07PrefabView));
            SceneManager.MoveGameObjectToScene(go, buildScene);
            var r = go.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1); r.sizeDelta = new Vector2(w, h);
            var view = go.GetComponent<Art07PrefabView>(); view.assetId = name; view.group = go.GetComponent<CanvasGroup>(); view.visualRoot = UiKit.Rect(r, "Visual", 0, 0, w, h); return view;
        }
        private static UnityEngine.UI.Image Picture(Transform p, string name, Sprite sprite, float x, float y, float w, float h, Color? tint = null)
        {
            var r = UiKit.Box(p, name, x, y, w, h, tint ?? Color.white); var image = r.GetComponent<UnityEngine.UI.Image>(); image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false; return image;
        }
        private static void Surface(Transform p, string template, float w, float h)
        { var image = Picture(p, "Surface", Sprite("UI/Templates/" + template), 0, 0, w, h); image.type = UnityEngine.UI.Image.Type.Sliced; image.preserveAspect = false; }
        private static TMP_Text Label(Transform p, string key, string text, float x, float y, float w, float h, float size, Color color)
        { var t = UiKit.Text(p, key, text, x, y, w, h, size, color); t.overflowMode = TextOverflowModes.Ellipsis; return t; }
        private static void BuildPrefab(Asset asset)
        {
            var preview = EditorSceneManager.NewPreviewScene(); buildScene = preview; GameObject root = null;
            try
            {
                Art07PrefabView view;
                if (asset.assetId == "dice_view") view = DiceView();
                else if (asset.assetId == "fx_dice_break") view = DiceBreak();
                else if (asset.category == "fx_prefab") view = EffectView(asset.assetId);
                else view = InterfaceView(asset.assetId);
                root = view.gameObject; SceneManager.MoveGameObjectToScene(root, preview);
                if (root.GetComponentsInChildren<Renderer>(true).Length == 0 && root.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).Length == 0) throw new InvalidOperationException("Visual-less prefab rejected " + asset.assetId);
                if (asset.assetId == "dice_view" && view.physicalFaces.Length != 6) throw new InvalidOperationException("Dice prefab must expose six actual mesh targets.");
                Folder(asset.runtimePath); PrefabUtility.SaveAsPrefabAsset(root, asset.runtimePath, out bool success); if (!success) throw new InvalidOperationException("Cannot save prefab " + asset.assetId); written.Add(asset.runtimePath);
            }
            finally { if (root != null) Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(preview); buildScene = default; }
        }
        private static Art07PrefabView InterfaceView(string id)
        {
            if (id == "ui_button")
            {
                var v = UIObject(id, 280, 72); v.action = UiKit.ActionButton(v.visualRoot, "Action", "动作", 0, 0, 280, 72, null, UiKit.Blue); v.title = v.action.GetComponentInChildren<TMP_Text>(); v.deactivateWhenFinished = false; return v;
            }
            if (id == "ui_controller_card" || id == "ui_item_card" || id == "ui_upgrade_card")
            {
                var v = UIObject(id, 160, 224); Surface(v.visualRoot, id == "ui_controller_card" ? "ui_card_controller" : id == "ui_item_card" ? "ui_card_item" : "ui_card_upgrade", 160, 224);
                v.title = Label(v.visualRoot, "Title", "名称", 12, 8, 136, 32, 22, UiKit.Ink);
                string sample = id == "ui_controller_card" ? "Controllers/icon_c02" : id == "ui_item_card" ? "Items/icon_i06" : "Upgrades/icon_u03";
                v.icon = Picture(v.visualRoot, "Illustration", Sprite("ContentIcons/" + sample), 20, 48, 120, 120);
                v.value = Label(v.visualRoot, "CurrentValue", "—", 12, 178, 136, 34, 22, UiKit.Ink);
                v.action = v.visualRoot.gameObject.AddComponent<UnityEngine.UI.Button>(); v.action.targetGraphic = v.visualRoot.Find("Surface").GetComponent<UnityEngine.UI.Image>(); v.action.targetGraphic.raycastTarget = true;
                if (id == "ui_controller_card")
                {
                    v.title.rectTransform.sizeDelta = new Vector2(104, 32);
                    v.badge = Picture(v.visualRoot, "EnchantBadge", Sprite("UI/Badges/badge_enchant_slotless"), 120, 8, 32, 32); v.badge.enabled = false;
                    v.fullOnly = Enumerable.Range(0, v.visualRoot.childCount).Select(i => v.visualRoot.GetChild(i).gameObject).ToArray();
                    v.compactRoot = UiKit.Rect(v.visualRoot, "Compact", 0, 0, 208, 104); Surface(v.compactRoot, "ui_card_controller_compact", 208, 104);
                    v.compactIcon = Picture(v.compactRoot, "Illustration", v.icon.sprite, 8, 16, 72, 72);
                    v.compactTitle = Label(v.compactRoot, "Title", "名称", 88, 8, 84, 48, 20, UiKit.Ink);
                    v.compactValue = Label(v.compactRoot, "CurrentValue", "—", 88, 64, 108, 30, 22, UiKit.Ink);
                    v.compactBadge = Picture(v.compactRoot, "EnchantBadge", v.badge.sprite, 176, 4, 28, 28); v.compactBadge.enabled = false;
                    v.compactRoot.Find("Surface").GetComponent<UnityEngine.UI.Image>().raycastTarget = true; v.compactRoot.gameObject.SetActive(false);
                }
                v.deactivateWhenFinished = false; return v;
            }
            if (id == "ui_dice_slot")
            {
                var v = UIObject(id, 152, 124); Surface(v.visualRoot, "ui_slot_dice", 152, 124);
                v.title = Label(v.visualRoot, "Ordinal", "—", 10, 7, 40, 24, 20, UiKit.Muted); v.value = Label(v.visualRoot, "EffectiveValue", "", 8, 88, 136, 30, 20, UiKit.TextColor);
                v.icon = Picture(v.visualRoot, "Lock", Sprite("UI/Symbols/ui_icon_lock"), 116, 6, 28, 28); v.icon.enabled = false; v.deactivateWhenFinished = false; return v;
            }
            if (id == "ui_stat_counter")
            {
                var v = UIObject(id, 272, 120); UiKit.Frame(v.visualRoot, "Panel", 0, 0, 272, 120, UiKit.Panel);
                v.title = Label(v.visualRoot, "Label", "名称", 16, 12, 240, 30, 22, UiKit.Muted); v.value = Label(v.visualRoot, "ExactValue", "—", 16, 48, 240, 56, 44, UiKit.Gold); v.value.font = numbers; v.deactivateWhenFinished = false; return v;
            }
            if (id == "ui_score_breakdown")
            {
                var v = UIObject(id, 1528, 160); UiKit.Frame(v.visualRoot, "Panel", 0, 0, 1528, 160, UiKit.Background);
                v.title = Label(v.visualRoot, "PrimaryHand", "计分明细", 16, 12, 900, 36, 26, UiKit.Paper); v.value = Label(v.visualRoot, "ExactScore", "—", 1040, 8, 472, 52, 40, UiKit.Gold); v.value.font = numbers;
                v.fieldKeys = new[] { "base", "hand", "color", "redGroup", "controllers", "enchantments" }; string[] labels = { "基础", "牌型", "颜色", "红组", "控制器", "附魔" }; v.fieldValues = new TMP_Text[6];
                for (int i = 0; i < 6; i++) { var chip = UiKit.Frame(v.visualRoot, "Term" + i, 16 + i * 250, 72, 236, 64, UiKit.Panel); Label(chip, "Label", labels[i], 12, 5, 94, 24, 20, UiKit.Muted); v.fieldValues[i] = Label(chip, "Exact", "—", 104, 5, 120, 48, 24, UiKit.Paper); }
                v.deactivateWhenFinished = false; return v;
            }
            if (id == "ui_probability_bar")
            {
                var v = UIObject(id, 420, 68); v.title = Label(v.visualRoot, "Label", "概率", 0, 0, 240, 28, 22, UiKit.Paper); v.value = Label(v.visualRoot, "ExactProbability", "—", 250, 0, 170, 28, 22, UiKit.Paper);
                Picture(v.visualRoot, "Track", Sprite("UI/Templates/ui_progress_track"), 0, 36, 420, 24, UiKit.Panel); v.fill = Picture(v.visualRoot, "Fill", Sprite("UI/Templates/ui_progress_fill"), 0, 36, 420, 24, UiKit.Blue); v.fill.type = UnityEngine.UI.Image.Type.Filled; v.fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal; v.fill.fillAmount = 0; v.deactivateWhenFinished = false; return v;
            }
            if (id == "ui_tooltip")
            {
                var v = UIObject(id, 420, 240); Surface(v.visualRoot, "ui_tooltip_frame", 420, 240); v.title = Label(v.visualRoot, "Heading", "详情", 16, 16, 388, 40, 26, UiKit.Ink); v.detail = Label(v.visualRoot, "Description", "", 16, 64, 388, 160, 22, UiKit.Ink); v.detail.overflowMode = TextOverflowModes.Overflow; v.deactivateWhenFinished = false; return v;
            }
            if (id == "ui_modal")
            {
                var v = UIObject(id, 1920, 1080); UiKit.Box(v.visualRoot, "InputShield", 0, 0, 1920, 1080, new Color(0, 0, 0, .35f), true); var panel = UiKit.Rect(v.visualRoot, "Dialog", 480, 180, 960, 720); Surface(panel, "ui_panel_modal", 960, 720);
                v.title = Label(panel, "Heading", "详情", 32, 24, 808, 56, 32, UiKit.Ink); v.detail = Label(panel, "Description", "", 32, 104, 896, 472, 24, UiKit.Ink);
                v.action = UiKit.Button(panel, "Close", "关闭", 688, 616, 240, 64, null, true, true); v.group.blocksRaycasts = true; v.deactivateWhenFinished = false; return v;
            }
            if (id == "ui_missing_asset")
            {
                var v = UIObject(id, 208, 104); UiKit.Frame(v.visualRoot, "Fallback", 0, 0, 208, 104, UiKit.Panel, UiKit.Gold); Picture(v.visualRoot, "Warning", Sprite("UI/Symbols/ui_icon_warning"), 12, 22, 48, 48);
                v.title = Label(v.visualRoot, "MissingId", "资源缺失", 72, 16, 124, 30, 22, UiKit.Paper); v.detail = Label(v.visualRoot, "FallbackText", "请查看文字明细", 72, 50, 124, 42, 18, UiKit.Muted); v.deactivateWhenFinished = false; return v;
            }
            throw new InvalidDataException("Unimplemented UI prefab " + id);
        }
        private static Art07PrefabView EffectView(string id)
        {
            bool ticket = id == "fx_value_ticket" || id == "fx_cash_ticket"; var v = UIObject(id, ticket ? 240 : 192, ticket ? 64 : 192); v.isEffect = true; v.group.blocksRaycasts = false; v.group.interactable = false;
            var p = v.visualRoot; var mat = Load<Material>(Art + "/FX/Materials/mat_ui_decor.mat");
            if (id == "fx_source_hit" || id == "fx_target_bracket" || id == "fx_slot_expand")
            {
                v.icon = Picture(p, "Edge", Sprite("UI/Templates/ui_focus_frame"), 4, 4, 184, 184, id == "fx_slot_expand" ? UiKit.Blue : UiKit.Paper); v.icon.type = UnityEngine.UI.Image.Type.Sliced;
                v.motion = id == "fx_source_hit" ? Art07PrefabView.Motion.Punch : id == "fx_slot_expand" ? Art07PrefabView.Motion.Expand : Art07PrefabView.Motion.Bracket;
                if (id == "fx_slot_expand") Picture(p, "Plus", Sprite("UI/Symbols/ui_icon_plus"), 72, 72, 48, 48, UiKit.Blue);
            }
            else if (ticket)
            {
                Surface(p, "ui_chip_label", 240, 64); v.value = Label(p, "ExactOperand", "", 52, 10, 176, 44, 30, UiKit.Ink); v.icon = Picture(p, "Kind", Sprite(id == "fx_cash_ticket" ? "UI/Symbols/ui_icon_coin" : "UI/Symbols/ui_icon_plus"), 12, 14, 36, 36, id == "fx_cash_ticket" ? UiKit.Gold : UiKit.Teal); v.motion = id == "fx_cash_ticket" ? Art07PrefabView.Motion.Cash : Art07PrefabView.Motion.Ticket;
            }
            else if (id == "fx_face_change")
            {
                Picture(p, "Boundary", Sprite("UI/Templates/ui_focus_frame"), 8, 8, 176, 176, UiKit.Red); v.fill = Picture(p, "PermanentStroke", Sprite("UI/Templates/ui_focus_frame"), 8, 8, 176, 176, UiKit.Blue); v.fill.type = UnityEngine.UI.Image.Type.Filled; v.fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal; v.detail = Label(p, "ChangeKind", "", 24, 150, 144, 32, 22, UiKit.Paper); v.motion = Art07PrefabView.Motion.Wipe;
            }
            else if (id == "fx_copy_link")
            {
                v.lineSegments = new RectTransform[3]; for (int i = 0; i < 3; i++) v.lineSegments[i] = UiKit.Box(p, "Link" + i, 12 + i * 48, 96, 48, 3, UiKit.Violet);
                v.icon = Picture(p, "Arrow", Sprite("UI/Symbols/ui_icon_arrow_right"), 148, 80, 32, 32, UiKit.Violet); v.motion = Art07PrefabView.Motion.Link;
            }
            else if (id == "fx_probability_change")
            {
                UiKit.Frame(p, "Track", 16, 72, 160, 24, UiKit.Panel); v.fill = Picture(p, "ActualDelta", Sprite("UI/Templates/ui_progress_fill"), 16, 72, 160, 24, UiKit.Blue); v.fill.type = UnityEngine.UI.Image.Type.Filled; v.fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal; v.value = Label(p, "ExactDelta", "", 16, 108, 160, 40, 26, UiKit.Paper); v.motion = Art07PrefabView.Motion.Probability;
            }
            else if (id == "fx_random_reveal")
            {
                v.icon = Picture(p, "FixedOutcomeWheel", Sprite("ContentIcons/Controllers/icon_c53"), 32, 8, 128, 128); v.value = Label(p, "ReceiptOutcome", "", 16, 144, 160, 40, 30, UiKit.Paper); v.motion = Art07PrefabView.Motion.Reveal; v.defaultDuration = .36f;
                var pointer = Picture(p, "OutcomePointer", Sprite("UI/Symbols/ui_icon_arrow_right"), 84, 0, 24, 24, UiKit.Gold); pointer.rectTransform.localRotation = Quaternion.Euler(0, 0, -90);
            }
            else if (id == "fx_card_destroy")
            {
                Surface(p, "ui_card_controller", 160, 192); Picture(p, "CommittedCrack", Sprite("FX/Textures/fx_crack_card"), 28, 12, 104, 156, UiKit.Danger); var parts = new List<Transform>();
                for (int i = 0; i < 5; i++) parts.Add(Picture(p, "PaperFragment" + i, Sprite("FX/Textures/fx_square_chip"), 24 + (i % 3) * 42, 50 + (i / 3) * 54, 36, 44, UiKit.Paper).transform);
                v.particles = parts.ToArray(); v.motion = Art07PrefabView.Motion.Shatter; v.defaultDuration = .46f;
            }
            else if (id == "fx_combo_burst")
            {
                v.burstLayers = new[] {
                    UiKit.Box(p, "Underline", 40, 148, 112, 3, UiKit.Gold).gameObject,
                    Picture(p, "GoldRing", Sprite("FX/Textures/fx_ring_soft"), 12, 12, 168, 168, UiKit.Gold).gameObject,
                    Picture(p, "Sweep", Sprite("FX/Textures/fx_line_soft"), 8, 78, 176, 32, UiKit.Gold).gameObject,
                    Picture(p, "OuterRing", Sprite("FX/Textures/fx_ring_soft"), 0, 0, 192, 192, UiKit.Gold).gameObject,
                    Picture(p, "Halo", Sprite("FX/Textures/fx_halo_soft"), 8, 8, 176, 176, new Color(1, .72f, .3f, .35f)).gameObject,
                    Picture(p, "ShortFlame", Sprite("FX/Textures/fx_short_flame"), 40, 128, 112, 56, UiKit.Gold).gameObject };
                var chips = new List<Transform>(); for (int i = 0; i < 36; i++) { float a = i * 2.399963f; chips.Add(Picture(p, "Chip" + i, Sprite("FX/Textures/fx_square_chip"), 88 + Mathf.Cos(a) * 40, 88 + Mathf.Sin(a) * 40, 12, 12, i % 2 == 0 ? UiKit.Red : UiKit.Blue).transform); }
                v.burstStaticStars = Enumerable.Range(0, 5).Select(i => Picture(p, "StaticStar" + i, Sprite("FX/Textures/fx_star_spark"), 36 + 24 * i, 164, 20, 20, UiKit.Gold).gameObject).ToArray();
                v.particles = chips.ToArray(); v.motion = Art07PrefabView.Motion.Burst; v.SetBurstLevel(0);
            }
            else if (id == "fx_stage_stamp")
            {
                UiKit.Outline(p, "StampEdge", 8, 48, 176, 88, UiKit.Gold, 4); v.value = Label(p, "ReceiptStatus", "达标", 20, 64, 152, 56, 40, UiKit.Gold); v.motion = Art07PrefabView.Motion.Stamp; v.defaultDuration = .32f;
            }
            else if (id == "fx_acquire_remove")
            {
                Surface(p, "ui_card_controller", 144, 192); v.icon = Picture(p, "ActualItem", Sprite("ContentIcons/Controllers/icon_c02"), 24, 32, 96, 96); v.detail = Label(p, "ReceiptDirection", "", 12, 140, 120, 40, 22, UiKit.Ink); v.motion = Art07PrefabView.Motion.Acquire; v.defaultDuration = .32f;
            }
            else throw new InvalidDataException("Unimplemented FX prefab " + id);
            var softMat = Load<Material>(Art + "/FX/Materials/mat_fx_soft.mat");
            foreach (var image in v.GetComponentsInChildren<UnityEngine.UI.Image>(true)) { image.raycastTarget = false; image.material = image.sprite != null && image.sprite.name.Contains("_soft") ? softMat : mat; }
            return v;
        }
        private static Art07PrefabView DiceView()
        {
            var go = new GameObject("dice_view", typeof(Art07PrefabView)); var v = go.GetComponent<Art07PrefabView>(); v.assetId = "dice_view"; v.deactivateWhenFinished = false;
            SceneManager.MoveGameObjectToScene(go, buildScene);
            var pivot = new GameObject("RollPivot").transform; pivot.SetParent(go.transform, false); var orientation = new GameObject("FaceOrientation").transform; orientation.SetParent(pivot, false); v.faceOrientation = orientation;
            var source = Load<GameObject>(Art + "/Dice/Models/dice_body_bevel.fbx"); var bodyMesh = source.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh).First(m => m != null);
            var body = new GameObject("DiceBody", typeof(MeshFilter), typeof(MeshRenderer)); body.transform.SetParent(orientation, false); body.GetComponent<MeshFilter>().sharedMesh = bodyMesh; v.diceBody = body.GetComponent<MeshRenderer>(); v.diceBody.sharedMaterial = Load<Material>(Art + "/Dice/Materials/mat_dice_ceramic.mat");
            Vector3[] normals = { Vector3.up, Vector3.forward, Vector3.right, Vector3.left, Vector3.back, Vector3.down }; v.physicalFaces = new MeshFilter[6];
            for (int i = 0; i < 6; i++)
            {
                string fid = "F" + (i + 1).ToString("00"); var anchor = new GameObject("Face_" + fid).transform; anchor.SetParent(orientation, false); anchor.localPosition = normals[i] * .502f; anchor.localRotation = Quaternion.FromToRotation(Vector3.forward, normals[i]);
                var face = new GameObject("DataFace_" + fid, typeof(MeshFilter), typeof(MeshRenderer)); face.transform.SetParent(anchor, false); v.physicalFaces[i] = face.GetComponent<MeshFilter>(); v.physicalFaces[i].sharedMesh = DefaultFace(i + 1); face.GetComponent<MeshRenderer>().sharedMaterial = Load<Material>(Art + "/Dice/Materials/mat_dice_pips.mat");
            }
            return v;
        }
        // An inspectable authored 1..6 default die; BindDie replaces these with the committed run data.
        private static Mesh DefaultFace(int pips)
        {
            string path = Art + "/Dice/Models/default_face_" + pips + ".asset"; Folder(path);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (mesh != null) return mesh;
            var vertices = new List<Vector3>(); var triangles = new List<int>(); var colors = new List<Color>();
            Action<float, float, float, Color, float> dot = (x, y, radius, color, z) =>
            {
                int start = vertices.Count; vertices.Add(new Vector3(x, y, z)); colors.Add(color);
                for (int j = 0; j <= 12; j++) { float a = j * Mathf.PI / 6; vertices.Add(new Vector3(x + Mathf.Cos(a) * radius, y + Mathf.Sin(a) * radius, z)); colors.Add(color); if (j > 0) triangles.AddRange(new[] { start, start + j, start + j + 1 }); }
            };
            Color ink = pips == 1 || pips == 4 ? UiKit.Red : UiKit.Blue;
            Action<float, float> pip = (x, y) => { dot(x, y, .076f, UiKit.Ink, 0); dot(x, y, .064f, ink, .001f); };
            if (pips % 2 == 1) pip(0, 0);
            if (pips >= 2) { pip(-.22f, .22f); pip(.22f, -.22f); }
            if (pips >= 4) { pip(.22f, .22f); pip(-.22f, -.22f); }
            if (pips == 6) { pip(-.22f, 0); pip(.22f, 0); }
            mesh = new Mesh { name = "Art07 authored default face " + pips }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.SetColors(colors); mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); written.Add(path); return mesh;
        }
        private static Art07PrefabView DiceBreak()
        {
            var go = new GameObject("fx_dice_break", typeof(Art07PrefabView)); var v = go.GetComponent<Art07PrefabView>(); v.assetId = "fx_dice_break"; v.isEffect = true; v.motion = Art07PrefabView.Motion.Shatter; v.defaultDuration = .32f;
            SceneManager.MoveGameObjectToScene(go, buildScene);
            var source = Load<GameObject>(Art + "/Dice/Models/dice_fragments.fbx"); var meshes = source.GetComponentsInChildren<MeshFilter>().Where(f => f.sharedMesh != null).ToArray(); if (meshes.Length != 6) throw new InvalidOperationException("Expected six authored fragment meshes.");
            v.particles = new Transform[6];
            for (int i = 0; i < 6; i++) { var part = new GameObject("Fragment_" + (i + 1).ToString("00"), typeof(MeshFilter), typeof(MeshRenderer)); part.transform.SetParent(go.transform, false); part.transform.localPosition = meshes[i].transform.localPosition; part.GetComponent<MeshFilter>().sharedMesh = meshes[i].sharedMesh; part.GetComponent<MeshRenderer>().sharedMaterial = Load<Material>(Art + "/Dice/Materials/mat_dice_glass.mat"); v.particles[i] = part.transform; }
            return v;
        }
        public static void BuildAtlases()
        {
            EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;
            if (EditorSettings.spritePackerMode != SpritePackerMode.SpriteAtlasV2) throw new InvalidOperationException("Sprite packing did not enable.");
            Atlas(Art + "/UI/Atlases/atlas_ui_pixel.spriteatlasv2", new[] { Art + "/UI/Templates", Art + "/UI/Badges", Art + "/UI/Symbols", Art + "/FX/Textures" }, false);
            Atlas(Art + "/UI/Atlases/atlas_content_icons_pixel.spriteatlasv2", new[] { Art + "/ContentIcons" }, false);
            Atlas(Art + "/FX/Atlases/atlas_fx_soft.spriteatlasv2", new[] { Art + "/FX/Textures" }, true);
        }
        private static void Atlas(string path, string[] folders, bool soft)
        {
            Object[] sprites = AssetDatabase.FindAssets("t:Sprite", folders).Select(AssetDatabase.GUIDToAssetPath).Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) && (!p.Contains("/FX/Textures/") || p.Contains("_soft") == soft)).Distinct().OrderBy(p => p, StringComparer.Ordinal).Select(AssetDatabase.LoadAssetAtPath<Sprite>).Where(s => s != null).Cast<Object>().ToArray();
            if (sprites.Length == 0) throw new InvalidOperationException("Atlas has no sprites: " + path);
            Folder(path); bool author = !File.Exists(path);
            if (!author)
            {
                // The only runtime type use here is the V2-supported GetPackables read.
                var runtime = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path); var current = runtime == null ? Array.Empty<Object>() : runtime.GetPackables();
                author = !current.Select(AssetDatabase.GetAssetPath).OrderBy(s => s, StringComparer.Ordinal).SequenceEqual(sprites.Select(AssetDatabase.GetAssetPath));
                if (author) { var existing = SpriteAtlasAsset.Load(path); existing.Remove(current); existing.Add(sprites); SpriteAtlasAsset.Save(existing, path); }
            }
            else { var asset = new SpriteAtlasAsset(); asset.Add(sprites); SpriteAtlasAsset.Save(asset, path); }
            if (author) AssetDatabase.ImportAsset(path);
            var importer = AssetImporter.GetAtPath(path) as SpriteAtlasImporter; if (importer == null) throw new InvalidOperationException("Not a V2 SpriteAtlas importer: " + path);
            var texture = importer.textureSettings; var packing = importer.packingSettings; var platform = importer.GetPlatformSettings("DefaultTexturePlatform");
            bool dirty = !importer.includeInBuild || texture.filterMode != (soft ? FilterMode.Bilinear : FilterMode.Point) || texture.generateMipMaps || texture.readable || !texture.sRGB || packing.enableRotation || packing.enableTightPacking || !packing.enableAlphaDilation || packing.padding != 4 || platform.maxTextureSize != 2048 || platform.textureCompression != TextureImporterCompression.Uncompressed;
            if (dirty)
            {
                texture.filterMode = soft ? FilterMode.Bilinear : FilterMode.Point; texture.generateMipMaps = false; texture.readable = false; texture.sRGB = true;
                packing.enableRotation = false; packing.enableTightPacking = false; packing.enableAlphaDilation = true; packing.padding = 4;
                platform.maxTextureSize = 2048; platform.textureCompression = TextureImporterCompression.Uncompressed;
                importer.textureSettings = texture; importer.packingSettings = packing; importer.SetPlatformSettings(platform); importer.includeInBuild = true; importer.SaveAndReimport();
            }
            written.Add(path);
        }
        private static void BuildConfig(Plan plan)
        {
            string source = File.ReadAllText(PlanPath); string dir = "Assets/Game/Config/Art07"; Directory.CreateDirectory(dir);
            WriteJson(dir + "/art07_theme.json", "{\"artVersion\":\"art0.7.0\",\"theme\":" + Property(source, "theme") + ",\"layout\":" + Property(source, "layout") + ",\"buttons\":" + Property(source, "buttons") + "}");
            WriteJson(dir + "/art07_motion.json", "{\"artVersion\":\"art0.7.0\",\"presentation\":" + Property(source, "presentation") + "}");
            WriteJson(dir + "/art07_fx_budget.json", "{\"artVersion\":\"art0.7.0\",\"globalBudgets\":" + Property(Property(source, "sharedResources"), "globalBudgets") + "}");
            WriteJson(dir + "/art07_face_visuals.json", "{\"artVersion\":\"art0.7.0\",\"dice\":" + Property(source, "dice") + ",\"enchantVisuals\":" + Property(source, "enchantVisuals") + "}");
            var mappings = File.Exists("../ArtSource/Art07/path_mappings.json") ? JsonUtility.FromJson<MappingFile>(File.ReadAllText("../ArtSource/Art07/path_mappings.json")).records.ToDictionary(m => m.assetId) : new Dictionary<string, Mapping>();
            var rows = plan.sharedResources.assets.Select(a => new MapEntry { assetId = a.assetId, runtimePath = mappings.TryGetValue(a.assetId, out var m) ? m.runtimePath : a.runtimePath, sourcePath = mappings.TryGetValue(a.assetId, out var n) ? n.sourcePath : a.sourcePath }).ToList();
            rows.AddRange(plan.contentResources.Select(c => new MapEntry { assetId = c.icon_id, runtimePath = c.iconExport, sourcePath = c.iconSource }));
            foreach (var row in rows)
            {
                if (row.assetId == "shader_ui_local_fx") row.runtimePath = row.sourcePath = "Assets/Game/Runtime/Shaders/Art07LocalFx.shader";
                if (row.assetId == "font_cjk_source") row.runtimePath = row.sourcePath = "Assets/Game/Fonts/NotoSansCJKsc-Regular.otf";
                if (row.assetId == "font_cjk_license") row.runtimePath = row.sourcePath = "Assets/Game/Fonts/LICENSE.txt";
                if (row.assetId == "font_cjk_tmp") row.runtimePath = row.sourcePath = "Assets/Game/Fonts/DiceChinese.asset";
                if (row.assetId.StartsWith("atlas_") && row.runtimePath.EndsWith(".spriteatlas", StringComparison.Ordinal)) row.runtimePath = row.sourcePath = row.runtimePath + "v2";
                row.exists = File.Exists(row.runtimePath); row.guid = row.exists ? AssetDatabase.AssetPathToGUID(row.runtimePath) : "";
            }
            string resourceMapPath = dir + "/art07_resource_map.json";
            WriteJson(resourceMapPath, JsonUtility.ToJson(new MapOutput { assets = rows.ToArray() }, true));
            // The map's own imported GUID only exists after the first valid map write.
            var self = rows.First(r => r.assetId == "art07_resource_map"); self.exists = true; self.guid = AssetDatabase.AssetPathToGUID(resourceMapPath);
            WriteJson(resourceMapPath, JsonUtility.ToJson(new MapOutput { assets = rows.ToArray() }, true));
        }
        private static void WriteJson(string path, string text) { if (!File.Exists(path) || File.ReadAllText(path) != text) File.WriteAllText(path, text); AssetDatabase.ImportAsset(path); written.Add(path); }
        /// <summary>Copies an exact raw JSON value; braces inside quoted strings never terminate it.</summary>
        private static string Property(string json, string name)
        {
            string key = "\"" + name + "\""; int i = json.IndexOf(key, StringComparison.Ordinal); if (i < 0) throw new InvalidDataException("Missing art property " + name);
            i = json.IndexOf(':', i + key.Length) + 1; while (i < json.Length && char.IsWhiteSpace(json[i])) i++; int start = i; int depth = 0; bool quoted = false, escaped = false;
            for (; i < json.Length; i++)
            {
                char c = json[i]; if (quoted) { if (escaped) escaped = false; else if (c == '\\') escaped = true; else if (c == '"') quoted = false; continue; }
                if (c == '"') { quoted = true; continue; } if (c == '{' || c == '[') depth++; else if (c == '}' || c == ']') { depth--; if (depth == 0) return json.Substring(start, i - start + 1); }
            }
            throw new InvalidDataException("Unterminated art property " + name);
        }
    }
    public sealed class Art07AtlasPrebuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;
        public void OnPreprocessBuild(BuildReport report) { Art07AssetLibrary.BuildAtlases(); }
    }
}
