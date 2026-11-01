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
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        [Serializable] private sealed class BreakdownRow07
        {
            public string name,text;public Rect bounds,ink;
            public int expectedCharacters,renderedCharacters;public float fontSize,preferredHeight;
        }
        [Serializable] private sealed class BreakdownLayoutReport07
        {
            public string id,sourceManifestSha256,runtimeMvid,testMvid,stateHash,saveSha;
            public int width,height,fontPercent,scrollEvents,pages;public long sequence;
            public List<BreakdownRow07> rows=new List<BreakdownRow07>();
            public List<string> gpuVerifiedRows=new List<string>();
        }

        [UnityTest] public IEnumerator PLAY_ART07_BreakdownEverySourceFitsAndWheelRevealsRealGlyphsFont100()=>BreakdownSourcesAtFont07(100);
        [UnityTest] public IEnumerator PLAY_ART07_BreakdownEverySourceFitsAndWheelRevealsRealGlyphsFont125()=>BreakdownSourcesAtFont07(125);
        [UnityTest] public IEnumerator PLAY_ART07_BreakdownEverySourceFitsAndWheelRevealsRealGlyphsFont150()=>BreakdownSourcesAtFont07(150);
        private IEnumerator BreakdownSourcesAtFont07(int requestedFont)
        {
            Assert.That(Screen.width==1280&&Screen.height==720||Screen.width==1920&&Screen.height==1080,Is.True,
                "Run once at real 1280x720 and once at real 1920x1080; no substituted viewport.");
            yield return ExtraBegin07();int width=Screen.width,height=Screen.height;
            foreach(int font in new[]{requestedFont})
            {
                yield return CardSetFont07(font);if(app.Session.State==null)StartRun("70827");
                foreach(var kind in new[]{Art07FixtureKind.MaximumTable,Art07FixtureKind.Debug32})
                {
                    var scene=InformationScene07(kind);scene.randomOverrides.Clear();ApplyInformationScene07(scene);yield return ExtraIdle07();
                    var snapshot=app.Session.PreviewSnapshot();var score=app.Session.PreviewScoreRange().minimum;
                    string state=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);long sequence=app.Session.ActionSequence;
                    yield return LegacyClick07("Factor4");yield return ExtraIdle07();
                    yield return LegacyClick07("FullScoreDetails");yield return ExtraIdle07();
                    yield return BreakdownWalk07(kind+"-font"+font,snapshot,score);
                    yield return LegacyClick07("CloseDialog");yield return ExtraIdle07();
                    Assert.That(StateJson(),Is.EqualTo(state));Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
                    Assert.That(Screen.width,Is.EqualTo(width));Assert.That(Screen.height,Is.EqualTo(height));
                }
            }
            ExtraFinish07();
        }

        [UnityTest]
        public IEnumerator PLAY_ART07_BreakdownHistoricalEffectsRemainCompleteAndReadonlyAtFont150()
        {
            Assert.That(Screen.width==1280&&Screen.height==720||Screen.width==1920&&Screen.height==1080,Is.True);
            yield return ExtraBegin07();yield return CardSetFont07(150);StartRun("70828");
            var scene=InformationScene07(Art07FixtureKind.PermanentPost);ApplyInformationScene07(scene);yield return ExtraIdle07();
            long before=app.Session.ActionSequence;yield return LegacyClick07("Submit");yield return ExtraIdle07();
            Assert.That(app.Session.ActionSequence,Is.EqualTo(before+1),"One actual pointer click submits exactly once.");
            var receipt=app.Session.State.processedReceipts.Last(r=>r.command.kind==CommandKind.Submit);
            Assert.That(receipt.effects.Any(e=>e.kind=="FaceValueChanged"),Is.True);
            Assert.That(receipt.effects.Any(e=>e.kind=="ControllerDestroyed"),Is.True);
            var history=app.Session.State.lastSubmission;Assert.That(history,Is.Not.Null);
            string state=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);long sequence=app.Session.ActionSequence;
            yield return LegacyClick07("PreviousSubmission");yield return ExtraIdle07();
            yield return LegacyClick07("FullScoreDetails");yield return ExtraIdle07();
            Assert.That(FindActive<TextMeshProUGUI>("Triggered").text,Does.Contain("实际提交后效果："));
            Assert.That(FindActive<TextMeshProUGUI>("Triggered").text,Does.Contain("永久点数："));
            Assert.That(FindActive<TextMeshProUGUI>("Triggered").text,Does.Contain("本次计分后自毁"));
            yield return BreakdownWalk07("historical-permanent-effects-font150",history.snapshot,history.breakdown);
            yield return LegacyClick07("CloseDialog");yield return ExtraIdle07();
            Assert.That(StateJson(),Is.EqualTo(state));Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
            ExtraFinish07();
        }

        private IEnumerator BreakdownWalk07(string id,SubmissionSnapshot snapshot,ScoreBreakdown score)
        {
            var trace=ScoreExplanation.Build(snapshot,score,app.Config);
            var scroll=FindActive<TextMeshProUGUI>("Formula").GetComponentInParent<UnityEngine.UI.ScrollRect>();Assert.That(scroll,Is.Not.Null);
            var expected=new List<string>{"Formula"};
            for(int i=0;i<snapshot.dice.Length;i++)
            {
                expected.Add("DieLine"+i);for(int j=0;j<trace.dice[i].steps.Length;j++)expected.Add("DieStep"+i+"_"+j);
                var die=snapshot.dice[i];var face=die.permanentFacesCopy.Single(f=>f.faceId==die.faceId);
                Assert.That(FindActive<TextMeshProUGUI>("DieLine"+i).text,Is.EqualTo(
                    $"{i+1}. {die.dieId}/{die.faceId}  永久 {face.permanentPip} → 当前 {die.currentPip} → 有效 {score.effectivePips[i]}  /  {(score.effectiveColors[i]=="red"?"红 R":"蓝 B")}\n永久六面和 {die.permanentSixFaceSum}  ·  本骰点数贡献 {score.pipContributions[i]}"));
            }
            expected.Add("GlobalTitle");for(int i=0;i<trace.global.Length;i++)expected.Add("GlobalStep"+i);expected.Add("Triggered");
            var labels=scroll.content.GetComponentsInChildren<TextMeshProUGUI>(false).OrderBy(t=>-t.rectTransform.anchoredPosition.y).ToArray();
            CollectionAssert.AreEqual(expected,labels.Select(t=>t.name).ToArray(),"Every actual per-die/global source remains present in its original order.");
            var report=new BreakdownLayoutReport07{id=id,sourceManifestSha256=extra07.sourceManifestSha256,runtimeMvid=extra07.runtimeMvid,testMvid=extra07.testMvid,
                stateHash=ExtraStateHash07(),saveSha=ExtraFileHash07(app.SaveLocation),sequence=app.Session.ActionSequence,width=Screen.width,height=Screen.height,fontPercent=Mathf.RoundToInt(UiKit.FontScale*100)};
            string state=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);
            string path=Path.Combine(extra07.output,id+"-rows.json");
            try
            {
                float lastBottom=0;
                foreach(var label in labels)
                {
                    label.ForceMeshUpdate();var row=new BreakdownRow07{name=label.name,text=label.text,fontSize=label.fontSize,preferredHeight=label.GetPreferredValues(label.text,label.rectTransform.rect.width,float.PositiveInfinity).y,
                        bounds=CardLocalRect07(scroll.content,label.rectTransform),expectedCharacters=CardWithoutSpace07(label.text).Length};
                    var vertices=new List<Vector3>();var characters=new List<char>();
                    for(int i=0;i<label.textInfo.characterCount;i++)
                    {
                        var glyph=label.textInfo.characterInfo[i];if(char.IsWhiteSpace(glyph.character))continue;
                        Assert.That(glyph.isVisible,Is.True,label.name+" hides "+glyph.character);characters.Add(glyph.character);
                        var mesh=label.textInfo.meshInfo[glyph.materialReferenceIndex];
                        for(int v=0;v<4;v++)vertices.Add(scroll.content.InverseTransformPoint(label.transform.TransformPoint(mesh.vertices[glyph.vertexIndex+v])));
                    }
                    row.renderedCharacters=characters.Count;
                    Assert.That(new string(characters.ToArray()),Is.EqualTo(CardWithoutSpace07(label.text)),label.name+" lost complete source text");
                    Assert.That(label.isTextTruncated,Is.False,label.name);Assert.That(label.isTextOverflowing,Is.False,label.name);
                    Assert.That(row.bounds.height,Is.GreaterThanOrEqualTo(row.preferredHeight-.1f),label.name+" must use actual scaled Noto height");
                    Assert.That(row.bounds.yMin,Is.GreaterThanOrEqualTo(lastBottom),label.name+" overlaps the previous source row");
                    lastBottom=row.bounds.yMax;
                    Assert.That(vertices,Is.Not.Empty);row.ink=Rect.MinMaxRect(vertices.Min(v=>v.x),-vertices.Max(v=>v.y),vertices.Max(v=>v.x),-vertices.Min(v=>v.y));
                    Assert.That(ScoreContains07(row.bounds,row.ink),Is.True,label.name+" painted ink leaves its own source row");
                    Assert.That(row.bounds.yMax,Is.LessThanOrEqualTo(scroll.content.rect.height),label.name+" cannot scroll to its entire height");
                    report.rows.Add(row);
                }
                File.WriteAllText(path,JsonUtility.ToJson(report,true));
                var checkedGlyphs=labels.ToDictionary(label=>label,label=>new HashSet<int>());
                var expectedGlyphs=labels.ToDictionary(label=>label,label=>Enumerable.Range(0,label.textInfo.characterCount).Where(i=>!char.IsWhiteSpace(label.textInfo.characterInfo[i].character)).ToArray());
                int wheelBudget=Mathf.CeilToInt(scroll.content.rect.height/scroll.scrollSensitivity)+4;
                for(int turn=0;turn<=wheelBudget;turn++)
                {
                    yield return null;Canvas.ForceUpdateCanvases();Rect viewport=V06ScreenRect(scroll.viewport);
                    var ready=new Dictionary<TextMeshProUGUI,int[]>();
                    foreach(var label in labels)
                    {
                        var indices=expectedGlyphs[label].Where(i=>!checkedGlyphs[label].Contains(i)&&ScoreContains07(viewport,BreakdownGlyphRect07(label,i))).ToArray();
                        if(indices.Length>0)ready.Add(label,indices);
                    }
                    if(ready.Count>0)
                    {
                        // Capture only fully exposed glyphs without changing any text, rect, clipping or source order.
                        // This also handles effects footers taller than a viewport by reading their real successive pages.
                        yield return BreakdownGpuCapture07(ready);
                        foreach(var pair in ready)foreach(int i in pair.Value)checkedGlyphs[pair.Key].Add(i);
                        report.gpuVerifiedRows=labels.Where(label=>checkedGlyphs[label].Count==expectedGlyphs[label].Length).Select(label=>label.name).ToList();
                        yield return ExtraCapture07(id+"-page"+report.pages.ToString("D2"),"S07","Real wheel traversal; every source glyph verified with counterfactual GPU pixels across its exposed pages.");report.pages++;
                    }
                    Assert.That(StateJson(),Is.EqualTo(state));Assert.That(app.Session.ActionSequence,Is.EqualTo(report.sequence));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
                    File.WriteAllText(path,JsonUtility.ToJson(report,true));if(report.gpuVerifiedRows.Count==labels.Length)break;
                    Assert.That(turn,Is.LessThan(wheelBudget),"All source rows, including the bottom effects footer, must become fully visible.");
                    float before=scroll.content.anchoredPosition.y;
                    var pointer=new PointerEventData(EventSystem.current){position=viewport.center,scrollDelta=new Vector2(0,-1)};
                    var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
                    Assert.That(hits,Is.Not.Empty);Assert.That(hits[0].gameObject.GetComponentInParent<UnityEngine.UI.ScrollRect>(),Is.SameAs(scroll),"The actual scroll viewport must receive the wheel.");
                    ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.scrollHandler);report.scrollEvents++;
                    yield return null;Canvas.ForceUpdateCanvases();
                    Assert.That(scroll.content.anchoredPosition.y,Is.GreaterThan(before+.01f),"Wheel must advance the actual source content until the last unchecked row is reachable.");
                }
                CollectionAssert.AreEquivalent(expected,report.gpuVerifiedRows);Assert.That(report.scrollEvents,Is.GreaterThan(0));
            }
            finally{File.WriteAllText(path,JsonUtility.ToJson(report,true));}
        }

        private static Rect BreakdownGlyphRect07(TextMeshProUGUI label,int index)
        {
            var glyph=label.textInfo.characterInfo[index];var mesh=label.textInfo.meshInfo[glyph.materialReferenceIndex];
            var points=Enumerable.Range(0,4).Select(v=>label.transform.TransformPoint(mesh.vertices[glyph.vertexIndex+v])).ToArray();
            return Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));
        }
        private IEnumerator BreakdownGpuCapture07(Dictionary<TextMeshProUGUI,int[]> exposed)
        {
            var labels=exposed.Keys.ToArray();var colors=labels.Select(t=>t.color).ToArray();
            string state=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);long sequence=app.Session.ActionSequence;
            var report=new ScorePixelReport07{width=Screen.width,height=Screen.height,fontScale=UiKit.FontScale,runtimeMvid=extra07.runtimeMvid,testMvid=extra07.testMvid,stateBefore=state,saveExistedBefore=true};
            bool done=false;Exception error=null;Texture2D hiddenA=null,hiddenB=null,visible=null;
            void Run(Action action){try{action();}catch(Exception ex){if(error==null)error=ex;}}
            void Restore(){for(int i=0;i<labels.Length;i++)if(labels[i]!=null){labels[i].color=colors[i];labels[i].ForceMeshUpdate();}Canvas.ForceUpdateCanvases();}
            IEnumerator Capture()
            {
                try
                {
                    Run(()=>{foreach(var label in labels){var c=label.color;c.a=0;label.color=c;label.ForceMeshUpdate();}Canvas.ForceUpdateCanvases();});if(error!=null)yield break;
                    yield return new WaitForEndOfFrame();Run(()=>{hiddenA=ScreenCapture.CaptureScreenshotAsTexture();report.hiddenFrameA=Time.frameCount;});if(error!=null)yield break;
                    yield return new WaitForEndOfFrame();Run(()=>{hiddenB=ScreenCapture.CaptureScreenshotAsTexture();report.hiddenFrameB=Time.frameCount;});
                    Run(Restore);if(error!=null)yield break;
                    yield return new WaitForEndOfFrame();Run(()=>{visible=ScreenCapture.CaptureScreenshotAsTexture();report.visibleFrame=Time.frameCount;});
                }
                finally{Run(Restore);done=true;}
            }
            var worker=app.StartCoroutine(Capture());float deadline=Time.realtimeSinceStartup+10f;
            while(!done&&Time.realtimeSinceStartup<deadline)yield return null;
            string path=Path.Combine(extra07.output,"breakdown-pixels-frame"+report.visibleFrame);
            try
            {
                if(!done){app.StopCoroutine(worker);Assert.Fail("Three real GPU captures did not finish within 10s.");}
                if(error!=null)throw new InvalidOperationException("Breakdown GPU capture failed",error);
                foreach(var texture in new[]{hiddenA,hiddenB,visible}){Assert.That(texture,Is.Not.Null);Assert.That(texture.width,Is.EqualTo(report.width));Assert.That(texture.height,Is.EqualTo(report.height));}
                var a=hiddenA.GetPixels32();var b=hiddenB.GetPixels32();var ink=visible.GetPixels32();
                foreach(var pair in exposed)
                {
                    var label=pair.Key;label.ForceMeshUpdate();Assert.That(label.canvasRenderer.cull,Is.False,label.name);
                    foreach(int index in pair.Value)
                    {
                        var glyph=label.textInfo.characterInfo[index];Assert.That(glyph.isVisible,Is.True,label.name);Assert.That(glyph.textElement.unicode,Is.EqualTo((uint)glyph.character),label.name+" substituted glyph");
                        var mesh=label.textInfo.meshInfo[glyph.materialReferenceIndex];Assert.That(mesh.material.mainTexture,Is.Not.Null);
                        if(glyph.materialReferenceIndex>0)
                        {
                            var child=label.GetComponentsInChildren<TMP_SubMeshUI>(true).Single(m=>m.mesh==mesh.mesh);
                            Assert.That(child.gameObject.activeInHierarchy&&child.enabled&&!child.canvasRenderer.cull,Is.True,label.name+" inactive fallback mesh");
                        }
                        for(int v=0;v<4;v++)Assert.That(mesh.colors32[glyph.vertexIndex+v].a,Is.GreaterThan(0));
                        Rect rect=BreakdownGlyphRect07(label,index);var viewport=label.GetComponentInParent<UnityEngine.UI.ScrollRect>().viewport;
                        Assert.That(ScoreContains07(V06ScreenRect(viewport),rect),Is.True,label.name+" glyph moved outside viewport during capture");
                        float origin=label.transform.TransformPoint(new Vector3(glyph.origin,0,0)).x,advance=label.transform.TransformPoint(new Vector3(glyph.xAdvance,0,0)).x;
                        rect.xMin=Mathf.Max(rect.xMin,Mathf.Min(origin,advance));rect.xMax=Mathf.Min(rect.xMax,Mathf.Max(origin,advance));
                        var row=new ScoreGlyphPixel07{label=label.name+"["+index+"]",character=glyph.character.ToString(),unicode=glyph.character,materialIndex=glyph.materialReferenceIndex,sampleRect=rect};
                        int x0=Mathf.Clamp(Mathf.CeilToInt(rect.xMin),0,report.width-1),x1=Mathf.Clamp(Mathf.FloorToInt(rect.xMax),0,report.width-1);
                        int y0=Mathf.Clamp(Mathf.CeilToInt(rect.yMin),0,report.height-1),y1=Mathf.Clamp(Mathf.FloorToInt(rect.yMax),0,report.height-1);
                        for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                        {
                            int p=y*report.width+x,delta=ScorePixelDistance07(ink[p],b[p]);
                            row.maxHiddenDelta=Math.Max(row.maxHiddenDelta,ScorePixelDistance07(a[p],b[p]));row.maxForegroundDelta=Math.Max(row.maxForegroundDelta,delta);if(delta>=2)row.changedPixels++;
                        }
                        report.glyphs.Add(row);
                        Assert.That(row.maxHiddenDelta,Is.LessThanOrEqualTo(1),row.label+" background must stay stable while testing real ink");
                        Assert.That(row.changedPixels,Is.GreaterThan(0),row.label+" has no GPU ink inside its own character advance");
                    }
                }
                report.stateAfter=StateJson();report.saveExistedAfter=File.Exists(app.SaveLocation);
                Assert.That(report.stateAfter,Is.EqualTo(state));Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
            }
            catch
            {
                if(visible!=null)File.WriteAllBytes(path+"-visible.png",visible.EncodeToPNG());if(hiddenA!=null)File.WriteAllBytes(path+"-hidden-repeat.png",hiddenA.EncodeToPNG());if(hiddenB!=null)File.WriteAllBytes(path+"-hidden.png",hiddenB.EncodeToPNG());throw;
            }
            finally{File.WriteAllText(path+".json",JsonUtility.ToJson(report,true));Restore();foreach(var texture in new[]{hiddenA,hiddenB,visible})if(texture!=null)UnityEngine.Object.Destroy(texture);}
        }
    }
}
