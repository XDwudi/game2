using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        private IEnumerator StageMotionPointer07(string name)
        {
            yield return app.StartCoroutine(StageMotionRendered07());
            Canvas.ForceUpdateCanvases();var button=Button(name);var corners=new Vector3[4];((RectTransform)button.transform).GetWorldCorners(corners);
            var point=RectTransformUtility.WorldToScreenPoint(null,(corners[0]+corners[2])*.5f);
            var data=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
            Assert.That(button.IsInteractable(),Is.True,name);Assert.That(hits.Count,Is.GreaterThan(0),name);
            Assert.That(hits[0].gameObject==button.gameObject||hits[0].gameObject.transform.IsChildOf(button.transform),Is.True,name+" actual hotzone covered");
            ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);
        }
        private IEnumerator StageMotionRendered07(){yield return new WaitForEndOfFrame();}
        private void LogStageRollRaycast07(string label)
        {
            var b=Button("Roll");var r=(RectTransform)b.transform;var corners=new Vector3[4];r.GetWorldCorners(corners);
            var point=RectTransformUtility.WorldToScreenPoint(null,(corners[0]+corners[2])*.5f);var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
            string value=label+" frame="+Time.frameCount+" realtime="+Time.realtimeSinceStartupAsDouble.ToString("F6")+" screen="+Screen.width+"x"+Screen.height+" canvas="+app.canvas.renderMode+" scale="+app.canvas.scaleFactor+" roll="+r.rect+" world="+string.Join(";",corners.Select(v=>v.ToString("F3")))+" point="+point+" exit="+app.StageCardExitElapsed07+"/"+app.StageCardExitDuration07+" hits="+string.Join(";",hits.Select(h=>h.gameObject.name))+"\n";
            value+="graphics="+string.Join(";",b.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).Select(g=>g.name+":enabled="+g.enabled+",active="+g.gameObject.activeInHierarchy+",depth="+g.depth+",cull="+g.canvasRenderer.cull+",raycast="+g.raycastTarget+",alpha="+g.canvasRenderer.GetInheritedAlpha()))+"\n";
            value+="groups="+string.Join(";",b.GetComponentsInParent<CanvasGroup>(true).Select(g=>g.name+":enabled="+g.enabled+",alpha="+g.alpha+",blocks="+g.blocksRaycasts+",interactable="+g.interactable+",ignoreParents="+g.ignoreParentGroups))+"\n";
            File.AppendAllText(Path.Combine(saveDirectory,"stage-roll-raycast.txt"),value);TestContext.WriteLine(value);
        }
        private IEnumerator StageMotionPng07(string name)
        {
            yield return new WaitForEndOfFrame();var texture=ScreenCapture.CaptureScreenshotAsTexture();
            try{File.WriteAllBytes(Path.Combine(saveDirectory,name+".png"),texture.EncodeToPNG());}finally{UnityEngine.Object.Destroy(texture);}
        }

        [UnityTest] public IEnumerator PLAY_ART07_StageCardEntranceDoesNotReplayOnChoiceAndExitPreservesLiveInput()
        {
            foreach(bool reduce in new[]{false,true})
            {
                if(app.Session.State!=null){Click("Pause");Click("MainMenu");}
                if(reduce){Click("MenuSettings");Click("SettingsTab2");Click("Reduced");Click("SettingsDone");}
                StartRun(reduce?"70732":"70731");
                var motion=app.canvas.GetComponentInChildren<Art07StageCardsMotion>(true);Assert.That(motion,Is.Not.Null);
                int enters=motion.EnterSequence;string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);long seq=app.Session.ActionSequence;
                Assert.That(motion.IsPlaying,Is.True);Assert.That(motion.Duration,Is.EqualTo(reduce?.08f:.16f).Within(.0001));
                Assert.That(motion.Offset,Is.EqualTo(reduce?0:20).Within(.01));
                var controllers=app.canvas.GetComponentsInChildren<RectTransform>(false).Where(r=>r.name.StartsWith("Controller_",StringComparison.Ordinal)).ToDictionary(r=>r.name,r=>r.position);
                double deadline=Time.realtimeSinceStartupAsDouble+2;
                while(motion.IsPlaying&&Time.realtimeSinceStartupAsDouble<deadline)
                {
                    Assert.That(FindActive<RectTransform>("NormalCard").anchoredPosition.y,Is.EqualTo(-432-motion.Offset).Within(.01));
                    AssertInteractionCheckpoint07(before,disk,seq);
                    foreach(var c in controllers)Assert.That(FindActive<RectTransform>(c.Key).position,Is.EqualTo(c.Value));
                    yield return null;
                }
                Assert.That(motion.IsPlaying,Is.False);Assert.That(motion.Elapsed,Is.EqualTo(motion.Duration).Within(.0001));Assert.That(motion.Offset,Is.Zero);
                yield return StageMotionPointer07("Normal");Assert.That(motion.EnterSequence,Is.EqualTo(enters));Assert.That(motion.IsPlaying,Is.False);AssertInteractionCheckpoint07(before,disk,seq);
                yield return StageMotionPng07("stage-"+(reduce?"reduced":"normal")+"-settled");
                yield return StageMotionPointer07("StartStage");Assert.That(app.Session.State.phase,Is.EqualTo("HandReady"));Assert.That(app.IsBusy,Is.False);
                Assert.That(app.StageCardExitPlaying07,Is.True);Assert.That(app.StageCardExitDuration07,Is.EqualTo(reduce?.12f:.22f).Within(.0001));Assert.That(app.StageCardExitGraphicCount07,Is.GreaterThan(0));
                CollectionAssert.AreEqual(new[]{CommandKind.StartStage},app.Session.State.processedReceipts.Where(r=>r.actionSequence>seq).Select(r=>r.command.kind));
                var tail=FindActive<RectTransform>("StageCardExit07");Assert.That(tail.GetComponentsInChildren<UnityEngine.UI.Selectable>(true),Is.Empty);
                Assert.That(tail.GetComponent<CanvasGroup>().blocksRaycasts,Is.False);Assert.That(Button("Roll").IsInteractable(),Is.True);
                LogStageRollRaycast07("before-first-render");
                yield return app.StartCoroutine(StageMotionPng07("stage-"+(reduce?"reduced":"normal")+"-first-render-before-raycast"));
                Canvas.ForceUpdateCanvases();LogStageRollRaycast07("after-first-render");
                Assert.That(app.StageCardExitPlaying07,Is.True,"The first real rendered frame must remain inside the old-card exit window.");
                Assert.That(app.StageCardExitElapsed07,Is.LessThan(app.StageCardExitDuration07));
                var corners=new Vector3[4];((RectTransform)Button("Roll").transform).GetWorldCorners(corners);var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,(corners[0]+corners[2])*.5f)},hits);
                Assert.That(hits.Count,Is.GreaterThan(0),"Live Roll center must raycast after the newly built canvas is updated; an empty hit list is a failure, not a skipped check.");
                Assert.That(hits[0].gameObject==Button("Roll").gameObject||hits[0].gameObject.transform.IsChildOf(Button("Roll").transform),Is.True,"renderer tail must not intercept live roll input");
                string committed=StateJson();byte[] saved=File.ReadAllBytes(app.SaveLocation);long committedSeq=app.Session.ActionSequence;
                yield return StageMotionPng07("stage-"+(reduce?"reduced":"normal")+"-tail");
                deadline=Time.realtimeSinceStartupAsDouble+2;
                while(app.StageCardExitPlaying07&&Time.realtimeSinceStartupAsDouble<deadline){AssertInteractionCheckpoint07(committed,saved,committedSeq);yield return null;}
                Assert.That(app.StageCardExitPlaying07,Is.False);Assert.That(app.StageCardExitGraphicCount07,Is.Zero);AssertInteractionCheckpoint07(committed,saved,committedSeq);
                yield return StageMotionPng07("stage-"+(reduce?"reduced":"normal")+"-hand-ready");
            }
        }

        [UnityTest] public IEnumerator PLAY_ART07_StageCardExitWaitsForRealSaveRetryAndDoesNotCommitAgain()
        {
            // Explicit debug checkpoint with a real C41 right neighbour. This prepares a
            // legal preview, not evidence of normal progression or a bypass of the UI action.
            var shop=ShopBoundaryFixture();shop.controllers.Add(BoundaryController(shop,"C41"));shop.controllers.Add(BoundaryController(shop,"C46"));
            var fixtureSession=new GameSession(app.Config,new MemorySaveStore(),shop);CoreShopStep(fixtureSession,CommandKind.LeaveShop);
            yield return LoadBoundaryFixture(fixtureSession.State);ResumeBoundaryFixture();yield return new WaitForSecondsRealtime(.25f);
            string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);long seq=app.Session.ActionSequence;
            var controllers=app.canvas.GetComponentsInChildren<RectTransform>(false).Where(r=>r.name.StartsWith("Controller_",StringComparison.Ordinal)).ToDictionary(r=>r.name,r=>r.position);
            Assert.That(controllers.Count,Is.EqualTo(2));var entrance=app.canvas.GetComponentInChildren<Art07StageCardsMotion>(true);int enters=entrance.EnterSequence;
            foreach(string choice in new[]{"Risk","Normal"})
            {
                yield return StageMotionPointer07(choice);Assert.That(entrance.EnterSequence,Is.EqualTo(enters));Assert.That(entrance.IsPlaying,Is.False);
                AssertInteractionCheckpoint07(before,disk,seq);foreach(var c in controllers)Assert.That(FindActive<RectTransform>(c.Key).position,Is.EqualTo(c.Value));
            }
            using(var locked=new FileStream(app.SaveLocation,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                yield return StageMotionPointer07("StartStage");Assert.That(app.Session.HasPendingSave,Is.True,"Requires real AtomicSaveStore replacement failure.");
                AssertInteractionCheckpoint07(before,disk,seq);Assert.That(app.StageCardExitPlaying07,Is.False);Assert.That(app.StageCardExitGraphicCount07,Is.Zero);
            }
            yield return new WaitForSecondsRealtime(.2f);yield return StageMotionPointer07("RetrySave");
            Assert.That(app.Session.HasPendingSave,Is.False);Assert.That(app.Session.State.phase,Is.EqualTo("HandReady"));Assert.That(app.Session.ActionSequence,Is.EqualTo(seq+1));
            Assert.That(app.StageCardExitPlaying07,Is.True);Assert.That(app.StageCardExitGraphicCount07,Is.GreaterThan(0));Assert.That(app.IsBusy,Is.False);
            foreach(var c in controllers)Assert.That(FindActive<RectTransform>(c.Key).position,Is.EqualTo(c.Value));
            string committed=StateJson();byte[] saved=File.ReadAllBytes(app.SaveLocation);long afterSeq=app.Session.ActionSequence;
            double deadline=Time.realtimeSinceStartupAsDouble+2;while(app.StageCardExitPlaying07&&Time.realtimeSinceStartupAsDouble<deadline){yield return null;AssertInteractionCheckpoint07(committed,saved,afterSeq);}
            Assert.That(app.StageCardExitPlaying07,Is.False);Assert.That(app.StageCardExitGraphicCount07,Is.Zero);
            CollectionAssert.AreEqual(new[]{CommandKind.StartStage},app.Session.State.processedReceipts.Where(r=>r.actionSequence>seq).Select(r=>r.command.kind));
        }
    }
}
