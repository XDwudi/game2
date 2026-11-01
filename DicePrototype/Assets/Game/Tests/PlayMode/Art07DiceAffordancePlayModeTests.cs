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
