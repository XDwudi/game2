using System;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using DiceCraft.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceCraft
{
    // V1.1.1 task W7b (RULES_DELTA 12.1 and 13 row 4): the run's own records on the settlement and result pages, and
    // the formal all-time highest hand on the main menu. The run's values are the session's; the all-time value lives
    // in FormalRecordStore, outside the run save, and only formal runs feed it.
    public sealed partial class GameApp
    {
        private FormalRecordStore formalRecords111;
        /// <summary>The formal record file beside the run save (the same folder the save and the display settings use).</summary>
        private FormalRecordStore FormalRecords111
        {
            get
            {
                string path=Path.Combine(SaveDirectoryOverride??Application.persistentDataPath,FormalRecordStore.FileName);
                if(formalRecords111==null||formalRecords111.RecordPath!=path)
                {
                    formalRecords111=new FormalRecordStore(path);
                    if(formalRecords111.LastLoadIssue!=null)Debug.LogWarning("历史记录文件读取有问题，已按可用的部分继续："+formalRecords111.LastLoadIssue);
                }
                return formalRecords111;
            }
        }
        /// <summary>The all-time highest single hand of formal runs, as an exact decimal string ("0" before the first).</summary>
        public string HistoricalHighestHandScore111=>FormalRecords111.HistoricalHighestHandScore.ToString();
        public string FormalRecordLocation111=>FormalRecords111.RecordPath;
        /// <summary>
        /// Takes a committed state's highest hand into the formal record (idempotent: the maximum). Called after every
        /// accepted command and after a save is restored. Nothing is noted while the run save is still pending, and a
        /// debug workspace, a debug run and practice never count.
        /// </summary>
        private void NoteFormalRecord111(RunState state)
        {
            if(state==null||Session==null||Session.HasPendingSave)return;
            var store=FormalRecords111;
            store.NoteRun(state,tutorial);
            if(store.LastWriteIssue!=null)Debug.LogWarning("历史记录没能写入，下次操作时重试："+store.LastWriteIssue);
        }

        // ---------------------------------------------------------------- texts
        private string RunHighestHand111=>CompactScore(ViewState?.highestHandScore??"0");
        private string RunClearRatio111=>RunRecords111.ClearRatio(ViewState)??"—";
        private const string RecordMark111="本关新纪录";

        // ---------------------------------------------------------------- main menu
        /// <summary>"历史最高单手 12,345" on the menu panel, under its buttons.</summary>
        private void DrawMenuRecord111(RectTransform panel,float y)
        {
            var best=FormalRecords111.HistoricalHighestHandScore;
            string text="历史最高单手 "+(best.IsZero?"暂无记录":CompactScore(best.ToString()));
            var label=FitLine13(panel,"MenuRecord111",text,48,y,520,22,LabelOn11(panel,PaperMuted071),TextAlignmentOptions.Center,false);
            label.raycastTarget=false;
        }

        // ---------------------------------------------------------------- stage settlement
        /// <summary>Two lines beside 明细 on the settlement sheet: the run's highest hand and highest clear ratio, each
        /// marked when this stage raised it.</summary>
        private void DrawSettlementRecords111(RectTransform sheet,bool large,float leftW,Color ink,Color muted)
        {
            float buttonW=large?260:220,x=32+buttonW+24,w=leftW-buttonW-24,size=20,line=Line11(Body11(size));
            float y=(large?676:538)+((large?64:60)-2*line)*.5f;
            bool hand=RunRecords111.HandRecordBroken(ViewState),ratio=RunRecords111.ClearRatioBroken(ViewState);
            var first=FitLine13(sheet,"SettleRecordHand111","本局最高单手 "+RunHighestHand111+(hand?" · "+RecordMark111:""),x,y,w,size,hand?ink:muted,TextAlignmentOptions.MidlineRight,hand);
            var second=FitLine13(sheet,"SettleRecordRatio111","最高过关倍数 "+RunClearRatio111+(ratio?" · "+RecordMark111:""),x,y+line,w,size,ratio?ink:muted,TextAlignmentOptions.MidlineRight,ratio);
            first.raycastTarget=false;second.raycastTarget=false;
        }
    }
}
