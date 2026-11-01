using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using DiceCraft.Core;
using DiceCraft.Verification;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        private void DiceInteractionTick07(float seconds)=>typeof(GameApp).GetMethod("TickDiceInteraction07",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(app,new object[]{seconds});
        private void DiceInteractionField07(string name,object value)=>typeof(GameApp).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(app,value);
        private Vector3 DiceInteractionPosition07(string id)=>app.table.GetFaceOrientation(id).parent.parent.position;
        private void DiceInteractionInvariant07(string state,byte[] bytes)
        {Assert.That(StateJson(),Is.EqualTo(state));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(app.SaveLocation));}

        private static void DiceInteractionColor07(Color actual,Color expected,string context)
        {
            string precise=$"{context}: actual({actual.r:R},{actual.g:R},{actual.b:R},{actual.a:R}) expected({expected.r:R},{expected.g:R},{expected.b:R},{expected.a:R})";
            Debug.Log(precise);
            // MPB's native color conversion can round one float ULP. This is < 1/3900 of an 8-bit channel.
            for(int channel=0;channel<4;channel++)Assert.That(actual[channel],Is.EqualTo(expected[channel]).Within(.000001f),precise+" channel "+channel);
        }
        private void DiceInteractionBothClocks07(float seconds)
        {app.table.TickLayoutTransitions07(seconds);DiceInteractionTick07(seconds);}

        [UnityTest] public IEnumerator PLAY_ART07_DICEINTERACT_HoverAnd120Up80DownUseActualContourAndSixPixelLift()
        {
            foreach(int font in new[]{100,125,150})
            {
                yield return DiceLabelBegin07(font);DiceLabelApply07(10,true);yield return new WaitForSecondsRealtime(.25f);
                app.enabled=false;app.table.enabled=false;
                try
                {
                    string id=app.Session.State.dice[0].dieId,state=StateJson();byte[] bytes=File.ReadAllBytes(app.SaveLocation);
                    var pointer=FindActive<DiePointer>("Die_"+id);var data=new PointerEventData(EventSystem.current);
                    var contour=app.table.GetFaceOrientation(id).Find("InkContour").GetComponent<MeshRenderer>();var props=new MaterialPropertyBlock();
                    ExecuteEvents.Execute(pointer.gameObject,data,ExecuteEvents.pointerEnterHandler);contour.GetPropertyBlock(props);
                    DiceInteractionColor07(props.GetColor("_BaseColor"),UiKit.Paper,"hover font"+font);
                    yield return CaptureEdge07("dice-interact-hover-font"+font);
                    ExecuteEvents.Execute(pointer.gameObject,data,ExecuteEvents.pointerExitHandler);contour.GetPropertyBlock(props);DiceInteractionColor07(props.GetColor("_BaseColor"),UiKit.Ink,"exit font"+font);
                    Vector3 before=DiceInteractionPosition07(id);Vector3 beforeScreen=app.table.viewCamera.WorldToScreenPoint(before);
                    ClickDie(id);Assert.That(app.table.LayoutDurationFor07(id),Is.EqualTo(.12f));Assert.That(DiceInteractionPosition07(id),Is.EqualTo(before));
                    app.table.TickLayoutTransitions07(.06f);Vector3 middle=DiceInteractionPosition07(id);Assert.That(Vector3.Distance(middle,before),Is.GreaterThan(0));Assert.That(app.table.LayoutPlayingFor07(id),Is.True);
                    yield return CaptureEdge07("dice-interact-rise60-font"+font);
                    app.table.TickLayoutTransitions07(.06f);Assert.That(app.table.LayoutPlayingFor07(id),Is.False);
                    float lift=(app.table.viewCamera.WorldToScreenPoint(DiceInteractionPosition07(id)).y-beforeScreen.y)/app.canvas.scaleFactor;
                    Assert.That(lift,Is.EqualTo(6).Within(.001f),"Actual world-space root projects to six reference pixels, not a simulated label offset.");
                    yield return CaptureEdge07("dice-interact-rise120-font"+font);
                    ClickDie(id);Assert.That(app.table.LayoutDurationFor07(id),Is.EqualTo(.08f));app.table.TickLayoutTransitions07(.04f);Assert.That(app.table.LayoutPlayingFor07(id),Is.True);
                    app.table.TickLayoutTransitions07(.04f);Assert.That(Vector3.Distance(DiceInteractionPosition07(id),before),Is.LessThan(.00001f));Assert.That(app.table.LayoutPlayingFor07(id),Is.False);
                    DiceInteractionInvariant07(state,bytes);
                }
                finally{app.enabled=true;app.table.enabled=true;}
            }
        }

        private void DiceInteractionC53Scene07()
        {
            var current=app.Session.State;var scene=Art07Fixtures.Create(app.Config,Art07FixtureKind.SharedHalf,current.nextItemSerial,current.nextDieSerial);
            scene.sourceRunId=current.sourceRunId;scene.orderedLockedIds.Clear();foreach(var d in scene.currentResults)d.isLocked=false;
            scene.currentResults[0].currentPip=3;scene.currentResults[0].remoteOverride=true;scene.currentResults[0].currentColor="blue";scene.redWave=true;scene.randomOverrides.Clear();
            var result=app.Send(new SessionCommand{kind=CommandKind.ApplyDebugScene,debugScene=scene});Assert.That(result.accepted,Is.True,result.error);
            if(HasActive("RunDebugScene"))Click("RunDebugScene");if(HasActive("CloseDialog"))Click("CloseDialog");
        }
        [UnityTest] public IEnumerator PLAY_ART07_DICEINTERACT_LockCommitsOnceThen25msStagger180msAndOnlyThenRevealsFrozenPreview()
        {
            yield return DiceLabelBegin07(150);DiceInteractionC53Scene07();yield return new WaitForSecondsRealtime(.25f);
            string[] ids=app.Session.State.dice.Select(d=>d.dieId).ToArray();ClickDie(ids[0]);Click("Confirm");app.SkipVisual();yield return null;
            app.enabled=false;app.table.enabled=false;
            try
            {
                string oldScore=FindActive<TextMeshProUGUI>("FinalScore").text;
                string oldSnapshot=JsonUtility.ToJson(app.Session.PreviewSnapshot());
                string[] ordered={ids[3],ids[1],ids[2]};foreach(string id in ordered)ClickDie(id);app.table.TickLayoutTransitions07(.12f);
                var positions=ordered.Select(DiceInteractionPosition07).ToArray();var before=app.Session.State;
                Click("Confirm");var after=app.Session.State;
                Assert.That(after.actionSequence,Is.EqualTo(before.actionSequence+1));Assert.That(after.processedReceipts.Count,Is.EqualTo(before.processedReceipts.Count+1));
                CollectionAssert.AreEqual(new[]{ids[0]}.Concat(ordered),after.hand.orderedLockedIds);
                Assert.That(app.Session.PendingIds,Is.Empty);Assert.That(app.LockVisualPlaying07,Is.True);Assert.That(app.IsBusy,Is.True);
                Assert.That(app.LockVisualDuration07,Is.EqualTo(.23f).Within(.000001f));
                string committed=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);
                Assert.That(File.ReadAllText(app.SaveLocation),Does.Contain(ordered[0]),"Commit is already on disk before the first movement frame.");
                var frozen=typeof(GameApp).GetMethod("DisplayScoreSnapshot07",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(app,null);
                Assert.That(JsonUtility.ToJson(frozen),Is.EqualTo(oldSnapshot),"Freeze actual current/color/permanent snapshot, not recomputed physical faces.");
                Assert.That(FindActive<TextMeshProUGUI>("FinalScore").text,Is.EqualTo(oldScore));
                Assert.That(Button("Confirm").interactable,Is.False);Assert.That(Button("Submit").interactable,Is.False);Assert.That(Button("Roll").interactable,Is.False);
                var duplicateAction=app.Send(new SessionCommand{kind=CommandKind.Submit});Assert.That(duplicateAction.accepted,Is.False);
                DiceInteractionBothClocks07(.01f);Assert.That(Vector3.Distance(DiceInteractionPosition07(ordered[0]),positions[0]),Is.GreaterThan(0));
                Assert.That(DiceInteractionPosition07(ordered[1]),Is.EqualTo(positions[1]));Assert.That(DiceInteractionPosition07(ordered[2]),Is.EqualTo(positions[2]));
                yield return CaptureEdge07("dice-interact-lock010-first-only");
                AssertPoolMotionLabels07();
                DiceInteractionBothClocks07(.02f);Assert.That(Vector3.Distance(DiceInteractionPosition07(ordered[1]),positions[1]),Is.GreaterThan(0));Assert.That(DiceInteractionPosition07(ordered[2]),Is.EqualTo(positions[2]));
                float paused=app.LockVisualElapsed07;Click("Pause");DiceInteractionTick07(.3f);Assert.That(app.LockVisualElapsed07,Is.EqualTo(paused));Click("CloseDialog");
                app.Refresh();Assert.That(app.LockVisualElapsed07,Is.EqualTo(paused));Assert.That(FindActive<TextMeshProUGUI>("FinalScore").text,Is.EqualTo(oldScore));
                DiceInteractionBothClocks07(.15f);Assert.That(app.table.LockProgress07(ordered[0]),Is.EqualTo(1));Assert.That(app.table.LockProgress07(ordered[2]),Is.LessThan(1));
                DiceInteractionBothClocks07(.049f);Assert.That(app.LockVisualPlaying07,Is.True);Assert.That(FindActive<TextMeshProUGUI>("FinalScore").text,Is.EqualTo(oldScore));
                yield return CaptureEdge07("dice-interact-lock229-last-not-finished");
                AssertPoolMotionLabels07();
                DiceInteractionBothClocks07(.0011f);Assert.That(app.IsBusy,Is.False);Assert.That(app.LockVisualPlaying07,Is.False);
                Assert.That(app.table.IsTransitioning,Is.False,"At 230ms all actual locked and unlocked pool geometry must be final.");
                Assert.That(FindActive<TextMeshProUGUI>("FinalScore").text,Is.Not.EqualTo(oldScore));
                Assert.That(app.Session.PreviewScoreRange().isRandom,Is.True,"C53 must remain an unrevealed range during a lock action.");
                DiceInteractionInvariant07(committed,disk);yield return CaptureEdge07("dice-interact-lock230-preview-updated");AssertPoolMotionLabels07();
            }
            finally{if(app.IsBusy)app.SkipVisual();app.enabled=true;app.table.enabled=true;}
        }

        private void AssertPoolMotionLabels07()
        {
            var state=app.Session.State;
            var faces=state.dice.Select(d=>app.table.GetFaceOrientation(d.dieId)).Where(p=>p!=null&&p.gameObject.activeInHierarchy)
                .SelectMany(p=>p.GetComponentsInChildren<MeshRenderer>(false)).Where(r=>r.enabled&&r.name.StartsWith("Face_")).ToArray();
            foreach(var die in state.dice.Where(d=>!state.hand.orderedLockedIds.Contains(d.dieId)))
                foreach(var glyph in DiceLabelVisibleGlyphRects07(FindActive<TextMeshProUGUI>("DieState"+die.dieId)))
                    foreach(var face in faces)Assert.That(glyph.Overlaps(DiceLabelRendererRect07(face)),Is.False,"Moving pool label must not cross another physical face: "+die.dieId);
        }

        [UnityTest] public IEnumerator PLAY_ART07_DICEINTERACT_FirstRollDotsNeverLoopAndFastReducedSkipFocusDoNotCommitAgain()
        {
            yield return DiceLabelBegin07(100);DiceLabelApply07(10,false);yield return new WaitForSecondsRealtime(.25f);
            app.enabled=false;
            try
            {
                var ready=Button("Roll").transform.Find("Label").GetComponent<TextMeshProUGUI>();
                Assert.That(ready.text,Is.EqualTo("掷骰 "+app.Session.State.hand.rollsRemaining+" [R]"));ready.ForceMeshUpdate();
                foreach(var glyph in ready.textInfo.characterInfo.Take(ready.textInfo.characterCount).Where(c=>!char.IsWhiteSpace(c.character)))
                    Assert.That(glyph.isVisible,Is.True,"Ready roll label must retain every glyph, including the real remaining count.");
                yield return CaptureEdge07("dice-interact-ready-real-roll-budget");
                Click("Roll");Assert.That(app.IsBusy,Is.True);var dots=FindActive<Art07RollButtonDots>("Roll");Assert.That(dots.IsShowing,Is.True);Assert.That(dots.VisibleStep,Is.Zero);
                string state=StateJson();byte[] bytes=File.ReadAllBytes(app.SaveLocation);
                DiceInteractionTick07(.091f);Assert.That(dots.VisibleStep,Is.EqualTo(3));yield return CaptureEdge07("dice-interact-roll-dots091");
                DiceInteractionTick07(.1f);Assert.That(dots.IsShowing,Is.False);DiceInteractionTick07(.2f);Assert.That(dots.IsShowing,Is.False,"Finite feedback cannot wrap or restart during the remaining roll.");
                app.Refresh();Assert.That(FindActive<Art07RollButtonDots>("Roll").IsShowing,Is.False);app.SkipVisual();DiceInteractionInvariant07(state,bytes);
            }
            finally{if(app.IsBusy)app.SkipVisual();app.enabled=true;}
            foreach(string mode in new[]{"fast","reduced","instant","skip","focus"})
            {
                DiceInteractionField07("fast",mode=="fast");DiceInteractionField07("reduced",mode=="reduced");DiceInteractionField07("instant07",mode=="instant");UiKit.ReducedMotion=mode=="reduced";
                DiceLabelApply07(10,true);yield return new WaitForSecondsRealtime(.25f);app.enabled=false;app.table.enabled=false;
                try
                {
                    string[] ids=app.Session.State.dice.Take(3).Select(d=>d.dieId).ToArray();foreach(string id in ids)ClickDie(id);app.table.TickLayoutTransitions07(.12f);
                    long sequence=app.Session.ActionSequence;Click("Confirm");Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence+1));
                    string state=StateJson();byte[] bytes=File.ReadAllBytes(app.SaveLocation);
                    if(mode=="reduced"||mode=="instant")Assert.That(app.IsBusy,Is.False,"Reduced/Instant must show final geometry without sliding.");
                    else if(mode=="fast")
                    {
                        Assert.That(app.LockVisualDuration07,Is.EqualTo(.115f).Within(.000001f),"fast lock duration");
                        DiceInteractionBothClocks07(.05f);Assert.That(app.IsBusy,Is.True,"fast lock 50ms");
                        DiceInteractionBothClocks07(.0651f);Assert.That(app.IsBusy,Is.False,"fast lock 115.1ms");
                        foreach(string id in ids){Assert.That(app.table.LockProgress07(id),Is.EqualTo(1),"fast locked die "+id);Assert.That(app.table.LayoutPlayingFor07(id),Is.False,"fast locked geometry "+id);}
                        // Other, unlocked pool dice retain their existing normal 180ms reflow. Drive their real clock too.
                        DiceInteractionBothClocks07(.065f);
                    }
                    else if(mode=="skip"){DiceInteractionBothClocks07(.04f);app.SkipVisual();}
                    else {DiceInteractionBothClocks07(.04f);app.SendMessage("OnApplicationFocus",false,SendMessageOptions.RequireReceiver);}
                    Assert.That(app.LockVisualPlaying07,Is.False,mode+" lock presentation must finish");
                    Assert.That(app.IsBusy,Is.False,mode+" session must become interactive");
                    string moving=string.Join(",",app.Session.State.dice.Where(d=>app.table.LayoutPlayingFor07(d.dieId)).Select(d=>d.dieId+"="+app.table.LayoutElapsedFor07(d.dieId).ToString("R")+"/"+app.table.LayoutDurationFor07(d.dieId).ToString("R")));
                    Debug.Log("DICE_FINAL "+mode+" moving="+moving);
                    Assert.That(app.table.IsTransitioning,Is.False,mode+" all actual geometry must be final; moving="+moving);
                    foreach(string id in ids)Assert.That(app.table.LockProgress07(id),Is.EqualTo(1));
                    DiceInteractionInvariant07(state,bytes);yield return CaptureEdge07("dice-interact-lock-final-"+mode);
                }
                finally{if(app.IsBusy)app.SkipVisual();app.enabled=true;app.table.enabled=true;}
            }
        }
    }
}
