using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using DiceCraft.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;

namespace DiceCraft.Verification
{
    public sealed partial class Art073CaptureTour
    {
        [Serializable] private sealed class ReleaseReplayReport
        {
            public string schema="v11-windows-replay-1",status="running",error="",theme,buildGuid,buildIdentity,platform,
                playerDataPath,traceSha256,terminalPhase,unityVersion,artVersion,rulesVersion;
            public bool isEditor;
            public int screenWidth,screenHeight,stepsSent,traceSteps,logErrors,finalStage;
            public string titleAtlasFormat="";
            public bool titleAtlasReadable;
            public int titleAtlasWidth,titleAtlasHeight,titleAtlasMipCount;
            public long titleAtlasTextureBytes,titleAtlasRuntimeBytes;
            public List<string> images;
            public List<ReplayTextFrame> textFrames;

            public void MeasureTitleAtlas(Texture2D atlas)
            {
                if(atlas==null)return;
                titleAtlasWidth=atlas.width;
                titleAtlasHeight=atlas.height;
                titleAtlasMipCount=atlas.mipmapCount;
                titleAtlasFormat=atlas.graphicsFormat.ToString();
                titleAtlasReadable=atlas.isReadable;
                titleAtlasRuntimeBytes=Profiler.GetRuntimeMemorySizeLong(atlas);
                // Storage of the texture data. The profiler also counts native object overhead.
                for(int mip=0;mip<titleAtlasMipCount;mip++)
                    titleAtlasTextureBytes+=GraphicsFormatUtility.ComputeMipmapSize(
                        Mathf.Max(1,atlas.width>>mip),Mathf.Max(1,atlas.height>>mip),atlas.graphicsFormat);
            }
        }
        private string releaseTheme;
        private int releaseLogErrors;
        [Serializable] private sealed class ReplayTextField
        {
            public string path, text;
        }
        [Serializable] private sealed class ReplayTextFrame
        {
            public ReplayScoreFocus scoreFocus;
            public string image;
            public List<ReplayTextField> fields=new List<ReplayTextField>();
        }
        private readonly List<ReplayTextFrame> releaseTextFrames=new List<ReplayTextFrame>();

        // Keep each live label alongside the pixels from the same end-of-frame.
        // Partially clipped labels retain their full parsed string for the reviewer.
        [Serializable] private sealed class ReplayScoreFocus
        {
            public string path,text,handId;
        }

        private void RecordReleaseText(Canvas canvas,string image,TextMeshProUGUI focus=null,
            UnityEngine.UI.ScrollRect scroll=null,string handId=null)
        {
            var frame=new ReplayTextFrame{image=image};
            var corners=new Vector3[4];
            foreach(var label in canvas.GetComponentsInChildren<TextMeshProUGUI>(false))
            {
                if(!label.isActiveAndEnabled||label.canvasRenderer.cull||label.color.a<=0||
                    label.canvasRenderer.GetInheritedAlpha()<=0||label.textInfo==null)continue;
                var targetCanvas=label.canvas;
                if(targetCanvas==null||!targetCanvas.isActiveAndEnabled)continue;
                var camera=targetCanvas.renderMode==RenderMode.ScreenSpaceOverlay?null:targetCanvas.worldCamera;
                label.rectTransform.GetWorldCorners(corners);
                var minimum=new Vector2(float.PositiveInfinity,float.PositiveInfinity);
                var maximum=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
                foreach(var corner in corners)
                {
                    var pixel=RectTransformUtility.WorldToScreenPoint(camera,corner);
                    minimum=Vector2.Min(minimum,pixel);
                    maximum=Vector2.Max(maximum,pixel);
                }
                if(maximum.x<=0||maximum.y<=0||minimum.x>=Screen.width||minimum.y>=Screen.height)continue;
                bool drawn=false;
                for(int i=0;i<label.textInfo.characterCount;i++)
                    if(label.textInfo.characterInfo[i].isVisible){drawn=true;break;}
                if(!drawn)continue;
                string parsed=label.GetParsedText();
                if(string.IsNullOrWhiteSpace(parsed))continue;
                var names=new Stack<string>();
                for(var node=label.transform;node!=null&&node!=canvas.transform;node=node.parent)
                    names.Push(node.name);
                var field=new ReplayTextField{path=string.Join("/",names),text=parsed};
                frame.fields.Add(field);
                if(label==focus)
                {
                    RequireScoreTextInViewport(scroll,focus);
                    frame.scoreFocus=new ReplayScoreFocus{path=field.path,text=field.text,handId=handId};
                }
            }
            if(focus!=null&&frame.scoreFocus==null)throw new InvalidOperationException("The captured image has no visible score focus: "+image);
            releaseTextFrames.Add(frame);
        }

