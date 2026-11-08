using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DiceCraft.Verification
{
    /// <summary>Opt-in recording of the isolated style lab, using the real presentation clock.</summary>
    public sealed class Art071LabRecording : MonoBehaviour
    {
        [Serializable] private sealed class Frame { public string file; public int frame; public double seconds; }
        [Serializable] private sealed class Operation { public string action,phase; public double seconds; public long sequence; }
        [Serializable] private sealed class Report
        {
            public string status="running",error,startedUtc,artVersion,runtimeMvid,coreMvid,saveRoot;
            public string clock="Actual realtime; rendered frames, no performance claim";
            public int width,height;
            public double duration;
            public List<Frame> frames=new List<Frame>();
            public List<Operation> operations=new List<Operation>();
        }
        private GameApp app;
        private Report report;
        private string output;
        private double start,nextCapture;
        private int step;
        private Button hovered;
        public string Status=>report?.status??"not_started";
        public string Error=>report?.error;

        public static Art071LabRecording Begin(string directory)
        {
            var lab=FindAnyObjectByType<Art071StyleLab>();
            var app=FindAnyObjectByType<GameApp>();
            if(lab==null||!lab.Ready||app==null||!app.IsDebugWorkspace||app.IsBusy||app.Session.HasPendingSave)
                throw new InvalidOperationException("Use an idle, fresh isolated ArtStyleLab.");
            string path=Path.GetFullPath(directory);
            if(!Path.IsPathRooted(directory)||path.StartsWith(Application.dataPath,StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Record to an absolute path outside Assets.");
            if(Directory.Exists(path)&&Directory.EnumerateFileSystemEntries(path).Any())throw new IOException("Keep previous recordings; choose a new directory.");
            if(FindObjectsByType<Art071LabRecording>().Any(r=>r.Status=="running"))throw new InvalidOperationException("A recording is already running.");
            Directory.CreateDirectory(Path.Combine(path,"frames"));
            var recorder=lab.gameObject.AddComponent<Art071LabRecording>();
            recorder.app=app;recorder.output=path;recorder.start=Time.realtimeSinceStartupAsDouble;
            recorder.report=new Report{startedUtc=DateTime.UtcNow.ToString("O"),artVersion=GameApp.ArtVersion,
                runtimeMvid=typeof(GameApp).Module.ModuleVersionId.ToString(),coreMvid=typeof(GameSession).Module.ModuleVersionId.ToString(),
                saveRoot=lab.SaveRoot,width=Screen.width,height=Screen.height};
            File.WriteAllText(Path.Combine(path,"state-before.json"),JsonUtility.ToJson(app.Session.State,true));
            recorder.Write();recorder.StartCoroutine(recorder.Record());return recorder;
        }
        private IEnumerator Record()
        {
            while(report.status=="running")
            {
                yield return new WaitForEndOfFrame();
                try
                {
                    double elapsed=Time.realtimeSinceStartupAsDouble-start;
                    if(elapsed>=nextCapture)
                    {
                        var texture=ScreenCapture.CaptureScreenshotAsTexture();
                        try
                        {
                            string file="frames/"+report.frames.Count.ToString("D5")+".png";
                            File.WriteAllBytes(Path.Combine(output,file),texture.EncodeToPNG());
                            report.frames.Add(new Frame{file=file,frame=Time.frameCount,seconds=elapsed});
                        }
                        finally{Destroy(texture);}
                        nextCapture=elapsed+1.0/30;
                    }
                    if(step==0&&elapsed>=.5)
                    {
                        hovered=app.canvas.GetComponentsInChildren<Button>().First(b=>b.name.StartsWith("Controller_"));
                        ExecuteEvents.Execute(hovered.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);
                        Log("card hover",elapsed);step++;
                    }
                    else if(step==1&&elapsed>=1.4)
                    {
                        ExecuteEvents.Execute(hovered.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerExitHandler);
                        Log("card exit",elapsed);step++;
                    }
                    else if(step==2&&elapsed>=2){Click("Confirm");Log("ConfirmLocks",elapsed);step++;}
                    else if(step==3&&elapsed>=3.5&&!app.IsBusy){Click("Submit");Log("Submit",elapsed);step++;}
                    else if(step==4&&elapsed>=8&&!app.IsBusy){Click("Roll");Log("Roll",elapsed);step++;}
                    else if(step==5&&elapsed>=10&&!app.IsBusy)
                    {
                        var die=app.Session.State.dice.First(d=>!app.Session.State.hand.orderedLockedIds.Contains(d.dieId));
                        var hit=app.canvas.GetComponentsInChildren<RectTransform>().First(r=>r.name=="Die_"+die.dieId);
                        Dispatch(hit.gameObject);Log("select die",elapsed);step++;
                    }
                    if(elapsed>=13)
                    {
                        if(step!=6)throw new InvalidOperationException("The intended operation sequence did not finish.");
                        report.duration=elapsed;report.status="captured_pending_visual_review";
                        File.WriteAllText(Path.Combine(output,"state-after.json"),JsonUtility.ToJson(app.Session.State,true));Write();
                    }
                }
                catch(Exception ex){report.status="failed";report.error=ex.ToString();Write();}
                yield return null;
            }
        }
        private void Click(string name)
        {
            var button=app.canvas.GetComponentsInChildren<Button>().First(b=>b.name==name);
            if(!button.IsInteractable())throw new InvalidOperationException(name+" is not interactable.");
            Dispatch(button.gameObject);
        }
        private static void Dispatch(GameObject target)
        {
            Canvas.ForceUpdateCanvases();var rect=(RectTransform)target.transform;
            var point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            var data=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
            if(hits.Count==0||ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)!=target)
                throw new InvalidOperationException(target.name+" is covered at its center.");
            ExecuteEvents.Execute(target,data,ExecuteEvents.pointerClickHandler);
        }
        private void Log(string action,double elapsed)=>report.operations.Add(new Operation{action=action,seconds=elapsed,phase=app.Session.State.phase,sequence=app.Session.ActionSequence});
        private void Write()=>File.WriteAllText(Path.Combine(output,"recording.json"),JsonUtility.ToJson(report,true));
        private void OnDestroy(){if(report!=null&&report.status=="running"){report.status="interrupted";Write();}}
    }
}
