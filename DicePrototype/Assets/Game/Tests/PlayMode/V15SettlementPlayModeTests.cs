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

        [Serializable] private sealed class ReceiptTrace15
        {
            public uint seed=0;
            public string configVersion=null;
            public ReceiptStep15[] steps=null;
        }
        [Serializable] private sealed class ReceiptStep15
        {
            public string kind=null,offerId=null;
            public string[] dieIds=null;
            public bool risk=false;
        }
        private static readonly string[] ReceiptBoardRoots15={"StageCard11","Tray11","PoolTab11","SubmitInfo11","LockedTray071","RollBudgetValue","SubmitBudgetValue"};
        private static readonly int[] ColdReceiptScales15={100,150};

        // Play the recorded first chapter using only a normal session. The two target EndStage clicks and
        // subsequent menu ContinueRun clicks hit actual controls; neither a fixture save nor debug unlock is used.
        private IEnumerator NormalReceiptPath15(int lastStage,int scale,Func<int,Dictionary<string,Rect>,IEnumerator> visit)
        {
            var path=Path.Combine(Application.dataPath,"..","..","evidence","v11","balance","trace_p110_5000.json");
            var trace=JsonUtility.FromJson<ReceiptTrace15>(File.ReadAllText(path));
            Assert.That(trace.configVersion,Is.EqualTo(app.Config.configVersion));
            Assert.That(trace.steps[0].kind,Is.EqualTo("NewRun"));
            StartRun(trace.seed.ToString());FinalOptions07("Instant",false);
            Click("Pause");Click("Settings");Click("SettingsTab2");
            for(int n=0;n<3&&Mathf.Abs(UiKit.FontScale-scale/100f)>.001f;n++)Click("TextScale");
            Assert.That(UiKit.FontScale,Is.EqualTo(scale/100f).Within(.001f));Click("SettingsDone");
            Assert.That(app.Session.State.isDebug||app.Session.State.debugWorkspace,Is.False);
            for(int i=1;i<trace.steps.Length;i++)
            {
                var step=trace.steps[i];var kind=(CommandKind)Enum.Parse(typeof(CommandKind),step.kind);
                int stage=app.Session.State.stage.stageIndex;
                Assert.That(kind,Is.Not.EqualTo(CommandKind.ApplyDebugScene));
                if(kind==CommandKind.EndStage&&(stage==0||stage==lastStage))
                {
                    FinalOptions07("Normal",false);
                    yield return ShopEntranceFrame07();
                    var ready=app.Session.State;
                    Assert.That(InformationText07("RollBudgetValue"),Is.EqualTo(ready.hand.rollsRemaining.ToString()),"live hand at EndStage");
                    Assert.That(InformationText07("SubmitBudgetValue"),Is.EqualTo(ready.stage.remainingSubmissions.ToString()),"live stage at EndStage");
                    TestContext.WriteLine($"SE14 live stage {stage+1}: rolls {ready.hand.rollsRemaining}, submissions {ready.stage.remainingSubmissions}.");
                    var live=ReceiptBoardRoots15.ToDictionary(n=>n,n=>V06ScreenRect(FindActive<RectTransform>(n)));
                    ShopEntrancePointer07("EndStage");
                    Assert.That(app.Session.State.phase,Is.EqualTo("StageSettlement"));
                    yield return ShopEntranceFrame07();
                    if(app.SettlementRevealPlaying07)ShopEntrancePointer07("SkipSettlementReveal07");
                    yield return visit(stage,live);
                    if(stage==lastStage)yield break;
                    FinalOptions07("Instant",false);
                    continue;
                }
                if(kind==CommandKind.ConfirmLocks)
                {
                    foreach(var id in step.dieIds)Assert.That(app.Session.TogglePending(id),Is.True);
                    app.Refresh();
                }
                var result=app.Send(new SessionCommand{kind=kind,risk=step.risk,offerId=step.offerId,dieIds=step.dieIds??Array.Empty<string>()});
                Assert.That(result.accepted,Is.True,"normal seed "+trace.seed+" step "+i+" "+kind+" "+result.error);
                yield return WaitForIdle();
            }
            Assert.Fail("The recorded chapter ended before the requested live settlement.");
        }

        [UnityTest,Timeout(180000)] public IEnumerator PLAY_V15_SE14_NormalMenuContinuesLiveOrdinaryAndBossReceipts(
            [ValueSource(nameof(ColdReceiptScales15))] int scale)
        {
            IEnumerator Verify(int stage,Dictionary<string,Rect> live)
            {
                string tag="normal-save-"+(stage==0?"ordinary":"boss")+"-"+scale;
                Assert.That(app.Session.State.stage.stageKind,Is.EqualTo(stage==0?"normal":"boss"));
                Assert.That(app.SettlementBackdropVisible07,Is.True);
                Assert.That(app.SettlementBackdropFromHistory07,Is.False);
                AssertReceiptBackdrop15(FindActive<Art07SettlementBackdrop>("SettlementBackdrop07"));
                var saved=app.Session.State;long seq=app.Session.ActionSequence;
                string path=app.SaveLocation,hash=GameSession.StateHash(saved);byte[] bytes=File.ReadAllBytes(path);
                Assert.That(Path.GetFileName(path),Is.EqualTo("active-run.json"));
                Assert.That(GameSession.StateHash(new AtomicSaveStore(path,app.Config,SaveSlotPolicy.Normal).Load()),Is.EqualTo(hash));
                yield return WriteReceiptShot15(tag+"-live");
                yield return LoadBootstrap();yield return ShopEntranceFrame07();
                ShopEntrancePointer07("ContinueRun");yield return ShopEntranceFrame07();
                Assert.That(app.SaveLocation,Is.EqualTo(path));Assert.That(app.Session.State.isDebug||app.Session.State.debugWorkspace,Is.False);
                Assert.That(app.Session.State.phase,Is.EqualTo("StageSettlement"));Assert.That(UiKit.FontScale,Is.EqualTo(scale/100f).Within(.001f));
                Assert.That(app.SettlementRevealPlaying07||app.PresentationPlaying07,Is.False);
                Assert.That(app.SettlementBackdropFromHistory07&&app.SettlementBackdropVisible07,Is.True);
                var backdrop=FindActive<Art07SettlementBackdrop>("SettlementBackdrop07");
                AssertReceiptBackdrop15(backdrop);Assert.That(backdrop.GetComponentsInChildren<Selectable>(true),Is.Empty);
                Assert.That(backdrop.GraphicCount,Is.GreaterThan(0));Assert.That(backdrop.DiceMeshCount,Is.GreaterThan(0));
                var board=FindActive<RectTransform>("Screen").Find("SavedSettlementBoardStaging07");
                Assert.That(board,Is.Not.Null);Assert.That(board.gameObject.activeSelf,Is.False);
                var snapshot=saved.lastSubmission.snapshot;
                int unused=saved.lastStageSettlement.remainingSubmissions;
                // Each sampled run ends early, so the final receipt and pre-submit snapshot are distinct.
                Assert.That(snapshot.submissionsRemainingBefore,Is.Not.EqualTo(unused),tag+" must exercise two different counts");
                Assert.That(InformationText07("SettlementUnusedReward"),Does.Contain($"剩余提交 {unused} 次"),tag+" paper chip");
                Assert.That(board.Find("SubmitBudgetValue").GetComponent<TextMeshProUGUI>().text,Is.EqualTo(unused.ToString()),tag+" cold committed count");
                Assert.That(board.Find("RollBudgetValue").GetComponent<TextMeshProUGUI>().text,Is.EqualTo(snapshot.rollsRemaining.ToString()),tag+" cold historical hand");
                CollectionAssert.IsSubsetOf(new[]{"SubmitBudgetValue","RollBudgetValue"},backdrop.CapturedRootNames15);
                TestContext.WriteLine($"SE14 cold {tag}: saved hand {snapshot.handId}, rolls {snapshot.rollsRemaining}, submitted with {snapshot.submissionsRemainingBefore}, unused {unused}.");
                foreach(var item in live)
                {
                    var restored=board.Find(item.Key) as RectTransform;Assert.That(restored,Is.Not.Null,item.Key);
                    var rect=V06ScreenRect(restored);
                    Assert.That(Vector2.Distance(rect.min,item.Value.min),Is.LessThan(2),tag+" "+item.Key+" minimum");
                    Assert.That(Vector2.Distance(rect.max,item.Value.max),Is.LessThan(2),tag+" "+item.Key+" maximum");
                }
                var frozen=GameObject.Find("SettlementFrozenDice07");
                Assert.That(frozen,Is.Not.Null);Assert.That(frozen.GetComponentsInChildren<Collider>(true),Is.Empty);
                var dice=snapshot.dice;
                var lockedBodies=frozen.GetComponentsInChildren<MeshRenderer>(false)
                    .Where(m=>m.name.StartsWith("$static-preview:settlement-locked-",StringComparison.Ordinal)&&m.name.EndsWith("/Body",StringComparison.Ordinal)).ToArray();
                Assert.That(lockedBodies.Length,Is.EqualTo(Math.Min(8,dice.Length)),tag+" visible historical submission page");
                for(int i=0;i<Math.Min(8,dice.Length);i++)
                {
                    var body=lockedBodies.Single(m=>m.name=="$static-preview:settlement-locked-"+i+"/Body");
                    var slot=(RectTransform)board.Find("Slot"+i);
                    Assert.That(V06ScreenRect(slot).Contains(app.table.viewCamera.WorldToScreenPoint(body.bounds.center)),Is.True,tag+" historical die outside slot "+i);
                    var die=dice[i];string face="$static-preview:settlement-locked-"+i+"/Face_"+die.faceId;
                    var mesh=frozen.GetComponentsInChildren<MeshFilter>(false).Single(m=>m.name==face);
                    Assert.That(mesh.sharedMesh.name,Does.StartWith("Pips_"+die.currentPip+"_"));
                    var index=board.Find("SlotIndex"+i);
                    Assert.That(index!=null&&index.gameObject.activeSelf,Is.False,tag+" bare slot caption");
                    Assert.That(board.Find("SavedSlotTab15_"+i+"/Ordinal"),Is.Not.Null);
                }
                yield return WriteReceiptShot15(tag+"-cold");
                Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(hash));
                Assert.That(app.Session.ActionSequence,Is.EqualTo(seq));
                CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));
            }
            yield return NormalReceiptPath15(2,scale,Verify);
        }

        [UnityTest] public IEnumerator PLAY_V15_SE14_AutomaticReceiptKeepsZeroUnusedAndRecordedExtraRoll()
        {
            var fixture=NewBoundaryFixture("se14-auto-extra-roll");
            string item=BoundaryItem(fixture,"I07");
            var memory=new MemorySaveStore();
            var core=new GameSession(app.Config,memory,fixture);
            CoreShopStep(core,CommandKind.StartStage);
            var ready=core.State;ready.stage.target="1";ready.stage.remainingSubmissions=1;
            core=new GameSession(app.Config,memory,ready);
            CoreShopStep(core,CommandKind.Roll);
            int afterRoll=core.State.hand.rollsRemaining;
            var grant=core.Execute(new SessionCommand{kind=CommandKind.UseItem,itemInstanceId=item,
                commandId="se14-extra-roll",expectedActionSequence=core.ActionSequence});
            Assert.That(grant.accepted,Is.True,grant.error);
            Assert.That(core.State.hand.rollsRemaining,Is.EqualTo(afterRoll+1));
            foreach(var die in core.State.dice)Assert.That(core.TogglePending(die.dieId),Is.True);
            CoreShopStep(core,CommandKind.ConfirmLocks);CoreShopStep(core,CommandKind.Submit);
            var settled=core.State;
            Assert.That(settled.phase,Is.EqualTo("StageSettlement"));
            Assert.That(settled.lastSubmission.snapshot.submissionsRemainingBefore,Is.EqualTo(1));
            Assert.That(settled.lastStageSettlement.remainingSubmissions,Is.Zero);
            Assert.That(settled.lastSubmission.snapshot.rollsRemaining,Is.EqualTo(afterRoll+1));
            yield return LoadBoundaryFixture(settled);ResumeBoundaryFixture();
            var board=FindActive<RectTransform>("Screen").Find("SavedSettlementBoardStaging07");
            Assert.That(app.SettlementBackdropFromHistory07,Is.True);
            Assert.That(board.Find("SubmitBudgetValue").GetComponent<TextMeshProUGUI>().text,Is.EqualTo("0"));
            Assert.That(board.Find("RollBudgetValue").GetComponent<TextMeshProUGUI>().text,Is.EqualTo((afterRoll+1).ToString()));
            Assert.That(InformationText07("SettlementUnusedReward"),Does.Contain("剩余提交 0 次"));
            yield return WriteReceiptShot15("automatic-zero-unused-extra-roll-cold");
        }

        [UnityTest,Timeout(90000)] public IEnumerator PLAY_V15_SE15_OrdinaryReceiptBlocksSecondPointerAtSamePosition()
        {
            IEnumerator Verify(int stage,Dictionary<string,Rect> live)
            {
                yield return ShopEntranceFrame07();
                var proceed=FindActive<RectTransform>("SettlementContinue");
                var point=RectTransformUtility.WorldToScreenPoint(null,proceed.TransformPoint(proceed.rect.center));
                long earlier=app.Session.ActionSequence;
                Art071PointerClick("SettlementContinue",point);
                double started=Time.realtimeSinceStartupAsDouble;
                Assert.That(app.LootPage15,Is.False);Assert.That(app.Session.State.phase,Is.EqualTo("Shop"));
                Assert.That(app.Session.ActionSequence,Is.EqualTo(earlier+1));var bytes=File.ReadAllBytes(app.SaveLocation);
                yield return ShopEntranceFrame07();
                while(Time.realtimeSinceStartupAsDouble-started<.1)yield return null;
                Assert.That(GameApp.PagePointerGuard15,Is.True,"Second point missed the 0.1–0.3 s protection window.");
                Assert.That(V06ScreenRect(FindActive<RectTransform>("LeaveShop")).Contains(point),Is.True);
                Art071PointerClick("LeaveShop",point);
                Assert.That(app.Session.State.phase,Is.EqualTo("Shop"));Assert.That(app.Session.ActionSequence,Is.EqualTo(earlier+1));
                CollectionAssert.AreEqual(bytes,File.ReadAllBytes(app.SaveLocation));
                while(Time.realtimeSinceStartupAsDouble-started<.32)yield return null;
                Assert.That(GameApp.PagePointerGuard15,Is.False);Art071PointerClick("LeaveShop",point);
                Assert.That(app.Session.State.phase,Is.EqualTo("StagePreview"));Assert.That(app.Session.ActionSequence,Is.EqualTo(earlier+2));
            }
            yield return NormalReceiptPath15(0,100,Verify);
        }
        private IEnumerator WriteReceiptShot15(string name)
        {
            Directory.CreateDirectory(ReceiptOutput15);Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();
            var shot=ScreenCapture.CaptureScreenshotAsTexture();
            try{WriteArtFrame23(Path.Combine(ReceiptOutput15,name+".png"),shot);}
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

        private DebugScene BossReceiptScene15(bool final,bool exhausted)
        {
            var scene=W11Scene(new[]{"C95","C133"},new[]{4,4,4,4,2},"bbbbb",5,true,
                stageIndex:final?app.Config.stages.Length-1:2,modifier:final?"B31":"B02",target:"1");
            scene.remainingSubmissions=1;
            if(exhausted)foreach(var die in scene.dice)die.lifetime="stage_temporary";
            return scene;
        }

        [UnityTest,Timeout(120000)]
        public IEnumerator PLAY_V15_SE17_BossReceiptScreens([ValueSource(nameof(LootStyles24))] string theme)
        {
            Assert.That(Screen.width==1920&&Screen.height==1080||Screen.width==1280&&Screen.height==720,Is.True);
            StartRun("11517");
            bool enabled=app.enabled,clean=GameApp.CleanCapture11;
            string oldTheme=app.ThemeId11;int oldScale=Mathf.RoundToInt(UiKit.FontScale*100);
            var report=new List<string>{IdentityHead20X()+" | Unity "+Application.unityVersion+" | "+Screen.width+"x"+Screen.height};
            try
            {
                // Drive the actual receipt clock so PNG readback cannot advance past its stamp or card flip.
                app.enabled=false;GameApp.CleanCapture11=true;
                SettlementField07("fast",false);SettlementField07("reduced",false);SettlementField07("instant07",false);
                app.ApplyTheme11(theme);
                foreach(int scale in new[]{100,150})
                foreach(string kind in new[]{"boss-mid","final","exhausted"})
                {
                    bool final=kind=="final",exhausted=kind=="exhausted";
                    W11TextScale(scale);W11Apply(BossReceiptScene15(final,exhausted));
                    // The preceding receipt's real Continue still guards the next page against a double-click.
                    if(GameApp.PagePointerGuard15)yield return new WaitUntil(()=>!GameApp.PagePointerGuard15);
                    yield return ShopEntranceFrame07();
                    Assert.That(app.ThemeId11,Is.EqualTo(theme));Assert.That(UiKit.FontScale,Is.EqualTo(scale/100f).Within(.001f));
                    long sequence=app.Session.ActionSequence;
                    ShopEntrancePointer07("Submit");
                    var committed=app.Session.State;
                    Assert.That(committed.actionSequence,Is.EqualTo(sequence+1));
                    Assert.That(committed.phase,Is.EqualTo("StageSettlement"));
                    Assert.That(committed.lastStageSettlement.stageKind,Is.EqualTo("boss"));
                    string branch=final?"RunWon":exhausted?"RunFailed":"Shop";
                    Assert.That(committed.stage.settlementContinuation,Is.EqualTo(branch));
                    Assert.That(committed.stage.stageIndex,Is.EqualTo(final?app.Config.stages.Length-1:2));
                    if(exhausted)
                    {
                        Assert.That(committed.dice,Is.Empty);
                        Assert.That(committed.failureReason,Is.EqualTo("NoPermanentDice"));
                        Assert.That(committed.lastEffects.Any(e=>e.kind=="TemporaryDieExpired"),Is.True);
                    }
                    var bytes=File.ReadAllBytes(app.SaveLocation);
                    app.SkipVisual();
                    var clock=app.GetComponent<Art07SettlementReveal>();
                    Assert.That(clock.IsPlaying,Is.True,kind);
                    string tag=theme+"-font"+scale;
                    if(kind=="boss-mid")
                    {
                        SettlementTick07(.35f);
                        var stamp=FindActive<RectTransform>("BossDefeated15");
                        Assert.That(clock.Elapsed,Is.EqualTo(.35f).Within(.001f));
                        Assert.That(stamp.GetComponent<CanvasGroup>().alpha,Is.EqualTo(1));
                        Assert.That(stamp.localScale.x,Is.GreaterThan(1.001f));
                        Assert.That(HasActive("LootBack15")&&HasActive("SkipSettlementReveal07"),Is.True);
                        Assert.That(HasActive("SettlementContinue"),Is.False);
                        yield return WriteReceiptShot15(tag+"-boss-mid");
                        SettlementTick07(.90f);
                        var front=FindActive<RectTransform>("LootFront15");
                        Assert.That(clock.IsPlaying&&HasActive("SkipSettlementReveal07"),Is.True);
                        Assert.That(HasActive("LootBack15"),Is.False);
                        Assert.That(front.localScale.x,Is.InRange(.1f,.9f));
                        yield return WriteReceiptShot15(tag+"-boss-flip");
                        report.Add(tag+" boss-mid at 0.35s (stamp and card back); boss-flip at 1.25s (turning card front)");
                        SettlementTick07(2);
                    }
                    else
                    {
                        SettlementTick07(2);Assert.That(clock.IsPlaying,Is.False);
                        Assert.That(HasActive("LootFront15")||HasActive("LootBack15"),Is.False);
                        Assert.That(InformationButton07("SettlementContinue"),Is.EqualTo(final?"查看整局结果":"查看结果"));
                        if(final)Assert.That(InformationText07("FinalBossNote15"),Is.EqualTo("终局首领 · 通关即胜利"));
                        else Assert.That(InformationText07("SettleNote15"),Does.Contain("没有骰子"));
                        yield return WriteReceiptShot15(tag+"-"+kind);
                        report.Add(tag+" "+kind+" stage="+(committed.stage.stageIndex+1)+" remaining dice="+committed.dice.Count+" continuation="+branch);
                    }
                    Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence+1));
                    CollectionAssert.AreEqual(bytes,File.ReadAllBytes(app.SaveLocation));
                    yield return ShopEntranceFrame07();ShopEntrancePointer07("SettlementContinue");
                    Assert.That(app.Session.State.phase,Is.EqualTo(branch));
                }
            }
            finally
            {
                Directory.CreateDirectory(ReceiptOutput15);
                File.WriteAllLines(Path.Combine(ReceiptOutput15,theme+"-se17-captures.txt"),report);
                app.enabled=enabled;GameApp.CleanCapture11=clean;app.ApplyTheme11(oldTheme);W11TextScale(oldScale);
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
                    W11Apply(BossReceiptScene15(final,exhausted));Assert.That(app.Send(new SessionCommand{kind=CommandKind.Submit}).accepted,Is.True);
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
