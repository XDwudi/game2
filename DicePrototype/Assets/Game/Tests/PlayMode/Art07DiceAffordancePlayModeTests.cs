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
using UnityEngine.UI;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        [UnityTest]
        public IEnumerator PLAY_ART07_DICE_DebugAppliedTableAllCloseRoutesRestoreGripsWithoutApplyingDraft()
        {
            foreach(int font in new[]{100,150})
            {
                yield return DiceLabelBegin07(font);
                string normalPath=Path.Combine(saveDirectory,"active-run.json");byte[] normal=File.ReadAllBytes(normalPath);
                foreach(string close in new[]{"RunDebugScene","CloseDialog","Escape"})
                {
                    Assert.That(app.EnterDebugWorkspace(),Is.True);
                    var initial=app.Session.State;
                    var scene=DiceCraft.Verification.Art07Fixtures.Create(app.Config,DiceCraft.Verification.Art07FixtureKind.MaximumTable,initial.nextItemSerial,initial.nextDieSerial);
                    scene.sourceRunId=initial.sourceRunId;
                    long initialSequence=app.Session.ActionSequence;
                    var applied=app.Send(new SessionCommand{kind=CommandKind.ApplyDebugScene,debugScene=scene});
                    Assert.That(applied.accepted,Is.True,applied.error);Assert.That(applied.pendingSave,Is.False);
                    Assert.That(app.Session.ActionSequence,Is.EqualTo(initialSequence+1));
                    yield return WaitForIdle();Click("ReloadDebugDraft");
                    Assert.That(app.Session.State.hand.orderedLockedIds.Count,Is.EqualTo(8));
                    Assert.That(AffordanceGrips07(),Is.Empty,"Applying under the modal suppresses background drag affordances until close.");
                    string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);long sequence=app.Session.ActionSequence;
                    var saved=app.Session.State;var diceRng=saved.diceRngState;var shopRng=saved.shopRngState;var effectRng=saved.effectRngState;
                    Click("DebugNav5");var wallet=FindActive<TMPro.TMP_InputField>("Wallet");
                    wallet.text=(saved.gold+123).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    Assert.That(wallet.text,Is.Not.EqualTo(saved.gold.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                    Assert.That(StateJson(),Is.EqualTo(before),"Typing an unapplied draft must not change committed rules.");
                    if(close=="Escape")yield return GripDebugRealEscape07();
                    else yield return LegacyVisibleClick07(Button(close));
                    yield return new WaitForSecondsRealtime(.25f);Canvas.ForceUpdateCanvases();
                    Assert.That(HasActive("Dialog"),Is.False,close+" must logically close the debug panel.");
                    string capture="debug-close-"+close+"-font"+font;
                    // Preserve the actual failing frame before any assertion about the missing dots.
                    yield return CaptureAffordance07(capture);
                    var grips=AffordanceGrips07();Assert.That(grips.Length,Is.EqualTo(8),close+" must rebuild all legal handles without a test-side Refresh.");
                    Texture2D shot=new Texture2D(2,2,TextureFormat.RGBA32,false);
                    try
                    {
                        Assert.That(shot.LoadImage(File.ReadAllBytes(Path.Combine(saveDirectory,"art07-dice-affordance",capture+".png"))),Is.True);
                        Assert.That(shot.width,Is.EqualTo(Screen.width));Assert.That(shot.height,Is.EqualTo(Screen.height));
                        foreach(var grip in grips)
                        {
                            var pointer=grip.GetComponentInParent<DiePointer>();Assert.That(pointer,Is.Not.Null);
                            Rect hit=GuideCheckScreenRect07((RectTransform)pointer.transform),area=GuideCheckScreenRect07(grip);
                            Assert.That(hit.xMin,Is.LessThanOrEqualTo(area.xMin));Assert.That(hit.xMax,Is.GreaterThanOrEqualTo(area.xMax));
                            Assert.That(hit.yMin,Is.LessThanOrEqualTo(area.yMin));Assert.That(hit.yMax,Is.GreaterThanOrEqualTo(area.yMax));
                            Assert.That(hit.width,Is.GreaterThanOrEqualTo(39.9f));Assert.That(hit.height,Is.GreaterThanOrEqualTo(39.9f));
                            var dots=grip.GetComponentsInChildren<Graphic>(false);Assert.That(dots.Length,Is.EqualTo(6));
                            foreach(var dot in dots)
                            {
                                Assert.That(dot.raycastTarget,Is.False);Assert.That(dot.canvasRenderer.cull,Is.False);
                                Assert.That(dot.canvasRenderer.GetInheritedAlpha(),Is.GreaterThan(.99f));
                                Assert.That(dot.canvasRenderer.GetMesh().vertexCount,Is.GreaterThanOrEqualTo(4));
                                Assert.That(GripPaperPixels07(shot,GuideCheckScreenRect07(dot.rectTransform)),Is.GreaterThan(0),capture+" / "+grip.name+" / "+dot.name+" has no actual Paper pixels.");
                            }
                            CheckAffordanceAgainstPips07(grip);
                            var data=new PointerEventData(EventSystem.current){position=area.center,button=PointerEventData.InputButton.Left};
                            var rays=new List<RaycastResult>();EventSystem.current.RaycastAll(data,rays);
                            Assert.That(rays.Count,Is.GreaterThan(0));Assert.That(rays[0].gameObject.GetComponentInParent<DiePointer>(),Is.SameAs(pointer));
                        }
                    }
                    finally{UnityEngine.Object.Destroy(shot);}
                    Assert.That(StateJson(),Is.EqualTo(before),"Closing must discard no rules and must not apply the edited draft.");
                    Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
                    CollectionAssert.AreEqual(normal,File.ReadAllBytes(normalPath));
                    Assert.That(app.Session.State.diceRngState,Is.EqualTo(diceRng));Assert.That(app.Session.State.shopRngState,Is.EqualTo(shopRng));Assert.That(app.Session.State.effectRngState,Is.EqualTo(effectRng));
                    string[] order=app.Session.State.hand.orderedLockedIds.ToArray();var expected=order.ToList();expected.RemoveAt(0);expected.Insert(2,order[0]);
                    DragLockedDieAfter(order[0],order[2]);yield return new WaitForSecondsRealtime(.2f);
                    CollectionAssert.AreEqual(expected,app.Session.State.hand.orderedLockedIds);
                    Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence+1),"One real drag after close must commit exactly once.");
                    Assert.That(app.Session.State.diceRngState,Is.EqualTo(diceRng));Assert.That(app.Session.State.shopRngState,Is.EqualTo(shopRng));Assert.That(app.Session.State.effectRngState,Is.EqualTo(effectRng));
                    CollectionAssert.AreEqual(normal,File.ReadAllBytes(normalPath));
                }
            }
        }
        private static int GripPaperPixels07(Texture2D shot,Rect rect)
        {
            Color32 expected=(Color32)UiKit.Paper;int count=0;
            for(int y=Mathf.Max(0,Mathf.CeilToInt(rect.yMin));y<Mathf.Min(shot.height,Mathf.FloorToInt(rect.yMax));y++)
            for(int x=Mathf.Max(0,Mathf.CeilToInt(rect.xMin));x<Mathf.Min(shot.width,Mathf.FloorToInt(rect.xMax));x++)
            {
                Color32 pixel=shot.GetPixel(x,y);
                // Flat unlit UI fill: allow only two 8-bit quantization steps, not an antialias/color relaxation.
                if(Math.Abs(pixel.r-expected.r)<=2&&Math.Abs(pixel.g-expected.g)<=2&&Math.Abs(pixel.b-expected.b)<=2)count++;
            }
            return count;
        }
        private IEnumerator GripDebugRealEscape07()
        {
            var previous=UnityEngine.InputSystem.Keyboard.current;UnityEngine.InputSystem.Keyboard injected=null;
            var editorRouting=UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode;
            var background=UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior;bool observed=false;
            Action observe=()=>{if(UnityEngine.InputSystem.LowLevel.InputState.currentUpdateType==UnityEngine.InputSystem.LowLevel.InputUpdateType.Dynamic&&injected!=null&&injected.escapeKey.isPressed)observed=true;};
            try
            {
                UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode=UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior=UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
                injected=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>("Art07GripCloseKeyboard");injected.MakeCurrent();
                UnityEngine.InputSystem.InputSystem.onAfterUpdate+=observe;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(injected,new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Escape));
                yield return null;yield return null;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(injected,new UnityEngine.InputSystem.LowLevel.KeyboardState());yield return null;
                Assert.That(observed,Is.True,"Escape must reach a real Dynamic input update, not invoke CloseModal directly.");
            }
            finally
            {
                UnityEngine.InputSystem.InputSystem.onAfterUpdate-=observe;
                if(injected!=null&&injected.added)UnityEngine.InputSystem.InputSystem.RemoveDevice(injected);
                UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode=editorRouting;
                UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior=background;
                if(previous!=null&&previous.added)previous.MakeCurrent();
            }
        }

        [UnityTest] public IEnumerator PLAY_ART07_DICE_VisibleGripUsesExistingHitAndPendingHollowChecksKeepPipsClear()
        {
            foreach(int font in new[]{100,125,150})
            {
                yield return DiceLabelBegin07(font);DiceLabelApply07(10,true);yield return new WaitForSecondsRealtime(.25f);
                string[] ids=app.Session.State.dice.Select(d=>d.dieId).ToArray();
                foreach(string id in ids.Take(8))ClickDie(id);yield return new WaitForSecondsRealtime(.25f);Canvas.ForceUpdateCanvases();
                Assert.That(app.canvas.GetComponentsInChildren<Art07HollowCheck>(false).Length,Is.EqualTo(8));
                foreach(var check in app.canvas.GetComponentsInChildren<Art07HollowCheck>(false))
                {
                    Assert.That(check.raycastTarget,Is.False);Assert.That(check.canvasRenderer.cull,Is.False);Assert.That(check.canvasRenderer.GetInheritedAlpha(),Is.GreaterThan(.99f));
                    var mesh=check.canvasRenderer.GetMesh();Assert.That(mesh.vertexCount,Is.EqualTo(12));Assert.That(mesh.triangles.Length,Is.EqualTo(36),"Only the contour is filled; the inside stays transparent.");
                    CheckAffordanceAgainstPips07(check.rectTransform);
                }
                Assert.That(AffordanceGrips07().Length,Is.Zero);DiceLabelCheck07();yield return CaptureAffordance07("pending-eight-font"+font);
                Click("Confirm");yield return WaitForIdle();yield return new WaitForSecondsRealtime(.25f);Canvas.ForceUpdateCanvases();
                Assert.That(app.canvas.GetComponentsInChildren<Art07HollowCheck>(false),Is.Empty);
                var grips=AffordanceGrips07();Assert.That(grips.Length,Is.EqualTo(8));
                string state=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);
                foreach(var grip in grips)
                {
                    var pointer=grip.GetComponentInParent<DiePointer>();Assert.That(pointer,Is.Not.Null);
                    Assert.That(grip.GetComponentsInChildren<Graphic>(false).All(g=>!g.raycastTarget),Is.True);
                    var hit=(RectTransform)pointer.transform;Rect hitRect=GuideCheckScreenRect07(hit),gripRect=GuideCheckScreenRect07(grip);
                    Assert.That(hitRect.xMin,Is.LessThanOrEqualTo(gripRect.xMin));Assert.That(hitRect.xMax,Is.GreaterThanOrEqualTo(gripRect.xMax));
                    Assert.That(hitRect.yMin,Is.LessThanOrEqualTo(gripRect.yMin));Assert.That(hitRect.yMax,Is.GreaterThanOrEqualTo(gripRect.yMax));
                    Assert.That(hitRect.width,Is.GreaterThanOrEqualTo(39.9f));Assert.That(hitRect.height,Is.GreaterThanOrEqualTo(39.9f));
                    CheckAffordanceAgainstPips07(grip);
                    var data=new PointerEventData(EventSystem.current){position=gripRect.center,button=PointerEventData.InputButton.Left};
                    var rays=new List<RaycastResult>();EventSystem.current.RaycastAll(data,rays);
                    Assert.That(rays.Count,Is.GreaterThan(0));Assert.That(rays[0].gameObject.GetComponentInParent<DiePointer>(),Is.SameAs(pointer),"Visible grip must hit its existing die, not intercept the neighboring slot.");
                }
                DiceLabelCheck07();yield return CaptureAffordance07("locked-eight-grips-font"+font);
                Assert.That(StateJson(),Is.EqualTo(state));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
                // Pending selections forbid moving already locked dice even when capacity is full.
                ClickDie(ids[8]);app.Refresh();yield return null;Assert.That(app.Session.PendingIds,Does.Contain(ids[8]));
                Assert.That(AffordanceGrips07(),Is.Empty);Assert.That(app.canvas.GetComponentsInChildren<Art07HollowCheck>(false).Length,Is.EqualTo(1));
                Click("Clear");Assert.That(AffordanceGrips07().Length,Is.EqualTo(8));
                try{app.Session.Busy=true;app.Refresh();Assert.That(AffordanceGrips07(),Is.Empty);}
                finally{app.Session.Busy=false;app.Refresh();}
                Assert.That(AffordanceGrips07().Length,Is.EqualTo(8));
                Assert.That(StateJson(),Is.EqualTo(state));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
            }
        }
        private RectTransform[] AffordanceGrips07()=>app.canvas.GetComponentsInChildren<RectTransform>(false).Where(r=>r.name.StartsWith("LockedGrip_",StringComparison.Ordinal)).ToArray();
        private void CheckAffordanceAgainstPips07(RectTransform mark)
        {
            Rect area=GuideCheckScreenRect07(mark);
            foreach(var die in app.Session.State.dice)
            {
                var root=app.table.GetFaceOrientation(die.dieId);if(root==null||!root.gameObject.activeInHierarchy)continue;
                foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(false))
                {
                    if(!renderer.enabled||!renderer.name.StartsWith("Face_",StringComparison.Ordinal))continue;
                    var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
                    var points=mesh.vertices.Where(p=>Mathf.Abs(p.x)<.31f&&Mathf.Abs(p.y)<.31f).Select(p=>app.table.viewCamera.WorldToScreenPoint(renderer.transform.TransformPoint(p))).ToArray();
                    if(points.Length==0)continue;
                    var pips=Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));
                    Assert.That(area.Overlaps(pips),Is.False,mark.name+" covers actual visible pips on "+die.dieId+"/"+renderer.name);
                }
            }
        }
        private IEnumerator CaptureAffordance07(string name)
        {
            bool done=false;Exception error=null;
            IEnumerator Capture()
            {
                yield return new WaitForEndOfFrame();Texture2D shot=null;
                try
                {
                    shot=ScreenCapture.CaptureScreenshotAsTexture();Assert.That(shot.width,Is.EqualTo(Screen.width));
                    string directory=Path.Combine(saveDirectory,"art07-dice-affordance");Directory.CreateDirectory(directory);
                    string path=Path.Combine(directory,name+".png");File.WriteAllBytes(path,shot.EncodeToPNG());TestContext.WriteLine("Actual affordance screen: "+path);
                }
                catch(Exception ex){error=ex;}
                finally{if(shot!=null)UnityEngine.Object.Destroy(shot);done=true;}
            }
            var worker=app.StartCoroutine(Capture());double deadline=Time.realtimeSinceStartupAsDouble+10;
            while(!done&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            if(!done){app.StopCoroutine(worker);Assert.Fail("Actual EOF/GameView capture timed out.");}if(error!=null)throw error;
        }
    }
}
