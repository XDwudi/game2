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
        [Serializable] private sealed class ModalExitFrame07
        { public float alpha,elapsed,duration;public int frame;public bool playing; }
        [UnityTest] public IEnumerator PLAY_ART07_ModalExitGpuActualDebug32RetainsGlyphsNormalAndReduced()
        {
            foreach(bool reduced in new[]{false,true})
            {
                PlayerPrefs.SetInt("reduced",reduced?1:0);
                yield return ScoreLabelBegin07(100);ScoreLabelApply07(Art07FixtureKind.Debug32,false);
                Assert.That(app.Session.State.dice.Count,Is.EqualTo(32));
                app.ShowDebugPanel();yield return new WaitForSecondsRealtime(.25f);
                var labels=new[]{FindActive<TextMeshProUGUI>("DialogTitle"),FindActive<TextMeshProUGUI>("DebugIsolation"),Button("RunDebugScene").GetComponentInChildren<TextMeshProUGUI>()};
                yield return ModalExitGlyphPixels07(labels,"actual-debug32-"+(reduced?"reduced":"normal"),reduced,new List<Material>());
            }
        }

        [UnityTest] public IEnumerator PLAY_ART07_ModalExitGpuFallbackAtlasSurvivesSourceCloseAndReusesOwnedMaterials()
        {
            yield return ScoreLabelBegin07(100);ScoreLabelApply07(Art07FixtureKind.Debug32,false);
            TMP_FontAsset primary=null,fallback=null;TextMeshProUGUI label=null;TMP_FontAsset originalFont=null;Material originalMaterial=null;
            var owned=new List<Material>();Art07PanelMotion tail=null;
            try
            {
                Font source=app.uiFont.sourceFontFile;
#if UNITY_EDITOR
                if(source==null)source=UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/Fonts/NotoSansCJKsc-Regular.otf");
#endif
                Assert.That(source,Is.Not.Null);
                primary=TMP_FontAsset.CreateFontAsset(source,48,7,GlyphRenderMode.SDFAA,512,512,AtlasPopulationMode.Dynamic,true);
                fallback=TMP_FontAsset.CreateFontAsset(source,48,7,GlyphRenderMode.SDFAA,512,512,AtlasPopulationMode.Dynamic,true);
                primary.name="Modal_ASCII_Only";fallback.name="Modal_Chinese_Fallback";
                Assert.That(primary.TryAddCharacters("H 78",out string missingPrimary),Is.True,missingPrimary);
                Assert.That(fallback.TryAddCharacters("＋混",out string missingFallback),Is.True,missingFallback);
                primary.atlasPopulationMode=fallback.atlasPopulationMode=AtlasPopulationMode.Static;
                primary.fallbackFontAssetTable=new List<TMP_FontAsset>{fallback};
                Assert.That(primary.material.mainTexture,Is.Not.SameAs(fallback.material.mainTexture));
                for(int cycle=0;cycle<2;cycle++)
                {
                    app.ShowDebugPanel();yield return new WaitForSecondsRealtime(.25f);
                    label=FindActive<TextMeshProUGUI>("DialogTitle");originalFont=label.font;originalMaterial=label.fontSharedMaterial;
                    // Keep the actual debug panel, real close button and production CaptureExit path.
                    // Only this label uses a deliberately separate fallback atlas.
                    label.font=primary;label.text="H＋混 78";label.ForceMeshUpdate();Canvas.ForceUpdateCanvases();yield return null;
                    Assert.That(label.textInfo.characterInfo.Take(label.textInfo.characterCount).Any(c=>c.character=='混'&&c.fontAsset==fallback&&c.materialReferenceIndex>0),Is.True);
                    Assert.That(label.GetComponentsInChildren<TMP_SubMeshUI>(false),Is.Not.Empty);
                    yield return ModalExitGlyphPixels07(new[]{label},"actual-debug32-two-atlases-cycle"+cycle,false,owned);
                    tail=app.canvas.GetComponentsInChildren<Art07PanelMotion>(true).Single(m=>m.IsExitTail);
                    label.font=originalFont;label.fontSharedMaterial=originalMaterial;
                }
                Assert.That(owned,Is.Not.Empty);
                UnityEngine.Object.Destroy(tail.gameObject);yield return null;yield return null;
                Assert.That(tail==null,Is.True);
                Assert.That(owned.All(m=>m==null),Is.True,"Destroying the real tail must release its owned material slots.");
            }
            finally
            {
                if(label!=null&&originalFont!=null){label.font=originalFont;label.fontSharedMaterial=originalMaterial;}
                ScoreDestroyTemporaryFont07(primary);ScoreDestroyTemporaryFont07(fallback);
            }
        }

        private IEnumerator ModalExitGlyphPixels07(TextMeshProUGUI[] labels,string scope,bool reduced,List<Material> owned)
        {
            var panel=FindActive<RectTransform>("Dialog");var enter=panel.GetComponent<Art07PanelMotion>();
            Assert.That(enter.IsPlaying,Is.False);Assert.That(enter.Alpha,Is.EqualTo(1).Within(.001f));
            float expectedDuration=reduced?Mathf.Min(.08f,enter.ExitDuration):enter.ExitDuration;
            var sourceMaterials=panel.GetComponentsInChildren<UnityEngine.UI.Graphic>(false).Select(g=>g is TMP_Text text?text.fontSharedMaterial:g is TMP_SubMeshUI sub?sub.sharedMaterial:g.material).ToArray();
            var colors=labels.Select(t=>t.color).ToArray();
            string state=GameSession.StateHash(app.Session.State);byte[] save=File.ReadAllBytes(app.SaveLocation);long sequence=app.Session.ActionSequence;
            string ordinaryPath=Path.Combine(saveDirectory,"active-run.json");byte[] ordinary=File.ReadAllBytes(ordinaryPath);
            string output=Path.Combine(saveDirectory,"art07-modal-exit-gpu",scope+"-"+Time.frameCount);Directory.CreateDirectory(output);
            // Reuse the existing per-glyph GPU report and pixel-distance contract, not a texture-routing mirror.
            var report=new TransactionGlyphReport07{scope=scope,reduced=reduced,width=Screen.width,height=Screen.height,stateBefore=state,output=output,runtimeMvid=typeof(GameApp).Module.ModuleVersionId.ToString(),testMvid=GetType().Module.ModuleVersionId.ToString()};
            Texture2D blank=null,original=null,snapshot=null,transition=null;Art07PanelMotion tail=null;
            ModalExitFrame07 transitionFrame=null;
            bool done=false;Exception error=null;
            void Run(Action action){try{action();}catch(Exception ex){if(error==null)error=ex;}}
            void RestoreText(){for(int i=0;i<labels.Length;i++)if(labels[i]!=null){labels[i].color=colors[i];labels[i].ForceMeshUpdate();}Canvas.ForceUpdateCanvases();}
            IEnumerator Capture()
            {
                try
                {
                    Run(()=>{foreach(var label in labels){var c=label.color;c.a=0;label.color=c;label.ForceMeshUpdate();}Canvas.ForceUpdateCanvases();});if(error!=null)yield break;
                    yield return new WaitForEndOfFrame();Run(()=>blank=ScreenCapture.CaptureScreenshotAsTexture());
                    Run(RestoreText);if(error!=null)yield break;
                    yield return new WaitForEndOfFrame();Run(()=>original=ScreenCapture.CaptureScreenshotAsTexture());
                    Run(()=>
                    {
                        foreach(var label in labels)
                        {
                            var rects=ScoreGlyphRects07(label);int index=0;
                            for(int i=0;i<label.textInfo.characterCount;i++)
                            {
                                var glyph=label.textInfo.characterInfo[i];if(char.IsWhiteSpace(glyph.character))continue;
                                var r=rects[index++];float a=label.transform.TransformPoint(new Vector3(glyph.origin,0,0)).x,b=label.transform.TransformPoint(new Vector3(glyph.xAdvance,0,0)).x;
                                r.xMin=Mathf.Max(r.xMin,Mathf.Min(a,b));r.xMax=Mathf.Min(r.xMax,Mathf.Max(a,b));
                                report.glyphs.Add(new TransactionGlyph07{label=label.name,character=glyph.character.ToString(),font=glyph.fontAsset.name,materialIndex=glyph.materialReferenceIndex,rect=r});
                            }
                        }
                        Click("RunDebugScene");Assert.That(HasActive("Dialog"),Is.False,"The production close must synchronously release the panel.");
                        tail=app.canvas.GetComponentsInChildren<Art07PanelMotion>(true).Single(m=>m.IsExitTail);
                        Assert.That(tail.IsPlaying,Is.True);Assert.That(tail.Duration,Is.EqualTo(expectedDuration).Within(.0001f));Assert.That(tail.Alpha,Is.EqualTo(1));
                        // Freeze only this actual tail's Update while obtaining GPU frames. Duration, geometry,
                        // texture overrides and close handler remain production values; no replacement renderer.
                        tail.enabled=false;report.snapshotMeshes=tail.CapturedGraphicCount;
                        Assert.That(labels.All(t=>!t.gameObject.activeInHierarchy),Is.True,"Pixels must come from the tail after source TMP teardown.");
                        Assert.That(tail.GetComponentsInChildren<UnityEngine.UI.Graphic>(true),Is.Empty);
                        Assert.That(tail.GetComponentsInChildren<UnityEngine.UI.Selectable>(true),Is.Empty);
                        Assert.That(tail.GetComponent<CanvasGroup>().blocksRaycasts,Is.False);
                        Canvas.ForceUpdateCanvases();
                    });if(error!=null)yield break;
                    // Cross two rendered frames so source fallback materials can be released before readback.
                    yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
                    Run(()=>snapshot=ScreenCapture.CaptureScreenshotAsTexture());
                }
                finally{done=true;}
            }
            var worker=app.StartCoroutine(Capture());double deadline=Time.realtimeSinceStartupAsDouble+10;
            try
            {
                while(!done&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                if(!done){app.StopCoroutine(worker);Assert.Fail("Real modal exit GPU frames did not complete within 10s; no synthetic frame substituted.");}
                if(error!=null)throw new InvalidOperationException("Modal exit GPU capture failed",error);
                foreach(var image in new[]{blank,original,snapshot}){Assert.That(image,Is.Not.Null);Assert.That(image.width,Is.EqualTo(report.width));Assert.That(image.height,Is.EqualTo(report.height));}
                File.WriteAllBytes(Path.Combine(output,"blank.png"),blank.EncodeToPNG());File.WriteAllBytes(Path.Combine(output,"source.png"),original.EncodeToPNG());File.WriteAllBytes(Path.Combine(output,"tail.png"),snapshot.EncodeToPNG());
                var bg=blank.GetPixels32();var src=original.GetPixels32();var dst=snapshot.GetPixels32();
                Assert.That(report.glyphs,Is.Not.Empty);
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
                    Assert.That(glyph.meanError,Is.LessThanOrEqualTo(2f),glyph.label+" tail/source RGB error: "+glyph.character);
                    Assert.That(glyph.mismatchPixels/(float)glyph.comparedPixels,Is.LessThanOrEqualTo(.02f),glyph.label+" exit tail must preserve glyph shape, not render its quad: "+glyph.character);
                }
                // A material-only assertion cannot detect CanvasRenderer.SetTexture overriding the atlas;
                // these ownership assertions deliberately follow the actual per-glyph GPU comparison.
                var materials=tail.GetComponentsInChildren<CanvasRenderer>(false).Select(r=>r.GetMaterial(0)).ToArray();
                Assert.That(materials.Length,Is.EqualTo(tail.CapturedGraphicCount));Assert.That(materials,Is.Not.Empty);
                Assert.That(materials.All(m=>m!=null),Is.True,"Closed source TMP must not destroy tail-owned materials.");
                Assert.That(materials.Distinct().Count(),Is.EqualTo(materials.Length));
                Assert.That(materials.All(m=>!sourceMaterials.Contains(m)),Is.True);
                if(owned.Count==0)owned.AddRange(materials);else CollectionAssert.AreEqual(owned,materials,"Repeated closes reuse the same tail material slots.");
                report.stateAfter=GameSession.StateHash(app.Session.State);Assert.That(report.stateAfter,Is.EqualTo(state));Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence));
                CollectionAssert.AreEqual(save,File.ReadAllBytes(app.SaveLocation));CollectionAssert.AreEqual(ordinary,File.ReadAllBytes(ordinaryPath));
                bool transitionDone=false;
                IEnumerator CaptureTransition()
                {
                    try
                    {
                        while(tail!=null&&tail.gameObject.activeSelf)
                        {
                            yield return new WaitForEndOfFrame();
                            if(tail.IsPlaying&&tail.Alpha>0&&tail.Alpha<1)
                            {
                                transitionFrame=new ModalExitFrame07{alpha=tail.Alpha,elapsed=tail.Elapsed,duration=tail.Duration,frame=Time.frameCount,playing=tail.IsPlaying};
                                transition=ScreenCapture.CaptureScreenshotAsTexture();yield break;
                            }
                        }
                    }
                    finally{transitionDone=true;}
                }
                // Also preserve an unmodified, naturally advancing intermediate exit frame.
                // Encode its PNG only after playback, so evidence I/O does not consume the short fade.
                // Drain the frame containing earlier PNG encoding/pixel comparison while the tail is
                // still frozen; that observer cost must not become the first unscaled animation delta.
                yield return null;yield return null;
                tail.enabled=true;var transitionWorker=app.StartCoroutine(CaptureTransition());double transitionDeadline=Time.realtimeSinceStartupAsDouble+2;
                while(!transitionDone&&Time.realtimeSinceStartupAsDouble<transitionDeadline)yield return null;
                if(!transitionDone)app.StopCoroutine(transitionWorker);
                Assert.That(transition,Is.Not.Null,"The real exit must render an intermediate frame before becoming inactive.");
                yield return new WaitForSecondsRealtime(expectedDuration+.06f);
                Assert.That(tail.gameObject.activeSelf,Is.False,"Unfreezing the real Update must finish its original exit duration.");
                File.WriteAllBytes(Path.Combine(output,"tail-in-motion.png"),transition.EncodeToPNG());
                File.WriteAllText(Path.Combine(output,"tail-in-motion.json"),JsonUtility.ToJson(transitionFrame,true));
                report.passed=true;
            }
            finally
            {
                if(!done)app.StopCoroutine(worker);
                if(tail!=null){tail.enabled=true;if(tail.IsPlaying)tail.CancelTail();}
                RestoreText();File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));TestContext.WriteLine("Real source/closed-panel tail GPU evidence: "+output);
                foreach(var image in new[]{blank,original,snapshot,transition})if(image!=null)UnityEngine.Object.Destroy(image);
            }
        }
    }
}
