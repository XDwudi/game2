using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DiceCraft.Presentation.Tests
{
    public sealed class W17SettlementMaskImportTests
    {
        [Test]
        public void EveryConfiguredBossUsesAnInstalledFamilyIncludingFinalBosses()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Game/Scenes/Bootstrap.unity");
            try
            {
                var app=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<GameApp>(true)).Single();
                string[] names={"cards","constraint","economy","final","multiplier","seal","target"};
                Assert.That(app.defaultBossEmblems15.Select(sprite=>sprite.name),Is.EquivalentTo(names.Select(name=>"emblem_"+name)));
                var config=JsonUtility.FromJson<DiceCraft.Core.GameConfig>(DiceCraft.Core.GameConfig.PrepareJsonForUnity(
                    System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath,"Game/Data/P0Config.json"))));
                Assert.That(config.bossModifiers.Length,Is.EqualTo(39));
                foreach(var modifier in config.bossModifiers)
                {
                    string family=GameApp.BossEmblemFamily15(modifier.id,modifier.tier);
                    Assert.That(names,Does.Contain(family),modifier.id);
                    Assert.That(family=="final",Is.EqualTo(modifier.tier==4),modifier.id);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void AllSevenMasksAreBoundOnceAsWholeTintableSprites()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Game/Scenes/Bootstrap.unity");
            try
            {
                var app = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameApp>(true)).Single();
                string[] names = { "ui_emblem_tier1", "ui_emblem_tier2", "ui_emblem_tier3", "ui_emblem_tier4",
                    "ui_icon_stage", "ui_icon_submit", "ui_icon_interest" };
                foreach (string name in names)
                {
                    string path = "Assets/Art/Art11/Surfaces/" + name + ".png";
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    Assert.That(sprite, Is.Not.Null, path);
                    Assert.That(app.art07Templates.Where(s => s != null && s.name == name), Is.EqualTo(new[] { sprite }), name);
                    int size = name.StartsWith("ui_emblem_", System.StringComparison.Ordinal) ? 352 : 96;
                    Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(size, size)), name);
                    Assert.That(sprite.border, Is.EqualTo(Vector4.zero), name);
                    Assert.That(sprite.pixelsPerUnit, Is.EqualTo(200), name);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect), name);
                    Assert.That(importer.mipmapEnabled, Is.False, name);
                    Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear), name);
                    Assert.That(importer.alphaIsTransparency, Is.True, name);
                    Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), name);
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