        private void CountReleaseLog(string message,string stackTrace,LogType type)
        {
            if(type==LogType.Exception||type==LogType.Assert||type==LogType.Error)
                Interlocked.Increment(ref releaseLogErrors);
        }

        private void OnDestroy()
        {
            if(releaseTheme!=null)Application.logMessageReceivedThreaded-=CountReleaseLog;
        }

        private IEnumerator DriveReleaseRun()
        {
            var measured=new ReleaseReplayReport{images=report.images,textFrames=releaseTextFrames,platform=Application.platform.ToString(),
                isEditor=Application.isEditor,buildGuid=Application.buildGUID,playerDataPath=Application.dataPath,
                unityVersion=Application.unityVersion,artVersion=GameApp.ArtVersion,
                buildIdentity=Resources.Load<TextAsset>("BuildIdentity")?.text};
            // Drive nested capture and settle iterators here, so a failure in either becomes a persisted failed run.
            var stack=new Stack<IEnumerator>();
            stack.Push(RunRelease(measured));
            while(stack.Count>0)
            {
                object current;
                try
                {
                    var top=stack.Peek();
                    if(!top.MoveNext()){stack.Pop();continue;}
                    current=top.Current;
                    if(current is IEnumerator child&&!(current is CustomYieldInstruction)){stack.Push(child);continue;}
                }
                catch(Exception ex){measured.error=ex.ToString();break;}
                yield return current;
            }
            measured.logErrors=Volatile.Read(ref releaseLogErrors);
            measured.status=measured.error.Length==0&&measured.logErrors==0?"completed":"failed";
            File.WriteAllText(Path.Combine(directory,"tour.json"),JsonUtility.ToJson(measured,true));
            Application.Quit(measured.status=="completed"?0:3);
        }

        private IEnumerator RunRelease(ReleaseReplayReport measured)
        {
            if(measured.isEditor||Application.platform!=RuntimePlatform.WindowsPlayer||string.IsNullOrEmpty(measured.buildIdentity))
                throw new InvalidOperationException("R-05 requires the Windows player with its embedded build identity.");
            if(string.IsNullOrWhiteSpace(releaseTheme)||string.IsNullOrEmpty(tracePath))
                throw new InvalidDataException("A theme and full release trace are required.");
            using(var stream=File.OpenRead(tracePath))
            using(var sha=SHA256.Create())
                measured.traceSha256=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
            var trace=JsonUtility.FromJson<Trace>(File.ReadAllText(tracePath));
            if(trace?.steps==null||trace.steps.Length<2||trace.steps[0].kind!="NewRun"||trace.steps[0].seed!=trace.seed||
                (trace.terminalPhase!="RunWon"&&trace.terminalPhase!="RunFailed"))
                throw new InvalidDataException("Expected a full trace from NewRun through RunWon or RunFailed.");
            GameApp app=null;
            float deadline=Time.realtimeSinceStartup+90;
            while((app=FindAnyObjectByType<GameApp>())==null||!app.StartupReady07)
            {
                if(Time.realtimeSinceStartup>=deadline)throw new TimeoutException("Player startup timed out.");
                yield return null;
            }
            var theme=ThemeRegistry.Find(releaseTheme);
            if(theme==null||theme.Preview||app.Config.configVersion!=trace.configVersion)
                throw new InvalidOperationException("Requested theme is unavailable to players, or trace rules differ.");
            if(Screen.width!=1920||Screen.height!=1080)
                throw new InvalidOperationException("R-05 requires an actual 1920x1080 player window.");
            yield return RunReleaseNavigation(app,trace,measured);
            measured.terminalPhase=app.ViewState.phase;
            measured.finalStage=app.ViewState.stage.stageIndex+1;
            if(measured.terminalPhase!=trace.terminalPhase)throw new InvalidDataException("Terminal replay phase differs from validated trace.");
            yield return CaptureRelease(app,"final-"+measured.terminalPhase);
        }

