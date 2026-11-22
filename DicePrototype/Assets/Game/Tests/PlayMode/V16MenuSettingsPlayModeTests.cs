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
        [UnityTest] public IEnumerator PLAY_V16_MenuNewRunAndSettingsUseApprovedControlsAtEveryTextScale()
        {
            int previousScale=PlayerPrefs.GetInt("art07.textScale",100);
            bool hadScale=PlayerPrefs.HasKey("art07.textScale");
            try
            {
                for(int step=0;step<3;step++)
                {
                    var paper=FindActive<RectTransform>("MenuWordmark");
                    Assert.That(paper.sizeDelta,Is.EqualTo(new Vector2(1000,334)));
                    Assert.That(InformationText07("MenuRules"),Is.EqualTo("掷骰  →  选中  →  锁定  →  提交"));
                    Assert.That(HasActive("ContinueRun")||HasActive("LockHint"),Is.False);
                    var newRun=Button("NewRun");
                    Assert.That(((RectTransform)newRun.transform).anchoredPosition,Is.EqualTo(new Vector2(36,-148)));
                    Click("NewRun");
                    Assert.That(InformationText07("DialogTitle"),Is.EqualTo("新的一局"));
                    Assert.That(InformationText07("InitialDiceLabel"),Is.EqualTo("初始 "+app.Config.initialDiceCount+" 颗骰子"));
                    Assert.That(InformationText07("SeedHelp16"),Is.EqualTo("同一个种子会掷出同样的骰子和商店"));
                    Assert.That(InformationButton07("StartNew"),Is.EqualTo("开始"));
                    Click("CloseDialog");Click("MenuSettings");Click("SettingsTab1");
                    var choice=(RectTransform)Button("Theme").transform;
                    Assert.That(choice.anchoredPosition.x,Is.EqualTo(UiKit.FontScale>=1.25f?12:460));
                    var help=FindActive<TextMeshProUGUI>("QualityHint");
                    Assert.That(help.rectTransform.rect.height,Is.GreaterThanOrEqualTo(help.preferredHeight-1));
                    var content=help.GetComponentInParent<ScrollRect>().content;
                    Assert.That(content.rect.height,Is.GreaterThanOrEqualTo(-help.rectTransform.anchoredPosition.y+help.rectTransform.rect.height));
                    Click("SettingsTab2");Click("TextScale");Click("SettingsDone");
                    yield return null;
                }
                Assert.That(app.Session.State,Is.Null);
            }
            finally
            {
                if(hadScale)PlayerPrefs.SetInt("art07.textScale",previousScale);else PlayerPrefs.DeleteKey("art07.textScale");
                UiKit.FontScale=previousScale/100f;
            }
        }

        [UnityTest] public IEnumerator PLAY_V16_EachThemeKeepsOneMenuPrimaryWithAndWithoutACheckpoint()
        {
            var previousTheme=app.ThemeId11;
            try
            {
                foreach(bool saved in new[]{false,true})
                {
                    if(saved){StartRun("16010");Click("Pause");Click("MainMenu");}
                    string file=saved?ExtraFileHash07(app.SaveLocation):null;
                    foreach(int scale in new[]{100,150})
                    {
                        SettlementField07("textScale07",scale);UiKit.FontScale=scale/100f;
                        foreach(var theme in ThemeRegistry.All)
                        {
                            app.ApplyTheme11(theme.Id);
                            var panel=FindActive<RectTransform>("MenuPanel");
                            var mainActions=panel.GetComponentsInChildren<Button>(false).Where(b=>b.IsInteractable()&&W11PrimaryColoured(b.targetGraphic.color)).Select(b=>b.name);
                            Assert.That(mainActions,Is.EqualTo(new[]{saved?"ContinueRun":"NewRun"}),theme.Id+" "+scale);
                            Assert.That(HasActive("ContinueRun"),Is.EqualTo(saved));
                            Assert.That(HasActive("LockHint"),Is.False);
                            var wordmark=FindActive<RectTransform>("MenuWordmark");
                            Assert.That(FindActive<TextMeshProUGUI>("MenuRules").transform.IsChildOf(wordmark),Is.True);
                            var action=(RectTransform)Button("NewRun").transform;
                            Assert.That(action.anchoredPosition.y,Is.EqualTo(saved?-342:-148));
                            Assert.That(app.canvas.GetComponentsInChildren<Image>().Count(image=>image.name=="PrintedCardBack"),Is.EqualTo(2));
                            for(int i=0;i<3;i++)Assert.That(app.table.GetFaceOrientation("$menu"+i).gameObject.activeInHierarchy,Is.True);
                            yield return null;
                        }
                    }
                    if(saved)Assert.That(ExtraFileHash07(app.SaveLocation),Is.EqualTo(file));
                    else Assert.That(File.Exists(app.SaveLocation),Is.False);
                }
            }
            finally { SettlementField07("textScale07",100);UiKit.FontScale=1;app.ApplyTheme11(previousTheme,false); }
        }

        [UnityTest] public IEnumerator PLAY_V16_SixPreviewsUseNativeSpritesAndOneCurrentMarker()
        {
            int oldPreview=PlayerPrefs.GetInt(ThemeRegistry.PreviewKey,0);
            bool hadPreview=PlayerPrefs.HasKey(ThemeRegistry.PreviewKey);
            try
            {
                PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,1);
                Click("MenuSettings");Click("SettingsTab1");Click("Theme");
                var choices=ThemeRegistry.Selectable(true);
                Assert.That(choices.Count,Is.EqualTo(6));
                for(int i=0;i<choices.Count;i++)
                {
                    var tile=Button("ThemeTile16_"+choices[i].Id);
                    var panel=(RectTransform)tile.transform;
                    float height=UiKit.FontScale>=1.25f?285:277;
                    Assert.That(panel.anchoredPosition,Is.EqualTo(new Vector2(24+(i%3)*416,-(140+(i/3)*(height+16)))));
                    var preview=tile.transform.Find("ThemePreview16").GetComponent<Image>();
                    Assert.That(preview.sprite,Is.Not.Null,choices[i].Id);
                    Assert.That(preview.sprite.rect.size,Is.EqualTo(new Vector2(480,270)));
                    Assert.That(preview.raycastTarget,Is.False);
                    bool current=choices[i].Id==app.ThemeId11;
                    Assert.That(InformationButton07(tile.name).StartsWith("✓ "),Is.EqualTo(current));
                    var ring=tile.transform.Find("CurrentTheme16");
                    Assert.That(ring!=null&&ring.gameObject.activeInHierarchy,Is.EqualTo(current));
                }
                Click("ThemeDone16");
                Assert.That(HasActive("DisplaySettings"),Is.True);
                Click("SettingsDone");
                Assert.That(app.Session.State,Is.Null);
                yield return null;
            }
            finally
            {
                if(hadPreview)PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,oldPreview);else PlayerPrefs.DeleteKey(ThemeRegistry.PreviewKey);
            }
        }
    }
}
