using System;
using System.IO;
using DiceCraft.Core;
using TMPro;
using UnityEngine;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        private Art07StartupLoad startupLoad07;
        private TextMeshProUGUI startupHint07;
        private string startupSavePath07;
        public bool StartupReady07 { get; private set; }
        public bool StartupLoadingHintEverShown07 { get; private set; }
        public double StartupHintFirstMilliseconds07 { get; private set; }
        public double StartupLoadElapsedMilliseconds07=>startupLoad07?.ElapsedMilliseconds??0;
        public int StartupLoadWorkerThreadId07 { get; private set; }
#if UNITY_EDITOR
        // Editor-only verification barrier. Never installed by product UI or a built Player.
        public static Action<string> StartupReadBarrierForTests07;
#endif
        private void BeginStartupLoad07()
        {
            startupSavePath07=Path.Combine(SaveDirectoryOverride??Application.persistentDataPath,"active-run.json");
            Action<string> barrier=null;
#if UNITY_EDITOR
            barrier=StartupReadBarrierForTests07;
            if(barrier!=null)
            {
                string isolated=string.IsNullOrEmpty(SaveDirectoryOverride)?"":Path.GetFullPath(SaveDirectoryOverride);
                string temporary=Path.GetFullPath(Application.temporaryCachePath).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;
                if(!Path.IsPathRooted(isolated)||!isolated.StartsWith(temporary,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Startup verification barrier requires isolated temporary saves.");
            }
#endif
            startupLoad07=Art07StartupLoad.Begin(startupSavePath07,JsonUtility.ToJson(Config),barrier);
            Refresh();TickStartupLoad07();
        }
        private void DrawStartupLoad07()
        {
            // Keep the real felt visible. No modal curtain, fake percentage or enabled rule action.
            UiKit.Clear(main);table.Hide();
            UiKit.Text(main,"StartupOverline07","DICE  /  BUILD  /  DECIDE",120,102,880,50,24,UiKit.Gold);
            UiKit.Text(main,"StartupTitle07","骰子构筑",112,170,1120,224,96,UiKit.Paper);
            startupHint07=UiKit.Text(main,"StartupReadHint07","正在读取存档…",1180,900,620,64,22,UiKit.Muted,TextAlignmentOptions.MidlineRight);
            startupHint07.gameObject.SetActive(StartupLoadingHintEverShown07);
        }
        private void TickStartupLoad07()
        {
            if(StartupReady07||startupLoad07==null||startupLoad07.IsAbandoned)return;
            if(startupLoad07.IsCompleted)
            {
                var loaded=startupLoad07.Completion.GetAwaiter().GetResult();
                StartupLoadWorkerThreadId07=loaded.WorkerThreadId;
                saves=loaded.Store??new AtomicSaveStore(startupSavePath07,Config,SaveSlotPolicy.Normal);
                string issue=null;bool backup=false;
                try
                {
                    if(!string.IsNullOrEmpty(loaded.Error))throw new InvalidDataException(loaded.Error);
                    Session=new GameSession(Config,saves,loaded.State);notice=saves.LastLoadNotice??"";
                    if(!string.IsNullOrEmpty(notice)){issue=notice;backup=loaded.State!=null;}
                }
                catch(Exception error){Session=new GameSession(Config,saves);notice="恢复失败，原存档保留："+error.Message;issue=notice;}
                RestoreResultView07();ApplyArtPreferences07();InitializeGuide07();
                StartupReady07=true;Refresh();
                if(issue!=null)ShowLoadIssue07(issue,backup);
                return;
            }
            if(startupLoad07.ElapsedMilliseconds>200&&!startupLoad07.IsCompleted)
            {
                if(!StartupLoadingHintEverShown07)StartupHintFirstMilliseconds07=startupLoad07.ElapsedMilliseconds;
                StartupLoadingHintEverShown07=true;
                if(startupHint07!=null)startupHint07.gameObject.SetActive(true);
            }
        }
        private void AbandonStartupLoad07()=>startupLoad07?.Abandon();
    }
}
