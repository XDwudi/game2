using System.Collections;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using DiceCraft.Verification;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        private IEnumerator CaptureEdge07(string id)
        {
            yield return new WaitForSecondsRealtime(.22f);
            yield return new WaitForEndOfFrame();
            var texture=ScreenCapture.CaptureScreenshotAsTexture();
            Assert.That(texture,Is.Not.Null);Assert.That(texture.width,Is.EqualTo(Screen.width));
            string output=Path.Combine(saveDirectory,"art07-edge-screens");Directory.CreateDirectory(output);
            File.WriteAllBytes(Path.Combine(output,id+".png"),texture.EncodeToPNG());Object.Destroy(texture);
            File.WriteAllText(Path.Combine(output,id+".json"),JsonUtility.ToJson(new EdgeCapture07{screen=id,width=Screen.width,height=Screen.height,
                frame=Time.frameCount,unity=Application.unityVersion,mvid=typeof(GameApp).Module.ModuleVersionId.ToString(),stateHash=GameSession.StateHash(app.Session.State),
                phase=app.Session.State?.phase,savePath=app.SaveLocation},true));
            TestContext.WriteLine("Actual rendered screen (pending visual review): "+Path.Combine(output,id+".png"));
        }
        [System.Serializable] private sealed class EdgeCapture07
        {public string screen,unity,mvid,stateHash,phase,savePath;public int width,height,frame;public bool isolatedArtTest=true;}

        [UnityTest] public IEnumerator PLAY_ART07_ResultReloadIgnoresNonSubmitPlaceholdersAndPreservesExactStatistics()
        {
            // Reach a normal terminal Run through real UI commands and actual atomic writes.
            // Exhausting all rolls and submitting one die per hand stays below stage 1's target.
            StartRun("70991");Click("StartStage");
            long expectedTotal=0,expectedMaximum=0;int expectedMultiplier=0;
            for(int hand=0;hand<3;hand++)
            {
                while(app.Session.State.hand.rollsRemaining>0){Click("Roll");app.SkipVisual();}
                var lowest=app.Session.State.hand.currentResults.OrderBy(d=>d.currentPip).First();
                ClickDie(lowest.dieId);Click("Confirm");app.SkipVisual();
                var preview=app.Session.PreviewScore();
                Assert.That(preview.totalHandMult100,Is.EqualTo(100));Assert.That(preview.redMult100,Is.EqualTo(100));
                Assert.That(preview.controllerMultiplierNumerator,Is.EqualTo("1"));Assert.That(preview.controllerMultiplierDenominator,Is.EqualTo("1"));
                Assert.That(preview.enchantMultiplierCount,Is.Zero);Assert.That(preview.colorMult100%100,Is.Zero);
                Click("Submit");app.SkipVisual();
                var committed=app.Session.State.processedReceipts.Last();Assert.That(committed.command.kind,Is.EqualTo(CommandKind.Submit));
                long value=long.Parse(committed.score,System.Globalization.CultureInfo.InvariantCulture);
                expectedTotal+=value;expectedMaximum=System.Math.Max(expectedMaximum,value);expectedMultiplier=System.Math.Max(expectedMultiplier,preview.colorMult100/100);
            }
            Assert.That(app.Session.State.phase,Is.EqualTo("RunFailed"));Assert.That(app.Session.State.isDebug,Is.False);
            Assert.That(app.Session.State.processedReceipts.Count(r=>r.command.kind==CommandKind.Submit),Is.EqualTo(3));
            string ordinaryPath=app.SaveLocation;byte[] ordinary=File.ReadAllBytes(ordinaryPath);
            var store=new AtomicSaveStore(ordinaryPath,app.Config,SaveSlotPolicy.Normal);var loaded=store.Load();
            Assert.That(loaded,Is.Not.Null);Assert.That(store.LastLoadNotice,Is.Null);
            // This is the real Unity serialization boundary, not a hand-built null object.
            Assert.That(loaded.processedReceipts.Any(r=>r.command.kind!=CommandKind.Submit&&r.submission?.breakdown!=null&&string.IsNullOrEmpty(r.submission.breakdown.score)),Is.True);
            string stateHash=GameSession.StateHash(loaded);long sequence=loaded.actionSequence;
            var ordinarySnapshot=JsonUtility.ToJson(loaded);
            AssertResultReloadStatistics07(expectedTotal,expectedMaximum,expectedMultiplier);
            Assert.That(app.UnlockDebug("XD is god"),Is.True);Assert.That(app.EnterDebugWorkspace(),Is.True);
            Assert.DoesNotThrow(()=>app.ReturnNormalWorkspace(),"Returning from debug must ignore non-Submit receipt placeholders, not parse their empty decimal fields.");
            yield return null;
            Assert.That(app.SaveLocation,Is.EqualTo(ordinaryPath));Assert.That(app.IsDebugWorkspace,Is.False);
            Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(stateHash));Assert.That(app.Session.State.actionSequence,Is.EqualTo(sequence));
            Assert.That(StateJson(),Is.EqualTo(ordinarySnapshot));CollectionAssert.AreEqual(ordinary,File.ReadAllBytes(ordinaryPath));
            AssertResultReloadStatistics07(expectedTotal,expectedMaximum,expectedMultiplier);
            yield return app.StartCoroutine(CaptureEdge07("S18-normal-terminal-return"));
            // Cold Bootstrap/Continue reaches the same statistics without a duplicate command.
            yield return LoadBootstrap();Click("ContinueRun");yield return null;
            Assert.That(app.Session.State.phase,Is.EqualTo("RunFailed"));Assert.That(StateJson(),Is.EqualTo(ordinarySnapshot));
            AssertResultReloadStatistics07(expectedTotal,expectedMaximum,expectedMultiplier);
            CollectionAssert.AreEqual(ordinary,File.ReadAllBytes(ordinaryPath));
            yield return app.StartCoroutine(CaptureEdge07("S18-normal-terminal-cold-reload"));
            // Filtering by command kind must not turn a damaged real Submit into zero.
            var damaged=loaded.Clone();damaged.processedReceipts.First(r=>r.command.kind==CommandKind.Submit).submission.breakdown.score="";
            var rejected=Assert.Catch<System.Exception>(()=>app.Session.ValidateState(damaged));Assert.That(rejected.GetType().Name,Is.EqualTo("RuleViolation"));
            CollectionAssert.AreEqual(ordinary,File.ReadAllBytes(ordinaryPath));
        }
        private void AssertResultReloadStatistics07(long total,long maximum,int multiplier)
        {
            string stats=FindActive<TextMeshProUGUI>("EndStats").text;
            Assert.That(stats,Does.Contain("总得分  "+total.ToString("N0",System.Globalization.CultureInfo.InvariantCulture)));
            Assert.That(stats,Does.Contain("最大单手  "+maximum.ToString("N0",System.Globalization.CultureInfo.InvariantCulture)));
            Assert.That(stats,Does.Contain("最高倍率  ×"+multiplier+"\n"));
            Assert.That(stats,Does.Contain("普通对局"));
        }

        [UnityTest] public IEnumerator PLAY_ART07_ResultScreensUseRealTerminalCommandsAndPreserveExactReceipts()
        {
            StartRun("70781");
            Assert.That(app.UnlockDebug("XD is god"),Is.True);Assert.That(app.EnterDebugWorkspace(),Is.True);Click("CloseDialog");
            foreach(string kind in new[]{"win","submissions","temporary","no-dice"})
            {
                var state=app.Session.State;var scene=Art07Fixtures.Create(app.Config,Art07FixtureKind.G147,state.nextItemSerial,state.nextDieSerial);
                scene.sourceRunId=state.sourceRunId;var growth=scene.controllers[0].Clone();scene.controllers.Clear();scene.target=kind=="submissions"?"999999999":"1";
                scene.stageIndex=kind=="win"?23:0;scene.remainingSubmissions=1;
                if(kind=="win")
                {
                    growth.definitionId="C36";scene.controllers.Add(growth);scene.capacityOverride=6;
                    foreach(var current in scene.currentResults.Take(2))
                    {
                        scene.dice.Single(d=>d.dieId==current.dieId).faces.Single(f=>f.faceId==current.currentFaceId).permanentPip=3;
                        current.currentPip=3;
                    }
                    var extra=DiceFactory.CreateStandard(app.Config,scene.dice.Max(d=>d.createdSerial)+1);scene.dice.Add(extra);
                    var face=extra.faces[0];scene.currentResults.Add(new DieHandState{dieId=extra.dieId,currentFaceId=face.faceId,currentPip=face.permanentPip,currentColor=face.baseColor,hasRolled=true});
                }
                if(kind=="temporary")foreach(var die in scene.dice)die.lifetime="stage_temporary";
                if(kind=="no-dice")
                {
                    scene.dice.RemoveRange(1,scene.dice.Count-1);scene.dice[0].isGlass=true;scene.currentResults=null;scene.orderedLockedIds.Clear();
                    scene.randomOverrides.Add(new DebugRandomOverride{kind="Glass",sourceId=scene.dice[0].dieId,value="break"});
                }
                var applied=app.Send(new SessionCommand{kind=CommandKind.ApplyDebugScene,debugScene=scene});Assert.That(applied.accepted,Is.True,applied.error);
                if(kind=="no-dice"){Click("Roll");app.SkipVisual();}
                else {Click("Submit");app.SkipVisual();if(app.Session.State.phase=="StageSettlement")Click("SettlementContinue");}
                Assert.That(app.Session.State.phase,Is.EqualTo(kind=="win"?"RunWon":"RunFailed"));
                Assert.That(FindActive<TextMeshProUGUI>("EndTitle").text,Is.EqualTo(kind=="win"?"24 关完成":"本次挑战结束"));
                string message=FindActive<TextMeshProUGUI>("EndMessage").text;
                if(kind=="submissions")Assert.That(message,Does.Contain("提交次数已用尽"));
                if(kind=="temporary")Assert.That(message,Does.Contain("本关已成功，但没有永久骰"));
                if(kind=="no-dice")Assert.That(message,Does.Contain("活动骰已耗尽"));
                Assert.That(FindActive<TextMeshProUGUI>("EndStats").text,Does.Contain("调试对局"));
                Assert.That(FindActive<TextMeshProUGUI>("EndStats").text,Does.Not.Contain("旧存档未记录"));
                if(kind=="win")
                {
                    Click("SkipResultReveal");Assert.That(app.Session.State.dice.Count,Is.EqualTo(6));Assert.That(app.Session.State.lastSubmission.snapshot.dice,Has.Length.EqualTo(5));
                    Assert.That(FindActive<TextMeshProUGUI>("LastDiceTitle").text,Does.Contain("最终骰组"));
                    foreach(var die in app.Session.State.dice)Assert.That(app.table.GetFaceOrientation(die.dieId)?.gameObject.activeInHierarchy,Is.True,die.dieId+" must be visible in the final dice set.");
                    var grown=app.Session.State.processedReceipts.SelectMany(r=>r.effects).Where(e=>e.kind=="FaceValueChanged"&&e.sourceId=="C36"&&e.oldValue==3&&e.newValue==4).ToArray();
                    Assert.That(grown,Has.Length.EqualTo(2));
                    foreach(var effect in grown)
                    {
                        Assert.That(app.Session.State.lastSubmission.snapshot.dice.Single(d=>d.dieId==effect.dieId).permanentFacesCopy.Single(f=>f.faceId==effect.faceId).permanentPip,Is.EqualTo(3));
                        var mesh=app.table.GetFaceOrientation(effect.dieId).Find("Face_"+effect.faceId).GetComponent<MeshFilter>().sharedMesh;
                        Assert.That(mesh.name,Does.StartWith("Pips_4_"),"Final display must use the committed permanent upgrade, not the score snapshot.");
                    }
                }
                else
                {
                    Assert.That(HasActive("FailureReasonTag"),Is.True);Assert.That(FindActive<TextMeshProUGUI>("EndTitle").color,Is.EqualTo(UiKit.Paper));
                }
                string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);
                yield return CaptureEdge07("S18-"+kind);
                Click("EndBreakdown");Click("CloseDialog");
                Assert.That(StateJson(),Is.EqualTo(before));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
            }
        }

        [UnityTest] public IEnumerator PLAY_ART07_DebugPasswordEntryDoesNotChangeOrdinaryRunBeforeExplicitCopy()
        {
            StartRun("70782");Click("StartStage");string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);
            Click("Pause");Click("Settings");Click("SettingsTab2");
            var field=FindActive<TMP_InputField>("DebugPassword");field.text="xd is god";Click("UnlockDebug");
            Assert.That(app.DebugUnlocked,Is.False);Assert.That(FindActive<TextMeshProUGUI>("DebugPasswordError").text,Does.Contain("口令不匹配"));
            yield return null;field.text="XD is god";Click("UnlockDebug");
            Assert.That(app.DebugUnlocked,Is.True);Assert.That(app.IsDebugWorkspace,Is.False);
            Assert.That(Button("CopyCurrentToDebug").interactable,Is.True);Assert.That(Button("BlankDebug").interactable,Is.True);Assert.That(Button("LoadDebugScene").interactable,Is.True);
            Assert.That(FindActive<TextMeshProUGUI>("Version").text,Does.Contain("debug-run.json"));
            yield return CaptureEdge07("S20-debug-entry");
            Assert.That(StateJson(),Is.EqualTo(before));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
            Click("CloseDialog");Assert.That(app.IsDebugWorkspace,Is.False);
        }

        [UnityTest] public IEnumerator PLAY_ART07_FirstRunGuideFollowsRealActionsAndShopWithoutMutatingRules()
        {
            PlayerPrefs.SetInt("art07.guideSeen",0);yield return LoadBootstrap();StartRun("70783");Click("StartStage");
            Assert.That(FindActive<TextMeshProUGUI>("GuideTitle").text,Does.StartWith("1 / 5"));
            yield return CaptureEdge07("S19-guide-roll");
            Click("Roll");app.SkipVisual();Assert.That(FindActive<TextMeshProUGUI>("GuideTitle").text,Does.StartWith("2 / 5"));
            ClickDie("D001");Assert.That(FindActive<TextMeshProUGUI>("GuideTitle").text,Does.StartWith("3 / 5"));
            Click("Confirm");yield return WaitForIdle();Assert.That(FindActive<TextMeshProUGUI>("GuideTitle").text,Does.StartWith("4 / 5"));
            yield return CaptureEdge07("S19-guide-submit");
            string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);Click("DismissGuide");
            Assert.That(HasActive("FirstRunGuide"),Is.False);Assert.That(PlayerPrefs.GetInt("art07.guideSeen"),Is.EqualTo(1));
            Assert.That(StateJson(),Is.EqualTo(before));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
            // The fifth step runs on a genuine shop reached from a normal, explicitly marked test fixture.
            var fixture=NewBoundaryFixture("art07-guide-shop");fixture.isDebug=false;fixture.debugWorkspace=false;
            // A legal upgraded-dice fixture; retain the real stage target/reward, then earn the score.
            foreach(var die in fixture.dice)foreach(var face in die.faces){face.permanentPip=6;face.baseColor="blue";}
            new AtomicSaveStore(app.SaveLocation,app.Config,SaveSlotPolicy.Normal).Save(fixture);
            PlayerPrefs.SetInt("art07.guideSeen",0);yield return LoadBootstrap();Click("ContinueRun");Click("StartStage");Click("Roll");app.SkipVisual();
            foreach(var die in app.Session.State.dice)ClickDie(die.dieId);Click("Confirm");yield return WaitForIdle();Click("Submit");app.SkipVisual();Assert.That(app.Session.CanEndStage,Is.True);Click("EndStage");Click("SettlementContinue");
            Assert.That(app.Session.State.phase,Is.EqualTo("Shop"));Assert.That(FindActive<TextMeshProUGUI>("GuideTitle").text,Does.StartWith("5 / 5"));
            yield return CaptureEdge07("S19-guide-shop");
        }
    }
}
