using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        private const string SubmitSymbol17="ui_icon_submit";
        private static readonly string[] IconThemes17={"default","swiss","sancai","comic","deco","glass"};

        private void OpenCounter17()
        {
            var scene=W11Scene(new[]{"C02"},new[]{4,4,4,4,2},"rrrrr",5,true,stageIndex:0,target:"1");
            scene.remainingSubmissions=2;
            W11Apply(scene);
        }

        private IEnumerator SubmitReceipt17()
        {
            Click("Submit");app.SkipVisual();yield return WaitForIdle();
            Click("EndStage");app.SkipVisual();yield return WaitForIdle();
            Assert.That(app.Session.State.phase,Is.EqualTo("StageSettlement"));
        }

        private IEnumerator MeasureMark17(Image mark,RectTransform area,string tag,List<string> measurements)
        {
            Assert.That(mark.sprite,Is.Not.Null,tag);Assert.That(mark.preserveAspect,Is.True);Assert.That(mark.raycastTarget,Is.False);
            Rect rect=V06ScreenRect(mark.rectTransform);
            Assert.That(Art073Inside(rect,V06ScreenRect(area)),Is.True,tag);
            Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();
            var drawn=ScreenCapture.CaptureScreenshotAsTexture();
            Texture2D bare=null;
            try
            {
                mark.enabled=false;
                yield return new WaitForEndOfFrame();
                bare=ScreenCapture.CaptureScreenshotAsTexture();
                var foreground=drawn.GetPixels32();var background=bare.GetPixels32();
                var samples=new List<KeyValuePair<int,float>>();
                for(int y=Mathf.CeilToInt(rect.yMin);y<Mathf.FloorToInt(rect.yMax);y++)
                    for(int x=Mathf.CeilToInt(rect.xMin);x<Mathf.FloorToInt(rect.xMax);x++)
                    {
                        int index=y*drawn.width+x;var f=foreground[index];var b=background[index];
                        int difference=Math.Abs(f.r-b.r)+Math.Abs(f.g-b.g)+Math.Abs(f.b-b.b);
                        samples.Add(new KeyValuePair<int,float>(difference,GameApp.Contrast11(f,b)));
                    }
                var ink=samples.OrderByDescending(s=>s.Key).Take(Math.Max(1,samples.Count/10)).Select(s=>s.Value).OrderBy(r=>r).ToArray();
                float ratio=ink[ink.Length/2];
                measurements.Add(tag+" "+mark.sprite.name+" "+rect.size+" median core "+ratio.ToString("F2"));
                Assert.That(ratio,Is.GreaterThanOrEqualTo(4.5f),tag+" screenshot contrast at opaque cores");
            }
            finally
            {
                mark.enabled=true;
                UnityEngine.Object.Destroy(drawn);
                if(bare!=null)UnityEngine.Object.Destroy(bare);
            }
        }

        [UnityTest]
        public IEnumerator PLAY_W17_SubmitIconOnHudAndReceiptAtBothTextScales([ValueSource(nameof(IconThemes17))] string theme)
        {
            StartRun("17073");FinalOptions07("Instant",false);
            bool hadPreview=PlayerPrefs.HasKey(ThemeRegistry.PreviewKey);int preview=PlayerPrefs.GetInt(ThemeRegistry.PreviewKey);
            string prior=app.ThemeId11;PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,1);
            var measurements=new List<string>{"Unity "+Application.unityVersion+" source "+IdentityHead20X()};
            try
            {
                app.ApplyTheme11(theme);Assert.That(app.ThemeId11,Is.EqualTo(theme));
                foreach(int scale in new[]{100,150})
                {
                    W11TextScale(scale);OpenCounter17();yield return W11Settle();
                    var expected=ThemeRegistry.Current.Template(SubmitSymbol17)??UiKit.Templates.Single(s=>s!=null&&s.name==SubmitSymbol17);
                    var well=FindActive<RectTransform>("SubmitBudgetWell11");var mark=FindActive<Image>("SubmitMark");
                    Assert.That(mark.sprite,Is.SameAs(expected));
                    Rect at=W14Design((RectTransform)mark.transform.parent),box=W14Design(well);
                    Assert.That(Vector2.Distance(at.position,box.position+new Vector2(14,25)),Is.LessThan(.6f));
                    Assert.That(Vector2.Distance(at.size,new Vector2(36,36)),Is.LessThan(.6f));
                    Assert.That(mark.rectTransform.anchoredPosition,Is.EqualTo(Vector2.zero));
                    Assert.That(mark.rectTransform.sizeDelta,Is.EqualTo(at.size));
                    yield return MeasureMark17(mark,well,theme+" "+scale+" HUD",measurements);
                    yield return SubmitReceipt17();
                    var chip=FindActive<RectTransform>("SettlementUnusedRewardChip");
                    var receipt=chip.Find("Icon").GetComponent<Image>();
                    Assert.That(receipt.sprite,Is.SameAs(expected));
                    Assert.That(receipt.rectTransform.sizeDelta,Is.EqualTo(Vector2.one*(scale==100?28:36)));
                    yield return MeasureMark17(receipt,chip,theme+" "+scale+" receipt",measurements);
                }
            }
            finally
            {
                string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","artifacts","cache","v11","w17","submit",Screen.width+"x"+Screen.height));
                Directory.CreateDirectory(dir);File.WriteAllLines(Path.Combine(dir,theme+".txt"),measurements);
                if(hadPreview)PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,preview);else PlayerPrefs.DeleteKey(ThemeRegistry.PreviewKey);
                app.ApplyTheme11(prior);W11TextScale(100);
            }
        }

        [UnityTest] public IEnumerator PLAY_W17_SubmitIconThemeOverrideDefaultAndProceduralFallback()
        {
            StartRun("17173");FinalOptions07("Instant",false);
            var original=UiKit.Templates;var themed=UiKit.ThemeTemplate;
            var defaultIcon=original.Single(s=>s!=null&&s.name==SubmitSymbol17);
            var witness=original.Single(s=>s!=null&&s.name=="ui_icon_stage");
            try
            {
                foreach(string source in new[]{"theme","default","missing","restored"})
                {
                    UiKit.ThemeTemplate=name=>name==SubmitSymbol17?(source=="theme"?witness:null):themed?.Invoke(name);
                    UiKit.Templates=source=="missing"?original.Where(s=>s!=null&&s!=defaultIcon).ToArray():original;
                    OpenCounter17();yield return W11Settle();
                    var counter=FindActive<RectTransform>("SubmitBudgetIcon");
                    var mark=counter.GetComponentsInChildren<Image>(false).SingleOrDefault(i=>i.name=="SubmitMark");
                    if(source=="missing")
                    {
                        Assert.That(mark,Is.Null);
                        foreach(string part in new[]{"Ticket","TicketLine1","TicketLine2","LeftNotch","RightNotch"})
                            Assert.That(counter.Find(part).gameObject.activeInHierarchy,Is.True,part);
                    }
                    else
                    {
                        Assert.That(mark.sprite,Is.SameAs(source=="theme"?witness:defaultIcon));
                        Assert.That(counter.Find("Ticket")?.gameObject.activeInHierarchy??false,Is.False);
                    }
                    yield return SubmitReceipt17();
                    var chip=FindActive<RectTransform>("SettlementUnusedRewardChip");
                    var icon=chip.GetComponentsInChildren<Image>(false).SingleOrDefault(i=>i.name=="Icon");
                    if(source=="missing")
                    {
                        Assert.That(icon,Is.Null);
                        Assert.That(((RectTransform)chip.Find("SettlementUnusedReward")).anchoredPosition.x,Is.EqualTo(12));
                    }
                    else Assert.That(icon.sprite,Is.SameAs(source=="theme"?witness:defaultIcon));
                }
            }
            finally {UiKit.Templates=original;UiKit.ThemeTemplate=themed;app.Refresh();}
        }
    }
}
