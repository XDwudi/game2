using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        private static void AssertReceiptBackdrop15(Art07SettlementBackdrop backdrop)
        {
            var roots=backdrop.CapturedRootNames15;
            CollectionAssert.IsSubsetOf(new[]{"StageCard11","Tray11","PoolTab11","SubmitInfo11","LockedTray071"},roots);
            Assert.That(roots.Intersect(new[]{"Roll","Confirm","Clear","Submit","EndStage","Skip","ActionReason","ActionReasonUnderlay",
                "Settle11","Qualified","QualifiedContinueIncome","GoalGap11","GoalReward11","GoalGapLabel11","Stats07"}),Is.Empty);
            Assert.That(roots.Any(n=>n.StartsWith("QualifiedReward",StringComparison.Ordinal)),Is.False);
        }

        private string ReceiptOutput15=>Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","artifacts","cache","v11","w15",
            Screen.width+"x"+Screen.height,"receipts"));
        private IEnumerator WriteReceiptShot15(string name)
        {
            Directory.CreateDirectory(ReceiptOutput15);Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();
            var shot=ScreenCapture.CaptureScreenshotAsTexture();
            try{File.WriteAllBytes(Path.Combine(ReceiptOutput15,name+".png"),shot.EncodeToPNG());}
            finally{UnityEngine.Object.Destroy(shot);}
        }

        [UnityTest] public IEnumerator PLAY_V15_SE_OrdinaryReceiptSixStylesAtTwoTextScales()
        {
            StartRun("11501");bool enabled=app.enabled,clean=GameApp.CleanCapture11;
            string original=app.ThemeId11;var report=new List<string>();var errors=new List<string>();
            Directory.CreateDirectory(ReceiptOutput15);
            try
            {
                app.enabled=false;GameApp.CleanCapture11=true;
                Assert.That(ThemeRegistry.All.Count,Is.EqualTo(6),"All six palettes are included.");
                foreach(var style in ThemeRegistry.All)foreach(int scale in new[]{100,150})
                {
                    app.ApplyTheme11(style.Id);PrepareRealSettlement07(scale);
                    string tag=style.Id+"-font"+scale;var cash=app.ViewState.lastStageSettlement;
                    Assert.That(HasActive("SettlementContinue"),Is.False);
                    Assert.That(InformationText07("SettlementNetCash"),Is.EqualTo("+0"));
                    Assert.That(InformationText07("Money"),Is.EqualTo(PlayerNumberFormat.Gold(cash.goldBefore)));
                    SettlementTick07(.48f);yield return WriteReceiptShot15(tag+"-normal-mid");
                    SettlementTick07(.27f);
                    var sheet=FindActive<RectTransform>("Rewards");
                    Assert.That(sheet.sizeDelta,Is.EqualTo(scale==100?new Vector2(880,620):new Vector2(1120,780)));
                    Assert.That(sheet.anchoredPosition,Is.EqualTo(scale==100?new Vector2(520,-176):new Vector2(400,-140)));
                    var continueButton=Button("SettlementContinue");
                    Assert.That(continueButton.GetComponent<RectTransform>().anchoredPosition,Is.EqualTo(new Vector2(1496,-962)));
                    Assert.That(W11PrimaryColoured(continueButton.targetGraphic.color),Is.True);
                    Assert.That(HasActive("SkipSettlementReveal07")||HasActive("SettlementDetails")||HasActive("SettlementStatus"),Is.False);
                    Assert.That(InformationText07("FinalStageScore"),Is.EqualTo("本关得分 "+PlayerNumberFormat.Exact(cash.score)+"（目标 "+PlayerNumberFormat.Exact(cash.target)+"）"));
                    Assert.That(InformationText07("SettlementNetCash"),Is.EqualTo("+"+PlayerNumberFormat.Gold(cash.goldAfter-cash.goldBefore)));
                    Assert.That(InformationText07("Money"),Is.EqualTo(PlayerNumberFormat.Gold(cash.goldAfter)));
                    AssertReceiptBackdrop15(FindActive<Art07SettlementBackdrop>("SettlementBackdrop07"));
                    var order=app.canvas.GetComponentsInChildren<Graphic>(false);
                    var labels=sheet.GetComponentsInChildren<TextMeshProUGUI>(false);
                    Assert.That(labels.Length,Is.GreaterThanOrEqualTo(9));
                    foreach(var label in labels)
                    {
                        Assert.That(label.fontSize,Is.GreaterThanOrEqualTo(label.name=="SettlementNetCash"?31.49f:19.49f),tag+" "+label.name);
                        W13GlyphCheck(label,tag,order,errors);
                    }
                    yield return W11bContrastPass(tag,report,errors,sheet);
                    yield return WriteReceiptShot15(tag+"-normal");
                    var disk=File.ReadAllBytes(app.SaveLocation);long seq=app.Session.ActionSequence;
                    Click("SettlementDetailsToggle15");app.Refresh();
                    Assert.That(HasActive("SettlementSummary15"),Is.False);Assert.That(HasActive("SettlementDetails"),Is.True);
                    Assert.That(InformationText07("SettlementInterestDetail"),Does.Contain("每关最多"));
                    yield return WriteReceiptShot15(tag+"-details");Click("SettlementDetailsToggle15");
                    Assert.That(app.Session.ActionSequence,Is.EqualTo(seq));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
                }
            }
            finally
            {
                app.enabled=enabled;GameApp.CleanCapture11=clean;app.ApplyTheme11(original);
                File.WriteAllLines(Path.Combine(ReceiptOutput15,"contrast-and-glyphs.txt"),report.Concat(errors.Select(s=>"FAIL "+s)));
            }
            Assert.That(report,Is.Not.Empty);Assert.That(errors,Is.Empty,string.Join("\n",errors));
        }

        [UnityTest] public IEnumerator PLAY_V15_SE_EscapeFreezesAndEachEnterAdvancesExactlyOneStep()
        {
            StartRun("11503");PrepareRealSettlement07(100);
            var clock=app.GetComponent<Art07SettlementReveal>();long settled=app.Session.ActionSequence;
            byte[] bytes=File.ReadAllBytes(app.SaveLocation);
            using(var keys=new RoutedKeyboard())
            {
                yield return keys.Press(UnityEngine.InputSystem.Key.Escape);
                Assert.That(HasActive("Resume"),Is.True);
                float at=clock.Elapsed;yield return new WaitForSecondsRealtime(.22f);
                Assert.That(clock.Elapsed,Is.EqualTo(at));
                yield return keys.Press(UnityEngine.InputSystem.Key.Escape);
                Assert.That(HasActive("Resume"),Is.False);
                yield return keys.Press(UnityEngine.InputSystem.Key.NumpadEnter);
                Assert.That(clock.IsPlaying,Is.False);Assert.That(app.Session.State.phase,Is.EqualTo("StageSettlement"));
                Assert.That(app.Session.ActionSequence,Is.EqualTo(settled));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(app.SaveLocation));
                yield return keys.Press(UnityEngine.InputSystem.Key.Enter);
                Assert.That(app.Session.State.phase,Is.EqualTo("Shop"));
                Assert.That(app.Session.ActionSequence,Is.EqualTo(settled+1));
            }
        }

        [UnityTest] public IEnumerator PLAY_V15_SE_BossStampFlipFinalOrExhaustedAndSavedWallet()
        {
            StartRun("11502");bool enabled=app.enabled;app.enabled=false;
            try
            {
                foreach(var mode in new[]{"normal","fast","reduced","instant","final","exhausted"})
                {
                    bool final=mode=="final",exhausted=mode=="exhausted";
                    SettlementField07("reduced",mode=="reduced");SettlementField07("fast",mode=="fast");SettlementField07("instant07",mode=="instant");
                    var scene=W11Scene(new[]{"C95","C133"},new[]{4,4,4,4,2},"bbbbb",5,true,stageIndex:final?app.Config.stages.Length-1:2,modifier:final?"B31":"B02",target:"1");
                    scene.remainingSubmissions=1;if(exhausted)foreach(var die in scene.dice)die.lifetime="stage_temporary";
                    W11Apply(scene);Assert.That(app.Send(new SessionCommand{kind=CommandKind.Submit}).accepted,Is.True);
                    app.SkipVisual();
                    Assert.That(app.Session.State.phase,Is.EqualTo("StageSettlement"));
                    var motion=app.GetComponent<Art07SettlementReveal>();
                    var boss=FindActive<RectTransform>("BossColumn15");Assert.That(boss,Is.Not.Null);
                    var cash=app.ViewState.lastStageSettlement;var disk=File.ReadAllBytes(app.SaveLocation);string state=StateJson();
                    float duration=mode=="fast"?.775f:mode=="reduced"?.12f:mode=="instant"?0:final?1.10f:exhausted?.74f:1.55f;
                    Assert.That(motion.Duration,Is.EqualTo(duration).Within(.001f));Assert.That(HasActive("LootPlaque15"),Is.EqualTo(!final&&!exhausted));
                    if(mode=="normal")
                    {
                        SettlementTick07(.35f);var stamp=FindActive<RectTransform>("BossDefeated15");
                        Assert.That(stamp.localScale.x,Is.InRange(1f,1.18f));Assert.That(HasActive("LootBack15"),Is.True);
                        SettlementTick07(.80f);Assert.That(HasActive("LootFront15"),Is.True);Assert.That(HasActive("LootBack15"),Is.False);
                        var front=FindActive<RectTransform>("LootFront15");Assert.That(front.localScale.x,Is.LessThan(.01f));
                        SettlementTick07(.25f);Assert.That(front.localScale.x,Is.EqualTo(1).Within(.001f));
                    }
                    SettlementTick07(2);Assert.That(motion.IsPlaying,Is.False);Assert.That(InformationText07("Money"),Is.EqualTo(PlayerNumberFormat.Gold(cash.goldAfter)));
                    Assert.That(InformationButton07("SettlementContinue"),Is.EqualTo(exhausted?"查看结果":final?"查看整局结果":"选择战利品"));
                    if(exhausted)Assert.That(InformationText07("SettleNote15"),Does.Contain("没有骰子"));
                    else if(final)Assert.That(InformationText07("FinalBossNote15"),Is.EqualTo("终局首领 · 通关即胜利"));
                    else Assert.That(InformationText07("LootShort15").Replace("\n",""),Is.EqualTo(app.Config.Loot(cash.lootKind).shortText));
                    Assert.That(StateJson(),Is.EqualTo(state));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
                    if(mode=="normal"||final||exhausted)yield return WriteReceiptShot15("boss-"+mode+(HasActive("MissingPicture15")?"-placeholder":""));
                }
            }
            finally{app.enabled=enabled;}
        }
    }
}
