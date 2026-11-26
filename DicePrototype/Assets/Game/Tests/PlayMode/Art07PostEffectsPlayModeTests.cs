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
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UObj=UnityEngine.Object;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        [UnityTest] public IEnumerator PLAY_TX07_StageEndReceiptsNameExpiredCardAndGrantedCardInTheCountingRecord()
        {
            FxScene07(scene=>
            {
                FxControllers07(scene,"C141","C112","C115");
                scene.controllers[0].state.growth=app.Config.Controller("C141").parameters.expireGrowth-1;
                scene.remainingSubmissions=1;scene.target="1";
            });
            var result=app.Send(new SessionCommand{kind=CommandKind.Submit});
            Assert.That(result.accepted,Is.True,result.error);
            Assert.That(result.receipt.effects.Any(x=>x.kind=="ControllerExpired"&&x.sourceId=="C141"),Is.True);
            var granted=result.receipt.effects.Single(x=>x.kind=="ControllerGranted"&&x.sourceId=="C112");
            string grantedName=app.Config.Controller(granted.detail.Split(':').Last()).displayName;
            Assert.That(result.receipt.effects.Any(x=>x.kind=="RandomUpgrade"),Is.True); // p1.1.1 (W7a): 无心插柳 always lands; "RandomUpgradeMissed" is no longer produced

            app.ShowCommittedPresentationDetails07();yield return null;
            var rows=app.canvas.GetComponentsInChildren<TextMeshProUGUI>(true).Where(row=>row.gameObject.activeInHierarchy&&row.name.StartsWith("Event",StringComparison.Ordinal)).ToArray();
            Assert.That(rows.Any(row=>row.text.Contains("昙花一现：加成耗尽，已消失")),Is.True); // p1.1.1 (W7a): C141 is a bonus card
            Assert.That(rows.Any(row=>row.text.Contains("滥竽充数：获得 "+grantedName)),Is.True);
            Assert.That(rows.Any(row=>row.text.Contains("无心插柳：")&&!row.text.Contains("状态已更新")),Is.True);
            foreach(var upgrade in result.receipt.effects.Where(x=>x.kind=="DieUpgraded"))
                Assert.That(rows.Any(row=>row.text.Contains(app.Config.Upgrade(upgrade.sourceId).displayName+" · 完成")),Is.True);
            foreach(var change in result.receipt.effects.Where(x=>x.kind=="FaceTransformed"))
                Assert.That(rows.Any(row=>row.text.Contains("骰面点数 "+change.oldValue+" → "+change.newValue)),Is.True);
            Assert.That(rows.Any(row=>row.text.Contains("状态已更新")),Is.False);
            Click("CloseDialog");app.SkipVisual();
        }

        private IEnumerator PostDone07()
        {
            double limit=Time.realtimeSinceStartupAsDouble+3;
            while(app.ActivePermanentFaceTransitions07>0&&Time.realtimeSinceStartupAsDouble<limit)yield return null;
            Assert.That(app.ActivePermanentFaceTransitions07,Is.Zero);
        }
        [UnityTest] public IEnumerator PLAY_ART07_POST_NetZeroUsesThinConfirmationWithoutWipeOrMeshSwap()
        {
            var scene=FxScene07(s=>{FxControllers07(s,"C36","C37","C38");s.capacityOverride=6;});
            var submit=app.Send(new SessionCommand{kind=CommandKind.Submit});Assert.That(submit.accepted,Is.True,submit.error);
            var netZero=submit.receipt.effects.Where(e=>e.kind=="FaceValueChanged").ToArray();
            // p1.1.1 (RULES_DELTA 4.6): C36 and C37 reach the first three submitted dice (was two).
            Assert.That(netZero,Has.Length.EqualTo(3),"Every growth target must actually produce committed effects; an empty All() is not evidence.");
            Assert.That(netZero.All(e=>e.sourceId=="C36+C37"&&e.oldValue==e.newValue),Is.True);
            var rows=app.CommittedPresentationDetails07;
            foreach(string kind in new[]{"FaceValueChanged","FaceDyed"})
            {
                var changes=submit.receipt.effects.Where(e=>e.kind==kind).ToArray();Assert.That(changes,Is.Not.Empty,kind);
                foreach(var e in changes)
                    Assert.That(rows.Any(row=>row.Contains(GameApp.FaceName11(e.faceId))&&row.Contains("骰子："+GameApp.DieName11(e.dieId))
                        &&row.Contains(kind=="FaceValueChanged"?"骰面点数":"染")),Is.True,kind+" "+e.faceId);
            }
            string state=GameSession.StateHash(app.Session.State);byte[] save=File.ReadAllBytes(app.SaveLocation);
            yield return WaitFxPhase07(PresentationPhase.PostScore);
            CollectionAssert.AreEquivalent(new[]{scene.dice[0].dieId,scene.dice[1].dieId,scene.dice[2].dieId,scene.dice[4].dieId}.Distinct(),app.UnchangedPermanentTargets07);
            Assert.That(app.ActivePermanentFaceTransitions07,Is.Zero);Assert.That(LiveFx07("fx_face_change"),Is.Empty);
            foreach(var id in app.UnchangedPermanentTargets07)
            {
                Assert.That(app.canvas.GetComponentsInChildren<RectTransform>(true).Any(r=>r.gameObject.activeInHierarchy&&r.name=="PermanentNoChange_"+id),Is.True);
                Assert.That(app.table.GetFaceOrientation(id).Find("Face_F06").GetComponent<MeshFilter>().sharedMesh.name,Does.StartWith("Pips_6_"));
            }
            app.SkipVisual();Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(state));CollectionAssert.AreEqual(save,File.ReadAllBytes(app.SaveLocation));
        }
        [UnityTest] public IEnumerator PLAY_ART07_POST_PermanentPipTransitionChangesActualGpuPixelsAndRestoresOpaqueFace()
        {
            var scene=FxScene07(s=>{FxControllers07(s,"C36");s.capacityOverride=6;foreach(var die in s.dice.Take(2))die.faces.Single(f=>f.faceId=="F06").permanentPip=3;foreach(var result in s.currentResults.Take(2)){result.currentPip=1;result.remoteOverride=true;}});
            var submit=app.Send(new SessionCommand{kind=CommandKind.Submit});Assert.That(submit.accepted,Is.True);
            Assert.That(submit.receipt.effects.Count(e=>e.kind=="FaceValueChanged"&&e.oldValue==3&&e.newValue==4),Is.EqualTo(2));
            yield return WaitFxPhase07(PresentationPhase.PostScore);
            Assert.That(Resources.Load<Shader>("Art07FaceTransition").isSupported,Is.True,"The dedicated transient material must compile for the active graphics API.");
            var face=app.table.GetFaceOrientation(scene.dice[0].dieId).Find("Face_F06");var physical=face.GetComponent<MeshRenderer>();
            Assert.That(physical.enabled,Is.False);Assert.That(app.TransitionBeforeMeshNames07.Any(n=>n.StartsWith("Pips_3_",StringComparison.Ordinal)),Is.True);
            var dieRoot=face.parent.parent.parent;var transforms=dieRoot.GetComponentsInChildren<Transform>(true);var layers=transforms.Select(t=>t.gameObject.layer).ToArray();foreach(var t in transforms)t.gameObject.layer=31;
            var cameraObject=new GameObject("PermanentFaceGpuCapture");var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=.54f;camera.aspect=1;camera.nearClipPlane=.1f;camera.farClipPlane=4;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.cullingMask=1<<31;
            camera.transform.position=face.position+face.forward*2;camera.transform.LookAt(face.position,face.up);
            var target=new RenderTexture(128,128,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);target.Create();var pixels=new Texture2D(128,128,TextureFormat.RGBA32,false,true);var oldActive=RenderTexture.active;
            Color Capture(){RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,128,128),0,0);pixels.Apply(false,false);return pixels.GetPixel(64,64);}
            try
            {
                Color old=Capture();yield return new WaitForSecondsRealtime(.08f);Color middle=Capture();yield return PostDone07();Color final=Capture();
                float Distance(Color a,Color b)=>Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b);
                Assert.That(Distance(old,final),Is.GreaterThan(.1f),"The old central pip must visibly fade to the final four-pip physical face.");
                Assert.That(Distance(old,middle),Is.GreaterThan(.015f));Assert.That(Distance(middle,final),Is.GreaterThan(.015f));
                Assert.That(physical.enabled,Is.True);Assert.That(physical.sharedMaterial,Is.SameAs(app.table.faceMaterial));Assert.That(face.GetComponent<MeshFilter>().sharedMesh.name,Does.StartWith("Pips_4_"));
            }
            finally{RenderTexture.active=oldActive;target.Release();UObj.Destroy(target);UObj.Destroy(pixels);UObj.Destroy(cameraObject);for(int i=0;i<transforms.Length;i++)if(transforms[i]!=null)transforms[i].gameObject.layer=layers[i];app.SkipVisual();}
        }
        [UnityTest] public IEnumerator PLAY_ART07_POST_ExpiredCopyTargetUsesCommittedPermanentDataAndReplayIsBounded()
        {
            var scene=FxScene07(s=>
            {
                FxControllers07(s,"C07","C36","C38");s.remainingSubmissions=1;s.target="1";s.capacityOverride=3;s.submissionOrdinal=0;
                s.orderedLockedIds=s.orderedLockedIds.Take(2).ToList();for(int i=0;i<s.currentResults.Count;i++)s.currentResults[i].isLocked=i<2;
                s.dice[0].faces.Single(f=>f.faceId=="F06").permanentPip=3;s.currentResults[0].currentPip=1;s.currentResults[0].remoteOverride=true;
                // A legal save can keep physical faces in different array orders on source and target.
                var src=s.dice[0].faces;var dst=s.dice[1].faces;
                foreach(var faces in new[]{src,dst})foreach(var f in faces.Where(f=>f.faceId=="F01"||f.faceId=="F02"))f.baseColor="blue";
                src.Single(f=>f.faceId=="F01").permanentPip=2;src.Single(f=>f.faceId=="F02").permanentPip=5;
                dst.Single(f=>f.faceId=="F01").permanentPip=1;dst.Single(f=>f.faceId=="F02").permanentPip=6;
                int one=Array.FindIndex(dst,f=>f.faceId=="F01"),two=Array.FindIndex(dst,f=>f.faceId=="F02");
                (dst[one],dst[two])=(dst[two],dst[one]);
                s.dice[1].lifetime="stage_temporary";s.dice[1].createdStageId=app.Config.stages[0].stageId;
            });
            var submit=app.Send(new SessionCommand{kind=CommandKind.Submit});Assert.That(submit.accepted,Is.True,submit.error);string target=scene.dice[1].dieId;
            Assert.That(app.CommittedPresentationDetails07.Single(line=>line.StartsWith("骰子复制完成")),
                Does.Contain("第 1 面 1 点蓝 → 2 点蓝；第 2 面 6 点蓝 → 5 点蓝"));
            Assert.That(submit.receipt.effects.Any(e=>e.kind=="DieCopied"&&e.dieId==target&&e.otherDieId==scene.dice[0].dieId),Is.True);
            Assert.That(submit.receipt.effects.Any(e=>e.kind=="FaceValueChanged"&&e.dieId==target&&e.oldValue==3&&e.newValue==4),Is.True,"Copy must precede permanent growth.");
            Assert.That(submit.receipt.effects.Any(e=>e.kind=="TemporaryDieExpired"&&e.dieId==target),Is.True);Assert.That(app.Session.State.dice.Any(d=>d.dieId==target),Is.False);
            string state=GameSession.StateHash(app.Session.State);byte[] save=File.ReadAllBytes(app.SaveLocation);
            yield return WaitFxPhase07(PresentationPhase.PostScore);Assert.That(app.ActivePermanentFaceTransitions07,Is.GreaterThan(0));yield return PostDone07();
            Assert.That(app.table.GetFaceOrientation(target).Find("Face_F06").GetComponent<MeshFilter>().sharedMesh.name,Does.StartWith("Pips_4_"));app.SkipVisual();int pairs=app.CreatedPermanentFacePairs07;
            for(int i=0;i<3;i++){Assert.That(app.ReplayLastPresentation07(),Is.True);yield return WaitFxPhase07(PresentationPhase.PostScore);yield return PostDone07();Assert.That(app.table.GetFaceOrientation(target).Find("Face_F06").GetComponent<MeshFilter>().sharedMesh.name,Does.StartWith("Pips_4_"));app.SkipVisual();Assert.That(app.CreatedPermanentFacePairs07,Is.EqualTo(pairs));}
            Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(state));CollectionAssert.AreEqual(save,File.ReadAllBytes(app.SaveLocation));
        }
        [UnityTest] public IEnumerator PLAY_ART07_POST_FastSharedHalfRevealsOnceWithoutWheelRotation()
        {
            Art07Fixtures.Apply(app,Art07FixtureKind.SharedHalf);FinalOptions07("Fast",false);
            // V1.1 (W13, coordinator decision): setup in earlier frames, then a frame that holds only the Submit (beat window,
            // clock and assertions unchanged).
            yield return null;yield return null;
            var receipt=Art07Fixtures.Submit(app,Art07FixtureKind.SharedHalf);
            string state=GameSession.StateHash(app.Session.State);byte[] save=File.ReadAllBytes(app.SaveLocation);yield return WaitFxPhase07(PresentationPhase.ControllerAndEnchant);
            var wheel=LiveFx07("fx_random_reveal").Single();Assert.That(wheel.CommittedRevealFadeOnly,Is.True);Assert.That(app.RevealedOwnerIds07,Has.Length.EqualTo(1));Assert.That(wheel.value.text,Is.EqualTo("×0.5"));
            Quaternion rotation=wheel.icon.rectTransform.localRotation;float previous=wheel.group.alpha;
            for(int i=0;i<2;i++){yield return null;if(wheel.gameObject.activeInHierarchy){Assert.That(Quaternion.Angle(rotation,wheel.icon.rectTransform.localRotation),Is.LessThan(.001f));Assert.That(wheel.group.alpha,Is.GreaterThanOrEqualTo(previous));previous=wheel.group.alpha;}}
            Assert.That(receipt.Events.Count(e=>e.EffectFamily=="random_reveal"),Is.EqualTo(1));app.SkipVisual();Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(state));CollectionAssert.AreEqual(save,File.ReadAllBytes(app.SaveLocation));
        }
        [UnityTest] public IEnumerator PLAY_ART07_POST_QualifiedHasNonblockingTailPauseSkipAndNoSecondRealStamp()
        {
            SettlementField07("textScale07",150);UiKit.FontScale=1.5f;
            FxScene07(s=>s.target="1");Assert.That(app.Send(new SessionCommand{kind=CommandKind.Submit}).accepted,Is.True);yield return WaitFxPhase07(PresentationPhase.TargetState);
            Assert.That(app.QualifiedStampPlaying07,Is.True);Assert.That(app.IsBusy,Is.True);int begins=app.QualifiedStampBegins07;
            Click("Pause");float fullPause=app.QualifiedStampElapsed07;
            yield return app.StartCoroutine(CaptureEdge07("S08-qualified-live-full-150"));
            Assert.That(app.QualifiedStampElapsed07,Is.EqualTo(fullPause).Within(.001f));Click("Resume");
            double limit=Time.realtimeSinceStartupAsDouble+3;while(app.PresentationPlaying07&&Time.realtimeSinceStartupAsDouble<limit)yield return null;
            Assert.That(app.IsBusy,Is.False);Assert.That(app.QualifiedStampPlaying07,Is.True,"The remaining 200ms must not extend the scoring gate.");Assert.That(app.QualifiedStampElapsed07,Is.GreaterThan(.24f).And.LessThan(.48f));
            string state=GameSession.StateHash(app.Session.State);byte[] save=File.ReadAllBytes(app.SaveLocation);
            Click("Pause");float paused=app.QualifiedStampElapsed07;yield return new WaitForSecondsRealtime(.08f);Assert.That(app.QualifiedStampElapsed07,Is.EqualTo(paused).Within(.001f));
            yield return app.StartCoroutine(CaptureEdge07("S08-qualified-live-tail-150"));
            Assert.That(app.QualifiedStampElapsed07,Is.EqualTo(paused).Within(.001f));Click("Resume");
            app.SkipVisual();Assert.That(app.QualifiedStampPlaying07,Is.False);Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(state));CollectionAssert.AreEqual(save,File.ReadAllBytes(app.SaveLocation));
            yield return app.StartCoroutine(CaptureEdge07("S08-qualified-live-badge-150"));
            Assert.That(app.Send(new SessionCommand{kind=CommandKind.Roll}).accepted,Is.True);app.SkipVisual();foreach(var die in app.Session.State.dice)app.Session.TogglePending(die.dieId);
            Assert.That(app.Send(new SessionCommand{kind=CommandKind.ConfirmLocks}).accepted,Is.True);yield return WaitForIdle();Assert.That(app.Send(new SessionCommand{kind=CommandKind.Submit}).accepted,Is.True);
            limit=Time.realtimeSinceStartupAsDouble+5;while(app.PresentationPlaying07&&Time.realtimeSinceStartupAsDouble<limit)yield return null;
            Assert.That(app.QualifiedStampBegins07,Is.EqualTo(begins),"An already-qualified new submission must not stamp again.");
        }
        [UnityTest] public IEnumerator PLAY_ART07_POST_ThirdAndFourthBrokenDiceKeepOriginalOutlineAndOnlyTwelveFragments()
        {
            var scene=FxScene07(s=>{s.orderedLockedIds.Clear();foreach(var result in s.currentResults)result.isLocked=false;foreach(var die in s.dice.Take(4)){die.isGlass=true;s.randomOverrides.Add(new DebugRandomOverride{kind="Glass",sourceId=die.dieId,value="break"});}});
            // p1.1.1 (W7a; RULES_DELTA 7.3): glass breaks after a submission, die by die, never on a roll, so the four forced
            // "break" tickets are drawn by the submission. p1.1.0 broke the four dice on this roll and the test checked the
            // roll's break visual (two shard effects of twelve fragments, outlines for the third and fourth die).
            // p1.1.1 (W7b): that visual now plays on the submission's post-score beat, and the four checks W7a had to drop
            // are back on that path below: the placement of the shards, the outlines of the third and fourth die, the
            // twelve-fragment limit, and that a skip clears them.
            yield return new WaitForSecondsRealtime(.2f);var roll=app.Send(new SessionCommand{kind=CommandKind.Roll});Assert.That(roll.accepted,Is.True,roll.error);Assert.That(roll.receipt.effects.Any(e=>e.kind=="DieBroken"),Is.False,"A roll never breaks glass.");
            Assert.That(app.BrokenOutlineIds07,Is.Empty);Assert.That(LiveFx07("fx_dice_break"),Is.Empty);app.SkipVisual();
            Assert.That(app.Session.State.dice.Count(d=>d.isGlass),Is.EqualTo(4));
            foreach(var die in scene.dice.Take(4))app.Session.TogglePending(die.dieId);
            Assert.That(app.Send(new SessionCommand{kind=CommandKind.ConfirmLocks}).accepted,Is.True);yield return WaitForIdle();
            app.table.FinishLayoutTransitions();yield return null;
            // Where each glass die stands in its slot before the submission: the shards must appear exactly there.
            var glass=scene.dice.Take(4).Select(d=>d.dieId).ToArray();
            var poses=glass.ToDictionary(id=>id,id=>{var t=app.table.GetFaceOrientation(id);return (position:t.position,rotation:t.rotation,scale:t.lossyScale);});
            var submit=app.Send(new SessionCommand{kind=CommandKind.Submit});Assert.That(submit.accepted,Is.True,submit.error);
            CollectionAssert.AreEqual(glass,submit.receipt.effects.Where(e=>e.kind=="DieBroken").Select(e=>e.dieId),"The submitted glass dice are judged in submission order.");
            Assert.That(app.Session.State.dice.Any(d=>d.isGlass),Is.False);Assert.That(app.Session.State.dice.Count,Is.EqualTo(scene.dice.Count-4));
            Assert.That(app.CommittedPresentationDetails07.Count(line=>line.Contains("玻璃骰碎了")),Is.EqualTo(4),"Each break has its line in the record.");
            Assert.That(app.ShatteredDice111,Is.Empty,"Nothing shatters before the post-score beat.");Assert.That(LiveFx07("fx_dice_break"),Is.Empty);
            foreach(string id in glass)Assert.That(app.table.GetFaceOrientation(id).gameObject.activeInHierarchy,Is.True,"The dice stay in their slots while the hand scores.");
            yield return WaitFxPhase07(PresentationPhase.PostScore);
            // The first two dice of the receipt shatter (six fragments each, twelve at most); the third and fourth leave an outline.
            CollectionAssert.AreEquivalent(glass,app.ShatteredDice111);
            CollectionAssert.AreEquivalent(glass.Skip(GameApp.ShatteredDiceLimit111),app.BrokenOutlineIds07);
            var shards=LiveFx07("fx_dice_break");Assert.That(shards,Has.Length.EqualTo(GameApp.ShatteredDiceLimit111));
            Assert.That(shards.SelectMany(v=>v.particles).Count(p=>p.gameObject.activeInHierarchy),Is.EqualTo(GameApp.ShatteredDiceLimit111*GameApp.FragmentsPerDie111));
            Assert.That(GameApp.ShatteredDiceLimit111*GameApp.FragmentsPerDie111,Is.EqualTo(12),"The twelve-fragment limit of the v0.7 break.");
            foreach(string id in glass.Take(GameApp.ShatteredDiceLimit111))
            {
                var pose=poses[id];var view=shards.SingleOrDefault(v=>Vector3.Distance(v.transform.parent.position,pose.position)<.001f);
                Assert.That(view,Is.Not.Null,"A shard effect stands where "+id+" stood.");
                Assert.That(Quaternion.Angle(view.transform.parent.rotation,pose.rotation),Is.LessThan(.01f));Assert.That(Vector3.Distance(view.transform.parent.lossyScale,pose.scale),Is.LessThan(.001f));
                view.Evaluate(.5f);Assert.That(Vector3.Distance(view.transform.parent.position,pose.position),Is.LessThan(.001f),"The motion never moves the placed pose.");
            }
            foreach(var id in app.BrokenOutlineIds07)Assert.That(app.canvas.GetComponentsInChildren<RectTransform>(true).Any(r=>r.gameObject.activeInHierarchy&&r.name=="BrokenOutline_"+id),Is.True);
            foreach(string id in glass)Assert.That(app.table.GetFaceOrientation(id)==null||!app.table.GetFaceOrientation(id).gameObject.activeInHierarchy,Is.True,"A broken die leaves the table on the beat it shatters.");
            // A redraw during the rest of the presentation must not put a broken die back.
            app.Refresh();
            foreach(string id in glass)Assert.That(app.table.GetFaceOrientation(id)==null||!app.table.GetFaceOrientation(id).gameObject.activeInHierarchy,Is.True);
            string state=GameSession.StateHash(app.Session.State);byte[] save=File.ReadAllBytes(app.SaveLocation);app.SkipVisual();Assert.That(app.BrokenOutlineIds07,Is.Empty);Assert.That(LiveFx07("fx_dice_break"),Is.Empty);Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(state));CollectionAssert.AreEqual(save,File.ReadAllBytes(app.SaveLocation));
        }
        [UnityTest] public IEnumerator PLAY_ART07_POST_ShopTextureAndGoldUseRealTwoLayerFadeDuringMaterialWindow()
        {
            foreach(string upgrade in new[]{"U01","U06"})
            {
                yield return TransactionShop07(s=>s.shop.offers.Any(o=>o.contentId==upgrade));yield return null;
                var before=app.Session.State;var offer=before.shop.offers.Single(o=>o.contentId==upgrade);var die=before.dice.First(d=>d.lifetime=="permanent");string face=die.faces.First(f=>app.Session.IsUpgradeTargetLegal(upgrade,die.dieId,f.faceId)).faceId;
                Assert.That(app.Send(new SessionCommand{kind=CommandKind.Buy,offerId=offer.offerId,targetDieId=die.dieId,targetFaceId=face}).accepted,Is.True);
                string state=GameSession.StateHash(app.Session.State);byte[] save=File.ReadAllBytes(app.SaveLocation);yield return TransactionAt07(.14f);
                var physical=app.table.GetFaceOrientation("$die-preview").Find("Face_"+face);Assert.That(physical.GetComponent<MeshRenderer>().enabled,Is.False);Assert.That(app.ActivePermanentFaceTransitions07,Is.EqualTo(1));
                var old=physical.Find("PreviousPermanentFace").GetComponent<MeshRenderer>();var committed=physical.Find("CommittedPermanentFace").GetComponent<MeshRenderer>();var props=new MaterialPropertyBlock();committed.GetPropertyBlock(props);
                Assert.That(old.enabled&&committed.enabled&&old.gameObject.activeInHierarchy,Is.True);Assert.That(props.GetFloat("_Progress"),Is.GreaterThan(0).And.LessThan(1));
                yield return TransactionDone07();Assert.That(app.ActivePermanentFaceTransitions07,Is.Zero);Assert.That(physical.GetComponent<MeshRenderer>().sharedMaterial,Is.SameAs(app.table.faceMaterial));
                Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(state));CollectionAssert.AreEqual(save,File.ReadAllBytes(app.SaveLocation));
            }
        }
        [UnityTest] public IEnumerator PLAY_ART07_POST_FacePoolSurvivesPhysicalTargetDestructionAndDisable()
        {
            var scene=FxScene07(s=>FxControllers07(s,"C36"));
            var source=app.table.GetFaceOrientation(scene.dice[0].dieId).Find("Face_F06");
            string state=GameSession.StateHash(app.Session.State);byte[] save=File.ReadAllBytes(app.SaveLocation);
            var shutdownRoot=new GameObject("FacePoolShutdownFixture");
            var poolHost=new GameObject("IndependentPermanentFacePool");poolHost.transform.SetParent(shutdownRoot.transform,false);
            var pool=poolHost.AddComponent<Art07FaceTransition>();pool.Initialize();
            MeshRenderer Physical(string name)
            {
                var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(shutdownRoot.transform,false);
                go.GetComponent<MeshFilter>().sharedMesh=source.GetComponent<MeshFilter>().sharedMesh;
                var view=go.GetComponent<MeshRenderer>();view.sharedMaterial=app.table.faceMaterial;return view;
            }
            void Add(MeshRenderer view)
            {
                var mesh=view.GetComponent<MeshFilter>().sharedMesh;
                pool.Add(new Art07FaceTransition.FaceImage{Id=view.name,Mesh=mesh,Renderer=view,Color=Color.blue,Visible=true},
                    new Art07FaceTransition.FaceImage{Id=view.name,Mesh=mesh,Renderer=view,Color=Color.red,Visible=true},.32f,0,true,false);
            }
            try
            {
                var first=Physical("RemovedPhysicalFace");Add(first);Assert.That(pool.ActiveFaces,Is.EqualTo(1));
                UObj.Destroy(first.gameObject);yield return null;
                Assert.DoesNotThrow(()=>pool.SetTime(.1f));Assert.DoesNotThrow(()=>pool.Finish());Assert.That(pool.ActiveFaces,Is.Zero);
                var second=Physical("SurvivingPhysicalFace");Add(second);
                var returnedChild=second.transform.Find("PreviousPermanentFace");poolHost.SetActive(false);
                Assert.That(second.enabled,Is.True);Assert.That(pool.ActiveFaces,Is.Zero);
                Assert.That(returnedChild.gameObject.activeSelf,Is.False);Assert.That(returnedChild.parent,Is.SameAs(second.transform),"Disable hides a borrowed pair without reparenting into a deactivating owner.");
                UObj.Destroy(returnedChild.gameObject);yield return null;poolHost.SetActive(true);
                Assert.DoesNotThrow(()=>Add(second));Assert.That(pool.ActiveFaces,Is.EqualTo(1));pool.SetTime(.4f);
                Assert.That(second.enabled,Is.True);Assert.That(pool.ActiveFaces,Is.Zero);Assert.That(pool.CreatedPairs,Is.LessThanOrEqualTo(16));
                Add(second);UObj.Destroy(poolHost);yield return null;
                Assert.That(second.enabled,Is.True);Assert.That(second.transform.childCount,Is.Zero,"Removing the pool must not leak borrowed ghost objects under a surviving face.");
                poolHost=new GameObject("FinalSceneFacePool");poolHost.transform.SetParent(shutdownRoot.transform,false);pool=poolHost.AddComponent<Art07FaceTransition>();pool.Initialize();
                Add(second);UObj.Destroy(shutdownRoot);yield return null;LogAssert.NoUnexpectedReceived();
                Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(state));CollectionAssert.AreEqual(save,File.ReadAllBytes(app.SaveLocation));
            }
            finally{if(shutdownRoot!=null)UObj.Destroy(shutdownRoot);}
        }
    }
}
