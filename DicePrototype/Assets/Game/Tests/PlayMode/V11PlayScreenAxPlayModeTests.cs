using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using DiceCraft.Presentation;
using DiceCraft.Verification;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UObj = UnityEngine.Object;

namespace DiceCraft.Tests
{
    /// <summary>
    /// V1.1 task W11 acceptance checks of what the player sees (DEV_PLAN 6: AX-01, AX-02, AX-03, AX-05). They read the
    /// rendered frame, not only text content: v1.0 shipped two defects (a covered boss readout and an undrawn title) that
    /// passed every text assertion.
    /// </summary>
    public sealed partial class GameplayPlayModeTests
    {
        // ------------------------------------------------------------------ AX-01: nothing covers a submitted die
        /// <summary>Screen polygon of a die body: the eight cube corners projected, convex hull, drawn in by a margin so the
        /// rounded edges and their anti-aliasing stay outside the sampled area.</summary>
        private List<Vector2> W11DieHull(string id,float inset)
        {
            var pivot=app.table.GetFaceOrientation(id);Assert.That(pivot,Is.Not.Null,"No 3D die for "+id);
            var points=new List<Vector2>();
            for(int i=0;i<8;i++)
            {
                var local=new Vector3((i&1)==0?-.5f:.5f,(i&2)==0?-.5f:.5f,(i&4)==0?-.5f:.5f);
                var screen=app.table.viewCamera.WorldToScreenPoint(pivot.TransformPoint(local));points.Add(new Vector2(screen.x,screen.y));
            }
            points=points.Distinct().OrderBy(p=>p.x).ThenBy(p=>p.y).ToList();
            float Cross(Vector2 o,Vector2 a,Vector2 b)=>(a.x-o.x)*(b.y-o.y)-(a.y-o.y)*(b.x-o.x);
            var hull=new List<Vector2>();
            foreach(var p in points){while(hull.Count>=2&&Cross(hull[hull.Count-2],hull[hull.Count-1],p)<=0)hull.RemoveAt(hull.Count-1);hull.Add(p);}
            for(int i=points.Count-2,t=hull.Count+1;i>=0;i--){var p=points[i];while(hull.Count>=t&&Cross(hull[hull.Count-2],hull[hull.Count-1],p)<=0)hull.RemoveAt(hull.Count-1);hull.Add(p);}
            hull.RemoveAt(hull.Count-1);
            var centre=new Vector2(hull.Average(p=>p.x),hull.Average(p=>p.y));
            return hull.Select(p=>centre+(p-centre)*(1-inset)).ToList();
        }
        private static bool W11Inside(List<Vector2> polygon,Vector2 p)
        {
            bool inside=false;
            for(int i=0,j=polygon.Count-1;i<polygon.Count;j=i++)
                if((polygon[i].y>p.y)!=(polygon[j].y>p.y)&&p.x<(polygon[j].x-polygon[i].x)*(p.y-polygon[i].y)/(polygon[j].y-polygon[i].y)+polygon[i].x)inside=!inside;
            return inside;
        }
        /// <summary>Captures the frame with and without the interface canvas and counts changed pixels inside each die body.</summary>
        private IEnumerator W11AssertDiceUncovered(string label,IList<string> dice,List<string> report)
        {
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();var with=ScreenCapture.CaptureScreenshotAsTexture();
            app.canvas.enabled=false;
            yield return new WaitForEndOfFrame();var without=ScreenCapture.CaptureScreenshotAsTexture();
            app.canvas.enabled=true;Canvas.ForceUpdateCanvases();
            try
            {
                var a=with.GetPixels32();var b=without.GetPixels32();int w=with.width,h=with.height;
                foreach(string id in dice)
                {
                    var hull=W11DieHull(id,.16f);int inside=0,changed=0;
                    int x0=Mathf.Max(0,Mathf.FloorToInt(hull.Min(p=>p.x))),x1=Mathf.Min(w-1,Mathf.CeilToInt(hull.Max(p=>p.x)));
                    int y0=Mathf.Max(0,Mathf.FloorToInt(hull.Min(p=>p.y))),y1=Mathf.Min(h-1,Mathf.CeilToInt(hull.Max(p=>p.y)));
                    for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                    {
                        if(!W11Inside(hull,new Vector2(x+.5f,y+.5f)))continue;inside++;
                        var p=a[y*w+x];var q=b[y*w+x];if(Math.Abs(p.r-q.r)+Math.Abs(p.g-q.g)+Math.Abs(p.b-q.b)>12)changed++;
                    }
                    report.Add(label+" "+id+" inside="+inside+" changed="+changed);
                    Assert.That(inside,Is.GreaterThan(200),label+": die "+id+" body not on screen.");
                    Assert.That(changed,Is.LessThanOrEqualTo(2),label+": the interface paints over submitted die "+id+" ("+changed+" of "+inside+" body pixels).");
                }
                if(report.Count<12)File.WriteAllBytes(Path.Combine(W11Output,"ax01-"+label+".png"),with.EncodeToPNG());
            }
            finally{UObj.Destroy(with);UObj.Destroy(without);}
        }

