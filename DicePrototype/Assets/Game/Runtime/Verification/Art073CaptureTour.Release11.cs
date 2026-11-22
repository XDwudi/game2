using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using DiceCraft.Core;
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
            var measured=new ReleaseReplayReport{images=report.images,platform=Application.platform.ToString(),
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
            app.ApplyTheme11(releaseTheme);
            measured.theme=app.ThemeId11;
            measured.rulesVersion=app.Config.configVersion;
            measured.traceSteps=trace.steps.Length-1;
            yield return Settle(app);
            yield return new WaitForEndOfFrame();
            measured.MeasureTitleAtlas(UiKit.TitleFont?.atlasTexture);
            measured.screenWidth=Screen.width;measured.screenHeight=Screen.height;
            if(measured.screenWidth!=1920||measured.screenHeight!=1080)
                throw new InvalidOperationException("R-05 requires an actual 1920x1080 player window.");
            yield return CaptureRelease(app,"menu");
            app.StartNew(trace.seed.ToString(CultureInfo.InvariantCulture));
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
                }
                var result=app.Send(new SessionCommand{kind=kind,seed=step.seed,risk=step.risk,offerId=step.offerId,
                    itemInstanceId=step.itemInstanceId,targetDieId=step.targetDieId,targetFaceId=step.targetFaceId,
                    handId=step.handId,dieIds=step.dieIds??Array.Empty<string>(),targetPips=step.targetPips??Array.Empty<int>(),
                    controllerIds=step.controllerIds??Array.Empty<string>()});
                if(!result.accepted)throw new InvalidOperationException("Rejected "+kind+" at "+i+": "+result.error);
                measured.stepsSent=i;
                yield return Settle(app);
                if(app.IsBusy||app.table.IsRolling)throw new TimeoutException("Presentation still busy at step "+i);
                string phase=app.ViewState.phase;
                if(seen.Add(phase+"/"+app.ViewState.stage.stageKind))
                    yield return CaptureRelease(app,phase+"-"+app.ViewState.stage.stageKind);
                if(app.Session.IsLootPending&&seen.Add("loot"))yield return CaptureRelease(app,"loot");
            }
            measured.terminalPhase=app.ViewState.phase;
            measured.finalStage=app.ViewState.stage.stageIndex+1;
            if(measured.terminalPhase!=trace.terminalPhase)throw new InvalidDataException("Terminal replay phase differs from validated trace.");
            yield return CaptureRelease(app,"final-"+measured.terminalPhase);
        }

        private IEnumerator CaptureRelease(GameApp app,string label)
        {
            if(app.ThemeId11!=releaseTheme)throw new InvalidOperationException("Theme changed before "+label);
            // These page-specific entrances keep animating after the scoring session is idle.
            var result=app.ViewState?.phase=="RunWon"||app.ViewState?.phase=="RunFailed"
                ?app.canvas.GetComponentInChildren<Art07ScreenReveal>(false):null;
            bool Revealing()=>app.SettlementRevealPlaying07||app.LootRevealPlaying15||(result!=null&&result.IsPlaying);
            double deadline=Time.realtimeSinceStartupAsDouble+10;
            while(Revealing()&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            if(Revealing())throw new TimeoutException("Page reveal did not settle before "+label);
            yield return Capture(label);
            if(app.ThemeId11!=releaseTheme)throw new InvalidOperationException("Theme changed during "+label);
        }
    }
}