        private IEnumerator RunReleaseNavigation(GameApp app,Trace trace,ReleaseReplayReport measured)
        {
            app.UseNormalReleaseCaptureSpeed11();
            app.ApplyTheme11(releaseTheme);
            measured.theme=app.ThemeId11;
            measured.rulesVersion=app.Config.configVersion;
            measured.traceSteps=trace.steps.Length-1;
            yield return Settle(app);
            yield return new WaitForEndOfFrame();
            measured.MeasureTitleAtlas(UiKit.TitleFont?.atlasTexture);
            measured.screenWidth=Screen.width;measured.screenHeight=Screen.height;
            var printedFrame=ReleaseNode<UnityEngine.UI.Image>(app,"MenuCardBack0");
            var themeFrame=UiKit.Template("ui_card_controller");
            if(themeFrame==null||printedFrame.sprite!=themeFrame)
                throw new InvalidOperationException("First menu image has a stale card-frame sprite for "+releaseTheme);
            ReleaseButton(app,"NewRun");
            yield return CaptureRelease(app,"menu");
            var menuState=ReleaseSnapshot(app);
            foreach(var page in new[]{("MenuHandbook","handbook","HandbookHands","CloseDialog"),
                                      ("MenuSettings","settings","SettingsTab0","SettingsDone")})
            {
                ClickRelease(app,page.Item1);
                yield return Settle(app);
                yield return CaptureReleaseControl(app,page.Item2,page.Item3);
                ClickRelease(app,page.Item4);
                yield return Settle(app);
                ReleaseButton(app,"NewRun");
                RequireReleaseSnapshot(app,menuState,page.Item2);
            }
            ClickRelease(app,"NewRun");
            yield return Settle(app);
            var seed=ReleaseNode<TMPro.TMP_InputField>(app,"SeedInput");
            seed.Select();
            seed.text=trace.seed.ToString(CultureInfo.InvariantCulture);
            seed.DeactivateInputField();
            yield return CaptureReleaseControl(app,"new-run","StartNew");
            RequireReleaseSnapshot(app,menuState,"new-run before confirmation");
            ClickRelease(app,"StartNew");
            yield return Settle(app);
            if(app.ViewState?.phase!="StagePreview"||app.ViewState.seed!=trace.seed)
                throw new InvalidOperationException("New run does not match the recorded seed or opening.");
            yield return CaptureRelease(app,"stage-preview");
            for(int i=1;i<trace.steps.Length;i++)
            {
                var step=trace.steps[i];
                var kind=(CommandKind)Enum.Parse(typeof(CommandKind),step.kind);
                if(kind==CommandKind.NewRun||kind==CommandKind.ApplyDebugScene)
                    throw new InvalidDataException("Unexpected command in release replay: "+kind);
                if(kind==CommandKind.ConfirmLocks)
                {
                    foreach(string id in step.dieIds??Array.Empty<string>())
                        if(!app.Session.TogglePending(id))throw new InvalidOperationException("Could not select die at step "+i);
                    app.Refresh();
                    if(app.Session.PendingIds.Length>0&&seen.Add("play-pending"))
                    {
                        yield return Settle(app);
                        yield return CaptureReleaseControl(app,"play-pending","Confirm");
                        if(!System.Linq.Enumerable.SequenceEqual(app.Session.PendingIds,step.dieIds))
                            throw new InvalidOperationException("Pending dice changed before the recorded ConfirmLocks.");
                    }
                }
                var result=app.Send(new SessionCommand{kind=kind,seed=step.seed,risk=step.risk,offerId=step.offerId,
                    itemInstanceId=step.itemInstanceId,targetDieId=step.targetDieId,targetFaceId=step.targetFaceId,
                    handId=step.handId,dieIds=step.dieIds??Array.Empty<string>(),targetPips=step.targetPips??Array.Empty<int>(),
                    controllerIds=step.controllerIds??Array.Empty<string>()});
                if(!result.accepted)throw new InvalidOperationException("Rejected "+kind+" at "+i+": "+result.error);
                measured.stepsSent=i;
                if(kind==CommandKind.Submit&&seen.Add("play-scoring"))
                {
                    // Capture before Settle consumes the real committed scoring presentation. The
                    // same live receipt and its Skip control must still be on screen after end-of-frame.
                    double until=Time.realtimeSinceStartupAsDouble+3;
                    while(!app.PresentationPlaying07&&Time.realtimeSinceStartupAsDouble<until)yield return null;
                    if(!app.PresentationPlaying07)throw new InvalidOperationException("No live scoring presentation after Submit.");
                    yield return CaptureReleaseControl(app,"play-scoring","Skip");
                    if(!app.PresentationPlaying07)throw new InvalidOperationException("Scoring ended before the capture was verified.");
                }
                yield return Settle(app);
                if(app.IsBusy||app.table.IsRolling)throw new TimeoutException("Presentation still busy at step "+i);
                string phase=app.ViewState.phase;
                if(seen.Add(phase+"/"+app.ViewState.stage.stageKind))
                    yield return CaptureRelease(app,phase+"-"+app.ViewState.stage.stageKind);
                if(phase=="HandReady"&&app.ViewState.stage.stageKind=="normal"&&seen.Add("pause"))
                {
                    var beforePause=ReleaseSnapshot(app);
                    ClickRelease(app,"Pause");
                    yield return Settle(app);
                    yield return CaptureReleaseControl(app,"pause","Resume");
                    ClickRelease(app,"Resume");
                    yield return Settle(app);
                    ReleaseButton(app,"Roll");
                    RequireReleaseSnapshot(app,beforePause,"pause");
                }
                if(kind==CommandKind.ConfirmLocks&&phase=="Selecting"&&app.ViewState.hand.orderedLockedIds.Count>0&&seen.Add("play-locked"))
                    yield return CaptureReleaseControl(app,"play-locked","Submit");
                if(kind==CommandKind.Submit&&phase=="HandReady"&&!seen.Contains("score-details"))
                {
                    var receipt=app.ViewState.lastSubmission;
                    var calculation=ScoreExplanation.Build(receipt.snapshot,receipt.breakdown,app.Config);
                    int factor=Array.FindIndex(calculation.global,s=>s.operation=="×"&&s.operand!="1");
                    if(factor>=0)
                    {
                        seen.Add("score-details");
                        var before=ReleaseSnapshot(app);
                        ClickRelease(app,"PreviousSubmission");
                        yield return Settle(app);
                        var formula=ReleaseNode<TMPro.TextMeshProUGUI>(app,"Formula");
                        var drawer=ReleaseNode<UnityEngine.UI.ScrollRect>(app,"ScoreDrawerTable");
                        ScrollTextIntoReleaseView(drawer,formula);
                        string originalFormula=formula.text;
                        if(!originalFormula.Contains("×")||!originalFormula.Contains("="))
                            throw new InvalidOperationException("The committed score drawer has no multiplier and result formula.");
                        yield return CaptureReleaseControl(app,"score-details","FullScoreDetails",formula,drawer,receipt.snapshot.handId);
                        ClickRelease(app,"FullScoreDetails");
                        yield return Settle(app);
                        if(ReleaseNode<TMPro.TextMeshProUGUI>(app,"Formula").text!=originalFormula)
                            throw new InvalidOperationException("The step reader belongs to a different score formula.");
                        var reader=ReleaseNode<UnityEngine.UI.ScrollRect>(app,"ScoreDetail");
                        var row=ReleaseNode<TMPro.TextMeshProUGUI>(app,"GlobalStep"+factor);
                        ScrollTextIntoReleaseView(reader,row);
                        if(!row.GetParsedText().Contains("×"))throw new InvalidOperationException("Selected score step is missing its multiplier.");
                        yield return CaptureReleaseControl(app,"score-steps","ScoreDrawerBack",row,reader,receipt.snapshot.handId);
                        ClickRelease(app,"ScoreDrawerBack");
                        yield return Settle(app);
                        ReleasePointer(app,"FullScoreDetails",out _);
                        ClickRelease(app,"CloseDialog");
                        yield return Settle(app);
                        ReleasePointer(app,"PreviousSubmission",out _);
                        RequireReleaseSnapshot(app,before,"score-details");
                    }
                }
                if(app.Session.IsLootPending&&seen.Add("loot"))
                {
                    if(!app.LootPage15)throw new InvalidOperationException("Loot is pending without its visible picker.");
                    yield return CaptureRelease(app,"loot");
                }
            }
        }

#if UNITY_EDITOR
        // The PlayMode test creates an inactive component so MonoBehaviour.Start cannot launch
        // a tour or quit Unity. It invokes the very same renderer, pointers, command loop and exporter.
        public IEnumerator CaptureReleaseNavigationSmoke(GameApp app,string traceJson,string dest)
        {
            if(!Application.isEditor||gameObject.activeInHierarchy||app.Session.State!=null)
                throw new InvalidOperationException("Editor smoke requires an inactive driver and an isolated empty menu.");
            var trace=JsonUtility.FromJson<Trace>(traceJson);
            if(trace?.steps==null||trace.steps.Length<2||trace.steps[0].kind!="NewRun"||
                trace.steps[0].seed!=trace.seed||trace.configVersion!=app.Config.configVersion)
                throw new InvalidDataException("Invalid editor smoke prefix.");
            directory=dest;
            Directory.CreateDirectory(directory);
            releaseTheme=GameTheme.DefaultId;
            var measured=new ReleaseReplayReport{schema="v11-editor-navigation-smoke-1",isEditor=true,
                platform=Application.platform.ToString(),unityVersion=Application.unityVersion,
                images=report.images,textFrames=releaseTextFrames};
            try
            {
                yield return RunReleaseNavigation(app,trace,measured);
                if(!seen.Contains("score-details"))throw new InvalidOperationException("Smoke did not reach a real score multiplier.");
                measured.status="editor-smoke-completed";
            }
            finally { File.WriteAllText(Path.Combine(directory,"smoke.json"),JsonUtility.ToJson(measured,true)); }
        }
#endif

