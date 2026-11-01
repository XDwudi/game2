using System;
using System.Collections;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using DiceCraft.Presentation;
using DiceCraft.Verification;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        private DebugScene FxScene07(Action<DebugScene> edit)
        {
            Art07Fixtures.Apply(app,Art07FixtureKind.G147);
            var s=app.Session.State;
            var scene=Art07Fixtures.Create(app.Config,Art07FixtureKind.G147,s.nextItemSerial,s.nextDieSerial);
            edit(scene);var result=app.Send(new SessionCommand{kind=CommandKind.ApplyDebugScene,debugScene=scene});
            Assert.That(result.accepted,Is.True,result.error);return scene;
        }
        private static void FxControllers07(DebugScene scene,params string[] definitions)
        {
            int serial=scene.controllers[0].acquiredSerial;
            scene.controllers=definitions.Select((id,i)=>new ControllerInstance{instanceId="ITEM_"+(serial+i).ToString("D5"),acquiredSerial=serial+i,definitionId=id,enchantmentId="none",state=new ControllerDynamicState{activeColor=id=="C45"?"blue":null}}).ToList();
            scene.mimicBindings=null;
        }
        private Art07PrefabView[] LiveFx07(string id)=>app.canvas.GetComponentsInChildren<Art07PrefabView>(true).Concat(app.table.GetComponentsInChildren<Art07PrefabView>(true)).Distinct().Where(v=>v.assetId==id&&v.gameObject.activeInHierarchy).ToArray();
        private IEnumerator WaitFxPhase07(PresentationPhase phase)
        {
            double limit=Time.realtimeSinceStartupAsDouble+6;
            // Read-only diagnostics distinguish an absent phase, a cold-frame time jump,
            // and an external Skip/focus loss. Never change the real clock or its limits.
            var player=(PresentationPlayer)typeof(GameApp).GetField("presentationPlayer07",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(app);
            var trace=new System.Collections.Generic.List<string>();PresentationPhase? previous=null;
            void Sample()
            {
                if(previous!=app.CurrentPresentationPhase07||Time.unscaledDeltaTime>.1f||!app.PresentationPlaying07)
                    trace.Add("frame="+Time.frameCount+" dt="+Time.unscaledDeltaTime.ToString("F4")+" elapsedMs="+player.Clock.ElapsedMilliseconds.ToString("F2")+" phase="+app.CurrentPresentationPhase07+" playing="+app.PresentationPlaying07+" focus="+Application.isFocused+" paused="+player.Clock.Paused);
                previous=app.CurrentPresentationPhase07;
            }
            Sample();while(app.PresentationPlaying07&&app.CurrentPresentationPhase07!=phase&&Time.realtimeSinceStartupAsDouble<limit){yield return null;Sample();}
            string detail="Wanted "+phase+"; speed="+player.Sequence.Options.Speed+"; phases="+string.Join(",",player.Sequence.Beats.Select(b=>b.Phase+"@"+b.StartMs+"+"+b.DurationMs))+"\n"+string.Join("\n",trace);
            TestContext.WriteLine("Actual presentation phase trace: "+detail);
            Assert.That(app.PresentationPlaying07,Is.True,"Presentation must still be playing at "+phase+"\n"+detail);
            Assert.That(app.CurrentPresentationPhase07,Is.EqualTo(phase));
        }
        private IEnumerator ObserveFinalPhysicalFaces07(string[] dieIds,bool replay)
        {
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var player=(PresentationPlayer)typeof(GameApp).GetField("presentationPlayer07",flags).GetValue(app);
            var frozen=(System.Collections.Generic.Dictionary<string,DieInstance>)typeof(GameApp).GetField("permanentVisualAfter07",flags).GetValue(app);
            foreach(string id in dieIds)Assert.That(frozen[id].faces.Single(f=>f.faceId=="F06").permanentPip,Is.EqualTo(4),"The committed display copy must survive ApplyDebugScene and replay.");
            var trace=new System.Collections.Generic.List<string>();double deadline=Time.realtimeSinceStartupAsDouble+2;
            while(Time.realtimeSinceStartupAsDouble<deadline)
            {
                var faces=dieIds.Select(id=>app.table.GetFaceOrientation(id).Find("Face_F06")).ToArray();
                string sample="frame="+Time.frameCount+" elapsedMs="+player.Clock.ElapsedMilliseconds.ToString("F2")+" dt="+Time.unscaledDeltaTime.ToString("F4")+" playing="+app.PresentationPlaying07+" replay="+player.IsReplay+" phase="+app.CurrentPresentationPhase07+" transitions="+app.ActivePermanentFaceTransitions07+" faces="+string.Join(";",faces.Select(f=>f.GetComponent<MeshFilter>().sharedMesh.name+" visible="+(f.gameObject.activeInHierarchy&&f.GetComponent<MeshRenderer>().enabled)));
                trace.Add(sample);
                if(!app.PresentationPlaying07||app.CurrentPresentationPhase07!=PresentationPhase.PostScore)break;
                if(app.ActivePermanentFaceTransitions07==0)
                {
                    // Assert inside the observing coroutine. Its caller can resume a
                    // later frame, after completion has correctly rebuilt current state.
                    TestContext.WriteLine("Observed final physical faces while presentation remains active:\n"+string.Join("\n",trace));
                    Assert.That(player.IsReplay,Is.EqualTo(replay));
                    foreach(var face in faces){Assert.That(face.gameObject.activeInHierarchy&&face.GetComponent<MeshRenderer>().enabled,Is.True);Assert.That(face.GetComponent<MeshFilter>().sharedMesh.name,Does.StartWith("Pips_4_"));}
                    yield break;
                }
                yield return null;
            }
            Assert.Fail("No visible final physical Pips4 frame observed before leaving the frozen PostScore display.\n"+string.Join("\n",trace));
        }
        [UnityTest] public IEnumerator PLAY_ART07_FX_FastSourceMarksLowQualityReplayAndSkipReset()
        {
            bool had=PlayerPrefs.HasKey("art07.quality");int old=PlayerPrefs.GetInt("art07.quality",1);
            try
            {
                Art07Fixtures.Apply(app,Art07FixtureKind.G147);Click("Pause");Click("Settings");Click("SettingsTab1");
                for(int n=0;n<3&&!Button("Quality").GetComponentInChildren<TextMeshProUGUI>().text.Contains("低");n++)Click("Quality");
                Click("SettingsTab2");Click("Speed");Click("SettingsDone");
                Art07Fixtures.Submit(app,Art07FixtureKind.G147);yield return WaitFxPhase07(PresentationPhase.ControllerAndEnchant);
                Assert.That(app.ActiveSourceMarks07,Is.EqualTo(4));Assert.That(app.ActiveCardPunches07,Is.Zero);
                yield return WaitFxPhase07(PresentationPhase.FinalScore);
                Assert.That(app.CurrentPresentationQuality07,Is.EqualTo(PresentationQuality.Low));
                Assert.That(LiveFx07("fx_combo_burst").Single().particles.Count(p=>p.gameObject.activeInHierarchy),Is.Zero);
                app.SkipVisual();string hash=GameSession.StateHash(app.Session.State);byte[] save=File.ReadAllBytes(app.SaveLocation);
                Assert.That(app.ReplayLastPresentation07(),Is.True);yield return WaitFxPhase07(PresentationPhase.FinalScore);
                Assert.That(app.CurrentPresentationQuality07,Is.EqualTo(PresentationQuality.Low));
                Assert.That(LiveFx07("fx_combo_burst").Single().particles.Count(p=>p.gameObject.activeInHierarchy),Is.Zero);
                app.SkipVisual();Assert.That(app.ActiveArtEffects07+app.ActiveCardPunches07+app.ActiveSourceMarks07,Is.Zero);
                Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(hash));CollectionAssert.AreEqual(save,File.ReadAllBytes(app.SaveLocation));
            }
            finally{if(had)PlayerPrefs.SetInt("art07.quality",old);else PlayerPrefs.DeleteKey("art07.quality");}
        }
        [UnityTest] public IEnumerator PLAY_ART07_FX_NormalPunchCompletesAcrossAdjacentBeatsAndSkipResets()
        {
            Art07Fixtures.Apply(app,Art07FixtureKind.G147);Art07Fixtures.Submit(app,Art07FixtureKind.G147);
            double limit=Time.realtimeSinceStartupAsDouble+6;
            while(app.PresentationPlaying07&&app.ActiveCardPunches07<2&&Time.realtimeSinceStartupAsDouble<limit)yield return null;
            Assert.That(app.ActiveCardPunches07,Is.GreaterThanOrEqualTo(2),"A 140ms focus must not cut the previous 220ms punch.");
            app.SkipVisual();Assert.That(app.ActiveCardPunches07,Is.Zero);
            foreach(var rect in app.canvas.GetComponentsInChildren<RectTransform>(true).Where(r=>r.name.StartsWith("Controller_",StringComparison.Ordinal)))Assert.That(rect.localScale,Is.EqualTo(Vector3.one));
        }
        [UnityTest] public IEnumerator PLAY_ART07_FX_MultipleRandomOwnersAndSharedCopyUseCompleteCommittedList()
        {
            var scene=FxScene07(s=>{FxControllers07(s,"C41","C53","C53");s.randomOverrides.Add(new DebugRandomOverride{kind="C53",sourceId=s.controllers[1].instanceId,value="1"});s.randomOverrides.Add(new DebugRandomOverride{kind="C53",sourceId=s.controllers[2].instanceId,value="16"});});
            var result=app.Send(new SessionCommand{kind=CommandKind.Submit});Assert.That(result.accepted,Is.True,result.error);
            double limit=Time.realtimeSinceStartupAsDouble+6;
            while(app.PresentationPlaying07&&app.RevealedOwnerIds07.Length<2&&Time.realtimeSinceStartupAsDouble<limit)yield return null;
            CollectionAssert.AreEquivalent(scene.controllers.Skip(1).Select(c=>c.instanceId),app.RevealedOwnerIds07);
            Assert.That(LiveFx07("fx_random_reveal"),Is.Empty,"Multiple owners use a static list, not one misleading wheel.");
            Assert.That(app.CommittedPresentationDetails07.Count(x=>x.StartsWith("已提交随机结果",StringComparison.Ordinal)),Is.EqualTo(2));
            Assert.That(string.Join("\n",app.CommittedPresentationDetails07),Does.Contain("×0.5").And.Contain("×8"));
            string hash=GameSession.StateHash(app.Session.State);byte[] save=File.ReadAllBytes(app.SaveLocation);
            app.ShowCommittedPresentationDetails07();yield return null;
            var rows=app.canvas.GetComponentsInChildren<TextMeshProUGUI>(true).Where(t=>t.gameObject.activeInHierarchy&&t.name.StartsWith("Event",StringComparison.Ordinal)).ToArray();
            Assert.That(rows.Length,Is.EqualTo(app.CommittedPresentationDetails07.Length));Click("CloseDialog");app.SkipVisual();
            Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(hash));CollectionAssert.AreEqual(save,File.ReadAllBytes(app.SaveLocation));
        }
        [UnityTest] public IEnumerator PLAY_ART07_FX_PostScoreThreeTargetsRiskAndFrozenReplayPhysicalPips()
        {
            var scene=FxScene07(s=>
            {
                FxControllers07(s,"C36","C42","C38","C01","C15");s.capacityOverride=6;
                foreach(int i in new[]{0,2,3,4})s.dice[i].faces.Single(f=>f.faceId=="F06").baseColor="red";
                foreach(var die in s.dice.Take(2))die.faces.Single(f=>f.faceId=="F06").permanentPip=3;
                foreach(var r in s.currentResults.Take(2)){r.currentPip=1;r.remoteOverride=true;}
                s.randomOverrides.Add(new DebugRandomOverride{kind="C42",sourceId=s.controllers[1].instanceId,value="break"});
            });
            var result=app.Send(new SessionCommand{kind=CommandKind.Submit});Assert.That(result.accepted,Is.True,result.error);
            Assert.That(result.receipt.effects.Count(e=>e.kind=="FaceDyed"),Is.GreaterThanOrEqualTo(3));
            Assert.That(result.receipt.effects.Any(e=>e.kind=="ControllerDestroyed"),Is.True);
            string[] changedTargets=scene.dice.Where(old=>{var final=app.Session.State.dice.Single(d=>d.dieId==old.dieId);return old.faces.Where((f,i)=>f.permanentPip!=final.faces[i].permanentPip||f.baseColor!=final.faces[i].baseColor).Any();}).Select(d=>d.dieId).ToArray();
            CollectionAssert.AreEquivalent(new[]{scene.dice[0].dieId,scene.dice[1].dieId,scene.dice[4].dieId},changedTargets,"The scene needs three real net-changed targets, not three no-op dye receipts.");
            yield return WaitFxPhase07(PresentationPhase.PostScore);
            Assert.That(LiveFx07("fx_card_destroy"),Has.Length.EqualTo(1),"Permanent target groups must leave room for risk.");
            Assert.That(LiveFx07("fx_face_change"),Has.Length.EqualTo(3));
            string frozen=app.FrozenPresentationAfterHash07;
            Assert.That(app.ActivePermanentFaceTransitions07,Is.GreaterThan(0));
            Assert.That(app.TransitionBeforeMeshNames07.Any(n=>n.StartsWith("Pips_3_",StringComparison.Ordinal)),Is.True);
            yield return ObserveFinalPhysicalFaces07(scene.dice.Take(2).Select(d=>d.dieId).ToArray(),false);
            app.SkipVisual();var changed=DebugScene.FromState(app.Session.State);
            foreach(var die in changed.dice.Take(2))die.faces.Single(f=>f.faceId=="F06").permanentPip=1;
            Assert.That(app.Send(new SessionCommand{kind=CommandKind.ApplyDebugScene,debugScene=changed}).accepted,Is.True);
            string hash=GameSession.StateHash(app.Session.State);byte[] save=File.ReadAllBytes(app.SaveLocation);
            Assert.That(app.ReplayLastPresentation07(),Is.True);yield return WaitFxPhase07(PresentationPhase.PostScore);
            Assert.That(app.FrozenPresentationAfterHash07,Is.EqualTo(frozen));
            Assert.That(app.TransitionBeforeMeshNames07.Any(n=>n.StartsWith("Pips_3_",StringComparison.Ordinal)),Is.True,"Replay must use frozen original physical faces.");
            yield return ObserveFinalPhysicalFaces07(scene.dice.Take(2).Select(d=>d.dieId).ToArray(),true);
            app.SkipVisual();Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(hash));CollectionAssert.AreEqual(save,File.ReadAllBytes(app.SaveLocation));
            foreach(var die in scene.dice.Take(2))Assert.That(app.table.GetFaceOrientation(die.dieId).Find("Face_F06").GetComponent<MeshFilter>().sharedMesh.name,Does.StartWith("Pips_1_"),"After replay, the live view must restore the newer committed debug state.");
        }
        [UnityTest] public IEnumerator PLAY_ART07_FX_RealRollAndRemoteBreakUsePlacedWorldPoolAndSkipResets()
        {
            foreach(bool remote in new[]{false,true})
            {
                var scene=FxScene07(s=>{s.orderedLockedIds.Clear();foreach(var r in s.currentResults)r.isLocked=false;s.dice[0].isGlass=true;s.randomOverrides.Add(new DebugRandomOverride{kind="Glass",sourceId=s.dice[0].dieId,value="break"});if(remote)s.inventory.Add(new OwnedItem{instanceId="ITEM_09999",contentId="I03"});});
                yield return new WaitForSecondsRealtime(.22f);
                var pose=app.table.GetFaceOrientation(scene.dice[0].dieId);Vector3 position=pose.position,scale=pose.lossyScale;Quaternion rotation=pose.rotation;
                var command=remote?new SessionCommand{kind=CommandKind.UseItem,itemInstanceId="ITEM_09999",dieIds=scene.dice.Skip(1).Take(2).Select(d=>d.dieId).ToArray(),targetPips=new[]{2,5}}:new SessionCommand{kind=CommandKind.Roll};
                var result=app.Send(command);Assert.That(result.accepted,Is.True,result.error);Assert.That(result.receipt.effects.Any(e=>e.kind=="DieBroken"),Is.True);
                var view=LiveFx07("fx_dice_break").Single();Assert.That(Vector3.Distance(view.transform.parent.position,position),Is.LessThan(.001f));
                Assert.That(Quaternion.Angle(view.transform.parent.rotation,rotation),Is.LessThan(.01f));Assert.That(Vector3.Distance(view.transform.parent.lossyScale,scale),Is.LessThan(.001f));
                string hash=GameSession.StateHash(app.Session.State);byte[] save=File.ReadAllBytes(app.SaveLocation);view.Evaluate(.5f);
                Assert.That(Vector3.Distance(view.transform.parent.position,position),Is.LessThan(.001f));Assert.That(Quaternion.Angle(view.transform.parent.rotation,rotation),Is.LessThan(.01f));
                app.SkipVisual();Assert.That(view.gameObject.activeInHierarchy,Is.False);Assert.That(app.ActiveArtEffects07,Is.Zero);
                Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(hash));CollectionAssert.AreEqual(save,File.ReadAllBytes(app.SaveLocation));yield return null;
            }
        }
        [UnityTest] public IEnumerator PLAY_ART07_FX_ExtraSubmissionAndExtraRollReportDifferentActualBudgets()
        {
            FxScene07(s=>{s.inventory.Add(new OwnedItem{instanceId="ITEM_09998",contentId="I06"});s.inventory.Add(new OwnedItem{instanceId="ITEM_09999",contentId="I07"});});
            var before=app.Session.State;
            Assert.That(app.Send(new SessionCommand{kind=CommandKind.UseItem,itemInstanceId="ITEM_09998"}).accepted,Is.True);Assert.That(app.LastTransactionSummary07,Is.EqualTo("提交 +1"));
            Assert.That(app.Session.State.stage.remainingSubmissions-before.stage.remainingSubmissions,Is.EqualTo(1));app.SkipVisual();
            Assert.That(app.Send(new SessionCommand{kind=CommandKind.UseItem,itemInstanceId="ITEM_09999"}).accepted,Is.True);Assert.That(app.LastTransactionSummary07,Is.EqualTo("剩掷 +1"));
            Assert.That(app.Session.State.hand.rollsRemaining-before.hand.rollsRemaining,Is.EqualTo(1));app.SkipVisual();yield return null;
        }
        [UnityTest] public IEnumerator PLAY_ART07_FX_CopyRouteUsesFiveSegmentsAcrossRowsAndNoInventedHiddenAnchor()
        {
            foreach(bool hiddenOwner in new[]{false,true})
            {
                FxScene07(s=>
                {
                    var definitions=Enumerable.Repeat("C46",hiddenOwner?11:10).ToArray();int mimic=hiddenOwner?9:4;
                    definitions[mimic]="C41";definitions[mimic+1]="C53";FxControllers07(s,definitions);
                    s.randomOverrides.Add(new DebugRandomOverride{kind="C53",sourceId=s.controllers[mimic+1].instanceId,value="1"});
                });
                Assert.That(app.Send(new SessionCommand{kind=CommandKind.Submit}).accepted,Is.True);
                double deadline=Time.realtimeSinceStartupAsDouble+6;
                while(app.PresentationPlaying07&&app.RevealedOwnerIds07.Length==0&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                Assert.That(app.RevealedOwnerIds07,Has.Length.EqualTo(1));
                if(hiddenOwner){Assert.That(app.InvisibleSourceLinks07,Is.GreaterThan(0));Assert.That(LiveFx07("fx_copy_link"),Is.Empty);}
                else
                {
                    var link=LiveFx07("fx_copy_link").Single();Assert.That(link.lineSegments.Count(s=>s.gameObject.activeInHierarchy),Is.EqualTo(5));
                    Assert.That(link.lineSegments[2].rect.width,Is.GreaterThan(0));Assert.That(app.InvisibleSourceLinks07,Is.Zero);
                }
                app.SkipVisual();yield return null;
            }
        }
    }
}
