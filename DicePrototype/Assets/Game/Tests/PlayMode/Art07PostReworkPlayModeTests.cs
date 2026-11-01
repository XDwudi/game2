using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DiceCraft.Core;
using DiceCraft.Presentation;
using DiceCraft.Verification;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UObj=UnityEngine.Object;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        [UnityTest] public IEnumerator PLAY_ART07_POSTREWORK_ActualGpuNeverCombinesOldAndNewPipsKeepsOrnamentsAndState()
        {
            var scene=FxScene07(s=>
            {
                FxControllers07(s,"C36","C38","C15");s.capacityOverride=6;
                foreach(var die in s.dice.Take(2)){var f=die.faces.Single(x=>x.faceId=="F06");f.permanentPip=3;f.isGold=true;f.texture="rough";}
                foreach(var result in s.currentResults.Take(2)){result.currentPip=1;result.remoteOverride=true;}
            });
            var submit=app.Send(new SessionCommand{kind=CommandKind.Submit});Assert.That(submit.accepted,Is.True,submit.error);
            Assert.That(submit.receipt.submission.snapshot.dice.Take(2).All(d=>d.currentPip==1),Is.True,"The scoring display must keep the accepted effective 1, not replace it with permanent 3.");
            Assert.That(submit.receipt.effects.Count(e=>e.kind=="FaceValueChanged"&&e.oldValue==3&&e.newValue==4),Is.EqualTo(2));
            string state=GameSession.StateHash(app.Session.State);byte[] disk=File.ReadAllBytes(app.SaveLocation);
            yield return WaitFxPhase07(PresentationPhase.PostScore);
            bool enabled=app.enabled,paused=app.table.Paused;app.enabled=false;app.table.Paused=true;app.table.FinishLayoutTransitions();
            var pool=app.table.GetComponentInChildren<Art07FaceTransition>(true);Assert.That(pool,Is.Not.Null);
            var orientation=app.table.GetFaceOrientation(scene.dice[0].dieId);var face=orientation.Find("Face_F06");var physical=face.GetComponent<MeshRenderer>();
            var old=face.Find("PreviousPermanentFace").GetComponent<MeshRenderer>();var next=face.Find("CommittedPermanentFace").GetComponent<MeshRenderer>();
            Assert.That(old.GetComponent<MeshFilter>().sharedMesh.name,Does.StartWith("Pips_3_Gold_2"));
            Assert.That(next.GetComponent<MeshFilter>().sharedMesh.name,Does.StartWith("Pips_4_Gold_2"));
            var physicalFaces=Enumerable.Range(1,6).Select(i=>orientation.Find("Face_F"+i.ToString("00"))).ToArray();
            var meshes=physicalFaces.Select(f=>f.GetComponent<MeshFilter>().sharedMesh).ToArray();
            var matrices=physicalFaces.Select(f=>f.localToWorldMatrix).ToArray();
            var objects=orientation.GetComponentsInChildren<Transform>(true);var layers=objects.Select(t=>t.gameObject.layer).ToArray();foreach(var t in objects)t.gameObject.layer=31;
            var cameraObject=new GameObject("PostExclusivePipsGpuCamera");var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=.54f;camera.aspect=1;camera.nearClipPlane=.1f;camera.farClipPlane=4;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.cullingMask=1<<31;
            camera.transform.position=face.position+face.forward*2;camera.transform.LookAt(face.position,face.up);
            var target=new RenderTexture(192,192,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);target.Create();var pixels=new Texture2D(192,192,TextureFormat.RGBA32,false,true);var active=RenderTexture.active;
            Color32[] Capture(string name)
            {
                RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,192,192),0,0);pixels.Apply(false,false);
                if(name!=null){string folder=Path.Combine(Path.GetDirectoryName(app.SaveLocation),"art07-post-rework");Directory.CreateDirectory(folder);File.WriteAllBytes(Path.Combine(folder,name+".png"),pixels.EncodeToPNG());}
                return pixels.GetPixels32();
            }
            var corners=new[]{new Vector3(-.305f,-.305f,0),new Vector3(.305f,.305f,0)}.Select(v=>camera.WorldToViewportPoint(face.TransformPoint(v))).ToArray();
            Rect roi=Rect.MinMaxRect(Mathf.Min(corners[0].x,corners[1].x)*192,Mathf.Min(corners[0].y,corners[1].y)*192,Mathf.Max(corners[0].x,corners[1].x)*192,Mathf.Max(corners[0].y,corners[1].y)*192);
            int Delta(Color32[] a,Color32[] b)
            {
                int n=0;for(int y=Mathf.CeilToInt(roi.yMin);y<Mathf.FloorToInt(roi.yMax);y++)for(int x=Mathf.CeilToInt(roi.xMin);x<Mathf.FloorToInt(roi.xMax);x++)
                {int i=y*192+x;int distance=Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b);if(distance>6)n++;}return n;
            }
            try
            {
                Assert.That(Resources.Load<Shader>("Art07FaceTransition").isSupported,Is.True);
                foreach(float p in new[]{0f,.25f,.49f,.50f,.51f,.75f,.99f})
                {
                    pool.SetTime(.32f*p);var whole=Capture("pip-transition-"+Mathf.RoundToInt(p*100));
                    old.enabled=false;var withoutOld=Capture(null);old.enabled=true;
                    next.enabled=false;var withoutNew=Capture(null);next.enabled=true;
                    int oldPixels=Delta(whole,withoutOld),newPixels=Delta(whole,withoutNew);
                    Assert.That(oldPixels==0||newPixels==0,Is.True,"Actual GPU p="+p+" contains both old and new point patterns ("+oldPixels+", "+newPixels+").");
                    if(p==0||p==.25f)Assert.That(oldPixels,Is.GreaterThan(5));
                    if(p==.75f||p==.99f)Assert.That(newPixels,Is.GreaterThan(5));
                    if(p==.5f){Assert.That(oldPixels,Is.Zero);Assert.That(newPixels,Is.Zero);}
                    for(int i=0;i<6;i++){Assert.That(physicalFaces[i].GetComponent<MeshFilter>().sharedMesh,Is.SameAs(meshes[i]));Assert.That(physicalFaces[i].localToWorldMatrix,Is.EqualTo(matrices[i]));}
                    TestContext.WriteLine("POST_EXCLUSIVE_GPU progress="+p+" oldPixels="+oldPixels+" newPixels="+newPixels);
                    Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(state));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
                }
                pool.SetTime(.33f);Capture("pip-transition-final");Assert.That(physical.enabled,Is.True);Assert.That(physical.sharedMaterial,Is.SameAs(app.table.faceMaterial));Assert.That(physical.GetComponent<MeshFilter>().sharedMesh.name,Does.StartWith("Pips_4_Gold_2"));Assert.That(pool.ActiveFaces,Is.Zero);
            }
            finally
            {
                old.enabled=true;next.enabled=true;RenderTexture.active=active;target.Release();UObj.Destroy(target);UObj.Destroy(pixels);UObj.Destroy(cameraObject);
                for(int i=0;i<objects.Length;i++)if(objects[i]!=null)objects[i].gameObject.layer=layers[i];app.SkipVisual();app.enabled=enabled;app.table.Paused=paused;
            }
            yield return null;
        }

        [UnityTest] public IEnumerator PLAY_ART07_POSTREWORK_ThreeFontsNormalAndReducedKeepCompleteTransitionsInsideOwnSlot()
        {
            foreach(int font in new[]{100,125,150})foreach(bool reduced in new[]{false,true})
            {
                yield return DiceLabelBegin07(font);FinalOptions07("Normal",reduced);Art07Fixtures.Apply(app,Art07FixtureKind.PermanentPost);
                var before=app.Session.State;string[] ids=before.hand.orderedLockedIds.ToArray();
                var submit=app.Send(new SessionCommand{kind=CommandKind.Submit});Assert.That(submit.accepted,Is.True,submit.error);
                string state=GameSession.StateHash(app.Session.State);byte[] disk=File.ReadAllBytes(app.SaveLocation);
                yield return WaitFxPhase07(PresentationPhase.PostScore);
                bool enabled=app.enabled,paused=app.table.Paused;app.enabled=false;app.table.Paused=true;app.table.FinishLayoutTransitions();
                try
                {
                    Canvas.ForceUpdateCanvases();
                    foreach(int index in new[]{0,1,4})
                    {
                        var label=FindActive<TextMeshProUGUI>("DieState"+ids[index]);string flat=label.text.Replace("\n"," ");
                        var stamp=FindActive<TextMeshProUGUI>("PermanentStamp_"+ids[index]);
                        Assert.That(stamp.text,Is.EqualTo("永久"));Assert.That(stamp.fontSize,Is.EqualTo(14*UiKit.FontScale).Within(.01f));
                        Assert.That(stamp.text+" "+flat,Does.Contain("永久"));if(index<2)Assert.That(flat,Does.Contain("3→4"));
                        AssertPostPrefixGeometry07(label,stamp);
                        Assert.That(flat,Does.Contain(index==1?"蓝→红":"红→蓝"),"Changed colors must remain explicit without sound, including reduced motion.");
                        Assert.That(label.fontSize,Is.EqualTo(14*UiKit.FontScale).Within(.01f));Assert.That(label.isTextTruncated,Is.False);
                        var slot=V06ScreenRect(FindActive<RectTransform>("Slot"+index));var glyphs=DiceLabelVisibleGlyphRects07(label);Assert.That(glyphs,Is.Not.Empty);
                        AssertPostCrossLineInk07(label);
                        foreach(var glyph in glyphs)
                        {
                            Assert.That(glyph.xMin,Is.GreaterThanOrEqualTo(slot.xMin-.1f));Assert.That(glyph.xMax,Is.LessThanOrEqualTo(slot.xMax+.1f));Assert.That(glyph.yMin,Is.GreaterThanOrEqualTo(slot.yMin-.1f));Assert.That(glyph.yMax,Is.LessThanOrEqualTo(slot.yMax+.1f));
                            foreach(string id in ids)foreach(var renderer in app.table.GetFaceOrientation(id).GetComponentsInChildren<MeshRenderer>(false).Where(r=>r.enabled))Assert.That(glyph.Overlaps(UpgradeMeshScreenRect07(renderer)),Is.False,"Permanent label covers actual projected die "+id);
                            foreach(string action in new[]{"Roll","Confirm","Clear","Submit","Skip"})if(HasActive(action))Assert.That(glyph.Overlaps(V06ScreenRect((RectTransform)Button(action).transform)),Is.False);
                        }
                    }
                    // Evaluate the real pooled overlays at entry and half travel;
                    // the app's rule and presentation clocks remain frozen.
                    foreach(float progress in new[]{0f,.5f})
                    {
                        foreach(var effect in LiveFx07("fx_face_change").Concat(LiveFx07("fx_cash_ticket")))effect.Evaluate(progress);
                        Canvas.ForceUpdateCanvases();yield return null;
                        AssertPostOverlayGeometry07(ids,true);
                        yield return CaptureEdge07("post-overlay-"+(reduced?"reduced":"normal")+"-font"+font+"-p"+Mathf.RoundToInt(progress*100));
                        Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(state));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
                    }
                    foreach(float seconds in reduced?new[]{0f,.04f,.08f}:new[]{0f,.22f,.29f,.36f})
                    {
                        typeof(GameApp).GetMethod("EvaluatePermanentStamps07",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(app,new object[]{seconds});
                        float expected=reduced?Mathf.Clamp01(seconds/.08f):Mathf.Clamp01((seconds-.22f)/.14f);
                        foreach(string id in new[]{ids[0],ids[1],ids[4]})
                        {
                            var stamp=FindActive<TextMeshProUGUI>("PermanentStamp_"+id);var prefix=FindActive<TextMeshProUGUI>("PermanentPrefix_"+id);
                            Assert.That(stamp.alpha,Is.EqualTo(1-expected).Within(.001f));Assert.That(prefix.alpha,Is.EqualTo(expected).Within(.001f));
                            Assert.That(stamp.alpha+prefix.alpha,Is.EqualTo(1).Within(.001f),"Permanent semantics cannot vanish during the handoff.");
                            AssertPostPrefixGeometry07(FindActive<TextMeshProUGUI>("DieState"+id),prefix);
                        }
                        Canvas.ForceUpdateCanvases();yield return null;AssertPostOverlayGeometry07(ids,false);
                        yield return CaptureEdge07("post-stamp-"+(reduced?"reduced":"normal")+"-font"+font+"-ms"+Mathf.RoundToInt(seconds*1000));
                    }
                    foreach(var effect in LiveFx07("fx_face_change"))effect.Evaluate(1);
                    Canvas.ForceUpdateCanvases();yield return null;
                    AssertPostOverlayGeometry07(ids,false); // Short frame faded; permanent meaning must remain.
                    yield return CaptureEdge07("post-rework-"+(reduced?"reduced":"normal")+"-font"+font);
                    Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(state));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
                }
                finally{app.SkipVisual();app.enabled=enabled;app.table.Paused=paused;}
            }
        }

        private void AssertPostPrefixGeometry07(TextMeshProUGUI label,TextMeshProUGUI stamp)
        {
            // Reconstruct the same original prefix briefly, without rendering or
            // advancing a clock, to prove the remaining glyph origins/baselines
            // were not moved to make the new overlap assertions pass.
            string current=label.text;Assert.That(current.StartsWith("\u3000\u3000",StringComparison.Ordinal),Is.True);
            label.ForceMeshUpdate();var actual=label.textInfo.characterInfo.Take(label.textInfo.characterCount)
                .Select(c=>new Vector2(c.origin,c.baseLine)).ToArray();
            try
            {
                label.text="永久"+current.Substring(2);label.ForceMeshUpdate();
                Assert.That(label.textInfo.characterCount,Is.EqualTo(actual.Length));
                for(int i=2;i<actual.Length;i++)if(!char.IsWhiteSpace(label.textInfo.characterInfo[i].character))
                    Assert.That(Vector2.Distance(actual[i],new Vector2(label.textInfo.characterInfo[i].origin,label.textInfo.characterInfo[i].baseLine)),Is.LessThanOrEqualTo(.01f),label.name+" point/color baseline shifted");
                stamp.ForceMeshUpdate();
                for(int i=0;i<2;i++)
                {
                    var prior=label.textInfo.characterInfo[i];var now=stamp.textInfo.characterInfo[i];
                    var a=label.rectTransform.TransformPoint(new Vector3(prior.origin,prior.baseLine,0));
                    var b=stamp.rectTransform.TransformPoint(new Vector3(now.origin,now.baseLine,0));
                    Assert.That(Vector3.Distance(a,b),Is.LessThanOrEqualTo(.01f),"Short stamp must use the former permanent-prefix baseline");
                }
            }
            finally{label.text=current;label.ForceMeshUpdate();}
        }

        private List<Rect> PostInkScreenRects07(TMP_Text text,bool complete=false)
        {
            text.ForceMeshUpdate();var result=new List<Rect>();
            if(complete){Assert.That(text.color.a,Is.GreaterThan(.01f),text.name);Assert.That(text.canvasRenderer.cull,Is.False,text.name);Assert.That(text.isTextTruncated,Is.False,text.name);}
            float inheritedAlpha=text.color.a;
            for(Transform p=text.transform;p!=null;p=p.parent)foreach(var group in p.GetComponents<CanvasGroup>())inheritedAlpha*=group.alpha;
            if(complete)Assert.That(inheritedAlpha,Is.GreaterThan(.01f),text.name+" must remain visible after the short face frame fades");
            if(inheritedAlpha<=.01f||text.canvasRenderer.cull)return result;
            foreach(var c in text.textInfo.characterInfo.Take(text.textInfo.characterCount))
            {
                if(char.IsWhiteSpace(c.character))continue;
                if(complete)Assert.That(c.isVisible,Is.True,text.name+" missing "+c.character);
                if(!c.isVisible)continue;Assert.That(c.textElement?.glyph,Is.Not.Null);
                var colors=text.textInfo.meshInfo[c.materialReferenceIndex].colors32;
                if(complete)for(int i=0;i<4;i++)Assert.That(colors[c.vertexIndex+i].a,Is.GreaterThan(0),text.name+" transparent "+c.character);
                if(colors[c.vertexIndex].a==0)continue;
                // Actual font ink, not TMP's transparent SDF padding quad.
                var m=c.textElement.glyph.metrics;float x=c.origin+m.horizontalBearingX*c.scale,y=c.baseLine+(m.horizontalBearingY-m.height)*c.scale;
                var local=new[]{new Vector2(x,y),new Vector2(x+m.width*c.scale,y),new Vector2(x,y+m.height*c.scale),new Vector2(x+m.width*c.scale,y+m.height*c.scale)};
                var pts=local.Select(v=>RectTransformUtility.WorldToScreenPoint(null,text.rectTransform.TransformPoint(v))).ToArray();
                result.Add(Rect.MinMaxRect(pts.Min(v=>v.x),pts.Min(v=>v.y),pts.Max(v=>v.x),pts.Max(v=>v.y)));
            }
            if(complete)Assert.That(result.Count,Is.EqualTo(text.text.Count(c=>!char.IsWhiteSpace(c))),text.name+" must keep every visible character");
            return result;
        }

        private void AssertPostOverlayGeometry07(string[] ids,bool checkCash)
        {
            var main=FindActive<RectTransform>("Screen");
            var stamps=app.canvas.GetComponentsInChildren<TextMeshProUGUI>(false).Where(t=>t.name.StartsWith("PermanentStamp_",StringComparison.Ordinal)).ToArray();
            Assert.That(stamps.Length,Is.EqualTo(3),"This accepted receipt has three net-changed targets.");
            var hud=main.GetComponentsInChildren<TextMeshProUGUI>(false).ToArray();
            var dieRects=ids.SelectMany(id=>app.table.GetFaceOrientation(id).GetComponentsInChildren<MeshRenderer>(false).Where(r=>r.enabled).Select(UpgradeMeshScreenRect07)).ToArray();
            var effectRoot=FindActive<RectTransform>("Art07LocalEffects");
            var otherEffects=effectRoot.GetComponentsInChildren<TMP_Text>(false).ToArray();
            foreach(var stamp in stamps)
            {
                string id=stamp.name.Substring("PermanentStamp_".Length);int index=Array.IndexOf(ids,id);Assert.That(index,Is.GreaterThanOrEqualTo(0));
                var prefix=FindActive<TextMeshProUGUI>("PermanentPrefix_"+id);
                Assert.That(prefix.text,Is.EqualTo(stamp.text));Assert.That(prefix.font,Is.SameAs(stamp.font));Assert.That(prefix.fontSize,Is.EqualTo(stamp.fontSize));
                Assert.That(V06ScreenRect(prefix.rectTransform),Is.EqualTo(V06ScreenRect(stamp.rectTransform)),"Only identical, co-located prefix text may crossfade.");
                var slot=V06ScreenRect(FindActive<RectTransform>("Slot"+index));
                foreach(var glyph in PostInkScreenRects07(stamp.alpha>=prefix.alpha?stamp:prefix,true))
                {
                    Assert.That(glyph.xMin,Is.GreaterThanOrEqualTo(slot.xMin));Assert.That(glyph.xMax,Is.LessThanOrEqualTo(slot.xMax));
                    Assert.That(glyph.yMin,Is.GreaterThanOrEqualTo(slot.yMin));Assert.That(glyph.yMax,Is.LessThanOrEqualTo(slot.yMax));
                    foreach(var die in dieRects)Assert.That(glyph.Overlaps(die),Is.False,stamp.name+" covers actual die mesh");
                    foreach(var text in hud.Where(t=>t.name.StartsWith("DieState",StringComparison.Ordinal)||t.name.StartsWith("SlotIndex",StringComparison.Ordinal)))
                        foreach(var ink in PostInkScreenRects07(text))Assert.That(glyph.Overlaps(ink),Is.False,stamp.name+" covers "+text.name+" actual ink");
                    foreach(var text in otherEffects.Where(t=>t!=stamp&&t!=prefix))foreach(var ink in PostInkScreenRects07(text))
                        Assert.That(glyph.Overlaps(ink),Is.False,stamp.name+" overlaps other overlay glyph "+text.name);
                }
            }
            foreach(var face in LiveFx07("fx_face_change"))
            {
                Assert.That(face.detail.text,Is.Empty,"Pooled long caption must not duplicate the permanent stamp.");
                foreach(var graphic in face.GetComponentsInChildren<UnityEngine.UI.Graphic>(false).Where(g=>!(g is TMP_Text)&&g.enabled&&g.color.a>.01f))
                {
                    // Conservatively test the entire frame graphic's rectangle,
                    // so neither its red boundary nor blue stroke can cut ink.
                    var frame=V06ScreenRect(graphic.rectTransform);
                    foreach(var text in hud.Where(t=>t.name.StartsWith("DieState",StringComparison.Ordinal)||t.name.StartsWith("SlotIndex",StringComparison.Ordinal)).Concat(otherEffects.Where(t=>!t.transform.IsChildOf(face.transform))))
                        foreach(var ink in PostInkScreenRects07(text))Assert.That(frame.Overlaps(ink),Is.False,graphic.name+" frame crosses "+text.name+" ink");
                }
            }
            var details=Button("PresentationDetails07");var detailsRect=V06ScreenRect((RectTransform)details.transform);
            Assert.That(detailsRect.width,Is.GreaterThanOrEqualTo(40));Assert.That(detailsRect.height,Is.GreaterThanOrEqualTo(40));
            foreach(var ink in PostInkScreenRects07(FindActive<TextMeshProUGUI>("FinalScore"),true))
                Assert.That(ink.Overlaps(detailsRect),Is.False,"Exact final score ink is covered by the full-record button");
            if(!checkCash)return;
            var tickets=LiveFx07("fx_cash_ticket");Assert.That(tickets.Length,Is.EqualTo(1),"Real receipt must produce one income ticket.");
            foreach(var ticket in tickets)
            {
                // The whole visible paper rectangle moves, not only its text.
                var paper=V06ScreenRect(ticket.visualRoot);
                Assert.That(paper.xMin,Is.GreaterThanOrEqualTo(Screen.safeArea.xMin));Assert.That(paper.xMax,Is.LessThanOrEqualTo(Screen.safeArea.xMax));
                Assert.That(paper.yMin,Is.GreaterThanOrEqualTo(Screen.safeArea.yMin));Assert.That(paper.yMax,Is.LessThanOrEqualTo(Screen.safeArea.yMax));
                Assert.That(PostInkScreenRects07(ticket.value,true),Is.Not.Empty);
                foreach(var button in app.canvas.GetComponentsInChildren<UnityEngine.UI.Button>(false))
                    if(!button.transform.IsChildOf(ticket.transform))Assert.That(paper.Overlaps(V06ScreenRect((RectTransform)button.transform)),Is.False,"Cash paper covers button "+button.name);
                foreach(var text in hud)foreach(var ink in PostInkScreenRects07(text))
                    Assert.That(paper.Overlaps(ink),Is.False,"Cash paper covers actual HUD glyph "+text.name);
                foreach(var text in otherEffects.Where(t=>!t.transform.IsChildOf(ticket.transform)))foreach(var ink in PostInkScreenRects07(text))
                    Assert.That(paper.Overlaps(ink),Is.False,"Cash paper covers another effect glyph "+text.name);
            }
        }

        private void AssertPostCrossLineInk07(TextMeshProUGUI label)
        {
            // TMP's bottomLeft/topRight include SDF padding. For the ordinary,
            // unstyled POST label use actual glyph.metrics + the resolved scale,
            // origin and baseline (including fallback fonts and final alignment).
            // This is the ink bounds used BEFORE padding by TextMeshProUGUI's
            // Calculate Vertices Position, not the transparent atlas rectangle.
            label.ForceMeshUpdate();var lines=new SortedDictionary<int,List<Rect>>();
            foreach(var ch in label.textInfo.characterInfo.Take(label.textInfo.characterCount))
            {
                if(char.IsWhiteSpace(ch.character))continue;
                Assert.That(ch.isVisible,Is.True);Assert.That(ch.textElement?.glyph,Is.Not.Null);
                var metrics=ch.textElement.glyph.metrics;float scale=ch.scale;
                var ink=new Rect(ch.origin+metrics.horizontalBearingX*scale,
                    ch.baseLine+(metrics.horizontalBearingY-metrics.height)*scale,
                    metrics.width*scale,metrics.height*scale);
                Assert.That(ink.width,Is.GreaterThan(0));Assert.That(ink.height,Is.GreaterThan(0));
                if(!lines.TryGetValue(ch.lineNumber,out var row)){row=new List<Rect>();lines.Add(ch.lineNumber,row);}row.Add(ink);
            }
            Assert.That(lines.Count,Is.GreaterThanOrEqualTo(2),"POST must retain real multiline text for this coverage.");
            var rows=lines.ToArray();
            for(int i=1;i<rows.Length;i++)
            {
                float upperBottom=rows[i-1].Value.Min(r=>r.yMin),lowerTop=rows[i].Value.Max(r=>r.yMax);
                float gap=upperBottom-lowerTop;
                TestContext.WriteLine("POST_LABEL_INK "+label.name+" font="+label.fontSize+" spacing="+label.lineSpacing+" lines="+rows[i-1].Key+"/"+rows[i].Key+" localInkGap="+gap);
                Assert.That(gap,Is.GreaterThanOrEqualTo(0),label.name+" visible ink of consecutive lines overlaps; SDF padding is excluded.");
            }
        }
    }
}