        private static void ScrollTextIntoReleaseView(UnityEngine.UI.ScrollRect scroll,TMPro.TextMeshProUGUI text)
        {
            if(!text.transform.IsChildOf(scroll.content))throw new InvalidOperationException("Score text is outside its own reader.");
            Canvas.ForceUpdateCanvases();
            var viewport=scroll.viewport;
            float travel=Mathf.Max(0,scroll.content.rect.height-viewport.rect.height);
            if(travel>0)
            {
                var center=viewport.InverseTransformPoint(text.transform.TransformPoint(text.rectTransform.rect.center));
                scroll.StopMovement();
                scroll.verticalNormalizedPosition=Mathf.Clamp01(scroll.verticalNormalizedPosition+(center.y-viewport.rect.center.y)/travel);
                Canvas.ForceUpdateCanvases();
            }
            RequireScoreTextInViewport(scroll,text);
        }

        private static void RequireScoreTextInViewport(UnityEngine.UI.ScrollRect scroll,TextMeshProUGUI text)
        {
            if(scroll==null||!text.transform.IsChildOf(scroll.content)||text.canvasRenderer.cull)
                throw new InvalidOperationException("Score focus is not visible in its reader.");
            var viewport=scroll.viewport;
            text.ForceMeshUpdate();
            int ink=0;
            for(int i=0;i<text.textInfo.characterCount;i++)
            {
                var glyph=text.textInfo.characterInfo[i];
                if(!glyph.isVisible)continue;
                ink++;
                foreach(var vertex in new[]{glyph.bottomLeft,glyph.topLeft,glyph.topRight,glyph.bottomRight})
                {
                    Vector2 pt=viewport.InverseTransformPoint(text.transform.TransformPoint(vertex));
                    if(!viewport.rect.Contains(pt))throw new InvalidOperationException("Clipped score glyph: "+text.name);
                }
            }
            if(ink==0||text.isTextTruncated||text.color.a*text.canvasRenderer.GetInheritedAlpha()<=0)
                throw new InvalidOperationException("The committed score text is not fully painted: "+text.name);
        }

