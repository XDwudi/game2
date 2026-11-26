using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using DiceCraft.Verification;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        private string ResultOutput15
        {
            get
            {
                string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","artifacts","cache","v11","w15","results",Screen.width+"x"+Screen.height));
                Directory.CreateDirectory(dir); return dir;
            }
        }

        /// <summary>p1.1.1 (RULES_DELTA 7.3): glass breaks after a submission, never on a roll. The "no dice" ending is now the
        /// last die, a locked glass die, breaking after a submission that misses the target with submissions left (the
        /// session ends the run with NoDice); p1.1.0 broke it on a roll.</summary>
        private static void NoDiceScene111(DebugScene scene)
        {
            scene.dice.RemoveRange(1,scene.dice.Count-1);var last=scene.dice[0];last.isGlass=true;var up=last.faces[0];
            scene.currentResults=new List<DieHandState>{new DieHandState{dieId=last.dieId,currentFaceId=up.faceId,currentPip=up.permanentPip,currentColor=up.baseColor,hasRolled=true,isLocked=true}};
            scene.orderedLockedIds.Clear();scene.orderedLockedIds.Add(last.dieId);
            scene.remainingSubmissions=2;scene.target="999999999999999999999";
            scene.randomOverrides.Add(new DebugRandomOverride{kind="Glass",sourceId=last.dieId,value="break"});
        }
        private IEnumerator TerminalFixture15(string kind)
        {
            var previous=app.Session.State;
            var scene=Art07Fixtures.Create(app.Config,kind=="win"?Art07FixtureKind.MaximumTable:Art07FixtureKind.G147,previous.nextItemSerial,previous.nextDieSerial);
            scene.sourceRunId=previous.sourceRunId;
            scene.stageIndex=kind=="win"?23:0; scene.remainingSubmissions=1; scene.target=kind=="submissions"?"999999999999999999999":"1";
            if(kind=="win")
            {
                scene.modifierId="B38";
                // MaximumTable contains two stage-temporary dice. Keep them so the real
                // settlement expires them, then exercise both result pages with 12 survivors.
                int extraPermanent = 12 - scene.dice.Count(d => d.lifetime == "permanent");
                for(int i=0;i<extraPermanent;i++)
                {
                    var extra=DiceFactory.CreateStandard(app.Config,scene.dice.Max(d=>d.createdSerial)+1);
                    scene.dice.Add(extra);
                    var f=extra.faces[0];scene.currentResults.Add(new DieHandState{dieId=extra.dieId,currentFaceId=f.faceId,currentPip=f.permanentPip,currentColor=f.baseColor,hasRolled=true});
                }
            }
            else scene.controllers.Clear();
            if(kind=="temporary")foreach(var d in scene.dice)d.lifetime="stage_temporary";
            if(kind=="no-dice")NoDiceScene111(scene);
            W11Apply(scene);
            Click("Submit");app.SkipVisual();yield return WaitForIdle();
            if(app.Session.State.phase=="StageSettlement"){Click("SettlementContinue");yield return WaitForIdle();}
            Assert.That(app.Session.State.phase,Is.EqualTo(kind=="win"?"RunWon":"RunFailed"),kind);
            if(kind=="win")
            {
                var surviving = app.Session.State.dice;
                Assert.That(surviving.Count, Is.EqualTo(12), "The paging fixture needs twelve survivors after settlement.");
                Assert.That(surviving.All(d=>d.lifetime=="permanent"), Is.True);
            }
        }

        [UnityTest] public IEnumerator PLAY_V15_SE_ResultsSixThemesAndTerminalCases()
        {
            Assert.That(Screen.width==1920&&Screen.height==1080||Screen.width==1280&&Screen.height==720);
            var clean=GameApp.CleanCapture11;var issues=new List<string>();var report=new List<string>{IdentityHead20X()+" | Unity "+Application.unityVersion};
            try
            {
                GameApp.CleanCapture11=true;StartRun("20261122");
                Assert.That(app.UnlockDebug("XD is god"));Assert.That(app.EnterDebugWorkspace());Click("CloseDialog");
                foreach(string kind in new[]{"win","submissions","temporary","no-dice"})
                {
                    yield return TerminalFixture15(kind);
                    if(HasActive("SkipResultReveal"))Click("SkipResultReveal");
                    string state=ExtraStateHash07(),save=ExtraFileHash07(app.SaveLocation);
                    foreach(string theme in new[]{"default","swiss","sancai","comic","deco","glass"})
                    {
                        app.ApplyTheme11(theme);Assert.That(app.ThemeId11,Is.EqualTo(theme));
                        foreach(int percent in new[]{100,150})
                        {
                            W11TextScale(percent); yield return Art073Settle(.2f);
                            string id=theme+"-"+kind+"-"+percent; bool large=percent==150;
                            var paper=FindActive<RectTransform>("RunResult");var dice=FindActive<RectTransform>("ResultDice15");
                            Assert.That(paper.anchoredPosition,Is.EqualTo(new Vector2(64,large?-40:-160)));
                            Assert.That(paper.sizeDelta,Is.EqualTo(new Vector2(1368,large?1000:760)));
                            Assert.That(dice.anchoredPosition,Is.EqualTo(new Vector2(1464,large?-40:-160)));
                            Assert.That(dice.sizeDelta,Is.EqualTo(new Vector2(424,large?1000:760)));
                            Assert.That(HasActive("Stats07")||HasActive("Pause")||HasActive("Handbook"),Is.False);
                            Assert.That(HasActive("PreviousSubmission"),Is.EqualTo(app.Session.State.lastSubmission!=null));
                            Assert.That(HasActive("BossEmblem15"),Is.EqualTo(kind=="win"));
                            Assert.That(HasActive("EndWhere15"),Is.EqualTo(kind!="win"));
                            Assert.That(HasActive("SkipResultReveal"),Is.False);
                            Assert.That(W11PrimaryColoured(Button("Again").targetGraphic.color));
                            foreach(string n in new[]{"EndBreakdown","ReturnMenu"})Assert.That(W11PrimaryColoured(Button(n).targetGraphic.color),Is.False,n);
                            var cards=FindActive<RectTransform>("EndBuild");
                            Assert.That(cards.GetComponentsInChildren<Button>(false).Count(b=>b.name.StartsWith("Controller_")),Is.EqualTo(app.Session.State.controllers.Count));
                            var graphics=app.canvas.GetComponentsInChildren<Graphic>(false);
                            var labels=paper.GetComponentsInChildren<TextMeshProUGUI>(false).Concat(dice.GetComponentsInChildren<TextMeshProUGUI>(false))
                                .Where(t=>!string.IsNullOrWhiteSpace(t.text)).ToArray();
                            Assert.That(labels.Length,Is.GreaterThan(12),id);
                            foreach(var t in labels)W13GlyphCheck(t,id,graphics,issues);
                            yield return W11bContrastPass(id,report,issues,W14Main);
                            yield return ResultShot15(id);
                            if(kind=="win")
                            {
                                Assert.That(FindActive<RectTransform>("ResultDice15").GetComponentsInChildren<Art073DieView>().Count(v=>v.IsShown),Is.EqualTo(10));
                                Click("ResultDiceNext");yield return Art073Settle(.15f);
                                Assert.That(FindActive<TextMeshProUGUI>("ResultDicePage").text,Is.EqualTo("2/2"));
                                var secondPage = FindActive<RectTransform>("ResultDice15");
                                Assert.That(secondPage.GetComponentsInChildren<Art073DieView>().Count(v=>v.IsShown),Is.EqualTo(2));
                                Assert.That(secondPage.Find("ResultDie15_10"), Is.Not.Null);
                                Assert.That(secondPage.Find("ResultDie15_11"), Is.Not.Null);
                                Click("ResultDicePrevious");yield return null;
                            }
                        }
                    }
                    if(kind=="win")
                    {
                        Click("Controller_"+app.Session.State.controllers[0].instanceId);
                        Assert.That(HasActive("Dialog"));Assert.That(HasActive("Sell"),Is.False);Click("CloseDialog");
                    }
                    Assert.That(ExtraStateHash07(),Is.EqualTo(state));Assert.That(ExtraFileHash07(app.SaveLocation),Is.EqualTo(save));
                }
            }
            finally
            {
                GameApp.CleanCapture11=clean;W11TextScale(100);app.ApplyTheme11(GameTheme.DefaultId,false);
                File.WriteAllLines(Path.Combine(ResultOutput15,"result-measurements.txt"),report.Concat(issues));
            }
            Assert.That(issues,Is.Empty,string.Join("\n",issues));
        }

        private IEnumerator ResultShot15(string name)
        {
            yield return new WaitForEndOfFrame();var texture=ScreenCapture.CaptureScreenshotAsTexture();
            try { File.WriteAllBytes(Path.Combine(ResultOutput15,name+".png"),texture.EncodeToPNG()); }
            finally {UnityEngine.Object.Destroy(texture);}
        }

        [UnityTest] public IEnumerator PLAY_V15_SE11_ReloadDuringBossEntranceKeepsCommittedStage()
        {
            var shop = ShopAfterStage10(1);
            shop.cycles[0].modifierId = "B25";
            yield return W14Load(shop);
            Click("LeaveShop");
            yield return WaitForIdle();
            Click("StartStage");
            var motion = app.GetComponent<BossEntryMotion15>();
            Assert.That(motion.Playing, Is.True);
            motion.AutomaticTick = false;
            var state = ExtraStateHash07();
            string savePath = app.SaveLocation, save = ExtraFileHash07(savePath);

            yield return LoadBootstrap();
            Assert.That(motion == null, Is.True);
            ResumeBoundaryFixture();
            Assert.That(app.BossEntryPlaying15, Is.False);
            Assert.That(app.Session.State.phase, Is.EqualTo("HandReady"));
            Assert.That(ExtraStateHash07(), Is.EqualTo(state));
            Assert.That(ExtraFileHash07(savePath), Is.EqualTo(save));
        }

        [UnityTest] public IEnumerator PLAY_V15_SE11_BossEntranceRealStartAndSpeeds()
        {
            foreach(string mode in new[]{"standard","fast","reduced","instant"})
            {
                var shop=ShopAfterStage10(1);shop.cycles[0].modifierId="B25";shop.cycles[0].lootKind="L1";
                yield return W14Load(shop);Click("LeaveShop");yield return WaitForIdle();
                SettlementField07("fast",mode=="fast");SettlementField07("reduced",mode=="reduced");
                UiKit.ReducedMotion=mode=="reduced";SettlementField07("instant07",mode=="instant");
                Click("StartStage");Assert.That(app.Session.State.phase,Is.EqualTo("HandReady"));
                Assert.That(Button("Roll").IsInteractable()); Assert.That(app.IsBusy,Is.False);
                if(mode=="instant"){Assert.That(app.BossEntryPlaying15,Is.False);continue;}
                var motion=app.GetComponent<BossEntryMotion15>();Assert.That(motion.Playing,Is.True);
                motion.AutomaticTick=false;
                var card=FindActive<RectTransform>("StageCard11");var emblem=card.Find("BossModifier10/BossEmblem11");
                var state=ExtraStateHash07();var save=ExtraFileHash07(app.SaveLocation);var seq=app.Session.ActionSequence;
                float ratio=mode=="fast"?.5f:1f;
                Assert.That(motion.Duration,Is.EqualTo(mode=="reduced"?.12f:.76f*ratio).Within(.001f));
                if(mode=="reduced")
                {
                    motion.Tick(.06f);Assert.That(card.GetComponent<CanvasGroup>().alpha,Is.EqualTo(.5f).Within(.01f));
                    Assert.That(emblem.localScale,Is.EqualTo(Vector3.one));
                }
                else
                {
                    float start=card.anchoredPosition.x;motion.Tick(.1f*ratio);Assert.That(card.anchoredPosition.x,Is.EqualTo(start).Within(.1f));
                    motion.Tick(.15f*ratio);Assert.That(card.anchoredPosition.x,Is.EqualTo(start-420+52.5f).Within(.3f));
                    Assert.That(app.TableMood11,Is.EqualTo(.625f).Within(.01f));
                    yield return ResultShot15("boss-entry-mid-"+mode);
                    motion.Tick(.15f*ratio);Assert.That(emblem.localScale.x,Is.EqualTo(1.18f).Within(.005f));
                }
                app.Refresh();Assert.That(motion.Playing,Is.True);
                motion.Tick(motion.Duration-motion.Elapsed+.0001f);Assert.That(motion.Playing,Is.False);
                Assert.That(emblem.localScale,Is.EqualTo(Vector3.one));Assert.That(app.TableMood11,Is.EqualTo(1));
                Assert.That(ExtraStateHash07(),Is.EqualTo(state));Assert.That(ExtraFileHash07(app.SaveLocation),Is.EqualTo(save));Assert.That(app.Session.ActionSequence,Is.EqualTo(seq));
                motion.AutomaticTick=true;
            }
            foreach(string theme in new[]{"default","swiss","sancai","comic","deco","glass"})
            {
                foreach(int percent in new[]{100,150})
                {
                    var shop=ShopAfterStage10(1);shop.cycles[0].modifierId="B25";shop.cycles[0].lootKind="L1";
                    yield return W14Load(shop);app.ApplyTheme11(theme);W11TextScale(percent);
                    Click("LeaveShop");yield return WaitForIdle();Click("StartStage");
                    var motion=app.GetComponent<BossEntryMotion15>();motion.AutomaticTick=false;
                    motion.Tick(.25f);yield return ResultShot15(theme+"-boss-entry-"+percent);
                    Click("Roll");Assert.That(motion.Playing,Is.False,"Real gameplay command finishes the visual");
                    motion.AutomaticTick=true;app.SkipVisual();yield return WaitForIdle();
                }
            }
        }
    }
}