        [UnityTest] public IEnumerator PLAY_V11_AX01_ScoringFramesNeverCoverASubmittedDie()
        {
            StartRun("71101");var report=new List<string>();
            foreach(string speed in new[]{"Normal","Fast"})
            {
                if(speed!="Normal")FinalOptions07(speed,false);
                // Four blue fours against a low target: every source pops, the top tier lights the rims, the stage qualifies (stamp).
                W11Apply(W11Scene(new[]{"C39","C139","C34","C08","C46"},new[]{4,4,4,4,2},"bbbbr",4,true,target:"900",score:"0"));
                yield return W11Settle();
                var dice=app.Session.State.hand.orderedLockedIds.ToArray();
                var result=app.Send(new SessionCommand{kind=CommandKind.Submit});Assert.That(result.accepted,Is.True,result.error);
                bool enabled=app.enabled;app.enabled=false;
                try
                {
                    var player=W11Player();var beats=player.Sequence.Beats.ToArray();Assert.That(beats.Length,Is.GreaterThan(2));
                    int n=0;
                    foreach(var beat in beats)
                        foreach(float f in new[]{.02f,.5f,.98f})
                        {
                            double target=beat.StartMs+Math.Max(1,beat.DurationMs*f);double step=target-player.Clock.ElapsedMilliseconds;
                            if(step>0)player.Tick(step/1000.0);if(!player.IsPlaying)break;
                            W11Invoke("TickScore11");W11Invoke("TickLibraryEffects07",0f);W11Invoke("TickHighScore073",.05f);W11Invoke("TickQualifiedStamp07",.05f);
                            yield return W11AssertDiceUncovered(speed+"-"+(n++).ToString("00")+"-"+beat.Phase,dice,report);
                        }
                    Assert.That(n,Is.GreaterThanOrEqualTo(6),"Too few sampled frames.");
                    Assert.That(app.HighScoreTierShown073,Is.GreaterThan(0),"The fixture must reach a celebration tier.");
                }
                finally{app.enabled=enabled;}
                app.SkipVisual();yield return W11Settle();
            }
            File.WriteAllLines(Path.Combine(W11Output,"ax01-report.txt"),report);
            string head=IdentityHead20X();
            Assert.That(head,Does.Match("^[0-9a-f]{40}$"),"Record the actual source revision.");
            string resolution=Screen.width+"x"+Screen.height;
            string run=Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","artifacts","cache","v11","w25",
                "ax01-frames","results",resolution,head+"-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ")));
            Directory.CreateDirectory(run);
            string input="source="+head+"\nresolution="+resolution+"\nunity="+Application.unityVersion
                +"\ntestAssemblyMvid="+GetType().Module.ModuleVersionId+"\ntestSourceSha256="
                +ExtraFileHash07(Path.Combine(Application.dataPath,"Game/Tests/PlayMode/V11PlayScreenAxPlayModeTests.cs"));
            File.WriteAllText(Path.Combine(run,"input.txt"),input);
            TestContext.Out.WriteLine("AX01_RUN "+run+" "+input);
            var failures=new List<string>();
            foreach(var mode in new[]{Art07RecordingMode.NormalHighest,Art07RecordingMode.FastHighest,Art07RecordingMode.NormalQualified})
            {
                string directory=Path.Combine(run,mode.ToString());
                yield return Ax01CompleteTrack(W11Ax01AllFrames(mode,directory),directory,failures);
            }
            Assert.That(failures,Is.Empty,string.Join("\n",failures));
        }

        // Run all three evidence tracks even when a preceding comparison or recorder failed.
        // Dispose every iterator so calibration, camera capture, and recorder finally blocks still run.
        private static IEnumerator Ax01CompleteTrack(IEnumerator track,string directory,List<string> failures)
        {
            var stack=new Stack<IEnumerator>();stack.Push(track);
            try
            {
                while(stack.Count>0)
                {
                    var current=stack.Peek();bool more=false;object value=null;Exception failure=null;
                    try {more=current.MoveNext();if(more)value=current.Current;}
                    catch(Exception ex){failure=ex;}
                    if(failure!=null)
                    {
                        Directory.CreateDirectory(directory);
                        File.WriteAllText(Path.Combine(directory,"track-failure.txt"),failure.ToString());
                        failures.Add(Path.GetFileName(directory)+": "+failure.Message);
                        break;
                    }
                    if(!more){(stack.Pop() as IDisposable)?.Dispose();continue;}
                    if(value is IEnumerator nested&&!(value is CustomYieldInstruction)){stack.Push(nested);continue;}
                    yield return value;
                }
            }
            finally {while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();}
        }

        [Serializable] private sealed class Ax01Die
        {
            public string id;
            public Vector2[] hull;
            public int inside, changed;
            public bool displayedLocked, poolTargetHit, insidePoolTarget, moving;
        }

        [Serializable] private sealed class Ax01Frame
        {
            public int unityFrame;
            public string phase, background, backgroundSha256, image, imageSha256;
            public bool playing, stampPlaying, stampVisible;
            public string recordingStage, viewPhase, viewHand, submittedHand, slotsTitle, rollText, ownership;
            public bool viewRolled, preRollVeil;
            public int capacity, emptySlots, highScoreTier;
            public float highScoreElapsed;
            public string[] shownLocked;
            public Rect[] stampRects;
            public RectInt bounds;
            public Ax01Die[] dice;
        }

        // Subset of the existing production recorder's JSONL. Its row numbers index complete,
        // lossless composed screenshots, including the first non-playing presentation frame.
        [Serializable] private sealed class Ax01RecordedFrame
        {
            public int unityFrame, presentationTicks;
            public string stage, file, pngSha256, saveSha256, stateHash, receiptId;
            public double presentationSeconds;
            public bool presentationPlaying;
        }

        // In this project's SDR URP renderer the camera's capture action is called before the
        // ScreenSpaceOverlay pass. Copy the same camera render; do not disable the Canvas or
        // render a second pose. Resolve the package facade without changing the tests' asmdef.
        private sealed class Ax01CameraPixels : IDisposable
        {
            private readonly Camera camera;
            private readonly Type bridge;
            private readonly Action<UnityEngine.Rendering.RenderTargetIdentifier,UnityEngine.Rendering.CommandBuffer> action;
            private readonly RenderTexture reference;
            public int frame = -1;
            public bool flipY;

            public Ax01CameraPixels(Camera camera)
            {
                this.camera=camera;
                bridge=Type.GetType("UnityEngine.Rendering.CameraCaptureBridge, Unity.RenderPipelines.Core.Runtime",true);
                reference=new RenderTexture(Screen.width,Screen.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
                reference.name="AX01 same-frame camera colour"; reference.filterMode=FilterMode.Point;
                Assert.That(reference.Create(),Is.True);
                action=(source,cmd)=>{frame=Time.frameCount;cmd.Blit(source,reference);};
                bridge.GetMethod("AddCaptureAction").Invoke(null,new object[]{camera,action});
            }

            public Texture2D Read(RectInt r,bool invert)
            {
                Assert.That(frame,Is.EqualTo(Time.frameCount),"Camera reference does not belong to this displayed frame.");
                var previous=RenderTexture.active;
                var texture=new Texture2D(r.width,r.height,TextureFormat.RGB24,false);
                try
                {
                    RenderTexture.active=reference;
                    int y=invert?reference.height-r.yMax:r.yMin;
                    texture.ReadPixels(new Rect(r.x,y,r.width,r.height),0,0,false);texture.Apply(false,false);
                    if(invert)
                    {
                        var pixels=texture.GetPixels32();var row=new Color32[r.width];
                        for(int i=0;i<r.height/2;i++)
                        {
                            int a=i*r.width,b=(r.height-i-1)*r.width;
                            Array.Copy(pixels,a,row,0,r.width);Array.Copy(pixels,b,pixels,a,r.width);Array.Copy(row,0,pixels,b,r.width);
                        }
                        texture.SetPixels32(pixels);texture.Apply(false,false);
                    }
                    return texture;
                }
                catch {UObj.Destroy(texture);throw;}
                finally {RenderTexture.active=previous;}
            }

            public void Dispose()
            {
                try {bridge.GetMethod("RemoveCaptureAction").Invoke(null,new object[]{camera,action});}
                finally {reference.Release();UObj.Destroy(reference);}
            }
        }

        private static Rect Ax01ScreenRect(RectTransform r)
        {
            var corners=new Vector3[4];
            r.GetWorldCorners(corners);
            var points=corners.Select(p=>RectTransformUtility.WorldToScreenPoint(null,p)).ToArray();
            return Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));
        }

        private Ax01Frame Ax01Geometry(string[] ids,string submittedHand=null)
        {
            var dice=new List<Ax01Die>();
            var view=app.ViewState;
            var screen=app.canvas.transform.Find("Screen");
            var controls=screen.Cast<Transform>().Where(t=>t.gameObject.activeInHierarchy).ToLookup(t=>t.name);
            Transform Control(string name)=>controls[name].SingleOrDefault();
            var roll=Control("Roll")?.GetComponent<UnityEngine.UI.Button>();
            var title=Control("SlotsTitle")?.GetComponent<TMP_Text>();
            var stampLayer=app.canvas.transform.Find("Art07QualifiedLayer");
            var stampRects=new List<Rect>();
            if(stampLayer!=null)
                foreach(string part in new[]{"FullStamp","PersistentBadge"})
                {
                    var layer=stampLayer.Find(part)?.GetComponent<CanvasGroup>();
                    if(layer!=null&&layer.isActiveAndEnabled&&layer.alpha>0)
                        stampRects.Add(Ax01ScreenRect((RectTransform)layer.transform));
                }
            foreach(string id in ids)
            {
                var p=app.table.GetFaceOrientation(id);
                var body=p==null?null:p.Find("Body")?.GetComponent<Renderer>();
                bool draw=body!=null&&body.enabled&&body.gameObject.activeInHierarchy&&!body.forceRenderingOff;
                var hull=draw?W11DieHull(id,.16f).ToArray():Array.Empty<Vector2>();
                bool locked=view.hand.orderedLockedIds.Contains(id);
                var hit=Control("Die_"+id) as RectTransform;
                bool poolHit=!locked&&hit!=null&&hit.GetComponent<DiePointer>()!=null;
                var center=draw?hull.Aggregate(Vector2.zero,(sum,point)=>sum+point)/hull.Length:Vector2.zero;
                dice.Add(new Ax01Die{id=id,hull=hull,displayedLocked=locked,moving=app.table.LayoutPlayingFor07(id),
                    poolTargetHit=poolHit,insidePoolTarget=poolHit&&draw&&RectTransformUtility.RectangleContainsScreenPoint(hit,center,null)});
            }
            var all=dice.SelectMany(d=>d.hull).ToArray();
            var stamp=stampLayer?.Find("FullStamp")?.GetComponent<CanvasGroup>();
            var frame=new Ax01Frame
            {
                unityFrame=Time.frameCount,phase=app.CurrentPresentationPhase07?.ToString(),playing=app.PresentationPlaying07,
                stampPlaying=app.QualifiedStampPlaying07,stampVisible=stamp!=null&&stamp.isActiveAndEnabled&&stamp.alpha>0,
                stampRects=stampRects.ToArray(),highScoreTier=app.HighScoreTierShown073,highScoreElapsed=app.HighScoreElapsed073,
                submittedHand=submittedHand,viewHand=view.hand.handId,viewPhase=view.phase,viewRolled=view.hand.hasRolled,
                shownLocked=view.hand.orderedLockedIds.ToArray(),capacity=view.stage.capacity,slotsTitle=title?.text,
                rollText=roll!=null&&roll.interactable?roll.GetComponentInChildren<TMP_Text>().text:null,
                preRollVeil=Control("PreRollVeil11")!=null,
                emptySlots=Enumerable.Range(0,view.stage.capacity).Count(i=>Control("EmptySlot"+i)!=null),dice=dice.ToArray()
            };
            if(submittedHand!=null)
            {
                bool rail=frame.viewHand==submittedHand&&frame.shownLocked.SequenceEqual(ids)
                    &&frame.slotsTitle=="提交栏 "+ids.Length+" / "+frame.capacity;
                bool newPool=frame.viewHand!=submittedHand&&frame.viewPhase=="HandReady"&&!frame.viewRolled
                    &&frame.shownLocked.Length==0&&frame.slotsTitle=="提交栏 0 / "+frame.capacity
                    &&frame.emptySlots==frame.capacity&&frame.preRollVeil&&frame.rollText!=null
                    &&frame.rollText.StartsWith("掷骰",StringComparison.Ordinal)
                    &&dice.All(d=>d.poolTargetHit&&d.hull.Length>0);
                frame.ownership=rail?"submitted":newPool?(dice.Any(d=>d.moving||!d.insidePoolTarget)?"returning-to-pool":"next-hand-pool"):"unexplained";
            }
            if(all.Length>0)
            {
                int x0=Mathf.Clamp(Mathf.FloorToInt(all.Min(v=>v.x)),0,Screen.width-1);
                int x1=Mathf.Clamp(Mathf.CeilToInt(all.Max(v=>v.x))+1,x0+1,Screen.width);
                int y0=Mathf.Clamp(Mathf.FloorToInt(all.Min(v=>v.y)),0,Screen.height-1);
                int y1=Mathf.Clamp(Mathf.CeilToInt(all.Max(v=>v.y))+1,y0+1,Screen.height);
                frame.bounds=new RectInt(x0,y0,x1-x0,y1-y0);
            }
            return frame;
        }

        private static int Ax01Compare(Ax01Frame frame,Texture2D visible,Texture2D bare)
        {
            var full=visible.GetPixels32();var refPixels=bare.GetPixels32();int total=0;
            foreach(var die in frame.dice)
            {
                die.inside=die.changed=0;if(die.hull.Length==0)continue;
                var hull=die.hull.ToList();
                int x0=Math.Max(frame.bounds.xMin,Mathf.FloorToInt(hull.Min(v=>v.x))),x1=Math.Min(frame.bounds.xMax,Mathf.CeilToInt(hull.Max(v=>v.x))+1);
                int y0=Math.Max(frame.bounds.yMin,Mathf.FloorToInt(hull.Min(v=>v.y))),y1=Math.Min(frame.bounds.yMax,Mathf.CeilToInt(hull.Max(v=>v.y))+1);
                for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++)
                {
                    if(!W11Inside(hull,new Vector2(x+.5f,y+.5f)))continue;
                    die.inside++;
                    var a=full[y*visible.width+x];var b=refPixels[(y-frame.bounds.y)*bare.width+x-frame.bounds.x];
                    if(Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+Math.Abs(a.b-b.b)>12)die.changed++;
                }
                total+=die.changed;
            }
            return total;
        }

        private IEnumerator Ax01Calibrate(Ax01CameraPixels probe,string dir)
        {
            var ids=app.Session.State.hand.orderedLockedIds.ToArray();
            Assert.That(ids,Has.Length.GreaterThan(0));
            var canvas=app.canvas;bool enabled=canvas.enabled;GameObject marker=null;
            try
            {
                canvas.enabled=false;
                yield return new WaitForEndOfFrame();
                var geometry=Ax01Geometry(ids);
                var full=ScreenCapture.CaptureScreenshotAsTexture();
                try
                {
                    var errors=new int[2];
                    for(int i=0;i<2;i++)
                    {
                        var bare=probe.Read(geometry.bounds,i!=0);
                        try {errors[i]=Ax01Compare(geometry,full,bare);}
                        finally {UObj.Destroy(bare);}
                    }
                    probe.flipY=errors[1]<errors[0];
                    var match=probe.Read(geometry.bounds,probe.flipY);
                    try
                    {
                        Ax01Compare(geometry,full,match);
                        File.WriteAllBytes(Path.Combine(dir,"calibration-off-camera.png"),match.EncodeToPNG());
                        File.WriteAllBytes(Path.Combine(dir,"calibration-off.png"),full.EncodeToPNG());
                    }
                    finally {UObj.Destroy(match);}
                    File.WriteAllText(Path.Combine(dir,"calibration-off.json"),JsonUtility.ToJson(geometry,true));
                    foreach(var die in geometry.dice)
                    {
                        Assert.That(die.inside,Is.GreaterThan(200),"Calibration body "+die.id);
                        Assert.That(die.changed,Is.LessThanOrEqualTo(2),"Native camera colours and displayed colours differ on "+die.id+"; normal="+errors[0]+", flipped="+errors[1]);
                    }
                }
                finally {UObj.Destroy(full);}
                canvas.enabled=true;
                var hull=geometry.dice[0].hull;
                var screen=hull.Aggregate(Vector2.zero,(a,b)=>a+b)/hull.Length;
                var canvasRect=(RectTransform)canvas.transform;
                marker=new GameObject("AX01 deliberate foreground occluder",typeof(RectTransform),typeof(UnityEngine.UI.Image));
                var rect=(RectTransform)marker.transform;rect.SetParent(canvasRect,false);rect.SetAsLastSibling();
                Assert.That(RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,screen,null,out var local),Is.True);
                rect.anchorMin=rect.anchorMax=canvasRect.pivot;rect.anchoredPosition=local;
                rect.sizeDelta=Vector2.one*24/canvas.scaleFactor;
                var image=marker.GetComponent<UnityEngine.UI.Image>();image.color=Color.magenta;image.raycastTarget=false;
                Canvas.ForceUpdateCanvases();yield return null;yield return new WaitForEndOfFrame();
                geometry=Ax01Geometry(ids);full=ScreenCapture.CaptureScreenshotAsTexture();
                var reference=probe.Read(geometry.bounds,probe.flipY);
                try
                {
                    Ax01Compare(geometry,full,reference);
                    var rectPixels=full.GetPixels(Mathf.RoundToInt(screen.x)-6,Mathf.RoundToInt(screen.y)-6,12,12);
                    Assert.That(rectPixels.Count(c=>c.r>.85f&&c.g<.15f&&c.b>.85f),Is.GreaterThan(100),
                        "The deliberate foreground patch must actually be painted over this die.");
                    File.WriteAllBytes(Path.Combine(dir,"calibration-cover-camera.png"),reference.EncodeToPNG());
                    File.WriteAllText(Path.Combine(dir,"calibration-cover.json"),JsonUtility.ToJson(geometry,true));
                    File.WriteAllBytes(Path.Combine(dir,"calibration-cover.png"),full.EncodeToPNG());
                    Assert.That(geometry.dice[0].changed,Is.GreaterThan(200),"A painted UI square over the first die must fail the real comparison.");
                }
                finally {UObj.Destroy(full);UObj.Destroy(reference);}
            }
            finally {if(marker!=null){marker.SetActive(false);UObj.Destroy(marker);}canvas.enabled=enabled;Canvas.ForceUpdateCanvases();}
        }

        private IEnumerator W11Ax01AllFrames(Art07RecordingMode mode,string directory)
        {
            Directory.CreateDirectory(directory);
            Assert.That(app.canvas.renderMode,Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            var fixture=mode==Art07RecordingMode.NormalQualified?Art07FixtureKind.Qualified:Art07FixtureKind.G147;
            Art07Fixtures.Apply(app,fixture);
            yield return W11Settle();
            var refs=new Dictionary<int,Ax01Frame>();
            using(var probe=new Ax01CameraPixels(app.table.viewCamera))
            {
                yield return Ax01Calibrate(probe,directory);
                yield return null;
                string recording=Path.Combine(directory,"recording");
                var recorder=Art07VisualRecorder.Begin(app,recording,mode);
                try
                {
                    string submittedHand=app.Session.State.hand.handId;
                    var ids=app.Session.State.hand.orderedLockedIds.ToArray();
                    Assert.That(ids,Is.Not.Empty);Assert.That(ids.Distinct().Count(),Is.EqualTo(ids.Length));
                    bool submitted=false;
                    string observed=Path.Combine(directory,"camera-geometry.jsonl");
                    while(recorder.IsRecording)
                    {
                        yield return new WaitForEndOfFrame();
                        if(!recorder.IsRecording)break;
                        submitted|=app.PresentationPlaying07;
                        if(submitted)
                        {
                            var frame=Ax01Geometry(ids,submittedHand);
                            if(frame.bounds.width>0)
                            {
                                var pixels=probe.Read(frame.bounds,probe.flipY);
                                try
                                {
                                    frame.background="camera-"+frame.unityFrame+".png";
                                    var bytes=pixels.EncodeToPNG();File.WriteAllBytes(Path.Combine(directory,frame.background),bytes);
                                    frame.backgroundSha256=ExtraHash07(bytes);
                                }
                                finally {UObj.Destroy(pixels);}
                            }
                            refs.Add(frame.unityFrame,frame);
                            File.AppendAllText(observed,JsonUtility.ToJson(frame)+Environment.NewLine);
                        }
                        yield return null;
                    }
                    Assert.That(recorder.Status,Is.EqualTo("captured_pending_visual_review"),recorder.Error);
                    var rows=File.ReadLines(Path.Combine(recording,"frames.jsonl")).Select(JsonUtility.FromJson<Ax01RecordedFrame>)
                        .Where(f=>f.stage=="presentation"||f.stage=="hold").ToArray();
                    var run=rows.Where(f=>f.stage=="presentation").ToArray();
                    Assert.That(rows.Count(f=>f.stage=="hold"),Is.EqualTo(30));
                    Assert.That(rows.Length,Is.GreaterThan(2));
                    Assert.That(rows[0].presentationSeconds,Is.EqualTo(0).Within(.00001),"The initial committed frame is missing.");
                    Assert.That(run[0].presentationPlaying,Is.True);Assert.That(run[run.Length-1].presentationPlaying,Is.False);
                    Assert.That(rows[0].receiptId,Is.Not.Empty);Assert.That(rows[0].saveSha256,Does.Match("^[0-9a-f]{64}$"));
                    Assert.That(refs.Keys.OrderBy(k=>k),Is.EqualTo(rows.Select(f=>f.unityFrame)));
                    var issues=new List<string>(); string ledger=Path.Combine(directory,"dice-per-frame.jsonl");
                    using(var writer=new StreamWriter(ledger,false,new System.Text.UTF8Encoding(false)))
                    {
                        foreach(var row in rows)
                        {
                            int i=row.unityFrame-rows[0].unityFrame;
                            Assert.That(i,Is.InRange(0,rows.Length-1));Assert.That(rows[i].unityFrame,Is.EqualTo(row.unityFrame));
                            Assert.That(row.presentationTicks,Is.EqualTo(i>0&&i<run.Length?1:0));
                            Assert.That(row.presentationSeconds,Is.EqualTo(Math.Min(i,run.Length-1)/60d).Within(.00001));
                            Assert.That(row.presentationPlaying,Is.EqualTo(i<run.Length-1));
                            Assert.That(row.saveSha256,Is.EqualTo(rows[0].saveSha256));Assert.That(row.stateHash,Is.EqualTo(rows[0].stateHash));
                            Assert.That(row.receiptId,Is.EqualTo(rows[0].receiptId));
                            var frame=refs[row.unityFrame];frame.recordingStage=row.stage;frame.image="recording/"+row.file;frame.imageSha256=row.pngSha256;
                            Assert.That(frame.playing,Is.EqualTo(row.presentationPlaying));
                            var shown=new Texture2D(2,2);Texture2D bare=null;
                            try
                            {
                                var png=File.ReadAllBytes(Path.Combine(directory,frame.image));
                                Assert.That(ExtraHash07(png),Is.EqualTo(frame.imageSha256));
                                Assert.That(shown.LoadImage(png),Is.True);
                                Assert.That((shown.width,shown.height),Is.EqualTo((Screen.width,Screen.height)));
                                if(frame.background!=null)
                                {
                                    png=File.ReadAllBytes(Path.Combine(directory,frame.background));
                                    Assert.That(ExtraHash07(png),Is.EqualTo(frame.backgroundSha256));
                                    bare=new Texture2D(2,2);
                                    Assert.That(bare.LoadImage(png),Is.True);
                                    Assert.That((bare.width,bare.height),Is.EqualTo((frame.bounds.width,frame.bounds.height)));
                                    Ax01Compare(frame,shown,bare);
                                }
                            }
                            finally {UObj.DestroyImmediate(shown);if(bare!=null)UObj.DestroyImmediate(bare);}
                            // A committed hand still shows its submitted dice until its real HUD hands them to
                            // the following unrolled hand. The body IDs persist while they move back under the pool veil.
                            if(frame.ownership=="unexplained"||frame.playing&&frame.ownership!="submitted")
                                issues.Add("frame "+row.unityFrame+" inconsistent ownership: "+frame.ownership+" "+frame.viewHand+" "+frame.slotsTitle+" "+frame.rollText);
                            foreach(var die in frame.dice)
                            {
                                if(frame.playing||frame.ownership=="submitted"||die.displayedLocked)
                                    if(die.inside<=200||die.changed>2||die.hull.Any(p=>p.x<0||p.y<0||p.x>=Screen.width||p.y>=Screen.height))
                                        issues.Add("frame "+row.unityFrame+" "+die.id+" "+die.changed+"/"+die.inside);
                                if(die.hull.Length==0||frame.stampRects.Length==0)continue;
                                // A disjoint rectangle proves every painted stamp pixel misses this die even
                                // while the next-hand pre-roll veil legitimately changes every body pixel.
                                var box=Rect.MinMaxRect(die.hull.Min(p=>p.x),die.hull.Min(p=>p.y),die.hull.Max(p=>p.x),die.hull.Max(p=>p.y));
                                if(frame.stampRects.Any(r=>r.Overlaps(box)))
                                    issues.Add("frame "+row.unityFrame+" verify overlapping stamp and "+die.id);
                            }
                            writer.WriteLine(JsonUtility.ToJson(frame));
                            // PNG decoding runs after the capture clock and outside gameplay performance checks.
                            if(i%8==7){writer.Flush();yield return null;}
                        }
                    }
                    TestContext.Out.WriteLine("AX01_FRAMES "+mode+" presentation="+run.Length+" hold="+(rows.Length-run.Length)
                        +" report="+ExtraFileHash07(Path.Combine(recording,"recording.json"))+" frames="+ExtraFileHash07(Path.Combine(recording,"frames.jsonl"))
                        +" ledger="+ExtraFileHash07(ledger)+" path="+ledger);
                    if(fixture==Art07FixtureKind.Qualified)
                    {
                        Assert.That(refs.Values.Any(f=>f.playing&&f.stampPlaying&&f.stampVisible),Is.True,"The real qualifying stamp never appeared over the live hand.");
                        Assert.That(refs.Values.Any(f=>!f.playing&&f.stampPlaying&&f.stampVisible),Is.True,"The nonblocking stamp tail was not captured.");
                        Assert.That(refs[rows[rows.Length-1].unityFrame].stampPlaying,Is.False);
                    }
                    Assert.That(refs[rows.Last().unityFrame].ownership,Is.EqualTo("next-hand-pool"),"All submitted dice must finish returning to their new hand's actual pool targets.");
                    Assert.That(issues,Is.Empty,string.Join("\n",issues.Take(15)));
                }
                finally {if(recorder.IsRecording)recorder.Cancel();UObj.Destroy(recorder.gameObject);}
            }
        }

        // ------------------------------------------------------------------ AX-02: one primary action in every state
        /// <summary>Screen-level buttons in the primary colour. The formula chips (children of the score row) are not actions;
        /// in the default palette the card chip shares the gold of the primary colour.</summary>
        private UnityEngine.UI.Button[] W11PrimaryButtons()
            =>app.canvas.GetComponentsInChildren<UnityEngine.UI.Button>(false).Where(b=>b.gameObject.activeInHierarchy&&b.targetGraphic!=null
                &&b.transform.parent!=null&&b.transform.parent.name=="Screen"&&W11PrimaryColoured(b.targetGraphic.color)).ToArray();
        /// <summary>
        /// V1.1 (W11c): a face drawn in the primary colour. The label rule (W11b) may deepen or lighten a face in 5 % steps
        /// so its label reads (the Swiss primary E80E16 draws as D10D16), so this compares the colour family, not the exact
        /// palette value: hue within 10 degrees, saturation within 0.2, value from 60 % of the primary's to 0.1 above it.
        /// </summary>
        private static bool W11PrimaryColoured(Color face)
        {
            Color.RGBToHSV(face,out float h,out float s,out float v);Color.RGBToHSV(UiKit.Primary,out float ph,out float ps,out float pv);
            float dh=Mathf.Abs(h-ph);dh=Mathf.Min(dh,1-dh);
            return dh<=10f/360f&&Mathf.Abs(s-ps)<=.2f&&v>=pv*.6f&&v<=pv+.1f;
        }
        /// <summary>One primary action in its fixed place. Enabled, it is the one primary-coloured button; a primary action that
        /// cannot be taken yet ("最多锁 N 颗") keeps the place, disabled, and then no button is primary-coloured.</summary>
        private void W11AssertOnePrimary(string state,string expected,bool enabled=true)
        {
            Canvas.ForceUpdateCanvases();var primary=W11PrimaryButtons();
            state=app.ThemeId11+" "+Mathf.RoundToInt(UiKit.FontScale*100)+"% "+state;
            if(expected==null){Assert.That(primary,Is.Empty,state+": no action is available, so no primary-coloured button.");return;}
            Assert.That(new[]{"Roll","Confirm","Submit"},Does.Contain(expected));
            if(enabled)Assert.That(primary.Select(b=>b.name).ToArray(),Is.EqualTo(new[]{expected}),state+": exactly one primary-coloured button.");
            else Assert.That(primary,Is.Empty,state+": a disabled primary action is not drawn in the primary colour.");
            var button=Button(expected);Assert.That(button.interactable,Is.EqualTo(enabled),state);
            var rect=(RectTransform)button.transform;
            // The primary action always sits in the same place.
            Assert.That(rect.anchoredPosition,Is.EqualTo(W11PrimaryPosition??rect.anchoredPosition),state+": the primary button moved.");
            W11PrimaryPosition=rect.anchoredPosition;
        }
        private Vector2? W11PrimaryPosition;
        [UnityTest] public IEnumerator PLAY_V11_AX02_OnePrimaryActionButtonInEveryState()
        {
            // V1.1 (W11c): in the default and the Swiss theme (the Swiss primary face is drawn deeper for its label).
            bool hadPreview=PlayerPrefs.HasKey(ThemeRegistry.PreviewKey);int previewValue=PlayerPrefs.GetInt(ThemeRegistry.PreviewKey,0);
            string savedTheme=PlayerPrefs.GetString(ThemeRegistry.PreferenceKey,GameTheme.DefaultId);
            PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,1);
            try
            {
                StartRun("71102");
                foreach(string theme in new[]{"default","swiss"})
                {
                    app.ApplyTheme11(theme);Assert.That(app.ThemeId11,Is.EqualTo(theme));
                    yield return W11Ax02Pass(theme);
                }
            }
            finally
            {
                app.ApplyTheme11(GameTheme.DefaultId);
                if(hadPreview)PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,previewValue);else PlayerPrefs.DeleteKey(ThemeRegistry.PreviewKey);
                PlayerPrefs.SetString(ThemeRegistry.PreferenceKey,savedTheme);PlayerPrefs.Save();
            }
        }
        private IEnumerator W11Ax02Pass(string theme)
        {
            foreach(int font in new[]{100,150})
            {
                W11TextScale(font);W11PrimaryPosition=null;
                W11Apply(W11Scene(W11Cards3,new[]{1,2,3,4,5},"bbbbb",0,false));yield return W11Settle();
                W11AssertOnePrimary("not thrown","Roll");Assert.That(HasActive("Confirm")||HasActive("Clear")||HasActive("Submit"),Is.False,"Unavailable actions are not shown.");
                Click("Roll");yield return WaitForIdle();yield return W11Settle();
                W11AssertOnePrimary("thrown, nothing chosen","Roll");
                ClickDie(app.Session.State.dice[0].dieId);yield return null;
                W11AssertOnePrimary("dice chosen","Confirm");Assert.That(Button("Clear").interactable,Is.True);Assert.That(HasActive("Submit"),Is.False);
                Click("Confirm");yield return WaitForIdle();yield return W11Settle();
                W11AssertOnePrimary("locked, can submit","Submit");Assert.That(Button("Roll").interactable,Is.True,"Re-roll stays as the secondary action.");
                // Too many chosen for the room left: the primary stays Confirm, disabled, and says how many fit.
                W11Apply(W11Scene(W11Cards3,new[]{4,4,6,2,4},"bbbrb",3,true,capacity:4));
                foreach(var die in app.Session.State.dice.Where(d=>!app.Session.State.hand.orderedLockedIds.Contains(d.dieId)))app.Session.TogglePending(die.dieId);
                app.Refresh();yield return null;
                W11AssertOnePrimary("too many chosen","Confirm",enabled:false);
                Assert.That(Button("Confirm").GetComponentInChildren<TextMeshProUGUI>().text,Does.Contain("最多锁 1 颗"));
                // Thrown, out of throws, nothing locked: no action to emphasise.
                var scene=W11Scene(W11Cards3,new[]{4,4,6,2,4},"bbbrb",0,true);scene.rollsRemaining=0;W11Apply(scene);yield return W11Settle();
                W11AssertOnePrimary("out of throws, nothing locked",null);
                // Busy: the presentation offers only Skip, as a second-level button.
                W11Apply(W11Scene(W11Cards3,new[]{4,4,4,2,6},"bbbrb",3,true));yield return W11Settle();
                var result=app.Send(new SessionCommand{kind=CommandKind.Submit});Assert.That(result.accepted,Is.True,result.error);
                Canvas.ForceUpdateCanvases();Assert.That(app.IsBusy,Is.True);W11AssertOnePrimary("scoring",null);Assert.That(Button("Skip").interactable,Is.True);
                app.SkipVisual();yield return W11Settle();
            }
            W11TextScale(100);
        }

        // ------------------------------------------------------------------ AX-05: every source pops where it is
        [UnityTest] public IEnumerator PLAY_V11_AX05_EachContributingDieAndCardPopsAtItsSource()
        {
            StartRun("71105");
            foreach(string speed in new[]{"Normal","Fast"})
            {
                if(speed!="Normal")FinalOptions07(speed,false);
                W11Apply(W11Scene(new[]{"C39","C34","C08","C46","C50"},new[]{4,4,4,4,2},"bbbbr",4,true,target:"100000"));yield return W11Settle();
                var dice=app.Session.State.hand.orderedLockedIds.ToArray();var slots=dice.ToDictionary(id=>id,id=>V06ScreenRect(FindActive<RectTransform>("Slot"+Array.IndexOf(dice,id))));
                var cards=app.Session.State.controllers.ToDictionary(c=>c.instanceId,c=>V06ScreenRect((RectTransform)Button("Controller_"+c.instanceId).transform));
                var before=app.Session.State;var result=app.Send(new SessionCommand{kind=CommandKind.Submit});Assert.That(result.accepted,Is.True,result.error);
                var receipt=PresentationReceiptAdapter.BuildCommitted(result,before,app.Session.State,app.Config);
                var popDice=new HashSet<string>();var popCards=new HashSet<string>();
                double deadline=Time.realtimeSinceStartupAsDouble+8;
                while(app.PresentationPlaying07&&Time.realtimeSinceStartupAsDouble<deadline)
                {
                    var layer=app.canvas.transform.Find("Score11Layer");
                    if(layer!=null)foreach(Transform child in layer)
                    {
                        if(!child.gameObject.activeInHierarchy)continue;var text=child.GetComponentInChildren<TextMeshProUGUI>();
                        if(text==null||string.IsNullOrEmpty(text.text)||text.color.a<.9f)continue;var r=V06ScreenRect((RectTransform)child);
                        if(child.name.StartsWith("DieTicket11_",StringComparison.Ordinal))
                        {
                            string id=child.name.Substring(12);Assert.That(slots.ContainsKey(id),Is.True,"Ticket on a die that was not submitted: "+id);
                            var hull=W11DieHull(id,0);float top=hull.Max(p=>p.y);
                            Assert.That(r.yMin,Is.GreaterThanOrEqualTo(top-1),"The +N of "+id+" must sit above its die, not on it.");
                            Assert.That(r.xMin>=slots[id].xMin-2&&r.xMax<=slots[id].xMax+2,Is.True,"The +N of "+id+" stays over its own slot.");
                            Assert.That(text.text,Does.Match(@"^\+\d|封印"));popDice.Add(id);
                        }
                        if(child.name.StartsWith("CardBadge11_",StringComparison.Ordinal))
                        {
                            string id=child.name.Substring(12);Assert.That(cards.ContainsKey(id),Is.True);
                            var card=cards[id];Assert.That(r.xMin>=card.xMin-2&&r.xMax<=card.xMax+2&&r.yMax<=card.yMax+20*app.canvas.scaleFactor,Is.True,"The badge of "+id+" stays on its card.");
                            popCards.Add(id);
                        }
                    }
                    yield return null;
                }
                CollectionAssert.AreEquivalent(dice,popDice,speed+": every submitted die shows its +N.");
                CollectionAssert.IsSubsetOf(receipt.ScoringEmitterIds.ToArray(),popCards.ToArray(),speed+": every card that took part shows its badge.");
                Assert.That(popCards,Is.Not.Empty);
                app.SkipVisual();yield return W11Settle();
            }
        }

        // ------------------------------------------------------------------ AX-03 and the new texts: drawn, inside, readable
        private static readonly string[] W11BodyTexts={"Chapter","MoneyLabel11","StageCardTitle11","BossModifierText","BossLoot11","StageGoalLabel11","StageReward11","StageNote11",
            "RollBudgetLabel","SubmitBudgetLabel","ItemsTitle","SlotsTitle","LockRule","Hand","FormulaCaptionText11","ActionReason","GoalGapLabel11","GoalReward11","QualifiedReward0","QualifiedReward1","QualifiedReward2","PoolTitle"};
        private static readonly string[] W11KeyNumbers={"StageScore","Target","Money","RollBudgetValue","SubmitBudgetValue","FinalScore","StableResultScore07","GoalGap11","StageGoal11","StageLabel11","BossModifierName"};
        private List<Rect> W11GlyphCheck(TextMeshProUGUI text,string scope,UnityEngine.UI.Graphic[] order=null,List<string> frameFailures=null)
        {
            text.ForceMeshUpdate();int expected=text.GetParsedText().Count(c=>!char.IsWhiteSpace(c));
            Assert.That(text.isTextTruncated,Is.False,scope+" "+text.name+" truncated");
            var rects=W11bGlyphRects(text);
            Assert.That(rects.Count,Is.EqualTo(expected),scope+" "+text.name+" draws "+rects.Count+" of "+expected+" characters: '"+text.text+"'");
            var own=V06ScreenRect(text.rectTransform);float slack=1.5f;
            foreach(var r in rects)Assert.That(r.xMin>=own.xMin-slack&&r.xMax<=own.xMax+slack&&r.yMin>=own.yMin-slack&&r.yMax<=own.yMax+slack,Is.True,scope+" "+text.name+" glyph "+r+" outside its box "+own);
            // W11b: and inside the visible frame of the surface it is drawn on (the frame line, bevel or ring is not room
            // for text), which is what the player sees, not the text's own box.
            if(order!=null)
            {
                var frame=W11bVisibleFrame(text,rects,order,out string surface);
                if(frame.HasValue)foreach(var r in rects)
                {
                    bool inside=r.xMin>=frame.Value.xMin-slack&&r.xMax<=frame.Value.xMax+slack&&r.yMin>=frame.Value.yMin-slack&&r.yMax<=frame.Value.yMax+slack;
                    string message=scope+" "+text.name+" glyph "+r+" outside the visible frame "+frame.Value+" of "+surface+": '"+text.text+"'";
                    if(frameFailures!=null){if(!inside){frameFailures.Add(message);break;}}
                    else Assert.That(inside,Is.True,message);
                }
            }
            return rects;
        }
        private IEnumerator W11TextPass(string scope)
        {
            Canvas.ForceUpdateCanvases();
            var texts=app.canvas.GetComponentsInChildren<TextMeshProUGUI>(false).Where(t=>t.gameObject.activeInHierarchy&&!string.IsNullOrWhiteSpace(t.text)&&
                (W11BodyTexts.Contains(t.name)||W11KeyNumbers.Contains(t.name)||t.name=="ShortText11"||t.name=="Name"&&t.transform.parent.name.StartsWith("Controller_",StringComparison.Ordinal)||t.name.StartsWith("Pending")||t.transform.parent.name.StartsWith("Factor",StringComparison.Ordinal))).ToArray();
            Assert.That(texts.Length,Is.GreaterThan(15),scope+": too few texts found.");
            var all=new List<KeyValuePair<TextMeshProUGUI,List<Rect>>>();
            var order=app.canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(false);var frameFailures=new List<string>();
            foreach(var t in texts)
            {
                var rects=W11GlyphCheck(t,scope,order,frameFailures);all.Add(new KeyValuePair<TextMeshProUGUI,List<Rect>>(t,rects));
                // AX-03: body text at least 13 px and key numbers at least 21 px at 1280x720 (19.5 and 31.5 design units).
                float px=t.fontSize*app.canvas.scaleFactor,design=t.fontSize;
                if(W11BodyTexts.Contains(t.name)||t.name=="ShortText11")Assert.That(design,Is.GreaterThanOrEqualTo(19.49f),scope+" "+t.name+" body text "+design+" units");
                if(W11KeyNumbers.Contains(t.name)&&t.name!="StageLabel11"&&t.name!="BossModifierName")Assert.That(design,Is.GreaterThanOrEqualTo(31.49f),scope+" "+t.name+" key number "+design+" units");
                if(Screen.height==720&&(W11BodyTexts.Contains(t.name)||t.name=="ShortText11"))Assert.That(px,Is.GreaterThanOrEqualTo(12.99f),scope+" "+t.name);
                // W11b: a formula chip's name is body text scaled by the text size like the rest (19.5 units times the scale).
                if(t.name=="Name"&&t.transform.parent.name.StartsWith("Factor",StringComparison.Ordinal))
                    Assert.That(design,Is.GreaterThanOrEqualTo(19.5f*UiKit.FontScale-.01f),scope+" chip name "+t.transform.parent.name+" "+design+" units");
                // W11b: a hint on two lines has no one- or two-character line.
                if(t.name=="ActionReason")W11bAssertNoOrphan(t,scope);
            }
            Assert.That(frameFailures,Is.Empty,scope+": glyphs on the drawn frame of their surface:\n"+string.Join("\n",frameFailures));
            // No two of these texts paint over each other.
            for(int i=0;i<all.Count;i++)for(int j=i+1;j<all.Count;j++)
                foreach(var a in all[i].Value)foreach(var b in all[j].Value)
                    if(Math.Min(a.xMax,b.xMax)-Math.Max(a.xMin,b.xMin)>1&&Math.Min(a.yMax,b.yMax)-Math.Max(a.yMin,b.yMin)>1)
                        Assert.Fail(scope+": "+all[i].Key.name+" and "+all[j].Key.name+" overlap ("+a+" / "+b+")");
            // GPU: each text changes its own pixels when shown (actually drawn, not covered by a later graphic).
            foreach(var t in texts)t.enabled=false;
            yield return new WaitForEndOfFrame();var hidden=ScreenCapture.CaptureScreenshotAsTexture();
            foreach(var t in texts)t.enabled=true;Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();var shown=ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                var a=hidden.GetPixels32();var b=shown.GetPixels32();int w=hidden.width,h=hidden.height;
                foreach(var pair in all)
                {
                    int ink=0;
                    foreach(var r in pair.Value)
                        for(int y=Mathf.Max(0,Mathf.FloorToInt(r.yMin));y<Mathf.Min(h,Mathf.CeilToInt(r.yMax));y++)
                            for(int x=Mathf.Max(0,Mathf.FloorToInt(r.xMin));x<Mathf.Min(w,Mathf.CeilToInt(r.xMax));x++)
                            {var p=a[y*w+x];var q=b[y*w+x];if(Math.Abs(p.r-q.r)+Math.Abs(p.g-q.g)+Math.Abs(p.b-q.b)>24)ink++;}
                    Assert.That(ink,Is.GreaterThanOrEqualTo(2*pair.Value.Count),scope+" "+pair.Key.name+" is not visibly drawn ('"+pair.Key.text+"', ink "+ink+")");
                }
            }
            finally{UObj.Destroy(hidden);UObj.Destroy(shown);}
        }
        /// <summary>Every button and die on the play screen is at least 40 physical pixels both ways (60 units at 720p).</summary>
        private void W11AssertHitTargets(string scope)
        {
            foreach(var control in app.canvas.GetComponentsInChildren<UnityEngine.UI.Selectable>(false).Where(c=>c.gameObject.activeInHierarchy&&c.interactable))
            {
                var r=V06ScreenRect((RectTransform)control.transform);
                Assert.That(r.width>=39.99f&&r.height>=39.99f,Is.True,scope+": "+control.name+" is "+r.width+" x "+r.height+" px");
            }
            foreach(var die in app.canvas.GetComponentsInChildren<DiePointer>(false).Where(d=>d.name.StartsWith("Die_",StringComparison.Ordinal)))
            {
                var r=V06ScreenRect((RectTransform)die.transform);Assert.That(r.width>=39.99f&&r.height>=39.99f,Is.True,scope+": "+die.name);
            }
        }
        [UnityTest] public IEnumerator PLAY_V11_W11_NewTextsAreDrawnCompleteAndReadableAtThreeTextSizes()
        {
            StartRun("71103");
            foreach(int font in new[]{100,125,150})
            {
                W11TextScale(font);string tag="font"+font;
                W11Apply(W11Scene(W11Cards3,new[]{4,4,6,2,4},"bbbrb",2,true));app.Session.TogglePending(app.Session.State.dice[4].dieId);app.Refresh();yield return W11Settle();
                yield return W11TextPass(tag+"-selecting");W11AssertHitTargets(tag+"-selecting");
                W11Apply(W11Scene(new[]{"C39","C139","C34","C56","C08"},new[]{1,2,3,4,5},"bbrbb",0,false,stageIndex:8,modifier:"B02",target:"13500"));yield return W11Settle();
                yield return W11TextPass(tag+"-boss");W11AssertHitTargets(tag+"-boss");
                W11Apply(W11Scene(new[]{"C39","C46","C50","C08"},new[]{5,5,3,5,1},"bbrbr",2,true,stageIndex:4,risk:true,target:"3000",score:"1240"));yield return W11Settle();
                yield return W11TextPass(tag+"-pressure");
                W11Apply(W11Scene(W11Cards7,new[]{6,6,5,3,1,4,2,6,5,1},"bbrbrbbrbb",3,true,items:3,capacity:8));yield return W11Settle();
                yield return W11TextPass(tag+"-ten-dice");W11AssertHitTargets(tag+"-ten-dice");
                // Qualified, after a scored hand: the stable result, the settle panel's rewards, the end-stage border.
                W11Apply(W11Scene(new[]{"C39","C46"},new[]{4,4,4,4,2},"bbbbr",4,true,target:"50"));yield return W11Settle();
                var result=app.Send(new SessionCommand{kind=CommandKind.Submit});Assert.That(result.accepted,Is.True,result.error);app.SkipVisual();yield return W11Settle();
                Assert.That(app.Session.State.stage.qualified,Is.True);Assert.That(HasActive("QualifiedGoldBorder"),Is.True);
                Assert.That(FindActive<TextMeshProUGUI>("FormulaCaptionText11").text,Does.StartWith("上一手"));
                yield return W11TextPass(tag+"-qualified");W11AssertHitTargets(tag+"-qualified");
                // A new stage before any hand is scored says so (PLAY_LAYOUT 5).
                W11Apply(W11Scene(W11Cards3,new[]{1,2,3,4,5},"bbbbb",0,false,stageIndex:6,target:"6000"));yield return W11Settle();
                Assert.That(FindActive<TextMeshProUGUI>("FormulaCaptionText11").text,Is.EqualTo("本关还没有得分"));
                Assert.That(HasActive("LockRule"),Is.False,"The lock note appears only when something is locked.");
                Assert.That(FindActive<TextMeshProUGUI>("ActionReason").text,Does.Contain("掷骰"));
            }
            W11TextScale(100);
        }

        // ------------------------------------------------------------------ helpers for the rewritten v0.7 assertions (W11_TEST_CHANGES)
        /// <summary>Of the given action names, those the action row shows (PLAY_LAYOUT 2: an unavailable action is not shown).</summary>
        private string[] W11ShownActions(params string[] names)=>names.Where(HasActive).ToArray();
        private static readonly string[] W11BadgeNames={"ColorBadge11_","GlassBadge11_","TempBadge11_"};
        private bool W11ColorAssist=>(bool)typeof(GameApp).GetField("colorAssist07",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(app);
        /// <summary>The shape badges a die shows, in their drawing order (colour, glass, temporary).</summary>
        private RectTransform[] W11Badges(string id)=>W11BadgeNames.Where(n=>HasActive(n+id)).Select(n=>FindActive<RectTransform>(n+id)).ToArray();
        /// <summary>The badges a die should show: its colour when thrown and colour assist is on (red circle, blue diamond),
        /// glass, stage-temporary (PLAY_LAYOUT 4, B4).</summary>
        private Play11Badge.Shape[] W11ExpectedBadges(string id)
        {
            var state=app.Session.State;var die=state.dice.Single(d=>d.dieId==id);var result=state.hand.currentResults.FirstOrDefault(r=>r.dieId==id);
            var shapes=new List<Play11Badge.Shape>();
            if(result?.hasRolled==true&&W11ColorAssist)shapes.Add(result.currentColor=="red"?Play11Badge.Shape.Circle:Play11Badge.Shape.Diamond);
            if(die.isGlass)shapes.Add(Play11Badge.Shape.Glass);
            if(die.lifetime=="stage_temporary")shapes.Add(Play11Badge.Shape.Clock);
            return shapes.ToArray();
        }
        /// <summary>Each badge of a die is drawn (a mesh, not culled), at least 12 design units, on screen, clear of every
        /// other die's projected body and of its own die's body core (the hull drawn in by 16 %, where the pips are).</summary>
        private void W11AssertBadgesClear(string id,string scope)
        {
            var others=app.Session.State.dice.Where(d=>d.dieId!=id).Select(d=>app.table.GetFaceOrientation(d.dieId)).Where(p=>p!=null&&p.gameObject.activeInHierarchy)
                .SelectMany(p=>p.GetComponentsInChildren<Renderer>(false)).Where(r=>r.enabled).Select(DiceLabelRendererRect07).ToArray();
            var core=W11DieHull(id,.16f);
            foreach(var badge in W11Badges(id))
            {
                var graphic=badge.GetComponent<Play11Badge>();Assert.That(graphic,Is.Not.Null,scope+" "+badge.name);
                Assert.That(graphic.canvasRenderer.cull,Is.False,scope+" "+badge.name);
                Assert.That(graphic.canvasRenderer.GetMesh()==null?0:graphic.canvasRenderer.GetMesh().vertexCount,Is.GreaterThan(0),scope+" "+badge.name+" has no mesh");
                Rect r=V06ScreenRect(badge);
                Assert.That(r.width,Is.GreaterThanOrEqualTo(12*app.canvas.scaleFactor-.01f),scope+" "+badge.name);
                Assert.That(ScoreContains07(Screen.safeArea,r),Is.True,scope+" "+badge.name+" leaves the screen");
                foreach(var body in others)Assert.That(r.Overlaps(body),Is.False,scope+" "+badge.name+" covers another die");
                for(int i=0;i<5;i++)for(int j=0;j<5;j++)
                    Assert.That(W11Inside(core,new Vector2(r.xMin+r.width*(i+.5f)/5,r.yMin+r.height*(j+.5f)/5)),Is.False,scope+" "+badge.name+" covers the body of its own die");
            }
        }
    }
}
