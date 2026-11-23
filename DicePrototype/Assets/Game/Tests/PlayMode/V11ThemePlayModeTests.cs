using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        private static readonly string[] ReleasedThemes11 = { "swiss", "sancai", "comic", "deco", "glass", "default" };

        private static IEnumerator CaptureReleasedTheme11(string name)
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/cache/v11/theme-opening",
                Screen.width+"x"+Screen.height));
            Directory.CreateDirectory(folder);
            yield return new WaitForEndOfFrame();
            var image=ScreenCapture.CaptureScreenshotAsTexture();
            try { File.WriteAllBytes(Path.Combine(folder,name+".png"),image.EncodeToPNG()); }
            finally { Object.Destroy(image); }
        }

        private void CheckMenuTheme11(GameTheme theme)
        {
            Assert.That(app.ThemeId11,Is.EqualTo(theme.Id));
            Assert.That(UiKit.TitleFont,Is.SameAs(theme.TitleFont));
            Assert.That(UiKit.TitleRoles,Is.EqualTo(theme.TitleRoles));
            var wordmark=FindActive<RectTransform>("MenuWordmark").Find("Title").GetComponent<Image>();
            Assert.That(wordmark.sprite,Is.SameAs(UiKit.Template("ui_wordmark_dicecraft")),theme.Id);
            var renderer=GameObject.Find("Art07Felt").GetComponent<Renderer>();
            var properties=new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            Texture expected=theme.PageScene!=null?theme.PageScene:theme.TableScene;
            if(expected==null)expected=renderer.sharedMaterial.GetTexture("_TableMap");
            Assert.That(expected,Is.Not.Null,theme.Id);
            Assert.That(properties.GetTexture("_TableMap"),Is.SameAs(expected),theme.Id+" actual menu table");
        }

        [UnityTest]
        public IEnumerator PLAY_V11_ReleasedThemesSwitchFromMainMenuAndPersistWithoutCreatingSave()
        {
            bool hadPreview=PlayerPrefs.HasKey(ThemeRegistry.PreviewKey);
            int oldPreview=PlayerPrefs.GetInt(ThemeRegistry.PreviewKey,0);
            bool hadTheme=PlayerPrefs.HasKey(ThemeRegistry.PreferenceKey);
            string oldTheme=PlayerPrefs.GetString(ThemeRegistry.PreferenceKey,GameTheme.DefaultId);
            try
            {
                PlayerPrefs.DeleteKey(ThemeRegistry.PreviewKey);
                PlayerPrefs.DeleteKey(ThemeRegistry.PreferenceKey);
                yield return LoadBootstrap();
                Assert.That(app.ThemeId11,Is.EqualTo(GameTheme.DefaultId));
                Click("MenuSettings");Click("SettingsTab1");Click("Theme");
                CollectionAssert.AreEquivalent(ReleasedThemes11,ThemeRegistry.Selectable(false).Select(t=>t.Id));
                foreach(string id in ReleasedThemes11)
                {
                    var theme=ThemeRegistry.Find(id);
                    Assert.That(theme.Preview,Is.False,id);
                    Click("ThemeTile16_"+id);
                    CheckMenuTheme11(theme);
                    Assert.That(PlayerPrefs.GetString(ThemeRegistry.PreferenceKey),Is.EqualTo(id));
                    Assert.That(InformationButton07("ThemeTile16_"+id),Is.EqualTo("✓ "+theme.DisplayName));
                    Assert.That(app.Session.State,Is.Null,id);
                    Assert.That(File.Exists(app.SaveLocation),Is.False,id);
                    yield return null;
                }
                Click("ThemeTile16_glass");
                yield return CaptureReleasedTheme11("main-menu-settings-glass");
                Click("ThemeDone16");Click("SettingsDone");
                CheckMenuTheme11(ThemeRegistry.Find("glass"));
                yield return CaptureReleasedTheme11("main-menu-glass");
                // A scene reload preserves static fields. Clear the live appearance without modifying
                // PlayerPrefs so that a missing startup restore cannot pass by retaining the last theme.
                app.ApplyTheme11(GameTheme.DefaultId);
                Assert.That(app.ThemeId11,Is.EqualTo(GameTheme.DefaultId));
                Assert.That(PlayerPrefs.GetString(ThemeRegistry.PreferenceKey),Is.EqualTo("glass"));
                yield return LoadBootstrap();
                CheckMenuTheme11(ThemeRegistry.Find("glass"));
                Assert.That(app.Session.State,Is.Null);
                Assert.That(File.Exists(app.SaveLocation),Is.False);
            }
            finally
            {
                if(hadPreview)PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,oldPreview);else PlayerPrefs.DeleteKey(ThemeRegistry.PreviewKey);
                if(hadTheme)PlayerPrefs.SetString(ThemeRegistry.PreferenceKey,oldTheme);else PlayerPrefs.DeleteKey(ThemeRegistry.PreferenceKey);
                PlayerPrefs.Save();
                app.ApplyTheme11(oldTheme,false);
            }
        }

        [UnityTest]
        public IEnumerator PLAY_V11_ThemeSwitchFromSettingsRecoloursAndKeepsRunAndSave()
        {
            bool hadPreview=PlayerPrefs.HasKey(ThemeRegistry.PreviewKey);int oldPreview=PlayerPrefs.GetInt(ThemeRegistry.PreviewKey,0);
            bool hadTheme=PlayerPrefs.HasKey(ThemeRegistry.PreferenceKey);string oldTheme=PlayerPrefs.GetString(ThemeRegistry.PreferenceKey,GameTheme.DefaultId);
            try
            {
                PlayerPrefs.DeleteKey(ThemeRegistry.PreviewKey);
                PlayerPrefs.DeleteKey(ThemeRegistry.PreferenceKey);
                yield return LoadBootstrap();
                StartRun("71101");Click("StartStage");Click("Roll");app.SkipVisual();yield return WaitForIdle();
                Assert.That(app.ThemeId11,Is.EqualTo(GameTheme.DefaultId));
                var standard=new ThemePalette();
                Assert.That(Button("Pause").transform.Find("Face").GetComponent<Image>().color,Is.EqualTo(standard.paper));
                var swiss=ThemeRegistry.Find("swiss");
                Assert.That(swiss,Is.Not.Null);
                Assert.That(swiss.Palette.paper,Is.Not.EqualTo(standard.paper));
                string state=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);long sequence=app.Session.ActionSequence;

                Click("Pause");Click("Settings");Click("SettingsTab1");
                Assert.That(Button("Theme").GetComponentInChildren<TextMeshProUGUI>().text,Does.StartWith("默认"));
                Click("Theme");Click("ThemeTile16_swiss");
                Assert.That(app.ThemeId11,Is.EqualTo(swiss.Id));
                Assert.That(PlayerPrefs.GetString(ThemeRegistry.PreferenceKey),Is.EqualTo(swiss.Id));
                Assert.That(InformationButton07("ThemeTile16_swiss"),Is.EqualTo("✓ "+swiss.DisplayName));
                yield return CaptureReleasedTheme11("pause-settings-swiss");
                Click("ThemeDone16");
                Assert.That(InformationButton07("Theme"),Does.StartWith(swiss.DisplayName));
                Assert.That(UiKit.Panel,Is.EqualTo(swiss.Palette.panel));
                Click("SettingsDone");yield return null;
                Assert.That(Button("Pause").transform.Find("Face").GetComponent<Image>().color,Is.EqualTo(swiss.Palette.paper));
                Assert.That(StateJson(),Is.EqualTo(state));Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence));
                CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));

                app.ApplyTheme11(GameTheme.DefaultId);
                Assert.That(Button("Pause").transform.Find("Face").GetComponent<Image>().color,Is.EqualTo(standard.paper));
                Assert.That(PlayerPrefs.GetString(ThemeRegistry.PreferenceKey),Is.EqualTo(swiss.Id));
                yield return LoadBootstrap();
                Assert.That(app.ThemeId11,Is.EqualTo(swiss.Id));
                CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
                Click("ContinueRun");
                Assert.That(Button("Pause").transform.Find("Face").GetComponent<Image>().color,Is.EqualTo(swiss.Palette.paper));
                Assert.That(StateJson(),Is.EqualTo(state));Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence));
                CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
            }
            finally
            {
                if(hadPreview)PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,oldPreview);else PlayerPrefs.DeleteKey(ThemeRegistry.PreviewKey);
                if(hadTheme)PlayerPrefs.SetString(ThemeRegistry.PreferenceKey,oldTheme);else PlayerPrefs.DeleteKey(ThemeRegistry.PreferenceKey);
                PlayerPrefs.Save();
                app.ApplyTheme11(oldTheme,false);
            }
        }

        [UnityTest]
        public IEnumerator PLAY_V11_PreviewThemesStayHiddenWithoutPreviewSetting()
        {
            bool hadPreview=PlayerPrefs.HasKey(ThemeRegistry.PreviewKey);
            int oldPreview=PlayerPrefs.GetInt(ThemeRegistry.PreviewKey,0);
            const string probe="unreleased-probe11";
            ThemeRegistry.RegisterForTests(GameTheme.CreateForTests(probe,new ThemePalette()));
            try
            {
                PlayerPrefs.DeleteKey(ThemeRegistry.PreviewKey);
                Assert.That(ThemeRegistry.Selectable(true).Any(t=>t.Id==probe),Is.True);
                CollectionAssert.AreEquivalent(ReleasedThemes11,ThemeRegistry.Selectable(ThemeRegistry.ShowPreviewThemes).Select(t=>t.Id));
                Click("MenuSettings");Click("SettingsTab1");Click("Theme");
                foreach(var theme in ThemeRegistry.All)
                    Assert.That(HasActive("ThemeTile16_"+theme.Id),Is.EqualTo(!theme.Preview),theme.Id);
                Click("CloseDialog");yield return null;
            }
            finally
            {
                ThemeRegistry.UnregisterForTests(probe);
                if(hadPreview)PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,oldPreview);else PlayerPrefs.DeleteKey(ThemeRegistry.PreviewKey);
            }
        }
    }
}
