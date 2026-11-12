using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using UnityEngine;

namespace DiceCraft.Verification
{
    /// <summary>
    /// Opt-in visual tour for v0.73 review. Inactive unless the player is started with
    /// <c>--art073-capture &lt;directory&gt; --art073-trace &lt;trace.json&gt;</c>. It replays real session commands
    /// against an isolated save directory, presses the new v0.73 controls by name (dice library, face
    /// buttons, style legend, face-upgrade target) and writes screenshots. It never touches the ordinary
    /// save, never changes rules, and is not a performance or acceptance probe.
    /// </summary>
    public sealed class Art073CaptureTour:MonoBehaviour
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
            public bool reducedMotion;public List<string> images=new List<string>();public List<string> notes=new List<string>();
            public int highScoreTierMax;
        }
        private string directory,tracePath,fixture;
        private int maxSteps=40;
        private bool reduced;
        private readonly Report report=new Report();
        private readonly HashSet<string> seen=new HashSet<string>(StringComparer.Ordinal);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            string[] args=Environment.GetCommandLineArgs();
            int at=Array.IndexOf(args,"--art073-capture");
            if(at<0||at+1>=args.Length)return;
            string dir=Path.GetFullPath(args[at+1]);Directory.CreateDirectory(dir);
            GameApp.SaveDirectoryOverride=Path.Combine(dir,"saves");
            var tour=new GameObject("Art073CaptureTour").AddComponent<Art073CaptureTour>();
            DontDestroyOnLoad(tour.gameObject);
            tour.directory=dir;
            int trace=Array.IndexOf(args,"--art073-trace");if(trace>=0&&trace+1<args.Length)tour.tracePath=Path.GetFullPath(args[trace+1]);
            int steps=Array.IndexOf(args,"--art073-steps");if(steps>=0&&steps+1<args.Length)tour.maxSteps=int.Parse(args[steps+1],CultureInfo.InvariantCulture);
            tour.reduced=Array.IndexOf(args,"--art073-reduced")>=0;
            int scene=Array.IndexOf(args,"--art073-fixture");if(scene>=0&&scene+1<args.Length)tour.fixture=args[scene+1];
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
            // Session-only: player preferences are shared with the real game and are never written here.
            if(reduced){UiKit.ReducedMotion=true;app.table.ReducedMotion=true;}
            report.reducedMotion=reduced;
            yield return new WaitForSecondsRealtime(.6f);
            yield return Capture("menu");
            report.screenWidth=Screen.width;report.screenHeight=Screen.height;
            if(!string.IsNullOrEmpty(fixture))
            {
                // Reference image 4 is this exact committed hand: 967,680 at x26,880 with four scoring cards.
                var kind=(Art07FixtureKind)Enum.Parse(typeof(Art07FixtureKind),fixture);
                Art07Fixtures.Apply(app,kind);yield return new WaitForSecondsRealtime(.8f);
                yield return Capture("fixture-"+fixture+"-before");
                Art07Fixtures.Submit(app,kind);
                float end=Time.realtimeSinceStartup+4f,next=Time.realtimeSinceStartup+.7f;int frame=0,best=0;
                while(Time.realtimeSinceStartup<end&&(app.IsBusy||app.HighScoreTierShown073>0))
                {
                    best=Math.Max(best,app.HighScoreTierShown073);
                    if(Time.realtimeSinceStartup>=next&&frame<8){yield return Capture("fixture-"+fixture+"-score-"+(char)('a'+frame));frame++;next=Time.realtimeSinceStartup+.14f;}
                    yield return null;
                }
                report.highScoreTierMax=best;report.notes.Add("fixture "+fixture+": high-score tier "+best);
                yield return new WaitForSecondsRealtime(.6f);
                yield return Capture("fixture-"+fixture+"-after");
                yield break;
            }
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
                if(kind==CommandKind.Submit&&seen.Add("library"))yield return DiceLibrary(app);
                if(kind==CommandKind.LeaveShop&&seen.Add("upgrade-target"))yield return UpgradeTarget(app);
                if(kind==CommandKind.LeaveShop&&seen.Add("shop-notice"))
                {
                    // A real rejected purchase: where the notice goes on the one-page shop, and what would block it.
                    app.Send(new SessionCommand{kind=CommandKind.Buy,offerId="missing-notice-probe"});
                    yield return new WaitForSecondsRealtime(.3f);
                    foreach(string line in app.NoticeBlockerReport073(49))report.notes.Add("notice "+line);
                    yield return Capture("shop-notice");
                    yield return new WaitForSecondsRealtime(2.4f);
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
                if(kind==CommandKind.Submit)
                {
                    // Dense frames across the score presentation: the high-score plate appears on the final-score beat.
                    int best=0;float end=Time.realtimeSinceStartup+2.6f;int frame=0;float next=Time.realtimeSinceStartup+.55f;
                    string tag="submit"+report.images.Count(n=>n.Contains("-score-")).ToString("00");
                    while(Time.realtimeSinceStartup<end&&(app.IsBusy||app.HighScoreTierShown073>0))
                    {
                        best=Math.Max(best,app.HighScoreTierShown073);
                        if(Time.realtimeSinceStartup>=next&&frame<6){yield return Capture(tag+"-score-"+(char)('a'+frame));frame++;next=Time.realtimeSinceStartup+.22f;}
                        yield return null;
                    }
                    report.highScoreTierMax=Math.Max(report.highScoreTierMax,best);
                    report.notes.Add("submit at step "+i+": high-score tier "+best);
                }
                yield return Settle(app);
                yield return CaptureOnce(step.kind.ToLowerInvariant()+"-to-"+(app.ViewState?.phase??"none").ToLowerInvariant());
                if(app.ViewState?.phase!=before)yield return Capture("phase-"+(app.ViewState?.phase??"none").ToLowerInvariant());
            }
        }

        private IEnumerator DiceLibrary(GameApp app)
        {
            if(!Press("ViewDice")){report.notes.Add("ViewDice button not found");yield break;}
            yield return new WaitForSecondsRealtime(.7f);
            yield return Capture("dice-library");
            // Drag the large die through its own handler, then turn it with a face button.
            var rotate=FindObjectsByType<DiePointer>(FindObjectsSortMode.None).FirstOrDefault(d=>d.name=="RotatePreview"&&d.gameObject.activeInHierarchy);
            if(rotate!=null&&rotate.dragged!=null)
            {
                rotate.dragged(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){delta=new Vector2(150,-60)});
                yield return new WaitForSecondsRealtime(.2f);
                yield return Capture("dice-library-dragged");
            }
            if(Press("Face_F06")){yield return new WaitForSecondsRealtime(.5f);yield return Capture("dice-library-face6");report.notes.Add("facing after Face_F06: "+app.SheetFacingFace073);}
            if(Press("StyleLegend073")){yield return new WaitForSecondsRealtime(.8f);yield return Capture("dice-styles");}
            Press("CloseDialog");
            yield return new WaitForSecondsRealtime(.4f);
        }

        private IEnumerator UpgradeTarget(GameApp app)
        {
            var offer=app.ViewState?.shop?.offers.FirstOrDefault(o=>o.category=="die_upgrade"&&!o.sold);
            if(offer==null){report.notes.Add("no face-upgrade offer in this shop");yield break;}
            if(!Press("Buy_"+offer.offerId)){report.notes.Add("upgrade buy button not found");yield break;}
            yield return new WaitForSecondsRealtime(.6f);
            var first=app.ViewState.dice.OrderBy(d=>d.createdSerial).First();
            Press("Target_"+first.dieId);
            yield return new WaitForSecondsRealtime(.5f);
            if(Press("Face_F02"))yield return new WaitForSecondsRealtime(.5f);
            yield return Capture("upgrade-target");
            Press("CloseDialog");
            yield return new WaitForSecondsRealtime(.4f);
        }

        private static bool Press(string name)
        {
            var button=FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None)
                .FirstOrDefault(b=>b.name==name&&b.gameObject.activeInHierarchy&&b.enabled&&b.interactable);
            if(button==null)return false;
            button.onClick.Invoke();return true;
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
