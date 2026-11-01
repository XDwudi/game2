using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using DiceCraft.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        [UnityTest] public IEnumerator PLAY_ART07_UPGRADE_ActualCeramicAndSingleQuestionFlipRenderBeforeMiddleAndAfter()
        {
            foreach(string upgrade in new[]{"U05","U07"})
            {
                yield return TransactionShop07(s=>s.shop.offers.Any(o=>o.contentId==upgrade));Click("Tab2");yield return null;
                var before=app.Session.State;var offer=before.shop.offers.First(o=>o.contentId==upgrade);
                var die=before.dice.First(d=>d.lifetime=="permanent"&&!d.isGlass);string face=upgrade=="U05"?"F02":die.faces.First(f=>app.Session.IsUpgradeTargetLegal(upgrade,die.dieId,f.faceId)).faceId;
                bool wasEnabled=app.enabled;var shots=new List<Texture2D>();Texture2D hiddenQuestion=null;
                var ceramic=app.table.bodyMaterial.GetColor("_BaseColor");var glass=app.table.glassMaterial.GetColor("_BaseColor");
                try
                {
                    // Freeze automatic UI ticking, then drive the SAME real transaction clock.
                    // Independent EOF captures still use the actual active GameView/GPU.
                    app.enabled=false;
                    var bought=app.Send(new SessionCommand{kind=CommandKind.Buy,offerId=offer.offerId,targetDieId=die.dieId,targetFaceId=upgrade=="U05"?null:face});
                    Assert.That(bought.accepted,Is.True,bought.error);Assert.That(app.TransactionVisualDuration07,Is.EqualTo(upgrade=="U05"?.42f:.36f).Within(.0001f));
                    var committed=app.Session.State;string state=GameSession.StateHash(committed);byte[] disk=File.ReadAllBytes(app.SaveLocation);
                    var surface=app.GetComponentInChildren<Art07UpgradeSurface>(true);Assert.That(surface,Is.Not.Null);
                    var clock=UpgradeTransactionClock07();Assert.That(clock,Is.Not.Null);
                    var orientation=app.table.GetFaceOrientation("$die-preview");var body=orientation.Find("Body").GetComponent<MeshRenderer>();
                    var faceRenderers=Enumerable.Range(1,6).Select(i=>orientation.Find("Face_F"+i.ToString("00")).GetComponent<MeshRenderer>()).ToArray();
                    var meshes=faceRenderers.Select(r=>r.GetComponent<MeshFilter>().sharedMesh).ToArray();var transforms=faceRenderers.Select(r=>r.transform.localToWorldMatrix).ToArray();
                    var props=new MaterialPropertyBlock();float previousAngle=-1;
                    foreach(float progress in new[]{0f,.175f,.475f,.80f,.99f})
                    {
                        clock.Tick(Math.Max(0,clock.Duration*progress-clock.Elapsed),false);
                        Assert.That(surface.IsActive,Is.True);Assert.That(surface.Progress,Is.EqualTo(progress).Within(.0001f));
                        if(upgrade=="U05")
                        {
                            body.GetPropertyBlock(props);Assert.That(Vector4.Distance(props.GetColor("_BaseColor"),Color.Lerp(ceramic,glass,progress)),Is.LessThan(.0001f));
                            Assert.That(props.GetFloat("_Glass"),Is.EqualTo(progress).Within(.0001f));
                            Assert.That(faceRenderers.Count(r=>r.enabled),Is.EqualTo(3),"Whole-die upgrade must show three physical faces, not a head-on flat tile.");
                            foreach(var renderer in faceRenderers.Where(r=>r.enabled))
                                Assert.That(Vector3.Dot(renderer.transform.forward,-app.table.viewCamera.transform.forward),Is.GreaterThan(.08f),"No edge-only substitute for a readable side face.");
                        }
                        else
                        {
                            var target=faceRenderers[DiceTable.FaceIndex(face)];var turning=target.transform.Find("UpgradeTurningPatch07");var question=target.transform.Find("UpgradeQuestion07");
                            Assert.That(turning,Is.Not.Null);Assert.That(question,Is.Not.Null);
                            Assert.That(Vector3.Dot(question.up,app.table.viewCamera.transform.up),Is.GreaterThan(.99f),"The question mark must stay upright on the screen, including downward-oriented physical face axes.");
                            Assert.That(Vector3.Dot(question.TransformVector(Vector3.right).normalized,app.table.viewCamera.transform.right),Is.GreaterThan(.99f),"The question mark must not mirror when seen from a back-facing physical face plane.");
                            bool unknown=progress>=.35f&&progress<.60f;
                            Assert.That(question.gameObject.activeSelf,Is.EqualTo(unknown));Assert.That(turning.gameObject.activeSelf,Is.EqualTo(!unknown));
                            Assert.That(surface.TurnDegrees,Is.GreaterThanOrEqualTo(previousAngle));previousAngle=surface.TurnDegrees;
                            Assert.That(surface.VisibleFaceStage,Is.EqualTo(progress<.35f?"OldPips":unknown?"Question":"CommittedPips"));
                            Assert.That(target.GetComponent<MeshFilter>().sharedMesh.name,Does.StartWith("Pips_"+committed.dice.Single(d=>d.dieId==die.dieId).faces.Single(f=>f.faceId==face).permanentPip+"_"));
                            if(unknown)
                            {
                                question.gameObject.SetActive(false);
                                yield return CaptureUpgradeGpu07(upgrade+"-question-hidden",t=>hiddenQuestion=t);
                                question.gameObject.SetActive(true);
                            }
                        }
                        for(int i=0;i<6;i++)
                        {
                            Assert.That(faceRenderers[i].GetComponent<MeshFilter>().sharedMesh,Is.SameAs(meshes[i]));
                            Assert.That(faceRenderers[i].transform.localToWorldMatrix,Is.EqualTo(transforms[i]),"The flip must not rotate a physical face or another die face.");
                        }
                        Texture2D shot=null;yield return CaptureUpgradeGpu07(upgrade+"-p"+Mathf.RoundToInt(progress*100),t=>shot=t);shots.Add(shot);
                        TestContext.WriteLine("UPGRADE_GPU "+upgrade+" progress="+progress+" stage="+surface.VisibleFaceStage+" turn="+surface.TurnDegrees+" frame="+Time.frameCount);
                        if(upgrade=="U07"&&progress>.35f&&progress<.60f)
                        {
                            var question=orientation.Find("Face_"+face+"/UpgradeQuestion07").GetComponent<MeshRenderer>();
                            Assert.That(UpgradeChangedPixels07(hiddenQuestion,shot,UpgradeMeshScreenRect07(question)),Is.GreaterThan(5),"Actual question glyph must contribute GPU pixels, not merely exist in a hierarchy.");
                        }
                        Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(state));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
                    }
                    Rect bodyRect=UpgradeMeshScreenRect07(body);
                    Assert.That(UpgradeChangedPixels07(shots[0],shots[2],bodyRect),Is.GreaterThan(10),"Before and middle must visibly differ on the actual die.");
                    Assert.That(UpgradeChangedPixels07(shots[0],shots[4],bodyRect),Is.GreaterThan(10),"Before and committed appearance must visibly differ.");
                    clock.Tick(clock.Duration-clock.Elapsed+.001f,false);
                    Assert.That(surface.IsActive,Is.False);Assert.That(app.TransactionVisualPlaying07,Is.False);Assert.That(orientation.gameObject.activeInHierarchy,Is.False);
                    Assert.That(app.table.bodyMaterial.GetColor("_BaseColor"),Is.EqualTo(ceramic));Assert.That(app.table.glassMaterial.GetColor("_BaseColor"),Is.EqualTo(glass));
                    Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(state));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
                }
                finally
                {
                    app.SkipVisual();app.enabled=wasEnabled;foreach(var shot in shots)if(shot!=null)UnityEngine.Object.Destroy(shot);if(hiddenQuestion!=null)UnityEngine.Object.Destroy(hiddenQuestion);
                }
            }
        }

        [UnityTest] public IEnumerator PLAY_ART07_UPGRADE_FastReducedPauseAndSkipKeepCommittedStateAndResetSurfaces()
        {
            int oldFast=PlayerPrefs.GetInt("fast",0),oldReduced=PlayerPrefs.GetInt("reduced",0);bool hadFast=PlayerPrefs.HasKey("fast"),hadReduced=PlayerPrefs.HasKey("reduced");
            try
            {
                foreach(bool reduce in new[]{false,true})foreach(string upgrade in new[]{"U05","U07"})
                {
                    PlayerPrefs.SetInt("fast",reduce?0:1);PlayerPrefs.SetInt("reduced",reduce?1:0);yield return LoadBootstrap();
                    yield return TransactionShop07(s=>s.shop.offers.Any(o=>o.contentId==upgrade));Click("Tab2");yield return null;
                    var before=app.Session.State;var offer=before.shop.offers.First(o=>o.contentId==upgrade);var die=before.dice.First(d=>!d.isGlass);
                    string face=upgrade=="U05"?null:die.faces.First(f=>app.Session.IsUpgradeTargetLegal(upgrade,die.dieId,f.faceId)).faceId;
                    bool wasEnabled=app.enabled;
                    try
                    {
                        app.enabled=false;var result=app.Send(new SessionCommand{kind=CommandKind.Buy,offerId=offer.offerId,targetDieId=die.dieId,targetFaceId=face});Assert.That(result.accepted,Is.True,result.error);
                        string state=GameSession.StateHash(app.Session.State);byte[] disk=File.ReadAllBytes(app.SaveLocation);
                        var clock=UpgradeTransactionClock07();var surface=app.GetComponentInChildren<Art07UpgradeSurface>(true);
                        Assert.That(clock.Duration,Is.EqualTo(reduce?.12f:.15f).Within(.0001f));clock.Tick(clock.Duration*.475f,false);
                        float elapsed=clock.Elapsed,progress=surface.Progress,angle=surface.TurnDegrees;clock.Tick(1,true);
                        Assert.That(clock.Elapsed,Is.EqualTo(elapsed));Assert.That(surface.Progress,Is.EqualTo(progress));Assert.That(surface.TurnDegrees,Is.EqualTo(angle));
                        if(upgrade=="U07")
                        {
                            Assert.That(surface.VisibleFaceStage,Is.EqualTo("Question"));if(reduce)Assert.That(surface.TurnDegrees,Is.Zero);
                            Texture2D shot=null;yield return CaptureUpgradeGpu07(upgrade+(reduce?"-reduced":"-fast")+"-question",t=>shot=t);UnityEngine.Object.Destroy(shot);
                        }
                        app.SkipVisual();Assert.That(surface.IsActive,Is.False);Assert.That(clock.IsPlaying,Is.False);Assert.That(app.Session.Busy,Is.False);
                        Assert.That(surface.GetComponentsInChildren<MeshRenderer>(true).Count(r=>r.gameObject.activeInHierarchy),Is.Zero);
                        Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(state));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
                    }
                    finally{app.SkipVisual();app.enabled=wasEnabled;}
                }
            }
            finally
            {if(hadFast)PlayerPrefs.SetInt("fast",oldFast);else PlayerPrefs.DeleteKey("fast");if(hadReduced)PlayerPrefs.SetInt("reduced",oldReduced);else PlayerPrefs.DeleteKey("reduced");}
        }

        private Art07TransactionMotion UpgradeTransactionClock07()
        {
            // S09 also pools Art07TransactionMotion below inactive FrozenUiGroup nodes.
            // Only this exact direct canvas child belongs to the committed transaction.
            var roots=app.canvas.transform.Cast<Transform>().Where(t=>t.name=="Art07TransactionMotion").ToArray();
            Assert.That(roots.Length,Is.EqualTo(1),"Expected the unique committed transaction root, not a frozen settlement snapshot.");
            var clock=roots[0].GetComponent<Art07TransactionMotion>();Assert.That(clock,Is.Not.Null);
            Assert.That(clock.IsPlaying,Is.True,"The real committed upgrade clock must be active before manual sampling.");
            Assert.That(clock.Kind,Is.EqualTo("die_upgrade"));
            Assert.That(clock.IsPlaying,Is.EqualTo(app.TransactionVisualPlaying07));
            Assert.That(clock.Kind,Is.EqualTo(app.TransactionVisualKind07));
            Assert.That(clock.Duration,Is.EqualTo(app.TransactionVisualDuration07));
            Assert.That(clock.Elapsed,Is.EqualTo(app.TransactionVisualElapsed07));
            TestContext.WriteLine("UPGRADE_CLOCK exact="+clock.transform.parent.name+"/"+clock.name+" duration="+clock.Duration.ToString("R")+" elapsed="+clock.Elapsed.ToString("R")+" otherPooledClocks="+(app.canvas.GetComponentsInChildren<Art07TransactionMotion>(true).Length-1));
            return clock;
        }

        private IEnumerator CaptureUpgradeGpu07(string id,Action<Texture2D> accept)
        {
            bool done=false;Exception error=null;Texture2D texture=null;
            IEnumerator Capture()
            {
                yield return new WaitForEndOfFrame();
                try
                {
                    texture=ScreenCapture.CaptureScreenshotAsTexture();Assert.That(texture.width,Is.EqualTo(Screen.width));Assert.That(texture.height,Is.EqualTo(Screen.height));
                    string dir=Path.Combine(Path.GetDirectoryName(app.SaveLocation),"art07-upgrade-frames");Directory.CreateDirectory(dir);
                    File.WriteAllBytes(Path.Combine(dir,id+"-"+Time.frameCount+".png"),texture.EncodeToPNG());accept(texture);texture=null;
                }
                catch(Exception ex){error=ex;}
                finally{if(texture!=null)UnityEngine.Object.Destroy(texture);done=true;}
            }
            var worker=app.StartCoroutine(Capture());double deadline=Time.realtimeSinceStartupAsDouble+10;
            while(!done&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            if(!done){app.StopCoroutine(worker);Assert.Fail("Actual EndOfFrame/GameView capture timed out.");}if(error!=null)throw error;
        }
        private Rect UpgradeMeshScreenRect07(MeshRenderer renderer)
        {
            var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
            var b=mesh.bounds;
            var vertices=mesh.isReadable?mesh.vertices:Enumerable.Range(0,8).Select(i=>b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))).ToArray();
            var points=vertices.Select(v=>app.table.viewCamera.WorldToScreenPoint(renderer.transform.TransformPoint(v))).ToArray();
            return Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));
        }
        private static int UpgradeChangedPixels07(Texture2D before,Texture2D after,Rect area)
        {
            var a=before.GetPixels32();var b=after.GetPixels32();int count=0;
            // Two 8-bit levels reject a one-level quantization change; no pure-colour
            // match that would reject valid anti-aliased edges or transparent glass.
            for(int y=Math.Max(0,Mathf.FloorToInt(area.yMin));y<Math.Min(after.height,Mathf.CeilToInt(area.yMax));y++)
                for(int x=Math.Max(0,Mathf.FloorToInt(area.xMin));x<Math.Min(after.width,Mathf.CeilToInt(area.xMax));x++)
                {int i=y*after.width+x;if(Math.Abs(a[i].r-b[i].r)>=2||Math.Abs(a[i].g-b[i].g)>=2||Math.Abs(a[i].b-b[i].b)>=2)count++;}
            return count;
        }
    }
}