        // UI-only visits must preserve the actual command sequence, state and serialized save.
        private static (long sequence,string state,byte[] save) ReleaseSnapshot(GameApp app)
        {
            var state=app.Session?.State;
            string file=app.SaveLocation;
            return (app.Session?.ActionSequence??0,state==null?"":JsonUtility.ToJson(state),
                !string.IsNullOrEmpty(file)&&File.Exists(file)?File.ReadAllBytes(file):null);
        }

        private static void RequireReleaseSnapshot(GameApp app,(long sequence,string state,byte[] save) before,string label)
        {
            var after=ReleaseSnapshot(app);
            bool sameBytes=before.save==null?after.save==null:
                after.save!=null&&System.Linq.Enumerable.SequenceEqual(before.save,after.save);
            if(before.sequence!=after.sequence||before.state!=after.state||!sameBytes)
                throw new InvalidOperationException("UI navigation changed the command sequence, run or save: "+label);
        }

        private static T ReleaseNode<T>(GameApp app,string name) where T:Component
        {
            T found=null;
            foreach(var candidate in app.canvas.GetComponentsInChildren<T>(false))
                if(candidate.name==name&&candidate.gameObject.activeInHierarchy)
                {
                    if(found!=null)throw new InvalidOperationException("Ambiguous live release control: "+name);
                    found=candidate;
                }
            return found!=null?found:throw new InvalidOperationException("Missing live release control: "+name);
        }

