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
using UnityEngine.TextCore.LowLevel;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        [UnityTest]
        public IEnumerator PLAY_ART07_ScoreLabelsPreviewMixedZeroAndRandomKeepEveryGlyphAfterRedraw()
        {
            foreach(int font in new[]{100,125,150})
            {
                yield return ScoreLabelBegin07(font);
                foreach(var kind in new[]{Art07FixtureKind.MaximumTable,Art07FixtureKind.ZeroMultiplier,Art07FixtureKind.SharedHalf,Art07FixtureKind.Debug32})
                {
                    ScoreLabelApply07(kind,true);yield return new WaitForSecondsRealtime(.25f);
                    string state=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);
                    for(int redraw=0;redraw<3;redraw++)
                    {
                        app.Refresh();yield return null;
                        var hand=FindActive<TextMeshProUGUI>("Hand");
                        if(kind==Art07FixtureKind.MaximumTable){Assert.That(hand.text,Does.Contain("＋顺子"));Assert.That(hand.text,Does.Contain("混色"));}
                        if(kind==Art07FixtureKind.SharedHalf)Assert.That(hand.text,Does.Contain("随机结果范围"));
                        var labels=new List<TextMeshProUGUI>{hand,FindActive<TextMeshProUGUI>("FinalScore")};
                        for(int i=0;i<6;i++)labels.Add(Button("Factor"+i).GetComponentInChildren<TextMeshProUGUI>());
                        yield return ScoreLabelGpuCapture07(labels);
                        Rect panel=V06ScreenRect((RectTransform)hand.transform.parent),factor=V06ScreenRect((RectTransform)Button("Factor0").transform);
                        foreach(var label in labels)
                        {
                            foreach(var glyph in ScoreGlyphRects07(label))
                            {
                                Assert.That(ScoreContains07(panel,glyph),Is.True,label.name+" leaves the scoring panel");
                                if(label==hand||label.name=="FinalScore")Assert.That(glyph.yMin,Is.GreaterThanOrEqualTo(factor.yMax-1),label.name+" covers multiplier buttons");
                                else Assert.That(ScoreContains07(V06ScreenRect((RectTransform)label.transform.parent),glyph),Is.True,label.transform.parent.name+" glyph exceeds its own button");
                            }
                        }
                    }
                    yield return CaptureEdge07("score-glyphs-"+kind+"-font"+font);
                    if(kind==Art07FixtureKind.Debug32)
                    {
                        var exact=app.Session.PreviewScoreRange().minimum;
                        Assert.That(exact.controllerMultiplierNumerator,Is.EqualTo("1853020188851841"));
                        Assert.That(Button("Factor4").GetComponentInChildren<TextMeshProUGUI>().text,Is.EqualTo("控制器 ×≈1.85e15"));
                        Click("Factor4");yield return new WaitForSecondsRealtime(.2f);
                        Click("FullScoreDetails");yield return new WaitForSecondsRealtime(.2f);
                        var formula=FindActive<TextMeshProUGUI>("Formula");
                        Assert.That(formula.text,Does.Contain("控制器 "+exact.controllerMultiplierNumerator+"/"+exact.controllerMultiplierDenominator));
                        var scroll=formula.GetComponentInParent<UnityEngine.UI.ScrollRect>();Assert.That(scroll,Is.Not.Null);
                        Rect viewport=V06ScreenRect(scroll.viewport);foreach(var glyph in ScoreGlyphRects07(formula))Assert.That(ScoreContains07(viewport,glyph),Is.True,"Exact formula must remain fully readable in its viewport");
                        yield return ScoreLabelGpuCapture07(new[]{formula});yield return CaptureEdge07("score-exact-controller-factor-font"+font);
                        Click("CloseDialog");
                    }
                    Assert.That(StateJson(),Is.EqualTo(state));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
                }
            }
        }

        [UnityTest]
        public IEnumerator PLAY_ART07_ScoreLabelsFallbackSubmeshSurvivesPooledReuseWithRealPixels()
        {
            yield return ScoreLabelBegin07(100);
            TMP_FontAsset primary=null,fallback=null;RectTransform probe=null;TMP_FontAsset prior=UiKit.Font;
            try
            {
                Font source=app.uiFont.sourceFontFile;
#if UNITY_EDITOR
                if(source==null)source=UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/Fonts/NotoSansCJKsc-Regular.otf");
#endif
                Assert.That(source,Is.Not.Null,"Fallback regression needs the actual Noto source Font; static TMP assets need not retain sourceFontFile at runtime.");
                primary=TMP_FontAsset.CreateFontAsset(source,48,7,GlyphRenderMode.SDFAA,512,512,AtlasPopulationMode.Dynamic,true);
                fallback=TMP_FontAsset.CreateFontAsset(source,48,7,GlyphRenderMode.SDFAA,512,512,AtlasPopulationMode.Dynamic,true);
                primary.name="ScoreProbe_ASCII_Only";fallback.name="ScoreProbe_RealFallback";
                Assert.That(primary.TryAddCharacters("H 78",out string missingMain),Is.True,missingMain);
                Assert.That(fallback.TryAddCharacters("＋混",out string missingFallback),Is.True,missingFallback);
                primary.atlasPopulationMode=fallback.atlasPopulationMode=AtlasPopulationMode.Static;
                primary.fallbackFontAssetTable=new List<TMP_FontAsset>{fallback};UiKit.Font=primary;
                Assert.That(primary.HasCharacter('混',false,false),Is.False,"This regression must exercise the fallback rather than warmed production glyphs.");
                probe=UiKit.Box(app.canvas.transform,"ScoreFallbackProbe07",400,450,1120,180,UiKit.Ink);
                foreach(int font in new[]{100,125,150})
                {
                    UiKit.FontScale=font/100f;
                    for(int redraw=0;redraw<3;redraw++)
                    {
                        UiKit.Clear(probe);
                        var text=UiKit.Text(probe,"ProbeText","H＋混 "+(redraw==2?"8":"7"),24,24,1072,132,32,UiKit.Paper);
                        text.ForceMeshUpdate();yield return null;
                        Assert.That(text.textInfo.characterInfo.Any(c=>c.character=='混'&&c.fontAsset==fallback),Is.True);
                        Assert.That(text.GetComponentsInChildren<TMP_SubMeshUI>(true).Length,Is.GreaterThan(0));
                        yield return ScoreLabelGpuCapture07(new[]{text});
                    }
                    yield return CaptureEdge07("score-fallback-reuse-font"+font);
                }
            }
            finally
            {
                UiKit.Font=prior;UiKit.FontScale=PlayerPrefs.GetInt("art07.textScale",100)/100f;
                if(probe!=null)UnityEngine.Object.Destroy(probe.gameObject);
                ScoreDestroyTemporaryFont07(primary);ScoreDestroyTemporaryFont07(fallback);
            }
        }

        [UnityTest]
        public IEnumerator PLAY_ART07_ScoreLabelsControllerOverflowHasDedicatedTenthCellAndCompleteLabel()
        {
            foreach(int font in new[]{100,125,150})
            {
                yield return ScoreLabelBegin07(font);
                foreach(var kind in new[]{Art07FixtureKind.MaximumTable,Art07FixtureKind.Debug32})
                {
                    ScoreLabelApply07(kind,false);yield return new WaitForSecondsRealtime(.25f);
                    var cards=app.canvas.GetComponentsInChildren<UnityEngine.UI.Button>(false).Where(b=>b.name.StartsWith("Controller_",StringComparison.Ordinal)).ToArray();
                    Assert.That(cards.Length,Is.EqualTo(kind==Art07FixtureKind.Debug32?9:10));
                    if(kind==Art07FixtureKind.MaximumTable){Assert.That(HasActive("AllControllerStates"),Is.False);continue;}
                    var all=Button("AllControllerStates");var text=all.GetComponentInChildren<TextMeshProUGUI>();
                    Assert.That(text.text,Is.EqualTo("全部 32 件 >"));Assert.That(text.textWrappingMode,Is.EqualTo(TextWrappingModes.NoWrap));
                    Rect rect=V06ScreenRect((RectTransform)all.transform);
                    Assert.That(rect.width,Is.GreaterThanOrEqualTo(39.9));Assert.That(rect.height,Is.GreaterThanOrEqualTo(39.9));
                    foreach(var card in cards)Assert.That(rect.Overlaps(V06ScreenRect((RectTransform)card.transform)),Is.False,"All controllers button covers "+card.name);
                    foreach(var glyph in ScoreGlyphRects07(text))Assert.That(ScoreContains07(rect,glyph),Is.True,"Overflow button loses a label glyph");
                    yield return ScoreLabelGpuCapture07(new[]{text});yield return CaptureEdge07("score-controller-overflow-font"+font);
                    string state=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);Click("AllControllerStates");
                    Assert.That(HasActive("Dialog"),Is.True);Click("CloseDialog");
                    Assert.That(StateJson(),Is.EqualTo(state));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
                }
            }
        }

        private IEnumerator ScoreLabelBegin07(int font)
        {
            PlayerPrefs.SetInt("art07.textScale",font);PlayerPrefs.SetInt("art07.guideSeen",1);
            yield return LoadBootstrap();StartRun("70817");
            Assert.That(app.UnlockDebug("XD is god"),Is.True);Assert.That(app.EnterDebugWorkspace(),Is.True);Click("CloseDialog");
        }
        private void ScoreLabelApply07(Art07FixtureKind kind,bool naturalRandom)
        {
            var state=app.Session.State;var scene=Art07Fixtures.Create(app.Config,kind,state.nextItemSerial,state.nextDieSerial);scene.sourceRunId=state.sourceRunId;
            if(naturalRandom)scene.randomOverrides.Clear();
            var applied=app.Send(new SessionCommand{kind=CommandKind.ApplyDebugScene,debugScene=scene});Assert.That(applied.accepted,Is.True,applied.error);
            if(HasActive("RunDebugScene"))Click("RunDebugScene");if(HasActive("CloseDialog"))Click("CloseDialog");
        }
        private static bool ScoreContains07(Rect outer,Rect inner)=>inner.xMin>=outer.xMin-.75f&&inner.xMax<=outer.xMax+.75f&&inner.yMin>=outer.yMin-.75f&&inner.yMax<=outer.yMax+.75f;
        private static List<Rect> ScoreGlyphRects07(TextMeshProUGUI label)
        {
            label.ForceMeshUpdate();var result=new List<Rect>();
            for(int i=0;i<label.textInfo.characterCount;i++)
            {
                var glyph=label.textInfo.characterInfo[i];if(char.IsWhiteSpace(glyph.character))continue;
                Assert.That(glyph.isVisible,Is.True,label.name+" hidden glyph "+glyph.character);
                var mesh=label.textInfo.meshInfo[glyph.materialReferenceIndex];var points=Enumerable.Range(0,4).Select(v=>label.transform.TransformPoint(mesh.vertices[glyph.vertexIndex+v])).ToArray();
                result.Add(Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y)));
            }
            return result;
        }
        [Serializable] private sealed class ScoreGlyphPixel07
        {
            public string label,character;public int unicode,materialIndex,changedPixels,maxForegroundDelta,maxHiddenDelta;
            public Rect sampleRect;
        }
        [Serializable] private sealed class ScorePixelReport07
        {
            public int width,height,hiddenFrameA,hiddenFrameB,visibleFrame,threshold8Bit=2,allowedHiddenNoise8Bit=1;
            public float fontScale;public bool saveExistedBefore,saveExistedAfter;public string runtimeMvid,testMvid,stateBefore,stateAfter;
            public List<ScoreGlyphPixel07> glyphs=new List<ScoreGlyphPixel07>();
        }
        private IEnumerator ScoreLabelGpuCapture07(IEnumerable<TextMeshProUGUI> labels)
        {
            var values=labels.ToArray();var colors=values.Select(t=>t.color).ToArray();
            string state=StateJson();bool saveExisted=File.Exists(app.SaveLocation);byte[] saved=saveExisted?File.ReadAllBytes(app.SaveLocation):null;
            var report=new ScorePixelReport07{width=Screen.width,height=Screen.height,fontScale=UiKit.FontScale,
                runtimeMvid=typeof(GameApp).Module.ModuleVersionId.ToString(),testMvid=GetType().Module.ModuleVersionId.ToString(),stateBefore=state,saveExistedBefore=saveExisted};
            // Runtime coroutine owns three real end-of-frame captures. The second
            // hidden frame bounds background noise independently from glyph ink.
            bool done=false;Exception error=null;Texture2D hiddenA=null,hiddenB=null,visible=null;
            void Run(Action action){try{action();}catch(Exception ex){if(error==null)error=ex;}}
            void Restore()
            {
                for(int i=0;i<values.Length;i++)if(values[i]!=null){values[i].color=colors[i];values[i].ForceMeshUpdate();}
                Canvas.ForceUpdateCanvases();
            }
            IEnumerator Capture()
            {
                try
                {
                    Run(()=>{foreach(var label in values){var color=label.color;color.a=0;label.color=color;label.ForceMeshUpdate();}Canvas.ForceUpdateCanvases();});
                    if(error!=null)yield break;
                    yield return new WaitForEndOfFrame();Run(()=>{hiddenA=ScreenCapture.CaptureScreenshotAsTexture();report.hiddenFrameA=Time.frameCount;});
                    if(error!=null)yield break;
                    yield return new WaitForEndOfFrame();Run(()=>{hiddenB=ScreenCapture.CaptureScreenshotAsTexture();report.hiddenFrameB=Time.frameCount;});
                    Run(Restore);if(error!=null)yield break;
                    yield return new WaitForEndOfFrame();Run(()=>{visible=ScreenCapture.CaptureScreenshotAsTexture();report.visibleFrame=Time.frameCount;});
                }
                finally{Run(Restore);done=true;}
            }
            var worker=app.StartCoroutine(Capture());float deadline=Time.realtimeSinceStartup+10f;
            while(!done&&Time.realtimeSinceStartup<deadline)yield return null;
            string output=Path.Combine(saveDirectory,"art07-edge-screens");Directory.CreateDirectory(output);
            string path=Path.Combine(output,"score-pixels-frame"+report.visibleFrame);
            try
            {
                if(!done){app.StopCoroutine(worker);Assert.Fail("Three real end-of-frame captures did not complete within10s; no substitute frames were used.");}
                if(error!=null)throw new InvalidOperationException("Real counterfactual GPU capture failed",error);
                foreach(var image in new[]{hiddenA,hiddenB,visible})
                {Assert.That(image,Is.Not.Null);Assert.That(image.width,Is.EqualTo(report.width));Assert.That(image.height,Is.EqualTo(report.height));}
                try
                {
                    ScoreLabelGpuCheck07(values,hiddenA,hiddenB,visible,report);
                    report.stateAfter=StateJson();Assert.That(report.stateAfter,Is.EqualTo(state));
                    report.saveExistedAfter=File.Exists(app.SaveLocation);Assert.That(report.saveExistedAfter,Is.EqualTo(saveExisted),"A visual measurement must preserve whether this save slot exists.");
                    if(saveExisted)CollectionAssert.AreEqual(saved,File.ReadAllBytes(app.SaveLocation));
                    File.WriteAllText(path+".json",JsonUtility.ToJson(report,true));
                }
                catch
                {
                    File.WriteAllBytes(path+"-visible.png",visible.EncodeToPNG());File.WriteAllBytes(path+"-hidden.png",hiddenB.EncodeToPNG());File.WriteAllBytes(path+"-hidden-repeat.png",hiddenA.EncodeToPNG());
                    File.WriteAllText(path+".json",JsonUtility.ToJson(report,true));TestContext.WriteLine("Actual failing counterfactual GPU frames: "+path);throw;
                }
            }
            finally
            {
                Restore();foreach(var image in new[]{hiddenA,hiddenB,visible})if(image!=null)UnityEngine.Object.Destroy(image);
            }
        }
        private static int ScorePixelDistance07(Color32 a,Color32 b)=>Math.Max(Math.Abs(a.r-b.r),Math.Max(Math.Abs(a.g-b.g),Math.Abs(a.b-b.b)));
        private static void ScoreLabelGpuCheck07(IEnumerable<TextMeshProUGUI> labels,Texture2D hiddenA,Texture2D hiddenB,Texture2D visible,ScorePixelReport07 report)
        {
            Color32[] backgroundA=hiddenA.GetPixels32(),backgroundB=hiddenB.GetPixels32(),foreground=visible.GetPixels32();
            foreach(var label in labels)
            {
                label.ForceMeshUpdate();Assert.That(label.isTextTruncated,Is.False,label.name);Assert.That(label.gameObject.activeInHierarchy,Is.True);Assert.That(label.canvasRenderer.cull,Is.False);
                int count=0;var rects=ScoreGlyphRects07(label);
                for(int i=0;i<label.textInfo.characterCount;i++)
                {
                    var glyph=label.textInfo.characterInfo[i];if(char.IsWhiteSpace(glyph.character))continue;
                    Assert.That(glyph.textElement,Is.Not.Null,label.name+" font element "+glyph.character);
                    Assert.That(glyph.textElement.unicode,Is.EqualTo((uint)glyph.character),label.name+" substituted glyph "+glyph.character);
                    var mesh=label.textInfo.meshInfo[glyph.materialReferenceIndex];
                    Assert.That(mesh.material,Is.Not.Null);Assert.That(mesh.material.mainTexture,Is.Not.Null);
                    if(glyph.materialReferenceIndex>0)
                    {
                        var child=label.GetComponentsInChildren<TMP_SubMeshUI>(true).FirstOrDefault(m=>m.mesh==mesh.mesh);
                        Assert.That(child,Is.Not.Null,label.name+" missing TMP fallback submesh for "+glyph.character);
                        Assert.That(child.gameObject.activeInHierarchy,Is.True,label.name+" inactive TMP fallback submesh for "+glyph.character);
                        Assert.That(child.canvasRenderer.cull,Is.False);Assert.That(child.enabled,Is.True);
                    }
                    for(int v=0;v<4;v++)Assert.That(mesh.colors32[glyph.vertexIndex+v].a,Is.GreaterThan(0));
                    Rect rect=rects[count++];
                    // Sample inside this character's advance, so adjacent padded
                    // glyph quads cannot supply ink for a missing slash or symbol.
                    float origin=label.transform.TransformPoint(new Vector3(glyph.origin,0,0)).x;
                    float advance=label.transform.TransformPoint(new Vector3(glyph.xAdvance,0,0)).x;
                    rect.xMin=Mathf.Max(rect.xMin,Mathf.Min(origin,advance));rect.xMax=Mathf.Min(rect.xMax,Mathf.Max(origin,advance));
                    var probe=new ScoreGlyphPixel07{label=label.name,character=glyph.character.ToString(),unicode=glyph.character,materialIndex=glyph.materialReferenceIndex,sampleRect=rect};report.glyphs.Add(probe);
                    for(int y=Mathf.Max(0,Mathf.FloorToInt(rect.yMin));y<Mathf.Min(visible.height,Mathf.CeilToInt(rect.yMax));y++)
                    for(int x=Mathf.Max(0,Mathf.CeilToInt(rect.xMin));x<Mathf.Min(visible.width,Mathf.CeilToInt(rect.xMax));x++)
                    {
                        int offset=y*visible.width+x,noise=ScorePixelDistance07(backgroundA[offset],backgroundB[offset]),delta=ScorePixelDistance07(foreground[offset],backgroundB[offset]);
                        probe.maxHiddenDelta=Math.Max(probe.maxHiddenDelta,noise);probe.maxForegroundDelta=Math.Max(probe.maxForegroundDelta,delta);
                        if(delta>=report.threshold8Bit)probe.changedPixels++;
                    }
                    Assert.That(probe.maxHiddenDelta,Is.LessThanOrEqualTo(report.allowedHiddenNoise8Bit),label.name+" has a changing background under "+glyph.character+"; counterfactual frames cannot identify ink reliably");
                    Assert.That(probe.changedPixels,Is.GreaterThan(0),label.name+" has no rendered foreground/background difference for "+glyph.character+" U+"+((int)glyph.character).ToString("X4"));
                }
                Assert.That(count,Is.EqualTo(label.GetParsedText().Count(c=>!char.IsWhiteSpace(c))),label.name+" lost a non-space character");
            }
        }
        private static void ScoreDestroyTemporaryFont07(TMP_FontAsset font)
        {
            if(font==null)return;foreach(var atlas in font.atlasTextures)if(atlas!=null)UnityEngine.Object.Destroy(atlas);
            if(font.material!=null)UnityEngine.Object.Destroy(font.material);UnityEngine.Object.Destroy(font);
        }
    }
}
