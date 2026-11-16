using System.Collections;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace DiceCraft.Tests
{
    // V1.1 G7 (docs/specs/v11/DEV_PLAN.md §4.6): F-THEME-01 (settings switch, persistence) and F-THEME-03
    // (a switch never changes the run or the save file). Asset completeness per theme (F-THEME-02) is
    // checked by the theme audit once the themes have their own art.
    public sealed partial class GameplayPlayModeTests
    {
        [UnityTest]
        public IEnumerator PLAY_V11_ThemeSwitchFromSettingsRecoloursAndKeepsRunAndSave()
        {
            bool hadPreview=PlayerPrefs.HasKey(ThemeRegistry.PreviewKey);int oldPreview=PlayerPrefs.GetInt(ThemeRegistry.PreviewKey,0);
            bool hadTheme=PlayerPrefs.HasKey(ThemeRegistry.PreferenceKey);string oldTheme=PlayerPrefs.GetString(ThemeRegistry.PreferenceKey,GameTheme.DefaultId);
            try
            {
                PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,1);
                StartRun("71101");Click("StartStage");Click("Roll");app.SkipVisual();yield return WaitForIdle();
                Assert.That(app.ThemeId11,Is.EqualTo(GameTheme.DefaultId));
                var standard=new ThemePalette();
                Assert.That(Button("Pause").transform.Find("Face").GetComponent<UnityEngine.UI.Image>().color,Is.EqualTo(standard.paper),"The default theme keeps the v0.73 colours.");
                var swiss=ThemeRegistry.Find("swiss");
                Assert.That(swiss,Is.Not.Null,"Assets/Resources/Themes/swiss/theme.json must be found.");
                Assert.That(swiss.Palette.paper,Is.Not.EqualTo(standard.paper));
                string state=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);long sequence=app.Session.ActionSequence;

                Click("Pause");Click("Settings");Click("SettingsTab1");
                Assert.That(Button("Theme").GetComponentInChildren<TextMeshProUGUI>().text,Does.StartWith("默认"));
                Click("Theme");
                Assert.That(app.ThemeId11,Is.EqualTo(swiss.Id),"The first theme after the default is the Swiss theme (order 1).");
                Assert.That(PlayerPrefs.GetString(ThemeRegistry.PreferenceKey),Is.EqualTo(swiss.Id));
                Assert.That(Button("Theme").GetComponentInChildren<TextMeshProUGUI>().text,Does.StartWith(swiss.DisplayName));
                Assert.That(UiKit.Panel,Is.EqualTo(swiss.Palette.panel));
                Click("SettingsDone");yield return null;
                Assert.That(Button("Pause").transform.Find("Face").GetComponent<UnityEngine.UI.Image>().color,Is.EqualTo(swiss.Palette.paper),"The page behind the settings was redrawn with the new palette.");
                Assert.That(StateJson(),Is.EqualTo(state));Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence));
                CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation),"A theme switch never writes the save file.");

                app.ApplyTheme11(GameTheme.DefaultId);
                Assert.That(Button("Pause").transform.Find("Face").GetComponent<UnityEngine.UI.Image>().color,Is.EqualTo(standard.paper));
                Assert.That(StateJson(),Is.EqualTo(state));
            }
            finally
            {
                app.ApplyTheme11(GameTheme.DefaultId,false);
                if(hadPreview)PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,oldPreview);else PlayerPrefs.DeleteKey(ThemeRegistry.PreviewKey);
                if(hadTheme)PlayerPrefs.SetString(ThemeRegistry.PreferenceKey,oldTheme);else PlayerPrefs.DeleteKey(ThemeRegistry.PreferenceKey);
            }
        }

        [UnityTest]
        public IEnumerator PLAY_V11_PreviewThemesStayHiddenWithoutPreviewSetting()
        {
            bool hadPreview=PlayerPrefs.HasKey(ThemeRegistry.PreviewKey);int oldPreview=PlayerPrefs.GetInt(ThemeRegistry.PreviewKey,0);
            try
            {
                PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,0);
                foreach(var theme in ThemeRegistry.Selectable(ThemeRegistry.ShowPreviewThemes))
                    Assert.That(theme.Preview,Is.False,theme.Id+" is unfinished and must not be offered to players.");
                Assert.That(ThemeRegistry.Selectable(false),Has.Some.Matches<GameTheme>(t=>t.IsDefault));
                yield return null;
            }
            finally{if(hadPreview)PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,oldPreview);else PlayerPrefs.DeleteKey(ThemeRegistry.PreviewKey);}
        }
    }
}
