using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DiceCraft
{
    [Serializable] public sealed class DisplayChoice
    {
        public string displayName;
        public int nativeWidth, nativeHeight, width, height;
        public FullScreenMode mode;
        public uint refreshNumerator, refreshDenominator;
        public DisplayChoice Clone() => (DisplayChoice)MemberwiseClone();
    }

    /// <summary>Machine preferences only. Never references a Run, command sequence or RNG.</summary>
    public sealed class DisplaySettings : MonoBehaviour
    {
        [Serializable] private sealed class Saved { public DisplayChoice confirmed, candidate; public bool pending; }
        private Saved saved = new Saved();
        private string path;
        private Coroutine transition;
        private double deadline;
        private double transitionDeadline;
        private bool restoring;
        public bool IsPending => saved.pending;
        public bool IsTransitioning => transition != null;
        public bool CanConfirm { get; private set; }
        public string Notice { get; private set; } = "";
        public int SecondsLeft => IsPending && CanConfirm ? Math.Max(0, (int)Math.Ceiling(deadline - Time.realtimeSinceStartupAsDouble)) : 0;
        public event Action Changed;
        public static FullScreenMode PlatformFullscreen => Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor ? FullScreenMode.MaximizedWindow : FullScreenMode.ExclusiveFullScreen;
        public static string ModeName(FullScreenMode m) => m == FullScreenMode.Windowed ? "窗口" : m == FullScreenMode.FullScreenWindow ? "无边框" : "全屏";
        public List<DisplayInfo> Displays() { var list = new List<DisplayInfo>(); Screen.GetDisplayLayout(list); if(list.Count==0)list.Add(Screen.mainWindowDisplayInfo); return list; }
        public static Vector2Int AutomaticWindow(RectInt area)
        {
            int w = Math.Max(320, Math.Min(1920, (int)(area.width * .85)));
            int h = Math.Max(180, Math.Min(1080, (int)(area.height * .85)));
            w = Math.Min(w, h * 16 / 9); h = w * 9 / 16;
            return new Vector2Int(w,h);
        }
        public DisplayChoice ReadActual()
        {
            var d = Screen.mainWindowDisplayInfo;
            return new DisplayChoice { displayName=d.name, nativeWidth=d.width, nativeHeight=d.height, width=Screen.width, height=Screen.height,
                mode=Screen.fullScreenMode,refreshNumerator=d.refreshRate.numerator,refreshDenominator=d.refreshRate.denominator };
        }
        public DisplayChoice Choice(DisplayInfo d, FullScreenMode mode, int width, int height)
        {
            var c=new DisplayChoice {displayName=d.name,nativeWidth=d.width,nativeHeight=d.height,width=width,height=height,mode=mode,
                refreshNumerator=d.refreshRate.numerator,refreshDenominator=d.refreshRate.denominator};
            if(mode==FullScreenMode.ExclusiveFullScreen&&Screen.mainWindowDisplayInfo.name==d.name)
            {
                var modes=Screen.resolutions.Where(r=>r.width==width&&r.height==height).ToArray();
                if(modes.Length>0){var r=modes.OrderBy(r=>Math.Abs(r.refreshRateRatio.value-d.refreshRate.value)).First();c.refreshNumerator=r.refreshRateRatio.numerator;c.refreshDenominator=r.refreshRateRatio.denominator;}
            }
            return c;
        }
        public Vector2Int[] Resolutions(DisplayInfo d,FullScreenMode mode)
        {
            if(mode==FullScreenMode.ExclusiveFullScreen)
            {
                if(Screen.mainWindowDisplayInfo.name!=d.name)return new[]{new Vector2Int(d.width,d.height)};
                return Screen.resolutions.Select(r=>new Vector2Int(r.width,r.height)).Distinct().OrderBy(r=>r.x).ThenBy(r=>r.y).ToArray();
            }
            var values=new List<Vector2Int>{new Vector2Int(1280,720),new Vector2Int(1600,900),new Vector2Int(1920,1080),new Vector2Int(2560,1440),new Vector2Int(d.width,d.height)};
            if(mode==FullScreenMode.Windowed)values.Add(AutomaticWindow(d.workArea));
            return values.Where(r=>r.x>0&&r.y>0&&r.x<=(mode==FullScreenMode.Windowed?d.workArea.width:d.width)&&r.y<=(mode==FullScreenMode.Windowed?d.workArea.height:d.height))
                .Distinct().OrderBy(r=>r.x).ThenBy(r=>r.y).ToArray();
        }
        public void Initialize(string settingsPath, bool applyStartup=true)
        {
            path=settingsPath;
            if(File.Exists(path))try { saved=JsonUtility.FromJson<Saved>(File.ReadAllText(path))??new Saved(); }
            catch(Exception e) when(e is IOException || e is ArgumentException){ Notice="显示偏好不可读，使用当前屏幕。";saved=new Saved(); }
            if(!applyStartup)return;
            var d=Screen.mainWindowDisplayInfo;
            bool interrupted=saved.pending;
            var target=saved.confirmed;
            if(target==null)target=Choice(d,FullScreenMode.FullScreenWindow,d.width,d.height);
            else if(!Available(target))target=SafeWindow();
            saved.pending=false;saved.candidate=null;CanConfirm=false;
            transition=StartCoroutine(ApplyNative(target,false));
            if(interrupted)Notice="上次显示调整未确认，已恢复已确认设置。";
        }
        // Exclusive mode changes the display's reported dimensions. Its identity must survive
        // that change so both confirmation and rollback keep targeting the same monitor.
        private bool Available(DisplayChoice c) => c!=null&&c.width>0&&c.height>0&&Enum.IsDefined(typeof(FullScreenMode),c.mode)&&Displays().Any(d=>d.name==c.displayName);
        private DisplayChoice SafeWindow(){var d=Screen.mainWindowDisplayInfo;var size=AutomaticWindow(d.workArea);return Choice(d,FullScreenMode.Windowed,size.x,size.y);}
        public bool Apply(DisplayChoice candidate)
        {
            if(IsPending||IsTransitioning||!Available(candidate))return false;
            var old=saved;
            saved=new Saved{confirmed=ReadActual(),candidate=candidate.Clone(),pending=true};
            try{Persist();}catch(Exception e) when(e is IOException||e is UnauthorizedAccessException){saved=old;Notice="显示设置未应用："+e.Message;Changed?.Invoke();return false;}
            deadline=double.PositiveInfinity;transitionDeadline=Time.realtimeSinceStartupAsDouble+10;restoring=false;CanConfirm=false;
            transition=StartCoroutine(ApplyNative(candidate,true));Changed?.Invoke();return true;
        }
        private IEnumerator ApplyNative(DisplayChoice c,bool confirmation)
        {
            // Always yield before finishing, so the caller can retain a valid coroutine handle.
            yield return null;
            var displays=Displays();int index=displays.FindIndex(d=>d.name==c.displayName);
            if(index<0){c=SafeWindow();index=displays.FindIndex(d=>d.name==c.displayName);}
            if(index>=0&&Screen.mainWindowDisplayInfo.name!=displays[index].name)
            {
                var d=displays[index];var move=Screen.MoveMainWindowTo(d,new Vector2Int(d.workArea.x,d.workArea.y));
                double until=Time.realtimeSinceStartupAsDouble+5;
                while(!move.isDone&&Time.realtimeSinceStartupAsDouble<until)yield return null;
            }
            if(c.mode==FullScreenMode.ExclusiveFullScreen)
            {
                var modes=Screen.resolutions.Where(r=>r.width==c.width&&r.height==c.height).ToArray();
                if(modes.Length==0||Screen.mainWindowDisplayInfo.name!=c.displayName)
                {
                    if(confirmation){transition=null;Notice="目标显示器不支持请求的独占分辨率，恢复原设置。";Revert();yield break;}
                    c=SafeWindow();
                }
                else {var d=Screen.mainWindowDisplayInfo;var r=modes.OrderBy(v=>Math.Abs(v.refreshRateRatio.value-d.refreshRate.value)).First();c=c.Clone();c.refreshNumerator=r.refreshRateRatio.numerator;c.refreshDenominator=r.refreshRateRatio.denominator;}
            }
            // Leaving fullscreen may consume a mode change before size is honored.
            if(c.mode==FullScreenMode.Windowed&&Screen.fullScreenMode!=c.mode){Screen.fullScreenMode=c.mode;yield return null;yield return null;}
            Screen.SetResolution(c.width,c.height,c.mode,new RefreshRate{numerator=c.refreshNumerator,denominator=Math.Max(1u,c.refreshDenominator)});
            yield return null;yield return null;
            var actual=ReadActual();
            bool displayMatches=actual.displayName==c.displayName;
            bool success=displayMatches&&actual.mode==c.mode&&actual.width>0&&actual.height>0;
            // Maximized windows can be constrained by the OS/notch/work area. Show actual size.
            if(c.mode==FullScreenMode.Windowed||c.mode==FullScreenMode.ExclusiveFullScreen)success&=actual.width==c.width&&actual.height==c.height;
            transition=null;
            if(confirmation&&!success){Notice="系统未能应用请求，已恢复原设置。";Revert();yield break;}
            if(confirmation){deadline=Time.realtimeSinceStartupAsDouble+15;CanConfirm=true;Notice="实际："+actual.width+" × "+actual.height+" · "+ModeName(actual.mode);}
            else {restoring=false;saved=new Saved{confirmed=actual};try{Persist();}catch(Exception e) when(e is IOException||e is UnauthorizedAccessException){Notice="显示已恢复，偏好写入失败："+e.Message;}}
            Changed?.Invoke();
        }
        public bool Confirm()
        {
            if(!IsPending||!CanConfirm||Time.realtimeSinceStartupAsDouble>=deadline)return false;
            var old=saved;saved=new Saved{confirmed=ReadActual()};
            try{Persist();}catch(Exception e) when(e is IOException||e is UnauthorizedAccessException){saved=old;Notice="确认保存失败："+e.Message;Changed?.Invoke();return false;}
            CanConfirm=false;Notice="已保存显示设置。";Changed?.Invoke();return true;
        }
        public void Revert()
        {
            if(!IsPending)return;
            if(transition!=null)StopCoroutine(transition);
            var c=Available(saved.confirmed)?saved.confirmed:SafeWindow();
            // Pending remains durable until the restoration has actually completed.
            restoring=true;CanConfirm=false;transition=StartCoroutine(ApplyNative(c,false));
        }
        private void Update()
        {
            if(IsPending&&Time.realtimeSinceStartupAsDouble>=deadline&&CanConfirm)Revert();
            if(IsPending&&!restoring&&transition!=null&&Time.realtimeSinceStartupAsDouble>=transitionDeadline){Notice="显示切换超时，恢复原设置。";Revert();}
            // An interrupted/missing monitor must never strand an awaiting confirmation.
            if(IsPending&&transition==null&&!Available(saved.candidate))Revert();
        }
        private void Persist()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".tmp";
            using(var f=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None))
            using(var writer=new StreamWriter(f)){writer.Write(JsonUtility.ToJson(saved,true));writer.Flush();f.Flush(true);}
            if(File.Exists(path))File.Replace(temp,path,path+".backup");else File.Move(temp,path);
        }
    }
}
