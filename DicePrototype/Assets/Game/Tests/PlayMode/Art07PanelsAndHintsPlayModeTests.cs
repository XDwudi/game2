using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
        private static readonly string[] BoundaryHintKeys07={"art07.hint.zero_rolls","art07.hint.c53_preview","art07.hint.qualified_continue"};
        // The shared fixture already isolates first-run guidance. Preserve the new profile-only keys too,
        // so unrelated gameplay tests do not mark the user's first encounters as read.
        [SetUp] public void PreserveBoundaryHintPreferences07()
        {
            foreach(string key in BoundaryHintKeys07){intPreferences[key]=PlayerPrefs.HasKey(key)?(int?)PlayerPrefs.GetInt(key):null;PlayerPrefs.SetInt(key,1);}
        }

        [UnityTest] public IEnumerator PLAY_ART07_ModalRedrawDoesNotRestartFadeAndCloseImmediatelyReleasesInputWithNoninteractiveTail()
        {
            StartRun("70771");Click("StartStage");string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);
            Click("Pause");var panel=FindActive<RectTransform>("Dialog");var motion=panel.GetComponent<Art07PanelMotion>();
            Assert.That(motion,Is.Not.Null);Assert.That(motion.Duration,Is.EqualTo(.16f).Within(.0001f));
            Vector2 position=panel.anchoredPosition;yield return new WaitForSecondsRealtime(.22f);
            Assert.That(motion.Alpha,Is.EqualTo(1).Within(.001f));Assert.That(panel.anchoredPosition,Is.EqualTo(position));
            Click("Settings");motion=FindActive<RectTransform>("Dialog").GetComponent<Art07PanelMotion>();
            int sequence=motion.EnterSequence;yield return null;float alpha=motion.Alpha,elapsed=motion.Elapsed;
            Click("SettingsTab1");Assert.That(motion.EnterSequence,Is.EqualTo(sequence));
            Assert.That(motion.Elapsed,Is.GreaterThanOrEqualTo(elapsed));Assert.That(motion.Alpha,Is.GreaterThanOrEqualTo(alpha));
            yield return new WaitForSecondsRealtime(.22f);Click("SettingsDone");
            Assert.That(HasActive("Dialog"),Is.False,"Logical close cannot wait for its exit animation.");
            Assert.That(Button("Roll").interactable,Is.True);
            var tail=app.canvas.GetComponentsInChildren<Art07PanelMotion>(true).Single(m=>m.IsExitTail);
            Assert.That(tail.CapturedGraphicCount,Is.GreaterThan(0),"A close tail must contain the rendered panel, not an empty timer.");
            Assert.That(tail.GetComponentsInChildren<Graphic>(true),Is.Empty,"Tail geometry must not retain targetable UI components.");
            Assert.That(tail.GetComponentsInChildren<Selectable>(true),Is.Empty);
            Assert.That(tail.GetComponent<CanvasGroup>().blocksRaycasts,Is.False);
            var textRenderers=tail.GetComponentsInChildren<CanvasRenderer>(false).Where(r=>r.GetMaterial()!=null&&r.GetMaterial().shader.name.Contains("TextMeshPro")).ToArray();
            Assert.That(textRenderers,Is.Not.Empty,"The exit visual retains actual dynamic text, not just a panel outline.");
            foreach(var renderer in textRenderers)
            {
                var uv=new List<Vector4>();renderer.GetMesh().GetUVs(0,uv);
                Assert.That(uv.Any(v=>Mathf.Abs(v.w)>.0001f),Is.True,"TMP SDF weight in UV0.w must survive the exit mesh copy.");
            }
            Assert.That(StateJson(),Is.EqualTo(before));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
            yield return new WaitForSecondsRealtime(.18f);Assert.That(tail.gameObject.activeSelf,Is.False);
        }

        [UnityTest] public IEnumerator PLAY_ART07_DiceSheetFadeKeepsApertureAndPreviewStationaryThenRestoresBoardSynchronously()
        {
            StartRun("70772");Click("StartStage");Click("Roll");app.SkipVisual();ClickDie("D001");Click("Confirm");yield return WaitForIdle();
            string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);ClickDie("D001");
            var panel=FindActive<RectTransform>("Dialog");var motion=panel.GetComponent<Art07PanelMotion>();
            Assert.That(motion.Duration,Is.EqualTo(.16f).Within(.0001f));Assert.That(motion.ExitDuration,Is.EqualTo(.10f).Within(.0001f));
            var veil=FindActive<CanvasGroup>("PreviewReveal07");Assert.That(veil.alpha,Is.EqualTo(1));Assert.That(veil.blocksRaycasts,Is.False);
            var preview=app.table.GetFaceOrientation("$die-preview");Assert.That(preview,Is.Not.Null);
            Vector3 previewPosition=preview.position;Quaternion previewPose=preview.rotation;
            double end=Time.realtimeSinceStartupAsDouble+.24;
            while(Time.realtimeSinceStartupAsDouble<end)
            {
                Assert.That(panel.anchoredPosition,Is.EqualTo(new Vector2(208,-96)));
                Assert.That(panel.sizeDelta,Is.EqualTo(new Vector2(1504,888)));
                Assert.That(Vector3.Distance(preview.position,previewPosition),Is.LessThan(.001f));
                Assert.That(Quaternion.Angle(preview.rotation,previewPose),Is.LessThan(.001f));
                if(motion.Elapsed<.06f)Assert.That(veil.alpha,Is.EqualTo(1).Within(.001f),"The preview reveal waits for the specified first 60ms.");
                yield return null;
            }
            Assert.That(veil.alpha,Is.EqualTo(0).Within(.001f));
            int enter=motion.EnterSequence;Click("Face_F03");Assert.That(motion.EnterSequence,Is.EqualTo(enter),"Selecting another permanent face is a redraw, not another entrance.");
            Assert.That(veil.alpha,Is.EqualTo(0).Within(.001f));
            Click("CloseDialog");Assert.That(HasActive("Dialog"),Is.False);Assert.That(HasActive("Die_D001"),Is.True);
            Assert.That(StateJson(),Is.EqualTo(before));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
        }

        [UnityTest] public IEnumerator PLAY_ART07_LoadIssueExplanationHasS22TimingAndDoesNotRewriteOrMigrateTheCheckpoint()
        {
            StartRun("70773");string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);
            var show=typeof(GameApp).GetMethod("ShowLoadIssue07",BindingFlags.Instance|BindingFlags.NonPublic);
            Assert.That(show,Is.Not.Null);
            foreach(bool recovered in new[]{false,true})
            {
                show.Invoke(app,new object[]{"测试读取说明：原始文件与备份保持。",recovered});
                var motion=FindActive<RectTransform>("Dialog").GetComponent<Art07PanelMotion>();
                Assert.That(motion.Duration,Is.EqualTo(.12f).Within(.0001f));Assert.That(motion.ExitDuration,Is.EqualTo(.10f).Within(.0001f));
                Assert.That(FindActive<TextMeshProUGUI>("RecoveryDetailBody").text,Does.Contain("原始文件与备份保持"));
                Click("CloseLoadIssue07");Assert.That(HasActive("Dialog"),Is.False);Assert.That(HasActive("NewRun"),Is.True);
                Assert.That(StateJson(),Is.EqualTo(before));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
                yield return null;
            }
        }

        [UnityTest] public IEnumerator PLAY_ART07_FirstBoundaryHintsOnlyChangeReadPreferencesAndNeverCoverActionsOrReplayAfterDismissal()
        {
            for(int kind=0;kind<3;kind++)
            {
                foreach(string key in BoundaryHintKeys07)PlayerPrefs.SetInt(key,1);
                PlayerPrefs.DeleteKey(BoundaryHintKeys07[kind]);
                var fixture=NewBoundaryFixture("normal-hint-"+kind);fixture.isDebug=false;fixture.debugWorkspace=false;
                if(kind==1)fixture.controllers.Add(BoundaryController(fixture,"C53"));
                var session=new GameSession(app.Config,new MemorySaveStore(),fixture);
                var started=session.Execute(new SessionCommand{kind=CommandKind.StartStage,commandId="hint-start-"+kind,expectedActionSequence=session.ActionSequence});Assert.That(started.accepted,Is.True,started.error);
                if(kind==0)for(int i=0;i<3;i++){var roll=session.Execute(new SessionCommand{kind=CommandKind.Roll,commandId="hint-roll-"+i,expectedActionSequence=session.ActionSequence});Assert.That(roll.accepted,Is.True,roll.error);}
                fixture=session.State;
                if(kind==2){fixture.stage.score=fixture.stage.target;fixture.stage.qualified=true;}
                new AtomicSaveStore(Path.Combine(saveDirectory,"active-run.json"),app.Config,SaveSlotPolicy.Normal).Save(fixture);
                yield return LoadBootstrap();Click("ContinueRun");
                string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);
                var reason=FindActive<TextMeshProUGUI>("ActionReason");
                Assert.That(reason.text,Does.Contain(kind==0?"仍可选骰":kind==1?"提交后揭示":"可以继续提交"));
                Assert.That(PlayerPrefs.GetInt(BoundaryHintKeys07[kind],0),Is.Zero,"Read status is persisted only when dismissed.");
                var dismiss=Button("DismissBoundaryHint07");Assert.That(((RectTransform)dismiss.transform).sizeDelta,Is.EqualTo(new Vector2(60,60)));
                var closeBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(app.canvas.transform,dismiss.transform);
                foreach(string id in new[]{"Roll","Confirm","Clear","Submit","EndStage"})
                {
                    var actionBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(app.canvas.transform,Button(id).transform);
                    bool overlap=closeBounds.min.x<actionBounds.max.x&&closeBounds.max.x>actionBounds.min.x&&closeBounds.min.y<actionBounds.max.y&&closeBounds.max.y>actionBounds.min.y;
                    Assert.That(overlap,Is.False,"Hint close overlaps "+id);
                }
                app.Refresh();Click("DismissBoundaryHint07");
                Assert.That(PlayerPrefs.GetInt(BoundaryHintKeys07[kind],0),Is.EqualTo(1));Assert.That(HasActive("DismissBoundaryHint07"),Is.False);
                Assert.That(StateJson(),Is.EqualTo(before));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
                yield return LoadBootstrap();Click("ContinueRun");Assert.That(HasActive("DismissBoundaryHint07"),Is.False,"A dismissed first encounter must stay read after reloading.");
            }
        }
    }
}
