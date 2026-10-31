using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        public static bool SuppressStartupDisplay;
        public DisplaySettings DisplayPreferences { get; private set; }
        private DisplayChoice displayDraft;
        private TextMeshProUGUI displayCountdown;
        private bool displayPanel;
        private int observedWidth,observedHeight;
        private Rect observedSafeArea;

        private void InitializeDisplay()
        {
            DisplayPreferences=gameObject.AddComponent<DisplaySettings>();
            DisplayPreferences.Initialize(Path.Combine(SaveDirectoryOverride??Application.persistentDataPath,"display-settings.json"),!SuppressStartupDisplay&&!Application.isEditor);
            DisplayPreferences.Changed+=()=>{if(displayPanel)ShowDisplaySettings();};
            // Keep the full 1920x1080 design inside the actual safe rectangle. The dice camera
            // and pointer coordinates use this same transform, including ultrawide/Retina.
            canvas.GetComponent<UnityEngine.UI.CanvasScaler>().uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize;
            UpdateViewport();
        }
        private void UpdateViewport()
        {
            if(main==null)return;
            Rect safe=Screen.safeArea;
            if(safe.width<=0||safe.height<=0)safe=new Rect(0,0,Screen.width,Screen.height);
            float scale=Math.Min(safe.width/1920f,safe.height/1080f);
            canvas.GetComponent<UnityEngine.UI.CanvasScaler>().scaleFactor=Math.Max(.1f,scale);
            Canvas.ForceUpdateCanvases();
            var root=(RectTransform)canvas.transform;
            Vector2 offset=new Vector2((safe.x+(safe.width-1920*scale)/2)/scale,(Screen.height-safe.yMax+(safe.height-1080*scale)/2)/scale);
            main.anchoredPosition=overlay.anchoredPosition=new Vector2(offset.x,-offset.y);
            table.designRoot=main;
            observedWidth=Screen.width;observedHeight=Screen.height;observedSafeArea=safe;
        }
        private Vector2 ScreenToDesign(Vector2 position)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(main,position,null,out var point);
            return new Vector2(point.x,-point.y);
        }
        private void TickDisplay()
        {
            if(observedWidth!=Screen.width||observedHeight!=Screen.height||observedSafeArea!=Screen.safeArea)
            {CancelControllerDrag();UpdateViewport();Refresh();if(controllerOrderPanel)ShowControllerOrder();else if(debugPanel)ShowDebugPanel();else if(displayPanel)ShowDisplaySettings();}
            if(displayCountdown!=null&&displayPanel&&DisplayPreferences.IsPending)
                displayCountdown.text=DisplayPreferences.Notice+"\n"+DisplayPreferences.SecondsLeft+" 秒内确认，否则恢复原设置。";
        }
        public void ShowDisplaySettings()
        {
            displayPanel=true;
            var p=OpenModal("显示设置");
            if(DisplayPreferences.IsPending)
            {
                displayCountdown=UiKit.Text(p,"DisplayCountdown",DisplayPreferences.Notice,40,135,1500,155,32,UiKit.Gold);
                UiKit.Button(p,"ConfirmDisplay","保留此显示设置",40,410,725,80,()=>{if(DisplayPreferences.Confirm()){displayDraft=null;ShowDisplaySettings();}},DisplayPreferences.CanConfirm,true);
                UiKit.Button(p,"RevertDisplay","恢复原设置 [Esc]",815,410,725,80,DisplayPreferences.Revert);
                return;
            }
            displayDraft=displayDraft??DisplayPreferences.ReadActual();
            var displays=DisplayPreferences.Displays();
            int index=displays.FindIndex(d=>d.name==displayDraft.displayName);
            if(index<0){displayDraft=DisplayPreferences.ReadActual();index=Math.Max(0,displays.FindIndex(d=>d.name==displayDraft.displayName));}
            var selected=displays[index];
            UiKit.Text(p,"DisplayActual","实际 "+Screen.width+" × "+Screen.height+" · "+DisplaySettings.ModeName(Screen.fullScreenMode)+"\n"+DisplayPreferences.Notice,40,96,1500,85,25,UiKit.Muted);
            UiKit.Button(p,"Monitor","显示器："+selected.name+" · "+selected.width+" × "+selected.height+"  >",40,196,1500,60,()=>{var d=displays[(index+1)%displays.Count];displayDraft=DisplayPreferences.Choice(d,displayDraft.mode,d.width,d.height);ShowDisplaySettings();},displays.Count>1);
            var modes=new[]{DisplaySettings.PlatformFullscreen,FullScreenMode.FullScreenWindow,FullScreenMode.Windowed};
            for(int i=0;i<3;i++){var mode=modes[i];UiKit.Button(p,"Mode"+i,DisplaySettings.ModeName(mode),40+i*510,280,480,60,()=>{var sizes=DisplayPreferences.Resolutions(selected,mode);var size=mode==FullScreenMode.Windowed?DisplaySettings.AutomaticWindow(selected.workArea):new Vector2Int(selected.width,selected.height);if(sizes.Length>0&&!sizes.Contains(size))size=sizes.Last();displayDraft=DisplayPreferences.Choice(selected,mode,size.x,size.y);ShowDisplaySettings();},true,displayDraft.mode==mode);}
            var resolutions=DisplayPreferences.Resolutions(selected,displayDraft.mode);
            var list=UiKit.Scroll(p,"ResolutionList",40,365,1500,190,Math.Max(180,((resolutions.Length+3)/4)*65));
            for(int i=0;i<resolutions.Length;i++){var size=resolutions[i];UiKit.Button(list,"Resolution"+i,size.x+" × "+size.y,(i%4)*370,(i/4)*65,350,52,()=>{displayDraft=DisplayPreferences.Choice(selected,displayDraft.mode,size.x,size.y);ShowDisplaySettings();},true,displayDraft.width==size.x&&displayDraft.height==size.y);}
            UiKit.Button(p,"AutomaticResolution","自动适配当前屏幕",40,580,725,64,()=>{var d=Screen.mainWindowDisplayInfo;var size=displayDraft.mode==FullScreenMode.Windowed?DisplaySettings.AutomaticWindow(d.workArea):new Vector2Int(d.width,d.height);displayDraft=DisplayPreferences.Choice(d,displayDraft.mode,size.x,size.y);ShowDisplaySettings();});
            UiKit.Button(p,"ApplyDisplay","应用 · 15秒确认",815,580,725,64,()=>{DisplayPreferences.Apply(displayDraft);ShowDisplaySettings();},!DisplayPreferences.IsTransitioning,true);
            UiKit.Text(p,"DisplayHelp","显示偏好独立保存。窗口模式可拖动边缘；全屏在 Windows 使用独占，在 macOS 使用系统全屏。",40,682,1500,64,23,UiKit.Muted);
        }
    }
}
