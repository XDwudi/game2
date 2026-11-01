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

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        [Serializable] private sealed class BackdropDiceGpu07 { public Rect rect;public int compared,visibleInk,mismatch;public float meanError; }
        [UnityTest] public IEnumerator PLAY_ART07_SETTLE_BACKDROP_GpuThreeFontsAnd200RenderCyclesPreserveInkPoseAndOwnedMaterials()
        {
            StartRun("70931");ApplyInformationScene07(InformationScene07(Art07FixtureKind.MaximumPool));
            bool enabled=app.enabled,paused=app.table.Paused;app.enabled=false;app.table.Paused=true;
            var host=new GameObject("SettlementBackdropGpuHost",typeof(RectTransform),typeof(CanvasGroup),typeof(Art07SettlementBackdrop));
            var rect=(RectTransform)host.transform;rect.SetParent(app.canvas.transform,false);rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.sizeDelta=new Vector2(1920,1080);
            rect.anchoredPosition=FindActive<RectTransform>("Screen").anchoredPosition;
            host.GetComponent<CanvasGroup>().blocksRaycasts=false;host.GetComponent<CanvasGroup>().interactable=false;
            var backdrop=host.GetComponent<Art07SettlementBackdrop>();var owned=new List<Material>();
            RectTransform card=null;Transform pose=null;
            string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);long sequence=app.Session.ActionSequence;
            try
            {
                foreach(int font in new[]{100,125,150})
                {
                    SettlementField07("textScale07",font);UiKit.FontScale=font/100f;app.Refresh();app.table.FinishLayoutTransitions();Canvas.ForceUpdateCanvases();yield return null;
                    card=FindActive<RectTransform>("Controller_"+app.Session.State.controllers[0].instanceId);
                    var die=app.Session.State.dice.First(d=>d.isGlass);pose=app.table.GetFaceOrientation(die.dieId);Assert.That(pose.gameObject.activeInHierarchy,Is.True);
                    yield return SettlementBackdropGpu07(card,pose,"font"+font,backdrop);
                }
                int uiPool=-1,worldPool=-1;
                for(int i=0;i<200;i++)
                {
                    foreach(var label in card.GetComponentsInChildren<TextMeshProUGUI>(false))label.ForceMeshUpdate();Canvas.ForceUpdateCanvases();
                    backdrop.Capture(new[]{card},new[]{pose},app.table.transform);card.gameObject.SetActive(false);pose.parent.parent.gameObject.SetActive(false);backdrop.Show();
                    yield return null;
                    Assert.That(backdrop.GetComponentsInChildren<UnityEngine.UI.Selectable>(true),Is.Empty);
                    Assert.That(backdrop.GetComponentsInChildren<UnityEngine.UI.Graphic>(true),Is.Empty,"Frozen UI consists only of renderers, not input Graphics.");
                    var materials=backdrop.GetComponentsInChildren<CanvasRenderer>(false).Select(r=>r.GetMaterial(0)).Where(m=>m!=null).ToArray();
                    if(i==0){owned.AddRange(materials);uiPool=backdrop.CreatedUiMeshCount;worldPool=backdrop.CreatedDiceMeshCount;}
                    else{CollectionAssert.AreEqual(owned,materials);Assert.That(backdrop.CreatedUiMeshCount,Is.EqualTo(uiPool));Assert.That(backdrop.CreatedDiceMeshCount,Is.EqualTo(worldPool));}
                    Assert.That(owned.All(m=>m!=null),Is.True,"Disabling source TMP must not destroy owned snapshot materials.");
                    backdrop.Clear();card.gameObject.SetActive(true);pose.parent.parent.gameObject.SetActive(true);Canvas.ForceUpdateCanvases();yield return null;
                    Assert.That(backdrop.Visible||backdrop.HasCapture,Is.False);Assert.That(StateJson(),Is.EqualTo(before));
                }
                Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
            }
            finally
            {
                if(card!=null)card.gameObject.SetActive(true);if(pose!=null)pose.parent.parent.gameObject.SetActive(true);
                backdrop.Clear();UnityEngine.Object.Destroy(host);app.enabled=enabled;app.table.Paused=paused;
            }
            yield return null;yield return null;
            Assert.That(host==null,Is.True);Assert.That(owned,Is.Not.Empty);Assert.That(owned.All(m=>m==null),Is.True,"Destroy releases snapshot-owned materials.");
        }

        [UnityTest] public IEnumerator PLAY_ART07_SETTLE_BACKDROP_CommitAndColdRecoveryShowReadonlyBoardWithoutCommandsOrReplay()
        {
            StartRun("70932");bool enabled=app.enabled;app.enabled=false;
            try
            {
                foreach(int font in new[]{100,125,150})
                {
                    PrepareRealSettlement07(font);SettlementTick07(.74f);
                    Assert.That(app.SettlementBackdropVisible07,Is.True);Assert.That(app.SettlementBackdropFromHistory07,Is.False);
                    Assert.That(app.SettlementBackdropGraphicCount07,Is.GreaterThan(0));Assert.That(app.SettlementBackdropDiceMeshCount07,Is.GreaterThan(0));
                    var backdrop=FindActive<Art07SettlementBackdrop>("SettlementBackdrop07");
                    Assert.That(backdrop.transform.GetSiblingIndex(),Is.LessThan(FindActive<RectTransform>("Screen").GetSiblingIndex()));
                    Assert.That(backdrop.GetComponentsInChildren<UnityEngine.UI.Selectable>(true),Is.Empty);
                    string state=StateJson();var bytes=File.ReadAllBytes(app.SaveLocation);long sequence=app.Session.ActionSequence;
                    yield return app.StartCoroutine(CaptureEdge07("settlement-backdrop-live-font"+font));
                    app.Refresh();Assert.That(app.SettlementBackdropFromHistory07,Is.False);
                    var rejected=app.Send(new SessionCommand{kind=CommandKind.Submit});Assert.That(rejected.accepted,Is.False);
                    Assert.That(StateJson(),Is.EqualTo(state));Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(app.SaveLocation));
                    Assert.That(app.SettlementBackdropVisible07,Is.True);
                    long gold=app.Session.State.gold;Click("SettlementContinue");Assert.That(app.Session.State.gold,Is.EqualTo(gold));Assert.That(app.SettlementBackdropVisible07,Is.False);
                }
                PrepareRealSettlement07(150);SettlementTick07(.74f);
                string savedState=StateJson();var saved=File.ReadAllBytes(app.SaveLocation);var savedSubmission=app.Session.State.lastSubmission.snapshot.Clone();
                PlayerPrefs.SetInt("art07.textScale",150);
                app.enabled=enabled;yield return LoadBootstrap();
                Assert.That(UiKit.FontScale,Is.EqualTo(1.5f),"The cold restart must restore the actual 150% preference.");
                Assert.That(app.UnlockDebug("XD is god"),Is.True);
                Assert.That(app.ResumeDebugWorkspace(),Is.True);if(HasActive("RunDebugScene"))Click("RunDebugScene");else Click("CloseDialog");
                Assert.That(app.Session.State.phase,Is.EqualTo("StageSettlement"));Assert.That(app.SettlementRevealPlaying07||app.PresentationPlaying07,Is.False);
                Assert.That(app.SettlementBackdropVisible07&&app.SettlementBackdropFromHistory07,Is.True);
                Assert.That(StateJson(),Is.EqualTo(savedState));CollectionAssert.AreEqual(saved,File.ReadAllBytes(app.SaveLocation));
                var frozen=GameObject.Find("SettlementFrozenDice07");Assert.That(frozen,Is.Not.Null);Assert.That(frozen.GetComponentsInChildren<Collider>(true),Is.Empty);
                for(int i=0;i<Math.Min(8,savedSubmission.dice.Length);i++)
                {
                    var die=savedSubmission.dice[i];string name="$static-preview:settlement-locked-"+i+"/Face_"+die.faceId;
                    var top=frozen.GetComponentsInChildren<MeshFilter>(false).Single(f=>f.name==name);
                    Assert.That(top.sharedMesh.name,Does.StartWith("Pips_"+die.currentPip+"_"),"Restore uses recorded current face without drawing again.");
                }
                yield return app.StartCoroutine(CaptureEdge07("settlement-backdrop-cold-font150"));
                Click("Pause");Click("MainMenu");Assert.That(app.SettlementBackdropVisible07,Is.False);
            }
            finally{if(app!=null)app.enabled=enabled;}
        }
        private IEnumerator SettlementBackdropGpu07(RectTransform card,Transform pose,string scope,Art07SettlementBackdrop backdrop)
        {
            bool reduced=false;var originalDie=pose.parent.parent.gameObject;
            var labels=card.GetComponentsInChildren<TextMeshProUGUI>(false).Where(t=>!string.IsNullOrWhiteSpace(t.text)).ToArray();
            Assert.That(labels,Is.Not.Empty);var colors=labels.Select(t=>t.color).ToArray();
            string state=GameSession.StateHash(app.Session.State);byte[] save=File.ReadAllBytes(app.SaveLocation);
            string output=Path.Combine(saveDirectory,"art07-settlement-backdrop-gpu",scope+"-"+(reduced?"reduced":"normal")+"-"+Time.frameCount);Directory.CreateDirectory(output);
            var report=new TransactionGlyphReport07{scope=scope,reduced=reduced,width=Screen.width,height=Screen.height,stateBefore=state,output=output,runtimeMvid=typeof(GameApp).Module.ModuleVersionId.ToString(),testMvid=GetType().Module.ModuleVersionId.ToString()};
            var corners=new List<Vector2>();
            foreach(var renderer in pose.GetComponentsInChildren<MeshRenderer>(false).Where(r=>r.enabled))
            {
                Bounds bounds=renderer.bounds;
                for(int i=0;i<8;i++){var v=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));corners.Add(app.table.viewCamera.WorldToScreenPoint(v));}
            }
            Assert.That(corners,Is.Not.Empty);var dieRect=Rect.MinMaxRect(corners.Min(v=>v.x),corners.Min(v=>v.y),corners.Max(v=>v.x),corners.Max(v=>v.y));
            Texture2D blank=null,original=null,snapshot=null;
            bool done=false;Exception error=null;
            void Run(Action action){try{action();}catch(Exception ex){if(error==null)error=ex;}}
            void RestoreText(){for(int i=0;i<labels.Length;i++)if(labels[i]!=null){labels[i].color=colors[i];labels[i].ForceMeshUpdate();}Canvas.ForceUpdateCanvases();}
            IEnumerator Capture()
            {
                try
                {
                    Run(()=>{originalDie.SetActive(false);foreach(var label in labels){var c=label.color;c.a=0;label.color=c;label.ForceMeshUpdate();}Canvas.ForceUpdateCanvases();});if(error!=null)yield break;
                    yield return new WaitForEndOfFrame();Run(()=>blank=ScreenCapture.CaptureScreenshotAsTexture());
                    Run(()=>{originalDie.SetActive(true);RestoreText();});if(error!=null)yield break;
                    yield return new WaitForEndOfFrame();Run(()=>original=ScreenCapture.CaptureScreenshotAsTexture());
                    Run(()=>
                    {
                        foreach(var label in labels)
                        {
                            label.ForceMeshUpdate();var rects=ScoreGlyphRects07(label);int index=0;
                            for(int i=0;i<label.textInfo.characterCount;i++)
                            {
                                var glyph=label.textInfo.characterInfo[i];if(char.IsWhiteSpace(glyph.character))continue;
                                var r=rects[index++];float a=label.transform.TransformPoint(new Vector3(glyph.origin,0,0)).x,b=label.transform.TransformPoint(new Vector3(glyph.xAdvance,0,0)).x;
                                r.xMin=Mathf.Max(r.xMin,Mathf.Min(a,b));r.xMax=Mathf.Min(r.xMax,Mathf.Max(a,b));
                                report.glyphs.Add(new TransactionGlyph07{label=label.name,character=glyph.character.ToString(),font=glyph.fontAsset.name,materialIndex=glyph.materialReferenceIndex,rect=r});
                            }
                        }
                        backdrop.Capture(new[]{card},new[]{pose},app.table.transform);report.snapshotMeshes=backdrop.GraphicCount;Assert.That(backdrop.HasCapture,Is.True);
                        card.gameObject.SetActive(false);originalDie.SetActive(false);backdrop.Show();
                        // No Tick or rule command: compare the stationary initial image,
                        // before normal/reduced fade can hide the regression.
                        Canvas.ForceUpdateCanvases();
                    });if(error!=null)yield break;
                    yield return new WaitForEndOfFrame();Run(()=>snapshot=ScreenCapture.CaptureScreenshotAsTexture());
                }
                finally{done=true;}
            }
            var worker=app.StartCoroutine(Capture());double deadline=Time.realtimeSinceStartupAsDouble+10;
            while(!done&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            try
            {
                if(!done){app.StopCoroutine(worker);Assert.Fail("Real settlement backdrop GPU frames did not complete within10s; no synthetic frame substituted.");}
                if(error!=null)throw new InvalidOperationException("Settlement backdrop GPU capture failed",error);
                foreach(var image in new[]{blank,original,snapshot}){Assert.That(image,Is.Not.Null);Assert.That(image.width,Is.EqualTo(report.width));Assert.That(image.height,Is.EqualTo(report.height));}
                File.WriteAllBytes(Path.Combine(output,"blank.png"),blank.EncodeToPNG());File.WriteAllBytes(Path.Combine(output,"source.png"),original.EncodeToPNG());File.WriteAllBytes(Path.Combine(output,"snapshot.png"),snapshot.EncodeToPNG());
                var bg=blank.GetPixels32();var src=original.GetPixels32();var dst=snapshot.GetPixels32();
                Assert.That(report.glyphs.Count,Is.GreaterThan(0));
                foreach(var glyph in report.glyphs)
                {
                    double total=0;
                    for(int y=Mathf.Max(0,Mathf.FloorToInt(glyph.rect.yMin));y<Mathf.Min(report.height,Mathf.CeilToInt(glyph.rect.yMax));y++)
                    for(int x=Mathf.Max(0,Mathf.CeilToInt(glyph.rect.xMin));x<Mathf.Min(report.width,Mathf.CeilToInt(glyph.rect.xMax));x++)
                    {
                        int i=y*report.width+x,delta=ScorePixelDistance07(src[i],dst[i]);glyph.comparedPixels++;total+=delta;
                        if(ScorePixelDistance07(src[i],bg[i])>=8)glyph.inkPixels++;
                        if(delta>8)glyph.mismatchPixels++;
                    }
                    glyph.meanError=glyph.comparedPixels==0?255:(float)(total/glyph.comparedPixels);
                    Assert.That(glyph.inkPixels,Is.GreaterThan(0),glyph.label+" source glyph has no actual GPU ink: "+glyph.character);
                    Assert.That(glyph.meanError,Is.LessThanOrEqualTo(2f),glyph.label+" snapshot/source RGB error: "+glyph.character);
                    Assert.That(glyph.mismatchPixels/(float)glyph.comparedPixels,Is.LessThanOrEqualTo(.02f),glyph.label+" snapshot must preserve actual glyph shape, not render its quad: "+glyph.character);
                }
                int diePixels=0,dieInk=0,dieMismatch=0;double dieError=0;
                for(int y=Mathf.Max(0,Mathf.FloorToInt(dieRect.yMin));y<Mathf.Min(report.height,Mathf.CeilToInt(dieRect.yMax));y++)
                for(int x=Mathf.Max(0,Mathf.FloorToInt(dieRect.xMin));x<Mathf.Min(report.width,Mathf.CeilToInt(dieRect.xMax));x++)
                {int i=y*report.width+x,delta=ScorePixelDistance07(src[i],dst[i]);diePixels++;dieError+=delta;if(delta>8)dieMismatch++;if(ScorePixelDistance07(src[i],bg[i])>=8)dieInk++;}
                Assert.That(dieInk,Is.GreaterThan(50),"The physical die must have actual GPU pixels, not just render objects.");
                Assert.That(dieError/Math.Max(1,diePixels),Is.LessThanOrEqualTo(2f),"Frozen die pose/material must preserve actual GPU appearance.");
                Assert.That(dieMismatch/(float)Math.Max(1,diePixels),Is.LessThanOrEqualTo(.02f));
                File.WriteAllText(Path.Combine(output,"dice-pixels.json"),JsonUtility.ToJson(new BackdropDiceGpu07{rect=dieRect,compared=diePixels,visibleInk=dieInk,mismatch=dieMismatch,meanError=(float)(dieError/Math.Max(1,diePixels))},true));
                report.stateAfter=GameSession.StateHash(app.Session.State);Assert.That(report.stateAfter,Is.EqualTo(state));CollectionAssert.AreEqual(save,File.ReadAllBytes(app.SaveLocation));report.passed=true;
            }
            finally
            {
                backdrop.Clear();card.gameObject.SetActive(true);originalDie.SetActive(true);RestoreText();
                File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));TestContext.WriteLine("Real source/snapshot glyph GPU evidence: "+output);
                foreach(var image in new[]{blank,original,snapshot})if(image!=null)UnityEngine.Object.Destroy(image);
            }
        }
    }
}
