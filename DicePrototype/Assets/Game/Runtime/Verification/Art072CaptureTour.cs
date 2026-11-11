using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using DiceCraft.Core;
using UnityEngine;

namespace DiceCraft.Verification
{
    /// <summary>
    /// Opt-in visual tour for v0.72 art review. Inactive unless the player is started with
    /// <c>--art072-capture &lt;directory&gt; --art072-trace &lt;trace.json&gt;</c>. It replays real session
    /// commands against an isolated save directory and writes screenshots; it never touches the
    /// ordinary save, never changes rules, and is not a performance or acceptance probe.
    /// </summary>
    public sealed class Art072CaptureTour:MonoBehaviour
    {
        [Serializable]private sealed class Trace{public uint seed=0;public Step[] steps=null;}
        [Serializable]private sealed class Step
        {
            public string kind=null,offerId=null,itemInstanceId=null,targetDieId=null,targetFaceId=null,handId=null;
            public string[] dieIds=null;public int[] targetPips=null;public bool risk=false;public uint seed=0;
        }
        [Serializable]private sealed class Report
        {
            public string status="running",error="";public int screenWidth,screenHeight,stepsSent;
            public bool reducedMotion;public List<string> images=new List<string>();
        }
        private string directory,tracePath;
        private int maxSteps=60;
        private bool reduced;
        private readonly Report report=new Report();
        private readonly HashSet<string> seen=new HashSet<string>(StringComparer.Ordinal);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            string[] args=Environment.GetCommandLineArgs();
            int at=Array.IndexOf(args,"--art072-capture");
            if(at<0||at+1>=args.Length)return;
            string dir=Path.GetFullPath(args[at+1]);Directory.CreateDirectory(dir);
            GameApp.SaveDirectoryOverride=Path.Combine(dir,"saves");
            var tour=new GameObject("Art072CaptureTour").AddComponent<Art072CaptureTour>();
            DontDestroyOnLoad(tour.gameObject);
            tour.directory=dir;
            int trace=Array.IndexOf(args,"--art072-trace");if(trace>=0&&trace+1<args.Length)tour.tracePath=Path.GetFullPath(args[trace+1]);
            int steps=Array.IndexOf(args,"--art072-steps");if(steps>=0&&steps+1<args.Length)tour.maxSteps=int.Parse(args[steps+1],CultureInfo.InvariantCulture);
            tour.reduced=Array.IndexOf(args,"--art072-reduced")>=0;
        }

        private IEnumerator Start()
        {
            IEnumerator run=Run();
            while(true)
            {
                bool more;
                try{more=run.MoveNext();}
                catch(Exception ex){report.status="failed";report.error=ex.ToString();more=false;}
                if(!more)break;
                yield return run.Current;
            }
            if(report.status=="running")report.status="completed";
            WriteReport();
            Application.Quit(report.status=="completed"?0:3);
        }

        private IEnumerator Run()
        {
            GameApp app=null;float deadline=Time.realtimeSinceStartup+60;
            while((app=FindAnyObjectByType<GameApp>())==null||!app.StartupReady07)
            {
                if(Time.realtimeSinceStartup>deadline)throw new TimeoutException("GameApp did not become ready.");
                yield return null;
            }
            if(reduced){UiKit.ReducedMotion=true;app.table.ReducedMotion=true;}
            report.reducedMotion=reduced;
            yield return new WaitForSecondsRealtime(.4f);
            yield return Capture("menu-rest");
            yield return new WaitForSecondsRealtime(1.6f);
            yield return Capture("menu-idle-a");
            yield return new WaitForSecondsRealtime(.9f);
            yield return Capture("menu-idle-b");
            report.screenWidth=Screen.width;report.screenHeight=Screen.height;
            if(string.IsNullOrEmpty(tracePath))yield break;
            var trace=JsonUtility.FromJson<Trace>(File.ReadAllText(tracePath));
            app.StartNew(trace.seed.ToString(CultureInfo.InvariantCulture));
            yield return Settle(app);
            yield return Capture("stage-preview");
            for(int i=1;i<trace.steps.Length&&i<=maxSteps;i++)
            {
                var step=trace.steps[i];var kind=(CommandKind)Enum.Parse(typeof(CommandKind),step.kind);
                if(kind==CommandKind.ConfirmLocks)
                {
                    foreach(string id in step.dieIds??Array.Empty<string>())
                        if(!app.Session.TogglePending(id))throw new InvalidOperationException("Could not select "+id);
                    app.Refresh();
                    yield return new WaitForSecondsRealtime(.35f);
                    yield return CaptureOnce("pending-selection");
                }
                string before=app.ViewState?.phase;
                var result=app.Send(new SessionCommand
                {
                    kind=kind,seed=step.seed,risk=step.risk,offerId=step.offerId,itemInstanceId=step.itemInstanceId,
                    targetDieId=step.targetDieId,targetFaceId=step.targetFaceId,handId=step.handId,
                    dieIds=step.dieIds??Array.Empty<string>(),targetPips=step.targetPips??Array.Empty<int>()
                });
                if(!result.accepted)throw new InvalidOperationException("Rejected "+step.kind+" at "+i+": "+result.error);
                report.stepsSent=i;
                if(kind==CommandKind.Roll&&seen.Add("roll-motion"))
                {
                    yield return new WaitForSecondsRealtime(.3f);yield return Capture("roll-motion-a");
                    yield return new WaitForSecondsRealtime(.25f);yield return Capture("roll-motion-b");
                }
                if(kind==CommandKind.Submit&&seen.Add("score-motion"))
                    for(int frame=0;frame<4;frame++){yield return new WaitForSecondsRealtime(.45f);yield return Capture("score-motion-"+(char)('a'+frame));}
                yield return Settle(app);
                yield return CaptureOnce(step.kind.ToLowerInvariant()+"-to-"+(app.ViewState?.phase??"none").ToLowerInvariant());
                if(app.ViewState?.phase!=before)yield return CaptureOnce("phase-"+(app.ViewState?.phase??"none").ToLowerInvariant());
            }
        }

        private static IEnumerator Settle(GameApp app)
        {
            float deadline=Time.realtimeSinceStartup+12;
            yield return null;
            while((app.IsBusy||app.table.IsRolling)&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return new WaitForSecondsRealtime(.9f);
        }
        private IEnumerator CaptureOnce(string label){if(seen.Add(label))yield return Capture(label);}
        private IEnumerator Capture(string label)
        {
            yield return new WaitForEndOfFrame();
            string name=report.images.Count.ToString("00",CultureInfo.InvariantCulture)+"-"+label+".png";
            var shot=ScreenCapture.CaptureScreenshotAsTexture();
            try{File.WriteAllBytes(Path.Combine(directory,name),shot.EncodeToPNG());}
            finally{Destroy(shot);}
            report.images.Add(name);
        }
        private void WriteReport()=>File.WriteAllText(Path.Combine(directory,"tour.json"),JsonUtility.ToJson(report,true));
    }
}
