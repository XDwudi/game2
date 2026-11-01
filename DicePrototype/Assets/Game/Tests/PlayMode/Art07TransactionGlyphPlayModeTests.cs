using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using DiceCraft.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TextCore.LowLevel;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        [Serializable] private sealed class TransactionGlyph07
        {
            public string label,character,font;public int materialIndex,inkPixels,comparedPixels,mismatchPixels;
            public float meanError;public Rect rect;
        }
        [Serializable] private sealed class TransactionGlyphReport07
        {
            public string scope,stateBefore,stateAfter,output,runtimeMvid,testMvid;
            public bool reduced,passed;public int width,height,snapshotMeshes;
            public List<TransactionGlyph07> glyphs=new List<TransactionGlyph07>();
        }
        [UnityTest] public IEnumerator PLAY_ART07_TX_GpuRealShopCardSnapshotRetainsEveryGlyphNormalAndReduced()
        {
            yield return TransactionShop07(s=>s.shop.offers.Any(o=>o.category=="controller"));
            yield return new WaitForSecondsRealtime(.3f);
            var offer=app.Session.State.shop.offers.First(o=>o.category=="controller");
            var card=TransactionRect07("Offer_"+offer.offerId);
            yield return TransactionGlyphBothModes07(card,"actual-shop-"+offer.contentId);
        }
        [UnityTest] public IEnumerator PLAY_ART07_TX_GpuSnapshotRetainsSeparatePrimaryAndChineseFallbackAtlases()
        {
            yield return TransactionShop07(s=>true);
            TMP_FontAsset primary=null,fallback=null;RectTransform probe=null;var prior=UiKit.Font;
            try
            {
                Font source=app.uiFont.sourceFontFile;
#if UNITY_EDITOR
                if(source==null)source=UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/Fonts/NotoSansCJKsc-Regular.otf");
#endif
                Assert.That(source,Is.Not.Null);
                primary=TMP_FontAsset.CreateFontAsset(source,48,7,GlyphRenderMode.SDFAA,512,512,AtlasPopulationMode.Dynamic,true);
                fallback=TMP_FontAsset.CreateFontAsset(source,48,7,GlyphRenderMode.SDFAA,512,512,AtlasPopulationMode.Dynamic,true);
                primary.name="TX_ASCII_Only";fallback.name="TX_Chinese_Fallback";
                Assert.That(primary.TryAddCharacters("H 78",out string missingPrimary),Is.True,missingPrimary);
                Assert.That(fallback.TryAddCharacters("＋混",out string missingFallback),Is.True,missingFallback);
                primary.atlasPopulationMode=fallback.atlasPopulationMode=AtlasPopulationMode.Static;
                primary.fallbackFontAssetTable=new List<TMP_FontAsset>{fallback};UiKit.Font=primary;
                probe=UiKit.Box(app.canvas.transform,"TX_FallbackProbe",400,450,1120,180,UiKit.Ink);
                var label=UiKit.Text(probe,"ActualMixedGlyphs","H＋混 78",24,24,1072,132,32,UiKit.Paper);
                label.ForceMeshUpdate();yield return null;
                Assert.That(label.textInfo.characterInfo.Any(c=>c.character=='混'&&c.fontAsset==fallback&&c.materialReferenceIndex>0),Is.True);
                Assert.That(label.GetComponentsInChildren<TMP_SubMeshUI>(false),Is.Not.Empty);
                yield return TransactionGlyphBothModes07(probe,"two-distinct-font-atlases");
            }
            finally
            {
                UiKit.Font=prior;if(probe!=null)UnityEngine.Object.Destroy(probe.gameObject);
                ScoreDestroyTemporaryFont07(primary);ScoreDestroyTemporaryFont07(fallback);
            }
        }
        private IEnumerator TransactionGlyphBothModes07(RectTransform card,string scope)
        {
            // This test owns the host outright. Do not rent a pooled UiKit node and then
            // Destroy it between modes: Destroy is deferred and the next Rent can return it.
            var host=new GameObject("TX_SnapshotGpuProbe",typeof(RectTransform));
            var rect=(RectTransform)host.transform;rect.SetParent(app.canvas.transform,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=Vector2.zero;rect.sizeDelta=new Vector2(1920,1080);
            var motion=host.AddComponent<Art07TransactionMotion>();var owned=new List<Material>();
            string state=GameSession.StateHash(app.Session.State);byte[] save=File.ReadAllBytes(app.SaveLocation);long sequence=app.Session.ActionSequence;
            try
            {
                foreach(bool reduced in new[]{false,true})yield return TransactionGlyphPixels07(card,scope,reduced,motion,owned);
                // Reuse the same populated pool across actual source disable/enable lifecycles.
                // Every iteration crosses frames so TMP gets to release its own fallback material.
                for(int i=0;i<200;i++)
                {
                    foreach(var label in card.GetComponentsInChildren<TextMeshProUGUI>(false))label.ForceMeshUpdate();Canvas.ForceUpdateCanvases();
                    motion.Prepare(new[]{card});TransactionGlyphMaterials07(motion,card,owned);
                    card.gameObject.SetActive(false);motion.Begin("gpu_material_pool",.34f,.16f,false,false,i%2!=0,null);
                    yield return null;
                    Assert.That(owned.All(m=>m!=null),Is.True,"Source TMP teardown must not destroy snapshot-owned materials.");
                    motion.Tick(motion.Duration,false);Assert.That(motion.IsPlaying||motion.IsPrepared,Is.False);
                    card.gameObject.SetActive(true);Canvas.ForceUpdateCanvases();yield return null;
                    Assert.That(motion.CreatedSnapshotMeshes,Is.EqualTo(owned.Count));
                }
                Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(state));Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence));CollectionAssert.AreEqual(save,File.ReadAllBytes(app.SaveLocation));
            }
            finally
            {
                motion.Finish(false);card.gameObject.SetActive(true);UnityEngine.Object.Destroy(host);
            }
            yield return null;yield return null;
            Assert.That(host==null,Is.True,"The dedicated test host must actually be destroyed.");
            Assert.That(owned,Is.Not.Empty);Assert.That(owned.All(m=>m==null),Is.True,"Destroying the motion must release every owned pooled material.");
        }
        private static void TransactionGlyphMaterials07(Art07TransactionMotion motion,RectTransform card,List<Material> owned)
        {
            var source=card.GetComponentsInChildren<UnityEngine.UI.Graphic>(false).Select(g=>g is TMP_Text text?text.fontSharedMaterial:g is TMP_SubMeshUI sub?sub.sharedMaterial:g.material).ToArray();
            var materials=motion.transform.Find("CommittedOldMeshes").GetComponentsInChildren<CanvasRenderer>(false).Select(r=>r.GetMaterial(0)).ToArray();
            Assert.That(materials.Length,Is.EqualTo(motion.CapturedGraphicCount));Assert.That(materials,Is.Not.Empty);
            Assert.That(materials.All(m=>m!=null),Is.True);Assert.That(materials.Distinct().Count(),Is.EqualTo(materials.Length));
            Assert.That(materials.All(m=>!source.Contains(m)),Is.True,"Snapshot must own materials independently from source Graphics/TMP.");
            if(owned.Count==0)owned.AddRange(materials);else CollectionAssert.AreEqual(owned,materials,"Every Prepare reuses the same bounded material slots.");
        }
        private IEnumerator TransactionGlyphPixels07(RectTransform card,string scope,bool reduced,Art07TransactionMotion motion,List<Material> owned)
        {
            var labels=card.GetComponentsInChildren<TextMeshProUGUI>(false).Where(t=>!string.IsNullOrWhiteSpace(t.text)).ToArray();
            Assert.That(labels,Is.Not.Empty);var colors=labels.Select(t=>t.color).ToArray();
            string state=GameSession.StateHash(app.Session.State);byte[] save=File.ReadAllBytes(app.SaveLocation);
            string output=Path.Combine(saveDirectory,"art07-tx-gpu",scope+"-"+(reduced?"reduced":"normal")+"-"+Time.frameCount);Directory.CreateDirectory(output);
            var report=new TransactionGlyphReport07{scope=scope,reduced=reduced,width=Screen.width,height=Screen.height,stateBefore=state,output=output,runtimeMvid=typeof(GameApp).Module.ModuleVersionId.ToString(),testMvid=GetType().Module.ModuleVersionId.ToString()};
            Texture2D blank=null,original=null,snapshot=null;
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
                            label.ForceMeshUpdate();var rects=ScoreGlyphRects07(label);int index=0;
                            for(int i=0;i<label.textInfo.characterCount;i++)
                            {
                                var glyph=label.textInfo.characterInfo[i];if(char.IsWhiteSpace(glyph.character))continue;
                                var r=rects[index++];float a=label.transform.TransformPoint(new Vector3(glyph.origin,0,0)).x,b=label.transform.TransformPoint(new Vector3(glyph.xAdvance,0,0)).x;
                                r.xMin=Mathf.Max(r.xMin,Mathf.Min(a,b));r.xMax=Mathf.Min(r.xMax,Mathf.Max(a,b));
                                report.glyphs.Add(new TransactionGlyph07{label=label.name,character=glyph.character.ToString(),font=glyph.fontAsset.name,materialIndex=glyph.materialReferenceIndex,rect=r});
                            }
                        }
                        motion.Prepare(new[]{card});report.snapshotMeshes=motion.CapturedGraphicCount;Assert.That(motion.IsPrepared,Is.True);TransactionGlyphMaterials07(motion,card,owned);
                        card.gameObject.SetActive(false);motion.Begin("gpu_readonly_snapshot",.34f,.16f,false,false,reduced,null);
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
                if(!done){app.StopCoroutine(worker);Assert.Fail("Real transaction GPU frames did not complete within10s; no synthetic frame substituted.");}
                if(error!=null)throw new InvalidOperationException("Transaction GPU capture failed",error);
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
                report.stateAfter=GameSession.StateHash(app.Session.State);Assert.That(report.stateAfter,Is.EqualTo(state));CollectionAssert.AreEqual(save,File.ReadAllBytes(app.SaveLocation));report.passed=true;
            }
            finally
            {
                motion.Finish(false);card.gameObject.SetActive(true);RestoreText();
                File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));TestContext.WriteLine("Real source/snapshot glyph GPU evidence: "+output);
                foreach(var image in new[]{blank,original,snapshot})if(image!=null)UnityEngine.Object.Destroy(image);
            }
        }
    }
}