        private static UnityEngine.UI.Button ReleaseButton(GameApp app,string name)
        {
            var b=ReleaseNode<UnityEngine.UI.Button>(app,name);
            if(!b.isActiveAndEnabled||!b.IsInteractable())throw new InvalidOperationException("Disabled release control: "+name);
            return b;
        }

        private static UnityEngine.EventSystems.PointerEventData ReleasePointer(GameApp app,string name,out UnityEngine.UI.Button button)
        {
            Canvas.ForceUpdateCanvases();
            button=ReleaseButton(app,name);
            var rect=(RectTransform)button.transform;
            var events=UnityEngine.EventSystems.EventSystem.current;
            if(events==null)throw new InvalidOperationException("No player EventSystem: "+name);
            var pointer=new UnityEngine.EventSystems.PointerEventData(events)
            {position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};
            var hits=new List<UnityEngine.EventSystems.RaycastResult>();
            events.RaycastAll(pointer,hits);
            if(hits.Count==0||hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()!=button)
                throw new InvalidOperationException("Release control is not the foreground pointer target: "+name);
            return pointer;
        }

        private static void ClickRelease(GameApp app,string name)
        {
            var pointer=ReleasePointer(app,name,out var button);
            button.OnPointerDown(pointer);button.OnPointerUp(pointer);button.OnPointerClick(pointer);
        }

        private IEnumerator CaptureReleaseControl(GameApp app,string label,string marker,TextMeshProUGUI focus=null,
            UnityEngine.UI.ScrollRect scroll=null,string handId=null)
        {
            ReleasePointer(app,marker,out _);
            yield return CaptureRelease(app,label,focus,scroll,handId);
            ReleasePointer(app,marker,out _);
        }

        private IEnumerator CaptureRelease(GameApp app,string label,TextMeshProUGUI focus=null,
            UnityEngine.UI.ScrollRect scroll=null,string handId=null)
        {
            if(app.ThemeId11!=releaseTheme)throw new InvalidOperationException("Theme changed before "+label);
            // These page-specific entrances keep animating after the scoring session is idle.
            var result=app.ViewState?.phase=="RunWon"||app.ViewState?.phase=="RunFailed"
                ?app.canvas.GetComponentInChildren<Art07ScreenReveal>(false):null;
            bool Revealing()=>app.SettlementRevealPlaying07||app.LootRevealPlaying15||(result!=null&&result.IsPlaying);
            double deadline=Time.realtimeSinceStartupAsDouble+10;
            while(Revealing()&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            if(Revealing())throw new TimeoutException("Page reveal did not settle before "+label);
            yield return Capture(label,image=>
            {
                if(focus!=null&&(string.IsNullOrEmpty(handId)||app.ViewState?.lastSubmission?.snapshot?.handId!=handId))
                    throw new InvalidOperationException("Score focus is not linked to the current frozen receipt.");
                RecordReleaseText(app.canvas,image,focus,scroll,handId);
            });
            if(app.ThemeId11!=releaseTheme)throw new InvalidOperationException("Theme changed during "+label);
        }
    }
}

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        // Use the ordinary scoring timeline in this opt-in Windows verification process,
        // including when the user's saved preference is Instant. Do not write preferences.
        internal void UseNormalReleaseCaptureSpeed11()
        {
            instant07=false;
            fast=false;
            reduced=false;
            ApplyArtPreferences07();
            table.ReducedMotion=false;
        }
    }
}
